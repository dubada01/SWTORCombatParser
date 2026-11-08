using NUnit.Framework;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.CombatParsing;
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using SWTORCombatParser.DataStructures;

namespace SWTORCombatParser_Test
{
    [TestFixture]
    public class TestLogCursor
    {
        [TestCase("bad line\r\n\r\npartial", 12, 2)]
        [TestCase("bad line\n\npartial", 10, 2)]
        [TestCase("partial", 0, 0)]
        public void HistoricalCursorCountsPhysicalCompleteLines(string contents, long expectedBytes, int expectedLines)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _7_0LogParsing.SetupRegex();
            CombatLogParser.SetParseDate(DateTime.Today);
            var encoding = Encoding.GetEncoding(1252);
            using var stream = new MemoryStream(encoding.GetBytes(contents));
            using var reader = new StreamReader(stream, encoding, true);
            var file = new CombatLogFile { Data = reader, Name = "test.txt" };

            CombatLogParser.ParseAllLines(file, true, out long completedBytes, out int physicalLines);

            Assert.That(completedBytes, Is.EqualTo(expectedBytes));
            Assert.That(physicalLines, Is.EqualTo(expectedLines));
        }

        [Test]
        public void HistoricalCursorIncludesUtf8Preamble()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _7_0LogParsing.SetupRegex();
            CombatLogParser.SetParseDate(DateTime.Today);
            var encoding = new UTF8Encoding(true);
            var contents = encoding.GetPreamble();
            using var stream = new MemoryStream();
            stream.Write(contents);
            stream.Write(encoding.GetBytes("bad\npartial"));
            stream.Position = 0;
            using var reader = new StreamReader(stream, Encoding.GetEncoding(1252), true);
            var file = new CombatLogFile { Data = reader, Name = "test.txt" };

            CombatLogParser.ParseAllLines(file, true, out long completedBytes, out int physicalLines);

