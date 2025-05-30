using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Overlays.PvP
{
    public enum MenaceTypes
    {
        None,
        Healer,
        Dps
    }
    public class OpponentHPBarViewModel : ReactiveObject
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
                this.RaiseAndSetIfChanged(ref _isCurrentInfo, value);
            }
        }

        public bool InRange
        {
            get => inRange; set
            {
                this.RaiseAndSetIfChanged(ref inRange, value);
            }
        }
        public GridLength RemainderWidth
        {
            get => remainderWidth; set
            {
                this.RaiseAndSetIfChanged(ref remainderWidth, value);
            }
        }
        public GridLength BarWidth
        {
            get => barWidth; set
            {
                this.RaiseAndSetIfChanged(ref barWidth, value);
            }
        }
        public MenaceTypes Menace
        {
            get => menace; set
            {
                this.RaiseAndSetIfChanged(ref menace, value);
            }
        }
        public bool IsTargeted
        {
            get => isTargeted; set
            {
                this.RaiseAndSetIfChanged(ref isTargeted, value);
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
    }
}
