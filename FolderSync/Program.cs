using FolderSync;

if (args.Length != 4)
{
    PrintError("expected 4 arguments.");
    return 1;
}

if (args.Any(string.IsNullOrWhiteSpace))
{
    PrintError("arguments must not be empty.");
    return 1;
}

var sourcePath = Path.GetFullPath(args[0]);
var replicaPath = Path.GetFullPath(args[1]);
var logFilePath = Path.GetFullPath(args[3]);

if (!Directory.Exists(sourcePath))
{
    PrintError($"source folder does not exist: {sourcePath}");
    return 1;
}

// WaitOne can't wait longer than ~24 days
if (!int.TryParse(args[2], out var intervalSeconds) || intervalSeconds <= 0 || intervalSeconds > 86400)
{
    PrintError($"interval must be a whole number of seconds from 1 to 86400, got '{args[2]}'.");
    return 1;
}

if (IsSameOrInside(replicaPath, sourcePath) || IsSameOrInside(sourcePath, replicaPath))
{
    PrintError("replica must not be the source folder or inside it, and vice versa.");
    return 1;
}

if (File.Exists(replicaPath))
{
    PrintError($"replica path is a file, not a folder: {replicaPath}");
    return 1;
}

// log in source gets copied, log in replica gets deleted
if (IsSameOrInside(logFilePath, sourcePath) || IsSameOrInside(logFilePath, replicaPath))
{
    PrintError("log file must not be inside the source or replica folder.");
    return 1;
}

Logger logger;
try
{
    logger = new Logger(logFilePath);
    // Logger ignores write errors later, so fail early here
    File.AppendAllText(logFilePath, "");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: cannot open log file: {ex.Message}");
    return 1;
}

var synchronizer = new FolderSynchronizer(sourcePath, replicaPath, logger);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    // The runtime will not kill the process
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"Syncing {sourcePath} -> {replicaPath} every {intervalSeconds} s. Press Ctrl+C to stop.");

while (!cts.IsCancellationRequested)
{
    // otherwise a missing source wipes the whole replica
    if (!Directory.Exists(sourcePath))
    {
        logger.Log("ERROR", $"source folder not found, pass skipped: {sourcePath}");
    }
    else
    {
        try
        {
            synchronizer.Synchronize();
        }
        catch (Exception ex)
        {
            logger.Log("ERROR", $"sync pass failed: {ex.Message}");
        }
    }

    cts.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(intervalSeconds));
}

Console.WriteLine("Stopped.");
return 0;

static void PrintError(string message)
{
    Console.Error.WriteLine($"Error: {message}");
    Console.Error.WriteLine("Usage: dotnet run --project FolderSync -- <source> <replica> <intervalSeconds> <logFile>");
    Console.Error.WriteLine("Example: dotnet run --project FolderSync -- ./source ./replica 30 ./logs/sync.log");
}

static bool IsSameOrInside(string path, string folder)
{
    path = Path.TrimEndingDirectorySeparator(path);
    folder = Path.TrimEndingDirectorySeparator(folder);

    // Ignore case because the default macOS file system is case-insensitive
    return path.Equals(folder, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
