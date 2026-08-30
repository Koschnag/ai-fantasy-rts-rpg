# Paketvertrag (linux-x64, T-038)

**Vertragskennung:** `riftward-paketvertrag-v1`
**Status:** Abschnitt 0 des Auftrags T-038 (gatender Vertragsspike, vor der
Implementierung abgeschlossen, Spike-Klausel `docs/QUALITAET.md`)
**Geltungsbereich:** genau der RID `linux-x64`; Windows, macOS und die
dreiplattformige Matrix bleiben unverändert an T-011 verwiesen (Q-OPS-002/
Q-OPS-003, NF-006 für die übrigen Pflicht-RIDs).

Dieser Vertrag fixiert die reversible Produktentscheidungen des kleinsten
Single-Platform-Releasepfads vor der Implementierung. Jede Entscheidung ist
mit ernsthaften Alternativen, begründeter Empfehlung, messbarem Prüf- oder
Playtestkriterium und Rückrollweg dokumentiert. Eine spätere Änderung eines
Abschnitts ist eine neue Vertragsversion; eine stille Feldumdeutung ist
verboten.

## 1. Paketformat und Paketlayout

| Option | Bewertung |
|---|---|
| **A. Deterministisches tar.gz (Empfehlung)** | POSIX-Archiv mit fixiertem `SOURCE_DATE_EPOCH`, gzip ohne Zeitstempel (`MTIME=0`), sortierter Dateireihenfolge, festen Besitzernamen und festen Modi; mit Standardwerkzeugen (`tar`/`sha256sum`) prüfbar, offline entpackbar, keine neue Abhängigkeit. |
| B. Unkomprimiertes tar | Gleiche Determinismusmechanik, aber 3–4× Transfergröße; für einen internen Alpha-Releasepfad ohne Vorteil. |
| C. zip | Widespread, aber deterministische Zip-Archive erfordern zusätzliche Werkzeugdisziplin (Zeitfelder, Extrabereiche), und das Linux-Pflichtpfad-Werkzeugumfeld arbeitet tar-nativ. |
| Verworfen: AppImage-/Installer-/Store-Tooling | Neue Abhängigkeit und eine nicht delegierbare Vertriebsentscheidung (Q-OPS-002/Q-OPS-003 bleiben `OFFEN`). |

**Empfehlung A.** Prüfkriterium: zwei Paketbaue desselben Baums erzeugen
byteidentische Archive und identische Sidecar-Prüfsummen (Test gebunden).
Rückrollweg: Formatwechsel ist ein neuer Vertragsabschnitt; bereits
geprüfte Pakete bleiben per Manifestkennung unterscheidbar.

### 1.1 Layout

Das Archiv enthält genau ein Wurzelverzeichnis
`riftward-<version>-linux-x64/` (sichere Entpackform, keine Streudateien):

```
riftward-<version>-linux-x64/
├── Riftward.App                 # selbstenthaltener Publish-Ausgabesatz unverändert
│   ...                          #   (Runtime-DLLs, deps.json, runtimeconfig.json, createdump)
├── toolchain.lock.json          # unverändert aus dem Baum (Hostpins, Budgetbindung)
├── native/
│   ├── lib/libSDL3.so.0         # Symlink auf libSDL3.so.0.4.14 (wie im Native-Dist)
│   ├── lib/libSDL3.so.0.4.14    # manifestiert
│   ├── lib/libriftbgfx.so       # manifestiert
│   ├── shaders/*.bin            # genau die 12 offline übersetzten Shaderbinaries
│   └── artifact-hashes.json     # Native-Artefaktmanifest mit Pfadpräfix native/
├── fixtures/command/*.graybox   # genau die versionierten Bestandsfixtures (byteidentisch)
├── docs/
│   ├── RELEASE_NOTES.md         # ehrliche interne-Alpha-Kennzeichnung, Aussagegrenze,
│   │                            #   Entpack-, Start- und Prüfanleitung (offline)
│   └── LIZENZEN.md              # Lizenz-/Attributionsmanifest mit Komponentenlizenztexten
├── package-manifest.json        # Schemaversion 1, Einträge je Datei, Schutzabschnitt
└── package-manifest.sha256      # Paketanker: sha256sum-Form über package-manifest.json
```

Pflichtregeln:

- Der Publish-Ausgabesatz wird bytegetreu gebündelt; der Paketbau entfernt,
  ergänzt oder verändert keine Publish-Datei.
