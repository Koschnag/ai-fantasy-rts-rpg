# Anforderungen

Jede Anforderung ist einzeln identifizierbar, testbar und mit einem fachlichen Ziel verknüpft. Lösungsdetails gehören nur hierher, wenn sie tatsächlich vorgeschrieben sind.

## Funktionale Anforderungen

| ID | Anforderung | Begründung | Priorität | Quelle / Ziel | Status |
|---|---|---|---|---|---|
| F-001 | Der Spieler kann eine Heldengruppe auswählen, bewegen, ausrüsten und Fähigkeiten taktisch einsetzen. | persönlicher RPG-Maßstab | MUST | Z-001 | ANGENOMMEN |
| F-002 | Erkundung, Dialoge und Aufgaben verändern verfügbare strategische Wege oder Ressourcen. | echte Verzahnung statt zweier getrennter Modi | MUST | Z-001 | ANGENOMMEN |
| F-003 | Der Spieler kann ein Schutzquartier aufbauen, zwei Ressourcen verwalten, Einheiten ausbilden und Verbände befehligen. | strategischer Kern | MUST | Z-001 | ANGENOMMEN |
| F-004 | Strategische Ergebnisse verändern spätere persönliche Inhalte, Beziehungen oder Weltzustände sichtbar. | Kausalität in Gegenrichtung | MUST | Z-001 | ANGENOMMEN |
| F-005 | Der Vertical Slice kann vollständig gespeichert und aus einem stabilen Schema geladen werden. | Kampagnen- und Testbarkeit | MUST | Z-001 | ANGENOMMEN |
| F-006 | Grafik, Audio, Sprache und Eingaben sind im Spiel konfigurierbar; Kernaktionen sind frei belegbar. | Plattform- und Zugänglichkeitsbedarf | MUST | Z-002, Z-003 | ANGENOMMEN |
| F-007 | Jeder autonome KI-Auftrag erzeugt nachvollziehbare Runs, Quellenzitate, Änderungen und Prüfevidenz. | kontrollierbare Vollautomation | MUST | Z-004 | ANGENOMMEN |
| F-008 | Jedes Shipping-Asset besitzt technische Metadaten, Hashes, Generator-/Bearbeitungsverlauf und geklärte Nutzungsgrundlage. | reproduzierbare, rechtlich nachvollziehbare Assetproduktion | MUST | Z-004, Z-005 | ANGENOMMEN |
| F-009 | Jeder agentische Assetgenerator nimmt ausschließlich ein strikt versioniertes, begrenztes internes Spec an, arbeitet ohne nicht inventarisierte kreative Inputs und liefert unabhängig prüfbare Artefakte, deterministische Metadaten sowie einen fail-closed Quarantäne-Lifecycle. | autonome Synthese darf weder Herkunft, Fehler noch technische Abweichungen verbergen | MUST | Z-004, Z-005 | ANGENOMMEN |

### Vorlage

#### F-XXX – Titel

- **Beschreibung:** Das System muss …
- **Nutzen / Begründung:**
- **Akteur:**
- **Vorbedingungen:**
- **Auslöser:**
- **Hauptablauf:**
- **Fehler- und Sonderfälle:**
- **Daten:**
- **Abhängigkeiten:**
- **Priorität:** MUST / SHOULD / COULD / WONT
- **Status:** OFFEN / ENTSCHIEDEN / READY / DONE

**Abnahmekriterien**

- Gegeben …, wenn …, dann …
- Gegeben …, wenn …, dann …

## Nichtfunktionale Anforderungen

Nicht zutreffende Bereiche werden bewusst als „nicht relevant“ markiert, nicht einfach ausgelassen.

| ID | Bereich | Messbare Anforderung | Prüfverfahren | Status |
|---|---|---|---|---|
| NF-001 | Leistung | Hardwareprofile, Frame-/Simulations-/RAM-/VRAM-Grenzen aus `PERFORMANCE_BUDGET.md` werden in Pflichtszenen eingehalten. | automatisierte Benchmarks auf Referenzklassen | ENTSCHIEDEN |
| NF-002 | Zuverlässigkeit | Ein deterministischer 8-Stunden-Soak-Test darf nicht abstürzen, hängen oder fortschreitend Speicher verlieren. | Replay-/Soak-Harness; genaue Schwelle im Spike | ANGENOMMEN |
| NF-003 | Sicherheit | Keine Secrets im Repository/Harness; Eingaben an Datei-, Netzwerk-, Mod- und Assetgrenzen werden validiert. | Secret-/Schema-/Threat-Model-Gates | ANGENOMMEN |
| NF-004 | Datenschutz / Aufbewahrung | Das Spiel benötigt kein Konto und keine Runtime-Cloud; Telemetrie ist lokal bzw. explizit opt-in. | Netzwerk-/Konfigurationsprüfung | ANGENOMMEN |
| NF-005 | Barrierefreiheit | frei belegbare Kerneingaben, skalierbare UI/Untertitel, pausierbare Dialoge, keine reine Farbcodierung | manuelle und automatisierte UI-Abnahme | ANGENOMMEN |
| NF-006 | Kompatibilität | Windows x64, Linux x64 und macOS arm64 besitzen native Build- und Smoke-Artefakte. | CI-Matrix und native Tests | ENTSCHIEDEN |
| NF-007 | Beobachtbarkeit | Entwicklungsbuilds geben Frame-/Tickzeiten, Allokationen, Agenten-, Draw-, VRAM-/RAM- und Streamingwerte maschinenlesbar aus. | Benchmark-Schema und Golden Runs | ANGENOMMEN |
| NF-008 | Wartbarkeit | gepinnte Toolchain, Locked Restore, 0 Buildwarnungen, explizite Modulgrenzen und ein gemeinsamer Befehlsvertrag | `./scripts/rift.sh verify` und Architekturtests | ANGENOMMEN |

## Nachverfolgbarkeit

| Ziel | Anforderung | User Flow | Backlog-Eintrag | Test / Nachweis |
|---|---|---|---|---|
| Z-001 | F-001–F-004 | UF-001 | T-030 | Vertical-Slice-Replay und manuelle Abnahme |
| Z-001 | F-005, NF-002 | UF-002 | T-031 | Save-Roundtrip, atomarer Abbruch, Korruptions- und Recovery-Fixtures |
| Z-001 | NF-004 | UF-001 | T-011, T-030 | kompletter Offline-Smoke mit gesperrtem Netzwerk; keine unerwarteten Verbindungen |
| Z-002 | NF-001, NF-002, NF-007 | UF-006 | T-020, T-021, T-022, T-023 | isolierte und integrierte Hardwarebenchmarks, maschinenlesbare Telemetrie und 8-Stunden-Soak |
| Z-003 | NF-006 | UF-001 | T-010, T-011 | native CI-Smokes |
| Z-004 | F-007, NF-003, NF-008 | UF-004 | T-001, T-002, T-004 | Harness-, Security-, Provenienz- und Integritätstests |
| Z-004, Z-005 | F-008, F-009 | UF-005 | T-003, T-005, T-006, T-007, T-050 | Spec-/Artefaktinspektion, Assetmanifest, technische Prüfung, Provenienz- und Originalitätsgate |
| Z-005 | F-002, F-004 | UF-001 | T-040 | Atmosphärenrubrik, Cross-Mode-Folgen und Originalitätsgate |
| Z-002, Z-003 | F-006, NF-005 | UF-003 | T-010, T-041 | Einstellungs-/Eingabe-Smoke und Barrierefreiheitsabnahme |
