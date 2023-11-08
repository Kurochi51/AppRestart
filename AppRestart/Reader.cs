namespace AppRestart;

public class Reader {
    private static readonly AutoResetEvent GetInput, GotInput;
    private static string? Input;

    static Reader() {
        GetInput = new AutoResetEvent(false);
        GotInput = new AutoResetEvent(false);
        var inputThread = new Thread(reader)
        {
            IsBackground = true
        };
        inputThread.Start();
    }

    private static void reader() {
        while (true) {
            GetInput.WaitOne();
            Input = Console.ReadLine() ?? string.Empty;
            GotInput.Set();
        }
    }

    // omit the parameter to read a line without a timeout
    public static string ReadLine(int timeOutMilliseconds = Timeout.Infinite) {
        GetInput.Set();
        var success = GotInput.WaitOne(timeOutMilliseconds);
        if (success)
            return Input ?? string.Empty;
        else
            throw new TimeoutException("User did not provide input within the time-limit.");
    }
}
