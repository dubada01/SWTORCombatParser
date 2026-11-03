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
        private double defaultHeight = 50;
        private double height;
        private string currentBossTarget;
        private bool _currentTargetIndicatorVisible;

        public double Height
        {
            get => height; set => this.RaiseAndSetIfChanged(ref height, value);
        }
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
            get => currentBossTarget; set => this.RaiseAndSetIfChanged(ref currentBossTarget, value);
        }
        public double BossMaxHP
        {
            get => bossMaxHP; set => this.RaiseAndSetIfChanged(ref bossMaxHP, value);
        }
        public double BossCurrentHP
        {
            get => bossCurrentHP; set
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                    value = 0;
                this.RaiseAndSetIfChanged(ref bossCurrentHP, value);
                var ratio = BossMaxHP <= 0 ? 0 : bossCurrentHP / BossMaxHP;
                if (double.IsNaN(ratio) || double.IsInfinity(ratio) || ratio < 0 || ratio > 1)
                    ratio = Math.Clamp(ratio, 0, 1); // or just ratio = 0;

                try
                {
                    BarWidth = new GridLength(ratio, GridUnitType.Star);
                    RemainderWidth = new GridLength(1 - ratio, GridUnitType.Star);
                }
                catch
                {
                    BarWidth = new GridLength(0, GridUnitType.Star);
                    RemainderWidth = new GridLength(1, GridUnitType.Star);
                }
                this.RaisePropertyChanged(nameof(HPPercentText));
                this.RaisePropertyChanged(nameof(BarWidth));
                this.RaisePropertyChanged(nameof(RemainderWidth));
            }
        }
        public string HPPercentText => ((BossCurrentHP / BossMaxHP) * 100).ToString("N2") + "%";
        public GridLength RemainderWidth { get; set; }
        public GridLength BarWidth { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
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
