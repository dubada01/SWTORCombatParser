using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class TestComabtLogParsing
{
    [Test]
    public void TestParseLog()
    {
        var path = Path.GetTempFileName();
        try
        {
            const string started = "[12:00:00.000] [] [] [] [Phase Event {836045448953670} RaidStarted {836045448953671}]";
            const string ended = "[12:10:00.000] [] [] [] [Phase Event {836045448953670} RaidEnded {836045448953672}]";
            File.WriteAllText(path, started + "\r\n" + ended[..15], Encoding.UTF8);
            Assert.That(ReadCompleteEffects(), Is.EqualTo(new[] { "RaidStarted" }));
            File.AppendAllText(path, ended[15..] + "\n", Encoding.UTF8);
            Assert.That(ReadCompleteEffects(), Is.EqualTo(new[] { "RaidStarted", "RaidEnded" }));
        }
        finally { File.Delete(path); }

        string[] ReadCompleteEffects()
        {
            var file = CombatLogLoader.LoadSpecificLog(path);
            using (file.Data)
            {
                CombatLogParser.SetParseDate(DateTime.Today);
                _7_0LogParsing.SetupRegex();
                return CombatLogParser.ParseAllLines(file).Select(x => x.Effect.EffectName).ToArray();
            }
        }
    }
}
