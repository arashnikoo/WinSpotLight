using System.Diagnostics;
using System.IO;
using WinSpotLight.Interop;
using WinSpotLight.Models;

namespace WinSpotLight.Search;

public sealed class FileSearchProvider : ISearchProvider
{
    private const int MaxEntries = 60000;
    private const int MaxResults = 10;

    private readonly List<FileEntry> _files = new();
    private volatile bool _initialized;
    private CancellationTokenSource? _cts;

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppData", "node_modules", ".git", ".svn", ".hg", "bin", "obj", "venv",
        ".venv", ".cache", ".gradle", ".idea", ".vscode", "$Recycle.Bin", "Windows.old",
        ".nuget", "packages", "dist", "build", "out", "target", "__pycache__",
    };

    private sealed record FileEntry(string Name, string FullPath, bool IsDirectory);

    public Task InitializeAsync()
    {
        if (_initialized) return Task.CompletedTask;
        _cts = new CancellationTokenSource();
        return Task.Run(() => Index(_cts.Token));
    }

    private void Index(CancellationToken token)
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(profile, "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        }
        .Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        var entries = new List<FileEntry>();
        foreach (string root in roots)
        {
            Walk(root, token, entries);
            if (entries.Count >= MaxEntries) break;
        }

        lock (_files)
        {
            _files.AddRange(entries);
        }
        _initialized = true;
    }

    private static void Walk(string dir, CancellationToken token, List<FileEntry> entries)
    {
        if (entries.Count >= MaxEntries || token.IsCancellationRequested) return;

        string[] subDirs;
        string[] files;
        try
        {
            subDirs = Directory.GetDirectories(dir);
            files = Directory.GetFiles(dir);
        }
        catch
        {
            return;
        }

        foreach (string file in files)
        {
            if (entries.Count >= MaxEntries) return;
            try
            {
                var fi = new FileInfo(file);
                if (fi.Attributes.HasFlag(FileAttributes.Hidden) || fi.Attributes.HasFlag(FileAttributes.System)) continue;
                entries.Add(new FileEntry(Path.GetFileName(file), file, false));
            }
            catch
            {
            }
        }

        foreach (string sub in subDirs)
        {
            if (entries.Count >= MaxEntries) return;
            string name = Path.GetFileName(sub);
            if (IgnoredDirectories.Contains(name)) continue;
            try
            {
                var di = new DirectoryInfo(sub);
                if (di.Attributes.HasFlag(FileAttributes.Hidden) || di.Attributes.HasFlag(FileAttributes.System)) continue;
                entries.Add(new FileEntry(name, sub, true));
                if (di.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                Walk(sub, token, entries);
            }
            catch
            {
            }
        }
    }

    public IEnumerable<SearchResult> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || !_initialized) yield break;

        var scored = new List<(int Score, FileEntry Entry)>();
        lock (_files)
        {
            foreach (var entry in _files)
            {
                int score = FuzzyMatcher.Score(query, entry.Name);
                if (score > 0) scored.Add((score, entry));
            }
        }

        foreach (var (score, entry) in scored
                     .OrderByDescending(s => s.Score)
                     .ThenBy(s => s.Entry.Name, StringComparer.OrdinalIgnoreCase)
                     .Take(MaxResults))
        {
            yield return ToResult(entry, score);
        }
    }

    private static SearchResult ToResult(FileEntry entry, int score)
    {
        return new SearchResult
        {
            Title = entry.Name,
            Subtitle = Path.GetDirectoryName(entry.FullPath) ?? string.Empty,
            Kind = entry.IsDirectory ? "Folder" : "File",
            Icon = IconExtractor.GetFileIcon(entry.FullPath),
            Glyph = entry.IsDirectory ? "\uE8B7" : "\uE8A5",
            Score = 100 + score,
            Execute = () =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(entry.FullPath) { UseShellExecute = true });
                }
                catch
                {
                }
            },
        };
    }
}
