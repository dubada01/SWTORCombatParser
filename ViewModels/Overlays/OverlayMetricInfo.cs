using SWTORCombatParser.Model.Overlays;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using SWTORCombatParser.DataStructures;
using System;
using System.Collections.Generic;

namespace SWTORCombatParser.ViewModels.Overlays
{
    public class OverlayMetricInfo : INotifyPropertyChanged
    {
        private double relativeLength;
        private double _value;
        private double _secondaryValue;
        private int leaderboardRank;
        private double defaultBarHeight = 35;
        private double defaultFontSize = 18;
        private double defaultValueWidth = 70;

        private string valueStringFormat = "#,##0";
        private double sizeScalar=1;

        public string MedalIconPath { get; set; } = "../../resources/redX.png";
        public string InfoText => $"{Type}: {(int)Value}" + (SecondaryType != OverlayType.None ? $"\n{SecondaryType}: {(int)SecondaryValue}" : "");
        public double SizeScalar
        {
            get => sizeScalar; set
            {
                sizeScalar = value;
                OnPropertyChanged("FontSize");
                OnPropertyChanged("InfoFontSize");
                OnPropertyChanged("RankFontSize");
                OnPropertyChanged("BarHeight");
                OnPropertyChanged("ValueWidth");
            }
        }
        public GridLength ValueWidth => new GridLength(defaultValueWidth*SizeScalar, GridUnitType.Pixel);
        public double FontSize => defaultFontSize * SizeScalar;
        public double InfoFontSize => FontSize - 2;
        public double RankFontSize => InfoFontSize - 5;
        public double BarHeight => defaultBarHeight * SizeScalar;
        public GridLength RemainderWidth { get; set; }
        public GridLength BarWidth { get; set; }
        public GridLength SecondaryBarWidth { get; set; }
        public double BorderThickness => IsLeaderboardValue ? 3 : 0;
        public CornerRadius BarRadius { get; set; } = new CornerRadius(3, 3, 3, 3);
        public CornerRadius BarRadiusSecondary { get; set; } = new CornerRadius(3, 3, 3, 3);
        public SolidColorBrush BarOutline => IsLeaderboardValue ? Brushes.WhiteSmoke : Brushes.Transparent;
        public bool AddSecondayToValue { get; set; }
        public Entity Player { get; set; }
        public bool RankIsPersonalRecord { get; set; }
        public string LeaderboardRank
        {
            get => leaderboardRank == 0 ? "" : leaderboardRank.ToString();
            set
            {
                leaderboardRank = int.Parse(value);
                OnPropertyChanged();
            }
        }
        public TextDecorationCollection RankDecoration => RankIsPersonalRecord ? new TextDecorationCollection(new List<TextDecoration> { new TextDecoration { Location = TextDecorationLocation.Underline } }) : new TextDecorationCollection();
        public string PlayerName => Player.Name;
        public bool IsLeaderboardValue { get; set; } = false;


        public double RelativeLength
        {
            get => double.IsNaN(relativeLength) ? 0 : relativeLength;
            set
            {
                if (Type == OverlayType.HealReactionTime)
                    valueStringFormat = "#,##0.##";
                if (Type == OverlayType.HealReactionTimeRatio)
                    valueStringFormat = "0.###";

                if (double.IsNaN(relativeLength) || double.IsInfinity(relativeLength) || Value + SecondaryValue <= 0 || TotalValue == "0" || TotalValue.Contains('-'))
                {
                    SetBarToZero();
                    return;
                }
                relativeLength = value;
                if (SecondaryType != OverlayType.None)
                {
                    var primaryFraction = Value / double.Parse(TotalValue, NumberStyles.AllowThousands | NumberStyles.Float, CultureInfo.InvariantCulture);
                    var secondaryFraction = SecondaryValue / double.Parse(TotalValue, NumberStyles.AllowThousands | NumberStyles.Float, CultureInfo.InvariantCulture);
                    BarWidth = new GridLength(relativeLength * primaryFraction, GridUnitType.Star);
                    SecondaryBarWidth = new GridLength(relativeLength * secondaryFraction, GridUnitType.Star);
                    RemainderWidth = new GridLength(1 - relativeLength, GridUnitType.Star);
                }
                else
                {
                    BarWidth = new GridLength(relativeLength, GridUnitType.Star);
                    SecondaryBarWidth = new GridLength(0, GridUnitType.Star);
                    RemainderWidth = new GridLength(1 - relativeLength, GridUnitType.Star);
                }
                OnPropertyChanged("RemainderWidth");
                OnPropertyChanged("BarWidth");
                BarRadiusSecondary = SecondaryType == OverlayType.None || SecondaryValue == 0 ? new CornerRadius(3, 3, 3, 3) : new CornerRadius(3, 0, 0, 3);
                BarRadius = SecondaryType == OverlayType.None ? new CornerRadius(3, 3, 3, 3) : new CornerRadius(0, 3, 3, 0);
                OnPropertyChanged("BarRadius");
                OnPropertyChanged("BarRadiusSecondary");
            }
        }
        private void SetBarToZero()
        {
            relativeLength = 0;
            BarWidth = new GridLength(0, GridUnitType.Star);
            RemainderWidth = new GridLength(1, GridUnitType.Star);
            OnPropertyChanged("RemainderWidth");
            OnPropertyChanged("BarWidth");
        }
        public double Value
        {
            get => double.IsNaN(_value) ? 0 : _value; set
            {
                _value = value;
                defaultValueWidth = 70;
                if (_value > 100000)
                    defaultValueWidth = 80;
                if (value > 1000000)
                    defaultValueWidth = 90;
                if (value > 10000000)
                    defaultValueWidth = 100;
                OnPropertyChanged();
            }
        }
        public double SecondaryValue
        {
            get => double.IsNaN(_secondaryValue) ? 0:_secondaryValue; set
            {
                _secondaryValue = value;
                OnPropertyChanged();
            }
        }
        public string TotalValue => Math.Max(0,Value + Math.Max(0,AddSecondayToValue ? SecondaryValue : SecondaryValue * -1)).ToString(valueStringFormat, CultureInfo.InvariantCulture);
        public void Reset()
        {
            Value = 0;
            SecondaryValue = 0;
            RelativeLength = 0;
        }
        public OverlayType Type { get; set; }
        public OverlayType SecondaryType { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
