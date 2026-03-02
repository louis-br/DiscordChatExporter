#!/usr/bin/env bash
set -euo pipefail

SRC_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT_DIR="$SRC_DIR/artifacts/cli-publish"

mkdir -p "$OUT_DIR"
rm -rf "$OUT_DIR"/*

echo "[isolated-build] Using Docker .NET SDK container"
docker run --rm \
  -u "$(id -u):$(id -g)" \
  -e HOME=/tmp \
  -e DOTNET_CLI_HOME=/tmp/.dotnet \
  -e DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
  -v "$SRC_DIR:/src" \
  -v "$OUT_DIR:/out" \
  -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  sh -lc '
    set -e
    mkdir -p /tmp/.dotnet
    dotnet --info >/tmp/dotnet-info.txt
    dotnet restore DiscordChatExporter.Cli/DiscordChatExporter.Cli.csproj
    dotnet publish DiscordChatExporter.Cli/DiscordChatExporter.Cli.csproj \
      -c Release \
      --self-contained false \
      -r linux-x64 \
      -o /out
    echo "Build done."
  '

echo "[isolated-build] Output: $OUT_DIR"
ls -la "$OUT_DIR" | sed -n '1,80p'
