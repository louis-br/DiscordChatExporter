using System;

namespace DiscordChatExporter.Core.Diagnostics;

public class ChannelExportBenchmark(
    string channelName,
    long requestCount,
    long retryCount,
    long advisoryDelayCount,
    TimeSpan advisoryDelayDuration,
    long pageCount,
    long reactionRequestCount,
    long reactionUserCount,
    long fetchedMessageCount,
    long exportedMessageCount,
    long filteredMessageCount,
    TimeSpan messageExportDuration,
    TimeSpan totalDuration
)
{
    public string ChannelName { get; } = channelName;

    public long RequestCount { get; } = requestCount;

    public long RetryCount { get; } = retryCount;

    public long AdvisoryDelayCount { get; } = advisoryDelayCount;

    public TimeSpan AdvisoryDelayDuration { get; } = advisoryDelayDuration;

    public long PageCount { get; } = pageCount;

    public long ReactionRequestCount { get; } = reactionRequestCount;

    public long ReactionUserCount { get; } = reactionUserCount;

    public long FetchedMessageCount { get; } = fetchedMessageCount;

    public long ExportedMessageCount { get; } = exportedMessageCount;

    public long FilteredMessageCount { get; } = filteredMessageCount;

    public TimeSpan MessageExportDuration { get; } = messageExportDuration;

    public TimeSpan TotalDuration { get; } = totalDuration;

    public string ToDisplayString() =>
        $"requests={RequestCount}, retries={RetryCount}, advisory-waits={AdvisoryDelayCount} ({AdvisoryDelayDuration.TotalSeconds:F1}s), "
        + $"reaction-requests={ReactionRequestCount}, reaction-users={ReactionUserCount}, "
        + $"pages={PageCount}, fetched={FetchedMessageCount}, exported={ExportedMessageCount}, filtered={FilteredMessageCount}, "
        + $"message-export={MessageExportDuration.TotalSeconds:F2}s, total={TotalDuration.TotalSeconds:F2}s";
}
