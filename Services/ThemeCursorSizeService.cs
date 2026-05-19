using CursorMagic.Models;

namespace CursorMagic.Services;

public static class ThemeCursorSizeService
{
    public static double Get(AppSettings settings, string themeId)
    {
        if (settings.ThemeCursorSizeOverrides.TryGetValue(SettingsService.NormalizeThemeId(themeId), out var size)
            || settings.ThemeCursorSizeOverrides.TryGetValue(themeId, out size))
        {
            return Clamp(size);
        }

        return 1.0;
    }

    public static void Set(AppSettings settings, string themeId, double size)
    {
        settings.ThemeCursorSizeOverrides[SettingsService.NormalizeThemeId(themeId)] = Clamp(size);
    }

    private static double Clamp(double value) => Math.Clamp(value <= 0 ? 1 : value, 0.7, 1.35);
}
