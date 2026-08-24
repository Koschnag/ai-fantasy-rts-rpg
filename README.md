<p align="center">
  <img src="docs/media/research-hero.svg" alt="Project Riftward – full-agentic SDLC and low-spec game research" width="100%">
</p>

<p align="center">
  <a href="https://github.com/Koschnag/ai-fantasy-rts-rpg/actions/workflows/verify.yml"><img src="https://github.com/Koschnag/ai-fantasy-rts-rpg/actions/workflows/verify.yml/badge.svg" alt="Verify"></a>
  <a href="https://github.com/Koschnag/ai-fantasy-rts-rpg/actions/workflows/dotnet-asset-calibration.yml"><img src="https://github.com/Koschnag/ai-fantasy-rts-rpg/actions/workflows/dotnet-asset-calibration.yml/badge.svg" alt="Asset calibration"></a>
  <a href="https://koschnag.github.io/ai-fantasy-rts-rpg/"><img src="https://img.shields.io/badge/live-research%20showcase-c48a46" alt="Live research showcase"></a>
  <img src="https://img.shields.io/badge/status-research%20preproduction-c48a46" alt="Research preproduction">
  <img src="https://img.shields.io/badge/runtime-not%20yet%20gameplay-526779" alt="Runtime not yet gameplay">
</p>

# Project Riftward

**Can a full-agentic software delivery chain build a modern-feeling game for
genuinely old hardware?**

Project Riftward is an open, exploratory engineering case study wrapped around
an original dark-fairytale RTS/RPG. It combines hero-led exploration,
community building and army-scale strategy while treating 1080p performance on
i7-3770-/GTX-660-class PCs and an 8 GB M1 as a product constraint from the
start.

Its product position is deliberate: hardware escalation is not treated as the
automatic price of better games. A small custom runtime built on focused FOSS
libraries must first exhaust data-oriented design, offline processing, culling,
LOD and measured trade-offs on DDR3-era PCs, Linux desktops and 8 GB M1 Macs.
AI is used to scale that one-time engineering effort; whether it amortizes over
real play sessions remains a falsifiable research question.

The second research object is the production system itself: versioned intent,
agent missions, local retrieval and memory, hard gates, recovery, provenance
and independently reviewable evidence. A model response is a candidate; only a
measured outcome advances the project.

> **Honest status (2026-08-24):** the reproducible production foundation and
> the native Linux SDL3/bgfx walking skeleton (`T-010`) are accepted; gameplay
> does not exist yet. The retail-era research showcase (`T-008`) is in review,
> and the cross-platform build matrix (`T-011`) remains a draft.

## Project documents

1. [PROJEKT.md](PROJEKT.md) – Problem, Zielbild, Zielgruppe und Umfang
2. [docs/OFFENE_FRAGEN.md](docs/OFFENE_FRAGEN.md) – Punkte, die wir gemeinsam klären
3. [docs/ANFORDERUNGEN.md](docs/ANFORDERUNGEN.md) – funktionale und nichtfunktionale Anforderungen
4. [docs/USER_FLOWS.md](docs/USER_FLOWS.md) – Nutzerwege und Fehlerfälle
5. [docs/ARCHITEKTUR.md](docs/ARCHITEKTUR.md) – technische Leitplanken
6. [docs/DATENMODELL.md](docs/DATENMODELL.md) – Daten, Beziehungen und Lebenszyklen
7. [BACKLOG.md](BACKLOG.md) – priorisierte Umsetzungseinheiten
8. [docs/QUALITAET.md](docs/QUALITAET.md) – Abnahme, Tests und Definition of Done
9. [docs/entscheidungen/README.md](docs/entscheidungen/README.md) – nachvollziehbare Entscheidungen
10. [AGENTS.md](AGENTS.md) – verbindliche Arbeitsregeln für implementierende KI-Agenten
11. [docs/GAME_DESIGN.md](docs/GAME_DESIGN.md) – Spielsäulen, Umfang und Vertical Slice
12. [docs/ART_DIRECTION.md](docs/ART_DIRECTION.md) – eigenständige Bild- und Klangidentität
13. [docs/PERFORMANCE_BUDGET.md](docs/PERFORMANCE_BUDGET.md) – feste Budgets für Zielhardware
14. [ADR 006](docs/entscheidungen/006-performancebeweis-sprachrollen-und-integration.md) – Performancebeweis, Sprachrollen und geschützte Integration
15. [docs/AUTOMATION.md](docs/AUTOMATION.md) – autonome KI-Produktionsschleife und Qualitätsgates
16. [docs/IP_UND_LIZENZEN.md](docs/IP_UND_LIZENZEN.md) – FOSS- und Asset-Provenienzregeln
17. [docs/CLEAN_ROOM.md](docs/CLEAN_ROOM.md) – verbindliche Trennung von Genreanalyse und Produktion
18. [docs/ATMOSPHAERE.md](docs/ATMOSPHAERE.md) – emotionaler Nordstern, Weltidentität und Review-Rubrik
19. [docs/HARNESS.md](docs/HARNESS.md) – KI-Verlauf, Gedächtnis, RAG und Evidenz
20. [docs/TOOLCHAIN.md](docs/TOOLCHAIN.md) – installierte und geplante FOSS-Werkzeuge
21. [docs/PLATTFORMMATRIX.md](docs/PLATTFORMMATRIX.md) – OS-, Backend-, Paket- und Smoke-Baselines
22. [docs/ASSET_PIPELINE.md](docs/ASSET_PIPELINE.md) – synthetische Asseterzeugung, FOSS-Werkzeuge und Modellzulassung

