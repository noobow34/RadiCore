using RadiCore.Infrastructure;

namespace RadiCore.Test
{
    [TestClass]
    public sealed class RecordingFileNameBuilderTest
    {
        private static readonly RecordingFileNameMetadata Metadata = new()
        {
            ProgramName   = "テスト番組",
            CastName      = "テスト出演者",
            StationName   = "テスト放送局",
            StationId     = "TEST",
            ProgramId     = "20260910130000",
            StartTime     = new DateTime(2026, 9, 10, 13, 0, 0),
            EndTime       = new DateTime(2026, 9, 10, 15, 30, 0),
            ReservationId = 12,
        };

        [TestMethod]
        public void 既定のテンプレートは放送日と番組名のファイル名を生成する()
        {
            var actual = RecordingFileNameBuilder.Build(RecordingFileNameBuilder.DefaultTemplate, Metadata);
            Assert.AreEqual("20260910_テスト番組.m4a", actual);
        }

        [TestMethod]
        public void 各プレースホルダーが展開される()
        {
            var actual = RecordingFileNameBuilder.Build(
                "{station}_{stationId}_{program}_{cast}_{programId}_{start}-{end}_{reservationId}", Metadata);
            Assert.AreEqual("テスト放送局_TEST_テスト番組_テスト出演者_20260910130000_1300-1530_12.m4a", actual);
        }

        [TestMethod]
        public void 日時プレースホルダーは書式を指定できる()
        {
            var actual = RecordingFileNameBuilder.Build("{date:yyyy-MM-dd}_{start:HH-mm}_{program}", Metadata);
            Assert.AreEqual("2026-09-10_13-00_テスト番組.m4a", actual);
        }

        [TestMethod]
        public void ファイル名に使えない文字はアンダースコアに置換される()
        {
            var metadata = Metadata with { ProgramName = @"A/B\C:D*E?F""G<H>I|J" };
            var actual = RecordingFileNameBuilder.Build("{program}", metadata);
            Assert.AreEqual("A_B_C_D_E_F_G_H_I_J.m4a", actual);
        }

        [TestMethod]
        public void テンプレートが空の場合は既定のテンプレートを使う()
        {
            Assert.AreEqual("20260910_テスト番組.m4a", RecordingFileNameBuilder.Build(null, Metadata));
            Assert.AreEqual("20260910_テスト番組.m4a", RecordingFileNameBuilder.Build("   ", Metadata));
        }

        [TestMethod]
        public void 展開結果が空になる場合は既定のテンプレートにフォールバックする()
        {
            var metadata = Metadata with { CastName = "" };
            Assert.AreEqual("20260910_テスト番組.m4a", RecordingFileNameBuilder.Build("{cast}", metadata));
        }

        [TestMethod]
        public void 長いファイル名はバイト数上限で切り詰められる()
        {
            var metadata = Metadata with { ProgramName = new string('あ', 200) };
            var actual = RecordingFileNameBuilder.Build("{program}", metadata);

            var stem = Path.GetFileNameWithoutExtension(actual);
            Assert.IsTrue(System.Text.Encoding.UTF8.GetByteCount(stem) <= RecordingFileNameBuilder.MaxFileNameBytes);
            Assert.AreEqual(RecordingFileNameBuilder.MaxFileNameBytes / 3, stem.Length);
            Assert.IsTrue(actual.EndsWith(RecordingFileNameBuilder.Extension));
        }

        [TestMethod]
        public void 先頭ハイフンとWindows予約名は退避される()
        {
            Assert.AreEqual("_-テスト番組.m4a", RecordingFileNameBuilder.Build("-{program}", Metadata));
            Assert.AreEqual("_CON.m4a", RecordingFileNameBuilder.Build("CON", Metadata));
            Assert.AreEqual("_com1.part.m4a", RecordingFileNameBuilder.Build("com1.part", Metadata));
        }

        [TestMethod]
        public void 未知のプレースホルダーは検証で検出される()
        {
            Assert.IsFalse(RecordingFileNameBuilder.TryValidate("{program}_{unknown}_{alsoUnknown}", out var unknownTokens));
            CollectionAssert.AreEqual(new[] { "{unknown}", "{alsoUnknown}" }, unknownTokens);

            Assert.IsTrue(RecordingFileNameBuilder.TryValidate("{program}_{date:yyyyMMdd}", out unknownTokens));
            Assert.AreEqual(0, unknownTokens.Count);
        }

        [TestMethod]
        public void 未知のプレースホルダーはそのまま残る()
        {
            Assert.AreEqual("テスト番組_{unknown}.m4a", RecordingFileNameBuilder.Build("{program}_{unknown}", Metadata));
        }
    }
}
