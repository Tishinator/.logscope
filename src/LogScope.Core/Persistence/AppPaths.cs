namespace LogScope.Core.Persistence;

/// <summary>
/// Resolves the application's local user-data locations. All profiles, preferences,
/// caches, and indexes live here — never inside the selected workspace (SR-02).
/// </summary>
public static class AppPaths
{
    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "logscope");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public static string ProfilesDirectory => Path.Combine(DataDirectory, "profiles");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(ProfilesDirectory);
    }
}
