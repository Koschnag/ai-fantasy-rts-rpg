# Backlog

Nur Einträge mit Status `READY` dürfen ohne weitere fachliche Klärung implementiert werden.

## Priorisierung

- `MUST`: für das MVP unverzichtbar
- `SHOULD`: hoher Nutzen, aber kein Freigabekriterium
- `COULD`: optional
- `WONT`: bewusst nicht in dieser Version

## Epics

| ID | Epic | Nutzen | Priorität | Abhängigkeiten | Status |
|---|---|---|---|---|---|
| E-001 | Autonome Produktionsplattform | KI-Arbeit ist reproduzierbar, erinnerungsfähig und prüfbar | MUST | – | IN ARBEIT |
| E-002 | Plattform-Walking-Skeleton | dasselbe leere Spiel startet auf Windows, Linux und macOS | MUST | E-001 | OFFEN |
| E-003 | Performancekern | Rendering und Simulation halten die Hardwarebudgets | MUST | E-002 | OFFEN |
| E-004 | Graybox-Hybridspiel | Held, Erkundung, Aufbau und Armee bilden eine spaßige Schleife | MUST | E-003 | OFFEN |
| E-005 | Atmosphärischer Vertical Slice | alle Kernsysteme und finalitätsnahe Inhalte bestehen zusammen | MUST | E-004 | OFFEN |
| E-006 | Contentproduktion | validierte KI-/prozedurale Pipelines skalieren den freigegebenen Umfang | MUST | E-005 | OFFEN |

## Umsetzungseinheiten

| ID | Epic | Ergebnis | Verknüpfte Anforderungen | Größe | Priorität | Status |
|---|---|---|---|---|---|---|
| T-001 | E-001 | lokales F#-Harness mit Run-Ledger, Hashkette, BM25-RAG, Zitaten und Integritätsprüfung | Z-004, NF-008 | M | MUST | DONE |
| T-002 | E-001 | Memory-Promotion, Konflikt-/Stalenessprüfung und Retrieval-Traces | Z-004, NF-003 | M | MUST | DONE |
| T-003 | E-001 | Clean-Room-, Asset-Provenienz-, Quarantäne- und technische Validatorhülle | Z-004, Z-005 | M | MUST | DONE |
| T-004 | E-001 | vollständige Run-Provenienz, Evidenzzuordnung, Trace-/Span-Felder, RAG-Buildmanifest und sichere Retention | Z-004, NF-003, NF-008 | M | MUST | DONE |
| T-005 | E-001 | striktes calibration-v1-Spec und unabhängiger .NET-Inspector prüfen GLB, PNG, Report und Proxybudgets ohne Blender | Z-002, Z-004, Z-005, F-008, F-009 | M | MUST | DONE |
| T-006 | E-001 | BCL-only-F#/.NET-Generator schreibt GLB und CPU-Preview deterministisch in-process und publiziert transaktional über T-003 in Quarantäne | Z-002, Z-004, Z-005, F-007, F-008, F-009 | M | MUST | DONE |
| T-007 | E-001 | Fresh-Checkout-CI beweist .NET-Pin, Null-Unterprozess/-Netz, Determinismus, T-005-Regression, Recovery und T-003-Crosschecks | Z-002, Z-004, Z-005, F-007, F-008, F-009 | M | MUST | DONE |
| T-010 | E-002 | SDL3-Fenster, Input und bgfx-Dreieck zuerst nativ auf linux-x64 auf Referenzhardware; Windows-/macOS-Nachweise folgen über T-011 | Z-002, Z-003 | L | MUST | DONE |
| T-011 | E-002 | plattformspezifische Shader-/Native-Buildmatrix und Smoke-Artefakte | Z-003 | L | MUST | DRAFT |
| T-020 | E-003 | leere Benchmarkszene mit Telemetrie auf allen Hardwareprofilen | Z-002 | M | MUST | DRAFT |
| T-021 | E-003 | headless feste Simulation mit 250 mobilen Testagenten | Z-002 | L | MUST | DRAFT |
| T-022 | E-003 | deterministischer 8-Stunden-Replay-Soak weist Stabilität und begrenztes Speicherwachstum nach | Z-002, NF-002 | M | MUST | DRAFT |
| T-023 | E-003 | integrierter repräsentativer Belastungsframe verbindet 350 sichtbare/250 simulierte Einheiten, Animation, Landschaft, Schatten, Partikel und vollständige Ressourcenmetriken auf den Minimum-Profilen | Z-002 | L | MUST | DRAFT |
| T-030 | E-004 | erste vollständige Graybox-Schleife von Erkundung bis Basiskampf | Z-001 | XL | MUST | DRAFT |
| T-031 | E-004 | versioniertes atomares Save/Load besteht Roundtrip-, Abbruch-, Korruptions- und Wiederherstellungsfixtures | Z-001, F-005, NF-002 | L | MUST | DRAFT |
| T-040 | E-005 | repräsentative Riftward-Mission besteht Atmosphären-, Originalitäts- und visuelles Lesbarkeitsgate | Z-001, Z-005 | XL | MUST | DRAFT |
| T-041 | E-005 | finale UI-, Eingabe-, Untertitel- und Einstellungsabnahme auf allen Zielplattformen | Z-002, Z-003 | L | MUST | DRAFT |
| T-050 | E-006 | eine validierte KI-/prozedurale Assetfamilie durchläuft Quarantäne, Review, LFS-Quelle und Cooking reproduzierbar | Z-004, Z-005 | L | MUST | DRAFT |
| T-051 | E-006 | gemessene Karten-/Quest-/Audio-Pipeline erzeugt konsistente Inhalte mit vollständiger Provenienz | Z-001, Z-004, Z-005 | XL | MUST | DRAFT |

