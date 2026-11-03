using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser.ViewModels.Overlays
{
    public class OverlayMetricInfo : INotifyPropertyChanged
    {
        private double relativeLength;
        private double _value;
        private double _secondaryValue;
        private int leaderboardRank;
        private double defaultBarHeight = 25;
        private double defaultFontSize = 13;
        private double defaultValueWidth = 60;

        private string valueStringFormat = "#,##0";
        private double sizeScalar = 1;
        public Bitmap ClassIcon { get; set; }
        public Bitmap MedalIconPath { get; set; } = new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/redX.png")));
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
                OnPropertyChanged("BorderThickness");
                OnPropertyChanged("TotalFontSize");
                OnPropertyChanged("LeaderboardSeperationDistance");
            }
        }
        public GridLength ValueWidth => new GridLength(defaultValueWidth * Math.Sqrt(SizeScalar), GridUnitType.Pixel);
        public double FontSize => Math.Max(7, defaultFontSize * SizeScalar);
        public double InfoFontSize => Math.Max(6.5, FontSize * 0.9);
        public double RankFontSize => Math.Max(6, InfoFontSize * 0.6);
        public double BarHeight => defaultBarHeight * SizeScalar;
        public GridLength RemainderWidth { get; set; }
        public GridLength BarWidth { get; set; }
        public GridLength SecondaryBarWidth { get; set; }
        public Thickness BorderThickness => IsLeaderboardValue ? new Thickness(1.25 * SizeScalar) : new Thickness(0);
        public CornerRadius BarRadius { get; set; } = new CornerRadius(3, 3, 3, 3);
        public CornerRadius BarRadiusSecondary { get; set; } = new CornerRadius(3, 3, 3, 3);
        public SolidColorBrush BarOutline => IsLeaderboardValue ? new SolidColorBrush(Brushes.WhiteSmoke.Color) : new SolidColorBrush(Brushes.Transparent.Color);
        public bool AddSecondayToValue { get; set; }
        public bool FlipSecondaryAndPrimaryBars { get; set; }
        public Entity Player { get; set; }
        public bool RankIsPersonalRecord { get; set; }
        public double LeaderboardSeperationDistance => (SizeScalar * 2);
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
        public FontWeight PlayerTextWeight => Player.IsLocalPlayer ? FontWeight.DemiBold : FontWeight.Normal;
        public TextDecorationCollection PlayerTextDecorations => Player.IsLocalPlayer ? new TextDecorationCollection(new List<TextDecoration> { new TextDecoration { Location = TextDecorationLocation.Underline } }) : new TextDecorationCollection();
        public bool IsLeaderboardValue { get; set; } = false;

        public OverlayMetricInfo()
        {
            MetricColorLoader.OnOverlayTypeColorUpdated += UpdateColor;
        }
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
                    if (!FlipSecondaryAndPrimaryBars)
                    {
                        BarWidth = new GridLength(relativeLength * primaryFraction, GridUnitType.Star);
                        SecondaryBarWidth = new GridLength(relativeLength * secondaryFraction, GridUnitType.Star);
                        RemainderWidth = new GridLength(1 - relativeLength, GridUnitType.Star);
                    }
                    else
                    {
                        var cachedPrimary = Type;
                        Type = SecondaryType;
                        SecondaryType = cachedPrimary;

                        BarWidth = new GridLength(relativeLength * secondaryFraction, GridUnitType.Star);
                        SecondaryBarWidth = new GridLength(relativeLength * primaryFraction, GridUnitType.Star);
                        RemainderWidth = new GridLength(1 - relativeLength, GridUnitType.Star);
                    }
                }
                else
                {
                    BarWidth = new GridLength(relativeLength, GridUnitType.Star);
                    SecondaryBarWidth = new GridLength(0, GridUnitType.Star);
                    RemainderWidth = new GridLength(1 - relativeLength, GridUnitType.Star);
                }
                OnPropertyChanged("SecondaryBarWidth");
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
            SecondaryBarWidth = new GridLength(0, GridUnitType.Star);
            OnPropertyChanged("RemainderWidth");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("SecondaryBarWidth");
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
            get => double.IsNaN(_secondaryValue) ? 0 : _secondaryValue; set
            {
                _secondaryValue = value;
                OnPropertyChanged();
            }
        }
        public string TotalValue => TrueValue.ToString(valueStringFormat, CultureInfo.InvariantCulture);
        public double TrueValue => GetTotalValue();
        public double OrderingValue => Value + SecondaryValue;
        private double GetTotalValue()
        {
            var total = Value;
            if (AddSecondayToValue)
            {
                total = Math.Max(0, Value + SecondaryValue);
            }
            return total;
        }

        public void Reset()
        {
            Value = 0;
            SecondaryValue = 0;
            RelativeLength = 0;
        }
        private void UpdateColor(OverlayType type)
        {
            if (type == Type)
                OnPropertyChanged("Type");
            if (type == SecondaryType)
                OnPropertyChanged("SecondaryType");
            OnPropertyChanged("DataContext");
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
