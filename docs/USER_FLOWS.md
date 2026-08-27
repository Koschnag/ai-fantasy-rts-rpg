# Nutzerwege

Nutzerwege beschreiben sichtbares und prüfbares Verhalten. Der Spielclient ist ein lokales Einzelspielerprodukt ohne Konto- oder Netzpflicht; Produktionswege richten sich an Projektleitung und KI-Agenten.

## UF-001 – Vertical Slice vom persönlichen Auftrag zur strategischen Entscheidung

- **Status:** ANGENOMMEN
- **Akteur:** Spieler
- **Ziel:** Eine vollständige 20–30-minütige Mission erleben, in der Heldenerkundung, Entscheidung, Aufbau und Armeekampf kausal zusammenhängen.
- **Startpunkt:** Hauptmenü mit gültigem Content-Paket und spielbaren Standardeinstellungen.
- **Erfolgsergebnis:** Boss beziehungsweise befestigte Bedrohung ist überwunden; die gewählte Questoption und ihr sichtbares Ergebnis werden im Abschlusszustand gespeichert.
- **Verknüpfte Anforderungen:** F-001–F-006, NF-001, NF-004, NF-005

### Standardablauf

1. Der Spieler startet `Neues Spiel`; die Karte zeigt zunächst die Hauptfigur und zwei Begleiter in einer fremden Ruinenlandschaft.
2. Kamera-, Auswahl- und Bewegungsfeedback erklären die Grundbefehle ohne langen Textblock.
3. Erkundung, ein Umweltwiderspruch des Wanderbruchs und ein kurzer Dialog führen zu einer Aufgabe mit zwei verständlichen Optionen.
4. Die Heldengruppe besteht ein taktisches Gefecht und gewinnt dadurch einen Standort, einen Fachmenschen, einen Weg oder eine Ressource.
5. Der Spieler verpflichtet sich sichtbar auf ein Schutzquartier; Bewohner und Transporte treffen ein, und der Übergang zum Aufbau wird diegetisch sowie über UI angekündigt.
6. Der Spieler verwaltet zwei Ressourcen, errichtet aus den fünf verfügbaren Gebäudetypen eine funktionsfähige Basis und bildet einen gemischten Verband aus vier Einheitentypen aus.
7. Die frühere Questentscheidung verändert mindestens eine strategische Bedingung, zum Beispiel Verbündete, Route, Kosten, Verteidigungsfenster oder Gegneraufstellung.
8. Heldenfähigkeiten und Armeebefehle werden gemeinsam eingesetzt, um eine angekündigte größere Bedrohung und den Boss zu überwinden.
9. Eine ruhige Nachhallphase zeigt Schäden, Überlebende und Weltreaktion. Das Spiel speichert den Abschlusszustand und bietet Fortsetzen, Wiederholen oder Rückkehr ins Hauptmenü an.

### Alternativen und Fehlerfälle

| Auslöser | Erwartetes Systemverhalten | Sichtbare Möglichkeit des Spielers |
|---|---|---|
| Befehl hat kein gültiges Ziel | Weltzustand bleibt unverändert; eindeutiger Marker/Ton erklärt die Ablehnung | Ziel oder Aktion neu wählen |
| Baufläche, Ressource oder Voraussetzung fehlt | Vorschau und konkrete Voraussetzung werden vor Bestätigung angezeigt | Standort ändern oder Voraussetzung erfüllen |
| Verband findet keinen vollständigen Weg | erreichbarer Teilweg beziehungsweise kontrollierte Ablehnung; keine Einheit verschwindet oder hängt endlos | neuen Weg oder andere Formation wählen |
| Held fällt oder missionskritischer Zustand scheitert | definierter Fehlschlag mit Ursache; kein unklarer Softlock | letzten gültigen Checkpoint laden oder neu starten |
| Spieler pausiert oder öffnet ein Menü | Simulation stoppt gemäß festgelegter Pausenregel; Auswahl und Kontext bleiben erhalten | Einstellungen ändern, speichern oder fortsetzen |
| Grafiklast überschreitet Budget | gewähltes Qualitätsprofil reduziert nur Darstellung, nie Simulation oder taktische Information | Profil wechseln; Benchmarkdaten bleiben lokal sichtbar im Entwicklungsbuild |
| Netzwerk ist nicht vorhanden oder vollständig gesperrt | Hauptmenü, neues Spiel, Save/Load und die vollständige Mission funktionieren unverändert; keine Konto- oder Cloudaufforderung | lokal weiterspielen |