`Project Riftward` is an internal research codename pending naming/trademark
review.

## Two experiments, one repository

| Agentic SDLC | Low-spec game engineering |
|---|---|
| How long can agents converge without human input? | How much atmosphere and tactical readability fit inside fixed budgets? |
| Which specifications and oracles actually prevent drift? | Can a lean runtime avoid hardware escalation instead of hiding it behind presets? |
| What do failed runs cost and teach? | Do measured player outcomes justify the development compute? |
| Can every accepted change be traced to intent and evidence? | Where do CPU, GPU, RAM, VRAM and content budgets really break? |

![CCD evidence loop](docs/media/ccd-evidence-loop.svg)

The idea that heavy one-time development compute could reduce hardware demand
across many players is explicitly a **hypothesis**, not a sustainability claim.
The project will not invent missing provider-energy data or ignore embodied
hardware cost, adoption and rebound effects. See the
[research protocol](docs/research/PROTOCOL.md).

## What is real today

- a local F#/.NET harness with tamper-evident run ledger, curated memory,
  deterministic BM25 retrieval, evidence binding and retention controls
- a fail-closed clean-room and asset-provenance gate
- a deterministic in-process generator that writes a calibration GLB and CPU
  preview without network access, subprocesses or a DCC runtime
- an independent inspector and fresh-checkout CI for the generator pipeline
- fixed platform, frame-time, memory, scene and effect budgets before runtime
  content scales
- an original world/art bible and quarantined, manifested concept research

### Binding engineering decisions

- the original fantasy RTS/RPG is not a reconstruction of any one external work
- a lean custom runtime replaces a full game engine
- .NET 10 LTS, C# and F# are the baseline; release builds may use Native AOT
- Windows, Linux and macOS require platform-specific builds and tests
- creative shipping assets are generated from project-owned specifications and
  still require technical, provenance and originality review
- dependencies are FOSS-first, versioned and paired with an exit strategy
- budgets remain hypotheses until a reproducible run passes on real target hardware
- C# owns runtime hot paths, F# owns typed offline specification and validation,
  and Python remains an optional untrusted production adapter
- autonomous checkpoints reach `main` only through pull requests and required gates

## What is not real yet

- no playable game or representative gameplay loop
- no proven 30/60 FPS result on the target hardware
- no evidence that the system can productively run for several days without
  intervention
- no finished-game scope, ecological benefit or general superiority claim
- no blanket approval of AI-generated concepts, models, animation or video

Concept art is never presented as gameplay. Generated raster/3D outputs remain
in ignored local quarantine until their technical, originality and license
reviews pass; Git tracks their specifications, manifests and receipts. The
public diagrams in this README are deterministic project-authored SVGs.

## Game hypothesis

The player arrives as a mortal cartographer and field engineer in a wounded
ring-continent. Regions intermittently overlap with possible past or future
material states. Understanding a place grows into responsibility for people,
infrastructure and battles: exploration informs settlement, settlements change
the landscape, and strategic outcomes remain personally visible.

The target tone is wistful, earthy, wondrous and determined. Magic is rare,
material and rule-bound; hope comes from work and relationships rather than
permanent spectacle.

## Hardware contract

