using System.IO;
using System.Windows;
using CursorMagic.Services;

namespace CursorMagic;

public partial class App : System.Windows.Application
{
    private AgentRuntime? _agentRuntime;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, args) =>
        {
            Log($"Unhandled UI exception: {args.Exception}");
            args.Handled = true;
        };

        try
        {
            AppPaths.Ensure();
            Log("App startup");
            if (e.Args.Any(arg => string.Equals(arg, AgentLauncher.Argument, StringComparison.OrdinalIgnoreCase)))
            {
                _agentRuntime = new AgentRuntime(out var alreadyRunning);
                if (alreadyRunning)
                {
                    Log("Agent already running");
                    Shutdown();
                    return;
                }

                _agentRuntime.Start();
                return;
            }

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

    protected override void OnExit(ExitEventArgs e)
    {
        _agentRuntime?.Dispose();
        base.OnExit(e);
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
