using CursorMagic.Models;

namespace CursorMagic.Services;

public static class ThemeAnimationService
{
    public static ThemeAnimationSettings Get(AppSettings settings, string themeId)
    {
        if (settings.ThemeAnimationOverrides.TryGetValue(SettingsService.NormalizeThemeId(themeId), out var themeSettings)
            || settings.ThemeAnimationOverrides.TryGetValue(themeId, out themeSettings))
        {
            return Clamp(themeSettings);
        }

        return new ThemeAnimationSettings();
    }

    public static void Set(AppSettings settings, string themeId, double scale, double brightness)
    {
        settings.ThemeAnimationOverrides[SettingsService.NormalizeThemeId(themeId)] = new ThemeAnimationSettings
        {
            Scale = Math.Clamp(scale <= 0 ? 1 : scale, 0.45, 2.0),
            Brightness = Math.Clamp(brightness <= 0 ? 1 : brightness, 0.35, 1.8)
        };
    }

    public static AppSettings RuntimeSettings(AppSettings settings, string themeId)
    {
        var themeSettings = Get(settings, themeId);
        return new AppSettings
        {
            EffectsPaused = settings.EffectsPaused,
            StartWithWindows = settings.StartWithWindows,
            RestoreOnExit = settings.RestoreOnExit,
            ActiveThemeId = settings.ActiveThemeId,
            AnimationScale = themeSettings.Scale,
            AnimationBrightness = themeSettings.Brightness,
            ThemeEffectOverrides = settings.ThemeEffectOverrides,
            ThemeColorOverrides = settings.ThemeColorOverrides,
            ThemeAnimationOverrides = settings.ThemeAnimationOverrides,
            ThemeCursorSizeOverrides = settings.ThemeCursorSizeOverrides,
            BlockedProcessNames = settings.BlockedProcessNames
        };
    }

    private static ThemeAnimationSettings Clamp(ThemeAnimationSettings settings) => new()
    {
        Scale = Math.Clamp(settings.Scale <= 0 ? 1 : settings.Scale, 0.45, 2.0),
        Brightness = Math.Clamp(settings.Brightness <= 0 ? 1 : settings.Brightness, 0.35, 1.8)
    };
}
