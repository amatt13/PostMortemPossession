using System;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Missions;
using TaleWorlds.MountAndBlade.ViewModelCollection;

namespace PostMortemPossession
{
    public class PostMortemPossession : MBSubModuleBase
    {
        private const int NUMBER_OF_PRIORITY_GROUPS = 9;

        private bool _allowControlAllies { get; set; }
        private bool _allowControlFormations { get; set; }
        private bool _muteExceptions { get; set; }
        private bool _verbose { get; set; }
        private InputKey _manualControlHotkey { get; set; }
        private InputKey _autoControlHotkey { get; set; }
        private Color? _messageColor { get; set; }
        private Color? _errorColor { get; set; }
        private int[] _autoSelectPriority { get; set; }
        private bool _autoSelectRandomWithinPriority { get; set; }
        private PriorityHelper _priorityHelper { get; set; }
        private string _upstartErrors { get; set; }

        private Agent _player { get; set; }
        private Team _playerTeam { get; set; }
        private Mission _mission { get; set; }

        protected override void OnSubModuleLoad()
        {
            _upstartErrors = String.Empty;

            var optionsFile = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly()?.Location) + @"\PostMortemPossession_options.json";
            if (File.Exists(optionsFile))
            {
                try
                {
                    var jobject = JObject.Parse(File.ReadAllText(optionsFile));

                    // bools
                    _allowControlAllies = (bool)jobject.Property("allowControlAllies").Value;
                    _allowControlFormations = (bool)jobject.Property("allowControlFormations").Value;
                    _muteExceptions = (bool)jobject.Property("muteExceptions").Value;
                    _verbose = (bool)jobject.Property("verbose").Value;
                    _autoSelectRandomWithinPriority = (bool)jobject.Property("autoSelectRandomWithinPriority").Value;

                    // hotkeys
                    var manualControlHotkeyString = (string)jobject.Property("hotKey").Value;
                    if (!String.IsNullOrEmpty(manualControlHotkeyString))
                    {
                        if (Enum.TryParse(manualControlHotkeyString, out InputKey tempKey)) 
                        {
                            _manualControlHotkey = tempKey;
                        }
                        else
                        {
                            _upstartErrors += $"Could not read 'hotKey'. Value '{ manualControlHotkeyString }'{ Environment.NewLine }Using default key 'O' instead" + Environment.NewLine;
                            _manualControlHotkey = InputKey.O;
                        }
                    }
                    var autoSelectPriorityHotKeyString = (string)jobject.Property("autoSelectPriorityHotKey").Value;
                    if (!String.IsNullOrEmpty(autoSelectPriorityHotKeyString))
                    {
                        if (Enum.TryParse(autoSelectPriorityHotKeyString, out InputKey tempKey))
                        {
                            _autoControlHotkey = tempKey;
                        }
                        else
                        {
                            _upstartErrors += $"Could not read 'autoSelectPriorityHotKey'. Value '{ autoSelectPriorityHotKeyString }'{ Environment.NewLine }Using default key 'U' instead" + Environment.NewLine;
                            _autoControlHotkey = InputKey.U;
                        }
                    }

                    // containers
                    var messageColorArray = jobject.Property("messageColor").Value.ToObject<int[]>();
                    if (messageColorArray.Length == 3 && messageColorArray.All(v => v >= 0 && v <= 255))
                        _messageColor = new Color(messageColorArray[0], messageColorArray[1], messageColorArray[2]);
                    else
                        _upstartErrors += "Could not read 'messageColor'. Invalid format or values. Must follow this pattern '[R, G, B]' where R, G, & B are values 0 trough 255" + Environment.NewLine;

                    var errorColorArray = jobject.Property("errorColor").Value.ToObject<int[]>();
                    if (errorColorArray.Length == 3 && errorColorArray.All(v => v >= 0 && v <= 255))
                        _errorColor = new Color(errorColorArray[0], errorColorArray[1], errorColorArray[2]);
                    else
                        _upstartErrors += "Could not read 'errorColor'. Invalid format or values. Must follow this pattern '[R, G, B]' where R, G, & B are values 0 trough 255" + Environment.NewLine;

                    var autoSelectPriorityArray = jobject.Property("autoSelectPriority").Value.ToObject<int[]>();
                    if (autoSelectPriorityArray.Length == NUMBER_OF_PRIORITY_GROUPS)
                    {
                        _autoSelectPriority = autoSelectPriorityArray;
                    }
                    else
                        _upstartErrors += "Could not read 'autoSelectPriority'. Invalid format. Must follow this pattern '[number, number, number, number, number, number, number]'" + Environment.NewLine;
                }
                catch (Exception e)
                {
                    _upstartErrors += $"Unable to read content of options file: '{ e.Message }'" + Environment.NewLine;
                }
            }
            else
            {
                _upstartErrors += $"PostMortemPossession: Unable to open (or find) options file: { optionsFile }" + Environment.NewLine;
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            if (!String.IsNullOrEmpty(_upstartErrors))
            {
                PrintError(_upstartErrors);
                _upstartErrors = String.Empty;
            }
        }

