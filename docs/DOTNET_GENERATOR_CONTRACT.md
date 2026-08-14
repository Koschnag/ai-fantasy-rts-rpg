# Vertrag für den .NET-Kalibrierungsgenerator

Status: **verbindliches T-006-Contract-Amendment; T-005 und T-006 sind `accepted`, T-007 ist `running`**

Dieser Vertrag ersetzt ab T-006 den ursprünglich geplanten DCC-Produktionspfad durch einen vollständig in-process laufenden F#/.NET-10-Generator. Er erzeugt GLB direkt und rendert die Preview mit einem deterministischen CPU-Rasterizer. Der Produktionspfad startet keinen Unterprozess, verwendet kein Netzwerk, kein DCC, kein Skript-Runtime-System, keine GPU und keine projektfremde native Bibliothek. Für Generator, Rasterizer und PNG-/GLB-Writer sind nur .NET-BCL-APIs zulässig; vorhandene Harness-Abhängigkeiten dürfen nicht in den Generatorpfad hineinwachsen.

T-005 wurde unter dem damaligen Dateinamen `docs/BLENDER_GENERATOR_CONTRACT.md` abgenommen; eine unveränderte historische Kopie dieses Vertrags bleibt deshalb im Repository. Seine Taskdatei und sein Abnahmebericht bleiben ebenfalls unveränderte historische Evidenz. Dieses Amendment ändert weder den akzeptierten Parser noch die Geometrie-, Inspector-, Sicherheits- oder Proxybudgetaussagen von T-005. Es ändert für T-006 bewusst den GLB-Generator-Identifier, das geschlossene Generatorquelleninventar und die Toolchainbindung. Deshalb muss T-006 nach Anpassung dieser drei Inspector-Konstanten die komplette T-005-Suite einschließlich aller Korruptionsfixtures erneut bestehen.

Die Familie bleibt ein neutrales, nicht shipping-fähiges Kalibrierungsobjekt. Ein Erfolg ist keine Welt-, Kultur-, Architektur-, Art-Bible-, Originalitäts-, Lizenz-, Runtime- oder Hardwarefreigabe.

## 1. Verantwortungsgrenzen und Dateiscope

| Task | Ergebnis | Darf einen Unterprozess oder DCC starten? | Darf Repository-Metadaten publizieren? |
|---|---|---:|---:|
| T-005 | striktes `calibration-v1`-Spec, Referenzmathematik und unabhängiger GLB-/PNG-/Report-Inspector | nein | nein |
| T-006 | BCL-only-.NET-Generator sowie transaktionaler T-003-Quarantäne-Lifecycle | nein | nur Receipt und Manifest nach erfolgreicher Prüfung |
| T-007 | Fresh-Checkout-, Determinismus-, Recovery- und CI-Nachweis | nein | nein; nur bereinigte CI-Evidenz |

Alle drei Tasks hängen direkt von T-003 ab. T-006 hängt zusätzlich von T-005 ab, T-007 von T-005 und T-006. Keiner nimmt ein Asset nach `assets/source/` auf.

Geplanter T-006-Scope:

- neue Hauptdateien: `.ai/schemas/asset-job-journal-entry.schema.json`, `tools/RiftHarness/AssetJobJournal.fs`, `tools/RiftHarness/DotnetAssetGenerator.fs`, `tests/RiftHarness.Tests/DotnetAssetGeneratorTests.fs`;
- zulässige Integrationsdateien: `tools/RiftHarness/Program.fs`, betroffene `.fsproj`-/Lockdateien, `scripts/rift.sh`, T-003-Integrationstests sowie die bestehenden calibration-v1-Schemas, Inspector- und Dokumentationsdateien;
- T-007 darf den pfadgefilterten Workflow, `scripts/fresh-checkout-test.sh`, eine .NET-Generator-Policytestsuite und ein bereinigtes Evidenzschema ergänzen.

Die historisch benannten Dateien `BlenderCalibration.fs`, `blender-calibration-v1.schema.json` und `blender-technique-report.schema.json` dürfen zur Wahrung der T-005-Abnahmeevidenz ihren Namen behalten; sie enthalten beziehungsweise beschreiben keinen DCC-Aufruf. `LinuxSandbox.fs`, eine Installations-Evidenz für ein DCC und ein externes Generatorskript gehören nicht zum aktiven T-006-Vertrag. Es werden keine GLB- oder PNG-Binärfixtures eingecheckt.

## 2. Öffentlicher CLI-Vertrag

Der normative Einstiegspunkt lautet:

