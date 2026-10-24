using System.IO;
using System.Text.Json;

namespace Gta6Countdown;

internal static class SettingsService
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string SettingsFile => Path.Combine(WidgetPaths.DataDirectory, "settings.json");

    public static WidgetSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return WidgetSettings.Defaults();

            var json = File.ReadAllText(SettingsFile);
            var settings = JsonSerializer.Deserialize<WidgetSettings>(json, JsonOptions);
            return (settings ?? WidgetSettings.Defaults()).Sanitized();
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Settings load failed; using defaults: {ex.Message}");
            return WidgetSettings.Defaults();
        }
    }

    public static void Save(WidgetSettings settings)
    {
        try
        {
            Directory.CreateDirectory(WidgetPaths.DataDirectory);
            var temp = SettingsFile + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings.Sanitized(), JsonOptions));
            File.Move(temp, SettingsFile, true);
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Settings save failed: {ex.Message}");
        }
    }

    public static string ToJson(WidgetSettings settings) =>
        JsonSerializer.Serialize(settings.Sanitized(), JsonOptions);
}