`T-003`, `T-005`, `T-006` und `T-007` sind unabhängig abgenommen. `T-006` hat den
BCL-only-.NET-in-process-Generator samt transaktionalem Quarantäne-Lifecycle
und dem ersten lokalen 3D-Quarantäneasset geliefert. `T-007` beweist diesen
Pfad aus einem sauberen Linux-x64-Checkout. Alle drei Assettasks hängen direkt von `T-003` ab; zusätzlich hängt
`T-006` von `T-005` und `T-007` von `T-005`/`T-006` ab. Der geschlossene Vertrag steht in
`docs/DOTNET_GENERATOR_CONTRACT.md`. Das ist ein bewusstes T-006-Amendment:
T-005 bleibt historisch abgenommen; seine komplette Inspector-Suite muss nach
der eng begrenzten Identifier-/Quellen-/Pin-Anpassung erneut bestehen. `T-050`
bleibt `DRAFT` und setzt `T-003`, `T-005`, `T-006` und `T-007`
voraus; erst T-050 verantwortet getrennte visuelle/rechtliche Reviews,
Source-Promotion, LFS, Backup, Cooking und produktionsnahe Messung.

`T-004` wurde am 2026-08-22 durch die Projektleitung freigegeben (`READY`), in
Lauf `01M0NCFAVJ308TVZ3XY7J8SY58` implementiert und durch den unabhängigen
Reviewlauf `01M0QMA3NAPRXX1KBVH8XFRA6J` akzeptiert; Abnahmedokument:
`docs/abnahme/T-004-run-provenance-and-evidence.md`.

`T-010` wurde am 2026-08-23 vom autonomen Planungsagenten (Autorisierung der
Projektleitung vom 2026-08-23) auf `READY` gesetzt. Die Epikabhängigkeit E-001
ist im für T-010 erforderlichen Umfang erfüllt: T-001 bis T-007 sind
abgenommen; das Epic selbst bleibt wegen möglicher Folgeeinheiten in Arbeit.
Die zuvor blockierenden Fragen Q-TEC-001/Q-TEC-003 sind verfahrensmäßig
entschieden (Klärungsprotokoll in `docs/OFFENE_FRAGEN.md`): Die konkreten
nativen Pins und Build-/Cachedetails entstehen als gatender erster Abschnitt
des Auftrags `.ai/tasks/T-010-native-walking-skeleton.json` nach vollständig
spezifizierten Kriterien gemäß der Spike-Klausel in `docs/QUALITAET.md`;
Rückrollbar durch Pin-Austausch und Neubau. Der Auftrag liefert die nativen
linux-x64-Nachweise von AC-T010-02/03 auf dem Entwickler-PC (i7-3770/RX 570);
Windows- und macOS-Builds, Smokes und Paketnachweise sind bewusst an T-011
überwiesen. Fehlt die linux-x64-Referenzhardware bei Umsetzung, bleiben die
Kriterien offen und werden eskaliert statt durch Cross-Compile oder Simulation
ersetzt; das Projektziel Z-003/NF-006 (alle drei Pflicht-RIDs) bleibt
unverändert. Audio (Q-TEC-007) bleibt ausdrücklich ausgeschlossen.

