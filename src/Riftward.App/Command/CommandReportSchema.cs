using System.Text.Json;
using Riftward.App.Bench;
using Riftward.Session;

namespace Riftward.App.Command;

/// <summary>
/// Maschinenpruefbarer Evidenzvertrag des Kommandoschleifen-Reports
/// (Schemaversionen 2 und 3, NF-007-Linie; T-033 erhoehht die Schemaversion
/// rein additiv um den Modussitzungsblock, die Wechselreaktionsgatekopplung
/// und die Modevertragsbindung; T-034 erhoehht sie bei Opt-in Aktivierung
/// rein additiv um den Erkundungssitzungsblock). Fail-closed: fehlende
/// Pflichtfelder, falsche Typen, erfundene Messwerte ohne Methodenkennung,
/// nicht begruendete unavailable-Kennzeichnungen und unbekannte Felder
/// lassen die Pruefung fehlschlagen. Die Schemaversion 3 ist an die Opt-in
/// Aktivierung gebunden: Ein Bestandsreport (Schemaversion 2) traegt keinen
/// Erkundungsblock, ein aktivierter Report traegt ihn vollstaendig.
/// Die Ausfuehrungsart (headless/interaktiv) waehlt strikte
/// Alternativformen: Headless kann Renderer-/GPU-Werte nicht messen und darf
/// sie nur als unavailable mit Grund ausweisen; der Interaktivmodus muss sie
/// messend ausweisen. Gategekoppelte Felder tragen keine Diagnosemarke; alle
/// uebrigen Messfelder sind verpflichtend gateCoupled=false.
/// </summary>
public static class CommandReportSchema
{
    /// <summary>Schemaversion ohne Erkundungsaktivierung (Bestandsstand, T-032/T-033).</summary>
    public const int VersionWithoutExploration = ExplorationContract.ReportSchemaVersionWithoutExploration;

    /// <summary>Schemaversion mit Erkundungsaktivierung (T-034: rein additiv um explorationSession).</summary>
    public const int VersionWithExploration = ExplorationContract.ReportSchemaVersionWithExploration;

    /// <summary>Aktuelle Schemaversion (T-034: rein additiv um explorationSession).</summary>
    public const int CurrentVersion = ExplorationContract.ReportSchemaVersionWithExploration;

    /// <summary>Schemaversion mit Entscheidungsaktivierung (T-035: rein additiv um decisionSession).</summary>
    public const int VersionWithDecision = DecisionContract.ReportSchemaVersionWithDecision;

    public const string ModeCommandLoop = "kommandoschleife";
    public const string ExecutionHeadless = "headless";
    public const string ExecutionInteractive = "interactive";

    /// <summary>Hex64-Darstellung eines 64-Bit-Zustands-Hashs.</summary>
    internal static readonly HexNode Hex = new();

    /// <summary>Hex256-Darstellung eines Artefakthashs.</summary>
    internal static readonly Sha256HexNode Sha256 = new();

    internal static RObj HeadlessBody { get; } = BuildBody(ExecutionHeadless, VersionWithoutExploration);

    internal static RObj InteractiveBody { get; } = BuildBody(ExecutionInteractive, VersionWithoutExploration);

    internal static RObj HeadlessExplorationBody { get; } = BuildBody(ExecutionHeadless, CurrentVersion);

    internal static RObj InteractiveExplorationBody { get; } = BuildBody(ExecutionInteractive, CurrentVersion);

    internal static RObj HeadlessDecisionBody { get; } = BuildBody(ExecutionHeadless, VersionWithDecision);

    internal static RObj InteractiveDecisionBody { get; } = BuildBody(ExecutionInteractive, VersionWithDecision);

    /// <summary>
    /// Versions- und Ausführungsdispatch: Die Schemaversion
    /// <see cref="VersionWithoutExploration"/> (Bestandsstand) toleriert
    /// keinen Erkundungsblock, die Schemaversion <see cref="CurrentVersion"/>
    /// verlangt ihn vollstaendig; die Schemaversion
    /// <see cref="VersionWithDecision"/> verlangt zusaetzlich den
    /// Entscheidungssitzungsblock vollstaendig (T-035). Alle Versionen
    /// waehlen strikt zwischen headless und interaktiv.
    /// </summary>
    private sealed class SchemaVersionDispatch : ReportNode
    {
        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (!element.TryGetProperty("schemaVersion", out var version)
                || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out var schemaVersion))
            {
                errors.Add("$.schemaVersion: ganzzahlige Schemaversion erwartet.");
                return;
            }

            if (schemaVersion is not (VersionWithoutExploration or CurrentVersion or VersionWithDecision))
            {
                errors.Add($"$.schemaVersion: Wert ausserhalb der erlaubten Schemaversionen; {VersionWithoutExploration}, {CurrentVersion} oder {VersionWithDecision} erwartet.");
                return;
            }

            if (!element.TryGetProperty("executionMode", out var mode)
                || mode.ValueKind != JsonValueKind.String)
            {
                errors.Add("$.executionMode: Ausfuehrungsart erwartet.");
                return;
            }

            ReportNode? body = (schemaVersion, mode.GetString()) switch
            {
                (VersionWithoutExploration, ExecutionHeadless) => HeadlessBody,
                (VersionWithoutExploration, ExecutionInteractive) => InteractiveBody,
                (CurrentVersion, ExecutionHeadless) => HeadlessExplorationBody,
                (CurrentVersion, ExecutionInteractive) => InteractiveExplorationBody,
                (VersionWithDecision, ExecutionHeadless) => HeadlessDecisionBody,
                (VersionWithDecision, ExecutionInteractive) => InteractiveDecisionBody,
                _ => null,
            };

            if (body is null)
            {
                errors.Add("$.executionMode: unbekannte Ausfuehrungsart.");
                return;
            }

            body.Check(path, element, errors);

            // Closed shapes und Wertebereiche genuegen fuer die additiven
            // T-034-Felder nicht: Der Report muss auch seine relationalen
            // Aussagen beweisen. Andernfalls koennten einzeln wohlgeformte,
            // aber untereinander widerspruechliche Protokoll-, Fortschritts-
            // und Darstellungswerte als Evidenz passieren.
            ValidatePresentationMeasurementRelations(path, element, errors);

            if (schemaVersion == CurrentVersion)
            {
                ValidateExplorationRelations(path, element, errors);
            }

