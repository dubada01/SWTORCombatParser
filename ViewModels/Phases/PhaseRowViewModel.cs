using SWTORCombatParser.Model.Phases;
using System;
using System.ComponentModel;
using System.Reactive;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Phases
{
    public class PhaseRowViewModel : ReactiveObject, INotifyPropertyChanged
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
        public ReactiveCommand<object,Unit> EditCommand => ReactiveCommand.Create<object>(Edit);
        private void Edit(object t)
        {
            EditRequested(this);
        }

        public ReactiveCommand<object,Unit> DeleteCommand => ReactiveCommand.Create<object>(Delete);
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
