using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using Com.H.IO;
using Com.H.Net;
using Com.H.Runtime.InteropServices;
using Com.H.Shell;
using Com.H.Text;

namespace Com.H.Pdf
{
	/// <summary>
	/// Converts HTML to PDF using an external PDF converter.
	/// by default, the converter looks up for chrome executable in default expected locations of different operating systems:
    /// Windows: C:\Program Files (x86)\Google\Chrome\Application\chrome.exe
    ///          or 
    ///          C:\Program Files\Google\Chrome\Application\chrome.exe
    /// Linux: /usr/bin/google-chrome
    ///        or
    ///        opt/google/chrome/chrome
    /// MacOS: /Applications/Google Chrome.app/Contents/MacOS/Google Chrome
    /// FreeBSD: /usr/local/bin/chromium
	/// </summary>

	public class ExternalPdfConverter
	{

		// [DllImport("kernel32.dll", SetLastError = true)]
		// static extern int SetErrorMode(int wMode);

		/// <summary>
		/// Example: a path to chrome executable (or any other executable that can convert HTML to PDF)
		/// On windows chrome usually installed under c:\Program Files\Google\Chrome\Application\chrome.exe
		/// On Linux, chrome usually located at opt/google/chrome/chrome
		/// </summary>
		public string? PdfConverterPath { get; set; }

		/// <summary>
		/// Example "--headless --print-to-pdf-no-header --run-all-compositor-stages-before-draw --print-to-pdf=\"{{output}}\" \"{{input}}\""
		/// for use with chrome.exe
		/// </summary>
		public string? PdfConverterParameters { get; set; }