| Profile | Target |
|---|---|
| PC minimum | i7-3770, GTX 660, 8 GB DDR3-class RAM, 1920×1080 Low, 30 FPS |
| Mac minimum | Apple M1, 8 GB unified memory, 30 FPS |
| high preset ceiling | RX-580 class, 1920×1080 High, 60 FPS preferred |

No ray tracing or real-time global illumination path is planned. Baked
lighting, probes, LOD, culling, instancing and controlled VFX must earn visual
quality inside the budget. Full thresholds are versioned in
[PERFORMANCE_BUDGET.md](docs/PERFORMANCE_BUDGET.md).

![Compute amortization hypothesis](docs/media/compute-amortization-hypothesis.svg)

## Research and build-in-public

- [live digital retail-era showcase and public status](https://koschnag.github.io/ai-fantasy-rts-rpg/)
- [case-study overview](docs/research/README.md)
- [research protocol and falsification signals](docs/research/PROTOCOL.md)
- [mapping to Cong-Driven Development](docs/research/CCD_MAPPING.md)
- [dated baseline and experiment log](docs/research/CASE_STUDY_LOG.md)
- [campaign and claim rules](docs/communication/CAMPAIGN.md)
- [visual, 3D, animation and film lab](docs/communication/MEDIA_LAB.md)
- [40-second research-teaser storyboard](docs/communication/STORYBOARD-001.md)

The case study is a practical probe for
[Cong-Driven Development](https://github.com/Koschnag/cong-driven-development)
and the proposition of working *on the system* rather than merely *in the
system* described in
[“Software Engineering im KI-Zeitalter: Gegenthese zum Hype”](https://de.linkedin.com/pulse/software-engineering-im-ki-zeitalter-gegenthese-zum-hype-nguyen-imnof).
It does not currently claim full CCD conformance.

## Run the evidence gates

Prerequisites and platform setup are documented in
[TOOLCHAIN.md](docs/TOOLCHAIN.md).

```bash
./scripts/rift.sh bootstrap
./scripts/rift.sh build
./scripts/rift.sh lint
./scripts/rift.sh test
./scripts/rift.sh security
./scripts/rift.sh plattformsmoke --report artifacts/t010/smoke.json
./scripts/rift.sh effizienzbaseline --report artifacts/t010/effizienz.json
./scripts/rift.sh asset-calibration validate-spec --spec assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json
./scripts/rift.sh assets-check
./scripts/rift.sh rag-build
./scripts/rift.sh verify
./scripts/rift.sh fresh-checkout-test
./scripts/dotnet-asset-calibration-ci.sh
```

Production gates that have no accepted implementation fail explicitly. They do
not return a cosmetic green result.

## Repository map

| Start here | Purpose |
|---|---|
| [PROJEKT.md](PROJEKT.md) | problem, scope and outcome |
| [BACKLOG.md](BACKLOG.md) | prioritized, status-bound implementation units |
| [AGENTS.md](AGENTS.md) | binding rules for implementing agents |
| [docs/HARNESS.md](docs/HARNESS.md) | runs, memory, retrieval and evidence |
| [docs/AUTOMATION.md](docs/AUTOMATION.md) | autonomous production loop and checkpoints |
| [docs/ARCHITEKTUR.md](docs/ARCHITEKTUR.md) | technical boundaries |
| [docs/ATMOSPHAERE.md](docs/ATMOSPHAERE.md) | world, tone and measurable art/gameplay rubric |
| [docs/ASSET_PIPELINE.md](docs/ASSET_PIPELINE.md) | synthetic asset lifecycle |
| [docs/CLEAN_ROOM.md](docs/CLEAN_ROOM.md) | separation from external creative works |
| [docs/IP_UND_LIZENZEN.md](docs/IP_UND_LIZENZEN.md) | license and provenance policy |
| [docs/QUALITAET.md](docs/QUALITAET.md) | acceptance and Definition of Done |
| [docs/entscheidungen/](docs/entscheidungen/) | architecture decision records |

Status is semantic: `READY` permits implementation; `DONE` means implemented,
tested and accepted. Open product decisions may not be silently invented by an
agent.

## Contributing and citation

Reproductions, hardware traces, methodological criticism and narrowly scoped
gate improvements are welcome; see [CONTRIBUTING.md](CONTRIBUTING.md). A
structured experiment issue template is available on GitHub.

Use [CITATION.cff](CITATION.cff) and cite the exact commit evaluated. The
repository's own software/content license is still an open product decision;
the presence of third-party FOSS dependencies does not make all project code or
media FOSS.
