# Savevertrag (T-031, Abschnitt 0)

**Vertragsversion:** 1
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
