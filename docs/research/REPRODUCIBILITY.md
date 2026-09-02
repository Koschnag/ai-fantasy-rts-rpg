# T-053 Reproduzierbarkeitsleitfaden

**Vertrag:** `riftward-observability-reproduction-v1`

**Protokoll:** `riftward-research-observability` 2.0.0

## Protokoll-Freeze

Der Protokollbundle besteht in exakt dieser Reihenfolge nach
Unicode-Codepoint-Sortierung aus:

```text
.ai/tasks/T-053-research-observability.json
docs/research/METRICS.md
docs/research/OBSERVABILITY_DATA_DICTIONARY.md
docs/research/PRIVACY_AND_PUBLICATION.md
docs/research/PROTOCOL.md
docs/research/PROTOCOL_CHANGELOG.md
docs/research/REPRODUCIBILITY.md
docs/research/THREATS_TO_VALIDITY.md
```

Fuer jede Datei wird SHA-256 ueber die eingecheckten UTF-8-Bytes gebildet.
Das Bundle-Manifest besteht aus einer Zeile
`<sha256><zwei Leerzeichen><repo-relativer Pfad><LF>` je Datei, in obiger
Sortierung. `protocolBundleSha256` ist SHA-256 ueber die exakten
Manifestbytes. Das Manifest selbst wird als Evidenz gespeichert, gehoert aber
nicht rekursiv zum Bundle.

Der Freeze ist nur gueltig, wenn:

1. alle acht Dateien im gebundenen Git-Baum liegen,
2. JSON-Syntax und Task-Schema gueltig sind,
3. interne Markdownlinks aufloesbar sind,
4. Protokollversion und Begleitvertraege einander nennen,
5. der Arbeitsbaum fuer diese Pfade sauber ist,
6. `protocol.frozen` vor `observation.started` persistiert wurde.

Eine spaetere Textaenderung erfordert Version, Changelog und neuen Hash. Der
alte Hash bleibt an alte Daten gebunden.

## Eingabemanifest je Beobachtung

Vor Ableitung oder Collection wird ein Eingabemanifest mit folgenden
Pflichtfeldern eingefroren:

- `observationId`, Evidenzklasse und Ziel-Task-ID,
- `studyId` sowie stabile pseudonyme Actor-/Agent-ID-Regel,
- Protokollversion und `protocolBundleSha256`,
- vollstaendiger Baseline-/Head-Commit, Baseline-/Ergebnis-Tree-ID und der als
  Exportziel gebundene `inputTreeId`,
- Hash des Ziel-Taskmanifests,
- Collector-/Exporter-Version und Toolchain-Hash,
- Zeitzone `UTC`, Locale `C`,
- Pfadkarten-Version fuer Architekturmetriken,
- Quellinventar mit Pfad/Adresse, Bytegroesse und SHA-256,
- Redaktionspolicy-Version,
- bei Ablationen genau eine Ablation-ID und der unveraenderte
  Kontroll-Eingabehash.

Fehlt ein Pflichtfeld, beginnt keine prospektive Beobachtung. Bei
retrospektiver Ableitung wird der fehlende Wert `unknown`; die Ableitung darf
nicht in `prospective-observed` umetikettiert werden.

## Retrospektive Kalibrierung T-037

1. Vollstaendigen Commit explizit waehlen; bewegliche Branch-Namen sind kein
   reproduzierbarer Eingang.
2. Nur Git-Objekte und bereits vorhandene, hashpruefbare Harness-/Reviewbelege
   inventarisieren.
3. Git-Commitzeit nur als Commitmetadatum ausgeben. Sie ist weder
   Arbeitsbeginn noch Laufdauer.
4. T-037-Taskmanifest und Baseline-/Ergebnisbaeume hashen.
5. Ereignisse als `retrospective-derived` ableiten und jede Ableitung an
   mindestens eine Quellreferenz binden.
6. Nicht belegte Zeit, Intervention, Tokenklasse, Kostenklasse und Outcome
   literal als `unknown` ausgeben.
7. Zweimal aus frischen leeren Ausgabeverzeichnissen exportieren und Hashes
   vergleichen.

Die Kalibrierung prueft das Instrument. Sie ist kein historischer
Autonomiebeweis und kein Kontrollarm fuer T-042.

## Prospektiver Erstlauf T-042

1. T-042 muss am Baseline-Commit vorhanden, schema-gueltig und durch seinen
   eigenen Prozess startberechtigt sein.
