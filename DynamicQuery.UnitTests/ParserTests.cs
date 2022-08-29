namespace VSCS.DynamicQuery.UnitTests;

public class ParserTests {
    private static readonly Expression _instance = Expression.Parameter(typeof(TestClass));
    private static readonly PropertyInfo _indexer = typeof(string).GetProperty("Chars")!;

    private record TestClass {
        public int Id { get; init; } = 42;
        public string Name { get; init; } = "SomeName";
    }

    [Theory]
    [InlineData("42", 42)]
    [InlineData("3.14", 3.14)]
    [InlineData("True", true)]
    [InlineData("\"ABC\"", "ABC")]
    [InlineData("'a'", 'a')]
    public void Parser_ParseFor_ForValues_Passes(string input, object value) {
        var expectedExpression = Expression.Constant(value);

        AssertResultTree(input, expectedExpression, value.GetType());
    }

    [Theory]
    [InlineData("42")]
    public void Parser_ParseFor_WrongResult_Throws(string input) {
        var action = () => FilterParser.ParseFor<string>(input, _instance);

        action.Should().Throw<FilterException>().WithMessage("Invalid syntax near '42' at position 1. The result of the expression must be a string.");
    }

    [Theory]
    [InlineData("42[2]")]
    public void Parser_ParseFor_NonStringIndexedValue_Throws(string input) {
        var action = () => FilterParser.ParseFor<int>(input, _instance);

        action.Should().Throw<FilterException>().WithMessage("Invalid syntax near '42' at position 1. The indexed value must be a string.");
    }

    [Theory]
    [InlineData("\"ABC\"[2]")]
    public void Parser_ParseFor_IndexedValueClause_Passes(string input) {
        var expectedExpression = Expression.MakeIndex(Expression.PropertyOrField(_instance, "Name"), _indexer, new[] { Expression.Constant(2) });

        AssertResultTree<char>(input, expectedExpression);
    }

    [Theory]
    [InlineData("Id")]
    [InlineData("Name")]
    public void Parser_ParseFor_ForLiterals_Passes(string input) {
        var expectedExpression = Expression.PropertyOrField(_instance, input);

        AssertResultTree(input, expectedExpression, typeof(TestClass).GetProperty(input)!.PropertyType);
    }

    [Theory]
    [InlineData("Invalid")]
    public void Parser_ParseFor_InvalidProperty_Throws(string input) {
        var action = () => FilterParser.ParseFor<int>(input, _instance);

        action.Should().Throw<FilterException>().WithMessage("Invalid syntax near 'Invalid' at position 1. 'Invalid' is not a public member of 'TestClass'.");
    }

    [Theory]
    [InlineData("Name[2]")]
    public void Parser_ParseFor_IndexedFieldClause_Passes(string input) {
        var expectedExpression = Expression.MakeIndex(Expression.PropertyOrField(_instance, "Name"), _indexer, new[] { Expression.Constant(2) });

        AssertResultTree<char>(input, expectedExpression);
    }

    [Theory]
    [InlineData("Id[2]")]
    public void Parser_ParseFor_NonStringIndexedProperty_Throws(string input) {
        var action = () => FilterParser.ParseFor<int>(input, _instance);

        action.Should().Throw<FilterException>().WithMessage("Invalid syntax near 'Id' at position 1. The indexed field must be a string.");
    }

    [Theory]
    [InlineData("-3.14")]
    public void Parser_ParseFor_ForUnaryMinus_Passes(string input) {
        var expectedExpression = Expression.Negate(Expression.Constant(3.14));

        AssertResultTree<double>(input, expectedExpression);
    }

    [Theory]
    [InlineData("NOT True")]
    public void Parser_ParseFor_ForUnaryNotAtStart_Passes(string input) {
        var expectedExpression = Expression.Not(Expression.Constant(true));

        AssertResultTree<bool>(input, expectedExpression);
    }

    [Theory]
    [InlineData("False AND NOT True")]
    public void Parser_ParseFor_ForUnaryNot_Passes(string input) {
        var expectedExpression = Expression.And(Expression.Constant(false), Expression.Not(Expression.Constant(true)));

        AssertResultTree<bool>(input, expectedExpression);
    }

