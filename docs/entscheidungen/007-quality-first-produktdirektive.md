# ADR 007: Quality-First-Direktive als querschnittliche Produktentscheidung

- **Status:** akzeptiert
- **Datum:** Entscheidung der Projektleitung 2026-08-25; Übernahme in dieses Register 2026-08-26
- **Entscheidungsverantwortung:** Projektleitung (bestätigte Direktive); Übernahme durch den autonomen Planungsagenten gemäß Autorisierung vom 2026-08-23
- **Bezug:** `PROJEKT.md`, `docs/QUALITAET.md`, ADR 006, T-022 (`docs/SOAKVERTRAG.md` V2), alle künftigen Backlog-Einheiten

## Kontext

Die Projektleitung hat am 2026-08-25 verbindlich entschieden („Quality First“),
dass Zeit und ein früher Demotermin keine limitierenden Produktfaktoren sind.
Auslöser war unter anderem der am 2026-08-25 nach 6 h 35 min Wanduhr
absichtlich per SIGTERM abgebrochene autoritative Achtstunden-Realzeitlauf von
T-022 (Exitcode 143, kein Report): Er bleibt ehrliche Diagnoseevidenz, ist aber
kein Achtstunden-Pass; sein Evidenzvertrag wurde bereits durch den
wiederholungsbasierten Soakvertrag V2 ersetzt (siehe `docs/SOAKVERTRAG.md`,
Abschnitt 4 und 6). Diese Direktive galt bislang nur als externe
Projektleitungsentscheidung und war nicht im Projektarchiv verankert. Der
nächste saubere, nicht mit T-022 vermischte Planungs-/Dokumentationslauf —
dieser Lauf — übernimmt sie gemäß ihrem eigenen Auftragswortlaut in
`PROJEKT.md` und in dieses Register.

## Entscheidung

Die Quality-First-Direktive wird als bestätigte, querschnittliche
Projektentscheidung übernommen. Verbindlicher Kern:

1. Gameplay, Grafik, Atmosphäre, Performance und Softwarequalität sind
   gleichwertige Pflichtdimensionen. Keine darf für einen schnellen sichtbaren
   Meilenstein nur behauptet oder dauerhaft geopfert werden.
2. Keine Abnahmekriterien, Budgets, Reviews oder Evidenzgates werden für Tempo
   abgeschwächt. Ein Termin erzeugt keine Freigabe.
3. Kleine vertikale Prototypen bleiben erwünscht — als Lern- und
   Qualitätsevidenz, nicht als vorgetäuschter Produktfortschritt.
4. Reversible Gameplay- und Gestaltungsentscheidungen bedürfen expliziter
   Alternativen, begründeter Hypothesen, Playtestkriterien und eines
   Rückrollwegs.
5. Spielerische Güte wird durch nachvollziehbare Spieltests, Verständlichkeit,
   Entscheidungsqualität, Pacing und Wiederholungsvarianz geprüft;
   technische Funktionsfähigkeit allein reicht nicht.
6. Grafik/Atmosphäre brauchen eigenständige, kohärente, lesbare Belege
   innerhalb der realen Hardwarebudgets; Konzeptmaterial ist kein
   Gameplaybeweis. Performanceaussagen benötigen reproduzierbare Messungen auf
   den gebundenen Hardwareklassen (ADR 006); Softwarequalität benötigt klare
   Architektur, Tests, Wartbarkeit, Fehlertoleranz und unabhängige Reviews.
7. Ist eine Dimension nicht belastbar gut, bleibt der Stand Kandidat oder
   Prototyp.
8. Der Entwicklungsfluss wird auf Durchlaufzeit ohne Gateabschwächung
   optimiert: schnelle Preflight-Tests und frische Review vor langen
   Abnahmeläufen; Langzeitnachweise laufen asynchron nur auf eingefrorenen,
   fingerprint-gebundenen Kandidaten.
9. Parallelität nur zwischen unabhängigen Repositories oder isolierten
   Git-Worktrees mit zentralem seriellen Integrator; mehrere schreibende
   Agenten im selben Arbeitsbaum bleiben verboten (konsistent zur
   Swarm-Policy).
10. Lange Gates müssen ihren Erkenntniswert gegen Wanduhrkosten belegen. Der
    T-022-Ersatzvertrag (V2) ist die dokumentierte Umsetzung für NF-002; ein
    falscher Pass ist verboten.

Ein Demotermin ist kein Scope- oder Freigabekriterium.

## Betrachtete Optionen

### Option A: Direktive nur extern führen, nicht in das Projektarchiv übernehmen

- Vorteile: keine Dokumentpflege.
- Nachteile: Die verbindliche Leitungsentscheidung wäre für implementierende
  und reviewende Agenten nicht auffindbar; Quellenhierarchie
  (`docs/entscheidungen/` vor übriger Dokumentation) bliebe ungenutzt.
- Risiken: stillschweigende Abweichungen von den Pflichtdimensionen.

### Option B: Übernahme als akzeptierte querschnittliche ADR plus Verweis in PROJEKT.md

- Vorteile: einheitliche, auffindbare Quelle der Wahrheit; direkte Anwendbarkeit
  auf Definition of Ready/Done und alle künftigen Freigaben.
- Nachteile / Risiken: keine bekannt; der Wortlaut wird unverändert übernommen,
  nichts neu erfunden.

## Folgen

- Positiv: messbare Pflichtdimensionen und Playtest-/Rückrollpflichten sind ab
  sofort Bestandteil jeder Ready-Spezifikation; Langzeitgates laufen asynchron
  auf eingefrorenen Kandidaten und blockieren den Arbeitsbaum nicht mehr.
- Negativ / Kompromisse: sichtbarer Fortschritt kann langsamer wachsen, weil
  Kandidaten erst nach vollständiger Dimensionsevidenz angenommen werden; dies
  ist ausdrücklich gewollt.
- Folgemaßnahmen: künftige Task-Manifeste weisen je betroffener Dimension
  Evidenzwege aus; reversible Gameplay-/Gestaltungsentscheidungen folgen dem
  Muster „Alternativen, Hypothese, Playtestkriterien, Rückrollweg“ (siehe
  Freigabeprotokoll zu T-031 als erste Anwendung auf einen E-004-Auftrag).
- Zeitpunkt für erneute Prüfung: bei jeder geplanten Änderung der
  Qualitäts-, Budget- oder Freigabeverträge; jede Abschwächung erfordert eine
  neue Projektleitungsentscheidung und eine neue Version dieser ADR.
