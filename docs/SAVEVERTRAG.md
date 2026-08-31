# Savevertrag (T-031, Abschnitt 0)

**Vertragsversion:** 3 (V3 ergänzt ausschließlich die autorisierte additive
Missions-Sektionsfläche gemäß Abschlussvertrag V1/T-039, Abschnitt 15; V2
ergänzte die additive Sitzungssektions-Erweiterung gemäß
`.ai/tasks/T-037-graybox-continuation-restart.json`, Abschnitt 13; alle
übrigen Abschnitte sind gegenüber V1 inhaltlich unverändert.)
**Status:** Durch den gatenden Vertragsspike des Auftrags
`.ai/tasks/T-031-atomic-save-load.json` festgelegt, bevor die
Saveimplementierung (Kodierung, Validierung, Slotprotokoll, `savecheck`-Lauf)
erfolgte. Die maschinenlesbaren Kennungen sind in
`src/Riftward.Save/SaveContract.cs` gespiegelt und werden von einem Test gegen
dieses Dokument gehalten.

Dieser Vertrag entscheidet Q-TEC-006 ausschließlich im Teilaspekt
Save-Umschlag/Persistenzformat des Simulationszustands verfahrensmäßig im
Rahmen der Spike-Klausel (`docs/QUALITAET.md`, Definition of Ready): Jede Wahl
nennt Alternativen, Gründe und Rückrollweg. Die Anteile Cooked-Paketformat,
Definitionsformat und Replaydateiformat bleiben ausdrücklich `OFFEN`
(Q-TEC-006-Rest); der Simulationsvertrag V1 wird an keiner Stelle ratifiziert
(Q-TEC-004 bleibt `OFFEN`). Es werden keine Budgetzeilen aus
`docs/PERFORMANCE_BUDGET.md` geändert; die Kartenladezeit-Budgetzeile bleibt
ausschließlich Eigentum von BENCH-LOAD. Es werden keine fachlichen
Spielregeln, Inhalte, UI-Flows oder Checkpoint-/Retry-Politiken festgelegt
(Q-GAM-001 bis Q-GAM-007, Q-NAR-002 bleiben `OFFEN`).

## 1. Kanonische Serialisierung

**Wahl:** Handschriftliche, kanonische Binärcodierung ohne Serialisierungs-
bibliothek (`riftward-save-canonical-binary-v1`). Feste Feldordnung in allen
Abschnitten, Little-Endian-Festbreiten-Ganzzahlen, UTF-8-Zeichenfolgen mit
vorangestellter `u16`-Bytelänge, keine Auffüllbytes, keine optionalen Felder,
keine Feldkennungen (Positionslayout). Die Schemaversion (`u16`) steht direkt
nach dem 4-Byte-Magic `RWSD` und ist damit zuerst lesbar. Der Ladepfad ist ein
strikter Einzelpass-Decoder mit exakter Bytelängenrechnung, vor der
Zuweisung geprüfter Längengrenzen und Re-Encoding-Vergleich
(Dekodieren → erneutes Kodieren muss byteidentisch sein). Er verwendet
ausschließlich BCL-Typen, keine Reflection, keinen Quellgenerator und keine
dynamische Codegenerierung.

**Erfüllte fixierte Kriterien:** Determinismus (feste Byteordnung, keine
Umgebungsabhängigkeit); BCL-only-Vorzug (keine neue Abhängigkeit);
AOT-/Trimming-freundlich ohne Reflection; vor-Payload-lesbarer Umschlag
(Kopfsection mit Anzeigemetadaten liegt vollständig vor dem Payload).

**Alternativen:**

| Alternative | Ablehnungsgrund |
|---|---|
| Kanonisches JSON | Textcodierung macht Zahlenformat-, Flucht- und Ordnungsregeln zur zweiten Fehlerquelle; strikte Duplikat-/Ordnungserkennung erfordert einen eigenen Parser mit denselben Eigenschaften wie der Binärdecoder, bei größerem Savevolumen und mehr Randfälle. |
| System.Text.Json mit Source Generators | Generatorkontext erzeugt eine zweite Kompilationsebene mit Versionsdrift im Ladepfad; die strikte Kanonform (Re-Encoding-Gleichheit) wäre nachträglich zu erzwingen. Kein Vorteil gegenüber dem direkten Decoder. |
| Binärcodierung mit Feldkennungen (TLV) | Kennungen erlauben Umordnungen und Duplikate überhaupt erst; der Positionsdecoder weist sie stattdessen strukturell ab. |

**Rückrollweg:** Änderungen der Codierung ausschließlich als neue
Vertragsversion dieses Dokuments mit neuer `saveSchemaVersion`; alte Saves
bleiben als historische Evidenz lesbar oder werden über dokumentierte,
getestete Migrationsschritte geführt (Abschnitt 8).

## 2. Integrität: payloadHash, metaHash und Metadatenabgrenzung

**Wahl:** Zwei voneinander getrennte SHA-256-Anker:

1. `payloadHash` = SHA-256 über genau die kanonischen Payloadbytes; im Kopf
   hinterlegt und durch den zweiten Anker gedeckt.
2. `metaHash` = SHA-256 über Magic, Schemaversion, Kopflänge und alle
   Kopfbytes einschließlich `payloadHash`-Feld; direkt hinter dem Kopf
   abgelegt.

Die UTC-Erzeugungs-/Änderungszeit, die opaque `saveId` und der `buildId`
sind ausdrücklich **nicht** Teil der Determinismusbehauptung: Zwei Prozesse,
die denselben Simulationszustand am selben Tick serialisieren, erzeugen
byteidentische Payloadbytes und denselben `payloadHash`, während sich ihre
Umschlagmetadaten und damit die Gesamtdateibytes zulässig unterscheiden
dürfen. Die Abgrenzungsmethode ist somit strukturell: Der Determinismusanker
liegt ausschließlich auf dem Payloadabschnitt, und die Metadaten liegen
außerhalb jedes Inhaltsanchors. Integrität dient der Fehlererkennung; ein
Signatur-, Manipulations- oder Anti-Cheat-Schutzversprechen wird nicht
erhoben (DATENMODELL).

**Prüfreihenfolge beim Laden (bindend, deterministische Klasse):**
Framing (Magic, Schemaversion, Längen, Abschneidung/Überhang, Größenlimits)
→ `metaHash` → `payloadHash` → kanonische Dekodierung mit
Re-Encoding-Gleichheit → Grenzwertprüfung → Referenzprüfung. Jeder Save wird
höchstens einer Verletzungsklasse zugeordnet; die Reihenfolge garantiert,
dass ein Payload-Bitfehler stets als Payloadintegritätsverletzung erscheint,
auch wenn er zusätzlich Grenzwerte träfe.

**Alternativen:**

| Alternative | Ablehnungsgrund |
|---|---|
| Nur ein Hash über alles | „Falscher payloadHash“ (Manipulation des Hashfelds) und „Bitfehler im Payload“ wären offline ununterscheidbar; die geforderte unterscheidbare Korruptionsmatrix (AC-T031-06) wäre nicht erfüllbar. |
| HMAC oder Signatur | Begründet ein Manipulationsschutzversprechen, das DATENMODELL für das Offline-Einzelspiel ausdrücklich nicht Ziel setzt. |
| CRC32 statt SHA-256 | DATENMODELL nennt SHA-256 als Referenz; Kollisionsreserven eines CRC reichen für den Korruptionsnachweis des Auftrags nicht heran. |

