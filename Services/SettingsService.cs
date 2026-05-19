using System.IO;
using System.Text.Json;
using CursorMagic.Models;

namespace CursorMagic.Services;

public sealed class SettingsService
{
    public AppSettings Load()
    {
        AppPaths.Ensure();
        if (!File.Exists(AppPaths.SettingsPath))
        {
            return new AppSettings
            {
                BlockedProcessNames =
                [
                    "obs64",
                    "devenv",
                    "mstsc",
                    "parsecd",
                    "steam",
                    "epicgameslauncher"
                ]
            };
        }

        try
        {
            var json = File.ReadAllText(AppPaths.SettingsPath);
            return JsonSerializer.Deserialize(json, CursorJsonContext.Default.AppSettings) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        AppPaths.Ensure();
        settings.AnimationScale = Math.Clamp(settings.AnimationScale <= 0 ? 1 : settings.AnimationScale, 0.45, 2.0);
        settings.AnimationBrightness = Math.Clamp(settings.AnimationBrightness <= 0 ? 1 : settings.AnimationBrightness, 0.35, 1.8);
        settings.ThemeAnimationOverrides = settings.ThemeAnimationOverrides
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .GroupBy(pair => NormalizeThemeId(pair.Key), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var value = group.Last().Value;
                    return new ThemeAnimationSettings
                    {
                        Scale = Math.Clamp(value.Scale <= 0 ? 1 : value.Scale, 0.45, 2.0),
                        Brightness = Math.Clamp(value.Brightness <= 0 ? 1 : value.Brightness, 0.35, 1.8)
                    };
                },
                StringComparer.OrdinalIgnoreCase);
        settings.ThemeCursorSizeOverrides = settings.ThemeCursorSizeOverrides
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .GroupBy(pair => NormalizeThemeId(pair.Key), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => Math.Clamp(group.Last().Value <= 0 ? 1 : group.Last().Value, 0.7, 2.0),
                StringComparer.OrdinalIgnoreCase);
        settings.BlockedProcessNames = settings.BlockedProcessNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(NormalizeProcessName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        File.WriteAllText(
            AppPaths.SettingsPath,
            JsonSerializer.Serialize(settings, CursorJsonContext.Default.AppSettings));
    }

    public static string NormalizeProcessName(string processName)
    {
        var name = Path.GetFileNameWithoutExtension(processName.Trim());
        return name.ToLowerInvariant();
    }

    public static string NormalizeThemeId(string themeId) => themeId.Trim().ToLowerInvariant();
}
