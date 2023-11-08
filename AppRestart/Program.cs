using System.Diagnostics;

namespace AppRestart;

public class AppRestart
{
    private static readonly AppRestart MainApp = new();
    private CancellationTokenSource? cts;
    private Process? appToRestart;
    private string appPath = string.Empty;
    private int restartInterval;
    private bool shouldExit;

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
        Console.WriteLine("Application: {0}", appToRestart.ProcessName);
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
                    Console.WriteLine("Exiting program.");
                    shouldExit = true;
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
        var appName = Console.ReadLine()?.Trim() ?? string.Empty;
        appToRestart = FindProcess(appName);
        if (appToRestart?.MainModule is null)
        {
            Console.WriteLine("Process not found.");
            return false;
        }
        appPath = appToRestart.MainModule.FileName;
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
        while (!token.IsCancellationRequested)
        {
            var sleepInMilliseconds = restartInterval * 1000 * 60 * 60;
            await Task.Delay(sleepInMilliseconds, token);
            
            if (shouldExit || appToRestart is null)
            {
                return;
            }

            if (FindProcessId(appToRestart.ProcessName) is 0)
            {
                Console.WriteLine("Program {0} isn't running.", appToRestart.ProcessName);
                return;
            }
            appToRestart.Kill();
            await appToRestart.WaitForExitAsync(token);
            appToRestart = Process.Start(appPath);
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

