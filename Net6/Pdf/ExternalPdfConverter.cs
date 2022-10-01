using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Com.H.IO;
using Com.H.Runtime.InteropServices;
using Com.H.Shell;
using Com.H.Text;

namespace Com.H.Pdf
{
	/// <summary>
	/// Converts HTML to PDF using an external PDF converter.
	/// by default, the converter looks up for chrome.exe in default expected locations of the following operating systems:
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

			if (string.IsNullOrWhiteSpace(htmlContentTempFilePath)) htmlContentTempFilePath = $"{IOExtensions.GetTempFilePath()}.html";
			File.WriteAllText(htmlContentTempFilePath, htmlContent);

			var args = PdfConverterParameters?
				.Replace("{{input}}", htmlContentTempFilePath)
				.Replace("{{output}}", outputFilePath);

			try
			{
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

		private static void Convert(string? converterPath, string? converterArgs)
		{
			if (string.IsNullOrWhiteSpace(converterPath)) throw new ArgumentNullException(nameof(converterPath));
			converterPath = converterPath.UnifyPathSeperator();
			if (!File.Exists(converterPath))
				throw new FileNotFoundException($"Can't find PDF converter at path '{converterPath}'");
			_ = converterPath.RunCommand(converterArgs ?? "", Path.GetDirectoryName(converterPath));
		}



	}


}
