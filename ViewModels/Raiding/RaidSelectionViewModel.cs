//using Newtonsoft.Json;
//using SWTORCombatParser.Model.CloudRaiding;
//using SWTORCombatParser.Utilities;
//using System;
//using System.Collections.Generic;
//using System.Collections.ObjectModel;
//using System.ComponentModel;
//using System.Diagnostics;
//using System.IO;
//using System.Linq;
//using System.Runtime.CompilerServices;
//using System.Text;
//using System.Windows.Input;

//namespace SWTORCombatParser.ViewModels.Raiding
//{
//    public class RaidGroupInfo:INotifyPropertyChanged
//    {
//        public string Name { get; set; }
//        public string DPSLeaderName { get; set; }
//        public string DPSLeaderValue { get; set; }
//        public string EHPSLeaderName { get; set; }
//        public string EHPSLeaderValue { get; set; }
//        public void SetLeaders(string dpsLeader, string dpsVal, string ehpsLeader, string ehpsVal)
//        {
//            DPSLeaderName = dpsLeader;
//            DPSLeaderValue = dpsVal;
//            EHPSLeaderName = ehpsLeader;
//            EHPSLeaderValue = ehpsVal;
//            OnPropertyChanged("DPSLeaderName");
//            OnPropertyChanged("DPSLeaderValue");
//            OnPropertyChanged("EHPSLeaderName");
//            OnPropertyChanged("EHPSLeaderValue");
//        }
//        public string Password { get; set; }
//        public Guid GroupId { get; set; }
//        public event PropertyChangedEventHandler PropertyChanged;
//        protected void OnPropertyChanged([CallerMemberName] string name = null)
//        {
//            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
//        }
//    }
//    public class RaidSelectionViewModel : INotifyPropertyChanged
//    {
//        private List<RaidGroupInfo> _raidGroupInfo = new List<RaidGroupInfo>();
//        private string _raidInfoFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DubaTech", "SWTORCombatParser", "raidgroup_info.json");
//        private PostgresConnection _databaseConnection;
//        private RaidGroupInfo selectedRaidGroup;

//        public RaidSelectionViewModel()
//        {
//            if (!Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DubaTech", "SWTORCombatParser")))
//            {
//                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DubaTech", "SWTORCombatParser"));
//            }
//            _databaseConnection = new PostgresConnection();
//            BuildAvailableList();
//        }
//        public event Action<bool, RaidGroupInfo> RaidingStateChanged = delegate { };
//        public ObservableCollection<RaidGroupInfo> AvailableRaidGroups { get; set; } = new ObservableCollection<RaidGroupInfo>();
//        public RaidGroupInfo SelectedRaidGroup
//        {
//            get => selectedRaidGroup; set
//            {
//                selectedRaidGroup = value;
//                if (selectedRaidGroup == null)
//                    return;
//                RaidingStateChanged(true, value);
//            }
//        }
//        public string RaidGroupName { get; set; }
//        public string RaidGroupPassword { get; set; }
//        public Guid GroupId { get; set; }

//        public void Cancel()
//        {
//            SelectedRaidGroup = null;
//            RaidingStateChanged(false, null);
//            OnPropertyChanged("SelectedRaidGroup");
//        }

//        public ICommand CreateNewGroup => new CommandHandler(NewGroup);

//        private void NewGroup(object test)
//        {
//            var valid = _databaseConnection.AddNewRaidGroup(RaidGroupName, RaidGroupPassword);

//            ValidateGroup(valid.Item1, valid.Item2);
//        }

//        public ICommand AddExisitingGroup => new CommandHandler(AddGroup);

//        private void AddGroup(object test)
//        {
//            if (_raidGroupInfo.Any(ri => ri.Name == RaidGroupName))
//            { 
//                Refresh();
//                return;
//            }
//            var valid = _databaseConnection.ValidateGroupInfo(RaidGroupName, RaidGroupPassword);
//            ValidateGroup(valid.Item1, valid.Item2);
//        }
//        private void ValidateGroup(bool valid, Guid groupId)
//        {
            
//            if (valid)
//            {
//                GroupId = groupId;
//                UpdateGroups();
//            }
//            else
//            {
//                Trace.WriteLine("Wrong!!!");
//            }
//        }
//        private void UpdateGroups()
//        {
//            var addedInfo = new RaidGroupInfo { Name = RaidGroupName, Password = RaidGroupPassword ,GroupId = GroupId};
//            _raidGroupInfo.Add(addedInfo);

//            Refresh();

//            SelectedRaidGroup = AvailableRaidGroups.First(ri=>ri.Name == addedInfo.Name);
//            OnPropertyChanged("SelectedRaidGroup");
            
//        }
//        private void Refresh()
//        {
//            RaidGroupName = "";
//            RaidGroupPassword = "";
//            OnPropertyChanged("RaidGroupName");
//            OnPropertyChanged("RaidGroupPassword");
//            SaveStateToFile();
//            BuildAvailableList();
//        }
//        private void SaveStateToFile()
//        {
//            if (!File.Exists(_raidInfoFilePath))
//            {
//                File.Create(_raidInfoFilePath).Close();
//            }
//            var stateString = JsonConvert.SerializeObject(_raidGroupInfo);
//            File.WriteAllText(_raidInfoFilePath, stateString);
//        }
//        private void BuildAvailableList()
//        {
//            AvailableRaidGroups.Clear();
//            if (!File.Exists(_raidInfoFilePath))
//            {
//                File.Create(_raidInfoFilePath).Close();
//            }
//            var raidInfoString = File.ReadAllText(_raidInfoFilePath);
//            _raidGroupInfo = JsonConvert.DeserializeObject<List<RaidGroupInfo>>(raidInfoString);
//            if (_raidGroupInfo == null || _raidGroupInfo.Count == 0)
//            {
//                _raidGroupInfo = new List<RaidGroupInfo>();
//                return;
//            }

//            _raidGroupInfo = _raidGroupInfo.OrderBy(ri => ri.Name).ToList();
//            _raidGroupInfo.ForEach(ri => AvailableRaidGroups.Add(ri));
//        }
//        public event PropertyChangedEventHandler PropertyChanged;
//        protected void OnPropertyChanged([CallerMemberName] string name = null)
//        {
//            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
//        }
//    }
//}
