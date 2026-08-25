using ATL;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Quartz;
using RadiCore.Data;
using RadiCore.Infrastructure;
using RadiCore.Radiko;
using RadiCore.Reservations;
using SlackNet;
using SlackNet.WebApi;
using File = System.IO.File;

namespace RadiCore.Jobs
{
    public class RecordingJob : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                int reservationId = context.JobDetail.JobDataMap.GetInt("ReservationId")!;
                bool isPrev = false;
                if (context.JobDetail.JobDataMap.ContainsKey("IsPrev"))
                    isPrev = context.JobDetail.JobDataMap.GetBoolean("IsPrev");

                if (isPrev) this.JournalWriteLine("前回分の録音を実施");
                this.JournalWriteLine($"録音開始 予約ID:{reservationId}");

                RadiCoreContext radiCoreContext = new();
                Recording rec;
                var reservation = radiCoreContext.Reservations.Find(reservationId);
                if (reservation == null)
                {
                    this.JournalWriteLine($"録音失敗 予約ID:{reservationId} が見つかりません");
                    return;
                }

                reservation.Status = ReservationStatus.Running;
                reservation.UpdatedAt = DateTime.Now;
                await radiCoreContext.SaveChangesAsync();

                string radikoMail = Environment.GetEnvironmentVariable("RADIKO_MAIL") ?? "";
                string radikoPass = Environment.GetEnvironmentVariable("RADIKO_PASS") ?? "";
                string station = reservation.StationId;
                DateOnly baseDate;

                if (reservation.TargetDate == null)
                {
                    if (isPrev)
                    {
                        if (reservation.RepeatType == RepeatType.Daily)
                        {
                            baseDate = TimeOnly.FromDateTime(DateTime.Now) < reservation.StartTime
                                ? DateOnly.FromDateTime(DateTime.Now.AddDays(-1))
                                : DateOnly.FromDateTime(DateTime.Now);
                        }
                        else if (reservation.RepeatType == RepeatType.Weekly)
                        {
                            if (DateTime.Now.DayOfWeek == reservation.RepeatDays)
                            {
                                baseDate = TimeOnly.FromDateTime(DateTime.Now) < reservation.StartTime
                                    ? DateOnly.FromDateTime(DateTime.Now.AddDays(-7))
                                    : DateOnly.FromDateTime(DateTime.Now);
                            }
                            else
                            {
                                baseDate = DateOnly.FromDateTime(GetPreviousWeekday(DateTime.Now, reservation.RepeatDays!.Value));
                            }
                        }
                        else
                        {
                            baseDate = DateOnly.FromDateTime(DateTime.Now);
                        }
                    }
                    else
                    {
                        baseDate = DateOnly.FromDateTime(DateTime.Now);
                    }
                }
                else
                {
                    baseDate = reservation.TargetDate!.Value;
                }

                var startDateTime = new DateTime(baseDate, reservation.StartTime);
                var endDateTime   = new DateTime(baseDate, reservation.EndTime);
                if (endDateTime <= startDateTime)
                {
                    endDateTime = endDateTime.AddDays(1);
                    this.JournalWriteLine($"日付またぎ対応 録音終了日時を翌日に変更: {endDateTime}");
                }

                string? programName = reservation.ProgramName;
                string? castName    = reservation.CastName;
                string? imageUrl    = reservation.ImageUrl;
                string  programId   = reservation.ProgramId;

                if ((reservation.RepeatType == RepeatType.Weekly || reservation.RepeatType == RepeatType.Daily) && !reservation.IsManual!.Value)
                {
                    this.JournalWriteLine($"繰り返し録音のためProgram情報を再取得 予約ID:{reservationId}");
                    var program = await radiCoreContext.Programs
                        .Where(p => p.StationId == reservation.StationId && p.StartTime == startDateTime)
                        .FirstOrDefaultAsync();
                    if (program != null)
                    {
                        this.JournalWriteLine($"繰り返し録音のためProgram情報を再取得成功 予約ID:{reservationId}");
                        programId   = program.Id;
                        programName = program.Title;
                        castName    = program.CastName;
                        imageUrl    = program.ImageUrl;
                    }
                }

                string fileName  = $@"0_{baseDate:yyyyMMdd}_{programName}.m4a";
                this.JournalWriteLine($"録音開始: station={station} from={startDateTime:yyyyMMddHHmmss} to={endDateTime:yyyyMMddHHmmss} output={fileName}");

                bool recordSuccess = await RadikoRecorder.RecordAsync(
                    station, startDateTime, endDateTime, fileName, radikoMail, radikoPass,
                    msg => this.JournalWriteLine(msg));

                if (!recordSuccess)
                {
                    this.JournalWriteLine($"録音失敗 予約ID:{reservationId}");
                    reservation.Status = ReservationStatus.Failed;
                    reservation.UpdatedAt = DateTime.Now;
                    await radiCoreContext.SaveChangesAsync();
                    return;
                }

