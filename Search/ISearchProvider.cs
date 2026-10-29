using WinSpotLight.Models;

namespace WinSpotLight.Search;

public interface ISearchProvider
{
    Task InitializeAsync();
    IEnumerable<SearchResult> Search(string query);
}
