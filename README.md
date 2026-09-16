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

| 要件 | 備考 |
|---|---|
| .NET 10 SDK / Runtime | ビルド時は SDK、実行のみなら ASP.NET Core Runtime |
| PostgreSQL | 動作確認は 18。`bytea` に録音データを格納するため容量に注意 |
| ffmpeg / ffprobe | **`PATH` 上に必要**。実行ファイル名で直接起動します |
| radiko プレミアムアカウント | 任意。エリアフリー録音を行う場合のみ |
| Slack Bot トークン | 任意。録音完了・エラー時の通知に使用 |

## Docker で動かす

アプリ本体・ASP.NET Core Runtime・ffmpeg を含むイメージを GitHub Container Registry（`ghcr.io/noobow34/radicore`）で配布しています。対応アーキテクチャは `linux/amd64` と `linux/arm64`（Raspberry Pi 等）です。データベースには PostgreSQL の公式イメージを使います。

### 必要なもの

- Docker Engine と Docker Compose v2（`docker compose` コマンド）。Docker Desktop にはどちらも含まれます

使うファイルは `docker-compose.yml` と `.env` の 2 つだけです。リポジトリの clone は不要です。

### 1. ファイルを用意する

作業用のフォルダを作り、その中に 2 つのファイルをダウンロードします。

```bash
mkdir radicore && cd radicore
```

```bash
curl -fsSL -o docker-compose.yml https://raw.githubusercontent.com/noobow34/RadiCore/master/docker-compose.yml
```

```bash
curl -fsSL -o .env https://raw.githubusercontent.com/noobow34/RadiCore/master/.env.example
```

Windows の PowerShell 5.1 では `curl` が別コマンドの別名になっているため、`curl.exe` と入力してください。`curl` が使えない場合は、ブラウザで [docker-compose.yml](docker-compose.yml) と [.env.example](.env.example) を開いて同じフォルダに保存し、`.env.example` を `.env` に名前変更してください。

### 2. 設定する

`.env` をテキストエディタで開き、値を設定します。

