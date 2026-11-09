using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.LogReview;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ReactiveUI;

namespace SWTORCombatParser.DataStructures
{
    public class DisplayableLogEntry:ReactiveObject
    {
        private long _sourceId;
        private long _targetId;
        private ulong _abilityId;
        private ulong _effectId;
        private readonly static SolidColorBrush _transparentBackground = new(Brushes.Transparent.Color);
        private readonly static SolidColorBrush _deathBackground = new(Brushes.IndianRed.Color);
        private readonly static SolidColorBrush _deathBackgroundWithSource = new(Brushes.Crimson.Color);
        private readonly static SolidColorBrush _revivedBackground = new(Brushes.CornflowerBlue.Color);
        private readonly static SolidColorBrush _damageBackground = new (Color.Parse("#613b3b"));
        private readonly string _logPath;
        private readonly long _lineNumber;
        private Task? _iconTask;
        private Bitmap? _abilityIcon;
        private Bitmap? _effectIcon;
        private ReactiveCommand<string, Unit>? _cellClickedCommand;
        private ReactiveCommand<Unit, Unit>? _openLogCommand;
        private ReactiveCommand<string, Unit>? _openJedipediaCommand;
        public ParsedLogEntry? LogEntry { get; }
        public double ElapsedSeconds { get; }
        public string Kind => LogEntry == null ? ValueType : LogSearchQuery.Kind(LogEntry);
        public string Amount => LogEntry == null ? Value : LogEntry.Value.EffectiveDblValue != 0 || Kind is "Damage" or "Healing"
            ? LogEntry.Value.EffectiveDblValue.ToString("N0") : string.IsNullOrWhiteSpace(Value) ? "—" : Value;
        public string EffectLabel => Kind == "Activation" ? "Activated" : EffectName;
        public string Result => WasValueCrit ? "Critical" : LogEntry?.Value.ValueType == DamageType.none
            ? Kind switch { "Apply" => "Applied", "Remove" => "Removed", _ => Kind }
            : ValueType switch { "intern" => "Internal", "heal" => "Healing", "none" => Kind,
                _ => string.IsNullOrEmpty(ValueType) ? Kind : char.ToUpperInvariant(ValueType[0]) + ValueType[1..] };
        public IBrush EventColor => Kind switch {
            "Damage" => Brushes.IndianRed, "Healing" => Brushes.MediumSeaGreen, "Death" => Brushes.Salmon,
            "Revive" => Brushes.CornflowerBlue, "Activation" => Brushes.MediumPurple, _ => Brushes.Silver };
        public string SourceDetail => EntityDetail(Source, _sourceId, LogEntry?.SourceInfo);
        public string TargetDetail => EntityDetail(Target, _targetId, LogEntry?.TargetInfo);
        public string AbilityDetail => Ability + "\nID " + _abilityId;
        public string EffectDetail => EffectName + "\n" + Kind + " · ID " + _effectId;
        public string TimeDetail => LogEntry == null ? SecondsSinceCombatStart : LogEntry.TimeStamp.ToString("HH:mm:ss.fff") + "\n" + _logPath + " · line " + _lineNumber;
        public string AmountDetail => LogEntry == null ? Value :
            $"{Result} · {ValueType}\nEffective: {LogEntry.Value.EffectiveDblValue:N0}\nRaw: {LogEntry.Value.DblValue:N0}\nMitigated: {LogEntry.Value.MitigatedDblValue:N0}\nThreat: {LogEntry.Threat:N0}" +
            (string.IsNullOrWhiteSpace(ModifierType) ? "" : $"\n{ModifierType}: {ModifierValue}");
        public string Summary => $"{SecondsSinceCombatStart}   {Source} → {Target}   ·   {Ability}";
        public string DetailSummary => $"{Kind} · {EffectName} · {Result} · {Amount}" +
            (LogEntry?.TargetInfo.MaxHP > 0 ? $"   |   Target HP {LogEntry.TargetInfo.CurrentHP:N0} / {LogEntry.TargetInfo.MaxHP:N0}" : "");
        public bool CanOpenLog => !string.IsNullOrEmpty(_logPath);
        public Uri? JedipediaAbilityUrl => JedipediaLookup.Ability(_abilityId);
        public Uri? JedipediaEffectUrl => JedipediaLookup.Effect(_effectId);
        public bool CanLookupAbility => JedipediaAbilityUrl != null;
        public bool CanLookupEffect => JedipediaEffectUrl != null;
        public bool CanLookupJedipedia => CanLookupAbility || CanLookupEffect;
        public string JedipediaAbilityLabel => "Ability: " + (string.IsNullOrWhiteSpace(Ability) ? "Unavailable" : Ability);
        public string JedipediaEffectLabel => "Effect: " + (string.IsNullOrWhiteSpace(EffectName) ? "Unavailable" : EffectName);
        private static string EntityDetail(string name, long id, EntityInfo? info) => name + "\nID " + id +
            (info?.MaxHP > 0 ? $"\nHP {info.CurrentHP:N0} / {info.MaxHP:N0} ({100d * info.CurrentHP / info.MaxHP:N1}%)" : "");

