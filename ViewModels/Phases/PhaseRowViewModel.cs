using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Challenges;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels.Phases
{
    public class PhaseRowViewModel:INotifyPropertyChanged
    {
        private SolidColorBrush _rowBackground;

        public event Action<PhaseRowViewModel> EditRequested = delegate { };
        public event Action<PhaseRowViewModel> DeleteRequested = delegate { };
        public event Action<PhaseRowViewModel> ActiveChanged = delegate { };

        public Phase SourcePhase { get; set; } = new Phase();
        public string Name => SourcePhase.Name;
        public string Type => SourcePhase.StartTrigger.ToString();
        public SolidColorBrush RowBackground
        {
            get => _rowBackground;
            set
            {
                _rowBackground = value;
                OnPropertyChanged();
            }
        }
        public ICommand EditCommand => new CommandHandler(Edit);
        private void Edit(object t)
        {
            EditRequested(this);
        }

        public ICommand DeleteCommand => new CommandHandler(Delete);
        private void Delete(object t)
        {
            DeleteRequested(this);
        }
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