### Abnahmebeispiele

> Gegeben ein neuer Vertical-Slice-Lauf, wenn der Spieler die Dialogoption A wählt und das Schutzquartier errichtet, dann verändert eine vorab spezifizierte Folge dieser Wahl spätestens im Abschlusskampf eine sichtbare und spielrelevante Bedingung.

> Gegeben eine abgeschlossene Mission, wenn der Spieler den gespeicherten Abschlussstand lädt, dann stimmen Wahl, überlebende Gruppe, relevante Ressourcen und Abschlussfakten mit dem Zustand vor dem Speichern überein.

> Gegeben ein System mit vollständig gesperrtem Netzwerk, wenn der Spieler vom Hauptmenü bis zum Missionsabschluss spielt und speichert, dann benötigt kein Pflichtschritt ein Konto, Telemetrie, einen externen Dienst oder eine Netzwerkfreigabe.

## UF-002 – Speichern, Laden und sichere Wiederherstellung

- **Status:** ANGENOMMEN
- **Akteur:** Spieler
- **Ziel:** Fortschritt ohne Datenverlust unterbrechen und reproduzierbar fortsetzen.
- **Startpunkt:** Laufende Mission oder Hauptmenü.
- **Erfolgsergebnis:** Ein validierter Spielstand wird geladen; der Spieler erhält denselben fachlichen Zustand am gespeicherten Simulationstick.
- **Verknüpfte Anforderungen:** F-005, NF-002

### Standardablauf

1. Der Spieler wählt einen manuellen Slot oder das Spiel erreicht einen definierten Checkpoint.
2. Der Client erzeugt einen konsistenten Snapshot und schreibt ihn zunächst getrennt vom letzten gültigen Stand.
3. Nach erfolgreicher Validierung erscheint der Slot mit Ort, Spielzeit, Zeitpunkt und optionaler Vorschau.
4. Beim Laden prüft der Client Save-Schema, Content-Kompatibilität, Referenzen und Integrität vor Aktivierung.
5. Kamera, Gruppe, Mission, Ressourcen, Gebäude, Quest-/Weltfakten und Fog of War werden wiederhergestellt; temporäre Präsentation darf neu aufgebaut werden.

### Alternativen und Fehlerfälle

| Auslöser | Erwartetes Systemverhalten | Sichtbare Möglichkeit des Spielers |
|---|---|---|
| Datenträger voll oder Schreibfehler | neuer Snapshot wird nicht als gültig markiert; vorheriger Slot bleibt unangetastet | Speicherort freigeben und erneut speichern |
| Datei abgeschnitten oder Hash falsch | Stand wird als beschädigt benannt und nicht teilweise geladen | anderen Slot/Backup wählen; Diagnose exportieren, falls implementiert |
| Save-Version benötigt bekannte Migration | Migration läuft auf einer Kopie und wird nachher validiert | migrierten Stand öffnen oder abbrechen |
| unbekannte/incompatible Version oder fehlender Content | keine erfundene Migration und kein stilles Verwerfen | verständliche Kompatibilitätsmeldung; Originaldatei bleibt erhalten |
| Abbruch während des Schreibens | letzter bestätigter Stand bleibt ladbar | erneut speichern |

### Abnahmebeispiel

