using System.Security.Cryptography;

namespace FolderSync;

public class FolderSynchronizer
{
    private readonly string _sourcePath;
    private readonly string _replicaPath;
    private readonly Logger _logger;

    public FolderSynchronizer(string sourcePath, string replicaPath, Logger logger)
    {
        _sourcePath = sourcePath;
        _replicaPath = replicaPath;
        _logger = logger;
    }

    public void Synchronize()
    {
        Directory.CreateDirectory(_replicaPath);
        SyncFolder(_sourcePath, _replicaPath);
    }

    private void SyncFolder(string sourceDir, string replicaDir)
    {
        // Read source first, if it can't be read we must not delete anything in replica
        var sourceFiles = Directory.GetFiles(sourceDir);
        var sourceDirs = Directory.GetDirectories(sourceDir);
        var sourceFileNames = sourceFiles.Select(Path.GetFileName).ToHashSet();
        var sourceDirNames = sourceDirs.Select(Path.GetFileName).ToHashSet();

        // Delete first, so a file that became a folder doesn't block the copy
        foreach (var replicaFile in Directory.GetFiles(replicaDir))
        {
            if (sourceFileNames.Contains(Path.GetFileName(replicaFile)))
                continue;

            try
            {
                // On Windows a read-only file can't be deleted
                File.SetAttributes(replicaFile, FileAttributes.Normal);
                File.Delete(replicaFile);
                _logger.Log("DELETE", RelativePath(replicaFile));
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", $"{RelativePath(replicaFile)}: {ex.Message}");
            }
        }

        foreach (var replicaSubDir in Directory.GetDirectories(replicaDir))
        {
            if (sourceDirNames.Contains(Path.GetFileName(replicaSubDir)))
                continue;

            try
            {
                foreach (var file in Directory.GetFiles(replicaSubDir, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(replicaSubDir, true);
                _logger.Log("DELETE", RelativePath(replicaSubDir));
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", $"{RelativePath(replicaSubDir)}: {ex.Message}");
            }
        }

        foreach (var sourceFile in sourceFiles)
        {
            var replicaFile = Path.Combine(replicaDir, Path.GetFileName(sourceFile));
            try
            {
                if (!File.Exists(replicaFile))
                {
                    File.Copy(sourceFile, replicaFile);
                    _logger.Log("COPY", RelativePath(replicaFile));
                }
                else if (!FilesAreEqual(sourceFile, replicaFile))
                {
                    // Copy keeps the read-only flag of the source, and a read-only file can't be overwritten
                    File.SetAttributes(replicaFile, FileAttributes.Normal);
                    File.Copy(sourceFile, replicaFile, true);
                    _logger.Log("UPDATE", RelativePath(replicaFile));
                }
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", $"{RelativePath(replicaFile)}: {ex.Message}");
            }
        }

        foreach (var sourceSubDir in sourceDirs)
        {
            var replicaSubDir = Path.Combine(replicaDir, Path.GetFileName(sourceSubDir));
            try
            {
                if (!Directory.Exists(replicaSubDir))
                {
                    Directory.CreateDirectory(replicaSubDir);
                    _logger.Log("CREATE", RelativePath(replicaSubDir));
                }
                SyncFolder(sourceSubDir, replicaSubDir);
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", $"{RelativePath(replicaSubDir)}: {ex.Message}");
            }
        }
    }

    private static bool FilesAreEqual(string pathA, string pathB)
    {
        if (new FileInfo(pathA).Length != new FileInfo(pathB).Length)
            return false;

        using var streamA = File.OpenRead(pathA);
        using var streamB = File.OpenRead(pathB);
        return MD5.HashData(streamA).SequenceEqual(MD5.HashData(streamB));
    }

    private string RelativePath(string path)
    {
        return Path.GetRelativePath(_replicaPath, path);
    }
}
