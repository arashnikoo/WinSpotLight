namespace WinSpotLight.Search;

public static class FuzzyMatcher
{
    public static int Score(string query, string text)
    {
        if (string.IsNullOrWhiteSpace(query)) return 1;
        if (string.IsNullOrWhiteSpace(text)) return 0;

        string q = query.Trim().ToLowerInvariant();
        string t = text.Trim().ToLowerInvariant();
        if (q.Length == 0) return 1;

        if (t == q) return 1000;

        if (t.StartsWith(q, StringComparison.Ordinal))
            return 900 - Math.Min(t.Length - q.Length, 100);

        int index = t.IndexOf(q, StringComparison.Ordinal);
        if (index >= 0)
        {
            int wordBonus = IsWordStart(t, index) ? 100 : 0;
            return 600 + wordBonus - index - Math.Min(t.Length - q.Length, 50);
        }

        return SubsequenceScore(q, t);
    }

    private static int SubsequenceScore(string q, string t)
    {
        int pos = 0;
        int gaps = 0;
        int lastMatch = -1;

        foreach (char c in q)
        {
            int found = t.IndexOf(c, pos);
            if (found < 0) return 0;
            if (lastMatch >= 0) gaps += found - lastMatch - 1;
            lastMatch = found;
            pos = found + 1;
        }

        int boundaryBonus = 0;
        pos = 0;
        foreach (char c in q)
        {
            int found = t.IndexOf(c, pos);
            if (IsWordStart(t, found)) boundaryBonus += 30;
            pos = found + 1;
        }

        return 300 + boundaryBonus - gaps * 5 - Math.Min(t.Length - q.Length, 60);
    }

    private static bool IsWordStart(string s, int index)
    {
        if (index == 0) return true;
        char prev = s[index - 1];
        return prev is ' ' or '.' or '_' or '-' or '/' or '\\' or '(' or '[';
    }
}
