namespace DiscordChatExporter.Core.Diagnostics;

public interface IExportLogger
{
    void Log(string source, string message);
}
