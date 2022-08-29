namespace System.Linq.DynamicQuery;

internal class SymbolToken : Token {
    public SymbolToken(int position, string symbol) : base(position, symbol) {
        Symbol = symbol.ToUpper();
    }

    public string Symbol { get; }
}