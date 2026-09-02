# T-053 Bedrohungen der Validitaet

**Protokoll:** `riftward-research-observability` 2.0.0

Die Fallstudie ist eine eingebettete Einzelprojektbeobachtung. Ihre Daten
koennen Prozessverlaeufe in Riftward beschreiben; sie beweisen keine allgemeine
Ueberlegenheit eines Modells, eines Harnesses oder agentischer Entwicklung.

## Konstruktvaliditaet

| Bedrohung | Wirkung | vorregistrierte Gegenmassnahme | verbleibende Grenze |
|---|---|---|---|
| Prozesslaufzeit wird als Autonomie missverstanden | Warten oder Schleifen erscheinen produktiv | Walltime, Toolzeit, Interventionen, Gates und Outcome getrennt berichten | unbeobachtete lokale Denk-/Arbeitszeit bleibt `unknown` |
| Commit-/Zeilenzahl wird als Fortschritt gewertet | grosse oder viele Diffs erscheinen besser | Outcome, Kriterienbindung und Gateevidenz sind Primaerbezug; Churn nur deskriptiv | Produktnutzen benoetigt spaetere Nutzer-/Spieltests |
| Gate-Pass ohne Zielbaumbindung | alter Pass wird neuem Ergebnis zugerechnet | Pass zaehlt nur auf Outcome-Zielbaum mit aufloesbarem Beleg | falsch spezifizierte Gates koennen trotzdem gruen sein |
| Eingriffskategorien sind interpretationsabhaengig | Klassifikatorbias | exklusive Regeln, Entscheidungsakt-Hash, `unknown` bei Mehrdeutigkeit | semantische Grenzfaelle bleiben |
| offener Eingriff wird als Menschenzeit geschaetzt | Antwortlatenz blaest aktive Minuten auf | Dauer nur aus `research intervention start`/`end` derselben monotonen Uhr; offen/`record` bleibt `unknown` | nicht instrumentierte Arbeit bleibt unbekannt |
| Architektur-Touchzahl wird als Kopplung interpretiert | Breite Aenderung wirkt automatisch schlecht | nur Rohwerte und bestaetigte Grenzbefunde; keine Qualitaetsrichtung aus Touchzahl | statische Kanten erfassen dynamische Kopplung nicht vollstaendig |
| fehlende Daten werden zu Null oder Erfolg | Performance wird ueberzeichnet | literal `unknown`, Unknown-Rate und fail-closed Primaerregel | hohe Unknown-Rate reduziert Aussagekraft |

## Interne Validitaet

| Bedrohung | Wirkung | Gegenmassnahme | verbleibende Grenze |
|---|---|---|---|
| T-037 retrospektiv, T-042 prospektiv | Historie ist unvollstaendiger und Tasks sind verschieden | Evidenzklassen strikt trennen; kein kausales A/B | deskriptive Kalibrierung erlaubt keine Wirkungsschaetzung |
| Beobachtereffekt | Collector oder Kenntnis der Metriken aendert Verhalten | read-only Zielzugriff, Nichtinterferenz-Snapshots, kein Gate-/Statuswrite | Menschen/Agenten koennen durch Protokollkenntnis ihr Verhalten aendern |
| gleichzeitige WIP-Aenderungen | falsche Attribution | Baseline-/Ergebnisbaum und Quellpfade binden; fremde Baeume ausschliessen | externe Dienste koennen dennoch Einfluss nehmen |
| WIP-Sidecar wird als Outcome oder Autoritaet gelesen | Kontinuitaet erscheint als Akzeptanz/Promotion | `continuityOnly`, Tree-/Sidecar-Hashbindung, keine Historienumschreibung oder direkte `main`-Autoritaet | externe Prozessfehler koennen Sidecars falsch erzeugen und muessen fail-closed validiert werden |
| Taskkomplexitaet und Domäne | Metrikdifferenzen werden Instrumentenwirkung zugeschrieben | Stratum-ID, keine Vergleiche ungleicher Tasks, spaetere Replikationen | mit P-001 existiert nur n=1 prospektiv |
| Lern-, Modell- und Toolchaintrend | spaetere Runs profitieren von Zeit und Erfahrung | Modell-/Toolchain-/Prompt-/Commitbindung | zeitliche Effekte lassen sich im Einzelfall nicht isolieren |
| Review ist Teil des Systems | Intervention und Qualitaetsorakel vermischen sich | Reviewereignis und menschliche Reviewkorrektur getrennt erfassen | Reviewerqualitaet ist nicht unabhaengig garantiert |
| selektives Stoppen oder Exportieren | guenstiger Ausschnitt dominiert | Abbruchregeln, append-only Kette, alle Exporte hashen | nicht erfasste Vorlaeufer koennen retrospektiv verborgen bleiben |
| freie Logs werden als Prozesswahrheit interpretiert | Textheuristik erfindet Gates, Modelle oder Interventionen | strukturierte Harnessgrenzen sind autoritativ; Logparsing nur supplemental | nicht instrumentierte Grenzen bleiben `unknown` |

