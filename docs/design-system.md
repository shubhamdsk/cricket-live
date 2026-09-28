# Cricket Live — Design System

The visual and interaction language. It should feel fast, clear, and current — a scoreboard you can
read at a glance on a phone, not a dashboard you have to study.

Tokens are defined once in `frontend/src/index.css` under Tailwind v4's `@theme`, so every utility
resolves to the same values. Components never hardcode a colour, radius, or shadow.

---

## Tokens

### Colour

| Token | Value | Used for |
| --- | --- | --- |
| `pitch` | `#f8fafc` | app background |
| `surface` | `#ffffff` | cards, header, footer |
| `surface-muted` | `#f1f5f9` | hover and inset areas |
| `line` | `#e2e8f0` | card edges and dividers |
| `line-strong` | `#94a3b8` | the border of a control you have to find |
| `ink` | `#0f172a` | primary text, scores |
| `ink-muted` | `#475569` | secondary text |
| `ink-subtle` | `#64748b` | meta, venue, timestamps |
| `brand` | `#059669` | primary action fills |
| `brand-strong` | `#047857` | links and brand text on light |
| `brand-soft` | `#ecfdf5` | active navigation, card hover |
| `brand-line` | `#a7f3d0` | the edge of a highlighted card |
| `live` | `#dc2626` | live status |
| `live-soft` | `#fef2f2` | live badge background |
| `live-line` | `#fecaca` | live badge border |
| `upcoming` | `#0369a1` | scheduled status |
| `upcoming-soft` | `#f0f9ff` | scheduled badge background |
| `danger` | `#b91c1c` | error text |
| `danger-soft` | `#fef2f2` | error surfaces |

Status is never communicated by colour alone — it is always paired with an icon or text.

**Red does double duty, so shape has to separate the two uses.** Live is a bright `live` with a
pulsing dot and the word "Live", always inside a small pill attached to a match. An error is the
darker `danger` with a warning icon and a heading, always filling a region where content should
have been. A person should never have to work out whether a red thing is a wicket or a failure.

Every colour that carries text clears 4.5:1 on `pitch`, which is why `brand` fills a button but
`brand-strong` writes on one. Colour that only has to be seen — a badge border, a live dot — clears
3:1.

### Type, radius, shadow, motion

- **Type:** one sans stack. Size and weight carry hierarchy; headings are semibold, body regular,
  meta small and muted.
- **Scores use tabular figures** (`.score-figures`), so a total does not shift sideways when it
  ticks from 99 to 100 or an over rolls from 19.5 to 20.0. This matters more here than anywhere
  else: these numbers change while someone is looking at them.
- **Radius:** `card` (0.75rem) for cards, inputs, and badges' containers; `panel` (1rem) for the
  featured match; full rounding for pills and buttons.
- **Shadow:** `card` for resting surfaces. No heavy or coloured shadows.
- **Motion:** roughly 200ms, and it communicates state — a live pulse, a skeleton, a hover. It never
  decorates, and all motion respects `prefers-reduced-motion`.

### Spacing

Tailwind's default scale. Within a card, prefer 3/4; between page sections, 6/8. Consistency matters
more than precision.

### Control sizes

Mobile-first, so the primary scale is a comfortable thumb target rather than a tight desktop one.

| Control | Height | Type |
| --- | --- | --- |
| `Button` `md`, nav item | 44px on mobile, 40px from `sm` | `text-sm` |
| `Button` `sm`, actions inside a row | 36px | `text-sm` |
| Tab | 40px | `text-xs`, `text-sm` from `sm` |

Cricket is watched one-handed on a phone, often while doing something else. The larger target on
mobile is the point, not an oversight; it tightens on pointer-sized screens where 40px is plenty.

### Breakpoints

Tailwind's defaults, used mobile-first: default is phone, `sm` large phone, `md` tablet, `lg`
laptop, `xl` desktop, `2xl` large desktop. A component is written for the phone first and widened.

---

## Components

### Primitives — `components/common/`

| Component | Notes |
| --- | --- |
| `Button` | `primary`, `secondary`, `ghost`; `sm`/`md` |
| `Card` | surface container with the standard border, radius, and shadow |
| `Badge` | `neutral`, `live`, `upcoming`, `completed`; the live tone brings its own pulsing dot |
| `Tabs` | real `tablist`: arrow keys and Home/End move, panels bound by `aria-controls` |
| `Skeleton` | a shape that matches the content that is coming |
| `Spinner` | inline, with an accessible label |
| `EmptyState` | icon, title, description |
| `ErrorState` | title, description, optional retry |

Primitives take props and never import from `features/` or `store/`.

### Cricket components — `components/match/`

| Component | Notes |
| --- | --- |
| `MatchCard` | the unit of the product: teams, scores, status line, whole card is one link |
| `FeaturedMatch` | the same information given room, for the top of the home page |
| `MatchHeader` | match identity and score at the top of the details page |
| `TeamScoreRow` | one side and its innings; short name on mobile, full name from `sm` |
| `MatchStatusBadge` | status as a badge, wired to the shared tones |

These are presentational. They take a match and render it; they do not fetch.

### Icons

Icons come from `lucide-react`, sized with `size-*`, inheriting colour through `currentColor`, and
`aria-hidden` when the surrounding control already carries the name. Raw `<svg>` is never pasted
into a component, and emoji are not interface icons.

---

## Score presentation

The one thing this product must get right.

- A side reads `runs/wickets (overs)` — `142/3 (15.2)`. Ten wickets down drops the wickets entirely:
  `341`, not `341/10`, because that is how cricket writes it.
- A Test side's two innings join with `&`: `341 & 88/2`.
- A side that has not batted says "Yet to bat" rather than showing `0/0`.
- The status line is the provider's sentence — "India need 37 runs in 28 balls", "England lead by
  162 runs", "India won by 5 wickets". It is the most useful line on the card and is never
  paraphrased by us.
- Team names shorten to their three-letter code on mobile and expand from `sm`. A long name truncates
  rather than pushing the score off the card.

---

## States

Every surface that can be slow, empty, or fail has a deliberate state — never a blank area or a
silent failure.

| State | Treatment |
| --- | --- |
| Loading | a skeleton shaped like the content that is coming, so nothing moves when data lands |
| Empty | `EmptyState` explaining what will appear and when |
| Error | `ErrorState` with a human message and a retry where retrying can help |
| Unsupported | hidden entirely — a tab the provider cannot fill is not shown as an empty tab |

That last row is specific to this product. We display cricket someone else gathers, and pretending
to offer commentary we cannot fetch is worse than not offering the tab.

---

## Writing

Plain, short, and factual. Cricket writes itself; we stay out of the way.

Say what happened and what to do next. No exclamation marks and no invented drama — the scoreline
carries it. "Scores are temporarily unavailable." rather than "Oops! Something went wrong 😢".

Never imply we are faster than we are. "Updated 12 seconds ago" is honest; "Live, ball by ball" is
a promise the provider has to keep for us.

Accessibility expectations for every screen are in
[engineering standards §14](./engineering-standards.md) and
[frontend §9](./frontend.md).
