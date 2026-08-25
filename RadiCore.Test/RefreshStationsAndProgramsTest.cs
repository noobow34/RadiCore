using Quartz;
using Quartz.Impl;
using RadiCore.Jobs;

namespace RadiCore.Test
{
    [TestClass]
    public sealed class RefreshStationsAndProgramsTest
    {
        [TestMethod]
        public async Task ExecuteTestAsync()
        {
            var schedulerFactory = new StdSchedulerFactory();
            var sch = await schedulerFactory.GetScheduler();
            await sch.Start();

            var jobDetail = JobBuilder.Create<RefreshStationsAndPrograms>()
                .WithIdentity("RefreshStationsAndPrograms")
                .Build();

            var startDate = DateTime.Now.AddSeconds(10);

            var trigger = TriggerBuilder.Create()
                .WithIdentity("RefreshStationsAndPrograms")
                .StartNow()
                .WithCronSchedule($"{startDate.Second} {startDate.Minute} {startDate.Hour} ? * * *")
                .Build();

            await sch.ScheduleJob(jobDetail, trigger);

            await Task.Delay(10 * 60 * 1000);
        }
    }
}
