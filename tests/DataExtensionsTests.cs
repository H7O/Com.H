using Com.H.Data;
using System.Text.Json;

namespace Com.H.Tests;

public class DataExtensionsTests
{
    [Fact]
    public void GetDataModelParameters_NullDataModel_ReturnsNull()
    {
        object? model = null;
        Assert.Null(model!.GetDataModelParameters());
    }

    [Fact]
    public void GetDataModelParameters_AnonymousObject_ReadsProperties()
    {
        var result = new { Name = "John", Age = 30 }.GetDataModelParameters();

        Assert.NotNull(result);
        Assert.Equal("John", result!["Name"]);
        Assert.Equal(30, result["Age"]);
    }

    [Fact]
    public void GetDataModelParameters_DefaultsToCaseInsensitiveKeys()
    {
        var result = new { Name = "John" }.GetDataModelParameters();

        Assert.NotNull(result);
        Assert.Equal("John", result!["name"]);
        Assert.Equal("John", result["NAME"]);
    }

    [Fact]
    public void GetDataModelParameters_CaseSensitiveTrue_DistinguishesKeyCasing()
    {
        var result = new { Name = "John" }.GetDataModelParameters(caseSensitive: true);

        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("Name"));
        Assert.False(result.ContainsKey("name"));
    }

    [Fact]
    public void GetDataModelParameters_StringObjectDictionary_IsRead()
    {
        var model = new Dictionary<string, object> { ["a"] = 1, ["b"] = "two" };

        var result = model.GetDataModelParameters();

        Assert.NotNull(result);
        Assert.Equal(1, result!["a"]);
        Assert.Equal("two", result["b"]);
    }

    [Fact]
    public void GetDataModelParameters_StringStringDictionary_IsRead()
    {
        var model = new Dictionary<string, string> { ["city"] = "Amman", ["country"] = "Jordan" };

        var result = model.GetDataModelParameters();

        Assert.NotNull(result);
        Assert.Equal("Amman", result!["city"]);
        Assert.Equal("Jordan", result["country"]);
    }

    [Fact]
    public void GetDataModelParameters_KeyValuePairSequence_IsRead()
    {
        var model = new List<KeyValuePair<string, string>>
        {
            new("first", "1"),
            new("second", "2")
        };

        var result = model.GetDataModelParameters();

        Assert.NotNull(result);
        Assert.Equal("1", result!["first"]);
        Assert.Equal("2", result["second"]);
    }

    [Fact]
    public void GetDataModelParameters_JsonElement_IsRead()
    {
        var json = JsonDocument
            .Parse("""{"name":"Jane","age":25,"active":true,"missing":null}""")
            .RootElement;

        var result = json.GetDataModelParameters();

        Assert.NotNull(result);
        Assert.Equal("Jane", result!["name"]);
        Assert.Equal(25d, result["age"]);
        Assert.Equal(true, result["active"]);
        Assert.Null(result["missing"]);
    }

    [Fact]
    public void GetDataModelParameters_JsonString_IsParsed()
    {
        var result = """{"name":"Jane","age":25}""".GetDataModelParameters();

        Assert.NotNull(result);
        Assert.Equal("Jane", result!["name"]);
        Assert.Equal(25d, result["age"]);
    }

    [Fact]
    public void GetDataModelParameters_NestedJson_ReturnsRawText()
    {
        var result = """{"user":{"id":7},"tags":[1,2]}""".GetDataModelParameters();

        Assert.NotNull(result);
        Assert.Equal("""{"id":7}""", result!["user"]);
        Assert.Equal("[1,2]", result["tags"]);
    }

    [Fact]
    public void GetDataModelParameters_NonJsonString_YieldsNoParameters()
    {
        var result = "not json at all".GetDataModelParameters();

        Assert.NotNull(result);
        Assert.Empty(result!);
    }

    [Fact]
    public void GetDataModelParameters_DuplicateKeys_DescendingFalse_KeepsFirst()
    {
        var model = new List<object>
        {
            new { Name = "first" },
            new { Name = "second" }
        };

        var result = model.GetDataModelParameters(descending: false);

        Assert.NotNull(result);
        Assert.Equal("first", result!["Name"]);
    }

    [Fact]
    public void GetDataModelParameters_DuplicateKeys_DescendingTrue_KeepsLast()
    {
        var model = new List<object>
        {
            new { Name = "first" },
            new { Name = "second" }
        };

        var result = model.GetDataModelParameters(descending: true);

        Assert.NotNull(result);
        Assert.Equal("second", result!["Name"]);
    }

    [Fact]
    public void GetDataModelParameters_LegacyTwoArgumentCall_MatchesThreeArgumentDefault()
    {
        var model = new { Name = "John" };

        // binds to the retained two-parameter overload
        var legacy = model.GetDataModelParameters(false);
        var current = model.GetDataModelParameters(false, false);

        Assert.NotNull(legacy);
        Assert.NotNull(current);
        Assert.Equal(current!["Name"], legacy!["Name"]);
        Assert.Equal("John", legacy["name"]);
    }

    [Fact]
    public void ReplaceQueryParameterMarkers_SwapsMarkers()
    {
        var query = "select * from t where a = {{a}} and b = {{b}}";

        var result = query.ReplaceQueryParameterMarkers("{{", "}}", "@", "");

        Assert.Equal("select * from t where a = @a and b = @b", result);
    }

    [Fact]
    public void ReplaceQueryParameterMarkers_EmptyQuery_ReturnsAsIs()
    {
        Assert.Equal("", "".ReplaceQueryParameterMarkers("{{", "}}", "@", ""));
    }
}
