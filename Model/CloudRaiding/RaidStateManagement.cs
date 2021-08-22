using MoreLinq;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Raiding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class StaticRaidInfo
    {
        public static event Action NewRaidCombatStarted = delegate { };
        public static void FireNewRaidCombatEvent()
        {
            NewRaidCombatStarted();
        }
    }
    public class RaidStateManagement
    {
        private DateTime _mostRecentLog;
        private DateTime _timeJoined;
        private Guid _currentRaidGroup;
        private bool _raidingActive;
        private PostgresConnection _postgresConnection;
        private bool combatEnding;
        private bool combatStarted;
        private List<RaidParticipantInfo> _currentParticipants = new List<RaidParticipantInfo>();

        public RaidStateManagement()
        {
            _postgresConnection = new PostgresConnection();
        }
        public event Action<List<RaidParticipantInfo>> UpdatedParticipants = delegate { };
        public event Action CombatFinished = delegate { };
        public void StopRaiding()
        {
            _raidingActive = false;
        }

        public void StartRaiding(Guid groupId)
        {
            _currentRaidGroup = groupId;
            _raidingActive = true;
            _mostRecentLog = GetMostRecentLog();
            _timeJoined = DateTime.Now;
            _currentParticipants = new List<RaidParticipantInfo>();
            StartKeepAlive();
            CheckForOngoingCombat();
            Task.Run(() =>
            {
                while (_raidingActive)
                {
                    Thread.Sleep(1000);
                    if (!_raidingActive)
                        return;
                    var logs = _postgresConnection.GetLogsAfterTime(_mostRecentLog, _currentRaidGroup);
                    if (!logs.Any())
                        continue;
                    ParseNewRaidLogs(logs);
                }
            });
        }
        private void StartKeepAlive()
        {
            Task.Run(() =>
            {
                while (_raidingActive)
                {
                    UploadKeepAlive();
                    AddMembers();
                    Thread.Sleep(2000);
                    if (!_raidingActive)
                        return;
                }
            });
        }
        private void AddMembers()
        {
            var currentMembers = GetCurrentlyAliveMembers();
            foreach (var member in currentMembers)
            {
                TryAddNewParticipant(member.Item2, member.Item1);
            }

        }
        private List<(string, string)> GetCurrentlyAliveMembers()
        {
            return _postgresConnection.CheckForKeepAlivesInGroupFromTime(_currentRaidGroup, _timeJoined);
        }
        private void CheckForOngoingCombat()
        {
            var cloudLogsFrom10Mins = _postgresConnection.GetLogsFromLast10Mins(_currentRaidGroup).OrderBy(l => l.TimeStamp).ToList();
            CheckForPersonalOngoingCombat(cloudLogsFrom10Mins);

            var lastCombatEndedLog = cloudLogsFrom10Mins.LastOrDefault(l => l.Ability == "SWTOR_PARSING_COMBAT_END");
            if (lastCombatEndedLog == null)
                return;
            var timeToCheck = lastCombatEndedLog == null ? cloudLogsFrom10Mins.First().TimeStamp : lastCombatEndedLog.TimeStamp;
            var combatInProgress = cloudLogsFrom10Mins.Any(l => l.Effect.EffectName == "EnterCombat" && l.TimeStamp > timeToCheck);
            if (combatInProgress)
            {
                var mostRecentCombat = cloudLogsFrom10Mins.First(l => l.Effect.EffectName == "EnterCombat" && l.TimeStamp > timeToCheck);
                var logsSinceOngoingCombatStart = _postgresConnection.GetLogsAfterTime(mostRecentCombat.TimeStamp, _currentRaidGroup);
                ParseNewRaidLogs(logsSinceOngoingCombatStart);
            }
        }
        private void CheckForPersonalOngoingCombat(List<ParsedLogEntry> cloudLogs)
        {
            var logsForFile = CombatLogParser.ParseLast10Mins(CombatLogLoader.LoadMostRecentLog());
            if (logsForFile.Count == 0)
                return;
            var lastCombatEndedLog = logsForFile.LastOrDefault(l => l.Effect.EffectName == "ExitCombat" || (l.Effect.EffectName == "Death" && l.Target.IsPlayer));
            var timeToCheck = lastCombatEndedLog == null ? logsForFile.First().TimeStamp : lastCombatEndedLog.TimeStamp;
            var combatInProgress = logsForFile.Any(l => l.Effect.EffectName == "EnterCombat" && l.TimeStamp > timeToCheck);
            if (combatInProgress)
            {
                var mostRecentCombat = logsForFile.First(l => l.Effect.EffectName == "EnterCombat" && l.TimeStamp > timeToCheck);
                var logsToUpload = logsForFile.Where(l => l.TimeStamp > timeToCheck);
                var logsNotYetInRaidGroup = logsToUpload.Where(
                    l =>
                    l.TimeStamp > timeToCheck &&
                    cloudLogs.Any(cl => cl.TimeStamp != l.TimeStamp) &&
                    cloudLogs.Any(cl => cl.Effect.EffectName != l.Effect.EffectName) &&
                    cloudLogs.Any(cl => cl.Ability != l.Ability)
                    );
                Parallel.ForEach(logsNotYetInRaidGroup, log =>
                {
                    _postgresConnection.AddLog(_currentRaidGroup, log);
                });
            }
        }
        private void ParseNewRaidLogs(List<ParsedLogEntry> logs)
        {
            var ordered = logs.OrderBy(t => t.TimeStamp);

            CheckForCombatStart(ordered);
            CheckForCombatEnd(ordered);
            var startTime = ordered.FirstOrDefault(l => l.Effect.EffectName == "EnterCombat")?.TimeStamp;
            var participantData = ordered.GroupBy(l => l.LogName);
            foreach (var participantFile in participantData)
            {
                var logName = participantFile.Key;
                var participantLogs = participantFile.ToList();
                if (startTime.HasValue)
                    participantLogs.Insert(0, ParsedLogEntry.GetDummyLog("StartCombatMarker", startTime.Value, -1));
                var participant = _currentParticipants.FirstOrDefault(p => p.LogName == logName);
                if (participant == null)
                {
                    TryAddNewParticipant(logName, _postgresConnection.GetMostRecentInfoFromLogName(_currentRaidGroup, logName));
                    var secondTry = _currentParticipants.FirstOrDefault(p => p.LogName == logName);
                    secondTry?.Update(participantLogs);
                }
                if (participant != null)
                    participant.Update(participantLogs);

            }
            RaidGroupMetaData.UpdateRaidGroupMetaData(ref _currentParticipants);
            UpdatedParticipants(_currentParticipants);
            if (combatEnding)
            {
                _currentParticipants.ForEach(r => r.FinishCombat());

                CombatFinished();
            }
        }

        private void TryAddNewParticipant(string logName, string info)
        {

            if (!_currentParticipants.Any(p => p.LogName == logName))
            {
                var newParticipant = new RaidParticipantInfo(new List<ParsedLogEntry>(), logName);

                newParticipant.PlayerName = info.Split("~?~", StringSplitOptions.None)[1];
                newParticipant.PlayerRole = Enum.Parse<Role>(info.Split("~?~", StringSplitOptions.None)[2]);

                _currentParticipants.Add(newParticipant);

            }
            else
            {
                var playerToUpdate = _currentParticipants.First(p => p.LogName == logName);
                playerToUpdate.PlayerName = info.Split("~?~", StringSplitOptions.None)[1];
                playerToUpdate.PlayerRole = Enum.Parse<Role>(info.Split("~?~", StringSplitOptions.None)[2]);
            }
            UpdatedParticipants(_currentParticipants);
        }

        private void CheckForCombatEnd(IOrderedEnumerable<ParsedLogEntry> ordered)
        {
            if (ordered.Any(c => c.Ability == "SWTOR_PARSING_COMBAT_END"))
            {
                combatStarted = false;
                combatEnding = true;
                _mostRecentLog = ordered.Last().TimeStamp;
            }
        }

        private void CheckForCombatStart(IOrderedEnumerable<ParsedLogEntry> logs)
        {
            if (logs.Any(c => c.Effect.EffectName == "EnterCombat") && !combatStarted)
            {
                combatEnding = false;
                combatStarted = true;
                App.Current.Dispatcher.Invoke(() =>
                {
                    var aliveMembers = GetCurrentlyAliveMembers();
                    _currentParticipants.RemoveAll(cm => !aliveMembers.Any(am => am.Item2 == cm.LogName));
                    _currentParticipants.ForEach(r => r.ResetCombat());
                    UpdatedParticipants(_currentParticipants);
                });
                StaticRaidInfo.FireNewRaidCombatEvent();
            }
        }
        private DateTime GetMostRecentLog()
        {
            var mostRecentLog = CombatLogLoader.LoadMostRecentLog();
            var startEvents = CombatLogParser.GetAllCombatStartEvents(mostRecentLog);
            if (startEvents.Count == 0)
            {
                return DateTime.Now;
            }
            return startEvents.MinBy(v => v.TimeStamp).First().TimeStamp;
        }
        private Role _currentRole;
        private string _characterName;
        private void UploadKeepAlive()
        {
            if (_currentRole == Role.Unknown || (_characterName == null || _characterName == "Unknown_Player"))
            {
                var mostRecentLog = CombatLogLoader.LoadMostRecentLog();

                List<ParsedLogEntry> loadedLogs = null;
                //_currentRole = CombatLogStateBuilder.GetPlayerRole(attemptedName);
                var currentState = CombatLogStateBuilder.CurrentState;
                if (string.IsNullOrEmpty(currentState.PlayerName))
                {
                    loadedLogs = CombatLogParser.ParseLast10Mins(mostRecentLog);
                    _characterName = CombatLogStateBuilder.GetPlayerName(loadedLogs);
                }
                else
                    _characterName = currentState.PlayerName;
                if(currentState.PlayerClass == null)
                    _currentRole = CombatLogStateBuilder.GetPlayerRole(loadedLogs == null?CombatLogParser.ParseLast10Mins(mostRecentLog): loadedLogs);
                else
                    _currentRole = CombatLogStateBuilder.CurrentState.PlayerClass.Role;
                _postgresConnection.UploadMemberKeepAlive(_currentRaidGroup, "HELLO~?~" + _characterName + "~?~" + _currentRole.ToString(), mostRecentLog.Name);
            }
        }
    }
}
