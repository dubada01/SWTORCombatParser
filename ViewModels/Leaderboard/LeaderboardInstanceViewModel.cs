using System.Collections.Generic;
using SWTORCombatParser.Model.CloudRaiding;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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

        public List<LeaderboardEntry> Leaders { get; set; } = new List<LeaderboardEntry>();
        public LeaderboardInstanceViewModel(LeaderboardEntryType type)
        {
            _leaderboardType = type;
            MetricName = type.ToString();
        }
        public async void Populate(string encounter, string boss, string difficulty, string players, bool isParsing = false, long parsingHP = 0)
        {
            var newLeaders = new List<LeaderboardEntry>();
            var isFlashpoint = string.IsNullOrEmpty(players);
            var playersString = isFlashpoint ? "4 " : players +" ";
            playersString = isParsing ? "" : playersString;
            var extraBossInfo = $"{{{playersString}{difficulty}}}";
            extraBossInfo = isParsing ? $"{{{parsingHP}HP }}" : extraBossInfo;
            var bossWithDifficulty = boss.Trim() + " " + extraBossInfo;
            var leaderboard = await PostgresConnection.GetEntriesForBossOfType(bossWithDifficulty, encounter, _leaderboardType);
            if(isFlashpoint)
            {
                var oldFlashpointBossInfo = boss.Trim() + " " + $"{{{difficulty}}}";
                var oldFlashpointBoard = await PostgresConnection.GetEntriesForBossOfType(oldFlashpointBossInfo, encounter, _leaderboardType);
                leaderboard.AddRange(oldFlashpointBoard);
            }
            
            
            var orderedLeaders = leaderboard.OrderByDescending(l => l.Value).ToList();
            for(var i = 0; i<orderedLeaders.Count; i++)
            {
                var entry = orderedLeaders[i];
                var backgroundColor = Brushes.Transparent;
                if (i%2==0)
                {
                    backgroundColor= Brushes.DimGray;
                }
                newLeaders.Add(new LeaderboardEntry {Position = i+1,Player = entry.Character, Metric = entry.Value, Discipline = entry.Class, Duration = entry.Duration.ToString(),CombatTime = entry.TimeStamp.ToString(), RowBackground = backgroundColor });


            }

            Leaders = newLeaders;
            OnPropertyChanged("Leaders");
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