        public DisplayableLogEntry(ParsedLogEntry log, DateTime start) : this(
            (log.TimeStamp - start).TotalSeconds.ToString(CultureInfo.InvariantCulture), log.Source.Name, log.Source.LogId,
            log.Target.Name, log.Target.LogId, log.Ability, log.AbilityId, log.Effect.EffectName, log.Effect.EffectId,
            log.Value.DisplayValue, log.Value.WasCrit, log.Value.ValueType != DamageType.none ? log.Value.ValueType.ToString() : log.Effect.EffectType.ToString(),
            log.Value.ModifierType, log.Value.ModifierDisplayValue, 0, log.Value.EffectiveDblValue, log.Threat, log.LogName, log.LogLineNumber)
        { LogEntry = log; }
        public DisplayableLogEntry(string sec, string source, long sourceId, string target, long targetId, string ability, ulong abilityId, string effectName, ulong effectId, string value, bool wasValueCrit, string type, string modifiertype, string modifierValue, double maxValue, double logValue, double threat,string logPath, long lineNumber)
        {
            _sourceId = sourceId;
            _targetId = targetId;
            _abilityId = abilityId;
            _effectId = effectId;

            _logPath = logPath;
            _lineNumber = lineNumber + 1;

            var doubleSeconds = double.Parse(sec, CultureInfo.InvariantCulture);
            ElapsedSeconds = doubleSeconds;
            var span = TimeSpan.FromSeconds(Math.Abs(doubleSeconds));
            SecondsSinceCombatStart = (doubleSeconds < 0 ? "−" : "") + ((int)span.TotalMinutes).ToString("00") + span.ToString(@"\:ss\.fff");
            Source = source;
            Target = target;
            Ability = ability;

            EffectName = effectName;

            EffectBackground = _transparentBackground;
            ValueBackground = _transparentBackground;

            AbilityTextMargin = _abilityId != 0 && IconGetter.HasIcon(_abilityId) ? new Thickness(18, 0, 0, 0) : new Thickness(5, 0, 0, 0);
            EffectTextMargin = _effectId != 0 && IconGetter.HasIcon(_effectId) ? new Thickness(18, 0, 0, 0) : new Thickness(5, 0, 0, 0);
            if (effectId == _7_0LogParsing.DeathCombatId)
            {
                EffectBackground = _deathBackground;
            }
            if (effectId == _7_0LogParsing.DeathCombatId && !string.IsNullOrEmpty(source))
            {
                EffectBackground = _deathBackgroundWithSource;
            }
            if (effectId == _7_0LogParsing.RevivedCombatId)
            {
                EffectBackground = _revivedBackground;
            }
            if (effectId == _7_0LogParsing._damageEffectId)
            {
                EffectBackground = _damageBackground;

            }

            if (maxValue > 0 && double.TryParse(value, out var r))
            {
                ValueBackground = new SolidColorBrush(GetColorForValue(logValue / maxValue));
            }

            Value = value;
            Threat = threat.ToString();
            WasValueCrit = wasValueCrit;
            ValueType = type;
            ModifierType = modifiertype;
            ModifierValue = modifierValue;
        }
        public Task AddIcons() => _iconTask ??= LoadIcons();
        private async Task LoadIcons()
        {
            var ability = _abilityId != 0 && IconGetter.HasIcon(_abilityId) ? await IconGetter.GetIconForId(_abilityId) : null;
            var effect = _effectId != 0 && IconGetter.HasIcon(_effectId) ? await IconGetter.GetIconForId(_effectId) : null;
            await Dispatcher.UIThread.InvokeAsync(() => {
                this.RaiseAndSetIfChanged(ref _abilityIcon, ability, nameof(AbilityIcon));
                this.RaiseAndSetIfChanged(ref _effectIcon, effect, nameof(EffectIcon));
            });
        }
        public SolidColorBrush EffectBackground { get; set; }
        public SolidColorBrush ValueBackground { get; set; }
        public string SecondsSinceCombatStart { get; }
        public string Source { get; }
        public string Target { get; }
        public string Ability { get; }
        public Bitmap? AbilityIcon => _abilityIcon;
        public string EffectName { get; }
        public Bitmap? EffectIcon => _effectIcon;
        public Thickness EffectTextMargin { get; set; }
        public Thickness AbilityTextMargin { get; set; }
        public string Value { get; }
        public string Threat { get; set; }
        public bool WasValueCrit { get; }
        public string ValueType { get; }
        public string ModifierType { get; }
        public string ModifierValue { get; }
        public ReactiveCommand<string, Unit> CellClickedCommand => _cellClickedCommand ??= ReactiveCommand.Create<string>(CellClicked);

