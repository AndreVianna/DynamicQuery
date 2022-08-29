namespace System.Linq.DynamicQuery;

[SuppressMessage("Roslynator", "RCS1194:Implement exception constructors.", Justification = "Unnecessary")]
public class DynamicQueryException : Exception {
    public DynamicQueryException() {
    }

    public DynamicQueryException(string message, Exception? innerException = null)
        : base(message, innerException) {
    }
}