using RadiCore.Infrastructure;

namespace RadiCore.Test
{
    /// <summary>埋め込まれたマイグレーションファイルの検証（DB 不要）</summary>
    [TestClass]
    public sealed class DatabaseMigratorTest
    {
        [TestMethod]
        public void マイグレーションが番号順に埋め込まれている()
        {
            var versions = DatabaseMigrator.GetMigrations().Select(m => m.Version).ToList();

            Assert.IsTrue(versions.Count >= 2);
            Assert.AreEqual(DatabaseMigrator.BaselineVersion, versions[0]);
            Assert.AreEqual("0002_seed_areas", versions[1]);
        }

        [TestMethod]
        public void ファイル名がNNNN_名前の形式で番号が重複しない()
        {
            var versions = DatabaseMigrator.GetMigrations().Select(m => m.Version).ToList();

            foreach (var v in versions)
                Assert.IsTrue(DatabaseMigrator.IsValidVersion(v), $"{v} は NNNN_名前（小文字英数字とアンダースコア）の形式ではありません");

            var numbers = versions.Select(v => v[..4]).ToList();
            Assert.AreEqual(numbers.Count, numbers.Distinct().Count(), "マイグレーションの番号が重複しています");
        }

        [TestMethod]
        public void psqlのメタコマンドを含まない()
        {
            // Npgsql で直接実行するため、\restrict などの psql 専用コマンドがあると失敗する
            foreach (var m in DatabaseMigrator.GetMigrations())
            {
                var lines = m.Sql.Split('\n');
                Assert.IsFalse(lines.Any(l => l.TrimStart().StartsWith('\\')), $"{m.Version} に psql メタコマンドが含まれています");
            }
        }

        [TestMethod]
        public void 初期データに47都道府県のエリアが含まれる()
        {
            string sql = DatabaseMigrator.GetMigrations().Single(m => m.Version == "0002_seed_areas").Sql;

            for (int i = 1; i <= 47; i++)
                StringAssert.Contains(sql, $"('JP{i}',");
        }
    }
}
