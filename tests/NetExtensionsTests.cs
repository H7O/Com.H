using Com.H.Net;

namespace Com.H.Tests;

public class NetExtensionsTests
{
    [Fact]
    public void GetParentUri_SimpleUrl_ReturnsParent()
    {
        // Exercises Range syntax [..(index)]
        var uri = new Uri("https://example.com/path/to/resource");
        var parent = uri.GetParentUri();

        Assert.NotNull(parent);
        Assert.Equal("https://example.com/path/to/", parent!.AbsoluteUri);
    }

    [Fact]
    public void GetParentUri_TrailingSlash_RemovesBeforeGettingParent()
    {
        var uri = new Uri("https://example.com/path/to/folder/");
        var parent = uri.GetParentUri();

        Assert.NotNull(parent);
        Assert.Equal("https://example.com/path/to/", parent!.AbsoluteUri);
    }

    [Fact]
    public void GetParentUri_RootUrl_ThrowsOnInvalidParent()
    {
        // "https://example.com/" → strips trailing slash → "https://example.com"
        // → parent is "https://" which is not a valid URI
        var uri = new Uri("https://example.com/");
        Assert.ThrowsAny<UriFormatException>(() => uri.GetParentUri());
    }

    [Fact]
    public void GetParentUri_NullUri_ReturnsNull()
    {
        var result = ((Uri?)null).GetParentUri();
        Assert.Null(result);
    }

    [Fact]
    public void GetParentUri_DeepPath_ReturnsImmediateParent()
    {
        var uri = new Uri("https://example.com/a/b/c/d/e");
        var parent = uri.GetParentUri();

        Assert.NotNull(parent);
        Assert.Equal("https://example.com/a/b/c/d/", parent!.AbsoluteUri);
    }
}
