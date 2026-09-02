# T-053 Datenschutz-, Redaktions- und Publikationsplan

**Policy:** `riftward-observability-publication-v1`

**Protokoll:** `riftward-research-observability` 2.0.0

## Datenminimierung

T-053 erfasst nur Daten, die eine praeregistrierte Metrik, Quellenpruefung
oder Reproduktion tragen. Nicht erfasst oder persistiert werden:

- verborgene Gedankengaenge oder Chain-of-Thought,
- Rohprompts, Rohmodellantworten oder vollstaendige Chattranskripte,
- Credentials, Tokens, Cookies, API-Keys, SSH-Material oder Recoverycodes,
- Klarnamen, E-Mail-Adressen, Account-, Billing- oder Request-IDs, sofern ein
  irreversibler Forschungs-Identifier genuegt,
- private Hostnamen, IPs, Routen, absolute Benutzerpfade oder Inventardetails,
- fremde proprietaere Quelltexte, Medien oder vertrauliche Providerinhalte,
- DWH-, Homelab- oder personenbezogene Inhalte ohne direkten Messzweck.

Gespeichert werden semantische Kurzfassungen beobachtbarer Entscheidungsakte,
Rollen und pro Study stabile pseudonyme `actorId`/Agenten-IDs,
repo-relative Pfade, kryptographische Hashes, Gate-/Task-IDs und exakt
benoetigte Messwerte. Die Pseudonyme duerfen keine Klarnamen, Accounts,
Sessions, Hosts, Provider- oder Modellkennungen codieren. Die private
Zuordnungstabelle liegt getrennt, ist nicht Teil des Exports und darf nicht
publiziert werden; ist eine stabile Zuordnung unbelegt, gilt literal `unknown`.

`changedPaths` enthaelt ausschliesslich sortierte repo-relative Pfade. Absolute
Arbeitsraumanteile werden entfernt, bevor der Eventhash entsteht. Kann ein
Pfad nicht sicher auf den gebundenen Repositorybaum bezogen oder ohne
Informationsleck redigiert werden, wird das gesamte Feld literal `unknown`;
die blosse Dateianzahl darf nur bei eigenem aufloesbarem Beleg verbleiben.

## Redaction vor Persistierung

Redaction erfolgt vor Hashbildung der persistierten Forschungsquelle. Ein
Collector darf Geheimnisse nicht erst unredigiert speichern und spaeter
bereinigen. Mindestens werden erkannt und ersetzt:

- Schluessel-/Wertformen fuer Passwort, Secret, Token, Cookie, Authorization,
  API-Key und private Key-Bloecke,
- E-Mail-, IPv4-/IPv6-, Tailnet-/Host- und absolute Pfadmuster,
- bekannte Account-/Billing-/Provider-Identifier,
- Freitextzuweisungen wie `credential=...`,
- Rohargumente von Tools, wenn ein Command-Digest und eine redigierte
  Aktionsklasse genuegen.

Ersatzwerte haben die Form `[REDACTED:<class>]`; der Originalwert wird weder
in einem Nebenlog noch in Git gehalten. Wird dadurch eine Messung nicht mehr
belegbar, ist ihr Wert `unknown` mit `availabilityReason=redacted`.

Redaktionsregeln muessen linear oder mit engem Timeout ausgefuehrt werden.
Eine ungueltige oder nicht terminierende Regel stoppt die Collection
fail-closed.

## Private kanonische Ebene

Die kanonische Forschungsquelle liegt lokal im bestehenden Harness-
Runtime-/Evidenzbereich und folgt dessen Retention- und Integritaetsvertrag.
Sie ist:

- append-only und hashverkettet,
- standardmaessig nicht in Git,
- auf minimal notwendige Rollen beschraenkt,
- ueber ein Quellinventar und SHA-256 gebunden,
- getrennt von T-042-Produkt-/Test-/Gatepfaden,
- nie ein zweiter Writer fuer den beobachteten Task.

WIP-Provenienz-Sidecars duerfen nur die vertraglichen Felder `Task`, `Phase`,
`Agent-Role`, `Run`, `Parent`, `LastGate`, `FailureClass`, `AutonomyState` und
`ResearchSchema` enthalten. Run-/Parent-IDs werden vor einer Public-Ableitung
erneut pseudonymisiert oder `unknown`; der Sidecar enthaelt nie Nachrichtentext,
Credentialwerte oder private Infrastruktur und vermittelt keine `main`-
Autoritaet.

Die bestehende Retention wird durch T-053 nicht gelockert. Loeschung oder
Offsite-Publikation ist kein Bestandteil des Section-0-Auftrags.

## Oeffentliche Ableitung

