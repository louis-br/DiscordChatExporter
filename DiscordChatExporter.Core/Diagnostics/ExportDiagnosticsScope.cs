using System;
using System.Net;
using System.Threading;

namespace DiscordChatExporter.Core.Diagnostics;

public class ExportDiagnosticsScope(string channelName, IExportLogger? logger = null)
{
    private readonly IExportLogger _logger = logger ?? NullExportLogger.Instance;
    private readonly long _startedAt = DateTime.UtcNow.Ticks;
    private long _requestCount;
    private long _retryCount;
    private long _advisoryDelayCount;
    private long _advisoryDelayTicks;
    private long _pageCount;
    private long _reactionRequestCount;
    private long _reactionUserCount;
    private long _fetchedMessageCount;
    private long _exportedMessageCount;
    private long _filteredMessageCount;
    private long _messageExportTicks;

    public void Log(string source, string message) =>
        _logger.Log(source, $"[{channelName}] {message}");

    public void RecordRequest(
        string url,
        HttpStatusCode statusCode,
        TimeSpan elapsed,
        int? remainingRequestCount,
        TimeSpan? resetAfterDelay
    )
    {
        Interlocked.Increment(ref _requestCount);

        Log(
            "discord.http",
            $"GET {url} -> {(int)statusCode} {statusCode} in {elapsed.TotalMilliseconds:F0}ms"
                + (remainingRequestCount is not null ? $", remaining={remainingRequestCount}" : "")
                + (
                    resetAfterDelay is not null
                        ? $", reset-after={resetAfterDelay.Value.TotalSeconds:F2}s"
                        : ""
                )
        );
    }

    public void RecordRetry(string url, int attemptNumber, TimeSpan delay, string reason)
    {
        Interlocked.Increment(ref _retryCount);

        Log(
            "discord.retry",
            $"Retry {attemptNumber + 1} for GET {url} after {delay.TotalSeconds:F2}s ({reason})"
        );
    }

    public void RecordAdvisoryDelay(TimeSpan delay)
    {
        Interlocked.Increment(ref _advisoryDelayCount);
        Interlocked.Add(ref _advisoryDelayTicks, delay.Ticks);

        Log("discord.ratelimit", $"Waiting {delay.TotalSeconds:F2}s for advisory rate limit reset");
    }

    public void RecordPage(int messageCount, string mode, string cursor)
    {
        Interlocked.Increment(ref _pageCount);
        Interlocked.Add(ref _fetchedMessageCount, messageCount);

        Log("discord.page", $"Fetched {messageCount} message(s) via {mode} cursor={cursor}");
    }

    public void RecordReactionRequest(string emojiName, int userCount)
    {
        Interlocked.Increment(ref _reactionRequestCount);
        Interlocked.Add(ref _reactionUserCount, userCount);

        Log("discord.reaction", $"Fetched {userCount} reaction user(s) for emoji={emojiName}");
    }

    public void RecordMessageExported(TimeSpan messageExportDuration)
    {
        Interlocked.Increment(ref _exportedMessageCount);
        Interlocked.Add(ref _messageExportTicks, messageExportDuration.Ticks);
    }

    public void RecordMessageFiltered() => Interlocked.Increment(ref _filteredMessageCount);

    public ChannelExportBenchmark CreateBenchmark() =>
        new(
            channelName,
            Interlocked.Read(ref _requestCount),
            Interlocked.Read(ref _retryCount),
            Interlocked.Read(ref _advisoryDelayCount),
            TimeSpan.FromTicks(Interlocked.Read(ref _advisoryDelayTicks)),
            Interlocked.Read(ref _pageCount),
            Interlocked.Read(ref _reactionRequestCount),
            Interlocked.Read(ref _reactionUserCount),
            Interlocked.Read(ref _fetchedMessageCount),
            Interlocked.Read(ref _exportedMessageCount),
            Interlocked.Read(ref _filteredMessageCount),
            TimeSpan.FromTicks(Interlocked.Read(ref _messageExportTicks)),
            TimeSpan.FromTicks(DateTime.UtcNow.Ticks - _startedAt)
        );
}
