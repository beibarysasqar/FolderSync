using System.Diagnostics;

namespace FolderSync.Tests;

// Argument checks live in Program.cs, so these tests start the real program as a process
public class ProgramTests : IDisposable
{
    private readonly string _root;
    private readonly string _source;
    private readonly string _replica;

    public ProgramTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "FolderSyncTests", Guid.NewGuid().ToString());
        _source = Path.Combine(_root, "source");
        _replica = Path.Combine(_root, "replica");
        Directory.CreateDirectory(_source);
    }

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }

    private (int ExitCode, string Error) RunProgram(params string[] args)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "FolderSync.dll"));
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        using var process = Process.Start(startInfo)!;

        if (!process.WaitForExit(10000))
        {
            process.Kill();
            Assert.Fail("Program did not exit, so the arguments were accepted.");
        }

        return (process.ExitCode, process.StandardError.ReadToEnd());
    }

    [Fact]
    public void LogFileInsideSource_IsRejected()
    {
        var logFile = Path.Combine(_source, "sync.log");

        var result = RunProgram(_source, _replica, "5", logFile);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("log file must not be inside", result.Error);
        Assert.False(File.Exists(logFile));
    }

    [Fact]
    public void LogFileThatCannotBeWritten_IsRejected()
    {
        var logFile = Path.Combine(_root, "logs");
        Directory.CreateDirectory(logFile);

        var result = RunProgram(_source, _replica, "5", logFile);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("cannot open log file", result.Error);
    }

    [Fact]
    public void LogFileInsideReplica_IsRejected()
    {
        var logFile = Path.Combine(_replica, "logs", "sync.log");

        var result = RunProgram(_source, _replica, "5", logFile);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("log file must not be inside", result.Error);
        Assert.False(Directory.Exists(_replica));
    }
}
