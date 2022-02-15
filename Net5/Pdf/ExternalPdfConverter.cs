using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
        /// Example a path to wkhtmltopdf.exe
        /// For use with wkhtmltopdf tool
        /// </summary>
        public string PdfConverterPath { get; set; }


        /// <summary>
        /// Example "--print-media-type  --load-error-handling ignore "\"{{input}}\" \"{{output}}\""
        /// for use with wkhtmltopdf tool
        /// </summary>
        public string PdfConverterParameters { get; set; }


        public FileStream HtmlToPdf(
            string content,
            string tempFilePath = null
            )
        {
            HtmlToPdfFile(content,
                tempFilePath);

            return new FileStream(tempFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite
                , 512, FileOptions.DeleteOnClose);
        }


        public void HtmlToPdfFile(
            string content,
            string outputFilePath,
            string tempFilePath = null
            )
        {
            if (string.IsNullOrEmpty(content)) throw new ArgumentNullException(nameof(content));
            if (string.IsNullOrEmpty(outputFilePath)) throw new ArgumentNullException(nameof(outputFilePath));

            if (tempFilePath is null) tempFilePath = $"{Path.GetTempFileName()}.html";
            File.WriteAllText(tempFilePath, content);

            var args = PdfConverterParameters
                .Replace("{{input}}", tempFilePath)
                .Replace("{{output}}", outputFilePath);
            
            if (InteropExt.CurrentOSPlatform == OSPlatform.Windows)
                ConvertWin(PdfConverterPath, args);
            else throw new NotSupportedException("Current OS platform is not supported at the moment");

            File.Delete(tempFilePath);

        }

        private static void ConvertWin(string converterPath, string converterArgs)
        {
            var pInfo = new ProcessStartInfo(converterPath, converterArgs)
            {
                UseShellExecute = false
            };
            int oldMode = SetErrorMode(3);
            Process p = Process.Start(pInfo);
            _ = SetErrorMode(oldMode);
            p.WaitForExit(60000);
        }

    }


}
