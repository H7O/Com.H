using Com.H.Events;

namespace Com.H.Tests;

public class EventArgsTests
{
    [Fact]
    public void HErrorEventArgs_InitProperties_SetViaConstructor()
    {
        var ex = new InvalidOperationException("test error");
        var sender = new object();

        var args = new HErrorEventArgs(sender, ex);

        Assert.Same(sender, args.Sender);
        Assert.Same(ex, args.Exception);
    }

    [Fact]
    public void HMsgEventArgs_InitProperties_SetViaConstructor()
    {
        var sender = new object();
        var args = new HMsgEventArgs(sender, "hello");

        Assert.Same(sender, args.Sender);
        Assert.Equal("hello", args.Message);
    }
}