```text
./scripts/rift.sh asset-calibration validate-spec --spec <repo-relativer-pfad>
./scripts/rift.sh asset-calibration inspect --spec <repo-relativer-pfad> --glb <repo-relativer-pfad> --preview <repo-relativer-pfad> --report <repo-relativer-pfad>
./scripts/rift.sh asset-calibration generate --spec <repo-relativer-pfad> --job-id <ULID>
./scripts/rift.sh asset-calibration recover --job-id <ULID>
```

`blender-calibration validate-spec|inspect` darf ausschließlich als historische T-005-Kompatibilitätsalias erhalten bleiben und muss byte- und exitcodegleich an dieselben reinen .NET-Funktionen delegieren. Unter diesem Alias gibt es kein `generate` oder `recover`. T-006 akzeptiert weder einen Binary-/DCC-Pfad noch frei wählbare Arbeits-, Ausgabe-, Netzwerk- oder Rendererargumente.

Argumente sind case-sensitive und dürfen genau einmal vorkommen. Das Subverb ist das erste Positionsargument; allein `--workspace <pfad>` darf davor oder danach stehen. Unbekannte Optionen und zusätzliche Positionsargumente sind Fehler.

Jeder Aufruf schreibt genau ein UTF-8-JSON-Objekt ohne BOM oder Einrückung und mit genau einem LF auf stdout. Eigenschaften sind rekursiv ordinal sortiert. Erfolg und Fehler verwenden:

```json
{"command":"validate-spec","ok":true,"result":{},"schemaVersion":1}
{"command":"validate-spec","error":{"code":"INVALID_SPEC","message":"validation failed"},"ok":false,"schemaVersion":1}
```

`validate-spec.result` und `inspect.result` behalten exakt den akzeptierten T-005-Vertrag. `generate.result` enthält exakt `assetId`, `glbSha256`, `jobId`, `manifestPath`, `manifestSha256`, `previewSha256`, `receiptPath`, `receiptSha256`, `reportSha256`, `specPath` und `specSha256`. `recover.result` enthält exakt `jobId` und `state`. Resultate enthalten nur IDs, Zähler, SHA-256 und workspace-relative POSIX-Pfade. Zeit, Host, Benutzer, Umgebungswerte und absolute Pfade sind auf stdout/stderr verboten; stderr ist UTF-8 und höchstens 1 MiB.

| Exit | Bedeutung | stabile Fehlercodes |
|---:|---|---|
| 0 | Erfolg | – |
| 2 | CLI, Spec oder Pfad ungültig | `INVALID_ARGUMENT`, `INVALID_SPEC`, `UNSAFE_PATH` |
| 3 | .NET-SDK-Pin oder Runtimevertrag ungültig | `UNSUPPORTED_RUNTIME`, `PIN_MISMATCH` |
| 4 | Deadline oder internes Ressourcenlimit | `RESOURCE_LIMIT` |
| 5 | GLB, PNG, Report, Budget oder Determinismus ungültig | `INVALID_ARTIFACT`, `BUDGET_EXCEEDED`, `DETERMINISM_MISMATCH` |
| 6 | Harness-, Receipt-, Manifest- oder T-003-Crosscheck ungültig | `PROVENANCE_FAILED` |
| 7 | Journal-, Lock-, Publikations- oder Recovery-Konflikt | `TRANSACTION_CONFLICT` |
| 8 | unerwarteter interner Fehler | `INTERNAL_ERROR` |

## 3. Safe Paths, Eingaben und Ressourcen

Repo-relative Pfade werden vor jedem Zugriff als POSIX-Pfade normalisiert. Sie sind höchstens 240 UTF-8-Bytes lang, jedes Segment höchstens 80 Bytes. Leere Segmente, `.`, `..`, Backslash, Doppelpunkt, NUL, Steuerzeichen, führender Slash und nicht normalisierte Unicodeformen werden abgelehnt. Bestehende Komponenten werden ohne Symlinkfolge geöffnet und unmittelbar vor Nutzung erneut geprüft. Dateien müssen regulär sein.

`generate` liest Specs ausschließlich unter `assets/specs/3d/`; `tests/Fixtures/Asset3d/` ist allein für die read-only Befehle `validate-spec` und `inspect` zulässig. Weitere erlaubte Lesewurzeln sind `.ai/runtime/asset-jobs/<job-id>/` und `assets/quarantine/3d/`. Fest abgeleitet gelesen werden dürfen nur `toolchain.lock.json`, die drei Generatorquellen aus Abschnitt 5 sowie die von T-003 benötigten lokalen Schemas und Policies. Schreibziele außerhalb des Jobroots sind ausschließlich die abgeleiteten Run-, Quarantäne-, Receipt- und Manifestpfade aus Abschnitt 13. Job-IDs sind 26 große ULID-Zeichen nach `[0-9A-HJKMNP-TV-Z]{26}`.

