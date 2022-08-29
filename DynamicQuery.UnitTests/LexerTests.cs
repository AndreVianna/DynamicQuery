namespace VSCS.DynamicQuery.UnitTests;

public class LexerTests {
    [Theory]
    [InlineData("null", typeof(object), null)]
    [InlineData("true", typeof(bool), true)]
    [InlineData("false", typeof(bool), false)]
    [InlineData("123", typeof(int), 123)]
    [InlineData("3.14", typeof(double), 3.14)]
    [InlineData(".14", typeof(double), 0.14)]
    [InlineData("3.", typeof(double), 3.0)]
    [InlineData("\"ABC\"", typeof(string), "ABC")]
    [InlineData("\'a\'", typeof(char), 'a')]
    [InlineData("\'\t\'", typeof(char), '\t')]
    public void Lexer_ReadFrom_ForValue_Passes(string input, Type expectedType, object? expectedValue) {
        var result = Lexer.ReadFrom(input);

        result.Should().BeEquivalentTo(new ValueToken(1, input, expectedType, expectedValue));
    }

    [Theory]
    [InlineData("(")]
    [InlineData(")")]
    [InlineData("=")]
    [InlineData(",")]
    [InlineData("<")]
    [InlineData(">")]
    [InlineData("[")]
    [InlineData("]")]
    [InlineData("<=")]
    [InlineData(">=")]
    [InlineData("<>")]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("*")]
    [InlineData("/")]
    [InlineData("%")]
    public void Lexer_ReadFrom_ForSymbol_Passes(string input) {
        var result = Lexer.ReadFrom(input);

        result.Should().BeEquivalentTo(new SymbolToken(1, input));
    }

    [Theory]
    [InlineData("AND")]
    [InlineData("or")]
    [InlineData("Not")]
    [InlineData("Between")]
    [InlineData("in")]
    public void Lexer_ReadFrom_ForReservedWord_Passes(string input) {
        var result = Lexer.ReadFrom(input);

        result.Should().BeEquivalentTo(new SymbolToken(1, input));
    }

    [Theory]
    [InlineData("SomeName")]
    [InlineData("_Some_Name_")]
    public void Lexer_ReadFrom_ForNames_Passes(string input) {
        var result = Lexer.ReadFrom(input);

        result.Should().BeEquivalentTo(new NamedToken(1, input));
    }

    [Theory]
    [InlineData("-3.14")]
    public void Lexer_ReadFrom_ForNegatives_Passes(string input) {
        var expectedTokens = new Token[] {
            new SymbolToken(1, "-"),
            new ValueToken(2, "3.14", typeof(double), 3.14),
        };

        var result = Lexer.ReadFrom(input);

        AssertTokenSequence(expectedTokens, result);
    }

    [Theory]
    [InlineData("2 + -3.14")]
    public void Lexer_ReadFrom_ForAddNegative_Passes(string input) {
        var expectedTokens = new Token[] {
            new ValueToken(1, "2", typeof(int), 2),
            new SymbolToken(3, "+"),
            new SymbolToken(5, "-"),
            new ValueToken(6, "3.14", typeof(double), 3.14),
        };

        var result = Lexer.ReadFrom(input);

        AssertTokenSequence(expectedTokens, result);
    }

    [Fact]
    public void Lexer_ReadFrom_ForExpression_Passes() {
        var expectedTokens = new Token[] {
            new NamedToken(1, "SomeField"),
            new SymbolToken(11, ">="),
            new ValueToken(14, "1", typeof(int), 1),
            new SymbolToken(16, "AND"),
            new NamedToken(20, "OtherField"),
            new SymbolToken(31, "="),
            new ValueToken(33, "\"ABC\"", typeof(string), "ABC")
        };

        var result = Lexer.ReadFrom("SomeField >= 1 AND OtherField = \"ABC\"");

        AssertTokenSequence(expectedTokens, result);
    }

    [Theory]
    [InlineData("?")]
    [InlineData("$")]
    public void Lexer_ReadFrom_ForInvalidSymbol_Throws(string input) {
        var action = () => Lexer.ReadFrom(input);

        action.Should().Throw<FilterException>().WithMessage($"Invalid syntax near '{input}' at position 1.");
    }

    private static void AssertTokenSequence(IReadOnlyList<Token> expectedTokens, Token? token) {
        for (var i = 0; i < expectedTokens.Count; i++) {
            token.Should().NotBeNull();
            token.Should().BeEquivalentTo(expectedTokens[i],
                options => options.Excluding(o => o.Next).Excluding(o => o.Previous));
            if (i == 0) token!.Previous.Should().BeNull();
            else token!.Previous.Should().BeEquivalentTo(expectedTokens[i - 1], options => options.Excluding(o => o.Next).Excluding(o => o.Previous));
            if (i == expectedTokens.Count - 1) token.Next.Should().BeNull();
            else token.Next.Should().BeEquivalentTo(expectedTokens[i + 1], options => options.Excluding(o => o.Next).Excluding(o => o.Previous));
            token = token.Next;
        }
    }
}