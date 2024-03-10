using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private ImageSource icon;

        public ImageSource Icon { get => icon; set 
            {
                icon = value;
                OnPropertyChanged();
            } 
        }
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
        private ObservableCollection<AbilityInfo> abilityInfoList = new ObservableCollection<AbilityInfo>();

        public event Action<AbilityListViewModel> OverlayClosed = delegate { };
        public Action<bool> OnLocking = delegate { };
        public Action OnHiding = delegate { };
        public Action OnShowing = delegate { };
        public Action CloseRequested = delegate { };
        public ObservableCollection<AbilityInfo> AbilityInfoList
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
            App.Current.Dispatcher.Invoke(() => {
                AbilityInfoList.Clear();
            });

        }
        private void UpdateList(Combat combat)
        {
            if (CombatLogStateBuilder.CurrentState.LocalPlayer == null)
                return;
            var abilities = new List<ParsedLogEntry>();
            if(combat.AbilitiesActivated.TryGetValue(CombatLogStateBuilder.CurrentState.LocalPlayer, out abilities))
            {
                var abilitiesUsedlist = abilities.AsEnumerable().Reverse().ToList();
                var newlyAddedAbilities = abilitiesUsedlist.Take((abilitiesUsedlist.Count - AbilityInfoList.Count));
                var iconGetTasks = new List<Task>();
                var newAbilityInfos = new List<AbilityInfo>();
                foreach (var newAbility in newlyAddedAbilities)
                {
                    var newAbilityInfo = new AbilityInfo
                    {
                        FontSize = FontSize,
                        AbilityName = newAbility.Ability,
                        UseTime = $"{((int)(newAbility.TimeStamp - combat.StartTime).TotalMinutes > 0 ? (int)(newAbility.TimeStamp - combat.StartTime).TotalMinutes + "m " : "")}{(newAbility.TimeStamp - combat.StartTime).Seconds}s"
                    };
                    BitmapImage icon;
                    if (IconGetter.IconDict.TryGetValue(newAbility.AbilityId, out icon))
                    {
                        newAbilityInfo.Icon = icon;

                    }
                    else
                    {
                        iconGetTasks.Add(Task.Run(async() =>
                        { 
                            var fetchedIcon = await GetIconFromId(newAbility.AbilityId);
                            newAbilityInfo.Icon = fetchedIcon;
                        }));
                    }
                    newAbilityInfos.Insert(0,newAbilityInfo);
                }
                App.Current.Dispatcher.Invoke(() =>
                {
                    foreach(var newAbility in newAbilityInfos)
                    {
                        AbilityInfoList.Insert(0,newAbility);
                    }
                });
            }

        }

        private async Task<BitmapImage> GetIconFromId(string abilityId)
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
                foreach(var ability in AbilityInfoList)
                {
                    ability.FontSize = FontSize;
                }
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
        public void OverlayDisabled()
        {
            IsEnabled = false;
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
            if (obj.Effect.EffectId == _7_0LogParsing.InConversationEffectId && obj.Effect.EffectType == EffectType.Apply && obj.Source.IsLocalPlayer)
            {
                OnHiding();
            }

            if (obj.Effect.EffectId == _7_0LogParsing.InConversationEffectId && obj.Effect.EffectType == EffectType.Remove && IsEnabled && obj.Source.IsLocalPlayer)
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
