using Spotlight.Models;

namespace Spotlight.Search;

public interface ISearchProvider
{
    Task InitializeAsync();
    IEnumerable<SearchResult> Search(string query);
}
