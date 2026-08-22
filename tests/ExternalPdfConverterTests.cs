using System.Text;
using Com.H.Pdf;
using Com.H.Shell;

namespace Com.H.Tests;

/// <summary>
/// Drives <see cref="ExternalPdfConverter"/> with the system shell standing in for chrome, so
/// the failure modes that used to pass silently can be produced on demand without a browser.
/// </summary>
public class ExternalPdfConverterTests : IDisposable
{
    private static readonly byte[] MinimalPdf =
        Encoding.ASCII.GetBytes("%PDF-1.4\n% enough of a PDF for the header check\n");

    private readonly string _dir;
    private readonly string _htmlPath;
    private readonly string _outputPath;
    private readonly string _validPdfPath;
    private readonly string _notAPdfPath;

    public ExternalPdfConverterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"comh_pdf_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        _htmlPath = Path.Combine(_dir, "input.html");
        _outputPath = Path.Combine(_dir, "output.pdf");
        _validPdfPath = Path.Combine(_dir, "source.pdf");
        _notAPdfPath = Path.Combine(_dir, "source.bin");

        File.WriteAllText(_htmlPath, "<html><body>hello</body></html>");
        File.WriteAllBytes(_validPdfPath, MinimalPdf);
        File.WriteAllText(_notAPdfPath, "this is not a pdf at all");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private ExternalPdfConverter Converter(string parameters) => new()
    {
        PdfConverterPath = Sh.Shell,
        PdfConverterParameters = parameters,
        ConversionTimeoutMs = 20000
    };

    /// <summary>A "converter" that copies a prepared file to wherever the output should go.</summary>
    private static string Produces(string source) => Sh.OnWindows
        ? $"/c copy /y \"{source}\" \"{{{{output}}}}\" > nul"
        : $"-c \"cp '{source}' '{{{{output}}}}'\"";

    /// <summary>A "converter" that exits with the given code having written nothing.</summary>
    private static string ProducesNothing(int exitCode) => Sh.Exit(exitCode);

    private static string RunsTooLong(string holding) => Sh.RunLongHolding(holding);

    #region the happy path still works

    [Fact]
    public void UriToPdfFile_ConverterProducesAPdf_Succeeds()
    {
        Converter(Produces(_validPdfPath)).UriToPdfFile(new Uri(_htmlPath), _outputPath);

        Assert.True(File.Exists(_outputPath));
        Assert.Equal(MinimalPdf.Length, new FileInfo(_outputPath).Length);
    }

    [Fact]
    public async Task UriToPdfFileAsync_ConverterProducesAPdf_Succeeds()
    {
        await Converter(Produces(_validPdfPath)).UriToPdfFileAsync(new Uri(_htmlPath), _outputPath);

        Assert.True(File.Exists(_outputPath));
    }

    [Fact]
    public void UriToPdfStream_ReturnsAReadableStreamOverTheResult()
    {
        using var stream = Converter(Produces(_validPdfPath))
            .UriToPdfStream(new Uri(_htmlPath), Path.Combine(_dir, "streamed.pdf"));

        Assert.Equal(MinimalPdf.Length, stream.Length);
    }

    [Fact]
    public void HtmlToPdfFile_WritesTheOutput()
    {
        Converter(Produces(_validPdfPath))
            .HtmlToPdfFile("<html><body>hello</body></html>", _outputPath,
                Path.Combine(_dir, "temp.html"));

        Assert.True(File.Exists(_outputPath));
    }

    #endregion

    #region failures that used to pass silently

    [Fact]
    public void UriToPdfFile_ConverterExitsZeroWritingNothing_Throws()
    {
        // chromium can do exactly this, which is why a zero exit code is not enough on its own.
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Converter(ProducesNothing(0)).UriToPdfFile(new Uri(_htmlPath), _outputPath));

