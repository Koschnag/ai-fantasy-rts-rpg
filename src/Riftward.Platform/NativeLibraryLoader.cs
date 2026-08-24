using System.Runtime.InteropServices;
using Riftward.Platform.Interop;

namespace Riftward.Platform;

/// <summary>
/// Laedt die geprueften nativen Bibliotheken aus dem Artefaktverzeichnis und
/// bindet sie an die LibraryImport-Deklarationen dieser Assembly. Das Laden
/// erfolgt ausschliesslich nach erfolgreicher Hashpruefung; fehlende oder
/// beschaedigte Artefakte werden zuvor als kontrollierter Fehler gemeldet.
/// </summary>
public static class NativeLibraryLoader
{
    private static readonly object LoadLock = new();
    private static bool _loaded;

    /// <summary>Absolute Pfade der geladenen Bibliotheken (fuer Reports).</summary>
    public static IReadOnlyList<string> LoadedPaths { get; private set; } = Array.Empty<string>();

    public static void EnsureLoaded(string sdlPath, string shimPath)
    {
        lock (LoadLock)
        {
            if (_loaded)
            {
                return;
            }

            try
            {
                var sdlHandle = NativeLibrary.Load(sdlPath);
                var shimHandle = NativeLibrary.Load(shimPath);

                // Resolver verankert beide Handles fuer alle P/Invoke-Aufrufe
                // dieser Assembly ("SDL3", "riftbgfx").
                NativeLibrary.SetDllImportResolver(
                    typeof(NativeLibraryLoader).Assembly,
                    (libraryName, assembly, searchPath) => Resolve(libraryName, sdlHandle, shimHandle));

                LoadedPaths = new[] { sdlPath, shimPath };
                _loaded = true;
            }
            catch (BadImageFormatException exception)
            {
                throw new PlatformException(new PlatformError(
                    PlatformErrorCode.ArtifactIncomplete,
                    "Native-Bibliothek passt nicht zur Architektur dieses Prozesses.",
                    exception.Message));
            }
            catch (DllNotFoundException exception)
            {
                throw new PlatformException(new PlatformError(
                    PlatformErrorCode.ArtifactMissing,
                    "Native-Bibliothek konnte nicht geladen werden.",
                    exception.Message));
            }
        }
    }

    private static nint Resolve(string libraryName, nint sdlHandle, nint shimHandle) =>
        libraryName switch
        {
            Sdl3Native.LibraryKey => sdlHandle,
            BgfxShimNative.LibraryName => shimHandle,
            _ => 0,
        };
}
