using Com.H.IO;
using Com.H.Net;
using Com.H.Shell;
using Com.H.Text.Template;

namespace Com.H.Pdf
{
    /// <summary>
    /// Convenience entry points for HTML to PDF conversion, backed by a lazily built
    /// <see cref="ExternalPdfConverter"/> configured from the Default* properties below.
    /// </summary>
    public static class PdfExtensions
    {
        private static ExternalPdfConverter? _externalPdfConverter = null;

        private static string? _defaultPdfConverterPath = null;

        /// <summary>
        /// Path to the executable used for conversion. Leave null to let the converter find
        /// Chrome or Edge in the usual locations for the current OS.
        /// </summary>
        public static string? DefaultPdfConverterPath
        {
            get
            {
                return _defaultPdfConverterPath;
            }
            set
            {
                _defaultPdfConverterPath = value;
                _externalPdfConverter = null;
            }
        }

        private static string? _defaultPdfConverterParameters = null;

        /// <summary>
        /// Command line template for the converter, using {{input}} and {{output}} placeholders.
        /// Leave null for the built in chromium defaults.
        /// </summary>
        public static string? DefaultPdfConverterParameters
        {
            get
            {
                return _defaultPdfConverterParameters;
            }
            set
            {
                _defaultPdfConverterParameters = value;
                _externalPdfConverter = null;
            }
        }

        private static System.Collections.Specialized.StringDictionary? _defaultEnvironmentVariables = null;

        /// <summary>
        /// Extra environment variables handed to the converter process.
        /// </summary>
        public static System.Collections.Specialized.StringDictionary? DefaultEnvironmentVariables
        {
            get
            {
                return _defaultEnvironmentVariables;
            }
            set
            {
                _defaultEnvironmentVariables = value;
                _externalPdfConverter = null;
            }
        }

        private static int _defaultConversionTimeoutMs = 60000;

        /// <summary>
        /// How long to wait, in milliseconds, for the converter to finish a single document
        /// before giving up and killing it. Defaults to 60 seconds.
        /// </summary>
        public static int DefaultConversionTimeoutMs
        {
            get
            {
                return _defaultConversionTimeoutMs;
            }
            set
            {
                _defaultConversionTimeoutMs = value;
                _externalPdfConverter = null;
            }
        }

        private static bool _defaultValidatePdfOutput = true;

        /// <summary>
        /// When true (the default) conversions throw if no usable PDF was produced, instead
        /// of failing silently.
        /// </summary>
        public static bool DefaultValidatePdfOutput
        {
            get
            {
                return _defaultValidatePdfOutput;
            }
            set
            {
                _defaultValidatePdfOutput = value;
                _externalPdfConverter = null;
            }
        }

        private static ExternalPdfConverter ExtPdfConv =>
            _externalPdfConverter ??=
                new ExternalPdfConverter()
                {
                    PdfConverterParameters = DefaultPdfConverterParameters,
                    PdfConverterPath = DefaultPdfConverterPath,
                    EnvironmentVariables = DefaultEnvironmentVariables,
                    ConversionTimeoutMs = DefaultConversionTimeoutMs,
                    ValidatePdfOutput = DefaultValidatePdfOutput
                };

        #region plain conversion

        /// <summary>
        /// Converts the document the uri points at into a PDF file on disk.
        /// </summary>
        /// <exception cref="ShellCommandException">The converter exited with a non zero code.</exception>
        /// <exception cref="TimeoutException">The converter did not finish in time.</exception>
        /// <exception cref="InvalidOperationException">No usable PDF was produced.</exception>
        public static Task ToPdfFileAsync(
            this Uri uri,
            string pdfFilePath,
            bool deleteInputFileAfterConversionProcess = false,
            CancellationToken cancellationToken = default
            )
            => ExtPdfConv.UriToPdfFileAsync(
                uri, pdfFilePath, deleteInputFileAfterConversionProcess, cancellationToken);

        /// <summary>
        /// Blocking equivalent of <see cref="ToPdfFileAsync"/>.
        /// </summary>
        public static void ToPdfFile(
            this Uri uri,
            string pdfFilePath,
            bool deleteInputFileAfterConversionProcess = false
            )
            => ExtPdfConv.UriToPdfFile(uri, pdfFilePath, deleteInputFileAfterConversionProcess);

        /// <summary>
        /// Converts the document the uri points at into a PDF and returns a stream over it.
        /// The file is deleted when the stream is closed.
        /// </summary>
        public static Task<FileStream> ToPdfStreamAsync(
            this Uri uri,
            string? pdfTempFilePath = null,
            CancellationToken cancellationToken = default
            )
            => ExtPdfConv.UriToPdfStreamAsync(uri, pdfTempFilePath, true, cancellationToken);

        /// <summary>
        /// Blocking equivalent of <see cref="ToPdfStreamAsync"/>.
        /// </summary>
        public static FileStream ToPdfStream(
            this Uri uri,
            string? pdfTempFilePath = null
            )
            => ExtPdfConv.UriToPdfStream(uri, pdfTempFilePath, true);

        #endregion

        #region templated conversion

        /// <summary>
        /// Renders the template the uri points at against a data model, then converts the
        /// result into a PDF and returns a stream over it.
        /// </summary>
        public static async Task<FileStream> ToRenderedPdfStreamAsync(
            this Uri uri,
            object? dataModel = null,
            string? openMarker = "{{",
            string? closemarker = "}}",
            string? nullReplacement = "null",
            Func<TemplateMultiDataRequest, IEnumerable<dynamic>?>? dataProviders = null,
            CancellationToken? cToken = null
            )
        {
            string htmlContentTempFilePath = ToTempHtmlFile(
                uri,
                dataModel,
                openMarker,
                closemarker,
                nullReplacement,
                dataProviders,
                cToken);

            try
            {
                return await new Uri(htmlContentTempFilePath)
                    .ToPdfStreamAsync(cancellationToken: cToken ?? default)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (File.Exists(htmlContentTempFilePath))
                    try
                    {
                        File.Delete(htmlContentTempFilePath);
                    }
                    catch { }
            }
        }

