# Vertrag für den Blender-Kalibrierungsgenerator

Status: **verbindlicher Vertrag für T-005, T-006 und T-007; T-005 ist unabhängig abgenommen, T-006 ist `READY`, T-007 bleibt bis zur Abnahme von T-006 `DRAFT`**

Dieser Vertrag schließt den ersten 3D-Spike technisch, ohne eine Welt-, Kultur-, Architektur- oder Art-Bible-Entscheidung zu treffen. Die Familie ist ein neutrales, nicht shipping-fähiges Kalibrierungsobjekt. Sie darf weder als visuelle Vorlage für die Welt noch als Nachweis für Originalität, Lizenzfreigabe, Laufzeitintegration oder Zielhardwareleistung behandelt werden.

## 1. Verantwortungsgrenzen

| Task | Ergebnis | Darf Blender starten? | Darf Repository-Metadaten publizieren? |
|---|---|---:|---:|
| T-005 | striktes `calibration-v1`-Spec, Referenzmathematik und unabhängiger GLB-/PNG-/Report-Inspector | nein | nein |
| T-006 | isolierter Generator sowie transaktionaler T-003-Quarantäne-Lifecycle | ja, nur gepinnt und isoliert | nur Receipt und Manifest nach erfolgreicher Prüfung |
| T-007 | Fresh-Checkout-, Pin-, Determinismus-, Recovery- und CI-Nachweis | ja, im T-006-Sandboxvertrag | nein; nur bereinigte CI-Evidenz |

Alle drei Tasks hängen direkt von T-003 ab. T-006 hängt zusätzlich von T-005, T-007 zusätzlich von T-005 und T-006 ab. Keiner der Tasks nimmt ein Asset nach `assets/source/` auf. Q-AST-001, Q-AST-002 und Q-AST-004 blockieren diesen quarantänisierten Kalibrierungspfad nicht; sie bleiben Gates vor Modellzulassung, Source-Promotion beziehungsweise produktionsnaher Assetbudgetierung.

### 1.1 Geplanter Dateiscope der Implementierung

Die spätere Implementierung bleibt auf diese Verantwortungsgruppen begrenzt. Die Pfade sind Teil der Taskabnahme; Umbenennung oder Zusammenlegung erfordert vor Implementierungsstart eine aktualisierte Taskprüfung, aber keine neue Produktentscheidung.

| Task | neue Hauptdateien | zulässige Integrationsdateien |
|---|---|---|
| T-005 | `.ai/schemas/blender-calibration-v1.schema.json`, `.ai/schemas/blender-technique-report.schema.json`, `assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json`, `tools/RiftHarness/BlenderCalibration.fs`, `tools/RiftHarness/Asset3dInspector.fs`, `tests/RiftHarness.Tests/BlenderCalibrationTests.fs` | `tools/RiftHarness/Program.fs`, beide betroffenen `.fsproj`/Lockfiles, `scripts/rift.sh`, Tool-/Test-README und Asset-Pipeline-Doku |
| T-006 | `.ai/schemas/blender-install-evidence.schema.json`, `.ai/schemas/asset-job-journal-entry.schema.json`, `tools/RiftHarness/BlenderGenerator.fs`, `tools/RiftHarness/LinuxSandbox.fs`, `tools/RiftHarness/AssetJobJournal.fs`, `tools/BlenderCalibration/generate.py`, `tests/RiftHarness.Tests/BlenderGeneratorTests.fs` | `tools/RiftHarness/Program.fs`, beide betroffenen `.fsproj`/Lockfiles, `scripts/rift.sh`, `scripts/bootstrap-blender-linux.sh`, T-003-Integrationstests und Tool-/Asset-Doku |
| T-007 | `tests/RiftHarness.Tests/BlenderFreshCheckoutPolicyTests.fs` und bereinigtes CI-Evidenzschema, falls das bestehende Evidenzformat es nicht ausdrücken kann | `.github/workflows/verify.yml`, `scripts/fresh-checkout-test.sh`, betroffene Testprojektdatei und CI-/Toolchain-Doku |

Es werden keine GLB-, PNG- oder Blenderdateien als Testfixture eingecheckt. T-005 baut seine kleinen synthetischen Positiv- und Korruptionsbytes im Testcode beziehungsweise in temporären Verzeichnissen. Keine der drei Einheiten darf ein NuGet-/pip-Paket oder ein weiteres ausführbares Werkzeug hinzufügen.

## 2. Öffentlicher CLI-Vertrag

Die spätere Implementierung erweitert den gemeinsamen Einstiegspunkt um diese exakten Verben:

```text
./scripts/rift.sh blender-calibration validate-spec --spec <repo-relativer-pfad>
./scripts/rift.sh blender-calibration inspect --spec <repo-relativer-pfad> --glb <repo-relativer-pfad> --preview <repo-relativer-pfad> --report <repo-relativer-pfad>
./scripts/rift.sh blender-calibration generate --spec <repo-relativer-pfad> --job-id <ULID> --blender <absoluter-pfad>
./scripts/rift.sh blender-calibration recover --job-id <ULID>
```

Argumente sind case-sensitive und dürfen genau einmal vorkommen. Das Subverb ist das erste Positionsargument; allein `--workspace <pfad>` darf davor oder danach stehen. Die Optionen des jeweiligen Subverbs dürfen danach in beliebiger Reihenfolge stehen. Unbekannte Optionen und zusätzliche Positionsargumente sind Fehler. `generate` ist Linux-x64-only. `validate-spec` und `inspect` sind reine .NET-Verben und starten keinen Unterprozess.

### 2.1 Ausgabe

Jeder Aufruf schreibt genau ein UTF-8-JSON-Objekt ohne BOM, ohne Einrückung und mit genau einem abschließenden LF auf stdout. Eigenschaften sind rekursiv nach ordinalem UTF-8-Schlüsselnamen sortiert. Erfolg und Fehler haben diese Hüllen:

```json
{"command":"validate-spec","ok":true,"result":{},"schemaVersion":1}
{"command":"validate-spec","error":{"code":"INVALID_SPEC","message":"validation failed"},"ok":false,"schemaVersion":1}
```

`validate-spec.result` besitzt exakt die Felder
`familyDecodedGeometryBytes`, `familyId`, `moduleCount`, `profile`,
`renderPrimitiveCount`, `specPath` und `specSha256`. `inspect.result` besitzt
exakt `familyDecodedGeometryBytes`, `familyId`, `glbBytes`, `glbPath`,
`glbSha256`, `materialCount`, `moduleCount`, `previewBytes`, `previewPath`,
`previewSha256`, `renderPrimitiveCount`, `reportBytes`, `reportPath`,
`reportSha256`, `specPath` und `specSha256`. Diese geschlossenen Hüllen sind
Teil des CLI-Vertrags; weitere Diagnose-, Claim- oder volatile Felder sind
verboten.

`message` ist eine feste, nicht von Eingabeinhalten abgeleitete Kurzmeldung. `result` enthält nur IDs, Zähler, SHA-256 und workspace-relative POSIX-Pfade. Absolute Pfade, Hostname, Benutzername, Umgebungswerte, rohe Speczeilen und Blender-Ausgabe sind auf stdout und stderr verboten. stderr ist UTF-8, auf 1 MiB begrenzt und darf nur bereinigte Diagnosecodes enthalten.

| Exit | Bedeutung | stabile Fehlercodes |
|---:|---|---|
| 0 | Erfolg | – |
| 2 | CLI, Spec oder Pfad ungültig | `INVALID_ARGUMENT`, `INVALID_SPEC`, `UNSAFE_PATH` |
| 3 | Plattform, Blender-Pin oder Installationsevidenz ungültig | `UNSUPPORTED_PLATFORM`, `PIN_MISMATCH` |
| 4 | Sandbox, Unterprozess, Timeout oder Ressourcenlimit | `ISOLATION_FAILED`, `PROCESS_FAILED`, `RESOURCE_LIMIT` |
| 5 | GLB, PNG, Report, Budget oder Determinismus ungültig | `INVALID_ARTIFACT`, `BUDGET_EXCEEDED`, `DETERMINISM_MISMATCH` |
| 6 | Harness-, Receipt-, Manifest- oder T-003-Crosscheck ungültig | `PROVENANCE_FAILED` |
| 7 | Journal-, Lock-, Publikations- oder Recovery-Konflikt | `TRANSACTION_CONFLICT` |
| 8 | unerwarteter interner Fehler | `INTERNAL_ERROR` |

