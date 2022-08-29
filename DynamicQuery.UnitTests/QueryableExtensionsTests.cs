namespace VSCS.DynamicQuery.UnitTests;

using System.Linq.DynamicQuery;

public class QueryableExtensionsTests {
    private record TestClass {
        public int Id { get; init; } = 42;
        public string? Name { get; init; } = "SomeName";
    }

    private readonly TestClass[] _items = {
        new TestClass { Id = 1, Name = "001" },
        new TestClass { Id = 2, Name = "003" },
        new TestClass { Id = 3, Name = "004" },
        new TestClass { Id = 4, Name = "005" },
        new TestClass { Id = 5, Name = "002" },
    };

    [Fact]
    public void QueryableExtensions_FilterBy_Passes() {
        var result = _items.AsQueryable().FilterBy("Id > 2").ToArray();

        result.Should().BeEquivalentTo(new [] { _items[2], _items[3], _items[4] });
    }

    [Fact]
    public void QueryableExtensions_SortBy_Passes() {
        var result = _items.AsQueryable().SortBy("Name DESC, Id").ToArray();

        result.Should().BeEquivalentTo(new[] { _items[3], _items[2], _items[1], _items[4], _items[0] });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void QueryableExtensions_FilterBy_ForNullOrEmptyInput_Throws(string input) {
        var action = () => _items.AsQueryable().FilterBy(input);

        action.Should().Throw<ArgumentException>().WithMessage("Filter clause cannot be null or empty. (Parameter 'clause')");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void QueryableExtensions_SortBy_ForNullOrEmptyInput_Throws(string input) {
        var action = () => _items.AsQueryable().SortBy(input);

        action.Should().Throw<ArgumentException>().WithMessage("Sorting clause cannot be null or empty. (Parameter 'clause')");
    }

    [Theory]
    [InlineData("Name ASC Foo")]
    [InlineData("Name Foo")]
    public void QueryableExtensions_SortBy_InvalidItemFormat_Throws(string input) {
        var action = () => _items.AsQueryable().SortBy(input);

        action.Should().Throw<SortingException>().WithMessage("Sorting item must be in the format of 'field[ ASC]' or 'field DESC'.");
    }

    [Theory]
    [InlineData("Foo")]
    public void QueryableExtensions_SortBy_InvalidField_Throws(string input) {
        var action = () => _items.AsQueryable().SortBy(input);

        action.Should().Throw<SortingException>().WithMessage("'Foo' is not a valid field for 'TestClass'.");
    }
}