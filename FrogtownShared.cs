using Harmony;
using RoR2;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;

namespace Frogtown
{
    public class FrogtownShared
    {
        public static UnityModManager.ModEntry modEntry;

        /// <summary>
        /// Number of active mods that should affect the isModded property. Call ModToggled to manipulate this.
        /// </summary>
        public static int modCount { private set; get; }

        /// <summary>
        /// Mapping of commands to handlers
        /// </summary>
        private static Dictionary<string, List<Func<string, string[], bool>>> chatCommandList = new Dictionary<string, List<Func<string, string[], bool>>>();

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = HarmonyInstance.Create("com.frogtown.sharedlibrary");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            FrogtownShared.modEntry = modEntry;
            modEntry.Logger.Log("Loaded frogtown helper.");
            return true;
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
        /// Maintains the number of mods that are active, and updates RoR2Application.isModded to match. Add one call to this in your OnToggle handler.
        /// </summary>
        /// <param name="enabled">True if mod is enabled</param>
        public static void ModToggled(bool enabled)
        {
            if (enabled)
            {
                if (modCount == 0)
                {
                    modEntry.Logger.Log("Enabling MOD mode.");
                }
                modCount++;
            }
            else
            {
                if (modCount == 1)
                {
                    modEntry.Logger.Log("Disabling MOD mode.");
                }
                modCount--;
            }
            RoR2.RoR2Application.isModded = modCount > 0;
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
                        modEntry.Logger.Log("Command " + pieces[0] + " logged exception \"" + e.Message + "\"");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Parses messages from the chat box to trigger commands
    /// </summary>
    [HarmonyPatch(typeof(RoR2.Chat))]
    [HarmonyPatch("AddMessage")]
    [HarmonyPatch(new Type[] { typeof(string) })]
    class ChatPatch
    {
        static void Prefix(ref string message)
        {
            if (ParseUserAndMessage(message, out string userName, out string text))
            {
                FrogtownShared.modEntry.Logger.Log("Recieved command \"" + text + "\" from user \"" + userName + "\"");
                string[] pieces = text.Split(' ');
                FrogtownShared.TriggerChatCommand(userName, pieces);
            }
        }

        public static bool ParseUserAndMessage(string input, out string user, out string message)
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
    }
}
