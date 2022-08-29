namespace VSCS.DynamicQuery.UnitTests;

public class SortingExceptionTests {
    [Fact]
    public void SortingException_DefaultConstructor_Passes() {
        _ = new SortingException();
    }

    [Fact]
    public void SortingException_PublicConstructor_Passes() {
        _ = new SortingException("SomeMessage", new Exception());
    }
}