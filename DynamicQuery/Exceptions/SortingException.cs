namespace System.Linq.DynamicQuery;

public class SortingException : DynamicQueryException {
    public SortingException() : base() {
    }

    public SortingException(string message, Exception? innerException = null)
        : base(message, innerException) {
    }
}