namespace AppRestart;

public static class Reader
{
    private static readonly AutoResetEvent GetInput = new(false);
    private static readonly AutoResetEvent GotInput = new(false);
    private static string? Input;

    static Reader()
    {
        var inputThread = new Thread(ReaderThread)
        {
            IsBackground = true,
        };
        inputThread.Start();
    }

    private static void ReaderThread()
    {
        while (true)
        {
            GetInput.WaitOne();
            Input = Console.ReadLine() ?? string.Empty;
            GotInput.Set();
        }
    }

    // omit the parameter to read a line without a timeout
    public static string ReadLine(int timeOutMilliseconds = Timeout.Infinite)
    {
        GetInput.Set();
        var success = GotInput.WaitOne(timeOutMilliseconds);
        if (success)
        {
            return Input ?? string.Empty;
        }
        throw new TimeoutException("User did not provide input within the time-limit.");
    }
}
