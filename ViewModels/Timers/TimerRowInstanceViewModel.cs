using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public bool CanEdit => !IsHOT;
        public bool IsEnabled
        {
            get
            {
                return _isEnabled;
            }
            set
            {
                _isEnabled = value;
                ActiveChanged(this);
            }
        }
        public string Name => SourceTimer.Name;
        public string Type => SourceTimer.TriggerType.ToString();
        public double DurationSec => SourceTimer.DurationSec;
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
