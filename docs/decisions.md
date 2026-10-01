# Cricket Live — Decision Log

Significant technical decisions, newest first. Each entry records the choice, why it was made, and
what it costs. An entry is only revised by adding a new one that explains the change.

---

## D-040 — Navy, and frosting that is actually frosting

**Status:** accepted. Revises the palette and the blur policy of **D-039**; its structure — one set
of token names, dark as the default, the DOM class as the source of truth — is unchanged and is
what made this a second palette rather than a second theme.

### Context

Two pieces of feedback on D-039, and both were right.

**"Still looks so basic, not proper glassy."** D-039 argued that blur is the least important part
of frosted glass and limited it to the header and the search dropdown, on the grounds that blur
only shows where something passes behind a panel. The premise was correct and the conclusion was
wrong, because the glow it relied on was pitched at roughly a tenth of the strength it needed. With
nothing visible behind the panels, the translucency had nothing to reveal and the result was
exactly what D-039 warned about: grey cards on black.

**"Don't keep green, need navy blue with shades."** A brand change.

### Decision

Frosting is four things, in this order of importance:

1. **Something worth frosting.** `--page-glow` is now a mesh of five overlapping navy, indigo and
   cyan blobs at alphas between 0.14 and 0.40, against roughly 0.07–0.14 before. The colour has to
   be there before a panel can diffuse it.
2. **Tinted translucency.** Surfaces are a pale blue-white rather than neutral white. This is the
   single biggest reason a dark theme reads as grey plastic instead of glass, and it costs nothing.
3. **A lit edge.** `--shadow-card` is four layers, only one of which is a drop shadow: a bright
   inset on top, a dim inset underneath, and two outer shadows at different distances. The insets
   are what make the eye read a pane with thickness.
4. **Then** blur and a saturation lift above 100%, because blur averages colour towards grey and
   the lift puts the hue back.

Blur now applies to **every element that paints a surface**, through a single `glass` utility
rather than a `backdrop-blur-*` scattered across twenty class strings, so the strength of the
effect is one number per theme. The reversal is deliberate: with a mesh behind the page,
*everything* has something behind it, and a page where two panels frost and ten do not looks like
a mistake rather than a decision.

Grain — 180 bytes of inline `feTurbulence` — is the last ingredient. It stops large smooth
gradients banding into visible steps, and it is most of why the surface reads as physical rather
than as a blur filter.

### Navy is a harder brand to carry than green was

A true navy is too dark to read as text on a dark page, so the fill stays navy and the accent is
its light shade. That is what "navy with shades" has to mean on a dark background, and it is the
same `brand` / `brand-strong` split D-039 already needed — the two tokens simply sit at opposite
ends of the ramp in each theme.

One knock-on that is easy to miss: **`upcoming` had to move from blue to cyan.** It sat next to a
green brand perfectly happily; next to a navy one it would have been a second blue, and a status
badge whose colour says the same thing as the furniture around it is not telling anyone anything.

The icon sources and the social card are regenerated from the same navy, so the favicon, the touch
icon, the manifest icons and the link preview all match the site rather than its previous brand.

### A real bug found in the build output

Writing both `backdrop-filter` and `-webkit-backdrop-filter` in the `glass` utility made Lightning
CSS collapse the pair and emit **only the prefixed one**. Firefox supports `backdrop-filter`
unprefixed and does not support the `-webkit-` form at all, so every Firefox user would have had no
frosting whatsoever — and the page would still have looked deliberate, which is how that kind of
thing survives.

The utility now declares the standard property only and lets the build add prefixes, which emits
both. Checked by reading the compiled CSS rather than by reasoning about it, because the first
version also looked correct in the source.

### Degrading

Two ways the effect can be unavailable or unwanted, and the same answer to both: stop pretending,
and make the surfaces solid enough to read on their own. `@supports not (backdrop-filter: …)`
raises them to near-opaque; `prefers-reduced-transparency: reduce` makes them opaque and drops the
mesh and the grain entirely.

---

## D-039 — A dark default and a light option, as one set of token names rather than two sets of classes

**Status:** accepted.

### Context

The app shipped with a single light palette, already expressed as semantic tokens in `index.css`:
`surface`, `ink-muted`, `brand-strong` and so on. A survey of every component found **one** raw
colour in the whole codebase — a `text-white` on a brand button — and no hardcoded hex anywhere.
That groundwork is what made a second theme a palette change rather than a rewrite.

### Decision

The token **names** are declared once. The `@theme` block holds the **dark** values, and
`html.theme-light` overrides them. Tailwind compiles every utility to `var(--color-…)`, so swapping
one class on the root element reskins the application, and **there is not a single `dark:` variant
in the codebase**. A component cannot tell which theme is on, which is the property worth having:
nothing can drift, because there is nothing per-theme to keep in step.

Dark is the default, and that choice has a mechanical payoff. No class means dark, so the pre-paint
script in `index.html` does nothing at all for a visitor who has not chosen — the common case
costs nothing and cannot flash.

`src/store/themeStore.ts` treats **the DOM class as the single source of truth** and reads it on
init rather than re-reading storage. The class is already correct by then, so consulting storage
again would create a second answer that could disagree with what is on screen; storage is only how
the preference survives a reload.

### What makes it look like glass, and what does not

Blur is the part people reach for first and it is the least of it. Three things together do the
work: surfaces that are **translucent white** rather than flat grey, **hairline light borders** so
a panel edge catches light, and `--page-glow` — fixed radial gradients behind everything — so the
translucency has something to reveal. Remove the glow and the identical surfaces read as grey
cards on black.

So `backdrop-filter` is applied **only where something actually passes behind a panel**: the
sticky header, and the search dropdown. Everywhere else it would buy a stacking context and
compositing cost for no visible difference.

The glow is a `background-image` on `body` rather than a pseudo-element, because a pseudo-element
would have had to sit behind an opaque body background, where it would not have been visible at
all. `background-attachment: fixed` is what makes it read as light in the room rather than
decoration that scrolls away.

`color-scheme` is set per theme and is not cosmetic: it is what makes the native `<select>` on the
matches page open a dark list, and stops the browser painting a white canvas during load.

### Three latent bugs this surfaced, which is the interesting part

A second theme is a test of whether tokens mean what their names say. Three did not:

1. **`Button`'s primary variant was `bg-brand text-surface`.** Correct only because `surface`
   happened to be white. Once `surface` became translucent, the label on a green button was almost
   invisible. Now `text-on-brand`, a token that exists to mean exactly that.
2. **`hover:bg-brand-strong` on brand buttons.** `brand-strong` is a *text* colour, and a readable
   accent on near-black has to be lighter — while a fill carrying white text has to stay dark. The
   same token cannot do both. Now `brand-hover`.
3. **`PointsTable` used `bg-surface-sunken`, a token that did not exist.** Tailwind emits nothing
   for an unknown token, so the element had simply had no background since it was written, in
   silence. The token is now defined, because the component's intent was right and only the
   declaration was missing.

None of the three was a dark-theme bug. They were existing bugs that a light-only palette could
not reveal, and that is the general lesson: a theme that reuses a token for a second purpose is
borrowing against the day the two purposes diverge.

---

## D-038 — Routes become paths, because hash routing had made the site unindexable

**Status:** accepted. Supersedes **D-019**, which chose the hash router.

### Context

The request was "improve SEO and add a good favicon". The metadata was not the problem.

D-019 chose `createHashRouter` for one stated reason: a deep link should resolve "without the host
being configured to rewrite unknown paths to `index.html`". That reason was sound when written and
is no longer true of anything. The host is Vercel, `frontend/vercel.json` was already in the
repository configuring caching, and the rewrite in question is one line of it.

What hash routing cost was not subtle. A fragment is never sent to a server. `/#/series` is a
request for `/`, so:

- every page on the site shared one address, and a crawler could only ever see the home page;
- `robots.txt` carried a comment explaining why it deliberately had no `Sitemap` line, because a
  sitemap could have listed exactly one URL;
- `usePageTitle` deliberately set only the document title, its own doc comment explaining that
  per-page `<meta>` tags would be "written for an audience that cannot read them". It was right.

Three artefacts had each independently documented the same consequence and accepted it. Adding
richer metadata on top of that would have been decoration over the actual fault.

### Decision

`createBrowserRouter`, a `rewrites` entry in `vercel.json` sending unmatched paths to
`index.html`, and `src/app/legacyHashRoute.ts` to keep already-shared links working: on load, a
URL of the form `/#/teams` is rewritten to `/teams` with `history.replaceState` before the router
is built.

With real addresses, the rest follows and is no longer decoration:

- `usePageMeta` replaces `usePageTitle` and sets title, description, canonical, Open Graph and
  Twitter tags per page, and `noindex` on `/search` and the not-found page.
- `robots.txt` gains the `Sitemap` line it could not have had, and `sitemap.xml` lists the fixed
  pages.
- `index.html` gains an `og:image` and `WebSite` JSON-LD.

### What this costs, and the two traps in it

**`replaceState` must happen before the router module is evaluated, not before render.**
`createBrowserRouter` reads the location as its module is evaluated, and a module body runs before
the body of whatever imported it. The call therefore lives in `router.tsx` immediately above
`createBrowserRouter`, not in `main.tsx`. Putting it in `main.tsx` first looked correct and was
not: opening `/#/teams` gave the home page at the address `/teams`. Found by opening it.

**Unknown paths now return 200.** A rewrite cannot know a path is wrong, so the host answers every
typo with `index.html` and a success status. Without an instruction, a crawler would read every
dead link as a real page. Hence `noindex` on the not-found page specifically — that tag is load
bearing, not tidiness.

**Link unfurlers still see one card.** Slack, Twitter and Facebook read the HTML as served and do
not run the app, so `usePageMeta`'s tags never reach them; what they show is the static block in
`index.html`, whichever page was shared. Those tags are therefore written to describe the site
rather than left to describe nothing. Per-page previews need the HTML to differ before JavaScript
runs, which means pre-rendering — a larger change, and not attempted here.

`SITE_ORIGIN` in `src/app/site.ts` is a constant rather than `window.location.origin` because the
app answers on more than one Vercel address. A canonical built from wherever the reader happens to
be would nominate each address as the original, which is the opposite of the tag's purpose.

### The icons, shipped in the same change

The old `favicon.svg` was the scaffold's purple lightning bolt — not cricket, and not the site's
green. It is replaced by a stitched cricket ball drawn in `--color-brand`, as two SVG sources:
`public/favicon.svg` with rounded corners for tabs, and `brand/icon-square.svg` full-bleed with a
smaller ball, because iOS rounds an apple-touch-icon itself and Android crops a maskable icon to
the launcher's shape, so an icon bringing its own corners has them clipped or doubled.

`brand/generate-icons.mjs` renders those into `favicon.ico`, `favicon-32.png`,
`apple-touch-icon.png`, `icon-192.png`, `icon-512.png` and the 1200×630 `social-card.png`. The
outputs are committed and `sharp` is **not** a dependency: it carries platform-specific binaries
and is needed only when the brand changes, so it is installed ad hoc, and a fresh clone builds
without it.

---

## D-037 — A page with an archive behind it does not fail because the provider did

**Status:** accepted. Fixes a gap that had been there since the archive was built, found by
watching the deployed site refuse to serve pages it could have served.

### Context

With the day's provider allowance spent and a freshly restarted container, the live site returned
**503 on results, series, teams and search**:

```
GET /api/series  ->  503
{"success":false,"data":null,"message":"Live cricket data is temporarily unavailable..."}
```

Every one of those pages reads the archive. The archive is in our own database, needed no provider
call, and held the answers. They failed because each one reads the provider's window first and let
a failed read fail the whole request.

This had been true since the archive existed and had simply never been visible. Warm caches masked
it: the window is cached five minutes and the series index keeps a last-known-good copy for a day,
so an outage short enough to sit inside those never reached a reader. It took an exhausted
allowance *and* a restart on the same afternoon to expose it — which is to say, it took a worse day
than any we had had.

**The whole purpose of the archive is that the provider's window is narrow and temporary.** Letting
a window failure take down the pages built on the archive defeats the reason the archive was built.

