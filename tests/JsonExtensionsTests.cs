using Com.H.Text.Json;
using System.Text.Json;

namespace Com.H.Tests;

public class JsonExtensionsTests
{
    [Fact]
    public async Task JsonSerializeAsync_SimpleObject_ProducesValidJson()
    {
        var data = new { Name = "Alice", Age = 30 };
        using var ms = new MemoryStream();

        await data.JsonSerializeAsync(ms);

        ms.Position = 0;
        var json = new StreamReader(ms).ReadToEnd();

        Assert.Contains("\"Name\"", json);
        Assert.Contains("\"Alice\"", json);
        Assert.Contains("\"Age\"", json);
        Assert.Contains("30", json);
    }

    [Fact]
    public async Task JsonSerializeAsync_EnumerableData_SerializesAsArray()
    {
        var data = new List<object> { "one", "two", "three" };
        using var ms = new MemoryStream();

        await data.JsonSerializeAsync(ms);

        ms.Position = 0;
        var json = new StreamReader(ms).ReadToEnd();

        Assert.Contains("one", json);
        Assert.Contains("two", json);
        Assert.Contains("three", json);
    }

    [Fact]
    public async Task JsonSerializeAsync_AsyncEnumerable_SerializesAll()
    {
        async IAsyncEnumerable<object> GetItems()
        {
            yield return "alpha";
            yield return "beta";
            yield return "gamma";
        }

        var data = GetItems();
        using var ms = new MemoryStream();

        await data.JsonSerializeAsync(ms);

        ms.Position = 0;
        var json = new StreamReader(ms).ReadToEnd();

        Assert.Contains("alpha", json);
        Assert.Contains("beta", json);
        Assert.Contains("gamma", json);
    }

    [Fact]
    public async Task JsonSerializeAsync_NestedObject_SerializesRecursively()
    {
        var data = new
        {
            Person = new { Name = "Bob", Age = 25 },
            Tags = new[] { "dev", "test" }
        };
        using var ms = new MemoryStream();

        await data.JsonSerializeAsync(ms);

        ms.Position = 0;
        var json = new StreamReader(ms).ReadToEnd();

        Assert.Contains("\"Person\"", json);
        Assert.Contains("\"Bob\"", json);
        Assert.Contains("\"dev\"", json);
    }

    [Fact]
    public async Task JsonSerializeAsync_NullValue_HandlesGracefully()
    {
        var data = new { Name = (string?)null, Value = 42 };
        using var ms = new MemoryStream();

        await data.JsonSerializeAsync(ms);

        ms.Position = 0;
        var json = new StreamReader(ms).ReadToEnd();

        Assert.Contains("\"Value\"", json);
        Assert.Contains("42", json);
    }
}