> Gegeben ein gültiger Slot und ein künstlich abgebrochener Überschreibvorgang, wenn der Spieler anschließend lädt, dann wird der letzte bestätigte Zustand angeboten und kein teilweise geschriebener Zustand aktiviert.

## UF-003 – Einstellungen, Eingaben und zugängliche Darstellung

- **Status:** ANGENOMMEN
- **Akteur:** Spieler
- **Ziel:** Grafik, Audio, Sprache und Kernaktionen ohne Dateibearbeitung an Gerät und Bedarf anpassen.
- **Startpunkt:** Hauptmenü oder pausiertes Spiel.
- **Erfolgsergebnis:** Gültige Einstellungen sind lokal gespeichert, sofort prüfbar und nach Neustart aktiv.
- **Verknüpfte Anforderungen:** F-006, NF-001, NF-005

### Standardablauf

1. Der Client startet beim ersten Lauf mit konservativen, sichtbaren Standardeinstellungen und einer sicheren Auflösung.
2. Der Spieler wählt Auflösung/Fensterart, Qualitätsprofil und gegebenenfalls VSync oder Framelimit.
3. Audiopegel, Textsprache, Untertitel, Text-/UI-Skalierung und bestätigte Zugänglichkeitsoptionen werden unabhängig angepasst.
4. Jede Kernaktion kann auf eine oder mehrere Eingaben gelegt werden; das UI verwendet semantische Aktionsnamen.
5. Kritische Anzeigeänderungen werden zeitlich bestätigt. Nach Erfolg werden Settings atomar gespeichert.

### Alternativen und Fehlerfälle

| Auslöser | Erwartetes Systemverhalten | Sichtbare Möglichkeit des Spielers |
|---|---|---|
| neue Anzeigeeinstellung wird nicht bestätigt oder verursacht Fokusverlust | automatische Rückkehr zur letzten funktionierenden Konfiguration | andere Auflösung/Fensterart wählen |
| Binding kollidiert | beide betroffenen Aktionen und Kontext werden angezeigt | ersetzen, zusätzlich binden oder abbrechen |
| Settings-Datei beschädigt | sichere Defaults laden; beschädigte Datei nicht blind überschreiben, bevor Nutzeraktion möglich ist | Defaults übernehmen und neu speichern |
| Spracheintrag fehlt | technischer Schlüssel wird im Shipping-Build nicht still gezeigt; Content-Gate muss zuvor fehlschlagen | im Entwicklungsbuild klarer Missing-Key-Hinweis |
| Farbsinn-/Kontrastbedarf | Ziel, Auswahl, Gefahr und Team bleiben zusätzlich über Form, Symbol, Muster oder Text unterscheidbar | passende Option aktivieren |

### Abnahmebeispiel

> Gegeben eine laufende Mission, wenn der Spieler `AbilitySlot1` neu bindet, UI-Skalierung und Untertitel ändert und den Client neu startet, dann sind die Einstellungen aktiv und die Mission bleibt ohne Informationsverlust bedienbar.

## UF-004 – Autonomer KI-Auftrag mit nachvollziehbarer Evidenz

- **Status:** ENTSCHIEDEN im Grundablauf; Erweiterungen gemäß T-002/T-004 noch OFFEN
- **Akteur:** KI-Agent; Projektleitung als Freigabestelle
- **Ziel:** Eine klar begrenzte Änderung reproduzierbar implementieren und objektiv prüfen, ohne offene Produktentscheidungen zu erfinden.
- **Startpunkt:** Backlog-Eintrag und maschinenlesbarer Task mit Status `READY`.
- **Erfolgsergebnis:** Änderung, Tests, Quellen, Ereigniskette und Abschlussbericht sind einem Run zugeordnet; Taskstatus wird nur nach separater Abnahme geändert.
- **Verknüpfte Anforderungen:** F-007, NF-003, NF-007, NF-008

### Standardablauf

