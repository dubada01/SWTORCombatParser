using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using Avalonia.Media;
using Newtonsoft.Json;
using NUnit.Framework;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels;

namespace SWTORCombatParser_Test;

[TestFixture, NonParallelizable]
public class MetricColorPickerTests
{
    private readonly Dictionary<FieldInfo, object> _originalFields = new();
    private readonly Color _initialColor = Color.Parse("#ffab3456");
    private readonly Color _defaultColor = Color.Parse("#ffcd5c5c");
    private string _directory;
    private string _currentPath;
    private string _defaultsPath;
    private readonly List<OverlayType> _updates = new();

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "orbs-metric-color-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _currentPath = Path.Combine(_directory, "metric_colors.json");
        _defaultsPath = Path.Combine(_directory, "default_metric_colors.json");
        Redirect("appDataPath", _directory);
        Redirect("infoPath", _currentPath);
        Redirect("defaultsPath", _defaultsPath);
        Redirect("CurrentMetricBrushDict", new Dictionary<OverlayType, SolidColorBrush>());
        Redirect("OnOverlayTypeColorUpdated", (Action<OverlayType>)(_ => { }));
        // Seed every metric so no Application resources are needed to fill missing defaults.
        var current = Enum.GetValues<OverlayType>().ToDictionary(metric => metric.ToString(), _ => Colors.SlateGray.ToString());
        current[nameof(OverlayType.DPS)] = _initialColor.ToString();
        var defaults = new Dictionary<string, string>(current) { [nameof(OverlayType.DPS)] = _defaultColor.ToString() };
        File.WriteAllText(_currentPath, JsonConvert.SerializeObject(current));
        File.WriteAllText(_defaultsPath, JsonConvert.SerializeObject(defaults));
        MetricColorLoader.SetCurrentBrushDict();
        _updates.Clear();
        MetricColorLoader.OnOverlayTypeColorUpdated += _updates.Add;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var (field, value) in _originalFields) field.SetValue(null, value);
        _originalFields.Clear();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Test]
    public void OpeningPickerReadsStoredColorWithoutWritingOrBroadcastingChanges()
    {
        var before = File.ReadAllText(_currentPath);
        var timestamp = SetKnownWriteTime();

        var vm = new MetricColorPickerViewModel(OverlayType.DPS);

        Assert.Multiple(() =>
        {
            Assert.That(vm.MetricColor, Is.EqualTo(_initialColor));
            Assert.That(vm.MetricBrush.Color, Is.EqualTo(_initialColor));
            Assert.That(File.ReadAllText(_currentPath), Is.EqualTo(before));
            Assert.That(File.GetLastWriteTimeUtc(_currentPath), Is.EqualTo(timestamp));
            Assert.That(_updates, Is.Empty);
        });
    }

    [Test]
    public void ColorEditUpdatesPersistencePreviewAndBoundProperties()
    {
        var vm = new MetricColorPickerViewModel(OverlayType.DPS);
        var properties = new List<string>();
        vm.PropertyChanged += (_, e) => properties.Add(e.PropertyName);
        var before = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(_currentPath));
        var selected = Color.Parse("#cc4287a1");

        vm.MetricColor = selected;

        var saved = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(_currentPath));
        Assert.Multiple(() =>
        {
            Assert.That(Color.Parse(saved[nameof(OverlayType.DPS)]), Is.EqualTo(selected));
            Assert.That(saved.Where(pair => pair.Key != nameof(OverlayType.DPS)),
                Is.EquivalentTo(before.Where(pair => pair.Key != nameof(OverlayType.DPS))));
            Assert.That(vm.MetricBrush.Color, Is.EqualTo(selected));
            Assert.That(MetricColorLoader.CurrentMetricBrushDict[OverlayType.DPS].Color, Is.EqualTo(selected));
            Assert.That(properties, Does.Contain(nameof(vm.MetricColor)));
            Assert.That(properties, Does.Contain(nameof(vm.MetricBrush)));
            Assert.That(_updates, Is.EqualTo(new[] { OverlayType.DPS }));
        });
    }

    [Test]
    public void AssigningExistingColorDoesNotRewriteSettingsOrNotifyBindings()
    {
        var vm = new MetricColorPickerViewModel(OverlayType.DPS);
        var properties = new List<string>();
        vm.PropertyChanged += (_, e) => properties.Add(e.PropertyName);
        var timestamp = SetKnownWriteTime();

        vm.MetricColor = vm.MetricColor;

        Assert.That(File.GetLastWriteTimeUtc(_currentPath), Is.EqualTo(timestamp));
        Assert.That(properties, Is.Empty);
        Assert.That(_updates, Is.Empty);
    }

    [Test]
    public void RestoreDefaultPersistsDefaultAndRefreshesPreview()
    {
        var vm = new MetricColorPickerViewModel(OverlayType.DPS);
        var defaultsBefore = File.ReadAllText(_defaultsPath);
        var properties = new List<string>();
        vm.PropertyChanged += (_, e) => properties.Add(e.PropertyName);

        using var command = vm.SetDefaultCommand;
        command.Execute().Timeout(TimeSpan.FromSeconds(5)).Wait();

        Assert.Multiple(() =>
        {
            Assert.That(vm.MetricColor, Is.EqualTo(_defaultColor));
            Assert.That(vm.MetricBrush.Color, Is.EqualTo(_defaultColor));
            Assert.That(MetricColorLoader.GetMetricCurrentColor(OverlayType.DPS), Is.EqualTo(_defaultColor));
            Assert.That(File.ReadAllText(_defaultsPath), Is.EqualTo(defaultsBefore));
            Assert.That(properties, Does.Contain(nameof(vm.MetricColor)));
            Assert.That(properties, Does.Contain(nameof(vm.MetricBrush)));
            Assert.That(_updates, Is.EqualTo(new[] { OverlayType.DPS }));
        });
    }

    [Test]
    public void CloseRequestsDismissalWithoutChangingStoredColor()
    {
        var vm = new MetricColorPickerViewModel(OverlayType.DPS);
        var closed = 0;
        vm.CloseRequested += () => closed++;
        var timestamp = SetKnownWriteTime();

        using var command = vm.CloseCommand;
        command.Execute().Timeout(TimeSpan.FromSeconds(5)).Wait();

        Assert.That(closed, Is.EqualTo(1));
        Assert.That(File.GetLastWriteTimeUtc(_currentPath), Is.EqualTo(timestamp));
        Assert.That(_updates, Is.Empty);
    }

    private DateTime SetKnownWriteTime()
    {
        File.SetLastWriteTimeUtc(_currentPath, new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        return File.GetLastWriteTimeUtc(_currentPath);
    }

    private void Redirect(string name, object value)
    {
        var field = typeof(MetricColorLoader).GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        _originalFields.Add(field, field.GetValue(null));
        field.SetValue(null, value);
    }
}
