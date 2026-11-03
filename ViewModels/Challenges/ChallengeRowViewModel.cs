using SWTORCombatParser.DataStructures;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Challenges
{
    public class ChallengeRowViewModel:ReactiveObject
    {
        private bool _isEnabled;
        private SolidColorBrush _rowBackground;

        public event Action<ChallengeRowViewModel> EditRequested = delegate { };
        public event Action<ChallengeRowViewModel> DeleteRequested = delegate { };
        public event Action<ChallengeRowViewModel> ActiveChanged = delegate { };
        public event Action<ChallengeRowViewModel> ShareRequested = delegate { };
        public Challenge SourceChallenge { get; set; } = new Challenge();
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                ActiveChanged(this);
            }
        }
        public string Name => SourceChallenge.Name;
        public string Type => SourceChallenge.ChallengeType.ToString();

        public SolidColorBrush RowBackground
        {
            get => _rowBackground;
            set
            {
                _rowBackground = value;
                OnPropertyChanged();
            }
        }

        public SolidColorBrush ChallengeBackground => SourceChallenge.BackgroundBrush;

        public ReactiveCommand<object,Unit> EditCommand => ReactiveCommand.Create<object>(Edit);
        private void Edit(object t)
        {
            EditRequested(this);
        }
        public ReactiveCommand<object,Unit> ShareCommand => ReactiveCommand.Create<object>(Share);
        private void Share(object t)
        {
            ShareRequested(this);
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

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

    }
}
