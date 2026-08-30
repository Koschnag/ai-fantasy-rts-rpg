# Abnahme T-038 – Kleinster Single-Platform-Releasepfad (linux-x64-Alphapaket)

**Status:** Implementierter Kandidat zur frischen Review. Der gatende
Abschnitt 0 (versionierter Paketvertrag `docs/PAKETVERTRAG.md` V1), der
öffentliche `package`-Befehl (Build und `--verify`), die Testmatrix (327/327)
und alle lokalen Gates sind grün. Der fensterpflichtige Paketsmoke bleibt
wegen der displaylosen Umgebung ein ausgewiesener Restpunkt mit
kontrolliertem Code-19-Nachweis (Präzedenz T-023/T-032/T-033 bis T-037).
Diese Datei beschreibt die aktuelle Produktwahrheit; sie behauptet keinen
noch nicht ausgeführten Gate-Erfolg.

## Ausgangslage und Abschnitt 0

Der gatende Vertragsspike wurde vor der Implementierung abgeschlossen
(`docs/PAKETVERTRAG.md` V1, Vertragskennung `riftward-paketvertrag-v1`):

- **Paketformat/Layout** (Abschnitt 1): deterministisches tar.gz mit
  fixiertem `SOURCE_DATE_EPOCH=1786623387`, Ustar-Einträgen in sortierter
  Reihenfolge und gzip ohne Zeitstempel; genau ein Wurzelverzeichnis
  `riftward-<version>-linux-x64/`; dokumentierte Alternativen (tar, zip),
  verworfen: AppImage-/Installer-Tooling (neue Abhängigkeit und nicht
  delegierbare Vertriebsentscheidung).
- **Runtimeform** (Abschnitt 2): `self-contained-coreclr-no-aot-no-trimming-v1`;
  Alternativen (framework-abhängig gebündelt, Native AOT) verworfen;
  Q-TEC-008 bleibt `OFFEN`.
- **Reprozierbarkeitsregel** (Abschnitt 3): byteidentischer Doppelbau vom
  selben Baum; Restore-Regel: der RID-Restore leitet die Restore-Lockdateien
  mit `NuGetLockFilePath=obj/restore/packages.lock.json` in das gitignorierte
  obj-Gebiet um, weil ein RID-Abschnitt in den versionierten
  `packages.lock.json` den vertraglichen locked Restore der Solution brechen
  würde; die Runtimeprojekte besitzen keine externen NuGet-Abhängigkeiten,
  die Runtime-Pack-Version ist über den gepinnten SDK-Stand gebunden; die
  einmalige Erstbeschaffung des Runtime-Packs in den lokalen NuGet-Cache ist
  die dokumentierte Offline-Ausnahme (analog zur Native-Cache-Erstbeschaffung).
- **Manifest-/Checksum-/Attributionsschema** (Abschnitt 4): SHA-256 je Datei
  plus Paketanker (`package-manifest.sha256`) und Archiv-Sidecar; dreizehn
  unterscheidbare Verletzungsklassen plus `ARTIFACT_MANIFEST_REJECTED` über
  die bestehende Host-Prüfung; ehrliche interne-Alpha-Kennzeichnung; das
  Lizenz-/Attributionsmanifest wird deterministisch aus `toolchain.lock.json`
  und `THIRD_PARTY_NOTICES.md` erzeugt; keine Lizenzbehauptung für eigenen
  Code (Q-PRD-001 bleibt `OFFEN`).
- **Versionierung** (Abschnitt 5): `0.1.0-alpha.<tree8>` mit Commit- und
  Baumbindung (SHA-256 über den 40-stelligen Git-Baum-Digest des
  hypothetischen Add-A-Baums, privater Temporärindex, echter Index unberührt).
- **Befehls-/Exitcode-/Installationsvertrag** (Abschnitt 6): ein
  öffentlicher `package`-Befehl mit Build- und `--verify`-Modus; neue
  Exitcodes 39 (Bau fehlgeschlagen, kein Report) und 40 (Verifikation
  fehlgeschlagen, Prüfreport mit Verletzungsklasse); unbekannte RIDs/Optionen
  mit bestehender Usage-Bedeutung 2; manuell installierbares Offline-Archiv
  gemäß Q-OPS-003-Arbeitsannahme; keine Signier-, Store- oder
  Update-Entscheidung (Q-OPS-002/Q-OPS-003, Q-PRD-005 bleiben `OFFEN`).
- **Paketsmoke** (Abschnitt 7) mit vorregistrierten Playtestkriterien;
  **Media-Lab-Entscheidung** (Abschnitt 8): höchstens ein opt-in Abgriff nach
  dem T-023-/T-033-Muster; ohne produzierten Abgriff kein Eintrag.