        public override void OnMissionBehaviourInitialize(Mission pMission)
        {
            base.OnMissionBehaviourInitialize(pMission);
            _mission = pMission;
            _player = null;
            if (_autoSelectPriority != null)
                _priorityHelper = new PriorityHelper(_autoSelectPriority, _autoSelectRandomWithinPriority);
        }

        protected override void OnApplicationTick(float dt)
        {
            try
            {
                if (Game.Current == null || _mission == null || _mission.Scene == null || Game.Current.CurrentState != Game.State.Running)
                    return;

                // Set player agent and team
                if (Agent.Main != null && Agent.Main.Index != (_player?.Index).GetValueOrDefault(-1))
                {
                    _player = Agent.Main;
                    _playerTeam = Agent.Main.Team;
                }
                else if (_player != null && _player.Health <= 0.0 && _playerTeam != null)
                {
                    if (Input.IsKeyPressed(_manualControlHotkey))
                    {
                        ManualControl();
                    }
                    else if (Input.IsKeyPressed(_autoControlHotkey))
                        AutomaticControl();
                }
            }
            catch (Exception e)
            {
                PrintException($"PostMortemPossession: An exception was thrown: '{ e.Message }'");
            }
        }

        protected void ManualControl()
        {
            MissionView missionView = (MissionView)_mission?.MissionBehaviours.Where(mb => mb is MissionView && (mb as MissionView).MissionScreen?.LastFollowedAgent != null).FirstOrDefault();
            if (missionView != null)
            {
                var lastFollowedAgent = missionView.MissionScreen.LastFollowedAgent;
                if (lastFollowedAgent.Health > 0.0f && lastFollowedAgent.Team != null)
                {
                    if (lastFollowedAgent.Team.IsPlayerTeam)
                    {
                        // take control of party soldier
                        TakeControlOfFriendlyAgent(lastFollowedAgent);
                    }
                    else if (lastFollowedAgent.Team.IsFriendOf(_playerTeam))
                    {
                        // attempt to take control of ally soldier
                        if (_allowControlAllies)
                        {
                            TakeControlOfFriendlyAgent(lastFollowedAgent);
                        }
                        else
                            PrintInformation($"You can't take control of ally '{ lastFollowedAgent.Name }'");
                    }
                    else if (lastFollowedAgent.Team.IsEnemyOf(_playerTeam))
                    {
                        // inform player that they can't take control of enemy soldiers
                        PrintInformation($"You can't take control of enemy '{ lastFollowedAgent.Name }'");
                    }
                }
            }
        }