        public FileStream UriToPdfStream(
            Uri uri, 
            string? pdfTempFilePath = null,
            CancellationToken? cToken = null,
            bool tryDeleteInputUriResourceAfterConversion = false
            )
        {
            if (string.IsNullOrWhiteSpace(pdfTempFilePath)
                )
            {

                if (new Uri(Path.GetTempPath()).IsWritableFolder())
                    pdfTempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.tmp.pdf");

                if (string.IsNullOrWhiteSpace(pdfTempFilePath)
                    &&
                    new Uri(AppDomain.CurrentDomain.BaseDirectory).IsWritableFolder())
                    pdfTempFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{Guid.NewGuid()}.tmp.pdf");

                if (string.IsNullOrWhiteSpace(pdfTempFilePath))
                    throw new UnauthorizedAccessException(
                        $"Can't find a writable folder to save temporary PDF file, kindly set {nameof(pdfTempFilePath)} parameter pointing to a folder with write access");
            }
            else
                if (!new Uri(pdfTempFilePath).GetParentUri().IsWritableFolder())
                throw new UnauthorizedAccessException(
                    $"Unable to write to temp PDF file {pdfTempFilePath}, kindly set {nameof(pdfTempFilePath)} parameter pointing to a folder with write access");


            this.UriToPdfFile(uri, pdfTempFilePath, cToken, tryDeleteInputUriResourceAfterConversion);

            return new FileStream(pdfTempFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite
                , 4000, FileOptions.DeleteOnClose);
        }
        public FileStream HtmlToPdfStream(
			string htmlContent,
			string? tempFolderPath = null,
            CancellationToken? cToken = null
			)
		{

            if (string.IsNullOrWhiteSpace(htmlContent)) throw new ArgumentNullException(nameof(htmlContent));

            if (string.IsNullOrWhiteSpace(tempFolderPath))
            {
                if (new Uri(AppDomain.CurrentDomain.BaseDirectory).IsWritableFolder())
                    tempFolderPath = AppDomain.CurrentDomain.BaseDirectory;

                if (string.IsNullOrWhiteSpace(tempFolderPath)
                    && new Uri(Path.GetTempPath()).IsWritableFolder())
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

            this.UriToPdfFile(new Uri(htmlContentTempFilePath), tempPdfFileOutputPath, cToken, true);

			return new FileStream(tempPdfFileOutputPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite
				, 4000, FileOptions.DeleteOnClose);
		}

        
        public void HtmlFileToPdfFile(
            string htmlFilePath,
            string pdfFilePath,
            CancellationToken? cToken = null,
            bool deleteHtmlFileAfterConversion = false
            )
        {
            // check if htmlFilePath exists
            if (!File.Exists(htmlFilePath))
                throw new FileNotFoundException($"File {htmlFilePath} not found");

            this.UriToPdfFile(new Uri(htmlFilePath), pdfFilePath, cToken, deleteHtmlFileAfterConversion);
        }

        // root implementation
        public void UriToPdfFile(
            Uri uri,
            string outputFilePath,
            CancellationToken? token = null,
            bool tryDeleteInputUriResourceAfterConversion = false
            )
        {
            #region check arguments
            if (uri is null) throw new ArgumentNullException(nameof(uri));
            if (!Uri.IsWellFormedUriString(uri.AbsoluteUri, UriKind.Absolute))
                throw new FormatException(
                    $"Invalid uri format : {uri.AbsoluteUri}");

            if (string.IsNullOrWhiteSpace(outputFilePath)) throw new ArgumentNullException(nameof(outputFilePath));
            //if (!new Uri(outputFilePath).GetParentUri().IsWritableFolder())
            //    throw new ArgumentException($"Output file path '{outputFilePath}' is not writable");


            #endregion

            #region check platform specific default PDF conversion tool if one isn't specified

            if (InteropExt.CurrentOSPlatform == OSPlatform.Windows)
            {
                if (File.Exists("C:/Program Files/Google/Chrome/Application/chrome.exe"))
                    PdfConverterPath = "C:/Program Files/Google/Chrome/Application/chrome.exe";
                else if (File.Exists("C:/Program Files (x86)/Google/Chrome/Application/chrome.exe"))
                    PdfConverterPath = "C:/Program Files (x86)/Google/Chrome/Application/chrome.exe";
                else
                    throw new MissingFieldException("Cannot find chrome.exe in either"
                    + " 'C:/Program Files/Google/Chrome/Application/' or"
                    + " 'C:/Program Files (x86)/Google/Chrome/Application/'"
                    + $" Please set {nameof(PdfConverterPath)} to chrome.exe path, or to any other PDF CLI converter app.");
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
            if (InteropExt.CurrentOSPlatform == OSPlatform.FreeBSD)
            {
                if (File.Exists("/usr/local/bin/chrome"))
                    PdfConverterPath = "/usr/local/bin/chrome";
                else
                    throw new MissingFieldException("Cannot find chrome in '/usr/local/bin/chrome'. "
                        + $"Please set {nameof(PdfConverterPath)} to chrome executable path, or to any other PDF CLI converter app");
            }
            
            if (string.IsNullOrWhiteSpace(PdfConverterPath)
                ||
                !File.Exists(PdfConverterPath)
                ) throw new MissingFieldException($"Please set {nameof(PdfConverterPath)} to chrome executable path, or to any other PDF CLI converter app");

            if (string.IsNullOrWhiteSpace(this.PdfConverterParameters))
                this.PdfConverterParameters = "--headless "
                                            + "--disable-gpu "
                                            + "--log-level=3 "
                                            // + "--disable-logging "
                                            // + "--silent "
                                            // + "--disable-software-rasterizer "
                                            // + "--no-sandbox "
                                            // + "--ignore-gpu-blocklist "
                                            // + "--enable-webgl-developer-extensions "
                                            // + "--enable-webgl-draft-extensions "
                                            + "--print-to-pdf-no-header --run-all-compositor-stages-before-draw "
                                            + "--print-to-pdf=\"{{output}}\" \"{{input}}\"";

            #endregion

            var args = PdfConverterParameters?
                .Replace("{{input}}", uri.AbsoluteUri)
                .Replace("{{output}}", outputFilePath);

            try
            {

                var parentDirectory = Path.GetDirectoryName(outputFilePath);
                if (!string.IsNullOrWhiteSpace(parentDirectory)
                    && !Directory.Exists(parentDirectory))
                    Directory.CreateDirectory(parentDirectory);


                Convert(this.PdfConverterPath, args);
                
            }
            catch (Exception ex)
            {
                // supresses chrome unsuppressable info messages that get printed to stderr
                // and only raises an exception for actual error messages
                if (this.PdfConverterPath?.ContainsIgnoreCase("chrome") == true
                    &&
                    ex.Message.Contains(":ERROR:"))
                    throw;
            }
            finally
            {

                try
                {
                    if (tryDeleteInputUriResourceAfterConversion
                        && uri.IsFile
                        )
                        File.Delete(uri.AbsoluteUri);
                }
                catch { }
            }

        }

        public void HtmlToPdfFile(
            string htmlContent,
            string pdfOutputFilePath,
            string? htmlContentTempFilePath = null,
            CancellationToken? cToken = null
            )
        {
            if (string.IsNullOrWhiteSpace(htmlContent)) throw new ArgumentNullException(nameof(htmlContent));
            if (string.IsNullOrWhiteSpace(pdfOutputFilePath)) throw new ArgumentNullException(nameof(pdfOutputFilePath));

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

            this.UriToPdfFile(new Uri(htmlContentTempFilePath), pdfOutputFilePath, cToken, true);

        }



        // root implementation to be deleted
        public void HtmlToPdfFileDepricated(
			string htmlContent,
			string outputFilePath,
			string? htmlContentTempFilePath = null
			)
		{
			if (string.IsNullOrWhiteSpace(htmlContent)) throw new ArgumentNullException(nameof(htmlContent));
			if (string.IsNullOrWhiteSpace(outputFilePath)) throw new ArgumentNullException(nameof(outputFilePath));
			if (string.IsNullOrWhiteSpace(PdfConverterPath))
			{
				if (InteropExt.CurrentOSPlatform == OSPlatform.Windows)
				{
					if (File.Exists("C:/Program Files/Google/Chrome/Application/chrome.exe"))
						PdfConverterPath = "C:/Program Files/Google/Chrome/Application/chrome.exe";
					else if (File.Exists("C:/Program Files (x86)/Google/Chrome/Application/chrome.exe"))
						PdfConverterPath = "C:/Program Files (x86)/Google/Chrome/Application/chrome.exe";
					else
						throw new MissingFieldException("Cannot find chrome.exe in either"
						+ " 'C:/Program Files/Google/Chrome/Application/' or"
						+ " 'C:/Program Files (x86)/Google/Chrome/Application/'"
						+ $" Please set {nameof(PdfConverterPath)} to chrome.exe path, or to any other PDF CLI converter app.");
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
				if (InteropExt.CurrentOSPlatform == OSPlatform.FreeBSD)
				{
					if (File.Exists("/usr/local/bin/chrome"))
						PdfConverterPath = "/usr/local/bin/chrome";
					else
						throw new MissingFieldException("Cannot find chrome in '/usr/local/bin/chrome'. "
                            + $"Please set {nameof(PdfConverterPath)} to chrome executable path, or to any other PDF CLI converter app");
				}
				if (string.IsNullOrWhiteSpace(this.PdfConverterParameters))
					this.PdfConverterParameters = "--headless "
                                                + "--disable-gpu "
                                                + "--log-level=3 "
                                                // + "--disable-logging "
                                                // + "--silent "
                                                // + "--disable-software-rasterizer "
                                                // + "--no-sandbox "
                                                // + "--ignore-gpu-blocklist "
                                                // + "--enable-webgl-developer-extensions "
                                                // + "--enable-webgl-draft-extensions "
                                                + "--print-to-pdf-no-header --run-all-compositor-stages-before-draw "
                                                + "--print-to-pdf=\"{{output}}\" \"{{input}}\"";

			}

            // check if PdfConverterPath is not null or white space
            if (string.IsNullOrWhiteSpace(PdfConverterPath))
                throw new MissingFieldException($"Please set {nameof(PdfConverterPath)} to chrome executable path, or to any other PDF CLI converter app");
                

			if (string.IsNullOrWhiteSpace(htmlContentTempFilePath)) htmlContentTempFilePath = $"{IOExtensions.GetTempFilePath()}.html";
			File.WriteAllText(htmlContentTempFilePath, htmlContent);

			var args = PdfConverterParameters?
				.Replace("{{input}}", htmlContentTempFilePath)
				.Replace("{{output}}", outputFilePath);

			try
			{
				var parentDirectory = Path.GetDirectoryName(outputFilePath);
				if (!string.IsNullOrWhiteSpace(parentDirectory)
					&& !Directory.Exists(parentDirectory))
					Directory.CreateDirectory(parentDirectory);
				Convert(this.PdfConverterPath, args);
			}
			catch(Exception ex)
			{
                // supresses chrome unsuppressable info messages that get printed to stderr
                // and only raises an exception for actual error messages
                if (this.PdfConverterPath?.ContainsIgnoreCase("chrome") == true
                    &&
                    ex.Message.Contains(":ERROR:"))
				    throw;
			}
			finally
			{

				try
				{
					File.Delete(htmlContentTempFilePath);
				}
				catch { }
			}


		}

		// private static void ConvertWin(string? converterPath, string? converterArgs)
		// {
		//     if (string.IsNullOrWhiteSpace(converterPath)) throw new ArgumentNullException(nameof(converterPath));
		//     converterPath = converterPath.UnifyPathSeperator();
		//     if (!File.Exists(converterPath)) 
		//         throw new FileNotFoundException($"Can't find PDF converter at path '{converterPath}'");
		//     var pInfo = new ProcessStartInfo(converterPath, converterArgs??"")
		//     {
		//         UseShellExecute = false,
		//         WorkingDirectory = Path.GetDirectoryName(converterPath)
		//     };
		//     int oldMode = SetErrorMode(3);
		//     var p = Process.Start(pInfo);
		//     _ = SetErrorMode(oldMode);
		//     p?.WaitForExit(60000);
		// }

		private static void Convert(string converterPath, string? converterArgs)
		{
			if (string.IsNullOrWhiteSpace(converterPath)) throw new ArgumentNullException(nameof(converterPath));
			converterPath = converterPath.UnifyPathSeperator();
			if (!File.Exists(converterPath))
				throw new FileNotFoundException($"Can't find PDF converter at path '{converterPath}'");
			_ = converterPath.RunCommand(converterArgs ?? "", Path.GetDirectoryName(converterPath));
		}



	}


}
