using System.Diagnostics;
using System.Threading;

namespace CursorMagic.Services;

public static class AgentLauncher
{
    public const string Argument = "--agent";
    public const string MutexName = "Local\\CursorMagic.Agent";

    public static bool IsAgentRunning()
    {
        using var mutex = new Mutex(false, MutexName, out var createdNew);
        return !createdNew;
    }

    public static void StartIfNeeded()
    {
        if (IsAgentRunning() || string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = Environment.ProcessPath,
            Arguments = Argument,
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }
}