| Eingabe/Ausgabe | harte Grenze |
|---|---:|
| Spec | 16 KiB, JSON-Tiefe 6, 64 Eigenschaften |
| GLB | 2.097.152 Bytes |
| PNG | 8.388.608 Bytes |
| Technikreport | 1.048.576 Bytes, JSON-Tiefe 8 |
| einzelne Jobdatei | 16.777.216 Bytes |
| Dateien im Jobroot | 64 |
| Summe im Jobroot | 25.165.824 Bytes |
| stdout / stderr | je 1.048.576 Bytes |
| Walltime eines Generierungsversuchs | 300 s |

Der Generator prüft alle Zähler und Multiplikationen vor Allokation, verwendet begrenzte Arrays/Streams und beachtet Cancellation vor und in jeder Modul-, Primitive-, Rasterzeilen- und Publikationsschleife. Er startet nachweislich null Kindprozesse, öffnet null Sockets, lädt keine dynamische oder native Projektbibliothek und liest keine DCC-, Nutzerprofil-, Cache- oder Temp-Konfiguration. Statische Referenz-/IL-Tests sperren im T-006-Generatorpfad mindestens `System.Diagnostics.Process`, `System.Net`, `NativeLibrary`, P/Invoke und dynamisches Assemblyladen. Das ersetzt die früher erwogene Prozesssandbox; es gibt keinen Sandbox-, Namespace- oder DCC-Pin-Vertrag mehr.

## 4. Exaktes `calibration-v1`-Spec

Das Spec ist die einzige kreative Dateneingabe. Es enthält keine Pfade, URIs, Prompts, Namen, Beschreibungen oder Referenzen. Alle variablen Werte sind JSON-Ganzzahlen; `null`, Fließkommazahlen, Exponentialnotation und unbekannte Eigenschaften sind verboten.

```json
{"familyId":"CAL-STONEWOOD-V1","geometry":{"lintelHeightMm":250,"moduleHeightMm":3000,"moduleWidthMm":4000,"mortarGapMm":20,"openingHeightMm":2000,"openingWidthMm":1600,"stoneCourseHeightMm":250,"stoneDepthJitterMm":30,"stoneLengthJitterMm":40,"stoneOffsetJitterMm":20,"timberDepthMm":160,"timberWidthMm":180,"wallThicknessMm":400},"materials":{"stoneBaseColorSrgb8":[96,92,82],"stoneMetallicPermille":0,"stoneRoughnessPermille":850,"woodBaseColorSrgb8":[92,58,32],"woodMetallicPermille":0,"woodRoughnessPermille":720},"profile":"calibration-v1","schemaVersion":1,"seed":1592594996}
```

Einzelfelder und Grenzen bleiben exakt T-005: `schemaVersion=1`, `profile=calibration-v1`, `familyId=CAL-STONEWOOD-V1`, Seed `0..4294967295`, Breite `4000`, Höhe `2400..3600`, Wanddicke `300..600`, gerade Öffnungsbreite `1200..2000`, Öffnungshöhe `1800..2400`, Sturz- und Kurs-Höhe je `250..400`, Mörtel `10..40`, Holzbreite `120..240`, Holztiefe `100..240`, Längen-/Tiefen-/Offsetjitter `0..80`, `0..60`, `0..40`, RGB8 je `0..255`, Roughness `500..1000` und Metallic `0..100` Permille.

Mit `W,H,T,O,OH,LH,C,G,TW,TD,JL,JD,JO` in dieser Feldreihenfolge gelten:

```text
H mod C = 0; OH mod C = 0; LH mod C = 0
OH + LH <= H - C
O + 2*TW <= W - 4*C
TD <= T; 4*G <= C; 2*JO + JL + G <= C; 2*JD <= T
```

Der Validator berechnet die Boxobergrenzen vorab und lehnt Specs außerhalb der Budgets aus Abschnitt 9 ab. Es gibt keine Defaults oder Korrekturen.

## 5. Kanonisierung, Quellen, Toolchain und PRNG

Spec und Technikreport verwenden rekursiv ordinal sortierte Eigenschaften, feste Arrayreihenfolge, UTF-8 ohne BOM, minimale JSON-Escapes, keine Leerzeichen und genau ein LF. Zahlen sind Dezimal-Ganzzahlen ohne führende Null oder `-0`. Der Referenzspec-Hash bleibt `39faae34c4cd515cb724a8ef1e2e4bee159a232136218fbb8afd8edd52db2cf8`.

Das geschlossene, ordinal sortierte `generatorSources`-Inventar besteht **exakt** aus:

```text
tools/RiftHarness/AssetJobJournal.fs
tools/RiftHarness/BlenderCalibration.fs
tools/RiftHarness/DotnetAssetGenerator.fs
```

