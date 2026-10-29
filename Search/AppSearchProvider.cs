using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Spotlight.Interop;
using Spotlight.Models;

namespace Spotlight.Search;

public sealed class AppSearchProvider : ISearchProvider
{
    private const int MaxResults = 8;
    private const int AppPriority = 10000; // apps/shortcuts rank above files

    private readonly List<AppEntry> _apps = new();
    private volatile bool _initialized;

    private static readonly ImageSource NullIcon = CreateNullIcon();

    private sealed class AppEntry
    {
        public required string Name { get; init; }
        public required string LaunchPath { get; init; }
        public string? IconPath { get; init; }
        public ImageSource? Icon { get; set; }
    }

    public Task InitializeAsync()
    {
        if (_initialized) return Task.CompletedTask;
        return Task.Run(Load);
    }

    private void Load()
    {
        var entries = new List<AppEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string dir in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                     Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                 })
        {
            foreach (string file in EnumerateLinks(dir))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(name) || !seen.Add(name)) continue;
                entries.Add(new AppEntry { Name = name, LaunchPath = file, IconPath = file });
            }
        }

        foreach (var entry in ReadRegistryApps())
        {
            if (string.IsNullOrEmpty(entry.LaunchPath) || !seen.Add(entry.Name)) continue;
            entries.Add(entry);
        }

        entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        lock (_apps)
        {
            _apps.AddRange(entries);
        }
        _initialized = true;
    }

    private static IEnumerable<string> EnumerateLinks(string dir)
    {
        try
        {
            return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .Where(f =>
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext == ".lnk" || ext == ".appref-ms" || ext == ".url" || ext == ".exe";
                })
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<AppEntry> ReadRegistryApps()
    {
        var results = new List<AppEntry>();

        var keys = new[]
        {
            (Root: RegistryHive.LocalMachine, Path: @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", View: RegistryView.Registry64),
            (Root: RegistryHive.LocalMachine, Path: @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", View: RegistryView.Registry32),
            (Root: RegistryHive.LocalMachine, Path: @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", View: RegistryView.Registry64),
            (Root: RegistryHive.CurrentUser, Path: @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", View: RegistryView.Registry64),
        };

        foreach (var (root, path, view) in keys)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(root, view);
                using var key = baseKey.OpenSubKey(path);
                if (key == null) continue;

                foreach (string subName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var sub = key.OpenSubKey(subName);
                        if (sub == null) continue;

                        string? name = sub.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        name = name.Trim();
                        if (name.StartsWith("KB", StringComparison.OrdinalIgnoreCase) || name.Contains("Update for")) continue;

                        object? systemComponent = sub.GetValue("SystemComponent");
                        if (systemComponent is int sc && sc != 0) continue;

                        string? icon = sub.GetValue("DisplayIcon") as string;
                        string? exe = StripIconIndex(icon);
                        if (exe == null || !exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(exe)) continue;

                        results.Add(new AppEntry { Name = name, LaunchPath = exe, IconPath = exe });
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        return results;
    }

    private static string? StripIconIndex(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon)) return null;
        icon = icon.Trim('"');
        int comma = icon.LastIndexOf(',');
        if (comma > 0) icon = icon[..comma];
        return string.IsNullOrWhiteSpace(icon) ? null : icon;
    }

    public IEnumerable<SearchResult> Search(string query)
    {
        if (!_initialized) yield break;

        if (string.IsNullOrWhiteSpace(query))
        {
            List<AppEntry> defaults;
            lock (_apps)
            {
                defaults = _apps.Take(6).ToList();
            }
            foreach (var app in defaults)
                yield return ToResult(app, 150);
            yield break;
        }

        var scored = new List<(int Score, AppEntry Entry)>();
        lock (_apps)
        {
            foreach (var app in _apps)
            {
                int score = FuzzyMatcher.Score(query, app.Name);
                if (score > 0) scored.Add((score, app));
            }
        }

        foreach (var (score, app) in scored
                     .OrderByDescending(s => s.Score)
                     .Take(MaxResults))
        {
            yield return ToResult(app, AppPriority + score);
        }
    }

    private static SearchResult ToResult(AppEntry app, int score)
    {
        ImageSource? icon = GetIcon(app);
        return new SearchResult
        {
            Title = app.Name,
            Subtitle = app.LaunchPath,
            Kind = "App",
            Icon = ReferenceEquals(icon, NullIcon) ? null : icon,
            Score = score,
            Execute = () =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(app.LaunchPath) { UseShellExecute = true });
                }
                catch
                {
                }
            },
        };
    }

    private static ImageSource? GetIcon(AppEntry entry)
    {
        if (entry.Icon != null) return entry.Icon;
        ImageSource? icon = null;
        if (!string.IsNullOrEmpty(entry.IconPath))
            icon = IconExtractor.GetFileIcon(entry.IconPath);
        entry.Icon = icon ?? NullIcon;
        return icon;
    }

    private static ImageSource CreateNullIcon()
    {
        var bmp = new WriteableBitmap(1, 1, 96, 96, PixelFormats.Bgra32, null);
        bmp.Freeze();
        return bmp;
    }
}
