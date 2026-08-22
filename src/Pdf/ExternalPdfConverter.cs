using System.Runtime.InteropServices;
using Com.H.IO;
using Com.H.Net;
using Com.H.Runtime.InteropServices;
using Com.H.Shell;

namespace Com.H.Pdf
{
    /// <summary>
    /// Converts HTML to PDF using an external PDF converter.
    /// by default, the converter looks up for edge or chrome executables in default expected locations of different operating systems:
    /// Windows: C:\Program Files (x86)\Google\Chrome\Application\chrome.exe
    ///          or
    ///          C:\Program Files\Google\Chrome\Application\chrome.exe
    ///          or
    ///          C:/Program Files/Microsoft/Edge/Application/msedge.exe
    /// Linux: /usr/bin/google-chrome
    ///        or
    ///        opt/google/chrome/chrome
    /// MacOS: /Applications/Google Chrome.app/Contents/MacOS/Google Chrome
    /// FreeBSD: /usr/local/bin/chromium
    /// </summary>
    public class ExternalPdfConverter
    {
        /// <summary>
        /// Example: a path to chrome executable (or any other executable that can convert HTML to PDF)
        /// On windows chrome usually installed under c:\Program Files\Google\Chrome\Application\chrome.exe
        /// On Linux, chrome usually located at opt/google/chrome/chrome
        /// </summary>
        public string? PdfConverterPath { get; set; }

        /// <summary>
        /// Example "--headless --no-pdf-header-footer --run-all-compositor-stages-before-draw --print-to-pdf=\"{{output}}\" \"{{input}}\""
        /// for use with chrome.exe
        /// </summary>
        public string? PdfConverterParameters { get; set; }

        /// <summary>
        /// Extra environment variables handed to the converter process.
        /// </summary>
        public System.Collections.Specialized.StringDictionary? EnvironmentVariables { get; set; }

        /// <summary>
        /// How long to wait, in milliseconds, for the converter to finish a single document
        /// before giving up and killing it. Defaults to 60 seconds.
        /// </summary>
        public int ConversionTimeoutMs { get; set; } = 60000;

        /// <summary>
        /// When true (the default) the converter checks that a non empty file with a PDF
        /// header actually appeared, and throws when it did not. Set to false if you are
        /// driving a converter that produces something other than PDF.
        /// </summary>
        public bool ValidatePdfOutput { get; set; } = true;

        private static string GetSuitableTempPath()
        {
            // check if we can write to temp folder
            if (new Uri(Path.GetTempPath()).IsWritableFolder())
                return Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.tmp");
            // check if we can write to current folder
            if (new Uri(AppDomain.CurrentDomain.BaseDirectory).IsWritableFolder())
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{Guid.NewGuid()}.tmp.pdf");
            return string.Empty;
        }

        #region uri -> pdf

