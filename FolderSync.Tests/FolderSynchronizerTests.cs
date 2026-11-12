namespace FolderSync.Tests;

public class FolderSynchronizerTests : IDisposable
{
    private readonly string _root;
    private readonly string _source;
    private readonly string _replica;
    private readonly string _logFile;

    public FolderSynchronizerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "FolderSyncTests", Guid.NewGuid().ToString());
        _source = Path.Combine(_root, "source");
        _replica = Path.Combine(_root, "replica");
        _logFile = Path.Combine(_root, "logs", "sync.log");
        Directory.CreateDirectory(_source);
    }

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }

    private void RunSync()
    {
        new FolderSynchronizer(_source, _replica, new Logger(_logFile)).Synchronize();
    }

    [Fact]
    public void NewFile_IsCopiedToReplica()
    {
        File.WriteAllText(Path.Combine(_source, "a.txt"), "hello");

        RunSync();

        Assert.Equal("hello", File.ReadAllText(Path.Combine(_replica, "a.txt")));
    }

    [Fact]
    public void ChangedFile_WithSameSize_IsUpdated()
    {
        var sourceFile = Path.Combine(_source, "a.txt");
        File.WriteAllText(sourceFile, "aaaa");
        RunSync();

        // Same length, so only the MD5 check can notice the change
        File.WriteAllText(sourceFile, "bbbb");
        RunSync();

        Assert.Equal("bbbb", File.ReadAllText(Path.Combine(_replica, "a.txt")));
    }

    [Fact]
    public void ReadOnlyFile_IsUpdatedAfterChange()
    {
        var sourceFile = Path.Combine(_source, "a.txt");
        File.WriteAllText(sourceFile, "v1");
        File.SetAttributes(sourceFile, FileAttributes.ReadOnly);
        RunSync();

        File.SetAttributes(sourceFile, FileAttributes.Normal);
        File.WriteAllText(sourceFile, "v2 changed");
        RunSync();

        Assert.Equal("v2 changed", File.ReadAllText(Path.Combine(_replica, "a.txt")));
    }

    [Fact]
    public void FileDeletedFromSource_IsDeletedFromReplica()
    {
        File.WriteAllText(Path.Combine(_source, "a.txt"), "hello");
        RunSync();

        File.Delete(Path.Combine(_source, "a.txt"));
        RunSync();

        Assert.False(File.Exists(Path.Combine(_replica, "a.txt")));
    }

    [Fact]
    public void ReadOnlyFileDeletedFromSource_IsDeletedFromReplica()
    {
        var sourceFile = Path.Combine(_source, "a.txt");
        File.WriteAllText(sourceFile, "hello");
        File.SetAttributes(sourceFile, FileAttributes.ReadOnly);
        RunSync();

        File.SetAttributes(sourceFile, FileAttributes.Normal);
        File.Delete(sourceFile);
        RunSync();

        Assert.False(File.Exists(Path.Combine(_replica, "a.txt")));
    }

    [Fact]
    public void FolderWithReadOnlyFile_IsDeletedFromReplica()
    {
        var sourceFile = Path.Combine(_source, "docs", "a.txt");
        Directory.CreateDirectory(Path.Combine(_source, "docs"));
        File.WriteAllText(sourceFile, "hello");
        File.SetAttributes(sourceFile, FileAttributes.ReadOnly);
        RunSync();

        File.SetAttributes(sourceFile, FileAttributes.Normal);
        Directory.Delete(Path.Combine(_source, "docs"), true);
        RunSync();

        Assert.False(Directory.Exists(Path.Combine(_replica, "docs")));
    }

    [Fact]
    public void NestedFolders_AreCopied()
    {
        Directory.CreateDirectory(Path.Combine(_source, "a", "b", "c"));
        File.WriteAllText(Path.Combine(_source, "a", "b", "c", "file.txt"), "deep");

        RunSync();

        Assert.Equal("deep", File.ReadAllText(Path.Combine(_replica, "a", "b", "c", "file.txt")));
    }

    [Fact]
    public void EmptyFolder_IsCreated()
    {
        Directory.CreateDirectory(Path.Combine(_source, "empty"));

        RunSync();

        Assert.True(Directory.Exists(Path.Combine(_replica, "empty")));
    }

    [Fact]
    public void SecondRun_ChangesNothing()
    {
        Directory.CreateDirectory(Path.Combine(_source, "docs", "empty"));
        File.WriteAllText(Path.Combine(_source, "a.txt"), "hello");
        File.WriteAllText(Path.Combine(_source, "docs", "b.txt"), "world");
        RunSync();
        var linesAfterFirstRun = File.ReadAllLines(_logFile).Length;

        RunSync();

        Assert.Equal(linesAfterFirstRun, File.ReadAllLines(_logFile).Length);
    }

    [Fact]
    public void UnreadableSubfolder_KeepsReplicaCopy()
    {
        // Folder permissions work differently on Windows
        if (OperatingSystem.IsWindows())
            return;

        var sourceSub = Path.Combine(_source, "sub");
        Directory.CreateDirectory(sourceSub);
        File.WriteAllText(Path.Combine(sourceSub, "a.txt"), "hello");
        RunSync();

        var oldMode = File.GetUnixFileMode(sourceSub);
        File.SetUnixFileMode(sourceSub, UnixFileMode.None);
        try
        {
            RunSync();
        }
        finally
        {
            File.SetUnixFileMode(sourceSub, oldMode);
        }

        Assert.True(File.Exists(Path.Combine(_replica, "sub", "a.txt")));
    }

    [Fact]
    public void ExtraFolderInReplica_IsDeleted()
    {
        Directory.CreateDirectory(Path.Combine(_replica, "old"));
        File.WriteAllText(Path.Combine(_replica, "old", "x.txt"), "stale");

        RunSync();

        Assert.False(Directory.Exists(Path.Combine(_replica, "old")));
    }
}
