using Com.H.IO;
using Com.H.Net;
using Com.H.Text.Template;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

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
            CancellationToken? cToken =null,
            bool deleteInputFileAfterConversionProcess = false
            )
            => ExtPdfConv.UriToPdfFile(uri, pdfFilePath, cToken, deleteInputFileAfterConversionProcess);
        public static FileStream ToPdfStream(
            this Uri uri, 
            CancellationToken? cToken = null,
            string? pdfTempFilePath = null
            )
            => ExtPdfConv.UriToPdfStream(uri, pdfTempFilePath, cToken, true);


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

            //            if (string.IsNullOrWhiteSpace(pdfTempOutputFilePath)
            //                &&
            //                    uri.IsFile
            //                    && uri.GetParentUri() is not null
            //                    && uri.GetParentUri().IsWritableFolder()
            //                )
            //            {
            //                // uri.GetParentUri() is not null here
            //#pragma warning disable CS8602 // Dereference of a possibly null reference.
            //                pdfTempOutputFilePath = Path.Combine(uri.GetParentUri().LocalPath, $"{new Guid().ToString()}.tmp.pdf");
            //#pragma warning restore CS8602 // Dereference of a possibly null reference.
            //            }



            //            if (string.IsNullOrWhiteSpace(pdfTempOutputFilePath) 
            //                && new Uri(AppDomain.CurrentDomain.BaseDirectory).IsWritableFolder())
            //                pdfTempOutputFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{Guid.NewGuid().ToString()}.tmp.pdf");

            //            if (string.IsNullOrWhiteSpace(pdfTempOutputFilePath)
            //                && new Uri(Path.GetTempPath()).IsWritableFolder())
            //                pdfTempOutputFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid().ToString()}.tmp.pdf");

            //            if (string.IsNullOrWhiteSpace(pdfTempOutputFilePath))
            //                throw new Exception("Could not determine a writable folder to create a temporary file for the PDF output.");

            try
            {

                return new Uri(htmlContentTempFilePath)
                    .ToPdfStream(cToken);
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
                htmlContentTempFilePath = Path.Combine(uri.GetParentUri().LocalPath, $"{Guid.NewGuid()}.tmp.html");

            if (string.IsNullOrWhiteSpace(htmlContentTempFilePath)
                && new Uri(AppDomain.CurrentDomain.BaseDirectory).IsWritableFolder())
                htmlContentTempFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{Guid.NewGuid()}.tmp.html");

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
                new Uri(htmlContentTempFilePath).ToPdfFile(pdfOutputFilePath, cToken, true);
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
