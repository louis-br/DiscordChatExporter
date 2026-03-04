using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Discord.Data;
using DiscordChatExporter.Core.Utils;
using DiscordChatExporter.Core.Utils.Extensions;

namespace DiscordChatExporter.Core.Exporting;

using DiscordChatExporter.Core.Diagnostics;

internal class ExportContext(
    DiscordClient discord,
    ExportRequest request,
    ExportDiagnosticsScope? diagnostics = null
)
{
    private readonly Dictionary<Snowflake, User> _usersById = new();
    private readonly Dictionary<Snowflake, Channel?> _channelsById = new();
    private readonly Dictionary<Snowflake, Role> _rolesById = new();

    private readonly ExportAssetDownloader _assetDownloader = new(
        request.AssetsDirPath,
        request.ShouldReuseAssets
    );

    public DiscordClient Discord { get; } = discord;

    public ExportRequest Request { get; } = request;

    public ExportDiagnosticsScope? Diagnostics { get; } = diagnostics;

    public DateTimeOffset NormalizeDate(DateTimeOffset instant) =>
        Request.IsUtcNormalizationEnabled ? instant.ToUniversalTime() : instant.ToLocalTime();

    public string FormatDate(DateTimeOffset instant, string format = "g") =>
        NormalizeDate(instant).ToString(format, Request.CultureInfo);

    public async ValueTask PopulateChannelsAndRolesAsync(
        CancellationToken cancellationToken = default
    )
    {
        await foreach (
            var channel in Discord.GetGuildChannelsAsync(
                Request.Guild.Id,
                Diagnostics,
                cancellationToken
            )
        )
        {
            _channelsById[channel.Id] = channel;
        }

        await foreach (
            var role in Discord.GetGuildRolesAsync(Request.Guild.Id, Diagnostics, cancellationToken)
        )
        {
            _rolesById[role.Id] = role;
        }
    }

    // Threads are not preloaded, so we resolve them on demand
    public async ValueTask PopulateChannelAsync(
        Snowflake id,
        CancellationToken cancellationToken = default
    )
    {
        if (_channelsById.ContainsKey(id))
            return;

        var channel = await Discord.TryGetChannelAsync(id, Diagnostics, cancellationToken);

        // Store the result even if it's null, to avoid re-fetching non-existing channels
        _channelsById[id] = channel;
    }

    public void PopulateUser(User user) => _usersById[user.Id] = user;

    public void PopulateUsers(IEnumerable<User> users)
    {
        foreach (var user in users)
            PopulateUser(user);
    }

    public User? TryGetUser(Snowflake id) => _usersById.GetValueOrDefault(id);

    public Channel? TryGetChannel(Snowflake id) => _channelsById.GetValueOrDefault(id);

    public Role? TryGetRole(Snowflake id) => _rolesById.GetValueOrDefault(id);

    public async ValueTask<string> ResolveAssetUrlAsync(
        string url,
        CancellationToken cancellationToken = default
    )
    {
        if (!Request.ShouldDownloadAssets)
            return url;

        try
        {
            var filePath = await _assetDownloader.DownloadAsync(url, cancellationToken);
            var relativeFilePath = Path.GetRelativePath(Request.OutputDirPath, filePath);

            // Prefer the relative path so that the export package can be copied around without breaking references.
            // However, if the assets directory lies outside the export directory, use the absolute path instead.
            var shouldUseAbsoluteFilePath =
                relativeFilePath.StartsWith(
                    ".." + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal
                )
                || relativeFilePath.StartsWith(
                    ".." + Path.AltDirectorySeparatorChar,
                    StringComparison.Ordinal
                );

            var optimalFilePath = shouldUseAbsoluteFilePath ? filePath : relativeFilePath;

            // For HTML, the path needs to be properly formatted
            if (Request.Format is ExportFormat.HtmlDark or ExportFormat.HtmlLight)
                return Url.EncodeFilePath(optimalFilePath);

            return optimalFilePath;
        }
        // Try to catch only exceptions related to failed HTTP requests
        // https://github.com/Tyrrrz/DiscordChatExporter/issues/332
        // https://github.com/Tyrrrz/DiscordChatExporter/issues/372
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            // We don't want this to crash the exporting process in case of failure.
            // TODO: add logging so we can be more liberal with catching exceptions.
            return url;
        }
    }
}
