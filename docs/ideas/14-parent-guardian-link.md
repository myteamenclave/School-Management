# Parent–Guardian Link

## Problem Statement

How might we let a parent log into the system and access their child's data, without introducing a separate registration flow or duplicating the guardian contact info already captured on the student record?

## Recommended Direction

Convert the existing `GuardianEmail` on a student record into a Parent user account — the Admin triggers this from the student detail screen ("Create parent login"). This avoids a separate sign-up flow and keeps the guardian email as the single source of truth.

The link between a Parent user and their child(ren) is a `StudentParent` junction table: `UserId (Parent) → StudentId`, many-to-many (one parent can have multiple children enrolled at the school; one student can have multiple parent accounts).

Account creation flow:
1. Admin opens a student's detail page and clicks "Create parent login"
2. System checks whether a Parent-role user with that `GuardianEmail` already exists for the school (covers the case where the same parent has multiple children)
3. If not: creates a new `User` (role = `Parent`) with the `GuardianEmail` and an Admin-set temporary password
4. If already exists: re-uses the existing parent account
5. Either way: creates the `StudentParent` link if it doesn't already exist
6. Admin is shown the parent's login credentials (email + the temporary password they just set) to communicate manually — no email/SMTP in v1

Parent accounts are managed by the Admin only — no self-registration, no self-service password reset in v1.

## Key Assumptions to Validate
- [ ] A student can have more than one parent account (e.g. mother and father both want access) — assumed yes; junction table supports it
- [ ] A Parent user account is always scoped to one school (`SchoolId`) — same as every other user
- [ ] The temporary password is set by the Admin at creation time (not auto-generated) — Admin tells the parent verbally or via their own channel
- [ ] No email invite or SMTP in v1 — manual credential handoff only
- [ ] If `GuardianEmail` is blank on the student record, the "Create parent login" action is unavailable until the Admin fills it in

## MVP Scope

**Backend**
- `StudentParent` junction entity: `UserId` (FK → Users) + `StudentId` (FK → Students), tenant-scoped
- `POST /api/students/{id}/parent-login` — Admin only; body: `{ temporaryPassword }`. Creates or reuses the Parent user, creates the junction link, returns the parent's email + confirmation. Side-effect-free GET rule: this is a POST because it mutates state.
- `GET /api/students/{id}/parents` — Admin only; lists parent accounts linked to this student (email, display name, account created date)
- `DELETE /api/students/{id}/parents/{parentUserId}` — Admin only; removes the link (does not delete the parent User account — they may still be linked to other children)

**Frontend**
- Student detail page: "Parent Accounts" section showing linked parents + "Create parent login" button
- Create parent login modal: shows pre-filled `GuardianEmail`, password field for Admin to set, submit creates the account/link and displays credentials for manual handoff

## Not Doing (and Why)
- Self-registration / email invite — SMTP dependency adds complexity for minimal demo value in v1
- Self-service password reset — same reason; Admin resets manually if needed
- Separate Guardians table — inline `GuardianEmail` on Student is the source of truth; no need to duplicate it
- Deactivating a parent account — out of scope for v1; Admin can remove the link instead

## Open Questions
*(resolved)*
- Account creation source: converts existing `GuardianEmail` on the student record — not a separate input
- Credential handoff: Admin sets the temporary password and communicates it manually — no email/SMTP in v1