2. Hashes von T-042-Taskmanifest, bestehenden Gatevertraegen sowie T-037-
   Garantien als Nichtinterferenz-Snapshot erfassen.
3. Collector nur auf den T-053-Ledger-/Exportpfad berechtigen. Keine
   Schreibberechtigung fuer T-042-Produkt-, Test-, Task- oder Gatepfade.
4. `research begin --study-manifest PATH` aufrufen. Die Operation validiert
   Protokollbundle, Baseline/HEAD/Tree, Taskberechtigung,
   Nichtinterferenz-Snapshot und dass noch kein Zielereignis existiert; sie
   schreibt unter einem Lock `protocol.frozen`, danach `observation.started`
   und publiziert den hashgebundenen Active-Marker erst nach Ledger-fsync,
   Tempdatei-fsync, atomarem Rename, Parent-Directory-fsync sowie no-follow
   Reopen und Bindungspruefung. Nur der erfolgreiche Aktivierungsreceipt
   autorisiert den Zielstart.
5. Erst nach erfolgreichem `begin` den T-042-Zielprozess starten. Ist der
   Zielprozess bereits gestartet, scheitert `begin` mit
   `PROSPECTIVE_START_TOO_LATE`; der Lauf wird nicht rueckwirkend prospektiv.
6. Ereignisse append-only erfassen; Redaction geschieht vor Persistierung.
7. Nach Zieloutcome den zweiten Nichtinterferenz-Snapshot bilden, Kette
   ueber `research close --observation ID --outcome-receipt REF` schliessen,
   die finale Kette fsyncen, den Active-Marker erst nach Revalidierung
   entfernen, dessen Parent-Verzeichnis fsyncen und die Rohquelle einfrieren.
8. Zwei Exporte in frischen, leeren Verzeichnissen mit Netzwerk deaktiviert,
   `TZ=UTC` und `LC_ALL=C` erzeugen.
9. Dateihashes vergleichen; erst danach darf die Forschungshypothese bewertet
   werden. T-042-Taskstatus wird ausschliesslich aus seiner eigenen Quelle
   gelesen, nie von T-053 geschrieben.
10. Den exakten Ergebnisbaum und beide Exporte einer vom Builder getrennten
    unabhaengigen Reviewinstanz vorlegen; nur deren `PASS` erfuellt den
    Abschlussvertrag von T-053.

Wenn T-042 im Zielbaum nicht existiert oder noch nicht startberechtigt ist,
wartet P-001. Eine historische oder parallele T-042-Aktivitaet wird nicht als
erster prospektiver Lauf umgedeutet.

Ist T-042 nicht startberechtigt, wartet P-001 und T-053 bleibt unfertig. Ein
Abbruch oder dokumentierter Restpunkt ist kein Ersatz fuer einen tatsaechlich
prospektiv beobachteten Lauf.

## Deterministische Exportform

- JSON und CSV folgen den Serialisierungsregeln des Datenwoerterbuchs.
- Eingaben werden nur ueber das eingefrorene Inventar gelesen; Dateisystem-
  Discovery waehrend des Exports ist verboten.
- Ereignisse werden nach `observationId`, dann numerischer `sequence`
  sortiert. Andere Tabellen folgen ihren dokumentierten Primaerschluesseln.
- Zeit wird nicht aus der Exportuhr erzeugt. `generatedAtUtc` ist der im
  Eingabemanifest eingefrorene Exportzeitpunkt.
- Absolute Pfade, Benutzernamen und Hostnamen werden vor Hashbildung der
  persistierten Forschungsquelle normalisiert oder redigiert.
- Keine Netzwerkabfrage, kein beweglicher Branch, keine aktuelle Uhr und
  keine zufaellige ID duerfen den Export beeinflussen.
- `study-manifest.json` ist die kanonische Exportkopie des eingefrorenen
  Eingabemanifests. Es bindet mindestens Study/Observation, Evidenzklasse,
  Protokollbundle, Collector-/Exporter-/Toolchainversion, Baseline-/Head-Commit,
  Baseline-/Ergebnis-/Input-Tree, Quellinventarhash, Redaktionspolicy,
  eingefrorenes `generatedAtUtc` und erwartete Exportpfade.
- `evidence-manifest.json` enthaelt Pfad, Bytegroesse und SHA-256 von
  `study-manifest.json` und allen kanonischen JSONL-/CSV-Datenexporten, niemals
  seiner selbst, `summary.json`, `report.md` oder `EXPORT.SHA256`.