## 3. Safe-Path- und Eingabelimits

Repo-relative Pfade werden vor jedem Zugriff als POSIX-Pfade normalisiert. Sie sind höchstens 240 UTF-8-Bytes lang, jedes Segment höchstens 80 Bytes. Leere Segmente, `.`, `..`, Backslash, Doppelpunkt, NUL, Steuerzeichen, führender Slash und nicht normalisierte Unicodeformen werden abgelehnt. Jede bereits existierende Komponente wird ohne Symlinkfolge geöffnet und unmittelbar vor Nutzung erneut geprüft. Dateien müssen regulär sein; Race- oder Typänderung ist `UNSAFE_PATH`.

Erlaubte Lesewurzeln:

- produktives Spec: `assets/specs/3d/`
- Testfixtures: `tests/Fixtures/Asset3d/`
- Jobstaging: `.ai/runtime/asset-jobs/<job-id>/`
- lokale 3D-Quarantäne: `assets/quarantine/3d/`

Zusätzlich darf der Orchestrator nur die fest abgeleiteten Dateien `toolchain.lock.json`, die fünf Generatorquellen aus Abschnitt 5, `.ai/runtime/toolchain/blender-5.2.0-linux-x64.install.json` sowie die von T-003 für Run-, Receipt-, Manifest-, Modelllock- und Clean-Room-Prüfung benötigten versionierten Schemas/Policies lesen. Diese Pfade sind keine CLI-Eingaben. Schreibziele außerhalb des Jobroots sind ausschließlich die abgeleiteten T-003-Runpfade sowie `assets/quarantine/3d/`, `assets/receipts/` und `assets/manifests/` im Journalablauf aus Abschnitt 13.

`--blender` ist die einzige absolute Pfadausnahme. Sie wird kanonisch aufgelöst, nie ausgegeben und muss eine reguläre, ausführbare, außerhalb des Repositorys liegende Datei sein. Die Installationsevidenz hat den abgeleiteten repo-relativen Pfad `.ai/runtime/toolchain/blender-5.2.0-linux-x64.install.json`; sie ist kein frei wählbares Argument. Job-IDs sind exakt 26 Zeichen lange, großgeschriebene ULIDs nach `[0-9A-HJKMNP-TV-Z]{26}`. Alle Ausgabepfade werden aus Spec-Hash und Job-ID abgeleitet, niemals vom Aufrufer gewählt.

| Eingabe/Ausgabe | harte Grenze |
|---|---:|
| Spec | 16 KiB, JSON-Tiefe 6, 64 Eigenschaften einschließlich verschachtelter Eigenschaften |
| GLB | 2.097.152 Bytes |
| PNG | 8.388.608 Bytes |
| Technikreport | 1.048.576 Bytes, JSON-Tiefe 8 |
| einzelne Prozessdatei | 16.777.216 Bytes |
| Dateien im Jobroot | 64 |
| Summe im Jobroot | 25.165.824 Bytes |
| stdout / stderr | je 1.048.576 Bytes |

## 4. Exaktes `calibration-v1`-Spec

Das Spec ist die einzige kreative Dateneingabe. Es enthält keine Pfade, URIs, Prompts, Negativprompts, Namen, Beschreibungen oder Referenzen. Alle variablen Werte sind JSON-Ganzzahlen. Alle Eigenschaften sind erforderlich; `null`, Fließkommazahlen, Exponentialnotation und unbekannte Eigenschaften sind verboten.

```json
{
  "familyId": "CAL-STONEWOOD-V1",
  "geometry": {
    "lintelHeightMm": 250,
    "moduleHeightMm": 3000,
    "moduleWidthMm": 4000,
    "mortarGapMm": 20,
    "openingHeightMm": 2000,
    "openingWidthMm": 1600,
    "stoneCourseHeightMm": 250,
    "stoneDepthJitterMm": 30,
    "stoneLengthJitterMm": 40,
    "stoneOffsetJitterMm": 20,
    "timberDepthMm": 160,
    "timberWidthMm": 180,
    "wallThicknessMm": 400
  },
  "materials": {
    "stoneBaseColorSrgb8": [96, 92, 82],
    "stoneMetallicPermille": 0,
    "stoneRoughnessPermille": 850,
    "woodBaseColorSrgb8": [92, 58, 32],
    "woodMetallicPermille": 0,
    "woodRoughnessPermille": 720
  },
  "profile": "calibration-v1",
  "schemaVersion": 1,
  "seed": 1592594996
}
```

Die mehrzeilige Darstellung dient nur der Lesbarkeit. Die später versionierte Specdatei muss bereits den kanonischen Einzeilenbytes aus Abschnitt 5 entsprechen; `validate-spec` lehnt eine semantisch gleiche, aber nicht kanonische Datei ab.

### 4.1 Felder und Einzelgrenzen

| Feld | Vertrag |
|---|---|
| `schemaVersion` | exakt `1` |
| `profile` | exakt `calibration-v1` |
| `familyId` | exakt `CAL-STONEWOOD-V1` |
| `seed` | `0..4294967295` |
| `moduleWidthMm` | exakt `4000` |
| `moduleHeightMm` | `2400..3600` |
| `wallThicknessMm` | `300..600` |
| `openingWidthMm` | gerade Zahl, `1200..2000` |
| `openingHeightMm` | `1800..2400` |
| `lintelHeightMm` | `250..400` |
| `stoneCourseHeightMm` | `250..400` |
| `mortarGapMm` | `10..40` |
| `timberWidthMm` | `120..240` |
| `timberDepthMm` | `100..240` |
| `stoneLengthJitterMm` | `0..80` |
| `stoneDepthJitterMm` | `0..60` |
| `stoneOffsetJitterMm` | `0..40` |
| jedes `*BaseColorSrgb8`-Element | `0..255`, genau drei Elemente |
| `stoneRoughnessPermille`, `woodRoughnessPermille` | `500..1000` |
| `stoneMetallicPermille`, `woodMetallicPermille` | `0..100` |

### 4.2 Cross-Field-Formeln

Mit `W=moduleWidthMm`, `H=moduleHeightMm`, `T=wallThicknessMm`, `O=openingWidthMm`, `OH=openingHeightMm`, `LH=lintelHeightMm`, `C=stoneCourseHeightMm`, `G=mortarGapMm`, `TW=timberWidthMm`, `TD=timberDepthMm`, `JL=stoneLengthJitterMm`, `JD=stoneDepthJitterMm` und `JO=stoneOffsetJitterMm` müssen alle Bedingungen gelten:

```text
H mod C = 0
OH mod C = 0
LH mod C = 0
OH + LH <= H - C
O + 2*TW <= W - 2*(2*C)
TD <= T
4*G <= C
2*JO + JL + G <= C
2*JD <= T
```

Zusätzlich berechnet der Validator vorab die Boxobergrenzen aus Abschnitt 6 und lehnt jedes Spec ab, dessen theoretische LOD-/Kollisionswerte ein Budget aus Abschnitt 9 überschreiten würden. Es gibt keine Defaults oder automatische Korrektur.

`TD<=T`, `4*G<=C`, `2*JD<=T` und `2*JO+JL+G<=C` sind bewusste
Defense-in-depth-Invarianten: Die strengeren v1-Einzelgrenzen implizieren sie
bereits. Ihre isolierten Negativtests rufen daher den reinen Formelauswerter
mit internen, außerhalb der Parserdomäne liegenden Zahlen auf; Parserfixtures
belegen zusätzlich die jeweils stärkere Einzelgrenze. Ein Test darf nicht
fälschlich behaupten, nur eine Cross-Field-Regel verletzt zu haben, wenn
zugleich eine öffentliche Einzelgrenze verletzt ist.