- Die Native-Laufzeitartefakte werden aus dem bestehenden Native-Dist
  (`.ai/runtime/cache/native/`) gebündelt; die Einträge (SHA-256, Bytes) des
  gebündelten `native/artifact-hashes.json` sind identisch zum aufgezeichneten
  Native-Buildmanifest, nur der Pfadpräfix wird deterministisch auf `native/`
  umgeschrieben. Das ist Manifestbindung über die bestehende Host-Prüfung
  (`--artifacts-dir native --manifest native/artifact-hashes.json`), keine
  neue Prüflogik.
- Die Fixture-Skripte werden byteidentisch aus `tests/fixtures/command/`
  gebündelt; der Paketbau erzeugt keine abweichende Kopie.
- Der Paketbau schreibt ausschließlich in den vertraglich erlaubten Ausgabe-
  und Arbeitsbereich (Abschnitt 5) und in die vom .NET-SDK selbst verwalteten
  `bin/`-/`obj/`-Buildausgaben. Kein Netzwerkzugriff, keine Schreibvorgänge
  außerhalb dieser Verzeichnisse.

## 2. Runtimeform des .NET-Anteils

| Option | Bewertung |
|---|---|
| **A. Selbstenthaltener CoreCLR-Publish ohne AOT und Trimming (Empfehlung)** | Start ohne installiertes SDK/Runtime, ohne Entwicklungsabhängigkeiten; null AOT-/Trimming-Warnungen erzwungen; Messverhalten identisch zum bestehenden Host. |
| B. Framework-abhängig mit gebündelter Runtime | Manual-Bundle (`dotnet store`/Runtimepakete) erzeugt eine zweite Verteilform mit eigenen Reproduzierbarkeitsfallen; kein Vorteil gegenüber A. |
| C. Native AOT | Verboten ohne die geforderte Messung (Q-TEC-008 bleibt `OFFEN`); Reflection-/Trimmingvertrag des Hosts wäre neu zu beweisen. |

**Empfehlung A.** Prüfkriterium: das extrahierte Paket startet in einem
frischen Verzeichnis ohne installiertes .NET-SDK, ohne Repository und ohne
Netzwerk und führt die Alpha-Loop-Verifikation aus (Abschnitt 6); der
Publish erzeugt null neue Compiler-, Analyzer- und AOT-/Trimming-Warnungen.
Rückrollweg: AOT-/Trimmingentscheidung bleibt über Q-TEC-008 offen und
bedarf einer messenden Entscheidung; die Paketform ändert keine
Laufzeitsprache oder Budgetwerte.

## 3. Reproduzierbarkeitsregel

| Option | Bewertung |
|---|---|
| **A. Byteidentischer Doppelbau vom selben Baum (Empfehlung)** | Fixiertes `SOURCE_DATE_EPOCH=1786623387` (Lockfile-`generatedAtUtc`, identisch zum Native-Build), deterministische MSBuild-Kompilation (`Deterministic=true`, `Directory.Build.props`), feste Dateireihenfolge (ordinal sortierte Pfade), Ustar-Einträge mit fixer mtime/uid/gid/uname/gname/mode, gzip ohne Zeitstempel. |
| B. Feldweise Normalisierung nach dem Bau | Nachträgliche Archivumschreibung erzeugt eine zweite Wahrheitsebene und verdeckt nichtdeterministische Eingaben. |
| C. Nur logische Reproduzierbarkeit (Manifestgleichheit) | Schwächer als Byteidentität; wird bewusst nicht als ausreichend erklärt. |

**Empfehlung A.** Prüfkriterium: zwei Paketbaue desselben Baums liefern
denselben Archiv-SHA-256 und dieselbe Sidecar-Prüfsumme; der Test bindet
diese Gleichheit. Jede beobachtete Abweichung ist als dokumentierte Ausnahme
mit Grund im Run ausgewiesen — niemals eine stille Lockerung. Die
Gleichheitsbehauptung gilt maschinengebunden für dieselbe
SDK-/Toolchain-Version (global.json 10.0.110); eine Cross-Build-Umgebungs-
zusage wird ausdrücklich nicht gemacht. Rückrollweg: eine Lockerung auf
logische Reproduzierbarkeit wäre eine dokumentierte Vertragsänderung mit
Begründung; Verschärfung (z. B. Cross-Umgebungsbindung) bleibt zulässig.

## 4. Manifest-, Checksum- und Attributionsschema