Ein Public Export wird ausschliesslich aus der bereits redigierten privaten
kanonischen Ebene erzeugt. Er enthaelt standardmaessig:

- Protokollversion und Bundle-Hash,
- Beobachtungs-ID als Pseudonym,
- Evidenzklasse, Ziel-Task-ID und oeffentliche Git-Commit-IDs,
- relative Repositorypfade zu bereits oeffentlichen Quellen,
- Metriken samt `unknown` und Availability-Grund,
- Gate-IDs/-Resultate und Forschungsresultat,
- aggregierte Interventionskategorien ohne Nachrichtentext,
- Threats-to-validity- und Abweichungshinweise,
- `study-manifest.json`, deterministisches `report.md` und das vollstaendige
  nichtrekursive `EXPORT.SHA256` der Public-Ableitung.

Nicht oeffentlich sind exakte private Zeitpunkte, Actor-IDs, interne Run-/
Request-IDs, absolute Pfade, Host-/Netzdetails, Rohlogs, Prompts, Antworten,
Providerreceipts und Accountmetadaten. Oeffentliche Zeitangaben werden auf das
UTC-Datum reduziert; vorregistrierte Dauern duerfen als Werte erscheinen,
wenn sie keine vertrauliche Betriebsinformation offenlegen.

Token- und Kostenwerte werden nur publiziert, wenn:

1. ein exaktes Provider-/Gatewayreceipt vorliegt,
2. keine Account-/Requestidentitaet mitpubliziert wird,
3. Providerbedingungen die Veroeffentlichung erlauben,
4. die Projektleitung die konkrete Datenklasse freigegeben hat.

Andernfalls steht im Public Export literal `unknown`, nicht 0 und nicht eine
Schaetzung. Energie und CO2e werden ohne direkte methodengebundene Messung nie
aus Tokens, Kosten, TDP oder Laufzeit abgeleitet.

## Publikationsgate

Vor jeder externen Veroeffentlichung muessen alle folgenden Punkte `PASS`
sein:

1. Schema-, Hashketten-, innere Evidence-Manifest- und vollstaendige aeussere
   `EXPORT.SHA256`-Pruefung.
2. Secret-/Credential-/PII-/private-Infrastruktur-Scan auf dem exakten
   Public-Baum.
3. Pruefung, dass jede Quelle bereits oeffentlich oder fuer diese
   Veroeffentlichung freigegeben ist.
4. Pruefung, dass `retrospective-derived`, `prospective-observed` und
   `synthetic-test-only` sichtbar getrennt bleiben.
5. Pruefung, dass `unknown` nicht zu 0, Erfolg oder Schaetzung umgeschrieben
   wurde.
6. Pruefung, dass Taskoutcome und Hypothesenresultat getrennt sind.
7. Unabhaengiger Review der Claims und Threats to Validity.
8. Ausdrueckliche Projektleitungsfreigabe fuer die konkrete Publikation.

Ein bestehendes Pages- oder Deploymentgate wird nicht durch T-053 ersetzt
oder abgeschwaecht. Ohne alle acht Nachweise bleibt der Export privat.

## Claim-Regeln

Zulaessig sind Formulierungen wie:

- „In dieser prospektiven Beobachtung ..."
- „Der Wert war nicht beobachtbar und ist `unknown`."
- „Die synthetische Ablation pruefte den Exporter, nicht die Projektleistung."
- „Die retrospektive Ableitung rekonstruiert Git-Fakten, keine vollstaendige
  damalige Lauftelemetrie."

Unzulaessig ohne zusaetzliche Evidenz sind:

- „vollautonom", „kostet nichts", „nachhaltig" oder „optimiert",
- Token-/Kosten-/Interventionszahlen aus Committexten oder Plausibilitaet,
- Verallgemeinerung von P-001 auf andere Tasks oder Projekte,
- Vermischung von WIP, Review und akzeptiertem Stand,
- Darstellung eines synthetischen Passes als realen Run-Erfolg.

## Korrektur und Ruecknahme

Publizierte Forschungsdaten werden nicht still ueberschrieben. Eine
Korrektur:

1. bewahrt den alten Exporthash,
2. nennt betroffene Beobachtungs-/Metrik-IDs,
3. begruendet die Korrektur ohne private Rohdaten,
4. erzeugt eine neue Export- und gegebenenfalls Protokollversion,
5. kennzeichnet den alten Export als supersediert,
6. entfernt bei einem Datenschutzvorfall den oeffentlichen Zugriff sofort,
   ohne die interne forensische Behandlung vorwegzunehmen.

Credential- oder PII-Funde blockieren Publikation. Rotation, Benachrichtigung
oder Loeschung benoetigen die jeweils zustaendige Autorisierung und gehoeren
nicht automatisch zum T-053-Scope.