### Decision

`ProviderWindow.OrEmptyAsync` reads the window and returns an empty one when the provider cannot be
reached. Applied to every read that merges the window with something durable: results, the series
list and series pages, teams, search, and the series-name filter. The window contributes what is in
play right now, and contributing nothing makes those pages slightly stale instead of absent.

**Deliberately not applied to the live and upcoming lists.** They have no second source, so an
empty array there would be a claim that no cricket is on — which is a different statement from "we
cannot currently tell you", and during an outage only one of them is true. Those still return 503.
Degrading honestly means degrading differently depending on what else you have.

Nothing is logged at the swallow. Every path that raises `CricketDataUnavailableException` has
already logged the cause with the detail that matters — status code, refusal reason, or exhausted
allowance — and a second line saying a caller swallowed it would add nothing and appear once per
page view.

### Verified against a real outage

A genuine provider outage is not something you can arrange; this one was available because the
day's hundred calls were gone:

```
matches/recent    200   1 row        from the archive
series            200   1 row        from the archive
teams             200   2 rows       from the archive
search?q=india    200   2 matches, 1 team, 1 series
matches/live      503                correctly, no second source
matches/upcoming  503                correctly, no second source
```

Before this change the first four were 503.

### Cost

- A page can be quietly thinner during an outage rather than announcing one. The series list
  dropped from 63 to 1, because a cold process has no cached index and no last-known-good copy to
  fall back on, and nothing on the page says so. The honest alternative — a banner — needs a way
  for a response to carry "this is partial", which no DTO here has. Worth doing; not worth blocking
  a restored site on.
- `ProviderWindow` is a static helper rather than a decorator on `ICricketDataProvider`, because the
  behaviour is a property of the *caller's* situation, not of the provider. The same failed read
  must break the live list and not break the series list, and a decorator cannot tell them apart.

---

## D-036 — History is ingested into the archive, not merged into the query that reads it

**Status:** accepted. The happy path is **not yet verified against the provider** — see the last
section, which is the honest part of this entry.

### Context

