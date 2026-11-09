using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser_Test;

[TestFixture, NonParallelizable]
public class OverlayAppearanceSettingsTests
{
    private static readonly FieldInfo DirectoryField = typeof(Settings).GetField("_appDataPath", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly FieldInfo PathField = typeof(Settings).GetField("_settingsPath", BindingFlags.NonPublic | BindingFlags.Static)!;
    private object _originalDirectory;
    private object _originalPath;
    private string _directory;

    [SetUp]
    public void SetUp()
    {
        _originalDirectory = DirectoryField.GetValue(null);
        _originalPath = PathField.GetValue(null);
        _directory = Path.Combine(Path.GetTempPath(), "orbs-appearance-test-" + Guid.NewGuid().ToString("N"));
        DirectoryField.SetValue(null, _directory);
        PathField.SetValue(null, Path.Combine(_directory, "general_settings.json"));
    }

    [TearDown]
    public void TearDown()
    {
        DirectoryField.SetValue(null, _originalDirectory);
        PathField.SetValue(null, _originalPath);
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Test]
    public void ExistingInstallationsKeepEachOverlayDesignUntilAnOverrideIsSelected()
    {
        Assert.Multiple(() =>
        {
            Assert.That(OverlayAppearanceSettings.ContentOpacity, Is.EqualTo(1));
            Assert.That(OverlayAppearanceSettings.BackgroundOpacity, Is.Null);
            Assert.That(OverlayAppearanceSettings.ResolveBackgroundOpacity(true, 0.066, 0.75), Is.EqualTo(0.066));
            Assert.That(OverlayAppearanceSettings.ResolveBackgroundOpacity(true, 0, 0.05), Is.Zero);
            Assert.That(OverlayAppearanceSettings.ResolveBackgroundOpacity(false, 0, 0.05), Is.EqualTo(0.05));
        });
    }

    [Test]
    public void AppearanceIsPersistedAndFreshReadsUseTheSavedValues()
    {
        OverlayAppearanceSettings.SetContentOpacity(0.42);
        OverlayAppearanceSettings.SetBackgroundOpacity(0.31);
        Assert.That(Settings.ReadSettingOfType<double>(OverlayAppearanceSettings.ContentOpacityKey), Is.EqualTo(0.42));
        Assert.That(Settings.ReadSettingOfType<double>(OverlayAppearanceSettings.BackgroundOpacityKey), Is.EqualTo(0.31));
        Settings.WriteSetting(OverlayAppearanceSettings.ContentOpacityKey, 0.68);
        Assert.That(OverlayAppearanceSettings.ContentOpacity, Is.EqualTo(0.68));
        Assert.That(OverlayAppearanceSettings.BackgroundOpacity, Is.EqualTo(0.31));
    }

    [TestCase(0)]
    [TestCase(0.35)]
    [TestCase(1)]
    public void BackgroundOverridesIncludeFullyTransparentAndFullyOpaque(double opacity)
    {
        OverlayAppearanceSettings.SetBackgroundOpacity(opacity);
        Assert.That(OverlayAppearanceSettings.ResolveBackgroundOpacity(true, 0.066, 0.75), Is.EqualTo(opacity));
        Assert.That(OverlayAppearanceSettings.ResolveBackgroundOpacity(true, 0, 0.05), Is.EqualTo(opacity));
        Assert.That(OverlayAppearanceSettings.ResolveBackgroundOpacity(false, 0.066, 0.75), Is.EqualTo(0.75));
    }

    [TestCase(-2, 0.2)]
    [TestCase(0, 0.2)]
    [TestCase(0.6, 0.6)]
    [TestCase(4, 1)]
    [TestCase(double.NaN, 1)]
    [TestCase(double.PositiveInfinity, 1)]
    public void InvalidSavedContentOpacityCannotMakeAnOverlayUnrecoverable(double saved, double expected)
    {
        Settings.WriteSetting(OverlayAppearanceSettings.ContentOpacityKey, saved);
        Assert.That(OverlayAppearanceSettings.ContentOpacity, Is.EqualTo(expected));
    }

    [TestCase(-1, 0)]
    [TestCase(3, 1)]
    public void BackgroundEditsAreClampedBeforeSaving(double input, double expected)
    {
        OverlayAppearanceSettings.SetBackgroundOpacity(input);
        Assert.That(Settings.ReadSettingOfType<double>(OverlayAppearanceSettings.BackgroundOpacityKey), Is.EqualTo(expected));
    }

    [Test]
    public void MalformedOrNonfiniteBackgroundFallsBackToTheNativeDesign()
    {
        Settings.WriteSetting(OverlayAppearanceSettings.BackgroundOpacityKey, "not an opacity");
        Assert.That(OverlayAppearanceSettings.BackgroundOpacity, Is.Null);
        Settings.WriteSetting(OverlayAppearanceSettings.BackgroundOpacityKey, double.NaN);
        Assert.That(OverlayAppearanceSettings.BackgroundOpacity, Is.Null);
    }

    [Test]
    public void CommittedAppearanceChangesNotifyLiveOverlaysWithTheUpdatedValues()
    {
        var changes = new List<string>();
        var appearances = new List<OverlayAppearance>();
        Action<OverlayAppearance> appearanceChanged = appearance => appearances.Add(appearance);
        Action<string> changed = name =>
        {
            if (!OverlayAppearanceSettings.IsAppearanceSetting(name)) return;
            changes.Add(name);
            if (name == OverlayAppearanceSettings.ContentOpacityKey)
                Assert.That(OverlayAppearanceSettings.ContentOpacity, Is.EqualTo(0.55));
            else
                Assert.That(OverlayAppearanceSettings.BackgroundOpacity, Is.EqualTo(0.25));
        };
        Settings.SettingsUpdated += changed;
        OverlayAppearanceSettings.AppearanceChanged += appearanceChanged;
        try
        {
            OverlayAppearanceSettings.SetContentOpacity(0.55);
            OverlayAppearanceSettings.SetBackgroundOpacity(0.25);
            Settings.WriteSetting("unrelated_setting", true);
            Assert.That(changes, Is.EqualTo(new[] { OverlayAppearanceSettings.ContentOpacityKey, OverlayAppearanceSettings.BackgroundOpacityKey }));
            Assert.That(appearances, Is.EqualTo(new[] { new OverlayAppearance(0.55, null), new OverlayAppearance(0.55, 0.25) }));
        }
        finally
        {
            Settings.SettingsUpdated -= changed;
            OverlayAppearanceSettings.AppearanceChanged -= appearanceChanged;
        }
    }

    [Test]
    public void ResetRestoresNativeBackgroundsWithoutChangingOtherSettings()
    {
        Settings.WriteSetting("overlay_bar_scale", 1.5);
        OverlayAppearanceSettings.SetContentOpacity(0.4);
        OverlayAppearanceSettings.SetBackgroundOpacity(0.9);
        OverlayAppearanceSettings.Reset();
        Assert.Multiple(() =>
        {
            Assert.That(OverlayAppearanceSettings.ContentOpacity, Is.EqualTo(1));
            Assert.That(OverlayAppearanceSettings.BackgroundOpacity, Is.Null);
            Assert.That(OverlayAppearanceSettings.ResolveBackgroundOpacity(true, 0, 0.05), Is.Zero);
            Assert.That(Settings.ReadSettingOfType<double>("overlay_bar_scale"), Is.EqualTo(1.5));
        });
    }

    [Test]
    public void PresentationDefaultsToCompactAndPersistsIndependentlyForEachOverlay()
    {
        Assert.That(OverlayPresentationSettings.Read("BossFrame"), Is.Zero);
        OverlayPresentationSettings.Write("BossFrame", 1);
        OverlayPresentationSettings.Write("Encounter", 0);
        Assert.Multiple(() =>
        {
            Assert.That(OverlayPresentationSettings.Read("BossFrame"), Is.EqualTo(1));
            Assert.That(OverlayPresentationSettings.Read("Encounter"), Is.Zero);
            Assert.That(OverlayPresentationSettings.Read("Discipline"), Is.Zero);
            Assert.That(Settings.ReadSettingOfType<int>(OverlayPresentationSettings.Prefix + "BossFrame"), Is.EqualTo(1));
        });
        OverlayAppearanceSettings.Reset();
        Assert.That(OverlayPresentationSettings.Read("BossFrame"), Is.EqualTo(1));
    }

    [Test]
    public void InvalidPresentationSelectionsDoNotOverwriteTheSavedChoice()
    {
        OverlayPresentationSettings.Write("BossFrame", 1);
        OverlayPresentationSettings.Write("BossFrame", -1); // ComboBox clears selection as items change.
        OverlayPresentationSettings.Write("BossFrame", 99);
        Assert.That(OverlayPresentationSettings.Read("BossFrame"), Is.EqualTo(1));
        Settings.WriteSetting(OverlayPresentationSettings.Prefix + "BossFrame", 8);
        Assert.That(OverlayPresentationSettings.Read("BossFrame"), Is.Zero);
    }
}
