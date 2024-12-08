using SWTORCombatParser.Model.Timers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Avalonia.Threading;
using ReactiveUI;
using Timer = SWTORCombatParser.DataStructures.Timer;

namespace SWTORCombatParser.ViewModels.Timers
{
    public class TimerRowInstanceViewModel :ReactiveObject, INotifyPropertyChanged
    {
        private bool _isEnabled;
        private SolidColorBrush _rowBackground;

        public event Action<TimerRowInstanceViewModel> EditRequested = delegate { };
        public event Action<TimerRowInstanceViewModel> CopyRequested = delegate { };
        public event Action<TimerRowInstanceViewModel> ShareRequested = delegate { };
        public event Action<TimerRowInstanceViewModel> DeleteRequested = delegate { };
        public event Action<TimerRowInstanceViewModel> ActiveChanged = delegate { };
        public Timer SourceTimer { get; set; } = new Timer();
        public bool IsHOT => SourceTimer.IsHot;
        public bool IsMechanic => SourceTimer.IsMechanic;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                ActiveChanged(this);
            }
        }

        public IImmutableSolidColorBrush AudioButtonBorderColor => !string.IsNullOrEmpty(SourceTimer.CustomAudioPath)
            ? Brushes.SeaGreen
            : Brushes.Transparent;
        public Bitmap AudioImageSource => SourceTimer.UseAudio ? new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/audioIcon.png"))) : new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/mutedIcon.png")));
        public Bitmap VisibilityImageSource => !SourceTimer.IsSubTimer ? new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/view.png"))) :new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/hidden.png")));
        public string Name => SourceTimer.Name;
        public string Type => SourceTimer.TriggerType.ToString();
        public string DurationSec => SourceTimer.IsAlert ? "Alert" : SourceTimer.DurationSec.ToString();

        public SolidColorBrush RowBackground
        {
            get => _rowBackground;
            set
            {
                _rowBackground = value;
                OnPropertyChanged();
            }
        }

        public SolidColorBrush TimerForeground => new SolidColorBrush(SourceTimer.TimerColor);
        public ReactiveCommand<object,Unit> ToggleAudioCommand => ReactiveCommand.Create<object>(ToggleAudio);

        private void ToggleAudio(object obj)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                SourceTimer.UseAudio = !SourceTimer.UseAudio;
                DefaultOrbsTimersManager.SetTimerAudio(SourceTimer.UseAudio, SourceTimer);
                TimerController.RefreshAvailableTimers();
                OnPropertyChanged("AudioImageSource");
            });

        }
        public ReactiveCommand<object,Unit> ToggleVisibilityCommand => ReactiveCommand.Create<object>(ToggleVisiblity);

        private void ToggleVisiblity(object obj)
        {
            SourceTimer.IsSubTimer = !SourceTimer.IsSubTimer;
            DefaultOrbsTimersManager.SetTimerVisibility(SourceTimer.IsSubTimer, SourceTimer);
            TimerController.RefreshAvailableTimers();
            OnPropertyChanged("VisibilityImageSource");
        }

        public ReactiveCommand<object,Unit> EditCommand => ReactiveCommand.Create<object>(Edit);
        private void Edit(object t)
        {
            EditRequested(this);
        }
        public ReactiveCommand<object,Unit> ShareCommand =>ReactiveCommand.Create<object>(Share);
        private void Share(object t)
        {
            ShareRequested(this);
        }
        public ReactiveCommand<object,Unit> CopyCommand => ReactiveCommand.Create<object>(Copy);

        private void Copy(object obj)
        {
            CopyRequested(this);
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
        internal void SetVisibility(bool visible)
        {
            if (visible)
            {
                SourceTimer.IsSubTimer = !visible;
            }
            else
            {
                SourceTimer.IsSubTimer = visible;
            }

            OnPropertyChanged("VisibilityImageSource");
        }
        internal void SetAudio(bool muted)
        {
            if (muted)
            {
                SourceTimer.UseAudio = !muted;
            }
            else
            {
                SourceTimer.UseAudio = !string.IsNullOrEmpty(SourceTimer.CustomAudioPath);
            }

            OnPropertyChanged("AudioImageSource");
        }

        internal void SetActive(bool allActive)
        {
            SourceTimer.IsEnabled = allActive;
            _isEnabled = allActive;
            OnPropertyChanged("IsEnabled");
        }
    }
}