1. Der Agent liest Quellenhierarchie, Taskscope, Nicht-Scope und Abnahmekriterien und startet einen Rift-Harness-Run.
2. Der lokale RAG-Index wird aktualisiert; verwendete Treffer werden mit Pfad, Zeilen und Hash festgehalten.
3. Der Agent plant eine kleine Änderung und dokumentiert nötige Annahmen. Eine blockierende `OFFEN`-Entscheidung beendet die Implementierung kontrolliert.
4. Implementierung, Tests und Dokumentation werden innerhalb des erlaubten Umfangs geändert.
5. Verfügbare Build-, Test-, Integritäts- und aufgabenspezifische Gates laufen mit maschinenlesbarer Evidenz. Nicht implementierte Gates melden `NICHT VERFÜGBAR` und gelten nicht als bestanden.
6. Der Agent schließt den Run mit Ergebnis, Änderungen, Prüfungen und Restpunkten. Ein Review akzeptiert oder verwirft Änderung und vorgeschlagene Memory-Records getrennt.

### Alternativen und Fehlerfälle

| Auslöser | Erwartetes Systemverhalten | Sichtbare Möglichkeit der Projektleitung |
|---|---|---|
| Task ist `DRAFT`/`OFFEN` oder Kriterium widersprüchlich | keine Implementierung; Run beziehungsweise Bericht nennt konkrete Lücke | Task klären und auf `READY` setzen |
| Retrieval enthält Anweisung oder Konflikt | Inhalt bleibt untrusted data; Policy ändert sich nicht; Konflikt wird zitiert | Quellenhierarchie entscheiden |
| Test oder Gate scheitert | Task wird nicht `DONE`; Fehler, Umgebung und Artefakthash bleiben sichtbar | Nachbesserung beauftragen oder Scope neu entscheiden |
| Logpayload enthält Secretmuster | Redaction vor Persistierung; Secret darf nicht in Index/History gelangen | Credentials rotieren, falls Exposition nicht ausgeschlossen ist |
| Agent schlägt neue Erkenntnis vor | Record bleibt `proposed` und ist keine Wahrheit | annehmen, ablehnen oder ältere Aussage explizit ersetzen |

### Abnahmebeispiel

> Gegeben ein `READY`-Task mit drei Kriterien, wenn der Run erfolgreich endet, dann lässt sich jedes Kriterium auf mindestens einen Befehl, ein Ergebnis und einen Artefakt-/Quellhash zurückführen; eine manipulierte Ereigniszeile lässt `verify` fehlschlagen.

## UF-005 – KI-/prozedural erzeugtes Asset bis zum Shipping-Paket

- **Status:** ANGENOMMEN; konkrete Generatoren und Artefaktspeicherung OFFEN
- **Akteur:** Asset-Agent; Art-/Produktfreigabe
- **Ziel:** Ein originäres, technisch budgetgerechtes und nachvollziehbares Asset produzieren.
- **Startpunkt:** freigegebene Asset-Spezifikation mit Zweck, Budget, Form-/Farbregeln und Negativliste.
- **Erfolgsergebnis:** Gecooktes Asset und vollständiges Manifest sind technisch, visuell, lizenzseitig und auf Eigenständigkeit freigegeben.
- **Verknüpfte Anforderungen:** F-008, Z-005

### Standardablauf

1. Der Agent vergibt eine Asset-ID und legt Prompt/Verfahren, Negativprompt, Tool/Modell/Version/Seed, Eingaben, Nutzungsgrundlage und Zielbudgets fest.
2. Output und alle Varianten landen mit Hashes in Quarantäne; keine Rohdatei wird direkt in Content referenziert.
3. Automatische Gates prüfen Format, Geometrie, Skalierung, Pivot, Materialslots, Texturen, Rig/Animation, LODs, Kollision und Plattform-Cookbarkeit, soweit zutreffend.
4. Ein Art-Review prüft Silhouette, Lesbarkeit bei Spielkamera, Kultur-/Biom-Bible, Atmosphäre und Ähnlichkeits-Negativliste.
5. Akzeptierte Nachbearbeitung wird als Abstammung im Manifest ergänzt. Der Cooker erzeugt das Runtimeformat und aktualisiert Paket-Hashes.