**Rückrollweg:** Ankeränderung nur als neue Vertragsversion; der doppelte
Anker kann jederzeit auf einfachen Hash reduziert werden, nicht aber ohne
Neuvertrag verschärft werden (Verschärfung bleibt erlaubt).

## 3. Payloadumfang V1 (simrelevanter Relevantzustand)

**Wahl:** Der Payload V1 umfasst exakt den von `SimWorld.ComputeStateHash()`
gehashten Relevantzustand des Simulationsvertrags V1 in dessen fester
Feldordnung: Tickindex, Seed, Gruppenziele sowie je Agent Position (X/Y),
Geschwindigkeit (X/Y), Zielkachel, Gruppenindex, Pfadstatus, geplante Zone,
Wegpunktcursor/-anzahl und die ausstehenden Wegpunkte ab Cursor.
Wegpunktcursor und -anzahl werden bytegetreu übernommen; ein transientes
Paar Cursor>Anzahl (Erschöpfung eines Pufferpfads vor leerem Neupfad) ist
bestandteil des gehashten Relevantzustands und wird mit kanonisch leeren
Schwanz kodiert, nicht glättet. Transiente Sucharbeitsplätze,
Serialzähler und diagnostische Erweiterungszähler gehören gemäß
Simulationsvertrag V1 Abschnitt 4 nicht zum Relevantzustand und werden
nicht gespeichert. Der Payload ist ausreichend, um in einem frischen
Prozess fortzusetzen und eine byteidentische Hashkettenfortsetzung zu
erreichen; der Nachweis erfolgt über AC-T031-04.

**Alternative:** Zusätzliche Persistenz diagnostischer Zähler — abgelehnt,
weil sie nicht zum Relevantzustand gehören und jede Aufnahme den Payloadumfang
ohne Determinismusnutzen verbreitern würde. **Rückrollweg:** Payloadumfang-
Änderungen erfordern eine neue `saveSchemaVersion` und diesen Vertrag in
neuer Version.

## 4. Envelopeabbild (SaveEnvelope aus DATENMODELL.md)

**Wahl:** Das Dateiformat bildet die logischen Pflichtfelder so ab:

| SaveEnvelope-Feld | Abbildung im Format V1 |
|---|---|
| `saveSchemaVersion` | `u16` direkt nach dem Magic, zuerst lesbar |
| `saveId` | 16 opake Bytes im Kopf (Zufallswert, Metadatum) |
| `createdAtUtc` / `updatedAtUtc` | Unix-Millisekunden (`i64`) im Kopf, UTC |
| `buildId` | UTF-8-Zeichenfolge im Kopf (Commitkennung) |
| `contentPackages` | `u16`-Anzahl; V1 bindet **keine** Paketreferenzen, weil noch keine Datenquelle existiert (T-050/T-051 stehen aus). Die Stelle wird als ausdrücklich leere/unavailable Vertragsstelle geführt, niemals erfunden. |
| `displayMetadata` | Spielzeit als Tickspiegelung, lokalisierbare Ortsangabe als ausdrücklich leere Zeichenfolge (unavailable), Vorschaubild als ausdrücklich unbelegtes Byte; alles liegt vor dem Payload und ist ohne vollständigen Payload lesbar (`ReadDisplayMetadata`). |
| `worldState` | kanonischer Payload (Abschnitt 3) |
| `payloadHash` | SHA-256 über den Payload, im Kopf, durch `metaHash` gedeckt |

Zusätzlich verankert der Kopf Seed, Welt-/Vertrags-/Codierkennung, Planhash
und den Zustandshash am Snapshot-Tick als Diagnoseanker; der Loader prüft die
wiederhergestellte Welt gegen diesen Anker, fail-closed.

**Alternative:** Pflichtfelder mit erfundenen Werten belegen (etwa ein
Platzhalterpaket) — abgelehnt als Erfindung nicht vorhandener Datenquellen;
DATENMODELL schreibt unavailable-Kennzeichnung vor. **Rückrollweg:** Neue
Kopffelder nur mit neuer Vertragsversion; das Positionslayout hält Erweiterungen
über eine neue `saveSchemaVersion`.

## 5. Atomarprotokoll

**Wahl (gemäß DATENMODELL-Lebenszyklus und ARCHITEKTUR.md):**

1. Schreiben in eine temporäre Datei im **selben Verzeichnis** wie der
   Ziel-Slot (gleiches Dateisystem für atomare Ersetzung).
2. Flush aller Puffer plus Sync der Dateiinhalte.
3. Vollständige Validierung der gerade geschriebenen Datei (Größenlimit,
   Framing, beide Hashanker, Grenzwerte, Referenzen) **vor** jeder
   Ersetzung.
4. Atomare Ersetzung des Zielslots per Umbenennungsprimitiv (POSIX
   `rename`). Ein Sync des Verzeichniseintrags wird bewusst **nicht**
   durchgeführt: Die BCL besitzt dafür kein Primitive (FileStream darf
   Verzeichnisse nicht öffnen), und der akzeptierte Architekturvertrag
   hält Native-Imports ausschließlich in der Plattformschicht
   (`T-010`-Architekturtest). Restrisiko: Geht das Gerät unmittelbar im
   Ersetzungsaugenblick verloren, ist der Verlust des Umbenennens selbst
   nicht auszuschließen; der alte oder der neue Stand bleibt dabei stets
   vollständig und gültig (Umbenennungsatomarität). Rückrollweg: Sobald
   die Plattformschicht einen sanktionierten Sync-Dienst anbietet, wird
   Schritt 4 um diesen Sync ergänzt und der Vertrag versioniert; bis dahin
   behauptet ein grünes Ergebnis genau die Schritte 1 bis 4 und nichts
   zusätzlich.
5. Laden immer in einen getrennten Zustandscontainer; erst nach vollständiger
   Validierung darf der Aufrufer aktivieren. Ein fehlgeschlagener Schritt
   löscht die temporäre Datei und lässt den letzten gültigen Stand unangetastet.
6. Schreibvorgänge erfolgen ausschließlich unterhalb eines konfigurierten
   Erlaubnisverzeichnisses; Symlink-Komponenten und Pfadaustritte werden
   kontrolliert abgewiesen.

**Alternativen:**

| Alternative | Ablehnungsgrund |
|---|---|
| Direktes Überschreiben des Slots | Jeder Zwischenabbruch hinterlässt einen teilgeschriebenen gültig aussehenden Stand; kosmetisch grüne Ergebnisse wären strukturell möglich (AC-T031-05 verbietet das). |
| Backup-Kopie statt Rename | Zwei Dateien verdoppeln die Abbruchflächen und verschieben das Problem nur; Rename ist auf POSIX das atomare Primitiv. |