        protected void AutomaticControl()
        {
            if (_priorityHelper != null)
            {
                var priorities = _priorityHelper.GetHigestPriorityOrder();
                var companions = _playerTeam.ActiveAgents.Where(a => a.IsHero);
                var alliedTeam = _mission.PlayerAllyTeam;

                Agent nextAgent = null;

                foreach (var priorityUnitCategory in priorities)
                {
                    if (_playerTeam.Formations.Count() > 0)
                    {
                        if (priorityUnitCategory == PriorityHelper.UnitCategory.Companions)
                        {
                            nextAgent = companions.Where(a => a.Health > 0.0f).FirstOrDefault();
                        }
                        else
                        {
                            nextAgent = _playerTeam.GetFormation((FormationClass)priorityUnitCategory).GetFirstUnit();
                            if (nextAgent == null)
                            {
                                if (_allowControlAllies && alliedTeam != null)
                                    nextAgent = alliedTeam.GetFormation((FormationClass)priorityUnitCategory).GetFirstUnit();
                            }
                        }
                    }
                    else
                    {
                        // pick the healtiest character when in an instance where no formations are available
                        nextAgent = _playerTeam.ActiveAgents.OrderByDescending(a => a.Health).FirstOrDefault();
                    }

                    if (nextAgent != null)
                    {
                        TakeControlOfFriendlyAgent(nextAgent);
                        PrintInformation($"Soldier found in group { priorityUnitCategory }");
                        return;
                    }
                }

                PrintInformation("No friendly eligible soldiers left");
            }
        }

        protected void TakeControlOfFriendlyAgent(Agent pAgent)
        {
            if (pAgent != null)
            {
                PrintInformation($"You are now controlling '{ pAgent.Name }'");
                if (_allowControlFormations)
                    TakeControlOfPlayersFormations(pAgent);
                pAgent.Controller = Agent.ControllerType.Player;
                _player = pAgent;
                _player.SetMaximumSpeedLimit(10000000, false);

                // reset "FastForward" in case the player activated FastForward from the scoreboard
                _mission.SetFastForwardingFromUI(false);

                var battleObserverMissionLogic = _mission.GetMissionBehaviour<BattleObserverMissionLogic>();
                var scoreBoard = (battleObserverMissionLogic?.BattleObserver as ScoreboardVM);
                if (scoreBoard != null)
                {
                    // do not display the "you are dead" message at the bottom of the screen
                    scoreBoard.IsMainCharacterDead = false;  
                }
            }
        }

        private void TakeControlOfPlayersFormations(Agent pNewOwner)
        {
            if (_playerTeam is null || pNewOwner is null)
                return;

            if (_playerTeam.PlayerOrderController.Owner == _player)
                _playerTeam.PlayerOrderController.Owner = pNewOwner;

            _playerTeam.FormationsIncludingEmpty.Where(f => f != null && f.PlayerOwner == _player).ToList().ForEach(pf => pf.PlayerOwner = pNewOwner);
        }

        #region output

        protected void PrintInformation(string pMessage)
        {
            var color = _messageColor != null ? _messageColor.Value : new Color(128, 128, 128);
            if (_verbose && !String.IsNullOrEmpty(pMessage))
                InformationManager.DisplayMessage(new InformationMessage($"PostMortemPossession: { pMessage }", color));
        }

        protected void PrintError(string pMessage)
        {
            var color = _errorColor != null ? _errorColor.Value : new Color(128, 0, 0);
            if (_verbose && !String.IsNullOrEmpty(pMessage))
                InformationManager.DisplayMessage(new InformationMessage($"PostMortemPossession: { pMessage }", color));
        }

        protected void PrintException(string pMessage)
        {
            var color = _errorColor != null ? _errorColor.Value : new Color(128, 0, 0);
            if (!_muteExceptions && !String.IsNullOrEmpty(pMessage))
                InformationManager.DisplayMessage(new InformationMessage($"PostMortemPossession: { pMessage }", color));
        }

        #endregion
    }
}