| 変数名 | 必須 | 内容 |
|---|:---:|---|
| `POSTGRES_USER` | ✅ | データベースのユーザー名（任意の値） |
| `POSTGRES_PASSWORD` | ✅ | データベースのパスワード（任意の値）。**ひな形の `change-me` から必ず変更してください** |
| `POSTGRES_DB` | ✅ | データベース名（任意の値） |
| `RADICORE_BIND` | — | 待ち受けるアドレス。既定 `127.0.0.1`（このマシンからのみ接続可） |
| `RADICORE_PORT` | — | ブラウザで開くポート番号。既定 `8080` |
| `RADIKO_MAIL` | — | radiko プレミアムのメールアドレス。未設定ならエリア内の放送局のみ録音できます |
| `RADIKO_PASS` | — | radiko プレミアムのパスワード |
| `RADICORE_LOGOUT_URL` | — | 画面右上に表示するログアウトリンクの URL。未設定なら表示しません（[ログアウトリンク](#ログアウトリンク)を参照） |
| `SLACK_BOT_TOKEN` | — | Slack 通知用の Bot トークン（`xoxb-` で始まる） |
| `SLACK_NOTIFY_CHANNEL` | — | Slack 通知先のチャンネル ID。トークンと両方設定した場合のみ通知します |

> [!TIP]
> PostgreSQL は `docker compose` が**専用のコンテナとして自動で用意**します。`POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_DB` は、そのコンテナ内にデータベースを新しく作るときの設定値です。
> **事前に PostgreSQL をインストールしたり、既存のデータベースの接続情報を調べたりする必要はありません。** 好きな値を決めて記入してください（ユーザー名と DB 名はひな形の `radicore` のままで構いません）。
> アプリはこの値を使ってコンテナ内のデータベースへ自動で接続します。

> [!IMPORTANT]
> `POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_DB` は**初回起動時にのみ**データベースへ反映されます。起動後に `.env` だけを書き換えても DB 側は変わらず、接続できなくなります。

### 3. 起動する

```bash
docker compose up -d
```

初回はイメージのダウンロードが行われ、アプリの起動時に空のデータベースへテーブルとエリア名の初期データが自動で作成されます。起動状態は次のコマンドで確認できます（`app` が `healthy` になれば準備完了です）。

```bash
docker compose ps
```

ブラウザで `http://localhost:8080` を開きます（ポートを変更した場合はその番号）。

### 4. 番組表を取り込む

初回起動直後は番組表が空です。画面上部の「設定」を開き、**「番組表の手動更新」の「今すぐ実行」** を押してください。全国の放送局を取得するため数分かかります。以降は毎日 6:00（設定画面で変更可）に自動で更新されます。

番組表が表示されたら、番組を選んで予約できます。

### 他の PC やスマートフォンから使う

既定では Docker を動かしているマシン自身からしか開けません。LAN 内の他の端末から使う場合は `.env` を次のように変更し、`docker compose up -d` で反映します。

```dotenv
RADICORE_BIND=0.0.0.0
```

> [!CAUTION]
> RadiCore には**ログイン機能がありません**。URL にアクセスできる人は誰でも予約・録音の削除ができます。ルーターのポート開放などで**インターネットへ直接公開しないでください**。外出先から使う場合は、VPN（Tailscale 等）や認証付きのリバースプロキシ（Cloudflare Access 等）を経由してください。

### 日常の操作

| やりたいこと | コマンド |
|---|---|
| 停止 | `docker compose stop` |
| 再開 | `docker compose start` |
| ログを見る | `docker compose logs -f app` |
| `.env` の変更を反映 | `docker compose up -d` |

録音や番組表更新の経過はアプリのログに出力されます。録音に失敗したときはまず `docker compose logs app` を確認してください。

### アップデート

```bash
docker compose pull
```

```bash
docker compose up -d
```

> [!WARNING]
> データベースのスキーマ変更は自動では適用されません。新しいバージョンでテーブル定義が変わる場合はリリースノートに記載しますので、その手順に従ってください。

特定のバージョンに固定したい場合は、`docker-compose.yml` の `image:` を `ghcr.io/noobow34/radicore:1.0.0` のようにバージョン指定に変更します。

### データの保存場所

録音データを含むすべてのデータは、Docker の名前付きボリューム `radicore_db-data` に保存されています。コンテナとは独立しているため、**イメージの更新やコンテナの作り直し（`docker compose pull` / `up -d` / `down`）ではデータは消えません**。

ボリュームは次のコマンドで確認できます。

```bash
docker volume ls --filter name=radicore
```

> [!WARNING]
> 次の操作ではデータが失われる、または読めなくなります。
>
> - **`docker compose down -v`** — ボリュームごと削除されます
> - **`docker volume prune` / `docker system prune --volumes`** — コンテナから使われていないボリュームが削除されます。RadiCore を停止・`down` している間に実行すると録音データが消えます
> - **`docker-compose.yml` の `postgres:18` を `postgres:19` などに書き換える** — PostgreSQL はメジャーバージョン間でデータ形式に互換性が無く、起動できなくなります。上げる場合は、下記の手順でバックアップを取り、新しいボリュームへ復元してください
>
> なお録音の実行中にコンテナを作り直すと、その録音だけは失敗します（保存済みの録音には影響しません）。

### バックアップと復元

バックアップはダンプファイル 1 つで完結します。以下は `.env` が既定値（ユーザー・DB 名とも `radicore`）の場合の例です。

バックアップ:

```bash
docker compose exec -T db pg_dump -U radicore -Fc radicore > radicore.dump
```

復元（既存のデータは上書きされます）:

```bash
docker compose exec -T db pg_restore -U radicore -d radicore --clean --if-exists < radicore.dump
```

録音が増えるとダンプファイルも大きくなります。保存先の空き容量に注意してください。

### アンインストール

コンテナを削除します（データは残ります）。

```bash
docker compose down
```

**録音データを含むデータベースもすべて削除する**場合は `-v` を付けます。元に戻せません。

```bash
docker compose down -v
```

### 補足

- **タイムゾーン** — 番組表・予約時刻を日本時間で扱うため、コンテナは `TZ=Asia/Tokyo` で動作します。ホストのタイムゾーン設定は影響しません
- **聴取エリア** — radiko プレミアム未設定時は、Docker を動かしているマシンのグローバル IP アドレスで聴取エリアが判定されます。VPN 経由などで海外や別地域の IP になっていると録音できません
- **ソースからビルド** — リポジトリを clone し、`docker-compose.yml` の `image:` 行を `build: .` に置き換えて `docker compose up -d --build` を実行します

### イメージの公開（メンテナー向け）

`v1.2.3` 形式のタグを push すると、[.github/workflows/docker.yml](.github/workflows/docker.yml) が `ghcr.io/noobow34/radicore` へ `1.2.3` / `1.2` / `1` / `latest` タグでイメージを公開します。

```bash
git tag v1.0.0 && git push origin v1.0.0
```

初回公開時のパッケージは非公開のため、GitHub の Packages 設定から Public に変更してください。

## セットアップ（Docker を使わない場合）

### 1. データベースの準備

データベースを作成します。

```bash
createdb -U postgres radicore
```

テーブル定義（[docs/schema.sql](docs/schema.sql)）とエリア名の初期データ（[docs/seed.sql](docs/seed.sql)）は、**アプリの起動時に自動で適用**されます（[DatabaseInitializer.cs](RadiCore/Infrastructure/DatabaseInitializer.cs)）。`reservations` テーブルが無ければ `schema.sql` を、`areas` が空なら `seed.sql` を実行し、既存のデータベースには何もしません。どちらの SQL もアセンブリに埋め込まれるため、実行環境に `docs/` を配置する必要はありません。

手動で適用する場合は以下です。

```bash
psql -U postgres -d radicore -f docs/schema.sql -f docs/seed.sql
```

[docs/schema.sql](docs/schema.sql) は稼働中のデータベースから `pg_dump --schema-only` で出力したものです。EF Core Migrations は使用していないため、スキーマを変更した際はこのファイルを更新してください。

```bash
pg_dump -U noobow --schema-only --no-owner --no-privileges radicore > docs/schema.sql
```

`--no-owner --no-privileges` は特定ロールへの依存を除くためです。なお PostgreSQL 18 の `pg_dump` は先頭と末尾に `\restrict` / `\unrestrict` メタコマンドを出力しますが、古い psql クライアントで実行できなくなるうえ、起動時の自動適用（Npgsql で直接実行）でも失敗するため、**必ず除いてから**コミットしてください。

適用されるテーブルは以下の 7 つです。各カラムの意味はエンティティクラスを参照してください。

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
> `stations_staging` / `programs_staging` は `docs/schema.sql` に含まれません。番組表更新ジョブが実行のたびに `CREATE TABLE ... (LIKE ... INCLUDING ALL)` で作成し、完了時に破棄する一時テーブルのためです。


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

## ライセンス

本プロジェクトは [MIT License](LICENSE) のもとで公開しています。

`RadiCore/Tools/rec_radiko_ts.sh` は uru 氏による [rec_radiko_ts](https://github.com/uru2/rec_radiko_ts) を同梱したもので、同じく MIT License です（[LICENSE](RadiCore/Tools/LICENSE)）。録音処理の C# 化に伴い**実行時には使用していません**が、radiko 側の仕様変更を追跡する参照実装としてソースツリーに残しています。

## 謝辞

- [uru2/rec_radiko_ts](https://github.com/uru2/rec_radiko_ts) — uru 氏によるシェルスクリプト実装。radiko のタイムフリー録音手順の参照実装として活用させていただきました。

## 免責

- 本ソフトウェアは作者個人の利用を主目的として開発されています。配布・汎用利用を前提とした作りにはなっていません（冒頭の「はじめにお読みください」を参照）。
- radiko の利用にあたっては [radiko の利用規約](https://radiko.jp/rule/)に従ってください。録音物の利用は私的使用の範囲に留めてください。
- 本ソフトウェアの使用によって生じたいかなる損害についても、作者は責任を負いません。
