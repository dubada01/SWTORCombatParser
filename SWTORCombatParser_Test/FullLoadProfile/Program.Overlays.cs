using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Controls.ApplicationLifetimes;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels;
using SWTORCombatParser.ViewModels.Overlays;
using SWTORCombatParser.ViewModels.Overlays.RaidHots;
using SWTORCombatParser.Views;

internal static partial class Program
{
    private static void WriteBuildInfo()
    {
        object Describe(Assembly assembly)
        {
            using var stream = File.OpenRead(assembly.Location);
            return new { Path = assembly.Location, SHA256 = Convert.ToHexString(SHA256.HashData(stream)),
                Configuration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration,
                Version = assembly.GetName().Version?.ToString() };
        }
        File.WriteAllText(Path.Combine(OutputDirectory, "build-info.json"), JsonSerializer.Serialize(new
        {
            Application = Describe(typeof(Settings).Assembly), Harness = Describe(typeof(Program).Assembly),
            DebugReplayEnabled = DebugFeatures.LogReplayEnabled, Runtime = RuntimeInformation.FrameworkDescription,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(), OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            Environment.ProcessId, RecordedUtc = DateTime.UtcNow, LiveReplay, ReplaySpeed, FullOverlays, AudioProbe
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed partial class FullLoadApp
{
    private static readonly ConditionalWeakTable<object, OverlayCounter> OverlayCounters = new();
    private static readonly ConditionalWeakTable<object, HashSet<int>> ObservedOverlayChildren = new();
    private static int OverlayIdentity;
    private sealed class OverlayCounter
    {
        internal readonly int Id = Interlocked.Increment(ref OverlayIdentity);
        internal long Properties, ChildProperties, Collections;
    }

    private static void ConfigureFullOverlayOptions(MainWindowViewModel vm)
    {
        var overlays = (OverlayViewModel)typeof(MainWindowViewModel).GetField("_overlayViewModel", PrivateInstance)!.GetValue(vm)!;
        var metrics = new[] { OverlayType.DPS, OverlayType.HPS, OverlayType.Threat };
        // Class detection may refresh a different role layout after the prelude.
        // Seed only isolated role settings so the requested metric windows persist.
        foreach (var role in new[] { "DPS", "Healer", "Tank", "Custom", "Unknown" })
        {
            if (!DefaultCharacterOverlays.DoesKeyExist(role)) DefaultCharacterOverlays.InitializeCharacterDefaults(role);
            foreach (var metric in metrics) DefaultCharacterOverlays.SetActiveStateCharacter(metric.ToString(), true, role);
        }
        var options = overlays.MainDamageOverlays.Concat(overlays.MainHealOverlays).Concat(overlays.MainMitigationOverlays)
            .Concat(overlays.AdvancedDamageOverlays).Concat(overlays.AdvancedHealOverlays).Concat(overlays.AdvancedMitigationOverlays)
            .Concat(overlays.AvailableGeneralOverlays).ToArray();
        foreach (var metric in metrics)
            typeof(OverlayViewModel).GetMethod("CreateOverlay", PrivateInstance)!
                .Invoke(overlays, new object[] { options.First(option => option.Type == metric), false });
        var utilityTypes = new[] { UtilityOverlayType.Personal, UtilityOverlayType.RaidBoss, UtilityOverlayType.RaidHot,
            UtilityOverlayType.ThreatTable, UtilityOverlayType.RunRace };
        foreach (var type in utilityTypes)
        {
            var option = overlays.AvailableUtilityOverlays.First(option => option.Type == type);
            if (!option.IsSelected) typeof(OverlayViewModel).GetMethod("ToggleUtility", PrivateInstance)!.Invoke(overlays, new object[] { option });
        }
        WriteLive("full-overlays-setup.json", new { Metrics = metrics.Select(metric => metric.ToString()).ToArray(),
            Utilities = utilityTypes.Select(type => type.ToString()).ToArray(),
            Notes = "Existing overlay option handlers and isolated role defaults. No PvP or room-hazard overlay enabled. RaidHOT cell names are populated from parsed live participants to exercise timer routing without screen capture/OCR.",
            Windows = CaptureOverlayWindows() });
    }

    private static object[] CaptureOverlayWindows()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return Array.Empty<object>();
        var result = new List<object>();
        foreach (var window in desktop.Windows.OfType<BaseOverlayWindow>())
        {
            if (window.DataContext is not BaseOverlayViewModel viewModel) continue;
            var counter = OverlayCounters.GetValue(viewModel, value =>
            {
                var added = new OverlayCounter();
                if (value is INotifyPropertyChanged notifier) notifier.PropertyChanged += (_, _) => Interlocked.Increment(ref added.Properties);
                return added;
            });
            if (Program.FullOverlays && viewModel is RaidFrameOverlayViewModel raid && CombatIdentifier.CurrentCombat is { } combat)
            {
                var names = combat.CharacterParticipants.Values.Where(player => !player.IsCompanion && !string.IsNullOrEmpty(player.Name))
                    .Select(player => player.Name).Distinct().Order(StringComparer.Ordinal).ToArray();
                var cells = raid.RaidHotCells.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column).ToArray();
                for (var index = 0; index < Math.Min(names.Length, cells.Length); index++)
                    if (cells[index].Name != names[index]) cells[index].Name = names[index];
            }
            var counts = new Dictionary<string, int>();
            foreach (var propertyName in new[] { "MetricBars", "PersonalOverlayInstances", "ThreatEntries", "BossesDetected", "RaidHotCells", "SwtorTimers", "ActiveChallengeInstances" })
            {
                if (OverlayProperty(viewModel, propertyName) is not IEnumerable children) continue;
                ObserveOverlayChild(children, counter);
                var count = 0;
                foreach (var child in children)
                {
                    count++;
                    ObserveOverlayChild(child, counter);
                    if (OverlayProperty(child, "HPContent") is Avalonia.Controls.Control hp && hp.DataContext != null)
                        ObserveOverlayChild(hp.DataContext, counter);
                    if (OverlayProperty(child, "ViewModel") is { } childViewModel) ObserveOverlayChild(childViewModel, counter);
                }
                counts[propertyName] = count;
            }
            if (OverlayProperty(viewModel, "Race") is { } race)
            {
                ObserveOverlayChild(race, counter);
                if (OverlayProperty(race, "Rows") is IEnumerable rows) counts["RunRaceRows"] = rows.Cast<object>().Count();
            }
            result.Add(new { InstanceId = counter.Id, ViewModelType = viewModel.GetType().Name, Name = viewModel.OverlayDisplayName,
                viewModel.Active, viewModel.ShouldBeVisible, window.IsVisible, DataCounts = counts,
                PropertyNotifications = Interlocked.Read(ref counter.Properties), ChildPropertyNotifications = Interlocked.Read(ref counter.ChildProperties),
                CollectionNotifications = Interlocked.Read(ref counter.Collections),
                MetricTotal = OverlayProperty(viewModel, "MetricTotal") as string });
        }
        return result.ToArray();
    }

    private static object? OverlayProperty(object? instance, string name)
    {
        // AlertsWindowViewModel hides the base SwtorTimers with another type.
        // Resolve the actual derived property instead of asking reflection to
        // choose between inherited properties with incompatible return types.
        for (var type = instance?.GetType(); type != null; type = type.BaseType)
        {
            var property = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .FirstOrDefault(candidate => candidate.Name == name && candidate.GetIndexParameters().Length == 0);
            if (property != null) return property.GetValue(instance);
        }
        return null;
    }

    private static void ObserveOverlayChild(object? child, OverlayCounter owner)
    {
        if (child == null) return;
        var owners = ObservedOverlayChildren.GetOrCreateValue(child);
        if (!owners.Add(owner.Id)) return;
        if (child is INotifyPropertyChanged notifier) notifier.PropertyChanged += (_, _) => Interlocked.Increment(ref owner.ChildProperties);
        if (child is INotifyCollectionChanged collection) collection.CollectionChanged += (_, _) => Interlocked.Increment(ref owner.Collections);
    }
}
