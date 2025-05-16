using System;
using SWTORCombatParser.DataStructures;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;


namespace SWTORCombatParser.ViewModels.Overlays.BossFrame
{
    public class HPModuleViewModel : INotifyPropertyChanged
    {
        private double bossCurrentHP;
        private double bossMaxHP;
        private string bossName;
        private double defaultHeight = 50;
        private double height;
        private string currentBossTarget;

        public double Height
        {
            get => height; set
            {
                height = value;
                OnPropertyChanged();
            }
        }
        public string BossName
        {
            get => bossName; set
            {
                bossName = value;
                OnPropertyChanged();
            }
        }
        public string CurrentBossTarget
        {
            get => currentBossTarget; set
            {
                currentBossTarget = value;
                OnPropertyChanged();
            }
        }
        public double BossMaxHP
        {
            get => bossMaxHP; set
            {
                bossMaxHP = value;
                OnPropertyChanged();
            }
        }
        public double BossCurrentHP
        {
            get => bossCurrentHP; set
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                    value = 0;
                bossCurrentHP = value;
                OnPropertyChanged();
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
                OnPropertyChanged("RemainderWidth");
                OnPropertyChanged("BarWidth");
                OnPropertyChanged("HPPercentText");
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
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
