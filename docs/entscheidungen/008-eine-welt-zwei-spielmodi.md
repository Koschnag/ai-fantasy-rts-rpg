# ADR 008: Eine Welt, zwei Spielmodi als verbindliche Produkt-, UX- und Architekturentscheidung

- **Status:** akzeptiert
- **Datum:** Entscheidung der Projektleitung 2026-08-26; Übernahme in dieses Register 2026-08-27
- **Entscheidungsverantwortung:** Projektleitung (bestätigte Direktive); Übernahme durch den autonomen Planungsagenten gemäß Autorisierung vom 2026-08-23
- **Bezug:** `PROJEKT.md`, `docs/ANFORDERUNGEN.md` (F-010), `docs/USER_FLOWS.md` (UF-007), `docs/GAME_DESIGN.md` (GS-010), `docs/ARCHITEKTUR.md`, `docs/SIMULATIONSVERTRAG.md`, `docs/KOMMANDOVERTRAG.md`, ADR 007, T-032, alle E-004-Folgeslices der T-030-Zerlegung

## Kontext

Die Projektleitung hat am 2026-08-26 verbindlich entschieden, wie die beiden
Maßstäbe von Riftward technisch und spielerisch zusammenhängen: Riftward ist
ein vollwertiger RTS/RPG-Hybrid in einer einzigen zusammenhängenden Welt, in
der der Spieler im laufenden Spiel zwischen einer strategischen, erhöhten
RTS-Sicht und der direkten Third-Person-Steuerung einer Heldenfigur wechselt.
Diese Entscheidung galt bislang nur als externe Projektleitungsentscheidung
und war nicht im Projektarchiv verankert. Der nächste saubere Planungsslice —
dieser Lauf — übernimmt sie gemäß ihrem eigenen Auftragswortlaut in
Produktquelle, Anforderungen, User Flows, Traceability und offene Fragen und
spezifiziert danach getrennt den kleinsten prüfbaren Mode-Switch-Prototyp über
dem unveränderten Simulationskern, bevor neue Kampf-, Wirtschafts- oder
Contentbreite hinzugefügt wird (Release-Modus der Projektleitung, Schritt 2).

## Entscheidung

Die Zwei-Modi-Entscheidung wird als bestätigte, atomare Produkt-/UX-/Architekturgrundlage übernommen. Verbindlicher Kern (Wortlaut der Direktive, ohne inhaltliche Erfindung):

1. Beide Modi greifen auf dieselbe autoritative Simulation, dieselben Akteure,
   stabilen Identitäten, Positionen, Kämpfe, Ressourcen, Gebäude, Quests und
   Weltveränderungen zu. Es gibt keine getrennte RTS- und RPG-Karte, keine
   duplizierten mode-spezifischen Weltzustände und keinen verdeckten
   Neustart der Welt beim Perspektivwechsel.
2. Der strategische Modus ist ein erstklassiges RTS-Bedienmodell: frei
   navigierbare, dreh- und zoombare Übersichtskamera, Auswahl und
   Mehrfachauswahl, Kontrollgruppen, kontextuelle Befehle, Formationen,
   Basisbau, Wirtschaft, Armeeführung, Gefechtslesbarkeit und später Minimap
   beziehungsweise Fog of War. Einzelheiten bleiben task- und playtestgebunden.
3. Der persönliche Modus ist ein erstklassiges Third-Person-RPG-Bedienmodell:
   direkte Bewegung und Kamera hinter beziehungsweise nahe der aktiven
   Heldenfigur, Erkundung, Interaktion, Dialog, Ausrüstung, Fähigkeiten und
   persönlicher Kampf. Er ist keine reine Nahzoom-Kamera über weiterhin nur
   indirekter RTS-Steuerung.
4. Ein Moduswechsel verändert den Weltzustand nicht aus sich heraus. Tick,
   Welt- und Akteuridentitäten, Positionen, Befehle, Ressourcen, Questfakten
   und Gebäude bleiben kontinuierlich. Die Wechselregel wird an einer
   definierten Tickgrenze deterministisch aufgelöst und ist in Save/Load und
   Replay wahrheitsgetreu fortsetzbar.
5. Beide Maßstäbe beeinflussen einander kausal: persönliche Handlungen haben
   sichtbare strategische Folgen; Wirtschaft, Armee und
   Gebietsentscheidungen verändern die persönliche Reise. Missionen dürfen
   die Modi gewichten, aber nicht als zwei unverbundene Minispiele behandeln.
6. Die Simulation bleibt die einzige fachliche Wahrheit. Kamera, HUD und
   Geräteeingabe mutieren sie nie direkt, sondern erzeugen validierte,
   semantische und tickgebundene Befehle. RTS- und RPG-Eingabekontexte
   dürfen nicht ineinander lecken; Kontrollübergabe, Begleiterautonomie und
   laufende Befehle brauchen einen expliziten Vertrag.
