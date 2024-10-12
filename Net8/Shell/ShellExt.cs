using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Com.H.Shell;

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
        string? workingDirectory = null,
        int timeout = 60000,
        System.Collections.Specialized.StringDictionary? environmentVariables = null
        )
    {
       
        if (string.IsNullOrWhiteSpace(workingDirectory))
            workingDirectory = AppDomain.CurrentDomain.BaseDirectory;

        var startInfo = new ProcessStartInfo(command, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        if (environmentVariables != null)
        {
            foreach (var key in environmentVariables.Keys)
            {
                var keyStr = key as string;
                if (string.IsNullOrWhiteSpace(keyStr))
                    continue;
                startInfo.EnvironmentVariables[keyStr] = environmentVariables[keyStr];
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            startInfo.LoadUserProfile = true; // Only set this on Windows
        }


        var process = new Process
        {
            StartInfo = startInfo,
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