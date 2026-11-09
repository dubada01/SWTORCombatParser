using System;
using System.IO;
using System.Reflection;
using Avalonia;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SWTORCombatParser.Model.Overlays;

namespace SWTORCombatParser_Test;

[TestFixture, NonParallelizable]
public class OverlayPositionResetTests
{
    private static readonly FieldInfo DirectoryField = typeof(OverlayPositionReset).GetField("_appDataPath", BindingFlags.NonPublic | BindingFlags.Static)!;
    private object _originalDirectory;
    private string _directory;

    [SetUp]
    public void SetUp()
    {
        _originalDirectory = DirectoryField.GetValue(null);
        _directory = Path.Combine(Path.GetTempPath(), "orbs-position-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        DirectoryField.SetValue(null, _directory);
    }

    [TearDown]
    public void TearDown()
    {
        DirectoryField.SetValue(null, _originalDirectory);
        Directory.Delete(_directory, true);
    }

    [Test]
    public void ResetChangesOnlyPositionFieldsInKnownLayoutFiles()
    {
        var global = JObject.Parse("{\"Timers\":{\"Position\":\"9000, 4000\",\"WidtHHeight\":\"400, 500\",\"Acive\":true,\"Locked\":true,\"Custom\":{\"Position\":\"preserve\"}}}");
        var character = JObject.Parse("{\"Healer/Medicine\":{\"HPS\":{\"Position\":\"9001, 4001\",\"WidtHHeight\":\"300, 250\",\"Acive\":false,\"UseAsWindow\":true}},\"Tank\":{\"Threat\":{\"Position\":\"-9000, 4000\",\"Acive\":true}}}");
        var raid = JObject.Parse("{\"Healer\":{\"Position\":\"9000, 4000\",\"WidtHHeight\":\"600, 800\",\"Rows\":8,\"Columns\":2,\"Acive\":true}}");
        var boss = JObject.Parse("{\"Position\":\"1000, 1000\",\"Scale\":1.5,\"TrackDOTS\":true}");
        var room = JObject.Parse("{\"Position\":\"1500, 1500\",\"WidtHHeight\":\"250, 250\",\"ViewExtraData\":false}");
        foreach (var (file, data) in new[] { ("global_overlay_info.json", global), ("character_overlay_info.json", character),
                     ("raidframe_overlay_info.json", raid), ("bossframe_overlay_info.json", boss), ("room_overlay_info.json", room) })
            File.WriteAllText(Path.Combine(_directory, file), data.ToString());
        const string unrelated = "custom data, byte-for-byte unchanged";
        foreach (var file in new[] { "general_settings.json", "timers.json", "metric_colors.json", "personal_overlay.json", "main_window_position.json" })
            File.WriteAllText(Path.Combine(_directory, file), unrelated);

        var count = OverlayPositionReset.ResetSavedPositions(new Point(-1200, 48));

        Assert.That(count, Is.EqualTo(6));
        global["Timers"]["Position"] = "-1200, 48";
        character["Healer/Medicine"]["HPS"]["Position"] = "-1200, 48";
        character["Tank"]["Threat"]["Position"] = "-1200, 48";
        raid["Healer"]["Position"] = "-1200, 48";
        boss["Position"] = "-1200, 48";
        room["Position"] = "-1200, 48";
        foreach (var (file, expected) in new[] { ("global_overlay_info.json", global), ("character_overlay_info.json", character),
                     ("raidframe_overlay_info.json", raid), ("bossframe_overlay_info.json", boss), ("room_overlay_info.json", room) })
            Assert.That(JToken.DeepEquals(expected, JObject.Parse(File.ReadAllText(Path.Combine(_directory, file)))), Is.True, file);
        foreach (var file in new[] { "general_settings.json", "timers.json", "metric_colors.json", "personal_overlay.json", "main_window_position.json" })
            Assert.That(File.ReadAllText(Path.Combine(_directory, file)), Is.EqualTo(unrelated));
        Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
    }

    [Test]
    public void MalformedLayoutDoesNotPartiallyResetEarlierFiles()
    {
        const string valid = "{\"Timers\":{\"Position\":\"999, 999\"}}";
        var path = Path.Combine(_directory, "global_overlay_info.json");
        File.WriteAllText(path, valid);
        File.WriteAllText(Path.Combine(_directory, "character_overlay_info.json"), "invalid json");
        Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => OverlayPositionReset.ResetSavedPositions(new Point(24, 24)));
        Assert.That(File.ReadAllText(path), Is.EqualTo(valid));
    }

    [Test]
    public void MissingLayoutsAreNotCreatedAndDoNotRequestWindowReloads()
    {
        var notifications = 0;
        Action handler = () => notifications++;
        OverlayPositionReset.PositionsReset += handler;
        try
        {
            Assert.That(OverlayPositionReset.ResetSavedPositions(new Point(24, 24)), Is.Zero);
            Assert.That(notifications, Is.Zero);
            Assert.That(Directory.GetFiles(_directory), Is.Empty);
        }
        finally { OverlayPositionReset.PositionsReset -= handler; }
    }

    [Test]
    public void SavedChangesAreAvailableBeforeLiveWindowsAreNotified()
    {
        var path = Path.Combine(_directory, "global_overlay_info.json");
        File.WriteAllText(path, "{\"Timers\":{\"Position\":\"999, 999\"}}");
        var notifications = 0;
        Action handler = () =>
        {
            notifications++;
            Assert.That((string)JObject.Parse(File.ReadAllText(path))["Timers"]["Position"], Is.EqualTo("24, 48"));
        };
        OverlayPositionReset.PositionsReset += handler;
        try
        {
            OverlayPositionReset.ResetSavedPositions(new Point(24, 48));
            Assert.That(notifications, Is.EqualTo(1));
        }
        finally { OverlayPositionReset.PositionsReset -= handler; }
    }
}