## 5. Kanonisierung, Hashes und PRNG

Spec und Technikreport verwenden rekursiv ordinal sortierte Eigenschaften, feste Arrayreihenfolge, UTF-8 ohne BOM, minimale JSON-Escapes, keine insignifikanten Leerzeichen und genau ein LF am Ende. Zahlen sind Dezimal-Ganzzahlen ohne führende Null oder `-0`. `specSha256` ist SHA-256 über genau diese kanonischen Specbytes.

Das Referenzspec aus Abschnitt 4 hat einschließlich seines abschließenden LF den SHA-256 `39faae34c4cd515cb724a8ef1e2e4bee159a232136218fbb8afd8edd52db2cf8`.

`generatorSourceSha256` ist SHA-256 über die Verkettung
`relativePath + LF + fileSha256 + LF` für jede Generatorquelldatei in ordinaler Pfadreihenfolge. Das Inventar ist geschlossen und besteht aus `tools/BlenderCalibration/generate.py`, `tools/RiftHarness/AssetJobJournal.fs`, `tools/RiftHarness/BlenderCalibration.fs`, `tools/RiftHarness/BlenderGenerator.fs` und `tools/RiftHarness/LinuxSandbox.fs`. Es enthält keine Buildoutputs und wird im Technikreport genau in dieser ordinalen Reihenfolge aufgeführt. Der Toolchain-Pinhash ist SHA-256 über das kanonisch serialisierte Blender-Objekt aus `toolchain.lock.json`.

### 5.1 PCG32

Alle Zufallsentscheidungen verwenden ausschließlich PCG-XSH-RR 32 mit unsigned Wraparound:

```text
multiplier = 6364136223846793005
increment  = 1442695040888963407
state = 0
next()
state = (state + uint64(seed)) mod 2^64
next()

next():
  old = state
  state = (old * multiplier + increment) mod 2^64
  xorshifted = uint32(((old >> 18) xor old) >> 27)
  rotation = uint32(old >> 59)
  return rotate_right_32(xorshifted, rotation)
```

Für `bounded(bound)` mit `1 <= bound <= 2^31` gilt `threshold = uint32(-bound) mod bound`; Werte kleiner als `threshold` werden verworfen, sonst wird `next() mod bound` geliefert. `signed(J)` ist `int(bounded(2*J+1))-J`. Auch bei `J=0` wird ein Wert gezogen. Die ersten zehn zurückgegebenen Werte des Referenzseeds `1592594996` sind:

```text
2931784231, 1733122091, 677491881, 1055047052, 458198092,
2644956477, 2758542496, 1581573961, 1174968268, 55324810
```

Der Alternativseed für Tests ist `1592594997`.

## 6. Geometrieformeln und Zugreihenfolge

Alle Spec- und Jitterwerte sind ganzzahlige Millimeter. Ableitungen werden verlustfrei in ganzzahligen Mikrometern gerechnet, damit auch Halbierungen exakt bleiben. Erst unmittelbar beim Erstellen eines Mesh-Vertices wird `float32(micrometres / 1000000.0)` gebildet. Negative Null wird zu positiver Null normalisiert. Es gibt keine Uhr-, Thread-, Hashmap- oder Dateisystemordnung als Eingabe.

Die Modulreihenfolge lautet `WALL-STRAIGHT`, `WALL-CORNER`, `WALL-OPENING`. Innerhalb eines Moduls gilt: X-Segment vor Y-Segment, Kurs von unten nach oben, opaker Bereich von negativer zu positiver Achse, Kandidat von kleiner zu größerer Tangentenkoordinate. Für jeden LOD0-Steinkandidaten werden unabhängig davon, ob ein Jitterwert null ist, genau diese drei Werte gezogen:

```text
lengthReductionMm = bounded(JL + 1)
depthDeltaMm      = signed(JD)
tangentOffsetMm   = signed(JO)
```

Ein Kurs belegt `z=[course*C+G/2, (course+1)*C-G/2]`. Die Zellbreite ist `4*C`. Gerade Kurse beginnen am Segmentminimum, ungerade Kurse um `2*C` nach links versetzt. Alle Zellen mit nichtleerem Schnitt zum opaken Segment werden an dessen Grenze geclippt. Danach gilt:

```text
nominalLength = clippedLength - G
stoneLength = nominalLength - lengthReductionMm
stoneDepth = T + depthDeltaMm
centerMin = clipMin + G/2 + stoneLength/2
centerMax = clipMax - G/2 - stoneLength/2
stoneCenter = clamp(clippedCenter + tangentOffsetMm, centerMin, centerMax)
```

Nichtpositive Längen sind ein Validatorfehler. Bei `WALL-STRAIGHT` und
`WALL-OPENING` bleibt die Tiefe um die Wandmittelebene zentriert. Beim
`WALL-CORNER` bleibt dagegen die nahtseitige Fläche jedes Beins trotz
`depthDeltaMm` exakt auf `T/2`; der Tiefenjitter wirkt ausschließlich von der
Naht weg. Für das X-Bein gilt deshalb in seiner normalen Y-Richtung
`maxY=T/2`, `minY=T/2-(T+depthDeltaMm)` und
`centerY=-depthDeltaMm/2`. Für das Y-Bein gelten dieselben Formeln in der
normalen X-Richtung. Der Zufallswert wird unverändert gezogen und die
Zugreihenfolge ändert sich nicht. Dadurch berühren sich beide Volumen nur an
der Nahtfläche und überlappen sich auch bei positivem Tiefenjitter nicht.
LOD0 verwendet je Kandidat eine geschlossene Box. Jede Box besitzt 24
getrennte Vertices, harte Normalen, UV0 je Fläche im Bereich `0..1` und 36
`UNSIGNED_SHORT`-Indizes für 12 Dreiecke.

Opaque Steinsegmente:

- `WALL-STRAIGHT`: X von `-W/2` bis `W/2` in jedem Kurs.
- `WALL-CORNER`: X-Bein mit axialem X-Intervall `[0,W]`; Y-Bein mit axialem Y-Intervall `[T/2,W]`. Beide haben die nominale Tiefe `T`; LOD0 variiert ihre tatsächliche Tiefe nach der einseitigen Jitterregel oben. Ihre Volumen berühren sich an der Nahtfläche `T/2`, überlappen sich aber nicht; damit wird die Ecke nur einmal belegt.
- `WALL-OPENING`: unterhalb `OH+LH` nur X-Segmente `[-W/2,-O/2]` und `[O/2,W/2]`, ab `OH+LH` das volle X-Segment. Da `OH` und `LH` Vielfache von `C` sind, gibt es keinen angeschnittenen Kurs.

Holzboxen verwenden keine Zufallswerte:

- `WALL-STRAIGHT`: zwei Pfosten mit Querschnitt `TW x TD`, Höhe `H`, Zentren bei `x=±(W/2-TW/2)`.
- `WALL-CORNER`: ein Eckpfosten `TW x TW`, zwei Endpfosten `TW x TD`; Zentren liegen bei `(TW/2,TW/2)`, `(W-TW/2,0)` und `(0,W-TW/2)`.
- `WALL-OPENING`: zwei Pfosten mit Höhe `OH` bei `x=±(O/2+TW/2)` und ein Sturz mit Länge `O+2*TW`, Tiefe `TD`, Höhe `LH`, Zentrum bei `z=OH+LH/2`.

LOD1 gruppiert innerhalb desselben Kurses, Beins und opaken Segments jeweils
höchstens acht aufeinanderfolgende LOD0-Steinboxen und ersetzt sie durch ihre
ganzzahlige umschließende Box. LOD2 ignoriert Einzelsteine und verwendet eine
Steinbox für `WALL-STRAIGHT`, zwei überlappungsfrei gekürzte Steinboxen für
`WALL-CORNER` und drei Steinboxen für linke Seite, rechte Seite und Oberfeld
von `WALL-OPENING`. Die beiden Corner-Steinboxen haben im Blender-Raum exakt
diese ganzzahligen Millimetergrenzen: X-Bein `min=(0,-T/2,0)`,
`max=(W,T/2,H)`; Y-Bein `min=(-T/2,T/2,0)`, `max=(T/2,W,H)`. Die normative
Mikrometerrechnung verwendet für jede halbe Dicke `T*1000/2`; sie ist für
jeden ganzzahligen Millimeterwert verlustfrei. Die Holzboxen sind in allen drei
Render-LODs gleich. Die Kollision entspricht ausschließlich den
LOD2-Steinboxen und enthält kein Render-Material.