Keine offene Produktfrage wurde verbraucht: Q-PRD-001 bis Q-PRD-005,
Q-OPS-002/Q-OPS-003, Q-TEC-006, Q-TEC-008, Q-AST-001/Q-AST-002, Q-OPS-001,
Q-GAM-001 bis Q-GAM-007, Q-GAM-010, Q-NAR-002 und Q-NAR-004 bleiben
ausgewiesen `OFFEN`.

## Lieferumfang des Slices

- `src/Riftward.App/Package/`: `PackageContract` (versionierte Vertrags-
  konstanten), `PackageManifestCodec` (kanonischer Einzeilencodierer, strikter
  Parser mit unterscheidbaren Klassen), `PackageVerificationException`
  (maschinenlesbare Klassennamen), `PackageSourceReader` (Commit-/Baumbindung
  über privaten Temporärindex), `PackageDocs` (deterministische Release Notes
  und Lizenz-/Attributionsmanifest, Pin-Kohortenbindung),
  `PackageComposer` (Staging, umpräfixtes Native-Artefaktmanifest, Manifest,
  Anker), `PackageArchive` (deterministischer Ustar/gzip-Schreiber, Entpacker),
  `PackageRunner` (Befehlsfläche, Publish-Orchestrierung, Eigenverifikation,
  Reports).
- `src/Riftward.Platform/PlatformError.cs`: neue kontrollierte Codes
  `PackageBuildFailed = 39` und `PackageVerificationFailed = 40`.
- `src/Riftward.App/Program.cs`: `package`-Dispatch, linux-x64-Gate-Meldung
  und Usage um den Befehl erweitert.
- `scripts/rift.sh`: `package` delegiert an den Host; `check` bleibt
  unverändert NICHT VERFÜGBAR; Hilfetext um die Paketzeile erweitert.
- Tests: `tests/RiftHarness.Tests/PackageTests.fs` (8 Einheiten) mit
  hermetischen synthetischen Eingaben (Publish-Ausgabe, Native-Dist und
  Artefaktmanifest im Temp-Verzeichnis erzeugt; gitignorierte Runtime-Evidenz
  ist Voraussetzung keines schnellen Gates — der CLI-Paketbau prüft das
  Fehlen des Native-Dists als kontrollierten Code 39, T-032-Präzedenz) und
  Registrierung in `Program.fs`.
- Dokumentation: `docs/PAKETVERTRAG.md` V1, `docs/AUTOMATION.md`
  (package-Zeile; check bleibt NICHT VERFÜGBAR), `docs/QUALITAET.md`
  (G-PACKAGE auf implementiert für linux-x64, Windows/macOS bleiben an
  T-011 verwiesen), `docs/PLATTFORMMATRIX.md` (Linux-Paketspalte verweist auf
  den Paketvertrag), `docs/NATIVE_UNTERBAU.md` (Befehlsabschnitt package und
  Exitcodetabelle 39/40). `GAME_DESIGN.md`, `ANFORDERUNGEN.md` und die
  Sitzungsverträge sind unberührt.

## Evidenz (autoritative Läufe, `artifacts/t038/authoritative/`)

- **Doppelbau byteidentisch:** zwei Paketbaue desselben Baums liefern
  SHA-256 `3230ffe7a593623eeb87a31f57d980218b35ad184a0b5a6c12d8269ce8b19987`
  (Version `0.1.0-alpha.1893a438`, 227 manifestierte Einträge, 96 896 957
  Bytes Nutzinhalt); beide Archive bestehen `--verify` mit `ok=true`
  (`verify-a.json`, `verify-b.json`). Während der Evidenzphase wurde eine
  Wahrheitsreparatur an den generierten Release Notes vorgenommen (das
  dokumentierte interaktive Verifikationskommando um `--horizon-ticks 11000`
  ergänzt, damit es am gebundenen Skriptkopf nicht mit Code 37 scheitert);
  die obige Bindung stammt vom reparierten Finalbaum, die Suite (326/326)
  und alle Gates wurden danach erneut grün ausgeführt.
- **Frischsystem-Verifikation:** das in ein frisches Verzeichnis extrahierte
  Paket läuft unter `env -i` (ohne Repository-Pfad, ohne SDK-Aufruf, ohne
  Netzwerk) und besteht `bench --scenario bench-sim` (`gate.pass=true`) und
  `savecheck` (alle Prüfklassen) sowie die Alpha-Loop-Kette über die
  gebündelte Fixture `t036-pressure-restart.graybox` (Seed 20260826,
  Speichergrenze 8100, Fortsetzungslauf als frischer Prozess).
