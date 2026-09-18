<div align="center">
  <img src="RadiCore/wwwroot/radicorelogo.png" alt="RadiCore" width="420">

  **radiko のタイムフリー録音・予約・ライブラリ管理を行うセルフホスト型 Web アプリケーション**

  *A self-hosted radiko (Japanese internet radio) timefree recorder, scheduler and web library.*

  ![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)
  ![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-MVC-512BD4)
  ![PostgreSQL](https://img.shields.io/badge/PostgreSQL-18-4169E1?logo=postgresql&logoColor=white)
  ![Quartz.NET](https://img.shields.io/badge/Quartz.NET-3.15-orange)
  [![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
</div>

---

> ## ⚠️ はじめにお読みください
>
> **本ソフトウェアは作者個人の利用を主目的として開発されています。配布・汎用利用を前提とした作りにはなっていません。**
>
> そのうえで、**[MIT License](LICENSE) に従ってご自由にご利用いただけます**（利用・改変・再配布とも可）。
> Issue・Pull Request・お問い合わせにはベストエフォートで対応しますが、対応や機能追加をお約束するものではありません。

---

<div align="center">

## 🐳 [Docker でご利用になりたい方はこちら](docs/docker.md)

[![Docker で使う](https://img.shields.io/badge/Docker%20%E3%81%A7%E4%BD%BF%E3%81%86-%E3%82%BB%E3%83%83%E3%83%88%E3%82%A2%E3%83%83%E3%83%97%E6%89%8B%E9%A0%86%E3%82%92%E8%A6%8B%E3%82%8B-2496ED?style=for-the-badge&logo=docker&logoColor=white)](docs/docker.md)

PostgreSQL・ffmpeg・.NET を個別に用意せず、`docker compose up -d` で起動できます。

</div>

---

## 概要

RadiCore は、radiko の番組表を取り込み、指定した番組を自動で録音して Web 上のライブラリから再生・ダウンロードできるようにするアプリケーションです。自宅サーバーや VPS 上での常時稼働を想定しています。

録音は radiko の**タイムフリー**（放送済み番組の後追い再生）を利用します。radiko プレミアム会員のアカウントを設定すれば、**エリアフリー**によるエリア外番組の録音にも対応します。

> **旧名:** RadikoShift
> 2026年8月に RadiCore へ改名しました。旧リポジトリ URL からは自動でリダイレクトされます。

## 主な機能

| 機能 | 内容 |
|---|---|
| **番組表** | 全国の放送局の週間番組表を毎日自動取得。放送局・日付・キーワードで絞り込み |
| **予約録音** | 番組表から予約。単発（`Once`）／毎日（`Daily`）／毎週（`Weekly`）の繰り返しに対応 |
| **手動予約** | 番組表に無い時間帯を、放送局・日時を直接指定して予約 |
| **自動削除** | 繰り返し予約で、新しい録音の完了時に前回分を自動削除（任意設定） |
| **ライブラリ** | 録音済み番組の一覧・再生・ダウンロード・削除 |
| **Slack 通知** | 録音完了時とエラー発生時に Slack へ通知 |
| **一時停止** | 録音・番組表更新のスケジューラ全体をブラウザから一時停止／再開 |
| **設定画面** | 番組表更新時刻・取得並列数・録音ファイル名テンプレートをブラウザから変更 |
| **ヘルスチェック** | `/healthz` でプロセスと DB 到達性を確認（デプロイ時のロールバック判定用） |

## 技術スタック

- **ランタイム** — .NET 10 / ASP.NET Core MVC
- **データベース** — PostgreSQL（Npgsql + Entity Framework Core 10、一括投入に Npgsql.Bulk）
- **ジョブスケジューラ** — Quartz.NET
- **録音** — ffmpeg / ffprobe（HLS セグメント取得とチャンク結合を委譲）
- **音声タグ** — z440.atl.core
- **通知** — SlackNet
- **フロントエンド** — Bootstrap 5 + 素の JavaScript（jQuery 非依存）

## 動作要件

以下は Docker を使わずに動かす場合の要件です。[Docker で使う場合](docs/docker.md)は Docker 以外に用意するものはありません。

| 要件 | 備考 |
|---|---|
| .NET 10 SDK / Runtime | ビルド時は SDK、実行のみなら ASP.NET Core Runtime |
| PostgreSQL | 動作確認は 18。`bytea` に録音データを格納するため容量に注意 |
| ffmpeg / ffprobe | **`PATH` 上に必要**。実行ファイル名で直接起動します |
| radiko プレミアムアカウント | 任意。エリアフリー録音を行う場合のみ |
| Slack Bot トークン | 任意。録音完了・エラー時の通知に使用 |

## セットアップ（Docker を使わない場合）

### 1. データベースの準備

データベースを作成します。

```bash
createdb -U postgres radicore
```

テーブルの作成や、バージョンアップに伴うスキーマ変更は、**アプリの起動時に自動で適用**されます（後述の[マイグレーション](#マイグレーション)を参照）。手動で SQL を実行する必要はありません。

作成されるテーブルは以下の 7 つです（このほか適用履歴を記録する `schema_migrations` が作られます）。各カラムの意味はエンティティクラスを参照してください。

| テーブル | 定義 |
|---|---|
| `stations` | [Station.cs](RadiCore/Data/Station.cs) |
| `programs` | [Program.cs](RadiCore/Data/Program.cs) |
| `areas` | [Area.cs](RadiCore/Data/Area.cs) |
| `reservations` | [Reservation.cs](RadiCore/Data/Reservation.cs) |
| `recordings` | [Recording.cs](RadiCore/Data/Recording.cs) |
| `recording_audio_data` | [RecordingAudioData.cs](RadiCore/Data/RecordingAudioData.cs) |
| `app_settings` | [AppSetting.cs](RadiCore/Data/AppSetting.cs) |

> [!NOTE]
> `stations_staging` / `programs_staging` はマイグレーションに含まれません。番組表更新ジョブが実行のたびに `CREATE TABLE ... (LIKE ... INCLUDING ALL)` で作成し、完了時に破棄する一時テーブルのためです。


### 2. 環境変数

| 変数名 | 必須 | 内容 |
|---|:---:|---|
| `RADICORE_CONNECTION_STRING` | ✅ | Npgsql 接続文字列。例: `Server=host; Port=5432; User Id=user; Password=pass; Database=radicore;` |
| `SLACK_BOT_TOKEN` | — | Slack Bot トークン（`xoxb-` で始まる） |
| `SLACK_NOTIFY_CHANNEL` | — | 通知先チャンネル ID |
| `RADIKO_MAIL` | — | radiko プレミアムのメールアドレス。**未設定ならフリー（エリア内）モードで動作** |
| `RADIKO_PASS` | — | radiko プレミアムのパスワード |
| `RADICORE_LOGOUT_URL` | — | ログアウトリンクの URL。未設定なら表示しない（[ログアウトリンク](#ログアウトリンク)を参照） |
| `ASPNETCORE_ENVIRONMENT` | — | `Production` / `Development` |

> [!NOTE]
> Slack 通知は `SLACK_BOT_TOKEN` と `SLACK_NOTIFY_CHANNEL` の両方が設定されている場合のみ行います。どちらかが未設定なら通知せずに動作します。

### 3. ビルドと実行

```bash
dotnet run --project RadiCore/RadiCore.csproj
```

既定で `http://localhost:5000` を待ち受けます（[appsettings.json](RadiCore/appsettings.json) の Kestrel 設定）。

本番向けの発行は以下です。

```bash
dotnet publish RadiCore.slnx -c Release --property:PublishDir=/path/to/deploy
```

## アプリケーション設定

以下は環境変数ではなく、`app_settings` テーブルに保存され**設定画面から変更**できます。

| キー | 既定値 | 範囲 | 内容 |
|---|---:|---|---|
| `RefreshHour` | `6` | 0–23 | 番組表更新ジョブの実行時刻（時） |
| `RefreshMinute` | `0` | 0–59 | 同（分） |
| `ParallelCount` | `10` | 1–50 | 番組表取得の並列数 |
| `FileNameTemplate` | `{date:yyyyMMdd}_{program}` | 200 文字以内 | 録音ファイル名のテンプレート（後述） |
| `SchedulerPaused` | `false` | true / false | スケジューラ全体の一時停止（後述） |

変更は Quartz のトリガーへ即時反映されます。DB へ到達できない起動時は既定値で続行します。

### 録音ファイル名のカスタマイズ

録音ファイル名は、番組のメタデータを埋め込んだテンプレートで指定できます。設定画面の「録音ファイル名」カードで編集でき、入力するとサンプルデータでのプレビューが表示されます。

| プレースホルダー | 内容 |
|---|---|
| `{program}` | 番組名 |
| `{cast}` | 出演者 |
| `{station}` | 放送局名 |
| `{stationId}` | 放送局 ID |
| `{programId}` | 番組 ID |
| `{date}` | 放送日（既定: `yyyyMMdd`） |
| `{start}` | 録音開始日時（既定: `HHmm`） |
| `{end}` | 録音終了日時（既定: `HHmm`） |
| `{reservationId}` | 予約 ID |

`{date}` `{start}` `{end}` は `{date:yyyy-MM-dd}` のように .NET の日時書式を指定できます。

- 拡張子 `.m4a` は自動で付与されるため、テンプレートには含めません
- ファイル名に使えない文字（`/` `\` `:` `*` `?` `"` `<` `>` `|`）は `_` に置換されます
- 展開後の長さは UTF-8 で 200 バイトに切り詰められます
- 展開結果が空になる場合は既定のテンプレートにフォールバックします
- 先頭がハイフンになる場合や Windows の予約デバイス名（`CON` など）になる場合は、先頭に `_` が付きます

ここで決まったファイル名は `recordings.file_name` に保存され、ライブラリからのダウンロード時のファイル名としても使われます。

### スケジューラの一時停止

設定画面の「スケジューラ」カードのスイッチで、Quartz のスケジューラ全体を一時停止・再開できます。一時停止中は画面上部に警告バーが常時表示されます。

- 一時停止中は**予約録音・番組表の自動更新・番組表の手動実行・前回分の録音のいずれも実行されません**（予約の登録・編集・削除は通常どおり行えます）
- 一時停止中に予定時刻を過ぎた**繰り返し録音と番組表更新は、再開時にまとめて実行されず**、次回の予定時刻を待ちます（Quartz のミスファイア方針を `DoNothing` に設定）
- 単発（`Once`）予約は一時停止中に時刻を過ぎても取りこぼさないよう、再開時に一度だけ実行されます
- 状態は `app_settings.SchedulerPaused` に保存されるため、**アプリを再起動しても一時停止のまま**です

## 動作の仕組み

### 番組表の更新

Quartz の Cron トリガーで 1 日 1 回起動し、全放送局の週間番組表を取得します。反映は以下の手順で行い、取得途中の失敗で番組表が欠損するのを防ぎます。

1. `stations_staging` / `programs_staging` を `CREATE TABLE ... (LIKE ... INCLUDING ALL)` で作成
2. 取得結果を staging へ一括投入（Npgsql.Bulk）
3. `ALTER TABLE ... RENAME` で本テーブルと入れ替え、旧テーブルを破棄

> [!IMPORTANT]
> `programs.p_id` の既定値が参照する `stations_id_seq` は、**どのテーブルにも `OWNED BY` されていません**。これは意図的で、手順 3 で旧テーブルを `DROP` した際にシーケンスまで巻き込まれないようにするためです。スキーマを手で作り直す場合、このシーケンスに `OWNED BY` を付けると 2 回目の番組表更新で失敗します。

### 録音

予約時刻になると `RecordingJob` が起動し、radiko の認証（プレミアム設定時はエリアフリーログイン）を経て HLS プレイリストを取得します。実際のセグメント取得と結合は ffmpeg に委譲し、長時間番組はチャンク分割して結合します。

完成した m4a（`audio/mp4`）は **PostgreSQL の `recording_audio_data.audio_data`（bytea）に格納**されます。

> [!NOTE]
> 音声をファイルシステムではなく DB に格納するのは意図的な設計です。**バックアップとリストアを容易にすること**を目的としています。
>
> 音声データとメタデータが単一のデータベースに集約されるため、`pg_dump` 一回でバックアップが完結し、リストアも同様に一回で済みます。ファイルツリーと DB の整合性を別途管理する必要がなく、「DB には録音レコードがあるのにファイルが無い」といった不整合が原理的に発生しません。移設もダンプファイル 1 つで完了します。
>
> 反面、運用が長期化すると DB サイズは増大します。繰り返し予約の `auto_delete_previous`（前回分の自動削除）と併用して調整してください。

### 起動時の予約復元

`ReservationBootstrapService` が起動時に DB の予約を読み込み、Quartz へ再登録します。プロセス再起動をまたいで予約が維持されます。

## セキュリティ

> [!CAUTION]
> **本アプリケーションは認証・認可の機構を持ちません。** アクセス制御は前段のリバースプロキシに委ねる設計です。
>
> 作者の環境では Cloudflare Access を前段に置いています。**インターネットに直接公開しないでください。**

### ログアウトリンク

RadiCore 自体にはログイン・ログアウトの機能がありません。画面のナビゲーションに表示できる「ログアウト」は、**前段に置いた認証プロキシのセッションを破棄するためのリンク**です。

もともとは作者自身の環境（Cloudflare Access で保護）専用に、Cloudflare Access のログアウト用パス `/cdn-cgi/access/logout` を固定で表示していました。しかし Cloudflare Access を使っていない環境ではこのリンクは機能しないため、現在は環境変数 `RADICORE_LOGOUT_URL` を設定した場合のみ表示し、**未設定なら表示しません**。

| 前段の構成 | `RADICORE_LOGOUT_URL` の例 |
|---|---|
| 認証プロキシなし（LAN 内・VPN 経由など） | 設定しない |
| Cloudflare Access（作者の環境） | `/cdn-cgi/access/logout` |
| oauth2-proxy | `/oauth2/sign_out` |
| その他 | 利用している認証プロキシのログアウト URL |

`/healthz` も認証なしで応答します。判定対象はプロセスの応答性と DB 到達性のみで、radiko への到達性や録音ジョブの状態は含みません（外部要因の障害でデプロイがロールバックされるのを避けるため）。

## エンドポイント

| パス | 内容 |
|---|---|
| `/` | 番組表（既定画面） |
| `/Reservation` | 予約一覧・登録・編集 |
| `/Library` | 録音ライブラリ |
| `/Settings` | アプリケーション設定 |
| `/healthz` | ヘルスチェック |

## 開発

```bash
dotnet build RadiCore.slnx
```

テストは MSTest です。DB 接続を必要とするため、接続情報は `RadiCore.Test/test.runsettings` に定義します（このファイルは `.gitignore` 対象です）。

```bash
dotnet test RadiCore.slnx --settings RadiCore.Test/test.runsettings
```

マイグレーションを実際の PostgreSQL で検証するテスト（`DatabaseMigratorIntegrationTest`）は、環境変数 `RADICORE_MIGRATION_TEST_CONNECTION_STRING` に `CREATE DATABASE` できるユーザーの接続文字列を設定した場合のみ実行されます。テストごとに一時データベースを作成・削除するため、既存の DB には触れません。GitHub Actions の [CI](.github/workflows/ci.yml) では PostgreSQL 18 のサービスコンテナに対して毎回実行しています。

### マイグレーション

スキーマは [RadiCore/Migrations/](RadiCore/Migrations/) の番号付き SQL で管理し、起動時に [DatabaseMigrator.cs](RadiCore/Infrastructure/DatabaseMigrator.cs) が未適用のものを番号順に適用します。SQL はアセンブリに埋め込まれるため、利用者はイメージ（またはバイナリ）を更新するだけでスキーマが追従します。

| ファイル | 内容 |
|---|---|
| `0001_baseline.sql` | 初期スキーマ（導入時点の `docs/schema.sql`） |
| `0002_seed_areas.sql` | `areas` の初期データ（47 都道府県） |

- 適用済みのバージョンは `schema_migrations` テーブルに記録されます
- 各マイグレーションは個別のトランザクションで実行されます。**失敗した場合はアプリの起動を中止します**（スキーマが中途半端なまま録音・番組表更新を動かさないため）。DB に接続できないだけの場合は起動を続けます
- 複数のプロセスが同時に起動しても二重に適用しないよう、アドバイザリロックで排他します
- マイグレーション導入前から稼働している DB（`reservations` はあるが `schema_migrations` が無い）は、`0001_baseline` を適用済みとして記録し、`0002` 以降から適用します

**スキーマを変更するときのルール:**

1. `RadiCore/Migrations/` に次の番号のファイルを追加します（例: `0003_add_recordings_memo.sql`）。ファイル名は `NNNN_小文字英数字とアンダースコア.sql` です
2. **適用済みのファイルは編集しません。** 修正が必要な場合も新しい番号のファイルを追加します
3. イメージを 1 つ前に戻しても DDL は戻らないため、**古いコードでも動く変更**（列の追加・NULL 許容・既定値付きなど）にします。列の削除や名前変更は「新しい列を追加 → コードを切り替え → 次のリリースで古い列を削除」の 2 段階で行います
4. `stations` / `programs` への変更も有効です。番組表更新は `LIKE ... INCLUDING ALL` で現在のテーブルを複製して入れ替えるため、追加した列やインデックスは引き継がれます
5. psql 専用のメタコマンド（`\restrict` など）は使えません（Npgsql で直接実行するため。テストで検出します）

[docs/schema.sql](docs/schema.sql) は現在のスキーマを確認するための**参考資料**で、実行には使われません。スキーマを変更したら稼働中の DB から出力し直してください。

```bash
pg_dump -U noobow --schema-only --no-owner --no-privileges radicore > docs/schema.sql
```

`--no-owner --no-privileges` は特定ロールへの依存を除くためです。PostgreSQL 18 の `pg_dump` は先頭と末尾に `\restrict` / `\unrestrict` を出力しますが、古い psql で実行できなくなるため除いてからコミットしてください。

### Docker イメージの公開

`v` で始まるタグを push すると、[.github/workflows/docker.yml](.github/workflows/docker.yml) が `ghcr.io/noobow34/radicore` へ `linux/amd64` と `linux/arm64` のイメージを公開します。

```bash
git tag v1.0.0 && git push origin v1.0.0
```

初回公開時のパッケージは非公開のため、GitHub の Packages 設定から Public に変更してください。

タグ名は **`v` で始まれば何でも構いません**（起動条件は `v*`）。付与されるイメージのタグは形式によって変わります。

| タグ名 | 付与されるイメージのタグ |
|---|---|
| `v1.0.0` | `v1.0.0` / `1.0.0` / `1.0` / `1` / `latest` |
| `v2026.9.18` | `v2026.9.18` / `2026.9.18` / `2026.9` / `2026` / `latest` |
| `v20260918` | `v20260918` / `latest` |
| `v1.0` | `v1.0` / `latest` |

タグ名そのもの（`v` 付き）と `latest` はどの形式でも付きます。`メジャー.マイナー.パッチ` の形式のときだけ、`v` を除いたバージョンと `1.0` / `1` のような部分指定が追加され、マイナーバージョン単位での固定ができます。それ以外の形式では実行ログに semver として解釈できない旨の警告が出ますが、ビルドと公開は行われます。

公開せずにビルドの成否だけを確認したい場合は、GitHub の Actions タブから `Docker image` ワークフローを手動実行（Run workflow）してください。タグを打つ前に Dockerfile の問題を洗い出せます。

## ライセンス

本プロジェクトは [MIT License](LICENSE) のもとで公開しています。

`RadiCore/Tools/rec_radiko_ts.sh` は uru 氏による [rec_radiko_ts](https://github.com/uru2/rec_radiko_ts) を同梱したもので、同じく MIT License です（[LICENSE](RadiCore/Tools/LICENSE)）。録音処理の C# 化に伴い**実行時には使用していません**が、radiko 側の仕様変更を追跡する参照実装としてソースツリーに残しています。

## 謝辞

- [uru2/rec_radiko_ts](https://github.com/uru2/rec_radiko_ts) — uru 氏によるシェルスクリプト実装。radiko のタイムフリー録音手順の参照実装として活用させていただきました。

## 免責

- 本ソフトウェアは作者個人の利用を主目的として開発されています。配布・汎用利用を前提とした作りにはなっていません（冒頭の「はじめにお読みください」を参照）。
- radiko の利用にあたっては [radiko の利用規約](https://radiko.jp/rule/)に従ってください。録音物の利用は私的使用の範囲に留めてください。
- 本ソフトウェアの使用によって生じたいかなる損害についても、作者は責任を負いません。
