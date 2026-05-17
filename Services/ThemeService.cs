using System.IO;
using System.Text.Json;
using CursorMagic.Models;

namespace CursorMagic.Services;

public sealed class ThemeService
{
    private readonly CursorAssetGenerator _generator = new();

    public List<CursorTheme> LoadThemes()
    {
        var themes = _generator.EnsureBuiltInThemes();
        themes.AddRange(LoadUserThemes());
        return themes;
    }

    public CursorTheme CreateUserTheme(
        string name,
        string imagePath,
        int hotspotX,
        int hotspotY,
        string clickEffect,
        string decoration)
    {
        return _generator.CreateUserTheme(name, imagePath, hotspotX, hotspotY, clickEffect, decoration);
    }

    public CursorTheme? RebuildBuiltInTheme(string id, string accentColor)
    {
        return _generator.RebuildBuiltInTheme(id, accentColor);
    }

    private static IEnumerable<CursorTheme> LoadUserThemes()
    {
        AppPaths.Ensure();
        foreach (var path in Directory.EnumerateFiles(AppPaths.UserThemes, "theme.json", SearchOption.AllDirectories))
        {
            CursorTheme? theme = null;
            try
            {
                var json = File.ReadAllText(path);
                theme = JsonSerializer.Deserialize(json, CursorJsonContext.Default.CursorTheme);
            }
            catch
            {
                // Ignore malformed user packs so one bad pack does not prevent the app opening.
            }

            if (theme is not null)
            {
                yield return theme;
            }
        }
    }
}
