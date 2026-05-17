using System.IO;
using System.Windows;
using CursorMagic.Services;

namespace CursorMagic;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            AppPaths.Ensure();
            Log("App startup");
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
            window.Activate();
            Log("Main window shown");
        }
        catch (Exception ex)
        {
            Log($"Startup crash: {ex}");
            System.Windows.MessageBox.Show(ex.ToString(), "Cursor Magic failed to open", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    public static void Log(string message)
    {
        try
        {
            AppPaths.Ensure();
            File.AppendAllText(
                Path.Combine(AppPaths.Root, "debug.log"),
                $"{DateTime.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never block the app from opening.
        }
    }
}
