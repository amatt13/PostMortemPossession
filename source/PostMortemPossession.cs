using System;
using System.Linq;
using System.IO;
using SandBox.GauntletUI.Missions;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade.View.Missions;
using TaleWorlds.MountAndBlade.ViewModelCollection;
using TaleWorlds.MountAndBlade.GauntletUI;
using TaleWorlds.Library;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PostMortemPossession
{
    public class PostMortemPossession : MBSubModuleBase
    {
        private bool _allowControlAllies { get; set; }   
        private bool _muteExceptions { get; set; }
        private bool _verbose { get; set; }
        private InputKey _hotkey { get; set; }
        private Color? _messageColor { get; set; }
        private Color? _errorColor { get; set; }

        private Agent _player { get; set; }
        private Team _playerTeam { get; set; }
        private Mission _mission { get; set; }

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            var optionsFile = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly()?.Location) + @"\PostMortemPossession_options.json";
            if (File.Exists(optionsFile))
            {
                try
                {
                    var jobject = JObject.Parse(File.ReadAllText(optionsFile));
                    this._allowControlAllies = (bool)jobject.Property("allowControlAllies").Value;
                    this._muteExceptions = (bool)jobject.Property("muteExceptions").Value;
                    this._verbose = (bool)jobject.Property("verbose").Value;

                    var hotkeyString = (string)jobject.Property("hotKey").Value;
                    if (!String.IsNullOrEmpty(hotkeyString))
                    {
                        if (Enum.TryParse(hotkeyString, out InputKey tempKey))
                            this._hotkey = tempKey;
                        else
                            this._hotkey = InputKey.O;
                    }

                    var messageColorArray = jobject.Property("messageColor").Value.ToObject<int[]>();
                    if (messageColorArray.Length == 3 && messageColorArray.All(v => v >= 0 && v <= 255))
                        _messageColor = new Color(messageColorArray[0], messageColorArray[1], messageColorArray[2]);
                    else
                        PrintError("Could not read 'messageColor'. Invalid format or values. Must follow this pattern '[R, G, B]' where R, G, & B are values 0 trough 255");

                    var errorColorArray = jobject.Property("errorColor").Value.ToObject<int[]>();
                    if (errorColorArray.Length == 3 && errorColorArray.All(v => v >= 0 && v <= 255))
                        _errorColor = new Color(errorColorArray[0], errorColorArray[1], errorColorArray[2]);
                    else
                        PrintError("Could not read 'errorColor'. Invalid format or values. Must follow this pattern '[R, G, B]' where R, G, & B are values 0 trough 255");
                }
                catch (Exception e)
                {
                    PrintError($"Unable to read content of options file: '{ e.Message }'");
                }
            }
            else
            {
                PrintError($"PostMortemPossession: Unable to open (or find) options file: { optionsFile }");
            }
        }

        public override void OnMissionBehaviourInitialize(Mission pMission)
        {
            base.OnMissionBehaviourInitialize(pMission);
            this._mission = pMission;
            this._player = null;
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
                    this._player = Agent.Main;
                    this._playerTeam = Agent.Main.Team;
                }

                // the currnet player character is dead and the input key (default "O") is pressed
                else if (Input.IsKeyPressed(_hotkey) && _player.Health <= 0.0 && _playerTeam != null)
                {
                    // normal mission
                    var missionBehaviour = _mission?.MissionBehaviours.OfType<MissionView>().FirstOrDefault(mv => mv is MissionSpectatorControl);

                    // tournament
                    if (missionBehaviour == null)
                        missionBehaviour = _mission.MissionBehaviours.OfType<MissionView>().FirstOrDefault(mb => mb is MissionGauntletTournamentView);

                    if (missionBehaviour != null && missionBehaviour?.MissionScreen != null)
                    {
                        var lastFollowedAgent = missionBehaviour?.MissionScreen?.LastFollowedAgent;
                        if (lastFollowedAgent != null && lastFollowedAgent.Health > 0.0f)
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
                                    TakeControlOfFriendlyAgent(lastFollowedAgent);
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
            }
            catch (Exception e)
            {
                PrintError($"PostMortemPossession: An exception was thrown: '{ e.Message }'");
            }
        }

        protected void TakeControlOfFriendlyAgent(Agent pAgent)
        {
            if (pAgent != null)
            {
                PrintInformation($"You are now controlling '{ pAgent.Name }'");
                pAgent.Controller = Agent.ControllerType.Player;
                this._player = pAgent;
                var battleObserverMissionLogic = _mission.GetMissionBehaviour<BattleObserverMissionLogic>();
                if (battleObserverMissionLogic != null)
                    (battleObserverMissionLogic.BattleObserver as ScoreboardVM).IsMainCharacterDead = false;  // do not display the "you are dead" message at the bottom of the screen
            }
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
            if (!_muteExceptions && !String.IsNullOrEmpty(pMessage))
                InformationManager.DisplayMessage(new InformationMessage($"PostMortemPossession: { pMessage }", color));
        }

        #endregion
    }
}