**Rückrollweg:** Durabilitymechanik (Sync-Reihenfolge, Tempbenennung) ist
austauschbar, solange die AC-T031-05-Garantien unangetastet bleiben; Änderungen
werden mit Testnachweis dokumentiert.

## 6. Größen-Sanity-Schwellwert

**Ableitungsmethode (bindend):** Jeder `savecheck`-Lauf misst die
Snapshotgröße am sicheren Standardtick in **mindestens zwei unabhängigen
Kalibrierläufen** innerhalb desselben Laufs und leitet den Schwellwert zur
Laufzeit als Vielfaches dieser Messung ab; weichen die Kalibrierläufe ab,
bricht der Lauf fail-closed ab, statt einen erfundenen Wert zu verwenden.
Die konkrete Kalibrierbasis der Abnahmeläufe ist Lauf-Evidenz und wird im
Abnahmedokument mit Pfad, Bytes und Reportverweis festgehalten; sie ist
keine Vertragskonstante.

**Wahl:** Schwellwert = gemessene Größe × Faktor **4** innerhalb des
Auftragbands 2× bis 16× (`SizeSanityFactor = 4`,
`SizeSanityFactorMinimum = 2`, `SizeSanityFactorMaximum = 16`); Überschreiten
des Schwellwerts ist eine definierte Gateverletzung. Zusätzlich gilt ein
absolutes Vorab-Limit von 64 MiB als DoS-Grenze, bevor Speicher zugewiesen
wird. Dauern gehen zu keinem Zeitpunkt in Gateentscheidungen ein; allein
dieser aus den Kalibrierläufen abgeleitete Größenschwellwert entscheidet
fail-closed.

**Ableitung des Faktors:** Der Faktor 4 liegt in der Bandmitte und deckt die
Varianz der Wegpunktschwänze zwischen Plänen deutlich ab, ohne schwach zu
sein: 2× ließe legitime Wegpunktvarianz fremder Seeds nahe an der Grenze,
16× erschöpft nur das Bandmaximum ohne Erkennungsgewinn.

**Alternativen:** Fixer absoluter Wert statt Vielfaches — abgelehnt, weil der
Auftrag die Ableitung aus mindestens zwei Kalibrierläufen als Vielfaches im
Band fordert. **Rückrollweg:** Faktoränderungen innerhalb des Bands sind
Verschärfungen/Verschiebungen mit neuer Vertragsversion; jede Lockerung
gegenüber dem abgeleiteten Wert oder Verlassen des Bands eskaliert an die
Projektleitung.

## 7. Sichere Ticks und Fortsetzungshorizont

**Wahl:** Sicherer Tick ist ein Tick, dessen Snapshot nach Abschluss von
`Tick()` außerhalb des Heisspfads als Kopie entsteht; der Standardlauf nimmt
den mittleren Planhorizonttick (Standard: Tick 1800 bei Horizont 3600).
Fortsetzungshorizont ist die Resttickanzahl bis zum Planende und beträgt
mindestens **die Hälfte des Planhorizonts**
(`MinContinuationFractionNumerator = 1`, `MinContinuationFractionDenominator = 2`);
der Standardlauf erfüllt ihn exakt, Diagnoseflags dürfen erhöhen. Kettenstichproben
alle 300 Ticks; verglichen werden sämtliche Stichproben nach dem sicheren
Tick sowie das Kettenende byteidentisch gegen einen unterbrochenen
Referenzlauf desselben Plans.

**Alternativen:** Fortsetzung über weniger als die Hälfte — abgelehnt, weil
der Nachweis „sämtliche simrelevanten Zustandsanteile erfasst“ sonst zu
früh enden würde; vollständige Verdopplung des Horizonts — unnötiger
Laufzeitkosten ohne zusätzliche Klasse. **Rückrollweg:** Horizont- und
Stichprobenparameter sind Laufparameter; die Mindestfraction ändert nur per
neuer Vertragsversion.

## 8. Migrationsregel

**Wahl:** `saveSchemaVersion` ist streng monoton. Das Produktformat kennt
genau Version 1; unbekannte frühere und zukünftige Versionen werden
kontrolliert mit definierter Verletzungsklasse abgelehnt, ohne eine Migration
zu erfinden oder still zu verwerfen. Migrationsschritte laufen ausschließlich
auf Kopien, schrittweise, validieren nach jedem Schritt und sind idempotent;
ein Migrationsfehler erhält den Originalstand. Das Produkt registriert
derzeit **keinen** Migrationsschritt; die Idempotenz wird an einem rein
internen synthetischen Zwei-Version-Fixturepaar nachgewiesen, das als
interne Testinfrastruktur gekennzeichnet ist und keinerlei
Produktmigrations- oder Altdatenzusagen begründet. Es gibt keine zu
übernehmende Altdatenquelle (DATENMODELL).

**Alternative:** Tolerantes Lesen naher Versionen — abgelehnt als stille
Formatdrift. **Rückrollweg:** Echte Migrationen entstehen später als
registrierte, getestete Schritte mit eigener Fixturepaaren; dieser Vertrag
nimmt ihnen nichts vorweg.

## 9. Zustandszugriff des Savekerns (Strukturentscheidung)

**Wahl:** Der Savekern liest und schreibt den Relevantzustand über
Kompilierungszeit-Accessoren (`System.Runtime.CompilerServices.UnsafeAccessor`)
auf die privaten SoA-Felder von `Riftward.Simulation.SimWorld`.
Damit bleibt `Riftward.Simulation` byteidentisch unverändert (Auftrag),
die öffentliche Fläche des Simulationskerns bleibt unberührt (Non-Scope),
der Ladepfad bleibt reflectionsfrei und AOT-tauglich (UnsafeAccessor ist
eine Kompilierungszeitbindung ohne `System.Reflection`-API und ohne dynamische
Codegenerierung), und es entsteht keine neue Abhängigkeit. Fail-closed:
Nach jeder Wiederherstellung prüft der Loader den Zustandshash der
rekonstruierten Welt gegen den Kopfanker; jede Bindungsabweichung
(Feldumbenennung, Layoutänderung) wird damit kontrolliert abgewiesen statt
still falsch fortzusetzen. Ein Architekturtest bindet die Accessorennamen an
die Simulationsquellen.

**Alternativen:**

| Alternative | Ablehnungsgrund |
|---|---|
| Reflection (`FieldInfo`) | Im Runtime-Ladepfad laut Auftrag eskalationspflichtig; Native-AOT-feindlich. |
| Öffentliche Wiederherstellungs-API in Riftward.Simulation | Ändert die öffentliche Fläche des Simulationskerns und ist laut Auftrag Non-Scope; der Blobvergleich müsste scheitern. |
| Präfix-Replay statt Laden | Bewiese nichts über die Payloadvollständigkeit und umging AC-T031-04; als Fortsetzungsmechanismus unzulässig. |
| Eigener Simulationsklon im Savekern | Verletzte die unveränderte Wiederverwendung von Riftward.Simulation; der Report bindet dessen Hashkette. |

**Rückrollweg:** Bietet der Simulationskern künftig eine offizielle
Persistenzfläche, werden die Accessoren durch sie ersetzt (Ein-Datei-Änderung
plus Testnachweis); der Vertrag ändert seine Formate dadurch nicht.

