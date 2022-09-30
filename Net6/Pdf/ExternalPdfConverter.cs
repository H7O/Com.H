using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Com.H.IO;
using Com.H.Runtime.InteropServices;


namespace Com.H.Pdf
{
    public class ExternalPdfConverter
    {
        /// <summary>
        /// Linux support coming soon.
        /// </summary>
        /// <param name="wMode"></param>
        /// <returns></returns>

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int SetErrorMode(int wMode);

        /// <summary>
        /// Example: a path to chrome.exe
        /// On windows it's usually c:\Program Files\Google\Chrome\Application\chrome.exe
        /// On Linux, it's usually opt/google/chrome/chrome
        /// </summary>
        public string? PdfConverterPath { get; set; }

        /// <summary>
        /// Example "--headless --print-to-pdf-no-header --run-all-compositor-stages-before-draw --print-to-pdf=\"{{output}}\" \"{{input}}\""
        /// for use with chrome.exe
        /// </summary>
        public string? PdfConverterParameters { get; set; }


        public FileStream HtmlToPdf(
            string htmlContent,
            string? tempFilePath = null
            )
        {
            if (string.IsNullOrWhiteSpace(tempFilePath))
                tempFilePath = $"{Path.GetTempFileName()}.pdf";
            
            string htmlContentTempFilePath = tempFilePath + ".html";


            HtmlToPdfFile(htmlContent,
                tempFilePath, 
                htmlContentTempFilePath);

            return new FileStream(tempFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite
                , 4000, FileOptions.DeleteOnClose);
        }


        public void HtmlToPdfFile(
            string htmlContent,
            string? outputFilePath,
            string? htmlContentTempFilePath = null
            )
        {
            if (string.IsNullOrWhiteSpace(htmlContent)) throw new ArgumentNullException(nameof(htmlContent));
            if (string.IsNullOrWhiteSpace(outputFilePath)) throw new ArgumentNullException(nameof(outputFilePath));
            if (string.IsNullOrWhiteSpace(PdfConverterPath)) throw new MissingFieldException(nameof(PdfConverterPath));

            if (string.IsNullOrWhiteSpace(htmlContentTempFilePath)) htmlContentTempFilePath = $"{Path.GetTempFileName()}.html";
            File.WriteAllText(htmlContentTempFilePath, htmlContent);

            var args = PdfConverterParameters?
                .Replace("{{input}}", htmlContentTempFilePath)
                .Replace("{{output}}", outputFilePath);
            
            if (InteropExt.CurrentOSPlatform == OSPlatform.Windows)
                ConvertWin(this.PdfConverterPath, args);
            else throw new NotSupportedException("Current OS platform is not supported at the moment");

            File.Delete(htmlContentTempFilePath);

        }

        private static void ConvertWin(string? converterPath, string? converterArgs)
        {
            if (string.IsNullOrWhiteSpace(converterPath)) throw new ArgumentNullException(nameof(converterPath));
            converterPath = converterPath.UnifyPathSeperator();
            if (!File.Exists(converterPath)) 
                throw new FileNotFoundException($"Can't find PDF converter at path '{converterPath}'");
            var pInfo = new ProcessStartInfo(converterPath, converterArgs??"")
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(converterPath)
            };
            int oldMode = SetErrorMode(3);
            var p = Process.Start(pInfo);
            _ = SetErrorMode(oldMode);
            p?.WaitForExit(60000);
        }

    }


}
