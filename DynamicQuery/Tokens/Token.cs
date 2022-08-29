namespace System.Linq.DynamicQuery;

internal class Token {
    public Token(int position, string text) {
        Position = position;
        Text = text;
    }

    public int Position { get; }
    public string Text { get; }
    public Token? Next { get; set; }
    public Token? Previous { get; set; }
}