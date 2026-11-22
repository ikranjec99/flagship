using System.Collections.Concurrent;
using System.Reflection;

namespace Flagship.Core.DataAccess;

internal static class SqlScriptLoader
{
    private static readonly Assembly Assembly = typeof(SqlScriptLoader).Assembly;
    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.Ordinal);

    public static string Load(string relativePath) => Cache.GetOrAdd(relativePath, static path =>
    {
        var resourceName = $"Flagship.Core.DataAccess.Scripts.{path.Replace('/', '.')}";
        using var stream = Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded SQL script '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });
}
