using System.Collections.Specialized;
using Quartz;
using Quartz.Impl;
using RadiCore.Infrastructure;

namespace RadiCore.Test
{
    [TestClass]
    public sealed class QuartzSchedulerPauseTest
    {
        /// <summary>テスト用の独立したスケジューラを起動する</summary>
        private static async Task<IScheduler> StartSchedulerAsync()
        {
            var props = new NameValueCollection
            {
                ["quartz.scheduler.instanceName"] = $"PauseTest-{Guid.NewGuid():N}",
                ["quartz.threadPool.threadCount"] = "2",
            };

            var scheduler = await new StdSchedulerFactory(props).GetScheduler();
            await scheduler.Start();
            return scheduler;
        }

        [TestMethod]
        public async Task 一時停止と再開で状態が切り替わる()
        {
            var scheduler = await StartSchedulerAsync();
            var target = new QuartzScheduler(scheduler);

            try
            {
                Assert.IsFalse(target.IsPaused);

                await target.PauseAllAsync();
                Assert.IsTrue(target.IsPaused);

                // 二重呼び出しでも状態は変わらない
                await target.PauseAllAsync();
                Assert.IsTrue(target.IsPaused);

                await target.ResumeAllAsync();
                Assert.IsFalse(target.IsPaused);

                await target.ResumeAllAsync();
                Assert.IsFalse(target.IsPaused);
            }
            finally
            {
                await scheduler.Shutdown();
            }
        }

        [TestMethod]
        public async Task 一時停止中はジョブが実行されず再開後に実行される()
        {
            var scheduler = await StartSchedulerAsync();
            var target = new QuartzScheduler(scheduler);

            try
            {
                await target.PauseAllAsync();

                CountingJob.Reset();
                var job = JobBuilder.Create<CountingJob>().WithIdentity("counting").Build();
                var trigger = TriggerBuilder.Create().WithIdentity("counting").StartNow().Build();
                await scheduler.ScheduleJob(job, trigger);

                await Task.Delay(500);
                Assert.AreEqual(0, CountingJob.ExecutedCount, "一時停止中にジョブが実行された");

                await target.ResumeAllAsync();
                await CountingJob.WaitForExecutionAsync(TimeSpan.FromSeconds(5));
                Assert.AreEqual(1, CountingJob.ExecutedCount, "再開後にジョブが実行されなかった");
            }
            finally
            {
                await scheduler.Shutdown();
            }
        }

        public class CountingJob : IJob
        {
            private static int _executedCount;
            private static TaskCompletionSource _executed = new();

            public static int ExecutedCount => Volatile.Read(ref _executedCount);

            public static void Reset()
            {
                Volatile.Write(ref _executedCount, 0);
                _executed = new TaskCompletionSource();
            }

            public static async Task WaitForExecutionAsync(TimeSpan timeout)
            {
                var completed = await Task.WhenAny(_executed.Task, Task.Delay(timeout));
                Assert.AreSame(_executed.Task, completed, "ジョブの実行を待機中にタイムアウトした");
            }

            public Task Execute(IJobExecutionContext context)
            {
                Interlocked.Increment(ref _executedCount);
                _executed.TrySetResult();
                return Task.CompletedTask;
            }
        }
    }
}
