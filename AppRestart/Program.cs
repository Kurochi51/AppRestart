using System.Diagnostics;

namespace AppRestart;

public class AppRestart
{
    private static readonly AppRestart MainApp = new();
    private CancellationTokenSource? cts;
    private Process? appToRestart;
    private string appPath = string.Empty, appName = string.Empty;
    private int restartInterval;

    private AppRestart()
    {
        
    }

    private static void Main()
    {
        MainApp.RestartApp();
        Console.WriteLine("Press any key to exit...");
        Console.ReadLine();
        Environment.Exit(0);
    }


    private void RestartApp()
    {
        if (!UserInput() || appToRestart is null)
        {
            return;
        }

        cts = new();
        var token = cts.Token;
        var monitor = MonitorTask(token);
        Console.WriteLine("Application: {0}", appName);
        Console.WriteLine("1. Exit");
        while (true)
        {
            if (monitor.IsCompleted)
            {
                Console.WriteLine("Exiting program. Monitor task is stopped.");
                break;
            }
            try
            {
                var optionInput = Reader.ReadLine(3000);
                if (!string.IsNullOrWhiteSpace(optionInput) && optionInput.Equals("1"))
                {
                    Console.WriteLine("Exiting program...");
                    break;
                }
                else
                {
                    Console.WriteLine("Invalid option {0}. Please select a valid option.", optionInput);
                    Console.WriteLine("1. Exit");
                }
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
        if (appToRestart?.MainModule is null)
        {
            Console.WriteLine("Process not found.");
            return false;
        }
        appPath = appToRestart.MainModule.FileName;
        appName = appToRestart.ProcessName;
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


    private async Task MonitorTask(CancellationToken token)
    {
        const int startDelay = 10;
        var firstTime = true;
        while (!token.IsCancellationRequested)
        {
            if (firstTime)
            {
                await Task.Delay(TimeSpan.FromHours(restartInterval), token);
                firstTime = false;
            }
            else
            {
                await Task.Delay(TimeSpan.FromHours(restartInterval) - TimeSpan.FromSeconds(startDelay), token);
            }
            
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (appToRestart is null || FindProcessId(appName) is 0)
            {
                Console.WriteLine("Program {0} isn't running.", appName);
                return;
            }
            var isDiscord = appName.Trim().ToLower().Equals("discord");
            var path = isDiscord ? "\"" + Directory.GetParent(appPath)?.Parent?.FullName + Path.DirectorySeparatorChar + "Update.exe --processStart Discord.exe" + "\"" : "\"" + appPath + "\"";
            var startArg = "/c " + path;
            appToRestart.Kill();
            await appToRestart.WaitForExitAsync(token);
            Process.Start("CMD.exe",startArg);
            await Task.Delay(TimeSpan.FromSeconds(startDelay), token);
            appToRestart = FindProcess(appName);
        }
    }
    
    private static Process? FindProcess(string processName)
    {
        var processList = Process.GetProcesses();
        return processList.FirstOrDefault(process => process.ProcessName.Trim().ToLower().Equals(processName.ToLower()));
    }

    private static int FindProcessId(string processName)
    {
        var processList = Process.GetProcesses();
        return (from process in processList where process.ProcessName.Equals(processName) select process.Id)
            .FirstOrDefault();
    }
}

