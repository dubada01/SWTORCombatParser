using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Notes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace SWTORCombatParser.ViewModels.Overlays.Notes
{
    public class RaidNotesViewModel:INotifyPropertyChanged
    {
        private string raidNote = string.Empty;
        private Dictionary<string, string> _savedRaidNotes = new Dictionary<string, string>();
        private Timer _uploadTimer;

        public event Action<bool> OnInInstanceChanged = delegate { };
        public event Action OnClosing = delegate { };
        public event Action OnHiding = delegate { };
        public event Action<bool> OnLocking  = delegate { };
        internal event Action OnShowing = delegate { };
        internal event Action CloseRequested = delegate { };
        private string selectedRaid = string.Empty;
        private bool isEnabled;

        public List<string> AvailableRaids { get; internal set; } = new List<string>();
        public string SelectedRaid
        {
            get => selectedRaid; set
            {
                selectedRaid = value;
                UpdateNotes();
                OnPropertyChanged();
            }
        }
        public bool OverlaysMoveable { get; internal set; }
        public Dictionary<string, string> RaidNotes { get; internal set; } = new Dictionary<string, string>();

        public string RaidNote
        {
            get => raidNote; set
            {
                raidNote = value;
                RaidNotes[SelectedRaid] = raidNote;
                OnPropertyChanged();
            }
        }
        public bool InInstance { get; set; } = false;
        public bool IsEnabled
        {
            get => isEnabled; internal set
            {
                isEnabled = value;
                if (isEnabled)
                    StartUploadTimer();
            }
        }

        public RaidNotesViewModel()
        {
            CombatLogStateBuilder.AreaEntered += CheckAreaForRaid;
            CombatLogStreamer.HistoricalLogsFinished += CheckForRaidAfterParseStart;
            var raids = EncounterLoader.SupportedEncounters.Where(e => e.EncounterType == EncounterType.Operation).Select(r => r.Name);
            var lair = EncounterLoader.SupportedEncounters.Where(e => e.EncounterType == EncounterType.Lair).Select(r => r.Name);
            var flashpoints = EncounterLoader.SupportedEncounters.Where(e => e.EncounterType == EncounterType.Flashpoint).Select(r => r.Name).Order();
            AvailableRaids.AddRange(raids);
            AvailableRaids.AddRange(lair);
            AvailableRaids.AddRange(flashpoints);
            RaidNotes = RaidNotesReader.GetAllRaidNotes();
            SelectedRaid = AvailableRaids.First();
            _savedRaidNotes = RaidNotes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }


        private void CheckForRaidAfterParseStart(DateTime time, bool arg2)
        {
            CheckAreaForRaid(CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(time));
        }

        public void StartUploadTimer()
        {
            _uploadTimer = new Timer();
            _uploadTimer.Interval = 5000;
            _uploadTimer.Elapsed += TrySaveRaidNotes;
            _uploadTimer.AutoReset = true;
            _uploadTimer.Enabled = true;
            _uploadTimer.Start();
        }

        private void CheckAreaForRaid(EncounterInfo info)
        {
            if (AvailableRaids.Contains(info.Name))
            {
                OnInInstanceChanged(true);
                SelectedRaid = info.Name;
                InInstance = true;
            }
            else
            {
                InInstance = false;
                OnInInstanceChanged(false);
            }
        }
        private void UpdateNotes()
        {
            if(!RaidNotes.ContainsKey(SelectedRaid))
            {
                RaidNotes[SelectedRaid] = "";
            }
            raidNote = RaidNotes[SelectedRaid];
            OnPropertyChanged("RaidNote");
        }

        private void TrySaveRaidNotes(object sender, ElapsedEventArgs e)
        {
            if(!AreDictionariesEqual(_savedRaidNotes, RaidNotes))
            {
                RaidNotesReader.SetNotes(RaidNotes);
                _savedRaidNotes = RaidNotes.ToDictionary(kvp=>kvp.Key, kvp=>kvp.Value);
            }
        }
        internal void OverlayDisabled()
        {
            OnClosing();
            _uploadTimer?.Stop();
        }
        public static bool AreDictionariesEqual(Dictionary<string, string> dict1, Dictionary<string, string> dict2)
        {
            // Check if both dictionaries are null or the same instance
            if (ReferenceEquals(dict1, dict2))
                return true;

            // Check if either dictionary is null (but not both)
            if (dict1 == null || dict2 == null)
                return false;

            // Check if dictionaries have the same count
            if (dict1.Count != dict2.Count)
                return false;

            // Check if dictionaries have the same keys and values
            foreach (var kvp in dict1)
            {
                if (!dict2.TryGetValue(kvp.Key, out string value) || kvp.Value != value)
                {
                    return false;
                }
            }

            return true;
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void LockOverlays()
        {
            OnLocking(true);
            OverlaysMoveable = false;
            OnPropertyChanged("OverlaysMoveable");
        }
        public void UnlockOverlays()
        {
            OnLocking(false);
            OverlaysMoveable = true;
            OnPropertyChanged("OverlaysMoveable");
        }
    }
}