Für die Vorabbudgetierung sei `cells(L,p)=ceil((L+p*2*C)/(4*C))` mit Kursparität `p` gleich `0` für gerade und `1` für ungerade Kurse, `N=H/C`, `S=(W-O)/2` und `K=(OH+LH)/C`. Die LOD0-Steinboxzahlen sind exakt:

```text
straight = sum(c=0..N-1, cells(W,c mod 2))
corner   = sum(c=0..N-1, cells(W,c mod 2) + cells(W-T/2,c mod 2))
opening  = sum(c=0..K-1, 2*cells(S,c mod 2))
         + sum(c=K..N-1, cells(W,c mod 2))
```

Für LOD1 wird in denselben Summen jedes `cells(L,p)` durch `ceil(cells(L,p)/8)` ersetzt. Renderboxzahlen entstehen durch Addition der Holzboxzahlen `2`, `3`, `3` für Straight, Corner, Opening. LOD2 hat einschließlich Holz exakt `3`, `5`, `6` Renderboxen; die Kollision exakt `1`, `2`, `3` Steinboxen. Je Renderbox gelten 24 Vertices und 12 Dreiecke. Diese Formeln werden bereits bei Specvalidierung gegen Abschnitt 9 gerechnet.

Für das Referenzspec ergeben sich damit diese festen Inspectorwerte; Indizes sind jeweils `3*Dreiecke`, jedes Render-LOD besitzt zwei und jede Kollision ein Primitive:

| Modul | LOD0 V/T | LOD1 V/T | LOD2 V/T | Kollision T | dekodierte Geometrie gesamt |
|---|---:|---:|---:|---:|---:|
| `WALL-STRAIGHT` | 1.344 / 672 | 336 / 168 | 72 / 36 | 12 | 61.968 Bytes |
| `WALL-CORNER` | 2.664 / 1.332 | 648 / 324 | 120 / 60 | 24 | 121.416 Bytes |
| `WALL-OPENING` | 1.272 / 636 | 576 / 288 | 144 / 72 | 36 | 71.664 Bytes |

Die Familie belegt nach der Formel aus Abschnitt 9 insgesamt 255.048 dekodierte Geometriebytes und 18 Renderprimitives. Jitter verändert Positionen und Bounds, nicht diese Zähler.

`boundsMicrometres` bezeichnet immer die Union sämtlicher `POSITION`-Werte
aller drei Render-LODs und der Kollision eines Moduls. Für Referenz- und
Alternativseed gilt damit die folgende zusätzliche Testvektortabelle im
Blender-Raum; die GLB-Werte entstehen anschließend ausschließlich durch die
Achsenkonvertierung aus Abschnitt 7:

| Modul | Referenzseed min / max in µm | Alternativseed min / max in µm |
|---|---|---|
| `WALL-STRAIGHT` | `(-2000000,-215000,0)` / `(2000000,215000,3000000)` | `(-2000000,-214500,0)` / `(2000000,214500,3000000)` |
| `WALL-CORNER` | `(-230000,-229000,0)` / `(4000000,4000000,3000000)` | `(-230000,-230000,0)` / `(4000000,4000000,3000000)` |
| `WALL-OPENING` | `(-2000000,-214000,0)` / `(2000000,214000,3000000)` | `(-2000000,-214500,0)` / `(2000000,214500,3000000)` |

## 7. Achsen, Pivots, Snap-Punkte und Namen

Der logische Blender-Raum ist rechtshändig: `+X` entlang der Wand, `+Y` in die Tiefe und `+Z` nach oben; Einheit ist ein Meter. Der GLB-Raum ist rechtshändig mit `+Y` nach oben. Die normative Konvertierung lautet `glTF(x,y,z)=(x,z,-y)`.

| Modul | Pivot im Blender-Raum | Snap A | Snap B | Z-Vierteldrehungen A/B |
|---|---|---|---|---|
| `WALL-STRAIGHT` | `(0,0,0)` Bodenmitte | `(-2000,0,0)` | `(2000,0,0)` | `2 / 0` |
| `WALL-CORNER` | `(0,0,0)` äußere Bodenecke | `(4000,0,0)` | `(0,4000,0)` | `0 / 1` |
| `WALL-OPENING` | `(0,0,0)` Bodenmitte | `(-2000,0,0)` | `(2000,0,0)` | `2 / 0` |

Snap-Translationen werden durch Division durch 1000 und die Achsenkonvertierung geprüft. Eine Vierteldrehung wird im GLB als Rotation um `+Y` mit Quaternion `[0,sin(q*pi/4),0,cos(q*pi/4)]` geprüft; Toleranz je Komponente `1e-6`. Skalierung ist exakt eins, Roottransforms sind Identität.

Zulässige ASCII-Namen sind vollständig geschlossen:

```text
SCENE_CAL_STONEWOOD_V1
MOD_WALL_STRAIGHT, MOD_WALL_CORNER, MOD_WALL_OPENING
MESH_<MODUL>_LOD0, MESH_<MODUL>_LOD1, MESH_<MODUL>_LOD2
COL_<MODUL>
SNAP_<MODUL>_A, SNAP_<MODUL>_B
MAT_CAL_STONE, MAT_CAL_WOOD
```

`<MODUL>` ist genau `WALL_STRAIGHT`, `WALL_CORNER` oder `WALL_OPENING`. Suffixe, lokalisierten Text, Blender-Autobenennung, freie Extras und weitere Namen lehnt der Inspector ab.

## 8. Materialien, GLB, PNG und Technikreport

### 8.1 Materialien

Es existieren familienweit genau zwei glTF-PBR-Materialien. Alpha ist als
vierter Wert von `baseColorFactor` exakt `1`; die semantischen glTF-Defaults
`alphaMode=OPAQUE` und `doubleSided=false` werden vom gepinnten Exporter als
fehlende Felder geschrieben und müssen in v1 fehlen. Emission, Texturen und
Extensions fehlen. `baseColorFactor`, `metallicFactor` und `roughnessFactor`
sind vorhanden; Metallic und Roughness sind `permille/1000`. Jeder
sRGB8-Kanal `c` wird vor Übergabe an den BSDF zu linear konvertiert:

```text
s = c / 255
linear = s/12.92                         falls s <= 0.04045
linear = ((s+0.055)/1.055)^2.4           sonst
```

Der Inspector vergleicht exportierte float32-Werte mit absoluter Toleranz `1e-6`.

### 8.2 GLB

Das Familienartefakt heißt `family.glb`, ist glTF 2.0 und enthält genau einen JSON- und danach genau einen BIN-Chunk. Es enthält genau eine Szene mit den drei Modulroots in der Reihenfolge aus Abschnitt 6. Jeder LOD-Mesh besitzt höchstens zwei Primitives in der Reihenfolge Stein, Holz; die beiden Materialindizes verweisen familienweit auf dieselben Objekte. Kollisionsmeshes besitzen genau ein materialloses Primitive.

