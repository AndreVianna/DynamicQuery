namespace System.Linq.DynamicQuery;

internal class ValueToken : Token {
    public ValueToken(int position, string text, Type type, object? value) : base(position, text) {
        Type = type;
        Value = value;
    }

    public Type Type { get; }
    public object? Value { get; }
}