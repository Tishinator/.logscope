namespace LogScope.Core.Persistence;

/// <summary>
/// Determines which profile applies to a file: a per-file override wins; otherwise
/// the nearest ancestor directory with an assignment; otherwise none (UR-06).
/// </summary>
public sealed class ProfileResolver
{
    public string? Resolve(
        string filePath,
        IReadOnlyDictionary<string, string> directoryAssignments,
        IReadOnlyDictionary<string, string> fileOverrides)
    {
        foreach (var kv in fileOverrides)
        {
            if (PathsEqual(kv.Key, filePath))
                return kv.Value;
        }

        string? bestProfile = null;
        int bestLength = -1;
        var normalizedFile = NormalizeDir(Path.GetDirectoryName(filePath) ?? filePath);

        foreach (var kv in directoryAssignments)
        {
            var assignedDir = NormalizeDir(kv.Key);
            if (IsAncestorOrSame(assignedDir, normalizedFile) && assignedDir.Length > bestLength)
            {
                bestLength = assignedDir.Length;
                bestProfile = kv.Value;
            }
        }

        return bestProfile;
    }

    private static bool IsAncestorOrSame(string ancestor, string descendant) =>
        descendant.Equals(ancestor, StringComparison.OrdinalIgnoreCase) ||
        descendant.StartsWith(ancestor + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeDir(string path) =>
        path.Replace('/', '\\').TrimEnd('\\');

    private static bool PathsEqual(string a, string b) =>
        NormalizeDir(a).Equals(NormalizeDir(b), StringComparison.OrdinalIgnoreCase);
}
