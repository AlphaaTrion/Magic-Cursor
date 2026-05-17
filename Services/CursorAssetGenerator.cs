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
    private const string AssetRevision = "q12";

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
        var spec = BuiltInSpecs().FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (spec is null)
        {
            return null;
        }

        return EnsureBuiltInTheme(spec with
        {
            AccentColor = accentColor,
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
        var previewPath = Path.Combine(folder, $"preview-{AssetRevision}.png");
        var cursorPath = Path.Combine(folder, $"arrow-{AssetRevision}.cur");

        var cursorArt = RenderBuiltIn(spec, CursorPixelSize);
        var previewArt = RenderBuiltIn(spec, PreviewPixelSize);
        SavePng(previewArt, previewPath);
        SaveCursor(cursorArt, cursorPath, spec.HotspotX, spec.HotspotY);

        return new CursorTheme
        {
            Id = spec.Id,
            Name = spec.Name,
            Description = spec.Description,
            Category = spec.Category,
            PreviewPath = previewPath,
            Effect = spec.Effect,
            Variants = VariantsFor(cursorPath, spec.HotspotX, spec.HotspotY)
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

    private static RenderTargetBitmap RenderBuiltIn(BuiltInThemeSpec spec, int outputSize)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(outputSize / (double)LogicalSize, outputSize / (double)LogicalSize));
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
        var ink = Pen("#15151C", 2.1);

        DrawWandWing(dc, true);
        DrawWandWing(dc, false);
        DrawWandHorn(dc, true);
        DrawWandHorn(dc, false);

        var handle = Geometry.Parse("M 20.8,27.5 L 27.2,27.5 L 27.2,42.8 L 20.8,42.8 Z");
        dc.DrawGeometry(Brush("#7157B7"), ink, handle);
        dc.PushClip(handle);
        dc.DrawGeometry(Brush("#B89AF3"), null, Geometry.Parse("M 20.9,28 L 27.2,34.2 L 27.2,40.2 L 20.9,34 Z"));
        dc.DrawGeometry(Brush("#4D3C8C"), null, Geometry.Parse("M 20.9,37.3 L 27.2,43.1 L 20.9,43.1 Z"));
        dc.Pop();

        dc.DrawEllipse(Brush("#F6D456"), ink, new Point(24, 42.2), 5.8, 3.0);
        var gem = Geometry.Parse("M 24,42.9 L 28.8,45.8 L 24,48 L 19.2,45.8 Z");
        dc.DrawGeometry(Brush("#EF8CFF"), Pen("#FFFFFF", 1.2), gem);
        dc.DrawLine(Pen("#B754D5", 1.1), new Point(24, 42.8), new Point(24, 47.6));
        dc.DrawLine(Pen("#B754D5", 1.1), new Point(19.2,45.7), new Point(28.8,45.7));

        DrawHeart(dc, new Point(24, 28.2), 0.38, Brush("#F04DA5"), Pen("#591B4B", 1.05));

        dc.DrawEllipse(Brush("#B58CE7"), ink, new Point(24, 15.3), 15.1, 15.1);
        dc.DrawEllipse(Brush("#BFE8F6"), Pen("#15151C", 1.7), new Point(24, 15.3), 11.6, 11.6);

        dc.DrawGeometry(Brush("#EAFBFF"), null, Geometry.Parse("M 14.2,12.7 L 20.5,6.1 L 23.1,12.6 Z"));
        dc.DrawGeometry(Brush("#D8F3FC"), null, Geometry.Parse("M 25.5,6.3 L 29.1,12.8 L 34,11.7 L 31.2,6.4 Z"));
        dc.DrawGeometry(Brush("#A8D9EA"), null, Geometry.Parse("M 13.4,18.2 L 20.5,17.7 L 15.7,23.1 Z"));
        dc.DrawGeometry(Brush("#9ED4E6"), null, Geometry.Parse("M 28.1,18 L 34.5,16.9 L 32.6,23.3 Z"));

        DrawStar(dc, new Point(24, 15.3), 10.2, 4.1, Brush("#FFD45E"), Pen("#FFFFFF", 1.05));
        DrawStar(dc, new Point(24, 15.3), 4.1, 1.7, Brush("#F47A22"), null);

        foreach (var p in new[] { new Point(24, 3.9), new Point(34.7,15.2), new Point(24,26.6), new Point(13.3,15.2), new Point(18.5,8.3), new Point(29.5,8.4) })
        {
            dc.DrawEllipse(Brush("#D43A9D"), null, p, 2.0, 2.0);
        }
    }

    private static void DrawWandWing(DrawingContext dc, bool left)
    {
        var s = left ? -1 : 1;
        var outer = new StreamGeometry();
        using (var ctx = outer.Open())
        {
            ctx.BeginFigure(new Point(24 + s * 12.3, 13), true, true);
            ctx.BezierTo(new Point(24 + s * 17.7, 1.2), new Point(24 + s * 25.7, 2.1), new Point(24 + s * 26.2, 14.8), true, false);
            ctx.BezierTo(new Point(24 + s * 26.4, 22.2), new Point(24 + s * 20.5, 24.6), new Point(24 + s * 17.5, 23), true, false);
            ctx.BezierTo(new Point(24 + s * 18.8, 28.5), new Point(24 + s * 13.1, 27.9), new Point(24 + s * 12.7, 21.6), true, false);
        }

        outer.Freeze();
        dc.DrawGeometry(Brush("#FFFFFF"), Pen("#15151C", 2.1), outer);

        var inner = new StreamGeometry();
        using (var ctx = inner.Open())
        {
            ctx.BeginFigure(new Point(24 + s * 18.2, 9), true, true);
            ctx.BezierTo(new Point(24 + s * 22.6, 7), new Point(24 + s * 22.9, 16.2), new Point(24 + s * 18, 21.5), true, false);
            ctx.BezierTo(new Point(24 + s * 20.6, 18), new Point(24 + s * 18.9, 12.5), new Point(24 + s * 18.2, 9), true, false);
        }

        inner.Freeze();
        dc.DrawGeometry(Brush("#B4B0F0"), null, inner);
    }

    private static void DrawWandHorn(DrawingContext dc, bool left)
    {
        var s = left ? -1 : 1;
        var horn = new StreamGeometry();
        using (var ctx = horn.Open())
        {
            ctx.BeginFigure(new Point(24 + s * 7.9, 5.3), true, true);
            ctx.BezierTo(new Point(24 + s * 9.7, 0.6), new Point(24 + s * 12.9, 0.4), new Point(24 + s * 11.3, 8.6), true, false);
            ctx.LineTo(new Point(24 + s * 6.9, 9.3), true, false);
        }

        horn.Freeze();
        dc.DrawGeometry(Brush("#D84CA9"), Pen("#15151C", 1.35), horn);
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

    private static void DrawHeroSword(DrawingContext dc)
    {
        dc.DrawGeometry(Brush("#000000", 0.15), null, Geometry.Parse("M 5,4 L 13,2 L 44,18 L 46,26 L 34,36 Z"));

        var blade = Geometry.Parse(
            "M 4,5 L 12,2.5 " +
            "L 40,16 L 44,15.5 " +
            "L 44.5,20 " +
            "L 45,24 L 33,33 " +
            "L 31,28 L 8,9 Z");
        dc.DrawGeometry(Linear("#F0F5FA", "#8090A4", 0, 0, 1, 1), Pen("#0C121A", 1.5), blade);

        dc.DrawGeometry(Linear("#FFFFFF", "#C8D2DE", 0, 0, 1, 1), null,
            Geometry.Parse("M 7,6.5 L 38,19 L 40,16.5 L 10,4.5 Z"));
        dc.DrawGeometry(Brush("#8A9AAC", 0.55), null,
            Geometry.Parse("M 38,19 L 44,20 L 37,30 L 32,28 Z"));

        dc.DrawLine(Pen("#606E80", 0.8), new Point(12, 7.5), new Point(38, 19));
        dc.DrawLine(Pen("#606E80", 0.65), new Point(14, 11), new Point(35, 22));
        dc.DrawLine(Pen("#FFFFFF", 0.75, 0.8), new Point(8, 5.5), new Point(30, 15));
        dc.DrawLine(Pen("#B8C5D2", 0.6), new Point(16, 14), new Point(38, 24));

        var guard = Geometry.Parse("M 26,30 L 33,24 L 40,30.5 L 33,37.5 Z");
        dc.DrawGeometry(Linear("#303848", "#101620", 0, 0, 1, 1), Pen("#05080D", 1.3), guard);
        dc.DrawEllipse(Brush("#4A5568"), null, new Point(28.5, 27.5), 1.0, 1.0);
        dc.DrawEllipse(Brush("#4A5568"), null, new Point(37.5, 27.5), 1.0, 1.0);
        dc.DrawEllipse(Brush("#4A5568"), null, new Point(37.5, 33.5), 1.0, 1.0);

        dc.DrawLine(Pen("#101620", 4.0), new Point(34, 36), new Point(45, 45));
        dc.DrawLine(Pen("#253040", 5.5), new Point(35.5, 34.5), new Point(43.5, 41));
        dc.DrawLine(Pen("#0B0F16", 1.0), new Point(36, 35), new Point(43, 40.5));

        dc.DrawEllipse(Brush("#55E5F2", 0.25), null, new Point(31.5, 32.5), 5.5, 5.5);
        dc.DrawEllipse(Linear("#9CF6FF", "#1A7A90", 0, 0, 1, 1), Pen("#102733", 0.9), new Point(31.5, 32.5), 3.2, 3.2);
        dc.DrawEllipse(Brush("#FFFFFF", 0.88), null, new Point(30.5, 31.5), 0.8, 0.8);
    }

    private static void DrawEclipseSword(DrawingContext dc)
    {
        dc.DrawGeometry(Brush("#FF1020", 0.10), null, Geometry.Parse("M 5,4 L 13,2 L 44,18 L 46,26 L 34,36 Z"));

        var blade = Geometry.Parse(
            "M 4,5 L 12,2.5 " +
            "L 40,16 L 44,15.5 " +
            "L 44.5,20 " +
            "L 45,24 L 33,33 " +
            "L 31,28 L 8,9 Z");
        dc.DrawGeometry(Linear("#1A2040", "#080C1A", 0, 0, 1, 1), Pen("#050810", 1.5), blade);

        dc.DrawGeometry(Linear("#141830", "#0A0E1A", 0, 0, 1, 1), null,
            Geometry.Parse("M 7,6.5 L 38,19 L 40,16.5 L 10,4.5 Z"));

        dc.DrawGeometry(Brush("#FF1030", 0.7), Pen("#FF4060", 0.4),
            Geometry.Parse("M 12,8 L 36,20.5 L 34.5,23 L 14,12 Z"));
        dc.DrawLine(Pen("#FF1030", 1.4), new Point(14, 10), new Point(33, 20));
        dc.DrawLine(Pen("#FF1030", 0.9), new Point(18, 14), new Point(35, 23));
        dc.DrawLine(Pen("#FF4060", 0.6, 0.8), new Point(9, 7), new Point(26, 16));
        dc.DrawLine(Pen("#FF1030", 0.7), new Point(38, 20), new Point(40, 27));

        var guard = Geometry.Parse("M 26,30 L 33,24 L 40,30.5 L 33,37.5 Z");
        dc.DrawGeometry(Linear("#2A1020", "#0D060B", 0, 0, 1, 1), Pen("#070305", 1.2), guard);
        dc.DrawLine(Pen("#FF1030", 1.2), new Point(30, 31), new Point(34, 27));

        dc.DrawEllipse(Brush("#FF1030", 0.28), null, new Point(31.5, 32.5), 5.0, 5.0);
        dc.DrawEllipse(Linear("#FF4060", "#B20618", 0, 0, 1, 1), Pen("#31040A", 0.8), new Point(31.5, 32.5), 3.0, 3.0);
        dc.DrawEllipse(Brush("#FF8888", 0.65), null, new Point(30.5, 31.5), 0.6, 0.6);

        dc.DrawLine(Pen("#120810", 4.5), new Point(34, 36), new Point(45, 45));
        dc.DrawLine(Pen("#2A1018", 5.5), new Point(35.5, 34.5), new Point(43.5, 41));
        dc.DrawLine(Pen("#0A060A", 1.0), new Point(36, 35), new Point(43, 40.5));
        dc.DrawLine(Pen("#FF1030", 0.7), new Point(38, 38), new Point(42, 41));
    }

    private static void DrawOmnitrix(DrawingContext dc)
    {
        dc.DrawEllipse(Linear("#4A4E50", "#282A2C", 0, 0, 1, 1), Pen("#0A0A0A", 2.5), new Point(24, 24), 21.0, 21.0);

        dc.DrawEllipse(Linear("#606468", "#3A3C40", 0, 0, 1, 1), Pen("#0A0A0A", 1.2), new Point(24, 24), 17.5, 17.5);

        dc.DrawEllipse(null, Pen("#90E828", 1.0), new Point(24, 24), 16.0, 16.0);

        dc.DrawEllipse(Brush("#585C60"), null, new Point(24, 24), 14.5, 14.5);

        dc.DrawGeometry(Brush("#90E828"), Pen("#0A0A0A", 1.5),
            Geometry.Parse("M 16,10 L 32,10 L 27,21 L 21,21 Z"));
        dc.DrawGeometry(Brush("#90E828"), Pen("#0A0A0A", 1.5),
            Geometry.Parse("M 16,38 L 32,38 L 27,27 L 21,27 Z"));

        dc.DrawGeometry(Brush("#505458"), Pen("#0A0A0A", 1.2),
            Geometry.Parse("M 10,16 L 21,21 L 21,27 L 10,32 Z"));
        dc.DrawGeometry(Brush("#505458"), Pen("#0A0A0A", 1.2),
            Geometry.Parse("M 38,16 L 27,21 L 27,27 L 38,32 Z"));

        dc.DrawGeometry(Brush("#B0FF40", 0.2), null,
            Geometry.Parse("M 18,11 L 28,11 L 26,20 L 22,20 Z"));

        dc.DrawEllipse(null, Pen("#0A0A0A", 2.8), new Point(24, 24), 21.0, 21.0);

        foreach (var p in new[] { new Point(24, 3.0), new Point(45.0, 24), new Point(24, 45.0), new Point(3.0, 24) })
        {
            dc.DrawEllipse(Brush("#3A3E40"), Pen("#0A0A0A", 0.8), p, 3.5, 3.5);
            dc.DrawEllipse(Brush("#90E828"), null, p, 2.5, 2.5);
            dc.DrawEllipse(Brush("#C0FF50", 0.4), null, new Point(p.X - 0.4, p.Y - 0.4), 0.7, 0.7);
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
        var outer = Geometry.Parse(
            "M 24,4 " +
            "L 5,12 L 7,14 L 5,16 L 7,18 L 5,20 " +
            "L 8,22 L 10,38 L 16,42 L 24,44 " +
            "L 32,42 L 38,38 L 40,22 " +
            "L 43,20 L 41,18 L 43,16 L 41,14 L 43,12 Z");
        dc.DrawGeometry(Brush("#E02020"), Pen("#0A0A0A", 1.0), outer);

        dc.DrawGeometry(Brush("#0A0A0A"), null,
            Geometry.Parse("M 24,6 L 20,12 L 22,12 L 24,8 L 26,12 L 28,12 Z"));

        dc.DrawGeometry(Brush("#0A0A0A"), null,
            Geometry.Parse("M 12,16 L 22,14 L 21,19 L 11,18 Z"));
        dc.DrawGeometry(Brush("#0A0A0A"), null,
            Geometry.Parse("M 36,16 L 26,14 L 27,19 L 37,18 Z"));

        dc.DrawGeometry(Brush("#0A0A0A"), null,
            Geometry.Parse("M 22,20 L 26,20 L 25.5,28 L 22.5,28 Z"));

        dc.DrawGeometry(Brush("#0A0A0A"), null,
            Geometry.Parse("M 10,22 L 12,22 L 13,30 L 11,30 Z"));
        dc.DrawGeometry(Brush("#0A0A0A"), null,
            Geometry.Parse("M 38,22 L 36,22 L 35,30 L 37,30 Z"));

        dc.DrawGeometry(Brush("#0A0A0A"), null,
            Geometry.Parse("M 13,31 L 21,29 L 20,33 L 14,33 Z"));
        dc.DrawGeometry(Brush("#0A0A0A"), null,
            Geometry.Parse("M 35,31 L 27,29 L 28,33 L 34,33 Z"));

        dc.DrawGeometry(Brush("#0A0A0A"), null,
            Geometry.Parse("M 17,34 L 31,34 L 28,38 L 20,38 Z"));

        dc.DrawGeometry(Brush("#0A0A0A"), null,
            Geometry.Parse("M 22,39 L 26,39 L 25,42 L 23,42 Z"));
    }

    private static void DrawDecepticonCrest(DrawingContext dc)
    {
        var outer = Geometry.Parse(
            "M 24,4 L 28,3 L 34,6 " +
            "L 43,10 L 41,12 L 43,14 L 41,16 L 43,18 " +
            "L 40,20 L 38,28 " +
            "L 34,34 L 24,46 " +
            "L 14,34 L 10,28 L 8,20 " +
            "L 5,18 L 7,16 L 5,14 L 7,12 L 5,10 " +
            "L 14,6 L 20,3 Z");
        dc.DrawGeometry(Brush("#6B2FA0"), Pen("#0A0A0A", 1.0), outer);

        dc.DrawGeometry(Brush("#0A0A0A"), null,
            Geometry.Parse("M 24,6 L 21,12 L 23,12 L 24,8 L 25,12 L 27,12 Z"));

        dc.DrawGeometry(Brush("#0A0A0A"), null,
            Geometry.Parse("M 11,14 L 22,13 L 21,18 L 10,17 Z"));
        dc.DrawGeometry(Brush("#0A0A0A"), null,
            Geometry.Parse("M 37,14 L 26,13 L 27,18 L 38,17 Z"));

        dc.DrawGeometry(Brush("#0A0A0A"), null,
            Geometry.Parse("M 22,19 L 26,19 L 25,26 L 23,26 Z"));

        dc.DrawGeometry(Brush("#0A0A0A"), null,
            Geometry.Parse("M 9,22 L 12,20 L 13,28 L 10,27 Z"));
        dc.DrawGeometry(Brush("#0A0A0A"), null,
            Geometry.Parse("M 39,22 L 36,20 L 35,28 L 38,27 Z"));

        dc.DrawGeometry(Brush("#0A0A0A"), null,
            Geometry.Parse("M 14,30 L 22,27 L 24,32 L 16,33 Z"));
        dc.DrawGeometry(Brush("#0A0A0A"), null,
            Geometry.Parse("M 34,30 L 26,27 L 24,32 L 32,33 Z"));

        dc.DrawGeometry(Brush("#0A0A0A"), null,
            Geometry.Parse("M 20,35 L 28,35 L 24,44 Z"));
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
        dc.DrawEllipse(Brush("#F0D060", 0.08), null, new Point(24, 14), 16, 16);
        dc.DrawEllipse(Brush("#F0D060", 0.04), null, new Point(24, 14), 22, 22);

        dc.DrawLine(Pen("#2A1808", 4.5), new Point(24, 18), new Point(24, 47));
        dc.DrawLine(Pen("#6B4828", 3.2), new Point(24, 18), new Point(24, 47));
        dc.DrawLine(Pen("#8B6840", 0.7, 0.5), new Point(23, 20), new Point(23, 45));
        dc.DrawLine(Pen("#4A2818", 0.5, 0.4), new Point(25, 20), new Point(25, 45));

        var head = Geometry.Parse(
            "M 24,2 " +
            "C 17,2 13,5 13,10 " +
            "C 13,14 16,17.5 20,18 " +
            "L 22,18.5 L 24,17 L 26,18.5 L 28,18 " +
            "C 32,17.5 35,14 35,10 " +
            "C 35,5 31,2 24,2 Z");
        dc.DrawGeometry(Linear("#7A5830", "#4A3018", 0, 0, 1, 1), Pen("#1D100B", 1.3), head);

        dc.DrawGeometry(Linear("#B08850", "#8A6838", 0, 0, 1, 1), null,
            Geometry.Parse("M 24,4 C 19,4 16,6 16,9.5 C 16,13 19,15.5 22,16 L 24,14.5 L 26,16 C 29,15.5 32,13 32,9.5 C 32,6 29,4 24,4 Z"));

        dc.DrawGeometry(Brush("#C8A868"), Pen("#5A3820", 0.6),
            Geometry.Parse("M 16.5,7 C 16.5,5.5 18.5,5 21,6.5 C 22,7.5 22,10 19.5,10.5 C 17.5,10.5 16.5,9 16.5,7 Z"));
        dc.DrawGeometry(Brush("#C8A868"), Pen("#5A3820", 0.6),
            Geometry.Parse("M 31.5,7 C 31.5,5.5 29.5,5 27,6.5 C 26,7.5 26,10 28.5,10.5 C 30.5,10.5 31.5,9 31.5,7 Z"));

        dc.DrawEllipse(Brush("#1D100B"), null, new Point(19, 8), 1.2, 0.7);
        dc.DrawEllipse(Brush("#1D100B"), null, new Point(29, 8), 1.2, 0.7);

        dc.DrawGeometry(Brush("#4A3018"), null,
            Geometry.Parse("M 22.5,11.5 L 25.5,11.5 L 24,13.5 Z"));

        dc.DrawEllipse(Brush("#F0D060", 0.12), null, new Point(24, 12), 12, 12);
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
            Opacity = opacity
        };
        brush.Freeze();
        return brush;
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
        new("crystal-arrow", "Crystal Arrow", "Faceted pale-blue pointer with glassy edges.", "Gem", 5, 3, new ClickEffect { Type = "Sparkles", PrimaryColor = "#8BE7FF", SecondaryColor = "#FFFFFF", ParticleCount = 9, Radius = 30, DurationMs = 540 }),
        new("pixel-sword", "Pixel Sword", "A compact pixel-art blade for retro desktops.", "Pixel", 9, 6, new ClickEffect { Type = "Sparkles", PrimaryColor = "#E8ECFF", SecondaryColor = "#F6C84D", ParticleCount = 8, Radius = 28, DurationMs = 500 }),
        new("moonbeam", "Moonbeam", "Dark pointer with a tiny crescent moon accent.", "Celestial", 4, 3, new ClickEffect { Type = "Rings", PrimaryColor = "#FFE78A", SecondaryColor = "#DDE8FF", ParticleCount = 6, Radius = 28, DurationMs = 560 }),
        new("bubble-pop", "Bubble Pop", "Soft aqua pointer with bubble highlights.", "Playful", 4, 3, new ClickEffect { Type = "Rings", PrimaryColor = "#9AF2FF", SecondaryColor = "#F69BD8", ParticleCount = 7, Radius = 30, DurationMs = 540 }),
        new("heart-charm", "Heart Charm", "Light pointer with a pink heart charm.", "Magic", 4, 3, new ClickEffect { Type = "Hearts", PrimaryColor = "#F25AAE", SecondaryColor = "#FFD6EC", ParticleCount = 8, Radius = 26, DurationMs = 560 }),
        new("glass-shard", "Glass Shard", "Sharp translucent pointer with slate lines.", "Minimal", 5, 3, new ClickEffect { Type = "Sparkles", PrimaryColor = "#EAF6FF", SecondaryColor = "#94A3B8", ParticleCount = 7, Radius = 28, DurationMs = 480 }),
        new("firefly-trail", "Firefly Trail", "Deep green pointer with tiny light motes.", "Nature", 4, 3, new ClickEffect { Type = "Fireflies", PrimaryColor = "#C9FF7A", SecondaryColor = "#FFEA7A", ParticleCount = 10, Radius = 34, DurationMs = 620 }),
        new("sketch-pen", "Sketch Pen", "Hand-drawn pen nib cursor with a red mark.", "Creative", 7, 4, new ClickEffect { Type = "Sparkles", PrimaryColor = "#202124", SecondaryColor = "#F0624D", ParticleCount = 6, Radius = 24, DurationMs = 480 }),
        new("candy-bolt", "Candy Bolt", "Bright lightning cursor with candy stripes.", "Playful", 21, 3, new ClickEffect { Type = "Sparkles", PrimaryColor = "#FFE46A", SecondaryColor = "#F25AAE", ParticleCount = 9, Radius = 30, DurationMs = 530 }),
        new("minimal-crosshair", "Minimal Crosshair", "Precision ring cursor for focused work.", "Utility", 24, 24, new ClickEffect { Type = "Rings", PrimaryColor = "#202124", SecondaryColor = "#55F7FF", ParticleCount = 4, Radius = 24, DurationMs = 450 }),
        new("lightsaber", "Lightsaber", "A glowing saber pointer with configurable blade color.", "Sci-Fi", 5, 5, new ClickEffect { Type = "Saber Sparks", PrimaryColor = "#55F7FF", SecondaryColor = "#FFFFFF", ParticleCount = 10, Radius = 34, DurationMs = 430 }, "#55F7FF"),
        new("hero-sword", "Sword of Daylight", "A broad silver daylight sword cursor with cyan jewel details.", "Fantasy", 7, 5, new ClickEffect { Type = "Blade Glints", PrimaryColor = "#DCE7F2", SecondaryColor = "#5FE2EF", ParticleCount = 8, Radius = 28, DurationMs = 470 }),
        new("eclipse-sword", "Sword of Eclipse", "A dark red energy sword cursor with subtle cursed glints.", "Fantasy", 7, 5, new ClickEffect { Type = "Cursed Sparks", PrimaryColor = "#FF123A", SecondaryColor = "#7E0A18", ParticleCount = 9, Radius = 28, DurationMs = 520 }),
        new("omnitrix", "Omnitrix", "A green alien-tech emblem cursor with pulse rings.", "Sci-Fi", 24, 24, new ClickEffect { Type = "Rings", PrimaryColor = "#B8FF2F", SecondaryColor = "#2DFF7A", ParticleCount = 6, Radius = 30, DurationMs = 520 }),
        new("tardis", "TARDIS", "A compact blue police box cursor with timey glow pulses.", "Sci-Fi", 10, 7, new ClickEffect { Type = "Rings", PrimaryColor = "#4A67FF", SecondaryColor = "#FFFFFF", ParticleCount = 6, Radius = 32, DurationMs = 580 }),
        new("autobot-crest", "Autobot Crest", "A red-and-silver robot faction crest inspired cursor.", "Robots", 24, 4, new ClickEffect { Type = "Energon Sparks", PrimaryColor = "#F04B4B", SecondaryColor = "#D8DEE8", ParticleCount = 9, Radius = 30, DurationMs = 500 }),
        new("decepticon-crest", "Decepticon Crest", "A purple angular robot faction crest inspired cursor.", "Robots", 24, 4, new ClickEffect { Type = "Energon Sparks", PrimaryColor = "#A673FF", SecondaryColor = "#3A234F", ParticleCount = 9, Radius = 30, DurationMs = 500 }),
        new("shera-sword", "Sword of Protection", "A crystal-and-gold heroic sword cursor.", "Fantasy", 24, 2, new ClickEffect { Type = "Sparkles", PrimaryColor = "#BDEFFF", SecondaryColor = "#F2C453", ParticleCount = 11, Radius = 34, DurationMs = 620 }),
        new("ancient-staff", "Ancient Staff", "A carved wooden staff cursor with warm magic motes.", "Fantasy", 31, 6, new ClickEffect { Type = "Fireflies", PrimaryColor = "#C58C58", SecondaryColor = "#F2C453", ParticleCount = 8, Radius = 30, DurationMs = 620 }),
        new("starfleet-delta", "Starfleet Delta", "A clean gold delta insignia cursor with scanner rings.", "Sci-Fi", 24, 3, new ClickEffect { Type = "Rings", PrimaryColor = "#F2D36B", SecondaryColor = "#5FE2EF", ParticleCount = 5, Radius = 26, DurationMs = 500 }),
        new("starship", "Starship", "A tiny silver starship cursor with engine-pink sparks.", "Sci-Fi", 13, 13, new ClickEffect { Type = "Warp Sparks", PrimaryColor = "#5FE2EF", SecondaryColor = "#F25AAE", ParticleCount = 10, Radius = 36, DurationMs = 520 }),
        new("dark-one-dagger", "Dark One Dagger", "A dark ornate dagger cursor with red cursed glints.", "Fantasy", 24, 5, new ClickEffect { Type = "Cursed Sparks", PrimaryColor = "#A6192E", SecondaryColor = "#D9DEE5", ParticleCount = 7, Radius = 24, DurationMs = 560 })
    ];

    private sealed record BuiltInThemeSpec(
        string Id,
        string Name,
        string Description,
        string Category,
        ushort HotspotX,
        ushort HotspotY,
        ClickEffect Effect,
        string AccentColor = "#55F7FF");
}