7. Der Wechsel soll im normalen Spiel ohne Ladebildschirm und ohne
   Welt-Neuinitialisierung möglich sein. Genaue Eingabe, Übergangsdauer,
   Kameraanimation, erlaubte beziehungsweise gesperrte Situationen und
   Abbruchverhalten werden als reversible UX-Hypothesen mit Playtests und
   Rückrollweg entschieden, nicht stillschweigend erfunden (ADR 007).
8. Nah- und Fernsicht müssen dieselbe Szene innerhalb eigener,
   reproduzierbarer Performancebudgets darstellen. LOD, Streaming,
   Animation, Picking, Navigation, Sichtbarkeit, Kamera-Kollision und
   Eingabelatenz werden für beide Modi gemessen; ein Pass in nur einer
   Perspektive ist kein Hybrid-Pass.
9. Mindestens ein Hybrid-Graybox-Flow muss mehrfach persönlich → strategisch
   → persönlich wechseln und belegen, dass derselbe Held und dieselbe Welt
   erhalten bleiben. Playtests prüfen Moduserkennbarkeit, Wiederfinden des
   Akteurs, Orientierung, Bedienkomfort, Entscheidungsqualität und ob sich
   beide Modi als ein kohärentes Spiel anfühlen.
10. T-032 bleibt die historische RTS-Kommandobaseline; ein akzeptierter
    Vertrag wird nicht rückwirkend umgedeutet. Von der Projektleitung genannte
    Vergleichsspiele sind ausschließlich funktionales, nichtnormatives
    Shorthand; Namen, Lore, Figuren, Karten, Texte, UI, Assets, Musik,
    konkrete Kamerawerte, Balancing oder andere geschützte beziehungsweise
    werkprägende Elemente werden nicht rekonstruiert oder in
    Produktionsprompts übernommen.

## Betrachtete Optionen

### Option A: Entscheidung nur extern führen, nicht in das Projektarchiv übernehmen

- Vorteile: keine Dokumentpflege.
- Nachteile: Die verbindliche Leitungsentscheidung wäre für implementierende
  und reviewende Agenten nicht auffindbar; die Quellenhierarchie
  (`docs/entscheidungen/` vor übriger Dokumentation) bliebe ungenutzt; der
  erste Mode-Switch-Slice hätte keine akzeptierte Grundlage und müsste
  Produktannahmen still treffen.
- Risiken: getrennte Karten oder duplizierte Weltzustände entstünden schleichend.

### Option B: Zwei getrennte Spielmodi mit eigenen Weltzuständen (eigenes RPG- und RTS-Spiel)

- Wurde von der Projektleitung ausdrücklich verworfen: keine getrennte
  RTS- und RPG-Karte, keine duplizierten mode-spezifischen Weltzustände,
  kein verdeckter Weltneustart beim Perspektivwechsel.

### Option C: Übernahme als akzeptierte ADR plus Verankerung in Produktquelle, Anforderungen, User Flows, Traceability und offenen Fragen

- Vorteile: eine auffindbare Quelle der Wahrheit; der kleinste
  Mode-Switch-Prototyp ist ohne stille Produktannahme spezifizierbar; die
  Playtest-/Rückrollpflicht aus ADR 007 greift für alle Wechseldetails.
- Nachteile / Risiken: keine bekannt; der Wortlaut wird unverändert
  übernommen, nichts neu erfunden.

## Folgen

- Positiv: jeder E-004-Folgeslice der T-030-Zerlegung kann die Zwei-Modi-
  Verpflichtung als akzeptierte Grundlage voraussetzen; der kleinste
  prüfbare Mode-Switch-Prototyp (Folgeslice T-033) ist implementierbar,
  ohne eine offene Produktfrage still zu beantworten.
- Negativ / Kompromisse: Slices, die nur eine Perspektive nachweisen, sind
  kein Hybrid-Nachweis; der hybride Nachweispfad vergrößert die
  Testmatrizen. Dies ist ausdrücklich gewollt.
- Folgemaßnahmen: der Prototypslice legt die Wechseldetails (Eingabe,
  Übergang, Same-Tick-Regel, Steuerungsabbildung) als versionierten
  Modevertrag mit Alternativen, Playtestkriterien und Rückrollweg fest;
  Q-GAM-010 registriert die noch offene finale Wechsel-Detailregel; die
  Persistenzwahrheit des Modusflags in Save/Load und Replay bleibt
  ausdrücklich einer späteren Erweiterung des Savevertrags vorbehalten und
  wird nicht still als erfüllt behauptet.
- Zeitpunkt für erneute Prüfung: bei jeder geplanten Änderung der
  Produktform, der Simulations- oder Speicherverträge; jede Abweichung
  erfordert eine neue Projektleitungsentscheidung und eine neue Version
  dieser ADR.
- **Rückrollweg:** Diese Übernahme ist ausschließlich durch eine neue
  Projektleitungsentscheidung mit einer neuen ADR-Version rückrollbar; sie
  ändert keinen Code, kein Budget und keinen abgenommenen Vertrag.
