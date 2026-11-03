using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ReactiveUI;
using SWTORCombatParser.Views.Overlay.AbilityList;

namespace SWTORCombatParser.ViewModels.Overlays.AbilityList
{
    public class AbilityInfo:ReactiveObject
    {
        private double fontSize;
        private Bitmap icon;

        public Bitmap Icon { get => icon; set
            {
                this.RaiseAndSetIfChanged(ref icon, value);
            } 
        }
        public string UseTime { get; set; }
        public string AbilityName { get; set; }
        public double FontSize { get => fontSize; set 
            { 
                this.RaiseAndSetIfChanged(ref fontSize, value);
            }
        }
    }
    public class AbilityListViewModel:BaseOverlayViewModel
    {
        private double defaultBarHeight = 35;
        private double defaultFontSize = 18;
        private double sizeScalar = 1;
        private IDisposable _updateSub;
        private ObservableCollection<AbilityInfo> abilityInfoList = new ObservableCollection<AbilityInfo>();
        public override bool ShouldBeVisible => true;

        public ObservableCollection<AbilityInfo> AbilityInfoList
        {
            get => abilityInfoList; set
            {
                this.RaiseAndSetIfChanged(ref abilityInfoList, value);
            }
        }
        public AbilityListViewModel(string overlayName):base(overlayName)
        {
            MainContent = new AbilityListView(this);
            CombatLogStreamer.CombatStarted += Reset;
            CombatSelectionMonitor.OnInProgressCombatSelected += UpdateList;
            CombatSelectionMonitor.PhaseSelected += UpdateList;
            _updateSub = Observable.FromEvent<Combat>(manager => CombatSelectionMonitor.CombatSelected += manager,
manager => CombatSelectionMonitor.CombatSelected -= manager).Subscribe(UpdateList);
            this.WhenAnyValue(x=>x.SizeScalar).Subscribe(_ => this.RaisePropertyChanged(nameof(BarHeight)));
            this.WhenAnyValue(x=>x.SizeScalar).Subscribe(_ => this.RaisePropertyChanged(nameof(FontSize)));
        }
        private void Reset()
        {
            Dispatcher.UIThread.Invoke(() => {
                AbilityInfoList.Clear();
            });

        }
        private void UpdateList(Combat combat)
        {
            if (CombatLogStateBuilder.CurrentState.LocalPlayer == null)
                return;
            var abilities = new ConcurrentQueue<ParsedLogEntry>();
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
                    Bitmap icon;
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
                Dispatcher.UIThread.Invoke(() =>
                {
                    foreach(var newAbility in newAbilityInfos)
                    {
                        AbilityInfoList.Insert(0,newAbility);
                    }
                });
            }

        }

        private async Task<Bitmap> GetIconFromId(ulong abilityId)
        {
            //TODO Get actual icons
            return await IconGetter.GetIconForId(abilityId);


        }

        public double FontSize => Math.Max(8, defaultFontSize * SizeScalar);
        public double BarHeight => defaultBarHeight * SizeScalar;
        public Thickness BarMargin => new Thickness(0,BarHeight/4,0,0);
        public double SizeScalar
        {
            get => sizeScalar; set
            {
                this.RaiseAndSetIfChanged(ref sizeScalar, value);
                foreach(var ability in AbilityInfoList)
                {
                    ability.FontSize = FontSize;
                }
            }
        }

        public bool IsEnabled { get; internal set; }

        public void LockOverlays()
        {
            OverlaysMoveable = false;
        }
        public void UnlockOverlays()
        {
            OverlaysMoveable = true;
        }
        private void Dispose()
        {
            CombatLogStreamer.CombatStarted -= Reset;
            CombatSelectionMonitor.CombatSelected -= UpdateList;
            CombatSelectionMonitor.PhaseSelected -= UpdateList;
            _updateSub.Dispose();
        }
    }
}
