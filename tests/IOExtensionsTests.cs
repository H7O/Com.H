using Com.H.IO;

namespace Com.H.Tests;

public class IOExtensionsTests
{
    #region ValidateAndGetNormalizeFileName

    [Fact]
    public void ValidateFileName_ValidName_ReturnsNormalized()
    {
        var result = IOExtensions.ValidateAndGetNormalizeFileName("report.pdf");
        Assert.Equal("report.pdf", result);
    }

    [Fact]
    public void ValidateFileName_ValidNameWithExtensionWhitelist_Passes()
    {
        var allowed = new HashSet<string> { ".pdf", ".txt" };
        var result = IOExtensions.ValidateAndGetNormalizeFileName("report.pdf", allowed);
        Assert.Equal("report.pdf", result);
    }

    [Fact]
    public void ValidateFileName_DisallowedExtension_Throws()
    {
        var allowed = new HashSet<string> { ".txt" };
        Assert.Throws<ArgumentException>(() =>
            IOExtensions.ValidateAndGetNormalizeFileName("report.pdf", allowed));
    }

    [Fact]
    public void ValidateFileName_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            IOExtensions.ValidateAndGetNormalizeFileName(""));
    }

    [Fact]
    public void ValidateFileName_PathTraversal_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            IOExtensions.ValidateAndGetNormalizeFileName("../etc/passwd"));
    }

    [Fact]
    public void ValidateFileName_ContainsDirectorySeparator_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            IOExtensions.ValidateAndGetNormalizeFileName("sub/file.txt"));
    }

    [Fact]
    public void ValidateFileName_ReservedWindowsName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            IOExtensions.ValidateAndGetNormalizeFileName("CON.txt"));
    }

    [Fact]
    public void ValidateFileName_TooLong_Throws()
    {
        var longName = new string('a', 151) + ".txt";
        Assert.Throws<ArgumentException>(() =>
            IOExtensions.ValidateAndGetNormalizeFileName(longName));
    }

    [Fact]
    public void ValidateFileName_ContainsColon_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            IOExtensions.ValidateAndGetNormalizeFileName("file:stream.txt"));
    }

    [Fact]
    public void ValidateFileName_InvalidChars_ThrowsWithDetails()
    {
        // NUL and other control chars
        var ex = Assert.Throws<ArgumentException>(() =>
            IOExtensions.ValidateAndGetNormalizeFileName("file\0name.txt"));
        Assert.Contains("control characters", ex.Message);
    }

    #endregion

    #region GetBase64DecodedSize

    [Fact]
    public void GetBase64DecodedSize_NoPadding_CalculatesCorrectly()
    {
        // "AAAA" = 3 bytes decoded
        Assert.Equal(3, "AAAA".GetBase64DecodedSize());
    }

    [Fact]
    public void GetBase64DecodedSize_OnePadding_CalculatesCorrectly()
    {
        // "AAA=" = 2 bytes decoded
        Assert.Equal(2, "AAA=".GetBase64DecodedSize());
    }

    [Fact]
    public void GetBase64DecodedSize_TwoPadding_CalculatesCorrectly()
    {
        // "AA==" = 1 byte decoded
        Assert.Equal(1, "AA==".GetBase64DecodedSize());
    }

    [Fact]
    public void GetBase64DecodedSize_Empty_ReturnsZero()
    {
        Assert.Equal(0, "".GetBase64DecodedSize());
    }

    [Fact]
    public void GetBase64DecodedSize_RealBase64_MatchesActualDecode()
    {
        var original = "Hello, World!"u8.ToArray();
        var base64 = Convert.ToBase64String(original);
        Assert.Equal(original.Length, base64.GetBase64DecodedSize());
    }

    #endregion

    #region WriteBase64ToFileAsync

    [Fact]
    public async Task WriteBase64ToFileAsync_WritesCorrectContent()
    {
        var original = "Hello, World!"u8.ToArray();
        var base64 = Convert.ToBase64String(original);

        var tempFile = Path.GetTempFileName();
        try
        {
            var bytesWritten = await base64.WriteBase64ToFileAsync(tempFile);
            Assert.Equal(original.Length, bytesWritten);

            var written = await File.ReadAllBytesAsync(tempFile);
            Assert.Equal(original, written);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteBase64ToFileAsync_ExceedsMaxSize_Throws()
    {
        var original = new byte[100];
        Random.Shared.NextBytes(original);
        var base64 = Convert.ToBase64String(original);

        var tempFile = Path.GetTempFileName();
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(async () =>
                await base64.WriteBase64ToFileAsync(tempFile, maxFileSizeInBytes: 10));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    #endregion

    #region WriteBase64ToTempFileAsync

    [Fact]
    public async Task WriteBase64ToTempFileAsync_CreatesFileWithContent()
    {
        var original = "Test content"u8.ToArray();
        var base64 = Convert.ToBase64String(original);

        var (tempPath, fileSize) = await base64.WriteBase64ToTempFileAsync(
            null, "test.bin", CancellationToken.None);
        try
        {
            Assert.True(File.Exists(tempPath));
            Assert.Equal(original.Length, fileSize);

            var written = await File.ReadAllBytesAsync(tempPath);
            Assert.Equal(original, written);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    #endregion

    #region IsWritableFolder

    [Fact]
    public void IsWritableFolder_TempPath_ReturnsTrue()
    {
        Assert.True(Path.GetTempPath().IsWritableFolder());
    }

    [Fact]
    public void IsWritableFolder_NullUri_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((Uri?)null).IsWritableFolder());
    }

    [Fact]
    public void IsWritableFolder_NullString_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((string?)null).IsWritableFolder());
    }

    #endregion
}
