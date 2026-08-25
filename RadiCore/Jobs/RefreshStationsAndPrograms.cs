using Microsoft.EntityFrameworkCore;
using Npgsql.Bulk;
using Quartz;
using RadiCore.Data;
using RadiCore.Infrastructure;
using RadiCore.Radiko;
using SlackNet;
using SlackNet.WebApi;
using System.Collections.Concurrent;

namespace RadiCore.Jobs
{
    public class RefreshStationsAndPrograms : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            RadiCoreContext? stagingContext = null;
            var startedAt = DateTimeOffset.Now;
            this.JournalBeginCapture();

            await SaveRefreshLogAsync(new RefreshLog
            {
                IsRunning = true,
                StartedAt = startedAt,
            });

            try
            {
                this.JournalWriteLine("番組表更新開始");

                string radikoMail = Environment.GetEnvironmentVariable("RADIKO_MAIL") ?? "";
                string radikoPass = Environment.GetEnvironmentVariable("RADIKO_PASS") ?? "";

                using var httpClient = await RadikoClient.CreateHttpClient(radikoMail, radikoPass);
                this.JournalWriteLine("ログイン完了");

                int partitionCount = AppSettingsService.DefaultParallelCount;
                if (context.JobDetail.JobDataMap.ContainsKey("ParallelCount"))
                    partitionCount = context.JobDetail.JobDataMap.GetInt("ParallelCount");
                this.JournalWriteLine($"並列数:{partitionCount}");

                this.JournalWriteLine("放送局取得");
                var stations = await RadikoClient.GetStations(true, httpClient);

                // ── ステージングテーブル作成 ──────────────────────────
                stagingContext = new RadiCoreStagingContext();
                await CreateStagingTablesAsync(stagingContext);
                this.JournalWriteLine("ステージングテーブル作成");

                // ── プログラム並列取得 ────────────────────────────────
                var stationPartitions = Partitioner
                    .Create(stations, EnumerablePartitionerOptions.NoBuffering)
                    .GetPartitions(partitionCount);

                List<Task> tasks = [];
                List<Data.Program> programs = [];

                int taskId = 0;
                foreach (var partition in stationPartitions)
                {
                    taskId++;
                    int currentTaskId = taskId;
                    tasks.Add(Task.Run(async () =>
                    {
                        var localPrograms = new List<Data.Program>();
                        using (partition)
                        {
                            while (partition.MoveNext())
                            {
                                var station = partition.Current;
                                this.JournalWriteLine($"T{currentTaskId}:{station.Name!}");
                                var progs = await RadikoClient.GetPrograms(station, httpClient);
                                localPrograms.AddRange(progs);
                            }
                        }
                        lock (programs)
                        {
                            programs.AddRange(localPrograms);
                        }
                    }));
                }

                await Task.WhenAll(tasks);

                // ── ステージングへBulk Insert（並列）────────────────────
                this.JournalWriteLine("ステージングへ保存");
                await using var stationsContext = new RadiCoreStagingContext();
                await using var programsContext = new RadiCoreStagingContext();
                await Task.WhenAll(
                    new NpgsqlBulkUploader(stationsContext).InsertAsync(stations),
                    new NpgsqlBulkUploader(programsContext).InsertAsync(programs)
                );

                // ── アトミック切り替え ────────────────────────────────
                this.JournalWriteLine("テーブル切り替え");
                await SwapTablesAsync(stagingContext);

                var finishedAt = DateTimeOffset.Now;
                this.JournalWriteLine($"番組表更新終了:{finishedAt - startedAt}");

                await SaveRefreshLogAsync(new RefreshLog
                {
                    StartedAt = startedAt,
                    FinishedAt = finishedAt,
                    Succeeded = true,
                    Lines = this.JournalGetCaptured(),
                });
            }
            catch (Exception ex)
            {
                // ステージングテーブルが残っていれば掃除
                if (stagingContext != null)
                {
                    try { await DropStagingTablesAsync(stagingContext); }
                    catch { /* 掃除失敗は無視 */ }
                }

                var api = new SlackServiceBuilder()
                    .UseApiToken(Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN"))
                    .GetApiClient();
                string errorMessage = $"放送局・番組表更新ジョブ実行中に例外が発生:{ex.StackTrace}";
                await api.Chat.PostMessage(new Message
                {
                    Text = errorMessage,
                    Channel = Environment.GetEnvironmentVariable("SLACK_NOTIFY_CHANNEL")
                });
                this.JournalWriteLine(errorMessage);

                await SaveRefreshLogAsync(new RefreshLog
                {
                    StartedAt = startedAt,
                    FinishedAt = DateTimeOffset.Now,
                    Succeeded = false,
                    Lines = this.JournalGetCaptured(),
                });
            }
            finally
            {
                stagingContext?.Dispose();
            }
        }

        /// <summary>DIに依存せず直接DBコンテキストを生成してログを保存する</summary>
        private static async Task SaveRefreshLogAsync(RefreshLog log)
        {
            try
            {
                using var db = new RadiCoreContext();
                var svc = new AppSettingsService(db);
                await svc.SaveRefreshLogAsync(log);
            }
            catch
            {
                // ログ保存失敗はメイン処理に影響させない
            }
        }

        // ── ステージングテーブル作成 ──────────────────────────────────
        private static async Task CreateStagingTablesAsync(RadiCoreContext ctx)
        {
            var db = ctx.Database;
            await db.ExecuteSqlRawAsync("DROP TABLE IF EXISTS stations_staging");
            await db.ExecuteSqlRawAsync("DROP TABLE IF EXISTS programs_staging");
            await db.ExecuteSqlRawAsync("CREATE TABLE stations_staging (LIKE stations INCLUDING ALL)");
            await db.ExecuteSqlRawAsync("CREATE TABLE programs_staging (LIKE programs INCLUDING ALL)");
        }

        private static async Task DropStagingTablesAsync(RadiCoreContext ctx)
        {
            var db = ctx.Database;
            await db.ExecuteSqlRawAsync("DROP TABLE IF EXISTS stations_staging");
            await db.ExecuteSqlRawAsync("DROP TABLE IF EXISTS programs_staging");
        }

        // ── アトミック切り替え ────────────────────────────────────────
        private static async Task SwapTablesAsync(RadiCoreContext ctx)
        {
            var db = ctx.Database;

            // EF Core のトランザクションで囲む
            await using var tx = await db.BeginTransactionAsync();
            try
            {
                await db.ExecuteSqlRawAsync("ALTER TABLE stations RENAME TO stations_old");
                await db.ExecuteSqlRawAsync("ALTER TABLE stations_staging RENAME TO stations");

                await db.ExecuteSqlRawAsync("ALTER TABLE programs RENAME TO programs_old");
                await db.ExecuteSqlRawAsync("ALTER TABLE programs_staging RENAME TO programs");

                await db.ExecuteSqlRawAsync("DROP TABLE stations_old");
                await db.ExecuteSqlRawAsync("DROP TABLE programs_old");

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
    }
}