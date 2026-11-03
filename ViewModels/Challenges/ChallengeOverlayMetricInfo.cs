using SWTORCombatParser.DataStructures;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SWTORCombatParser.ViewModels.Challenges
{
    public class ChallengeOverlayMetricInfo : INotifyPropertyChanged
    {
        private double relativeLength;
        private double _value;
        private static double defaultBarHeight = 35;
        private static double defaultFontSize = 18;
        private static double defaultValueWidth = 70;

        private string valueStringFormat = "#,##0";
        private double sizeScalar = 1;
        private SolidColorBrush challengeColor;

        public string InfoText => $"{Type}: {(int)Value}";
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
        public SolidColorBrush ChallengeColor
        {
            get => challengeColor; set
            {
                challengeColor = value;
                OnPropertyChanged();
            }
        }
        public ChallengeOverlayMetricInfo(SolidColorBrush background)
        {
            ChallengeColor = background;
        }
        public GridLength ValueWidth => new GridLength(defaultValueWidth * SizeScalar, GridUnitType.Pixel);
        public double FontSize => defaultFontSize * SizeScalar;
        public double InfoFontSize => FontSize - 2;
        public double RankFontSize => InfoFontSize - 5;
        public double BarHeight => defaultBarHeight * SizeScalar;
        public GridLength RemainderWidth { get; set; }
        public GridLength BarWidth { get; set; }
        public Thickness BorderThickness => new Thickness(0d);
        public CornerRadius BarRadius { get; set; } = new CornerRadius(3, 3, 3, 3);
        public SolidColorBrush BarOutline => new SolidColorBrush(Brushes.Transparent.Color);
        public Entity Player { get; set; }
        public string PlayerName => Player.Name;

        public double RelativeLength
        {
            get => double.IsNaN(relativeLength) ? 0 : relativeLength;
            set
            {
                if (double.IsNaN(relativeLength) || double.IsInfinity(relativeLength) || Value == 0 || TotalValue == "0" || TotalValue.Contains('-'))
                {
                    SetBarToZero();
                    return;
                }
                relativeLength = value;

                BarWidth = new GridLength(relativeLength, GridUnitType.Star);
                RemainderWidth = new GridLength(1 - relativeLength, GridUnitType.Star);

                OnPropertyChanged("RemainderWidth");
                OnPropertyChanged("BarWidth");
                BarRadius = new CornerRadius(3, 3, 3, 3);
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
            get => _value; set
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
        public string TotalValue => Math.Max(0, Value).ToString(valueStringFormat, CultureInfo.InvariantCulture);
        public void Reset()
        {
            Value = 0;
            RelativeLength = 0;
        }
        public ChallengeType Type { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
