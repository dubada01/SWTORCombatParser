using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Overlays.RaidHots;
using SWTORCombatParser.Views.Overlay.RaidHOTs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
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
        private bool raidHotsEnabled;
        private string _editText = "Reposition\nRaid Frame";
        private string _unEditText = "Lock Raid Frame";
        private string toggleEditText;

        public event PropertyChangedEventHandler PropertyChanged;

        public RaidHotsConfigViewModel()
        {
            _currentOverlay = new RaidFrameOverlay();
            _currentOverlayViewModel = new RaidFrameOverlayViewModel() { Columns = int.Parse(RaidFrameColumns), Rows = int.Parse(RaidFrameRows), Width = 500, Height = 450, Editable = false };
            _currentOverlay.DataContext = _currentOverlayViewModel;
            ToggleEditText = _editText;
            RaidHotsEnabled = true;
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

        private void AutoDetection(object obj)
        {
            _currentOverlay.Hide();
            var raidFrameBitmap = RaidFrameScreenGrab.GetRaidFrameBitmap(_currentOverlayViewModel.TopLeft, (int)_currentOverlayViewModel.Width, (int)_currentOverlayViewModel.Height);
            _currentOverlay.Show();
            raidFrameBitmap.Save("test.png");
            var names = AutoHOTOverlayPosition.GetCurrentPlayerLayout(raidFrameBitmap, _currentOverlayViewModel.Rows, _currentOverlayViewModel.Columns);
            var orderedNames = new List<string>();
            for (var r = 0; r < _currentOverlayViewModel.Rows; r++)
            {
                for (var c = 0; c < _currentOverlayViewModel.Columns; c++)
                {
                    var nameInPosition = names.FirstOrDefault(n => n.Row == r && n.Column == c);
                    if (nameInPosition != null)
                        orderedNames.Add(nameInPosition.Name);
                    else
                    {
                        orderedNames.Add("");
                    }
                }
            }
            _currentOverlayViewModel.UpdateNames(orderedNames);
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
