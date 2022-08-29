namespace VSCS.DynamicQuery.UnitTests;

public class FilterExceptionTests {
    [Fact]
    public void FilterException_DefaultConstructor_Passes() {
        _ = new FilterException();
    }

    [Fact]
    public void FilterException_PublicConstructor_Passes() {
        _ = new FilterException("SomeMessage", new Exception());
    }
}