### Alternativen und Fehlerfälle

| Auslöser | Erwartetes Systemverhalten | Sichtbare Möglichkeit der Freigabe |
|---|---|---|
| Quelle, Outputrechte oder Modellversion unklar | Status bleibt Quarantäne; kein Shipping-Cook | Quelle klären oder neu erzeugen |
| erkennbare Nähe zu konkreter fremder Figur, Fraktion, Karte, UI oder Track | Originalitäts-Gate schlägt unabhängig von technischer Qualität fehl | verwerfen und aus unabhängiger Spezifikation neu gestalten |
| Polygon-, Knochen-, Material- oder Texturbudget überschritten | technisches Gate scheitert mit Messwert und Grenzwert | gezielt optimieren oder Budgetänderung mit Profil beantragen |
| externer Generator nicht erreichbar | Job bleibt reproduzierbar wartbar; Runtime und bestehende Builds sind unbeeinflusst | später wiederholen oder FOSS-lokale Alternative wählen |
| visuell gut, aber aus Spielkamera unlesbar | keine Freigabe | Silhouette, Wertehierarchie oder VFX überarbeiten |

### Abnahmebeispiel

> Gegeben ein Kandidat für eine normale Einheit, wenn `assets-check` und das visuelle Review abgeschlossen sind, dann liegen Hash, Provenienz, gültige LODs, höchstens 48 Knochen, höchstens zwei Materialien und ein freigegebenes 1K-Texturset vor; andernfalls erreicht das Asset das Spielpaket nicht.

## UF-006 – Hardware- und Performancefreigabe

- **Status:** ENTSCHIEDEN für Ziele; konkrete Referenzrechner und Messtoleranzen teilweise OFFEN
- **Akteur:** Performance-Agent / Projektleitung
- **Ziel:** Nachweisen, dass Gameplay und höchste geplante Bildqualität die vereinbarten Leistungsklassen einhalten.
- **Startpunkt:** reproduzierbarer Build, gepinnter Content und verfügbare Referenzhardware.
- **Erfolgsergebnis:** Alle Pflichtbenchmarks liefern vergleichbare Messartefakte und erfüllen das jeweilige Profil oder blockieren die Freigabe.
- **Verknüpfte Anforderungen:** NF-001, NF-002, NF-007

### Standardablauf

1. Build-ID, Commit, Contenthash, OS, Treiber, Hardwareprofil, Grafiksetting und Warm-up werden protokolliert.
2. `BENCH-EMPTY`, `BENCH-ARMY`, `BENCH-BATTLE`, `BENCH-BASE`, `BENCH-PATH` und `BENCH-LOAD` laufen mit festen Seeds und Kamerapfaden.
3. Frame-/GPU-/Tickzeiten, Arbeitssatz, VRAM beziehungsweise Unified-Memory-Druck, Draws, sichtbare Einheiten, Allokationen und Ladezeit werden lokal erfasst.
4. p99- und harte Grenzwerte werden gegen `PERFORMANCE_BUDGET.md` sowie eine akzeptierte Baseline ausgewertet.
5. Ein Ergebnisbericht verlinkt Rohartefakte, Hashes, Deltas und mögliche Budgetverletzungen. Erst ein grüner Lauf auf realer Zielklasse erlaubt die Meilensteinfreigabe.

### Alternativen und Fehlerfälle

