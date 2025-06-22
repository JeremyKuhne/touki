namespace Touki;

public class DebuggingTests
{
    [Fact]
    public void Assert_ExpressionNotEvaluated_WhenConditionTrue()
    {
        int value = 0;
        Debugging.Assert(true, $"Value {++value}");
        value.Should().Be(0);
    }

#if !DEBUG
    [Fact]
    public void Assert_Elided_InRelease()
    {
        int value = 0;
        Debugging.Assert(true, $"Value {++value}");
        value.Should().Be(0);
    }
#endif
}