Der zweite Name ist ausschließlich historische T-005-Kompatibilität. `generatorSourceSha256` ist SHA-256 über die Verkettung `relativePath + LF + fileSha256 + LF` für diese drei Dateien. Report, Event, Receipt und Manifest führen beziehungsweise binden dieselben lokalen Bytes; unbekannte, fehlende, zusätzliche oder anders sortierte Quellen sind ungültig.

`toolchainPinSha256` bindet ausschließlich den geschlossenen `dotnet-sdk`-Eintrag aus `toolchain.lock.json`, kanonisch ordinal serialisiert und mit LF:

```json
{"id":"dotnet-sdk","install":"scripts/bootstrap-dotnet.sh","integrity":"platform-specific SHA-512 values embedded in bootstrap script","license":"MIT","version":"10.0.110"}
```

Das sind 173 Bytes und SHA-256 `840ca3968e7f20d9e525a2d3a0337e8ba81fad50800942ef299496ae18677d4b`. Im Manifest lautet der logische Pin exakt `dotnet-sdk:10.0.110`. Kein anderer Tool- oder Archiveintrag gehört zum Produktionspin.

PCG-XSH-RR 32 bleibt unverändert: Multiplier `6364136223846793005`, Increment `1442695040888963407`, unsigned Wraparound, Initialisierung `state=0; next(); state+=seed; next()`. `bounded(bound)` verwendet `threshold=uint32(-bound) mod bound`; `signed(J)=int(bounded(2*J+1))-J` zieht auch bei `J=0`. Die ersten zehn Werte des Referenzseeds sind:

```text
2931784231, 1733122091, 677491881, 1055047052, 458198092,
2644956477, 2758542496, 1581573961, 1174968268, 55324810
```

Alternativseed ist `1592594997`.

## 6. Geometrie und feste Reihenfolgen

Alle Ableitungen erfolgen verlustfrei in ganzzahligen Mikrometern. Erst beim GLB-Vertexwrite wird in IEEE-754-Binary32-Meter konvertiert; negative Null wird positiv normalisiert. Modulreihenfolge ist `WALL-STRAIGHT`, `WALL-CORNER`, `WALL-OPENING`. Innerhalb eines Moduls: X- vor Y-Segment, Kurse unten nach oben, opake Segmente von negativ nach positiv, Kandidaten entlang der Tangente aufsteigend. Pro LOD0-Stein werden immer `bounded(JL+1)`, `signed(JD)`, `signed(JO)` in dieser Reihenfolge gezogen.

Ein Kurs belegt `z=[course*C+G/2,(course+1)*C-G/2]`; Zellenbreite ist `4*C`, ungerade Kurse beginnen `2*C` weiter links. Zellen werden am opaken Segment geclippt. Danach gelten:

```text
nominalLength = clippedLength - G
stoneLength = nominalLength - lengthReductionMm
stoneDepth = T + depthDeltaMm
centerMin = clipMin + G/2 + stoneLength/2
centerMax = clipMax - G/2 - stoneLength/2
stoneCenter = clamp(clippedCenter + tangentOffsetMm, centerMin, centerMax)
```

Straight und Opening sind um die Wandmitte zentriert. Beim Corner endet die Nahtseite beider Beine immer bei `T/2`; Jitter wirkt nur von der Naht weg. Das X-Bein liegt axial `[0,W]`, das Y-Bein `[T/2,W]`. Opening verwendet unter `OH+LH` die Segmente `[-W/2,-O/2]` und `[O/2,W/2]`, darüber das volle Segment. Holzboxen und die LOD1-/LOD2-/Kollisionsableitungen bleiben bitgenau den akzeptierten T-005-Referenzfunktionen entsprechend. LOD1 fasst je Kurs/Bein/Segment maximal acht folgende Steine in ihrer Integer-Hülle zusammen; LOD2 nutzt 1/2/3 Steinboxen für Straight/Corner/Opening. Kollision besteht nur aus diesen LOD2-Steinboxen.

Jede Box besitzt 24 getrennte Vertices und 36 `UNSIGNED_SHORT`-Indizes. Die sechs Flächen folgen `+X,-X,+Y,-Y,+Z,-Z`; je Fläche folgen vier Ecken gegen den Uhrzeigersinn von außen, UV `(0,0),(1,0),(1,1),(0,1)` und Indizes `0,1,2,0,2,3` plus Basis. Boxreihenfolge ist die Ableitungsreihenfolge oben; Holz folgt nach Stein.

