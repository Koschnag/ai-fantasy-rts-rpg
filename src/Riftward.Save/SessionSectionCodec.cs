using System.Buffers.Binary;
using Riftward.Simulation;

namespace Riftward.Save;

/// <summary>
/// Unterscheidbare Verletzungsklassen der Sitzungssektion (Savevertrag V2,
/// Abschnitt 13.5). Jede Sektion wird hoechstens einer Klasse zugeordnet; die
/// Pruefreihenfolge des Dokuments garantiert, dass ein Sektionsbitfehler
/// stets als Sektionsintegritaetsverletzung erscheint, bevor der Inhalt
/// gelesen wird.
/// </summary>
public enum SessionSectionRejectionClass
{
    None = 0,

    /// <summary>Sektionsbytes passen nicht zum sessionSectionHash-Anker.</summary>
    IntegrityViolation,

    /// <summary>Sektionsversion unbekannt, Framing/Ordnung/Grenzen/Referenzen verletzt.</summary>
    Invalid,
}

/// <summary>Eine kontrollierte Sektionsablehnung mit Klasse und verstaendlichem Detail.</summary>
public sealed record SessionSectionRejection(SessionSectionRejectionClass Class, string Detail)
{
    public override string ToString() => $"{Class}: {Detail}";
}

/// <summary>
/// Kanonische Binaercodierung der Sitzungssektion V1
/// (<c>riftward-session-section-canonical-binary-v1</c>, Savevertrag V2
/// Abschnitte 13.1 und 13.4): feste Feldordnung, Little-Endian-
/// Festbreiten-Ganzzahlen, keine Auffuellbytes, keine Feldkennungen, exakter
/// Byteverbrauch und Re-Encoding-Gleichheit wie der Savekern. Strikter
/// Einzelpass-Decoder mit vor der Zuweisung gepruefter Laengengrenzen;
/// BCL-only, reflectionsfrei, ohne Fließkommaanteil.
/// </summary>
public static class SessionSectionCodec
{
    /// <summary>Einzige Sektionsversion dieser Vertragsstufe.</summary>
    public const ushort SectionVersion = 1;

    /// <summary>Kennung der kanonischen Sektionscodierung (Vertragsanker).</summary>
    public const string CodecId = "riftward-session-section-canonical-binary-v1";

    public const byte ModeStrategic = 0;
    public const byte ModePersonal = 1;
    public const byte ChoiceKindA = 0;
    public const byte ChoiceKindB = 1;
    public const byte ChoiceKindUnset = 255;
    public const byte EndReasonOpen = 0;
    public const byte EndReasonSuccess = 1;
    public const byte EndReasonExpired = 2;
    public const byte ArrivalModeNone = 0;
    public const byte CauseKindNone = 0;
    public const byte CauseKindWindowExpired = 1;

    /// <summary>Hoechstzahl schwebender Moduswechsel (DoS-Grenze vor Zuweisung).</summary>
    public const int MaxPendingSwitches = 8;

    /// <summary>Hoechstzahl Aufsuchregistrierungen (je Vertragszone eine).</summary>
    public const int MaxVisits = NavWorld.ZoneCount;

    /// <summary>Hoechstzahl Fensterinstanzen (DoS-Grenze vor Zuweisung).</summary>
    public const int MaxWindows = 8192;

    /// <summary>Festes Minimum der Sektionslaenge (alle Kopfzaehler ohne Listen).</summary>
    public const int MinimumSectionBytes = sizeof(ushort) + 1 + sizeof(uint)
        + 1 + sizeof(uint)
        + 1 + 1 + (3 * sizeof(long)) + (2 * sizeof(int)) + 1 + sizeof(long) + 1 + 1
            + sizeof(int) + 1 + sizeof(long) + (3 * sizeof(long))
        + 1 + sizeof(long) + sizeof(uint)
        + (3 * sizeof(long)) + 1 + sizeof(int) + sizeof(long) + 1;

    /// <summary>Feste Stranglaenge einer Fensterinstanz in Bytes.</summary>
    public const int WindowStrideBytes = (4 * sizeof(long)) + 3;