                // タグ埋め込み
                Track recorded = new(fileName)
                {
                    Title  = programName,
                    Artist = $"{reservation.StationName}-{castName}"
                };
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    using var httpClient = new HttpClient();
                    var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
                    var picture = PictureInfo.fromBinaryData(imageBytes, PictureInfo.PIC_TYPE.CD);
                    recorded.EmbeddedPictures.Add(picture);
                    this.JournalWriteLine("録音ファイルタグ埋め込み 画像あり");
                }
                recorded.Save();
                this.JournalWriteLine("録音ファイルタグ埋め込み");

                // DB 保存
                await using var conn = new NpgsqlConnection(radiCoreContext.Database.GetConnectionString());
                await conn.OpenAsync();
                await using var tx = await conn.BeginTransactionAsync();

                var fileInfo = new FileInfo(fileName);
                rec = new Recording
                {
                    ReservationId = reservation.Id,
                    ProgramId     = programId,
                    StationId     = reservation.StationId,
                    StationName   = reservation.StationName,
                    ProgramName   = programName,
                    CastName      = castName,
                    StartTime     = startDateTime,
                    EndTime       = endDateTime,
                    FileName      = Path.GetFileName(fileName),
                    MimeType      = "audio/mp4",
                    FileSize      = fileInfo.Length,
                    CreatedAt     = DateTime.UtcNow
                };

                radiCoreContext.Recordings.Add(rec);
                await radiCoreContext.SaveChangesAsync();

                await using var cmd = new NpgsqlCommand(@"
                    insert into recording_audio_data (recording_id, audio_data)
                    values (@id, @data)", conn, tx);

                cmd.Parameters.AddWithValue("id", rec.Id);
                await using var fs = File.OpenRead(fileName);
                var p = cmd.Parameters.Add("data", NpgsqlTypes.NpgsqlDbType.Bytea);
                p.Value = fs;
                await cmd.ExecuteNonQueryAsync();
                await tx.CommitAsync();
                this.JournalWriteLine($"録音ファイルをDBに保存 保存ID:{rec.Id}");

                File.Delete(fileName);
                this.JournalWriteLine($"録音ファイル削除: {fileName}");

                reservation.Status = ReservationStatus.Completed;
                reservation.UpdatedAt = DateTime.Now;
                await radiCoreContext.SaveChangesAsync();
                this.JournalWriteLine("ステータス更新");
                this.JournalWriteLine($"録音完了 予約ID:{reservationId}");

                // 繰り返し予約かつ自動削除が有効な場合、今回分を除く前回録音を削除
                bool autoDeleted = false;
                if ((reservation.RepeatType == RepeatType.Weekly || reservation.RepeatType == RepeatType.Daily)
                    && reservation.AutoDeletePrevious)
                {
                    var previousRecordings = await radiCoreContext.Recordings
                        .Where(r => r.ReservationId == reservation.Id && r.Id != rec.Id)
                        .ToListAsync();

                    if (previousRecordings.Count > 0)
                    {
                        // recording_audio_data も含めて削除（CASCADE 設定がない場合に備え先に削除）
                        var previousIds = previousRecordings.Select(r => r.Id).ToList();
                        foreach (var prevId in previousIds)
                        {
                            await using var delAudioCmd = new NpgsqlCommand(
                                "DELETE FROM recording_audio_data WHERE recording_id = @id", conn);
                            delAudioCmd.Parameters.AddWithValue("id", prevId);
                            await delAudioCmd.ExecuteNonQueryAsync();
                        }

                        radiCoreContext.Recordings.RemoveRange(previousRecordings);
                        await radiCoreContext.SaveChangesAsync();

                        autoDeleted = true;
                        this.JournalWriteLine($"前回録音を自動削除 削除件数:{previousRecordings.Count} IDs:[{string.Join(",", previousIds)}]");
                    }
                    else
                    {
                        this.JournalWriteLine("自動削除対象の前回録音なし");
                    }
                }

                var api = new SlackServiceBuilder()
                    .UseApiToken(Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN"))
                    .GetApiClient();

                string slackText = $"予約完了\n{reservation}\n{rec}";
                if (autoDeleted)
                    slackText += "\n（前回分の録音を自動削除しました）";

                await api.Chat.PostMessage(new Message
                {
                    Text    = slackText,
                    Channel = Environment.GetEnvironmentVariable("SLACK_NOTIFY_CHANNEL")
                });

                if (reservation.RepeatType == RepeatType.Weekly || reservation.RepeatType == RepeatType.Daily)
                {
                    reservation.Status = ReservationStatus.Scheduled;
                    reservation.UpdatedAt = DateTime.Now;
                    this.JournalWriteLine($"繰り返し予約のためステータスを再度予約中に更新 予約ID:{reservationId}");
                    await radiCoreContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                var api = new SlackServiceBuilder()
                    .UseApiToken(Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN"))
                    .GetApiClient();
                string errorMessage = $"録音ジョブ実行中に例外が発生:{ex.StackTrace}";
                await api.Chat.PostMessage(new Message { Text = errorMessage, Channel = Environment.GetEnvironmentVariable("SLACK_NOTIFY_CHANNEL") });
                this.JournalWriteLine(errorMessage);
            }
        }

        private DateTime GetPreviousWeekday(DateTime baseDate, DayOfWeek targetDay)
        {
            int diff = (7 + (baseDate.DayOfWeek - targetDay)) % 7;
            if (diff == 0) diff = 7;
            return baseDate.Date.AddDays(-diff);
        }
    }
}
