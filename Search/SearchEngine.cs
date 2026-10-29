using WinSpotLight.Models;

namespace WinSpotLight.Search;

public sealed class SearchEngine
{
    private readonly List<ISearchProvider> _providers = new();
    private CancellationTokenSource? _cts;
    private Task? _initTask;

    public void AddProvider(ISearchProvider provider) => _providers.Add(provider);

    public Task InitializeAsync()
    {
        if (_initTask != null) return _initTask;
        _initTask = Task.Run(async () =>
        {
            foreach (var provider in _providers)
            {
                try
                {
                    await provider.InitializeAsync();
                }
                catch
                {
                }
            }
        });
        return _initTask;
    }

    public async Task<List<SearchResult>> SearchAsync(string query)
    {
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();

        return await Task.Run(() =>
        {
            var results = new List<SearchResult>(64);
            foreach (var provider in _providers)
            {
                if (cts.Token.IsCancellationRequested) break;
                try
                {
                    foreach (var result in provider.Search(query))
                    {
                        results.Add(result);
                        if (results.Count >= 500) break;
                    }
                }
                catch
                {
                }
            }

            return results
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.Title, StringComparer.CurrentCultureIgnoreCase)
                .Take(14)
                .ToList();
        }, cts.Token);
    }
}