    [Theory]
    [InlineData("+42")]
    public void Parser_ParseFor_ForUnaryPlus_Passes(string input) {
        var expectedExpression = Expression.Constant(42);

        AssertResultTree<int>(input, expectedExpression);
    }

    [Theory]
    [InlineData("Max(3, 2)")]
    public void Parser_ParseFor_MaxCallClause_Passes(string input) {
        var maxMethod = typeof(Math).GetMethod(nameof(Math.Max), new[] { typeof(int), typeof(int) })!;
        var expectedExpression = Expression.Call(null, maxMethod, Expression.Constant(3), Expression.Constant(2));

        AssertResultTree<int>(input, expectedExpression);
    }

    [Theory]
    [InlineData("1 + Max(3, 2)")]
    public void Parser_ParseFor_OperateOnCallClause_Passes(string input) {
        var maxMethod = typeof(Math).GetMethod(nameof(Math.Max), new[] { typeof(int), typeof(int) })!;
        var expectedExpression = Expression.Add(
            Expression.Constant(1),
            Expression.Call(null, maxMethod, Expression.Constant(3), Expression.Constant(2)));

        AssertResultTree<int>(input, expectedExpression);
    }

    [Theory]
    [InlineData("3 ^ 2")]
    public void Parser_ParseFor_ForPowerClause_Passes(string input) {
        var expectedExpression = Expression.Power(
            Expression.Convert(Expression.Constant(3), typeof(double)),
            Expression.Convert(Expression.Constant(2), typeof(double)));

        AssertResultTree<double>(input, expectedExpression);
    }

    [Theory]
    [InlineData("3 * 2", ExpressionType.Multiply, typeof(int))]
    [InlineData("3 / 2", ExpressionType.Divide, typeof(int))]
    [InlineData("3 % 2", ExpressionType.Modulo, typeof(int))]
    [InlineData("3 + 2", ExpressionType.Add, typeof(int))]
    [InlineData("3 - 2", ExpressionType.Subtract, typeof(int))]
    [InlineData("3 > 2", ExpressionType.GreaterThan, typeof(bool))]
    [InlineData("3 >= 2", ExpressionType.GreaterThanOrEqual, typeof(bool))]
    [InlineData("3 = 2", ExpressionType.Equal, typeof(bool))]
    [InlineData("3 < 2", ExpressionType.LessThan, typeof(bool))]
    [InlineData("3 <= 2", ExpressionType.LessThanOrEqual, typeof(bool))]
    [InlineData("3 <> 2", ExpressionType.NotEqual, typeof(bool))]
    public void Parser_ParseFor_ForBinaryNumericClause_Passes(string input, ExpressionType type, Type expectedType) {
        var expectedExpression = Expression.MakeBinary(type,
            Expression.Constant(3),
            Expression.Constant(2));

        AssertResultTree(input, expectedExpression, expectedType);
    }

    [Theory]
    [InlineData("True AND False", ExpressionType.And)]
    [InlineData("True OR False", ExpressionType.Or)]
    public void Parser_ParseFor_ForBinaryLogicalClause_Passes(string input, ExpressionType type) {
        var expectedExpression = Expression.MakeBinary(type,
            Expression.Constant(true),
            Expression.Constant(false));

        AssertResultTree<bool>(input, expectedExpression);
    }

    private static readonly IDictionary<string, MethodInfo> _methods = new Dictionary<string, MethodInfo> {
        ["StartsWith"] = typeof(string).GetMethod(nameof(string.StartsWith), new[] { typeof(string) })!,
        ["EndsWith"] = typeof(string).GetMethod(nameof(string.EndsWith), new[] { typeof(string) })!,
        ["Contains"] = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!,
    };

    [Theory]
    [InlineData("\"ABC\" Contains \"B\"", "Contains")]
    [InlineData("\"ABC\" StartsWith \"B\"", "StartsWith")]
    [InlineData("\"ABC\" EndsWith \"B\"", "EndsWith")]
    public void Parser_ParseFor_ForBinaryStringClause_Passes(string input, string method) {
        var expectedExpression = Expression.Call(Expression.Constant("ABC"), _methods[method], Expression.Constant("B"));

        AssertResultTree<bool>(input, expectedExpression);
    }