| Option | Bewertung |
|---|---|
| **A. JSON-Manifest + SHA-256 je Datei + Paketanker (Empfehlung)** | `package-manifest.json` (Schemaversion 1) listet jeden Archivinhalt außer dem Manifest selbst und seinem Anker mit Pfad, SHA-256, Bytegröße, Unix-Modus und Eintragstyp (Datei/Symlink); `package-manifest.sha256` ist der Anker über das Manifest in `sha256sum`-Form; das Archiv-Bytepaar bindet ein Sidecar `<archiv>.sha256`. |
| B. Nur ein einzelner Pakethash | Ein defektes Paket wäre nicht lokalisierbar; Dateiebene fehlt. |
| C. Signaturkette | Signierentscheidung ist ausdrücklich nicht Teil dieses Slices (Q-OPS-002/Q-OPS-003). |

**Empfehlung A.** Prüfregeln (`package --verify`):

- Pflichtinhalte des Manifests: Schemaversion, Vertragskennung
  `riftward-paketvertrag-v1`, Paketkennung/Version/RID/Runtimeform/Alpha-
  Marker, Quellbindung (Commit, Baum-Digest, `sourceDateEpoch`), Artefakt-
  manifestbindung (Pfad, SHA-256, Upstream-Pinkennung), Schutzabschnitt
  (Bestandscodes 14–17), sortierte Einträge mit gültigen SHA-256-Formen
  (64 hex) und nichtnegativen Bytegrößen, sowie Symlinkeinträgen mit exakt
  einem Ziel.
- Verletzungsklassen (unterscheidbar, maschinenlesbar im Prüfreport):
  `MANIFEST_MISSING`, `MANIFEST_MALFORMED`, `MANIFEST_HASH_INVALID`,
  `ANCHOR_MISSING`, `ANCHOR_MISMATCH`, `SIDE_CAR_MISSING`,
  `SIDE_CAR_MISMATCH`, `ENTRY_MISSING`, `ENTRY_INCOMPLETE`,
  `ENTRY_HASH_MISMATCH`, `ENTRY_SYMLINK_MISMATCH`, `UNMANIFESTED_FILE`.
  Zusätzlich prüft die Verifikation das gebündelte Native-Artefaktmanifest
  über die bestehende Host-Prüfung (`NativeArtifacts.Validate`) und bindet
  deren unterscheidbaren Bestandsgrund (Codes 14–17) in denselben Report.
- Ehrliche Kennzeichnung: `interne Alpha` mit Aussagegrenze (Graybox,
  kein Gameplay-, Atmosphären-, Performance- oder Shipping-Beleg). Keine
  erfundene Lizenzbehauptung für eigenen Code (Q-PRD-001 bleibt `OFFEN`).
- Attributionsableitung: `docs/LIZENZEN.md` wird deterministisch aus
  `toolchain.lock.json` (`nativeComponents`, Kohorte) und
  `THIRD_PARTY_NOTICES.md` erzeugt und nennt je gebündelter Komponente
  Version/Pin, Lizenzbezeichner, Zweck und den Lizenztext (zlib für SDL3,
  BSD-2-Clause für bgfx/bx/bimg, MIT-Hinweis für die gebündelte
  .NET-Runtime samt Pakethinweis auf die NuGet-Notices). Rückrollweg:
  Felderweiterungen sind additive Schemaversion 2; bestehende Felder werden
  nicht umgedeutet.

## 5. Versionierungs- und Kennzeichnungsschema

| Option | Bewertung |
|---|---|
| **A. `0.1.0-alpha.<tree8>` mit Quellbindung (Empfehlung)** | `<tree8>` sind die ersten 8 Hexzeichen des SHA-256 des hypothetischen Add-A-Baums (Kandidatenidentität, privater Index, ohne Änderung des echten Index). Zusätzlich bindet das Manifest den vollen Commit-SHA-256 und den Baum-Digest. |
| B. Nur Datums-/Kalenderversion | Bindet keine Quelle; zwei Pakete desselben Datums wären ununterscheidbar. |
| C. Nur Baum-Digest als Name | Ehrlich, aber ohne Ordnungshinweis; Alpha-Stufe bliebe unmarkiert. |

**Empfehlung A.** Prüfkriterium: zwei Baue desselben Baums erzeugen
dieselbe Version und denselben Paketnamen; eine Quelländerung ändert
`<tree8>` nachweislich. Kennzeichnung: Archivname
`riftward-<version>-linux-x64.tar.gz`, Wurzelverzeichnis entsprechend,
Alpha-Marker `internal-alpha-graybox-v1` mit Aussagegrenze im Manifest und
in den Release Notes. Rückrollweg: Schemaänderung ist additive
Vertragsänderung; bestehende Pakete bleiben per Manifestkennung
unterscheidbar.

## 6. Befehls-, Exitcode- und Installationsvertrag

### 6.1 Befehlsform

