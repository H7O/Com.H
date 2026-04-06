using Com.H.Text.Csv;

namespace Com.H.Tests;

public class CsvExtensionsTests
{
    [Fact]
    public void ParseCsv_BasicInput_ReturnsCorrectRowsAndColumns()
    {
        var csv = "Name,Age,City\r\nAlice,30,London\r\nBob,25,Paris";
        var result = csv.ParseCsv().ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal("Alice", (string)result[0].Name);
        Assert.Equal("30", (string)result[0].Age);
        Assert.Equal("London", (string)result[0].City);
        Assert.Equal("Bob", (string)result[1].Name);
    }

    [Fact]
    public void ParseCsv_ValuesWithSpaces_TrimsCorrectly()
    {
        // This exercises StringSplitOptions.TrimEntries
        var csv = "Name , Age , City \r\n Alice , 30 , London ";
        var result = csv.ParseCsv().ToList();

        Assert.Single(result);
        Assert.Equal("Alice", ((string)result[0].Name).Trim());
    }

    [Fact]
    public void ParsePsv_BasicInput_ParsesPipeDelimited()
    {
        var psv = "Name|Age\r\nAlice|30\r\nBob|25";
        var result = psv.ParsePsv().ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal("Alice", (string)result[0].Name);
        Assert.Equal("30", (string)result[0].Age);
    }

    [Fact]
    public void ParseDelimited_CustomDelimiters_Works()
    {
        var data = "Name;Age\nAlice;30";
        var result = data.ParseDelimited(
            new[] { "\n" }, new[] { ";" }).ToList();

        Assert.Single(result);
        Assert.Equal("Alice", (string)result[0].Name);
        Assert.Equal("30", (string)result[0].Age);
    }

    [Fact]
    public void ParseCsv_HeaderOnlyNoRows_ReturnsEmpty()
    {
        var csv = "Name,Age,City";
        var result = csv.ParseCsv().ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void ParseCsv_DynamicPropertyAccess_ExpandoObjectWorksCorrectly()
    {
        // Tests that ExpandoObject TryAdd works (affected by consolidation)
        var csv = "Key,Value\r\nfoo,bar";
        var result = csv.ParseCsv().ToList();

        IDictionary<string, object> dict = result[0];
        Assert.True(dict.ContainsKey("Key"));
        Assert.Equal("foo", dict["Key"]);
    }
}
