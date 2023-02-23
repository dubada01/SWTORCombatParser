using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using SWTORCombatParser.DataStructures;

namespace SWTORCombatParser.ViewModels.Overlays.BossFrame
{
    public class HPModuleViewModel : INotifyPropertyChanged
    {
        private double bossCurrentHP;
        private double bossMaxHP;
        private string bossName;
        private double defaultHeight = 50;
        private double height;

        public double Height { get => height; set
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
                RemainderWidth = new GridLength(1 - ratio, GridUnitType.Star);
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
