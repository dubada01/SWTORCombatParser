﻿using SWTORCombatParser.Model.Overlays;
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

        public event PropertyChangedEventHandler PropertyChanged;

        public RaidHotsConfigViewModel()
        {
            RaidFrameOverlayManager.Init();
            
            _currentOverlay = new RaidFrameOverlay();


            _currentOverlayViewModel = new RaidFrameOverlayViewModel(_currentOverlay) { Columns = int.Parse(RaidFrameColumns), Rows = int.Parse(RaidFrameRows), Width = 500, Height = 450, Editable = false };
            _currentOverlayViewModel.NamesUpdated += ReFindImage;
            _currentOverlay.DataContext = _currentOverlayViewModel;

            ToggleEditText = _editText;

            var defaults = RaidFrameOverlayManager.GetDefaults(_currentCharacter);
            _currentOverlay.Width = defaults.WidtHHeight.X;
            _currentOverlay.Height = defaults.WidtHHeight.Y;
            _currentOverlay.Top = defaults.Position.Y;
            _currentOverlay.Left = defaults.Position.X;
            RaidHotsEnabled = defaults.Acive;
            StartInitialCheck();
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
        public bool RaidHotsEnabled
        {
            get => raidHotsEnabled; set
            {
                raidHotsEnabled = value;
                if (raidHotsEnabled)
                    _currentOverlay.Show();
                else
                    _currentOverlay.Hide();
                _currentOverlayViewModel.Active = raidHotsEnabled;
                RaidFrameOverlayManager.SetActiveState(RaidHotsEnabled, _currentCharacter);
                OnPropertyChanged();
            }
        }
        public ICommand StartRaidHotPositioning => new CommandHandler(StartPositioning);

        private void StartPositioning(object obj)
        {
            if (!_isRaidFrameEditable)
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
            AutoDetection(null);
        }
        private void AutoDetection(object obj)
        {
            Application.Current.Dispatcher.Invoke(() => {
                _currentOverlay.Hide();
            });
            
            var raidFrameBitmap = RaidFrameScreenGrab.GetRaidFrameBitmap(_currentOverlayViewModel.TopLeft, (int)_currentOverlayViewModel.Width, (int)_currentOverlayViewModel.Height);
            Application.Current.Dispatcher.Invoke(() => {
                _currentOverlay.Show();
            });
            var names = AutoHOTOverlayPosition.GetCurrentPlayerLayout(_currentOverlayViewModel.TopLeft,raidFrameBitmap, _currentOverlayViewModel.Rows, _currentOverlayViewModel.Columns);
            Application.Current.Dispatcher.Invoke(() =>
            {
                _currentOverlayViewModel.UpdateNames(names);
            });
        }
        private void StartInitialCheck()
        {
            Task.Run(() => {
                while (_shouldCheckInitial)
                {
                    if (RaidFrameEditable)
                    {
                        Application.Current.Dispatcher.Invoke(() => {
                            _currentOverlay.Hide();
                        });
                    }
                    
                    var raidFrameBitmap = RaidFrameScreenGrab.GetRaidFrameBitmap(_currentOverlayViewModel.TopLeft, (int)_currentOverlayViewModel.Width, (int)_currentOverlayViewModel.Height);
                    if(RaidFrameEditable)
                    {
                        Application.Current.Dispatcher.Invoke(() => {
                            _currentOverlay.Show();
                        });
                    }

                    var redPixelAverage = RaidFrameScreenGrab.GetRatioOfRedPixels(raidFrameBitmap);
                    Trace.WriteLine(redPixelAverage.ToString());
                    if (redPixelAverage > 0.015)
                    {
                        AutoDetection(null);
                        _shouldCheckInitial = false;
                        break;
                    }
                    Thread.Sleep(1000);
                }
            });

        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void PlayerDetected(string name)
        {
            _currentCharacter = name;
            var defaults = RaidFrameOverlayManager.GetDefaults(name);
            RaidHotsEnabled = defaults.Acive;
            _currentOverlayViewModel.FirePlayerChanged(name);
        }
    }
}