# PokeScout

PokeScout is a web platform for Pokémon TCG players that combines **collection tracking**, **deck building**, and a **meta hub** (think “LoL champion page” vibe for archetypes).

---

## Features

### Inventory Tracker (My Collection)
Track the cards you own and instantly see what decks you can build.
- Add/remove cards + quantities
- Card search (name / set / rarity)
- Inventory summary (counts, favorites)
- Planned: condition tracking, organization tags, pricing (later)

### Deck Builder
Build decks manually or import/export decklists, then compare against your inventory.
- Create/edit/delete decks (format-aware later)
- Import/export decklist text
- Basic validation (60 cards; legality rules later)
- **Build with my inventory** → missing cards + completion %
- Planned: substitutions, templates/auto-fill, shareable public deck pages

### Recommendations
Start from an archetype/template and generate what you need to finish it.
- Pick archetype → load recommended list
- Compare to inventory → missing cards + shopping list
- Planned: “best decks this week”, “best decks you can almost build”, budget constraints (later)

### Meta Hub (Archetypes + Content)
Archetype pages with curated info and content.
- Archetype list + detail pages (overview, core list, flex/tech choices)
- Meta snapshots (manual at first)
- Posts/articles + YouTube embeds
- Planned: automated tournament ingestion, trend charts, matchup/win-rate stats where reliable

### Stats (Two Tracks)
- **Tournament-derived stats** (only when credible data exists)
- **Personal stats**: manual match logging → “my matchup chart”

---

## Architecture

### Solution Projects
- `PokeScout.Api` — ASP.NET Core .NET 9 Web API (cards, inventory, decks, archetypes, meta, auth)
- `PokeScout.Web` — Blazor Web App (UI)
- `PokeScout.Worker` — background jobs (imports, caching, cleanup)
- `PokeScout.Shared` — shared DTOs/contracts/utilities

### Data Stores
- PostgreSQL (source of truth)
- Redis (optional caching)

---

## API Docs (Scalar)

- OpenAPI JSON: `/openapi/v1.json` (`AddOpenApi()` + `MapOpenApi()`)
- Scalar UI: `/scalar` (`MapScalarApiReference()`)

Scalar stays in the project as the primary “try it” API tester while the frontend evolves.

---

## Integrations (Planned)
- Pokémon TCG API (card catalog)
- Limitless (tournament/meta sources)
- YouTube embeds (guides/gameplay)

External data is cached into Postgres for speed + stability.

---

## Tech Stack
- C# / .NET 9, ASP.NET Core Web API
- Blazor Web App
- PostgreSQL + EF Core (migrations)
- Scalar (OpenAPI UI)
- Background jobs: Hangfire **or** Quartz.NET

Optional: Redis, Serilog, FluentValidation, Mapster/AutoMapper

---

## Repo Layout
/src
/PokeScout.Api
/PokeScout.Web
/PokeScout.Worker
/PokeScout.Shared
/infra
/docker-compose.yml
/docs
/architecture.md
/api.md
/decisions.md


---

## MVP Entities
User, Card (cached), InventoryItem, Deck, DeckCard, Archetype, ArchetypeDecklist, MetaSnapshot

---

## Roadmap
- **Phase 0:** solution + Postgres + migrations + Scalar
- **Phase 1:** Inventory + Deck Builder MVP (missing cards + completion %)
- **Phase 2:** Archetype pages + manual meta/content
- **Phase 3:** Worker imports + trends/meta ingestion
- **Phase 4:** smart recs (subs, budget optimizer, personal match logging)