        Assert.Contains("without producing", ex.Message);
    }

    [Fact]
    public void UriToPdfFile_OutputIsNotAPdf_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Converter(Produces(_notAPdfPath)).UriToPdfFile(new Uri(_htmlPath), _outputPath));

        Assert.Contains("is not a PDF", ex.Message);
    }

    [Fact]
    public void UriToPdfFile_PreviousOutputStillOnDisk_IsClearedAndTheFailureReported()
    {
        // Left in place, yesterday's file makes a failed run look like a successful one.
        File.Copy(_validPdfPath, _outputPath, true);

        Assert.Throws<InvalidOperationException>(() =>
            Converter(ProducesNothing(0)).UriToPdfFile(new Uri(_htmlPath), _outputPath));

        Assert.False(File.Exists(_outputPath));
    }

    [Fact]
    public void UriToPdfFile_ConverterExitsNonZero_ThrowsCarryingTheExitCode()
    {
        // Chrome already running on the same profile exits 21, and used to be swallowed.
        var ex = Assert.Throws<ShellCommandException>(() =>
            Converter(ProducesNothing(21)).UriToPdfFile(new Uri(_htmlPath), _outputPath));

        Assert.Equal(21, ex.ExitCode);
    }

    [Fact]
    public void UriToPdfFile_ValidatePdfOutputDisabled_DoesNotThrow()
    {
        var converter = Converter(ProducesNothing(0));
        converter.ValidatePdfOutput = false;

        converter.UriToPdfFile(new Uri(_htmlPath), _outputPath);

        Assert.False(File.Exists(_outputPath));
    }

    #endregion

    #region input cleanup

    [Fact]
    public void UriToPdfFile_AskedToDeleteTheInput_ActuallyDeletesIt()
    {
        // This used to pass a file:// url to File.Delete inside a catch-all, so it silently
        // never happened.
        Converter(Produces(_validPdfPath))
            .UriToPdfFile(new Uri(_htmlPath), _outputPath, tryDeleteInputUriResourceAfterConversion: true);

        Assert.False(File.Exists(_htmlPath));
    }

    [Fact]
    public void UriToPdfFile_NotAskedToDeleteTheInput_LeavesItAlone()
    {
        Converter(Produces(_validPdfPath)).UriToPdfFile(new Uri(_htmlPath), _outputPath);

        Assert.True(File.Exists(_htmlPath));
    }

    #endregion

    #region timeout and cancellation

    [Fact]
    public void UriToPdfFile_ConverterHangs_ThrowsTimeoutException()
    {
        var converter = Converter(RunsTooLong(Path.Combine(_dir, "held.txt")));
        converter.ConversionTimeoutMs = 1500;

        Assert.Throws<TimeoutException>(() =>
            converter.UriToPdfFile(new Uri(_htmlPath), _outputPath));
    }

    [Fact]
    public async Task UriToPdfFileAsync_Cancelled_ThrowsOperationCanceled()
    {
        var converter = Converter(RunsTooLong(Path.Combine(_dir, "held.txt")));
        using var cts = new CancellationTokenSource();

        var task = converter.UriToPdfFileAsync(new Uri(_htmlPath), _outputPath, false, cts.Token);
        await Task.Delay(600);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    #endregion

    #region PdfExtensions passes its defaults through

    [Fact]
    public void PdfExtensions_DefaultsFlowIntoTheConverter()
    {
        var path = PdfExtensions.DefaultPdfConverterPath;
        var parameters = PdfExtensions.DefaultPdfConverterParameters;
        var timeout = PdfExtensions.DefaultConversionTimeoutMs;
        var validate = PdfExtensions.DefaultValidatePdfOutput;
        try
        {
            PdfExtensions.DefaultPdfConverterPath = Sh.Shell;
            PdfExtensions.DefaultPdfConverterParameters = ProducesNothing(0);
            PdfExtensions.DefaultValidatePdfOutput = true;

            Assert.Throws<InvalidOperationException>(() => new Uri(_htmlPath).ToPdfFile(_outputPath));

            // Changing the default has to rebuild the cached converter, not keep the old one.
            PdfExtensions.DefaultValidatePdfOutput = false;
            new Uri(_htmlPath).ToPdfFile(_outputPath);
        }
        finally
        {
            PdfExtensions.DefaultPdfConverterPath = path;
            PdfExtensions.DefaultPdfConverterParameters = parameters;
            PdfExtensions.DefaultConversionTimeoutMs = timeout;
            PdfExtensions.DefaultValidatePdfOutput = validate;
        }
    }

    [Fact]
    public async Task PdfExtensions_ToPdfFileAsync_Converts()
    {
        var path = PdfExtensions.DefaultPdfConverterPath;
        var parameters = PdfExtensions.DefaultPdfConverterParameters;
        try
        {
            PdfExtensions.DefaultPdfConverterPath = Sh.Shell;
            PdfExtensions.DefaultPdfConverterParameters = Produces(_validPdfPath);

            await new Uri(_htmlPath).ToPdfFileAsync(_outputPath);

            Assert.True(File.Exists(_outputPath));
        }
        finally
        {
            PdfExtensions.DefaultPdfConverterPath = path;
            PdfExtensions.DefaultPdfConverterParameters = parameters;
        }
    }

    #endregion
}
