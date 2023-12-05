using System;
using System.IO;
using System.Linq;
using System.Timers;
using System.Threading;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace ConsoleAppRestart;

public class AppRestart
{
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);
    private static readonly AppRestart MainApp = new();
    private static readonly Timer Timer = new();
    private CancellationTokenSource? sourceToken;
    private Process? appToRestart = null;
    private string appName = string.Empty;
    private int restartInterval;

    private static void Main()
    {
        MainApp.RestartApp();
        Timer.Dispose();
        Console.WriteLine("Press any key to exit...");
        Reader.ReadLine();
        Environment.Exit(0);
    }

#pragma warning disable MA0011 // IFormatProvider is missing
    private void RestartApp()
    {
        if (!UserInput() || appToRestart is null)
        {
            return;
        }

        sourceToken = new CancellationTokenSource();
        var token = sourceToken.Token;
#if DEBUG
        var restartTime = DateTime.Now.AddSeconds(restartInterval);
#else
        var restartTime = DateTime.Now.AddHours(restartInterval);
#endif
        var restartTimeSpan = restartTime - DateTime.Now;
        var restartString = restartTime.Hour.ToString("D2") + ":" + restartTime.Minute.ToString("D2") + ":" + restartTime.Second.ToString("D2");
        var monitor = MonitorTask(restartTimeSpan, token);
        Console.WriteLine("Application: {0}", appName);
        Console.WriteLine("Restart occurs at: {0}", restartString);
        Console.WriteLine("1. Exit");
        Console.WriteLine();
        _ = Task.Run(() => SetupTimer(restartTimeSpan, token), token);
        while (!sourceToken.IsCancellationRequested)
        {
            if (monitor.IsCompleted)
            {
                if (monitor.IsFaulted)
                {
                    var message = monitor.Exception?.Message;
                    Console.WriteLine("Monitor task faulted with message: {0}", message ?? "Unknown");
                    break;
                }
                Console.WriteLine("Exiting program. Monitor task is stopped.");
                break;
            }
            try
            {
                var optionInput = Reader.ReadLine(30000);
                if (!string.IsNullOrWhiteSpace(optionInput))
                {
                    if (optionInput.Equals("1", StringComparison.Ordinal))
                    {
                        Timer.Stop();
                        Console.WriteLine("Exiting program...");
                        break;
                    }
                    Console.WriteLine("Invalid option {0}. Please select a valid option.", optionInput);
                }
                else
                {
                    Console.WriteLine("No option selected. Please select a valid option.");

                }
                Console.WriteLine("1. Exit");
            }
            catch
            {
                // ignored
            }
        }
        sourceToken.Cancel();
    }
