namespace Touki;

#if NETFRAMEWORK
using AssertInterpolatedStringHandler = System.Diagnostics.AssertInterpolatedStringHandler;
#else
using AssertInterpolatedStringHandler = System.Diagnostics.Debug.AssertInterpolatedStringHandler;
#endif

public class AssertInterpolatedStringHandlerTests
{
    // The built-in handler in .NET 9 does not support calling the append
    // methods when the assertion succeeds, so limit these tests to .NET Framework.
#if NETFRAMEWORK
    [Fact]
    public void Constructor_SetsShouldAppend_FalseWhenConditionTrue()
    {
        bool shouldAppend;
        AssertInterpolatedStringHandler handler = new(5, 1, true, out shouldAppend);
        shouldAppend.Should().BeFalse();
        handler.AppendLiteral("Hello");
        handler.ToStringAndClear().Should().BeEmpty();
    }

    [Fact]
    public void Constructor_SetsShouldAppend_TrueWhenConditionFalse()
    {
        bool shouldAppend;
        AssertInterpolatedStringHandler handler = new(5, 1, false, out shouldAppend);
        shouldAppend.Should().BeTrue();
        handler.AppendLiteral("Hello");
        handler.ToStringAndClear().Should().Be("Hello");
    }
#endif
}
