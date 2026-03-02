namespace DiscordChatExporter.Core.Diagnostics;

public sealed class NullExportLogger : IExportLogger
{
    public static NullExportLogger Instance { get; } = new();

    private NullExportLogger() { }

    public void Log(string source, string message) { }
}
