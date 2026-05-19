using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CursorMagic.Models;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CursorMagic.Services;

public sealed class CursorAssetGenerator
{
    private const int LogicalSize = 48;
    private const int CursorPixelSize = 128;
    private const int PreviewPixelSize = 512;
    private const string AssetRevision = "q32";

    private static readonly string[] ThemeRoles =
    [
        "Arrow",
        "Help",
        "AppStarting",
        "Wait",
        "Crosshair",
        "IBeam",
        "NWPen",
        "No",
        "SizeNS",
        "SizeWE",
        "SizeNWSE",
        "SizeNESW",
        "SizeAll",
        "UpArrow",
        "Hand",
        "Pin",
        "Person"
    ];

    public List<CursorTheme> EnsureBuiltInThemes()
    {
        AppPaths.Ensure();
        var themes = BuiltInSpecs().Select(EnsureBuiltInTheme).ToList();
        File.WriteAllText(
            Path.Combine(AppPaths.BuiltInThemes, "themes.json"),
            JsonSerializer.Serialize(themes, CursorJsonContext.Default.ListCursorTheme));
        return themes;
    }

    public CursorTheme? RebuildBuiltInTheme(string id, string accentColor)
    {
        return RebuildBuiltInTheme(id, accentColor, 1.0, 1.0, 1.0);
    }

    public CursorTheme? RebuildBuiltInTheme(string id, string accentColor, double glowScale, double glowBrightness)
    {
        return RebuildBuiltInTheme(id, accentColor, glowScale, glowBrightness, 1.0);
    }

    public CursorTheme? RebuildBuiltInTheme(string id, string accentColor, double glowScale, double glowBrightness, double cursorScale)
    {
        var spec = BuiltInSpecs().FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (spec is null)
        {
            return null;
        }

        return EnsureBuiltInTheme(spec with
        {
            AccentColor = accentColor,
            GlowScale = Math.Clamp(glowScale <= 0 ? 1 : glowScale, 0.45, 2.0),
            GlowBrightness = Math.Clamp(glowBrightness <= 0 ? 1 : glowBrightness, 0.35, 1.8),
            CursorScale = Math.Clamp(cursorScale <= 0 ? 1 : cursorScale, 0.7, 1.35),
            Effect = EffectWithAccent(spec.Effect, accentColor)
        });
    }

    public CursorTheme CreateUserTheme(
        string name,
        string imagePath,
        int hotspotX,
        int hotspotY,
        string clickEffect,
        string decoration)
    {
        AppPaths.Ensure();
        var id = Slug(name);
        var themeFolder = Path.Combine(AppPaths.UserThemes, id);
        var suffix = 1;
        while (Directory.Exists(themeFolder))
        {
            id = $"{Slug(name)}-{suffix++}";
            themeFolder = Path.Combine(AppPaths.UserThemes, id);
        }

        Directory.CreateDirectory(themeFolder);

        var bitmap = LoadBitmap(imagePath);
        var cursorArt = RenderUserCursor(bitmap, decoration, CursorPixelSize);
        var previewArt = RenderUserCursor(bitmap, decoration, PreviewPixelSize);
        var previewPath = Path.Combine(themeFolder, "preview.png");
        var cursorPath = Path.Combine(themeFolder, "arrow.cur");
        SavePng(previewArt, previewPath);
        SaveCursor(cursorArt, cursorPath, hotspotX, hotspotY);

        var effect = EffectFor(clickEffect);
        var theme = new CursorTheme
        {
            Id = id,
            Name = name,
            Category = "Custom",
            Description = "Created in Cursor Magic.",
            PreviewPath = previewPath,
            IsUserTheme = true,
            Effect = effect,
            Variants = VariantsFor(cursorPath, hotspotX, hotspotY)
        };

        File.WriteAllText(
            Path.Combine(themeFolder, "theme.json"),
            JsonSerializer.Serialize(theme, CursorJsonContext.Default.CursorTheme));
        return theme;
    }

    public static ImageSource LoadPreview(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private CursorTheme EnsureBuiltInTheme(BuiltInThemeSpec spec)
    {
        var folder = Path.Combine(AppPaths.BuiltInThemes, spec.Id);
        Directory.CreateDirectory(folder);
        var variantSuffix = BuiltInThemeUsesAccent(spec.Id)
            ? $"-{ColorSlug(spec.AccentColor)}"
            : "";
        var glowVariantSuffix = spec.Id.Equals("lightsaber", StringComparison.OrdinalIgnoreCase)
            ? $"{variantSuffix}-s{SettingSlug(spec.GlowScale)}-b{SettingSlug(spec.GlowBrightness)}"
            : variantSuffix;
        var cursorScaleSuffix = $"-c{CursorScaleSlug(spec.CursorScale)}";
        var previewPath = Path.Combine(folder, $"preview-{AssetRevision}{variantSuffix}{cursorScaleSuffix}.png");
        var cursorPath = Path.Combine(folder, $"arrow-{AssetRevision}{variantSuffix}{cursorScaleSuffix}.cur");

        if (!File.Exists(previewPath))
        {
            var previewArt = RenderBuiltIn(spec, PreviewPixelSize);
            SavePng(previewArt, previewPath);
        }

        if (!File.Exists(cursorPath))
        {
            var cursorArt = RenderBuiltIn(spec, CursorPixelSize);
            SaveCursor(cursorArt, cursorPath, spec.HotspotX, spec.HotspotY);
        }

        var glowPath = "";
        if (spec.Id is "lightsaber" or "omnitrix")
        {
            glowPath = Path.Combine(folder, $"arrow-glow-{AssetRevision}{glowVariantSuffix}.cur");
            if (!File.Exists(glowPath))
            {
                var glowArt = RenderBuiltInGlow(spec, CursorPixelSize);
                SaveCursor(glowArt, glowPath, spec.HotspotX, spec.HotspotY);
            }
        }

        return new CursorTheme
        {
            Id = spec.Id,
            Name = spec.Name,
            Description = spec.Description,
            Category = spec.Category,
            PreviewPath = previewPath,
            Effect = spec.Effect,
            Variants = VariantsFor(cursorPath, spec.HotspotX, spec.HotspotY),
            GlowCursorPath = glowPath
        };
    }

    private static List<CursorVariant> VariantsFor(string cursorPath, int hotspotX, int hotspotY)
    {
        return ThemeRoles.Select(role => new CursorVariant
        {
            Role = role,
            AssetPath = cursorPath,
            HotspotX = hotspotX,
            HotspotY = hotspotY,
            Size = CursorPixelSize
        }).ToList();
    }

    private static bool BuiltInThemeUsesAccent(string id) =>
        id.Equals("lightsaber", StringComparison.OrdinalIgnoreCase);

    private static string ColorSlug(string color)
    {
        var slug = new string(color
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
        return string.IsNullOrWhiteSpace(slug) ? "accent" : slug;
    }

    private static string SettingSlug(double value) =>
        Math.Clamp(value <= 0 ? 1 : value, 0.35, 2.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace(".", "");

    private static string CursorScaleSlug(double value) =>
        Math.Clamp(value <= 0 ? 1 : value, 0.7, 1.35).ToString("0.00", CultureInfo.InvariantCulture).Replace(".", "");

    private static RenderTargetBitmap RenderBuiltInGlow(BuiltInThemeSpec spec, int outputSize)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(outputSize / (double)LogicalSize, outputSize / (double)LogicalSize));
            dc.PushTransform(new TranslateTransform(LogicalSize / 2.0, LogicalSize / 2.0));
            dc.PushTransform(new ScaleTransform(spec.CursorScale, spec.CursorScale));
            dc.PushTransform(new TranslateTransform(-LogicalSize / 2.0, -LogicalSize / 2.0));
            switch (spec.Id)
            {
                case "lightsaber":
                    DrawLightsaberGlow(dc, spec.AccentColor, spec.GlowScale, spec.GlowBrightness);
                    break;
                case "omnitrix":
                    DrawOmnitrixGlow(dc);
                    break;
                default:
                    return RenderBuiltIn(spec, outputSize);
            }
            dc.Pop();
        }
        return RenderVisual(visual, outputSize);
    }

