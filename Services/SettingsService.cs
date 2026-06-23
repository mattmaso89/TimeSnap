using System.Text.Json;

namespace TimeSnap.Services;

// Persists AppSettings as JSON under %AppData%\TimeSnap\settings.json.
// The app runs unpackaged (no MSIX identity), so Windows.Storage.ApplicationData
// isn't reliably available — a plain JSON file works regardless of packaging.
internal static class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TimeSnap", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppSettings Current { get; private set; } = Load();

    // The screenshot folder actually in effect — falls back to the default
    // location if the configured value is somehow blank.
    public static string EffectiveScreenshotFolder =>
        string.IsNullOrWhiteSpace(Current.ScreenshotFolder)
            ? AppSettings.DefaultScreenshotFolder
            : Current.ScreenshotFolder;

    public static void Save(AppSettings settings)
    {
        Current = settings;
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
                if (settings is not null)
                    return settings;
            }
        }
        catch
        {
            // Corrupt or unreadable settings file — fall back to defaults.
        }
        return new AppSettings();
    }
}
