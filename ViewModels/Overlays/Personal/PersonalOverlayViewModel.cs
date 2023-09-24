using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Views.Overlay.Personal;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SWTORCombatParser.ViewModels.Overlays.Personal
{
    public class PersonalOverlayViewModel : INotifyPropertyChanged
    {
        private bool active;
        private PersonalOverlayWindow _window;
        private bool overlaysMoveable;
        private double rows;
        private double _currentScale;

        public event Action<OverlayInstanceViewModel> OverlayClosed = delegate { };
        public event Action CloseRequested = delegate { };
        public event Action<bool> OnLocking = delegate { };
        public event Action OnHiding = delegate { };
        public event Action OnShowing = delegate { };
        public event Action<bool> ActiveChanged = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;
        private string _currentOwner;
        public PersonalOverlayViewModel(double currentScale)
        {
            _currentScale = currentScale;
            _window = new PersonalOverlayWindow(this);
            var defaults = DefaultGlobalOverlays.GetOverlayInfoForType("Personal");
            Active = defaults.Acive;
            _window.Top = defaults.Position.Y;
            _window.Left = defaults.Position.X;
            _window.Width = defaults.WidtHHeight.X;
            _window.Height = defaults.WidtHHeight.Y;

            DefaultPersonalOverlaysManager.NewDefaultSelected += UpdateMetrics;
            CombatLogStreamer.NewLineStreamed += CheckForConversation;
            UpdateMetrics("Damage");
        }
        private bool _conversationActive;
        private void CheckForConversation(ParsedLogEntry obj)
        {
            if (!obj.Source.IsLocalPlayer)
                return;
            if (obj.Effect.EffectId == _7_0LogParsing.InConversationEffectId && obj.Effect.EffectType == EffectType.Apply && Active)
            {
                _conversationActive = true;
                OnHiding();
            }

            if (obj.Effect.EffectId == _7_0LogParsing.InConversationEffectId && obj.Effect.EffectType == EffectType.Remove && Active)
            {
                _conversationActive = false;
                OnShowing();
            }
        }
        private void UpdateMetrics(string defaultName)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                PersonalOverlayInstances.Clear();
                _currentOwner = defaultName;
                var dpsSettings = DefaultPersonalOverlaysManager.GetSettingsForOwner(defaultName);
                var intitialCells = dpsSettings.CellInfos.Select(m => new PersonalOverlayInstanceViewModel(OverlaysMoveable, _currentScale, m));
                foreach (var cell in intitialCells)
                {
                    cell.CellRemoved += RemoveCell;
                    cell.CellUpdated += UpdateDefaults;
                    cell.CellChangedFromNone += AddNewBlank;
                    PersonalOverlayInstances.Add(cell);
                }
                AddNewBlank();
            });
        }

        private void AddNewBlank()
        {
            var initialCell = new PersonalOverlayInstanceViewModel(OverlaysMoveable, _currentScale);
            initialCell.CellRemoved += RemoveCell;
            initialCell.CellUpdated += UpdateDefaults;
            initialCell.CellChangedFromNone += AddNewBlank;
            PersonalOverlayInstances.Add(initialCell);
            if (OverlaysMoveable)
                Rows = (int)Math.Ceiling(PersonalOverlayInstances.Count / 2d);
            else
                Rows = (int)Math.Floor(PersonalOverlayInstances.Count / 2d);
        }

        private void RemoveCell(PersonalOverlayInstanceViewModel obj)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                PersonalOverlayInstances.Remove(obj);
                UpdateDefaults();
                Rows = (int)Math.Ceiling(PersonalOverlayInstances.Count / 2d);
            });
        }
        private void UpdateDefaults()
        {
            DefaultPersonalOverlaysManager.SetSettingsForOwner(_currentOwner, new PersonalOverlaySettings { CellInfos = PersonalOverlayInstances.Where(c => c.SelectedMetric != OverlayType.None).Select(i => i.CurrentCellInfo).ToList() });
        }
        public ObservableCollection<PersonalOverlayInstanceViewModel> PersonalOverlayInstances { get; set; } = new ObservableCollection<PersonalOverlayInstanceViewModel>();
        public void UpdateScale(double scale)
        {
            _currentScale = scale;
            App.Current.Dispatcher.Invoke(() =>
            {
                foreach (var cell in PersonalOverlayInstances)
                {
                    cell.UpdateScale(scale);
                }
            });
        }
        public bool OverlaysMoveable
        {
            get => overlaysMoveable; internal set
            {
                if (overlaysMoveable == value)
                    return;
                overlaysMoveable = value;
                foreach (var instance in PersonalOverlayInstances)
                {
                    instance.OverlayUnlocked = overlaysMoveable;
                }
                if (!OverlaysMoveable)
                {
                    if (PersonalOverlayInstances.Count % 2 != 0)
                    {
                        Rows = Rows - 1;
                    }
                }
                else
                {
                    if (PersonalOverlayInstances.Count % 2 != 0)
                    {
                        Rows = Rows + 1;
                    }
                }
                OnPropertyChanged();
            }
        }
        public double Rows
        {
            get => rows; set
            {
                rows = value;
                OnPropertyChanged();
            }
        }
        internal void UpdateLock(bool value)
        {
            OverlaysMoveable = !value;
            OnLocking(value);
        }
        public bool Active
        {
            get => active; internal set
            {
                if (active != value)
                {
                    ActiveChanged(value);
                }
                active = value;
                DefaultGlobalOverlays.SetActive("Personal", active);
                if (active)
                {
                    _window.Show();
                }
                else
                {
                    _window.Hide();
                }
                OnPropertyChanged();
            }
        }

        internal void OverlayClosing()
        {
            Active = false;
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
