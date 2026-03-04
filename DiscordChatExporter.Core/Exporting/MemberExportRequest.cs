using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using DiscordChatExporter.Core.Discord.Data;
using DiscordChatExporter.Core.Utils.Extensions;

namespace DiscordChatExporter.Core.Exporting;

public class MemberExportRequest
{
    public Guild Guild { get; }

    public string OutputFilePath { get; }

    public string OutputDirPath { get; }

    public string AssetsDirPath { get; }

    public bool ShouldDownloadAssets { get; }

    public bool ShouldReuseAssets { get; }

    public bool IsUtcNormalizationEnabled { get; }

    public MemberExportRequest(
        Guild guild,
        string outputPath,
        string? assetsDirPath,
        bool shouldDownloadAssets,
        bool shouldReuseAssets,
        bool isUtcNormalizationEnabled
    )
    {
        Guild = guild;
        ShouldDownloadAssets = shouldDownloadAssets;
        ShouldReuseAssets = shouldReuseAssets;
        IsUtcNormalizationEnabled = isUtcNormalizationEnabled;

        OutputFilePath = GetOutputFilePath(guild, outputPath);
        OutputDirPath = Path.GetDirectoryName(OutputFilePath)!;

        AssetsDirPath = !string.IsNullOrWhiteSpace(assetsDirPath)
            ? FormatPath(assetsDirPath, guild)
            : $"{OutputFilePath}_Files{Path.DirectorySeparatorChar}";
    }

    public DateTimeOffset NormalizeDate(DateTimeOffset instant) =>
        IsUtcNormalizationEnabled ? instant.ToUniversalTime() : instant.ToLocalTime();

    public static string GetDefaultOutputFileName(Guild guild) =>
        Path.EscapeFileName($"{guild.Name} - members [{guild.Id}].json");

    private static string FormatPath(string path, Guild guild) =>
        Regex.Replace(
            path,
            "%.",
            m =>
                Path.EscapeFileName(
                    m.Value switch
                    {
                        "%g" => guild.Id.ToString(),
                        "%G" => guild.Name,
                        "%d" => DateTimeOffset.Now.ToString(
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture
                        ),
                        "%%" => "%",
                        _ => m.Value,
                    }
                )
        );

    private static string GetOutputFilePath(Guild guild, string outputPath)
    {
        var actualOutputPath = FormatPath(outputPath, guild);

        if (
            Directory.Exists(actualOutputPath)
            || string.IsNullOrWhiteSpace(Path.GetExtension(actualOutputPath))
        )
        {
            return Path.Combine(actualOutputPath, GetDefaultOutputFileName(guild));
        }

        return actualOutputPath;
    }
}
