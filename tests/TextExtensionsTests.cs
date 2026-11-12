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

    /// <summary>Runs something as a machine set to the named culture would. "" is the invariant culture.</summary>
    private static T In<T>(string culture, Func<T> run)
    {
        var before = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            return run();
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    // ExtractDates: an entry that is not a date

    [Theory]
    [InlineData("soon")]
    // A word, a space and the letter d. Before 11.0.1 this was taken for a date like "Nov 5", and threw.
    [InlineData("next day")]
    // The letter d on both sides of a hyphen. Before 11.0.1 this was taken for a date like "11-05", and threw.
    [InlineData("mid-december")]
    [InlineData("d-d")]
    [InlineData("Foo 12")]
    [InlineData("2026-02-30")]
    [InlineData("2026-13-45")]
    public void ExtractDates_AnEntryThatIsNotADate_IsLeftOut(string entry)
    {
        foreach (var culture in new[] { "", "en-US", "en-GB", "de-DE", "ar-AE" })
        {
            Assert.Empty(In(culture, () => entry.ExtractDates().ToList()));
            Assert.Equal(
                new[] { new DateTime(2026, 12, 24), new DateTime(2026, 12, 31) },
                In(culture, () => $"2026-12-24|{entry}|2026-12-31".ExtractDates().ToList()));
        }
    }

    // ExtractDates: a month's name and a day, without a year

    [Theory]
    [InlineData("")]
    [InlineData("en-US")]
    // The day is written first here, and the general reader takes "Nov 5" as November of the year 05.
    [InlineData("en-GB")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("ar-AE")]
    // Cultures with a calendar of their own.
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void ExtractDates_AMonthsNameAndADay_IsThatDayOfThisYearInEveryCulture(string culture)
    {
        var year = DateTime.Now.Year;

        var dates = In(culture, () => "Nov 5|nov 05|NOVEMBER  5|Dec 31|January 1".ExtractDates().ToList());

        Assert.Equal(new[] { new DateTime(year, 11, 5), new DateTime(year, 12, 31), new DateTime(year, 1, 1) }, dates);
    }

    [Theory]
    [InlineData("en-GB")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("es-ES")]
    [InlineData("ru-RU")]
    [InlineData("tr-TR")]
    [InlineData("ar-AE")]
    public void ExtractDates_AMonthsNameInTheLanguageOfTheCulture_IsRead(string culture)
    {
        var format = CultureInfo.GetCultureInfo(culture).DateTimeFormat;
        var year = DateTime.Now.Year;
        var read = 0;
        for (var month = 1; month <= 12; month++)
        {
            // The names as this runtime has them, which differ between .NET and .NET Framework.
            var names = new[]
            {
                format.MonthNames[month - 1], format.AbbreviatedMonthNames[month - 1],
                format.MonthGenitiveNames[month - 1], format.AbbreviatedMonthGenitiveNames[month - 1],
            };
            foreach (var name in names.Distinct().Where(name => System.Text.RegularExpressions.Regex.IsMatch(name, @"^\p{L}+\.?$")))
            {
                Assert.Equal(new[] { new DateTime(year, month, 5) }, In(culture, () => $"{name} 5".ExtractDates().ToList()));
                read++;
            }
        }
        Assert.True(read >= 12, $"Only {read} names of {culture} were tried.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("en-US")]
    [InlineData("en-GB")]
    [InlineData("ar-AE")]
    public void ExtractDates_ANumberThatIsNotADayOfTheMonth_IsNotTakenForAYear(string culture)
    {
        var year = DateTime.Now.Year;

        // Where the day is written first, the general reader takes "Feb 30" as February of 2030.
        Assert.Empty(In(culture, () => "Feb 30|Nov 31|Nov 0|Jan 32|Mar 45|Dec 99".ExtractDates().ToList()));

        var expected = DateTime.IsLeapYear(year) ? new[] { new DateTime(year, 2, 29) } : new DateTime[0];
        Assert.Equal(expected, In(culture, () => "Feb 29".ExtractDates().ToList()));
    }

    /// <summary>
    /// The rule for a month's name and a day is what a culture that writes the month first has always
    /// read. So where the month is written first nothing changed in 11.0.1, whatever the number is.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("en-US")]
    public void ExtractDates_AMonthsNameAndANumber_IsReadAsWhereTheMonthIsWrittenFirst(string culture)
    {
        var english = CultureInfo.InvariantCulture.DateTimeFormat;
        var different = new List<string>();
        for (var month = 0; month < 12; month++)
            foreach (var name in new[] { english.MonthNames[month], english.AbbreviatedMonthNames[month].ToLowerInvariant() })
                for (var number = 0; number <= 99; number++)
                    foreach (var entry in new[] { $"{name} {number}", $"{name}  {number:00}", $"{name}. {number}" })
                    {
                        var (general, extracted) = In(culture, () =>
                            (DateTime.TryParse(entry, out var date) ? new[] { date } : new DateTime[0], entry.ExtractDates().ToArray()));
                        if (!general.SequenceEqual(extracted)) different.Add(entry);
                    }

        Assert.Empty(different);
    }

    // ExtractDates: a date in numbers, without a year

    [Fact]
    public void ExtractDates_ADateInNumbersWithoutAYear_IsReadInTheOrderOfTheCulture()
    {
        var year = DateTime.Now.Year;

        Assert.Equal(new[] { new DateTime(year, 11, 5) }, In("en-US", () => "11-05".ExtractDates().ToList()));
        Assert.Equal(new[] { new DateTime(year, 5, 11) }, In("en-GB", () => "11-05".ExtractDates().ToList()));

        // What that order can't make a date of is left out, and not read in the other order.
        Assert.Empty(In("en-US", () => "13-05".ExtractDates().ToList()));
        Assert.Empty(In("en-GB", () => "05-13".ExtractDates().ToList()));
    }

    // ExtractInts and ExtractRangeInts

    [Theory]
    [InlineData("a 12 b -3, 007", new[] { 12, -3, 7 })]
    // 15 and 2026 in Arabic-Indic digits. Before 11.0.1 the method found them and threw on reading them.
    [InlineData("١٥ ٢٠٢٦", new[] { 15, 2026 })]
    // 15 in Persian, Devanagari and fullwidth digits.
    [InlineData("۱۵ १५ １５", new[] { 15, 15, 15 })]
    // A minus sign before Arabic-Indic digits, and the digits of two scripts in one number.
    [InlineData("-٣ 1٥", new[] { -3, 15 })]
    public void ExtractInts_DigitsOfAnyScript_AreRead(string text, int[] expected)
    {
        Assert.Equal(expected, text.ExtractInts().ToArray());
    }

    [Theory]
    [InlineData("1,15,25..28", new[] { 1, 15, 25, 26, 27, 28 })]
    [InlineData(" 28 - 25, 15 ,1, 15", new[] { 1, 15, 25, 26, 27, 28 })]
    // The first list in Arabic-Indic digits.
    [InlineData("١,١٥,٢٥..٢٨", new[] { 1, 15, 25, 26, 27, 28 })]
    // 1 and 15 with the Arabic comma between them, which is the comma of an Arabic keyboard.
    // Taken for something other than a comma, it would make this every number from 1 to 15.
    [InlineData("١، ١٥", new[] { 1, 15 })]
    // 1, 15 and 3 in fullwidth digits, with the fullwidth and the ideographic comma.
    [InlineData("１，１５、３", new[] { 1, 3, 15 })]
    public void ExtractRangeInts_DigitsAndCommasOfOtherScripts_AreRead(string text, int[] expected)
    {
        Assert.Equal(expected, text.ExtractRangeInts().ToArray());
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
