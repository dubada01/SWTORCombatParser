using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Newtonsoft.Json;
using NUnit.Framework;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Overlays.RaidHots;

namespace SWTORCombatParser_Test;

[TestFixture, NonParallelizable]
public class RaidFrameSettingsTests
{
    private static readonly FieldInfo DirectoryField = typeof(RaidFrameOverlayManager)
        .GetField("appDataPath", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly FieldInfo PathField = typeof(RaidFrameOverlayManager)
        .GetField("infoPath", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly FieldInfo CharacterDirectoryField = typeof(DefaultCharacterOverlays)
        .GetField("appDataPath", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly FieldInfo CharacterPathField = typeof(DefaultCharacterOverlays)
        .GetField("infoPath", BindingFlags.Static | BindingFlags.NonPublic)!;
    private object _originalDirectory;
    private object _originalPath;
    private object _originalCharacterDirectory;
    private object _originalCharacterPath;
    private string _directory;
    private string _path;

    [SetUp]
    public void SetUp()
    {
        _originalDirectory = DirectoryField.GetValue(null);
        _originalPath = PathField.GetValue(null);
        _originalCharacterDirectory = CharacterDirectoryField.GetValue(null);
        _originalCharacterPath = CharacterPathField.GetValue(null);
        _directory = Path.Combine(Path.GetTempPath(), "orbs-raid-settings-test-" + Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_directory, "raidframe_overlay_info.json");
        DirectoryField.SetValue(null, _directory);
        PathField.SetValue(null, _path);
        CharacterDirectoryField.SetValue(null, _directory);
        CharacterPathField.SetValue(null, Path.Combine(_directory, "character_overlay_info.json"));
        DefaultCharacterOverlays.Init();
    }

    [TearDown]
    public void TearDown()
    {
        DirectoryField.SetValue(null, _originalDirectory);
        PathField.SetValue(null, _originalPath);
        CharacterDirectoryField.SetValue(null, _originalCharacterDirectory);
        CharacterPathField.SetValue(null, _originalCharacterPath);
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [Test]
    public void FirstCharacterWorksWithoutAnExistingAnonymousLayout()
    {
        var defaults = RaidFrameOverlayManager.GetDefaults("Healer/Medicine");

        Assert.Multiple(() =>
        {
            Assert.That(defaults.Rows, Is.EqualTo(4));
            Assert.That(defaults.Columns, Is.EqualTo(2));
            Assert.That(defaults.WidtHHeight, Is.EqualTo(new Point(400, 400)));
            Assert.That(RaidFrameOverlayManager.GetDefaults("Healer/Medicine").Rows, Is.EqualTo(4));
        });
    }

    [Test]
    public void NewCharacterCopiesAnonymousLayoutWithoutChangingIt()
    {
        RaidFrameOverlayManager.SetDefaults(new Point(-100, 250), new Point(600, 450), "no character");
        RaidFrameOverlayManager.SetRowsColumns(6, 3, "no character");
        var defaults = RaidFrameOverlayManager.GetDefaults("Healer/Medicine");
        RaidFrameOverlayManager.SetRowsColumns(4, 2, "Healer/Medicine");

        Assert.Multiple(() =>
        {
            Assert.That(defaults.Rows, Is.EqualTo(6));
            Assert.That(defaults.Columns, Is.EqualTo(3));
            Assert.That(defaults.Position, Is.EqualTo(new Point(-100, 250)));
            Assert.That(defaults.WidtHHeight, Is.EqualTo(new Point(600, 450)));
            Assert.That(RaidFrameOverlayManager.GetDefaults("no character").Rows, Is.EqualTo(6));
        });
    }

    [TestCase(4, 0, 4, 2)]
    [TestCase(-1, 3, 4, 3)]
    [TestCase(2.5, 1000000, 4, 2)]
    public void InvalidSavedDimensionsAreRepairedIndependently(double rows, double columns,
        double expectedRows, double expectedColumns)
    {
        RaidFrameOverlayManager.Init();
        File.WriteAllText(_path, JsonConvert.SerializeObject(new Dictionary<string, RaidFrameOverlayInfo>
        {
            ["Healer"] = new RaidFrameOverlayInfo
            {
                Position = new Point(120, 200), WidtHHeight = new Point(500, 600),
                Rows = rows, Columns = columns, Acive = true
            }
        }));

        var defaults = RaidFrameOverlayManager.GetDefaults("Healer");
        var saved = JsonConvert.DeserializeObject<Dictionary<string, RaidFrameOverlayInfo>>(File.ReadAllText(_path))["Healer"];
        Assert.Multiple(() =>
        {
            Assert.That(defaults.Rows, Is.EqualTo(expectedRows));
            Assert.That(defaults.Columns, Is.EqualTo(expectedColumns));
            Assert.That(saved.Rows, Is.EqualTo(expectedRows));
            Assert.That(saved.Columns, Is.EqualTo(expectedColumns));
            Assert.That(saved.Position, Is.EqualTo(new Point(120, 200)));
            Assert.That(saved.Acive, Is.True);
        });
    }

    [TestCase(0, 2)]
    [TestCase(4, 25)]
    [TestCase(double.NaN, 2)]
    [TestCase(4, double.PositiveInfinity)]
    [TestCase(1.5, 2)]
    public void InvalidEditsDoNotOverwriteTheSavedGrid(double rows, double columns)
    {
        RaidFrameOverlayManager.SetRowsColumns(8, 2, "Healer");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RaidFrameOverlayManager.SetRowsColumns(rows, columns, "Healer"));
        Assert.That(RaidFrameOverlayManager.GetDefaults("Healer").Rows, Is.EqualTo(8));
        Assert.That(RaidFrameOverlayManager.GetDefaults("Healer").Columns, Is.EqualTo(2));
    }

    [Test]
    public void SimultaneousPositionAndGridWritesPreserveEveryCharacter()
    {
        Parallel.For(0, 30, i =>
        {
            var character = "Healer" + i;
            Parallel.Invoke(
                () => RaidFrameOverlayManager.SetRowsColumns(8, 3, character),
                () => RaidFrameOverlayManager.SetDefaults(new Point(i, 200), new Point(500, 600), character),
                () => RaidFrameOverlayManager.SetActiveState(true, character));
        });

        for (var i = 0; i < 30; i++)
        {
            var saved = RaidFrameOverlayManager.GetDefaults("Healer" + i);
            Assert.Multiple(() =>
            {
                Assert.That(saved.Rows, Is.EqualTo(8));
                Assert.That(saved.Columns, Is.EqualTo(3));
                Assert.That(saved.Position, Is.EqualTo(new Point(i, 200)));
                Assert.That(saved.WidtHHeight, Is.EqualTo(new Point(500, 600)));
                Assert.That(saved.Acive, Is.True);
            });
        }
    }

    [Test]
    public void MissingCharacterOverlayGetsItsSavedRaidGeometryAndAnonymousEnabledState()
    {
        DefaultCharacterOverlays.SetCharacterDefaults("RaidFrame", new Point(1, 2), new Point(300, 400), "no character");
        DefaultCharacterOverlays.SetActiveStateCharacter("RaidFrame", true, "no character");
        RaidFrameOverlayManager.SetDefaults(new Point(-200, 350), new Point(600, 700), "Healer/Medicine");

        EnsureCharacterOverlayDefaults("Healer/Medicine");

        var saved = DefaultCharacterOverlays.GetCharacterDefaults("Healer/Medicine")["RaidFrame"];
        Assert.Multiple(() =>
        {
            Assert.That(saved.Position, Is.EqualTo(new Point(-200, 350)));
            Assert.That(saved.WidtHHeight, Is.EqualTo(new Point(600, 700)));
            Assert.That(saved.Acive, Is.True);
        });
    }

    [Test]
    public void ExistingCharacterOverlayKeepsItsGeometryAndEnabledState()
    {
        DefaultCharacterOverlays.SetCharacterDefaults("RaidFrame", new Point(100, 250), new Point(300, 400), "Healer/Medicine");
        DefaultCharacterOverlays.SetActiveStateCharacter("RaidFrame", true, "Healer/Medicine");
        RaidFrameOverlayManager.SetDefaults(new Point(900, 900), new Point(700, 800), "Healer/Medicine");
        RaidFrameOverlayManager.SetActiveState(false, "Healer/Medicine");

        EnsureCharacterOverlayDefaults("Healer/Medicine");

        var saved = DefaultCharacterOverlays.GetCharacterDefaults("Healer/Medicine")["RaidFrame"];
        Assert.Multiple(() =>
        {
            Assert.That(saved.Position, Is.EqualTo(new Point(100, 250)));
            Assert.That(saved.WidtHHeight, Is.EqualTo(new Point(300, 400)));
            Assert.That(saved.Acive, Is.True);
        });
    }

    private static void EnsureCharacterOverlayDefaults(string character) =>
        typeof(RaidHotsConfigViewModel).GetMethod("EnsureCharacterOverlayDefaults",
            BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { character });
}
