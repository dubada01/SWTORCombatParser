using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SWTORCombatParser.ViewModels.Overlays.BossFrame
{
    public class HPModuleViewModel : INotifyPropertyChanged
    {
        private double bossCurrentHP;
        private double bossMaxHP;
        private string bossName;

        public string BossName
        {
            get => bossName; set
            {
                bossName = value;
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
                bossCurrentHP = value;
                OnPropertyChanged();
                var ratio = BossCurrentHP / BossMaxHP;
                BarWidth = new GridLength(ratio, GridUnitType.Star);
                RemainderWidth = new GridLength(1-ratio, GridUnitType.Star);
                OnPropertyChanged("RemainderWidth");
                OnPropertyChanged("BarWidth");
                OnPropertyChanged("HPPercentText");
            }
        }
        public string HPPercentText => ((BossCurrentHP / BossMaxHP)*100).ToString("N2") + "%";
        public GridLength RemainderWidth { get; set; }
        public GridLength BarWidth { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public HPModuleViewModel(EntityInfo sourceBossInfo)
        {
            NewBossStarted(sourceBossInfo.Entity.Name, sourceBossInfo.MaxHP);
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
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
