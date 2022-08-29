namespace System.Linq.DynamicQuery;

internal class NamedToken : Token {
    public NamedToken(int position, string name) : base(position, name) {
        Name = name;
    }

    public string Name { get; }
}