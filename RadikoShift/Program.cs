using Quartz;
using Quartz.Impl;
using RadikoShift.Data;
using RadikoShift.Infrastructure;
using RadikoShift.Jobs;

string rsCs = Environment.GetEnvironmentVariable("RADIKOSHIFT_CONNECTION_STRING") ?? "";
Console.WriteLine($"RADIKOSHIFT_CONNECTION_STRING:{rsCs.Length}");

// DB から設定を読み込んでスケジュール構築
int refreshHour   = AppSettingsService.DefaultRefreshHour;
int refreshMinute = AppSettingsService.DefaultRefreshMinute;
int parallelCount = AppSettingsService.DefaultParallelCount;
try
{
    using var bootContext = new ShiftContext();
    var svc   = new AppSettingsService(bootContext);
    refreshHour   = svc.RefreshHour;
    refreshMinute = svc.RefreshMinute;
    parallelCount = svc.ParallelCount;
}
catch
{
    // DB 未準備の場合はデフォルト値で続行
}

var schedulerFactory = new StdSchedulerFactory();
var sch = await schedulerFactory.GetScheduler();
await sch.Start();

var jobDetailRP = JobBuilder.Create<RefreshStationsAndPrograms>()
    .WithIdentity("RefreshPrograms")
    .UsingJobData("ParallelCount", parallelCount)
    .Build();
var triggerRP = TriggerBuilder.Create()
    .WithIdentity("RefreshPrograms")
    .StartNow()
    .WithCronSchedule($"0 {refreshMinute} {refreshHour} ? * * *")
    .Build();
await sch.ScheduleJob(jobDetailRP, triggerRP);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(options =>
{
    options.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(
        System.Text.Unicode.UnicodeRanges.All);
});

builder.Services.AddDbContext<ShiftContext>();

// デプロイスクリプトのヘルスチェック用。判定対象は「プロセスが起動してリクエストを受けられるか」と
// 「DBに到達できるか」のみ。Radikoへの番組取得や録音ジョブの実行状況は、
// 外部要因の失敗でデプロイがロールバックされるのを避けるため意図的に含めない。
builder.Services.AddHealthChecks()
    .AddNpgSql(rsCs, name: "postgres");

builder.Services.AddScoped<AppSettingsService>();
builder.Services.AddSingleton<QuartzScheduler>(new QuartzScheduler(sch));
builder.Services.AddHostedService<ReservationBootstrapService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Home/Error");

app.UseRouting();
app.MapStaticAssets();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}")
   .WithStaticAssets();

// ローカルからの curl で叩けるようにする。現状このアプリに認可ミドルウェアは無いため
// AllowAnonymous は実質無効だが、意図の明示として付けている。
app.MapHealthChecks("/healthz").AllowAnonymous();

app.Run();
