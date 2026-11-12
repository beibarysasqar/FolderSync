# FolderSync

A console program that periodically synchronizes a source folder into a replica folder, one way.
After each pass the replica is an exact copy of the source: files, subfolders and empty folders.
Every create, copy, update and delete is written to the console and to a log file.

## Build and run

Requires the .NET 10 SDK.

```
dotnet build
dotnet run --project FolderSync -- ./source ./replica 30 ./logs/sync.log
```

Press Ctrl+C to stop. The current pass finishes first, then the program exits.

## Arguments

```
<source> <replica> <intervalSeconds> <logFile>
```

- `source` - folder to copy from, must exist.
- `replica` - folder to copy into. Created if missing. Must not be the source folder or inside it, and the source must not be inside it.
- `intervalSeconds` - time between passes, a whole number from 1 to 86400 (one day).
- `logFile` - log file path, must not be inside the source or replica folder. Its folder is created if missing, new lines are appended.

Wrong arguments print an error with a usage example, and the program exits with code 1.

A log line looks like this:

```
2026-11-12 16:13:14 COPY   docs/a.txt
```

## Notes

Files are compared by size first, and only if the sizes match, by MD5 hash. An error on one file (for example, a locked or unreadable file) is logged as ERROR and the pass continues with the rest; the file is tried again on the next pass. If the source folder disappears, the pass is skipped instead of deleting the whole replica. A source folder that can't be read is logged as ERROR and its copy in the replica is left as it is. Read-only files in the replica are updated and deleted like any other file.

If the log file can't be opened for writing at startup, the program exits with an error.

## Tests

```
dotnet test
```

The xUnit tests in `FolderSync.Tests` use real temporary folders.

- `FolderSynchronizerTests` check a sync pass: new, changed, read-only and deleted files, nested and empty folders, an unreadable source folder, and that a second run changes nothing. The unreadable folder test does nothing on Windows, where folder permissions work differently.
- `ProgramTests` start the built program as a separate process and check that it exits with code 1 if the log file is inside the source or replica folder, or can't be written. Each run is stopped after 10 seconds, because with valid arguments the program would keep running.
