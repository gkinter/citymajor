using System.Reflection;

namespace Forge.SimWasm;

/// <summary>
/// Reads game JSON packs embedded in the WASM assembly at publish time.
/// </summary>
internal static class WasmEmbeddedData
{
    private static readonly Assembly Assembly = typeof(WasmEmbeddedData).Assembly;

    public static string Read(string logicalName)
    {
        using var stream = Assembly.GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {logicalName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