Die Szene besitzt exakt 21 Nodes und 12 Meshes. Ihre drei Rootnodes stehen in der Modulreihenfolge und tragen kein Mesh. Jeder Root hat exakt sechs Kinder in dieser Reihenfolge: LOD0, LOD1, LOD2, Collision, Snap A, Snap B. Die ersten vier Kinder tragen je ein gleichnamiges Mesh; Snapnodes tragen keines. Scene, Modulroots, Kindnodes, Meshes und Materialien müssen jeweils exakt den geschlossenen Namen aus Abschnitt 7 tragen. Buffer, BufferViews, Accessors und Primitives besitzen kein `name`-Feld; auch sonstige glTF-Objekte dürfen keine weiteren Namen einführen. Root-, Render- und Collisionnodes besitzen weder `matrix` noch TRS-Felder; ihre fehlende Transformation ist normativ die Identität. Snapnodes besitzen immer die exakte `translation`, niemals `matrix` oder `scale`; `rotation` fehlt genau bei Vierteldrehung 0 und ist für Vierteldrehung 1 oder 2 exakt vorhanden. Die fehlende Skalierung ist normativ `[1,1,1]`, die fehlende Rotation normativ die Identität. Es gibt keine weiteren Nodes oder Meshes. Pro Render-LOD werden alle Steinboxen zu einem Steinprimitive und alle Holzboxen zu einem Holzprimitive zusammengeführt; leere Renderprimitives sind verboten.

Jedes Renderprimitive besitzt genau `POSITION` und `NORMAL` als nicht normalisierte `FLOAT VEC3` sowie `TEXCOORD_0` als nicht normalisierte `FLOAT VEC2`. Ein Collisionprimitive besitzt genau `POSITION` und `NORMAL` als nicht normalisierte `FLOAT VEC3`, aber kein UV-Attribut und kein Material. Indizes sind `UNSIGNED_SHORT`, semantischer Modus ist `TRIANGLES`. Entsprechend dem gepinnten Exporter fehlen die Defaultfelder `primitive.mode`, `accessor.normalized` und `accessor.byteOffset`; der Inspector behandelt ihre Abwesenheit normativ als 4, `false` und 0 und lehnt explizite Varianten in v1 ab. Sparse Accessors, Morph Targets, Draco/Meshopt, Interleaving und geteilte Accessors zwischen Primitives sind in v1 verboten. Top-Level `scene` ist exakt 0, genau eine benannte Szene besitzt in `nodes` die drei Modulrootindizes in fester Reihenfolge und jede `children`-Liste löst in die vorgeschriebene Namensreihenfolge auf. Der einzige Buffer besitzt kein `uri`; jeder Primitive besitzt `indices`. `asset` enthält exakt `version="2.0"` und `generator="Khronos glTF Blender I/O v5.2.39"`; `minVersion`, Copyright und weitere Assetfelder fehlen. Der Generatorwert ist eine Eigenschaft des gepinnten Blender-5.2.0-Archivs und keine freie Eingabe. `extensionsUsed` und `extensionsRequired` fehlen ebenso. Es gibt keine Images, Textures, Samplers, Cameras, Lights, Skins, Animations, externe Buffer-URI, Copyrightangabe oder freien Extras. Buffer-, BufferView- und Accessorbereiche dürfen weder überlappen noch Padding als Daten referenzieren; alle Zahlen sind endlich.

Der Inspector liest vor Allokation den 12-Byte-Header und jedes Chunklimit. Zusätzlich zum 2-MiB-Gesamtlimit gelten höchstens 1 MiB JSON-Chunk, JSON-Tiefe 32, 64 Nodes, 16 Meshes, 32 Primitives, 128 Accessors, 128 BufferViews, zwei Materialien und ein Buffer. Überschreitung, Integerüberlauf, ungerichteter Nodezyklus, mehrfacher Parent oder ein Bereich außerhalb des BIN-Chunks ist `INVALID_ARTIFACT`. T-005 prüft im Technikreport die geschlossene Generatorquellen-Pfadliste, jeden syntaktisch gültigen SHA-256 sowie den daraus berechneten Aggregathash gegeneinander. Da vier der fünf Quellen erst T-006 gehören und bei T-005 absichtlich noch nicht existieren, bindet T-005 diese Hashwerte nicht an lokale Dateien. Die lokale Bytebindung aller fünf Quellen ist zwingend Teil von T-006.

### 8.3 Preview-PNG

Die Preview heißt `preview.png`. Sie zeigt nur Instanzen der drei Module mit identischen Meshdaten: Straight bei `(-5,0,0)`, Corner bei `(0,0,0)`, Opening bei `(5,0,0)`, jeweils ohne Rotation oder Skalierung. Sie verwendet `BLENDER_EEVEE_NEXT`, 64 Samples, 960x540 Pixel, 100 %, RGBA8, PNG-Kompressionsstufe 9, undurchsichtigen Film und genau Frame 1. Kamera: Position `(10,-14,9)` Meter im Blender-Raum, Ziel `(1,1,1.4)`, lokale `-Z`-Achse zum Ziel, lokale `+Y`-Achse möglichst zu Welt-`+Z`, Brennweite 50 mm, Sensorbreite 36 mm, Clipping `0.1..100`. Weltfarbe ist linear `(0.035,0.035,0.035)`. Ein Area-Light steht bei `(2,-4,10)`, Leistung 1200 W, Größe 8 m; ein zweites Area-Light bei `(-6,3,5)`, Leistung 500 W, Größe 6 m. Farbmanagement: `AgX`, Look `AgX - Medium High Contrast`, Exposure `0`, Gamma `1`.

`render.use_stamp` und alle einzelnen Stampfelder für Datum, Uhrzeit, Renderdauer, Host, Frame, Kamera, Szene, Marker, Speicher, Notiz, Lens, Label, Filename und Sequencer werden explizit deaktiviert. Der gepinnte Blender schreibt dennoch konstante ancillary Metadatenchunks einschließlich EXIF. Deshalb normalisiert das versionierte Generatorskript den Render anschließend deterministisch mit Python-Standardbibliothek: Es prüft Signatur, CRC und Chunkgrenzen, parst die Eingabe vollständig bis genau einem leeren `IEND`, verbietet nachlaufende Bytes und verlangt mindestens einen zusammenhängenden `IDAT`. Erst dann übernimmt es genau `IHDR` und die unveränderten `IDAT`-Payloads, schreibt ein neues leeres `IEND` und berechnet alle CRCs neu. Diese explizite `png-normalize-v1`-Transformation ist Teil der Provenienz; der rohe Blender-PNG ist nur Jobstaging und wird nie als Artefakt gehasht oder publiziert.

Der unabhängige PNG-Inspector verlangt Signatur, genau einen IHDR vor IDAT, mindestens einen IDAT, genau einen IEND, gültige CRCs, Breite 960, Höhe 540, Bittiefe 8, Farbart 6, Kompression/Filter/Interlace jeweils 0. Die geschlossene publizierte v1-Chunkfolge ist ausschließlich `IHDR`, ein oder mehrere unmittelbar zusammenhängende `IDAT`, `IEND`; alle anderen kritischen, ancillary, bekannten oder unbekannten Chunks sind verboten. Der Inspector dekomprimiert die verketteten IDAT-Daten begrenzt als zlib, verlangt exakt 540 Scanlines aus je einem Filterbyte `0..4` und 3840 RGBA-Nutzbytes und lehnt zu kurze, zu lange, nachlaufende oder ungültige Deflate-Daten ab. Byteidentität wird separat durch Wiederholung bewiesen.

### 8.4 Technikreport

`technique.json` ist kanonisches JSON nach Abschnitt 5 und hat exakt diese Top-Level-Felder:

```text
schemaVersion=1, profile, familyId, seed, specSha256,
generatorSourceSha256, generatorSources[], toolchainPinSha256,
artifacts{glb{path,sha256,bytes},preview{path,sha256,bytes}},
materials[], modules[], familyMetrics{}, limits{}
```

`modules` steht in der festen Modulreihenfolge und enthält je Modul `id`, `boundsMicrometres`, `snapPoints`, sowie für `lod0`, `lod1`, `lod2` und `collision` die ganzen Werte `vertices`, `indices`, `triangles`, `primitives` und `decodedGeometryBytes`. `materials` steht in der Namensreihenfolge aus Abschnitt 7 und enthält ausschließlich die kanonischen Specwerte. `limits` wiederholt die Zahlen aus Abschnitt 9. `familyMetrics` enthält `glbBytes`, `decodedGeometryBytes`, `materialCount` und `renderPrimitiveCount`. Job-ID, Run-ID, Zeit, Host, Benutzer, absolute Pfade, Blenderlogs und Report-Selbsthash sind verboten.

