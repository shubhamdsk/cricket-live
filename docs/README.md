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

Sprint 1 is complete and merged: the repository, the React application shell, the ASP.NET Core API
split into four projects, the response envelope, CORS, global exception handling, `GET /api/health`,
and CI for both halves. Sprint 2, the design system and mocked pages, is in progress on
`feature/ui-foundation`.

No cricket data provider is connected yet, and neither Redis nor PostgreSQL is provisioned. Both
arrive in the sprint that first needs them — Redis in Sprint 5, PostgreSQL in Sprint 7. Current
status always lives in [sprint-plan.md](./sprint-plan.md).

## Documents that do not exist yet

`api.md`, `security.md`, `architecture.md`, and `system-design.md` describe a system that is mostly
still ahead of us. They record what is true today and grow as each sprint lands, rather than
describing a system we have not built. A provider investigation and a production-readiness review
will be written when there is something to investigate and review.
