using System.Globalization;
using System.Windows;
using Spotlight.Calculator;
using Spotlight.Models;

namespace Spotlight.Search;

public sealed class CalculatorSearchProvider : ISearchProvider
{
    private static readonly HashSet<string> TriggerWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "pi", "e", "tau", "π", "τ", "sqrt", "sin", "cos", "tan", "asin", "acos", "atan",
        "sinh", "cosh", "tanh", "ln", "log", "exp", "abs", "floor", "ceil", "round", "pow",
    };

    private static readonly char[] OperatorChars = { '+', '-', '*', '/', '^', '%', '(', '√' };

    public Task InitializeAsync() => Task.CompletedTask;

    public IEnumerable<SearchResult> Search(string query)
    {
        query = query.Trim();
        if (query.StartsWith('='))
        {
            query = query[1..].Trim();
        }
        else if (!ShouldTrigger(query))
        {
            yield break;
        }

        if (string.IsNullOrEmpty(query)) yield break;

        if (!ExpressionParser.TryEvaluate(query, out double result)) yield break;

        string formatted = Format(result);
        yield return new SearchResult
        {
            Title = formatted,
            Subtitle = $"{query} = {formatted}  ·  Enter to copy",
            Kind = "Calculator",
            Glyph = "\uE8EF",
            Score = 1000,
            Execute = () =>
            {
                try
                {
                    System.Windows.Clipboard.SetText(formatted);
                }
                catch
                {
                }
            },
        };
    }

    private static bool ShouldTrigger(string query)
    {
        if (TriggerWords.Contains(query)) return true;
        if (!query.Any(char.IsDigit)) return false;
        if (query.IndexOfAny(OperatorChars) >= 0) return true;
        foreach (string word in TriggerWords)
        {
            if (query.Contains(word, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static string Format(double value)
    {
        double rounded = System.Math.Round(value, 12);
        if (System.Math.Abs(rounded) >= 1e15 || (System.Math.Abs(rounded) > 0 && System.Math.Abs(rounded) < 1e-9))
        {
            return rounded.ToString("G10", CultureInfo.CurrentCulture);
        }
        if (System.Math.Abs(rounded - System.Math.Floor(rounded)) < 1e-12)
        {
            return rounded.ToString("0", CultureInfo.CurrentCulture);
        }
        return rounded.ToString("0.############", CultureInfo.CurrentCulture);
    }
}