    /// <summary>Feste Stranglaenge einer Aufsuchregistrierung in Bytes.</summary>
    public const int VisitStrideBytes = sizeof(long) + sizeof(int) + 1;

    /// <summary>Feste Stranglaenge eines schwebenden Moduswechsels in Bytes.</summary>
    public const int PendingSwitchStrideBytes = (2 * sizeof(long)) + 1;

    /// <summary>Kodiert den vollstaendigen Sitzungszustand kanonisch.</summary>
    public static byte[] Encode(SessionSectionState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var switches = state.PendingSwitches;
        var visits = state.ExplorationVisits;
        var windows = state.PressureWindows;
        var total = MinimumSectionBytes
            + ((long)switches.Count * PendingSwitchStrideBytes)
            + ((long)visits.Count * VisitStrideBytes)
            + ((long)windows.Count * WindowStrideBytes);

        if (total > int.MaxValue)
        {
            throw new InvalidOperationException("Sektion ueberschreitet die Kodiergrenze.");
        }

        var section = new byte[total];
        var offset = 0;

        WriteU16(section, ref offset, SectionVersion);
        WriteU8(section, ref offset, state.ActiveMode);

        WriteU32(section, ref offset, (uint)switches.Count);

        foreach (var pending in switches)
        {
            WriteI64(section, ref offset, pending.IntentTick);
            WriteI64(section, ref offset, pending.EffectiveBoundaryTick);
            WriteU8(section, ref offset, pending.NewMode);
        }

        WriteU8(section, ref offset, state.ExplorationActive);
        WriteU32(section, ref offset, (uint)visits.Count);

        foreach (var visit in visits)
        {
            WriteI64(section, ref offset, visit.BoundaryTick);
            WriteI32(section, ref offset, visit.ZoneIndex);
            WriteU8(section, ref offset, visit.Mode);
        }

        WriteU8(section, ref offset, state.DecisionActive);
        WriteU8(section, ref offset, state.DecisionOfferOpened);
        WriteI64(section, ref offset, state.DecisionOfferBoundaryTick);
        WriteI32(section, ref offset, state.DecisionOptionZoneA);
        WriteI32(section, ref offset, state.DecisionOptionZoneB);
        WriteU8(section, ref offset, state.DecisionDecided);
        WriteI64(section, ref offset, state.DecisionBoundaryTick);
        WriteU8(section, ref offset, state.DecisionChoiceKind);
        WriteU8(section, ref offset, state.DecisionModeKind);
        WriteI32(section, ref offset, state.DecisionFollowUpZoneIndex);
        WriteU8(section, ref offset, state.DecisionFollowUpCompleted);
        WriteI64(section, ref offset, state.DecisionArrivalBoundaryTick);
        WriteI64(section, ref offset, state.DecisionRejectionsBeforeOffer);
        WriteI64(section, ref offset, state.DecisionRejectionsInStrategicMode);
        WriteI64(section, ref offset, state.DecisionRejectionsAfterDecision);

        WriteU8(section, ref offset, state.PressureActive);
        WriteI64(section, ref offset, state.PressureCycleCount);
        WriteU32(section, ref offset, (uint)windows.Count);

        foreach (var window in windows)
        {
            WriteI64(section, ref offset, window.Instance);
            WriteI64(section, ref offset, window.Cycle);
            WriteI64(section, ref offset, window.StartBoundaryTick);
            WriteI64(section, ref offset, window.EndBoundaryTick);
            WriteU8(section, ref offset, window.EndReasonKind);
            WriteI64(section, ref offset, window.ArrivalBoundaryTick);
            WriteU8(section, ref offset, window.ArrivalModeKind);
            WriteU8(section, ref offset, window.FailureCauseKind);
        }

        WriteI64(section, ref offset, state.PressureLastFailureBoundaryTick);
        WriteU8(section, ref offset, state.PressureHasLastFailure);
        WriteI32(section, ref offset, state.PressureLastFailureFollowUpZoneIndex);
        WriteI64(section, ref offset, state.PressureLastReopenBoundaryTick);
        WriteU8(section, ref offset, state.PressureReopenPendingRecording);

        if (offset != total)
        {
            throw new InvalidOperationException("Sektionsmass und Kodierung weichen ab.");
        }

        return section;
    }