Die verschachtelten Formen sind ebenfalls geschlossen; unbekannte Felder sind überall verboten:

```text
generatorSources[] = {path,sha256}
artifacts.<kind>    = {bytes,path,sha256}
materials[]        = {baseColorSrgb8[3],metallicPermille,name,roughnessPermille}
snapPoints[]       = {id,rotationQuarterTurns,translationMm[3]}
metric             = {decodedGeometryBytes,indices,primitives,triangles,vertices}
modules[]          = {boundsMicrometres{max[3],min[3]},collision,id,lod0,lod1,lod2,snapPoints[2]}
familyMetrics      = {decodedGeometryBytes,glbBytes,materialCount,renderPrimitiveCount}
limits             = {collisionTriangles,decodedGeometryBytes,glbBytes,lod0Triangles,lod0Vertices,
                      lod1Triangles,lod1Vertices,lod2Triangles,lod2Vertices,
                      materials,renderPrimitivesPerLod}
```

`generatorSources` ist ordinal nach Pfad, `materials` nach Abschnitt 7, `modules` nach Abschnitt 6 und `snapPoints` A vor B sortiert. T-005 prüft ihre geschlossene Pfad-/Hashliste und den Aggregathash intern; die lokale Bytebindung aller fünf Quellen folgt in T-006, weil vier Dateien vorher absichtlich noch nicht existieren. `toolchainPinSha256` ist dagegen bereits in T-005 lokal gebunden: Der Inspector liest `toolchain.lock.json` sicher und begrenzt, verlangt genau einen Blender-Eintrag mit den fünf geschlossenen Feldern `id`, `license`, `platform`, `sha256`, `version`, kanonisiert dieses Objekt mit LF und vergleicht SHA-256. Für den aktuellen Pin sind das 163 Bytes mit Hash `697aadbbbf7125884bf1910c5d02bc694118f676c714a28bd3ae26c17d03abf4`. `artifacts.glb.path` und `artifacts.preview.path` sind die endgültigen logischen repo-relativen Zielpfade aus Abschnitt 12. Die physischen `--glb`-/`--preview`-Argumente dürfen für Staging und Tests unter den Safe-Path-Wurzeln abweichen; Bytezahl und Hash müssen dennoch mit den tatsächlich gelesenen Dateien übereinstimmen. `boundsMicrometres` wird im Blender-Raum berichtet: Der Inspector konvertiert jeden glTF-`POSITION`-Wert vor der Mikrometer-Union mit der inversen Abbildung `Blender(x,y,z)=(glTF.x,-glTF.z,glTF.y)` zurück. Die Union umfasst alle Render-LODs und die Kollision des jeweiligen Moduls und muss die Referenzgeometrie exakt umschließen; eine Float32-Rückrechnung darf höchstens 1 Mikrometer abweichen. `translationMm` und `rotationQuarterTurns` sind ebenfalls Blender-Raum-Werte und müssen Abschnitt 7 entsprechen.

## 9. Proxybudgets und Formeln

| Bereich je Modul | Vertices | Dreiecke |
|---|---:|---:|
| LOD0 | höchstens 3.072 | höchstens 4.096 |
| LOD1 | höchstens 1.024 | höchstens 1.024 |
| LOD2 | höchstens 256 | höchstens 192 |
| Kollision | – | höchstens 48 |

Zusätzlich gelten genau zwei familienweit geteilte Render-Materialien, höchstens zwei Render-Primitives je Modul und Render-LOD, Familien-GLB höchstens 2.097.152 Bytes und geschätzte dekodierte Familiengeometrie höchstens 2.097.152 Bytes.

Da v1 nur nicht überlappende, nicht interleavte und ungeteilte
Accessorranges zulässt, ist die dekodierte Geometriesumme bei gleichem
2-MiB-Limit bereits durch die kleinere GLB-Datei begrenzt und bleibt stets
geradzahlig. Dieses zweite Limit ist bewusst Defense-in-depth: Tests prüfen
die unabhängige Accessorformel, die Referenzsummen und abweichende
Reportwerte, statt einen physisch unmöglichen isolierten `Limit+1`-GLB zu
konstruieren. Erreichbare Zähl- und Dateigrenzen werden weiterhin jeweils um
genau eins verletzt.

Für jedes Primitive gilt `triangles=indexAccessor.count/3`; nicht durch drei teilbare Indizes sind ungültig. `vertices=POSITION.count`; bei Renderprimitives müssen NORMAL/UV0 dieselbe Anzahl besitzen. Da Attribute und Indizes v1-fest sind, gilt:

```text
decodedGeometryBytes je Primitive = vertices*(3*4 + 3*4 + 2*4) + indices*2
decodedGeometryBytes Familie = Summe über alle Render- und Kollisions-Primitives
```

Kollisionsvertices besitzen POSITION und NORMAL; dort gilt
`vertices*(3*4 + 3*4) + indices*2`. Kein Accessor darf von zwei Primitives
geteilt werden. Diese Zahlen sind konservative Strukturproxies für kleine,
instanzierbare Module. Sie sind weder eine Antwort auf Q-AST-004 noch ein
FPS-, Drawcall-, RAM- oder VRAM-Nachweis für GTX-660-, M1- oder RX-580-Klassen.

## 10. Toolchainpin und nicht gatender Smoke

Einziger Pin der Generierung ist der Blender-Eintrag in `toolchain.lock.json`:

```text
id=blender
version=5.2.0
platform=linux-x64
archiveSha256=96f6c181a30f4950607839dc84d42a354b250d8a0231b098b59b7bc69c351c48
license=GPL-3.0-or-later
```

Der Bootstrap schreibt erst nach erfolgreichem Archivehash, sicherer Extraktion und Binaryhashbildung eine kanonische Installationsevidenz nach `.ai/runtime/toolchain/blender-5.2.0-linux-x64.install.json`. Sie enthält Schema-, Lockdatei-, Archive- und Binary-SHA-256, Version, Plattform und kanonischen Binarypfad. `generate` kreuzprüft Evidenz, aktuelle Binarybytes, Hostplattform, `blender --version` und Lock; fehlt etwas oder weicht es ab, endet es mit Exit 3 vor einem Produktionsskript. T-007 lädt im frischen Checkout neu beziehungsweise prüft auch einen Cache erneut gegen den Archivehash.

Der lokal beobachtete Buildhash `fbe6228777e7` und die erfolgreichen minimalistischen GLB-/PNG-Smokes sind **observation/non-gating**. Sie sind weder Bestandteil des Pins noch Ersatz für T-005, T-006, T-007, T-003 oder einen Performancebeleg.

## 11. Prozess- und Netzwerkisolation

Blender läuft ohne Degradationsfallback in neuen Linux-User-, Mount-, PID- und Netzwerk-Namespaces. Im Netzwerk-Namespace bleibt Loopback deaktiviert; es existiert keine Nicht-Loopback-Schnittstelle und keine Route. Der Workspace wird read-only gebunden. Nur `.ai/runtime/asset-jobs/<job-id>/work/` ist schreibbar; HOME, TMPDIR, XDG_CONFIG_HOME und XDG_CACHE_HOME zeigen auf eigene Unterverzeichnisse darin. stdin ist `/dev/null`. Die Umgebung enthält ausschließlich `PATH`, `LANG=C.UTF-8`, `LC_ALL=C.UTF-8`, `TZ=UTC`, `HOME`, `TMPDIR`, die beiden XDG-Pfade und explizite interne Dateideskriptoren; Blenderstartup, Userprefs, Add-ons und Autosave werden deaktiviert.

Grenzen: Walltime 300 s, CPU 240 s, Address Space 8 GiB, 64 Prozesse, 128 offene Dateien, 16 MiB je Datei, 64 Ausgabedateien, 24 MiB Gesamtoutput und je 1 MiB stdout/stderr. Bei Walltime oder Limit sendet der Orchestrator SIGTERM an die gesamte Prozessgruppe, wartet höchstens 5 s und sendet dann SIGKILL. Beide Streams werden ab Prozessstart parallel begrenzt gelesen. Namespace- oder Limitsetupfehler sind Exit 4; Blender darf dann nicht außerhalb der Sandbox starten.

