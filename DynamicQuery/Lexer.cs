namespace System.Linq.DynamicQuery;

internal static class Lexer {
    private const RegexOptions _regexOptions = RegexOptions.IgnoreCase | RegexOptions.Compiled;
    private static readonly Regex _whiteSpaces = new(@"^\s+", _regexOptions);
    private static readonly Regex _chars = new(@"^('[^\\']'|'\\[\\'trn]')", _regexOptions); // 'a', '1', '!', '\'', '\\', '\t', '\r', '\n'
    private static readonly Regex _strings = new(@"^""[^""]*""", _regexOptions); // "anything"
    private static readonly Regex _integers = new(@"^\d+", _regexOptions); // "123"
    private static readonly Regex _decimals = new(@"^(\d+\.\d*|\.\d+)", _regexOptions); // ".14", "3.14", "13.14", "13."
    private static readonly Regex _symbols = new(@"^(<>|<=|>=|\[|\]|\-|[().,+*/%^=<>])", _regexOptions); // any word
    private static readonly Regex _words = new(@"^\w+", _regexOptions); // any word
    private static readonly Regex _nulls = new("^null$", _regexOptions); // null values
    private static readonly Regex _booleans = new("^(True|False)$", _regexOptions); // true, false values
    private static readonly Regex _reservedWords = new("^(AND|OR|NOT|BETWEEN|IN|IS|CONTAINS|STARTSWITH|ENDSWITH)$", _regexOptions); // any word

    private static readonly Func<int, string, Token> _createChar = (pos, a) => new ValueToken(pos, a, typeof(char), char.Parse(a[1..^1]));
    private static readonly Func<int, string, Token> _createString = (pos, a) => new ValueToken(pos, a, typeof(string), a[1..^1]);
    private static readonly Func<int, string, Token> _createSymbol = (pos, a) => new SymbolToken(pos, a);
    private static readonly Func<int, string, Token> _createDouble = (pos, a) => new ValueToken(pos, a, typeof(double), double.Parse(a));
    private static readonly Func<int, string, Token> _createInteger = (pos, a) => new ValueToken(pos, a, typeof(int), int.Parse(a));
    private static readonly Func<int, string, Token> _createNull = (pos, a) => new ValueToken(pos, a, typeof(object), null);
    private static readonly Func<int, string, Token> _createBoolean = (pos, a) => new ValueToken(pos, a, typeof(bool), bool.Parse(a));

    internal static Token ReadFrom(string input) {
        var index = 0;
        return GetNextToken(input, ref index)!;
    }

    private static Token GetNextToken(string input, ref int index, Token? previous = null) {
        var token = ReadToken(input, ref index);
        token.Previous = previous;
        if (index >= input.Length) return token;
        token.Next = GetNextToken(input, ref index, token);
        return token;
    }

    private static Token ReadToken(string input, ref int index) {
#pragma warning disable IDE0046 // Convert to conditional expression
        if (TryReadToken(input, ref index, out var token)) return token!;
        throw new FilterException(new Token(index + 1, input[index].ToString()));
#pragma warning restore IDE0046 // Convert to conditional expression
    }

    private static bool TryReadToken(string input, ref int index, out Token? token) {
        while (true) {
            if (TrySkipWhiteSpaces(input, ref index)) continue;
            return TryReadChar(input, ref index, out token)
                || TryReadString(input, ref index, out token)
                || TryReadDouble(input, ref index, out token)
                || TryReadInteger(input, ref index, out token)
                || TryReadSymbol(input, ref index, out token)
                || TryReadWord(input, ref index, out token);
        }
    }

    private static bool TrySkipWhiteSpaces(string input, ref int index) {
        var match = _whiteSpaces.Match(input[index..]);
        if (!match.Success) return false;
        index += match.Length;
        return true;
    }

    private static bool TryReadChar(string input, ref int index, out Token? token) =>
        TryAddToken(input[index..], ref index, _chars, _createChar, out token);

    private static bool TryReadString(string input, ref int index, out Token? token) =>
        TryAddToken(input[index..], ref index, _strings, _createString, out token);

    private static bool TryReadSymbol(string input, ref int index, out Token? token) =>
        TryAddToken(input[index..], ref index, _symbols, _createSymbol, out token);

    private static bool TryReadDouble(string input, ref int index, out Token? token) =>
        TryAddToken(input[index..], ref index, _decimals, _createDouble, out token);

    private static bool TryReadInteger(string input, ref int index, out Token? token) =>
        TryAddToken(input[index..], ref index, _integers, _createInteger, out token);

    private static bool TryReadWord(string input, ref int index, out Token? token) {
        token = null!;
        var match = _words.Match(input[index..]);
        if (!match.Success) return false;
        var word = match.Value;
        return TryAddBoolean(word, ref index, out token)
            || TryAddNull(word, ref index, out token)
            || TryAddReservedWord(word, ref index, out token)
            || TryAddName(word, ref index, out token);
    }

    private static bool TryAddNull(string input, ref int index, out Token? token) =>
        TryAddToken(input, ref index, _nulls, _createNull, out token);

    private static bool TryAddBoolean(string input, ref int index, out Token? token) =>
        TryAddToken(input, ref index, _booleans, _createBoolean, out token);

    private static bool TryAddReservedWord(string input, ref int index, out Token? token) =>
        TryAddToken(input, ref index, _reservedWords, _createSymbol, out token);

    private static bool TryAddName(string input, ref int index, out Token token) {
        token = new NamedToken(index + 1, input);
        index += input.Length;
        return true;
    }

    private static bool TryAddToken(string input, ref int index, Regex regex, Func<int, string, Token> createToken, out Token? token) {
        token = null;
        var match = regex.Match(input);
        if (!match.Success) return false;
        token = createToken(index + 1, match.Value);
        index += match.Length;
        return true;
    }
}
