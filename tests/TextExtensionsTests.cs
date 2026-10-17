using System.Globalization;
using Com.H.Data;
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

    // Fill with QueryParams sets

    [Fact]
    public void FillSets_AValueThatLooksLikeAPlaceholder_IsNotFilledAgain()
    {
        var sets = new List<QueryParams> { new() { DataModel = new { a = "{{b}}", b = "x" } } };
        Assert.Equal("{{b}} x", "{{a}} {{b}}".Fill(sets));
    }

    [Fact]
    public void FillSets_TextJoinedByAnEmptyValue_IsNotAPlaceholder()
    {
        // Removing {[a]} joins "{" and "[b]}". Only the {[b]} written as such is filled.
        var sets = new List<QueryParams>
        {
            new() { DataModel = new { a = "", b = "x" }, OpenMarker = "{[", CloseMarker = "]}" },
        };
        Assert.Equal("{[b]} x", "{{[a]}[b]} {[b]}".Fill(sets));
    }

    [Fact]
    public void FillSets_ANameTheLatestSetLacks_IsFilledFromAnEarlierSet()
    {
        var sets = new List<QueryParams>
        {
            new() { DataModel = new { title = "Report" } },
            new() { DataModel = new { name = "Ann" } },
        };
        Assert.Equal("Report: Ann", "{{title}}: {{name}}".Fill(sets));
    }

    [Fact]
    public void FillSets_TheLatestSetWithTheName_DecidesEvenWithANullValue()
    {
        var sets = new List<QueryParams>
        {
            new() { DataModel = new { name = "Outer" } },
            new() { DataModel = new Dictionary<string, object?> { ["name"] = null }, NullReplacement = "-" },
        };
        Assert.Equal("-", "{{name}}".Fill(sets));
    }

    [Fact]
    public void FillSets_ANameNoSetHas_GetsTheLatestSetsNullReplacement()
    {
        var sets = new List<QueryParams>
        {
            new() { DataModel = new { a = "1" }, NullReplacement = "outer" },
            new() { DataModel = new { b = "2" }, NullReplacement = "inner" },
        };
        Assert.Equal("inner", "{{x}}".Fill(sets));
    }

    [Fact]
    public void FillSets_ADollarSignInAValue_IsWrittenAsItIs()
    {
        var sets = new List<QueryParams> { new() { DataModel = new { a = "$0 $$ $1 ${x}" } } };
        Assert.Equal("[$0 $$ $1 ${x}]", "[{{a}}]".Fill(sets));
    }

    [Theory]
    [InlineData("[[", "]]")]
    [InlineData("||", "||")]
    [InlineData("$(", ")")]
    [InlineData("<?", "?>")]
    public void FillSets_MarkersAreMatchedAsWritten(string open, string close)
    {
        var sets = new List<QueryParams>
        {
            new() { DataModel = new { a = "1" }, OpenMarker = open, CloseMarker = close },
        };
        Assert.Equal("x 1 y", $"x {open}a{close} y".Fill(sets));
    }

    [Fact]
    public void FillSets_NamesIgnoreCase()
    {
        var sets = new List<QueryParams> { new() { DataModel = new { name = "Ann" } } };
        Assert.Equal("Ann Ann", "{{NAME}} {{name}}".Fill(sets));
    }

    [Fact]
    public void FillSets_AMarkerWrittenInAnotherCase_IsNotAPlaceholder()
    {
        var sets = new List<QueryParams>
        {
            new() { DataModel = new { a = "1" }, OpenMarker = "{j{", CloseMarker = "}}" },
        };
        Assert.Equal("1 {J{a}}", "{j{a}} {J{a}}".Fill(sets));
    }

    [Fact]
    public void FillSets_NamesAreComparedCharacterByCharacter()
    {
        // A culture-aware comparison ignores the soft hyphen, so {{a<soft hyphen>}} would take a's value.
        var sets = new List<QueryParams> { new() { DataModel = new { a = "1" }, NullReplacement = "-" } };
        Assert.Equal("1|-", "{{a}}|{{a­}}".Fill(sets));
    }

    // Fill with a data model

    [Fact]
    public void Fill_AValueThatLooksLikeAPlaceholder_IsNotFilledAgain()
    {
        Assert.Equal("{{b}} x", "{{a}} {{b}}".Fill(new { a = "{{b}}", b = "x" }, "{{", "}}"));
    }

    [Fact]
    public void Fill_AValueThatLooksLikeAPlaceholderNoModelHas_IsKept()
    {
        Assert.Equal("{{zzz}}", "{{a}}".Fill(new { a = "{{zzz}}" }, "{{", "}}", "-"));
    }

    [Fact]
    public void Fill_TheFirstModelWithAValue_FillsAName()
    {
        object models = new object[] { new { a = (string?)null, b = "1" }, new { a = "2", b = "3" } };
        Assert.Equal("2 1", "{{a}} {{b}}".Fill(models, "{{", "}}"));
    }

    [Fact]
    public void Fill_ADollarSignInTheNullReplacement_IsWrittenAsItIs()
    {
        Assert.Equal("[$0]", "[{{x}}]".Fill(new { a = "1" }, "{{", "}}", "$0"));
    }

    [Fact]
    public void Fill_AValueThatIsNotIConvertible_IsWrittenWithItsToString()
    {
        Assert.Equal("00000000-0000-0000-0000-000000000000",
            "{{id}}".Fill(new { id = Guid.Empty }, "{{", "}}"));
    }

    [Theory]
    [InlineData("[[", "]]")]
    [InlineData("||", "||")]
    public void Fill_MarkersAreMatchedAsWritten(string open, string close)
    {
        Assert.Equal("x 1 y", $"x {open}a{close} y".Fill(new { a = "1" }, open, close));
    }

    [Fact]
    public void Fill_WithoutMarkers_ReplacesEachKeysTextOnce()
    {
        var model = new Dictionary<string, object?> { ["a"] = "b", ["b"] = "c" };
        Assert.Equal("b c", "a b".Fill(model));
    }

    [Fact]
    public void Fill_WithoutMarkers_TheFirstModelWithAValueFillsAKey()
    {
        object models = new object[]
        {
            new Dictionary<string, object?> { ["a"] = null, ["b"] = "1" },
            new Dictionary<string, object?> { ["a"] = "2", ["b"] = "3" },
        };
        Assert.Equal("2 1", "a b".Fill(models));
    }

    [Fact]
    public void Fill_WithoutMarkers_IgnoresTheNullReplacement()
    {
        Assert.Equal("ab", "ab".Fill(new { c = "1" }, nullValueReplacement: "x"));
    }

    [Fact]
    public void Fill_Values_AreWrittenInTheInvariantCulture()
    {
        var culture = CultureInfo.CurrentCulture;
        var other = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        other.NumberFormat.NumberDecimalSeparator = ",";
        other.DateTimeFormat.ShortDatePattern = "dd.MM.yyyy";
        CultureInfo.CurrentCulture = other;
        try
        {
            Assert.Equal("1.5 10/15/2026 09:45:00 +00:00", "{{v}} {{d}}".Fill(
                new { v = 1.5, d = new DateTimeOffset(2026, 10, 15, 9, 45, 0, TimeSpan.Zero) }, "{{", "}}"));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void Fill_ANameInAnotherCaseThanItsKey_IsFilled()
    {
        Assert.Equal("x", "{{NAME}}".Fill(new { name = "x" }, "{{", "}}", "-"));
    }

    [Fact]
    public void Fill_KeysThatDifferOnlyInCase_EachFillTheirOwnSpelling()
    {
        var model = new Dictionary<string, object?> { ["MM"] = "month", ["mm"] = "minute" };
        Assert.Equal("month minute", "{{MM}} {{mm}}".Fill(model, "{{", "}}"));
    }

    [Fact]
    public void Fill_AStrayOpeningMarkerBeforeAPlaceholder_StaysAsWritten()
    {
        // The pattern reads "{{{{a}}" as one placeholder named "{{a", which has no value. The {{a}}
        // inside it is filled first, so the null replacement no longer finds that text.
        Assert.Equal("{{1 1", "{{{{a}} {{a}}".Fill(new { a = "1" }, "{{", "}}", "-"));
    }

    [Fact]
    public void Fill_APlaceholderWithoutAValue_IsReplacedWhereThePatternFoundIt()
    {
        // The pattern reads "{{x {{b}}" as one placeholder, so the {{b}} inside it is not one of its own.
        Assert.Equal("- -", "{{b}} {{x {{b}}".Fill(new { a = "1" }, "{{", "}}", "-"));
    }

    [Fact]
    public void Fill_AnEmptyPlaceholder_GetsTheNullReplacement()
    {
        Assert.Equal("a-b", "a{{}}b".Fill(new { a = "1" }, "{{", "}}", "-"));
    }

    [Fact]
    public void Fill_WithOnlyAnOpeningMarker_LeavesTheOtherMarkersAsTheyAre()
    {
        Assert.Equal("1 and {{b", "{{a and {{b".Fill(new { a = "1" }, "{{", null, "-"));
    }

    [Fact]
    public void Fill_WithOnlyAClosingMarker_LeavesTheTextBeforeTheOtherMarkersAsItIs()
    {
        Assert.Equal("1 and b}}", "a}} and b}}".Fill(new { a = "1" }, null, "}}", "-"));
    }

    [Fact]
    public void Fill_QueryParamsPassedAsAnObjectWithANullReplacement_UseIt()
    {
        object sets = new List<QueryParams> { new() { DataModel = new { a = "x" } } };
        Assert.Equal("x -", "{{a}} {{b}}".Fill(sets, nullValueReplacement: "-"));
    }

    [Fact]
    public void Fill_QueryParamsPassedAsAnObject_KeepTheirData()
    {
        object sets = new List<QueryParams> { new() { DataModel = new { a = "x" } } };
        Assert.Equal("x", "{{a}}".Fill(sets));
    }

    [Fact]
    public void Fill_QueryParamsPassedAsAnObjectWithMarkers_UseThoseMarkers()
    {
        object sets = new List<QueryParams> { new() { DataModel = new { a = "x" } } };
        Assert.Equal("x {{a}}", "[a] {{a}}".Fill(sets, "[", "]"));
    }

    // FillDate

    [Fact]
    public void FillDate_FormatsThatDifferOnlyInCase_KeepTheirOwnMeaning()
    {
        var date = new DateTime(2026, 10, 15, 9, 45, 0);
        Assert.Equal("10-45", "{date{MM}}-{date{mm}}".FillDate(date));
    }

    [Fact]
    public void FillDate_MarkersAreMatchedAsWritten()
    {
        var date = new DateTime(2026, 10, 15, 9, 45, 0);
        Assert.Equal("2026", "[now[yyyy]]".FillDate(date, "[now[", "]]"));
    }

    [Fact]
    public void FillDate_AnEmptyPlaceholder_StaysAsItIs()
    {
        Assert.Equal("a{date{}}b", "a{date{}}b".FillDate(new DateTime(2026, 10, 15)));
    }
}