    /// <summary>
    /// Dekodiert eine Sektion strikt im Einzelpass: Framing und Grenzen vor
    /// jeder Zuweisung, exakter Byteverbrauch, vertragliche Relations- und
    /// Referenzwahrheiten. Jede Verletzung erhaelt eine unterscheidbare
    /// Klasse; der Rueckgabestand ist bei Erfolg vollstaendig geprueft.
    /// </summary>
    public static (SessionSectionRejection? Rejection, SessionSectionState? State) Decode(ReadOnlySpan<byte> section)
    {
        if (section.Length < MinimumSectionBytes)
        {
            return Invalid("Sektion unterschreitet das feste Mass des Sitzungszustands.");
        }

        try
        {
            return DecodeChecked(section);
        }
        catch (SectionBoundsException)
        {
            return Invalid("Sektion endet innerhalb eines Zustandsfelds (Framing verletzt das feste Mass).");
        }
    }

    private static (SessionSectionRejection? Rejection, SessionSectionState? State) DecodeChecked(ReadOnlySpan<byte> section)
    {
        var offset = 0;
        var version = ReadU16(section, ref offset);

        if (version != SectionVersion)
        {
            return Invalid($"Sektionsversion {version} wird ohne erfundene Migration nicht unterstuetzt.");
        }

        var activeMode = ReadU8(section, ref offset);

        if (activeMode > ModePersonal)
        {
            return Invalid("Aktiver Sitzungsmodus ist unbekannt.");
        }

        if (!TryReadListCount(section, ref offset, MaxPendingSwitches, out var pendingCount))
        {
            return Invalid("Anzahl schwebender Moduswechsel ausserhalb der Vertragsgrenze.");
        }

        EnsureBytes(section, offset, (long)pendingCount * PendingSwitchStrideBytes);
        var pending = new SessionSectionPendingSwitch[pendingCount];

        for (var index = 0; index < pendingCount; index++)
        {
            var intentTick = ReadI64(section, ref offset);
            var effectiveTick = ReadI64(section, ref offset);
            var newMode = ReadU8(section, ref offset);

            if (newMode > ModePersonal)
            {
                return Invalid("Zielmodus eines schwebenden Wechsels ist unbekannt.");
            }

            if (effectiveTick <= intentTick)
            {
                return Invalid("Wirksamkeitsgrenze eines schwebenden Wechsels liegt nicht nach seinem Intent.");
            }

            pending[index] = new SessionSectionPendingSwitch(intentTick, effectiveTick, newMode);
        }

        var explorationActive = ReadU8(section, ref offset);

        if (explorationActive > 1)
        {
            return Invalid("Erkundungsaktivierung ist kein boolescher Wert.");
        }

        if (!TryReadListCount(section, ref offset, MaxVisits, out var visitCount))
        {
            return Invalid("Anzahl Aufsuchregistrierungen ausserhalb der Vertragsgrenze.");
        }

        EnsureBytes(section, offset, (long)visitCount * VisitStrideBytes);
        var visits = new SessionSectionVisit[visitCount];
        var seenZones = new bool[NavWorld.ZoneCount];

        for (var index = 0; index < visitCount; index++)
        {
            var boundaryTick = ReadI64(section, ref offset);
            var zoneIndex = ReadI32(section, ref offset);
            var mode = ReadU8(section, ref offset);

            if (boundaryTick < 0)
            {
                return Invalid("Registrierungsgrenze ist negativ.");
            }

            if (zoneIndex < 0 || zoneIndex >= NavWorld.ZoneCount)
            {
                return Invalid($"Registrierungszone {zoneIndex} ist unbekannt.");
            }

            if (mode != ModePersonal)
            {
                return Invalid("Registrierung ist nicht ausschliesslich persoenlich (Erkundungsvertrag Abschnitt 3).");
            }

            if (seenZones[zoneIndex])
            {
                return Invalid($"Landmarkenzone {zoneIndex} wurde mehrfach registriert.");
            }

            if (index > 0 && boundaryTick <= visits[index - 1].BoundaryTick)
            {
                return Invalid("Registrierungsgrenzen sind nicht strikt steigend.");
            }

            seenZones[zoneIndex] = true;
            visits[index] = new SessionSectionVisit(boundaryTick, zoneIndex, mode);
        }

        var decisionActive = ReadU8(section, ref offset);
        var offerOpened = ReadU8(section, ref offset);
        var offerBoundaryTick = ReadI64(section, ref offset);
        var optionZoneA = ReadI32(section, ref offset);
        var optionZoneB = ReadI32(section, ref offset);
        var decided = ReadU8(section, ref offset);
        var decisionBoundaryTick = ReadI64(section, ref offset);
        var choiceKind = ReadU8(section, ref offset);
        var decisionModeKind = ReadU8(section, ref offset);
        var followUpZoneIndex = ReadI32(section, ref offset);
        var followUpCompleted = ReadU8(section, ref offset);
        var arrivalBoundaryTick = ReadI64(section, ref offset);
        var rejectionsBeforeOffer = ReadI64(section, ref offset);
        var rejectionsInStrategicMode = ReadI64(section, ref offset);
        var rejectionsAfterDecision = ReadI64(section, ref offset);

        if (decisionActive > 1 || offerOpened > 1 || decided > 1 || followUpCompleted > 1)
        {
            return Invalid("Entscheidungsboolesche Werte sind unbekannt.");
        }

        if (rejectionsBeforeOffer < 0 || rejectionsInStrategicMode < 0 || rejectionsAfterDecision < 0)
        {
            return Invalid("Abweisungszaehler der Entscheidung sind negativ.");
        }

        if (offerOpened == 0
            && (offerBoundaryTick != -1 || optionZoneA != -1 || optionZoneB != -1))
        {
            return Invalid("Ohne Angebot tragen Angebotsgrenze oder Optionszonen keine Sentinele.");
        }

        if (offerOpened == 1
            && (offerBoundaryTick < 0
                || !IsValidZone(optionZoneA)
                || !IsValidZone(optionZoneB)
                || optionZoneA == optionZoneB))
        {
            return Invalid("Angebot traegt keine zwei verschiedenen gueltigen Optionszonen.");
        }

        if (decided == 0
            && (decisionBoundaryTick != -1
                || choiceKind != ChoiceKindUnset
                || decisionModeKind != 0
                || followUpZoneIndex != -1
                || followUpCompleted != 0
                || arrivalBoundaryTick != -1))
        {
            return Invalid("Ohne Entscheidung tragen Entscheidungsfelder keine Sentinele.");
        }

        if (decided == 1
            && (offerOpened != 1
                || decisionBoundaryTick < 0
                || (choiceKind != ChoiceKindA && choiceKind != ChoiceKindB)
                || decisionModeKind != ModePersonal
                || !IsValidZone(followUpZoneIndex)
                || followUpZoneIndex != (choiceKind == ChoiceKindA ? optionZoneA : optionZoneB)))
        {
            return Invalid("Entscheidung widerspricht Angebot, Wahlart oder gewaehlter Zone.");
        }

        if (followUpCompleted == 1
            && (decided != 1
                || arrivalBoundaryTick < decisionBoundaryTick))
        {
            return Invalid("Folgenabschluss liegt nicht an oder nach der Entscheidungsgrenze.");
        }

        if (followUpCompleted == 0 && arrivalBoundaryTick != -1)
        {
            return Invalid("Ohne Abschluss traegt die Ankunftsgrenze keinen Sentinel.");
        }

        var pressureActive = ReadU8(section, ref offset);

        if (pressureActive > 1)
        {
            return Invalid("Druckaktivierung ist kein boolescher Wert.");
        }

        var cycleCount = ReadI64(section, ref offset);

        if (cycleCount < 0)
        {
            return Invalid("Zyklusanzaehlung ist negativ.");
        }

        if (!TryReadListCount(section, ref offset, MaxWindows, out var windowCount))
        {
            return Invalid("Anzahl Fensterinstanzen ausserhalb der Vertragsgrenze.");
        }

        EnsureBytes(section, offset, (long)windowCount * WindowStrideBytes);

        if (cycleCount != windowCount)
        {
            return Invalid("Zyklusanzaehlung entspricht nicht der Instanzanzahl.");
        }

        var windows = new SessionSectionWindow[windowCount];
        var openInstances = 0;
        var lastExpiredEndTick = -1L;

        for (var index = 0; index < windowCount; index++)
        {
            var instance = ReadI64(section, ref offset);
            var cycle = ReadI64(section, ref offset);
            var start = ReadI64(section, ref offset);
            var end = ReadI64(section, ref offset);
            var endReason = ReadU8(section, ref offset);
            var arrival = ReadI64(section, ref offset);
            var arrivalMode = ReadU8(section, ref offset);
            var cause = ReadU8(section, ref offset);

            if (instance != index + 1L || instance != cycle)
            {
                return Invalid("Instanz- und Zyklusnummern sind nicht fortlaufend und identisch.");
            }

            if (start < 0)
            {
                return Invalid("Startgrenze einer Fensterinstanz ist negativ.");
            }

            if (endReason == EndReasonOpen)
            {
                if (end != -1 || arrival != -1 || arrivalMode != ArrivalModeNone || cause != CauseKindNone)
                {
                    return Invalid("Eine offene Instanz traegt End- oder Ankunftswerte.");
                }

                openInstances++;
            }
            else if (endReason == EndReasonSuccess)
            {
                if (end < start)
                {
                    return Invalid("Die Endgrenze eines Erfolgs liegt vor der Startgrenze.");
                }

                if (arrival < start || arrival > end || arrivalMode != ModePersonal || cause != CauseKindNone)
                {
                    return Invalid("Ein Erfolg traegt seine persoenliche Ankunft innerhalb der Instanzgrenzen.");
                }
            }
            else if (endReason == EndReasonExpired)
            {
                if (end < start || arrival != -1 || arrivalMode != ArrivalModeNone || cause != CauseKindWindowExpired)
                {
                    return Invalid("Ein Ablauf traegt die Ursachenkennung und keine Ankunft.");
                }

                lastExpiredEndTick = end;
            }
            else
            {
                return Invalid("Endgrund einer Fensterinstanz ist unbekannt.");
            }

            windows[index] = new SessionSectionWindow(instance, cycle, start, end, endReason, arrival, arrivalMode, cause);
        }

        if (openInstances > 1)
        {
            return Invalid("Mehr als eine offene Fensterinstanz.");
        }

        var lastFailureBoundaryTick = ReadI64(section, ref offset);
        var hasLastFailure = ReadU8(section, ref offset);
        var lastFailureFollowUpZoneIndex = ReadI32(section, ref offset);
        var lastReopenBoundaryTick = ReadI64(section, ref offset);
        var reopenPendingRecording = ReadU8(section, ref offset);

        if (hasLastFailure > 1 || reopenPendingRecording > 1)
        {
            return Invalid("Fehlschlags- oder Wiederauffrischungskennung ist kein boolescher Wert.");
        }

        if (hasLastFailure == 0
            && (lastFailureBoundaryTick != -1
                || lastFailureFollowUpZoneIndex != -1))
        {
            return Invalid("Ohne Fehlschlag tragen Fehlschlagsfelder keine Sentinele.");
        }

        if (hasLastFailure == 1
            && (lastFailureBoundaryTick < 0
                || lastFailureBoundaryTick != lastExpiredEndTick
                || lastFailureFollowUpZoneIndex != -1 && !IsValidZone(lastFailureFollowUpZoneIndex)))
        {
            return Invalid("Fehlschlag entspricht nicht der letzten abgelaufenen Instanz.");
        }

        if (hasLastFailure == 1
            && lastReopenBoundaryTick != -1
            && lastReopenBoundaryTick != lastFailureBoundaryTick + 1)
        {
            return Invalid("Wiederauffrischung liegt nicht genau an der naechsten Vorgrenze nach dem Fehlschlag.");
        }

        if (reopenPendingRecording == 1 && hasLastFailure != 1)
        {
            return Invalid("Wiederauffrischungspendenz ohne Fehlschlag.");
        }

        if (offset != section.Length)
        {
            return Invalid("Sektion besitzt Restbytes jenseits des festen Zustandsmasses.");
        }

        var state = new SessionSectionState
        {
            ActiveMode = activeMode,
            PendingSwitches = pending,
            ExplorationActive = explorationActive,
            ExplorationVisits = visits,
            DecisionActive = decisionActive,
            DecisionOfferOpened = offerOpened,
            DecisionOfferBoundaryTick = offerBoundaryTick,
            DecisionOptionZoneA = optionZoneA,
            DecisionOptionZoneB = optionZoneB,
            DecisionDecided = decided,
            DecisionBoundaryTick = decisionBoundaryTick,
            DecisionChoiceKind = choiceKind,
            DecisionModeKind = decisionModeKind,
            DecisionFollowUpZoneIndex = followUpZoneIndex,
            DecisionFollowUpCompleted = followUpCompleted,
            DecisionArrivalBoundaryTick = arrivalBoundaryTick,
            DecisionRejectionsBeforeOffer = rejectionsBeforeOffer,
            DecisionRejectionsInStrategicMode = rejectionsInStrategicMode,
            DecisionRejectionsAfterDecision = rejectionsAfterDecision,
            PressureActive = pressureActive,
            PressureCycleCount = cycleCount,
            PressureWindows = windows,
            PressureLastFailureBoundaryTick = lastFailureBoundaryTick,
            PressureHasLastFailure = hasLastFailure,
            PressureLastFailureFollowUpZoneIndex = lastFailureFollowUpZoneIndex,
            PressureLastReopenBoundaryTick = lastReopenBoundaryTick,
            PressureReopenPendingRecording = reopenPendingRecording,
        };

        return (null, state);
    }

