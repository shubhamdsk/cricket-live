# Cricket Live — Documentation

Three sources, three jobs: the **project plan** decides what we build, the **sprint plan** decides
the order, and the **codebase** is the current state. Standards decide how. Read all three before
making an architectural decision.

| Document | Answers |
| --- | --- |
| [`../project-plan.md`](../project-plan.md) | What the product is. Vision, features, target architecture, roadmap. Source of truth for scope. |
| [sprint-plan.md](./sprint-plan.md) | The execution roadmap, sprint by sprint, with status and exit criteria. |
| [engineering-standards.md](./engineering-standards.md) | How we write code. Binding for every sprint. |
| [frontend.md](./frontend.md) | Frontend standards: structure, layering, state, routing, styling, accessibility, verification. |
| [backend.md](./backend.md) | Backend standards: structure, layering rules, results and errors, provider isolation, conventions. |
| [design-system.md](./design-system.md) | Tokens, components, icons, states, and writing tone. |
| [system-design.md](./system-design.md) | How the system is designed at the upper level: context, flows, scale, failure behaviour, trade-offs. |
| [architecture.md](./architecture.md) | How the code is actually laid out today: projects, layers, ports, local run commands. |
| [api.md](./api.md) | The implemented HTTP surface, its conventions, and the client rules. |
| [security.md](./security.md) | Threat model, controls in place, tracked gaps, per-sprint rules. |
| [decisions.md](./decisions.md) | Why things are the way they are. Reversing a decision means adding an entry. |

Where a standards document and `project-plan.md` disagree on *what* to build, the project plan wins.
Where they disagree on *how*, the standards win.

## Where we are

Sprints 1 to 5 are complete and merged. The API is split into four projects with a shared response
envelope, CORS, global exception handling and CI on both halves; real cricket data reaches our own
DTOs through `GET /api/matches/live`, `/upcoming`, `/recent` and `/{matchId}`; every screen renders
that data with no mock data remaining; and a match in progress streams to the browser over SSE at
`GET /api/matches/{matchId}/stream`.

Redis was planned for Sprint 5 and deliberately not built — with one API instance there is nothing
to fan out across, and `IMatchBroadcaster` is the seam that lets it drop in later
([D-014](./decisions.md)).

One thing is worth knowing before reading further: the provider is **CricketData, not SportScore**
as `project-plan.md` says — the Sprint 3 spike rejected SportScore and the reasoning is in
[D-012](./decisions.md). A consequence of that spike shows up in the UI, not just the backend. The
provider carries no toss, no editorial summary, and no players at the crease, so the match page
sections that assumed them were removed rather than stubbed; see the Sprint 4 notes in
[sprint-plan.md](./sprint-plan.md).

Neither Redis nor PostgreSQL is provisioned. Both arrive in the sprint that first needs them — Redis
in Sprint 5, PostgreSQL in Sprint 7. Current status always lives in
[sprint-plan.md](./sprint-plan.md).

## Documents that do not exist yet

`api.md`, `security.md`, `architecture.md`, and `system-design.md` describe a system that is mostly
still ahead of us. They record what is true today and grow as each sprint lands, rather than
describing a system we have not built. A provider investigation and a production-readiness review
will be written when there is something to investigate and review.
