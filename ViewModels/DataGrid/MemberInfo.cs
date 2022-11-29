using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using SWTORCombatParser.DataStructures.ClassInfos;

namespace SWTORCombatParser.ViewModels.DataGrid
{
    public class MemberInfoViewModel
    {
        private SolidColorBrush _evenRow = new SolidColorBrush(Color.FromArgb(255, 100, 100, 100));
        private SolidColorBrush _oddRow = new SolidColorBrush(Color.FromArgb(255, 90, 90, 90));
        private string valueStringFormat = "#,##0";
        public Entity _entity;
        private List<Combat> _info;
        public MemberInfoViewModel(int order, Entity e, List<Combat> info, List<OverlayType> selectedColumns)
        {
            _info = info;
            _entity = e;
            IsLocalPlayer = e.IsLocalPlayer;
            StatsSlots = new ObservableCollection<StatsSlotViewModel>(selectedColumns.Select(i => new StatsSlotViewModel(i) {  Value = GetValue(i) }));
            StatsSlots.Insert(0, new StatsSlotViewModel(OverlayType.None, _entity.Name, GetIconPathFromClass(CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(_entity.Name, info.Last().StartTime)), IsLocalPlayer));
            if(selectedColumns.Count < 10)
                StatsSlots.Add(new StatsSlotViewModel(OverlayType.None) { Value = "" });
        }
        public bool IsLocalPlayer { get; set; }
        public void AssignBackground(int position)
        {
            foreach(var slot in StatsSlots)
            {
                slot.BackgroundColor = position % 2 == 0 ? _evenRow : _oddRow;
            }
        }
        private string GetValue(OverlayType columnType)
        {
           return GetValueForMetric(columnType, _info, _entity).ToString(valueStringFormat);
        }
        private static double GetValueForMetric(OverlayType type, List<Combat> combats, Entity participant)
        {
            double value = 0;
            switch (type)
            {
                case OverlayType.APM:
                    value = combats.SelectMany(c => c.APM).Where(v=>v.Key == participant).Select(v=>v.Value).Average();
                    break;
                case OverlayType.DPS:
                    value = combats.SelectMany(c => c.EDPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.EHPS:
                    value = combats.SelectMany(c => c.EHPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    value += combats.SelectMany(c => c.PSPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.ProvidedAbsorb:
                    value = combats.SelectMany(c => c.PSPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.FocusDPS:
                    value = combats.SelectMany(c => c.EFocusDPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.ThreatPerSecond:
                    value = combats.SelectMany(c => c.TPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.Threat:
                    value = combats.SelectMany(c => c.TotalThreat).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.DamageTaken:
                    value = combats.SelectMany(c => c.EDTPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.Mitigation:
                    value = combats.SelectMany(c => c.MPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.DamageSavedDuringCD:
                    value = combats.SelectMany(c => c.DamageSavedFromCDPerSecond).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.DamageAvoided:
                    value = combats.SelectMany(c => c.DAPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.ShieldAbsorb:
                    value = combats.SelectMany(c => c.SAPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.BurstDPS:
                    value = combats.SelectMany(c => c.MaxBurstDamage).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.BurstEHPS:
                    value = combats.SelectMany(c => c.MaxBurstHeal).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.BurstDamageTaken:
                    value = combats.SelectMany(c => c.MaxBurstDamageTaken).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.HealReactionTime:
                    value = combats.SelectMany(c => c.NumberOfHighSpeedReactions).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.TankHealReactionTime:
                    value = combats.SelectMany(c => c.AverageTankDamageRecoveryTimeTotal).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.InterruptCount:
                    value = combats.SelectMany(c => c.TotalInterrupts).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
            }
            return value;
        }
        private string GetIconPathFromClass(SWTORClass classInfo)
        {
            return classInfo.Role == Role.Healer ? "healingIcon.png" : classInfo.Role == Role.Tank ? "tankIcon.png" : "dpsIcon.png";
        }
        public ObservableCollection<StatsSlotViewModel> StatsSlots { get; set; } = new ObservableCollection<StatsSlotViewModel>();
    }
}