    [Theory]
    [InlineData("3 BETWEEN 2 AND 4")]
    public void Parser_ParseFor_ForBetweenClause_Passes(string input) {
        var expectedExpression = Expression.And(
                Expression.GreaterThanOrEqual(Expression.Constant(3), Expression.Constant(2)),
                Expression.LessThanOrEqual(Expression.Constant(3), Expression.Constant(4)));

        AssertResultTree<bool>(input, expectedExpression);
    }

    [Theory]
    [InlineData("True IS True")]
    public void Parser_ParseFor_IsOperation_Passes(string input) {
        var expectedExpression = Expression.Equal(
            Expression.Constant(true),
            Expression.Constant(true));

        AssertResultTree<bool>(input, expectedExpression);
    }

    [Theory]
    [InlineData("False IS NOT True")]
    public void Parser_ParseFor_IsNotOperation_Passes(string input) {
        var expectedExpression = Expression.Equal(
            Expression.Constant(false),
            Expression.Not(Expression.Constant(true)));

        AssertResultTree<bool>(input, expectedExpression);
    }

    [Theory]
    [InlineData("3 IN (1, 2 , 3, 4)")]
    public void Parser_ParseFor_ForInClause_Passes(string input) {
        var expectedExpression = Expression.Or(Expression.Constant(false),
                Expression.Or(Expression.Equal(Expression.Constant(3), Expression.Constant(1)),
                    Expression.Or(Expression.Equal(Expression.Constant(3), Expression.Constant(2)),
                        Expression.Or(Expression.Equal(Expression.Constant(3), Expression.Constant(3)),
                            Expression.Equal(Expression.Constant(3), Expression.Constant(4))))));

        AssertResultTree<bool>(input, expectedExpression);
    }

    [Theory]
    [InlineData("(Id + 2)")]
    [InlineData("((Id + 2))")]
    public void Parser_ParseFor_Expressions_Passes(string input) {
        var expectedExpression = Expression.Add(
            Expression.PropertyOrField(_instance, "Id"),
            Expression.Constant(2));

        AssertResultTree<int>(input, expectedExpression);
    }

    [Theory]
    [InlineData("2 + 3 + 4")]
    public void Parser_ParseFor_SamePrecedence_Passes(string input) {
        var expectedExpression = Expression.Add(
            Expression.Add(
                Expression.Constant(2),
                Expression.Constant(3)),
            Expression.Constant(4));

        AssertResultTree<int>(input, expectedExpression);
    }

    [Theory]
    [InlineData("2 + 3 * 4")]
    public void Parser_ParseFor_HigherPrecedence_Passes(string input) {
        var expectedExpression = Expression.Add(
            Expression.Constant(2),
            Expression.Multiply(
                Expression.Constant(3),
                Expression.Constant(4)));

        AssertResultTree<int>(input, expectedExpression);
    }

    [Theory]
    [InlineData("2 + 3.1")]
    public void Parser_ParseFor_MathConvertLeft_Passes(string input) {
        var expectedExpression = Expression.Add(
            Expression.Convert(Expression.Constant(2), typeof(double)),
            Expression.Constant(3.1));

        AssertResultTree<double>(input, expectedExpression);
    }

    [Theory]
    [InlineData("2.1 + 3")]
    public void Parser_ParseFor_MathConvertRight_Passes(string input) {
        var expectedExpression = Expression.Add(
            Expression.Constant(2.1),
            Expression.Convert(Expression.Constant(3), typeof(double)));

        AssertResultTree<double>(input, expectedExpression);
    }

    [Theory]
    [InlineData("2 * 3 + 4")]
    public void Parser_ParseFor_LowerPrecedence_Passes(string input) {
        var expectedExpression = Expression.Add(
            Expression.Multiply(
                Expression.Constant(2),
                Expression.Constant(3)),
            Expression.Constant(4));

        AssertResultTree<int>(input, expectedExpression);
    }