        /// <summary>
        /// Converts the resource the uri points at into a PDF file on disk.
        /// This is the root implementation every other conversion goes through.
        /// </summary>
        /// <param name="uri">The document to convert.</param>
        /// <param name="outputFilePath">Where to write the PDF.</param>
        /// <param name="tryDeleteInputUriResourceAfterConversion">Delete the input file afterwards, best effort.</param>
        /// <param name="cancellationToken">Kills the converter process tree and throws when signalled.</param>
        /// <exception cref="ShellCommandException">The converter exited with a non zero code.</exception>
        /// <exception cref="TimeoutException">The converter did not finish within <see cref="ConversionTimeoutMs"/>.</exception>
        /// <exception cref="InvalidOperationException">No usable PDF was produced.</exception>
        public async Task UriToPdfFileAsync(
            Uri uri,
            string outputFilePath,
            bool tryDeleteInputUriResourceAfterConversion = false,
            CancellationToken cancellationToken = default
            )
        {
            #region check arguments
            if (uri is null) throw new ArgumentNullException(nameof(uri));
            if (!Uri.IsWellFormedUriString(uri.AbsoluteUri, UriKind.Absolute))
                throw new FormatException(
                    $"Invalid uri format : {uri.AbsoluteUri}");

            if (string.IsNullOrWhiteSpace(outputFilePath)) throw new ArgumentNullException(nameof(outputFilePath));
            #endregion

            ResolveConverter();

            var args = PdfConverterParameters?
                .Replace("{{input}}", uri.AbsoluteUri)
                .Replace("{{output}}", outputFilePath);

            try
            {
                var parentDirectory = Path.GetDirectoryName(outputFilePath);
                if (!string.IsNullOrWhiteSpace(parentDirectory)
                    && !Directory.Exists(parentDirectory))
                    Directory.CreateDirectory(parentDirectory);

                // Remove any previous output first. Without this a failed conversion leaves
                // yesterday's file sitting there and the caller has no way of telling that
                // nothing was produced this time round.
                if (File.Exists(outputFilePath))
                {
                    try { File.Delete(outputFilePath); } catch { }
                }

                await ConvertAsync(
                    this.PdfConverterPath!, args, EnvironmentVariables,
                    this.ConversionTimeoutMs, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    if (tryDeleteInputUriResourceAfterConversion
                        && uri.IsFile
                        )
                        // LocalPath, not AbsoluteUri - the latter is a file:// url and
                        // File.Delete cannot do anything useful with it.
                        File.Delete(uri.LocalPath);
                }
                catch { }
            }

            if (this.ValidatePdfOutput)
                EnsurePdfWasProduced(outputFilePath, this.PdfConverterPath, args);
        }

        /// <summary>
        /// Blocking equivalent of <see cref="UriToPdfFileAsync"/>.
        /// </summary>
        public void UriToPdfFile(
            Uri uri,
            string outputFilePath,
            bool tryDeleteInputUriResourceAfterConversion = false
            )
            => SyncRunner.Run(() => UriToPdfFileAsync(
                uri, outputFilePath, tryDeleteInputUriResourceAfterConversion));

        /// <summary>
        /// Converts the resource the uri points at into a PDF and returns a read/write stream
        /// over it. The file is deleted when the stream is closed.
        /// </summary>
        public async Task<FileStream> UriToPdfStreamAsync(
            Uri uri,
            string? pdfTempFilePath = null,
            bool tryDeleteInputUriResourceAfterConversion = false,
            CancellationToken cancellationToken = default
            )
        {
            if (string.IsNullOrWhiteSpace(pdfTempFilePath))
            {
                // get a suitable writable temp file path
                pdfTempFilePath = GetSuitableTempPath();
                if (string.IsNullOrWhiteSpace(pdfTempFilePath))
                    throw new UnauthorizedAccessException(
                        $"Can't find a writable folder to save temporary PDF file, kindly set {nameof(pdfTempFilePath)} parameter pointing to a folder with write access");
                pdfTempFilePath += ".pdf";
            }
            else
            // check if we can write to user defined pdfTempFilePath folder
                if (!new Uri(pdfTempFilePath).GetParentUri().IsWritableFolder())
                throw new UnauthorizedAccessException(
                    $"Unable to write to temp PDF file {pdfTempFilePath}, kindly set {nameof(pdfTempFilePath)} parameter pointing to a folder with write access");

            // every path above either assigned a value or threw
            var outputFilePath = pdfTempFilePath!;

            await UriToPdfFileAsync(
                uri, outputFilePath, tryDeleteInputUriResourceAfterConversion, cancellationToken)
                .ConfigureAwait(false);

            // return a stream to the pdf file
            return new FileStream(outputFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite
                , 4000, FileOptions.DeleteOnClose);
        }

        /// <summary>
        /// Blocking equivalent of <see cref="UriToPdfStreamAsync"/>.
        /// </summary>
        public FileStream UriToPdfStream(
            Uri uri,
            string? pdfTempFilePath = null,
            bool tryDeleteInputUriResourceAfterConversion = false
            )
            => SyncRunner.Run(() => UriToPdfStreamAsync(
                uri, pdfTempFilePath, tryDeleteInputUriResourceAfterConversion));

        #endregion

        #region html -> pdf