## 10. Befehls- und Exitcodevertrag

**Wahl:** Der öffentliche Befehl ist
`./scripts/rift.sh savecheck --report PFAD [--work VERZ] [--seed N]
[--plan-ticks N] [--safe-tick N] [--sample-interval-ticks N] [--lock DATEI]`.
Der Fortsetzungshorizont ergibt sich vertraglich als Resthorizont
`planTicks − safeTick` (Abschnitt 7) und ist kein eigenes Flag. Er läuft headless nativ auf linux-x64 rein CPU-seitig ohne Fenster, Renderer, native SDL3-/bgfx-Artefakte
und Netzwerk. Bestehende Exitcodes bleiben unverändert; neu sind:

| Code | Bedeutung |
|---|---|
| 33 | Save-Gate verletzt (Prüfklassenmatrix); Report wurde dennoch geschrieben und klar als nicht bestanden markiert |
| 34 | Savecheck unvollständig oder vorzeitig beendet; der Teilreport gilt ausdrücklich nicht als Evidenz |

Schemawidersprüche nutzen weiterhin Code 27, nicht schreibbare Reportpfade
Code 28, Usagefehler Code 2; ein fehlender App-Build bricht im Shell-Wrapper
mit dem bestehenden Buildguard ab.

**Alternative:** Wiederverwendung der Bench-Codes 26/31 — abgelehnt, weil
save-spezifische Verletzungsklassen eigene, dokumentierte Codes erhalten
sollen und bestehende Bedeutungen unverändert bleiben müssen.
**Rückrollweg:** Codes sind Teil des öffentlichen Vertrags und folgen dem
Registryerweiterungsmuster; Änderungen benötigen eine dokumentierte Entscheidung
und Testanpassung.

## 11. Verletzungsklassen (unterscheidbare kontrollierte Ablehnung)

Bindende Klassencodes des Loaders; jeder Save wird höchstens einer Klasse
zugeordnet (Prüfreihenfolge Abschnitt 2):

| Klasse | Auslöser |
|---|---|
| `MAGIC_INVALID` | Datei beginnt nicht mit `RWSD` |
| `SCHEMA_VERSION_UNSUPPORTED` | Schemaversion ≠ 1 (unbekannt/zukünftig) |
| `TRUNCATED_FILE` | Datei kürzer als die deklarierte Rahmenstruktur |
| `SIZE_LIMIT_EXCEEDED` | Deklarierte oder tatsächliche Größe oberhalb Sanity-/Absolute-Limits |
| `META_INTEGRITY_VIOLATION` | Kopfbytes (einschließlich payloadHash-Feld) passen nicht zu metaHash |
| `PAYLOAD_INTEGRITY_VIOLATION` | Payloadbytes passen nicht zu payloadHash (umfasst Bitfehler im Payload) |
| `CANONICAL_VIOLATION` | Verletzte feste Ordnung/Framing: Überhangbytes, fehlsitzender Abschnitt, Re-Encoding-Ungleichheit, falsche Bytelängen |
| `LIMIT_VIOLATION` | Grenzwertverletzung (Bereiche von Tick, Zonen, Gruppen, Pfadstatus, Cursor/Anzahl, Positionen) |
| `REFERENCE_INVALID` | Fehlende/beschädigte Referenz (begehbare Agentenpositionen und begehbare Wegpunktkacheln; die Zielkachel darf vertraglich eine unpassierbare Zellmitte zeigen, wenn das Ziel als unerreichbar gemeldet wurde) |

Die Datensatzliste „finalitätsnah gültig“ aus DATENMODELL ist vor
T-030-/T-051-Inhalt nicht darstellbar; sie bleibt ausdrücklich der
Contentstufe vorbehalten (dokumentierte Zurückstellung, keine Abschwächung)
und wird bei Aufkommen des Inhalts nachgeholt.

**Maschinenlesbare Aussagen des Reports** (Spiegel in
`src/Riftward.Save/SaveContract.cs`, von einem Test gegen dieses Dokument
gehalten):

- `cooked-package-definition-and-replay-formats-remain-open-qtec006-not-decided-in-this-task`
- `f005-partial-sim-state-envelope-only-full-worldstate-payload-deferred-to-t030-t051-content`
- `datenmodell-fixture-class-finality-valid-deferred-to-content-stage-documented-postponement-no-weakening`

## 12. Geltungsbereich

Dieser Vertrag beschreibt ausschließlich den versionierten, atomaren
Save/Lade-Pfad des unveränderten Simulationskerns und den `savecheck`-Nachweis.
Er begründet kein Cooked-Paket-, Definitions- oder Replaydateiformat, keine
UI-, Slotanzahl-, Vorschaubild- oder Einstellungspersistenz (F-006/T-041),
keine Cross-Build-/Cross-Plattform-Save-Kompatibilität (entspricht
Hashklasse K3: nicht behauptet), keine Signatur-/Anti-Cheat-Zusagen und keine
fachlichen Spielregeln. F-005 bleibt anteilig offen, solange die
vollständige WorldState-/MissionState-Payload des Vertical Slice nicht
existiert (T-030/T-051).

## 13. Versionierte Erweiterung V2 (T-037): additive Sitzungssektion

**Status:** Durch den gatenden Abschnitt-0-Spike des Auftrags
`.ai/tasks/T-037-graybox-continuation-restart.json` vor der Implementierung
festgelegt. Diese Erweiterung macht den letzten benannten Muss-Element-Block
des Alpha-Loops des Release-Modus („Save/Load überlebt einen
Prozessneustart“, UF-001 Schritt 9 und UF-002) über der abgenommenen
T-032-/T-033-Laufzeitlinie prüfbar. Sie antwortet auf keine offene
Produktfrage: Q-GAM-001 bis Q-GAM-007, Q-GAM-010 (Produkt-Persistenzwahrheit
des Modusflags in Save/Replay bleibt als Detail der finalen Wechsel-Detailregel
OFFEN — hier wird ausschließlich die dokumentierte Graybox-Kettenwahrheit
persistiert), Q-NAR-002, Q-NAR-004 und Q-TEC-006 bleiben `OFFEN`. Kein
Budgetwert wird geändert; Pflichtprofile bleiben `NOT-MEASURED`
(Q-OPS-001). Die Version 1 dieses Vertrags bleibt als historischer
Bestandsvertrag unverändert; die Abschnitte 1 bis 12 gelten zeichentreu
weiter, soweit Abschnitt 13 nichts Additives ergänzt.

### 13.1 Sektionsaufbau (`session-section-full-state-v1`)

