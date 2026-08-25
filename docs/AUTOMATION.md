# KI-Automation und Produktionssystem

## Ziel

KI soll den überwiegenden Teil der Produktion ausführen können: Spezifikation verfeinern, Code und Tests erstellen, Assets erzeugen und cooken, Benchmarks auswerten, Fehler eingrenzen und Dokumentation aktualisieren. Automatisierung ersetzt dabei keine prüfbaren Verträge.

## Autonome Arbeitsschleife

```mermaid
flowchart LR
    A[READY-Auftrag] --> B[Plan + betroffene IDs]
    B --> C[Implementierung / Generierung]
    C --> D[Format + statische Gates]
    D --> E[Tests + Replay]
    E --> F[Performance + Assetbudgets]
    F --> G[Security + Lizenz + Provenienz]
    G --> H{Alle Kriterien erfüllt?}
    H -- nein --> B
    H -- ja --> I[Review-Artefakt + reproduzierbarer Build]
    I --> J[Lokaler Checkpoint auf Arbeitsbranch]
    J --> K[Pull Request + verpflichtende Repository-Gates]
    K --> L[Squash-Merge in vorzeigbaren main]
```

## Geschützte GitHub-Integration

- Agenten besitzen keinen allgemeinen GitHub-Publisher und pushen niemals
  direkt auf `main`.
- Ein lokaler, repo-gebundener Integrator akzeptiert nur den festgelegten
  Arbeitsbranch und genau dieses Repository.
- Er öffnet oder aktualisiert einen Pull Request und wartet auf alle
  verpflichtenden Checks. Rote oder fehlende Gates, Konflikte, ein schmutziger
  Arbeitsbaum und abweichende Bäume blockieren den Merge fail-closed.
- Ein begrenzter frischer Reviewlauf darf belegte Integrationsfehler
  reparieren. Wiederholtes Scheitern beendet die Automation sichtbar.
- Erst nach Squash-Merge und identischem freigegebenem Baum werden lokale Refs
  neu verankert. Dadurch bleibt `main` jederzeit der abnehmbare und öffentlich
  demonstrierbare Stand.

Der Integrator verwaltet keine anderen Repositories und erweitert weder
Agentenrechte noch Modellzugriffe. Details und Begründung stehen in ADR 006.

## Einheitlicher Befehlsvertrag

Diese Aufgaben werden früh im Repository bereitgestellt und bleiben die einzige öffentliche Automationsschnittstelle:

- `bootstrap`: gepinnte Werkzeuge und Abhängigkeiten vorbereiten
- `build`: Development-Build der aktuellen Plattform
- `fmt`: Formatierung anwenden
- `lint`: Format, Toolchain-/Lizenz-/ISA-Prüfung und Daten-Schemas nur prüfen
- `test`: deterministische Tests ohne Netzwerk ausführen
- `assets-check`: Roh- und Cooked-Assets prüfen
- `plattformsmoke`: nativen linux-x64-Smoke (Fenster, GL-3.3-Dreieck, maschinenlesbarer Report) ausführen
- `effizienzbaseline`: Effizienzlauf mit Budgetgate (Startzeit, RSS, p99, Allokationen, Draw-Aufrufe) und Report ausführen
- `bench --scenario bench-empty --report PFAD`: deterministische leere Benchmarkszene (T-020) mit maschinenlesbarer Telemetrie nach NF-007 und fail-closed Budgetgate ausführen; unbekannte oder noch nicht implementierte Szenarien (`bench-army`/`-battle`/`-base`/`-path`/`-load`) schlagen mit Exitcode 25 fehl und erzeugen keinen Report. Läufe auf dem Entwickler-PC sind diagnostische Baseline gemäß Q-OPS-001; Profilbestehen entsteht nur durch deklarierte Referenzklassenbindung bei benannten Referenzrechnern
- `bench --scenario bench-sim --report PFAD`: deterministische headless Simulationsbaseline (T-021) mit festem 20-Hz-Tick und genau 250 vollständig simulierten mobilen Testagenten nativ auf linux-x64 ausführen; rein CPU-seitig ohne Fenster/Renderer, Report nach NF-007 mit Zustands-Hashkette, Budgetgate fail-closed gegen 8 ms Ziel/16 ms harte Grenze je Tick sowie die in `docs/SIMULATIONSVERTRAG.md` fixierte Allokationsgrenze je warmem Tick; dieselben Szenario-/Profil-Ehrlichkeitsregeln wie bench-empty
- `bench --scenario bench-representative --report PFAD [--capture-frame PFAD]`: integrierten deterministischen Belastungsframe (T-023) nativ auf linux-x64 ausführen — 350 sichtbare instanzierte Einheiten mit 48-Bone-Skinningpfad (davon genau die 250 vollständig simulierten Agenten), Graybox-Landschaft, eine Sonne plus vier lokale Schattenlichter mit aktiven Schattenpaessen, Partikelspitze bis 5000; Report Schemaversion 3 nach NF-007 mit Kompositionsbindung, Zustands-Hashkette und fail-closed Budgetgate ausschließlich gegen dokumentierte Grenzwerte; das opt-in `--capture-frame` schreibt nach dem Messfenster genau einen lokal gebundenen Einzelabgriff mit Aussagegrenze „Graybox-Lastbelegung“ (niemals Gameplay-/Atmosphären-/Shipping-Beleg); die übrigen Pflichtszenarien (`bench-army`/`-battle`/`-base`/`-path`) schlagen weiterhin mit Exitcode 25 fehl und erzeugen keinen Report; dieselben Profil-Ehrlichkeitsregeln wie bench-empty
- `soak --scenario soak-replay --report PFAD [--diagnostic-accelerated [--horizon-ticks N]] [--reference-out PFAD]`: deterministischen Zuverlässigkeits-Replay-Soak (T-022) nativ auf linux-x64 im bestehenden Host ausführen — rein CPU-seitig ohne Fenster/Renderer, SDL3-/bgfx-Artefakte werden nicht geladen. Evidenzmodell nach Soakvertrag `docs/SOAKVERTRAG.md` V2 (Projektleitungsentscheidung 2026-08-25): NF-002 wird durch mindestens drei unabhängige Fresh-Prozess-Wiederholungsläufe über den kompletten skriptierten Planhorizont des Simulationsvertrags V1 (576000 Messsticks, genau 250 vollständig simulierte Agenten) in Release-naher Konfiguration nachgewiesen; die beschleunigte Taktung ist dafür zulässig, weil die Pacing-Unabhängigkeit durch Test belegt ist. Jeder Lauf entscheidet fail-closed ausschließlich gegen die absoluten Grenzwerte des Soakvertrags (doppelte Speicherschwellwertform mit Konsistenzbedingung, Fortschritts-Watchdog, strenge Per-Tick-Allokation gemäß Simulationsvertrag §5, Golden-Fixture-Kettenintegrität) und ist je Report als Evidenzeinheit (`execution.evidenceUnit`) markiert; horizontverkürzte Läufe (`--horizon-ticks`) bleiben rein diagnostisch. `--reference-out` ist auch bei vollständigem Horizont stets eine eigenständig markierte diagnostische Referenzemission und niemals Evidenz; erst ein separater Fresh-Prozess-Lauf darf die versionierte Fixture bestätigen. Das Restrisiko des nicht nachgewiesenen zusammenhängenden Achtstunden-Echtzeitbetriebs ist vertraglich ausgewiesen; der frühere autoritative Achtstundenlauf wurde absichtlich abgebrochen und darf nicht neu gestartet werden. Die fensterweise Tickzeitdrift ist gatefrei diagnostisch, die tolerierte Benchmarkstreuung (Q-TEC-010) bleibt offen. Unbekannte oder noch nicht implementierte Soakszenarien brechen mit Exitcode 32 ab und erzeugen keinen Report; Abschnitt-0-Kalibrierläufe laufen über `soak --scenario soak-calibration` (rein diagnostisch). Läufe auf dem Entwickler-PC sind diagnostische Baseline gemäß Q-OPS-001; Pflichtprofile bleiben `NOT-MEASURED`
- `security`: Secrets, Abhängigkeiten und Lizenzen prüfen
- `check`: alle nicht verändernden lokalen Gates ausführen
- `package`: Release-Artefakt für genau einen RID erzeugen

