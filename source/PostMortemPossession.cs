using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade.View.Missions;
using TaleWorlds.MountAndBlade.ViewModelCollection;
using TaleWorlds.Library;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Linq;
using System;

namespace PostMortemPossession
{
    public class PostMortemPossession : MBSubModuleBase
    {
        [JsonProperty("allowControlAllies")]
        private bool _allowControlAllies { get; set; }

        [JsonProperty("muteExceptions")]
        private bool _muteExceptions { get; set; }


        [JsonProperty("verbose")]
        private bool _verbose { get; set; }

        private InputKey _hotkey { get; set; }

        private Agent _player { get; set; }
        private Team _playerTeam { get; set; }
        private Mission _mission { get; set; }

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            var optionsFile = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\PostMortemPossession_options.json";
            if (File.Exists(optionsFile))
            {
                try
                {
                    var jobject = JObject.Parse(File.ReadAllText(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\PostMortemPossession_options.json"));
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
                }
                catch (Exception e)
                {
                    InformationManager.DisplayMessage(new InformationMessage($"PostMortemPossession: unable to read content of options file: '{ e.Message }'", new Color(100, 0, 0)));
                }

            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage($"PostMortemPossession: Unable to open (or find) options file: { optionsFile }", new Color(100, 0, 0)));
            }
        }

        public override void OnMissionBehaviourInitialize(Mission pMission)
        {
            base.OnMissionBehaviourInitialize(pMission);
            _mission = pMission;
            _player = null;
        }

        protected override void OnApplicationTick(float dt)
        {
            try
            {
                if (Game.Current == null || _mission == null || _mission.Scene == null || Game.Current.CurrentState > Game.State.Running)
                    return;

                if (Agent.Main != null && Agent.Main.Index != (_player?.Index).GetValueOrDefault(-1))
                {
                    _player = Agent.Main;
                    _playerTeam = Agent.Main.Team;
                }
                // the currnet player character is dead and the input key (default "O") is pressed
                else if (Input.IsKeyPressed(_hotkey) && _player.Health <= 0.0 && _playerTeam != null)
                {
                    // normal mission
                    var missionBehaviour = _mission?.MissionBehaviours.OfType<MissionView>().FirstOrDefault(mv => mv is TaleWorlds.MountAndBlade.GauntletUI.MissionSpectatorControl);

                    // tournament
                    if (missionBehaviour == null)
                        missionBehaviour = _mission.MissionBehaviours.OfType<MissionView>().FirstOrDefault(mb => mb is SandBox.GauntletUI.Missions.MissionGauntletTournamentView);

                    if (missionBehaviour != null && missionBehaviour.MissionScreen != null)
                    {
                        var lastFollowedAgent = missionBehaviour.MissionScreen.LastFollowedAgent;
                        if (lastFollowedAgent != null && lastFollowedAgent.Health > 0.0f)
                        {
                            // take control of party soldier
                            if (lastFollowedAgent.Team.IsPlayerTeam)
                            {
                                TakeControlOfFriendlyAgent(lastFollowedAgent);
                            }
                            // attempt to take control of ally soldier
                            else if (lastFollowedAgent.Team.IsFriendOf(_playerTeam))
                            {
                                if (_allowControlAllies)
                                    TakeControlOfFriendlyAgent(lastFollowedAgent);
                                else
                                    PrintInformation($"PostMortemPossession: You can't take control of ally '{ lastFollowedAgent.Name }'");
                            }
                            // inform player that they can't take control of enemy soldier
                            else if (lastFollowedAgent.Team.IsEnemyOf(_playerTeam))
                            {
                                PrintInformation($"PostMortemPossession: You can't take control of enemy '{ lastFollowedAgent.Name }'");
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
                PrintInformation($"PostMortemPossession: You are now controlling '{ pAgent.Name }'");
                pAgent.Controller = Agent.ControllerType.Player;
                _player = pAgent;
                var battleObserverMissionLogic = _mission.GetMissionBehaviour<BattleObserverMissionLogic>();
                if (battleObserverMissionLogic != null)
                    (battleObserverMissionLogic.BattleObserver as ScoreboardVM).IsMainCharacterDead = false;  // do not display the "you are dead" message at the buttom off the screen
            }
        }

        protected void PrintInformation(string pMessage)
        {
            if (_verbose)
                InformationManager.DisplayMessage(new InformationMessage(pMessage, new Color(0, 0, 100)));
        }

        protected void PrintError(string pMessage)
        {
            if (!_muteExceptions)
                InformationManager.DisplayMessage(new InformationMessage(pMessage, new Color(100, 0, 0)));
        }
    }
}