Die Spezifikation wurde am 2026-08-23 durch den unabhängigen Reviewlauf
`01M0QQYJDX9CS56144Z7VGN8J4` geprüft: Task-Manifest schema-validiert,
Dokumente konsistent, alle lokalen Gates grün. Die Implementierung hat noch
nicht begonnen.

T-010 wurde am 2026-08-24 durch den unabhängigen Review-/Vollendungslauf
`01M0QYAA11MC89GVMP6BWR7016` (Akteur `t010-review-completion`) abgenommen und
ist `DONE`: Zwei abgebrochene Implementierungsläufe wurden geprüft, deren
In-Scope-Defekte repariert (bgfx-Ausgabepfade, x86-64-v2-/PIC-Buildflags,
Shim-Link gegen bimg, SDL3-X11-Laufzeitbindung, Shader-Semantikdefinition,
`SOURCE_DATE_EPOCH` für byteidentische Neubauten, Manifest-Neuschreiben im
Verify-Modus) und die fehlenden Anteile vollendet: nativer Build samt
Reproduzierbarkeitsnachweis, C#-Interop mit LibraryImport/Besitzregeln/
Fehlerobjekten, Host mit `plattformsmoke`/`effizienzbaseline`,
Toolchain-/Lizenz-/ISA-Gate in `lint`+`security`, Fault-Injection- und
Architekturtests, Doku (`NATIVE_UNTERBAU.md`, Mindestbasis in
`PLATTFORMMATRIX.md`, Befehlsvertrag in `AUTOMATION.md`). Alle
Abnahmekriterien AC-T010-01 bis AC-T010-08 sind mit Evidenz im Lauf
nachgewiesen; Smoke und Effizienzbaseline liefen nativ auf dem
Entwickler-PC (i7-3770/RX 570, Mesa 26.0.3). Die durch den Pin-Nachtrag
invalidierten T-006/T-007-Bindungen (Manifest-Input-Hash, Receipt-Kette,
CI-Evidenzschema) wurden über die dokumentierte Regeneration neu verankert;
die generierten Assets blieben byteidentisch. Windows-/macOS-Builds, Smokes
und Paketnachweise bleiben gemäß Auftrag an T-011 überwiesen; Abnahmedokument:
`docs/abnahme/T-010-native-walking-skeleton.md`.

Nach T-010 werden die isolierten Baselines T-020/T-021 und anschließend der
integrierte Repräsentativitätsnachweis T-023 gegenüber weiterer allgemeiner
Produktionsinfrastruktur priorisiert, soweit deren Abhängigkeiten `READY` sind.
Der bewusst einfache Belastungsframe ist der erste Beleg für oder gegen die
Effizienzhypothese; Architektur und Budgets allein gelten nicht als Optimierung
(ADR 005).

## Vorlage für eine Umsetzungseinheit

### T-XXX – Kurzer ergebnisorientierter Titel

- **Status:** OFFEN
- **Zweck:** Warum wird das gebraucht?
- **Ergebnis:** Welcher beobachtbare Zustand soll entstehen?
- **Enthalten:**
- **Nicht enthalten:**
- **Abhängigkeiten:**
- **Betroffene Anforderungen:**
- **Abnahmekriterien:**
  - [ ] Konkretes, von außen prüfbares Ergebnis
- **Erforderliche Tests:**
- **Dokumentation:**
- **Offene Punkte:**
