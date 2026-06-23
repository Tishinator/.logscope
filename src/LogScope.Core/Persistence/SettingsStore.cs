using System.Text.Json;

namespace LogScope.Core.Persistence;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON. Corrupt or missing files
/// degrade gracefully to defaults rather than throwing (SR-07).
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;

    public SettingsStore(string filePath) => _filePath = filePath;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new AppSettings();

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(settings, Options);
        File.WriteAllText(_filePath, json);
    }

    public void Reset()
    {
        if (File.Exists(_filePath))
            File.Delete(_filePath);
    }
}
