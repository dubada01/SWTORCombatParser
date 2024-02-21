using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SWTORCombatParser.ViewModels.Overlays.AbilityList
{
    public class AbilityInfo:INotifyPropertyChanged
    {
        private double fontSize;

        public ImageSource Icon { get; set; }
        public string UseTime { get; set; }
        public string AbilityName { get; set; }
        public double FontSize { get => fontSize; set 
            { 
                fontSize = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
    public class AbilityListViewModel:INotifyPropertyChanged
    {
        private double defaultBarHeight = 35;
        private double defaultFontSize = 18;
        private double sizeScalar = 1;
        private IDisposable _updateSub;
        private List<AbilityInfo> abilityInfoList = new List<AbilityInfo>();

        public event Action<AbilityListViewModel> OverlayClosed = delegate { };
        public Action<bool> OnLocking = delegate { };
        public Action OnHiding = delegate { };
        public Action OnShowing = delegate { };
        public Action CloseRequested = delegate { };
        public List<AbilityInfo> AbilityInfoList
        {
            get => abilityInfoList; set
            {
                abilityInfoList = value;
                OnPropertyChanged();
            }
        }
        public AbilityListViewModel()
        {
            CombatLogStreamer.NewLineStreamed += CheckForConversation;
            CombatLogStreamer.CombatStarted += Reset;
            CombatSelectionMonitor.OnInProgressCombatSelected += UpdateList;
            CombatSelectionMonitor.PhaseSelected += UpdateList;
            _updateSub = Observable.FromEvent<Combat>(manager => CombatSelectionMonitor.CombatSelected += manager,
manager => CombatSelectionMonitor.CombatSelected -= manager).Subscribe(UpdateList);
        }
        private void Reset()
        {
            AbilityInfoList.Clear();
        }
        private async void UpdateList(Combat combat)
        {
            var abilitiesUsedlist = combat.AbilitiesActivated[CombatLogStateBuilder.CurrentState.LocalPlayer].AsEnumerable().Reverse().ToList();

            var tasks = abilitiesUsedlist.Select(async a =>
            {
                var icon = await GetIconFromId(a.AbilityId); // Assume this is your async method to get icons
                return new AbilityInfo
                {
                    FontSize = FontSize,
                    AbilityName = a.Ability,
                    Icon = icon,
                    UseTime = $"{((int)(a.TimeStamp - combat.StartTime).TotalMinutes > 0 ? (int)(a.TimeStamp - combat.StartTime).TotalMinutes + "m " : "")}{(a.TimeStamp - combat.StartTime).Seconds}s"
                };
            });

            var abilityInfoList = await Task.WhenAll(tasks);

            App.Current.Dispatcher.Invoke(() =>
            {
                // Assuming AbilityInfoList is a property or variable that should be updated on the UI
                AbilityInfoList = abilityInfoList.ToList();
            });
        }

        private async Task<ImageSource> GetIconFromId(string abilityId)
        {
            //TODO Get actual icons

            return await IconGetter.GetIconForId(abilityId);


        }

        public double FontSize => Math.Max(8, defaultFontSize * SizeScalar);
        public double BarHeight => defaultBarHeight * SizeScalar;
        public Thickness BarMargin => new Thickness(0,BarHeight/4,0,0);
        public bool OverlaysMoveable { get; internal set; } = false;
        public double SizeScalar
        {
            get => sizeScalar; set
            {
                sizeScalar = value;
                AbilityInfoList.ForEach(a => a.FontSize = FontSize);
                OnPropertyChanged("BarHeight");
                OnPropertyChanged("FontSize");
                OnPropertyChanged();
            }
        }

        public bool IsEnabled { get; internal set; }

        public void LockOverlays()
        {
            OnLocking(true);
            OverlaysMoveable = false;
            OnPropertyChanged("OverlaysMoveable");
        }
        public void UnlockOverlays()
        {
            OnLocking(false);
            OverlaysMoveable = true;
            OnPropertyChanged("OverlaysMoveable");
        }
        public void OverlayClosing()
        {
            Dispose();
            OverlayClosed(this);
        }
        public void RequestClose()
        {
            Dispose();
            CloseRequested();
        }
        private void CheckForConversation(ParsedLogEntry obj)
        {
            if (!obj.Source.IsLocalPlayer)
                return;
            if (obj.Effect.EffectId == _7_0LogParsing.InConversationEffectId && obj.Effect.EffectType == EffectType.Apply)
            {
                OnHiding();
            }

            if (obj.Effect.EffectId == _7_0LogParsing.InConversationEffectId && obj.Effect.EffectType == EffectType.Remove && IsEnabled)
            {
                OnShowing();
            }
        }
        private void Dispose()
        {
            CombatLogStreamer.CombatStarted -= Reset;
            CombatSelectionMonitor.CombatSelected -= UpdateList;
            CombatSelectionMonitor.PhaseSelected -= UpdateList;
            CombatLogStreamer.NewLineStreamed -= CheckForConversation;
            _updateSub.Dispose();
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