**Wahl:** Der vollständige Sitzungszustand an einer Vorgrenze wird als **eine
versionierte, eigen-hashgebundene additive Sektion** im bestehenden atomaren
Umschlag gespeichert. Inhalt: aktiver Sitzungsmodus samt schwebender
Moduswechsel (Pending-Switches des Modevertrags), Aufsuchprotokoll samt
Erkundungsfortschritt/-abschluss (T-034), Entscheidungsangebot/Wahl/Folgen-
zustand samt Zykluszurücksetzungsstand und Sitzungsabweisungszählern (T-035,
V2), Druckfensterinstanzen samt Zykluszustand, letztem Fehlschlag,
Wiederauffrischungsgrenze und Offenzustand (T-036). Die Sektion trägt eine
eigene Sektionsversion (`u16`, `sessionSectionVersion = 1`, unabhängig von
`saveSchemaVersion`), wird von einem eigenen SHA-256-Anker
(`sessionSectionHash`) über exakt die Sektionsbytes gedeckt und lebt im
Dokument V2 nach dem Payload. Die strikte Einpassvalidierung der Sektion
erfolgt vollständig vor Aktivierung innerhalb des unveränderten
Atomarprotokolls (Abschnitt 5): Framing und Größenlimits → metaHash →
payloadHash → kanonische Payload-Dekodierung mit Re-Encoding-Gleichheit →
Grenzwerte → Referenzen → Sektionsframing mit exakter Gesamtlänge →
`sessionSectionHash` → kanonische Sektionsdekodierung mit exaktem
Byteverbrauch und Re-Encoding-Gleichheit → Sektionsgrenzwerte →
Sektionsreferenzen. Jede Verletzung erhält eine unterscheidbare Klasse
(Abschnitt 13.5).

**Alternativen:**

| Alternative | Bewertung |
|---|---|
| **A: vollständiger Sitzungszustand als eine versionierte, eigen-hashgebundene additive Sektion (Empfehlung)** | Eine Sektion, ein Anker, eine Versionierung: Die Kettenwahrheit ist ohne Wiederaufbau vollständig, die T-031-Prüfklassen gelten uneingeschränkt für die Sektion, und V1-Dokumente bleiben ohne Feldberührung lesbar. Nachteil: der Umschlag V2 trägt zwei Sektionslängen (Payload und Sitzung) — Framingfläche, die vollständig getestet wird. |
| B: nur Minimal-/Teilzustand (etwa nur Modus und Zähler) | Kleinerer Sektionskopf, aber die Kette wäre nach dem Laden nicht wahrheitsgetreu fortsetzbar: Aufsuchprotokoll, Angebots-/Wahl-/Folgenzustand und Fensterinstanzen wären verloren — der Auftrag verlangt genau die vollständige Kettenwahrheit. |
| C: Ereignisprotokoll-Wiederaufbau (Intents seit Sitzungsbeginn erneut ausführen) | Wäre ein Präfix-Replay und umginge die Persistenzbehauptung (T-031-Präzedenz Abschnitt 9: „Bewiese nichts über die Persistenzvollständigkeit“); über der Prozessgrenze zudem kein deterministischer Wiederaufbau der Beobachtungsgrenzen. |

**Ausdrücklich verworfen:** jede Umdeutung bestehender Felder (stille
Formatdrift — nur Vertragsversion); jede Aufnahme der darstellseitigen
Auswahl in die Sektion (Auswahl bleibt rein darstellseitiger Zustand gemäß
Kommandovertrag Abschnitt 3; die Fortsetzungsskripte reichen ihre Auswahl
nach der Ladegrenze selbst wieder auf, was die Kettenfortsetzung nicht
berührt, weil der Simulationszahlpfad ausschließlich von Kernbefehlen abhängt);
jede Aufnahme von Pipeline-Diagnosezählern (kein Kettenzustand).

**Playtestkriterium:** Ein gespeicherter und ladbarer Zustand ist binnen
2 Sekunden erkennbar; nach dem Laden stimmen Modus, Erkundungs-,
Entscheidungs- und Druckwahrheit in beiden Modi mit dem Zustand vor dem
Prozessende überein. **Rückrollweg:** Rückkehr zum Vertragsstand V1 durch
Weglassen der Sektionserzeugung (Bestandsverhalten byteidentisch); eine
Sektionsänderung erfordert eine neue Sektionsversion mit
Fixture-Regeneration.

### 13.2 Headless Aktivierungsform (`opt-in-continuation-flags-v2`)

**Wahl:** Befehlsflags am bestehenden öffentlichen Befehl `kommandoschleife`
im savecheck-Präzedenzmuster:

```bash
./scripts/rift.sh kommandoschleife --scenario kommando-graybox \
  --input-script PFAD --seed N --report PFAD \
  --slot-dir VERZ --slot NAME --save-at-tick N      # Speicherlauf
./scripts/rift.sh kommandoschleife --scenario kommando-graybox \
  --input-script PFAD --seed N --report PFAD \
  --slot-dir VERZ --slot NAME --load-slot           # Fortsetzungslauf
```

Der Speicherlauf spielt die unveränderte Skriptgrammatik
(`graybox-input-script-v1/v2/v3` byteidentisch) bis zur dokumentierten
Speichervorgrenze `--save-at-tick` (Vorgrenze `T`: Zustand nach Abschluss
des Ticks `T − 1`, `TickIndex == T`), schreibt Simulation plus Sitzungsschicht
in den Slot und endet. Der Fortsetzungslauf ist ein frischer Prozess: Er lädt
den Slot, validiert ihn vollständig vor Aktivierung (Prüfreihenfolge
Abschnitt 13.1), stellt Welt und Sitzungsschicht wieder her und setzt
dieselbe Skriptausführung ab der Vorgrenze `T` fort; Skriptintents vor `T`
sind im Speicherlauf verbraucht und werden im Fortsetzungslauf übersprungen
(keine erneute Anwendung). Fortsetzungshorizont ist der bestehende
`--horizon-ticks`. Die Aktivierungsform folgt dem savecheck-Präzedenzmuster
(Slotpfad über `--slot-dir VERZ --slot NAME`), Speichergrenze über
`--save-at-tick N` innerhalb des Messfensters
(`[warmupTicks + 1, horizonTicks)`), Fortsetzungshorizont über den
bestehenden Flag.

**Alternativen:**

| Alternative | Bewertung |
|---|---|
| **A: Befehlsflags am bestehenden kommandoschleife-Befehl (Empfehlung)** | Derselbe öffentliche Befehl und derselbe Pipelinepfad bleiben vertraglich (T-034-/T-035-/T-036-Präzedenz); keine neue Befehlsfläche. |
| B: Skriptgrammatik-Erweiterung (`graybox-input-script-v4` mit Save/Lade-Aktionen) | Verbreitert die Diagnosegrammatik um Persistenzverben, die kein Sitzungsintent sind, und erzwingt eine vierte Grammatikstufe mit vollständiger Fixturefläche — abgelehnt als dokumentierte Alternative. |
| Verworfen: separates Untercommand | Widerspricht dem Auftrag: derselbe öffentliche Befehl und derselbe Pipelinepfad (T-034- bis T-036-Präzedenz). |

**Playtestkriterium:** Zwei unabhängige Fresh-Prozesspaare (Speicherlauf +
Fortsetzungslauf) sind builderidentisch; die Fortsetzungskette ist ab der
Ladegrenze byteidentisch zur unterbrochenen Referenz. **Rückrollweg:** Flags
und Reportblock entfernen; ohne Flags bleibt der Bestandsstand byteidentisch.

