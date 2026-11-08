using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using SWTORCombatParser.Model.Timers;

namespace SWTORCombatParser_Test;

[TestFixture, NonParallelizable]
public class TimerPersistenceTests
{
    [Test]
    public void ConcurrentImportsAndSharedInitializationPreserveEverySource()
    {
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var type = typeof(DefaultOrbsTimersManager);
        var pathField = type.GetField("infoPath", flags)!;
        var textField = type.GetField("_currentDefaults", flags)!;
        var cacheField = type.GetField("_defaults", flags)!;
        var originalPath = pathField.GetValue(null);
        var originalText = textField.GetValue(null);
        var originalCache = cacheField.GetValue(null);
        var directory = Path.Combine(Path.GetTempPath(), "orbs-timers-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "timers.json");
            File.WriteAllText(path, "[]");
            pathField.SetValue(null, path);
            textField.SetValue(null, "");
            cacheField.SetValue(null, null);

            Parallel.For(0, 40, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                DefaultOrbsTimersManager.AddSource(new DefaultTimersData
                {
                    TimerSource = $"Synthetic Encounter|Boss {i}",
                    IsBossSource = true
                });
                DefaultOrbsTimersManager.GetDefaults("Shared");
            });

            var defaults = DefaultOrbsTimersManager.GetAllDefaults();
            Assert.That(defaults.Count(d => d.TimerSource == "Shared"), Is.EqualTo(1));
            for (var i = 0; i < 40; i++)
                Assert.That(defaults.Count(d => d.TimerSource == $"Synthetic Encounter|Boss {i}"), Is.EqualTo(1));
        }
        finally
        {
            pathField.SetValue(null, originalPath);
            textField.SetValue(null, originalText);
            cacheField.SetValue(null, originalCache);
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public void ImportContinuesPastEmptySourceAndIgnoresNullEntries()
    {
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var type = typeof(DefaultOrbsTimersManager);
        var pathField = type.GetField("infoPath", flags)!;
        var textField = type.GetField("_currentDefaults", flags)!;
        var cacheField = type.GetField("_defaults", flags)!;
        var originalPath = pathField.GetValue(null);
        var originalText = textField.GetValue(null);
        var originalCache = cacheField.GetValue(null);
        var directory = Path.Combine(Path.GetTempPath(), "orbs-timers-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "timers.json");
            File.WriteAllText(path, "[null]");
            pathField.SetValue(null, path);
            textField.SetValue(null, "");
            cacheField.SetValue(null, null);

            DefaultOrbsTimersManager.AddSources(new List<DefaultTimersData>
            {
                new() { TimerSource = "" },
                new() { TimerSource = "Shared" }
            });

            Assert.That(DefaultOrbsTimersManager.GetAllDefaults(), Has.Some.Matches<DefaultTimersData>(x => x.TimerSource == "Shared"));
        }
        finally
        {
            pathField.SetValue(null, originalPath);
            textField.SetValue(null, originalText);
            cacheField.SetValue(null, originalCache);
            Directory.Delete(directory, true);
        }
    }
}
