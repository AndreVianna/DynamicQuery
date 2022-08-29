namespace System.Linq.DynamicQuery;

public class FilterException : DynamicQueryException {
    public FilterException() {
    }

    public FilterException(string message, Exception? innerException = null)
        : this(new(1, string.Empty), message, innerException) {
    }

    internal FilterException(Token token, string? message = null, Exception? innerException = null)
        : base($"Invalid syntax near '{token.Text}' at position {token.Position}.{(string.IsNullOrWhiteSpace(message) ? null : $" {message}")}", innerException) {
        Position = token.Position;
        Text = token.Text;
    }

    internal FilterException(Token token, string message)
        : this(token, message, null) {
    }

    public int Position { get; } = 1;
    public string Text { get; } = string.Empty;
}