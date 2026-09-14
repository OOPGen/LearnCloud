using System.Reflection;
using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.MultiTenancy.Context;

// Finds the entity types that make up the single LearnCloud database model.
//
// Feature modules reference MultiTenancy, so MultiTenancy cannot reference them back.
// Instead every LearnCloud.*.dll next to the running application is scanned once for
// concrete BaseEntity subclasses and IEntityTypeConfiguration classes. The API host,
// EF design-time tooling and integration tests all run from a directory that contains
// every module, so they build the same model.
public static class LearnCloudModel
{
    private static readonly Lazy<IReadOnlyList<Assembly>> _assemblies = new(Discover);

    public static IReadOnlyList<Assembly> Assemblies => _assemblies.Value;

    public static IEnumerable<Type> EntityTypes(Assembly assembly) =>
        SafeGetTypes(assembly).Where(t =>
            t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } &&
            typeof(BaseEntity).IsAssignableFrom(t) &&
            !IsTestType(t));

    private static IReadOnlyList<Assembly> Discover()
    {
        var byName = new SortedDictionary<string, Assembly>(StringComparer.Ordinal);
        foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = loaded.GetName().Name;
            if (IsLearnCloudModule(name)) byName[name!] = loaded;
        }
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "LearnCloud.*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (!IsLearnCloudModule(name) || byName.ContainsKey(name)) continue;
            try { byName[name] = Assembly.Load(new AssemblyName(name)); }
            catch (FileLoadException) { }
            catch (BadImageFormatException) { }
        }
        return byName.Values.ToList();
    }

    private static bool IsLearnCloudModule(string? name) =>
        name is not null &&
        name.StartsWith("LearnCloud.", StringComparison.Ordinal) &&
        !name.EndsWith("Tests", StringComparison.Ordinal);

    // Unit tests still live inside some production assemblies (to be split out later).
    private static bool IsTestType(Type t) =>
        t.Namespace?.Split('.').Contains("Tests") == true;

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
    }

    private static readonly HashSet<string> Uncountable = new(StringComparer.Ordinal)
    {
        "settings", "staff", "sms", "progress", "data", "news", "equipment"
    };

    // Final table name: snake_case with the last word pluralised, e.g.
    // StudentMark -> student_marks, AIProviderSettings -> ai_provider_settings.
    // Explicitly configured names ("users") are left alone by the caller.
    public static string PluralTableName(string clrName)
    {
        var words = System.Text.RegularExpressions.Regex
            .Split(clrName, "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")
            .Where(w => w.Length > 0)
            .Select(w => w.ToLowerInvariant())
            .ToList();
        var last = words[^1];
        words[^1] = Uncountable.Contains(last) ? last
            : last.EndsWith('y') && last.Length > 1 && !"aeiou".Contains(last[^2]) ? last[..^1] + "ies"
            : last.EndsWith('s') || last.EndsWith('x') || last.EndsWith('z') || last.EndsWith("ch") || last.EndsWith("sh") ? last + "es"
            : last + "s";
        return string.Join('_', words);
    }
}
