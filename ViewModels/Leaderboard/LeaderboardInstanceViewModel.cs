using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Utilities;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Avalonia.Controls;

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
    public class LeaderboardInstanceViewModel : INotifyPropertyChanged
    {
        private LeaderboardEntryType _leaderboardType;
        private bool _showLoadingSplash = false;
        private LoadingSplash splash;

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
            if (_showLoadingSplash)
            {
                splash = LoadingWindowFactory.ShowInstancedLoading();
            }
            if (!_showLoadingSplash)
            {
                _showLoadingSplash = true;
            }
            var newLeaders = new List<LeaderboardEntry>();
            var isFlashpoint = string.IsNullOrEmpty(players);
            var playersString = isFlashpoint ? "4 " : players + " ";
            playersString = isParsing ? "" : playersString;
            var extraBossInfo = $"{{{playersString}{difficulty}}}";
            extraBossInfo = isParsing ? $"{{{parsingHP}HP }}" : extraBossInfo;
            var bossWithDifficulty = boss.Trim() + " " + extraBossInfo;
            if(encounter == "Open World")
            {
                encounter =  await API_Connection.GetEncounterForBossName(bossWithDifficulty);
            }
            var leaderboard = await API_Connection.GetEntriesForBossOfType(bossWithDifficulty, encounter, _leaderboardType);
            if (isFlashpoint)
            {
                var oldFlashpointBossInfo = boss.Trim() + " " + $"{{{difficulty}}}";
                var oldFlashpointBoard = await API_Connection.GetEntriesForBossOfType(oldFlashpointBossInfo, encounter, _leaderboardType);
                leaderboard.AddRange(oldFlashpointBoard);
            }


            var orderedLeaders = leaderboard.OrderByDescending(l => l.Value).ToList();
            for (var i = 0; i < orderedLeaders.Count; i++)
            {
                var entry = orderedLeaders[i];
                var backgroundColor = Brushes.Transparent;
                if (i % 2 == 0)
                {
                    backgroundColor = (SolidColorBrush)App.Current.FindResource("Gray3Brush");
                }
                newLeaders.Add(new LeaderboardEntry { Position = i + 1, Player = entry.Character, Metric = entry.Value, Discipline = entry.Class, Duration = entry.Duration.ToString(), CombatTime = entry.TimeStamp.ToString(), RowBackground = backgroundColor });


            }

            Leaders = newLeaders;
            OnPropertyChanged("Leaders");
            if (splash != null)
            {
                LoadingWindowFactory.HideInstancedLoading(splash);
                splash = null;
            }
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
