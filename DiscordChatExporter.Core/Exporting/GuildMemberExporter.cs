using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Discord.Data;
using DiscordChatExporter.Core.Utils.Extensions;

namespace DiscordChatExporter.Core.Exporting;

public class GuildMemberExporter(DiscordClient discord)
{
    private async ValueTask<string?> ResolveAssetUrlAsync(
        string? url,
        MemberExportRequest request,
        ExportAssetDownloader assetDownloader,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (!request.ShouldDownloadAssets)
            return url;

        try
        {
            var filePath = await assetDownloader.DownloadAsync(url, cancellationToken);
            var relativeFilePath = Path.GetRelativePath(request.OutputDirPath, filePath);

            var shouldUseAbsoluteFilePath =
                relativeFilePath.StartsWith(
                    ".." + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal
                )
                || relativeFilePath.StartsWith(
                    ".." + Path.AltDirectorySeparatorChar,
                    StringComparison.Ordinal
                );

            return shouldUseAbsoluteFilePath ? filePath : relativeFilePath;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return url;
        }
    }

    public async ValueTask ExportMembersAsync(
        MemberExportRequest request,
        CancellationToken cancellationToken = default
    )
    {
        Directory.CreateDirectory(request.OutputDirPath);

        var roles = new List<Role>();
        await foreach (
            var role in discord.GetGuildRolesAsync(
                request.Guild.Id,
                cancellationToken: cancellationToken
            )
        )
        {
            roles.Add(role);
        }
        roles.Sort((x, y) => y.Position.CompareTo(x.Position));

        var rolesById = roles.ToDictionary(r => r.Id);
        var members = new List<Member>();
        await foreach (
            var member in discord.GetGuildMembersAsync(
                request.Guild.Id,
                cancellationToken: cancellationToken
            )
        )
        {
            members.Add(member);
        }

        var assetDownloader = new ExportAssetDownloader(
            request.AssetsDirPath,
            request.ShouldReuseAssets
        );

        await using var stream = File.Create(request.OutputFilePath);
        using var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = true,
                SkipValidation = true,
            }
        );

        writer.WriteStartObject();

        writer.WriteStartObject("guild");
        writer.WriteString("id", request.Guild.Id.ToString());
        writer.WriteString("name", request.Guild.Name);
        writer.WriteEndObject();

        writer.WriteString("exportedAt", request.NormalizeDate(DateTimeOffset.UtcNow));

        writer.WriteStartArray("roles");
        foreach (var role in roles)
        {
            writer.WriteStartObject();
            writer.WriteString("id", role.Id.ToString());
            writer.WriteString("name", role.Name);
            writer.WriteNumber("position", role.Position);

            if (role.Color is not null)
                writer.WriteString("color", role.Color.Value.ToHex());

            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("members");
        foreach (var member in members)
        {
            var userRoles = member
                .RoleIds.Select(rolesById.GetValueOrDefault)
                .WhereNotNull()
                .OrderByDescending(r => r.Position)
                .ToArray();

            writer.WriteStartObject();
            writer.WriteString("id", member.Id.ToString());
            writer.WriteString("username", member.User.Name);
            writer.WriteString("fullName", member.User.FullName);
            writer.WriteString("displayName", member.User.DisplayName);

            if (!string.IsNullOrWhiteSpace(member.DisplayName))
                writer.WriteString("nickname", member.DisplayName);

            writer.WriteString(
                "avatarUrl",
                await ResolveAssetUrlAsync(
                    member.User.AvatarUrl,
                    request,
                    assetDownloader,
                    cancellationToken
                )
            );

            var memberAvatarUrl = await ResolveAssetUrlAsync(
                member.AvatarUrl,
                request,
                assetDownloader,
                cancellationToken
            );

            if (!string.IsNullOrWhiteSpace(memberAvatarUrl))
                writer.WriteString("memberAvatarUrl", memberAvatarUrl);

            var topRoleColor = userRoles
                .Where(r => r.Color is not null)
                .Select(r => r.Color)
                .FirstOrDefault();
            if (topRoleColor is not null)
                writer.WriteString("color", topRoleColor.Value.ToHex());

            writer.WriteStartArray("roleIds");
            foreach (var roleId in member.RoleIds)
                writer.WriteStringValue(roleId.ToString());
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteNumber("memberCount", members.Count);
        writer.WriteEndObject();

        await writer.FlushAsync(cancellationToken);
    }
}
