using MoreLinq;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.ViewModels.BattleReview
{
    public class MapViewModel : INotifyPropertyChanged
    {
        private Combat _currentCombat;
        private DateTime _startTime;
        private Dictionary<Entity, System.Windows.Point> _currentCharacterLocations = new Dictionary<Entity, System.Windows.Point>();
        private string bossMapImagePath;
        private IDisposable _sliderUpdateSubscription;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public WpfPlot WPFPlot { get; set; }
        public string BossMapImagePath
        {
            get => bossMapImagePath; set
            {
                bossMapImagePath = value;
                OnPropertyChanged();
            }
        }
        public MapViewModel()
        {
            _sliderUpdateSubscription = Observable.FromEvent<double>(
                handler => ReviewSliderUpdates.OnSliderUpdated += handler,
                handler => ReviewSliderUpdates.OnSliderUpdated -= handler).Sample(TimeSpan.FromSeconds(0.1)).Subscribe(newPos => { UpdateEtitiesPositionAtTime(newPos); });
            WPFPlot = new WpfPlot();
            WPFPlot.Plot.Style(dataBackground: Color.FromArgb(50, 10, 10, 10), figureBackground: Color.FromArgb(0, 10, 10, 10));
            var xAxis = WPFPlot.Plot.XAxis;
            xAxis.IsVisible = false;
            var yAxis = WPFPlot.Plot.YAxis;
            yAxis.IsVisible = false;
            WPFPlot.Refresh();
        }
        public void SetCombat(Combat currentcombat)
        {
            _startTime = currentcombat.StartTime;
            _currentCombat = currentcombat;
            _currentCharacterLocations = new Dictionary<Entity, System.Windows.Point>();
            //BossMapImagePath = "../../resources/BossMaps/ZornToth.png";
        }
        private object lockObject = new object();
        public void UpdateEtitiesPositionAtTime(double time)
        {
            Task.Run(() => {
                if (_currentCombat == null)
                    return;
                foreach (var character in _currentCombat.CharacterParticipants)
                {
                    var closestLogToTime = _currentCombat.GetLogsInvolvingEntity(character).MinBy(l => Math.Abs((l.TimeStamp - _startTime).TotalSeconds - time)).First();
                    var position = closestLogToTime.Target == character ? closestLogToTime.TargetInfo.Position : closestLogToTime.SourceInfo.Position;
                   // Trace.WriteLine($"{character.Name} at {position.X},{position.Z} at {time}");
                    _currentCharacterLocations[character] = new System.Windows.Point(position.X, position.Z);
                }
                foreach (var target in _currentCombat.Targets)
                {
                    var closestLogToTime = _currentCombat.GetLogsInvolvingEntity(target).MinBy(l => Math.Abs((l.TimeStamp - _startTime).TotalSeconds - time)).First();
                    var position = closestLogToTime.Target == target ? closestLogToTime.TargetInfo.Position : closestLogToTime.SourceInfo.Position;
                    // Trace.WriteLine($"{character.Name} at {position.X},{position.Z} at {time}");
                    _currentCharacterLocations[target] = new System.Windows.Point(position.X, position.Z);
                }
                UpdatePlotWithToonPosition();
            });
        }
        private void UpdatePlotWithToonPosition()
        {
            lock (lockObject)
            {
                WPFPlot.Plot.Clear();
                foreach (var character in _currentCharacterLocations.Where(n=>!string.IsNullOrEmpty(n.Key.Name)))
                {
                    WPFPlot.Plot.AddText(character.Key.Name, character.Value.X, character.Value.Y);
                }
                App.Current.Dispatcher.Invoke(() => {
                    WPFPlot.Refresh();
                });
            }
        }
    }
}