### 13.3 Interaktive Aktivierungsform (`opt-in-interactive-slot-actions-v2`)

**Wahl:** Genau zwei frei belegbare Keymap-Aktionen `save-slot` und
`load-slot` in der bestehenden Familie gemäß Kommandovertrag Abschnitt 9 und
NF-005. Standardbelegung: `save-slot` = F5 (Scancode 62), `load-slot` = F9
(Scancode 66) — beide im Bestandsstand unbesetzt. Die Validierungsregeln des
Abschnitts 9 (mindestens eine Bindung je Aktion, keine Doppelbindungen, keine
unbekannten Namen) gelten unverändert; die Maussemantik bleibt unverändert
umbelegbar-nie. Die Aktionen sind nur nutzbar, wenn der Lauf über
`--slot-dir VERZ` ein Erlaubnisverzeichnis besitzt; ohne konfiguriertes
Verzeichnis erhält der Impuls eine kontrollierte, unterscheidbare Ablehnung
(`slot-directory-not-configured`) ohne Welt-, Ketten- oder Kernänderung. Das
Speichern erfasst den Zustand an der laufenden Vorgrenze (`TickIndex` nach
dem letzten abgeschlossenen Tick) und schreibt atomar in den Slot
`slot-interactive.rwsaved`; das Laden validiert vollständig vor Aktivierung
und ersetzt Welt, Sitzungsschicht und Pipeline kontrolliert — ein Laden mit
unpassendem, inkompatiblem oder korruptem Slot ergibt eine kontrollierte,
unterscheidbare Ablehnung mit maschinenlesbarer Kennung (UF-001-Fehlerzeile)
ohne Welt-, Ketten- oder Kernänderung. Nach dem Laden weist der Titel-HUD-
Ausweis die wiederhergestellte Kettenwahrheit in beiden Modi ohne Tastendruck
aus (bestehendes Titel-HUD-Muster, NF-005, nie reine Farbcodierung).

**Alternativen:**

| Alternative | Bewertung |
|---|---|
| **A: genau zwei frei belegbare Keymap-Aktionen in der bestehenden Familie (Empfehlung)** | Konsistent mit `mode-switch` und `choose-a`/`choose-b`; der Spieler behält die Kontrolle über den Zeitpunkt; keine neue Renderfläche. |
| B: ein kontextgebundener Einzelbefehl mit folgender Zielfrage | Spart eine Belegung, aber die Nachfrage bräuchte eine zweite Eingabephase mit eigenem Zustandsautomaten und unverzüglicher Sichtbarkeit — für diesen Slice unangemessene Komplexität. |
| Verworfen: Text-, Menü- oder Dialogfläche als neue Renderfläche | Späterer Slice nach Modevertrag-Abschnitt-8-Präzedenz; keine Schrift-/UI-Renderfläche in diesem Auftrag. |

**Playtestkriterien:** gespeicherter und ladbarer Zustand sind binnen
2 Sekunden erkennbar; die restaurierte Kettenwahrheit stimmt in beiden Modi
mit dem Zustand vor dem Speichern überein; eine abgelehnte Ladung verändert
sichtbar nichts (Missverständnisrate < 10 %). **Rückrollweg:** Belegung und
Aktionen sind Hypothesenkonstanten; Austausch ohne Kernänderung, solange die
Zweikanal-Erkennbarkeit erhalten bleibt.

### 13.4 Modulgrenze des Sektionscodecs (`session-section-codec-boundary-v2`)

**Wahl:** Additive Codecfläche neben der bestehenden Saveverträglichkeit,
gespeist aus einer kanonischen Sitzungsserialisierung der Sitzungsschicht:
Die kanonische Sektionsstruktur (`SessionSectionState`, feste Feldordnung,
Little-Endian-Festbreiten, Sentinel-Regeln wie der Savekern, kein
Fließkommaanteil, BCL-only, keine Reflection) lebt im Saveprojekt neben
`CanonicalSaveCodec`; die Sitzungsschicht (`Riftward.Session`) liefert die
Erfassung aus ihren vier Schichten und die Wiederherstellung in sie und
referenziert dafür das Saveprojekt (Laufrichtung Gameplay → Save-System
gemäß `docs/ARCHITEKTUR.md`). Der Loader in `Riftward.Save` validiert die
Sektion vollständig (Hash, exakter Byteverbrauch, Re-Encoding-Gleichheit,
Grenzwerte, Referenzen), bevor irgendein Aufrufer sie sehen kann — die
T-031-Prüfklassen gelten uneingeschränkt für die neue Sektion. Die
Sitzungsschicht bleibt frei von SDL3-, bgfx- und Betriebssystemtypen;
Runtime-Hotpaths liegen in C#; F# und Python sind vom Laufzeitpfad
ferngehalten.

**Alternative:** opake versionierte Bytesektion mit eigener Prüfsumme
(Dekodierung erst beim Aktivierer) — abgelehnt, weil die T-031-Prüfklassen
(kanonische Dekodierung, Re-Encoding-Gleichheit, Grenzwerte, Referenzen)
vertraglich uneingeschränkt für die Sektion gelten müssen und ein opaker
Pfad die vollständige Validierung vor Aktivierung nicht im Loader bündelt.

**Rückrollweg:** Die Sektion ist eine additive Fläche; ein Rückbau entfernt
Sektionscodec, Aktivierungsflags und Reportblock, ohne den Simulationskern,
die Bestandsverträge oder die T-031-Garantien zu berühren.

### 13.5 Umschlag V2, V1-Kompatibilität und Verletzungsklassen

**Umschlag V2 (`save-schema-version-2`):** `saveSchemaVersion` wird 2; das
Produktformat kennt genau die Versionen 1 und 2. Das Dokument V2 erweitert
den Kopf rein additiv um `sessionSectionLength` (`u64`) und
`sessionSectionHash` (SHA-256 über exakt die Sektionsbytes) am Kopfende;
`metaHash` deckt den gesamten erweiterten Kopf einschließlich beider neuen
Felder nach derselben Regel wie V1 (SHA-256 über Magic, Schemaversion,
Kopflänge und alle Kopfbytes). Die Rahmenstruktur ist
`Vorspann | Kopf | metaHash | Payload | Sitzungssektion`; die Gesamtlänge
ist vollständig aus dem Kopf ableitbar, Überhangbytes sind vertragswidrig.
Die Sektion besitzt ein absolutes Vorab-Limit
(`MaxSessionSectionBytes = 1 MiB`) als DoS-Grenze vor Zuweisung.

**V1-Kompatibilität (`legacy-v1-session-emptiness-v2`):** Slots der Version 1
laden unverändert mit ehrlicher, maschinenlesbarer Sitzungsleere und
unveränderter Kette
(`legacy-v1-slot-loads-with-honest-machine-readable-session-emptiness-and-unchanged-chain`)
— ohne Migrationserfindung, ohne Feldumdeutung. Der
Loader kennzeichnet den Ursprung (`FromLegacyV1Document`); der Report weist
die ehrliche Leere maschinenlesbar aus. Streng monoton: Version 0 und alle
Versionen ab 3 bleiben kontrolliert mit definierter Klasse abgewiesen. Der
Migrationsvertrag Abschnitt 8 gilt fort: Es wird kein Migrationsschritt
erfunden; die unterstützte Legacy-Version 1 ist eine identische No-op-
Erreichbarkeit (byteidentisch, null Schritte), frühere und zukünftige
Versionen werden abgewiesen. Das Bestandskorruptions-Fixture
`unknown-schema-version` zielt weiter auf die erste nicht unterstützte
zukünftige Version (nun 3); seine Erwartungsklasse ist unverändert.

