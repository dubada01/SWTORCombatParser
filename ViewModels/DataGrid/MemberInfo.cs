using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using ReactiveUI;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SWTORCombatParser.Utilities.Converters;

namespace SWTORCombatParser.ViewModels.DataGrid
{
    public class MemberInfoViewModel : ReactiveObject
    {
        private string valueStringFormat = "#,##0";
        private string floatValueString = "0.00";
        public Entity _entity;
        private Combat? _info;
        private SWTORClass _playerClass;
        private Bitmap _classIcon;

        public MemberInfoViewModel(int order, Entity e, Combat info, List<OverlayType> selectedColumns)
        {
            _entity = e;
            IsTotalsRow = e == null;
            StatsSlots.Add(new StatsSlotViewModel(OverlayType.None, IsTotalsRow ? "Totals" : e.Name,
                entity: e) { Header = "Name" });
            Update(info, selectedColumns, e);
        }

        public void Update(Combat newInfo, List<OverlayType> selectedColumns, Entity entity = null)
        {
            var previousName = PlayerName;
            var previousClass = ClassName;
            var wasLocal = IsLocalPlayer;
            _info = newInfo;
            if (entity != null) _entity = entity;
            IsLocalPlayer = _entity?.IsLocalPlayer == true;
            _playerClass = _entity == null ? null : GetClassForEntity(newInfo, _entity);
            var nameSlot = StatsSlots[0];
            nameSlot.Value = PlayerName;
            // Icons load asynchronously; retry the lookup even if the discipline
            // is unchanged, then notify only when the actual bitmap changes.
            nameSlot.UpdateIcon(_playerClass?.Discipline);
            this.RaiseAndSetIfChanged(ref _classIcon, nameSlot.RoleIcon ?? IconFactory._unknownIcon, nameof(ClassIcon));
            if (PlayerName != previousName) this.RaisePropertyChanged(nameof(PlayerName));
            if (ClassName != previousClass) this.RaisePropertyChanged(nameof(ClassName));
            if (PlayerName != previousName || ClassName != previousClass)
                this.RaisePropertyChanged(nameof(PlayerSummary));
            if (IsLocalPlayer != wasLocal) this.RaisePropertyChanged(nameof(NameForeground));

            // Reconcile the selected metrics without replacing existing slots.
            foreach (var slot in StatsSlots.Skip(1).Where(slot => !selectedColumns.Contains(slot.OverlayType)).ToList())
                StatsSlots.Remove(slot);
            for (var i = 0; i < selectedColumns.Count; i++)
            {
                var type = selectedColumns[i];
                var slot = StatsSlots.Skip(1).FirstOrDefault(slot => slot.OverlayType == type);
                if (slot == null)
                {
                    slot = new StatsSlotViewModel(type, entity: _entity);
                    StatsSlots.Insert(i + 1, slot);
                }
                else if (StatsSlots.IndexOf(slot) != i + 1)
                    StatsSlots.Move(StatsSlots.IndexOf(slot), i + 1);
                slot.Value = GetValue(type);
            }
        }
        public bool IsTotalsRow { get; set; }
    
        public bool IsLocalPlayer { get; set; }
        public string PlayerName => _entity?.Name ?? "Total";
        public IBrush NameForeground => IsLocalPlayer ? Brushes.Goldenrod : Brushes.WhiteSmoke;
        public string PlayerSummary => IsTotalsRow ? "Raid total" : $"Player: {PlayerName}\nClass: {ClassName}";
        public Bitmap ClassIcon => _classIcon;

        public string ClassName => _playerClass?.Name + "/" + _playerClass?.Discipline;

        private string GetValue(OverlayType columnType)
        {
            var formatToUse = columnType == OverlayType.CleanseSpeed ? floatValueString : valueStringFormat;
            if (_entity == null)
                return MetricGetter.GetTotalforMetric(columnType, _info).ToString(formatToUse);
            return MetricGetter.GetValueForMetric(columnType, _info, _entity).ToString(formatToUse);
        }

        private static SWTORClass GetClassForEntity(Combat combat, Entity entity)
        {
            if (combat?.CharacterClases != null)
            {
                var classEntry = combat.CharacterClases.FirstOrDefault(kvp =>
                    (entity.LogId != 0 && kvp.Key.LogId == entity.LogId) ||
                    (entity.Id != 0 && kvp.Key.Id == entity.Id) ||
                    kvp.Key == entity);

                if (classEntry.Key != null)
                    return classEntry.Value;
            }

            return CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(entity, combat?.StartTime ?? System.DateTime.MinValue);
        }

        public ObservableCollection<StatsSlotViewModel> StatsSlots { get; set; } = new();
    }
}