        /// <summary>
        /// Converts an HTML file on disk into a PDF file on disk.
        /// </summary>
        public async Task HtmlFileToPdfFileAsync(
            string htmlFilePath,
            string pdfFilePath,
            bool deleteHtmlFileAfterConversion = false,
            CancellationToken cancellationToken = default
            )
        {
            // check if htmlFilePath exists
            if (!File.Exists(htmlFilePath))
                throw new FileNotFoundException($"File {htmlFilePath} not found");

            await UriToPdfFileAsync(
                new Uri(htmlFilePath), pdfFilePath, deleteHtmlFileAfterConversion, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Blocking equivalent of <see cref="HtmlFileToPdfFileAsync"/>.
        /// </summary>
        public void HtmlFileToPdfFile(
            string htmlFilePath,
            string pdfFilePath,
            bool deleteHtmlFileAfterConversion = false
            )
            => SyncRunner.Run(() => HtmlFileToPdfFileAsync(
                htmlFilePath, pdfFilePath, deleteHtmlFileAfterConversion));

        /// <summary>
        /// Converts an HTML string into a PDF file on disk, via a temporary HTML file.
        /// </summary>
        public async Task HtmlToPdfFileAsync(
            string htmlContent,
            string pdfOutputFilePath,
            string? htmlContentTempFilePath = null,
            CancellationToken cancellationToken = default
            )
        {
            if (string.IsNullOrWhiteSpace(htmlContent)) throw new ArgumentNullException(nameof(htmlContent));
            if (string.IsNullOrWhiteSpace(pdfOutputFilePath)) throw new ArgumentNullException(nameof(pdfOutputFilePath));

            if (string.IsNullOrWhiteSpace(htmlContentTempFilePath))
            {
                if (new Uri(AppDomain.CurrentDomain.BaseDirectory).IsWritableFolder())
                    htmlContentTempFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{Guid.NewGuid()}.tmp.html");

                if (string.IsNullOrWhiteSpace(htmlContentTempFilePath)
                    && new Uri(Path.GetTempPath()).IsWritableFolder())
                    htmlContentTempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.tmp.html");
                if (string.IsNullOrWhiteSpace(htmlContentTempFilePath))
                    throw new UnauthorizedAccessException(
                        $"Can't find a writable folder to save temporary HTML file, kindly set {nameof(htmlContentTempFilePath)} parameter pointing to a folder with write access");
            }
            else
                if (!new Uri(htmlContentTempFilePath).GetParentUri().IsWritableFolder())
                    throw new UnauthorizedAccessException(
                        $"Unable to write to temp file {htmlContentTempFilePath}, kindly set {nameof(htmlContentTempFilePath)} parameter pointing to a folder with write access");

            File.WriteAllText(htmlContentTempFilePath, htmlContent);

            await UriToPdfFileAsync(
                new Uri(htmlContentTempFilePath), pdfOutputFilePath, true, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Blocking equivalent of <see cref="HtmlToPdfFileAsync"/>.
        /// </summary>
        public void HtmlToPdfFile(
            string htmlContent,
            string pdfOutputFilePath,
            string? htmlContentTempFilePath = null
            )
            => SyncRunner.Run(() => HtmlToPdfFileAsync(
                htmlContent, pdfOutputFilePath, htmlContentTempFilePath));

        /// <summary>
        /// Converts an HTML string into a PDF and returns a read/write stream over it.
        /// The file is deleted when the stream is closed.
        /// </summary>
        public async Task<FileStream> HtmlToPdfStreamAsync(
            string htmlContent,
            string? tempFolderPath = null,
            CancellationToken cancellationToken = default
            )
        {
            if (string.IsNullOrWhiteSpace(htmlContent)) throw new ArgumentNullException(nameof(htmlContent));

            if (string.IsNullOrWhiteSpace(tempFolderPath))
            {
                tempFolderPath = Path.GetTempPath();
                if (string.IsNullOrWhiteSpace(tempFolderPath))
                    throw new UnauthorizedAccessException(
                        $"Can't find a writable folder to save temporary HTML & PDF files, kindly set {nameof(tempFolderPath)} parameter pointing to a folder with write access");
            }
            else
                if (!new Uri(tempFolderPath).IsWritableFolder())
                throw new UnauthorizedAccessException(
                    $"Unable to write to temp folder {tempFolderPath}, kindly set {nameof(tempFolderPath)} parameter pointing to a folder with write access");

            var tmpId = Guid.NewGuid().ToString();
            var htmlContentTempFilePath = Path.Combine(tempFolderPath, $"{tmpId}.tmp.html");
            var tempPdfFileOutputPath = Path.Combine(tempFolderPath, $"{tmpId}.tmp.pdf");

            File.WriteAllText(htmlContentTempFilePath, htmlContent);

            await UriToPdfFileAsync(
                new Uri(htmlContentTempFilePath), tempPdfFileOutputPath, true, cancellationToken)
                .ConfigureAwait(false);

            return new FileStream(tempPdfFileOutputPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite
                , 4000, FileOptions.DeleteOnClose);
        }

        /// <summary>
        /// Blocking equivalent of <see cref="HtmlToPdfStreamAsync"/>.
        /// </summary>
        public FileStream HtmlToPdfStream(
            string htmlContent,
            string? tempFolderPath = null
            )
            => SyncRunner.Run(() => HtmlToPdfStreamAsync(htmlContent, tempFolderPath));

        #endregion

        #region internals

        /// <summary>
        /// Fills in <see cref="PdfConverterPath"/> and <see cref="PdfConverterParameters"/>
        /// from the well known browser locations for the current OS when they were not set.
        /// </summary>
        private void ResolveConverter()
        {
            if (!string.IsNullOrWhiteSpace(PdfConverterPath)
                &&
                !File.Exists(PdfConverterPath)
                )
                throw new FileNotFoundException($"PDF converter path '{PdfConverterPath}' not found");

            if (string.IsNullOrWhiteSpace(PdfConverterPath))
            {
                if (InteropExt.CurrentOSPlatform == OSPlatform.Windows)
                {
                    // check if edge browser is installed
                    if (File.Exists("C:/Program Files/Microsoft/Edge/Application/msedge.exe"))
                        PdfConverterPath = "C:/Program Files/Microsoft/Edge/Application/msedge.exe";
                    else if (File.Exists("C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe"))
                        PdfConverterPath = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
                    else
                    // check if chrome browser is installed
                    if (File.Exists("C:/Program Files/Google/Chrome/Application/chrome.exe"))
                        PdfConverterPath = "C:/Program Files/Google/Chrome/Application/chrome.exe";
                    else if (File.Exists("C:/Program Files (x86)/Google/Chrome/Application/chrome.exe"))
                        PdfConverterPath = "C:/Program Files (x86)/Google/Chrome/Application/chrome.exe";
                    else
                        // throw exception for missing PDF converter app informing the user they can set it manually
                        // as the default PDF converter app like chrome or edge are not installed
                        throw new MissingFieldException($"Cannot find chrome.exe or msedge.exe in: {Environment.NewLine}"
                        + $" 'C:/Program Files/Google/Chrome/Application/',{Environment.NewLine}"
                        + $" 'C:/Program Files (x86)/Google/Chrome/Application/',{Environment.NewLine}"
                        + $" 'C:/Program Files/Microsoft/Edge/Application/' or {Environment.NewLine}"
                        + " 'C:/Program Files (x86)/Microsoft/Edge/Application/'"
                        + $" Please set '{nameof(PdfConverterPath)}' to chrome.exe or msedge.exe path "
                        + "or to any other PDF CLI converter app.");
                }
                if (InteropExt.CurrentOSPlatform == OSPlatform.Linux)
                {
                    if (File.Exists("/usr/bin/google-chrome"))
                        PdfConverterPath = "/usr/bin/google-chrome";
                    else if (File.Exists("/opt/google/chrome/chrome"))
                        PdfConverterPath = "/opt/google/chrome/chrome";
                    else
                        throw new MissingFieldException($"Cannot find chrome in either"
                        + " '/usr/bin/google-chrome' or"
                        + " '/opt/google/chrome/chrome'"
                        + $" Please set {nameof(PdfConverterPath)} to chrome executable path, or to any other PDF CLI converter app");
                }
                if (InteropExt.CurrentOSPlatform == OSPlatform.OSX)
                {
                    if (File.Exists("/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"))
                        PdfConverterPath = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
                    else
                        throw new MissingFieldException("Cannot find chrome in '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'. "
                                                    + $"Please set {nameof(PdfConverterPath)} to chrome executable path, or to any other PDF CLI converter app");
                }
#if NET8_0_OR_GREATER
                if (InteropExt.CurrentOSPlatform == OSPlatform.FreeBSD)
                {
                    if (File.Exists("/usr/local/bin/chrome"))
                        PdfConverterPath = "/usr/local/bin/chrome";
                    else
                        throw new MissingFieldException("Cannot find chrome in '/usr/local/bin/chrome'. "
                            + $"Please set {nameof(PdfConverterPath)} to chrome executable path, or to any other PDF CLI converter app");
                }
#endif
            }

            if (string.IsNullOrWhiteSpace(PdfConverterPath)
                ||
                !File.Exists(PdfConverterPath)
                ) throw new MissingFieldException($"Please set {nameof(PdfConverterPath)} to chrome executable path, or to any other PDF CLI converter app");

            if (string.IsNullOrWhiteSpace(this.PdfConverterParameters))
                this.PdfConverterParameters = "--headless "
                                            + "--disable-gpu "
                                            + "--log-level=3 "
                                            + "--no-pdf-header-footer "
                                            + "--run-all-compositor-stages-before-draw "
                                            + "--virtual-time-budget=5000 "
                                            + "--print-to-pdf=\"{{output}}\" \"{{input}}\"";
        }

        /// <summary>
        /// Confirms the converter actually left a usable PDF behind. Some converters,
        /// chromium in particular, can fail in ways that produce a zero exit code and no
        /// output at all, so the exit code alone is not enough to trust.
        /// </summary>
        private static void EnsurePdfWasProduced(string outputFilePath, string? converterPath, string? args)
        {
            string Context() =>
                $"{Environment.NewLine}{Environment.NewLine}Converter: {converterPath}"
                + $"{Environment.NewLine}Arguments: {args}";

            if (!File.Exists(outputFilePath))
                throw new InvalidOperationException(
                    $"The PDF converter finished without producing '{outputFilePath}'." + Context());

            if (new FileInfo(outputFilePath).Length == 0)
                throw new InvalidOperationException(
                    $"The PDF converter produced an empty file at '{outputFilePath}'." + Context());

            var header = new byte[5];
            var read = 0;
            using (var stream = File.OpenRead(outputFilePath))
            {
                int chunk;
                while (read < header.Length
                    && (chunk = stream.Read(header, read, header.Length - read)) > 0)
                    read += chunk;
            }

            if (read < header.Length
                || header[0] != (byte)'%'
                || header[1] != (byte)'P'
                || header[2] != (byte)'D'
                || header[3] != (byte)'F'
                || header[4] != (byte)'-')
                throw new InvalidOperationException(
                    $"The file the converter produced at '{outputFilePath}' is not a PDF." + Context());
        }

        private static async Task ConvertAsync(
            string converterPath,
            string? converterArgs,
            System.Collections.Specialized.StringDictionary? environmentVars,
            int timeout,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(converterPath)) throw new ArgumentNullException(nameof(converterPath));
            converterPath = converterPath.UnifyPathSeperator();
            if (!File.Exists(converterPath))
                throw new FileNotFoundException($"Can't find PDF converter at path '{converterPath}'");

            _ = await converterPath.RunCommandAsync(
                    converterArgs ?? "",
                    Path.GetDirectoryName(converterPath),
                    timeout,
                    environmentVars,
                    cancellationToken).ConfigureAwait(false);
        }

        #endregion
    }
}
