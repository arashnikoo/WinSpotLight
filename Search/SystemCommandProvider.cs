using System.Diagnostics;
using Spotlight.Interop;
using Spotlight.Models;

namespace Spotlight.Search;

public sealed class SystemCommandProvider : ISearchProvider
{
    private const int KindBonus = 10000; // commands rank above files

    private sealed record Command(string Title, string Glyph, string[] Aliases, Action Action);

    private static readonly Command[] Commands =
    {
        new("Lock Screen", "\uE72E", new[] { "lock", "lock screen" }, () => NativeMethods.LockWorkStation()),
        new("Sleep", "\uE7E8", new[] { "sleep" }, () => NativeMethods.SetSuspendState(false, false, false)),
        new("Hibernate", "\uE7E8", new[] { "hibernate" }, () => NativeMethods.SetSuspendState(true, false, false)),
        new("Restart", "\uE777", new[] { "restart", "reboot" }, () => Run("shutdown", "/r /t 0")),
        new("Shut Down", "\uE7E8", new[] { "shut down", "shutdown", "power off" }, () => Run("shutdown", "/s /t 0")),
        new("Sign Out", "\uE77B", new[] { "sign out", "log out", "logout" }, () => Run("shutdown", "/l")),
        new("Empty Recycle Bin", "\uE74D", new[] { "empty recycle bin", "empty trash", "recycle bin" }, () => NativeMethods.EmptyRecycleBin()),
        new("Open Settings", "\uE713", new[] { "settings", "open settings" }, () => Open("ms-settings:")),
        new("Calculator", "\uE8EF", new[] { "calc", "calculator" }, () => Run("calc.exe")),
        new("Task Manager", "\uE9D9", new[] { "task manager", "taskmgr" }, () => Run("taskmgr.exe")),
        new("Control Panel", "\uE713", new[] { "control panel" }, () => Run("control.exe")),
        new("Windows Terminal", "\uE756", new[] { "terminal", "wt" }, () => RunFirst("wt.exe", "cmd.exe")),
        new("Command Prompt", "\uE756", new[] { "cmd", "command prompt" }, () => Run("cmd.exe")),
        new("PowerShell", "\uE756", new[] { "powershell", "ps" }, () => Run("powershell.exe")),
        new("Screenshot", "\uE722", new[] { "screenshot", "snip", "capture" }, () => Open("ms-screenclip:")),
    };

    public Task InitializeAsync() => Task.CompletedTask;

    public IEnumerable<SearchResult> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            foreach (var command in Commands.Take(8))
                yield return ToResult(command, KindBonus + 10);
            yield break;
        }

        var scored = new List<(int Score, Command Cmd)>();
        foreach (var command in Commands)
        {
            int best = FuzzyMatcher.Score(query, command.Title);
            foreach (string alias in command.Aliases)
                best = Math.Max(best, FuzzyMatcher.Score(query, alias));
            if (best > 0) scored.Add((best, command));
        }

        foreach (var (score, command) in scored.OrderByDescending(s => s.Score))
            yield return ToResult(command, KindBonus + score);
    }

    private static SearchResult ToResult(Command command, int score)
    {
        return new SearchResult
        {
            Title = command.Title,
            Subtitle = string.Join(", ", command.Aliases),
            Kind = "Command",
            Glyph = command.Glyph,
            Score = score,
            Execute = () =>
            {
                try
                {
                    command.Action();
                }
                catch
                {
                }
            },
        };
    }

    private static void Run(string file, string args)
    {
        Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true });
    }

    private static void Run(string file) => Run(file, "");

    private static void RunFirst(string first, string fallback)
    {
        try
        {
            Process.Start(new ProcessStartInfo(first) { UseShellExecute = true });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo(fallback) { UseShellExecute = true });
            }
            catch
            {
            }
        }
    }

    private static void Open(string uri)
    {
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    }
}
