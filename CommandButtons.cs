using System;
using System.Collections.Generic;
using System.Linq;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Command Buttons", "ITU", "1.2.0")]
    [Description("Create your own GUI buttons for commands.")]
    class CommandButtons : RustPlugin
    {
        #region Variables

        private static CuiPanel Menu;

        #endregion
        

        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Background Color")]
            public string backgroundColor = "0 0 0 0.8";

            [JsonProperty(PropertyName = "GUI Left Top Position")]
            public string LeftTopPosition = "0.01 0.88";

            [JsonProperty(PropertyName = "Distance between buttons")]
            public float BetweenButtons = 0.002f;

            [JsonProperty(PropertyName = "Button width")]
            public float ButtonWidth = 0.085f;

            [JsonProperty(PropertyName = "Button height")]
            public float ButtonHeight = 0.035f;
            
            [JsonProperty(PropertyName = "List of buttons", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ConfigButton> Buttons = new List<ConfigButton> { new ConfigButton() };
        }

        private class ConfigButton
        {
            [JsonIgnore] public CuiButton Button;
            
            [JsonProperty(PropertyName = "Button color")]
            public string ButtonColor = "0.0 0.0 0.0 1.0";

            [JsonProperty(PropertyName = "Text color")]
            public string TextColor = "#ffffff";

            [JsonProperty(PropertyName = "Text size")]
            public short TextSize = 12;

            [JsonProperty(PropertyName = "Button permission")]
            public string Permission = "commandbuttons.admin";

            [JsonProperty(PropertyName = "Button text")]
            public string Text = "Vanish";

            [JsonProperty(PropertyName = "Execute chat (true) or console (false) command")]
            public bool IsChatCommand = true;

            [JsonProperty(PropertyName = "Executing command")]
            public string Command = "/vanish";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion

        #region Data

        private Data _Data;

        private class Data
        {
            [JsonProperty(PropertyName = "Players Disabled the Buttons")]
            public List<ulong> Players = new List<ulong>();
        }

        private void LoadData()
        {
            _Data = Interface.Oxide.DataFileSystem.ReadObject<Data>(Name);
            if (_Data == null)
                ClearData();
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _Data);

        private void ClearData()
        {
            _Data = new Data();
            SaveData();
        }

        #endregion

        #region Commands

        [ChatCommand("cb_gui")]
        private void ToggleButtonsGUI(BasePlayer player, string cmd, string[] args)
        {
            if (calcButtons(player) == 0) {
                PrintToChat(player, GetMsg("No Permission", player.UserIDString));
                return;
            }

            if (IsButtonsEnabled(player)) {
                AddPlayerToData(player);
                PrintToChat(player, GetMsg("Hide Button GUI", player.UserIDString));
                DestroyUI(player);
            }
            else {
                RemovePlayerFromData(player);
                PrintToChat(player, GetMsg("Show Button GUI", player.UserIDString));
                ShowUI(player, _config);
            }
        }

        #endregion

        #region Hooks

        private int calcButtons(BasePlayer player)
        {
            int counter = 0;
            foreach(ConfigButton btn in _config.Buttons) {
                if (CanUse(player, btn.Permission))
                    counter++;
            }
            return counter;
        }

        private bool IsButtonsEnabled(BasePlayer player)
        {
            return !_Data.Players.Contains(player.userID);
        }

        private void AddPlayerToData(BasePlayer player)
        {
            _Data.Players.Add(player.userID);
        }

        private void RemovePlayerFromData(BasePlayer player)
        {
            _Data.Players.Remove(player.userID);
        }

        private void Init()
        {
            LoadConfig();
            RegisterPermissions();
            LoadData();
            
            cmd.AddConsoleCommand("commandbuttons.exec", this, arg =>
            {
                if (!arg.HasArgs(2)) return false;
                
                var isChat = arg.Args[0] == "chat";
                SendCommand(arg.Connection, arg.Args.Skip(1).ToArray(), isChat);
                
                return false;
            });
            
            foreach (var player in BasePlayer.activePlayerList) {
                    ShowUI(player, _config);
            }
        }

        private void Unload()
        {
            SaveData();
            foreach (var player in BasePlayer.activePlayerList) {
                DestroyUI(player);
            }
        }

        private void OnServerSave()
        {
            SaveData();
        }

        //Update UI when permission change
        private void OnUserPermissionGranted(string id, string permName) => UpdatePlayerUI(id);
        private void OnUserPermissionRevoked(string id, string permName) => UpdatePlayerUI(id);
        private void OnGroupPermissionGranted(string name, string permName) => UpdatePlayersInGroup(name, permName);
        private void OnGroupPermissionRevoked(string name, string permName) => UpdatePlayersInGroup(name, permName);
        private void OnUserGroupAdded(string id, string groupName) => UpdatePlayerUI(id);
        private void OnUserGroupRemoved(string id, string groupName) => UpdatePlayerUI(id);

        private void UpdatePlayersInGroup(string name, string permName)
        {
            foreach (var user in permission.GetUsersInGroup(name))
            {
                UpdatePlayerUI(user.Split(' ')[0]);
            }
        }

        private void UpdatePlayerUI(string id)
        {
            var player = BasePlayer.activePlayerList.Where(x => x.UserIDString == id).FirstOrDefault();
            if (player != null) ShowUI(player, _config);
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
                ShowUI(player, _config);
        }

        private void OnPlayerDeath(BasePlayer player)
        {
            DestroyUI(player);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"No Permission", "You don't have enough permission to run this command!"},
                {"Only Player", "This command can be used only by players!"},
                {"Show Button GUI", "Showing GUI buttons."},
                {"Hide Button GUI", "Hideing GUI buttons."}
            }, this);
            
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"No Permission", "你没有权限使用这个指令!"},
                {"Only Player", "这个指令只能由玩家使用!"},
                {"Show Button GUI", "顯示 GUI 按钮"},
                {"Hide Button GUI", "隱藏 GUI 按钮"}
            }, this, "zh-CN");
        }

        #endregion


        #region Helpers

        private void RegisterPermissions()
        {
            foreach(ConfigButton btn in _config.Buttons)
            {
                var perm = btn.Permission;
                if (!string.IsNullOrEmpty(perm) && !permission.PermissionExists(perm, this)){
                    permission.RegisterPermission(perm, this);
                }
            }
        }

        private void ShowUI(BasePlayer player, Configuration config)
        {
            if(!IsButtonsEnabled(player)) return;
            
            // Destroy existing UI
            DestroyUI(player);

            int buttonsCount = calcButtons(player);
            var GUIElement = new CuiElementContainer();   

            // Loading CUIs
            var minGuiMarginHorizontal = _config.BetweenButtons;
            var minGuiMarginVertical = _config.BetweenButtons * 2;
            var minGuiWidth = _config.ButtonWidth;
            var minGuiHeight = _config.ButtonHeight;

            var backgroundWidth = 2 * minGuiMarginHorizontal + minGuiWidth;
            var backgroundHeight = minGuiMarginVertical + buttonsCount * (minGuiHeight + minGuiMarginVertical);

            string[] LeftTopCorner = _config.LeftTopPosition.Split(' ');
            var mleft = double.Parse(LeftTopCorner[0]);
            var mtop = double.Parse(LeftTopCorner[1]);
            var mright = mleft + backgroundWidth;
            var mbottom = mtop - backgroundHeight;

            var relativeButtonWidth = minGuiWidth / backgroundWidth;
            var relativeButtonHeight = minGuiHeight / backgroundHeight;
            var relativeMarginHorizontal = minGuiMarginHorizontal / backgroundWidth;
            var relativeMarginVertical = minGuiMarginVertical / backgroundHeight;
            
            // Loading menu
            Menu = new CuiPanel
            {
                Image =
                {
                    Color = _config.backgroundColor
                },
                CursorEnabled = false,
                RectTransform =
                {
                    AnchorMin = $"{mleft} {mbottom}",
                    AnchorMax = $"{mright} {mtop}"
                }
            };
            GUIElement.Add(Menu, "Hud.Menu", Name);


            // Loading buttons
            if (buttonsCount == 0) return;
            int count = 0;
            foreach(ConfigButton btn in _config.Buttons) {
                if (CanUse(player, btn.Permission)) {
                    var left =  relativeMarginHorizontal;
                    var top = 1 - relativeMarginVertical - count++ * (relativeButtonHeight + relativeMarginVertical);
                    var bottom = top - relativeButtonHeight;
                    var right = left + relativeButtonWidth;

                    var type = btn.IsChatCommand ? "chat" : "console";
                    btn.Button = new CuiButton
                    {
                        Text =
                        {
                            Text = $"<color={btn.TextColor}>{btn.Text}</color>",
                            FontSize = btn.TextSize,
                            Align = TextAnchor.MiddleCenter,
                        },
                        Button =
                        {
                            Color = btn.ButtonColor,
                            Command = $"commandbuttons.exec {type} {btn.Command}",
                        },
                        RectTransform =
                        {
                            AnchorMin = $"{left} {bottom}",
                            AnchorMax = $"{right} {top}"
                        }
                    };
                    GUIElement.Add(btn.Button, Name, "CommandButtonsCUIButton");
                }
            }
            CuiHelper.AddUi(player, GUIElement);
        }

        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Name);
        }

        private void SendCommand(Connection conn, string[] args, bool isChat)
        {
            if (!Net.sv.IsConnected()) return;

            var command = string.Empty;
            var argsLength = args.Length;
            for (var i = 0; i < argsLength; i++)
                command += $"{args[i]} ";
            
            if (isChat)
                command = $"chat.say {command.QuoteSafe()}";
            
            Net.sv.write.Start();
            Net.sv.write.PacketID(Message.Type.ConsoleCommand);
            Net.sv.write.String(command);
            Net.sv.write.Send(new SendInfo(conn));
        }

        private bool CanUse(BasePlayer player, string perm) =>
            player.IsAdmin || string.IsNullOrEmpty(perm) || permission.UserHasPermission(player.UserIDString, perm);

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}