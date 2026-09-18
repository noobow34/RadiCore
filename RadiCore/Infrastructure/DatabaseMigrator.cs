using Npgsql;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RadiCore.Infrastructure
{
    /// <summary>
    /// 起動時に未適用のマイグレーション（Migrations/NNNN_名前.sql）を番号順に適用する。
    /// SQL はアセンブリに埋め込むため、利用者はイメージ（またはバイナリ）を更新するだけでスキーマが追従する。
    /// 適用済みのバージョンは public.schema_migrations に記録する。
    /// </summary>
    public static partial class DatabaseMigrator
    {
        /// <summary>初期スキーマのバージョン。マイグレーション導入前から稼働している DB では適用済みとして記録だけする</summary>
        public const string BaselineVersion = "0001_baseline";

        private const string ResourcePrefix = "Migrations.";

        // 複数プロセスが同時に起動しても二重適用しないためのアドバイザリロックのキー（任意の固定値）
        private const long AdvisoryLockKey = 7_246_318_905_531_001;

        [GeneratedRegex(@"^\d{4}_[a-z0-9_]+$")]
        private static partial Regex VersionPattern();

        /// <summary>埋め込まれたマイグレーションを番号順に返す</summary>
        public static IReadOnlyList<Migration> GetMigrations()
        {
            var assembly = Assembly.GetExecutingAssembly();
            return assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.Ordinal))
                .Select(n => new Migration(n[ResourcePrefix.Length..^".sql".Length], ReadResource(assembly, n)))
                .OrderBy(m => m.Version, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// 未適用のマイグレーションを番号順に適用する。
        /// 各マイグレーションは個別のトランザクションで実行し、失敗した時点で <see cref="MigrationException"/> を送出する。
        /// </summary>
        /// <returns>適用したバージョンの一覧</returns>
        public static async Task<IReadOnlyList<string>> MigrateAsync(string connectionString, Action<string> log)
        {
            // マイグレーションの SQL がセッション設定（search_path 等）を変更してもプールへ持ち越さないようにする
            var csb = new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false };
            await using var conn = new NpgsqlConnection(csb.ConnectionString);
            await conn.OpenAsync();

            await ExecuteAsync(conn, null, $"SELECT pg_advisory_lock({AdvisoryLockKey})");
            try
            {
                await ExecuteAsync(conn, null, """
                    CREATE TABLE IF NOT EXISTS public.schema_migrations (
                        version    text PRIMARY KEY,
                        applied_at timestamp with time zone NOT NULL DEFAULT now()
                    )
                    """);

                var applied = await GetAppliedVersionsAsync(conn);

                // マイグレーション導入前の DB（テーブルはあるが記録が無い）は、初期スキーマを適用済みとして扱う
                if (applied.Count == 0 && await ScalarBoolAsync(conn, "SELECT to_regclass('public.reservations') IS NOT NULL"))
                {
                    log($"既存のデータベースを検出したため {BaselineVersion} を適用済みとして記録します");
                    await RecordAsync(conn, null, BaselineVersion);
                    applied.Add(BaselineVersion);
                }

                var result = new List<string>();
                foreach (var migration in GetMigrations().Where(m => !applied.Contains(m.Version)))
                {
                    log($"マイグレーション {migration.Version} を適用します");
                    try
                    {
                        await using var tx = await conn.BeginTransactionAsync();
                        await ExecuteAsync(conn, tx, migration.Sql);
                        await RecordAsync(conn, tx, migration.Version);
                        await tx.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        throw new MigrationException(migration.Version, ex);
                    }

                    // SQL が変更したセッション設定を戻し、次のマイグレーションに影響させない
                    await ExecuteAsync(conn, null, "RESET ALL");
                    result.Add(migration.Version);
                }

                if (result.Count > 0)
                    log($"マイグレーションを {result.Count} 件適用しました");
                return result;
            }
            finally
            {
                await ExecuteAsync(conn, null, $"SELECT pg_advisory_unlock({AdvisoryLockKey})");
            }
        }

        private static async Task<HashSet<string>> GetAppliedVersionsAsync(NpgsqlConnection conn)
        {
            var versions = new HashSet<string>(StringComparer.Ordinal);
            await using var cmd = new NpgsqlCommand("SELECT version FROM public.schema_migrations", conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                versions.Add(reader.GetString(0));
            return versions;
        }

        private static async Task RecordAsync(NpgsqlConnection conn, NpgsqlTransaction? tx, string version)
        {
            await using var cmd = new NpgsqlCommand("INSERT INTO public.schema_migrations (version) VALUES (@version)", conn, tx);
            cmd.Parameters.AddWithValue("version", version);
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task<bool> ScalarBoolAsync(NpgsqlConnection conn, string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            return (bool)(await cmd.ExecuteScalarAsync())!;
        }

        private static async Task ExecuteAsync(NpgsqlConnection conn, NpgsqlTransaction? tx, string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            await cmd.ExecuteNonQueryAsync();
        }

        private static string ReadResource(Assembly assembly, string name)
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        /// <summary>バージョン名が NNNN_名前（小文字英数字とアンダースコア）の形式か</summary>
        public static bool IsValidVersion(string version) => VersionPattern().IsMatch(version);

        public sealed record Migration(string Version, string Sql);
    }

    /// <summary>マイグレーションの SQL 実行に失敗した（DB への接続失敗とは区別する）</summary>
    public sealed class MigrationException(string version, Exception inner)
        : Exception($"マイグレーション {version} の適用に失敗しました: {inner.Message}", inner)
    {
        public string Version { get; } = version;
    }
}
