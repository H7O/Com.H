using Com.H.Text;

namespace Com.H.Tests;

public class TextExtensionsTests
{
    [Fact]
    public void ExtractDates_FullDate_ParsesCorrectly()
    {
        var text = "2024-03-15";
        var result = text.ExtractDates().ToList();

        Assert.Single(result);
        Assert.Equal(new DateTime(2024, 3, 15), result[0]);
    }

    [Fact]
    public void ExtractDates_MultipleDatesWithDefaultSeparator_AllParsed()
    {
        var text = "2024-01-01|2024-06-15|2024-12-25";
        var result = text.ExtractDates().ToList();

        Assert.Equal(3, result.Count);
        Assert.Equal(new DateTime(2024, 1, 1), result[0]);
        Assert.Equal(new DateTime(2024, 6, 15), result[1]);
        Assert.Equal(new DateTime(2024, 12, 25), result[2]);
    }

    [Fact]
    public void ExtractDates_CustomSeparators_Works()
    {
        var text = "2024-01-01;2024-06-15";
        var result = text.ExtractDates(new[] { ";" }).ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ExtractDates_DatesWithSpaces_TrimsCorrectly()
    {
        // Exercises StringSplitOptions.TrimEntries path
        var text = " 2024-01-01 | 2024-06-15 ";
        var result = text.ExtractDates().ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateTime(2024, 1, 1), result[0]);
        Assert.Equal(new DateTime(2024, 6, 15), result[1]);
    }
}