    private static bool IsValidZone(int zone) => zone >= 0 && zone < NavWorld.ZoneCount;

    /// <summary>Internes Signal fuer Lesezugriffe jenseits der Sektionsgrenze.</summary>
    private sealed class SectionBoundsException : Exception
    {
    }

    private static void EnsureBytes(ReadOnlySpan<byte> section, int offset, long required)
    {
        if (offset + required > section.Length)
        {
            throw new SectionBoundsException();
        }
    }

    private static bool TryReadListCount(ReadOnlySpan<byte> section, ref int offset, int maximum, out int count)
    {
        count = 0;

        if (offset + sizeof(uint) > section.Length)
        {
            return false;
        }

        var raw = ReadU32(section, ref offset);

        if (raw > (uint)maximum)
        {
            return false;
        }

        count = (int)raw;
        return true;
    }

    private static void WriteU8(byte[] target, ref int offset, byte value)
    {
        target[offset++] = value;
    }

    private static void WriteU16(byte[] target, ref int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(target.AsSpan(offset, sizeof(ushort)), value);
        offset += sizeof(ushort);
    }

    private static void WriteU32(byte[] target, ref int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(target.AsSpan(offset, sizeof(uint)), value);
        offset += sizeof(uint);
    }

    private static void WriteI32(byte[] target, ref int offset, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(target.AsSpan(offset, sizeof(int)), value);
        offset += sizeof(int);
    }