        private void CellClicked(string obj)
        {
            //use the cross platform clipboard class to copy the data to the clipboard
            switch (obj)
            {
                case "Source":
                    CrossPlatformClipboard.SetText(_sourceId.ToString());
                    break;
                case "Target":
                    CrossPlatformClipboard.SetText(_targetId.ToString());
                    break;
                case "Ability":
                    CrossPlatformClipboard.SetText(_abilityId.ToString());
                    break;
                case "Effect":
                    CrossPlatformClipboard.SetText(_effectId.ToString());
                    break;

            }
        }
        public ReactiveCommand<Unit, Unit> OpenLogCommand => _openLogCommand ??= ReactiveCommand.Create(OpenLog);
        public ReactiveCommand<string, Unit> OpenJedipediaCommand => _openJedipediaCommand ??= ReactiveCommand.Create<string>(OpenJedipedia);

        private void OpenJedipedia(string field)
        {
            var url = field switch { "Ability" => JedipediaAbilityUrl, "Effect" => JedipediaEffectUrl, _ => null };
            if (url == null) return;
            try { Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true }); }
            catch (Exception error) { Logging.LogError("Could not open Jedipedia: " + error.Message); }
        }

        private void OpenLog()
        {
            if (string.IsNullOrEmpty(_logPath))
            {
                return;
            }

            try
            {
                var logsDirectory = Settings.ReadSettingOfType<string>("combat_logs_path");
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var logPathWithDirectory = Path.Combine(logsDirectory, _logPath);

                    // Check if Notepad++ is available
                    var notepadPlusPlusPath = @"C:\Program Files\Notepad++\notepad++.exe";
                    if (File.Exists(notepadPlusPlusPath))
                    {
                        Process.Start(notepadPlusPlusPath, $"-n{_lineNumber} \"{logPathWithDirectory}\"");
                    }
                    else
                    {
                        // Fall back to regular Notepad if Notepad++ is not available
                        Process.Start("notepad.exe", logPathWithDirectory);
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // Open the file in TextEdit on macOS
                    Process.Start("open", $"-a TextEdit \"{Path.Combine(logsDirectory,_logPath)}\"");
                }
                else
                {
                    Logging.LogError("Unsupported operating system.");
                }
            }
            catch (Exception ex)
            {
                Logging.LogError($"Failed to open log: {ex.Message}");
            }
        }

        private Color GetColorForValue(double fraction)
        {
            Color startColor = Colors.Transparent;
            Color endColor = Colors.Red;


            // Calculate the color based on the linear interpolation formula
            byte a = (byte)(startColor.A + (endColor.A - startColor.A) * fraction);
            byte r = (byte)(startColor.R + (endColor.R - startColor.R) * fraction);
            byte g = (byte)(startColor.G + (endColor.G - startColor.G) * fraction);
            byte b = (byte)(startColor.B + (endColor.B - startColor.B) * fraction);

            // Create a new color object
            Color selectedColor = Color.FromArgb(a, r, g, b);
            return selectedColor;
        }
    }
    public class ParsedLogEntry
    {
        public ErrorType Error;
        public long RaidLogId;
        public string LogName;
        public int LogBytes;
        public long LogLineNumber { get; set; }
        public string LogLocation { get; set; }
        public ulong LogLocationId { get; set; }
        public ulong LogDifficultyId { get; set; }
        public DateTime TimeStamp { get; set; }
        public double SecondsSinceCombatStart { get; set; }
        public Entity Source => SourceInfo.Entity;
        public EntityInfo SourceInfo { get; set; }
        public Entity Target => TargetInfo.Entity;
        public EntityInfo TargetInfo { get; set; }
        public string Ability { get; set; }
        public ulong AbilityId { get; set; }
        public Effect Effect { get; set; }
        public string ModifierEffectName => string.Intern(Ability + AddSecondHalf(Ability, Effect.EffectName));
        public Value Value { get; set; }
        public double Threat { get; set; }

        private static string AddSecondHalf(string firstHalf, string effectName)
        {
            if (firstHalf == effectName)
                return "";
            else
                return ": " + effectName;
        }

        public List<string> Strings() {
            return [
                Effect.EffectName,
                Effect.EffectId.ToString(),
                Ability,
                AbilityId.ToString(),
                Source.Name,
                Source.LogId.ToString(),
                Target.Name,
                Target.LogId.ToString(),
            ];
        }
    }
    public class PositionData
    {
        public float X;
        public float Y;
        public float Facing;
        public float Z;
    }
    public enum ErrorType
    {
        None,
        IncompleteLine
    }
    public class Entity:IEquatable<Entity>
    {
        public static Entity EmptyEntity = new Entity();
        public string Name { get; set; }
        public long Id { get; set; }
        public long LogId { get; set; }
        public bool IsCharacter;
        public bool IsLocalPlayer;
        public bool IsCompanion;
        public bool IsBoss;
        public bool Equals(Entity? other)
        {
            return other != null && LogId == other.LogId;
        }
    }
    public class EntityInfo
    {
        public SWTORClass Class { get; set; }
        public Entity Entity { get; set; } = Entity.EmptyEntity;
        public PositionData Position { get; set; } = new PositionData();
        public uint MaxHP { get; set; }
        public uint CurrentHP { get; set; } = 0;
        public bool IsAlive { get; set; }
    }
    public class Effect
    {
        public EffectType EffectType { get; set; }
        public string EffectName { get; set; }
        public ulong EffectId { get; set; }
        public ulong SecondEffectId { get; set; }
    }
    public class Value
    {
        public double DblValue;
        public double EffectiveDblValue;
        public double MitigatedDblValue;
        public string StrValue;
        public string DisplayValue { get; set; }
        public string ModifierDisplayValue { get; set; }
        public string ModifierType { get; set; }
        public DamageType ValueType { get; set; }
        public long ValueTypeId { get; set; }

        public Value Modifier;
        public bool WasCrit { get; set; }
    }
    public enum EffectType
    {
        Apply,
        Remove,
        Event,
        Spend,
        Restore,
        AreaEntered,
        DisciplineChanged,
        TargetChanged,
        ModifyCharges,
        AbsorbShield,
        Unknown,
        ModifyThreat
    }
    public enum DamageType
    {
        none,
        heal,
        kinetic,
        energy,
        intern,
        elemental,
        shield,
        miss,
        parry,
        deflect,
        dodge,
        immune,
        resist,
        cover,
        unknown,
        absorbed
    }
    public enum ValueType
    {
        Damage,
        Location,
        Resource
    }
}
