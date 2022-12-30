using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Utilities;
using System;
using System.Windows.Input;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels.Timers
{
    public class TimerRowInstanceViewModel
    {
        private bool _isEnabled;

        public event Action<TimerRowInstanceViewModel> EditRequested = delegate { };
        public event Action<TimerRowInstanceViewModel> ShareRequested = delegate { };
        public event Action<TimerRowInstanceViewModel> DeleteRequested = delegate { };
        public event Action<TimerRowInstanceViewModel> ActiveChanged = delegate { };
        public Timer SourceTimer { get; set; } = new Timer();
        public bool IsHOT => SourceTimer.IsHot;
        public bool IsMechanic => SourceTimer.IsMechanic;
        public bool CanEdit => !IsHOT && !SourceTimer.IsBuiltInDot;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                ActiveChanged(this);
            }
        }
        public string Name => SourceTimer.Name;
        public string Type => SourceTimer.TriggerType.ToString();
        public string DurationSec => SourceTimer.IsAlert ? "Alert" : SourceTimer.DurationSec.ToString();
        public SolidColorBrush RowBackground { get; set; }
        public SolidColorBrush TimerBackground => new SolidColorBrush(SourceTimer.TimerColor);
        public ICommand EditCommand => new CommandHandler(Edit);
        private void Edit(object t)
        {
            EditRequested(this);
        }
        public ICommand ShareCommand => new CommandHandler(Share);
        private void Share(object t)
        {
            ShareRequested(this);
        }
        public ICommand DeleteCommand => new CommandHandler(Delete);
        private void Delete(object t)
        {
            DeleteRequested(this);
        }
    }
}
