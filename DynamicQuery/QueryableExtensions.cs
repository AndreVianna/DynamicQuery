namespace System.Linq.DynamicQuery;

public static class QueryableExtensions {
    public static IQueryable<TInput> FilterBy<TInput>(this IQueryable<TInput> source, string clause) where TInput : class {
        if (string.IsNullOrWhiteSpace(clause)) throw new ArgumentException("Filter clause cannot be null or empty.", nameof(clause));
        var instance = Expression.Parameter(typeof(TInput));
        var body = FilterParser.ParseFor<bool>(clause, instance);
        var predicate  = Expression.Lambda<Func<TInput, bool>>(body, instance);
        return source.Where(predicate);
    }

    public static IOrderedQueryable<TInput> SortBy<TInput>(this IQueryable<TInput> source, string clause) where TInput : class {
        if (string.IsNullOrWhiteSpace(clause)) throw new ArgumentException("Sorting clause cannot be null or empty.", nameof(clause));
        var items = clause.Split(',').Select(ParseFieldAndDirection).ToArray();
        var inputFields = typeof(TInput).GetProperties().Select(p => p.Name).ToArray();
        var instance = Expression.Parameter(typeof(TInput));
        foreach (var item in items) {
            ValidateItem(item, typeof(TInput), inputFields);
            var expression = BuildKeySelector<TInput>(instance, item.Field);
            source = item.Direction == "ASC" ? source.OrderBy(expression) : source.OrderByDescending(expression);
        }
        return (IOrderedQueryable<TInput>)source;
    }

    private static (string Field, string Direction) ParseFieldAndDirection(string clause) {
        var parts = clause.Trim().Split(" ", StringSplitOptions.RemoveEmptyEntries);
#pragma warning disable IDE0046 // Convert to conditional expression
        if (parts.Length > 2) throw new SortingException("Sorting item must be in the format of 'field[ ASC]' or 'field DESC'.");
        return (Field: parts[0], Direction: parts.Length == 1 ? "ASC" : parts[1].ToUpper());
#pragma warning restore IDE0046 // Convert to conditional expression
    }

    private static void ValidateItem((string Field, string Direction) item, Type inputType, IEnumerable<string> inputFields) {
        if (item.Direction != "ASC" && item.Direction != "DESC") throw new SortingException("Sorting item must be in the format of 'field[ ASC]' or 'field DESC'.");
        if (!inputFields.Contains(item.Field)) throw new SortingException($"'{item.Field}' is not a valid field for '{inputType.Name}'.");
    }

    private static Expression<Func<TInput, object>> BuildKeySelector<TInput>(ParameterExpression instance, string field)
        where TInput : class {
        var body = Expression.PropertyOrField(instance, field);
        return Expression.Lambda<Func<TInput, object>>(Expression.Convert(body, typeof(object)), instance);
    }
}