    [Theory]
    [InlineData("1 ^ 2 * 3 + 4 * 5 ^ 6")]
    public void Parser_ParseFor_ComplexPrecedence_Passes(string input) {
        var expectedExpression = Expression.Add(
            Expression.Multiply(
                Expression.Power(
                    Expression.Convert(Expression.Constant(1), typeof(double)),
                    Expression.Convert(Expression.Constant(2), typeof(double))),
                Expression.Convert(Expression.Constant(3), typeof(double))),
            Expression.Multiply(
                Expression.Convert(Expression.Constant(4), typeof(double)),
                Expression.Power(
                    Expression.Convert(Expression.Constant(5), typeof(double)),
                    Expression.Convert(Expression.Constant(6), typeof(double)))));

        AssertResultTree<double>(input, expectedExpression);
    }

    [Theory]
    [InlineData("1 + 2 * 3 + 4")]
    public void Parser_ParseFor_AlternatingInner_Passes(string input) {
        var expectedExpression = Expression.Add(
            Expression.Add(
                Expression.Constant(1),
                Expression.Multiply(
                    Expression.Constant(2),
                    Expression.Constant(3))),
            Expression.Constant(4));

        AssertResultTree<int>(input, expectedExpression);
    }

    [Theory]
    [InlineData("1 * 2 + 3 * 4")]
    public void Parser_ParseFor_AlternatingOuter_Passes(string input) {
        var expectedExpression = Expression.Add(
            Expression.Multiply(
                Expression.Constant(1),
                Expression.Constant(2)),
            Expression.Multiply(
                Expression.Constant(3),
                Expression.Constant(4)));

        AssertResultTree<int>(input, expectedExpression);
    }

    [Theory]
    [InlineData("1 * (2 + 3) * 4")]
    public void Parser_ParseFor_WithExpressionInner_Passes(string input) {
        var expectedExpression = Expression.Multiply(
            Expression.Constant(1),
            Expression.Add(
                Expression.Constant(2),
                Expression.Multiply(
                    Expression.Constant(3),
                    Expression.Constant(4))));

        AssertResultTree<int>(input, expectedExpression);
    }

    [Theory]
    [InlineData("1 ^ 2 ^ 3", ExpressionType.Power)]
    public void Parser_ParseFor_PowerPrecedence_Passes(string input, ExpressionType type) {
        var expectedExpression = Expression.MakeBinary(type,
            Expression.Convert(Expression.MakeBinary(type,
                Expression.Convert(Expression.Constant(1), typeof(double)),
                Expression.Convert(Expression.Constant(2), typeof(double))), typeof(double)),
            Expression.Convert(Expression.Constant(3), typeof(double)));

        AssertResultTree<double>(input, expectedExpression);
    }

    [Theory]
    [InlineData("1 * 2 * 3", ExpressionType.Multiply)]
    [InlineData("1 + 2 + 3", ExpressionType.Add)]
    public void Parser_ParseFor_MathPrecedence_Passes(string input, ExpressionType type) {
        var expectedExpression = Expression.MakeBinary(type,
            Expression.MakeBinary(type,
                Expression.Constant(1),
                Expression.Constant(2)),
            Expression.Constant(3));

        AssertResultTree<int>(input, expectedExpression);
    }

    [Theory]
    [InlineData("3 > 2 AND True")]
    public void Parser_ParseFor_CompareLeft_Passes(string input) {
        var expectedExpression = Expression.And(
            Expression.GreaterThan(
                Expression.Constant(3),
                Expression.Constant(2)),
            Expression.Constant(true));

        AssertResultTree<bool>(input, expectedExpression);
    }

    [Theory]
    [InlineData("1 + 3 > 2")]
    public void Parser_ParseFor_CompareRight_Passes(string input) {
        var expectedExpression = Expression.GreaterThan(
            Expression.Add(
                Expression.Constant(1),
                Expression.Constant(3)),
            Expression.Constant(2));

        AssertResultTree<bool>(input, expectedExpression);
    }

    [Theory]
    [InlineData("True AND False AND True", ExpressionType.And)]
    [InlineData("True OR False OR True", ExpressionType.Or)]
    public void Parser_ParseFor_LogicalPrecedence_Passes(string input, ExpressionType type) {
        var expectedExpression = Expression.MakeBinary(type,
            Expression.MakeBinary(type,
                Expression.Constant(true),
                Expression.Constant(false)),
            Expression.Constant(false));

        AssertResultTree<bool>(input, expectedExpression);
    }

