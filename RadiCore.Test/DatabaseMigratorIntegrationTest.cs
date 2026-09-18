using Npgsql;
using RadiCore.Infrastructure;

namespace RadiCore.Test
{
    /// <summary>
    /// 実際の PostgreSQL にマイグレーションを適用する検証。
    /// 環境変数 RADICORE_MIGRATION_TEST_CONNECTION_STRING（CREATE DATABASE できるユーザーの接続文字列）が
    /// 設定されている場合のみ実行する。テストごとに一時データベースを作成・削除するため、既存の DB には触れない。
    /// </summary>
    [TestClass]
    public sealed class DatabaseMigratorIntegrationTest
    {
        private const string ConnectionStringVariable = "RADICORE_MIGRATION_TEST_CONNECTION_STRING";

        [TestMethod]
        public async Task 空のDBに全マイグレーションを適用できる()
        {
            await using var db = await TemporaryDatabase.CreateAsync();

            var applied = await DatabaseMigrator.MigrateAsync(db.ConnectionString, _ => { });

            CollectionAssert.AreEqual(DatabaseMigrator.GetMigrations().Select(m => m.Version).ToList(), applied.ToList());
            Assert.AreEqual(47L, await db.ScalarAsync<long>("SELECT count(*) FROM public.areas"));
            Assert.AreEqual((long)applied.Count, await db.ScalarAsync<long>("SELECT count(*) FROM public.schema_migrations"));
        }

        [TestMethod]
        public async Task 二回目の実行では何も適用しない()
        {
            await using var db = await TemporaryDatabase.CreateAsync();
            await DatabaseMigrator.MigrateAsync(db.ConnectionString, _ => { });

            var applied = await DatabaseMigrator.MigrateAsync(db.ConnectionString, _ => { });

            Assert.AreEqual(0, applied.Count);
        }

        [TestMethod]
        public async Task 導入前から稼働しているDBは初期スキーマを適用済みとして扱う()
        {
            await using var db = await TemporaryDatabase.CreateAsync();
            // マイグレーション導入前の状態（初期スキーマのみで schema_migrations が無い）を再現する
            string baseline = DatabaseMigrator.GetMigrations().Single(m => m.Version == DatabaseMigrator.BaselineVersion).Sql;
            await db.ExecuteAsync(baseline);
            await db.ExecuteAsync("INSERT INTO public.areas VALUES ('JP13', '独自の名前')");

            var applied = await DatabaseMigrator.MigrateAsync(db.ConnectionString, _ => { });

            CollectionAssert.DoesNotContain(applied.ToList(), DatabaseMigrator.BaselineVersion);
            Assert.AreEqual(1L, await db.ScalarAsync<long>($"SELECT count(*) FROM public.schema_migrations WHERE version = '{DatabaseMigrator.BaselineVersion}'"));
            // 既存のエリア名は上書きしない
            Assert.AreEqual("独自の名前", await db.ScalarAsync<string>("SELECT area_name FROM public.areas WHERE area_code = 'JP13'"));
        }

        /// <summary>テスト用に一時データベースを作成し、破棄時に削除する</summary>
        private sealed class TemporaryDatabase : IAsyncDisposable
        {
            private readonly string _adminConnectionString;
            private readonly string _name;

            public string ConnectionString { get; }

            private TemporaryDatabase(string adminConnectionString, string name)
            {
                _adminConnectionString = adminConnectionString;
                _name = name;
                ConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = name, Pooling = false }.ConnectionString;
            }

            public static async Task<TemporaryDatabase> CreateAsync()
            {
                string? admin = Environment.GetEnvironmentVariable(ConnectionStringVariable);
                if (string.IsNullOrWhiteSpace(admin))
                    Assert.Inconclusive($"{ConnectionStringVariable} が未設定のためスキップします");

                var db = new TemporaryDatabase(admin, $"radicore_migration_test_{Guid.NewGuid():N}");
                await using var conn = new NpgsqlConnection(admin);
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand($"CREATE DATABASE {db._name}", conn);
                await cmd.ExecuteNonQueryAsync();
                return db;
            }

            public async Task ExecuteAsync(string sql)
            {
                await using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();
            }

            public async Task<T> ScalarAsync<T>(string sql)
            {
                await using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand(sql, conn);
                return (T)(await cmd.ExecuteScalarAsync())!;
            }

            public async ValueTask DisposeAsync()
            {
                await using var conn = new NpgsqlConnection(_adminConnectionString);
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS {_name} WITH (FORCE)", conn);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}
