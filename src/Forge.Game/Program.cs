using System.Reflection;
using System.Runtime.InteropServices;
using Forge.Engine.Core;

namespace Forge.Game;

public static class Program
{
    public static void Main(string[] args)
    {
        RegisterNativeLibraryResolvers();

        var config = new Config
        {
            WindowTitle = "Iron & Oak - City Builder",
            WindowWidth = 1280,
            WindowHeight = 720,
            WorldSize = 512,
            ChunkSize = 64,
            DebugOverlay = true,
            VSync = true,
        };

        var game = new IronAndOakGame(config);
        game.Run();
    }

    /// <summary>
    /// Register native library resolvers so .NET can find SDL2 from Homebrew on macOS.
    /// The ppy.SDL2-CS NuGet package only bundles x64 native libs, not arm64.
    /// On Apple Silicon Macs, we fall back to the Homebrew-installed libSDL2.
    /// </summary>
    private static void RegisterNativeLibraryResolvers()
    {
        NativeLibrary.SetDllImportResolver(
            typeof(SDL2.SDL).Assembly,
            NativeLibraryResolver);
    }

    private static IntPtr NativeLibraryResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != "SDL2")
            return IntPtr.Zero;

        // Try default resolution first (works on Windows, Linux, and Intel Mac with bundled libs)
        if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out IntPtr handle))
            return handle;

        // macOS Homebrew paths (Apple Silicon and Intel)
        string[] homebrewPaths =
        [
            "/opt/homebrew/lib/libSDL2.dylib",     // Apple Silicon
            "/usr/local/lib/libSDL2.dylib",         // Intel Mac Homebrew
        ];

        foreach (string path in homebrewPaths)
        {
            if (NativeLibrary.TryLoad(path, out handle))
                return handle;
        }

        return IntPtr.Zero;
    }
}