    [Theory]
    [InlineData(")", 1, ")")]
    [InlineData("^ 4", 1, "^")]
    [InlineData("* 4", 1, "*")]
    [InlineData("< 4", 1, "<")]
    [InlineData("= 4", 1, "=")]
    [InlineData("BETWEEN 4 AND 3", 1, "BETWEEN")]
    [InlineData("IN (1, 3, 4)", 1, "IN")]
    [InlineData("3 BETWEEN AND 2", 11, "AND")]
    [InlineData("3 BETWEEN 1 2", 13, "2")]
    [InlineData("3 BETWEEN 1 + 2", 15, "2")]
    [InlineData("3 BETWEEN 1 AND +", 17, "+")]
    [InlineData("(3 + 2", 6, "2")]
    [InlineData("Contains \"B\"", 1, "Contains")]
    [InlineData("3 IN (1, 2", 10, "2")]
    [InlineData("Name[]", 6, "]")]
    public void Parser_ParseFor_InvalidExpression_Throws(string input, int position, string text) {
        var action = () => FilterParser.ParseFor<int>(input, _instance);

        action.Should().Throw<FilterException>().Where(e => e.Position == position && e.Text == text);
    }

    [Theory]
    [InlineData("\"A\" ^ 2", 1, "\"A\"")]
    [InlineData("3 ^ 'a'", 5, "'a'")]
    [InlineData("3 = 'a'", 5, "'a'")]
    [InlineData("\"A\" + 2", 1, "\"A\"")]
    [InlineData("3 + 'a'", 5, "'a'")]
    [InlineData("3 > 'a'", 5, "'a'")]
    [InlineData("\"A\" > 2", 1, "\"A\"")]
    [InlineData("3 IN ('a', 'b')", 7, "'a'")]
    [InlineData("3 IN (1, 'b')", 10, "'b'")]
    [InlineData("\"A\" BETWEEN 2 AND 4", 1, "\"A\"")]
    [InlineData("3 BETWEEN 'a' AND 4", 11, "'a'")]
    [InlineData("3 BETWEEN 2 AND 'a'", 17, "'a'")]
    [InlineData("3 StartsWith \"A\"", 1, "3")]
    [InlineData("\"ABC\" StartsWith 3", 18, "3")]
    [InlineData("3 AND True", 1, "3")]
    [InlineData("True AND 3", 10, "3")]
    public void Parser_ParseFor_TypeError_Throws(string input, int position, string text) {
        var action = () => FilterParser.ParseFor<int>(input, _instance);

        action.Should().Throw<FilterException>().Where(e => e.Position == position && e.Text == text);
    }

    [Theory]
    [InlineData("Sin(2)", 1, "Sin")]
    public void Parser_ParseFor_UnsupportedMethosCall_Throws(string input, int position, string text) {
        var action = () => FilterParser.ParseFor<int>(input, _instance);

        action.Should().Throw<FilterException>().Where(e => e.Position == position && e.Text == text);
    }

    [Theory]
    [InlineData("3 4", 3, "4")]
    [InlineData("Max(1, 2) 4", 11, "4")]
    [InlineData("Name[1] 4", 9, "4")]
    [InlineData("3 Name", 3, "Name")]
    [InlineData("Max(1, 2) Name", 11, "Name")]
    [InlineData("Name[1] Name", 9, "Name")]
    [InlineData("3 Min(1, 3)", 3, "Min")]
    [InlineData("Max(1, 2) Min(1, 3)", 11, "Min")]
    [InlineData("Name[1] Min(1, 3)", 9, "Min")]
    public void Parser_ParseFor_LiteralsWithoutOperator_Throws(string input, int position, string text) {
        var action = () => FilterParser.ParseFor<int>(input, _instance);

        action.Should().Throw<FilterException>().Where(e => e.Position == position && e.Text == text);
    }

    private static void AssertResultTree<TOutput>(string input, Expression expectedExpression) {
        var result = FilterParser.ParseFor<TOutput>(input, _instance);

        result.Should().BeEquivalentTo(expectedExpression);
    }

    private static void AssertResultTree(string input, Expression expectedExpression, Type expectedType) {
        var result = FilterParser.ParseFor(input, _instance, expectedType);

        result.Should().BeEquivalentTo(expectedExpression);
    }
}
