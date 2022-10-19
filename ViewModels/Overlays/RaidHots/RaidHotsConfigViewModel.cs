using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Overlays.RaidHots;
using SWTORCombatParser.Views.Overlay.RaidHOTs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SWTORCombatParser.ViewModels.Overlays
{
    public class RaidHotsConfigViewModel:INotifyPropertyChanged
    {
        private string raidFrameRows = "4";
        private string raidFrameColumns = "2";
        private RaidFrameOverlay _currentOverlay;
        private RaidFrameOverlayViewModel _currentOverlayViewModel;
        private bool _isRaidFrameEditable = false;
        private bool raidHotsEnabled = false;
        private string _editText = "Reposition\nRaid Frame";
        private string _unEditText = "Lock Raid Frame";
        private string toggleEditText;
        private string _currentCharacter = "no character";
        private bool _shouldCheckInitial = true;
        private bool _liveParseActive = false;

        public event PropertyChangedEventHandler PropertyChanged;
        public event Action<bool> EnabledChanged = delegate { };

        public RaidHotsConfigViewModel()
        {
            RaidFrameOverlayManager.Init();
            
            _currentOverlay = new RaidFrameOverlay();
            CombatLogStreamer.HistoricalLogsFinished += () => UpdateVisualsBasedOnRole(CombatLogStateBuilder.CurrentState.GetLocalPlayerClassAtTime(DateTime.Now));
            CombatLogStateBuilder.PlayerDiciplineChanged += SetClass;
            CombatLogStateBuilder.AreaEntered += ReFindImage;
            _currentOverlayViewModel = new RaidFrameOverlayViewModel(_currentOverlay) { Columns = int.Parse(RaidFrameColumns), Rows = int.Parse(RaidFrameRows), Width = 500, Height = 450, Editable = _isRaidFrameEditable };
            _currentOverlayViewModel.NamesUpdated += ReFindImage;
            _currentOverlay.DataContext = _currentOverlayViewModel;

            ToggleEditText = _editText;

            var defaults = RaidFrameOverlayManager.GetDefaults(_currentCharacter);
            _currentOverlay.Width = defaults.WidtHHeight.X;
            _currentOverlay.Height = defaults.WidtHHeight.Y;
            _currentOverlay.Top = defaults.Position.Y;
            _currentOverlay.Left = defaults.Position.X;

            
        }

        public bool RaidFrameEditable => _isRaidFrameEditable;
        public string ToggleEditText
        {
            get => toggleEditText; set
            {
                toggleEditText = value;
                OnPropertyChanged();
            }
        }
        public string RaidFrameRows
        {
            get => raidFrameRows;
            set
            {
                if (value.Any(v => !char.IsDigit(v)))
                    return;
                raidFrameRows = value;
                if (raidFrameRows == "")
                    return;
                _currentOverlayViewModel.Rows = int.Parse(RaidFrameRows);
            }
        }
        public string RaidFrameColumns
        {
            get => raidFrameColumns; set
            {
                if (value.Any(v => !char.IsDigit(v)))
                    return;
                raidFrameColumns = value;
                if (raidFrameColumns == "")
                    return;
                _currentOverlayViewModel.Columns = int.Parse(RaidFrameColumns);
            }
        }
        public void HideRaidHots()
        {
            Application.Current.Dispatcher.Invoke(() => {
                _currentOverlay.Hide();
            });
        }
        public bool RaidHotsEnabled
        {
            get => raidHotsEnabled; set
            {
                if (raidHotsEnabled == value)
                    return;
                raidHotsEnabled = value;
                
                if (raidHotsEnabled)
                {
                    _currentOverlay.Show();
                    if(_liveParseActive)
                        StartInitialCheck();
                }
                
                else
                {
                    _shouldCheckInitial = false;
                    _currentOverlay.Hide();
                    _currentOverlayViewModel.CurrentNames.Clear();
                }
                
                EnabledChanged(raidHotsEnabled);
                _currentOverlayViewModel.Active = raidHotsEnabled;
                RaidFrameOverlayManager.SetActiveState(RaidHotsEnabled, _currentCharacter);
                OnPropertyChanged();
            }
        }
        private void StartPositioning(bool isLocked)
        {
            if (!isLocked)
            {
                ToggleEditText = _unEditText;
                _isRaidFrameEditable = true;
                _currentOverlayViewModel.Editable = true;
            }
            else
            {
                ToggleEditText = _editText;
                _isRaidFrameEditable = false;
                _currentOverlayViewModel.Editable = false;
            }
            OnPropertyChanged("RaidFrameEditable");
        }
        public ICommand StartAutoDetection => new CommandHandler(AutoDetection);
        private void ReFindImage()
        {
            if (!RaidHotsEnabled)
                return;
            Task.Run(() => {
                Thread.Sleep(20000);
                AutoDetection(null);
            });
        }
        private bool _preformingAutoDetection;
        private void AutoDetection(object obj)
        {
            Task.Run(() => {
                _preformingAutoDetection = true;
                Application.Current.Dispatcher.Invoke(() => {
                    _currentOverlay.Opacity = 0;
                });
                Thread.Sleep(250);
                var raidFrameBitmap = RaidFrameScreenGrab.GetRaidFrameBitmap(_currentOverlayViewModel.TopLeft, (int)_currentOverlayViewModel.Width, (int)_currentOverlayViewModel.Height);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _currentOverlay.Opacity = 1;
                });
                var names = AutoHOTOverlayPosition.GetCurrentPlayerLayout(_currentOverlayViewModel.TopLeft, raidFrameBitmap, _currentOverlayViewModel.Rows, _currentOverlayViewModel.Columns);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _currentOverlayViewModel.UpdateNames(names);
                });
                _shouldCheckInitial = false;
                _preformingAutoDetection = false;
                //StartCheckForEndOfGroup();
            }); 
        }
        private void StartInitialCheck()
        {
            Task.Run(() => {
                while (_shouldCheckInitial)
                {
                    if(_preformingAutoDetection)
                    {
                        Thread.Sleep(100);
                        continue;
                    }
                    if (RaidFrameEditable)
                    {
                        Application.Current.Dispatcher.Invoke(() => {
                            _currentOverlay.Opacity = 0; ;
                        });
                    }
                    using (var raidFrameBitmap = RaidFrameScreenGrab.GetRaidFrameBitmap(_currentOverlayViewModel.TopLeft, (int)_currentOverlayViewModel.Width, (int)_currentOverlayViewModel.Height))
                    {
                        if (RaidFrameEditable)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _currentOverlay.Opacity = 1; ;
                            });
                        }

                        var redPixelAverage = RaidFrameScreenGrab.GetRatioOfRedPixels(raidFrameBitmap);
                        if (redPixelAverage > 0.015)
                        {
                            AutoDetection(null);
                            _shouldCheckInitial = false;
                            break;
                        }
                        Thread.Sleep(1000);
                    }
                }
            });

        }

        internal void ToggleLock(bool overlaysLocked)
        {
            StartPositioning(overlaysLocked);
        }

        private void StartCheckForEndOfGroup()
        {
            Task.Run(() => {
                while (RaidHotsEnabled && !_shouldCheckInitial)
                {
                    if (RaidFrameEditable)
                    {
                        Application.Current.Dispatcher.Invoke(() => {
                            _currentOverlay.Opacity = 0; ;
                        });
                    }

                    using (var raidFrameBitmap = RaidFrameScreenGrab.GetRaidFrameBitmap(_currentOverlayViewModel.TopLeft, (int)_currentOverlayViewModel.Width, (int)_currentOverlayViewModel.Height))
                    {
                        if (RaidFrameEditable)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _currentOverlay.Opacity = 1; ;
                            });
                        }

                        var redPixelAverage = RaidFrameScreenGrab.GetRatioOfRedPixels(raidFrameBitmap);
                        if (redPixelAverage <= 0.015)
                        {
                            _currentOverlayViewModel.CurrentNames.Clear();
                            break;
                        }
                        Thread.Sleep(3000);
                    }
                }
            });

        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void UpdateVisualsBasedOnRole(SWTORClass mostRecentDiscipline)
        {
            if (mostRecentDiscipline == null)
                return;
            if (mostRecentDiscipline.Role == Role.Healer)
            {
                var defaults = RaidFrameOverlayManager.GetDefaults(_currentCharacter);
                App.Current.Dispatcher.Invoke(() =>
                {
                    RaidHotsEnabled = defaults.Acive;
                    _currentOverlayViewModel.FirePlayerChanged(_currentCharacter);
                });
            }
            else
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    RaidHotsEnabled = false;
                });
            }
        }

        private void SetClass(Entity arg1, SWTORClass arg2)
        {
            if (_currentCharacter == arg1.Name + "/" + arg2.Discipline)
                return;
            _currentCharacter = arg1.Name + "/" + arg2.Discipline;
            UpdateVisualsBasedOnRole(arg2);
        }

        internal void LiveParseActive(bool state)
        {
            _liveParseActive = state;
            if (RaidHotsEnabled)
                StartInitialCheck();
        }
    }
}
