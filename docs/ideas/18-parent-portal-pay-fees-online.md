# Parent Portal — Pay Fees Online

## Problem Statement
**How Might We** let a logged-in parent settle one issued-invoice installment through a real
sandbox gateway (Stripe test mode) — so the installment moves Pending → Paid via an
idempotent, auditable, webhook-confirmed path, the balance hero updates honestly, and the
admin's "revenue vs. outstanding" dashboard reflects it live — without inventing partial
payments, refunds, or a second source of truth for money?

## Context
This is the **fifth parent-facing surface** and the **first parent *write* path** — the first
place the parent portal mutates money. It closes the loop the founding doc
([docs/ideas/school-management-system.md](school-management-system.md)) built the whole pitch on:
*a teacher marks attendance → a parent sees it and pays a fee online → an admin sees both in a
live dashboard.* It is also the founding doc's explicitly flagged **highest technical-risk item**
("spike it early, not last" — now landing last, which is acceptable for a demo because the risk is
concentrated in the confirmation path, not the UI).

It builds directly on:
- **Fee invoicing** (spec 12 / [docs/ideas/08-fee-invoicing.md](08-fee-invoicing.md)):
  `FeeInvoiceInstallment` already carries `Status` (Pending/Paid/Overdue), `AmountPaid`, `PaidAt`
  — **payment-ready but always `Pending`/`null` today**. This module is what finally writes them.
- **Parent portal — view fees** (spec 20 / [docs/ideas/17-parent-portal-fees.md](17-parent-portal-fees.md)):
  the parent shell, `ResolveLinkedChildOrThrow` guard, `ChildFeesPage` balance hero + installment
  schedule, and the `FeeInvoiceService` `TotalPaid` rollup all already exist. Spec 20 deliberately
  left `Paid = 0.00` and promised "no rework when payments land." This module makes that true.
- **Auth** (spec 02): JWT-in-`SameSite=Lax`-cookie. This directly shapes the webhook design (below).

**What makes this NOT another read surface:** every prior parent slice was a GET reusing an existing
read. This one introduces an inbound *server-to-server* callback outside the auth pipeline, an
idempotent state mutation on money, and a new entity — the first parent-facing write.

## Recommended Direction
The risk is entirely in the **confirmation path**, not the UI. The spine has three moves and one
invariant (a single idempotent reconcile is the *only* writer of payment truth):

1. **Initiate (parent, authenticated):**
   `POST /api/parent/children/{childId}/installments/{installmentId}/pay` — reuses
   `ResolveLinkedChildOrThrow`, **re-derives the amount server-side** (never trust the client),
   rejects an installment that is already Paid or belongs to a non-Issued/Cancelled invoice, creates
   a `Payment` row (status `Pending`) and a Stripe `PaymentIntent` stamping `SchoolId` + `PaymentId`
   + `InstallmentId` into intent **metadata**. Returns the client secret.
2. **Pay (browser):** Stripe **Elements inline** card form (test card `4242…`) in a modal — no
   redirect, so the entire parent flow stays under the existing cookie+JWT auth (no top-level
   redirect-return cookie dance).
3. **Confirm — two callers, one function:** a single **idempotent `ReconcilePayment(intentId)`** is
   the *only* code that flips the installment → Paid and the `Payment` → Succeeded. It is called by:
   - **the Stripe webhook** (`POST /api/webhooks/stripe`, `[AllowAnonymous]`, **signature-verified**,
     raw-body, tenant resolved *from verified intent metadata*) — **authoritative**; and
   - **the return path** (authenticated XHR after `confirmCardPayment` resolves) — for instant UI.
   Reconcile is a **no-op if the `Payment` is already Succeeded**, so double webhooks, double-clicks,
   and the webhook/return race are all safe by construction.

### Why the webhook lives outside the auth mechanism (by design)
The webhook is a server-to-server POST from Stripe: it carries **no cookie and no JWT**. Therefore:
- It **must be `[AllowAnonymous]`** and excluded from the JWT/tenant middleware; it authenticates by
  **Stripe signature verification** (`Stripe-Signature` header + webhook signing secret), which is a
  shared-secret HMAC — strictly stronger than a cookie for a server callback.
- The `SameSite=Lax` CSRF rule is **satisfied, not violated**: Lax exists to stop a *browser*
  silently attaching the cookie to a forged cross-site request; Stripe is not a browser and there is
  no cookie to attach. The mutation is correctly on a POST (the "GET must be side-effect-free" rule
  holds).
- It needs the **raw request body** for signature verification, so it cannot use the normal JSON
  model-binding that consumes the stream.
- **Tenant wrinkle:** `ITenantProvider` is claims-based, but the webhook has no claims. It resolves
  the `Payment` by the globally-unique Stripe intent id and sets `SchoolId` from the verified intent
  metadata — the **one sanctioned bypass** of the EF Core global query filter, which must be an
  explicit, commented seam so it doesn't read as a bug.

The parent-facing initiate + reconcile-on-return calls are same-origin XHR carrying cookie+JWT
normally — **no auth changes** there.

### Why the `Payment` entity
One `Payment` row per intent (amount snapshot, gateway ref, status, timestamps) linked to the
installment **is** the collections **audit trail** named in the founding scope. The existing
`TotalPaid` rollup in `FeeInvoiceService` and the admin dashboard already read `AmountPaid`, so once
reconcile writes `AmountPaid`/`PaidAt`/`Status=Paid`, balance + dashboard update with **zero
rework** — exactly what spec 20 set up.