Ein Probeprozess im selben Sandboxpfad vergleicht die Namespace-IDs mit dem Elternprozess, liest Interfaces/Routen, versucht einen Workspace-Write und einen Socket-Connect auf eine reservierte Dokumentationsadresse ohne DNS. Erfolg erfordert: andere Namespace-IDs, keine Route, der Connect scheitert lokal und nur der Jobroot ist schreibbar. Fehlende Kernelfähigkeit ist eine messbare Runner-Inkompatibilität, keine Erlaubnis für einen unsandboxed Fallback.

## 12. Deterministische Ausgaben und Identität

Aus `specSha256` wird `assetId = CAL-STONEWOOD-V1-` plus den ersten zwölf Hexzeichen in Großschreibung gebildet. Der Job-ID beeinflusst keine Artefaktbytes. Vor Publikation liegen Dateien unter `.ai/runtime/asset-jobs/<job-id>/stage/`. Zielpfade sind:

```text
assets/quarantine/3d/<assetId>/family.glb
assets/quarantine/3d/<assetId>/preview.png
assets/quarantine/3d/<assetId>/technique.json
assets/receipts/<assetId>.generation.json
assets/manifests/<assetId>.json
```

Determinismus bedeutet: zwei vollständig getrennte temporäre Repository-/Publikationswurzeln desselben kanonischen Specs auf demselben Worker, mit derselben Binary und demselben Pin, liefern byteidentische GLB-, PNG- und Reportbytes. So kollidieren die identischen abgeleiteten Asset-IDs nicht, ohne den Produktionsvertrag um einen Testmodus zu erweitern. Innerhalb derselben Repositorywurzel wird ein bereits vorhandenes Ziel fail-closed mit Exit 7 abgelehnt. Der Alternativseed muss GLB und PNG ändern; Struktur, Namen und Budgets bleiben gleich. Cross-CPU-, Cross-Host-, Cross-OS- oder zukünftige Blender-Byteidentität ist ausdrücklich nicht behauptet.

## 13. T-003-Provenienz und transaktionale Publikation

Der Generator ist im Manifest `kind=procedural`, `executionMode=local`, mit `model`, `modelVersion` und `modelArtifactSha256` jeweils `null`; `prompts` ist `null`. Ein Prompt oder Negativprompt wird weder akzeptiert noch erzeugt. `seed`, `generatorSourceSha256` und `toolchainPin` sind Pflicht. Das Inputinventar enthält genau diese zwei Einträge in dieser Reihenfolge; SHA-256 ist jeweils über die gelesenen Bytes gebunden:

| `id` | `path` | `origin` | `originClass` | `creativeInfluence` | `license` | `rightsEvidence` | `allowedUse` | `referenceUseReviewed` |
|---|---|---|---|---:|---|---|---|---:|
| `CAL-STONEWOOD-V1-SPEC` | `assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json` | `versioned project specification` | `internal-specification` | `true` | `project-internal-unreleased` | `tracked numeric specification; no external media input` | `internal-specification` | `true` |
| `BLENDER-TOOLCHAIN-LOCK` | `toolchain.lock.json` | `versioned technical toolchain pin` | `technical-nonexpressive` | `false` | `not-applicable-technical-metadata` | `archive identity and SPDX tool license recorded in lock` | `technical-calibration` | `true` |

Freie oder fremde Eingaben sind verboten.

Für `calibration-v1` gelten zusätzlich diese festen Manifestwerte:

```text
generator.tool=rift-blender-calibration
generator.version=1
generator.toolchainPin=blender:5.2.0:linux-x64:sha256:96f6c181a30f4950607839dc84d42a354b250d8a0231b098b59b7bc69c351c48
status=quarantine
reviews=[]
supersedes=[]
licenseBasis.modelTerms=not-applicable-procedural-generator
licenseBasis.inputRights=internal-specification-and-versioned-project-code
licenseBasis.outputPolicy=quarantine-only-pending-license-review
licenseBasis.commercialUseReviewed=false
licenseBasis.reviewedAtUtc=null
licenseBasis.termsEvidenceArtifact=null
```

Die drei `outputs` stehen in der Reihenfolge `family.glb` (`model/gltf-binary`), `preview.png` (`image/png`), `technique.json` (`application/json`); Pfad, SHA-256 und Bytezahl müssen mit Report und lokalen Dateien übereinstimmen. `transformations` steht in der Reihenfolge `calibration-v1-geometry`, `glb-export`, `eevee-preview`, `png-normalize-v1`. Operation, Werkzeug/Version und Parameterbindung sind exakt:

| `operation` | `tool` / `version` | Bytes für `parametersSha256` |
|---|---|---|
| `calibration-v1-geometry` | `rift-blender-calibration` / `1` | kanonische Specbytes aus Abschnitt 5 |
| `glb-export` | `Blender` / `5.2.0` | das folgende kanonische `glb-export-v1`-Objekt plus LF |
| `eevee-preview` | `Blender` / `5.2.0` | das folgende kanonische `preview-v1`-Objekt plus LF |
| `png-normalize-v1` | `rift-blender-calibration` / `1` | das folgende kanonische `png-normalize-v1`-Objekt plus LF |

```json
{"animations":false,"applyModifiers":true,"cameras":false,"exportFormat":"GLB","exportNormals":true,"exportTangents":false,"exportTexcoords":true,"exportYup":true,"images":false,"lights":false,"materials":"EXPORT","profile":"calibration-v1","schemaVersion":1,"skins":false,"useSelection":true,"vertexColors":false}
```

```json
{"camera":{"clipEndMicrometres":100000000,"clipStartMicrometres":100000,"lensMillimetres":50,"locationMicrometres":[10000000,-14000000,9000000],"sensorWidthMillimetres":36,"targetMicrometres":[1000000,1000000,1400000]},"colorManagement":{"exposureMilliStops":0,"gammaPermille":1000,"look":"AgX - Medium High Contrast","viewTransform":"AgX"},"engine":"BLENDER_EEVEE_NEXT","filmTransparent":false,"frame":1,"lights":[{"locationMicrometres":[2000000,-4000000,10000000],"powerMilliwatts":1200000,"sizeMillimetres":8000,"type":"AREA"},{"locationMicrometres":[-6000000,3000000,5000000],"powerMilliwatts":500000,"sizeMillimetres":6000,"type":"AREA"}],"moduleLocationsMicrometres":[[-5000000,0,0],[0,0,0],[5000000,0,0]],"output":{"colorDepth":8,"colorMode":"RGBA","compression":9,"format":"PNG","height":540,"interlace":0,"percentage":100,"width":960},"samples":64,"schemaVersion":1,"stamps":{"camera":false,"date":false,"enabled":false,"filename":false,"frame":false,"hostname":false,"label":false,"lens":false,"marker":false,"memory":false,"note":false,"renderTime":false,"scene":false,"sequencerStrip":false,"time":false},"worldColorLinearMillionths":[35000,35000,35000]}
```

```json
{"allowedChunks":["IHDR","IDAT","IEND"],"preserveIdatPayloads":true,"profile":"png-normalize-v1","recomputeCrc":true,"schemaVersion":1}
```

T-003-Schemafelder, die nicht hier weiter eingeschränkt werden, behalten unverändert ihren T-003-Vertrag.

Der Harness-Run bindet Actor, Spec-, Generatorquellen-, Toolchain- und Outputhashes im kanonischen `asset.generation.completed`-Event. Erst danach darf er erfolgreich enden und ein T-003-Receipt in das Stage-Verzeichnis exportieren. Receipt, Manifest, Event und Dateien müssen `runId`, `finalEventHash`, `actorId`, `assetId`, Spec-, Generator-, Toolchain- und alle Outputhashes cross-validieren. Ein historischer Run wird bei Abbruch oder Recovery nie umgeschrieben.

