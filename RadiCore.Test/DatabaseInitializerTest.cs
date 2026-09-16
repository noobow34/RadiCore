using RadiCore.Infrastructure;

namespace RadiCore.Test
{
    [TestClass]
    public sealed class DatabaseInitializerTest
    {
        [TestMethod]
        public void schema_sqlが埋め込まれている()
        {
            string sql = DatabaseInitializer.ReadResource("schema.sql");

            StringAssert.Contains(sql, "CREATE TABLE public.reservations");
            StringAssert.Contains(sql, "CREATE TABLE public.areas");
        }

        [TestMethod]
        public void seed_sqlに47都道府県のエリアが含まれる()
        {
            string sql = DatabaseInitializer.ReadResource("seed.sql");

            for (int i = 1; i <= 47; i++)
                StringAssert.Contains(sql, $"('JP{i}',");
        }

        [TestMethod]
        public void psqlのメタコマンドを含まない()
        {
            // Npgsql で直接実行するため、\restrict などの psql 専用コマンドがあると失敗する
            foreach (var name in new[] { "schema.sql", "seed.sql" })
            {
                var lines = DatabaseInitializer.ReadResource(name).Split('\n');
                Assert.IsFalse(lines.Any(l => l.TrimStart().StartsWith('\\')), $"{name} に psql メタコマンドが含まれています");
            }
        }
    }
}
