using System.IO;
using System.IO.Compression;
using System.Text.Json;
using CursorMagic.Models;

namespace CursorMagic.Services;

public sealed class CursorPackService
{
    public void Export(CursorTheme theme, string destinationPath)
    {
        using var archive = ZipFile.Open(destinationPath, ZipArchiveMode.Create);
        var manifest = CloneForPack(theme);
        manifest.PreviewPath = Path.GetFileName(theme.PreviewPath);
        foreach (var variant in manifest.Variants)
        {
            variant.AssetPath = Path.GetFileName(variant.AssetPath);
        }

        AddTextEntry(archive, "theme.json", JsonSerializer.Serialize(manifest, CursorJsonContext.Default.CursorTheme));
        AddFileIfExists(archive, theme.PreviewPath, Path.GetFileName(theme.PreviewPath));
        foreach (var assetPath in theme.Variants.Select(v => v.AssetPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            AddFileIfExists(archive, assetPath, Path.GetFileName(assetPath));
        }
    }

    public CursorTheme Import(string sourcePath)
    {
        AppPaths.Ensure();
        using var archive = ZipFile.OpenRead(sourcePath);
        var manifestEntry = archive.GetEntry("theme.json")
            ?? throw new InvalidOperationException("This pack does not contain a theme.json manifest.");

        CursorTheme theme;
        using (var stream = manifestEntry.Open())
        {
            theme = JsonSerializer.Deserialize(stream, CursorJsonContext.Default.CursorTheme)
                ?? throw new InvalidOperationException("Could not read the cursor pack manifest.");
        }

        var idBase = string.IsNullOrWhiteSpace(theme.Id) ? "imported-theme" : Slug(theme.Id);
        var id = idBase;
        var folder = Path.Combine(AppPaths.UserThemes, id);
        var suffix = 1;
        while (Directory.Exists(folder))
        {
            id = $"{idBase}-{suffix++}";
            folder = Path.Combine(AppPaths.UserThemes, id);
        }

        Directory.CreateDirectory(folder);
        foreach (var entry in archive.Entries.Where(e => !string.IsNullOrWhiteSpace(e.Name)))
        {
            entry.ExtractToFile(Path.Combine(folder, entry.Name), overwrite: true);
        }

        theme.Id = id;
        theme.IsUserTheme = true;
        theme.IsActive = false;
        theme.PreviewPath = Path.Combine(folder, Path.GetFileName(theme.PreviewPath));
        foreach (var variant in theme.Variants)
        {
            variant.AssetPath = Path.Combine(folder, Path.GetFileName(variant.AssetPath));
        }

        File.WriteAllText(
            Path.Combine(folder, "theme.json"),
            JsonSerializer.Serialize(theme, CursorJsonContext.Default.CursorTheme));
        return theme;
    }

    private static CursorTheme CloneForPack(CursorTheme theme)
    {
        return new CursorTheme
        {
            Id = theme.Id,
            Name = theme.Name,
            Description = theme.Description,
            Category = theme.Category,
            PreviewPath = theme.PreviewPath,
            IsUserTheme = true,
            Effect = new ClickEffect
            {
                Type = theme.Effect.Type,
                PrimaryColor = theme.Effect.PrimaryColor,
                SecondaryColor = theme.Effect.SecondaryColor,
                ParticleCount = theme.Effect.ParticleCount,
                Radius = theme.Effect.Radius,
                DurationMs = theme.Effect.DurationMs
            },
            Variants = theme.Variants.Select(v => new CursorVariant
            {
                Role = v.Role,
                AssetPath = v.AssetPath,
                HotspotX = v.HotspotX,
                HotspotY = v.HotspotY,
                Size = v.Size
            }).ToList()
        };
    }

    private static void AddTextEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    private static void AddFileIfExists(ZipArchive archive, string path, string entryName)
    {
        if (File.Exists(path) && archive.GetEntry(entryName) is null)
        {
            archive.CreateEntryFromFile(path, entryName, CompressionLevel.Optimal);
        }
    }

    private static string Slug(string value)
    {
        var chars = value.ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var slug = string.Join("-", new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(slug) ? $"imported-{DateTime.Now:yyyyMMddHHmmss}" : slug;
    }
}