## Statistische Schlussvaliditaet

- P-001 hat Stichprobengroesse eins. Es werden keine Signifikanztests,
  Konfidenzintervalle oder Populationsschaetzungen berichtet.
- Ratios werden mit Nenner und Rohzaehlern exportiert. Kleine Nenner werden
  nicht als stabile Rate interpretiert.
- Architekturtrends beginnen erst bei drei prospektiven Beobachtungen im
  gleichen Stratum. Ein Trend ist deskriptiv, kein Kausalparameter.
- Multiple Metriken werden nicht nachtraeglich nach einem auffaelligen Wert
  ausgewaehlt. Primaer- und Sekundaermetriken sind im Protokoll getrennt.
- Ein fehlender Wert bleibt `unknown`; es gibt keine Imputation, Extrapolation
  oder Umrechnung nicht vergleichbarer Providerfelder.
- Ablationen pruefen Instrumentverhalten auf synthetischen Kopien. Sie
  vergroessern nicht die reale Stichprobe.

## Externe Validitaet

- Riftward ist ein einzelnes FOSS-orientiertes Spiel-/Harnessprojekt mit
  spezifischen Regeln. Ergebnisse uebertragen sich nicht automatisch auf
  andere Repositories, Organisationen, Sprachen oder Risikoklassen.
- T-037 und T-042 sind Graybox-/Prozessauftraege. Sie vertreten nicht Asset-,
  Plattform-, Security-, Nutzerforschungs- oder Vollproduktionsarbeit.
- Lokale Hardware- und Netzwerkbedingungen werden gebunden, vertreten aber
  kein Hardwareprofil, solange die Projektvertraege es nicht so deklarieren.
- Providerpreise, Modelleigenschaften und Telemetrie koennen sich aendern.
  Nur quittierte Werte des gebundenen Zeitfensters gelten.
- Eine erfolgreiche Beobachtung von T-042 beweist Beobachtbarkeit dieses
  Laufs, nicht mehrtaegige Autonomie oder oekologische Amortisierung.

## Daten- und Messvaliditaet

| Bedrohung | Regel |
|---|---|
| Uhrdrift oder fehlende Zeitquelle | betroffene Dauer `unknown`; Commitzeit nicht substituieren |
| doppelte Zustellung/Ereignisse | stabile Entscheidungs-/Ergebnishashes deduplizieren; Rohereignisse append-only behalten |
| verlorener Collector-Tail | Kettenluecke melden, Primaermetrik `false` oder bei unlesbarer Quelle `unknown` |
| manipulierte Quelle | Hash-/Adresspruefung scheitert; Referenz `resolvable=false` |
| redaktionsbedingter Informationsverlust | Wert `unknown` mit `availabilityReason=redacted`; keine private Rekonstruktion im Public Export |
| unterschiedliche Serialisierung | Byteidentitaet scheitert; semantische Gleichheit gilt nicht als Ersatz |
| Providerfeldsemantik unklar | einzelne Token-/Kostenmetrik `unknown`; keine lokale Normalisierung |
| Binaerdiff | Zeilenwerte fuer diesen Anteil `unknown`; Binaerdateizahl separat |
| paralleler Writer oder Torn Tail | exklusiver Lock, atomarer Append und fail-closed `TORN_TAIL`; Recovery nur in neue Datei, nie stille Trunkierung |
| stabile Pseudonyme werden deanonymisiert | Public Export entfernt Actor-/Run-Zuordnung; private Map getrennt und zugriffsbeschraenkt |

## Researcher Degrees of Freedom

Nach Freeze sind ohne neue Protokollversion nicht aenderbar:

- Evidenzklassen und Unknown-Regel,
- P-001-Ziel, Primaerhypothese und Primaermetriken,
- Interventionstaxonomie,
- Exportdateien und Sortierung,
- Ablationsfaktoren,
- Faktoren der spaeteren isolierten Full-/Memory-/Session-/Review-/Routingexperimente,
- RQ-07-Ergebnisregel,
- Abbruch- und Nichtinterferenzregeln.

Zulaessig ohne Protokollrevision sind reine Tippfehlerkorrekturen nur dann,
wenn sie keinerlei Semantik, Berechnung, Feld oder Schwelle aendern. Auch sie
werden im Changelog dokumentiert und erzeugen einen neuen Bundle-Hash; Daten
bleiben an den alten Hash gebunden.

## Interpretation

Jeder Bericht enthaelt einen Abschnitt mit:

1. Evidenzklasse und Beobachtungsfenster,
2. Unknown-Rate und fehlenden Primaerfeldern,
3. Abweichungen vom Protokoll,
4. moeglichen Confoundern,
5. Reichweite der Aussage,
6. alternativen Erklaerungen,
7. naechstem eng begrenztem Replikationsschritt.

`Inconclusive` ist ein valides Ergebnis. Es darf nicht als verdecktes
`supports` formuliert werden.