#pragma warning restore MA0011 // IFormatProvider is missing

    private bool UserInput()
    {
        Console.WriteLine("Enter the name of the process you want to search for:");
        appName = Console.ReadLine()?.Trim() ?? string.Empty;
        appToRestart = FindProcess(appName);
        if (appToRestart?.MainModule?.FileVersionInfo.ProductName is null)
        {
            Console.WriteLine("Process not found.");
            return false;
        }
        appName = appToRestart.MainModule.FileVersionInfo.ProductName;
        Console.WriteLine("Enter the restart interval in hours:");
        while (true)
        {
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("Invalid input. Please enter a valid number.");
            }
            else if (int.TryParse(input, CultureInfo.InvariantCulture, out restartInterval))
            {
                break;
            }
        }

        return true;
    }


    private async Task MonitorTask(TimeSpan restartTimeSpan, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(restartTimeSpan, token).ConfigureAwait(false);

            if (token.IsCancellationRequested)
            {
                return;
            }

            appToRestart = FindProcess(appName);
            if (appToRestart?.MainModule?.FileVersionInfo.ProductName is null || FindProcessId(appToRestart.ProcessName.Trim()) is 0)
            {
                Console.WriteLine("Program {0} isn't running.", appName);
                sourceToken?.Cancel();
                return;
            }

            var workingDir = Path.GetDirectoryName(appToRestart.MainModule.FileName)
                             ?? Path.GetPathRoot(appToRestart.MainModule.FileName)!;
            Process newApp;
            if (appToRestart.MainModule.FileVersionInfo.ProductName.Contains("discord", StringComparison.OrdinalIgnoreCase))
            {
                var lastSeparator = workingDir.LastIndexOf(Path.DirectorySeparatorChar);
                workingDir = lastSeparator != -1 ? workingDir[..lastSeparator] : workingDir;
                var discordUpdaterPath = workingDir + Path.DirectorySeparatorChar + "Update.exe";
                newApp = new()
                {
                    StartInfo = new(discordUpdaterPath)
                    {
                        WorkingDirectory = workingDir,
                        Arguments = "--processStart Discord.exe",
                        UseShellExecute = false,
                    },
                };
            }
            else
            {
                newApp = new()
                {
                    StartInfo = new(appToRestart.MainModule.FileName)
                    {
                        WorkingDirectory = workingDir,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                    },
                };
            }
            appToRestart.Kill();
            await appToRestart.WaitForExitAsync(token).ConfigureAwait(false);
            Console.WriteLine("{0} was killed, a new instance is starting...", appName);
            newApp.Start();
        }
    }

    private static Process? FindProcess(string processName)
    {
        var processList = Process.GetProcesses();
        return (from process in processList
                where process.ProcessName.Trim().Equals(processName, StringComparison.OrdinalIgnoreCase)
                select process).FirstOrDefault();
    }

    private static int FindProcessId(string processName)
    {
        var processList = Process.GetProcesses();
        return (from process in processList
                where process.ProcessName.Trim().Equals(processName, StringComparison.OrdinalIgnoreCase)
                select process.Id).FirstOrDefault();
    }

    private static void SetupTimer(TimeSpan restartTimeSpan, CancellationToken ct)
    {
        var timeToWait = restartTimeSpan;
        var originPos = Console.GetCursorPosition();
        Timer.Interval = OneSecond.TotalMilliseconds;
        Timer.AutoReset = true;
        Timer.Elapsed += (sender, e)
            => HandleTimer(sender, e, ref timeToWait, restartTimeSpan, originPos, ct);
        Timer.Start();
    }

#pragma warning disable S1172 // Unused method parameters should be removed
#pragma warning disable IDE0060 // Remove unused parameter
    private static void HandleTimer(
        object? sender,
        ElapsedEventArgs e,
        ref TimeSpan timeToWait,
        TimeSpan restartTimeSpan,
        (int Left, int Top) originPos,
        CancellationToken ct)
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning restore S1172 // Unused method parameters should be removed
    {
        var timeFormat = timeToWait.TotalDays > 9 ? @"dd\:hh\:mm\:ss" :
                         timeToWait.TotalDays < 1 || (int)timeToWait.TotalHours == 24 ? @"hh\:mm\:ss" : @"d\:hh\:mm\:ss";
        if (!ct.IsCancellationRequested)
        {
            var currentPos = Console.GetCursorPosition();
            if (currentPos != originPos)
            {
                Console.SetCursorPosition(originPos.Left, originPos.Top);
                Console.Write("\rTime until restart: {0}", timeToWait.ToString(timeFormat));
                Console.SetCursorPosition(currentPos.Left, currentPos.Top);
            }
            else
            {
                Console.SetCursorPosition(originPos.Left, originPos.Top);
                Console.Write("\rTime until restart: {0}", timeToWait.ToString(timeFormat));
                Console.SetCursorPosition(currentPos.Left, currentPos.Top + 1);
            }
            timeToWait = timeToWait.Subtract(OneSecond);
            if (timeToWait.TotalSeconds <= 0)
            {
                timeToWait = restartTimeSpan;
            }
        }
    }
}

