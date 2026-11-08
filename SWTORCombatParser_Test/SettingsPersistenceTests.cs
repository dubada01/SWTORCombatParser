using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser_Test;

[TestFixture, NonParallelizable]
public class SettingsPersistenceTests
{
    [Test]
    public void ConcurrentWritesPreserveDistinctSettingsAndReadersSeeCompleteJson()
    {
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var directoryField = typeof(Settings).GetField("_appDataPath", flags)!;
        var pathField = typeof(Settings).GetField("_settingsPath", flags)!;
        var originalDirectory = directoryField.GetValue(null);
        var originalPath = pathField.GetValue(null);
        var directory = Path.Combine(Path.GetTempPath(), "orbs-settings-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            directoryField.SetValue(null, directory);
            pathField.SetValue(null, Path.Combine(directory, "general_settings.json"));
            Parallel.For(0, 100, i =>
            {
                Settings.WriteSetting("test_" + i, i);
                Settings.ReadSettingOfType<int>("test_" + i);
            });

            for (var i = 0; i < 100; i++)
                Assert.That(Settings.ReadSettingOfType<int>("test_" + i), Is.EqualTo(i));
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }
        finally
        {
            directoryField.SetValue(null, originalDirectory);
            pathField.SetValue(null, originalPath);
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
