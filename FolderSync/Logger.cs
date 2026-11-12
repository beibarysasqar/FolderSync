namespace FolderSync;

public class Logger
{
    private readonly string _logFilePath;

    public Logger(string logFilePath)
    {
        _logFilePath = logFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);
    }

    public void Log(string operation, string path)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {operation,-6} {path}";
        Console.WriteLine(line);

        // Called from catch blocks, must not throw
        try
        {
            File.AppendAllText(_logFilePath, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not write to log file: {ex.Message}");
        }
    }
}