Referenzwerte bleiben: Straight `LOD0 1344/672`, `LOD1 336/168`, `LOD2 72/36`, Collision 12; Corner `2664/1332`, `648/324`, `120/60`, Collision 24; Opening `1272/636`, `576/288`, `144/72`, Collision 36. Familie: 255.048 dekodierte Bytes und 18 Renderprimitives. Die akzeptierten Referenz-/Alternativseed-Bounds aus T-005 bleiben Regressionstestvektoren.

## 7. Koordinaten, Snap-Punkte und Namen

Der logische Generatorraum ist rechtshändig, `+X` Wandrichtung, `+Y` Tiefe, `+Z` oben, Einheit Meter. glTF ist rechtshändig mit `+Y` oben; Konvertierung ist `glTF(x,y,z)=(x,z,-y)`. Pivots und Snaps bleiben:

| Modul | Pivot | Snap A / B in mm | Z-Vierteldrehungen A/B |
|---|---|---|---|
| Straight | Bodenmitte | `(-2000,0,0)` / `(2000,0,0)` | `2 / 0` |
| Corner | äußere Bodenecke | `(4000,0,0)` / `(0,4000,0)` | `0 / 1` |
| Opening | Bodenmitte | `(-2000,0,0)` / `(2000,0,0)` | `2 / 0` |

Geschlossene Namen: `SCENE_CAL_STONEWOOD_V1`, `MOD_WALL_STRAIGHT`, `MOD_WALL_CORNER`, `MOD_WALL_OPENING`, je `MESH_<MODUL>_LOD0/1/2`, `COL_<MODUL>`, `SNAP_<MODUL>_A/B`, `MAT_CAL_STONE`, `MAT_CAL_WOOD`.

## 8. Direkter GLB-Writer, CPU-Preview, PNG und Report

### 8.1 GLB-Bytes

`family.glb` ist GLB 2.0 mit genau JSON- und BIN-Chunk. Alle Integer im Container/BIN sind little-endian. Header ist Magic `0x46546C67`, Version 2, Gesamtlänge; Chunks sind `0x4E4F534A` und `0x004E4942`. JSON ist minimales UTF-8 mit rekursiv ordinalen Objektschlüsseln und `0x20`-Padding auf vier Bytes; BIN verwendet Nullpadding. Binary32 entsteht durch die .NET-10-Konvertierung `float32(micrometres/1000000.0)` beziehungsweise die akzeptierte Material-/Quaternion-Referenzfunktion, wird als Bitmuster geschrieben und darf weder NaN, Infinity noch negative Null enthalten.

Top-Level-Felder sind exakt `accessors,asset,bufferViews,buffers,materials,meshes,nodes,scene,scenes`; `asset` ist exakt:

```json
{"generator":"Riftward .NET Asset Generator v1","version":"2.0"}
```

Es gibt 12 Meshes in Modul- und je `LOD0,LOD1,LOD2,Collision`-Reihenfolge, 21 Nodes in je `Root,LOD0,LOD1,LOD2,Collision,SnapA,SnapB`-Reihenfolge und eine Szene. Jeder Rendermesh hat Stein- und Holzprimitive, Collision genau ein materialloses Primitive. Pro Primitive werden BIN-Segmente und zugleich BufferViews/Accessors exakt in `POSITION,NORMAL,TEXCOORD_0,indices`-Reihenfolge geschrieben; Collision lässt UV aus. Attribute haben Target 34962, Indizes 34963. `POSITION` ist `FLOAT VEC3` mit exakten `min/max`; `NORMAL` ist `FLOAT VEC3`, UV `FLOAT VEC2`, Indizes `UNSIGNED_SHORT SCALAR`; nur Position besitzt min/max. Jedes Segment wird vor Beginn auf seine Komponentenbreite mit Nullbytes ausgerichtet. `byteOffset` ist vorhanden, `byteStride`, Accessor-Offset und explizite glTF-Defaults fehlen.

Materialien entsprechen unverändert T-005: genau zwei PBR-Materialien, keine Texturen/Extensions, `baseColorFactor` aus der akzeptierten sRGB8-nach-linear-Funktion, Alpha 1, Metallic/Roughness Permille. Es gibt keine URI, Bilder, Sampler, Kameras, Lights, Skins, Animationen, Morphs, Sparse-Accessors, Interleaving, geteilte Accessors, freien Extras oder zusätzliche Namen.

### 8.2 Deterministischer CPU-Rasterizer

Die Preview rastert ausschließlich LOD0 derselben Boxdaten, Instanzen Straight `(-5,0,0)`, Corner `(0,0,0)`, Opening `(5,0,0)` Meter, in dieser Reihenfolge; Steinprimitive vor Holz, Box-/Flächen-/Dreiecksreihenfolge wie Abschnitt 6. Ausgabe ist 960×540 RGBA8 mit Hintergrund `[9,9,9,255]`. Es gibt kein Multisampling, Dithering, Gamma-/Farbprofil, GPU- oder Plattform-API.