            if (schemaVersion == VersionWithDecision)
            {
                ValidateExplorationRelations(path, element, errors);
                ValidateDecisionRelations(path, element, errors);
            }
        }
    }

    /// <summary>Gesamtschema des von CommandLoopRunner geschriebenen Reports.</summary>
    internal static ReportNode Root { get; } = new SchemaVersionDispatch();

    private static RObj BuildBody(string executionMode, int version)
    {
        var fields = new List<(string Name, ReportNode Node)>
        {
            ("schemaVersion", new RInt(version, version)),
            ("mode", new RLit(ModeCommandLoop)),
            ("executionMode", new RLit(executionMode)),
            ("command", new RStr()),
            ("scenario", new RObj(
                ("id", new RLit(SessionContract.ScenarioId)),
                ("seed", new RInt(0, uint.MaxValue)),
                ("tickRateHz", new RInt(Riftward.Simulation.SimulationContract.TickRateHz, Riftward.Simulation.SimulationContract.TickRateHz)),
                ("agentCount", new RInt(Riftward.Simulation.SimulationContract.AgentCount, Riftward.Simulation.SimulationContract.AgentCount)),
                ("worldId", new RLit(Riftward.Simulation.SimulationContract.WorldId)),
                ("content", new RLit(SessionContract.ContentId)))),
            ("commandContract", new RObj(
                ("document", new RLit(SessionContract.DocumentPath)),
                ("version", new RLit(SessionContract.ContractVersion)),
                ("scriptFormat", ScriptFormat()),
                ("selectionModel", new RLit(SessionContract.SelectionModelId)),
                ("cameraModel", new RLit(SessionContract.CameraModelId)),
                ("diagnosticOnlyReplayDisclaimer", new RBool(true)),
                ("modeContract", new RObj(
                    ("document", new RLit(ModeContract.DocumentPath)),
                    ("version", new RLit(ModeContract.ContractVersion)))))),
            ("modeSession", ModeSessionBody()),
        };

        // Rein additive Schemaversion 3 (T-034, Erkundungsvertrag
        // Abschnitt 6): ausschließlich neue Felder, keine Umdeutung,
        // Umbenennung oder Entfernung bestehender Felder; der Block existiert
        // ausschließlich in der aktivierten Schemaversion und ist dort
        // Pflicht.
        if (version == CurrentVersion)
        {
            fields.Add(("explorationSession", ExplorationSessionBody()));
        }

        // Rein additive Schemaversion 4 (T-035, Entscheidungsvertrag
        // Abschnitt 8): ausschließlich neue Felder, keine Umdeutung;
        // vertraglich an die Erkundungsaktivierung gekoppelt, daher traegt
        // die Entscheidungs-Schemaversion stets auch den Erkundungsblock.
        if (version == VersionWithDecision)
        {
            fields.Add(("explorationSession", ExplorationSessionBody()));
            fields.Add((DecisionContract.ReportBlockId, DecisionSessionBody()));
        }

        fields.AddRange(new List<(string Name, ReportNode Node)>
        {
            ("simulationContract", new RObj(
                ("document", new RLit(Riftward.Simulation.SimulationContract.DocumentPath)),
                ("version", new RLit(Riftward.Simulation.SimulationContract.ContractVersion)),
                ("numericModel", new RLit(Riftward.Simulation.SimulationContract.NumericModelId)),
                ("hashAlgorithm", new RLit(Riftward.Simulation.SimulationContract.HashAlgorithmId)),
                ("allocationLimitBytesPerWarmTick", new RInt(0)))),
            ("inputScript", new RObj(
                ("scriptSha256", Sha256),
                ("intentPlanHash", Hex),
                ("horizonTicks", new RInt(1)),
                ("warmupTicks", new RInt(30)),
                ("intentsTotal", new RInt(0)),
                ("appliedTotal", new RInt(0)),
                ("rejectedTotal", new RInt(0)),
                ("emptyPointDeselects", new RInt(0)),
                ("moveWithoutSelectionRejects", new RInt(0)),
                ("noZoneRejects", new RInt(0)),
                ("kernelCommandsTotal", new RInt(0)))),
            ("startedAtUtc", new RStr()),
            ("finishedAtUtc", new RStr()),
            ("environment", new RObj(
                ("os", new RObj(("type", new RStr()), ("kernelRelease", new RStr()))),
                ("cpu", new RObj(("model", new RStr()))),
                ("rid", new RLit("linux-x64")),
                ("commit", new RStr()),
                ("buildMode", new RStr()),
                ("display", Display(executionMode)),
                ("pins", new RArr(new RObj(
                    ("id", new RStr()),
                    ("refType", new RStr()),
                    ("ref", new RStr()),
                    ("commit", new RStr()),
                    ("sourceSha256", new RStr()),
                    ("licenseSpdx", new RStr())), 4)))),
            ("measurement", new RObj(
                ("warmupTicks", new RInt(30)),
                ("sampleTicks", new RInt(1)),
                ("ticksExecuted", new RInt(2)),
                ("hashSampleIntervalTicks", new RInt(1)),
                ("rssSampleIntervalTicks", new RInt(1)),
                ("windowCompleted", new RBool()))),
            ("metrics", Metrics(executionMode)),
            ("stateHashChain", new RObj(
                ("unit", new RLit("hex64")),
                ("method", new RLit(Riftward.Simulation.SimulationContract.HashAlgorithmId)),
                ("start", Hex),
                ("intervalSampleTicks", new RArr(new RInt(0), 1)),
                ("intervalHashes", new RArr(Hex, 1)),
                ("end", Hex))),
            ("gate", new RObj(
                ("limits", new RObj(
                    ("p99TickTimeHardLimitMs", new RNum(true)),
                    ("p99TickTimeTargetMs", new RNum(true)),
                    ("allocationsPerWarmTickBytesMax", new RInt(0)),
                    ("reactionHardLimitTicks", new RInt(SessionContract.ReactionHardLimitTicks, SessionContract.ReactionHardLimitTicks)),
                    ("reactionTargetTicks", new RInt(SessionContract.ReactionTargetTicks, SessionContract.ReactionTargetTicks)),
                    ("runtimeShaderCompilationAllowed", new RBool(false)),
                    ("switchReactionHardLimitTicks", new RInt(ModeContract.SwitchReactionHardLimitTicks, ModeContract.SwitchReactionHardLimitTicks)),
                    ("switchReactionTargetTicks", new RInt(ModeContract.SwitchReactionTargetTicks, ModeContract.SwitchReactionTargetTicks)))),
                ("stateChainSelfConsistency", ChainConsistencyAlternative()),
                ("switchReaction", SwitchReactionAlternative()),
                ("pass", new RBool()),
                ("tickTimeTargetMet", new RBool()),
                ("reactionTargetMet", new RBool()),
                ("violations", new RArr(new RStr())))),
            ("openQuestions", new RObj(
                ("qtec004", new RLit("open")),
                ("qtec006", new RLit("open")),
                ("qtec010", new RLit("open")),
                ("qgam001", new RLit("open")),
                ("qgam002", new RLit("open")),
                ("qgam003", new RLit("open")),
                ("qgam004", new RLit("open")),
                ("qgam005", new RLit("open")),
                ("qgam006", new RLit("open")),
                ("qgam007", new RLit("open")),
                ("qgam010", new RLit("open")),
                ("qnar002", new RLit("open")))),
            ("profiles", new RArr(new RObj(
                ("id", new RStr()),
                ("status", new RStr()),
                ("boundReferenceClass", new RNullableStr()),
                ("reason", new RStr())), 3)),
            ("baseline", new RObj(
                ("classification", new RLit("diagnostic-developer-workstation")),
                ("protocol", new RLit("qops001-2026-08-24")))),
            ("frameEvidence", new FrameEvidenceAlternative()),
            ("exitCode", new RInt(int.MinValue, int.MaxValue)),
        });

        return new RObj(fields.ToArray());
    }

    /// <summary>
    /// Erkundungssitzungsblock (T-034, Erkundungsvertrag Abschnitt 7): bei
    /// Aktivierung vertraglich gebunden — Kennungen, Landmarkenmenge in
    /// fester Zonenordnung mit an die Kernelgeometrie gebundenen Ankern,
    /// Aufsuchprotokoll in kanonischer Registrierungsfolge, Fortschritt/
    /// Abschluss, die versionierte Nichtpersistenzaussage und die
    /// fensterpflichtigen Ausweise. Rein diagnostisch
    /// (gateCoupled=false); kein Feld koppelt an ein Gate oder einen
    /// Budgetwert, und keine Exitcodebedeutung entsteht.
    /// </summary>
    private static RObj ExplorationSessionBody() => new(
        ("contract", new RObj(
            ("document", new RLit(ExplorationContract.DocumentPath)),
            ("version", new RLit(ExplorationContract.ContractVersion)))),
        ("activationId", new RLit(ExplorationContract.ActivationId)),
        ("landmarkModel", new RLit(ExplorationContract.LandmarkModelId)),
        ("visitRule", new RLit(ExplorationContract.VisitRuleId)),
        ("counterModel", new RLit(ExplorationContract.CounterModelId)),
        ("landmarks", new LandmarksNode()),
        ("visitProtocol", new RArr(VisitEvent())),
        ("progress", new RObj(
            ("visitedCount", new RInt(0, Riftward.Simulation.NavWorld.ZoneCount)),
            ("landmarkCount", new RInt(Riftward.Simulation.NavWorld.ZoneCount, Riftward.Simulation.NavWorld.ZoneCount)),
            ("completed", new RBool()),
            ("gateCoupled", new RBool(false)))),
        ("persistence", new RObj(
            ("statementId", new RLit(ExplorationContract.NotPersistedStatementId)),
            ("persisted", new RBool(false)),
            ("saveLoad", new RLit("not-continued")),
            ("replay", new RLit("not-continued")),
            ("gateCoupled", new RBool(false)))),
        ("gateCoupled", new RBool(false)),
        ("hud", ExplorationHudAlternative()),
        ("landmarkChannel", ExplorationChannelAlternative()));

    /// <summary>
    /// Landmarkenbindung (T-034, Vertrag Abschnitte 2 und 7): exakt eine
    /// Landmarke je Vertragszone in fester Zonenordnung; der Anker liegt in
    /// der eigenen Zone und ist betretbar (Kernelbindung IsInsideZone/
    /// IsWalkable). Der Schemator haelt die Landmarkenmenge damit an die
    /// gebundene Vertragsgeometrie.
    /// </summary>
    private sealed class LandmarksNode : ReportNode
    {
        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                errors.Add($"{path}: Landmarken-Array erwartet.");
                return;
            }

            var count = 0;

            foreach (var item in element.EnumerateArray())
            {
                if (count >= Riftward.Simulation.NavWorld.ZoneCount)
                {
                    errors.Add($"{path}: hoechstens {Riftward.Simulation.NavWorld.ZoneCount} Landmarken (je Vertragszone eine) erwartet.");
                    return;
                }

                var shape = LandmarkShape();
                shape.Check($"{path}[{count}]", item, errors);

                if (item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty("zoneIndex", out var zoneIndex)
                    && zoneIndex.TryGetInt32(out var zoneIndexValue)
                    && zoneIndexValue != count)
                {
                    errors.Add($"{path}[{count}].zoneIndex: Landmarken muessen in fester Zonenordnung erscheinen.");
                }

                if (item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty("zoneIndex", out zoneIndex)
                    && zoneIndex.TryGetInt32(out zoneIndexValue)
                    && zoneIndexValue >= 0
                    && zoneIndexValue < Riftward.Simulation.NavWorld.ZoneCount
                    && item.TryGetProperty("anchorTileX", out var anchorTileX)
                    && anchorTileX.TryGetInt32(out var anchorTileXValue)
                    && item.TryGetProperty("anchorTileY", out var anchorTileY)
                    && anchorTileY.TryGetInt32(out var anchorTileYValue))
                {
                    if (!Riftward.Simulation.NavWorld.IsInsideZone(
                        zoneIndexValue, anchorTileXValue, anchorTileYValue))
                    {
                        errors.Add($"{path}[{count}]: Ankerkachel liegt nicht in ihrer Vertragszone.");
                    }

                    if (!Riftward.Simulation.NavWorld.IsWalkable(anchorTileXValue, anchorTileYValue))
                    {
                        errors.Add($"{path}[{count}]: Ankerkachel ist in der gebundenen Vertragswelt nicht betretbar.");
                    }

                    var expected = ExplorationAnchors.DeriveLandmarks()[zoneIndexValue];

                    if (anchorTileXValue != expected.AnchorTileX
                        || anchorTileYValue != expected.AnchorTileY)
                    {
                        errors.Add($"{path}[{count}]: Ankerkachel widerspricht der kanonischen zeilenmajoritischen Ableitung.");
                    }
                }

                count++;
            }

            if (count != Riftward.Simulation.NavWorld.ZoneCount)
            {
                errors.Add($"{path}: genau {Riftward.Simulation.NavWorld.ZoneCount} Landmarken (je Vertragszone eine) in fester Zonenordnung erwartet.");
            }
        }

        private static RObj LandmarkShape() => new(
            ("zoneIndex", new RInt(0, Riftward.Simulation.NavWorld.ZoneCount - 1)),
            ("anchorTileX", new RInt(0, Riftward.Simulation.NavWorld.TilesX - 1)),
            ("anchorTileY", new RInt(0, Riftward.Simulation.NavWorld.TilesY - 1)),
            ("walkable", new RBool(true)));
    }

    private static RObj VisitEvent() => new(
        ("evaluationBoundaryTick", new RInt(0)),
        ("zoneIndex", new RInt(0, Riftward.Simulation.NavWorld.ZoneCount - 1)),
        ("mode", ModeName()),
        ("visitOrder", new RInt(1, Riftward.Simulation.NavWorld.ZoneCount)),
        ("gateCoupled", new RBool(false)));

    /// <summary>
    /// Relationale T-034-Bindung: eindeutige Zonen, strikt kanonische
    /// Besuchsreihenfolge und -zeit, ausschliesslich persoenliche
    /// Registrierung sowie identische Zaehler-/Abschlussaussagen in
    /// Protokoll, Fortschritt und den gemessenen Darstellungskaenaelen.
    /// </summary>
    private static void ValidateExplorationRelations(
        string path,
        JsonElement root,
        List<string> errors)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("explorationSession", out var exploration)
            || exploration.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var explorationPath = $"{path}.explorationSession";
        var protocolCount = -1;

        if (exploration.TryGetProperty("visitProtocol", out var protocol)
            && protocol.ValueKind == JsonValueKind.Array)
        {
            protocolCount = protocol.GetArrayLength();

            if (protocolCount > Riftward.Simulation.NavWorld.ZoneCount)
            {
                errors.Add($"{explorationPath}.visitProtocol: hoechstens {Riftward.Simulation.NavWorld.ZoneCount} eindeutige Registrierungen erwartet.");
            }

            var seenZones = new bool[Riftward.Simulation.NavWorld.ZoneCount];
            long previousBoundaryTick = -1;
            var index = 0;

            foreach (var visit in protocol.EnumerateArray())
            {
                if (visit.ValueKind == JsonValueKind.Object)
                {
                    if (visit.TryGetProperty("visitOrder", out var visitOrder)
                        && visitOrder.TryGetInt64(out var visitOrderValue)
                        && visitOrderValue != index + 1L)
                    {
                        errors.Add($"{explorationPath}.visitProtocol[{index}].visitOrder: fortlaufender Wert {index + 1} erwartet.");
                    }

                    if (visit.TryGetProperty("mode", out var mode)
                        && mode.ValueKind == JsonValueKind.String
                        && !string.Equals(mode.GetString(), ModeContract.ModePersonalId, StringComparison.Ordinal))
                    {
                        errors.Add($"{explorationPath}.visitProtocol[{index}].mode: Registrierung ist ausschliesslich im persoenlichen Modus zulaessig.");
                    }

                    if (visit.TryGetProperty("zoneIndex", out var zoneIndex)
                        && zoneIndex.TryGetInt32(out var zoneIndexValue)
                        && zoneIndexValue >= 0
                        && zoneIndexValue < seenZones.Length)
                    {
                        if (seenZones[zoneIndexValue])
                        {
                            errors.Add($"{explorationPath}.visitProtocol[{index}].zoneIndex: Landmarkenzone wurde mehrfach registriert.");
                        }

                        seenZones[zoneIndexValue] = true;
                    }

                    if (visit.TryGetProperty("evaluationBoundaryTick", out var boundaryTick)
                        && boundaryTick.TryGetInt64(out var boundaryTickValue))
                    {
                        if (index > 0 && boundaryTickValue <= previousBoundaryTick)
                        {
                            errors.Add($"{explorationPath}.visitProtocol[{index}].evaluationBoundaryTick: strikt steigende Registrierungsgrenzen erwartet.");
                        }

                        previousBoundaryTick = boundaryTickValue;
                    }
                }

                index++;
            }
        }

        if (!exploration.TryGetProperty("progress", out var progress)
            || progress.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        int? visitedCount = null;
        bool? completed = null;

        if (progress.TryGetProperty("visitedCount", out var visited)
            && visited.TryGetInt32(out var visitedValue))
        {
            visitedCount = visitedValue;

            if (protocolCount >= 0 && visitedValue != protocolCount)
            {
                errors.Add($"{explorationPath}.progress.visitedCount: Wert muss der Laenge des Aufsuchprotokolls entsprechen.");
            }
        }

        if (progress.TryGetProperty("completed", out var completedElement)
            && completedElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            completed = completedElement.GetBoolean();

            if (visitedCount is { } count
                && completed.Value != (count == Riftward.Simulation.NavWorld.ZoneCount))
            {
                errors.Add($"{explorationPath}.progress.completed: muss genau visitedCount == landmarkCount abbilden.");
            }
        }

        ValidateMeasuredExplorationFields(
            explorationPath, exploration, visitedCount, completed, errors);
    }

    private static void ValidateMeasuredExplorationFields(
        string path,
        JsonElement exploration,
        int? visitedCount,
        bool? completed,
        List<string> errors)
    {
        if (exploration.TryGetProperty("hud", out var hud)
            && hud.ValueKind == JsonValueKind.Object
            && hud.TryGetProperty("measured", out var hudMeasured)
            && hudMeasured.ValueKind == JsonValueKind.True
            && hud.TryGetProperty("fields", out var hudFields)
            && hudFields.ValueKind == JsonValueKind.Object)
        {
            if (visitedCount is { } count
                && hudFields.TryGetProperty("visitedCount", out var hudVisited)
                && hudVisited.TryGetInt32(out var hudVisitedValue)
                && hudVisitedValue != count)
            {
                errors.Add($"{path}.hud.fields.visitedCount: widerspricht dem gebundenen Fortschritt.");
            }

            if (completed is { } isCompleted
                && hudFields.TryGetProperty("completed", out var hudCompleted)
                && hudCompleted.ValueKind is JsonValueKind.True or JsonValueKind.False
                && hudCompleted.GetBoolean() != isCompleted)
            {
                errors.Add($"{path}.hud.fields.completed: widerspricht dem gebundenen Abschlussstatus.");
            }
        }

        if (exploration.TryGetProperty("landmarkChannel", out var channel)
            && channel.ValueKind == JsonValueKind.Object
            && channel.TryGetProperty("measured", out var channelMeasured)
            && channelMeasured.ValueKind == JsonValueKind.True
            && channel.TryGetProperty("fields", out var channelFields)
            && channelFields.ValueKind == JsonValueKind.Object
            && visitedCount is { } registeredCount
            && channelFields.TryGetProperty("registeredCount", out var registered)
            && registered.TryGetInt32(out var registeredValue)
            && registeredValue != registeredCount)
        {
            errors.Add($"{path}.landmarkChannel.fields.registeredCount: widerspricht dem gebundenen Fortschritt.");
        }
    }

    /// <summary>
    /// Entscheidungssitzungsblock (T-035, Entscheidungsvertrag Abschnitt 8):
    /// bei Aktivierung vertraglich gebunden — Vertrags- und Modellkennungen,
    /// Angebot (Optionszonen bzw. ehrlicher Nichtöffnungsgrund), Entscheidung
    /// mit Modus und gewählter Zone, Folge mit Ankunft, Abweisungszähler der
    /// Auswertungsordnung, versionierte Nichtpersistenzaussage und die
    /// fensterpflichtigen Darstellungsausweise. Rein diagnostisch
    /// (gateCoupled=false); kein Feld koppelt an ein Gate oder einen
    /// Budgetwert, und keine Exitcodebedeutung entsteht.
    /// </summary>
    private static RObj DecisionSessionBody() => new(
        ("contract", new RObj(
            ("document", new RLit(DecisionContract.DocumentPath)),
            ("version", new RLit(DecisionContract.ContractVersion)))),
        ("activationId", new RLit(DecisionContract.ActivationId)),
        ("offerRule", new RLit(DecisionContract.OfferRuleId)),
        ("optionsModel", new RLit(DecisionContract.OptionsModelId)),
        ("choiceScopingRule", new RLit(DecisionContract.ChoiceScopingRuleId)),
        ("followUpRule", new RLit(DecisionContract.FollowUpRuleId)),
        ("arrivalRule", new RLit(DecisionContract.ArrivalRuleId)),
        ("offer", new FlagAlternative("opened",
        [
            new RObj(
                ("opened", new RBool(true)),
                ("boundaryTick", new RInt(0)),
                ("optionZoneA", ZoneIndexNode()),
                ("optionZoneB", ZoneIndexNode())),
            new RObj(
                ("opened", new RBool(false)),
                ("boundaryTick", new RInt((int)DecisionTelemetry.UnsetBoundaryTick)),
                ("optionZoneA", new RInt(DecisionTelemetry.UnsetZoneIndex)),
                ("optionZoneB", new RInt(DecisionTelemetry.UnsetZoneIndex)),
                ("reason", new LiteralAlternative([DecisionContract.OfferNotOpenedReason]))),
        ])),
        ("decision", new FlagAlternative("decided",
        [
            new RObj(
                ("decided", new RBool(true)),
                ("boundaryTick", new RInt(0)),
                ("choice", new LiteralAlternative(
                    [DecisionContract.ChoiceOptionAId, DecisionContract.ChoiceOptionBId])),
                ("mode", new LiteralAlternative([ModeContract.ModePersonalId])),
                ("optionZone", ZoneIndexNode())),
            new RObj(
                ("decided", new RBool(false)),
                ("boundaryTick", new RInt((int)DecisionTelemetry.UnsetBoundaryTick)),
                ("choice", new RNullableStr()),
                ("mode", new RNullableStr()),
                ("optionZone", new RInt(DecisionTelemetry.UnsetZoneIndex))),
        ])),
        ("followUp", new RObj(
            ("zoneIndex", new RInt(DecisionTelemetry.UnsetZoneIndex, Riftward.Simulation.NavWorld.ZoneCount - 1)),
            ("completed", new RBool()),
            ("arrivalBoundaryTick", new RInt((int)DecisionTelemetry.UnsetBoundaryTick)),
            ("gateCoupled", new RBool(false)))),
        ("rejections", new RObj(
            ("beforeOffer", new RInt(0)),
            ("inStrategicMode", new RInt(0)),
            ("afterDecision", new RInt(0)),
            ("gateCoupled", new RBool(false)))),
        ("persistence", new RObj(
            ("statementId", new RLit(DecisionContract.NotPersistedStatementId)),
            ("persisted", new RBool(false)),
            ("saveLoad", new RLit("not-continued")),
            ("replay", new RLit("not-continued")),
            ("gateCoupled", new RBool(false)))),
        ("gateCoupled", new RBool(false)),
        ("hud", DecisionHudAlternative()),
        ("followUpChannel", DecisionChannelAlternative()));

    /// <summary>Zonenknoten inklusive vertraglichem Sentinel.</summary>
    private static RInt ZoneIndexNode() =>
        new(DecisionTelemetry.UnsetZoneIndex, Riftward.Simulation.NavWorld.ZoneCount - 1);

    /// <summary>
    /// Titel-HUD-Ausweis der Entscheidung (Vertrag Abschnitte 6 und 8): im
    /// Interaktivmodus messend mit Angebots-, Options-, Folgen- und
    /// Abschlusszustand; headless und in unvollständigen Läufen ausdrücklich
    /// nicht gemessen mit maschinenlesbarem Grund statt stiller Behauptung.
    /// </summary>
    private static MeasuredAlternative DecisionHudAlternative() =>
        new(
        [
            new RObj(
                ("measured", new RBool(true)),
                ("kind", new RLit(DecisionContract.HudModelId)),
                ("fields", new RObj(
                    ("offerOpened", new RBool()),
                    ("optionZoneA", ZoneIndexNode()),
                    ("optionZoneB", ZoneIndexNode()),
                    ("followUpZoneIndex", ZoneIndexNode()),
                    ("followUpCompleted", new RBool())))),
            new RObj(
                ("measured", new RBool(false)),
                ("kind", new RLit(DecisionContract.HudModelId)),
                ("reason", new RStr())),
        ]);

    /// <summary>
    /// Folgezielkanal-Bindung (T-035, Vertrag Abschnitte 6 und 8): im
    /// Interaktivmodus messend ausgewiesen; headless ausdrücklich nicht
    /// gemessen mit maschinenlesbarem Grund statt stiller Behauptung.
    /// </summary>
    private static MeasuredAlternative DecisionChannelAlternative() =>
        new(
        [
            new RObj(
                ("measured", new RBool(true)),
                ("kind", new RLit(DecisionContract.FollowUpChannelModelId)),
                ("fields", new RObj(
                    ("zoneIndex", ZoneIndexNode()),
                    ("active", new RBool())))),
            new RObj(
                ("measured", new RBool(false)),
                ("kind", new RLit(DecisionContract.FollowUpChannelModelId)),
                ("reason", new RStr())),
        ]);

    /// <summary>Alternativnode, der auf einem booleschen Feld mit festem Namen dispatcht.</summary>
    private sealed class FlagAlternative(string flagName, IReadOnlyList<RObj> shapes) : ReportNode
    {
        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{path}: Objekt erwartet.");
                return;
            }

            if (!element.TryGetProperty(flagName, out var flag)
                || flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                errors.Add($"{path}.{flagName}: boolesche Kennung erwartet.");
                return;
            }

            shapes[flag.GetBoolean() ? 0 : 1].Check(path, element, errors);
        }
    }

    /// <summary>
    /// Relationale T-035-Bindung (Entscheidungsvertrag Abschnitt 8,
    /// fail-closed): die Angebotszonen sind verschieden; die gewählte Zone ist
    /// eine Angebotszone und der Wahl zugeordnet; die Folgenzone ist die
    /// gewählte Zone; die Ankunft liegt an oder nach der Entscheidungsgrenze;
    /// Abschluss und Ankunftsgrenze tragen dieselbe Aussage; ohne Angebot
    /// gibt es keine Entscheidung und keine Folge; ohne Entscheidung gibt es
    /// keine gewählte Zone und keine Folge (Sentinel-Wahrheit vor der Wahl).
    /// </summary>
    private static void ValidateDecisionRelations(
        string path,
        JsonElement root,
        List<string> errors)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("decisionSession", out var decisionSession)
            || decisionSession.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var decisionPath = $"{path}.decisionSession";

        if (!decisionSession.TryGetProperty("offer", out var offer)
            || offer.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var offerOpened = ReadBool(offer, "opened");
        var optionZoneA = ReadInt(offer, "optionZoneA");
        var optionZoneB = ReadInt(offer, "optionZoneB");

        if (offerOpened == true
            && optionZoneA is { } zoneA
            && optionZoneB is { } zoneB
            && zoneA == zoneB)
        {
            errors.Add($"{decisionPath}.offer: die beiden Optionszonen muessen verschieden sein.");
        }

        if (!decisionSession.TryGetProperty("decision", out var decision)
            || decision.ValueKind != JsonValueKind.Object
            || !decisionSession.TryGetProperty("followUp", out var followUp)
            || followUp.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var decided = ReadBool(decision, "decided");
        var decisionBoundary = ReadInt(decision, "boundaryTick");
        var choice = decision.TryGetProperty("choice", out var choiceElement)
            && choiceElement.ValueKind == JsonValueKind.String
            ? choiceElement.GetString()
            : null;
        var chosenZone = ReadInt(decision, "optionZone");
        var followUpZone = ReadInt(followUp, "zoneIndex");
        var followUpCompleted = ReadBool(followUp, "completed");
        var arrivalBoundary = ReadInt(followUp, "arrivalBoundaryTick");

        if (offerOpened == false && decided == true)
        {
            errors.Add($"{decisionPath}.decision: ohne Angebot gibt es keine Entscheidung.");
        }

        // Abschluss- und Ankunftsaussage ist unabhaengig vom Entscheidungsstand
        // gebunden (Entscheidungsvertrag Abschnitt 8): ohne Abschluss keine
        // Ankunftsgrenze, mit Abschluss die zugehoerige Grenze.
        if (followUpCompleted != (arrivalBoundary >= 0))
        {
            errors.Add($"{decisionPath}.followUp: Abschluss und Ankunftsgrenze muessen dieselbe Aussage tragen.");
        }

        // Ohne Angebot existiert weder Folge noch Abschluss (unabhaengig vom
        // Entscheidungsstand; Vertrag Abschnitt 8).
        if (offerOpened == false
            && ((followUpZone is { } orphanZone && orphanZone >= 0)
                || followUpCompleted == true
                || (arrivalBoundary is { } orphanArrival && orphanArrival >= 0)))
        {
            errors.Add($"{decisionPath}.followUp: ohne Angebot gibt es keine Folge.");
        }

        // Ohne Entscheidung existiert weder gewaehlte Zone noch Folge
        // (Sentinel-Wahrheit vor der Wahl; Vertrag Abschnitte 5 und 8).
        if (decided == false
            && ((chosenZone is { } unchosenZone && unchosenZone >= 0)
                || (followUpZone is { } prematureZone && prematureZone >= 0)
                || followUpCompleted == true
                || (arrivalBoundary is { } prematureArrival && prematureArrival >= 0)))
        {
            errors.Add($"{decisionPath}.followUp: ohne Entscheidung gibt es keine gewaehlte Zone und keine Folge.");
        }

        if (decided != true)
        {
            return;
        }

        if (choice == DecisionContract.ChoiceOptionAId
            && optionZoneA is { } boundA
            && chosenZone is { } pickedA
            && pickedA != boundA)
        {
            errors.Add($"{decisionPath}.decision.optionZone: Wahl a muss die Optionszone A gewaehlt haben.");
        }

        if (choice == DecisionContract.ChoiceOptionBId
            && optionZoneB is { } boundB
            && chosenZone is { } pickedB
            && pickedB != boundB)
        {
            errors.Add($"{decisionPath}.decision.optionZone: Wahl b muss die Optionszone B gewaehlt haben.");
        }

        if (chosenZone is { } picked
            && followUpZone is { } followZone
            && picked != followZone)
        {
            errors.Add($"{decisionPath}.followUp.zoneIndex: die Folgenzone ist die gewaehlte Zone.");
        }

        if (arrivalBoundary is { } arrival
            && decisionBoundary is { } decidedAt
            && arrival >= 0
            && arrival < decidedAt)
        {
            errors.Add($"{decisionPath}.followUp.arrivalBoundaryTick: die Ankunft liegt an oder nach der Entscheidungsgrenze.");
        }
    }

    private static bool? ReadBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static int? ReadInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    /// <summary>
    /// Ein visueller Kanal darf nur dann als gemessen gelten, wenn ein
    /// interaktives Fenster sein Messfenster tatsaechlich abgeschlossen hat.
    /// Headless- und Early-Quit-Reports muessen fail-closed unavailable
    /// ausweisen, statt allein aus executionMode eine Sichtbarkeit abzuleiten.
    /// </summary>
    private static void ValidatePresentationMeasurementRelations(
        string path,
        JsonElement root,
        List<string> errors)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("executionMode", out var executionMode)
            || executionMode.ValueKind != JsonValueKind.String
            || !root.TryGetProperty("measurement", out var measurement)
            || measurement.ValueKind != JsonValueKind.Object
            || !measurement.TryGetProperty("windowCompleted", out var windowCompleted)
            || windowCompleted.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return;
        }

        var shouldBeMeasured = string.Equals(
                executionMode.GetString(), ExecutionInteractive, StringComparison.Ordinal)
            && windowCompleted.GetBoolean();

        ValidateMeasuredFlag(
            path, root, "modeSession", "hud", shouldBeMeasured, errors);

        if (root.TryGetProperty("explorationSession", out _))
        {
            ValidateMeasuredFlag(
                path, root, "explorationSession", "hud", shouldBeMeasured, errors);
            ValidateMeasuredFlag(
                path, root, "explorationSession", "landmarkChannel", shouldBeMeasured, errors);
        }

        if (root.TryGetProperty("decisionSession", out _))
        {
            ValidateMeasuredFlag(
                path, root, "decisionSession", "hud", shouldBeMeasured, errors);
            ValidateMeasuredFlag(
                path, root, "decisionSession", "followUpChannel", shouldBeMeasured, errors);
        }
    }

    private static void ValidateMeasuredFlag(
        string path,
        JsonElement root,
        string blockName,
        string channelName,
        bool expected,
        List<string> errors)
    {
        if (root.TryGetProperty(blockName, out var block)
            && block.ValueKind == JsonValueKind.Object
            && block.TryGetProperty(channelName, out var channel)
            && channel.ValueKind == JsonValueKind.Object
            && channel.TryGetProperty("measured", out var measured)
            && measured.ValueKind is JsonValueKind.True or JsonValueKind.False
            && measured.GetBoolean() != expected)
        {
            errors.Add($"{path}.{blockName}.{channelName}.measured: erwarteter Wert {expected.ToString().ToLowerInvariant()} fuer Ausfuehrungsart und Fensterabschluss.");
        }
    }

    /// <summary>
    /// Titel-HUD-Ausweis der Erkundung (Vertrag Abschnitte 5 und 7): im
    /// Interaktivmodus messend mit Fortschritt und Abschluss; headless und
    /// in unvollständigen Läufen ausdrücklich nicht gemessen mit
    /// maschinenlesbarem Grund statt stiller Behauptung.
    /// </summary>
    private static MeasuredAlternative ExplorationHudAlternative() =>
        new(
        [
            new RObj(
                ("measured", new RBool(true)),
                ("kind", new RLit(ExplorationContract.HudModelId)),
                ("fields", new RObj(
                    ("visitedCount", new RInt(0, Riftward.Simulation.NavWorld.ZoneCount)),
                    ("landmarkCount", new RInt(Riftward.Simulation.NavWorld.ZoneCount, Riftward.Simulation.NavWorld.ZoneCount)),
                    ("completed", new RBool())))),
            new RObj(
                ("measured", new RBool(false)),
                ("kind", new RLit(ExplorationContract.HudModelId)),
                ("reason", new RStr())),
        ]);

    /// <summary>
    /// Landmarkenzustandskanal-Bindung (T-034, Vertrag Abschnitt 5): im
    /// Interaktivmodus messend ausgewiesen; headless ausdrücklich nicht
    /// gemessen mit maschinenlesbarem Grund statt stiller Behauptung.
    /// </summary>
    private static MeasuredAlternative ExplorationChannelAlternative() =>
        new(
        [
            new RObj(
                ("measured", new RBool(true)),
                ("kind", new RLit(ExplorationContract.LandmarkChannelModelId)),
                ("fields", new RObj(
                    ("landmarkCount", new RInt(Riftward.Simulation.NavWorld.ZoneCount, Riftward.Simulation.NavWorld.ZoneCount)),
                    ("registeredCount", new RInt(0, Riftward.Simulation.NavWorld.ZoneCount))))),
            new RObj(
                ("measured", new RBool(false)),
                ("kind", new RLit(ExplorationContract.LandmarkChannelModelId)),
                ("reason", new RStr())),
        ]);

    /// <summary>
    /// Anzeigebindung: im Interaktivmodus messend in der tatsächlichen
    /// Builderform (Renderer, GPU-Kennungen, GL-Version; T-033
    /// Nebenreparatur: die frühere unit/method-Form passte nie zum Builder
    /// und hätte jeden echten Interaktivlauf am Schemator scheitern lassen),
    /// headless unavailable mit Grund.
    /// </summary>
    private static ReportNode Display(string executionMode) =>
        executionMode == ExecutionInteractive
            ? new RObj(
                ("measured", new RBool(true)),
                ("renderer", new RStr()),
                ("vendorId", new RInt(0)),
                ("deviceId", new RInt(0)),
                ("glVersion", new RStr()))
            : new UnavailableOnly();

    private static RObj Metrics(string executionMode)
    {
        var renderDependentHeadless = new UnavailableOnly();
        var frameBand = NumericBand();
        return new RObj(
            ("tickTimeMs", RMetric.Numeric(true,
                ("p50", new RNum(true)), ("p95", new RNum(true)), ("p99", new RNum(true)))),
            ("managedAllocationsBytes", RMetric.Numeric(true,
                ("perWarmTick", new RNum(true)))),
            ("reactionTicks", new RObj(
                ("unit", new RLit("ticks")),
                ("method", new RLit("command-submission-tick-to-first-effect-state-hash-delta")),
                ("p50", new RInt(0)),
                ("p95", new RInt(0)),
                ("p99", new RInt(0)),
                ("max", new RInt(0)),
                ("count", new RInt(0)),
                ("target", new RInt(SessionContract.ReactionTargetTicks, SessionContract.ReactionTargetTicks)),
                ("hardLimit", new RInt(SessionContract.ReactionHardLimitTicks, SessionContract.ReactionHardLimitTicks)))),
            ("runtimeShaderCompilation", RMetric.Numeric(true,
                ("value", new RBool(false)))),
            ("gcPauseSumMs", Diagnostic(RMetric.Numeric(true, ("value", new RNum(true))))),
            ("gcPauseCount", Diagnostic(RMetric.Numeric(true, ("value", new RInt(0))))),
            ("activeAgents", Diagnostic(RMetric.Numeric(true, ("value", new RInt(1))))),
            ("workingSetKiB", new MeasuredAlternative([
                new RObj(
                    ("measured", new RBool(true)),
                    ("unit", new RStr()),
                    ("method", new RStr()),
                    ("min", new RInt(1)),
                    ("max", new RInt(1)),
                    ("end", new RInt(1)),
                    ("gateCoupled", new RBool(false))),
                new RObj(
                    ("measured", new RBool(false)),
                    ("reason", new RStr())),
            ])),
            ("frameTimeMs",
                executionMode == ExecutionInteractive
                    ? Diagnostic(frameBand)
                    : renderDependentHeadless),
            ("gpuTimeMs",
                executionMode == ExecutionInteractive
                    ? new MeasuredAlternative([
                        new RObj(
                            ("measured", new RBool(true)),
                            ("unit", new RStr()),
                            ("method", new RStr()),
                            ("p99", new RNum(true)),
                            ("timerFreqHz", new RInt(0)),
                            ("gateCoupled", new RBool(false))),
                        new RObj(
                            ("measured", new RBool(false)),
                            ("reason", new RStr())),
                    ])
                    : renderDependentHeadless),
            ("drawSubmitCallsPerFrame",
                executionMode == ExecutionInteractive
                    ? Diagnostic(Counted())
                    : renderDependentHeadless),
            ("visibleTrianglesPerFrame",
                executionMode == ExecutionInteractive
                    ? Diagnostic(Counted())
                    : renderDependentHeadless),
            ("concurrentMarkers",
                executionMode == ExecutionInteractive
                    ? Diagnostic(new RObj(
                        ("unit", new RStr()),
                        ("method", new RStr()),
                        ("peak", new RInt(0)),
                        ("gateCoupled", new RBool(false))))
                    : renderDependentHeadless));
    }

    private static RObj NumericBand() => RMetric.Numeric(true,
        ("p50", new RNum(true)), ("p95", new RNum(true)), ("p99", new RNum(true)));

    private static RObj Counted() => RMetric.Numeric(true, ("value", new RInt(0)));

    /// <summary>Kennzahl mit zwingender Diagnosemarke gateCoupled=false.</summary>
    private static RObj Diagnostic(RObj metric)
    {
        var fields = new List<(string Name, ReportNode Node)>(metric.Fields)
        {
            ("gateCoupled", new RBool(false)),
        };
        return new RObj(fields.ToArray());
    }

    /// <summary>
    /// Skriptformatbindung: Der Report weist das tatsächlich gelaufene Format
    /// aus (Legacy v1, T-033-Obermenge v2 oder T-035-Obermenge v3); ein
    /// fremder Bezeichner wird abgewiesen.
    /// </summary>
    private static LiteralAlternative ScriptFormat() =>
        new([SessionContract.ScriptFormatId, ModeContract.ScriptFormatIdV2, DecisionContract.ScriptFormatIdV3]);

    /// <summary>
    /// Modussitzungsblock (T-033, Modevertrag Abschnitt 7): Wechselprotokoll
    /// je Grenze inklusive Heldenstatus von Agentenindex 0, Kontextabwei-
    /// sungszähler, Lenk-Dedupe und die diagnostische Wechselreaktions-
    /// verteilung. Rein diagnostisch; die fail-closed Koppelung von Kriterium
    /// 6 erfolgt ausschließlich über gate.switchReaction.
    /// </summary>
    private static RObj ModeSessionBody() => new(
        ("contract", new RObj(
            ("document", new RLit(ModeContract.DocumentPath)),
            ("version", new RLit(ModeContract.ContractVersion)))),
        ("initialMode", ModeName()),
        ("finalMode", ModeName()),
        ("switchProtocol", new RArr(SwitchEvent())),
        ("strategyIntentsRejectedInPersonalMode", new RInt(0)),
        ("steerIntentsRejectedInStrategyMode", new RInt(0)),
        ("steerIdleDedupes", new RInt(0)),
        ("interactiveContextRejections", new RInt(0)),
        ("hud", HudAlternative()),
        ("switchReactionTicks", new RObj(
            ("unit", new RLit("ticks")),
            ("method", new RLit("mode-switch-intent-tick-to-first-validity-boundary-in-new-mode")),
            ("p50", new RInt(0)),
            ("p95", new RInt(0)),
            ("p99", new RInt(0)),
            ("max", new RInt(0)),
            ("count", new RInt(0)),
            ("target", new RInt(ModeContract.SwitchReactionTargetTicks, ModeContract.SwitchReactionTargetTicks)),
            ("hardLimit", new RInt(ModeContract.SwitchReactionHardLimitTicks, ModeContract.SwitchReactionHardLimitTicks)),
            ("gateCoupled", new RBool(false)))));

    /// <summary>Maschinenlesbarer Modusname des Modevertrags.</summary>
    private static LiteralAlternative ModeName() =>
        new([ModeContract.ModeStrategicId, ModeContract.ModePersonalId]);

    private static RObj SwitchEvent() => new(
        ("intentTick", new RInt(0)),
        ("evaluatedBoundaryTick", new RInt(0)),
        ("effectiveBoundaryTick", new RInt(0)),
        ("previousMode", ModeName()),
        ("newMode", ModeName()),
        ("effectiveInRun", new RBool()),
        ("switchReactionTicks", new RInt(0)),
        ("heroPositionXMm", new RInt(0)),
        ("heroPositionYMm", new RInt(0)),
        ("heroZoneIndex", new RInt(-1)),
        ("heroPathState", new RInt(0, 255)));

    /// <summary>
    /// Kriterium 6 der Gatematrix (Modevertrag Abschnitt 7): entweder
    /// über mindestens einen wirksamen Wechsel ausgewertet
    /// ({ evaluated: true, max, targetMet }) oder ausdrücklich nicht
    /// auswertbar mit maschinenlesbarem Grund; ein stiller Vakuumpass ohne
    /// Messung ist unzulaessig.
    /// </summary>
    private static EvaluatedAlternative SwitchReactionAlternative() =>
        new(
        [
            new RObj(
                ("evaluated", new RBool(true)),
                ("max", new RInt(0)),
                ("targetMet", new RBool())),
            new RObj(
                ("evaluated", new RBool(false)),
                ("reason", new RStr())),
        ]);

    /// <summary>Alternativnode, der exakt einen der gebundenen Literalwerte akzeptiert.</summary>
    private sealed class LiteralAlternative(IReadOnlyList<string> literals) : ReportNode
    {
        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                errors.Add($"{path}: Zeichenkettenliteral erwartet.");
                return;
            }

            var value = element.GetString() ?? string.Empty;

            if (!literals.Contains(value, StringComparer.Ordinal))
            {
                errors.Add($"{path}: konstanter Wert [{string.Join("|", literals)}] erwartet.");
            }
        }
    }

    /// <summary>
    /// Titel-HUD-Bindung (T-033, Modevertrag Abschnitt 8): im Interaktivmodus
    /// gebundener Modus/Heldenzonenausweis; headless und in unvollständigen
    /// Läufen ausdrücklich nicht gemessen mit maschinenlesbarem Grund statt
    /// stiller Behauptung.
    /// </summary>
    private static MeasuredAlternative HudAlternative() =>
        new(
        [
            new RObj(
                ("measured", new RBool(true)),
                ("kind", new RLit(ModeContract.HudModelId)),
                ("fields", new RObj(
                    ("mode", ModeName()),
                    ("heroZone", new RInt(-1, Riftward.Simulation.NavWorld.ZoneCount - 1))))),
            new RObj(
                ("measured", new RBool(false)),
                ("kind", new RLit(ModeContract.HudModelId)),
                ("reason", new RStr())),
        ]);

    /// <summary>
    /// Abgriffbindung (T-033, Modevertrag Abschnitt 8): Bei captured=true
    /// bindet der Report das Abgriffpaar — exakt zwei Einzelabgriffe, je einer
    /// pro Modus über demselben Weltzustand am selben Tick, je hashgebunden
    /// (SHA-256, Abmessungen, Format, Modus) mit der Aussagegrenze
    /// Graybox-Zustandsbelegung und dem gemeinsamen gebundenen Weltzustand
    /// (Tick und Zustands-Hash).
    /// </summary>
    private sealed class CapturePairAlternative : ReportNode
    {
        private static readonly RObj CaptureShape = new(
            ("mode", new LiteralAlternative([ModeContract.ModeStrategicId, ModeContract.ModePersonalId])),
            ("sha256", Sha256),
            ("width", new RInt(1920, 1920)),
            ("height", new RInt(1080, 1080)),
            ("format", new RLit(Bench.FrameEvidence.FormatId)),
            ("statementLimit", new RLit(CommandFrameEvidence.StatementLimit)));

        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                errors.Add($"{path}: Abgriffpaar-Array erwartet.");
                return;
            }

            var count = 0;

            foreach (var item in element.EnumerateArray())
            {
                CaptureShape.Check($"{path}[{count}]", item, errors);
                count++;
            }

            if (count != 2)
            {
                errors.Add($"{path}: exakt zwei Einzelabgriffe (je einer pro Modus) erwartet.");
            }
        }
    }

    /// <summary>
    /// Ausweisschema des Ketten-Selbstkonsistenzkriteriums (Kommandovertrag
    /// §7): entweder ausgewertet ({ evaluated: true }) oder ausdrücklich
    /// nicht auswertbar mit maschinenlesbarem Grund; eine Behauptung ohne
    /// Auswertung ist unzulaessig.
    /// </summary>
    private static EvaluatedAlternative ChainConsistencyAlternative() =>
        new(
        [
            new RObj(("evaluated", new RBool(true))),
            new RObj(
                ("evaluated", new RBool(false)),
                ("reason", new RStr())),
        ]);

    /// <summary>Alternativnode, der auf dem booleschen Feld "evaluated" dispatcht.</summary>
    private sealed class EvaluatedAlternative(IReadOnlyList<RObj> shapes) : ReportNode
    {
        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{path}: Objekt erwartet.");
                return;
            }

            if (!element.TryGetProperty("evaluated", out var flag)
                || flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                errors.Add($"{path}.evaluated: boolesche Auswertungskennung erwartet.");
                return;
            }

            shapes[flag.GetBoolean() ? 0 : 1].Check(path, element, errors);
        }
    }

    private sealed class UnavailableOnly : ReportNode
    {
        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{path}: Objekt erwartet.");
                return;
            }

            if (!element.TryGetProperty("measured", out var flag))
            {
                errors.Add($"{path}.measured: boolesche Messkennung erwartet.");
                return;
            }

            if (flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                errors.Add($"{path}.measured: boolesche Messkennung erwartet.");
                return;
            }

            if (flag.GetBoolean())
            {
                errors.Add(
                    $"{path}.measured: headless Szenario kann diesen Wert nicht messen; nur unavailable erlaubt.");
                return;
            }

            if (!element.TryGetProperty("reason", out var reason)
                || reason.ValueKind != JsonValueKind.String
                || reason.GetString()?.Length == 0)
            {
                errors.Add($"{path}.reason: maschinenlesbarer Grund erforderlich.");
            }
        }
    }

    private sealed class MeasuredAlternative(IReadOnlyList<RObj> shapes) : ReportNode
    {
        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{path}: Objekt erwartet.");
                return;
            }

            if (!element.TryGetProperty("measured", out var flag)
                || flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                errors.Add($"{path}.measured: boolesche Messkennung erwartet.");
                return;
            }

            shapes[flag.GetBoolean() ? 0 : 1].Check(path, element, errors);
        }
    }

    private sealed class FrameEvidenceAlternative : ReportNode
    {
        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{path}: Objekt erwartet.");
                return;
            }

            if (!element.TryGetProperty("captured", out var flag)
                || flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                errors.Add($"{path}.captured: boolesche Kennung erwartet.");
                return;
            }

            if (flag.GetBoolean())
            {
                new RObj(
                    ("captured", new RBool(true)),
                    ("afterMeasurementWindow", new RBool(true)),
                    ("boundTick", new RInt(0)),
                    ("boundStateHash", Hex),
                    ("captures", new CapturePairAlternative())).Check(path, element, errors);
            }
            else
            {
                new RObj(
                    ("captured", new RBool(false)),
                    ("reason", new RStr())).Check(path, element, errors);
            }
        }
    }

    internal sealed class HexNode : ReportNode
    {
        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                errors.Add($"{path}: Hexzeichenkette erwartet.");
                return;
            }

            var value = element.GetString();

            if (value is null || value.Length != 16 || !IsLowerHex(value))
            {
                errors.Add($"{path}: 16-stelliger Kleinbuchstaben-Hexwert erwartet.");
            }
        }

        internal static bool IsLowerHex(string value)
        {
            foreach (var character in value)
            {
                var isDigit = character is >= '0' and <= '9';
                var isLowerHexLetter = character is >= 'a' and <= 'f';

                if (!isDigit && !isLowerHexLetter)
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal sealed class Sha256HexNode : ReportNode
    {
        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                errors.Add($"{path}: Hexzeichenkette erwartet.");
                return;
            }

            var value = element.GetString();

            if (value is null || value.Length != 64 || !HexNode.IsLowerHex(value))
            {
                errors.Add($"{path}: 64-stelliger Kleinbuchstaben-Hexwert erwartet.");
            }
        }
    }

    /// <summary>Prueft einen Reporttext; Rueckgabe ist die Fehlerliste (leer == gueltig).</summary>
    public static IReadOnlyList<string> Validate(string json) =>
        BenchReportSchema.ValidateWith(Root, json);
}