**Neue unterscheidbare Verletzungsklassen der Sektion:**

| Klasse | Auslöser |
|---|---|
| `SESSION_SECTION_INTEGRITY_VIOLATION` | Sektionsbytes passen nicht zum `sessionSectionHash`-Anker (umfasst Bitfehler in der Sektion) |
| `SESSION_SECTION_INVALID` | Verletzte feste Ordnung/Framing/Grenzen/Referenzen der Sektion: abgeschnittene Sektion jenseits der Rahmenprüfung, Überhang, fehlsitzender Abschnitt, Re-Encoding-Ungleichheit, unbekannte Sektionsversion, unbekannte Aufzählungswerte, verletzte Zonenzuordnung, verletzte Relationswahrheiten (Besuchszonen eindeutig und ausschließlich persönlich, Entscheidungs-/Folgen-/Ankunftskonsistenz, Fenster-/Zykluskonsistenz, Fehlschlag-/Wiederauffrischungsrelation) |

Rahmenniveau-Fehler (Datei kürzer/länger als die deklarierte Gesamtlänge)
behalten die Bestandsklassen `TRUNCATED_FILE`/`CANONICAL_VIOLATION`;
Größenüberschreitung der Sektion ist `SIZE_LIMIT_EXCEEDED`. Die
Prüfreihenfolge von Abschnitt 2 gilt unverändert und ordnet jede Datei
höchstens einer Klasse zu.

**Aktivierungsgrenzen (`untrusted-slot-activation-guards-v2`):** Slotdateien
gelten uneingeschränkt als untrusted. Vor der Aktivierung prüft der
Aktivierer neben der vollständigen Dokumentvalidierung die Passung an den
Laufkontext mit unterscheidbaren, maschinenlesbaren Ablehnungskennungen:
`foreign-world-id` (Weltkennung des Slots widerspricht der Vertragswelt),
`foreign-seed` (Seed des Slots widerspricht dem angeforderten Laufseed),
`layer-activation-mismatch` (die Schichtaktivierung des Fortsetzungslaufs
widerspricht der Sitzungssektion des Slots),
`later-schema-version`/`unsupported-schema-version` (aus der Klasse
`SCHEMA_VERSION_UNSUPPORTED`), Sektionsklassen gemäß Abschnitt 13.5. Eine
abgewiesene Ladung ändert Welt, Kette, Kern oder letzten gültigen Stand nie.

### 13.6 Persistenz-Präzisierungen der vier Sitzungsverträge und Replay-Ausnahme

Die versionierten Nichtpersistenzaussagen der vier Sitzungsverträge
(`session-local-not-persisted-v1` des Erkundungsvertrags,
`decision-session-local-not-persisted-v1` des Entscheidungsvertrags,
`pressure-session-local-not-persisted-v1` des Druckvertrags sowie die
Persistenzvorbehaltszeile des Modevertrags Abschnitt 1) werden durch diese
autorisierte additive Erweiterung zu versionierten Save/Load-Persistenz-
aussagen präzisiert
(`session-section-persisted-in-save-load-with-explicit-replay-exception-t037`): Der Sitzungszustand der vier Schichten ist ab dieser
Vertragsversion über die additive Sektion in Save/Load fortsetzbar
(`persisted=true`, `saveLoad=continued`); die **ausdrückliche
Replay-Ausnahme** bleibt bestehen: Replay und Soak setzen den Sitzungszustand
nicht fort (`replay=not-continued`), und der Replayanteil von ADR 008
Kernaussage 4 bleibt Produktendzustand hinter Q-GAM-010 und Q-TEC-006. Jede
der vier Präzisierungen ist als versionierter Zusatzabschnitt des jeweiligen
Vertrags dokumentiert (Modevertrag V2, Erkundungsvertrag V2,
Entscheidungsvertrag V3, Druckvertrag V2); die Tests, die die v1-
Nichtpersistenz gebunden haben, wurden im selben Kandidaten auf die
Präzisierung fortgeschrieben (Fixture-Regeneration nach T-035-V2-Präzedenz).
Die Kettenwahrheit von Simulation und Hash bleibt unverändert: kein
Sektionsbyte berührt Simulationszustand oder Hash, und ein Zwilling ohne
Aktivierung bleibt byteidentisch.

### 13.7 Vorregistriertes Playtestprotokoll (T-037)

Vollständiges Protokoll einer Displaysession (Entwickler-PC, gegebenenfalls
virtuelles Wayland nach T-023-Präzedenz), vor der Implementierung
registriert:

1. **Speichererkennbarkeit:** Speicheraktion (Taste F5 bzw. belegte
   Aktion); gespeicherter und ladbarer Zustand sind binnen 2 Sekunden
   erkennbar (kontrollierte Bestätigung am Live-Pfad, kein Menü).
2. **Fortsetzungswahrheit:** Nach Laden (Taste F9) stimmen Modus,
   Erkundungs-, Entscheidungs- und Druckwahrheit in beiden Modi mit dem
   Zustand vor dem Prozessende überein; der Titel-HUD-Ausweis zeigt sie
   ohne Tastendruck.
3. **Ablehnung:** Ein Laden mit unpassendem, inkompatiblem oder korruptem
   Slot erhält eine kontrollierte, unterscheidbare Ablehnung mit
   maschinenlesbarer Kennung; Welt, Kette und Kern bleiben unverändert.
4. **Beobachtungstreue:** Strategische und persönliche Bedienung bleiben
   unverändert; kein Befehlspuls und keine Weltänderung geht aus dem reinen
   Speichern/Laden hervor.

Ausführung: dokumentiert im Abnahmelauf; ist kein Display verfügbar, bleiben
Interaktivsmoke und Playtestausführung ausgewiesene Restpunkte mit
kontrolliertem Code-19-Nachweis ohne Simulation (Präzedenz
T-023/T-032/T-033/T-034/T-035/T-036).

### 13.8 Exitcodes und Reportlinie