        /// <summary>
        /// Blocking equivalent of <see cref="ToRenderedPdfStreamAsync"/>.
        /// </summary>
        public static FileStream ToRenderedPdfStream(
            this Uri uri,
            object? dataModel = null,
            string? openMarker = "{{",
            string? closemarker = "}}",
            string? nullReplacement = "null",
            Func<TemplateMultiDataRequest, IEnumerable<dynamic>?>? dataProviders = null,
            CancellationToken? cToken = null
            )
            => SyncRunner.Run(() => uri.ToRenderedPdfStreamAsync(
                dataModel, openMarker, closemarker, nullReplacement, dataProviders, cToken));

        /// <summary>
        /// Renders the template the uri points at against a data model, then converts the
        /// result into a PDF file on disk.
        /// </summary>
        public static async Task ToRenderedPdfFileAsync(
            this Uri uri,
            string pdfOutputFilePath,
            object? dataModel = null,
            string? openMarker = "{{",
            string? closemarker = "}}",
            string? nullReplacement = "null",
            Func<TemplateMultiDataRequest, IEnumerable<dynamic>?>? dataProviders = null,
            CancellationToken? cToken = null
            )
        {
            #region check arguments
            if (uri is null) throw new ArgumentNullException(nameof(uri));

            if (!Uri.IsWellFormedUriString(uri.AbsoluteUri, UriKind.Absolute))
                throw new FormatException(
                    $"Invalid uri format : {uri.AbsoluteUri}");

            if (string.IsNullOrWhiteSpace(pdfOutputFilePath)) throw new ArgumentNullException(nameof(pdfOutputFilePath));
            #endregion

            string htmlContentTempFilePath = ToTempHtmlFile(
                uri,
                dataModel,
                openMarker,
                closemarker,
                nullReplacement,
                dataProviders,
                cToken);

            try
            {
                await new Uri(htmlContentTempFilePath)
                    .ToPdfFileAsync(pdfOutputFilePath, true, cToken ?? default)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (File.Exists(htmlContentTempFilePath))
                    try
                    {
                        File.Delete(htmlContentTempFilePath);
                    }
                    catch { }
            }
        }

        /// <summary>
        /// Blocking equivalent of <see cref="ToRenderedPdfFileAsync"/>.
        /// </summary>
        public static void ToRenderedPdfFile(
            this Uri uri,
            string pdfOutputFilePath,
            object? dataModel = null,
            string? openMarker = "{{",
            string? closemarker = "}}",
            string? nullReplacement = "null",
            Func<TemplateMultiDataRequest, IEnumerable<dynamic>?>? dataProviders = null,
            CancellationToken? cToken = null
            )
            => SyncRunner.Run(() => uri.ToRenderedPdfFileAsync(
                pdfOutputFilePath, dataModel, openMarker, closemarker,
                nullReplacement, dataProviders, cToken));

        /// <summary>
        /// Renders the template the uri points at and writes it to a temporary HTML file.
        /// </summary>
        /// <returns>returns a file path to the rendered HTML content</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        private static string ToTempHtmlFile(
            this Uri uri,
            object? dataModel = null,
            string? openMarker = "{{",
            string? closemarker = "}}",
            string? nullReplacement = "null",
            Func<TemplateMultiDataRequest, IEnumerable<dynamic>?>? dataProviders = null,
            CancellationToken? cToken = null)
        {
            #region check arguments
            if (uri is null) throw new ArgumentNullException(nameof(uri));

            if (!Uri.IsWellFormedUriString(uri.AbsoluteUri, UriKind.Absolute))
                throw new FormatException(
                    $"Invalid uri format : {uri.AbsoluteUri}");
            #endregion

            var htmlContent = uri.RenderContent(dataModel, openMarker, closemarker, nullReplacement, dataProviders, cToken);
            string? htmlContentTempFilePath = null;
            // if the URI is pointing to a local HTML template, see if the temporary rendered html can be written in the same folder as the URI html template.
            if (uri.IsFile && uri.GetParentUri() is not null && uri.GetParentUri().IsWritableFolder())
                // uri.GetParentUri() is not null here
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                htmlContentTempFilePath = Path.Combine(uri.GetParentUri().LocalPath, $"{Guid.NewGuid()}.tmp.html");
#pragma warning restore CS8602 // Dereference of a possibly null reference.

            // if the URI is pointing is either not pointing to a local file or the parent folder is not writable, see if the temporary rendered html can be written in the current application folder.
            if (string.IsNullOrWhiteSpace(htmlContentTempFilePath)
                && new Uri(AppDomain.CurrentDomain.BaseDirectory).IsWritableFolder())
                htmlContentTempFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{Guid.NewGuid()}.tmp.html");
            // if the URI is pointing is either not pointing to a local file, the parent folder is not writable, or the current application folder is not writable, see if the temporary rendered html can be written in the system temp folder.
            if (string.IsNullOrWhiteSpace(htmlContentTempFilePath)
                && new Uri(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.tmp.html")).IsWritableFolder())
                htmlContentTempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.tmp.html");

            if (string.IsNullOrWhiteSpace(htmlContentTempFilePath)
                )
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

            // every path above either assigned a value or threw
            File.WriteAllText(htmlContentTempFilePath, htmlContent);
            return htmlContentTempFilePath!;
        }

        #endregion
    }
}