- `summary.json` bindet den Hash von `evidence-manifest.json`, Study,
  Protokollversion, Baseline-/Head-Commit und Ergebnis-/Input-Tree.
- `report.md` wird aus genau diesem verifizierten Export mit festem Template
  erzeugt und bindet Study, Protokollversion, Baseline-/Head-Commit,
  Ergebnis-/Input-Tree sowie die SHA-256 von `evidence-manifest.json` und
  `summary.json`. Es enthaelt keine aktuelle Uhr und keine freie Reihenfolge.
- `EXPORT.SHA256` ist die aeusserste, nichtrekursive Schicht. Es listet Pfad
  und SHA-256 jeder Exportdatei ausser sich selbst, also auch
  `study-manifest.json`, `evidence-manifest.json`, `summary.json` und
  `report.md`, in Unicode-Codepoint-Reihenfolge. Keine innere Datei hasht
  `EXPORT.SHA256`; dadurch ist der Export vollstaendig und kreisfrei.
- `observation.closed` bindet das eingefrorene Quellmanifest, weil Exporte erst
  danach entstehen. Keine Exportdatei wird in die Primaerkette zurueckgeschrieben.

## Exportpruefung

Ein reproduzierter Export ist byteidentisch, wenn jede erwartete Datei
existiert, keine zusaetzliche Datei vorliegt und Pfad, Bytegroesse und SHA-256
vollstaendig uebereinstimmen. `EXPORT.SHA256` muss jede andere Datei exakt
einmal nennen und wird selbst separat gegen den erwarteten aeusseren Hash
geprueft. Semantisch gleiches, aber anders serialisiertes JSON ist kein
byteidentischer Pass.

Der Vergleich berichtet mindestens:

- Eingabemanifest-Hash,
- Protokollbundle-Hash,
- Collector-/Exporter- und Toolchainversion,
- Dateiliste beider Exporte,
- Hashvergleich je Datei,
- Gesamtresultat `true`, `false` oder literal `unknown`.

## Ledger-Schreib- und Recovery-Vertrag

Jede Beobachtung hat genau einen Writer unter exklusivem OS-Dateilock. Unter
dem Lock werden letzte Sequenz und letzter Hash verifiziert. Die komplette
kanonische JSONL-Zeile wird auf demselben Dateisystem temporaer geschrieben
und fsynct, danach atomar in das Ledger angehaengt und das Ledger fsynct.
Lockkonkurrenz scheitert mit `CONCURRENT_WRITER`; es gibt kein
last-write-wins.

Ein nicht LF-abgeschlossener oder hashungueltiger letzter Satz ist
`TORN_TAIL`. `research verify` bricht fail-closed ab und veraendert das Original
nicht. Nur ein explizites `--recover-to NEW_PATH` darf eine neue Datei aus dem
laengsten verifizierten Praefix plus `ledger.recovery.recorded` erzeugen. Das
Original wird niemals still gekuerzt, repariert oder ueberschrieben.

## Pflicht-Testmatrix

