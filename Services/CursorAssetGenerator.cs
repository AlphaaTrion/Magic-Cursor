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
    private const string AssetRevision = "q10";

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
        dc.DrawGeometry(Brush("#000000", 0.18), null, Geometry.Parse("M 5.5,9 L 10,6 L 42,22.5 L 46,22 L 35,37 Z"));

        var blade = Geometry.Parse("M 3.5,6.5 L 8.5,3.5 L 39.5,21 L 43.5,19.5 L 40.5,25 L 45.5,25.5 L 33,35 Z");
        dc.DrawGeometry(Linear("#F7FBFF", "#6A778A", 0, 0, 1, 1), Pen("#0C121A", 1.65), blade);

        dc.DrawGeometry(Linear("#FFFFFF", "#C0CAD6", 0, 0, 1, 1), null, Geometry.Parse("M 7.5,6 L 35,21.5 L 31,27 L 9.5,9 Z"));
        dc.DrawGeometry(Brush("#8090A4", 0.6), null, Geometry.Parse("M 35,21.5 L 42,22.5 L 33.5,33 L 31,27 Z"));

        dc.DrawLine(Pen("#52606E", 0.85), new Point(14, 10), new Point(34, 21.5));
        dc.DrawLine(Pen("#52606E", 0.7), new Point(16.5, 13.5), new Point(31.5, 23));
        dc.DrawLine(Pen("#FFFFFF", 0.85, 0.85), new Point(8, 6.5), new Point(28, 17.5));
        dc.DrawLine(Pen("#B8C5D2", 0.7), new Point(18, 16), new Point(37, 24.5));

        var guard = Geometry.Parse("M 26.5,32 L 32.5,25.5 L 40.5,32 L 34.5,39 Z");
        dc.DrawGeometry(Linear("#303948", "#101620", 0, 0, 1, 1), Pen("#05080D", 1.3), guard);

        dc.DrawLine(Pen("#101620", 4.0), new Point(34.5, 37.5), new Point(45.5, 45.5));
        dc.DrawLine(Pen("#253142", 5.4), new Point(36, 35), new Point(44, 41));
        dc.DrawLine(Pen("#0B0F16", 1.0), new Point(36.5, 35.5), new Point(43.5, 40.5));
        dc.DrawLine(Pen("#E6F3F7", 0.9), new Point(36, 34.5), new Point(38, 36));

        dc.DrawEllipse(Brush("#55E5F2", 0.25), null, new Point(31.5, 33.5), 5.5, 5.5);
        dc.DrawEllipse(Linear("#9CF6FF", "#1A7A90", 0, 0, 1, 1), Pen("#102733", 0.9), new Point(31.5, 33.5), 3.0, 3.0);
        dc.DrawEllipse(Brush("#FFFFFF", 0.88), null, new Point(30.5, 32.5), 0.8, 0.8);
    }

    private static void DrawEclipseSword(DrawingContext dc)
    {
        dc.DrawGeometry(Brush("#FF1838", 0.14), null, Geometry.Parse("M 3,8 L 9,4 L 43,24 L 47,24 L 34,37 Z"));

        var blade = Geometry.Parse("M 4,7.5 L 9,4.5 L 40,22.5 L 43.5,21 L 40.5,26.5 L 45,27 L 33,35.5 Z");
        dc.DrawGeometry(Linear("#4A5565", "#101824", 0, 0, 1, 1), Pen("#070A10", 1.65), blade);

        dc.DrawGeometry(Linear("#202A38", "#0A1018", 0, 0, 1, 1), null, Geometry.Parse("M 8.5,7 L 32,22 L 30,27.5 L 11,10.5 Z"));

        dc.DrawGeometry(Brush("#FF123A", 0.78), Pen("#FF6A7F", 0.5), Geometry.Parse("M 14,11 L 35.5,23 L 33.5,25.5 L 16.5,14.5 Z"));
        dc.DrawLine(Pen("#FF123A", 1.2), new Point(17, 13), new Point(31, 21.5));
        dc.DrawLine(Pen("#FF123A", 0.9), new Point(23, 19), new Point(32.5, 24.5));
        dc.DrawLine(Pen("#FF6A7F", 0.7, 0.85), new Point(11, 8.5), new Point(26, 17));

        var guard = Geometry.Parse("M 27,32.5 L 33,26 L 40,32 L 33.5,39 Z");
        dc.DrawGeometry(Linear("#3A1B24", "#0D080B", 0, 0, 1, 1), Pen("#070305", 1.2), guard);
        dc.DrawLine(Pen("#FF123A", 1.5), new Point(30.5, 31.5), new Point(34.5, 27.5));

        dc.DrawEllipse(Brush("#FF123A", 0.28), null, new Point(32.5, 33), 4.8, 4.8);
        dc.DrawEllipse(Linear("#FF4D66", "#B20621", 0, 0, 1, 1), Pen("#31040A", 0.8), new Point(32.5, 33), 2.7, 2.7);
        dc.DrawEllipse(Brush("#FF8888", 0.65), null, new Point(31.5, 32), 0.6, 0.6);

        dc.DrawLine(Pen("#1C0E13", 5.5), new Point(35, 37.5), new Point(45.5, 45));
        dc.DrawLine(Pen("#3F1822", 1.8), new Point(35.5, 38), new Point(44.5, 44));
        dc.DrawLine(Pen("#FF123A", 0.9), new Point(39, 40), new Point(42, 42.2));
    }

    private static void DrawOmnitrix(DrawingContext dc)
    {
        dc.DrawEllipse(Brush("#000000", 0.2), null, new Point(25, 25), 20.5, 20.5);

        dc.DrawEllipse(Linear("#7A7C7E", "#3A3C3E", 0, 0, 1, 1), Pen("#0A0A0A", 1.8), new Point(24, 24), 20.0, 20.0);
        dc.DrawEllipse(Brush("#2A2C2A"), Pen("#181918", 0.7), new Point(24, 24), 17.0, 17.0);

        dc.DrawEllipse(Brush("#0C0D0C"), Pen("#1A1B1A", 0.5), new Point(24, 24), 14.5, 14.5);

        dc.DrawGeometry(Brush("#5A5C5E"), Pen("#0A0A0A", 0.7),
            Geometry.Parse("M 9.0,18.5 L 15.0,21.5 L 15.0,26.5 L 9.0,29.5 C 7.8,25.8 7.8,22.2 9.0,18.5 Z"));
        dc.DrawGeometry(Brush("#5A5C5E"), Pen("#0A0A0A", 0.7),
            Geometry.Parse("M 39.0,18.5 L 33.0,21.5 L 33.0,26.5 L 39.0,29.5 C 40.2,25.8 40.2,22.2 39.0,18.5 Z"));

        var hourglass = Geometry.Parse("M 17.5,10.5 L 30.5,10.5 L 25.8,21.5 L 25.8,26.5 L 30.5,37.5 L 17.5,37.5 L 22.2,26.5 L 22.2,21.5 Z");
        dc.DrawGeometry(Brush("#00D413"), Pen("#004D08", 1.1), hourglass);

        dc.DrawGeometry(Brush("#66FF66", 0.25), null,
            Geometry.Parse("M 19.0,11.5 L 28.0,11.5 L 24.5,20.5 L 23.0,20.5 Z"));

        dc.DrawEllipse(Brush("#00D413"), Pen("#004D08", 1.0), new Point(24, 24), 3.0, 3.0);
        dc.DrawEllipse(Brush("#88FF88", 0.45), null, new Point(23.3, 23.3), 0.9, 0.9);

        dc.DrawEllipse(null, Pen("#0A0B0A", 2.2), new Point(24, 24), 20.0, 20.0);

        foreach (var p in new[] { new Point(24, 4.0), new Point(44.0, 24), new Point(24, 44.0), new Point(4.0, 24) })
        {
            dc.DrawEllipse(Brush("#0C0E0C"), Pen("#0A0A0A", 0.7), p, 3.5, 3.5);
            dc.DrawEllipse(Brush("#00D413"), null, p, 2.4, 2.4);
            dc.DrawEllipse(Brush("#88FF88", 0.5), null, new Point(p.X - 0.5, p.Y - 0.5), 0.6, 0.6);
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
        dc.DrawGeometry(Brush("#000000", 0.2), null,
            Geometry.Parse("M 25,5 L 41,12 L 38,30 L 25,47 L 12,30 L 9,12 Z"));

        var shield = Geometry.Parse("M 24,3 L 40.5,10.5 L 37.5,29 L 32,38.5 L 24,45 L 16,38.5 L 10.5,29 L 7.5,10.5 Z");
        dc.DrawGeometry(Linear("#F0F2F5", "#8A919A", 0, 0, 1, 1), Pen("#111721", 1.55), shield);

        var face = Geometry.Parse(
            "M 24,6 " +
            "L 35,11.5 L 37,14 " +
            "L 33.5,17.5 " +
            "L 36,21 L 35.5,26 " +
            "L 32,30 L 30,35 L 29,37 " +
            "L 24,43 " +
            "L 19,37 L 18,35 L 16,30 " +
            "L 12.5,26 L 12,21 " +
            "L 14.5,17.5 " +
            "L 11,14 L 13,11.5 Z");
        dc.DrawGeometry(Linear("#E83030", "#8E1218", 0, 0, 1, 1), Pen("#1A0B0E", 0.7), face);

        dc.DrawGeometry(Brush("#CC2020"), null,
            Geometry.Parse("M 24,7 L 20,13 L 24,11 L 28,13 Z"));

        dc.DrawGeometry(Brush("#0E141C"), null,
            Geometry.Parse("M 15,18 L 22,16.5 L 21,21 L 14,20.5 Z"));
        dc.DrawGeometry(Brush("#0E141C"), null,
            Geometry.Parse("M 33,18 L 26,16.5 L 27,21 L 34,20.5 Z"));

        dc.DrawGeometry(Brush("#0E141C"), null,
            Geometry.Parse("M 22,24 L 26,24 L 24,28 Z"));

        dc.DrawGeometry(Brush("#0E141C"), null,
            Geometry.Parse("M 20,32 L 28,32 L 24,37 Z"));

        dc.DrawGeometry(Brush("#0E141C", 0.5), null,
            Geometry.Parse("M 22.5,37 L 25.5,37 L 24,41 Z"));

        dc.DrawLine(Pen("#6E0C11", 1.0), new Point(14, 22), new Point(21, 23));
        dc.DrawLine(Pen("#6E0C11", 1.0), new Point(34, 22), new Point(27, 23));

        dc.DrawLine(Pen("#5E0C11", 0.9), new Point(24, 7.5), new Point(24, 42));

        dc.DrawLine(Pen("#FF9090", 0.6, 0.5), new Point(13.5, 12), new Point(23, 7));
    }

    private static void DrawDecepticonCrest(DrawingContext dc)
    {
        dc.DrawGeometry(Brush("#000000", 0.2), null,
            Geometry.Parse("M 25,5 L 40,16 L 36,39 L 25,47 L 14,39 L 10,16 Z"));

        var outer = Geometry.Parse("M 24,3 L 41,15 L 36.5,38 L 24,46 L 11.5,38 L 7,15 Z");
        dc.DrawGeometry(Linear("#D8BCFF", "#5D368F", 0, 0, 1, 1), Pen("#140C1F", 1.6), outer);

        var face = Geometry.Parse(
            "M 24,7 " +
            "L 34,13 L 36,16 " +
            "L 33,19.5 " +
            "L 36.5,23 L 36,27 " +
            "L 33,31 L 30,36 L 29,38 " +
            "L 24,44 " +
            "L 19,38 L 18,36 L 15,31 " +
            "L 12,27 L 11.5,23 " +
            "L 15,19.5 " +
            "L 12,16 L 14,13 Z");
        dc.DrawGeometry(Linear("#2A1840", "#100A1A", 0, 0, 1, 1), Pen("#080410", 0.7), face);

        dc.DrawGeometry(Brush("#B888F0", 0.5), null,
            Geometry.Parse("M 24,8 L 20,14 L 24,12 L 28,14 Z"));

        dc.DrawGeometry(Brush("#DCCEFF"), null,
            Geometry.Parse("M 14,19 L 22,17.5 L 21,22 L 13,21.5 Z"));
        dc.DrawGeometry(Brush("#DCCEFF"), null,
            Geometry.Parse("M 34,19 L 26,17.5 L 27,22 L 35,21.5 Z"));

        dc.DrawGeometry(Brush("#B888F0", 0.5), null,
            Geometry.Parse("M 22.5,8 L 25.5,8 L 24,27 Z"));

        dc.DrawGeometry(Brush("#DCCEFF", 0.35), null,
            Geometry.Parse("M 19,35 L 29,35 L 24,42 Z"));

        dc.DrawLine(Pen("#1A0E28", 1.0), new Point(24, 8), new Point(24, 43));

        dc.DrawLine(Pen("#EAD8FF", 0.65, 0.5), new Point(14.5, 14), new Point(23, 8));
    }

    private static void DrawSheraSword(DrawingContext dc)
    {
        dc.DrawGeometry(Brush("#9CF4FF", 0.15), null,
            Geometry.Parse("M 24,0 L 32,9 L 31.5,25 L 24,39 L 16.5,25 L 16,9 Z"));

        var blade = Geometry.Parse("M 24,1 L 30,9.5 L 29.5,24 L 24,36 L 18.5,24 L 18,9.5 Z");
        dc.DrawGeometry(Linear("#FFFFFF", "#8AECFF", 0, 0, 1, 1), Pen("#1E7080", 1.2), blade);

        dc.DrawGeometry(Brush("#8EDFEF", 0.45), null,
            Geometry.Parse("M 24,3 L 19.5,10 L 19.8,23 L 24,33 Z"));
        dc.DrawGeometry(Brush("#D4F8FF", 0.6), null,
            Geometry.Parse("M 24,3 L 28.5,10 L 28.2,23 L 24,33 Z"));

        dc.DrawLine(Pen("#FFFFFF", 0.9, 0.9), new Point(24, 2), new Point(24, 34));
        dc.DrawLine(Pen("#BDF8FF", 0.7), new Point(21.5, 10.5), new Point(26.5, 5.5));
        dc.DrawLine(Pen("#3DAABD", 0.6), new Point(19.5, 23), new Point(28.5, 23));

        var guardLeft = Geometry.Parse("M 24,28 C 20,23 15,22 9,28 C 13,32 18,33 24,31 Z");
        var guardRight = Geometry.Parse("M 24,28 C 28,23 33,22 39,28 C 35,32 30,33 24,31 Z");
        dc.DrawGeometry(Linear("#FFE89A", "#BD7D18", 0, 0, 1, 1), Pen("#68450C", 1.2), guardLeft);
        dc.DrawGeometry(Linear("#FFE89A", "#BD7D18", 0, 0, 1, 1), Pen("#68450C", 1.2), guardRight);

        dc.DrawGeometry(Brush("#FFF7C8", 0.45), null,
            Geometry.Parse("M 24,29 C 20,25 16,24 12,28 C 17,30 21,30.5 24,30 Z"));
        dc.DrawGeometry(Brush("#FFF7C8", 0.45), null,
            Geometry.Parse("M 24,29 C 28,25 32,24 36,28 C 31,30 27,30.5 24,30 Z"));

        dc.DrawEllipse(Linear("#B6FCFF", "#19AFC7", 0, 0, 1, 1), Pen("#6F4D11", 0.9), new Point(24, 28.5), 3.0, 4.0);
        dc.DrawEllipse(Brush("#FFFFFF", 0.75), null, new Point(23.2, 27.2), 0.8, 1.0);

        dc.DrawLine(Pen("#C08A20", 4.0), new Point(24, 32), new Point(24, 44.5));
        dc.DrawLine(Pen("#FFE8A0", 0.8, 0.7), new Point(23.2, 33), new Point(23.2, 43.5));

        dc.DrawLine(Pen("#8C6418", 0.8), new Point(22.0, 34), new Point(26.0, 35.5));
        dc.DrawLine(Pen("#8C6418", 0.8), new Point(22.0, 37), new Point(26.0, 38.5));
        dc.DrawLine(Pen("#8C6418", 0.8), new Point(22.0, 40), new Point(26.0, 41.5));

        dc.DrawEllipse(Linear("#F7D66C", "#B66C12", 0, 0, 1, 1), Pen("#68450C", 1.0), new Point(24, 45), 3.5, 2.4);
    }

    private static void DrawAncientStaff(DrawingContext dc)
    {
        dc.DrawEllipse(Brush("#FFB040", 0.12), null, new Point(33.5, 7), 9, 9);

        dc.DrawLine(Pen("#0C0806", 5.5), new Point(11.5, 45.5), new Point(31, 8));
        dc.DrawLine(Pen("#4A2B1D", 4.0), new Point(11.5, 45.5), new Point(31, 8));

        dc.DrawLine(Pen("#9A6948", 0.9, 0.7), new Point(13, 41), new Point(29.5, 10));
        dc.DrawLine(Pen("#6E4430", 0.6, 0.55), new Point(12.5, 43), new Point(30, 9));

        dc.DrawLine(Pen("#1D100B", 0.8, 0.7), new Point(18.5, 32), new Point(21, 29.5));
        dc.DrawLine(Pen("#1D100B", 0.8, 0.7), new Point(23, 23), new Point(25.5, 20.5));

        var head = Geometry.Parse("M 25.5,4.5 C 28,-0.5 35.5,0.5 38.5,5 C 38,12 31.5,14.5 26,10.5 C 24,8.5 23.8,6.5 25.5,4.5 Z");
        dc.DrawGeometry(Linear("#BD8A62", "#5B3424", 0, 0, 1, 1), Pen("#1D100B", 1.4), head);

        dc.DrawGeometry(Brush("#6E4430", 0.85), null,
            Geometry.Parse("M 27,5 L 32,2 L 37,5.5 L 35,10.5 L 28.5,10 Z"));

        dc.DrawLine(Pen("#24130C", 0.9), new Point(28, 7), new Point(34.5, 4.5));
        dc.DrawLine(Pen("#24130C", 0.9), new Point(29.5, 10), new Point(35.5, 6.5));
        dc.DrawLine(Pen("#D09B72", 0.8, 0.6), new Point(27, 4.5), new Point(33.5, 2.5));

        dc.DrawEllipse(Brush("#2A1912"), null, new Point(30.5, 6.2), 1.3, 1.5);
        dc.DrawEllipse(Brush("#2A1912"), null, new Point(34.5, 5.8), 1.1, 1.35);

        dc.DrawEllipse(Brush("#FFB040", 0.18), null, new Point(33.5, 7), 7, 7);
    }

    private static void DrawStarfleetDelta(DrawingContext dc)
    {
        dc.DrawGeometry(Brush("#000000", 0.14), null,
            Geometry.Parse("M 25.5,5 L 40,45 L 25,37 L 10,45 Z"));

        var delta = Geometry.Parse("M 24,3 L 39,44 L 24,36 L 9,44 Z");
        dc.DrawGeometry(Linear("#FFF0A8", "#A86F16", 0, 0, 1, 1), Pen("#121821", 1.5), delta);

        dc.DrawGeometry(Brush("#FFFFFF", 0.3), null,
            Geometry.Parse("M 24,6.5 L 29.5,32 L 24,30 Z"));

        var inner = Geometry.Parse("M 24,14 L 30,34.5 L 24,31 L 18,34.5 Z");
        dc.DrawGeometry(Linear("#19202A", "#06090D", 0, 0, 1, 1), Pen("#5E3E0B", 0.65), inner);

        var starShape = Geometry.Parse("M 24,17 C 27,22 27,26 24,31 C 21,26 21,22 24,17 Z");
        dc.DrawGeometry(Linear("#FFEAA1", "#A87020", 0, 0, 1, 1), Pen("#5E3E0B", 0.5), starShape);

        dc.DrawLine(Pen("#FFEAA1", 0.8, 0.7), new Point(14, 39), new Point(23.5, 5.5));

        dc.DrawEllipse(Brush("#5FE2EF", 0.25), null, new Point(24, 39.5), 3.8, 3.8);
        dc.DrawEllipse(Brush("#5FE2EF"), Pen("#102A32", 0.7), new Point(24, 39.5), 2.0, 2.0);
        dc.DrawEllipse(Brush("#FFFFFF", 0.5), null, new Point(23.4, 38.9), 0.5, 0.5);
    }

    private static void DrawStarship(DrawingContext dc)
    {
        dc.DrawEllipse(Brush("#79F2FF", 0.06), null, new Point(15.5, 13), 15, 8);

        var saucer = Geometry.Parse("M 2.5,13.5 C 5,5 25,3.5 33.5,10.5 C 35,16.5 27.5,21 15.5,21.5 C 7,21.8 1.2,18.5 2.5,13.5 Z");
        dc.DrawGeometry(Linear("#F8FBFF", "#8A96A6", 0, 0, 1, 1), Pen("#1B2633", 1.1), saucer);

        dc.DrawEllipse(Brush("#E8EFF6", 0.9), Pen("#9AA7B4", 0.7), new Point(15.8, 12.5), 10, 4);
        dc.DrawEllipse(Brush("#D8E1EA"), Pen("#7A8796", 0.6), new Point(15.9, 12), 5.5, 2.0);
        dc.DrawEllipse(Brush("#F8FBFF"), null, new Point(15.9, 11.2), 2.8, 0.8);

        dc.DrawEllipse(Brush("#5FE2EF"), null, new Point(8.8, 12.5), 0.9, 0.9);
        DrawFittedText(dc, "NCC-1701", new Rect(10.8, 15.2, 9.2, 1.8), "#263344", 1.55, FontWeights.Bold);
        dc.DrawLine(Pen("#FFFFFF", 0.55, 0.65), new Point(5.8, 12.3), new Point(20.5, 7.3));

        dc.DrawGeometry(Linear("#EAF0F6", "#8794A4", 0, 0, 1, 1), Pen("#243140", 0.7),
            Geometry.Parse("M 22.5,18 L 27.5,20.5 L 25,31 L 20.5,28.5 Z"));

        dc.DrawGeometry(Linear("#F3F7FB", "#7B8798", 0, 0, 1, 1), Pen("#243140", 0.8),
            Geometry.Parse("M 25,29.5 C 27,25.5 33,25 37,28.5 C 38.5,32.5 35,36 30,36.5 C 27.5,35 25.5,33 25,29.5 Z"));
        dc.DrawEllipse(Brush("#41CFE4"), Pen("#1F4751", 0.45), new Point(35.5, 30), 1.7, 1.1);

        dc.DrawLine(Pen("#CBD5DF", 1.85), new Point(28, 29), new Point(35.5, 23.5));
        dc.DrawLine(Pen("#243140", 0.55), new Point(28, 29), new Point(35.5, 23.5));
        dc.DrawLine(Pen("#CBD5DF", 1.85), new Point(27.5, 33.5), new Point(35.5, 38.5));
        dc.DrawLine(Pen("#243140", 0.55), new Point(27.5, 33.5), new Point(35.5, 38.5));

        DrawNacelle(dc, new Point(37, 22.5), -17);
        DrawNacelle(dc, new Point(37, 39.5), 17);
    }

    private static void DrawNacelle(DrawingContext dc, Point center, double angle)
    {
        dc.PushTransform(new RotateTransform(angle, center.X, center.Y));
        var body = new Rect(center.X - 9.0, center.Y - 2.55, 18.0, 5.1);
        dc.DrawRoundedRectangle(Brush("#000000", 0.14), null, new Rect(body.X + 0.7, body.Y + 0.7, body.Width, body.Height), 2.7, 2.7);
        dc.DrawRoundedRectangle(Linear("#F4F7FA", "#7B8798", 0, 0, 1, 1), Pen("#23303F", 0.85), body, 2.55, 2.55);
        dc.DrawRectangle(Linear("#6B83FF", "#334BE0", 0, 0, 1, 1), null, new Rect(center.X - 7.0, center.Y - 2.05, 4.5, 4.1));
        dc.DrawRectangle(Brush("#D7E0EA", 0.78), null, new Rect(center.X - 2.5, center.Y - 2.0, 8.8, 4.0));
        dc.DrawLine(Pen("#FFFFFF", 0.52, 0.85), new Point(center.X - 4.0, center.Y - 1.55), new Point(center.X + 6.2, center.Y - 1.55));
        dc.DrawLine(Pen("#657284", 0.45, 0.65), new Point(center.X - 1.0, center.Y + 1.75), new Point(center.X + 5.2, center.Y + 1.75));
        dc.DrawEllipse(Brush("#F25AAE", 0.28), null, new Point(center.X + 8.3, center.Y), 3.2, 3.2);
        dc.DrawEllipse(Linear("#FF79B8", "#D43B86", 0, 0, 1, 1), Pen("#7A1F4B", 0.65), new Point(center.X + 8.0, center.Y), 2.25, 2.25);
        dc.DrawEllipse(Brush("#FFFFFF", 0.55), null, new Point(center.X + 7.3, center.Y - 0.7), 0.55, 0.55);
        dc.Pop();
    }

    private static void DrawDarkOneDagger(DrawingContext dc)
    {
        dc.DrawGeometry(Brush("#000000", 0.14), null,
            Geometry.Parse("M 20,2 L 28,2 L 29,16 C 35,16 38,18 37,20 L 29,22 C 30,30 28,40 24,48 C 20,40 18,30 19,22 L 11,20 C 10,18 13,16 19,16 Z"));

        dc.DrawLine(Pen("#050506", 5.8), new Point(24, 4), new Point(24, 16));
        dc.DrawLine(Pen("#1A1A1E", 4.0), new Point(24, 4.5), new Point(24, 15.5));
        for (var y = 6.0; y < 15.0; y += 1.8)
        {
            dc.DrawLine(Pen("#44444A", 0.8, 0.75), new Point(22.0, y), new Point(26.0, y + 1.0));
        }

        dc.DrawEllipse(Linear("#45474D", "#111114", 0, 0, 1, 1), Pen("#050506", 1.05), new Point(24, 3.5), 4.2, 2.5);
        dc.DrawEllipse(Linear("#E44558", "#8E1020", 0, 0, 1, 1), Pen("#050506", 0.8), new Point(24, 2.8), 2.8, 1.8);
        dc.DrawEllipse(Brush("#FFA0AB", 0.7), null, new Point(23.3, 2.2), 0.7, 0.4);

        dc.DrawEllipse(Linear("#F0F2F5", "#8A8E96", 0, 0, 1, 1), Pen("#080809", 1.1), new Point(24, 18.5), 11.5, 3.2);
        dc.DrawEllipse(Brush("#070708"), null, new Point(24, 17.8), 3.2, 1.3);
        dc.DrawGeometry(Linear("#D0D4DA", "#585C64", 0, 0, 1, 1), Pen("#080809", 0.85),
            Geometry.Parse("M 20.5,19.5 C 21,16 27,16 27.5,19.5 C 27,22 21,22 20.5,19.5 Z"));
        dc.DrawEllipse(Brush("#050506"), null, new Point(22.5, 19), 0.85, 0.75);
        dc.DrawEllipse(Brush("#050506"), null, new Point(25.5, 19), 0.85, 0.75);
        dc.DrawGeometry(Brush("#050506"), null, Geometry.Parse("M 23.5,20.2 L 24.5,20.2 L 24,21.2 Z"));
        dc.DrawGeometry(Brush("#050506"), null,
            Geometry.Parse("M 17.5,20 L 20.5,22.5 L 16.5,24 Z M 30.5,20 L 27.5,22.5 L 31.5,24 Z"));

        var blade = Geometry.Parse(
            "M 20.5,21.5 " +
            "C 19,27 21,30 19.5,34 " +
            "C 18.5,37 21,41 24,47 " +
            "C 27,41 29.5,37 28.5,34 " +
            "C 27,30 29,27 27.5,21.5 " +
            "C 25.5,22.5 22.5,22.5 20.5,21.5 Z");
        dc.DrawGeometry(Linear("#FFFFFF", "#7A8290", 0, 0, 1, 1), Pen("#101012", 1.3), blade);

        dc.DrawGeometry(Brush("#D8DCE2", 0.8), null,
            Geometry.Parse("M 24,22.5 C 23,28.5 23,37.5 24,45 C 25,37.5 25,28.5 24,22.5 Z"));
        dc.DrawGeometry(Brush("#A0A4AC", 0.5), null,
            Geometry.Parse("M 26.5,22.5 C 27.5,28 27,34 28,38 C 27.5,40 26.5,42 25,44.5 C 25.5,37 25.5,29 24.5,22.5 Z"));
        dc.DrawLine(Pen("#202124", 0.7), new Point(24, 22), new Point(24, 45.5));

        dc.PushTransform(new RotateTransform(70, 23, 31));
        DrawFittedText(dc, "RUMPLE", new Rect(20, 29.5, 9, 2), "#1A1A1D", 2.0, FontWeights.Bold);
        dc.Pop();

        dc.DrawLine(Pen("#171719", 0.6), new Point(21, 27), new Point(26, 30));
        dc.DrawLine(Pen("#171719", 0.6), new Point(21.5, 33), new Point(26.5, 36));
        dc.DrawGeometry(null, Pen("#171719", 0.55), Geometry.Parse("M 21,38.5 C 22.5,37.5 25.5,39 27,38"));
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
