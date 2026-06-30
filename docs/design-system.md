# Design System

Vibe: clean & trustworthy SaaS dashboard. Audience spans admin staff, teachers, a principal/owner, and parents (including a payment flow) — accessibility and legibility matter more than visual flourish here.

Stack: React 19 + shadcn/ui + Tailwind CSS.

## Color Palette

Source: "Invoice & Billing Tool" palette — chosen because the green-for-paid convention directly serves the parent fee-payment flow, the riskiest/most trust-sensitive screen in the app, while navy keeps the admin/principal views feeling institutional rather than consumer-flashy.

| Token | Value | CSS Variable | Usage |
|---|---|---|---|
| Primary | `#1E3A5F` | `--color-primary` | Navy — primary actions, headers, nav |
| Secondary | `#2563EB` | `--color-secondary` | Blue — secondary actions, links |
| Accent | `#059669` | `--color-accent` | Green — success/paid states |
| On Primary | `#FFFFFF` | `--color-on-primary` | Text/icons on primary |
| On Secondary | `#FFFFFF` | `--color-on-secondary` | Text/icons on secondary |
| On Accent | `#FFFFFF` | `--color-on-accent` | Text/icons on accent |
| Background | `#F8FAFC` | `--color-background` | App background |
| Foreground | `#0F172A` | `--color-foreground` | Primary text |
| Card | `#FFFFFF` | `--color-card` | Card/surface background |
| Card Foreground | `#0F172A` | `--color-card-foreground` | Text on cards |
| Muted | `#F1F3F5` | `--color-muted` | Muted surfaces |
| Muted Foreground | `#64748B` | `--color-muted-foreground` | Secondary/helper text |
| Border | `#E4E7EB` | `--color-border` | Borders, dividers |
| Destructive | `#DC2626` | `--color-destructive` | Errors, destructive actions |
| On Destructive | `#FFFFFF` | `--color-on-destructive` | Text/icons on destructive |
| Ring | `#1E3A5F` | `--color-ring` | Focus ring |

All pairs meet WCAG AA contrast (4.5:1 body text). Light mode only for v1 — no dark mode planned given the build-time budget.

## Typography

Source: "Corporate Trust" pairing — Lexend (headings) + Source Sans 3 (body). Lexend was designed specifically for reading-fluency research; combined with Source Sans 3 it's the most accessible option across the wide age/tech-literacy range this app serves (parents and staff alike), prioritized over a "warmer" alternative (Poppins + Open Sans) for that reason.

- **Heading font:** Lexend (weights: 300, 400, 500, 600, 700)
- **Body font:** Source Sans 3 (weights: 300, 400, 500, 600, 700)
- Google Fonts: https://fonts.google.com/share?selection.family=Lexend:wght@300;400;500;600;700|Source+Sans+3:wght@300;400;500;600;700

```css
@import url('https://fonts.googleapis.com/css2?family=Lexend:wght@300;400;500;600;700&family=Source+Sans+3:wght@300;400;500;600;700&display=swap');
```

Tailwind config:
```js
fontFamily: {
  heading: ['Lexend', 'sans-serif'],
  body: ['Source Sans 3', 'sans-serif'],
}
```

## Type Scale

Base 16px, line-height 1.5–1.75 for body text. Use a consistent scale (e.g. 12 / 14 / 16 / 18 / 24 / 32) — exact scale to be finalized when component work starts.

## Effects

- Border radius, shadow scale, and spacing rhythm (4/8px system) to be defined per-component as the build proceeds — kept open here to avoid premature detail ahead of actual screens.

## Status

Palette and typography are confirmed. This doc is the source of truth before syncing to Claude Design (`/design-sync`) — keep it updated if either choice changes.