- **Kettenbindung Paket ↔ Entwicklerbaum:** Endhashes je Paar byteidentisch —
  bench-sim `de43976087a5f6a2`, Speicherlauf `e660daf4a5eb10c4` (dokumentierter
  T-037-Wert), Fortsetzungslauf `8b4767bf5a75abb8` (dokumentierter
  T-036-/T-037-Wert); `chainContinuity.verified=true` über 48 Stichproben.
- **Manipulationsnegativfall:** eine veränderte
  `native/shaders/triangle.vs.bin` im entpackten Paket wird durch die
  bestehende Host-Artefaktprüfung kontrolliert mit Exitcode 17
  (`ArtifactHashMismatch`, gebundener Soll-Hash `fb8822a5…`) abgewiesen —
  innerhalb der gebundenen Bestandscodes 14–17; der Schutzabschnitt des
  Paketmanifests bindet Mechanismus und Codeband maschinenlesbar.
- **Blobvergleich:** alle Quelldateien von `Riftward.Simulation` sind gegen
  den Vorblob (HEAD 46d02e5) byteidentisch (`git hash-object` je Datei,
  `simulation-blob-files.txt`).
- **Testmatrix:** Suite 327/327 (318 Bestand + 9 T-038-Einheiten), davon
  gebunden: Codec-Roundtrip/Kanonform, Ablehnungsmatrix
  (schlechtes JSON, falsche Vertragskennung, falsche RID, ungültige
  Hashform, Unsortiertheit, unsicherer Pfad), deterministisches Staging und
  Manifest (zwei Compose-Läufe byteidentisch, Anker-/Versions-/Notes-/Lizenz-
  Bindungen), Verletzungsmatrix (`ENTRY_HASH_MISMATCH`, `ENTRY_INCOMPLETE`,
  `ENTRY_MISSING`, `UNMANIFESTED_FILE`, `ANCHOR_MISMATCH`,
  `ANCHOR_MISSING`, `ENTRY_SYMLINK_MISMATCH`,
  `ARTIFACT_MANIFEST_REJECTED`), Archivdeterminismus mit Entpack-/Ankerprüfung,
  CLI-Usage-Ablehnungen (2/2/2, fehlendes Archiv 40), CLI-Paketbau mit
  positiver Verifikation und `SIDE_CAR_MISMATCH`/`SIDE_CAR_MISSING`-Negativfällen,
  Exitcode-/Doku-Bindung (39/40, NATIVE_UNTERBAU- und PAKETVERTRAG-Anker).
- **Bestandsregressionen:** `bench-sim` `gate.pass=true`,
  `savecheck` `gate.pass=true` (alle Prüfklassen), Soak-Kurzlauf 3000 Ticks
  diagnostisch `gate.pass=true` (`evidenceUnit=false`); die
  kommandoschleifen Legacy-v1-/v2-/v3- und
  Erkundungs-/Entscheidungs-/Druck-/Fortsetzungsskripte sind in der Suite
  gebunden; alle bestehenden Exitcodebedeutungen unverändert.
- **Gates:** fmt `--check` ohne Fixes, lint PASS (0 Befunde), Release-Build
  mit 0 Warnungen (Publish erzeugt keine neuen Compiler-/Analyzer-/AOT-/Trimming-
  Warnungen; `TreatWarningsAsErrors` erzwingt dies), security PASS,
  rag-build OK, `rift.sh verify` valid (`runsChecked=69`).
- **Unabhängiger Review-Lauf (2026-08-30) und Wahrheitsreparatur:** die
  Review-Sitzung fand einen In-Scope-Defekt am Systemrand des
  `--verify`-Vertrags und reparierte ihn im selben Kandidaten: korrupte oder
  nicht entpackbare Archivbytes mit konsistentem Sidecar führten zuvor zu
  einem unkontrollierten Prozessabbruch (SIGABRT, Exit 134, kein
  Prüfreport); der Verifikator weist sie jetzt kontrolliert mit Exit 40,
  Prüfreport und der Vertragsklasse `ARCHIVE_UNREADABLE` ab (PAKETVERTRAG
  Abschnitt 4 um die Klasse ergänzt; ebenso falsch typisierte Manifestfelder
  kontrolliert als `MANIFEST_MALFORMED`). Testmatrix danach 327/327 (neu:
  CLI-Verifikation des unlesbaren Archivs), Doppelbau auf dem Reparaturbaum
  erneut byteidentisch, Frischsystemkette (bench-sim/savecheck/
  Speicher-/Fortsetzungslauf) aus dem Reparaturpaket erneut mit
  builderidentischen Endhashes gebunden, Manipulationsabweisung 17 und
  displayloser Code-19-Nachweis erneut belegt.

