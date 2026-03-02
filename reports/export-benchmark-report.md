# Export Benchmark Report

Date: 2026-03-01

Run:
- Command: `exportall --format Json --limit 100 --benchmark --log-path ...`
- Output root: `artifacts/exportall-json-20260301-203147`

## Summary

The run did not look dominated by message pagination.

- Successful exports: 46 channels
- Logged HTTP requests: 4491
- Average logged HTTP latency: about 284 ms
- Retry count: 6
- Forbidden responses: 38
- Fetch scopes logged: 78
- Message pages logged: 40
- Reaction requests logged: 2757
- Member requests logged: 1335
- Guild channel preload requests logged: 90
- Guild role preload requests logged: 84

## What `--benchmark` Measured

Per channel, the benchmark reported:

- `requests`
- `retries`
- `advisory-waits`
- `pages`
- `fetched`
- `exported`
- `filtered`
- `referenced-users`
- `member-resolution`
- `message-export`
- `total`

In the first implementation, `requests` mostly reflected message-fetch pagination. It did not fully expose reaction lookups, member lookups, and guild metadata preloads, which made some slow channels look misleadingly cheap on the network side.

## Observed Bottlenecks

The main bottleneck from the run was downstream export work, not page-fetch volume.

Representative slow channels:

- `welcome / announcements`: 3 fetch requests, 0 retries, 183.63 s in `message-export`
- `welcome / releases`: 3 fetch requests, 0 retries, 120.18 s in `message-export`
- `welcome / shadow-says-stuff`: 3 fetch requests, 0 retries, 51.19 s in `message-export`
- `OpenClaw / showcase`: 2 fetch requests, 25.26 s in `member-resolution`, 42.03 s in `message-export`
- `OpenClaw / introductions`: 2 fetch requests, 27.08 s in `member-resolution`, 22.67 s in `message-export`

The diagnostics log also showed that reaction expansion was expensive:

- Reaction requests: 2757
- Member requests: 1335

This explains why some channels with only 1 message page still took tens or hundreds of seconds.

## Rate Limit Findings

Rate limiting existed but was not the dominant cost in this bounded run.

Observed retries:

- `users/@me/guilds?...` after 2 s
- `guilds/1456350064065904867/channels` after 4 s
- `guilds/1456350064065904867/channels` after 16 s
- `guilds/1456350064065904867/channels` after 49 s
- `guilds/1456350064065904867/members/831249712526262333` after 2 s
- `guilds/1456350064065904867/channels` after 38 s

## Access / Exportability Issues

The run also failed on channels that were not exportable with the current token or export mode:

- 38 forbidden message requests
- multiple forum channels rejected because they must be exported via threads
- several empty channels emitted warnings

## Recommended Improvements

1. Add `--no-reactions` to allow a fast-path export that avoids reaction user expansion.
2. Expand benchmark coverage so reaction requests and member lookups show up explicitly.
3. Cache guild metadata across channels instead of reloading guild channels and roles per export.
4. Skip direct export of forum channels earlier unless thread export is enabled.
5. Consider optional fast-path modes for expensive enrichments such as reactions and member resolution.