## Key Assumptions to Validate
- [ ] **The publicly-reachable deploy receives Stripe webhooks** — confirm a real test-mode event
      hits the deployed endpoint before relying on webhook-authoritative. (Fallback: return-path
      primary, webhook best-effort.)
- [ ] **Reconcile is fully idempotent** — prove with a test that fires the same webhook twice and a
      double-click; installment ends Paid exactly once, one Succeeded `Payment`.
- [ ] **The tenant-from-metadata bypass is sound and singular** — the webhook resolves `Payment` by
      globally-unique Stripe intent id and sets `SchoolId` from verified metadata; confirm it is the
      only place bypassing the global query filter, explicitly commented.
- [ ] **The server re-derives the payable amount** from the installment — the client sends only ids,
      never an amount; confirm rejection paths for already-Paid and non-Issued/Cancelled invoices.
- [ ] **`InstallmentStatus` stays Pending/Paid; Overdue remains on-the-fly** — no persisted Overdue,
      no cron (doc 08 deferral holds); the `Overdue` enum member stays vestigial.
- [ ] **Money is `decimal` server-side; Stripe amount is integer minor units** — the PHP
      peso→centavo conversion is centralized and lossless.

## MVP Scope
**In:**
- **Domain:** new `Payment` entity (`ITenantScoped`): `InstallmentId`, `Amount`, `Currency`,
  `Status` (Pending/Succeeded/Failed), `StripePaymentIntentId` (unique), `PaidAt`, timestamps. One
  migration.
- **Backend:** `PaymentService` (initiate + idempotent `ReconcilePayment`); direct `Stripe.net`
  integration behind a **thin internal seam** (kept small — not a full multi-provider abstraction);
  parent initiate endpoint on the existing `api/parent` controller; `POST /api/webhooks/stripe`
  anonymous signature-verified handler; reconcile writes `AmountPaid`/`PaidAt`/`Status=Paid` on the
  installment. Config via `IOptions<StripeOptions>` (secret key + webhook signing secret).
- **Frontend:** a "Pay" action per Pending installment on `ChildFeesPage`; a Stripe Elements modal
  (`@stripe/react-stripe-js`); refetch of the fee overview on success. The balance hero + status
  badges already exist from spec 20 — Paid rows now actually appear.
- **States:** success, card decline / failed intent, already-paid (idempotent), Cancelled-invoice
  guard, loading/error — mirroring specs 18–20.

**Out:**
- **Partial payments** — one installment, full amount (doc 08 deferral).
- **Refunds** — instead, **block invoice Cancel once any Payment is Succeeded** (coherent state
  without building refund flows).
- **Saved cards / wallets / GCash / multi-currency** — test-mode card only; PHP only.
- **Receipt PDF / email** — the `Payment` row is the record; rendering a receipt is a later slice.
- **Admin manual/cash payment recording** — parent-initiated online only for this slice.
- **Persisted overdue / reminders / cron** — unchanged from spec 20.

## Not Doing (and Why)
- **Marking installments Paid from the client or the return path alone** — one idempotent reconcile,
  webhook-authoritative; the browser never asserts payment truth.
- **Trusting a client-supplied amount** — the server re-derives from the installment; the request
  carries ids only. Classic tamper vector, closed by construction.
- **A full `IPaymentGateway` multi-provider abstraction** — Stripe is the chosen gateway; a thin seam
  is enough. No speculative PayMongo indirection.
- **Cookie/JWT auth on the webhook** — impossible (Stripe sends no cookie) and unnecessary; signature
  verification is the correct, stronger auth for a server-to-server callback.
- **Redirect-based Stripe Checkout** — Elements inline keeps the flow same-site under existing auth
  and avoids the redirect-return cookie dance.

## Open Questions
- **Concurrency between two linked guardians** paying the same installment — is a DB uniqueness/row
  guard in reconcile sufficient (assumed yes), or do we also prevent a second *intent* while one
  `Payment` is Pending?
- **Failed/abandoned intents** — keep `Payment(Failed)` rows for the audit trail (recommended) or
  prune them? Leaning keep.
- **Currency conversion home** — a new shared `Money`/minor-units helper, or inline in
  `PaymentService`?
- **Does the demo script need a visible "admin sees revenue tick up"** beat right after payment,
  which would raise the priority of the dashboard live-refresh?

## Resolved Decisions
- **Pay granularity:** **one installment at a time**, full amount — maps 1:1 to the installment
  status model; no partial payments.
- **Gateway:** **Stripe test mode**, integrated directly (thin seam, no multi-provider abstraction).
- **Confirmation:** **webhook-authoritative + reconcile-on-return**, both routed through one
  idempotent `ReconcilePayment`.
- **Record:** **dedicated `Payment` entity** (one row per intent) — the collections audit trail.
- **Webhook delivery:** **publicly-reachable deploy** receives Stripe events directly (no CLI
  dependency).
- **Card capture:** **Stripe Elements inline** (no redirect), chosen to fit the cookie auth model.

## Resolved (inherited from specs 18–20, restated for this slice)
- **Unlinked/unknown child:** `ResolveLinkedChildOrThrow` → **404**, never leak existence.
- **Withdrawn/transferred children:** payable regardless of `EnrollmentStatus` — the `StudentParent`
  link is the authorization truth.
- **All parent mutations are POST** (SameSite=Lax CSRF rule); parent identity always from the JWT
  `sub` claim, never the request. The webhook is the sole anonymous, signature-verified exception.
