using Newtonsoft.Json;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;

namespace SWTORCombatParser.ViewModels.Raiding
{
    public class RaidInfo
    {
        public string Name { get; set; }
        public string Password { get; set; }
        public Guid GroupId { get; set; }
    }
    public class RaidSelectionViewModel : INotifyPropertyChanged
    {
        private List<RaidInfo> _raidGroupInfo = new List<RaidInfo>();
        private string _raidInfoFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DubaTech", "SWTORCombatParser", "raidgroup_info.json");
        private PostgresConnection _databaseConnection;
        private RaidInfo selectedRaidGroup;

        public RaidSelectionViewModel()
        {
            if (!Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DubaTech", "SWTORCombatParser")))
            {
                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DubaTech", "SWTORCombatParser"));
            }
            _databaseConnection = new PostgresConnection();
            BuildAvailableList();
        }
        public event Action<bool, RaidInfo> RaidingStateChanged = delegate { };
        public ObservableCollection<RaidInfo> AvailableRaidGroups { get; set; } = new ObservableCollection<RaidInfo>();
        public RaidInfo SelectedRaidGroup
        {
            get => selectedRaidGroup; set
            {
                selectedRaidGroup = value;
                RaidingStateChanged(true, value);
            }
        }
        public string RaidGroupName { get; set; }
        public string RaidGroupPassword { get; set; }
        public Guid GroupId { get; set; }

        public ICommand CancelRaiding => new CommandHandler(Cancel);

        private void Cancel()
        {
            RaidingStateChanged(false, null);
        }

        public ICommand CreateNewGroup => new CommandHandler(NewGroup);

        private void NewGroup()
        {
            var valid = _databaseConnection.AddNewRaidGroup(RaidGroupName, RaidGroupPassword);

            ValidateGroup(valid.Item1, valid.Item2);
        }

        public ICommand AddExisitingGroup => new CommandHandler(AddGroup);

        private void AddGroup()
        {
            if (_raidGroupInfo.Any(ri => ri.Name == RaidGroupName))
            { 
                Refresh();
                return;
            }
            var valid = _databaseConnection.ValidateGroupInfo(RaidGroupName, RaidGroupPassword);
            ValidateGroup(valid.Item1, valid.Item2);
        }
        private void ValidateGroup(bool valid, Guid groupId)
        {
            
            if (valid)
            {
                GroupId = groupId;
                UpdateGroups();
            }
            else
            {
                Trace.WriteLine("Wrong!!!");
            }
        }
        private void UpdateGroups()
        {
            var addedInfo = new RaidInfo { Name = RaidGroupName, Password = RaidGroupPassword ,GroupId = GroupId};
            _raidGroupInfo.Add(addedInfo);

            Refresh();

            SelectedRaidGroup = AvailableRaidGroups.First(ri=>ri.Name == addedInfo.Name);
            OnPropertyChanged("SelectedRaidGroup");
            
        }
        private void Refresh()
        {
            RaidGroupName = "";
            RaidGroupPassword = "";
            OnPropertyChanged("RaidGroupName");
            OnPropertyChanged("RaidGroupPassword");
            SaveStateToFile();
            BuildAvailableList();
        }
        private void SaveStateToFile()
        {
            if (!File.Exists(_raidInfoFilePath))
            {
                File.Create(_raidInfoFilePath).Close();
            }
            var stateString = JsonConvert.SerializeObject(_raidGroupInfo);
            File.WriteAllText(_raidInfoFilePath, stateString);
        }
        private void BuildAvailableList()
        {
            AvailableRaidGroups.Clear();
            if (!File.Exists(_raidInfoFilePath))
            {
                File.Create(_raidInfoFilePath).Close();
            }
            var raidInfoString = File.ReadAllText(_raidInfoFilePath);
            _raidGroupInfo = JsonConvert.DeserializeObject<List<RaidInfo>>(raidInfoString);
            if (_raidGroupInfo == null || _raidGroupInfo.Count == 0)
            {
                _raidGroupInfo = new List<RaidInfo>();
                return;
            }

            _raidGroupInfo = _raidGroupInfo.OrderBy(ri => ri.Name).ToList();
            _raidGroupInfo.ForEach(ri => AvailableRaidGroups.Add(ri));
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