Es entstehen **keine neuen Exitcodebedeutungen**. Bestehende Bedeutungen
(insbesondere 19, 27, 28, 33–38 und 2/4) bleiben unverändert: Ein
Speicherlauf, dessen Slot-Schreibvorgang fehlschlägt, ist ein unvollständiger
Lauf (Code 36, Teilreport ist keine Evidenz); ein Fortsetzungslauf mit
abgewiesener Ladung endet kontrolliert unvollständig (Code 36) mit
maschinenlesbarer Ablehnungskennung im Teilreport; Gateverletzungen bleiben
Code 35. Die Reportlinie wird rein additiv auf **Schemaversion 6** erhöht
(Pflichtblock `continuation`; die Sitzungsblöcke der Schichten erscheinen
genau dann, wenn ihre Aktivierung vertraglich besteht — Schemaversion 6
erzwingt keine Schichtaktivierung und keine Schichtblockpflicht); alle neuen
Felder tragen `gateCoupled=false`, und die Kettenfortsetzungswahrheit bindet
der Bestandskriterium-5-Ausweis (`gate.stateChainSelfConsistency`) im
Fortsetzungslauf fail-closed gegen die unterbrochene In-Prozess-Referenz
(neue Felder selbst ohne Gatekopplung). Läufe ohne Sitzungsaktivierung
bleiben bei der Bestandsschemaversion 2 byteidentisch; die
Persistenzpräzisierung der vier Sitzungsverträge (Abschnitt 13.6) ändert
ausschließlich die Werte der bestehenden persistence-Blöcke der
Schichtläufe (Schemaversionen 3/4/5) mit Fixture-Regeneration, nicht ihre
Struktur und nicht die Schemaversionen.

## 14. Geltungsbereich V2

Die Erweiterung dieses Abschnitts gilt ausschließlich für die additive
Sitzungssektion, ihre Aktivierungsformen und die Persistenzpräzisierung der
vier Sitzungsverträge. Sie begründet kein Cooked-Paket-, Definitions- oder
Replaydateiformat (Q-TEC-006 bleibt `OFFEN`; Cross-Plattform-Hashzusagen
bleiben unbehauptet, K3), keine UI-/Menüflächen, keine Out-of-Session-
Neustartsemantik (Hauptmenü, Neues Spiel, Weltneuaufbau), keine
Änderung an `Riftward.Simulation`, seinem Vertrag oder seiner
Byteidentität, und keine Antwort auf eine offene Produktfrage. `DATENMODELL.md`
bleibt byteidentisch, weil Umschlag und Atomarprotokoll unverändert bleiben
und die Sektion im vertraglich versionierten Payloadumfang als additive
Sektion lebt.

## 15. Autorisierte additive Missions-Sektionsfläche (V3, T-039)

**Status:** Autorisierte additive Erweiterung gemäß dem gatenden Abschnitt 0
des Auftrags `.ai/tasks/T-039-graybox-completion-repeat.json` und dem
Abschlussvertrag V1 (`docs/ABSCHLUSSVERTRAG.md`, Abschnitt 5,
`mission-chain-run-counter-persisted-v1`). Sie antwortet auf keine offene
Produktfrage; Q-TEC-006 bleibt `OFFEN`; kein Budgetwert wird geändert; die
Abschnitte 1 bis 14 gelten zeichentreu weiter, soweit dieser Abschnitt
nichts Additives ergänzt.

### 15.1 Additive Sektionsfelder (`mission-chain-run-section-fields-v3`)

**Wahl:** Die kanonische Sitzungssektion erhält als **Sektionsversion 2**
genau zwei additive Felder am Sektionsende (nach dem Druckausklang, feste
Feldordnung, Little-Endian-Festbreiten, Sentinel-Regeln wie der Bestand):

| Feld | Typ | Bedeutung |
|---|---|---|
| `MissionActive` | u8 | Aktivierungskennung der Abschluss- und Wiederholungsschicht (0/1); 1 ist vertraglich an `PressureActive = 1` gekoppelt. |
| `MissionChainRunCount` | i64 | Kettenlauf-Anzahl der aktuellen Kette; beginnt bei 1 und erhöht sich je wirksamer Wiederholung um genau eins. |

Relationswahrheiten der Sektion (fail-closed, unterscheidbare
Verletzungsklasse `SESSION_SECTION_INVALID`): `MissionActive > 1` ist
unbekannt; ohne Aktivierung trägt die Zählung 0; mit Aktivierung trägt sie
mindestens 1; mit Aktivierung ohne Druckaktivierung widerspricht die
vertragliche Kopplung. Die abgeleitete Abschlusswahrheit selbst trägt kein
Sektionsbyte (Abschlussvertrag Abschnitt 2): sie ist nach dem Laden aus den
fortgesetzten Erkundungs-, Entscheidungs- und Druckwahrheiten erneut
ableitbar. `saveSchemaVersion` bleibt unverändert 2; die
Sektionsversion (`sessionSectionVersion`) wird 2 und ist von der
Umschlagsversion unabhängig wie bisher.

**Alternativen:** eigenes zweites Sektionsobjekt nur für die Missionsfläche
(zweite Hash-/Framingfläche ohne Mehrwert — abgelehnt); Missionswahrheit im
Payload (Berührung des simrelevanten Relevantzustands — abgelehnt, die
Sektion ist die vertragliche Sitzungswahrheit).

### 15.2 Legacy-Kompatibilität, Guards und Reportlinie

**Legacy (`legacy-section-v1-mission-emptiness-v3`):** Slots mit Sektionsversion 1
laden unverändert mit ehrlicher, maschinenrebarer Missionsleere
(`MissionActive = 0`, `MissionChainRunCount = 0`) ohne Migrationserfindung;
das Re-Encoding einer dekodierten Sektion erfolgt versionsgetreu (v1-Bytes
bleiben v1, v2-Bytes bleiben v2), sodass die Re-Encoding-Gleichheit beider
Bestände bindet. Neue Slots schreiben Sektionsversion 2.

**Aktivierungsgrenze:** Die bestehende Grenze `layer-activation-mismatch`
gilt unverändert für die Missionsaktivierung: die Schichtflags des
Fortsetzungslaufs müssen mit der Sektion übereinstimmen; ein Widerspruch ist
ein kontrollierter Abbruch ohne Aktivierung.

**Reportlinie:** Save-/Ladeläufe mit Missionsaktivierung tragen die rein
additive Schemaversion 7 (Abschlussvertrag Abschnitt 8) mit dem
Pflichtblock `missionSession` und dem Fortsetzungsblock; Save-/Ladeläufe
ohne Missionsaktivierung bleiben bei Schemaversion 6 byteidentisch. Es
entstehen keine neuen Exitcodebedeutungen; die ausdrückliche Replay-Ausnahme
(Abschnitt 13.6) gilt unverändert für die Kettenlaufwahrheit
(`replay=not-continued`).

**Playtestkriterien:** Nach dem Speichern in Kettenlauf 2 und dem Laden in
einem frischen Prozess trägt der Report dieselbe Kettenlauf-Anzahl und
denselben abgeleiteten Abschlusszustand; ein Slot der Sektionsversion 1
lädt unverändert mit ehrlicher Missionsleere. **Rückrollweg:** Umkehr auf
V2 (keine Missionsfelder) durch Vertragsversionswechsel mit Neubau und
Fixture-Regeneration; die Abschlussschicht trägt dann die ehrliche
Nichtpersistenz ihres Zählers. **Fixture-Regeneration:** Tests, die die
Sektionslänge, die Prüfklassen und die Restaurierung an der
Sektionsversion 1 gebunden haben, wurden im selben Kandidaten um die
Sektionsversion 2 erweitert; Payload-, Ketten- und Endhashbindungen bleiben
unverändert gültig.