- `rift.sh package --output-dir VERZ [--work VERZ] [--rid linux-x64]`
  erzeugt Archiv und Sidecar im Ausgabeverzeichnis (Standard
  `artifacts/package/`, gitignored); Arbeits- und Stagingbereich liegt
  standardmäßig unter dem Ausgabeverzeichnis.
- `rift.sh package --verify ARCHIV.tar.gz [--work VERZ]` prüft Sidecar,
  Archiv, Manifestkette und Native-Artefaktmanifest und schreibt einen
  einzeiligen, maschinenlesbaren Prüfreport.
- `check` bleibt unverändert NICHT VERFÜGBAR; die bisherige
  NICHT-VERFÜGBAR-Meldung entfällt nur für `package`.
- Unbekannte Optionen, ein fehlender oder unbekannter `--rid`-Wert und
  Usage-Fehlanwendungen schlagen kontrolliert mit der bestehenden
  Usage-Bedeutung 2 fehl. Neue, bislang ungenutzte Exitcodes: **39**
  Paketbau fehlgeschlagen (kontrolliert, kein Teilvertrauen, kein Report)
  und **40** Paketverifikation fehlgeschlagen (Prüfreport geschrieben, klar
  als nicht bestanden markiert, Verletzungsklasse maschinenlesbar). Alle
  bestehenden Exitcodebedeutungen bleiben unverändert; die Tabelle in
  `docs/NATIVE_UNTERBAU.md` wird synchronisiert.

| Option | Bewertung |
|---|---|
| **A. Ein öffentlicher `package`-Befehl mit Build+Verify (Empfehlung)** | Ein Befehl, zwei dokumentierte Modi; Verifikation ist im selben Vertrag prüfbar und im Test bindbar. |
| B. Getrennte `package`/`package-verify`-Befehle | Zwei öffentliche Oberflächen ohne Mehrwert; Verwechslungsrisiko im Gate. |
| C. Verifikation nur als Test ohne öffentlichen Befehl | Der Frischsystemlauf verlöre die prüfbare Manifestwahrheit außerhalb der Suite. |

### 6.2 Installations- und Entpackform

| Option | Bewertung |
|---|---|
| **A. Manuell installierbares Offline-Archiv (Empfehlung)** | `tar xzf <archiv>` in ein beliebiges Nutzer-/Anwendungsverzeichnis; Start `./Riftward.App`; alle Pflichtargumente für den Paketsmoke sind in den Release Notes dokumentiert. |
| B. Fixiertes Installationsziel (z. B. `/opt`) | Benötigt Root-Rechte und eine nicht delegierbare Systemintegrationsentscheidung. |
| C. Ein-Skript-Installer | Ein zusätzlicher Ausführungspfad mit eigenen Fehlerfällen; für die interne Alpha ohne Mehrwert. |

**Empfehlung A** gemäß Q-OPS-003-Arbeitsannahme; keine Signier-, Notarie-
rungs-, Store- oder Auto-Update-Entscheidung (Q-OPS-002/Q-OPS-003,
Q-PRD-005 bleiben `OFFEN`). Prüfkriterium: der Frischsystemlauf (Abschnitt 7)
entpackt und startet ausschließlich mit den Anleitungsschritten der Release
Notes. Rückrollweg: ein späterer Installer wäre ein eigener Auftrag mit
eigener Freigabe.

## 7. Paketsmoke-Umfang (Evidenz)

**Headless Frischsystemlauf (Pflichtnachweis):** das in ein frisches
Verzeichnis (außerhalb des Repositorys) extrahierte Paket wird ohne
Repository, ohne .NET-SDK und ohne Netzwerk ausgeführt und bestätigt genau
den Alpha-Loop:

1. `./Riftward.App bench --scenario bench-sim --report PFAD` → Gate grün.
2. `./Riftward.App savecheck --report PFAD` → Gate grün.
3. `./Riftward.App kommandoschleife ...` headless über die gebündelte
   Bestandsfixtureskette (Erkundung, Entscheidung, Druck; Start-/Fortsetzungs-
   paare über `--save-at-tick`/`--load-slot` per T-037-Vertragsfixture) →
   Fortsetzungskette und Endhash builderidentisch zum Entwicklerbaum-
   Referenzlauf.
4. Manipulationsnegativfall: eine veränderte Native-Artefaktdatei im
   entpackten Paket wird durch die bestehende Host-Artefaktprüfung
   kontrolliert abgewiesen (Bestandscodes 14–17); der Grund wird
   maschinenlesbar im Schutzabschnitt des Pakets gebunden.