Ein nicht implementiertes Gate muss fehlschlagen oder ausdrücklich `NICHT VERFÜGBAR` melden; es darf keinen leeren grünen Erfolg vortäuschen.

## Codeproduktion

- Jeder Auftrag verweist auf Anforderungs- und Test-IDs.
- Neue Architektur oder Abhängigkeiten erfordern ein ADR.
- Hot Paths benötigen Benchmark oder begründete Budgetzuordnung.
- Ein Budget oder passender Entwurf darf nicht als Optimierungsnachweis
  bezeichnet werden; dieser entsteht erst durch die in ADR 006 und
  `PERFORMANCE_BUDGET.md` gebundene reale Messung.
- Releasepfade dürfen keine Reflection-, Trimming- oder AOT-Warnungen unterdrücken.
- Replay-/Seed-gesteuerte Szenarien dienen als objektive Gameplayregression.
- Automatische Reviews prüfen Spezifikation, Codequalität, Performance, Sicherheit und Lizenz getrennt.

## Assetproduktion

```text
asset-spec -> generation -> raw quarantine -> validation -> normalization
           -> LOD/material/rig pass -> visual review -> cooking -> package
```

Jeder Generierungsjob erzeugt neben dem Rohasset ein Manifest mit:

- Asset-ID und fachlicher Zweck
- Prompt und Negativprompt
- Modell, Tool, Version, Seed und Ausführungsdatum
- Eingabereferenzen mit Herkunft und Lizenz
- Ausgabedateien mit SHA-256
- automatischen Prüfergebnissen
- Abstammung bei Varianten und Nachbearbeitungen
- Freigabestatus

Unbekannte Herkunft, unklare Modelllizenz oder direkte Ähnlichkeit mit einer geschützten Vorlage blockiert den Shipping-Pfad.

Speichervertrag:

- ungeprüfte Generatorausgaben: `assets/quarantine/`, lokal und gitignored
- angenommene bearbeitbare Quellen: `assets/source/`, binär über Git LFS
- Provenienz: `assets/manifests/`, normales versioniertes JSON
- reproduzierbare Laufzeitausgabe: `assets/cooked/`, gitignored

Git LFS ersetzt kein Backup. Vor dem ersten wichtigen Binärasset wird ein gesicherter LFS-Remote festgelegt und Wiederherstellung getestet.

## Menschliche Kontrollpunkte

Auch bei maximaler Automation bleiben explizite Freigaben sinnvoll für:

- endgültige Produkt- und Weltentscheidungen
- Art-Bible-Keyframes und musikalische Hauptthemen
- Lizenz- und IP-Risiken
- Änderungen der Hardware- oder Qualitätsbudgets
- Freigabe eines Meilensteins zur Inhaltsvervielfachung

Diese Kontrollpunkte sollen selten und entscheidungsorientiert sein; technische Routine wird automatisiert.

## Reproduzierbarkeit

- Runtime, Compiler, native Quellen und KI-Produktionswerkzeuge versionsgenau pinnen.
- Netzwerk ist in `test`, `check` und Runtime-Smoke-Tests standardmäßig nicht erforderlich.
- Release-Artefakte werden auf dem jeweiligen Zielbetriebssystem erstellt.
- Prompts allein gelten nicht als reproduzierbar: Modellkennung, Seed, Eingaben, Toolchain und Hashes gehören dazu.