Die feste orthografische Kamera liegt in Mikrometern bei `C=(10000000,-14000000,9000000)` und schaut entlang `F=(-90,150,-76)` zum Ziel `(1000000,1000000,1400000)`. Orthogonale Basen sind `R=(5,3,0)` und `U=(-114,190,510)`. Für `D=P-C` werden Screenkoordinaten in Q8 mit vorzeichenkorrektem Round-to-nearest/ties-to-even berechnet:

```text
sxQ8 = 480*256 + roundEven(dot(D,R)*256 / 150000)
syQ8 = 270*256 - roundEven(dot(D,U)*256 / 10000000)
depth = dot(D,F)
```

Alle Referenzgeometrie muss `depth>0` erfüllen. Dreiecke mit Nullfläche werden verworfen; bei negativer Screenfläche werden Vertex 1 und 2 samt Tiefe vertauscht. Pixelzentren sind `(x*256+128,y*256+128)`. Coverage verwendet Integer-Edgefunktionen und die Top-left-Regel (`dy>0` oder `dy=0 && dx<0` ist einschließend). Baryzentrische Tiefe wird mit `Int128` und Round-to-nearest/ties-to-even auf `int64` berechnet. Kleinere positive Tiefe gewinnt; bei Gleichheit bleibt das früher geschriebene Fragment.

Da v1 nur achsenparallele Boxflächen besitzt, ist die normative kombinierte Festlichtintensität in Permille für `+X,-X,+Y,-Y,+Z,-Z` exakt `620,360,460,700,1000,240`. Dies entspricht festem Key-, Fill- und Ambientlicht und ist die normative Lichtauswertung; es gibt keine Fließkomma-Normalisierung. Pro sRGB8-Materialkanal gilt `clamp((base*intensity+500)/1000,0,255)`, Alpha 255.

### 8.3 Deterministisches PNG

`preview.png` besitzt genau `IHDR,IDAT,IEND`. IHDR ist 960×540, 8 Bit, Color Type 6, Compression/Filter/Interlace 0. Jede Scanline beginnt mit Filterbyte 0. IDAT enthält genau einen zlib-Stream mit Header `78 01`; DEFLATE besteht ausschließlich aus aufeinanderfolgenden unkomprimierten Stored-Blöcken von höchstens 65.535 Bytes, nur der letzte hat `BFINAL=1`, `LEN/NLEN` sind little-endian. Den Abschluss bildet Adler-32 der ungefilterten Scanlinebytes in big-endian. PNG-Längen und CRC-32 (Polynom `0xEDB88320`, Initialwert/Finit-Xor `0xffffffff`) sind big-endian. Es gibt keine ancillary Chunks oder nachlaufenden Bytes. Damit ist kein plattformabhängiger Codec beteiligt.

### 8.4 Technikreport und Provenienzparameter

`technique.json` behält exakt die T-005-Felder und -Formen: `artifacts,familyId,familyMetrics,generatorSourceSha256,generatorSources,limits,materials,modules,profile,schemaVersion,seed,specSha256,toolchainPinSha256`. Die lokale Bytebindung verwendet Abschnitt 5; Bounds werden im Generatorraum berichtet. Zeit, Job-/Run-ID, Host, Benutzer, absolute Pfade oder Selbsthash fehlen.

Die Transformationsreihenfolge ist exakt:

| Operation | Tool / Version | Parameterbindung |
|---|---|---|
| `calibration-v1-geometry` | `riftward-dotnet-asset-generator` / `1` | kanonische Specbytes |
| `gltf2-direct-write` | `riftward-dotnet-asset-generator` / `1` | folgendes kanonisches `gltf2-direct-write-v1`-Objekt |
| `cpu-preview-v1` | `riftward-dotnet-asset-generator` / `1` | folgendes kanonisches Kamera-/Raster-/Lichtobjekt |
| `png-encode-v1` | `riftward-dotnet-asset-generator` / `1` | folgendes kanonisches PNG-Objekt |

```json
{"accessorOrder":["POSITION","NORMAL","TEXCOORD_0","indices"],"assetGenerator":"Riftward .NET Asset Generator v1","binPaddingByte":0,"boxFaceOrder":["+X","-X","+Y","-Y","+Z","-Z"],"jsonCanonicalization":"ordinal-minimal-utf8-v1","jsonPaddingByte":32,"profile":"gltf2-direct-write-v1","schemaVersion":1}
```

