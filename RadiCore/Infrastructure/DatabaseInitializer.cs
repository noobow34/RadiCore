using Npgsql;
using System.Reflection;

namespace RadiCore.Infrastructure
{
    /// <summary>
    /// 起動時に空のデータベースへテーブル定義と初期データを適用する。
    /// Docker 利用時に schema.sql / seed.sql を別途配置・実行しなくて済むようにするため、
    /// docs/ 配下の SQL をアセンブリに埋め込んで実行する。
    /// </summary>
    public static class DatabaseInitializer
    {
        // 番組表更新で RENAME されるテーブル（stations / programs）は判定に使わない
        private const string SchemaExistsSql = "SELECT to_regclass('public.reservations') IS NOT NULL";
        private const string AreasEmptySql   = "SELECT NOT EXISTS (SELECT 1 FROM public.areas)";

        /// <summary>
        /// テーブルが無ければ schema.sql を、areas が空なら seed.sql を適用する。
        /// 既存のデータベースに対しては何もしない。
        /// </summary>
        /// <returns>何らかの SQL を適用した場合 true</returns>
        public static async Task<bool> InitializeAsync(string connectionString, Action<string> log)
        {
            // schema.sql は search_path をセッション単位で変更するため、プールに戻さない接続で実行する
            var csb = new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false };
            await using var conn = new NpgsqlConnection(csb.ConnectionString);
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            bool applied = false;

            if (!await ScalarBoolAsync(conn, tx, SchemaExistsSql))
            {
                log("テーブルが存在しないため schema.sql を適用します");
                await ExecuteAsync(conn, tx, ReadResource("schema.sql"));
                applied = true;
            }

            if (await ScalarBoolAsync(conn, tx, AreasEmptySql))
            {
                log("areas が空のため seed.sql を適用します");
                await ExecuteAsync(conn, tx, ReadResource("seed.sql"));
                applied = true;
            }

            await tx.CommitAsync();
            if (applied)
                log("データベースの初期化が完了しました");
            return applied;
        }

        /// <summary>埋め込みリソースの SQL を読み込む</summary>
        public static string ReadResource(string name)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"埋め込みリソース {name} が見つかりません");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private static async Task<bool> ScalarBoolAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            return (bool)(await cmd.ExecuteScalarAsync())!;
        }

        private static async Task ExecuteAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
