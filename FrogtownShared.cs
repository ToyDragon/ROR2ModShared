using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using RoR2;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Frogtown
{
    public delegate void OnToggle(bool newEnabled);

    [BepInPlugin("com.frogtown.shared", "Frogtown Shared", "2.0.1")]
    public class FrogtownShared : BaseUnityPlugin
    {
        private static Dictionary<string, List<Func<string, string[], bool>>> chatCommandList = new Dictionary<string, List<Func<string, string[], bool>>>();
        internal static List<string> log = new List<string>();

        private static FrogtownShared instance;

        public static void Log(string owner, LogLevel level, string message)
        {
            if(log.Count > 200)
            {
                log.RemoveAt(0);
            }

            string line = "[" + owner + "]: ";
            line += message;

            //TODO do something with log level

            log.Add(line);
        }

        public static void RegisterMod(FrogtownModDetails details)
        {
            ModManager.RegisterMod(details);
        }

        /// <summary>
        /// Chat commands are triggered by users typing "/command [piece1 [piece2 ...]]" in chat.  Your handler should be a function with parameters "string playerName, string[] pieces" and return a boolean. Commands are case insensitive. Commands are called even when your mod is marked as disabled, so make sure to track your disabled status and respect it in handlers.
        /// </summary>
        /// <param name="command">Case insensitive command</param>
        /// <param name="delegate">Function to call when command entered</param>
        public static void AddChatCommand(string command, Func<string, string[], bool> @delegate)
        {
            command = command.ToUpper();
            if (!chatCommandList.ContainsKey(command))
            {
                chatCommandList.Add(command, new List<Func<string, string[], bool>>());
            }
            if (chatCommandList.TryGetValue(command, out var list))
            {
                list.Add(@delegate);
            }
        }

        /// <summary>
        /// Sends a message to all connected players in the chat box.
        /// </summary>
        /// <param name="message">message to send</param>
        public static void SendChat(string message)
        {
            Chat.SendBroadcastChat(new Chat.PlayerChatMessage()
            {
                networkPlayerName = new NetworkPlayerName()
                {
                    steamId = new CSteamID(1234),
                    nameOverride = "user"
                },
                baseToken = message
            });
        }

        void Awake()
        {
            instance = this;

            On.RoR2.Chat.AddMessage_string += (orig, message) =>
            {
                orig(message);
                if (ParseUserAndMessage(message, out string userName, out string text))
                {
                    string[] pieces = text.Split(' ');
                    TriggerChatCommand(userName, pieces);
                }
            };

            FrogtownModDetails details = new FrogtownModDetails("com.frogtown.shared")
            {
                githubAuthor = "ToyDragon",
                githubRepo = "ROR2ModShared",
                description = "Powers this UI and contains shared resources for other mods.",
                noDisable = true
            };
            details.OnlyContainsBugFixesOrUIChangesThatArentContriversial();
            ModManager.RegisterMod(details);

            //I use this to test multiplayer, leave it off in releases
            //AddChatCommand("clear_mod_flag", OnClearModFlagCommand);
            Invoke(nameof(Init), 1f);
        }

        private void Init()
        {
            RoR2Application.isModded = false;
            ModManager.FindAndInitMods();
            new GameObject(typeof(UI).FullName, typeof(UI));
            StartCoroutine(nameof(CheckForUpdates));
        }

        private IEnumerator CheckForUpdates()
        {
            return ModManager.CheckForUpdates();
        }

        internal static ConfigFile GetConfig()
        {
            return instance.Config;
        }
        
        //private static bool OnClearModFlagCommand(string userName, string[] pieces)
        //{
        //    RoR2Application.isModded = false;
        //    SendChat("Clearing mod flag.");
        //    return true;
        //}

        private static bool ParseUserAndMessage(string input, out string user, out string message)
        {
            user = "";
            message = "";
            int ix = input.IndexOf("<noparse>/");
            if (ix >= 0)
            {
                int start = "<color=#123456><noparse>".Length;
                int len = ix - "</noparse>:0123456789012345678901234".Length; // lol
                user = input.Substring(start, len);
                message = input.Substring(ix + "<noparse>/".Length);
                message = message.Substring(0, message.IndexOf("</noparse>"));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Changes the prefab for the given player.
        /// </summary>
        /// <param name="playerName">Case Sensitive player name</param>
        /// <param name="prefab">Prefab object from BodyCatalog</param>
        /// <returns>true if changed successfully</returns>
        public static bool ChangePrefab(string playerName, GameObject prefab)
        {
            if (prefab != null)
            {
                PlayerCharacterMasterController player = GetPlayerWithName(playerName);
                if (player != null)
                {
                    var body = player.master.GetBodyObject();
                    var oldPos = body.transform.position;
                    var oldRot = body.transform.rotation;
                    player.master.DestroyBody();
                    player.master.bodyPrefab = prefab;
                    player.master.SpawnBody(prefab, oldPos, oldRot);
                    body.transform.position = oldPos;
                    body.transform.rotation = oldRot;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the player master controller with the given display name.
        /// </summary>
        /// <param name="playerName">Case Sensitive display name</param>
        /// <returns></returns>
        public static PlayerCharacterMasterController GetPlayerWithName(string playerName)
        {
            PlayerCharacterMasterController[] allPlayers = MonoBehaviour.FindObjectsOfType<PlayerCharacterMasterController>();
            foreach (PlayerCharacterMasterController player in allPlayers)
            {
                if (player.networkUser.GetNetworkPlayerName().GetResolvedName() == playerName)
                {
                    return player;
                }
            }

            return null;
        }

        /// <summary>
        /// Called when a user types a command like string. Should not be called from mods, unless you are exposing a new chat interface for users.
        /// </summary>
        /// <param name="userName">The display name of the user</param>
        /// <param name="pieces">The space separated command</param>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void TriggerChatCommand(string userName, string[] pieces)
        {
            if(pieces.Length == 0)
            {
                return;
            }
            if (chatCommandList.TryGetValue(pieces[0].ToUpper(), out var list))
            {
                foreach (var func in list)
                {
                    try
                    {
                        func.Invoke(userName, pieces);
                    }
                    catch (Exception e)
                    {
                        Debug.Log("Command " + pieces[0] + " logged exception \"" + e.Message + "\"");
                    }
                }
            }
        }
    }
}
