using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Notes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using ReactiveUI;
using SWTORCombatParser.Views.Overlay.Notes;

namespace SWTORCombatParser.ViewModels.Overlays.Notes
{
    public class RaidNotesViewModel : BaseOverlayViewModel
    {
        private string raidNote = string.Empty;
        private Timer _saveTimer;

        private bool _notesDirty;
        private bool _imagesDirty;

        public event Action<bool> OnInInstanceChanged = delegate { };
        public event Action OnClosing = delegate { };
        public override bool ShouldBeVisible => InInstance;

        private string selectedRaid = string.Empty;
        private bool _inInstance = false;

        public List<string> AvailableRaids { get; internal set; } = new();
        public Dictionary<string, string> RaidNotes { get; internal set; } = new();
        public Dictionary<string, List<RaidNoteImage>> RaidImages { get; internal set; } = new();
        public string Test { get; set; }
        public string SelectedRaid
        {
            get => selectedRaid;
            set
            {
                this.RaiseAndSetIfChanged(ref selectedRaid, value);
                EnsureRaidEntryExists(selectedRaid);
                UpdateNotes();
            }
        }

        public string RaidNote
        {
            get => raidNote;
            set
            {
                this.RaiseAndSetIfChanged(ref raidNote, value);
                EnsureRaidEntryExists(SelectedRaid);
                RaidNotes[SelectedRaid] = raidNote;
                _notesDirty = true;
            }
        }
        public bool IsEnabled { get; set; }
        public bool InInstance
        {
            get => _inInstance;
            set
            {
                _inInstance = value;
                UpdateVisibility();
            }
        }

        public RaidNotesViewModel(string overlayName) : base(overlayName)
        {
            RaidNotesReader.Init();

            MainContent = new RaidNotesView(this);

            CombatLogStateBuilder.AreaEntered += CheckAreaForRaid;
            CombatLogStreamer.HistoricalLogsFinished += CheckForRaidAfterParseStart;

            var raids = EncounterLoader.SupportedEncounters.Where(e => e.EncounterType == EncounterType.Operation).Select(r => r.Name);
            var lair = EncounterLoader.SupportedEncounters.Where(e => e.EncounterType == EncounterType.Lair).Select(r => r.Name);
            var flashpoints = EncounterLoader.SupportedEncounters.Where(e => e.EncounterType == EncounterType.Flashpoint).Select(r => r.Name).Order();

            AvailableRaids.AddRange(raids);
            AvailableRaids.AddRange(lair);
            AvailableRaids.AddRange(flashpoints);

            RaidNotes = RaidNotesReader.GetAllRaidNotes();
            RaidImages = RaidNotesReader.GetAllRaidImages();

            SelectedRaid = AvailableRaids.FirstOrDefault() ?? "";
            EnsureRaidEntryExists(SelectedRaid);
            UpdateNotes();

            // Start immediately
            _saveTimer = new Timer
            {
                Interval = 2000,
                AutoReset = true,
                Enabled = true
            };
            _saveTimer.Elapsed += (_, __) =>
            {
                if (_notesDirty)
                {
                    _notesDirty = false;
                    RaidNotesReader.SetNotes(RaidNotes);
                }

                if (_imagesDirty)
                {
                    _imagesDirty = false;
                    RaidNotesReader.SetImages(RaidImages);
                }
            };
            _saveTimer.Start();
        }

        private void CheckForRaidAfterParseStart(DateTime time, bool arg2)
        {
            CheckAreaForRaid(CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(time));
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

        private void EnsureRaidEntryExists(string raid)
        {
            if (string.IsNullOrWhiteSpace(raid))
                return;

            if (!RaidNotes.ContainsKey(raid))
                RaidNotes[raid] = "";

            if (!RaidImages.ContainsKey(raid))
                RaidImages[raid] = new List<RaidNoteImage>();
        }

        private void UpdateNotes()
        {
            EnsureRaidEntryExists(SelectedRaid);
            raidNote = RaidNotes[SelectedRaid] ?? "";
            this.RaisePropertyChanged(nameof(RaidNote));
        }

        // --- image API for the view ---
        public IReadOnlyList<RaidNoteImage> GetImagesForSelectedRaid()
        {
            EnsureRaidEntryExists(SelectedRaid);
            return RaidImages[SelectedRaid];
        }

        public void UpsertImageForSelectedRaid(RaidNoteImage img)
        {
            EnsureRaidEntryExists(SelectedRaid);

            var list = RaidImages[SelectedRaid];
            var idx = list.FindIndex(x => x.Id == img.Id);
            if (idx >= 0) list[idx] = img;
            else list.Add(img);

            _imagesDirty = true;
        }

        public void RemoveImageForSelectedRaid(Guid id)
        {
            EnsureRaidEntryExists(SelectedRaid);
            var list = RaidImages[SelectedRaid];
            list.RemoveAll(x => x.Id == id);
            _imagesDirty = true;
        }

        internal void OverlayDisabled()
        {
            OnClosing();
            _saveTimer?.Stop();

            // final flush
            if (_notesDirty) RaidNotesReader.SetNotes(RaidNotes);
            if (_imagesDirty) RaidNotesReader.SetImages(RaidImages);

            _notesDirty = false;
            _imagesDirty = false;
        }

        public void LockOverlays() => OverlaysMoveable = false;
        public void UnlockOverlays() => OverlaysMoveable = true;
    }
}
