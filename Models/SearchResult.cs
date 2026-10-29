using System.Windows.Media;

namespace WinSpotLight.Models;

public sealed class SearchResult
{
    public required string Title { get; init; }
    public string Subtitle { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public ImageSource? Icon { get; init; }
    public string Glyph { get; init; } = "\uE7C3";
    public int Score { get; set; }
    public required Action Execute { get; init; }

    public bool HasSubtitle => !string.IsNullOrEmpty(Subtitle);
}
