using System.Text.Json;
using LogScope.Core.Documents;

namespace LogScope.Core.Persistence;

/// <summary>
/// Stores named parser profiles as JSON files in the app data location, and
/// supports exporting/importing individual profiles to arbitrary local files (UR-06).
/// </summary>
public sealed class ProfileRepository
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private readonly string _directory;

    public ProfileRepository(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
    }

    public void Save(LogProfile profile)
    {
        var path = Path.Combine(_directory, SafeFileName(profile.Name) + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(LogProfileDto.From(profile), Options));
    }

    public IReadOnlyList<LogProfile> LoadAll()
    {
        if (!Directory.Exists(_directory))
            return [];

        var profiles = new List<LogProfile>();
        foreach (var file in Directory.EnumerateFiles(_directory, "*.json"))
        {
            try
            {
                var dto = JsonSerializer.Deserialize<LogProfileDto>(File.ReadAllText(file), Options);
                if (dto != null)
                    profiles.Add(dto.ToProfile());
            }
            catch (JsonException)
            {
                // skip a corrupt profile file rather than failing the whole load
            }
        }
        return profiles.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public void Delete(string profileName)
    {
        var path = Path.Combine(_directory, SafeFileName(profileName) + ".json");
        if (File.Exists(path))
            File.Delete(path);
    }

    public void Export(LogProfile profile, string destinationPath) =>
        File.WriteAllText(destinationPath, JsonSerializer.Serialize(LogProfileDto.From(profile), Options));

    public LogProfile Import(string sourcePath)
    {
        var dto = JsonSerializer.Deserialize<LogProfileDto>(File.ReadAllText(sourcePath), Options)
                  ?? throw new InvalidDataException($"Not a valid profile file: {sourcePath}");
        return dto.ToProfile();
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "profile" : cleaned;
    }
}
