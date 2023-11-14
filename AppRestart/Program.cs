using System.Diagnostics;
using System.Timers;
using Timer = System.Timers.Timer;

namespace AppRestart;

public class AppRestart
{
    private static readonly AppRestart MainApp = new();
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);
    private Process? appToRestart;
    private string appName = string.Empty;
    private int restartInterval;
    private static readonly Timer Timer = new();

    private AppRestart()
    {
        appToRestart = null;
    }

    private static void Main()
    {
        MainApp.RestartApp();
        Timer.Dispose();
        Console.WriteLine("Press any key to exit...");
        Reader.ReadLine();
        Environment.Exit(0);
    }

    private void RestartApp()
    {
        if (!UserInput() || appToRestart is null)
        {
            return;
        }

        //var stopWatch = new Stopwatch();
        //stopWatch.Start();
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        var restartTime = DateTime.Now.AddHours(restartInterval);
        var restartTimeSpan = restartTime - DateTime.Now;
        var restartString = restartTime.Hour.ToString("D2") + ":" + restartTime.Minute.ToString("D2") + ":" + restartTime.Second.ToString("D2");
        var monitor = MonitorTask(restartTimeSpan, token);
        Console.WriteLine("Application: {0}", appName);
        Console.WriteLine("Restart occurs at: {0}", restartString);
        Console.WriteLine("1. Exit");
        Console.WriteLine();
        _ = Task.Run(() => SetupTimer(restartTimeSpan, token), token);
        //SetupTimer(restartTimeSpan, token);
        //_ = Countdown(restartTimeSpan, stopWatch, token);
        while (true)
        {
            if (monitor.IsCompleted)
            {
                Console.WriteLine("Exiting program. Monitor task is stopped.");
                break;
            }
            try
            {
                var optionInput = Reader.ReadLine(30000);
                if (!string.IsNullOrWhiteSpace(optionInput))
                {
                    if (optionInput.Equals("1"))
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
        cts.Cancel();
    }

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
            else if (int.TryParse(input, out restartInterval))
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
            await Task.Delay(restartTimeSpan, token);

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (appToRestart?.MainModule is null || FindProcessId(appToRestart.ProcessName.Trim()) is 0)
            {
                Console.WriteLine("Program {0} isn't running.", appName);
                return;
            }

            var workingDir = Path.GetDirectoryName(appToRestart.MainModule.FileName)
                             ?? Path.GetPathRoot(appToRestart.MainModule.FileName)!;
            var newApp = new Process
            {
                StartInfo = new(appToRestart.MainModule.FileName)
                {
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                },
            };
            appToRestart.Kill();
            await appToRestart.WaitForExitAsync(token);
            appToRestart = newApp;
            appToRestart.Start();
        }
    }

    private static Process? FindProcess(string processName)
    {
        var processList = Process.GetProcesses();
        return (from process in processList
                where process.ProcessName.Trim().ToLower().Equals(processName.ToLower())
                select process).FirstOrDefault();
    }

    private static int FindProcessId(string processName)
    {
        var processList = Process.GetProcesses();
        return (from process in processList
                where process.ProcessName.Trim().Equals(processName)
                select process.Id).FirstOrDefault();
    }

    private static async Task Countdown(TimeSpan restartTimeSpan, Stopwatch stopWatch, CancellationToken ct)
    {
        var timeToWait = restartTimeSpan;
        var timeFormat = timeToWait.TotalDays > 9 ? @"dd\:hh\:mm\:ss" :
                         timeToWait.TotalDays < 1 || (int)timeToWait.TotalHours == 24 ? @"hh\:mm\:ss" : @"d\:hh\:mm\:ss";
        var originPos = Console.GetCursorPosition();
        while (!ct.IsCancellationRequested)
        {
            var currentPos = Console.GetCursorPosition();
            if (currentPos != originPos)
            {
                Console.SetCursorPosition(originPos.Left, originPos.Top);
                Console.Write("\rTime until restart: {0}\n", timeToWait.ToString(timeFormat));
                Console.SetCursorPosition(currentPos.Left, currentPos.Top);
            }
            else
            {
                Console.SetCursorPosition(originPos.Left, originPos.Top);
                Console.Write("\rTime until restart: {0}\n", timeToWait.ToString(timeFormat));
            }
            timeToWait = timeToWait.Subtract(OneSecond);
            if (timeToWait.TotalSeconds <= 0)
            {
                timeToWait = restartTimeSpan;
            }
            stopWatch.Stop();
            var waitTime = OneSecond - stopWatch.Elapsed;
            await Task.Delay(waitTime, ct).ConfigureAwait(false);
            stopWatch.Restart();
        }
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

    private static void HandleTimer(
        object? sender,
        ElapsedEventArgs e,
        ref TimeSpan timeToWait,
        TimeSpan restartTimeSpan,
        (int Left, int Top) originPos,
        CancellationToken ct)
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

