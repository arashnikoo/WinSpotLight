using System.Globalization;

namespace Spotlight.Calculator;

public static class ExpressionParser
{
    public static bool TryEvaluate(string input, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;
        try
        {
            var parser = new Parser(input);
            double value = parser.ParseExpression();
            parser.SkipWhitespace();
            if (!parser.AtEnd) return false;
            if (double.IsNaN(value) || double.IsInfinity(value)) return false;
            result = value;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class Parser
    {
        private readonly string _s;
        private int _i;

        public Parser(string s) => _s = s;

        public bool AtEnd => _i >= _s.Length;

        private char Peek => _s[_i];

        public void SkipWhitespace()
        {
            while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
        }

        private bool Consume(char c)
        {
            if (_i < _s.Length && _s[_i] == c)
            {
                _i++;
                return true;
            }
            return false;
        }

        public double ParseExpression()
        {
            double value = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (Consume('+')) value += ParseTerm();
                else if (Consume('-')) value -= ParseTerm();
                else return value;
            }
        }

        private double ParseTerm()
        {
            double value = ParseFactor();
            while (true)
            {
                SkipWhitespace();
                if (Consume('*')) value *= ParseFactor();
                else if (Consume('/')) value /= ParseFactor();
                else if (Consume('%') && !AtEnd) value %= ParseFactor();
                else return value;
            }
        }

        private double ParseFactor()
        {
            double value = ParseUnary();
            while (true)
            {
                SkipWhitespace();
                if (Consume('^'))
                {
                    double exponent = ParseFactor();
                    value = System.Math.Pow(value, exponent);
                }
                else return value;
            }
        }

        private double ParseUnary()
        {
            SkipWhitespace();
            if (Consume('-')) return -ParseUnary();
            if (Consume('+')) return ParseUnary();
            if (Consume('√')) return System.Math.Sqrt(ParseUnary());
            return ParsePrimary();
        }

        private double ParsePrimary()
        {
            SkipWhitespace();
            if (AtEnd) throw new FormatException("Unexpected end of expression.");

            if (Consume('('))
            {
                double value = ParseExpression();
                SkipWhitespace();
                if (!Consume(')')) throw new FormatException("Expected ')'.");
                return value;
            }

            char c = Peek;
            if (char.IsDigit(c) || c == '.')
            {
                double value = ParseNumber();
                SkipWhitespace();
                if (!AtEnd && Peek == '%')
                {
                    int save = _i;
                    _i++;
                    SkipWhitespace();
                    if (AtEnd || !(char.IsDigit(Peek) || Peek == '(' || Peek == '.'))
                    {
                        value /= 100.0;
                    }
                    else
                    {
                        _i = save;
                    }
                }
                return value;
            }

            if (char.IsLetter(c))
            {
                string word = ReadWord();
                if (TryConstant(word, out double constant)) return constant;

                if (word.Equals("pow", StringComparison.OrdinalIgnoreCase))
                {
                    SkipWhitespace();
                    if (!Consume('(')) throw new FormatException("Expected '('.");
                    double a = ParseExpression();
                    SkipWhitespace();
                    if (!Consume(',')) throw new FormatException("Expected ','.");
                    double b = ParseExpression();
                    SkipWhitespace();
                    if (!Consume(')')) throw new FormatException("Expected ')'.");
                    return System.Math.Pow(a, b);
                }

                SkipWhitespace();
                if (!Consume('(')) throw new FormatException("Expected '('.");
                double arg = ParseExpression();
                SkipWhitespace();
                if (!Consume(')')) throw new FormatException("Expected ')'.");
                return ApplyFunction(word, arg);
            }

            throw new FormatException($"Unexpected character '{c}'.");
        }

        private double ParseNumber()
        {
            int start = _i;
            while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
            if (_i < _s.Length && _s[_i] == '.')
            {
                _i++;
                while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
            }
            if (_i < _s.Length && (_s[_i] == 'e' || _s[_i] == 'E'))
            {
                int save = _i;
                _i++;
                if (_i < _s.Length && (_s[_i] == '+' || _s[_i] == '-')) _i++;
                if (_i < _s.Length && char.IsDigit(_s[_i]))
                {
                    while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
                }
                else
                {
                    _i = save;
                }
            }
            string text = _s[start.._i];
            return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private string ReadWord()
        {
            int start = _i;
            while (_i < _s.Length && char.IsLetter(_s[_i])) _i++;
            return _s[start.._i];
        }

        private static bool TryConstant(string word, out double value)
        {
            switch (word.ToLowerInvariant())
            {
                case "pi":
                case "π":
                    value = System.Math.PI;
                    return true;
                case "tau":
                case "τ":
                    value = System.Math.Tau;
                    return true;
                case "e":
                    value = System.Math.E;
                    return true;
                default:
                    value = 0;
                    return false;
            }
        }

        private static double ApplyFunction(string name, double arg)
        {
            switch (name.ToLowerInvariant())
            {
                case "sqrt": return System.Math.Sqrt(arg);
                case "sin": return System.Math.Sin(arg);
                case "cos": return System.Math.Cos(arg);
                case "tan": return System.Math.Tan(arg);
                case "asin": return System.Math.Asin(arg);
                case "acos": return System.Math.Acos(arg);
                case "atan": return System.Math.Atan(arg);
                case "sinh": return System.Math.Sinh(arg);
                case "cosh": return System.Math.Cosh(arg);
                case "tanh": return System.Math.Tanh(arg);
                case "ln": return System.Math.Log(arg);
                case "log":
                case "log10": return System.Math.Log10(arg);
                case "exp": return System.Math.Exp(arg);
                case "abs": return System.Math.Abs(arg);
                case "floor": return System.Math.Floor(arg);
                case "ceil": return System.Math.Ceiling(arg);
                case "round": return System.Math.Round(arg);
                default: throw new FormatException($"Unknown function '{name}'.");
            }
        }
    }
}