    private static RenderTargetBitmap RenderBuiltIn(BuiltInThemeSpec spec, int outputSize)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(outputSize / (double)LogicalSize, outputSize / (double)LogicalSize));
            dc.PushTransform(new TranslateTransform(LogicalSize / 2.0, LogicalSize / 2.0));
            dc.PushTransform(new ScaleTransform(spec.CursorScale, spec.CursorScale));
            dc.PushTransform(new TranslateTransform(-LogicalSize / 2.0, -LogicalSize / 2.0));
            switch (spec.Id)
            {
                case "star-wand":
                    DrawStarWand(dc);
                    break;
                case "neon-ring":
                    DrawArrow(dc, "#05070B", "#55F7FF", "#FF4FD8");
                    dc.DrawEllipse(null, Pen("#55F7FF", 2), new Point(31, 18), 7, 7);
                    break;
                case "crystal-arrow":
                    DrawCrystal(dc);
                    break;
                case "pixel-sword":
                    DrawPixelSword(dc);
                    break;
                case "moonbeam":
                    DrawCrescent(dc);
                    break;
                case "bubble-pop":
                    DrawBubble(dc);
                    break;
                case "heart-charm":
                    DrawHeartCursor(dc);
                    break;
                case "glass-shard":
                    DrawShard(dc);
                    break;
                case "firefly-trail":
                    DrawArrow(dc, "#17251A", "#C9FF7A", "#FFEA7A");
                    dc.DrawEllipse(Brush("#F8FF8A"), null, new Point(34, 11), 2.5, 2.5);
                    dc.DrawEllipse(Brush("#C9FF7A"), null, new Point(38, 18), 1.8, 1.8);
                    break;
                case "sketch-pen":
                    DrawSketchPen(dc);
                    break;
                case "candy-bolt":
                    DrawCandyBolt(dc);
                    break;
                case "minimal-crosshair":
                    DrawCrosshair(dc);
                    break;
                case "lightsaber":
                    DrawLightsaber(dc, spec.AccentColor);
                    break;
                case "hero-sword":
                    DrawHeroSword(dc);
                    break;
                case "eclipse-sword":
                    DrawEclipseSword(dc);
                    break;
                case "omnitrix":
                    DrawOmnitrix(dc);
                    break;
                case "tardis":
                    DrawTardis(dc);
                    break;
                case "autobot-crest":
                    DrawAutobotCrest(dc);
                    break;
                case "decepticon-crest":
                    DrawDecepticonCrest(dc);
                    break;
                case "shera-sword":
                    DrawSheraSword(dc);
                    break;
                case "ancient-staff":
                    DrawAncientStaff(dc);
                    break;
                case "starfleet-delta":
                    DrawStarfleetDelta(dc);
                    break;
                case "starship":
                    DrawStarship(dc);
                    break;
                case "dark-one-dagger":
                    DrawDarkOneDagger(dc);
                    break;
            }
            dc.Pop();
            dc.Pop();
            dc.Pop();
            dc.Pop();
        }

        return RenderVisual(visual, outputSize);
    }

    private static RenderTargetBitmap RenderUserCursor(BitmapSource source, string decoration, int outputSize)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(outputSize / (double)LogicalSize, outputSize / (double)LogicalSize));
            var scale = Math.Min(36 / source.Width, 36 / source.Height);
            var width = source.Width * scale;
            var height = source.Height * scale;
            dc.DrawImage(source, new Rect((LogicalSize - width) / 2, (LogicalSize - height) / 2, width, height));

            if (decoration == "Star")
            {
                DrawStar(dc, new Point(36, 12), 8, 4, Brush("#FFE36A"), Pen("#743E9E", 1.4));
            }
            else if (decoration == "Heart")
            {
                DrawHeart(dc, new Point(36, 13), 0.55, Brush("#F25AAE"), Pen("#6E2B5C", 1.2));
            }
            else if (decoration == "Ring")
            {
                dc.DrawEllipse(null, Pen("#55F7FF", 2.2), new Point(35, 14), 8, 8);
            }

            dc.DrawEllipse(Brush("#FFFFFFFF"), Pen("#272B35", 1), new Point(4, 4), 3, 3);
            dc.Pop();
        }
        return RenderVisual(visual, outputSize);
    }

    private static RenderTargetBitmap RenderVisual(DrawingVisual visual, int outputSize)
    {
        var bitmap = new RenderTargetBitmap(outputSize, outputSize, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapSource LoadBitmap(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static void SavePng(BitmapSource bitmap, string path)
    {
        using var stream = File.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
    }

    private static void SaveCursor(BitmapSource bitmap, string path, int hotspotX, int hotspotY)
    {
        using var pngStream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(pngStream);
        var png = pngStream.ToArray();

        using var writer = new BinaryWriter(File.Create(path));
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;
        var scaledHotspotX = (int)Math.Round(hotspotX * (width / (double)LogicalSize));
        var scaledHotspotY = (int)Math.Round(hotspotY * (height / (double)LogicalSize));
        writer.Write((ushort)0);
        writer.Write((ushort)2);
        writer.Write((ushort)1);
        writer.Write((byte)(width >= 256 ? 0 : width));
        writer.Write((byte)(height >= 256 ? 0 : height));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)Math.Clamp(scaledHotspotX, 0, width - 1));
        writer.Write((ushort)Math.Clamp(scaledHotspotY, 0, height - 1));
        writer.Write(png.Length);
        writer.Write(22);
        writer.Write(png);
    }

    private static void DrawStarWand(DrawingContext dc)
    {
        if (DrawStarWandFromReference(dc))
        {
            return;
        }

        DrawStarWandVectorFallback(dc);
    }

    private static bool DrawStarWandFromReference(DrawingContext dc)
    {
        var referencePath = Path.Combine(AppContext.BaseDirectory, "Assets", "StarWandReference.png");
        if (!File.Exists(referencePath))
        {
            return false;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(referencePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            var scale = 0.00515;
            var transform = new TransformGroup();
            transform.Children.Add(new TranslateTransform(-bitmap.PixelWidth / 2.0, -bitmap.PixelHeight / 2.0));
            transform.Children.Add(new RotateTransform(43));
            transform.Children.Add(new ScaleTransform(scale, scale));
            transform.Children.Add(new TranslateTransform(24, 20.0));

            dc.PushTransform(transform);
            dc.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            dc.Pop();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void DrawStarWandVectorFallback(DrawingContext dc)
    {
        var ink = Pen("#111118", 1.65);

        DrawWandWing(dc, true);
        DrawWandWing(dc, false);
        DrawWandHorn(dc, true);
        DrawWandHorn(dc, false);

        var handle = Geometry.Parse("M 21.2,25.8 L 26.8,25.8 L 26.8,43.1 C 26.8,44.2 25.8,44.9 24,44.9 C 22.2,44.9 21.2,44.2 21.2,43.1 Z");
        dc.DrawGeometry(Linear("#8268C9", "#4F419D", 0, 0, 1, 1), ink, handle);
        dc.PushClip(handle);
        dc.DrawGeometry(Brush("#B99DF5"), null, Geometry.Parse("M 21.3,26.0 L 26.9,31.4 L 26.9,36.2 L 21.3,30.8 Z"));
        dc.DrawGeometry(Brush("#C5A8FF"), null, Geometry.Parse("M 21.3,34.9 L 26.9,40.4 L 26.9,44.5 L 21.3,39.0 Z"));
        dc.DrawGeometry(Brush("#372A83"), null, Geometry.Parse("M 21.3,30.8 L 26.9,36.2 L 26.9,40.2 L 21.3,34.9 Z"));
        dc.DrawLine(Pen("#D4C7FF", 0.5, 0.45), new Point(21.9, 26.3), new Point(21.9, 43.2));
        dc.Pop();

        dc.DrawEllipse(Linear("#FFE26D", "#C7922D", 0, 0, 0, 1), ink, new Point(24, 43.1), 5.0, 2.3);
        var gem = Geometry.Parse("M 24,43.5 L 27.8,45.7 L 24,48 L 20.2,45.7 Z");
        dc.DrawGeometry(Linear("#FF9FF5", "#B94DD5", 0, 0, 0, 1), Pen("#F7D5FF", 1.0), gem);
        dc.DrawLine(Pen("#FFFFFF", 0.75, 0.85), new Point(24, 43.5), new Point(24, 47.2));
        dc.DrawLine(Pen("#B754D5", 0.75), new Point(20.3,45.5), new Point(27.7,45.5));

        var neck = Geometry.Parse("M 20.9,24.2 L 27.1,24.2 L 24,27.5 Z");
        dc.DrawGeometry(Brush("#D1A445"), ink, neck);
        DrawHeart(dc, new Point(24, 26.7), 0.24, Linear("#FF61B5", "#CA2A80", 0, 0, 0, 1), Pen("#591B4B", 0.85));
        dc.DrawGeometry(Brush("#FF8ED0", 0.45), null, Geometry.Parse("M 23.1,20.2 C 20.9,19.4 20.2,23 23.4,25.3 C 23.0,23.4 23.1,21.5 23.1,20.2 Z"));

        dc.DrawEllipse(Linear("#C9A2F2", "#9F72DD", 0, 0, 1, 1), ink, new Point(24, 13.7), 11.9, 11.9);
        dc.DrawEllipse(Brush("#C9ECF7"), Pen("#111118", 1.25), new Point(24, 13.7), 9.0, 9.0);

        dc.DrawGeometry(Brush("#EAFBFF", 0.82), null, Geometry.Parse("M 17.0,10.9 L 20.8,5.8 L 22.0,11.4 Z"));
        dc.DrawGeometry(Brush("#ECFBFF", 0.65), null, Geometry.Parse("M 26.0,5.8 L 28.8,11.0 L 31.2,9.9 L 29.8,6.2 Z"));
        dc.DrawGeometry(Brush("#A9D8E8", 0.86), null, Geometry.Parse("M 16.6,17.0 L 20.8,16.5 L 17.5,20.1 Z"));
        dc.DrawGeometry(Brush("#9ED4E6", 0.82), null, Geometry.Parse("M 27.2,16.6 L 31.6,15.8 L 30.2,20.0 Z"));

        DrawStar(dc, new Point(24, 13.7), 7.2, 3.0, Linear("#FFE783", "#F2B736", 0, 0, 0, 1), Pen("#FFFFFF", 0.9));
        DrawStar(dc, new Point(24, 13.7), 3.0, 1.25, Brush("#F47A22"), null);
        dc.DrawLine(Pen("#FFF5BC", 0.55, 0.9), new Point(24, 6.9), new Point(24, 20.5));
        dc.DrawLine(Pen("#FFF5BC", 0.5, 0.82), new Point(17.0,13.7), new Point(31.0,13.7));

        foreach (var p in new[] { new Point(19.5,7.9), new Point(28.5,8.0), new Point(32.0,13.7), new Point(24,22.4), new Point(16.0,13.7) })
        {
            DrawHeart(dc, p, 0.062, Brush("#D43A9D"), null);
        }
    }

    private static void DrawWandWing(DrawingContext dc, bool left)
    {
        var s = left ? -1 : 1;
        var outer = new StreamGeometry();
        using (var ctx = outer.Open())
        {
            ctx.BeginFigure(new Point(24 + s * 10.7, 11.9), true, true);
            ctx.BezierTo(new Point(24 + s * 13.7, 2.7), new Point(24 + s * 20.5, -0.2), new Point(24 + s * 22.7, 8.6), true, false);
            ctx.BezierTo(new Point(24 + s * 25.5, 14.3), new Point(24 + s * 23.0, 20.0), new Point(24 + s * 18.4, 20.8), true, false);
            ctx.BezierTo(new Point(24 + s * 20.9, 22.2), new Point(24 + s * 19.5, 25.3), new Point(24 + s * 15.8, 24.1), true, false);
            ctx.BezierTo(new Point(24 + s * 13.8, 28.6), new Point(24 + s * 9.9, 27.1), new Point(24 + s * 10.5, 21.9), true, false);
            ctx.BezierTo(new Point(24 + s * 8.0, 19.1), new Point(24 + s * 8.5, 14.8), new Point(24 + s * 10.7, 11.9), true, false);
        }

        outer.Freeze();
        dc.DrawGeometry(Brush("#FFFFFF"), Pen("#111118", 1.65), outer);

        var inner = new StreamGeometry();
        using (var ctx = inner.Open())
        {
            ctx.BeginFigure(new Point(24 + s * 16.4, 7.8), true, true);
            ctx.BezierTo(new Point(24 + s * 20.2, 5.3), new Point(24 + s * 22.2, 11.9), new Point(24 + s * 17.4, 19.2), true, false);
            ctx.BezierTo(new Point(24 + s * 19.4, 14.2), new Point(24 + s * 18.4, 9.8), new Point(24 + s * 16.4, 7.8), true, false);
        }

        inner.Freeze();
        dc.DrawGeometry(Brush("#B4B0F0", 0.95), null, inner);

        var innerLobe = new StreamGeometry();
        using (var ctx = innerLobe.Open())
        {
            ctx.BeginFigure(new Point(24 + s * 18.4, 9.1), true, true);
            ctx.BezierTo(new Point(24 + s * 21.7, 10.5), new Point(24 + s * 20.5, 16.4), new Point(24 + s * 17.7, 19.2), true, false);
            ctx.BezierTo(new Point(24 + s * 18.7, 15.5), new Point(24 + s * 18.6, 12.3), new Point(24 + s * 18.4, 9.1), true, false);
        }

        innerLobe.Freeze();
        dc.DrawGeometry(Brush("#C8C4FF", 0.78), null, innerLobe);

        var lower = new StreamGeometry();
        using (var ctx = lower.Open())
        {
            ctx.BeginFigure(new Point(24 + s * 10.9, 20.0), true, true);
            ctx.BezierTo(new Point(24 + s * 14.5, 24.2), new Point(24 + s * 18.0, 23.5), new Point(24 + s * 20.7, 20.7), true, false);
            ctx.BezierTo(new Point(24 + s * 17.2, 27.2), new Point(24 + s * 10.8, 26.0), new Point(24 + s * 10.9, 20.0), true, false);
        }

        lower.Freeze();
        dc.DrawGeometry(Brush("#EDEAFF"), null, lower);

        var crease = new StreamGeometry();
        using (var ctx = crease.Open())
        {
            ctx.BeginFigure(new Point(24 + s * 13.2, 12.9), false, false);
            ctx.BezierTo(new Point(24 + s * 16.0, 15.0), new Point(24 + s * 17.7, 18.4), new Point(24 + s * 16.4, 23.2), true, false);
        }

        crease.Freeze();
        dc.DrawGeometry(null, Pen("#D7D4F4", 0.55, 0.75), crease);
    }

    private static void DrawWandHorn(DrawingContext dc, bool left)
    {
        var s = left ? -1 : 1;
        var horn = new StreamGeometry();
        using (var ctx = horn.Open())
        {
            ctx.BeginFigure(new Point(24 + s * 8.0, 4.4), true, true);
            ctx.BezierTo(new Point(24 + s * 9.3, 0.9), new Point(24 + s * 12.0, 0.5), new Point(24 + s * 11.2, 7.6), true, false);
            ctx.BezierTo(new Point(24 + s * 10.1, 9.0), new Point(24 + s * 8.4, 9.0), new Point(24 + s * 7.2, 8.0), true, false);
        }

        horn.Freeze();
        dc.DrawGeometry(Linear("#F062C0", "#A72A86", 0, 0, 0, 1), Pen("#111118", 1.25), horn);
    }

    private static void DrawArrow(DrawingContext dc, string fill, string stroke, string accent)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(4, 3), true, true);
            ctx.LineTo(new Point(33, 27), true, false);
            ctx.LineTo(new Point(21, 29), true, false);
            ctx.LineTo(new Point(28, 43), true, false);
            ctx.LineTo(new Point(20, 46), true, false);
            ctx.LineTo(new Point(13, 31), true, false);
            ctx.LineTo(new Point(5, 40), true, false);
        }

        geometry.Freeze();
        dc.DrawGeometry(Brush(fill), Pen(stroke, 2.4), geometry);
        dc.DrawLine(Pen(accent, 1.5), new Point(10, 10), new Point(25, 25));
    }

    private static void DrawCrystal(DrawingContext dc)
    {
        var geometry = Geometry.Parse("M 5,3 L 35,22 L 23,26 L 30,43 L 21,45 L 14,29 L 6,39 Z");
        dc.DrawGeometry(Brush("#D7FAFF"), Pen("#35495F", 2.2), geometry);
        dc.DrawLine(Pen("#78D7FF", 2), new Point(12, 9), new Point(22, 26));
        dc.DrawLine(Pen("#FFFFFF", 1.4), new Point(8, 7), new Point(32, 22));
    }

    private static void DrawPixelSword(DrawingContext dc)
    {
        dc.DrawRectangle(Brush("#23212E"), null, new Rect(9, 6, 6, 6));
        dc.DrawRectangle(Brush("#E8ECFF"), null, new Rect(15, 12, 6, 6));
        dc.DrawRectangle(Brush("#B8C2FF"), null, new Rect(21, 18, 6, 6));
        dc.DrawRectangle(Brush("#7A88D7"), null, new Rect(27, 24, 6, 6));
        dc.DrawRectangle(Brush("#322844"), null, new Rect(18, 30, 21, 5));
        dc.DrawRectangle(Brush("#F6C84D"), null, new Rect(30, 34, 5, 10));
        dc.DrawRectangle(null, Pen("#1D1826", 2), new Rect(9, 6, 30, 38));
    }

    private static void DrawCrescent(DrawingContext dc)
    {
        DrawArrow(dc, "#1E2433", "#DDE8FF", "#FFE78A");
        dc.DrawEllipse(Brush("#FFE78A"), null, new Point(33, 12), 8, 8);
        dc.DrawEllipse(Brush("#1E2433"), null, new Point(36, 9), 8, 8);
    }

    private static void DrawBubble(DrawingContext dc)
    {
        DrawArrow(dc, "#E7FCFF", "#56B6D6", "#F69BD8");
        dc.DrawEllipse(null, Pen("#9AF2FF", 2), new Point(34, 13), 6, 6);
        dc.DrawEllipse(null, Pen("#F69BD8", 1.6), new Point(38, 23), 4, 4);
    }

    private static void DrawHeartCursor(DrawingContext dc)
    {
        DrawArrow(dc, "#FFF1FA", "#82335F", "#F25AAE");
        DrawHeart(dc, new Point(34, 15), 0.58, Brush("#F25AAE"), Pen("#82335F", 1.2));
    }

    private static void DrawShard(DrawingContext dc)
    {
        var geometry = Geometry.Parse("M 5,3 L 38,18 L 20,25 L 31,43 L 21,45 L 14,29 L 6,39 Z");
        dc.DrawGeometry(Brush("#EAF6FF"), Pen("#334155", 2), geometry);
        dc.DrawLine(Pen("#94A3B8", 1.5), new Point(13, 8), new Point(20, 25));
        dc.DrawLine(Pen("#FFFFFF", 1.5), new Point(8, 6), new Point(35, 18));
    }

    private static void DrawSketchPen(DrawingContext dc)
    {
        var body = Geometry.Parse("M 7,4 L 35,28 L 25,38 L 4,7 Z");
        dc.DrawGeometry(Brush("#FFF8E8"), Pen("#202124", 2.2), body);
        dc.DrawLine(Pen("#202124", 2), new Point(9, 5), new Point(35, 28));
        dc.DrawLine(Pen("#F0624D", 3), new Point(20, 18), new Point(29, 27));
    }

    private static void DrawCandyBolt(DrawingContext dc)
    {
        var bolt = Geometry.Parse("M 21,3 L 8,25 L 20,24 L 14,45 L 39,17 L 25,19 Z");
        dc.DrawGeometry(Brush("#FFE46A"), Pen("#3A2439", 2.4), bolt);
        dc.DrawLine(Pen("#F25AAE", 3), new Point(17, 13), new Point(26, 19));
        dc.DrawLine(Pen("#6FE7FF", 3), new Point(15, 27), new Point(25, 25));
    }

    private static void DrawCrosshair(DrawingContext dc)
    {
        dc.DrawEllipse(null, Pen("#202124", 2.3), new Point(24, 24), 13, 13);
        dc.DrawLine(Pen("#202124", 2), new Point(24, 5), new Point(24, 17));
        dc.DrawLine(Pen("#202124", 2), new Point(24, 31), new Point(24, 43));
        dc.DrawLine(Pen("#202124", 2), new Point(5, 24), new Point(17, 24));
        dc.DrawLine(Pen("#202124", 2), new Point(31, 24), new Point(43, 24));
        dc.DrawEllipse(Brush("#55F7FF"), null, new Point(24, 24), 2.5, 2.5);
    }

    private static void DrawLightsaber(DrawingContext dc, string bladeColor)
    {
        dc.DrawLine(Pen(bladeColor, 15.0, 0.10), new Point(4.5, 4.5), new Point(28.5, 28.5));
        dc.DrawLine(Pen(bladeColor, 9.0, 0.26), new Point(4.5, 4.5), new Point(28.5, 28.5));
        dc.DrawLine(Pen("#FFFFFF", 4.6), new Point(5.0, 5.0), new Point(28.0, 28.0));
        dc.DrawLine(Pen(bladeColor, 2.4, 0.78), new Point(5.2, 5.2), new Point(27.8, 27.8));
        dc.DrawLine(Pen("#FFFFFF", 1.0, 0.88), new Point(7.5, 5.8), new Point(24.5, 22.8));

        DrawStar(dc, new Point(6.8, 6.5), 3.0, 1.2, Brush("#FFFFFF", 0.90), null);
        dc.DrawEllipse(Brush(bladeColor, 0.55), null, new Point(16.5, 16.5), 4.0, 4.0);

        var emitter = Geometry.Parse("M 26.0,31.2 L 31.2,26.0 L 35.2,30.0 L 30.0,35.2 Z");
        dc.DrawGeometry(Linear("#C8D0DA", "#6A7280", 0, 0, 1, 1), Pen("#080C12", 1.3), emitter);
        dc.DrawLine(Pen("#DEE4EC", 0.55, 0.8), new Point(28.2, 27.8), new Point(32.0, 31.6));

        dc.DrawEllipse(Brush(bladeColor, 0.55), null, new Point(28.8, 28.8), 3.2, 3.2);
        dc.DrawEllipse(Brush(bladeColor), Pen("#080C12", 0.9), new Point(28.8, 28.8), 1.9, 1.9);
        dc.DrawEllipse(Brush("#FFFFFF", 0.80), null, new Point(28.2, 28.2), 0.65, 0.65);

        var hilt = Geometry.Parse("M 29.8,35.0 L 35.0,29.8 L 45.6,40.4 C 46.7,41.5 46.7,43.5 45.6,44.6 L 44.6,45.6 C 43.5,46.7 41.5,46.7 40.4,45.6 Z");
        dc.DrawGeometry(Linear("#2C3644", "#0C1018", 0, 0, 1, 1), Pen("#050A10", 1.5), hilt);

        dc.DrawLine(Pen("#566270", 0.85), new Point(32.5, 32.5), new Point(42.5, 42.5));
        dc.DrawLine(Pen("#A0AAB4", 0.4, 0.55), new Point(31.8, 33.5), new Point(41.5, 43.2));

        dc.DrawLine(Pen("#50606E", 1.5), new Point(35.0, 31.2), new Point(32.0, 34.2));
        dc.DrawLine(Pen("#50606E", 1.5), new Point(37.4, 33.6), new Point(34.4, 36.6));
        dc.DrawLine(Pen("#50606E", 1.5), new Point(39.8, 36.0), new Point(36.8, 39.0));
        dc.DrawLine(Pen("#50606E", 1.5), new Point(42.2, 38.4), new Point(39.2, 41.4));

        dc.DrawEllipse(Brush("#DD1111"), Pen("#080C12", 0.7), new Point(35.2, 35.8), 1.3, 1.3);
        dc.DrawEllipse(Brush("#FF5555", 0.5), null, new Point(34.8, 35.4), 0.4, 0.4);

        dc.DrawEllipse(Linear("#4A5565", "#1A2030", 0, 0, 1, 1), Pen("#050A10", 1.0), new Point(43.2, 43.2), 2.6, 2.6);
        dc.DrawEllipse(Brush(bladeColor, 0.35), null, new Point(43.2, 43.2), 1.4, 1.4);
    }

    private static void DrawLightsaberGlow(DrawingContext dc, string bladeColor, double glowScale, double glowBrightness)
    {
        var size = Math.Clamp(glowScale <= 0 ? 1 : glowScale, 0.45, 2.0);
        var brightness = Math.Clamp(glowBrightness <= 0 ? 1 : glowBrightness, 0.35, 1.8);
        var tip = new Point(
            28.5 + (size - 1) * 4.0,
            28.5 + (size - 1) * 4.0);
        var coreTip = new Point(
            28.0 + (size - 1) * 3.5,
            28.0 + (size - 1) * 3.5);
        var innerTip = new Point(
            27.8 + (size - 1) * 3.4,
            27.8 + (size - 1) * 3.4);
        var brightColor = Brighten(bladeColor, brightness);

        // Same as normal lightsaber, but slider-controlled blade glow.
        dc.DrawLine(Pen(brightColor, 22.0 * size, Opacity(0.18 * brightness)), new Point(4.5, 4.5), tip);
        dc.DrawLine(Pen(brightColor, 15.0 * size, Opacity(0.30 * brightness)), new Point(4.5, 4.5), tip);
        dc.DrawLine(Pen("#FFFFFF", 6.0 * Math.Min(1.35, size), Opacity(0.65 + 0.25 * brightness)), new Point(5.0, 5.0), coreTip);
        dc.DrawLine(Pen(brightColor, 3.2 * Math.Min(1.55, size), Opacity(0.65 + 0.22 * brightness)), new Point(5.2, 5.2), innerTip);
        dc.DrawLine(Pen("#FFFFFF", 1.4 * Math.Min(1.25, size), Opacity(0.75 + 0.18 * brightness)), new Point(7.5, 5.8), new Point(24.5 + (size - 1) * 2.2, 22.8 + (size - 1) * 2.2));

        DrawStar(dc, new Point(6.8, 6.5), 4.5 * Math.Min(1.35, size), 1.8, Brush("#FFFFFF", Opacity(0.75 + 0.18 * brightness)), null);
        dc.DrawEllipse(Brush(brightColor, Opacity(0.45 + 0.18 * brightness)), null, new Point(16.5, 16.5), 5.5 * size, 5.5 * size);

        var emitter = Geometry.Parse("M 26.0,31.2 L 31.2,26.0 L 35.2,30.0 L 30.0,35.2 Z");
        dc.DrawGeometry(Linear("#C8D0DA", "#6A7280", 0, 0, 1, 1), Pen("#080C12", 1.3), emitter);
        dc.DrawLine(Pen("#DEE4EC", 0.55, 0.8), new Point(28.2, 27.8), new Point(32.0, 31.6));

        dc.DrawEllipse(Brush(brightColor, Opacity(0.50 + 0.18 * brightness)), null, new Point(28.8, 28.8), 4.0 * size, 4.0 * size);
        dc.DrawEllipse(Brush(brightColor), Pen("#080C12", 0.9), new Point(28.8, 28.8), 1.9 * Math.Min(1.35, size), 1.9 * Math.Min(1.35, size));
        dc.DrawEllipse(Brush("#FFFFFF", 0.90), null, new Point(28.2, 28.2), 0.75, 0.75);

        var hilt = Geometry.Parse("M 29.8,35.0 L 35.0,29.8 L 45.6,40.4 C 46.7,41.5 46.7,43.5 45.6,44.6 L 44.6,45.6 C 43.5,46.7 41.5,46.7 40.4,45.6 Z");
        dc.DrawGeometry(Linear("#2C3644", "#0C1018", 0, 0, 1, 1), Pen("#050A10", 1.5), hilt);

        dc.DrawLine(Pen("#566270", 0.85), new Point(32.5, 32.5), new Point(42.5, 42.5));
        dc.DrawLine(Pen("#A0AAB4", 0.4, 0.55), new Point(31.8, 33.5), new Point(41.5, 43.2));

        dc.DrawLine(Pen("#50606E", 1.5), new Point(35.0, 31.2), new Point(32.0, 34.2));
        dc.DrawLine(Pen("#50606E", 1.5), new Point(37.4, 33.6), new Point(34.4, 36.6));
        dc.DrawLine(Pen("#50606E", 1.5), new Point(39.8, 36.0), new Point(36.8, 39.0));
        dc.DrawLine(Pen("#50606E", 1.5), new Point(42.2, 38.4), new Point(39.2, 41.4));

        dc.DrawEllipse(Brush("#DD1111"), Pen("#080C12", 0.7), new Point(35.2, 35.8), 1.3, 1.3);
        dc.DrawEllipse(Brush("#FF5555", 0.5), null, new Point(34.8, 35.4), 0.4, 0.4);

        dc.DrawEllipse(Linear("#4A5565", "#1A2030", 0, 0, 1, 1), Pen("#050A10", 1.0), new Point(43.2, 43.2), 2.6, 2.6);
        dc.DrawEllipse(Brush(brightColor, Opacity(0.25 + 0.12 * brightness)), null, new Point(43.2, 43.2), 1.4 * Math.Min(1.5, size), 1.4 * Math.Min(1.5, size));
    }

    private static void DrawHeroSword(DrawingContext dc)
    {
        // Sword of Daylight - asymmetric blade with crescent cutout (Trollhunters reference)

        // Ambient cyan glow aura
        dc.DrawGeometry(Brush("#00E5FF", 0.15), null,
            Geometry.Parse("M 24,0 L 29,7 L 29,29 L 20,29 L 20,15 A 3.5,3.5 0 0 0 20,8 L 20,4 Z"));

        // Main blade - right side cleaver edge, left side crescent cutout
        var blade = Geometry.Parse(
            "M 24,1 L 28,7 L 28,28 " +
            "L 21,28 L 21,15 A 3,3 0 0 0 21,9 L 21,5 Z");
        dc.DrawGeometry(Linear("#B2EBF2", "#00ACC1", 0, 0, 1, 0), Pen("#00BCD4", 0.5), blade);

        // Central ridge highlight
        dc.DrawLine(Pen("#FFFFFF", 0.6, 0.9), new Point(24, 1), new Point(25, 7));
        dc.DrawLine(Pen("#FFFFFF", 0.6, 0.9), new Point(25, 7), new Point(25, 28));
        // Chisel line from left upper to ridge
        dc.DrawLine(Pen("#FFFFFF", 0.4, 0.7), new Point(21, 5), new Point(25, 7));

        // Rune channel
        dc.DrawLine(Pen("#00838F", 1.3, 0.5), new Point(24.6, 10), new Point(24.6, 25));

        // Glowing runes - T shape
        dc.DrawLine(Pen("#FFFFFF", 0.5, 0.95), new Point(24.1, 12), new Point(25.1, 12));
        dc.DrawLine(Pen("#FFFFFF", 0.5, 0.95), new Point(24.6, 12), new Point(24.6, 13.7));
        // X shape
        dc.DrawLine(Pen("#FFFFFF", 0.5, 0.95), new Point(24.1, 16), new Point(25.1, 17.7));
        dc.DrawLine(Pen("#FFFFFF", 0.5, 0.95), new Point(25.1, 16), new Point(24.1, 17.7));
        // Z shape
        dc.DrawLine(Pen("#FFFFFF", 0.5, 0.95), new Point(24.1, 20.5), new Point(25.1, 20.5));
        dc.DrawLine(Pen("#FFFFFF", 0.5, 0.95), new Point(25.1, 20.5), new Point(24.1, 22.2));
        dc.DrawLine(Pen("#FFFFFF", 0.5, 0.95), new Point(24.1, 22.2), new Point(25.1, 22.2));

        // Heavy troll crossguard
        var guard = Geometry.Parse(
            "M 17,28 L 31,28 L 33,31 L 28,32 L 24,31 L 20,32 L 16,31 Z");
        dc.DrawGeometry(Linear("#78909C", "#263238", 0, 0, 1, 1), Pen("#1A237E", 0.5), guard);

        // Amulet center gem
        dc.DrawEllipse(Linear("#78909C", "#263238", 0, 0, 1, 1), Pen("#90A4AE", 0.5), new Point(24, 30), 2.5, 2.5);
        dc.DrawEllipse(Brush("#00E5FF"), null, new Point(24, 30), 1.4, 1.4);
        dc.DrawGeometry(Brush("#FFFFFF", 0.8), null,
            Geometry.Parse("M 24,29 L 25.2,30 L 24,31 L 22.8,30 Z"));

        // Wrapped grip
        dc.DrawLine(Pen("#1C313A", 2.5), new Point(24, 33), new Point(24, 44));
        // Spiral accents
        dc.DrawLine(Pen("#00B0FF", 0.4, 0.8), new Point(22.8, 35), new Point(25.3, 35.8));
        dc.DrawLine(Pen("#00B0FF", 0.4, 0.8), new Point(22.8, 37.5), new Point(25.3, 38.3));
        dc.DrawLine(Pen("#00B0FF", 0.4, 0.8), new Point(22.8, 40), new Point(25.3, 40.8));
        dc.DrawLine(Pen("#00B0FF", 0.4, 0.8), new Point(22.8, 42.5), new Point(25.3, 43.3));

        // Pommel
        dc.DrawEllipse(Linear("#78909C", "#263238", 0, 0, 1, 1), Pen("#1A237E", 0.5), new Point(24, 45), 1.5, 1.5);
        dc.DrawEllipse(Brush("#00E5FF"), null, new Point(24, 45), 0.5, 0.5);
    }

    private static void DrawEclipseSword(DrawingContext dc)
    {
        // Sword of Eclipse - dark obsidian blade with red/orange energy (same silhouette as Daylight)

        // Ambient red glow aura
        dc.DrawGeometry(Brush("#FF3D00", 0.18), null,
            Geometry.Parse("M 24,0 L 29,7 L 29,29 L 20,29 L 20,15 A 3.5,3.5 0 0 0 20,8 L 20,4 Z"));

        // Main blade - dark with red/orange edge stroke
        var blade = Geometry.Parse(
            "M 24,1 L 28,7 L 28,28 " +
            "L 21,28 L 21,15 A 3,3 0 0 0 21,9 L 21,5 Z");
        dc.DrawGeometry(Linear("#37474F", "#0D1117", 0, 0, 1, 0), Pen("#FF3D00", 0.6), blade);

        // Red inner edge glow lines
        dc.DrawLine(Pen("#FF6D00", 0.4, 0.8), new Point(24, 1), new Point(28, 7));
        dc.DrawLine(Pen("#FF6D00", 0.4, 0.8), new Point(28, 7), new Point(28, 28));
        dc.DrawLine(Pen("#FF6D00", 0.4, 0.8), new Point(21, 5), new Point(21, 9));
        dc.DrawLine(Pen("#FF6D00", 0.4, 0.8), new Point(21, 15), new Point(21, 28));

        // Central ridge (red heat)
        dc.DrawLine(Pen("#FF3D00", 0.5, 0.9), new Point(24, 1), new Point(25, 7));
        dc.DrawLine(Pen("#FF3D00", 0.5, 0.9), new Point(25, 7), new Point(25, 28));

        // Rune channel (dark brown)
        dc.DrawLine(Pen("#3E2723", 1.3), new Point(24.6, 10), new Point(24.6, 25));

        // Glowing red/orange runes - T shape
        dc.DrawLine(Pen("#FF9100", 0.5, 0.95), new Point(24.1, 12), new Point(25.1, 12));
        dc.DrawLine(Pen("#FF9100", 0.5, 0.95), new Point(24.6, 12), new Point(24.6, 13.7));
        // X shape
        dc.DrawLine(Pen("#FF9100", 0.5, 0.95), new Point(24.1, 16), new Point(25.1, 17.7));
        dc.DrawLine(Pen("#FF9100", 0.5, 0.95), new Point(25.1, 16), new Point(24.1, 17.7));
        // Z shape
        dc.DrawLine(Pen("#FF9100", 0.5, 0.95), new Point(24.1, 20.5), new Point(25.1, 20.5));
        dc.DrawLine(Pen("#FF9100", 0.5, 0.95), new Point(25.1, 20.5), new Point(24.1, 22.2));
        dc.DrawLine(Pen("#FF9100", 0.5, 0.95), new Point(24.1, 22.2), new Point(25.1, 22.2));

        // Spiked black crossguard (slightly wider)
        var guard = Geometry.Parse(
            "M 16,28 L 32,28 L 33,31 L 29,32 L 24,31 L 19,32 L 15,31 Z");
        dc.DrawGeometry(Linear("#424242", "#141414", 0, 0, 1, 1), Pen("#000000", 0.5), guard);

        // Eclipse amulet gem (red core)
        dc.DrawEllipse(Linear("#424242", "#141414", 0, 0, 1, 1), Pen("#424242", 0.5), new Point(24, 30), 2.5, 2.5);
        dc.DrawEllipse(Brush("#FF1744"), null, new Point(24, 30), 1.4, 1.4);
        dc.DrawGeometry(Brush("#FFFF00", 0.7), null,
            Geometry.Parse("M 24,29 L 25.2,30 L 24,31 L 22.8,30 Z"));

        // Dark grip
        dc.DrawLine(Pen("#151515", 2.5), new Point(24, 33), new Point(24, 44));
        // Red spiral accents
        dc.DrawLine(Pen("#DD2C00", 0.4, 0.9), new Point(22.8, 35), new Point(25.3, 35.8));
        dc.DrawLine(Pen("#DD2C00", 0.4, 0.9), new Point(22.8, 37.5), new Point(25.3, 38.3));
        dc.DrawLine(Pen("#DD2C00", 0.4, 0.9), new Point(22.8, 40), new Point(25.3, 40.8));
        dc.DrawLine(Pen("#DD2C00", 0.4, 0.9), new Point(22.8, 42.5), new Point(25.3, 43.3));

        // Pommel
        dc.DrawEllipse(Linear("#424242", "#141414", 0, 0, 1, 1), Pen("#000000", 0.5), new Point(24, 45), 1.5, 1.5);
        dc.DrawEllipse(Brush("#FF3D00"), null, new Point(24, 45), 0.5, 0.5);
    }

    private static void DrawOmnitrix(DrawingContext dc)
    {
        // Outer dark ring
        dc.DrawEllipse(Brush("#1C1D1F"), Pen("#000000", 3.5), new Point(24, 24), 22.0, 22.0);
        // Middle gray ring
        dc.DrawEllipse(Brush("#3A3D40"), Pen("#000000", 2.5), new Point(24, 24), 17.0, 17.0);
        // Inner dark face
        dc.DrawEllipse(Brush("#232426"), Pen("#000000", 2.0), new Point(24, 24), 13.5, 13.5);
        // Green hourglass with arc top/bottom edges following the inner circle
        dc.DrawGeometry(Brush("#92FA1B"), Pen("#000000", 1.5),
            Geometry.Parse("M 14.5,14.5 A 13.5,13.5 0 0 1 33.5,14.5 L 28.5,24 L 33.5,33.5 A 13.5,13.5 0 0 1 14.5,33.5 L 19.5,24 Z"));
        // Inner circle border on top for clean edge
        dc.DrawEllipse(null, Pen("#000000", 2.0), new Point(24, 24), 13.5, 13.5);
        // Four indicator dots at cardinal points
        foreach (var p in new[] { new Point(24, 2.0), new Point(46.0, 24), new Point(24, 46.0), new Point(2.0, 24) })
        {
            dc.DrawEllipse(Brush("#232426"), Pen("#000000", 1.0), p, 2.5, 2.5);
            dc.DrawEllipse(Brush("#92FA1B"), Pen("#000000", 0.6), p, 1.4, 1.4);
        }
    }

    private static void DrawOmnitrixGlow(DrawingContext dc)
    {
        // Same as normal omnitrix but hourglass is red instead of green
        dc.DrawEllipse(Brush("#1C1D1F"), Pen("#000000", 3.5), new Point(24, 24), 22.0, 22.0);
        dc.DrawEllipse(Brush("#3A3D40"), Pen("#000000", 2.5), new Point(24, 24), 17.0, 17.0);
        dc.DrawEllipse(Brush("#232426"), Pen("#000000", 2.0), new Point(24, 24), 13.5, 13.5);
        // Red hourglass instead of green
        dc.DrawGeometry(Brush("#FF2020"), Pen("#000000", 1.5),
            Geometry.Parse("M 14.5,14.5 A 13.5,13.5 0 0 1 33.5,14.5 L 28.5,24 L 33.5,33.5 A 13.5,13.5 0 0 1 14.5,33.5 L 19.5,24 Z"));
        dc.DrawEllipse(null, Pen("#000000", 2.0), new Point(24, 24), 13.5, 13.5);
        // Indicator dots also red
        foreach (var p in new[] { new Point(24, 2.0), new Point(46.0, 24), new Point(24, 46.0), new Point(2.0, 24) })
        {
            dc.DrawEllipse(Brush("#232426"), Pen("#000000", 1.0), p, 2.5, 2.5);
            dc.DrawEllipse(Brush("#FF2020"), Pen("#000000", 0.6), p, 1.4, 1.4);
        }
    }

    private static void DrawTardis(DrawingContext dc)
    {
        dc.DrawRectangle(Brush("#000000", 0.15), null, new Rect(11, 7.5, 29, 39));

        dc.DrawRectangle(Linear("#254A9E", "#122460", 0, 0, 1, 1), Pen("#030610", 1.45), new Rect(8.5, 8.8, 31.0, 36.2));

        dc.DrawRectangle(Linear("#2E52A8", "#1A3078", 0, 0, 1, 1), Pen("#030610", 1.15), new Rect(7.2, 5.8, 33.6, 3.8));

        dc.DrawRectangle(Linear("#2448A0", "#152768", 0, 0, 1, 1), Pen("#030610", 1.05), new Rect(10.0, 3.5, 28.0, 2.8));

        dc.DrawRectangle(Brush("#E8F0FF"), Pen("#030610", 0.6), new Rect(20.5, 0.6, 7.0, 3.2));
        dc.DrawEllipse(Brush("#FFFFFF", 0.20), null, new Point(24, 2.0), 5.0, 3.5);

        dc.DrawRectangle(Brush("#0A0A0A"), null, new Rect(10.8, 11.8, 26.4, 5.0));
        DrawFittedText(dc, "POLICE", new Rect(12.5, 12.2, 8.5, 3.2), "#FFFFFF", 3.3, FontWeights.Bold);
        DrawFittedText(dc, "PUBLIC", new Rect(21.5, 12.0, 6.0, 2.0), "#FFFFFF", 2.1, FontWeights.Bold);
        DrawFittedText(dc, "CALL", new Rect(22.0, 14.2, 4.5, 1.8), "#FFFFFF", 2.0, FontWeights.Bold);
        DrawFittedText(dc, "BOX", new Rect(28.5, 12.2, 7.0, 3.2), "#FFFFFF", 3.3, FontWeights.Bold);

        dc.DrawLine(Pen("#3A5FBF", 0.9), new Point(12.0, 9.2), new Point(12.0, 43.8));
        dc.DrawLine(Pen("#0A1447", 0.9), new Point(36.0, 9.2), new Point(36.0, 43.8));
        dc.DrawLine(Pen("#030610", 1.1), new Point(24, 17.2), new Point(24, 44.8));

        foreach (var rect in new[] { new Rect(13.8, 19.8, 7.8, 7.4), new Rect(26.4, 19.8, 7.8, 7.4) })
        {
            dc.DrawRectangle(Linear("#E4ECF5", "#8A9AAE", 0, 0, 1, 1), Pen("#05070B", 0.85), rect);
            dc.DrawLine(Pen("#05070B", 0.65), new Point(rect.Left + rect.Width / 2, rect.Top), new Point(rect.Left + rect.Width / 2, rect.Bottom));
            dc.DrawLine(Pen("#05070B", 0.65), new Point(rect.Left, rect.Top + rect.Height / 2), new Point(rect.Right, rect.Top + rect.Height / 2));
            dc.DrawLine(Pen("#FFFFFF", 0.45, 0.7), new Point(rect.Left + 0.7, rect.Top + 0.6), new Point(rect.Right - 0.7, rect.Top + 0.6));
        }

        foreach (var rect in new[] { new Rect(14.0, 29.8, 7.6, 11.2), new Rect(26.4, 29.8, 7.6, 11.2) })
        {
            dc.DrawRectangle(null, Pen("#05070B", 1.1), rect);
            dc.DrawRectangle(Brush("#1A3080", 0.5), null, new Rect(rect.Left + 0.9, rect.Top + 0.9, rect.Width - 1.8, rect.Height - 1.8));
        }

        dc.DrawRectangle(Linear("#2A45A0", "#132462", 0, 0, 1, 1), Pen("#030610", 1.05), new Rect(6.5, 43.8, 35.0, 3.4));
    }

    private static void DrawAutobotCrest(DrawingContext dc)
    {
        // Red fill shapes from official Autobot SVG (scaled to 48x48)
        var redFill = Geometry.Parse(
            "F0 " +
            "M 18.06,13.62 L 9.26,9.07 8.12,3.39 H 2.5 l 2.62,14.98 5.84,4.04 H 19.7 l -1.64,-8.8 z " +
            "m -1.77,0.85 l 0.08,1.56 -9.86,-4.44 -0.13,-1.6 9.91,4.48 z " +
            "m 0.98,4.33 v 1.49 L 7.13,15.78 l -0.16,-1.3 10.3,4.33 z " +
            "M 37.25,8.21 l 0.79,-3.68 C 28.43,1.13 19.01,1 9.77,4.16 l 0.63,3.77 13,6.6 13.85,-6.33 z " +
            "m -13.49,1.92 L 18.22,6.3 h 11.15 L 23.76,10.13 z " +
            "M 45.5,3.88 h -5.75 l -1.36,5.46 -9.24,4.48 -1.63,8.73 h 9.38 l 5.75,-3.62 2.84,-15.06 z " +
            "m -4.2,7.17 v 1.5 l -9.94,3.9 v -1.49 l 9.94,-3.91 z " +
            "m -1.06,4.28 v 1.34 L 30.07,20.85 V 19.23 l 10.17,-3.9 z " +
            "M 9.12,26.05 L 8.68,22.57 l -3.26,-2.07 1.27,19.96 7.75,4.04 0.08,-15.05 -5.4,-3.41 z " +
            "M 16.07,45.01 l 1.5,1.06 1.99,-6.6 h 7.74 l 2.05,6.52 1.71,-0.92 v -15.62 l -3.48,-3.2 v 10.72 H 19.7 l -0.14,-10.72 -3.49,3.2 v 15.55 z " +
            "M 19.99,14.68 l 1.2,7.31 v 13.64 h 4.77 V 21.85 l 1.34,-7.17 -3.9,1.64 -3.41,-1.64 z " +
            "M 20.56,40.89 l -1.56,6.11 h 8.96 l -1.78,-6.11 H 20.56 z " +
            "M 42.15,20.93 l -3.7,2.56 -0.14,2.7 -5.75,3.48 v 15.13 l 8.04,-3.54 1.55,-20.32 z");
        dc.DrawGeometry(Brush("#CC2229"), null, redFill);

        // Black outline from official SVG
        dc.DrawGeometry(null, Pen("#000000", 0.5),
            Geometry.Parse(
            "M 8.68,22.57 l -3.26,-2.07 1.27,19.96 7.75,4.04 0.08,-15.05 -5.4,-3.41 -0.44,-3.47 z " +
            "m 31.07,-18.69 l -1.36,5.46 -9.24,4.48 -1.63,8.73 h 9.38 l 5.75,-3.62 2.84,-15.06 h -5.75 v 0 z " +
            "m 0.49,12.79 L 30.07,20.85 V 19.23 l 10.17,-3.9 v 1.34 z " +
            "m 1.06,-4.12 l -9.94,3.9 v -1.49 l 9.94,-3.91 v 1.5 z " +
            "m -2.84,10.94 l -0.14,2.7 -5.75,3.48 v 15.13 l 8.04,-3.54 1.55,-20.32 -3.7,2.56 z " +
            "M 16.29,14.47 L 6.38,9.99 l 0.13,1.6 L 16.37,16.04 l -0.08,-1.56 z " +
            "m 20.96,-6.27 L 23.4,14.54 10.4,7.94 l -0.63,-3.77 C 19.01,1 28.43,1.13 38.04,4.53 l -0.79,3.68 z " +
            "M 17.27,18.8 L 6.97,14.47 l 0.16,1.3 L 17.27,20.29 v -1.49 z " +
            "m 3.92,3.19 v 13.64 h 4.77 V 21.85 l 1.34,-7.17 -3.9,1.64 -3.41,-1.64 1.2,7.31 z " +
            "M 18.06,13.62 l 1.64,8.81 H 10.97 L 5.13,18.37 l -2.62,-14.98 h 5.62 L 9.26,9.07 l 8.8,4.55 z " +
            "m 5.7,-3.49 l 5.61,-3.84 H 18.22 l 5.54,3.84 z " +
            "M 16.07,45.01 v -15.55 l 3.49,-3.2 0.14,10.72 h 7.88 v -10.72 l 3.48,3.2 v 15.62 l -1.71,0.92 -2.05,-6.52 h -7.74 l -1.99,6.6 -1.5,-1.06 z " +
            "m 2.92,1.99 h 8.96 l -1.78,-6.11 H 20.56 l -1.56,6.11 z"));
    }

    private static void DrawDecepticonCrest(DrawingContext dc)
    {
        // Purple fill shapes from official Decepticon SVG (scaled to 48x48)
        var purpleFill = Geometry.Parse(
            "F0 " +
            "M 5.82,40.33 l 15.27,6.59 -16.64,-23.56 1.37,16.97 z " +
            "M 45.6,3.23 l -3.51,4.71 -10.76,3.18 -0.51,3.29 9.81,-2.52 -0.25,1.96 -9.92,2.7 -0.16,2.07 9.68,-2.43 -0.22,2.06 -8.79,2.31 " +
            "c -0.94,0.23 -1.81,0.76 -2.65,1.57 l -4.37,3.86 -4.41,-4.15 " +
            "c -0.67,-0.61 -1.43,-1.06 -2.31,-1.34 l -8.59,-2.62 -0.25,-2.06 9.36,2.74 -0.34,-2.31 -9.6,-2.91 -0.25,-1.9 9.64,2.84 -0.39,-3.26 -10.71,-3.26 " +
            "L 2.4,2.96 l 1.96,16.63 19.29,27.41 19.63,-26.9 2.31,-16.88 z " +
            "m -16.45,29.73 l -3.01,-6.08 12.61,-3.77 -9.61,9.85 z " +
            "M 8.91,22.68 l 12.69,4.04 -3.01,6.25 -9.68,-10.28 z " +
            "M 41.57,40.67 l 1.64,-17.05 -16.73,23.22 15.09,-6.17 z " +
            "M 20.06,19.51 l 3.85,3.77 3.7,-3.52 3.09,-18.51 -4.21,7.54 h -4.63 L 17.39,1 l 2.67,18.51 z " +
            "m 6.17,-7.62 L 24.08,18.65 l -2.23,-6.76 h 4.38 z");
        dc.DrawGeometry(Brush("#5A5D80"), null, purpleFill);

        // Black outline from official SVG
        dc.DrawGeometry(null, Pen("#000000", 0.55),
            Geometry.Parse(
            "M 41.57,40.67 l -15.09,6.17 16.73,-23.22 -1.64,17.05 z " +
            "M 26.23,11.89 H 21.85 L 24.08,18.65 l 2.15,-6.76 z " +
            "m -6.17,7.62 l -2.67,-18.51 4.46,7.79 h 4.63 l 4.21,-7.54 -3.09,18.51 L 23.91,23.28 l -3.85,-3.77 z " +
            "M 8.91,22.68 l 9.68,10.28 3.01,-6.25 -12.69,-4.04 z " +
            "m 12.18,24.25 L 4.45,23.36 l 1.37,16.97 15.27,6.59 z " +
            "m 5.05,-20.04 l 12.61,-3.77 -9.61,9.85 -3.01,-6.08 z " +
            "m 15.95,-18.95 l -10.76,3.18 -0.51,3.29 9.81,-2.52 -0.25,1.96 -9.92,2.7 -0.16,2.07 9.68,-2.43 -0.22,2.06 -8.79,2.31 " +
            "c -0.94,0.23 -1.81,0.76 -2.65,1.57 l -4.37,3.86 -4.41,-4.15 " +
            "c -0.67,-0.61 -1.43,-1.06 -2.31,-1.34 l -8.59,-2.62 -0.25,-2.06 9.36,2.74 -0.34,-2.31 -9.6,-2.91 -0.25,-1.9 9.64,2.84 -0.39,-3.26 -10.71,-3.26 " +
            "L 2.4,2.96 l 1.96,16.63 19.29,27.41 19.63,-26.9 2.31,-16.88 -3.51,4.71 z"));
    }

    private static void DrawSheraSword(DrawingContext dc)
    {
        dc.DrawGeometry(Brush("#9CF4FF", 0.12), null,
            Geometry.Parse("M 18,2 L 30,2 L 30,28 L 24,30 L 18,28 Z"));

        var blade = Geometry.Parse("M 19,3 L 24,1 L 29,3 L 29,27 L 24,29 L 19,27 Z");
        dc.DrawGeometry(Linear("#E8F8FF", "#A8DFEF", 0, 0, 1, 1), Pen("#5098AA", 1.0), blade);

        dc.DrawGeometry(Brush("#C8F0FF", 0.5), null,
            Geometry.Parse("M 20,4 L 24,2.5 L 24,28 L 20,26 Z"));
        dc.DrawGeometry(Brush("#E8FCFF", 0.6), null,
            Geometry.Parse("M 28,4 L 24,2.5 L 24,28 L 28,26 Z"));

        dc.DrawLine(Pen("#5098AA", 0.6), new Point(24, 2), new Point(24, 28));

        dc.DrawGeometry(Brush("#FFFFFF", 0.5), null,
            Geometry.Parse("M 22,8 L 24,6 L 26,8 L 24,10 Z"));
        dc.DrawEllipse(Brush("#FFFFFF", 0.4), null, new Point(21, 5), 0.7, 0.7);
        dc.DrawEllipse(Brush("#FFFFFF", 0.4), null, new Point(27, 5), 0.7, 0.7);

        var guardLeft = Geometry.Parse("M 24,27 C 20,24 14,23 8,27 C 11,31 17,32.5 24,30 Z");
        var guardRight = Geometry.Parse("M 24,27 C 28,24 34,23 40,27 C 37,31 31,32.5 24,30 Z");
        dc.DrawGeometry(Linear("#FFE89A", "#C08A20", 0, 0, 1, 1), Pen("#68450C", 1.1), guardLeft);
        dc.DrawGeometry(Linear("#FFE89A", "#C08A20", 0, 0, 1, 1), Pen("#68450C", 1.1), guardRight);

        dc.DrawGeometry(Brush("#FFF0B0", 0.4), null,
            Geometry.Parse("M 24,28 C 19,25.5 14,25 10,27.5 C 15,29.5 20,30 24,29.5 Z"));
        dc.DrawGeometry(Brush("#FFF0B0", 0.4), null,
            Geometry.Parse("M 24,28 C 29,25.5 34,25 38,27.5 C 33,29.5 28,30 24,29.5 Z"));

        dc.DrawEllipse(Linear("#B6FCFF", "#19AFC7", 0, 0, 1, 1), Pen("#6F4D11", 0.9), new Point(24, 28.5), 3.0, 3.5);
        dc.DrawEllipse(Brush("#FFFFFF", 0.75), null, new Point(23.2, 27.5), 0.7, 0.8);

        dc.DrawLine(Pen("#C08A20", 3.8), new Point(24, 32), new Point(24, 44.5));
        dc.DrawLine(Pen("#FFE8A0", 0.7, 0.7), new Point(23.2, 33), new Point(23.2, 43.5));

        dc.DrawLine(Pen("#8C6418", 0.7), new Point(22.2, 34.5), new Point(25.8, 35.5));
        dc.DrawLine(Pen("#8C6418", 0.7), new Point(22.2, 37), new Point(25.8, 38));
        dc.DrawLine(Pen("#8C6418", 0.7), new Point(22.2, 39.5), new Point(25.8, 40.5));
        dc.DrawLine(Pen("#8C6418", 0.7), new Point(22.2, 42), new Point(25.8, 43));

        dc.DrawEllipse(Linear("#F7D66C", "#B66C12", 0, 0, 1, 1), Pen("#68450C", 0.9), new Point(24, 45.5), 3.0, 2.2);
    }

    private static void DrawAncientStaff(DrawingContext dc)
    {
        // Staff body - dark brown wooden rod (vertical)
        dc.DrawLine(Pen("#0E0807", 5.5), new Point(24, 20), new Point(24, 47));
        dc.DrawLine(Pen("#4B2D28", 4.0), new Point(24, 20), new Point(24, 47));
        dc.DrawLine(Pen("#8F573C", 1.8), new Point(23.4, 22), new Point(23.4, 46));
        dc.DrawLine(Pen("#A9734A", 0.7, 0.6), new Point(22.8, 23), new Point(22.8, 45));

        // Owl head - angular carved shape
        var headOuter = Geometry.Parse(
            "M 24,1 " +
            "C 14,1 8,5 8,13 " +
            "C 8,18 11,21 16,22 " +
            "L 20,22 L 24,24 L 28,22 L 32,22 " +
            "C 37,21 40,18 40,13 " +
            "C 40,5 34,1 24,1 Z");
        dc.DrawGeometry(Brush("#0E0807"), null, headOuter);

        // Inner carved face - medium brown
        var headInner = Geometry.Parse(
            "M 24,2 " +
            "C 15,2 10,6 10,13 " +
            "C 10,17 13,20 17,21 " +
            "L 21,21 L 24,23 L 27,21 L 31,21 " +
            "C 35,20 38,17 38,13 " +
            "C 38,6 33,2 24,2 Z");
        dc.DrawGeometry(Brush("#4B2D28"), null, headInner);

        // Lighter face panel
        dc.DrawGeometry(Brush("#A9734A"), null,
            Geometry.Parse(
                "M 24,4 C 17,4 13,7 13,12 " +
                "C 13,16 16,19 20,19.5 " +
                "L 24,18 L 28,19.5 " +
                "C 32,19 35,16 35,12 " +
                "C 35,7 31,4 24,4 Z"));

        // Forehead crease
        dc.DrawGeometry(Brush("#4B2D28"), null,
            Geometry.Parse("M 22,5 L 26,5 L 24,9 Z"));

        // Left eye socket
        dc.DrawGeometry(Brush("#8F573C"), Pen("#4B2D28", 0.5),
            Geometry.Parse(
                "M 14,9 C 14,6 17,6 20,8 " +
                "C 21,9 21,12 19,13 " +
                "C 16,13 14,11.5 14,9 Z"));
        // Left eye dark
        dc.DrawEllipse(Brush("#0E0807"), null, new Point(17.5, 10.2), 2.0, 1.5);

        // Right eye socket
        dc.DrawGeometry(Brush("#8F573C"), Pen("#4B2D28", 0.5),
            Geometry.Parse(
                "M 34,9 C 34,6 31,6 28,8 " +
                "C 27,9 27,12 29,13 " +
                "C 32,13 34,11.5 34,9 Z"));
        // Right eye dark
        dc.DrawEllipse(Brush("#0E0807"), null, new Point(30.5, 10.2), 2.0, 1.5);

        // Beak / nose
        dc.DrawGeometry(Brush("#4B2D28"), null,
            Geometry.Parse("M 22,14 L 26,14 L 24,17.5 Z"));

        // Neck joint detail
        dc.DrawGeometry(Brush("#4B2D28"), null,
            Geometry.Parse("M 20,21 L 24,23 L 28,21 L 26,20 L 24,21.5 L 22,20 Z"));
    }

    private static void DrawStarfleetDelta(DrawingContext dc)
    {
        dc.DrawGeometry(Brush("#000000", 0.14), null,
            Geometry.Parse("M 25,3 C 30,18 36,34 40,46 C 32,42 28,39 25,37 C 22,39 18,42 10,46 C 14,34 20,18 25,3 Z"));

        var delta = Geometry.Parse(
            "M 24,2 " +
            "C 29,16 35,32 39,45 " +
            "C 31,41 27,38 24,36 " +
            "C 21,38 17,41 9,45 " +
            "C 13,32 19,16 24,2 Z");
        dc.DrawGeometry(Linear("#F5D070", "#C08820", 0, 0, 1, 1), Pen("#0A0A0A", 2.0), delta);

        dc.DrawGeometry(Brush("#FFFFFF", 0.25), null,
            Geometry.Parse("M 24,5 C 27,16 31,28 33,36 L 24,32 Z"));

        DrawStar(dc, new Point(24, 22), 7.0, 3.2, Brush("#0A0A0A"), null);

        dc.DrawLine(Pen("#FFE8A0", 0.7, 0.6), new Point(14, 38), new Point(23.5, 5));
    }

    private static void DrawStarship(DrawingContext dc)
    {
        dc.DrawEllipse(Brush("#B0C0D0", 0.08), null, new Point(24, 14), 18, 18);

        dc.DrawEllipse(Linear("#E0E6EC", "#909AA6", 0, 0, 1, 1), Pen("#1B2633", 1.2), new Point(24, 14), 17, 12);

        dc.DrawEllipse(Brush("#D0D8E0", 0.6), Pen("#8A94A0", 0.5), new Point(24, 14), 12, 8);

        dc.DrawEllipse(Linear("#C8D0D8", "#8A94A0", 0, 0, 1, 1), Pen("#606A76", 0.6), new Point(24, 13), 5, 3);
        dc.DrawEllipse(Brush("#B0BAC4"), Pen("#707A86", 0.4), new Point(24, 12.5), 2.5, 1.5);

        dc.DrawLine(Pen("#FFFFFF", 0.5, 0.5), new Point(10, 10), new Point(24, 5));

        dc.DrawGeometry(Linear("#D0D8E0", "#8A94A6", 0, 0, 1, 1), Pen("#1B2633", 0.8),
            Geometry.Parse("M 22,24 L 26,24 L 25.5,36 L 22.5,36 Z"));

        dc.DrawGeometry(Linear("#D0D8E0", "#8A94A6", 0, 0, 1, 1), Pen("#1B2633", 0.8),
            Geometry.Parse("M 20,36 L 28,36 L 27,42 L 21,42 Z"));

        dc.DrawEllipse(Brush("#41CFE4"), Pen("#1F4751", 0.5), new Point(24, 35), 1.5, 1.0);

        dc.DrawLine(Pen("#A0AAB4", 2.0), new Point(22, 38), new Point(10, 32));
        dc.DrawLine(Pen("#1B2633", 0.5), new Point(22, 38), new Point(10, 32));
        dc.DrawLine(Pen("#A0AAB4", 2.0), new Point(26, 38), new Point(38, 32));
        dc.DrawLine(Pen("#1B2633", 0.5), new Point(26, 38), new Point(38, 32));

        DrawNacelle(dc, new Point(8, 30), true);
        DrawNacelle(dc, new Point(40, 30), false);
    }

    private static void DrawNacelle(DrawingContext dc, Point center, bool isLeft)
    {
        var body = new Rect(center.X - 2.5, center.Y - 8, 5, 16);
        dc.DrawRoundedRectangle(Linear("#D8E0E8", "#8090A0", 0, 0, 1, 1), Pen("#1B2633", 0.8), body, 2.5, 2.5);
        dc.DrawRectangle(Linear("#6080FF", "#3050D0", 0, 0, 1, 1), null,
            new Rect(center.X - 1.8, center.Y - 7, 3.6, 4));
        dc.DrawRectangle(Brush("#C0CAD4", 0.7), null,
            new Rect(center.X - 1.8, center.Y - 3, 3.6, 9));
        dc.DrawEllipse(Brush("#5FE2EF", 0.3), null, new Point(center.X, center.Y - 6.5), 3, 3);
        dc.DrawEllipse(Linear("#70D0E8", "#3090B0", 0, 0, 1, 1), Pen("#1F4751", 0.5),
            new Point(center.X, center.Y - 6.5), 1.8, 1.8);
    }

    private static void DrawDarkOneDagger(DrawingContext dc)
    {
        var blade = Geometry.Parse(
            "M 20,28 " +
            "C 18.5,23 20.5,20 19,16 " +
            "C 17.5,12 20,8 24,1 " +
            "C 28,8 30.5,12 29,16 " +
            "C 27.5,20 29.5,23 28,28 " +
            "C 26,27 22,27 20,28 Z");
        dc.DrawGeometry(Linear("#E8EAEE", "#8A8E96", 0, 0, 1, 1), Pen("#101012", 1.3), blade);

        dc.DrawGeometry(Brush("#D0D4DA", 0.7), null,
            Geometry.Parse("M 24,2.5 C 22.5,12 22.5,20 24,27 C 25.5,20 25.5,12 24,2.5 Z"));
        dc.DrawLine(Pen("#3A3A40", 0.6), new Point(24, 2), new Point(24, 27));

        dc.DrawLine(Pen("#202124", 0.5), new Point(20.5, 22), new Point(27, 20));
        dc.DrawLine(Pen("#202124", 0.5), new Point(19.5, 16), new Point(28, 15));
        dc.DrawLine(Pen("#202124", 0.5), new Point(20, 10), new Point(27.5, 9));

        dc.DrawEllipse(Linear("#E8EAEE", "#808490", 0, 0, 1, 1), Pen("#080809", 1.2), new Point(24, 30), 12.5, 3.5);
        dc.DrawEllipse(Linear("#C8CCD2", "#505460", 0, 0, 1, 1), Pen("#080809", 0.7), new Point(24, 30), 8, 2.2);
        dc.DrawLine(Pen("#FFFFFF", 0.5, 0.5), new Point(14, 29), new Point(34, 29));

        dc.DrawLine(Pen("#050506", 5.5), new Point(24, 33), new Point(24, 44.5));
        dc.DrawLine(Pen("#1A1A1E", 3.8), new Point(24, 33.5), new Point(24, 44));
        for (var y = 34.5; y < 43.5; y += 1.6)
        {
            dc.DrawLine(Pen("#44444A", 0.7, 0.7), new Point(22.2, y), new Point(25.8, y + 0.8));
        }

        dc.DrawEllipse(Linear("#50525A", "#181A1E", 0, 0, 1, 1), Pen("#050506", 1.0), new Point(24, 45.5), 4.5, 2.8);
        dc.DrawEllipse(Linear("#E44558", "#8E1020", 0, 0, 1, 1), Pen("#050506", 0.8), new Point(24, 46.2), 2.8, 1.8);
        dc.DrawEllipse(Brush("#FFA0AB", 0.7), null, new Point(23.3, 46.8), 0.7, 0.4);
    }

    private static void DrawHeart(DrawingContext dc, Point center, double scale, Brush fill, Pen? pen)
    {
        var geometry = Geometry.Parse("M 0,-8 C -8,-17 -23,-6 -18,8 C -15,17 -5,23 0,28 C 5,23 15,17 18,8 C 23,-6 8,-17 0,-8 Z").Clone();
        geometry.Transform = new TransformGroup
        {
            Children =
            {
                new ScaleTransform(scale, scale),
                new TranslateTransform(center.X, center.Y)
            }
        };
        dc.DrawGeometry(fill, pen, geometry);
    }

    private static void DrawStar(DrawingContext dc, Point center, double outer, double inner, Brush fill, Pen? pen)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var i = 0; i < 10; i++)
            {
                var angle = -Math.PI / 2 + i * Math.PI / 5;
                var radius = i % 2 == 0 ? outer : inner;
                var point = new Point(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius);
                if (i == 0)
                {
                    ctx.BeginFigure(point, true, true);
                }
                else
                {
                    ctx.LineTo(point, true, false);
                }
            }
        }

        geometry.Freeze();
        dc.DrawGeometry(fill, pen, geometry);
    }

    private static void DrawTinyText(DrawingContext dc, string text, double size, Point origin, string color)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            System.Windows.FlowDirection.LeftToRight,
            new Typeface(new System.Windows.Media.FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            size,
            Brush(color),
            1.0);
        dc.DrawText(formatted, origin);
    }

    private static void DrawFittedText(DrawingContext dc, string text, Rect bounds, string color, double maxSize, FontWeight weight)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            System.Windows.FlowDirection.LeftToRight,
            new Typeface(new System.Windows.Media.FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
            maxSize,
            Brush(color),
            1.0);
        var width = Math.Max(0.01, formatted.WidthIncludingTrailingWhitespace);
        var height = Math.Max(0.01, formatted.Height);
        var scale = Math.Min(bounds.Width / width, bounds.Height / height);

        dc.PushTransform(new TranslateTransform(bounds.Left + (bounds.Width - width * scale) / 2, bounds.Top + (bounds.Height - height * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.DrawText(formatted, new Point(0, 0));
        dc.Pop();
        dc.Pop();
    }

    private static LinearGradientBrush Linear(string startColor, string endColor, double startX, double startY, double endX, double endY)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(startX, startY),
            EndPoint = new Point(endX, endY)
        };
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(startColor), 0));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(endColor), 1));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush Glow(string color, double opacity) => Brush(color, opacity);

    private static Pen Pen(string color, double thickness) => new(Brush(color), thickness)
    {
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round,
        LineJoin = PenLineJoin.Round
    };

    private static Pen Pen(string color, double thickness, double opacity) => new(Brush(color, opacity), thickness)
    {
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round,
        LineJoin = PenLineJoin.Round
    };

    private static SolidColorBrush Brush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush Brush(string color, double opacity)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color))
        {
            Opacity = Opacity(opacity)
        };
        brush.Freeze();
        return brush;
    }

    private static double Opacity(double value) => Math.Clamp(value, 0, 1);

    private static string Brighten(string color, double brightness)
    {
        var parsed = (Color)ColorConverter.ConvertFromString(color);
        brightness = Math.Clamp(brightness <= 0 ? 1 : brightness, 0.35, 1.8);
        return Color.FromArgb(
            parsed.A,
            BrightenChannel(parsed.R, brightness),
            BrightenChannel(parsed.G, brightness),
            BrightenChannel(parsed.B, brightness)).ToString();
    }

    private static byte BrightenChannel(byte value, double brightness)
    {
        if (brightness <= 1)
        {
            return (byte)Math.Clamp(Math.Round(value * brightness), 0, 255);
        }

        return (byte)Math.Clamp(Math.Round(value + (255 - value) * (brightness - 1) / 0.8), 0, 255);
    }

    private static string Slug(string value)
    {
        var chars = value.ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var slug = string.Join("-", new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(slug) ? $"custom-{DateTime.Now:yyyyMMddHHmmss}" : slug;
    }

    private static ClickEffect EffectFor(string name) => name switch
    {
        "Hearts" => new ClickEffect { Type = "Hearts", PrimaryColor = "#F25AAE", SecondaryColor = "#FFD6EC", ParticleCount = 8, Radius = 26, DurationMs = 560 },
        "Rings" => new ClickEffect { Type = "Rings", PrimaryColor = "#55F7FF", SecondaryColor = "#FFFFFF", ParticleCount = 5, Radius = 30, DurationMs = 520 },
        "Fireflies" => new ClickEffect { Type = "Fireflies", PrimaryColor = "#C9FF7A", SecondaryColor = "#FFEA7A", ParticleCount = 10, Radius = 32, DurationMs = 620 },
        _ => new ClickEffect { Type = "Sparkles", PrimaryColor = "#FFD957", SecondaryColor = "#F25AAE", ParticleCount = 12, Radius = 32, DurationMs = 560 }
    };

    private static ClickEffect EffectWithAccent(ClickEffect effect, string accentColor)
    {
        return new ClickEffect
        {
            Type = effect.Type,
            PrimaryColor = accentColor,
            SecondaryColor = effect.SecondaryColor,
            ParticleCount = effect.ParticleCount,
            Radius = effect.Radius,
            DurationMs = effect.DurationMs
        };
    }

    private static List<BuiltInThemeSpec> BuiltInSpecs() =>
    [
        new("star-wand", "Star Wand", "A cursor-sized recreation of Star Butterfly's wand with tiny hearts and sparkles.", "Magic", 24, 14, new ClickEffect { Type = "Star Wand", PrimaryColor = "#FFD957", SecondaryColor = "#F25AAE", ParticleCount = 12, Radius = 32, DurationMs = 580 }),
        new("neon-ring", "Neon Ring", "Black pointer with cyan and pink glow accents.", "Neon", 4, 3, new ClickEffect { Type = "Rings", PrimaryColor = "#55F7FF", SecondaryColor = "#FF4FD8", ParticleCount = 6, Radius = 30, DurationMs = 520 }),
        new("crystal-arrow", "Crystal Arrow", "Faceted pale-blue pointer with glassy edges.", "Gem", 5, 3, new ClickEffect { Type = "Crystal Shards", PrimaryColor = "#8BE7FF", SecondaryColor = "#FFFFFF", ParticleCount = 9, Radius = 30, DurationMs = 540 }),
        new("pixel-sword", "Pixel Sword", "A compact pixel-art blade for retro desktops.", "Pixel", 9, 6, new ClickEffect { Type = "Pixel Bits", PrimaryColor = "#E8ECFF", SecondaryColor = "#F6C84D", ParticleCount = 10, Radius = 28, DurationMs = 500 }),
        new("moonbeam", "Moonbeam", "Dark pointer with a tiny crescent moon accent.", "Celestial", 4, 3, new ClickEffect { Type = "Moon Dust", PrimaryColor = "#FFE78A", SecondaryColor = "#DDE8FF", ParticleCount = 8, Radius = 28, DurationMs = 560 }),
        new("bubble-pop", "Bubble Pop", "Soft aqua pointer with bubble highlights.", "Playful", 4, 3, new ClickEffect { Type = "Bubble Burst", PrimaryColor = "#9AF2FF", SecondaryColor = "#F69BD8", ParticleCount = 8, Radius = 30, DurationMs = 540 }),
        new("heart-charm", "Heart Charm", "Light pointer with a pink heart charm.", "Magic", 4, 3, new ClickEffect { Type = "Hearts", PrimaryColor = "#F25AAE", SecondaryColor = "#FFD6EC", ParticleCount = 8, Radius = 26, DurationMs = 560 }),
        new("glass-shard", "Glass Shard", "Sharp translucent pointer with slate lines.", "Minimal", 5, 3, new ClickEffect { Type = "Sparkles", PrimaryColor = "#EAF6FF", SecondaryColor = "#94A3B8", ParticleCount = 7, Radius = 28, DurationMs = 480 }),
        new("firefly-trail", "Firefly Trail", "Deep green pointer with tiny light motes.", "Nature", 4, 3, new ClickEffect { Type = "Fireflies", PrimaryColor = "#C9FF7A", SecondaryColor = "#FFEA7A", ParticleCount = 10, Radius = 34, DurationMs = 620 }),
        new("sketch-pen", "Sketch Pen", "Hand-drawn pen nib cursor with a red mark.", "Creative", 7, 4, new ClickEffect { Type = "Ink Flick", PrimaryColor = "#202124", SecondaryColor = "#F0624D", ParticleCount = 7, Radius = 24, DurationMs = 480 }),
        new("candy-bolt", "Candy Bolt", "Bright lightning cursor with candy stripes.", "Playful", 21, 3, new ClickEffect { Type = "Candy Burst", PrimaryColor = "#FFE46A", SecondaryColor = "#F25AAE", ParticleCount = 10, Radius = 30, DurationMs = 530 }),
        new("minimal-crosshair", "Minimal Crosshair", "Precision ring cursor for focused work.", "Utility", 24, 24, new ClickEffect { Type = "Rings", PrimaryColor = "#202124", SecondaryColor = "#55F7FF", ParticleCount = 4, Radius = 24, DurationMs = 450 }),
        new("lightsaber", "Lightsaber", "A glowing saber pointer with configurable blade color.", "Sci-Fi", 5, 5, new ClickEffect { Type = "Saber Blade", PrimaryColor = "#55F7FF", SecondaryColor = "#FFFFFF", ParticleCount = 3, Radius = 0, DurationMs = 430 }, "#55F7FF"),
        new("hero-sword", "Sword of Daylight", "An asymmetric cyan blade cursor with crescent cutout and rune channel.", "Fantasy", 24, 1, new ClickEffect { Type = "Daylight Glint", PrimaryColor = "#DCE7F2", SecondaryColor = "#5FE2EF", ParticleCount = 7, Radius = 24, DurationMs = 390 }),
        new("eclipse-sword", "Sword of Eclipse", "A dark obsidian blade cursor with red energy veins and rune channel.", "Fantasy", 24, 1, new ClickEffect { Type = "Eclipse Runes", PrimaryColor = "#FF123A", SecondaryColor = "#7E0A18", ParticleCount = 8, Radius = 24, DurationMs = 430 }),
        new("omnitrix", "Omnitrix", "A green alien-tech emblem cursor with red core flash.", "Sci-Fi", 24, 24, new ClickEffect { Type = "Omnitrix Core", PrimaryColor = "#FF2020", SecondaryColor = "#FF6262", ParticleCount = 3, Radius = 0, DurationMs = 420 }),
        new("tardis", "TARDIS", "A compact blue police box cursor with timey glow pulses.", "Sci-Fi", 10, 7, new ClickEffect { Type = "Time Vortex", PrimaryColor = "#4A67FF", SecondaryColor = "#FFFFFF", ParticleCount = 5, Radius = 0, DurationMs = 640 }),
        new("autobot-crest", "Autobot Crest", "A red-and-silver robot faction crest inspired cursor.", "Robots", 24, 4, new ClickEffect { Type = "Energon Sparks", PrimaryColor = "#F04B4B", SecondaryColor = "#D8DEE8", ParticleCount = 9, Radius = 30, DurationMs = 500 }),
        new("decepticon-crest", "Decepticon Crest", "A purple angular robot faction crest inspired cursor.", "Robots", 24, 4, new ClickEffect { Type = "Energon Sparks", PrimaryColor = "#A673FF", SecondaryColor = "#3A234F", ParticleCount = 9, Radius = 30, DurationMs = 500 }),
        new("shera-sword", "Sword of Protection", "A crystal-and-gold heroic sword cursor.", "Fantasy", 24, 2, new ClickEffect { Type = "Protection Burst", PrimaryColor = "#BDEFFF", SecondaryColor = "#F2C453", ParticleCount = 11, Radius = 34, DurationMs = 620 }),
        new("ancient-staff", "Ancient Staff", "A carved wooden staff cursor with warm magic motes.", "Fantasy", 31, 6, new ClickEffect { Type = "Rune Motes", PrimaryColor = "#C58C58", SecondaryColor = "#F2C453", ParticleCount = 8, Radius = 30, DurationMs = 620 }),
        new("starfleet-delta", "Starfleet Delta", "A clean gold delta insignia cursor with scanner rings.", "Sci-Fi", 24, 3, new ClickEffect { Type = "Scanner Sweep", PrimaryColor = "#F2D36B", SecondaryColor = "#5FE2EF", ParticleCount = 4, Radius = 0, DurationMs = 500 }),
        new("starship", "Starship", "A tiny silver starship cursor with engine-pink sparks.", "Sci-Fi", 13, 13, new ClickEffect { Type = "Warp Trail", PrimaryColor = "#5FE2EF", SecondaryColor = "#FFFFFF", ParticleCount = 10, Radius = 40, DurationMs = 360 }),
        new("dark-one-dagger", "Dark One Dagger", "A dark ornate dagger cursor with red cursed glints.", "Fantasy", 24, 5, new ClickEffect { Type = "Dark Curse", PrimaryColor = "#A6192E", SecondaryColor = "#D9DEE5", ParticleCount = 7, Radius = 22, DurationMs = 470 })
    ];

    private sealed record BuiltInThemeSpec(
        string Id,
        string Name,
        string Description,
        string Category,
        ushort HotspotX,
        ushort HotspotY,
        ClickEffect Effect,
        string AccentColor = "#55F7FF",
        double GlowScale = 1.0,
        double GlowBrightness = 1.0,
        double CursorScale = 1.0);
}
