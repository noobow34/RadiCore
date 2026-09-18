# Docker で RadiCore を使う

[← README に戻る](../README.md)

> [!CAUTION]
> RadiCore は作者個人の利用を主目的として開発されています。ご利用前に [README 冒頭の注意事項](../README.md)をお読みください。

アプリ本体・ASP.NET Core Runtime・ffmpeg を含むイメージを GitHub Container Registry（`ghcr.io/noobow34/radicore`）で配布しています。対応アーキテクチャは `linux/amd64` と `linux/arm64`（Raspberry Pi 等）です。データベースには PostgreSQL の公式イメージを使います。

## 必要なもの

- Docker Engine と Docker Compose v2（`docker compose` コマンド）。Docker Desktop にはどちらも含まれます

使うファイルは `docker-compose.yml` と `.env` の 2 つだけです。リポジトリの clone は不要です。

## 1. ファイルを用意する

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

Windows の PowerShell 5.1 では `curl` が別コマンドの別名になっているため、`curl.exe` と入力してください。`curl` が使えない場合は、ブラウザで [docker-compose.yml](../docker-compose.yml) と [.env.example](../.env.example) を開いて同じフォルダに保存し、`.env.example` を `.env` に名前変更してください。

## 2. 設定する

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
| `RADICORE_LOGOUT_URL` | — | 画面右上に表示するログアウトリンクの URL。未設定なら表示しません（[README のログアウトリンク](../README.md#ログアウトリンク)を参照） |
| `SLACK_BOT_TOKEN` | — | Slack 通知用の Bot トークン（`xoxb-` で始まる） |
| `SLACK_NOTIFY_CHANNEL` | — | Slack 通知先のチャンネル ID。トークンと両方設定した場合のみ通知します |

> [!TIP]
> PostgreSQL は `docker compose` が**専用のコンテナとして自動で用意**します。`POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_DB` は、そのコンテナ内にデータベースを新しく作るときの設定値です。
> **事前に PostgreSQL をインストールしたり、既存のデータベースの接続情報を調べたりする必要はありません。** 好きな値を決めて記入してください（ユーザー名と DB 名はひな形の `radicore` のままで構いません）。
> アプリはこの値を使ってコンテナ内のデータベースへ自動で接続します。

> [!IMPORTANT]
> `POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_DB` は**初回起動時にのみ**データベースへ反映されます。起動後に `.env` だけを書き換えても DB 側は変わらず、接続できなくなります。

## 3. 起動する

```bash
docker compose up -d
```

初回はイメージのダウンロードが行われ、アプリの起動時にデータベースへテーブルとエリア名の初期データが自動で作成されます。起動状態は次のコマンドで確認できます（`app` が `healthy` になれば準備完了です）。

```bash
docker compose ps
```

ブラウザで `http://localhost:8080` を開きます（ポートを変更した場合はその番号）。

## 4. 番組表を取り込む

初回起動直後は番組表が空です。画面上部の「設定」を開き、**「番組表の手動更新」の「今すぐ実行」** を押してください。全国の放送局を取得するため数分かかります。以降は毎日 6:00（設定画面で変更可）に自動で更新されます。

番組表が表示されたら、番組を選んで予約できます。

## 他の PC やスマートフォンから使う

既定では Docker を動かしているマシン自身からしか開けません。LAN 内の他の端末から使う場合は `.env` を次のように変更し、`docker compose up -d` で反映します。

```dotenv
RADICORE_BIND=0.0.0.0
```

> [!CAUTION]
> RadiCore には**ログイン機能がありません**。URL にアクセスできる人は誰でも予約・録音の削除ができます。ルーターのポート開放などで**インターネットへ直接公開しないでください**。外出先から使う場合は、VPN（Tailscale 等）や認証付きのリバースプロキシ（Cloudflare Access 等）を経由してください。

## 日常の操作

| やりたいこと | コマンド |
|---|---|
| 停止 | `docker compose stop` |
| 再開 | `docker compose start` |
| ログを見る | `docker compose logs -f app` |
| `.env` の変更を反映 | `docker compose up -d` |

録音や番組表更新の経過はアプリのログに出力されます。録音に失敗したときはまず `docker compose logs app` を確認してください。

## アップデート

```bash
docker compose pull
```

```bash
docker compose up -d
```

新しいバージョンでデータベースのテーブル定義が変わる場合も、**アプリの起動時に自動で更新されます**。SQL を手で実行する必要はありません。

> [!TIP]
> 念のため、アップデートの前に[バックアップ](#バックアップと復元)を取っておくことをおすすめします。データベースの更新は元に戻せないため、問題が起きたときに以前の状態へ戻せるようにするためです。
>
> データベースの更新に失敗した場合、アプリは起動せず停止します（`docker compose logs app` に原因が出力されます）。

特定のバージョンに固定したい場合は、`docker-compose.yml` の `image:` を `ghcr.io/noobow34/radicore:1.0.0` のようにバージョン指定に変更します。

## データの保存場所

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

## バックアップと復元

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

## アンインストール

コンテナを削除します（データは残ります）。

```bash
docker compose down
```

**録音データを含むデータベースもすべて削除する**場合は `-v` を付けます。元に戻せません。

```bash
docker compose down -v
```

## 補足

- **タイムゾーン** — 番組表・予約時刻を日本時間で扱うため、コンテナは `TZ=Asia/Tokyo` で動作します。ホストのタイムゾーン設定は影響しません
- **聴取エリア** — radiko プレミアム未設定時は、Docker を動かしているマシンのグローバル IP アドレスで聴取エリアが判定されます。VPN 経由などで海外や別地域の IP になっていると録音できません
- **ソースからビルド** — リポジトリを clone し、`docker-compose.yml` の `image:` 行を `build: .` に置き換えて `docker compose up -d --build` を実行します