Ein Fehler vor dem erfolgreichen Runabschluss beendet den Run als `failed` und erzeugt kein Generation Receipt. Ein Publikations- oder Recoveryfehler nach dem unveränderlichen `succeeded`-Abschluss darf diesen Run nicht manipulieren; er wird als nicht publizierter beziehungsweise fehlgeschlagener Job im Journal referenziert. Nur ein vollständig cross-validierter Receipt darf in `assets/receipts/` erscheinen.

### 13.1 Journal

Jeder Job besitzt `.ai/runtime/asset-jobs/<job-id>/journal.jsonl` und genau einen exklusiven Lock. Jede LF-terminierte kanonische Zeile enthält `schemaVersion=1`, `sequence`, `jobId`, `state`, `previousEntrySha256`, `atUtc`, `ownedPaths[]` mit repo-relativem Pfad/Typ/SHA-256 sowie `entrySha256`. `entrySha256` ist SHA-256 der kanonischen Zeile ohne dieses Feld. Vor jeder extern sichtbaren Aktion werden Daten und Journal auf dasselbe Dateisystem geschrieben und synchronisiert.

Der einzige Zustandsgraph ist:

```text
CREATED -> GENERATED -> INSPECTED -> PROVENANCE_PREPARED
         -> QUARANTINE_PUBLISHED -> METADATA_PUBLISHED
         -> VERIFIED -> COMMITTED
jeder nicht COMMITTED Zustand -> ROLLED_BACK
```

`PROVENANCE_PREPARED` bedeutet: GLB, PNG, Report, Receipt und Manifest liegen vollständig gehasht im Stagebereich; der Run ist bereits unveränderlich `succeeded`. Danach wird das Quarantäneverzeichnis per Rename auf demselben Dateisystem publiziert. Receipt wird als atomarer Tempfile-/Rename-Write publiziert, Manifest zuletzt. `METADATA_PUBLISHED` darf deshalb nie vor vorhandenem Quarantäneoutput und Receipt auftreten. `VERIFIED` verlangt `assets-check --require-local`; `--require-approved` muss für dasselbe Manifest ungleich null liefern. Erst dann folgt `COMMITTED`.

Recovery ist idempotent und prüft Journalhashkette, Lock, kanonische Pfade, Typen und aktuelle Hashes erneut. Sie darf nur eigene Pfade löschen oder ersetzen, deren Typ und SHA dem letzten gültigen Journaleintrag entsprechen. Bei unverändertem vollständigem Stageinventar darf sie vorwärts fortsetzen; andernfalls rollt sie rückwärts zurück. Fremde, symlinkende oder veränderte Dateien bleiben unangetastet, der Job endet mit Exit 7 und Journal bleibt erhalten. Ein Manifest ohne passenden lokalen Output und Receipt darf nach erfolgreicher Recovery nie bestehen.

## 14. Fresh-Checkout-CI

T-007 fügt einen separaten, pfadgefilterten Linux-x64-Job mit minimalen Berechtigungen, ohne Secrets und mit begrenzter Laufzeit hinzu. Netzwerk ist nur für Checkout, Locked Restore und checksummengeprüften Blender-Bootstrap erlaubt. Alle Generator- und Blenderprozesse laufen danach im geschlossenen T-006-Netzwerk-Namespace.

Der Job heißt `blender-calibration-linux-x64`, besitzt nur `contents: read`, `timeout-minutes: 30` und eine Concurrency-Gruppe mit Abbruch veralteter Läufe. Sein positiver Pfadfilter umfasst exakt die drei Taskdateien, `docs/BLENDER_GENERATOR_CONTRACT.md`, `docs/ASSET_PIPELINE.md`, `assets/specs/3d/**`, `toolchain.lock.json`, `scripts/bootstrap-blender-linux.sh`, `scripts/rift.sh`, `scripts/fresh-checkout-test.sh`, `.gitignore`, `.gitattributes`, die in Abschnitt 1.1 genannten Implementierungs-/Testdateien sowie die von T-003 gelesenen Assetmanifest-, Receipt-, Modelllock- und Clean-Room-Schemas/Policies. Eine Workflowänderung selbst löst ihn ebenfalls aus.

Der Job führt aus einem sauberen Checkout mindestens aus:

1. Task-/Spec-/Schema-/Vertragsprüfung und Locked Restore;
2. T-005-Unit-, Grenz-, Korruptions- und CLI-Fixtures ohne Blender;
3. Pinprüfung des neu geladenen oder erneut gehashten Blender-Archives;
4. T-006-Isolations-, Ressourcen-, Fehler- und alle Crash-Injection-Punkte;
5. zwei getrennte temporäre Checkout-/Publikationswurzeln für die Referenzseed-Läufe und eine dritte für den Alternativseed;
6. unabhängigen Inspector sowie T-003 `--require-local` und erwarteten `--require-approved`-Fehlschlag;
7. Gitstatus-/Index-/Diff-Prüfung auf Quarantäne-, Cooked-, Recovery-, Source-, Memory- und RAG-Leakage.

CI speichert ausschließlich bereinigte JSON-Evidenz und begrenzte Testlogs: Lock-/Archivehash, Host/RID, semantische Version, Namespace-/Limitstatus, Befehls-ID, Exitcode, AC-ID und Artefakthashes. GLB, PNG und lokale Quarantäne sind keine freizugebenden CI-Artefakte. Der Buildhash des Vorab-Smokes darf nur im Feld `observation.nonGating` erscheinen.

## 15. Test- und AC-Zuordnung

| Testsuite / Job | zwingend abgedeckte Kriterien |
|---|---|
| `CalibrationSpecContractTests` | AC-T005-01, AC-T005-02 |
| `Pcg32ReferenceTests` | AC-T005-02 |
| `GlbInspectorCorruptionTests` | AC-T005-03, AC-T005-05 |
| `PngInspectorMetadataTests` | AC-T005-04 |
| `TechniqueReportCrossFieldTests` | AC-T005-04, AC-T005-05 |
| `CalibrationCliSafetyTests` | AC-T005-01, AC-T005-06 |
| `BlenderPinEvidenceTests` | AC-T006-01, AC-T006-05 |
| `BlenderGenerationContractTests` | AC-T006-02, AC-T006-03 |
| `BlenderIsolationAndLimitsTests` | AC-T006-04, AC-T006-05 |
| `AssetJobJournalRecoveryTests` | AC-T006-06, AC-T006-07 |
| `AssetGenerationProvenanceTests` | AC-T006-08 |
| `FreshCheckoutAssetGeneratorPolicyTests` | AC-T007-01, AC-T007-04, AC-T007-05, AC-T007-06 |
| CI-Job `blender-calibration-linux-x64` | AC-T007-01 bis AC-T007-06 sowie alle T-005-/T-006-Suites |

Jede Suite besitzt positive Referenzfälle und pro Fehlerklasse mindestens ein einzelnes negatives Fixture. Tests speichern keinen entdeckten Zufallsseed: Referenzseed und Alternativseed sind in Abschnitt 5 fest. Lange Blenderläufe sind nur T-006-Integration beziehungsweise T-007-CI, nie T-005-Unit-Test.

## 16. Clean-Room- und Freigabegrenze

Produktionsläufe starten ohne fremde Medien und ohne namentliche Fremdwerke im Kontext. Das Spec besitzt keine Freitextfläche; damit gibt es auch keinen versteckten Prompt oder Negativprompt. Generatorcode, Tests, Namen, Materialien und Formen leiten sich ausschließlich aus diesem numerischen Vertrag und allgemeinen technischen Geometrieprinzipien ab.

Ein erfolgreicher T-005/T-006/T-007-Lauf belegt nur: reproduzierbare interne Synthese, technische Struktur, begrenzte Proxybudgets und T-003-Provenienz. Die Ausgabe bleibt `quarantine`. Gesamtkomposition, Eigenständigkeit, Ähnlichkeit, Nutzungsgrundlage, produktionsweite Assetbudgets, LFS/Backup, Cooking, Runtimeleistung und Shipping werden erst in T-050 beziehungsweise den dort verlangten getrennten Reviews entschieden.
