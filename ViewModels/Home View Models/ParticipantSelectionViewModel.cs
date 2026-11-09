using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.ViewModels.Home_View_Models
{
    public class ParticipantSelectionViewModel:ReactiveObject
    {
        private bool viewEnemies;
        private int rows;
        private int columns;
        private ObservableCollection<ParticipantViewModel> _availableParticipants = new ObservableCollection<ParticipantViewModel>();
        private Combat _selectedCombat;
        private Entity _selectedParticipant;
        private static readonly Dictionary<Role, Bitmap> RoleImages = new();

        public event Action<Entity> ParticipantSelected = delegate { };
        public event Action<int> ViewEnemiesToggled = delegate { };

        public ObservableCollection<ParticipantViewModel> AvailableParticipants
        {
            get => _availableParticipants;
            set => this.RaiseAndSetIfChanged(ref _availableParticipants, value);
        }

        public int Rows
        {
            get => rows; set
            {
                if (rows == value) return;
                this.RaiseAndSetIfChanged(ref rows, value);
            }
        }
        public int Columns
        {
            get => columns; set
            {
                if (columns == value) return;
                this.RaiseAndSetIfChanged(ref columns, value);
            }
        }

        public Combat SelectedCombat
        {
            get => _selectedCombat;
            set => _selectedCombat = value;
        }

        public Entity SelectedParticipant
        {
            get => _selectedParticipant;
            set => _selectedParticipant = value;
        }

        public ParticipantSelectionViewModel()
        {
            ParticipantSelectionHandler.SelectionUpdated += SetSelection;
        }
        public bool ViewEnemies
        {
            get => viewEnemies; set
            {
                if (viewEnemies == value) return;
                this.RaiseAndSetIfChanged(ref viewEnemies, value);
                if (SelectedCombat == null) return;
                UpdateParticipantsData(SelectedCombat);
                var entitiesToShow = ViewEnemies ? SelectedCombat.AllEntities.Values.Where(e => e.IsBoss || e.IsCharacter).ToList() : SelectedCombat.CharacterParticipants.Values;
                ViewEnemiesToggled(entitiesToShow.Count);
                if (!viewEnemies && SelectedParticipant?.IsBoss == true)
                    SetSelection(entitiesToShow.FirstOrDefault(e => e.IsLocalPlayer) ?? entitiesToShow.FirstOrDefault());
            }
        }
        public void SelectLocalPlayer()
        {
            var uiElement = AvailableParticipants.FirstOrDefault(p => p.Entity.IsLocalPlayer);
            if (uiElement == null)
                return;
            SelectParticipant(uiElement, true);
        }
        private void UpdateLayout()
        {
            Columns = Math.Min(8, Math.Max(1, AvailableParticipants.Count));
            Rows = Math.Max(1, (AvailableParticipants.Count + 7) / 8);
        }

        private void SelectParticipant(ParticipantViewModel obj, bool isSelected)
        {
            if (isSelected)
                SetSelection(obj.Entity);
        }
        private void SetSelection(Entity obj)
        {
            var currentSelection = AvailableParticipants.FirstOrDefault(part => IsSameEntity(part.Entity, obj));
            if (currentSelection == null)
                return;
            SelectedParticipant = currentSelection.Entity;
            var previouslySelected = AvailableParticipants.FirstOrDefault(p => p.IsSelected && !IsSameEntity(p.Entity, currentSelection.Entity));
            if (previouslySelected != null)
                previouslySelected.ToggleSelection();
            if (!currentSelection.IsSelected)
                currentSelection.ToggleSelection();
            ParticipantSelected(currentSelection.Entity);
            ParticipantSelectionHandler.UpdateSelection(currentSelection.Entity);
        }
        private ParticipantViewModel GenerateInstance(Entity e)
        {
            ParticipantViewModel viewModel = new ParticipantViewModel();
            viewModel.Entity = e;
            viewModel.PlayerName = e.Name;
            viewModel.IsLocalPlayer = e.IsLocalPlayer;
            viewModel.RoleImageSource = IconFactory._unknownIcon;
            viewModel.IsSelected = ParticipantSelectionHandler.CurrentlySelectedParticpant?.LogId == 0 ?
                ParticipantSelectionHandler.CurrentlySelectedParticpant?.Id == viewModel.Entity?.Id :
                ParticipantSelectionHandler.CurrentlySelectedParticpant?.LogId == viewModel.Entity?.LogId;
            return viewModel;
        }
        public IEnumerable<Entity> UpdateParticipantsData(Combat info)
        {
            SelectedCombat = info;
            var entitiesToView = ViewEnemies ? info.AllEntities.Values.Where(e => e.IsBoss || e.IsCharacter).ToList() : info.CharacterParticipants.Values;
            var damage = info.EDPS;
            var healing = info.EHPS;
            var incoming = info.EDTPS;
            var desired = new List<ParticipantViewModel>();
            foreach (var participant in entitiesToView)
            {
                var participantViewModel = AvailableParticipants.FirstOrDefault(p => IsSameEntity(p.Entity, participant));
                if (participantViewModel == null)
                {
                    participantViewModel = GenerateInstance(participant);
                    participantViewModel.SelectionChanged += SelectParticipant;
                }
                participantViewModel.Entity = participant;
                participantViewModel.PlayerName = participant.Name;
                participantViewModel.IsLocalPlayer = participant.IsLocalPlayer;
                var imagePath = IconFactory._unknownIcon;
                participantViewModel.RoleOrdering = 3;
                if (info.CharacterClases.TryGetValue(participant, out var swtorClass) && swtorClass != null)
                {
                    imagePath = GetRoleImage(swtorClass);
                    participantViewModel.RoleOrdering = swtorClass.Role == Role.Tank ? 0 : swtorClass.Role == Role.Healer ? 1 : 2;
                }
                participantViewModel.SetValues(damage.GetValueOrDefault(participant), healing.GetValueOrDefault(participant), incoming.GetValueOrDefault(participant), imagePath);
                desired.Add(participantViewModel);
            }
            desired = desired.OrderBy(p => p.RoleOrdering).ThenBy(p => p.PlayerName).ToList();
            for (var i = AvailableParticipants.Count - 1; i >= 0; i--)
                if (!desired.Contains(AvailableParticipants[i]))
                {
                    AvailableParticipants[i].SelectionChanged -= SelectParticipant;
                    AvailableParticipants.RemoveAt(i);
                }
            for (var i = 0; i < desired.Count; i++)
            {
                var oldIndex = AvailableParticipants.IndexOf(desired[i]);
                if (oldIndex < 0) AvailableParticipants.Insert(i, desired[i]);
                else if (oldIndex != i) AvailableParticipants.Move(oldIndex, i);
            }
            SelectedParticipant = AvailableParticipants.FirstOrDefault(p => p.IsSelected)?.Entity;
            UpdateLayout();
            if (!AvailableParticipants.Any(p => p.IsSelected))
            {
                SelectLocalPlayer();
            }
            return entitiesToView;
        }

        private static bool IsSameEntity(Entity left, Entity right)
        {
            if (left == null || right == null)
                return false;
            if (left.LogId != 0 && right.LogId != 0)
                return left.LogId == right.LogId;
            if (left.Id != 0 && right.Id != 0)
                return left.Id == right.Id;
            return left.Name == right.Name;
        }

        private Bitmap GetRoleImage(SWTORClass sWTORClass)
        {
            if (sWTORClass == null)
                return IconFactory._unknownIcon;
            if (RoleImages.TryGetValue(sWTORClass.Role, out var image)) return image;
            var name = sWTORClass.Role switch { Role.DPS => "dpsIcon", Role.Healer => "healingIcon", Role.Tank => "tankIcon", _ => null };
            if (name == null) return IconFactory._unknownIcon;
            return RoleImages[sWTORClass.Role] = new Bitmap(AssetLoader.Open(new Uri($"avares://Orbs/resources/{name}.png")));
        }
    }
}
