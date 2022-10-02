using System;
using System.Diagnostics;

namespace Com.H.Shell
{

	public static class ShellExt
	{
		/// <summary>
		/// Run a command in a shell
		/// </summary>
		/// <param name="command"></param>
		/// <param name="args"></param>
		/// <param name="workingDirectory"></param>
		/// <param name="timeout"></param>
		/// <returns></returns>
		public static string RunCommand(
			this string command,
			string args,
			string workingDirectory = null,
			int timeout = 60000
			)
		{
			if (string.IsNullOrWhiteSpace(workingDirectory))
				workingDirectory = AppDomain.CurrentDomain.BaseDirectory;

			var pInfo = new ProcessStartInfo(command, args)
			{
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				WorkingDirectory = workingDirectory,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden
			};

			var process = new Process
			{
				StartInfo = pInfo,
				EnableRaisingEvents = true
			};
			process.Start();
			process.WaitForExit(timeout);
			if (!process.HasExited)
				throw new Exception($"Process {command} {args} did not exit in {timeout} ms");
			var output = process.StandardOutput.ReadToEnd();
			var error = process.StandardError.ReadToEnd();
			if (!string.IsNullOrWhiteSpace(error))
				throw new Exception($"Process {command} {args} exited with exception:\r\n{error}");
			return output;
		}

	}
}