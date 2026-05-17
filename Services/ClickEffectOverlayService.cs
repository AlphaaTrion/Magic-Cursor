using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using CursorMagic.Models;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Point = System.Windows.Point;

namespace CursorMagic.Services;

public sealed class ClickEffectOverlayService : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmLButtonDown = 0x0201;
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly ForegroundAppService _foreground = new();
    private readonly List<Particle> _particles = [];
    private readonly DispatcherTimer _timer;
    private readonly LowLevelMouseProc _mouseProc;
    private readonly Random _random = new();

    private OverlayWindow? _window;
    private IntPtr _hookId;

    public ClickEffect CurrentEffect { get; set; } = new();
    public AppSettings Settings { get; set; } = new();
    public bool IsTemporarilySuspended { get; private set; }
    public string CurrentGlowCursorPath { get; set; } = "";

    private DispatcherTimer? _glowRevertTimer;

    public ClickEffectOverlayService()
    {
        _mouseProc = HookCallback;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += (_, _) => Tick();
    }

    public void Start()
    {
        if (_window is null)
        {
            _window = new OverlayWindow();
            _window.SourceInitialized += (_, _) => MakeClickThrough(_window);
            _window.Show();
        }

        if (_hookId == IntPtr.Zero)
        {
            _hookId = SetHook(_mouseProc);
        }

        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }

        _window?.Close();
        _window = null;
        _particles.Clear();
    }

    public void PreviewAtScreenPoint(double screenX, double screenY)
    {
        if (_window is null)
        {
            return;
        }

        _window.UpdateBounds();
        var point = ScreenPixelsToOverlayPoint(screenX, screenY);
        SpawnParticles(point.X, point.Y);
    }

    public void Dispose() => Stop();

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == WmLButtonDown)
        {
            var hook = Marshal.PtrToStructure<NativeMethods.MouseHookStruct>(lParam);
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() => TrySpawn(hook.pt.X, hook.pt.Y));
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void TrySpawn(int screenX, int screenY)
    {
        IsTemporarilySuspended = Settings.EffectsPaused
            || _foreground.IsForegroundFullscreen()
            || _foreground.IsBlockedForeground(Settings.BlockedProcessNames);

        if (IsTemporarilySuspended || _window is null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(CurrentGlowCursorPath))
        {
            TriggerGlowSwap();
        }

        _window.UpdateBounds();
        var point = ScreenPixelsToOverlayPoint(screenX, screenY);
        SpawnParticles(point.X, point.Y);
    }

    private void TriggerGlowSwap()
    {
        _glowRevertTimer?.Stop();

        try
        {
            WindowsCursorService.SwapToGlow(CurrentGlowCursorPath);
        }
        catch
        {
            return;
        }

        _glowRevertTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(CurrentEffect.DurationMs > 0 ? CurrentEffect.DurationMs : 400)
        };
        _glowRevertTimer.Tick += (_, _) =>
        {
            _glowRevertTimer.Stop();
            try
            {
                WindowsCursorService.SwapBackFromGlow();
            }
            catch { /* best effort */ }
        };
        _glowRevertTimer.Start();
    }

    private Point ScreenPixelsToOverlayPoint(double screenX, double screenY)
    {
        if (_window is null)
        {
            return new Point(screenX, screenY);
        }

        var hwnd = new WindowInteropHelper(_window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            var clientPoint = new NativeMethods.Point
            {
                X = (int)Math.Round(screenX),
                Y = (int)Math.Round(screenY)
            };

            if (NativeMethods.ScreenToClient(hwnd, ref clientPoint))
            {
                var clientDip = new Point(clientPoint.X, clientPoint.Y);
                var clientSource = PresentationSource.FromVisual(_window);
                if (clientSource?.CompositionTarget is not null)
                {
                    clientDip = clientSource.CompositionTarget.TransformFromDevice.Transform(clientDip);
                }

                return clientDip;
            }
        }

        var screenPoint = new Point(screenX, screenY);
        var source = PresentationSource.FromVisual(_window);
        if (source?.CompositionTarget is not null)
        {
            screenPoint = source.CompositionTarget.TransformFromDevice.Transform(screenPoint);
        }

        return new Point(screenPoint.X - _window.Left, screenPoint.Y - _window.Top);
    }

    private void SpawnParticles(double x, double y)
    {
        if (_window is null)
        {
            return;
        }

        var effect = CurrentEffect;
        var animationScale = Math.Clamp(Settings.AnimationScale <= 0 ? 1 : Settings.AnimationScale, 0.45, 2.0);
        var isTrail = effect.Type.Contains("Trail", StringComparison.OrdinalIgnoreCase);
        var isGlow = effect.Type.Contains("Glow", StringComparison.OrdinalIgnoreCase);
        var isPulse = effect.Type.Contains("Pulse", StringComparison.OrdinalIgnoreCase);
        var isStatic = isGlow || isPulse;

        var spawnX = x;
        var spawnY = y;
        if (isGlow)
        {
            spawnX += 10;
            spawnY += 10;
        }

        for (var i = 0; i < Math.Max(1, effect.ParticleCount); i++)
        {
            double angle;
            if (isTrail)
            {
                angle = Math.PI / 2 + (_random.NextDouble() - 0.5) * 0.6;
            }
            else
            {
                angle = (Math.PI * 2 / effect.ParticleCount) * i + _random.NextDouble() * 0.45;
            }

            var distance = isStatic ? 0 : effect.Radius * animationScale * (0.45 + _random.NextDouble() * 0.55);
            var element = CreateParticleElement(effect, i);
            Canvas.SetLeft(element, spawnX - element.Width / 2);
            Canvas.SetTop(element, spawnY - element.Height / 2);
            _window.Layer.Children.Add(element);
            _particles.Add(new Particle
            {
                Element = element,
                Start = DateTime.UtcNow,
                Duration = TimeSpan.FromMilliseconds(effect.DurationMs + _random.Next(-60, 80)),
                StartX = spawnX,
                StartY = spawnY,
                EndX = spawnX + Math.Cos(angle) * distance,
                EndY = spawnY + Math.Sin(angle) * distance,
                Spin = isStatic ? 0 : _random.NextDouble() * 160 - 80,
                StayInPlace = isStatic
            });
        }
    }

    private FrameworkElement CreateParticleElement(ClickEffect effect, int index)
    {
        var animationScale = Math.Clamp(Settings.AnimationScale <= 0 ? 1 : Settings.AnimationScale, 0.45, 2.0);
        var primary = Brush(effect.PrimaryColor);
        var secondary = Brush(effect.SecondaryColor);
        var brush = index % 2 == 0 ? primary : secondary;

        if (effect.Type.Contains("Glow", StringComparison.OrdinalIgnoreCase))
        {
            var size = (28 + index * 10) * animationScale;
            return new Ellipse
            {
                Width = size * 1.8,
                Height = size,
                Fill = primary,
                Opacity = 0.55,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new TransformGroup
                {
                    Children =
                    {
                        new ScaleTransform(0.4, 0.4),
                        new RotateTransform(45)
                    }
                }
            };
        }

        if (effect.Type.Contains("Slash", StringComparison.OrdinalIgnoreCase))
        {
            return new Path
            {
                Data = Geometry.Parse("M -10,-2 Q 0,-12 10,-2 Q 0,-6 -10,-2 Z"),
                Fill = brush,
                Stroke = secondary,
                StrokeThickness = 0.6 * animationScale,
                Width = 22 * animationScale,
                Height = 14 * animationScale,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new TransformGroup
                {
                    Children =
                    {
                        new ScaleTransform(0.5, 0.5),
                        new RotateTransform()
                    }
                }
            };
        }

        if (effect.Type.Contains("Pulse", StringComparison.OrdinalIgnoreCase))
        {
            var size = (20 + index * 8) * animationScale;
            return new Ellipse
            {
                Width = size,
                Height = size,
                Fill = primary,
                Opacity = 0.65,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(0.3, 0.3)
            };
        }

        if (effect.Type.Contains("Trail", StringComparison.OrdinalIgnoreCase))
        {
            return new Border
            {
                Width = 3 * animationScale,
                Height = 24 * animationScale,
                CornerRadius = new CornerRadius(1.5 * animationScale),
                Background = brush,
                Opacity = 0.85,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new TransformGroup
                {
                    Children =
                    {
                        new ScaleTransform(0.6, 0.6),
                        new RotateTransform()
                    }
                }
            };
        }

        if (effect.Type.Contains("Saber", StringComparison.OrdinalIgnoreCase))
        {
            return new Border
            {
                Width = 22 * animationScale,
                Height = 3.5 * animationScale,
                CornerRadius = new CornerRadius(6 * animationScale),
                Background = brush,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(0.55 * animationScale),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new TransformGroup
                {
                    Children =
                    {
                        new ScaleTransform(0.48, 0.48),
                        new RotateTransform()
                    }
                }
            };
        }

        if (effect.Type.Contains("Blade", StringComparison.OrdinalIgnoreCase))
        {
            return new Path
            {
                Data = Geometry.Parse("M 0,-8 L 2.2,-1.8 L 8,0 L 2.2,1.8 L 0,8 L -2.2,1.8 L -8,0 L -2.2,-1.8 Z"),
                Fill = brush,
                Stroke = Brushes.White,
                StrokeThickness = 0.65 * animationScale,
                Width = 13 * animationScale,
                Height = 13 * animationScale,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new TransformGroup
                {
                    Children =
                    {
                        new ScaleTransform(0.5, 0.5),
                        new RotateTransform()
                    }
                }
            };
        }

        if (effect.Type.Contains("Energon", StringComparison.OrdinalIgnoreCase))
        {
            return new Path
            {
                Data = Geometry.Parse("M 0,-7 L 6,-3 L 6,3 L 0,7 L -6,3 L -6,-3 Z"),
                Fill = brush,
                Stroke = Brushes.White,
                StrokeThickness = 0.55 * animationScale,
                Width = 13 * animationScale,
                Height = 13 * animationScale,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new TransformGroup
                {
                    Children =
                    {
                        new ScaleTransform(0.5, 0.5),
                        new RotateTransform()
                    }
                }
            };
        }

        if (effect.Type.Contains("Warp", StringComparison.OrdinalIgnoreCase))
        {
            return new Border
            {
                Width = 18 * animationScale,
                Height = 2.4 * animationScale,
                CornerRadius = new CornerRadius(5 * animationScale),
                Background = brush,
                Opacity = 0.92,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new TransformGroup
                {
                    Children =
                    {
                        new ScaleTransform(0.55, 0.55),
                        new RotateTransform()
                    }
                }
            };
        }

        if (effect.Type.Contains("Cursed", StringComparison.OrdinalIgnoreCase))
        {
            return new Path
            {
                Data = Geometry.Parse("M 0,-7 L 5,0 L 0,7 L -5,0 Z M 0,-3 L 2,0 L 0,3 L -2,0 Z"),
                Fill = brush,
                Stroke = secondary,
                StrokeThickness = 0.75 * animationScale,
                Width = 12 * animationScale,
                Height = 12 * animationScale,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new TransformGroup
                {
                    Children =
                    {
                        new ScaleTransform(0.52, 0.52),
                        new RotateTransform()
                    }
                }
            };
        }

        if (effect.Type.Contains("Heart", StringComparison.OrdinalIgnoreCase)
            || effect.Type.Contains("Wand", StringComparison.OrdinalIgnoreCase) && index % 3 == 0)
        {
            return new Path
            {
                Data = Geometry.Parse("M 0,-4 C -4,-9 -12,-3 -9,4 C -7,8 -2,11 0,14 C 2,11 7,8 9,4 C 12,-3 4,-9 0,-4 Z"),
                Fill = brush,
                Stroke = Brushes.White,
                StrokeThickness = 0.7 * animationScale,
                Width = 16 * animationScale,
                Height = 16 * animationScale,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new TransformGroup
                {
                    Children =
                    {
                        new ScaleTransform(0.58, 0.58),
                        new RotateTransform()
                    }
                }
            };
        }

        if (effect.Type.Contains("Ring", StringComparison.OrdinalIgnoreCase))
        {
            return new Ellipse
            {
                Width = 12,
                Height = 12,
                Stroke = brush,
                StrokeThickness = 1.8 * animationScale,
                Fill = Brushes.Transparent,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(0.4, 0.4)
            };
        }

        return new Path
        {
            Data = Geometry.Parse("M 0,-7 L 2,-2 L 7,0 L 2,2 L 0,7 L -2,2 L -7,0 L -2,-2 Z"),
            Fill = brush,
            Stroke = Brushes.White,
            StrokeThickness = 0.7 * animationScale,
            Width = 14 * animationScale,
            Height = 14 * animationScale,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new TransformGroup
            {
                Children =
                {
                    new ScaleTransform(0.52, 0.52),
                    new RotateTransform()
                }
            }
        };
    }

    private void Tick()
    {
        if (_window is null)
        {
            return;
        }

        _window.UpdateBounds();
        var now = DateTime.UtcNow;
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var particle = _particles[i];
            var progress = Math.Clamp((now - particle.Start).TotalMilliseconds / particle.Duration.TotalMilliseconds, 0, 1);
            var eased = 1 - Math.Pow(1 - progress, 3);
            var x = Lerp(particle.StartX, particle.EndX, eased);
            var y = Lerp(particle.StartY, particle.EndY, eased);
            if (!particle.StayInPlace)
            {
                y -= Math.Sin(progress * Math.PI) * 8;
            }
            Canvas.SetLeft(particle.Element, x - particle.Element.Width / 2);
            Canvas.SetTop(particle.Element, y - particle.Element.Height / 2);
            particle.Element.Opacity = Math.Clamp((1 - progress) * Math.Clamp(Settings.AnimationBrightness <= 0 ? 1 : Settings.AnimationBrightness, 0.35, 1.8), 0, 1);
            ApplyTransform(particle.Element, progress, particle.Spin);

            if (progress >= 1)
            {
                _window.Layer.Children.Remove(particle.Element);
                _particles.RemoveAt(i);
            }
        }
    }

    private static void ApplyTransform(FrameworkElement element, double progress, double spin)
    {
        var scale = 0.55 + Math.Sin(progress * Math.PI) * 0.45;
        switch (element.RenderTransform)
        {
            case TransformGroup group:
                if (group.Children[0] is ScaleTransform st)
                {
                    st.ScaleX = scale;
                    st.ScaleY = scale;
                }

                if (group.Children.Count > 1 && group.Children[1] is RotateTransform rt)
                {
                    if (Math.Abs(spin) > 0.01)
                    {
                        rt.Angle = spin * progress;
                    }
                }
                break;
            case ScaleTransform scaleTransform:
                scaleTransform.ScaleX = scale;
                scaleTransform.ScaleY = scale;
                break;
        }
    }

    private static double Lerp(double start, double end, double amount) => start + (end - start) * amount;

    private static IntPtr SetHook(LowLevelMouseProc proc)
    {
        using var currentProcess = Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule;
        var moduleHandle = currentModule is null
            ? IntPtr.Zero
            : NativeMethods.GetModuleHandle(currentModule.ModuleName);
        return NativeMethods.SetWindowsHookEx(WhMouseLl, proc, moduleHandle, 0);
    }

    private static void MakeClickThrough(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var styles = NativeMethods.GetWindowLong(hwnd, GwlExStyle);
        NativeMethods.SetWindowLong(hwnd, GwlExStyle, styles | WsExTransparent | WsExToolWindow | WsExNoActivate);
    }

    private SolidColorBrush Brush(string color)
    {
        var parsed = (Color)ColorConverter.ConvertFromString(color);
        var brightness = Math.Clamp(Settings.AnimationBrightness <= 0 ? 1 : Settings.AnimationBrightness, 0.35, 1.8);
        var adjusted = Color.FromArgb(
            parsed.A,
            ScaleChannel(parsed.R, brightness),
            ScaleChannel(parsed.G, brightness),
            ScaleChannel(parsed.B, brightness));
        var brush = new SolidColorBrush(adjusted);
        brush.Freeze();
        return brush;
    }

    private static byte ScaleChannel(byte value, double brightness)
    {
        if (brightness <= 1)
        {
            return (byte)Math.Clamp(Math.Round(value * brightness), 0, 255);
        }

        return (byte)Math.Clamp(Math.Round(value + (255 - value) * (brightness - 1) / 0.8), 0, 255);
    }

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    private sealed class Particle
    {
        public required FrameworkElement Element { get; init; }
        public DateTime Start { get; init; }
        public TimeSpan Duration { get; init; }
        public double StartX { get; init; }
        public double StartY { get; init; }
        public double EndX { get; init; }
        public double EndY { get; init; }
        public double Spin { get; init; }
        public bool StayInPlace { get; init; }
    }

    private sealed class OverlayWindow : Window
    {
        private const double EdgeGutter = 8;

        public Canvas Layer { get; } = new();

        public OverlayWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            Focusable = false;
            IsHitTestVisible = false;
            Content = Layer;
            UpdateBounds();
        }

        public void UpdateBounds()
        {
            // Leave the screen edge uncovered so Windows auto-hide taskbars can reveal on hover.
            var left = SystemParameters.VirtualScreenLeft + EdgeGutter;
            var top = SystemParameters.VirtualScreenTop + EdgeGutter;
            var width = Math.Max(1, SystemParameters.VirtualScreenWidth - EdgeGutter * 2);
            var height = Math.Max(1, SystemParameters.VirtualScreenHeight - EdgeGutter * 2);
            if (Math.Abs(Left - left) > 0.1) Left = left;
            if (Math.Abs(Top - top) > 0.1) Top = top;
            if (Math.Abs(Width - width) > 0.1) Width = width;
            if (Math.Abs(Height - height) > 0.1) Height = height;
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            var helper = new WindowInteropHelper(this);
            NativeMethods.SetWindowPos(helper.Handle, IntPtr.Zero, 0, 0, 0, 0,
                NativeMethods.SwpNoActivate | NativeMethods.SwpNoMove | NativeMethods.SwpNoSize);
        }
    }

    private static class NativeMethods
    {
        public const uint SwpNoSize = 0x0001;
        public const uint SwpNoMove = 0x0002;
        public const uint SwpNoActivate = 0x0010;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ScreenToClient(IntPtr hWnd, ref Point lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MouseHookStruct
        {
            public Point pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
    }
}