| Auslöser | Erwartetes Systemverhalten | Sichtbare Möglichkeit der Projektleitung |
|---|---|---|
| Referenzhardware fehlt | Messung auf schnellerer Hardware wird als Vorabdiagnose, nicht als Zielnachweis markiert | Hardwarelauf nachholen |
| Treiber-/OS-/Thermalzustand nicht stabil | Lauf wird ungültig statt schöngerechnet | Umgebung stabilisieren und wiederholen |
| Low-Profil unterschreitet 30 FPS oder High-Profil 60 FPS | Freigabe blockiert; Ursache nach CPU, GPU, Speicher, Streaming oder Inhalt klassifiziert | optimieren oder Budget nur per dokumentierter Entscheidung ändern |
| Qualitätsstufe entfernt taktische Information | visueller Abnahmetest scheitert trotz besserer Framerate | Darstellung optimieren, Gameplayinformation erhalten |

### Abnahmebeispiel

> Gegeben `HW-PC-MIN` bei 1920×1080 Low und aufgewärmter Pflichtszene, wenn `BENCH-BATTLE` läuft, dann liegt p99 der Framezeit höchstens bei 33,3 ms, der Arbeitssatz unter dem harten Ladepeak und der VRAM-Verbrauch unter 1,8 GB; die Simulationsmenge bleibt unverändert.

## UF-007 – Moduswechsel zwischen persönlicher Heldensicht und strategischer Übersicht

- **Status:** ANGENOMMEN als Produktfluss; Wechseldetails (Eingabe, Übergang, Sperren, Abbruch) bleiben reversible UX-Hypothesen gemäß ADR 008 und werden playtestgebunden entschieden
- **Akteur:** Spieler
- **Ziel:** Derselbe Held in derselben unveränderten Welt ist in beiden Maßstäben bedienbar: direkte Third-Person-Steuerung nahe der Heldenfigur und strategische RTS-Führung über demselben Simulationzustand.
- **Startpunkt:** Laufendes Spiel in einem der beiden Modi.
- **Erfolgsergebnis:** Der Wechsel geschieht ohne Ladebildschirm oder Weltneuinitialisierung an einer definierten Tickgrenze; Held, Akteure, Positionen, Befehle und Weltzustand bleiben kontinuierlich und wiederfinden.
- **Verknüpfte Anforderungen:** F-010, NF-001

### Standardablauf

1. Der Spieler spielt im persönlichen Modus nahe der Heldenfigur; Kamera und Eingabe zielen auf direkte Bewegung und Interaktion.
2. Der Spieler löst den Wechsel in die strategische Sicht aus; die Übersichtskamera zeigt dieselbe Welt an derselben Tickgrenze ohne Weltzustandsänderung.
3. Der Spieler führt strategische Entscheidungen aus (Auswahl, Befehle, Führung); die Simulation läuft mit denselben Identitäten weiter.
4. Der Spieler wechselt zurück in die persönliche Sicht; die Heldenfigur ist sofort wiederfindbar, und laufende Weltveränderungen setzen fortgesetzt sichtbar ein.

### Alternativen und Fehlerfälle

| Auslöser | Erwartetes Systemverhalten | Sichtbare Möglichkeit des Spielers |
|---|---|---|
| Wechsel in einer gemäß finaler Regel gesperrten Situation | kontrollierte, verständliche Abweisung ohne Weltzustandsänderung; die Sperrregel selbst bleibt playtestgebundene, reversible UX-Hypothese (Q-GAM-010, ADR 008) | Situation auflösen und Wechsel erneut versuchen |
| laufende Befehle beim Wechsel | laufende Befehle bleiben gemäß explizitem Kontrollübergabevertrag erhalten oder enden definiert; niemals stilles Verwerfen oder Doppelbefehle | Befehle neu bewerten |
| Wechsel während Simulationslast | Wechsel kostet keinen Simulationstick zusätzlich; Reaktion folgt der Budgetzeile Eingabe-zu-Reaktion | unverändert weiterspielen |

### Abnahmebeispiel

> Gegeben ein Hybrid-Graybox-Flow, wenn der Spieler mehrfach persönlich → strategisch → persönlich wechselt, dann bleiben Held und Weltzustand kontinuierlich (nachweislich identischer Simulationszustand gegenüber dem Ablauf ohne Wechsel), und der Held ist unmittelbar wiederfindbar.
