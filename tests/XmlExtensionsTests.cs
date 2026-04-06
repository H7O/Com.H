using Com.H.Xml.Linq;
using Com.H.Xml;
using System.Xml;

namespace Com.H.Tests;

public class XmlExtensionsTests
{
    #region XmlLinqExtensions - ParseXml / AsDynamic (exercises TryAdd on ExpandoObject)

    [Fact]
    public void ParseXml_SimpleElement_ReturnsDynamicWithProperties()
    {
        var xml = "<root><name>Alice</name><age>30</age></root>";
        var result = XmlLinqExtensions.ParseXml(xml);

        Assert.NotNull(result);
        // Without keepRoot, child element values are returned as a list
        var list = (List<object>)result;
        Assert.Equal(2, list.Count);
        Assert.Equal("Alice", (string)list[0]);
        Assert.Equal("30", (string)list[1]);
    }

    [Fact]
    public void ParseXml_WithKeepRoot_ReturnsRootProperties()
    {
        var xml = "<person><name>Alice</name><age>30</age></person>";
        var result = XmlLinqExtensions.ParseXml(xml, keepRoot: true);

        Assert.NotNull(result);
        // With keepRoot, we get the root element as a dynamic object
        IDictionary<string, object> dict = result;
        Assert.True(dict.ContainsKey("person"));
    }

    [Fact]
    public void ParseXml_WithAttributes_AttributesBecomeProperties()
    {
        var xml = "<root><item id=\"1\" name=\"test\">value</item></root>";
        dynamic? result = XmlLinqExtensions.ParseXml(xml);

        Assert.NotNull(result);
        var list = (List<object>)result;
        Assert.Single(list);

        IDictionary<string, object> item = (IDictionary<string, object>)list[0];
        Assert.True(item.ContainsKey("id"));
        Assert.Equal("1", item["id"]);
        Assert.True(item.ContainsKey("name"));
        Assert.Equal("test", item["name"]);
    }

    [Fact]
    public void ParseXml_NestedElements_ParsesRecursively()
    {
        var xml = "<root><parent><child>value</child></parent></root>";
        var result = XmlLinqExtensions.ParseXml(xml);

        Assert.NotNull(result);
    }

    [Fact]
    public void ParseXml_MultipleChildren_ReturnsList()
    {
        var xml = "<root><item>one</item><item>two</item></root>";
        var result = XmlLinqExtensions.ParseXml(xml);

        Assert.NotNull(result);
        var list = (List<object>)result;
        Assert.Equal(2, list.Count);
    }

    #endregion

    #region XmlSerializeAsync (exercises IAsyncEnumerable branch)

    [Fact]
    public async Task XmlSerializeAsync_SimpleObject_ProducesValidXml()
    {
        var data = new { Name = "Alice", Age = 30 };
        using var ms = new MemoryStream();

        await data.XmlSerializeAsync(ms, rootElementName: "Person");

        ms.Position = 0;
        var xml = new StreamReader(ms).ReadToEnd();

        Assert.Contains("Name", xml);
        Assert.Contains("Alice", xml);
        Assert.Contains("Age", xml);
        Assert.Contains("30", xml);
    }

    [Fact]
    public async Task XmlSerializeAsync_EnumerableData_SerializesAll()
    {
        var data = new List<object> { "one", "two", "three" };
        using var ms = new MemoryStream();

        await data.XmlSerializeAsync(ms, rootElementName: "Items");

        ms.Position = 0;
        var xml = new StreamReader(ms).ReadToEnd();

        Assert.Contains("one", xml);
        Assert.Contains("two", xml);
        Assert.Contains("three", xml);
    }

    [Fact]
    public async Task XmlSerializeAsync_AsyncEnumerable_SerializesAll()
    {
        async IAsyncEnumerable<object> GetItems()
        {
            yield return "alpha";
            yield return "beta";
            yield return "gamma";
        }

        var data = GetItems();
        using var ms = new MemoryStream();

        await data.XmlSerializeAsync(ms, rootElementName: "Items");

        ms.Position = 0;
        var xml = new StreamReader(ms).ReadToEnd();

        Assert.Contains("alpha", xml);
        Assert.Contains("beta", xml);
        Assert.Contains("gamma", xml);
    }

    #endregion
}