            Assert.That(completedBytes, Is.EqualTo(7));
            Assert.That(physicalLines, Is.EqualTo(1));
        }

        [Test]
        public void LiveHistoricalCursorRetainsATerminatedIncompleteTail()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _7_0LogParsing.SetupRegex();
            CombatLogParser.SetParseDate(DateTime.Today);
            var encoding = Encoding.GetEncoding(1252);
            using var stream = new MemoryStream(encoding.GetBytes("\r\npartial\r\n"));
            using var reader = new StreamReader(stream, encoding, true);
            var file = new CombatLogFile { Data = reader, Name = "test.txt" };

            CombatLogParser.ParseAllLines(file, true, out long completedBytes, out int physicalLines, deferIncompleteTail: true);

            Assert.That(completedBytes, Is.EqualTo(2));
            Assert.That(physicalLines, Is.EqualTo(1));
        }

        [Test]
        public void LiveCursorAdvancesPastBlankLinesButRetainsPartialRecord()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "\n\r\npartial", Encoding.GetEncoding(1252));
                // Construct just the isolated frame reader; its normal constructor reads user settings.
                var streamer = (CombatLogStreamer)RuntimeHelpers.GetUninitializedObject(typeof(CombatLogStreamer));
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(CombatLogStreamer).GetField("_logToMonitor", flags).SetValue(streamer, path);
                typeof(CombatLogStreamer).GetField("_fileEncoding", flags).SetValue(streamer, Encoding.GetEncoding(1252));
                typeof(CombatLogStreamer).GetField("_combatLock", flags).SetValue(streamer, new object());
                typeof(CombatLogStreamer).GetField("_pendingLiveLogs", flags).SetValue(streamer, new List<ParsedLogEntry>());

                typeof(CombatLogStreamer).GetMethod("ParseLogFile", flags).Invoke(streamer, null);

                Assert.That(typeof(CombatLogStreamer).GetField("_numberOfProcessedBytes", flags).GetValue(streamer), Is.EqualTo(3L));
                Assert.That(typeof(CombatLogStreamer).GetField("numberOfProcessedLines", flags).GetValue(streamer), Is.EqualTo(2));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        [NonParallelizable]
        public void LiveCursorReplaysATemporarilyTerminatedPartialLineAfterItIsRepaired()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _7_0LogParsing.SetupRegex();
            CombatLogParser.SetParseDate(DateTime.Today);
            CombatLogStateBuilder.ClearState();
            var encoding = Encoding.GetEncoding(1252);
            const string complete = "[19:41:55.291] [@Player#689235581908948|(1605.93,-934.38,364.08,-23.85)|(8032/8032)] [=] [Advanced Kyrprax Proficient Stim {4256312590336000}] [ApplyEffect {836045448945477}: Advanced Kyrprax Proficient Stim {4256312590336000}]";
            var path = Path.GetTempFileName();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var streamer = (CombatLogStreamer)RuntimeHelpers.GetUninitializedObject(typeof(CombatLogStreamer));
            typeof(CombatLogStreamer).GetField("_logToMonitor", flags).SetValue(streamer, path);
            typeof(CombatLogStreamer).GetField("_fileEncoding", flags).SetValue(streamer, encoding);
            typeof(CombatLogStreamer).GetField("_combatLock", flags).SetValue(streamer, new object());
            typeof(CombatLogStreamer).GetField("_combatDetector", flags).SetValue(streamer, new CombatDetector());
            typeof(CombatLogStreamer).GetField("_pendingLiveLogs", flags).SetValue(streamer, new List<ParsedLogEntry>());
            var parse = typeof(CombatLogStreamer).GetMethod("ParseLogFile", flags);
            var received = new List<ParsedLogEntry>();
            Action<ParsedLogEntry> handler = received.Add;
            CombatLogStreamer.NewLineStreamed += handler;
            try
            {
                File.WriteAllText(path, complete.Substring(0, complete.Length / 2) + "\r\n", encoding);
                parse.Invoke(streamer, null);
                Assert.That(typeof(CombatLogStreamer).GetField("_numberOfProcessedBytes", flags).GetValue(streamer), Is.EqualTo(0L));
                Assert.That(received, Is.Empty);

                File.WriteAllText(path, complete + "\r\n", encoding);
                parse.Invoke(streamer, null);
                Assert.That(received.Count, Is.EqualTo(1));
                Assert.That(received[0].LogLineNumber, Is.EqualTo(0));
                Assert.That(typeof(CombatLogStreamer).GetField("_numberOfProcessedBytes", flags).GetValue(streamer), Is.EqualTo((long)encoding.GetByteCount(complete + "\r\n")));
            }
            finally
            {
                CombatLogStreamer.NewLineStreamed -= handler;
                File.Delete(path);
            }
        }

        [Test]
        [NonParallelizable]
        public void CombatEndFlushesPendingEntriesBeforeStop()
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var streamer = (CombatLogStreamer)RuntimeHelpers.GetUninitializedObject(typeof(CombatLogStreamer));
            var entry = new ParsedLogEntry();
            var received = new List<CombatStatusUpdate>();
            typeof(CombatLogStreamer).GetField("_logToMonitor", flags).SetValue(streamer, "test.txt");
            typeof(CombatLogStreamer).GetField("_combatLock", flags).SetValue(streamer, new object());
            typeof(CombatLogStreamer).GetField("_pendingLiveLogs", flags).SetValue(streamer, new List<ParsedLogEntry> { entry });
            typeof(CombatLogStreamer).GetField("_isInCombat", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
            Action<CombatStatusUpdate> handler = received.Add;
            CombatLogStreamer.CombatUpdated += handler;
            try
            {
                typeof(CombatLogStreamer).GetMethod("EndCombat", flags).Invoke(streamer, new object[] { true, null });
                Assert.That(received.Count, Is.EqualTo(2));
                Assert.That(received[0].Type, Is.EqualTo(UpdateType.Update));
                Assert.That(received[0].Logs, Does.Contain(entry));
                Assert.That(received[1].Type, Is.EqualTo(UpdateType.Stop));
            }
            finally
            {
                CombatLogStreamer.CombatUpdated -= handler;
                typeof(CombatLogStreamer).GetField("_isInCombat", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, false);
            }
        }
    }
}