```json
{"backgroundRgba8":[9,9,9,255],"cameraMicrometres":[10000000,-14000000,9000000],"depth":"int128-round-even-smaller-wins-first-on-tie","faceLightPermille":[620,360,460,700,1000,240],"forward":[-90,150,-76],"height":540,"instancesMicrometres":[[-5000000,0,0],[0,0,0],[5000000,0,0]],"pixelCenterQ8":128,"profile":"cpu-preview-v1","right":[5,3,0],"screenDenominatorX":150000,"screenDenominatorY":10000000,"schemaVersion":1,"shade":"clamp((base*intensity+500)/1000,0,255)","topLeft":"dy-positive-or-horizontal-dx-negative","up":[-114,190,510],"width":960}
```

```json
{"adler32":"rfc1950-big-endian","colorType":6,"crc32Polynomial":"edb88320","deflate":"stored-blocks-max-65535","filter":0,"height":540,"idatCount":1,"interlace":0,"profile":"png-encode-v1","schemaVersion":1,"width":960,"zlibHeaderHex":"7801"}
```

`parametersSha256` bindet jeweils die dargestellten Bytes plus genau ein LF. Die festen Hashes sind `81d7fcdea55de043c85ff8494bdb0f484a90e2a1de9b651b654123ba7f9db2c8`, `c25bac11724a0f293f56460e157bc554e1672e75c04a5919a2b847ecaa30d1ea` und `a875004622ac3d9b76fb52b0b32a01a2a7f4e50911e2f2aa86a2dc89418c4a50`. Für `calibration-v1-geometry` ist es der jeweilige `specSha256`. Implementierung und Tests halten diese exakten Erwartungsbytes versioniert im F#-Code, nicht in einer externen Datei.

## 9. Proxybudgets

Je Modul gelten LOD0 höchstens 3.072 Vertices/4.096 Dreiecke, LOD1 1.024/1.024, LOD2 256/192 und Collision 48 Dreiecke. Familienweit gelten genau zwei Materialien, höchstens zwei Renderprimitives je LOD sowie je 2.097.152 Bytes für GLB und dekodierte Geometrie. Renderbytes je Primitive sind `vertices*32 + indices*2`, Collision `vertices*24 + indices*2`. Kein Accessor wird geteilt. Das sind Strukturproxies, keine Hardwareaussage.

## 10. Determinismus und Identität

`assetId = CAL-STONEWOOD-V1-` plus die ersten zwölf Spec-Hashzeichen in Großschreibung. Stage liegt unter `.ai/runtime/asset-jobs/<job-id>/stage/`; Ziele bleiben `assets/quarantine/3d/<assetId>/family.glb`, `preview.png`, `technique.json`, `assets/receipts/<assetId>/<runId>.json` und `assets/manifests/<assetId>.json`. Die Receipt-Run-ID ist die erfolgreiche T-003-`generationRunId`; ein flacher oder aus der Job-ID abgeleiteter Receiptpfad ist verboten.

Job-/Run-ID, Zeit, Host und absolute Pfade beeinflussen keine Artefaktbytes. Zwei getrennte Repository-/Publikationswurzeln mit denselben Spec-, drei Quellen- und .NET-Pinbytes liefern byteidentische GLB-, PNG- und Reportbytes. Alternativseed ändert mindestens GLB und PNG, nicht Struktur/Namen/Budgets. T-007 beweist dies zunächst auf Linux-x64 mit .NET SDK 10.0.110; eine ungeprüfte plattformweite Behauptung folgt daraus nicht.

## 11. T-003-Provenienz

Manifestwerte sind exakt:

```text
generator.kind=procedural
generator.tool=riftward-dotnet-asset-generator
generator.version=1
generator.executionMode=local
generator.model/modelVersion/modelArtifactSha256=null
generator.toolchainPin=dotnet-sdk:10.0.110
prompts=null
status=quarantine
reviews=[]
supersedes=[]
```

Das Inputinventar enthält in Reihenfolge das kanonische Spec, danach die drei Generatorquellen aus Abschnitt 5 und zuletzt `toolchain.lock.json`. Spec ist `internal-specification`/kreativ; Quellen sind `agentic-synthetic`, `generation-input`, nicht kreativ; Lock ist `technical-nonexpressive`, `technical-calibration`, nicht kreativ. T-003 leitet den Aggregathash aus den drei lokalen Quelleneinträgen ab und verlangt Gleichheit mit `generatorSourceSha256`; keine synthetische Aggregatdatei und kein zusätzlicher Input ist zulässig.

Event, Receipt, Manifest, Report und Dateien kreuzprüfen Actor, Run, finalen Eventhash, Asset-/Spec-/Quellen-/Toolchain- und alle Outputhashes. `assets-check --require-local` muss bestehen, `--require-approved` für dasselbe Manifest scheitern. Kein Output erreicht Source, Cooked, Memory, RAG oder Git-Index.