## AC-Abdeckung

- **AC-T038-01** (Abschnitt 0 vor Implementierung): PAKETVERTRAG.md V1 mit
  allen geforderten Entscheidungen, Alternativen, Empfehlungen, Prüf-/Playtest-
  kriterien und Rückrollwegen; Media-Lab-Abschnitt; keine verbrauchte
  offene Frage; Spiegelbindung der Vertragskonstanten im Test.
- **AC-T038-02** (`package` für linux-x64): Befehl implementiert; Doppelbau
  byteidentisch; Pflichtinhalte gebunden; Schreibgrenzen (Ausgabe-/Arbeits-
  bereich plus vom SDK verwaltete Buildausgaben); kein Netzwerk nach der
  dokumentierten Runtime-Pack-Erstbeschaffung; keine neue Abhängigkeit; null
  neue Warnungen; Usage-/RID-Ablehnung Code 2, Bau-Abbruch 39 ohne Report;
  `check` bleibt NICHT VERFÜGBAR; G-PACKAGE-Zeile fortgeschrieben.
- **AC-T038-03** (Frischsystem-Verifikation): headless Alpha-Loop aus dem
  Paket ohne Repository/SDK/Netz mit builderidentischer Fortsetzungskette und
  Endhashbindung an den Entwicklerbaum-Referenzlauf; Manipulationsabweisung
  über die bestehende Host-Prüfung (Code 17) mit maschinenlesbarem
  Schutzabschnitt; Blobvergleich Riftward.Simulation byteidentisch.
- **AC-T038-04** (fensterpflichtiger Paketsmoke): ohne nutzbares Display in
  dieser Sitzung ist der kontrollierte Code-19-Nachweis die ehrliche Evidenz:
  virtuelles kwin_wayland/Xwayland wurde gestartet, die X-Sockets sind in
  dieser Sandbox nicht erreichbar („Authorization required“ beziehungsweise
  „No available video device“); plattformsmoke und
  `kommandoschleife --interactive --auto-exit-at-horizon` aus dem entpackten
  Paket enden kontrolliert mit Code 19 ohne Report und ohne Simulation. Die
  vorregistrierten Playtestkriterien (Start ohne Entwicklungsabhängigkeit,
  Alpha-Kennzeichnung, Moduswechsel, Save/Load aus dem Paket) bleiben
  ausgewiesener Restpunkt einer Displaysession; kein Abgriff produziert,
  kein Media-Lab-Eintrag erzeugt. **Vorfallhinweis (Ehrlichkeit):** während
  des Displayversuchs beendete ein `pkill`-Muster des Builders die laufende
  grafische Sitzung des Entwickler-PCs (tty2); der SDDM-Greeter ist aktiv,
  der Vorfall ist im Harness-Run dokumentiert und nicht reproduziert.
- **AC-T038-05** (Gates und Regressionen): alle schnellen Gates grün;
  0 neue Warnungen; keine neue Abhängigkeit; Bestandsregressionen grün;
  Exitcodebedeutungen unverändert; Dokumentation bildet den implementierten
  Stand zeichentreu ab; dieses Abnahmedokument verknüpft jedes Kriterium mit
  Evidenz; das Playtestprotokoll ist mit dem displaylosen Restpunkt verbunden.

## Restpunkte

- Der fensterpflichtige Paketsmoke (plattformsmoke und interaktive
  Kommandoschleife aus dem Paket, Playtestkriterien, optionaler opt-in
  Abgriff) bleibt einer Displaysession vorbehalten; der kontrollierte
  Code-19-Abbruch aus dem Paket ist belegt.
- Pflichtprofile bleiben `NOT-MEASURED` (Q-OPS-001); der Paketsmoke ist kein
  Performancebeweis und es entsteht kein neuer budgettragender Pfad.
- Windows-/macOS-Paketierung, Signierung, Notarisierung, Installer, Store,
  Update und Vertrieb bleiben an T-011 beziehungsweise Q-OPS-002/Q-OPS-003/
  Q-PRD-005 verwiesen; die hier versionierte Linux-Paketform ist dokumentierte
  Basis für T-011, nicht dessen Ersatz.
- Die Projektlizenz bleibt offen (Q-PRD-001); das Paket enthält die
  Komponentenlizenzen der gebündelten Dritten und erhebt keine
  Lizenzbehauptung für eigenen Code.