    private static void WriteI64(byte[] target, ref int offset, long value)
    {
        BinaryPrimitives.WriteInt64LittleEndian(target.AsSpan(offset, sizeof(long)), value);
        offset += sizeof(long);
    }

    private static ushort ReadU16(ReadOnlySpan<byte> source, ref int offset)
    {
        var value = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(offset, sizeof(ushort)));
        offset += sizeof(ushort);
        return value;
    }

    private static uint ReadU32(ReadOnlySpan<byte> source, ref int offset)
    {
        var value = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, sizeof(uint)));
        offset += sizeof(uint);
        return value;
    }

    private static byte ReadU8(ReadOnlySpan<byte> source, ref int offset) => source[offset++];

    private static int ReadI32(ReadOnlySpan<byte> source, ref int offset)
    {
        var value = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(offset, sizeof(int)));
        offset += sizeof(int);
        return value;
    }

    private static long ReadI64(ReadOnlySpan<byte> source, ref int offset)
    {
        var value = BinaryPrimitives.ReadInt64LittleEndian(source.Slice(offset, sizeof(long)));
        offset += sizeof(long);
        return value;
    }

    private static (SessionSectionRejection?, SessionSectionState?) Invalid(string detail) =>
        (new SessionSectionRejection(SessionSectionRejectionClass.Invalid, detail), null);
}
