using System.Text.RegularExpressions;

namespace Gta6Countdown;

public sealed class WidgetSettings
{
    public double Blur { get; set; } = 34;
    public double Brightness { get; set; } = 0.52;
    public double Saturation { get; set; } = 1.4;
    public double Glow { get; set; } = 1.0;
    public double Grain { get; set; } = 0.085;
    public double Vignette { get; set; } = 0.9;
    public double NumberScale { get; set; } = 1.0;
    public bool Motion { get; set; } = true;
    public string Accent { get; set; } = "#ff2f96";
    public string Language { get; set; } = "zh";
    public bool ShowTrayIcon { get; set; } = true;

    public WidgetSettings Clone() => (WidgetSettings)MemberwiseClone();

    public WidgetSettings Sanitized()
    {
        var clean = Clone();
        clean.Blur = Math.Clamp(clean.Blur, 0, 60);
        clean.Brightness = Math.Clamp(clean.Brightness, 0.25, 0.85);
        clean.Saturation = Math.Clamp(clean.Saturation, 0.6, 2.0);
        clean.Glow = Math.Clamp(clean.Glow, 0, 1.5);
        clean.Grain = Math.Clamp(clean.Grain, 0, 0.2);
        clean.Vignette = Math.Clamp(clean.Vignette, 0, 1);
        clean.NumberScale = Math.Clamp(clean.NumberScale, 0.75, 1.25);
        clean.Language = string.Equals(clean.Language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";
        clean.Accent = NormalizeAccent(clean.Accent);
        return clean;
    }

    public static string NormalizeAccent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "#ff2f96";
        var candidate = value.Trim();
        if (!candidate.StartsWith('#')) candidate = "#" + candidate;
        return Regex.IsMatch(candidate, "^#[0-9a-fA-F]{6}$") ? candidate.ToLowerInvariant() : "#ff2f96";
    }

    public static WidgetSettings Defaults() => new();
}
