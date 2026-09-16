# syntax=docker/dockerfile:1

# ── ビルド ──────────────────────────────────────────────────────────
# SDK はビルドホストのアーキテクチャで動かし、-a で対象アーキテクチャ向けに発行する
# （arm64 向けでも QEMU エミュレーションで dotnet を動かさずに済む）
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY RadiCore/RadiCore.csproj RadiCore/
RUN dotnet restore RadiCore/RadiCore.csproj -a $TARGETARCH

COPY RadiCore/ RadiCore/
RUN dotnet publish RadiCore/RadiCore.csproj -c Release -a $TARGETARCH --no-restore -o /out

# ── 実行 ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0

# ffmpeg / ffprobe : 録音（PATH 上の実行ファイル名で起動する）
# tzdata           : 番組表・予約時刻・Cron をローカル時刻（JST）で扱うため
# curl             : HEALTHCHECK 用
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg tzdata curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /out .

# 録音ファイルはカレントディレクトリへ一時的に書き出されるため、実行ユーザーが書き込めるようにする
RUN chown app:app /app

ENV TZ=Asia/Tokyo \
    ASPNETCORE_ENVIRONMENT=Production \
    Kestrel__Endpoints__MyHttpEndpoint__Url=http://+:8080

USER app
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -fsS http://localhost:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "RadiCore.dll"]
