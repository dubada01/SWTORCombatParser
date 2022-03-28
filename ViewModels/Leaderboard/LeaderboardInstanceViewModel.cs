using SWTORCombatParser.Model.CloudRaiding;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels.Leaderboard
{
    public class LeaderboardEntry
    {
        public SolidColorBrush RowBackground { get; set; }
        public int Position { get; set; }
        public string Player { get; set; }
        public double Metric { get; set; }
        public string Discipline { get; set; }
        public string Duration { get; set; }
        public string CombatTime { get; set; }
    }
    public class LeaderboardInstanceViewModel:INotifyPropertyChanged
    {
        private LeaderboardEntryType _leaderboardType;

        public event PropertyChangedEventHandler PropertyChanged;
        public string MetricName { get; set; }

        public ObservableCollection<LeaderboardEntry> Leaders { get; set; } = new ObservableCollection<LeaderboardEntry>();
        public LeaderboardInstanceViewModel(LeaderboardEntryType type)
        {
            _leaderboardType = type;
            MetricName = type.ToString();
        }
        public async void Populate(string encounter, string boss, string difficulty, string players)
        {
            Leaders.Clear();
            var playersString = string.IsNullOrEmpty(players) ? "" : players +" ";
            var bossWithDifficulty = boss.Trim() + " " +$"{{{playersString}{difficulty}}}";
            var leaderboard = await PostgresConnection.GetEntriesForBossOfType(bossWithDifficulty, encounter, _leaderboardType);
            var orderedLeaders = leaderboard.OrderByDescending(l => l.Value).ToList();
            for(var i = 0; i<orderedLeaders.Count; i++)
            {
                var entry = orderedLeaders[i];
                var backgroundColor = Brushes.Transparent;
                if (i%2==0)
                {
                    backgroundColor= Brushes.DimGray;
                }
                    Leaders.Add(new LeaderboardEntry {Position = i+1,Player = entry.Character, Metric = entry.Value, Discipline = entry.Class, Duration = entry.Duration.ToString(),CombatTime = entry.TimeStamp.ToString(), RowBackground = backgroundColor });


            }
            OnPropertyChanged("Leaders");
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
