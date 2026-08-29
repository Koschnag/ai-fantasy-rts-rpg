using System.Security.Cryptography;
using Riftward.Save;
using Riftward.Session;
using Riftward.Simulation;

namespace Riftward.App.Command;

/// <summary>
/// Dokument- und Slotnahtstelle des headless Fortsetzungspfads (T-037,
/// Savevertrag V2 Abschnitt 13.2): verpackt eine Speichererfassung in ein
/// vollständiges V2-Dokument über das unveränderte Atomarprotokoll und lädt
/// einen Slot mit vollständiger Validierung und Aktivierungsgrenzen
/// (<c>untrusted-slot-activation-guards-v2</c>) zurück. Sie erzeugt niemals
/// einen Kernbefehl und schreibt ausschließlich in das vertragliche
/// Slotverzeichnis.
/// </summary>
internal static class ContinuationRunner
{
    /// <summary>Maschinenlesbare Ablehnung eines Slots vor Aktivierung.</summary>
    internal sealed record SlotRejection(string Reason, string Detail);

    /// <summary>
    /// Schreibt die Erfassung eines Speicherlaufs atomar in den Slot: V2-
    /// Umschlag mit Simulation plus Sitzungssektion, Validierung vor
    /// Ersetzung. Eine fehlgeschlagene Schreibphase lässt den letzten
    /// gültigen Stand unangetastet. Der Intentplanhash des Skripts wird als
    /// vertraglicher Diagnoseanker im Kopf verankert (T-031-Präzedenz).
    /// </summary>
    internal static SlotWriteResult WriteSlot(
        string slotDirectory,
        string slotName,
        SessionSaveCapture capture,
        ulong intentPlanHash,
        string buildId)
    {
        var document = CanonicalSaveCodec.WriteDocumentV2(
            capture.Simulation,
            capture.BoundaryStateHash,
            intentPlanHash,
            buildId,
            SaveEnvelopeMetadata.CreateFresh(),
            SessionSectionCodec.Encode(capture.Session));

        var store = new SlotStore(slotDirectory);
        return store.WriteSlotAtomic(slotName, document);
    }

    /// <summary>
    /// Liest und validiert einen Slot vollständig vor Aktivierung: Dokument-
    /// validierung (Prüfklassen T-031 uneingeschränkt für die Sektion) plus
    /// Aktivierungsgrenzen an den Laufkontext (Weltkennung, Seed,
    /// Vertragsversion). Rueckgabe ist die restaurierte Erfassung oder eine
    /// unterscheidbare Ablehnung ohne Welt-, Ketten- oder Kernänderung.
    /// </summary>
    internal static (SessionSaveCapture? Capture, SlotRejection? Rejection) LoadSlot(
        string slotDirectory,
        string slotName,
        uint requestSeed)
    {
        var store = new SlotStore(slotDirectory);
        var read = store.ReadSlot(slotName);

        if (!read.Success || read.Bytes is null)
        {
            return (
                null,
                new SlotRejection(
                    SaveContract.RejectionSlotUnreadable,
                    read.Rejection?.ToString() ?? "Slot existiert nicht oder ist unlesbar."));
        }

        var (rejection, document) = SaveDocumentValidator.Validate(read.Bytes);

        if (rejection is not null || document is null)
        {
            var reason = rejection?.Class switch
            {
                SaveRejectionClass.SchemaVersionUnsupported => SaveContract.RejectionUnsupportedSchemaVersion,
                _ => rejection?.Class.ToString().ToLowerInvariant() ?? "unknown",
            };
            return (null, new SlotRejection(reason, rejection?.ToString() ?? "Unbekannte Ablehnung."));
        }

        if (!string.Equals(document.WorldId, SimulationContract.WorldId, StringComparison.Ordinal))
        {
            return (
                null,
                new SlotRejection(
                    SaveContract.RejectionForeignWorldId,
                    $"Slot trägt Weltkennung '{document.WorldId}' statt der Vertragswelt."));
        }

        if (document.State.Seed != requestSeed)
        {
            return (
                null,
                new SlotRejection(
                    SaveContract.RejectionForeignSeed,
                    $"Slot trägt Seed {document.State.Seed} statt des angeforderten Laufseeds {requestSeed}."));
        }

        if (document.FromLegacyV1Document)
        {
            // V1-Kompatibilität (Savevertrag V2 Abschnitt 13.5): das Dokument
            // lädt unveraendert mit ehrlicher Sitzungsleere; die Kette bleibt
            // unverändert. Fortsetzbarkeit der Kette und Session-Leere sind
            // maschinenlesbar.
            return (
                new SessionSaveCapture(
                    BoundaryTick: document.State.TickIndex,
                    BoundaryStateHash: document.SnapshotStateHash,
                    Simulation: document.State,
                    Session: SessionSectionState.Empty),
                null);
        }

        return (
            new SessionSaveCapture(
                BoundaryTick: document.State.TickIndex,
                BoundaryStateHash: document.SnapshotStateHash,
                Simulation: document.State,
                Session: document.SessionSection),
            null);
    }

    /// <summary>SHA-256-Hexdarstellung eines Dokuments (Reportbindung des Slots).</summary>
    internal static string DocumentSha256Hex(byte[] documentBytes) =>
        Convert.ToHexString(SHA256.HashData(documentBytes)).ToLowerInvariant();
}