| Fall | Fixture/Aktion | erwartetes Ergebnis |
|---|---|---|
| kanonischer Roundtrip | gueltige Events importieren, exportieren, erneut importieren/exportieren | alle kanonischen Bytes und `EXPORT.SHA256` identisch |
| manipulierte Kette | Byte in einem Event aendern | `verify` fail-closed, kein Export |
| manipulierter Export | Byte nach Export aendern | aeussere Manifestpruefung fail-closed |
| doppelte ID | `eventId` oder `interventionId` wiederverwenden | Validierungsfehler, kein Append |
| Providerkosten fehlen | Providerreceipt ohne Kostenfeld | exakte und estimated Kosten literal `unknown` |
| Providerkosten unknown | `costProvenance=unknown` | exakte und estimated Kosten literal `unknown` |
| unterbrochener/resumierter Run | Run endet nicht, spaeter `run.resumed` mit gebundener Quelle | offener Erstlauf bleibt sichtbar; Kontinuitaet nur bei aufloesbarer Bindung |
| Crash mitten im Write/Torn Tail | letzte JSONL-Zeile unvollstaendig | `TORN_TAIL`, Original unveraendert; Recovery nur nach `--recover-to` |
| konkurrierender Writer | zweiter Append waehrend gehaltenem Lock | `CONCURRENT_WRITER`, keine Teilzeile |
| Wall-Clock rueckwaerts | spaetere UTC-Zeit kleiner, Monotonzeit gueltig | UTC-Dauer ungueltig/`unknown`; monotone Dauer bleibt nur innerhalb derselben Uhr gueltig |
| Tokens fehlen | Receipt ohne eine benoetigte Tokenklasse | betroffene Token-/Effizienzmetrik `unknown`, nie 0 |
| Modell unbekannt | fehlende konkrete `modelVersion` | `modelVersion=unknown`, keine Familienimputation |
| Intervention offen | `started` ohne `ended` | `INT-OPEN` steigt; Dauer literal `unknown` |
| Secret-Redaction | Fixture enthaelt Credential-/PII-Muster | Rohsecret erscheint in keiner persistierten oder exportierten Datei |
| malformed Git-Import | bewegliche, unaufloesbare oder syntaktisch falsche Grenze | `import-git-history` scheitert ohne Ereignisse |
| verspaetetes/doppeltes Begin | Zielereignis existiert bereits oder dieselbe/zweite Beobachtung ist aktiv | `PROSPECTIVE_START_TOO_LATE` beziehungsweise Active-Konflikt; kein Marker und keine neue prospektive Kette |
| Close ohne Outcome-Receipt | fehlender, unaufloesbarer oder nicht zum Zielpfad passender Receipt | fail-closed; Kette und Active-Marker bleiben offen und unveraendert |
| Crash vor Marker-Rename | Prozessabbruch nach Startketten-fsync, aber vor Rename | kein erfolgreicher Aktivierungsreceipt; kein Zielstart; Startkette bleibt `INCOMPLETE_ACTIVATION`, Marker wird nicht rekonstruiert und keine prospektive Verwendbarkeit behauptet |
| Crash nach Marker-Rename vor Directory-fsync | Prozessabbruch zwischen Rename und Parent-Directory-fsync | kein erfolgreicher Aktivierungsreceipt; nach Neustart wird vorhandener Marker samt Kette idempotent fsynct/revalidiert oder ein fehlender Marker als `INCOMPLETE_ACTIVATION` behandelt; keine rueckwirkende Behauptung |
| Crash nach Abschluss-fsync vor Marker-Unlink | Prozessabbruch nach finaler Kette, vor Unlink | geschlossene Kette plus `STALE_ACTIVE_MARKER`; Hooks bleiben inaktiv, Close-Retry entfernt ihn ohne doppeltes Abschlussereignis |
| Crash nach Marker-Unlink vor Directory-fsync | Prozessabbruch zwischen Unlink und Parent-Directory-fsync | geschlossene Kette bleibt autoritativ; ein nach Neustart vorhandener Stale-Marker wird idempotent entfernt, ein fehlender bleibt fehlend; Hooks bleiben inaktiv |
| Evidenzklassentrennung | drei Klassen im Eingabepool | getrennte Beobachtungen, keine klassenuebergreifende Aggregation |
| deterministischer Export | gleiche Inputs in zwei frischen Verzeichnissen | jede Datei einschliesslich `report.md` und `EXPORT.SHA256` byteidentisch |

## Isolierte Ablationen

Jede Ablation verwendet denselben eingefrorenen Kontrollinput. Ein
deterministischer Transform erzeugt eine neue Fixturekopie und ein
Transformmanifest mit Ablation-ID, einzigem veraenderten Faktor, Vorher-/
Nachherhash und Werkzeugversion. Die Kontrolle wird danach erneut aus dem
Originalinput exportiert; sie darf nicht aus dem Ablationsoutput rekonstruiert
werden.

ABL-01, ABL-02 und ABL-03 laufen jeweils in einem eigenen frischen
Ausgabeverzeichnis. Eine zweite Aenderung, gemischte Evidenzklasse oder ein
unbeabsichtigtes Outcome-/Gate-Delta macht die Ablation ungueltig. Alle
Ablationsdateien tragen sichtbar `synthetic-test-only`.

## Reproduktionsbericht

Der Bericht nennt exakt:

- untersuchte Beobachtung und Evidenzklasse,
- Baseline-/Ergebniscommit oder `unknown`,
- Protokoll- und Eingabemanifesthash,
- ausgefuehrte Befehlsdigests und Exitcodes,
- Exporthashes,
- abweichende Bytes/Dateien,
- alle `unknown`-Metriken samt Grund,
- bestaetigte Nichtinterferenz oder `unknown`,
- Resultat der Hypothese getrennt vom Taskoutcome.

Ein fehlgeschlagener Reproduktionslauf bleibt publizierbares Negativergebnis.
Er darf nicht durch Auswahl eines guenstigeren Exports ersetzt werden.
