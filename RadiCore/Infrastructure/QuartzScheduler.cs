using Quartz;
using RadiCore.Data;
using RadiCore.Jobs;
using RadiCore.Reservations;

namespace RadiCore.Infrastructure
{
    public class QuartzScheduler
    {
        private readonly IScheduler _scheduler;

        public QuartzScheduler(IScheduler scheduler)
        {
            _scheduler = scheduler;
        }

        // ── 予約ジョブ ───────────────────────────────────────

        public async Task RegisterAsync(Reservation reservation)
        {
            var jobKey = new JobKey($"reservation-{reservation.Id}");

            if (await _scheduler.CheckExists(jobKey))
            {
                this.JournalWriteLine($"既存の予約をSchedulerから削除します：{reservation}");
                await _scheduler.DeleteJob(jobKey);
            }

            this.JournalWriteLine($"予約をSchedulerに登録します：{reservation}");

            var job = JobBuilder.Create<RecordingJob>()
                .WithIdentity(jobKey)
                .UsingJobData("ReservationId", reservation.Id.ToString())
                .Build();

            var trigger = BuildReservationTrigger(reservation);
            await _scheduler.ScheduleJob(job, trigger);
        }

        public async Task RegisterPrevious(Reservation reservation)
        {
            var jobKey = new JobKey($"reservation-{reservation.Id}-prev");
            this.JournalWriteLine($"前回分録音をSchedulerに登録します：{reservation}");

            var job = JobBuilder.Create<RecordingJob>()
                .WithIdentity(jobKey)
                .UsingJobData("ReservationId", reservation.Id.ToString())
                .UsingJobData("IsPrev", true)
                .Build();

            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity($"trigger-{reservation.Id}-prev")
                .StartNow()
                .Build();

            await _scheduler.ScheduleJob(job, trigger);

            this.JournalWriteLine($"前回分録音が完了したためジョブをSchedulerから削除：{reservation}");
            await _scheduler.DeleteJob(jobKey);
        }

        public async Task UnregisterAsync(Reservation reservation)
        {
            if (await _scheduler.CheckExists(new JobKey($"reservation-{reservation.Id}")))
            {
                this.JournalWriteLine($"予約をSchedulerから削除します：{reservation}");
                await _scheduler.DeleteJob(new JobKey($"reservation-{reservation.Id}"));
            }
        }

        // ── 番組表更新ジョブ ──────────────────────────────────

        public async Task RescheduleRefreshJobAsync(int hour, int minute, int parallelCount)
        {
            var jobKey = new JobKey("RefreshPrograms");

            var job = JobBuilder.Create<RefreshStationsAndPrograms>()
                .WithIdentity(jobKey)
                .UsingJobData("ParallelCount", parallelCount)
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity("RefreshPrograms")
                .WithCronSchedule($"0 {minute} {hour} ? * * *")
                .Build();

            await _scheduler.DeleteJob(jobKey);
            await _scheduler.ScheduleJob(job, trigger);
        }

        public async Task TriggerRefreshJobNowAsync(int parallelCount)
        {
            var data = new JobDataMap();
            data.Put("ParallelCount", parallelCount);
            await _scheduler.TriggerJob(new JobKey("RefreshPrograms"), data);
        }

        // ── 内部ヘルパー ──────────────────────────────────────

        private ITrigger BuildReservationTrigger(Reservation r)
        {
            return r.RepeatType switch
            {
                RepeatType.Once    => BuildOnceTrigger(r),
                RepeatType.Daily   => BuildDailyTrigger(r),
                RepeatType.Weekly  => BuildWeeklyTrigger(r),
                _ => throw new NotSupportedException()
            };
        }

        private ITrigger BuildOnceTrigger(Reservation r)
        {
            var startAt = new DateTime(r.TargetDate!.Value, r.EndTime);
            if (r.EndTime <= r.StartTime) startAt = startAt.AddDays(1);
            startAt = startAt.AddMinutes(5);

            return TriggerBuilder.Create()
                .WithIdentity($"trigger-{r.Id}")
                .StartAt(startAt)
                .WithSimpleSchedule(x => x.WithRepeatCount(0))
                .Build();
        }

        private ITrigger BuildDailyTrigger(Reservation r)
        {
            return TriggerBuilder.Create()
                .WithIdentity($"trigger-{r.Id}")
                .WithSchedule(
                    CronScheduleBuilder.DailyAtHourAndMinute(r.EndTime.Hour, r.EndTime.Minute))
                .Build();
        }

        private ITrigger BuildWeeklyTrigger(Reservation r)
        {
            var days = new DayOfWeek[] { r.RepeatDays!.Value };
            if (r.EndTime <= r.StartTime) days[0] = days[0] + 1;
            var jobStart = r.EndTime.AddMinutes(5);

            return TriggerBuilder.Create()
                .WithIdentity($"trigger-{r.Id}")
                .WithSchedule(
                    CronScheduleBuilder.AtHourAndMinuteOnGivenDaysOfWeek(
                        jobStart.Hour, jobStart.Minute, days))
                .Build();
        }
    }
}
