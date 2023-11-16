using Com.H.IO;
using Com.H.Net;
using Com.H.Text.Template;


namespace Com.H.Pdf
{
    public static class PdfExtensions
    {
        private static ExternalPdfConverter? _externalPdfConverter = null;
        public static string? DefaultPdfConverterPath { get; set; }
        public static string? DefaultPdfConverterParameters { get; set; }
        private static ExternalPdfConverter ExtPdfConv => 
            _externalPdfConverter ??= 
                new ExternalPdfConverter() 
                { 
                    PdfConverterParameters = DefaultPdfConverterParameters, 
                    PdfConverterPath = DefaultPdfConverterPath 
                };

        public static void ToPdfFile(
            this Uri uri, 
            string pdfFilePath,
            bool deleteInputFileAfterConversionProcess = false
            )
            => ExtPdfConv.UriToPdfFile(uri, pdfFilePath, deleteInputFileAfterConversionProcess);
        public static FileStream ToPdfStream(
            this Uri uri, 
            string? pdfTempFilePath = null
            )
            => ExtPdfConv.UriToPdfStream(uri, pdfTempFilePath, true);


        public static FileStream ToRenderedPdfStream(
            this Uri uri,
            object? dataModel = null,
            string? openMarker = "{{",
            string? closemarker = "}}",
            string? nullReplacement = "null",
            Func<TemplateMultiDataRequest, IEnumerable<dynamic>?>? dataProviders = null,
            CancellationToken? cToken = null
            // string? pdfTempOutputFilePath = null
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

                return new Uri(htmlContentTempFilePath)
                    .ToPdfStream();
            }
            catch
            {
                throw;
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
        /// 
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="dataModel"></param>
        /// <param name="openMarker"></param>
        /// <param name="closemarker"></param>
        /// <param name="nullReplacement"></param>
        /// <param name="dataProviders"></param>
        /// <param name="cToken"></param>
        /// <returns>returns a file path to the rendered HTML content</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        private static string ToTempHtmlFile(
            this Uri uri, 
            // string pdfOutputFilePath, 
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

            File.WriteAllText(htmlContentTempFilePath, htmlContent);
            return htmlContentTempFilePath;
        }

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
                new Uri(htmlContentTempFilePath).ToPdfFile(pdfOutputFilePath, true);
            }
            catch
            {
                throw;
            }
            finally
            {
                {
                    if (File.Exists(htmlContentTempFilePath))
                        try
                        {
                            File.Delete(htmlContentTempFilePath);
                        }
                        catch { }
                }
            }

        }

    }
}
