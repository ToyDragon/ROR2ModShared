using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using RoR2;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Frogtown
{
    public delegate void OnToggle(bool newEnabled);

    [BepInPlugin("com.frogtown.shared", "Frogtown Shared", "2.0.0")]
    public class FrogtownShared : BaseUnityPlugin
    {
        public static int modCount { private set; get; }
        private static Dictionary<string, List<Func<string, string[], bool>>> chatCommandList = new Dictionary<string, List<Func<string, string[], bool>>>();
        internal static Dictionary<string, ModDetails> allModDetails = new Dictionary<string, ModDetails>();
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

        public void Awake()
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

            AddChatCommand("enable_mod", OnEnableModCommand);
            AddChatCommand("disable_mod", OnDisableModCommand);


            var deetz = new ModDetails("com.frogtown.shared")
            {
                githubAuthor = "ToyDragon",
                githubRepo = "ROR2ModShared",
                description = "Powers this UI and contains shared resources for other mods.",
                noDisable = true
            };
            deetz.OnlyContainsBugFixesOrUIChangesThatArentContriversial();
            RegisterMod(deetz);

            //I use this to test multiplayer, leave it off in releases
            //AddChatCommand("clear_mod_flag", OnClearModFlagCommand);
            Invoke(nameof(InitUI), 0.5f);
            Invoke(nameof(StartCheckingForUpdates), 2f);
        }

        private void StartCheckingForUpdates()
        {
            StartCoroutine(nameof(CheckForUpdates));
        }

        private IEnumerator CheckForUpdates()
        {
            var config = GetConfig();
            foreach (string GUID in allModDetails.Keys)
            {
                if (allModDetails.TryGetValue(GUID, out ModDetails details))
                {
                    details.newVersionLoading = true;
                }
            }

            bool anyError = false;
            foreach (string GUID in allModDetails.Keys)
            {
                if(allModDetails.TryGetValue(GUID, out ModDetails details)){
                    if(!string.IsNullOrWhiteSpace(details.githubAuthor) && !string.IsNullOrWhiteSpace(details.githubRepo))
                    {
                        string lastUpdateInst = config.Wrap("modupdates", "lastcheck-"+GUID, "", "0").Value;
                        if(long.TryParse(lastUpdateInst, out long updateInst))
                        {
                            DateTime lastUpdate = new DateTime(updateInst);
                            if(lastUpdate.AddDays(0.5) > DateTime.Now)
                            {
                                string newV = config.Wrap("modupdates", "newestversion-" + GUID, "", "0").Value;
                                if(newV.CompareTo(details.version) > 0)
                                {
                                    details.newVersion = newV;
                                }
                                Log("FrogShared", LogLevel.Info, "Loaded new version of " + GUID + " from cache.");
                                details.newVersionLoading = false;
                                continue;
                            }
                        }

                        if (anyError)
                        {
                            details.newVersionLoading = false;
                            continue;
                        }

                        string url = "https://api.github.com/repos/" + details.githubAuthor + "/" + details.githubRepo + "/releases";
                        Log("FrogShared", LogLevel.Info, "Requesting " + url);
                        var req = UnityWebRequest.Get(url).SendWebRequest();
                        while (!req.isDone)
                        {
                            yield return new WaitForEndOfFrame();
                        }
                        details.newVersionLoading = false;
                        if (req.webRequest.isHttpError || req.webRequest.isNetworkError)
                        {
                            Log("FrogShared", LogLevel.Error, "Error loading releases from " + url);
                            anyError = true;
                        }
                        else
                        {
                            try
                            {
                                string text = req.webRequest.downloadHandler.text;

                                string topRelease = "";
                                int ix = 0;
                                while ((ix = text.IndexOf("\"tag_name\": \"")) >= 0)
                                {
                                    text = text.Substring(ix + "\"tag_name\": \"".Length, text.Length - ix - "\"tag_name\": \"".Length);
                                    string version = text.Substring(0, text.IndexOf("\""));
                                    if(version.CompareTo(topRelease) > 0 && version.CompareTo(details.version) > 0)
                                    {
                                        topRelease = version;
                                    }
                                }
                                details.newVersion = topRelease;
                            }catch(Exception e)
                            {
                                Log("FrogShared", LogLevel.Info, "Error parsing JSON ");
                                Log("FrogShared", LogLevel.Info, e.Message + " " + e.StackTrace);
                            }
                            config.Wrap("modupdates", "lastcheck-" + GUID, "", "0").Value = DateTime.Now.Ticks.ToString();
                            config.Wrap("modupdates", "newestversion-" + GUID, "", "0").Value = details.newVersion;
                        }
                    }
                }
            }
        }

        private void InitUI()
        {
            RoR2Application.isModded = false;
            new GameObject(typeof(UI).FullName, typeof(UI));
        }

        internal static ConfigFile GetConfig()
        {
            return instance.Config;
        }

        public static bool CanToggleMod(string GUID, out bool status)
        {
            if (allModDetails.TryGetValue(GUID, out ModDetails deets) && !deets.noDisable)
            {
                status = deets.enabled;
                return true;
            }

            status = true;
            return false;
        }
        
        private static bool OnClearModFlagCommand(string userName, string[] pieces)
        {
            RoR2Application.isModded = false;
            SendChat("Clearing mod flag.");
            return true;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="details"></param>
        public static void RegisterMod(ModDetails details)
        {
            details.afterToggle += AfterModToggle;
            allModDetails.Add(details.GUID, details);
            details.isRegistered = true;

            if (details.noDisable)
            {
                details.SetEnabled(true);
            }
        }

        private static List<ModDetails> GetModsFromId(string id)
        {
            List<ModDetails> details = new List<ModDetails>();

            foreach (string modGUID in allModDetails.Keys)
            {
                if (id == "all" || modGUID.ToLower() == id || modGUID.EndsWith("." + id))
                {
                    details.Add(allModDetails[modGUID]);
                }
            }

            return details;
        }

        internal static bool SetModStatus(string guid, bool enabled)
        {
            List<ModDetails> details = GetModsFromId(guid);
            bool found = false;
            foreach (ModDetails detail in details)
            {
                detail.SetEnabled(enabled);
                found = true;
            }
            return found;
        }

        private static bool OnEnableModCommand(string userName, string[] pieces)
        {
            if(pieces.Length < 2 || pieces[1].Length == 0)
            {
                SendChat("Include a mod ID to enable.");
                return true;
            }

            string expectedId = pieces[1].ToLower();
            if (SetModStatus(expectedId, true))
            {
                SendChat("Enabled " + expectedId + ".");
            }
            else
            {
                SendChat("Mod " + expectedId + " not found.");
            }

            return true;
        }

        private static bool OnDisableModCommand(string userName, string[] pieces)
        {
            if (pieces.Length < 2 || pieces[1].Length == 0)
            {
                SendChat("Include a mod ID to disable.");
                return true;
            }
            
            string expectedId = pieces[1].ToLower();
            if (SetModStatus(expectedId, false))
            {
                SendChat("Disabled " + expectedId + ".");
            }
            else
            {
                SendChat("Mod " + expectedId + " not found.");
            }
            return true;
        }

        private static void AfterModToggle(ModDetails details)
        {
            if (details.isNotCheaty)
            {
                return;
            }

            if (details.enabled)
            {
                modCount++;
            }
            else
            {
                modCount--;
            }

            bool newIsModded = modCount > 0;
            if (RoR2Application.isModded != newIsModded)
            {
                RoR2Application.isModded = newIsModded;
                Log("FrogShared", LogLevel.Info, "Set isModded flag to " + newIsModded);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="user"></param>
        /// <param name="message"></param>
        /// <returns></returns>
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
            if(chatCommandList.TryGetValue(command, out var list))
            {
                list.Add(@delegate);
            }
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
                PlayerCharacterMasterController player = FrogtownShared.GetPlayerWithName(playerName);
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

    public class ModDetails
    {
        public string githubAuthor;
        public string githubRepo;
        public string description;
        public string newVersion;
        public bool noDisable;

        internal bool isRegistered;

        public string GUID { get; private set; }
        public bool isNotCheaty { get; private set; }
        public bool enabled { get; private set; }
        public string version { get; private set; }

        public string releaseUrl { get; internal set; }
        public bool newVersionLoading { get; internal set; }

        public delegate void AfterToggleHandler(ModDetails details);
        public AfterToggleHandler afterToggle = null;

        public ModDetails(string GUID)
        {
            this.GUID = GUID;
        }

        public void SetEnabled(bool newEnabled)
        {
            if (isRegistered && newEnabled != enabled)
            {
                enabled = newEnabled;
                afterToggle?.Invoke(this);
            }
        }

        public void OnlyContainsBugFixesOrUIChangesThatArentContriversial()
        {
            isNotCheaty = true;
        }

        internal void SetVersion(string version)
        {
            this.version = version;
        }
    }
}
