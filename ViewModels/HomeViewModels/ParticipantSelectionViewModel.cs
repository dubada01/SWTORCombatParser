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
                viewEnemies = value;
                UpdateParticipantsData(SelectedCombat);
                var entitiesToShow = ViewEnemies ? SelectedCombat.AllEntities.Where(e => e.IsBoss || e.IsCharacter).ToList() : SelectedCombat.CharacterParticipants;
                ViewEnemiesToggled(entitiesToShow.Count);
                if (!viewEnemies && SelectedParticipant.IsBoss)
                    SetSelection(entitiesToShow.First(e => e.IsLocalPlayer));
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
            if (AvailableParticipants.Count <= 8)
            {
                Columns = 8;
                Rows = 1;
            }
            if (AvailableParticipants.Count > 8)
            {
                Columns = 8;
                Rows = 2;
            }
        }

        private void SelectParticipant(ParticipantViewModel obj, bool isSelected)
        {
            if (isSelected)
                SetSelection(obj.Entity);
        }
        private void SetSelection(Entity obj)
        {
            if (!AvailableParticipants.Any(part => part.Entity == obj))
                return;
            SelectedParticipant = obj;
            var previouslySelected = AvailableParticipants.Where(p => p.Entity.Id != obj.Id).FirstOrDefault(p => p.IsSelected);
            if (previouslySelected != null)
                previouslySelected.ToggleSelection();
            var currentSelection = AvailableParticipants.First(p => p.Entity.Id == obj.Id);
            if (!currentSelection.IsSelected)
                currentSelection.ToggleSelection();
            ParticipantSelected(obj);
            ParticipantSelectionHandler.UpdateSelection(obj);
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
        public List<Entity> UpdateParticipantsData(Combat info)
        {
            SelectedCombat = info;
            var entitiesToView = ViewEnemies ? info.AllEntities.Where(e => e.IsBoss || e.IsCharacter).ToList() : info.CharacterParticipants;
            var names = entitiesToView.Select(e => e.Name);
            if (AvailableParticipants.Count() == names.Count() && AvailableParticipants.All(ap => names.Contains(ap.PlayerName))) return entitiesToView;
            AvailableParticipants.Clear();
            foreach (var participant in entitiesToView)
            {
                ParticipantViewModel participantViewModel = GenerateInstance(participant);
                participantViewModel.SelectionChanged += SelectParticipant;
                var imagePath = IconFactory._unknownIcon;
                if (participant.IsCompanion)
                    imagePath = new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/LocalPlayerIcon.png")));
                if (info.CharacterClases.ContainsKey(participant))
                {
                    var swtorClass = info.CharacterClases[participant];
                    imagePath = GetRoleImage(swtorClass);
                    participantViewModel.RoleOrdering = swtorClass.Role == Role.Tank ? 0 : swtorClass.Role == Role.Healer ? 1 : 2;
                }
                participantViewModel.SetValues(info.EDPS[participant], info.EHPS[participant], info.EDTPS[participant], imagePath);
                AvailableParticipants.Add(participantViewModel);
            }
            AvailableParticipants = new ObservableCollection<ParticipantViewModel>(AvailableParticipants.OrderBy(p => p.RoleOrdering));
            UpdateLayout();
            if (ParticipantSelectionHandler.CurrentlySelectedParticpant == null)
            {
                SelectLocalPlayer();
            }
            return entitiesToView;
        }

        private Bitmap GetRoleImage(SWTORClass sWTORClass)
        {
            if (sWTORClass == null)
                return IconFactory._unknownIcon;
            switch (sWTORClass.Role)
            {
                case Role.DPS:
                    return new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/dpsIcon.png")));
                case Role.Healer:
                    return new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/healingIcon.png")));
                case Role.Tank:
                    return new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/tankIcon.png")));
                default:
                    return IconFactory._unknownIcon;
            }
        }
    }
}