The results page held two matches and the teams page two sides, because the archive only ever
accumulated forward: it began the day this site did. [D-033](#d-033) through [D-035](#d-035) fixed
three pages by reading wider provider endpoints at request time. Results cannot be fixed that way,
and the reason is worth stating because it is the only one of the four that is different.

`GetFinishedAsync` filters, counts and pages **in SQL**. Mixing provider rows into that at read
time means two sources with no shared opinion about what page three is or what the total is: ask
for twenty, get eleven, and a count that agrees with neither. The provider's list is 15,531 matches
at 25 a page, so it cannot be read whole and sorted in memory either.

### Decision

Write the provider's rows into the archive instead, in the background, and leave every query
exactly as it is. The results page, the series pages, the teams page and search all already read
the archive, so one writer widens four readers and none of them needed changing.

The rows are the right shape for this. `/v1/matches` returns the same `CricketDataMatch` the live
window does — id, name, venue, teams, teamInfo, **score**, `series_id` on every row — so the
existing mapper consumes them and `SaveFinishedAsync` already keeps only completed matches and
already ignores one it holds. The backfill writes no new kind of record.

### Three guards, because this could eat the whole allowance

1. **A page count per UTC day**, default six, held **in the database**. In memory it would be
   meaningless: the host spins the container down after fifteen idle minutes and rebuilds it on the
   next request, so the counter would reset several times a day.
2. **A refusal to run past half the day's allowance.** Stricter than the poller's reserve, and
   deliberately so — the poller is serving somebody who is watching, and this is serving nobody
   yet.
3. **A bounded depth**, default forty pages, then it starts again from the top. Walking all 621
   pages at a polite rate would take months and spend most of them in seasons nobody will open.
   Lapping keeps recent history complete instead.

A full lap is about a week at six pages a day, and takes the archive from a handful of matches to
roughly a thousand.

### What running it actually taught us

The first real run found a flaw that reading the code had not. The in-memory budget starts a fresh
process believing nothing has been spent, so the first tick after every restart spent one doomed
call on an exhausted day — and on a host that rebuilds its container several times a day, it spent
that same doomed call several times. The response corrects the belief, but only after the call.

An account refusal is therefore now written down as "no more pages today", which survives the
restart the in-memory count does not. A refusal about the *request* is only logged, because that is
a different problem and marking the day spent would hide it.

### Known limits, stated rather than discovered later

- **The list is ordered by series, newest series first — not by date.** So "the first forty pages"
  is the thousand matches of the most recently active series, not the thousand most recent matches.
  Close enough to be useful; not the same thing.
- **The offset drifts.** A new series appearing at the front shifts every offset behind it, so a
  lap can read a page twice or skip one. Reading twice costs nothing, because the archive ignores a
  match it already holds; a skipped page is picked up on the next lap. A persistent cursor over a
  list that reorders itself is approximate by nature, and the alternative — a stable cursor the
  provider does not offer — does not exist.
- **A page that always fails stalls the lap**, because the offset is deliberately not advanced on
  failure. `LapsCompleted` is logged so that this is visible as a number that stops rising rather
  than as silence.

### Shipped switched off, and why that is the decision rather than a hedge

`MatchBackfillPages` defaults to **0**, which resolves to a loop that logs that it is off and
returns. Forty is the intended value.

Everything here builds, the 203 tests pass, the migration applies, the loop starts, and the
**refusal path was observed in a real run against the live provider.** The **happy path — reading a
page and writing its matches — has not been.** The day's hundred calls were spent verifying D-034
and D-035, and the provider refused with "hits today exceeded hits limit", which is itself the
evidence.

So the code ships reviewed, deployed and dormant. Nothing unobserved runs against production data,
and the alternative — holding the branch — would have left a migration unapplied and a week of
drift to re-resolve for no gain. **To turn it on:** run locally with
`CricketData__MatchBackfillPages=40` once the allowance has reset, confirm a page is read and the
archive grows, then change the default here. That is one line and this paragraph is the record of
why it was not written yet.

Recording it rather than leaving it to be assumed, because [D-032](#d-032) was written about
exactly this: a mechanism that looked finished and had never actually run. The difference is that
this one says so out loud and is switched off until it has.

### What exhausting the allowance accidentally proved

With the day's calls gone, the deployed site was measured rather than guessed at:

```
matches/live      200   0 rows
matches/upcoming  200   0 rows
matches/recent    200   1 row
series            200   63 rows
teams             200   2 rows
```

No errors anywhere — it degrades to empty lists, which is what every "failures are swallowed"
remark in D-033 through D-035 claimed would happen and none of them had seen. `series` still
answering with all 63 is the last-known-good cache from D-033 serving a stale index through a
provider outage, which is the one path in that change that could not be tested when it was written.

---

## D-035 — `cricScore` decides what is on, `series_info` says what it is

**Status:** accepted. Third and last of the changes that began with [D-033](#d-033).

### Context

The upcoming list was empty. Not thin — empty, for weeks, on a site whose whole purpose is to say
what cricket is coming. The cause was the same one as D-033 and D-034, one layer further down: we
asked a single endpoint and treated its answer as the state of the world. Measured within the same
minute:

```
/v1/currentMatches  ->  2 rows,  0 unfinished
/v1/cricScore       ->  6 rows,  4 unfinished   (3rd ODI, 1st T20I, 2 Sheffield Shield fixtures)
```

Four matches were coming and we were showing none of them. The provider documents no relationship
between these two endpoints and we could not find one — neither is a subset of the other — so both
are read.

`cricScore` alone cannot fill the page. It carries an `id`, a `series` *name*, a state, and two
scores. No series id, no venue, no match description, and a team written `"India [IND]"` with the
name and abbreviation run together. Building a `MatchDto` from it would mean putting a row with no
venue and a team called "India [IND]" next to complete ones.

### Decision

Three sources, each doing the one thing it is good at:

1. **`cricScore`** names which matches are on — `IMatchIndex`, one call, cached five minutes to
   match the live window it is read beside.
2. **the series index** turns those series names into ids, which `cricScore` does not carry.
3. **`series_info`** (already built for D-034, already cached) supplies the matches in full, matched
   back to the ids from step 1.

`PendingMatches` performs the join, and `MatchService` merges its result under the live window —
under, because a match in both has started and only the window carries a score.

**The index defines the window; the fixture lists only furnish it.** Taking everything unplayed
from a fixture list would put all 31 Sheffield Shield fixtures on a page asking what is on this
week. Matching back by id keeps the answer to what the provider itself considers current.

Cost is one call plus one per distinct active series, capped at six, all cached and all shared
between visitors. Two series were active the day this was built.

### Reading the state field backwards, on purpose

`ms` was measured as `"fixture"` and `"result"`. It is documented nowhere, nothing was in play on
any day we probed, and so the set is not known to be closed. `IsFinished` therefore asks whether
the value *is* `"result"` rather than listing the values that mean it is not. An unseen state —
live, abandoned, anything added later — reads as pending and gets looked at. The asymmetry is
deliberate: a finished match wrongly shown as pending is corrected by the detail `series_info`
fills in, whereas a pending match read as finished is dropped and never looked at again.

### The teams page widened without any code about teams

`TeamService` reads the same two sources, so pointing it at the same join took the list from 2
sides to 6 — Queensland, South Australia, Victoria and Western Australia, from next week's
Sheffield Shield fixtures. That is the whole change: teams *are* the matches, so a source of
matches is a source of teams.

It widens the limitation rather than fixing it, and the limitation is worth restating. There is no
team endpoint and no team identifier — `countries` returns two-letter country codes, and Queensland
is not a country, so ours stay derived from the team name. A side appears when it has a match and
not before.

### Cost

- Series names are matched **exactly**, case-insensitively. The index lists "Sri Lanka tour of West
  Indies 2026" and "Sri Lanka tour of West Indies, 2026" as different series, so anything looser
  would pick whichever came first and be confidently wrong about which season it was showing. The
  price is that a match whose series name is not in the pages of the index we read contributes
  nothing, silently.
- An upcoming match shows "Yet to bat" for both sides, because it is true and because the
  alternative is a blank.
- The upcoming list is now as wide as `cricScore`, which is a provider-shaped window and not a
  schedule. A fixture three weeks out is on its series page, not here.

---

## D-034 — A series page lists the provider's fixtures, and the title overrules `matchType`

**Status:** accepted. Builds on [D-033](#d-033), which fixed the list and left every page it
linked to nearly empty.

### Context

D-033 made the series list honest: 63 series instead of one. It also made a new problem obvious.
The list came from the provider's index, but each series *page* still showed only matches we hold,
so 61 of those 63 cards opened onto an empty page explaining why it was empty. A list of links to
apologies is not much better than a list of one.

`GET /v1/series_info?id=` closes it in a single call. Measured on the West Indies tour:

```
series_info?id=702ce6cb-…  ->  info{…}  matchList[8]
  rows carry: id, name ("India vs West Indies, 4th T20I"), matchType, status, venue,
              date, dateTimeGMT, teams[], teamInfo[], matchStarted, matchEnded
  rows carry series_id on 0 of 8
```

That is the same shape `CricketDataMatchMapper` already consumes for the live window, which is the
whole reason this was cheap: no new mapping, no new DTO, no new shape for the frontend to learn.
The missing `series_id` is filled in from the id we asked about, which is not a guess — it is the
question.

### Decision

`ISeriesFixtures` is a second existence-only source, parallel to `ISeriesIndex` and deliberately
not part of `ICricketDataProvider`. `SeriesService.GetByIdAsync` merges three sources weakest-first
into a dictionary keyed by match id:

1. the provider's fixture list — complete, but every match reads as unplayed
2. our archive — scores, for matches played since this site started
3. the live window — the current state of anything in play

Later wins, so a match we hold a score for keeps the score, and one we do not still appears with
its venue and its date. The West Indies tour went from 2 matches to 8; Sheffield Shield from 0 to
31; Ranji Trophy from 0 to 119.

One call per series *actually opened*, cached three hours, so the page that benefits is the page
that pays and nobody pays for the 61 cards they scrolled past. `SeriesFixturesCacheHours = 0`
resolves to `NoSeriesFixtures` rather than to a zero-length cache, because a cache that expires
immediately would read the provider on every page view and spend the day's allowance in an
afternoon. Off is a decision; a zero lifetime would be an accident.

### The provider contradicts itself about format

Building this surfaced a data-quality bug that would have shipped straight to the user's screen.
Every one of the five T20Is on the West Indies tour arrives with `matchType = "odi"`:

```
India vs West Indies, 1st T20I   matchType=odi
India vs West Indies, 2nd T20I   matchType=odi
India vs West Indies, 3rd T20I   matchType=odi
India vs West Indies, 4th T20I   matchType=odi
India vs West Indies, 5th T20I   matchType=odi
```

Five fixtures, five wrong labels, and the format filter would have agreed with all of them. When
the two disagree the title wins, because the title is what a human wrote and read back, and it is
also what our own page displays — a card reading "4th T20I" with an ODI badge is visibly wrong in
a way that a wrong-but-consistent label is not. Every disagreement is logged at warning level, so
if the provider's `matchType` ever becomes trustworthy we will see the logs go quiet rather than
having to go looking.

The title is only consulted when it names a format unambiguously. "1st Match" and "Elite Group A"
name none, so those keep `matchType`.

### Cost

- A series page can show a fixture with no score where it previously showed nothing. The counts
  say which is which: "8 matches, 2 with scores recorded here."
- A card can no longer show a closed date range for a tour in progress. The only end date we have
  is that of the last match *we hold*, which for a live tour is somewhere in the middle, and a
  range ending there would contradict the list it opens. Closed range only when we hold every
  match; otherwise "From ⟨start⟩". Computing real end dates would mean fetching all 63 series'
  fixtures to render one list.
- `FromTitle` is a word-match against English fixture names. It will not read a title in another
  language, and it reads "T20I", "T20", and "Super T20" all as T20, which is correct here and is
  an assumption worth remembering.

---

## D-033 — The series list reads the provider's index after all, and D-013 was wrong about why

**Status:** accepted. Reverses the reasoning in [D-013](#d-013) while keeping its finding intact.

### Context

The series page showed one series: "West Indies tour of India, 2026". It was reported as a fault
and it was not one — it was the design working exactly as specified, which is a worse problem.

`SeriesService` built the list entirely from matches we hold, meaning the provider's current window
plus our archive. Measured on the day this was written:

```
/v1/currentMatches  ->  status=success  totalRows=2   returned=2   (both India v West Indies)
/v1/series          ->  status=success  totalRows=1190  25 per page
/v1/matches         ->  status=success  totalRows=15531 25 per page
```

Nothing was broken. One series went in and one came out. The provider's window is a few days wide,
so outside a busy fortnight the list was always going to be a handful of rows, and the quota was
barely touched: 7 calls of 100 used that day.

### What the original reasoning got wrong

The note on `SeriesDto` said the index "would cost a call from a hundred-a-day budget and buy a
worse answer than counting the matches we already have in hand". Every factual claim in it still
holds — `endDate` has never once arrived as an ISO date, always `"Apr 11"` with the year left to
guess — but it answered the wrong question. The index is a bad source of *detail* and the only
source of *existence*, and those do not compete. Rejecting it for being thin on fields meant the
list could only ever contain series we already had a match of.

The budget argument was also weaker than it sounded, and nobody re-checked it. The poller only
spends calls when something is live, so the allowance sits almost untouched most days.

### Decision

Read the index for existence; keep deriving everything else from matches.

`ISeriesIndex` lives in Application, separate from `ICricketDataProvider`, and the separation is
load-bearing: provider failure means the site has nothing to show and must propagate, whereas index
failure means a shorter list and is swallowed. `CricketDataSeriesIndex` serves the last good answer
or an empty list, and an empty list is treated as silence rather than as "there are no series".

Two things the index is filtered on, which turn out to be one thing:

| Condition | Why |
| --- | --- |
| `matches > 0` | a series with no fixtures opens a page with nothing on it |
| `startDate` parses as a full ISO date | the year-less form cannot be placed on a calendar |

Across fifty rows sampled these correlated perfectly — every row with matches had an ISO date, every
row without had `"Oct 18"` — so requiring both costs nothing that requiring either would have kept.
It also removes a duplicate: the provider lists some tours twice, once as a stub with no matches and
again with its fixtures, and a naive read showed both.

**Four pages, cached six hours**, which is 16 calls a day against the budget of 100. Four pages of
25 yielded 63 series after filtering, reaching about four months back and five ahead.

### Two counts, not one

`MatchCount` is what we can show. `TotalMatchCount` is what the series has, or null when the index
did not cover it. Null and zero are different claims and only the first is ours to make. The UI
prints both — "2 of 8 matches held" — because printing only the first invites it to be read as the
second, which is the error this whole entry is about.

A series the index lists and we hold nothing of now resolves to a real page with no matches rather
than a 404. Listing something and then refusing to open it is worse than not listing it.

### Ordering, which took two attempts

Descending by date was tried first and read badly: the index lists fixtures a year out, so the page
opened on a tour in March 2027 with this week's cricket far below it. The list is now ordered by
whether a match is in progress, then whether we hold anything, then by **distance from today in
either direction**. That needs no end dates — and we have none — and it puts the current week at the
top with the page falling away in both directions.

### Cost

16 calls a day, and a series page that can show a name and a match total for a series it holds no
matches of. The second is the honest state of affairs rather than a defect: we keep matches from the
live window and from our own archive, which starts when this site did.

### What this says about the earlier decision

D-013 measured the endpoint carefully and recorded the measurement accurately. The error was in the
inference, not the data — "thin on fields" was converted into "not worth reading" without asking
what question it would be answering. The measurement is still in the codebase and still correct; a
re-read of it two sprints later is what produced the opposite conclusion.

---

## D-032 — Branch protection, and the second mechanism today that ran without having authority

**Status:** accepted. Completes `1.4`, which was written in Sprint 1 and never done, and `8.21`.

### Context

The Sprint 8 exit criteria claimed "No secrets in the repository — enforced by the CI secret scan on
both branches", and left "CI blocks merges on failing tests" unticked with the note "the secret scan
is the only gate". Both statements were wrong in the same direction, and reconciling the plan
against reality is what surfaced it.

`.github/workflows/backend.yml` has run `dotnet test` on every pull request to `master` and
`develop` since Sprint 1. All 203 tests run there. `secrets.yml` scans both branches. Four green
checks appear on every PR. None of them gated anything:

```
$ gh api repos/shubhamdsk/cricket-live/branches/master/protection
{"message":"Branch not protected","status":"404"}
$ gh api repos/shubhamdsk/cricket-live/branches/develop/protection
{"message":"Branch not protected","status":"404"}
```

Neither branch had ever been protected. A red check would have blocked nothing; every merge in this
repository, including the six in the last session, would have gone through with a failing test suite
and a triggered secret scan. The checks were **reporting**, and reporting had been read as
**enforcing** — including by me, when I wrote that exit criterion.

### Decision

Protect both `master` and `develop`, requiring the three checks that mean something:

| Setting | Value | Why |
| --- | --- | --- |
| Required checks | `backend`, `frontend`, `scan` | build, lint, the 203 tests, and the secret scan |
| Strict (branch up to date) | off | a solo repository would spend its time rebasing for no safety gained |
| Required reviews | none | there is one developer; requiring a second would mean disabling the rule to merge, which is worse than not having it |
| Enforce for admins | off | the rule is here to catch mistakes, not to lock the only maintainer out of his own recovery path |
| Force push, deletion | blocked | the one irreversible pair |

The two build jobs were both named `build`, which made the status context ambiguous — requiring
`build` would have matched either workflow. They are now `backend` and `frontend`, and the reason is
written in the workflow files so nobody renames them back.

### Cost, and what this does not do

Protection with `enforce_admins` off is bypassable by the repository owner, deliberately. This is
not a security boundary against a hostile maintainer and is not meant to be; it is a guard against a
distracted one. Anyone treating it as the former has made the same mistake twice.

### The pattern worth naming

This is the **second** finding in a row with an identical shape, and the repetition is the
interesting part:

- [D-031](#d-031): `CricbuzzOptions.AutoResolve` was changed from `true` to `false` in C#. The
  change was correct, reviewable, and inert, because `appsettings.json` stated the key explicitly
  and a property initialiser only applies when the key is absent. Production kept serving
  scorecards.
- Here: `dotnet test` ran on every PR and passed. The run was real and the result was correct and
  it bound nothing, because no rule required it.

In both cases the visible artifact — a default in source, a green check on a PR — was not the thing
that had authority, and in both cases reading the diff would never have revealed it. **Both were
caught the same way: by querying the running system and comparing the answer against what the
artifact implied.** One took a request to production; one took two API calls. Neither took cleverness.

The generalisation for this project: when something is meant to *prevent* an outcome, the evidence
that it works is the prevented outcome, not the presence of the mechanism. A test that runs, a
default that is written, a scan that scans — none of those are the same as a merge that was
actually refused.

---

## D-031 — Cricbuzz's terms, read in full: neither route to that data is licensed

**Status:** accepted. Strengthens [D-020](#d-020), [D-024](#d-024) and [D-027](#d-027) rather than
reversing any of them, and fixes the wrong default that [D-029](#d-029) found.

### Context

[D-030](#d-030) read CricketData's terms and found a clause the project was breaking. It also noted
that the Cricbuzz side had never had the same treatment. This is that review. There are **two**
permissions to assess, not one, because the project can reach Cricbuzz's data two ways: by reading
`www.cricbuzz.com` directly, and through a RapidAPI listing that reads it for us.

### Reading the site directly is not licensed, and the terms say so more plainly than robots.txt did

D-020 proceeded on the judgement that `robots.txt` is a crawling convention rather than a licence
term. That judgement is still defensible, and it is now beside the point, because the licence term
exists separately and says the same thing:

> The Company grants You a personal, revocable, non-exclusive, non-transferable right to access and
> use the Site, for non-commercial use only **and private viewing only**.

> You shall not use, reproduce, redistribute, sell, offer on commercial, rental, ... adapt,
> **communicate to the public**, make a derivative work ... in any manner whatsoever.

> Except as stated herein, none of the Materials may be modified, copied, reproduced, distributed,
> republished, downloaded, displayed, sold, compiled, posted or transmitted in any form or by any
> means ... without the prior express written permission of the Company.

"Private viewing only" and "communicate to the public" are the operative phrases. Serving
Cricbuzz-derived content on a public website is the opposite of private viewing, whatever one
thinks of `robots.txt`. The `robots.txt` was also re-read and is unchanged: `User-agent: *` followed
by `Disallow: /`, with named exceptions for search engines and ours not among them.

**The asymmetry with CricketData is the whole finding.** CricketData volunteers that match data is
*"PURELY FACTUAL INFORMATION ... incapable of copyright protection"*. Cricbuzz claims the opposite —
all Materials are its intellectual property — and backs it with a grievance officer and a
copyright-complaint procedure. Two sources, the same underlying facts, and opposite positions on
whether those facts are theirs. We are not obliged to accept Cricbuzz's characterisation, but we are
not the right party to test it, and the terms are what a dispute would start from. Governing law is
India, exclusive jurisdiction Bengaluru.

### The RapidAPI route does not fix it, because RapidAPI grants nothing

This was the interesting half. RapidAPI's own terms are explicit that it is not in the licence
chain at all:

> With respect to each API, API Consumers and the API Provider ... acknowledge and agree that the
> terms and conditions applicable to the use of ... such API by each such API Consumer are solely
> between each such API Consumer and such API Provider, **and not with Rapid**.

> API Provider, not Rapid, is responsible for monitoring and enforcing the API Content/Terms
> applicable to each API.

So paying RapidAPI buys gateway access and no rights. Whatever permission we would have comes from
the listing's publisher, who is not Cricbuzz and who warranted to RapidAPI that it holds rights it
is in no position to hold. **The licence chain has a broken link in the middle, and being three
parties away from Cricbuzz does not lengthen it into a licence.** D-027 called this source "a
reseller of a scrape" on instinct; the terms of all three parties now say the same thing on the
record.

### What changes in the code

**`Cricbuzz:AutoResolve` now defaults to `false`.** This is the [D-029](#d-029) finding turned into
a fix. The scorecard reaches a gateway, so `CricbuzzApi:Enabled` reads as a decision about that
gateway — but pairing goes through the listing reader, so that one switch was enough to start
reading Cricbuzz's website. Two switches representing permissions from two different parties were
collapsed into one, and the one that was visible belonged to the wrong party.

Every path to `www.cricbuzz.com` is now off until a deployment says otherwise: `Cricbuzz:Enabled`
for batters, `Cricbuzz:StandingsEnabled` for points tables, and now `Cricbuzz:AutoResolve` for
pairing. `Cricbuzz:MatchIds` still pairs by hand, which reads no page and stays available.

**It took two changes, not one, and the first one silently did nothing.** Flipping the C# default
shipped, deployed, and left production behaving exactly as before, because `appsettings.json` states
every one of these settings explicitly — so the property initialiser is only what applies when the
key is absent, and for this key it was not. Caught by testing the deployed endpoint rather than by
reasoning about the diff, which is the only reason it was caught at all. **A default and a committed
configuration value are two places, and a switch changed in one of them looks correct in review and
changes nothing at run time.**

### What does not change

**The code stays.** Deleting it would make the repository a worse record than it is: D-020, D-024,
D-027 and this entry are the reasoning, and reasoning with the subject removed is hard to check.
Everything is off by default, nothing polls, and the parser tests run against captured pages rather
than live traffic.

**D-020 is not reversed.** It remains a decision the project owner took knowingly, with the
obligations written down. What this entry adds is that the `robots.txt` argument was never the
strongest one against it — the licence grant was — and that the standings feature is therefore on
weaker ground than it looked, not stronger.

### Cost, and the recommendation

- **A deployment with `CricbuzzApi:Enabled=true` loses automatic pairing** until it also sets
  `Cricbuzz:AutoResolve=true`. That is the intended effect: it is a decision someone should make on
  purpose. It is also a live behaviour change, not a theoretical one.
- **The honest reading is that the scorecard should be off in production.** It is a public,
  non-commercial site, which satisfies one half of Cricbuzz's grant and fails the other half —
  "private viewing only" — and the RapidAPI route supplies no permission to make up the difference.
  This entry does not switch it off, because that is the project owner's call and the switches now
  present it as one, but it records that the technical work is finished and the remaining question
  is not technical.
- **No equivalent carve-out exists to lean on.** With CricketData there was one: they say the data
  is not theirs to own. Cricbuzz says it is.

---

## D-030 — The provider's terms, read in full: no attribution owed, and one clause we were breaking

**Status:** accepted. Settles `8.27` and the open question in [D-012](#d-012); confirms the guess
recorded against `3.5`.

### Context

Task `8.27` carried two halves: make the attribution visible, and re-read the provider's terms
before anything went public. The first was done in Sprint 3. The second never was — Sprint 3
recorded that attribution "is not demanded by CricketData's published terms the way SportScore's
badge is, but the terms have not been read in full", and that guess then sat unverified through
five sprints and a public deployment. It has now been read end to end.

### Attribution is not required, and the footer stays anyway

There is no clause obliging an API consumer to display a credit. The only credit-related language
runs the other way — a prohibition on removing *their* notices from material taken from the site —
and the one link-back requirement is scoped narrowly to resharing content on social platforms. The
Sprint 3 guess was right.

The footer credit stays regardless. It costs nothing, it tells a reader where the numbers come
from, and a project that reads someone else's data for free should say so whether or not it is
made to.

### Hot-linking their images is forbidden, and we were doing it

> Hot-linking of images we serve is not allowed. Download the images we've provided and set up
> your own CDN, you can use Amazon Cloudfront or other CDNs. Your domain may get blacklisted if
> you do this.

Team crests were rendered as `<img src="https://g.cricapi.com/iapi/...">` in two components, so
every visitor to the public site was spending the provider's bandwidth directly. This is the one
concrete breach the re-read found, and the stated penalty is aimed at the domain rather than the
image — a blacklisting would have taken the API down with the pictures, which makes it a
reliability problem as much as a licensing one.

**Crests are now served from our own origin.** A DTO no longer carries a provider image address:
it carries `/api/crests/<token>`, the token being the provider URL encoded, and `CrestsController`
fetches the bytes once and serves them thereafter. Held in memory for a week, returned with
`Cache-Control: public, max-age=604800, immutable`, so a reader fetches each crest once and a
restart costs a handful of small requests rather than one per visitor per page view.

**Archived rows are rewritten on the way out, not migrated.** The archive stores whole DTOs as
JSON, and rows written before this change hold the absolute address. Translating on read keeps the
payload an honest record of what the provider actually said, and the translation is idempotent so
a newer row passing through twice is left alone.

**The route is not a general proxy.** The address is recovered from the token and checked against a
two-host allow-list before anything is requested — on the way in *and* again on the way out, since
the token arrives from the caller. Without the second check, anyone could encode any address and
have the deployment fetch it from inside the host's network. An unrecognised host is dropped rather
than passed through, so the crest falls back to the side's initials, which is already what the many
teams with no crest look like.

### Two other clauses worth having written down

**The licence is personal and non-commercial, and their definition of commercial is broad:** "Any
use for which you receive any remuneration, whether in money or otherwise, is a commercial use for
the purposes of this clause." A public, ad-free, donation-free site is within it. Advertising, a
donate button, or any paid use is not, and the terms are explicit that such use is permitted only
after buying credits. **So monetising this site in any form is a paid-plan decision first and a
product decision second.**

**They disclaim copyright in the data itself.** "Exclusive rights over Match Information generated
during a cricket match, which is purely factual information, incapable of copyright protection — so
basically what we collect and serve to you is PURELY FACTUAL INFORMATION." This sits oddly beside
the blanket copyright clause elsewhere on the same page, and it is the firmer ground: it means
[D-017](#d-017)'s archive of finished matches keeps facts they do not claim to own, rather than
copies of their material.

### Cost

- **A restart re-fetches the crests**, because the cache is in memory and the host has no disk. A
  handful of requests for a few kilobytes each, against one per visitor per page view before — but
  it is not literally "your own CDN" as the clause describes, and a deployment with a disk or a CDN
  in front of it should do better.
- **An image route is a new shape of request to defend.** Counted in its own rate-limit bucket at a
  much higher ceiling, because a list page asks for two crests per card and counting them alongside
  everything else would let one page view spend the minute's allowance on pictures.
- **The token is opaque**, so a crest URL is no longer readable at a glance. The alternative —
  naming the file and rebuilding the provider's path — bakes their URL layout into our code and
  breaks quietly when they change it.
- **The Cricbuzz scorecard source still has no equivalent review.** It is a reseller of a scrape
  ([D-027](#d-027)), and [D-029](#d-029) established that enabling it also starts reading Cricbuzz's
  own website. No image of theirs reaches our DTOs, which was checked, so the hot-linking question
  does not arise there — but the permission question is untouched and remains a reason to leave it
  off.

---

## D-029 — What the first deployment measured, including two predictions that were wrong

**Status:** accepted. Corrects the risk recorded in [D-001](#d-001) and the caveat in
[D-027](#d-027); neither decision changes.

### Context

Several things in this log were written as reasoning about how a host would behave rather than
as observations of one. The first deployment — Render, Neon, Vercel — is the first chance to
check them, and the results should be recorded whichever way they fell.

### What was measured

**SSE survives the proxy.** [D-001](#d-001) chose Server-Sent Events knowing that long-lived
responses are the thing hosts buffer or terminate, and Sprint 5 was written to verify it early
rather than discover it late. Measured with a plain HTTP client, so the browser could not
confound the result: the connection held past **100 seconds**, keepalives arrived every 20, and
the first `match` frame was delivered immediately rather than held. Render neither buffers
`text/event-stream` nor cuts an idle stream. `X-Accel-Buffering: no` and `DisableBuffering()`
were both in place, so this does not establish that they were *needed* — only that the
arrangement works.

**Automatic Cricbuzz pairing works.** [D-027](#d-027) built the scorecard but could only verify
it by writing a Cricbuzz match id by hand, which left automatic resolution — the part that has
to recognise the same match under two providers' naming — as the weakest link. It resolved
`2nd ODI / West Indies tour of India, 2026` unaided and returned a live card. One case, not a
guarantee, but it is the case the feature turns on.

**Co-location matters more than the code does.** Every archive query against a Neon project in
Ohio, from a Render container in Singapore, logs at exactly **202ms** — a single-row primary-key
lookup that should be about 1ms. A figure constant to three digits is distance, not work.

### The two predictions that were wrong

Recorded because the reasoning behind them sounded plausible and was written down as though it
were a finding, which is the mistake worth not repeating.

- **"Cricbuzz will refuse a datacenter address."** It did not. The listing was served to
  Render's Singapore IP without complaint.
- **"The scraping ships disabled."** Only partly true, and the part that is false matters more.
  `CricbuzzEnrichmentProvider` and the standings reader are both gated on `Cricbuzz:Enabled`,
  but `CricbuzzMatchDirectory` is registered unconditionally and does the pairing by reading
  Cricbuzz's public website. So enabling `CricbuzzApi:Enabled` alone begins reading that site.
  The switch does not mean what its name implies, and anyone weighing the attribution question
  in D-027 was, until now, weighing it with wrong information.

### Cost

- The wrong-switch finding is a design wrinkle left in place rather than fixed. Gating the
  directory on `Cricbuzz:Enabled` would be more honest, but it would also mean the scorecard
  silently does nothing unless *two* switches agree, which is its own trap. Documented instead.
- One case does not verify pairing in general. A series the two providers name differently will
  still decline, and declining remains the correct behaviour.
- The region finding is recorded, not acted on. Neon cannot move a project between regions, so
  fixing it means recreating the database.

### The region finding, acted on

Recorded here rather than as a new entry because it is the same measurement taken twice.

The 202ms figure understated the cost. A page load runs **six** queries, and each one logged
197–220ms regardless of what it asked for — a `count(*)` over a single row cost the same as
fetching a payload, which is what round-trip time looks like when the work is negligible. That is
roughly **1.2 seconds per page load spent on distance alone**, and it moved the region change from
a nicety to the obvious next thing to do.

The project was recreated in Singapore and the schema reapplied. The same statements now log
**2–3ms**, with the first query on a fresh connection at 90–101ms because it carries the TLS
handshake, and an occasional 593ms when Neon is waking from scale-to-zero. Neither is distance.

One thing was expected to be lost and was not: the single archived match was thought to be stranded
in the old project, but the archiver re-captured it from the provider's window on the first poll
after the switch. The `INSERT` in the log is the proof it was a fresh database — on the old one that
row already existed and the existence check would have skipped it.

---

## D-028 — The archive moves to PostgreSQL, because the host has no disk

**Status:** accepted. Supersedes the "SQLite because it is a file" half of [D-017](#d-017) for
deployment only, and completes the swap [D-004](#d-004) and D-017 both anticipated.

### Context

D-017 chose SQLite and gave the reason plainly: it is a file, so there is nothing to install,
nothing to run alongside the API and nothing to provision. **That reasoning has not been
falsified.** SQLite is still the right database for this workload and is still what runs locally
and in the tests.

What changed is the host. Deployment had to be free and without a credit card, which rules out
Fly.io — the one free tier that will keep a file. Render's free web services cannot attach a disk
at all, and the container is rebuilt from the image whenever the service wakes from a spin-down,
about fifteen minutes after the last visitor. So a SQLite archive would not be lost per release;
it would be lost several times a day.

That matters here more than it usually would. The archive is not a cache. D-017's whole premise
is that no source available to us can supply completed matches, so the archive accumulates
forward and anything lost is lost permanently. An archive that empties nightly is Sprint 7
producing nothing while appearing to work.

### Decision

**PostgreSQL in deployment, on Neon's free plan. SQLite for local runs and for the tests.**

D-017 predicted this would cost "one registration and the migration", and that was very nearly
right. The registration picks a provider from the *shape* of the connection string rather than
from a separate setting, because a provider setting and a connection string are two facts that
can contradict each other and the contradiction would surface in production.

Three things were not free:

- **The one provider-specific line in the model.** `SeriesName` used SQLite's `NOCASE` collation
  so series filtering is case-insensitive and still index-backed. PostgreSQL needs a collation
  that exists before a column can reference it, so the model declares a non-deterministic ICU
  collation and a migration creates it. The `DbContext` comment had already named this as the
  line that would have to change.
- **The migrations are PostgreSQL's alone.** They are provider-specific SQL and EF keeps one set
  per assembly. The local SQLite database is built from the model with `EnsureCreated` instead.
- **Connection URLs.** Every managed host issues `postgresql://…` and Npgsql parses only
  `Host=…;Database=…`. The app converts. This is not a convenience: Npgsql reports the mismatch
  by throwing with the whole connection string in the message, so the failure mode is a deploy
  that prints the database password into a log. It did exactly that once while this was being
  built, and that password had to be reset.

### Cost

- **Local and deployed now run different databases.** ASCII case folding in SQLite against
  Unicode rules in PostgreSQL means an accented series name compares slightly differently in the
  two. Matching them exactly would mean giving up index-backed filtering on one side.
- **The PostgreSQL path has no automated coverage**, because the tests run on SQLite and there is
  no test database. It was verified by rendering the migration to SQL and reading it. Deployment
  is where it is really proven.
- **A model change that breaks a migration will not show up locally.** It shows up on deploy.
- **Idle suspension is now a runtime concern.** Neon force-suspends after five minutes, so EF's
  retry-on-failure is enabled; without it the first visitor after a quiet spell gets an error and
  everyone behind them is fine.
- **A second service to keep alive**, with its own free-tier limits. The binding one is compute
  at 100 CU-hours a month, not the 0.5 GB of storage, which is decades of matches.

---

## D-027 — The scorecard is built; commentary is not, and the line between them is one request

**Status:** accepted. Supersedes the "will not be built" half of [D-024](#d-024) for the
scorecard only, on the measurement in [D-026](#d-026).

### Context

D-024 closed Sprint 6 unbuilt. D-026 corrected its premise: the data is real, live, and reachable
through a RapidAPI listing that scrapes Cricbuzz. What remained was the price. The free plan is
**200 requests a month**, about 6.6 a day — roughly fifteen times *worse* than our main provider's
free tier, not better, which is the mistake this evaluation nearly made.

The instruction was to build on the free plan regardless. So the question stopped being "can we
afford live cricket data" and became "what can 200 requests a month actually buy".

### Decision

**Build the scorecard. Do not build commentary.**

The split is not a judgement about which is more valuable. It falls straight out of the
arithmetic:

- A **scorecard** is a whole innings in one request. Every batter, every bowler, every wicket.
  Once a match finishes it never changes again, so it is one request forever. A reader who opens a
  completed match's card costs one request and the next hundred readers cost nothing.
- **Commentary** is only worth anything if it keeps up. The spike measured nine balls in seventy
  seconds, so following one innings honestly is roughly 120 requests an hour. The entire monthly
  allowance is **three minutes** of one match. Commentary on this plan is not a reduced feature,
  it is a broken one.

Four consequences follow, each of which is in the code:

1. **Nothing polls it.** `LiveMatchPoller` stays on our main provider. The new source is only ever
   touched by a reader asking for something.
2. **It is behind a button.** Not a lazy load, an explicit press. Fetching on page open would mean
   every visit to every match page spends a request.
3. **A live card is cached for five minutes.** Slow for live sport, and deliberate: thirty seconds
   would empty the allowance before lunch on the first of the month.
4. **Sixty requests are reserved for finished matches.** Without a reserve, one live match takes
   everything — it keeps changing, so it keeps being worth re-fetching — and every completed
   scorecard for the rest of the month shows nothing. A finished match is worth one request ever,
   which is the best value in the whole budget, so it gets protected from the worst.

The budget is reconciled against the gateway's own `x-ratelimit-requests-remaining` on every
response rather than trusted from our own count, because the key is spent by anything holding it,
including a spike script.

### Costs

- **The source is an unlicensed scraper.** It is not a Cricbuzz product; Cricbuzz has never
  published a developer API. No SLA, no affiliation, and a history of going offline during major
  tournaments. This is the same objection D-024 raised about scraping Cricbuzz ourselves, and
  paying an intermediary to do it does not answer it. It is why the feature ships **disabled** and
  why the registration is conditional rather than dormant: a deployment has to mean it.
- **It drags Cricbuzz listing reads in with it.** Our ids are CricketData GUIDs, the source uses
  Cricbuzz integers, and nothing maps between them. Resolution reuses `CricbuzzMatchDirectory`,
  which scrapes the listing — free and cached for half an hour, but it means enabling scorecards
  enables that reading too. There is no way to use this source without identifying matches in its
  terms. See [D-020](#d-020).
- **404 covers four different situations** — disabled, unmatched, out of budget, upstream silent —
  and clients cannot tell them apart. That is intentional; the alternative is an API that reports
  our billing state.
- **The five-minute live cache will look wrong to anyone watching the match.** It is the honest
  consequence of the plan and is stated in `docs/api.md` rather than hidden.
- **Two instances would each believe they hold the whole monthly allowance**, and would overspend
  it together. The same limitation as [D-014](#d-014); the fix is a shared counter, not a cleverer
  local one.

### What would change this

A paid tier. At $9.99 a month the allowance buys about five and a half hours of live following a
day, which makes commentary a real feature rather than a broken one, and lets the live cache drop
to something that deserves the word. D-024's cost objection is unchanged and still nobody's to
overrule but the project owner's.

---

## D-026 — Sprint 6's data exists and is live; what stops us is cost, not availability

**Status:** accepted. Corrects the premise of [D-024](#d-024).

D-024 closed Sprint 6 on the finding that the data could not be had. That finding was true of
CricketData and false in general, and the difference matters enough to record properly: every
struck-out task in Sprint 6 is buildable from a source we can reach today. We are still not
building it, but the reason has changed from *cannot* to *will not pay*.

### It was measured, not read about

Four calls against RapidAPI's Cricbuzz listing, on a match in progress — Australia A v India A,
day two — using a key held in user secrets:

| | |
| --- | --- |
| First call | `340/8` at ball `1121`, `ismatchcomplete: false` |
| 70 seconds later | `342/8` at ball `1130` |

Nine balls bowled, and the payload moved with them. That rules out the explanation that mattered
most: this is not a snapshot cached when the match started, it is a live feed.

Commentary is there too, under `comwrapper[].commentary` rather than the `commentaryList` the
listing's documentation implies:

```json
{ "commtxt": "Tanush Kotian to Rocchiccioli, 1 run",
  "overnum": 109.6, "eventtype": "over-break",
  "oversep": { "score": 339, "wickets": 8, "oversummary": "0 0 0 1 1 1 ",
               "runs": 3, "batstrikerdetails": "119(205)" } }
```

The `oversep` block is `6.8` — over-by-over with a per-ball summary string and the striker's score
— a task CricketData has no field for at any tier. Scorecards, fall of wickets, partnerships and
extras all arrived in full on a separate finished-match call.

### The arithmetic is the whole decision

Nine balls in seventy seconds means following one innings honestly costs a call every thirty
seconds: 120 an hour.

| Plan | Cost | Calls per day | Hours of one live match |
| --- | --- | --- | --- |
| BASIC | free | ~6.6 | **3 minutes** |
| PRO | $9.99/mo | ~660 | ~5.5 |
| ULTRA | $29.99/mo | ~3,300 | ~27 |

The free tier is not a small budget, it is an unusable one: 200 calls a month buys under two hours
of a single match, once. PRO buys roughly one T20 a day with nothing spare for page loads. ULTRA is
the first tier that could actually run this site.

So the project spends nothing and stays on CricketData, as [D-025](#d-025) decided.

### What still stands, and what does not

No longer true, and struck from the record: that the data does not exist, that no source serves it,
and that the ceiling is technical.

Still true, and still sufficient on its own: the licensing objection in D-024. These listings are
reverse-engineered scrapers of Cricbuzz with no affiliation and no SLA, and this repository is
public. The frequency argument in D-024 is in fact *strengthened* by the measurement above — a
call every thirty seconds for the duration of every match is exactly the imposition that section
declined to make.

One implementation cost is worth writing down in case this is ever revisited. Every field in these
payloads is lowercase with no separators — `commtxt`, `overnum`, `strkrate`, `outdec`,
`batteamsname`, `inningsid`. The solution serialises with `JsonSerializerOptions.Web`, which is
camelCase, so a property named `StrkRate` would look for `strkRate`, find nothing, and bind zero
without raising anything. Every property of every one of these DTOs would need an explicit
`[JsonPropertyName]`, and a missing one fails silently as a plausible-looking number. That is a
mapping layer of real size, not a quick bind.

**What this costs:** nothing new. Sprint 6 was already closed. What changes is that the plan and
the decision log now say why honestly, so nobody re-derives a false conclusion about what cricket
data can be had.

---

## D-025 — The provider was reconsidered and kept; the alternatives were priced, not assumed

**Status:** accepted

CricketData stays. Two replacements were evaluated to see whether [D-024](#d-024) could be
reopened, and both were declined — one on quota, one on cost.

### Why it was reconsidered at all

The motivation was sound: our ceiling is 100 calls a day, and the provider serves no scorecard,
commentary or player statistics. A richer source would reopen Sprint 6 and possibly
[D-023](#d-023). So the question was not whether the current data set is limited — it plainly is —
but whether anything available is actually better.

### RapidAPI's "Cricbuzz Cricket": right data, unusable budget

The data is genuinely there. `/mcenter/v1/{matchId}/scard` serves scorecards, `/comm` serves
ball-by-ball commentary, `/overs` serves over-by-over and `/mcenter/v1/{matchId}/team/{teamId}`
serves the players in a match — every part of Sprint 6 that D-024 recorded as unavailable. It goes
further than that: `/stats/v1/player/search?plrN=` and `/stats/v1/rankings/{category}` mean player
search and ICC rankings are there too, so this would reopen [D-023](#d-023) as well as D-024.

Worth stating plainly, because it is the strongest argument against this decision: the data ceiling
described in D-023 and D-024 is not a fact about cricket data in general. It is a fact about the
free tier of one provider. Everything those two entries record as unavailable is available
somewhere, for money.

It was declined on the number, which is the part that had been assumed rather than checked:

| Plan | Cost | Requests |
| --- | --- | --- |
| BASIC | free | **200 per month** |
| PRO | $9.99/mo | 20,000 per month |
| ULTRA | $29.99/mo | 100,000 per month |

The free tier is 200 calls a *month* — roughly 6.6 a day, against the ~3,000 a month CricketData
already gives us. Switching to it in order to escape a quota would have cut the budget by about
fifteen times. A card is required even for the free plan.

Two further facts worth keeping, since they would apply again:

- These listings are **not Cricbuzz**. Cricbuzz has never published a developer API; the RapidAPI
  listings are reverse-engineered scrapers with no affiliation, no SLA, and a record of going
  offline during major tournaments — which is precisely when a live-scores site matters. Paying a
  reseller does not change what D-024 declined to do; it adds a middleman and a bill.
- `matchId` is an integer. So is a Sportmonks fixture id. `Slug.TryExtractId` requires a GUID, and
  the archive is keyed by them, so **any** provider change breaks every match route and strands
  existing history with no mapping across. That cost belongs to the swap itself, not to a provider.

### Sportmonks: right data, real price

Licensed, 99.98% claimed uptime, and one fixture call assembles `batting`, `bowling`, `lineup` and
`balls` — deliveries with commentary. A `/players` endpoint carries career statistics, which is the
exact thing D-023 closed player pages for lacking. The limit is 3,000 calls per hour per endpoint,
which is the difference between a scorecard that updates and one that is merely present.

Declined on cost. There is no permanent free cricket tier: the three plans are €29, €75 and €125 a
month, the 14-day trial needs a card, and the default free token reaches three leagues — T20I, Big
Bash and the CSA T20 Challenge — with no IPL, Tests or ODIs. Good enough to develop against, not
to launch with.

### What this costs

Sprint 6 stays closed and D-024 stands unrevised. Player pages stay unbuilt. The architecture keeps
every accommodation it made for a hundred calls a day — `DailyHitBudget`, `ReservedHits`, the
single retry, the circuit breaker, the poller that will not tick unless somebody is connected — not
as caution but as necessity.

The honest summary is that this project's data ceiling is a budget decision rather than a technical
one, and the budget is zero. That is a legitimate choice, and it is recorded here so the question
is not reopened on the assumption that something free and richer exists. It was looked for.

---

## D-024 — Sprint 6 is closed unbuilt, and Cricbuzz will not be scraped to fill it

**Status:** accepted, but read [D-026](#d-026) first — the claim below that this data cannot be had
was measured and found false. The conclusion stands; the reasoning for it does not.

Scorecards, ball-by-ball, commentary and match stats are not implemented. The provider refuses
them, and the one source that publishes them will not be read for this.

### The provider was asked directly, not inferred from a flag

`3.3` observed `bbbEnabled: false` and concluded the data was gated. That was a reading of the
match list, so before closing the sprint the endpoints that would actually serve it were called:

| Call | Result |
| --- | --- |
| `match_scorecard` | `status: "failure"` — *"Scorecard `90ae280c…` not found"* |
| `match_bbb` | `status: "failure"` — *"Not able to get BBB for match `90ae280c…`"* |
| `match_squad` | `status: "success"`, **0 squads** |
| `currentMatches` | `bbbEnabled: false` on every match, `hasScorecard` absent |

The refusals name the match and come back as explicit failures rather than empty successes, so this
is the provider declining rather than our mapping missing a field. Six matches across two spikes,
none with a scorecard.

Whether a paid tier would unlock it was not established and is a commercial question rather than a
technical one. Nothing here should be read as "the data cannot exist" — only as "this plan does not
serve it, and we asked."

### Cricbuzz has all of it, and that is not sufficient reason

[D-020](#d-020) already crossed this line for points tables: Cricbuzz's `robots.txt` disallows every
agent it has not named, ours is not named, and it is read anyway by a deliberate decision of the
project owner. Extending that to scorecards is not the same decision at a larger size, and the
difference is the part worth writing down:

- **Frequency.** A points table is read once per series and cached three hours. A live scorecard is
  worth having only if it is refreshed while play continues — so tens of requests per match, per
  match in progress, indefinitely.
- **Volume.** Commentary and ball-by-ball are the largest pages on that site, and they grow through
  an innings.
- **Character.** One cached read is a footnote. A live mirror of another site's match coverage is
  the thing their `robots.txt` exists to prevent, and it would make this project a re-publisher of
  their editorial work rather than a reader of a table of numbers.

A decision already taken once is not a licence to take it again wherever it would be convenient,
and the honest place to stop is before the imposition changes in kind. Sprint 6 is therefore closed
unbuilt rather than filled from a source that declined us.

**What this costs:** the match page has no depth beyond per-innings scores, the result sentence and
whatever the enrichment source supplies at the crease. That is the ceiling this data set allows, and
the page shows exactly it rather than tabs that lead nowhere — which is `6.13`, the one task in the
sprint that was ever achievable.

---

## D-023 — Teams are assembled from matches; players are not built at all

**Status:** accepted

`/api/teams` follows [D-021](#d-021--a-series-is-assembled-from-matches-not-fetched) and for a
stronger reason: **the provider issues no team identifier and has no team endpoint.** A match
names its two sides and that is the whole of it, so a team is not a record to fetch and decorate —
it is what the matches say, and nothing else could be true of it.

Identity therefore comes from the name, as a slug. That is weaker than the series case, where an
opaque `series_id` arrives free, and the weakness is real: a side that changes how its name is
spelled becomes a second team. The slug is at least stable under spacing and punctuation, and it
is the value `TeamDto.Id` already carried before any of this existed, so nothing new was invented.

**Unlike `SeriesId`, the team columns were backfilled.** Adding indexed columns to the archive
normally leaves existing rows empty — that is what happened for `SeriesId`, and it means history
is unreachable through the new column. Here the payload already held both sides, so the migration
extracted them with SQLite's `json_extract` rather than writing off every match archived before
today. Without it a team's first match would appear to be whenever the columns happened to be
added. This is the second provider-specific line in the project after the `NOCASE` collation, and
it is confined to a migration, which is the one place a SQL dialect is expected to show.

### There is no won-lost record, and its absence is the decision

The provider states a result only as prose — `"India won by 8 wkts"`, `"Match tied"`,
`"No result"`. Turning that into a record means parsing free text and then publishing the parse as
a team's history. The formats a team played and the sides it faced come from mapped fields and are
facts; who won does not, so the page reports what was played and says nothing about how it fared.
A wrong record would be indistinguishable from a right one to a reader, which is exactly why it is
not offered.

### Players: no source, so no feature

Tasks `7.3` and `7.11` asked for player endpoints and a player page with profile, batting, bowling
and recent matches. **Two calls established that none of it is available**, and the evidence is
worth recording because the tasks look reasonable until you look:

| Checked | Result |
| --- | --- |
| `players_info` fields | `id`, `name`, `country`, `playerImg` — **no stats of any kind** |
| `series_squad` | `data: []`, so no player is linked to any match we hold |
| `players?search=Kohli` | 10 hits: Aseem, Abir, Aryaveer, Shashwat, Smriti — **not Virat** |
| Mentions of `matchId` or `recent` in a player payload | 0 |

So a player cannot be reached from a match, a match cannot be reached from a player, and a player
profile would hold a name and a flag. The response came back `status: "success"` rather than as a
plan rejection, so this is what the endpoint returns rather than something a paid tier is
withholding — though that distinction is worth re-testing if the plan ever changes.

Building the pages anyway would mean inventing statistics, which is the one thing this project does
not do. The tasks are recorded as blocked with this evidence instead, and search says plainly that
players are not searchable rather than returning an empty group and letting the reader wonder.

**What this costs:** `7.14`'s "match to team to player" stops at the team. Cross-entity navigation
is match ↔ team ↔ series, which is every edge the data actually supports.

---

## D-022 — A tally excludes what the caller already counted

**Status:** accepted

`GetSeriesTalliesAsync` and `GetTeamTalliesAsync` take the ids the caller is counting from the
provider window and leave those rows out of the aggregate.

A match that finished minutes ago is in **both** the window and the archive. The services add a
window-derived tally to a SQL-derived one, so without this a recently-finished match is counted
twice and a series or team claims more matches than it has. The bug was silent: the number is
plausible, only wrong, and it grows worse the more often the poller runs.

The exclusion list is the window — a few dozen ids — so it is a short `NOT IN` rather than
anything needing a temporary table, and an empty list adds no clause at all.

`GetSeriesNamesAsync` passes an empty list deliberately: it reduces to a distinct set of names, so
a duplicate costs nothing and `Distinct` already removes it. Nothing there is counted.

---

## D-021 — A series is assembled from matches, not fetched

**Status:** accepted

`/api/series` is derived from the matches already in hand — the provider's current window plus the
archive — and costs no call beyond the one every other list already shares.

**The provider's series endpoints were measured first, and they are an index rather than data.**
Four calls, spent deliberately:

| Endpoint | Returned |
| --- | --- |
| `series` | 25 per page: id, name, start/end, format counts |
| `series_info` | an info block and an empty `matchList` |
| `series_squad` | `data: []` |
| `players` | id, name, country, and nothing else |

Three findings decided it. **`endDate` was never an ISO date** — 0 of 25, always `"Apr 11"` with
no year, and in the 7 cases where `startDate` was ISO the two fields disagreed on format *inside
the same object*, so the year would have to be inferred from the series name. **`squads` was 0 for
all 25**, which is why `series_squad` came back empty. And 18 of 25 declared ODI, T20 or Test
counts while reporting `matches: 0`. Reading any of this would spend from a hundred-a-day budget
to learn less than the matches already in memory can say.

**`series_id` is the one field worth taking, and it arrives free** on every match. It is what
makes a series identifiable: the name reaches us as the tail of a free-text field, and two matches
of one series do not reliably spell it the same way, so grouping by name would split a series in
half. Ids group; names display.

**`matchCount` means matches we hold, and the UI says "held" rather than implying completeness.**
A tournament that started before the archive did will show a fraction of what it played. That is
the same limitation the results page already states about its own history.

**Adding `SeriesId` as a `required` member broke every row already archived,** which a live run
caught before this shipped: `MatchDetailsDto` is what the archive stores as JSON, and a required
member is one that no previously written payload has. Every archived match failed to deserialise
and silently vanished from results. The field is now optional with a default, and a regression
test writes a payload without it. **Anything added to `MatchDto` from now on needs a default for
the same reason** — the archive's durability is the whole point of it.

---

## D-020 — Standings are read from Cricbuzz, against its robots.txt

**Status:** accepted, deliberately and with the cost understood
**Supersedes part of:** D-015

A points table is read from Cricbuzz series pages. Off by default, behind its own switch.

**Standings cannot be derived and cannot be bought.** CricketData publishes no table at all — the
full `series_info` payload was searched for `points`, `standing`, `table`, `nrr`, `wins` and
`losses`, and matched none of them. Computing one from results would mean encoding each
competition's points rules, and the County Championship alone awards bonus points for batting and
bowling. A derived table is a guess presented as a standing, which this project does not do.
Cricbuzz publishes one: the Ranji Trophy Elite table is 740 KB of server-rendered HTML, four
groups, no JavaScript assembly.

**Cricbuzz's `robots.txt` disallows us, and we are proceeding anyway.** The file reads:

```
User-agent: *
Allow: /ads.txt
Disallow: /
```

It then grants broad access to roughly eighteen *named* agents — Googlebot, Bingbot, Applebot,
Twitterbot, AmazonAdBot and others. Ours is not among them. This is not a site that failed to
consider automated readers; it is one that decided which ones it wants. The judgement taken here
is that `robots.txt` is a crawling convention rather than a licence term, and the project owner
made that call with the file in front of them.

**What is not on the table is sending a user agent the file permits.** Declining a stated
preference and impersonating someone granted an exception are different acts, and only the first
is being done. We identify ourselves as `cricket-live/1.0` with no `Referer` and no `Origin`,
exactly as D-015 established, and a test asserts it.

**The obligations this creates are in the code, not in good intentions:**

- **Off by default,** under `Cricbuzz:StandingsEnabled`, separate from the switch that governs
  batters because it is a separate and larger decision. When off, the reader is not registered at
  all rather than registered and dormant.
- **Cached for three hours,** not seconds. A table changes when a match finishes.
- **No retry, ever.** A page that did not answer is not an invitation to ask again.
- **One listing read serves every series**, so resolution is a request per cache period rather
  than per page view.
- **The live test is opt-in** behind `CRICKET_LIVE_LIVE_TESTS=1`, so CI does not send this traffic
  from every branch on every push.

**A wrong table is worse than no table,** so the parser is built to decline. It reads the header
rather than assuming column positions, because competitions differ — Ranji publishes P, W, L, NR,
Pts and NRR with no tie column, and a fixed layout would shift every value after the missing one.
A row whose numbers do not parse is dropped rather than defaulted to zero, since a zero is a claim
about a result. Anything unreadable becomes an empty list, which renders as no section at all.

**Cost:** this reads a presentation detail of someone else's HTML and will break without warning.
The tests against a captured page are the alarm.

**Reopen this when:** Cricbuzz blocks an honest agent — in which case stop, per D-015 — or a
source appears that publishes standings under terms that permit it.

---

## D-019 — Routes live in the fragment

**Status:** accepted
**Extends:** D-018

`createHashRouter` rather than `createBrowserRouter`. URLs read `/#/matches?status=completed`.

**A path router needs the host to cooperate.** Every unknown path has to be rewritten to
`index.html`, or reloading `/matches` is a 404 from the server before the app ever loads. That
rewrite is a different setting on every host — a `_redirects` file, a `try_files` directive, a
rewrite rule — and it is easy to deploy without. When it is missing, what breaks is reload, shared
links and the back button, which are precisely the three things D-018 put filter state in the URL
to preserve. The fragment is never sent to the server, so none of it can go wrong.

**Search params still work.** They live inside the fragment and `useSearchParams` reads them
unchanged; a deep link with two filters was verified restoring both.

**Cost, and it is a real one: crawlers.** Search engines index the path, not the fragment, so every
route collapses to one indexable URL. For a public scores site that is a genuine loss, and it is
the reason this would be worth revisiting. Reopen if organic search becomes a goal, or when the
deployment target is known to rewrite reliably — the change is one function name and nothing else,
because no code reads `window.location` or builds a path by hand.

Nothing else was affected: links are `<Link to="/matches">` and react-router adds the `#` itself,
and the API base URL is unrelated to how the app routes.

---

## D-018 — One filter, applied twice, and dates cross the wire as instants

**Status:** accepted
**Extends:** D-017

Matches live in two places now — the provider's window in memory and the archive on disk — and
filtering has to mean the same thing in both.

**The filter is described once and applied twice.** `MatchFilter` carries the four conditions and
owns the in-memory predicate; `SqlMatchArchive` translates the same conditions into a `WHERE`. Two
implementations were unavoidable, because filtering the archive after reading a page gives a page
with holes in it — ask for twenty, discard nine, serve eleven, and the count no longer agrees with
what came back. What was avoidable is two *descriptions*, which is how a LINQ predicate and a SQL
clause end up quietly disagreeing about case sensitivity. The tests for the two halves cover the
same cases deliberately.

**Dates cross the wire as instants, not as a date.** A calendar day is a different interval in
every timezone: the same match starts on the 27th in London and the 28th in Sydney. A server given
`date=2026-09-27` has to guess whose 27th that is, and any guess is wrong for most readers. The API
therefore takes `from` and `to` as instants and the browser — the only participant that knows the
reader's timezone — converts its local day into them. The range is half-open so consecutive days
abut without overlapping or leaving a gap, and a match starting exactly at midnight belongs to the
later day only.

**Series is matched whole, not as a substring.** `tour of India` would pull in every touring series
at once, which is a filter quietly becoming a search and returning more than was asked for.

**Case-insensitivity comes from the column's collation, not from the comparison.** `NOCASE` on
`SeriesName` keeps the match index-backed while agreeing with `OrdinalIgnoreCase` in memory.
`LIKE` was the obvious alternative and is the wrong tool twice over: a series named `100% Cricket
League` would become a wildcard matching everything, and `LIKE` is case-insensitive in SQLite but
case-sensitive in PostgreSQL. This is the one provider-specific line in the model, and it is in the
model precisely so the PostgreSQL swap has one place to look.

**The series list is read from the rows, never held as a list**, and drawn from both sources since
neither is a superset of the other. The filter can then only offer selections with something behind
them.

**Contradictions are answered, not rejected.** Asking the archive for live matches, or `/upcoming`
for live ones, returns empty — a coherent question with an empty answer. The second case returns
empty *without calling the provider*, which matters because provider calls are the budgeted
resource and a client sending one filter to all three lists should not spend calls on the two it
excluded. An inverted date range is different and returns **400**: it selects nothing, and serving
an empty list for it would look like an answer.

**Filter state lives in the URL.** A filtered view is then linkable, reloadable and reachable with
the back button. Component state would break all three silently. Changes `replace` rather than
`push`, so adjusting a dropdown does not bury the previous page under a dozen history entries.

**Cost:** four new query parameters on three endpoints, one new endpoint, and a migration for the
collation. A filter naming a series that has since aged out of both sources shows an empty list;
the filter bar keeps the name visible as an option so the reader can see what they are filtered to
rather than facing a blank dropdown above nothing.

---

## D-017 — Results are kept in a SQLite file, and only from today forward

**Status:** accepted
**Extends:** D-012

The provider's current-matches window is a few days wide, so "recent results" meant "results since
the day before yesterday" and nothing more. A match that finished last week was gone, and so was
its match page, because the only place we had ever held it was the window.

**We keep what passes through rather than fetching history.** No source available to us can supply
completed matches with results. CricketData's `recent-matches` route was measured and returns the
same short window under a different name; Cricbuzz's own listing mixes live and upcoming fixtures,
carries no result sentence in the one part of the page we are willing to read, and dates entries
relatively ("Today", "Yesterday"). Anything we presented as older history would therefore have been
assembled from fragments, and a wrong result is worse than a missing one.

So the archive **accumulates forward**. It began empty on the day it shipped. Matches played before
that cannot appear, which is stated on the results page rather than hidden, and `total` in the API
is explicitly "what we hold" rather than "what was played".

**SQLite because it is a file.** Nothing to install, nothing to run alongside the API, nothing to
provision. A few thousand finished matches a year is not a workload that needs more. PostgreSQL is
still the deployment target from D-004; `IMatchArchive` is the seam, so that swap changes one
registration and the migration, and nothing above Infrastructure.

**Each match is stored whole, as JSON, beside a handful of indexed columns.** The columns — id,
slug, start time, series — are the ones we sort, page and look up by. The payload is the entire
serialised match and is what gets returned. Normalising instead would mean deciding today how every
field maps, and any field mapped carelessly would be silently lost for good; this way an archived
match reads back byte-identical to a live one, and a column can be promoted out of the payload
later without a backfill.

**Archiving is a decorator over the data provider, not a step inside it.** Fetching cricket and
keeping cricket are different jobs. It is deliberately not in the live poller either, because the
poller only runs while somebody is watching a live match — history would then depend on whether
anyone happened to be watching. Every window fetch passes through the decorator, so anyone opening
the site contributes.

**A failed write never fails a read.** The decorator logs and returns the provider's data. Losing a
match from history is a much smaller harm than a home page that will not load, and the results
endpoint separately merges any finished match the window still holds but the archive does not, so
a failed write costs history rather than today's results.

**Cost:** `GET /api/matches/recent` is now paged and returns an envelope instead of a bare array,
which is a breaking change to that one endpoint. A finished match is never rewritten, so a later
provider correction to a completed match will not be picked up — accepted, because a finished match
does not change, and keeping what we recorded at the time is the more defensible of the two.

---

## D-016 — Matches are paired on title and series, or not at all

**Status:** accepted
**Extends:** D-015

Enrichment needs to know which Cricbuzz match is which of ours, and the two providers share no
identifier. The first version required somebody to write each pair into configuration, which meant
no match was ever enriched unless a human had been watching.

Measuring a real listing of 26 Cricbuzz matches against our window settled how to do better, and
also ruled out the obvious approach:

**The team pair is not a key.** `ind` appeared on three of the 26 fixtures, `skr` on four, and two
were `tbc-vs-tbc` because Cricbuzz lists finals before the finalists are known. Our own
`IND v WI, 1st ODI` matched two entries on teams alone — the 2nd and 3rd ODIs, **both wrong**,
because the 1st had already dropped off the listing. That join does not fail; it puts another
match's batters on the page and looks right doing it.

**The slug tail is a key, and we can reproduce it exactly.** Cricbuzz links read
`/live-cricket-scores/151543/ind-vs-wi-2nd-odi-west-indies-tour-of-india-2026`. Everything after
the teams is the match title and series name, and slugifying CricketData's own `matchTitle` and
`seriesName` produces the identical string once punctuation is dropped. Across the 26 fixtures this
gave 24 distinct values, the only two collisions being group-stage fixtures sharing "Pool A" and
"Pool B" within one tournament — and those have different teams, so teams plus tail is unique
across the whole listing.

So: exact match on title and series; teams consulted only to break a collision, because the two
providers do not always abbreviate alike and demanding agreement up front would reject good matches.

**A unique answer or no answer.** Zero candidates means no enrichment. Two candidates the teams
cannot separate means no enrichment. There is no best guess, because declining costs two player
names and guessing tells the reader something false about a match they are watching.

**Cost:** one request for a listing that serves every match, cached for thirty minutes, rather than
one request per match. Which fixtures exist changes over hours; only the scores move quickly, and
those are fetched separately. `Cricbuzz:MatchIds` survives as an override for when resolution
declines, and `Cricbuzz:AutoResolve` turns the whole mechanism off independently of `Enabled`,
because it is a separate risk from reading the site at all.

**Verified** against live Cricbuzz: resolution found `155422` for "2nd unofficial Test / Australia A
tour of India 2026" from the title and series alone. The unit tests use the real 26-match listing,
including both `tbc-vs-tbc` fixtures and the Pool A collision.

---

## D-015 — Batters come from a second source that is off by default

**Status:** accepted

The question was whether to replace CricketData with a self-hosted scraper to escape the hundred
calls a day. The answer is no, and the reason is structural rather than a matter of taste: the
scraper proposed (`mskian/live-cricket-score-api`) exposes exactly one route, `GET /?score={id}`,
which needs a Cricbuzz match id. It cannot answer "what is on right now", so it cannot implement
`GetCurrentMatchesAsync`, so it cannot be a replacement for anything.

It was run anyway, against real pages, because arguing from a README is not evidence. Of five
matches sampled, none returned fully clean data and every failure was reported as HTTP 200 with
`"status": "success"`:

- team abbreviations over four letters were silently truncated — `INDWA 136/4` came back as `NDWA`
- Test matches, with two innings in the title, could not be parsed at all
- every bowler name carried page furniture, as in `Tushar Deshpande View match performance View profile`
- a lone batter was discarded entirely, because it kept results only in pairs
- names were HTML-escaped rather than decoded, so an O'Brien would reach the browser as `O&#x27;Brien`

One field survived: the batters at the crease, which is also the one thing CricketData does not
give us. So that field, and only that field, is taken — from a C# port rather than by running the
Python service, because the useful logic is about forty lines and porting it costs no new runtime,
no third process, and lands the parsing in the test suite where a markup change fails CI instead of
quietly returning placeholder text to users. The five defects above are fixed in the port and each
has a test.

**Only the `og:title` meta tag is read, never the page body.** That is what makes the bowler
pollution impossible rather than merely fixed: a meta tag has no link labels next to the name.
Anything that cannot be taken from that tag belongs to the primary provider.

**We identify ourselves honestly.** The original sends a browser user agent with `Referer` and
`Origin` set to cricbuzz.com so its traffic reads as the site's own. That is evasion and it is not
reproduced; a test asserts we send neither header. Cricbuzz was verified to answer an honest agent
with HTTP 200, so the disguise bought nothing anyway. If an honest agent is ever blocked, that is
an answer about whether the data is ours to take, and the response is to stop rather than to hide.

**It ships disabled.** Whether to read a public website is a judgement about someone else's terms,
not a technical default, so `Cricbuzz:Enabled` is `false`.

**Matches are paired on title and series, and never by guesswork.** See D-016.

**Cost:** the port reads a presentation detail of someone else's HTML and will break without
warning. The tests are the alarm, and the feature degrades to absence rather than to error — an
empty list renders as no section at all, never as "nobody is batting", because those are different
claims and we only know the first.

**Reopen this when:** Cricbuzz blocks an honest agent, the parser tests start failing for reasons
other than our own changes, or a provider appears that supplies batters under terms that permit it.

---

## D-014 — Live state stays in process; Redis waits for a second instance

**Status:** accepted
**Amends:** D-003, which assigned live state to Redis
**Applies:** D-006, which provisions infrastructure in the sprint that needs it

Sprint 5 was planned as Redis plus a poller plus SSE. It shipped the poller and SSE, and no Redis.

Redis earns its place when there is something to share between processes. There is one API
instance, nothing is deployed, and the sprint's own hardest exit criterion — one live match
produces one provider poll regardless of how many clients are connected — is met by a background
service and an in-memory subscriber list. Adding Redis today would introduce a network hop, a
serialization format, a connection to supervise and a service to run, in exchange for nothing
observable.

The seam is where it needs to be. `IMatchBroadcaster` is the only thing the poller and the stream
endpoint know about, so the day a second instance exists, a Redis-backed implementation replaces
`MatchBroadcaster` and nothing else changes.

**Reopen this when any of these becomes true:**

- more than one API instance runs, so a client connected to A must see a poll made by B
- live state has to survive a restart rather than being re-fetched
- the subscriber list outgrows what one process should hold in memory

**Cost:** a restart drops every open stream. Clients reconnect with backoff and the endpoint sends
current state on connect, so the visible effect is a brief "Reconnecting…" rather than a blank
page. Nothing durable is lost, because nothing here is the system of record.

---

## D-013 — A section the data cannot support is removed, not stubbed

**Status:** accepted
**Follows from:** D-012

The Sprint 3 spike found that the provider carries no toss, no editorial summary, and no batters or
bowler at the crease. Sprint 2 had built all four, because Sprint 1 assumed a provider would supply
them. The choice was to keep them behind empty states or delete them.

They are deleted, along with the Scorecard, Commentary and Stats tabs that had never held anything.
An empty state is a promise that content belongs there and is temporarily absent. When it is
permanently absent, the promise is false, and a page that keeps making it reads as broken rather
than as finished. A match page with a header, an innings breakdown and match information is
complete; the same page with three tabs saying "arrives in Sprint 6" is the same content with a
notice that it is not.

This applies to real gaps too, not just permanent ones. If the provider has no venue for a match we
show nothing in that line rather than "Venue: unknown", and a team with no crest gets no
placeholder shape — the row reads the same either way.

**Cost:** UI gets deleted and later rebuilt. The Sprint 6 scorecard work now starts from nothing
instead of from a tab that already existed. That is the right trade: the tab cost about an hour and
was misleading every day it shipped. It also means a screen cannot be built more than a sprint
ahead of the data behind it, which is a constraint on planning, not just on code.

---

## D-012 — CricketData replaces SportScore as the cricket provider

**Status:** accepted
**Supersedes the provider named in:** D-002, `project-plan.md`

`project-plan.md` names SportScore. The Sprint 3 spike measured it and four alternatives against
real responses, and SportScore turned out to be unusable for cricket.

What was measured, not read off a landing page:

| Provider | Free tier | One call returns all live scores | Innings with overs | Verdict |
| --- | --- | --- | --- | --- |
| **CricketData / CricAPI** | 100/day, permanent | yes | yes | **chosen** |
| SportScore | keyless, ~10k/day | yes | no — one string, `"54/7"` | rejected |
| Cricwix | 7-day trial, 100/day | — | — | rejected |
| Big Balls Data | 250/day (500 with GitHub) | no — one call per match | yes | rejected |
| Roanuz | limited trial | — | — | rejected |

SportScore returns `incidents: []`, `stats: []` and `lineups: null` on every live cricket match, has
no venue, no format and no over count, and reports a Test match's score as `"-"`. Its `status_text`
— the field our design treats as the provider's authoritative sentence — returns values such as
`"Abnormal"` and `"Cut in half"`. Cricwix advertises 1,000 calls a day with "no trial clock" and
delivers a seven-day trial at 100. Big Balls Data has excellent scorecards but serves live state one
match at a time, so a five-match evening exhausts its allowance before lunch.

CricketData wins on one structural property rather than generosity: `/currentMatches` returns every
match in the window with per-innings runs, wickets and overs **in a single call**. Our cost per
refresh is therefore flat no matter how much cricket is being played, which is the only reason a
hundred calls a day is survivable. `/api/matches/live`, `/upcoming` and `/recent` are partitions of
that one response, not three upstream requests.

**Cost:** a hundred calls a day caps refresh at roughly five minutes, which Sprint 5 must design
around rather than against. The provider states its free data is "always a few minutes behind
real-time" regardless of plan, so paying would raise volume without improving freshness. Ball-by-ball
is gated behind a `bbbEnabled` flag that was `false` on every match observed, so Sprint 6's scorecard
and commentary scope is not yet supported by any evidence.

---

## D-011 — The reference documentation was adapted, not adopted

**Status:** accepted

The `docs/` set began as a copy of another project's documentation. It was rewritten for Cricket
Live rather than followed, because several of its rules encode that product's constraints rather
than general truth.

Kept: the layering discipline, the no-`any` rule, the deliberate loading/empty/error states, tokens
defined once with no hardcoded colour in a component, the decision-log habit, and the manual
verification checklist.

Not kept, and why:

- **Its frontend layout** (`lib/`, `components/ui/`, `<feature>.api.ts`, a service layer between
  hook and API). `project-plan.md` specifies a structure, Sprints 1 and 2 are built on it, and a
  service layer would currently hold nothing — our business rules are score formatting, which lives
  in a feature's `utils/`.
- **Its icon rule** (one hand-written `icons.tsx`, no library). `project-plan.md` picks Lucide, and
  a cricket app does not have a bespoke icon language to protect.
- **Its 40px control cap.** That product is a desktop shell; this one is watched on a phone, so the
  primary target is 44px on mobile and tightens from `sm`.
- **Its "no tests unless asked" rule.** See D-010.

**Cost:** two documents in `docs/` now describe a system we have not finished building, and they
have to be kept honest sprint by sprint rather than written once.

---

## D-010 — Existing tests stay, the suite does not grow until Sprint 8

**Status:** accepted

The backend test project and its CI gate remain and must keep passing. No new tests are added until
Sprint 8, with one exception: the provider mappers in Sprint 3.

Sprint time in the early sprints buys more from working software than from coverage of code that is
still moving. The mappers are the exception because they are pure functions over a shape we do not
control, they are the single most likely place for a provider change to break us silently, and the
captured fixtures needed to test them are a Sprint 3 deliverable anyway.

**Cost:** regressions in the middle sprints are caught by hand or not at all, and Sprint 8 will
have more to write than if tests had accumulated along the way.

---

## D-009 — UI is built against mock data behind the real function signatures

**Status:** accepted

Sprint 2 builds the interface before any provider exists. The fixtures live in the feature's
`mocks/`, but they are served through the functions the API layer will keep —
`getLiveMatches(signal)` returning `Promise<Match[]>` — resolving after a delay and honouring the
abort signal.

The alternative, importing fixtures directly into components, is quicker and leaves a rewrite in
Sprint 4. This way the loading and error states are exercised for real, cancellation works, and
when the endpoint lands only the function bodies change.

It also inverts a risk usefully: `features/matches/types.ts` becomes the contract the backend has to
meet, written from what the screens actually need rather than from whatever shape the provider
happens to return.

**Cost:** the types were written before anyone had seen a provider response, so Sprint 3 had to
reconcile them — and where the provider cannot fill a field, the screen that assumed it has to
change. In the event they held up well: runs, wickets, overs, venue, format and team short names
all exist. Toss, summary, current batters and current bowler do not, and those sections come out.

---

## D-008 — HTTPS redirection is enabled outside Development only

**Status:** accepted

The Vite dev server calls the API over plain HTTP. With redirection on everywhere, that call was
sent to a self-signed HTTPS endpoint and the fetch failed with a certificate error that looks
nothing like its cause.

Production still redirects, because there the certificate is real and the redirect is a control.

**Cost:** a developer running the API in Development can reach it over HTTP, which is the point but
also means the production transport path is not exercised locally.

---

## D-007 — Linting is oxlint, not ESLint

**Status:** accepted

`project-plan.md` lists ESLint. The Vite React template ships oxlint as of `create-vite` 9, so
oxlint is what a new project gets with no configuration. It is substantially faster and covers the
rules we actually rely on.

This was the zero-configuration path rather than a considered preference. Moving to ESLint is a
package install and a config file if a rule we need turns out to be missing.

**Cost:** a smaller rule ecosystem than ESLint, and any rule that exists only as an ESLint plugin
is unavailable.

---

## D-006 — Redis and PostgreSQL are provisioned in the sprint that needs them

**Status:** accepted

`project-plan.md` lists both under Sprint 1 foundation. Neither was added: Redis arrives in Sprint 5
with the live engine, PostgreSQL in Sprint 7 with persisted teams and players.

Infrastructure that nothing reads from is not foundation; it is a service that can fail, a
connection string to manage, and a container to run, in exchange for nothing until several sprints
later. The foundation that mattered — the projects, the envelope, the pipeline, CI — is in place,
and adding a data store to a working system is a contained change.

**Cost:** Sprints 5 and 7 each carry provisioning work that would otherwise have been done once at
the start, including whatever free-tier setup the hosting provider requires.

---

## D-005 — Every endpoint returns the same envelope

**Status:** accepted

Responses are `{ success, data, message }`, with an optional `errors` array for validation. `success`
is the only thing that decides whether a call worked, so `data: null` on a success means "nothing to
return" rather than "something went wrong".

The frontend unwraps this in exactly one place, `services/apiClient.ts`, and throws `ApiError`
carrying the message and status. No screen parses a response shape.

The exception middleware deliberately does not write the envelope once a response has started. An
SSE stream is mid-flight by definition, and wrapping a half-sent stream in JSON produces something
no client can parse; the exception is rethrown instead and the connection drops, which is what a
streaming client is already built to handle.

**Cost:** an envelope is a little more ceremony than returning the object directly, and a caller
that ignores `success` and reads `data` will silently see `null` instead of an error.

---

## D-004 — We poll, detect change, and fan out; we do not pretend to be real-time

**Status:** accepted

A background service polls the provider, compares the result with the last known state, writes
Redis, and pushes over SSE only when something changed.

The system cannot be fresher than the provider, so the design makes that limit explicit rather than
hiding it. Change detection keeps an unchanged over from waking every connected browser, polling
backs off when nothing is live, and the interval is set from the provider's measured update
frequency rather than a guess — which is why measuring it is a Sprint 3 deliverable.

The user-facing consequence is a rule in the design system: screens say when they were last updated
and never claim ball-by-ball immediacy.

**Cost:** a fixed floor on latency that no amount of work on our side can lower, and a polling loop
that must be tuned per provider rather than a push we are simply handed.

---

## D-003 — Redis holds live state; PostgreSQL is the system of record

**Status:** accepted

Redis holds current scores, live match state, recent commentary, and cache entries under the key
scheme in `project-plan.md`. PostgreSQL holds what we persist: teams, players, competitions, venues,
and match metadata.

Redis is not the database. Anything in it can be rebuilt from the provider or from PostgreSQL, which
is what makes it safe to run on a free tier that may evict or restart.

**Cost:** two data stores to operate, and the discipline of deciding which one owns each piece of
data every time a new one appears.

---

## D-002 — The cricket provider sits behind `ICricketDataProvider`

**Status:** accepted

`ICricketDataProvider` lives in `Application`. The implementation and every provider response model
live in `Infrastructure/<Provider>`, and those models are `internal` to that project — the API
cannot reference them even by accident.

A provider's free tier, coverage, and commercial terms can all change, so a swap is a question of
when rather than if. Behind the interface it is a new implementation and a registration change;
without it, it would reach the controllers and then the frontend. D-012 is that swap happening
before the first line of provider code was written, which is the cheapest moment it could have.

The mappers are where the provider's quirks are absorbed, which is also why they are the one thing
we test before Sprint 8 (D-010).

**Cost:** an interface, a set of DTOs, and a mapping layer that a direct call would not need, plus
the ongoing discipline of not letting a convenient provider field leak through because it is easier.

---

## D-001 — Live updates use Server-Sent Events

**Status:** accepted

Scores reach the browser over SSE rather than WebSockets or SignalR.

The traffic is one-directional: the server has new scores, the client has nothing to say back. SSE
is plain HTTP, reconnects on its own, needs no protocol upgrade, and passes through infrastructure
that sometimes refuses websockets. A cricket scoreboard is the shape of problem SSE was designed
for.

**Cost:** one-way only, so anything interactive later needs a second mechanism; a per-connection
limit per domain on HTTP/1.1; and long-lived responses that some hosts buffer or terminate — which
is why Sprint 5 verifies the production host rather than leaving it to deployment.
