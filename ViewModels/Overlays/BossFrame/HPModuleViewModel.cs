using System;
using SWTORCombatParser.DataStructures;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using ReactiveUI;
using SWTORCombatParser.Utilities;


namespace SWTORCombatParser.ViewModels.Overlays.BossFrame
{
    public class HPModuleViewModel:ReactiveObject
    {
        private double bossCurrentHP;
        private double bossMaxHP;
        private string bossName;
        private double defaultHeight = 28;
        private double height;
        private string currentBossTarget;
        private bool _currentTargetIndicatorVisible;

        public double Height
        {
            get => height;
            set
            {
                this.RaiseAndSetIfChanged(ref height, value);
                this.RaisePropertyChanged(nameof(NameFontSize));
                this.RaisePropertyChanged(nameof(HealthFontSize));
                this.RaisePropertyChanged(nameof(TargetFontSize));
            }
        }
        public double NameFontSize => Height * 0.5;
        public double HealthFontSize => Height * 0.57;
        public double TargetFontSize => Height * 0.43;
        public string BossName
        {
            get => bossName; set => this.RaiseAndSetIfChanged(ref bossName, value);
        }

        public bool CurrentTargetIndicatorVisible
        {
            get => _currentTargetIndicatorVisible;
            set => this.RaiseAndSetIfChanged(ref _currentTargetIndicatorVisible, value);
        }

        public string CurrentBossTarget
        {
            get => currentBossTarget;
            set { this.RaiseAndSetIfChanged(ref currentBossTarget, value); this.RaisePropertyChanged(nameof(BossTargetText)); }
        }
        public string BossTargetText => string.IsNullOrWhiteSpace(CurrentBossTarget) ? "—" : CurrentBossTarget;
        public double BossMaxHP
        {
            get => bossMaxHP;
            set { this.RaiseAndSetIfChanged(ref bossMaxHP, double.IsFinite(value) ? Math.Max(0, value) : 0); RefreshHealth(); }
        }
        public double BossCurrentHP
        {
            get => bossCurrentHP; set
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                    value = 0;
                this.RaiseAndSetIfChanged(ref bossCurrentHP, Math.Max(0, value));
                RefreshHealth();
            }
        }
        private double HealthRatio => BossMaxHP > 0 ? Math.Clamp(BossCurrentHP / BossMaxHP, 0, 1) : 0;
        public string HPPercentText => BossMaxHP > 0 ? (HealthRatio * 100).ToString("0.0") + "%" : "—";
        public string CompactHealth => BossCurrentHP >= 1_000_000 ? (BossCurrentHP / 1_000_000).ToString("0.0") + "m"
            : BossCurrentHP >= 1_000 ? (BossCurrentHP / 1_000).ToString("0.0") + "k" : BossCurrentHP.ToString("N0");
        public string DetailedHealth => $"{BossCurrentHP:N0} / {BossMaxHP:N0} HP";
        private void RefreshHealth()
        {
            BarWidth = new GridLength(HealthRatio, GridUnitType.Star);
            RemainderWidth = new GridLength(1 - HealthRatio, GridUnitType.Star);
            this.RaisePropertyChanged(nameof(HPPercentText));
            this.RaisePropertyChanged(nameof(CompactHealth));
            this.RaisePropertyChanged(nameof(DetailedHealth));
            this.RaisePropertyChanged(nameof(BarWidth));
            this.RaisePropertyChanged(nameof(RemainderWidth));
        }
        public GridLength RemainderWidth { get; set; }
        public GridLength BarWidth { get; set; }

        public HPModuleViewModel(EntityInfo sourceBossInfo, bool isDuplicate, double scale)
        {
            UpdateScale(scale);
            var bossName = isDuplicate ? sourceBossInfo.Entity.Name + " (B)" : sourceBossInfo.Entity.Name;
            NewBossStarted(bossName, sourceBossInfo.MaxHP);
            Settings.SettingsUpdated += CheckForBossTagetEnabled;
            CurrentTargetIndicatorVisible = Settings.ReadSettingOfType<bool>(Settings.BossFrameTargetSetting);
        }

        private void CheckForBossTagetEnabled(string settingName)
        {
            if (settingName != Settings.BossFrameTargetSetting)
                return;
            CurrentTargetIndicatorVisible = Settings.ReadSettingOfType<bool>(Settings.BossFrameTargetSetting);
        }

        public void NewBossStarted(string bossName, double maxHP)
        {
            BossName = bossName;
            BossMaxHP = maxHP;
            BossCurrentHP = maxHP;
        }
        public void UpdateHP(double newHP)
        {
            BossCurrentHP = newHP;
        }

        public void UpdateTarget(string newTarget)
        {
            if (CurrentBossTarget != newTarget)
                CurrentBossTarget = newTarget;
        }
        public void UpdateScale(double scale)
        {
            Height = defaultHeight * scale;
        }
    }
}
