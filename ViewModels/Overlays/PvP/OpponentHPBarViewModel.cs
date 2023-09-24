using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace SWTORCombatParser.ViewModels.Overlays.PvP
{
    public enum MenaceTypes
    {
        None,
        Healer,
        Dps
    }
    public class OpponentHPBarViewModel : INotifyPropertyChanged
    {
        private bool inRange;
        private GridLength remainderWidth;
        private GridLength barWidth;
        private bool isTargeted;
        private double _value;
        private MenaceTypes menace;
        private bool _isCurrentInfo;
        private string _playerName;

        public bool IsCurrentInfo
        {
            get => _isCurrentInfo;
            set
            {
                _isCurrentInfo = value;
                OnPropertyChanged();
            }
        }

        public bool InRange
        {
            get => inRange; set
            {
                inRange = value;
                OnPropertyChanged();
            }
        }
        public GridLength RemainderWidth
        {
            get => remainderWidth; set
            {
                remainderWidth = value;
                OnPropertyChanged();
            }
        }
        public GridLength BarWidth
        {
            get => barWidth; set
            {
                barWidth = value;
                OnPropertyChanged();
            }
        }
        public MenaceTypes Menace
        {
            get => menace; set
            {
                menace = value;
                OnPropertyChanged("IsMenace");
            }
        }
        public bool IsTargeted
        {
            get => isTargeted; set
            {
                isTargeted = value;
                OnPropertyChanged();
            }
        }
        public double Value
        {
            get => _value; set
            {
                _value = value;
                BarWidth = new GridLength(_value, GridUnitType.Star);
                RemainderWidth = new GridLength(1 - _value, GridUnitType.Star);
            }
        }

        public string PlayerName
        {
            get => _playerName;
            set => _playerName = value;
        }


        public OpponentHPBarViewModel(string playerName)
        {
            PlayerName = playerName;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