## 12. Journal, Publikation und Recovery

Der append-only, hashverkettete Journal-/Lockvertrag bleibt unverändert:

```text
CREATED -> GENERATED -> INSPECTED -> PROVENANCE_PREPARED
        -> QUARANTINE_PUBLISHED -> METADATA_PUBLISHED
        -> VERIFIED -> COMMITTED
jeder nicht COMMITTED Zustand -> ROLLED_BACK
```

Jede kanonische LF-Zeile bindet Schema, Sequence, Job-ID, Zustand, Vorgängerhash, Zeit, eigene Pfade/Typen/Hashes und Selbsthash. Quarantäneverzeichnis, Receipt und zuletzt Manifest werden einzeln atomar aus demselben vorbereiteten Inventar publiziert. Recovery ist idempotent, besitzt genau einen exklusiven Joblock und verändert nur kanonische, nicht symlinkende Pfade, deren Typ und aktueller Hash dem eigenen Journal entsprechen. Fremde/geänderte Pfade bleiben unangetastet und führen zu Exit 7 mit erhaltener Evidenz.

## 13. Fresh-Checkout-CI und Abnahme

T-007 liefert einen pfadgefilterten Linux-x64-Job `dotnet-asset-calibration-linux-x64` mit `contents: read`, ohne Secrets und höchstens 30 Minuten. Netzwerk ist für die GitHub-Control-Plane bei Checkout, Toolchain-/Locked-Restore-Vorbereitung und dem abschließenden Upload ausschließlich der bereinigten Evidenz erlaubt; die Generierung und ihre direkten Prüffunktionen verwenden nachweislich keine Netzwerk- oder Prozess-API. Der Filter umfasst T-005/T-006/T-007, diesen Vertrag, Asset-Pipeline, Specs, Toolchainlock, Generator-/Inspector-/Journalquellen, Tests, T-003-Schemas/Policies und Workflow/Skripte.

Der Job prüft mindestens: JSON/Verträge und Locked Restore; komplette T-005-Suite nach Identifier-/Quell-/Pin-Amendment; direkte GLB-/Raster-/PNG-Goldenregeln; BCL-/Null-Unterprozess-/Null-Netz-Abhängigkeit; alle Ressourcen- und Crashpunkte; zweimal Referenzseed plus Alternativseed in getrennten Wurzeln; unabhängigen Inspector; T-003 local/approved-Erwartungen; Gitstatus/Index/Diff auf Leakage. Evidenz enthält SDK-/Lockhash, Host/RID, Befehls-ID, Exitcode, AC-ID und Artefakthashes, aber keine GLB/PNG oder absoluten Pfade.

Der lokale Reproduktionsbefehl ist `./scripts/dotnet-asset-calibration-ci.sh`. Er arbeitet ausschließlich aus dem archivierten aktuellen Commit, schreibt die kanonische Evidenz nach `artifacts/t007/dotnet-asset-calibration.json` und ein auf 1 MiB begrenztes, bereinigtes Testlog nach `artifacts/t007/test.log`. Der Workflow lädt genau diese beiden Dateien für sieben Tage hoch; Quarantäne-, Cooked-, Runtime- oder Binärdaten sind ausgeschlossen.

| Suite | Kriterien |
|---|---|
| komplette bestehende T-005-Suite | AC-T005-01 bis AC-T005-06; Regression nach Amendment |
| `DotnetAssetGeneratorContractTests` | AC-T006-01 bis AC-T006-05 |
| `AssetJobJournalRecoveryTests` | AC-T006-06, AC-T006-07 |
| `AssetGenerationProvenanceTests` | AC-T006-08 |
| Fresh-Checkout-Policy/CI | AC-T007-01 bis AC-T007-06 |

## 14. Clean-Room- und Kontrollwerkzeuggrenze

Produktionsläufe erhalten keine fremden Medien, Freitexte oder Referenzwerke. Generatorcode, Namen, Materialien und Formen folgen nur diesem numerischen Vertrag und allgemeinen Geometrieprinzipien. Blender darf außerhalb von T-006/T-007 als optionales FOSS-Kontrollwerkzeug für eine manuelle Sichtprüfung lokaler GLB-Dateien installiert bleiben. Seine Version, Ausgabe und Verfügbarkeit sind nicht gatend, nicht im Produktionsmanifest und kein Bestandteil der reproduzierbaren Generierung.

Ein erfolgreicher T-005/T-006/T-007-Lauf belegt nur interne Synthese, technische Struktur, Proxybudgets und T-003-Provenienz. Ausgabe bleibt `quarantine`; Reviews, Promotion, LFS/Backup, Cooking, Runtimeleistung und Shipping gehören zu T-050 oder späteren getrennten Freigaben.
