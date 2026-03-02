using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DiscordChatExporter.Core.Diagnostics;
using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Discord.Data;
using DiscordChatExporter.Core.Exceptions;
using Gress;

namespace DiscordChatExporter.Core.Exporting;

public class ChannelExporter(
    DiscordClient discord,
    IExportLogger? logger = null,
    bool shouldEmitBenchmarkLogs = false
)
{
    private readonly IExportLogger _logger = logger ?? NullExportLogger.Instance;

    private async IAsyncEnumerable<Message> GetMessagesAsync(
        ExportRequest request,
        IProgress<Percentage>? progress,
        ExportDiagnosticsScope diagnostics,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        if (request.MessageLimit is not int messageLimit)
        {
            var messages = !request.IsReverseMessageOrder
                ? discord.GetMessagesAsync(
                    request.Channel.Id,
                    request.After,
                    request.Before,
                    progress,
                    diagnostics,
                    cancellationToken
                )
                : discord.GetMessagesInReverseAsync(
                    request.Channel.Id,
                    request.After,
                    request.Before,
                    progress,
                    diagnostics,
                    cancellationToken
                );

            await foreach (var message in messages.WithCancellation(cancellationToken))
                yield return message;

            yield break;
        }

        diagnostics.Log(
            "export.limit",
            request.IsReverseMessageOrder
                ? $"Export limited to newest {messageLimit} message(s)"
                : $"Export limited to newest {messageLimit} message(s) and reordered chronologically"
        );

        var reverseMessages = discord.GetMessagesInReverseAsync(
            request.Channel.Id,
            request.After,
            request.Before,
            progress,
            diagnostics,
            cancellationToken
        );

        if (request.IsReverseMessageOrder)
        {
            var yieldedMessageCount = 0;
            await foreach (var message in reverseMessages.WithCancellation(cancellationToken))
            {
                yield return message;

                yieldedMessageCount++;
                if (yieldedMessageCount >= messageLimit)
                    yield break;
            }

            yield break;
        }

        var buffer = new List<Message>(messageLimit);
        await foreach (var message in reverseMessages.WithCancellation(cancellationToken))
        {
            buffer.Add(message);

            if (buffer.Count >= messageLimit)
                break;
        }

        for (var i = buffer.Count - 1; i >= 0; i--)
            yield return buffer[i];
    }

    public async ValueTask<ChannelExportBenchmark> ExportChannelAsync(
        ExportRequest request,
        IProgress<Percentage>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var diagnostics = new ExportDiagnosticsScope(
            request.Channel.GetHierarchicalName(),
            _logger
        );

        // Forum channels don't have messages, they are just a list of threads
        if (request.Channel.Kind == ChannelKind.GuildForum)
        {
            throw new DiscordChatExporterException(
                $"Channel '{request.Channel.Name}' "
                    + $"of guild '{request.Guild.Name}' "
                    + $"is a forum and cannot be exported directly. "
                    + "You need to pull its threads and export them individually."
            );
        }

        // Build context
        var context = new ExportContext(discord, request);
        await context.PopulateChannelsAndRolesAsync(cancellationToken);

        // Initialize the exporter before further checks to ensure the file is created even if
        // an exception is thrown after this point.
        await using var messageExporter = new MessageExporter(context);

        // Check if the channel is empty
        if (request.Channel.IsEmpty)
        {
            throw new ChannelEmptyException(
                $"Channel '{request.Channel.Name}' "
                    + $"of guild '{request.Guild.Name}' "
                    + $"does not contain any messages; an empty file will be created."
            );
        }

        // Check if the 'before' and 'after' boundaries are valid
        if (
            (
                request.Before is not null
                && !request.Channel.MayHaveMessagesBefore(request.Before.Value)
            )
            || (
                request.After is not null
                && !request.Channel.MayHaveMessagesAfter(request.After.Value)
            )
        )
        {
            throw new ChannelEmptyException(
                $"Channel '{request.Channel.Name}' "
                    + $"of guild '{request.Guild.Name}' "
                    + $"does not contain any messages within the specified period; an empty file will be created."
            );
        }

        var messages = GetMessagesAsync(request, progress, diagnostics, cancellationToken);

        await foreach (var message in messages)
        {
            try
            {
                var memberResolutionStopwatch = Stopwatch.StartNew();
                var referencedUserCount = 0;

                // Resolve members for referenced users
                foreach (var user in message.GetReferencedUsers())
                {
                    await context.PopulateMemberAsync(user, cancellationToken);
                    referencedUserCount++;
                }

                memberResolutionStopwatch.Stop();
                diagnostics.RecordReferencedUsersResolved(referencedUserCount);

                // Export the message
                if (request.MessageFilter.IsMatch(message))
                {
                    var messageExportStopwatch = Stopwatch.StartNew();
                    await messageExporter.ExportMessageAsync(message, cancellationToken);
                    messageExportStopwatch.Stop();

                    diagnostics.RecordMessageExported(
                        memberResolutionStopwatch.Elapsed,
                        messageExportStopwatch.Elapsed
                    );
                }
                else
                {
                    diagnostics.RecordMessageFiltered(memberResolutionStopwatch.Elapsed);
                }
            }
            catch (Exception ex)
            {
                // Provide more context to the exception, to simplify debugging based on error messages
                throw new DiscordChatExporterException(
                    $"Failed to export message #{message.Id} "
                        + $"in channel '{request.Channel.Name}' (#{request.Channel.Id}) "
                        + $"of guild '{request.Guild.Name} (#{request.Guild.Id})'.",
                    ex is not DiscordChatExporterException dex || dex.IsFatal,
                    ex
                );
            }
        }

        var benchmark = diagnostics.CreateBenchmark();

        if (shouldEmitBenchmarkLogs)
            diagnostics.Log("export.benchmark", benchmark.ToDisplayString());

        return benchmark;
    }
}