5. Der Lauf weist maschinenlesbar aus, dass kein Repository-, SDK- oder
   Netzwerkpfad benutzt wurde; `Riftward.Simulation` bleibt gegen den
   Vorblob byteidentisch (Blobvergleich im Run).

**Fensterverpflichtender Paketsmoke (Displaysession):** `plattformsmoke` und
`kommandoschleife --interactive --auto-exit-at-horizon` (Moduswechsel,
Save-/Lade-Keymap F5/F9) laufen aus dem Paket mit
`--artifacts-dir native --manifest native/artifact-hashes.json` auf dem
Entwickler-PC, gegebenenfalls virtuelles Wayland nach T-023-/T-033-
Präzedenz. Ohne nutzbares Display ist der kontrollierte Code-19-Nachweis mit
ausgewiesenem Restpunkt die ehrliche Evidenz; keine Simulation.

**Vorregistrierte Playtestkriterien des Paketsmokes** (Ausführung im Run
dokumentiert):

1. Paketstart ohne Entwicklungsabhängigkeit: nach Entpacken startet das
   Spiel binnen 30 Sekunden ohne Fehlermeldung, ohne SDK/Runtime-Installations-
   hinweis.
2. Sichtbare Alpha-Kennzeichnung: der Titel-HUD weist die interne Alpha-
   Kennzeichnung sichtbar aus.
3. Moduswechsel aus dem Paket: `mode-switch` (Tab) wechselt sichtbar zwischen
   strategischem und persönlichem Modus binnen der vertraglichen
   Reaktionsgrenze (≤ 3 Ticks hart / ≤ 2 Ziel).
4. Gespeicherter/ladbarer Sitzungszustand aus dem Paket: F5 speichert, F9
   lädt; der Titel-HUD weist die restaurierte Kettenwahrheit aus; beide
   Aktionen innerhalb eines 10-minütigen Fensters erkennbar.

Rückrollweg: Kriterienänderungen sind vor dem Playtestlauf zu registrieren;
ein nachträglich abgeschwächtes Kriterium ist unzulässig.

## 8. Media-Lab-Entscheidung

Ein opt-in Einzelabgriff ist höchstens im fensterpflichtigen Paketsmoke
zulässig und folgt ausschließlich der bestehenden T-023-/T-033-Regel (lokal,
hashgebunden, Aussagegrenze Graybox-Zustandsbelegung, niemals Gameplay-,
Atmosphären- oder Shipping-Beleg, öffentliche Verwendung nur über
`docs/communication/MEDIA_LAB.md` plus Projektleitungsautorisierung). Ohne
produzierten Abgriff entsteht kein Media-Lab-Eintrag. Das headless
Frischsystemlauf erzeugt kein Bild.

## 9. Ausgeschlossene Entscheidungen und offene Fragen

Dieser Vertrag verbraucht keine offene Produktfrage. Ausdrücklich `OFFEN`
und unberührt bleiben: Q-PRD-001 (Projektlizenz), Q-PRD-002 bis Q-PRD-005
(Veröffentlichung, Vertrieb, Preis, Titel), Q-OPS-002/Q-OPS-003 (Signierung,
Notarisierung, Installer, Update), Q-TEC-006 (Cooked-/Definitions-/Replay-
format), Q-TEC-008 (AOT-/Trimmingentscheidung), Q-AST-001/Q-AST-002
(Generator-, Storage-/Backupfreigaben), Q-OPS-001 (Referenzhardware;
Pflichtprofile bleiben `NOT-MEASURED`), Q-GAM-001 bis Q-GAM-007, Q-GAM-010,
Q-NAR-002 und Q-NAR-004 (fachliche Spielregeln). Kein Paketinhalt ist ein
Shipping-Asset; die Grayboxwelt entsteht unverändert laufzeitdeterministisch.

## 10. Abschnitt-0-Protokoll

- Paketformat: tar.gz (Abschnitt 1, Empfehlung A).
- Runtimeform: selbstenthaltener CoreCLR-Publish ohne AOT/Trimming
  (Abschnitt 2, Empfehlung A).
- Reproduzierbarkeit: byteidentischer Doppelbau (Abschnitt 3, Empfehlung A).
- Manifest-/Checksum-/Attributionschema: Abschnitt 4, Empfehlung A.
- Versionierung: `0.1.0-alpha.<tree8>` mit Commit-/Baumbindung (Abschnitt 5,
  Empfehlung A).
- Befehls-/Exitcode-/Installationsvertrag: Abschnitt 6, Empfehlungen A; neue
  Codes 39/40.
- Paketsmokeumfang und Playtestkriterien: Abschnitt 7.
- Media-Lab-Entscheidung: Abschnitt 8.
