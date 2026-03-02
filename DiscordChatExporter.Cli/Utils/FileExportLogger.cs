using System;
using System.IO;
using DiscordChatExporter.Core.Diagnostics;

namespace DiscordChatExporter.Cli.Utils;

public class FileExportLogger : IExportLogger
{
    private readonly object _syncRoot = new();
    private readonly StreamWriter _writer;

    public FileExportLogger(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var dirPath = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(dirPath))
            Directory.CreateDirectory(dirPath);

        _writer = new StreamWriter(
            new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read)
        )
        {
            AutoFlush = true,
        };
    }

    public void Log(string source, string message)
    {
        lock (_syncRoot)
            _writer.WriteLine($"{DateTimeOffset.Now:O} [{source}] {message}");
    }
}
