using BepInEx;
using BepInEx.Logging;
using RoR2;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace Frogtown
{
    public class ModManager
    {
        public static int modCount { private set; get; }

        private static string DISABLED_MOD_FOLDER;
        private static string ENABLED_MOD_FOLDER;
        internal static Dictionary<string, FrogtownModDetails> frogtownDetails = new Dictionary<string, FrogtownModDetails>();
        internal static Dictionary<string, ModDetails> modDetails = new Dictionary<string, ModDetails>();

        private static bool hasInitialized = false;

        public static void RegisterMod(FrogtownModDetails details)
        {
            if (!string.IsNullOrEmpty(details.GUID)){
                frogtownDetails.Add(details.GUID, details);
            }
        }

        public static void FindAndInitMods()
        {
            if (!hasInitialized)
            {
                hasInitialized = true;
                var loc = Assembly.GetExecutingAssembly().Location;
                ENABLED_MOD_FOLDER = loc.Substring(0, loc.LastIndexOf("BepInEx")) + "BepInEx\\plugins\\Unmanaged";
                System.IO.Directory.CreateDirectory(ENABLED_MOD_FOLDER);
                DISABLED_MOD_FOLDER = loc.Substring(0, loc.LastIndexOf("BepInEx")) + "DisabledMods";
                System.IO.Directory.CreateDirectory(DISABLED_MOD_FOLDER);
                FindInactiveMods();
                FindActiveMods();
                InitMods();
            }
        }

        internal static IEnumerator CheckForUpdates()
        {
            var config = FrogtownShared.GetConfig();
            foreach (string GUID in modDetails.Keys)
            {
                if (modDetails.TryGetValue(GUID, out ModDetails details))
                {
                    if(details.frogtownModDetails != null)
                    {
                        details.newVersionLoading = true;
                    }
                }
            }

            bool anyError = false;
            foreach (string GUID in modDetails.Keys)
            {
                if (modDetails.TryGetValue(GUID, out ModDetails details))
                {
                    if (details.frogtownModDetails != null)
                    {
                        string lastUpdateInst = config.Wrap("modupdates", "lastcheck-" + GUID, "", "0").Value;
                        if (long.TryParse(lastUpdateInst, out long updateInst))
                        {
                            DateTime lastUpdate = new DateTime(updateInst);
                            if (lastUpdate.AddDays(0.5) > DateTime.Now)
                            {
                                string newV = config.Wrap("modupdates", "newestversion-" + GUID, "", "0").Value;
                                if (newV.CompareTo(details.version) > 0)
                                {
                                    details.frogtownModDetails.newVersion = newV;
                                }
                                FrogtownShared.Log("FrogShared", LogLevel.Info, "Loaded new version of " + GUID + " from cache.");
                                details.newVersionLoading = false;
                                continue;
                            }
                        }

                        if (anyError)
                        {
                            details.newVersionLoading = false;
                            continue;
                        }

                        string url = "https://api.github.com/repos/" + details.frogtownModDetails.githubAuthor + "/" + details.frogtownModDetails.githubRepo + "/releases";
                        FrogtownShared.Log("FrogShared", LogLevel.Info, "Requesting " + url);
                        var req = UnityWebRequest.Get(url).SendWebRequest();
                        while (!req.isDone)
                        {
                            yield return new WaitForEndOfFrame();
                        }
                        details.newVersionLoading = false;
                        if (req.webRequest.isHttpError || req.webRequest.isNetworkError)
                        {
                            FrogtownShared.Log("FrogShared", LogLevel.Error, "Error loading releases from " + url);
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
                                    if (version.CompareTo(topRelease) > 0 && version.CompareTo(details.version) > 0)
                                    {
                                        topRelease = version;
                                    }
                                }
                                details.frogtownModDetails.newVersion = topRelease;
                            }
                            catch (Exception e)
                            {
                                FrogtownShared.Log("FrogShared", LogLevel.Info, "Error parsing JSON ");
                                FrogtownShared.Log("FrogShared", LogLevel.Info, e.Message + " " + e.StackTrace);
                            }
                            config.Wrap("modupdates", "lastcheck-" + GUID, "", "0").Value = DateTime.Now.Ticks.ToString();
                            config.Wrap("modupdates", "newestversion-" + GUID, "", "0").Value = details.frogtownModDetails.newVersion;
                        }
                    }
                }
            }
        }

        private static void AfterModToggle(ModDetails details)
        {
            if (details.frogtownModDetails != null && details.frogtownModDetails.isNotCheaty)
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
                FrogtownShared.Log("FrogShared", LogLevel.Info, "Set isModded flag to " + newIsModded);
            }
        }

        private static void FindInactiveMods()
        {
            var disabledFiles = System.IO.Directory.EnumerateFiles(DISABLED_MOD_FOLDER);
            foreach(var file in disabledFiles)
            {
                int ix = file.LastIndexOf("\\");
                string cleanName = file.Substring(ix, file.Length - ix);
                modDetails.Add(cleanName, new ModDetails(cleanName));
            }
        }

        private static void FindActiveMods()
        {
            var config = FrogtownShared.GetConfig();

            var allModDetails =
                (from a in AppDomain.CurrentDomain.GetAssemblies()
                 from t in a.GetTypes()
                 let attributeList = t.GetCustomAttributes(typeof(BepInPlugin), true).Take(1)
                 where attributeList != null && attributeList.Count() > 0
                 let attribute = attributeList.First() as BepInPlugin
                 select new ModDetails(attribute, t.Assembly.Location)).ToArray();

            foreach (var info in allModDetails)
            {
                if (frogtownDetails.TryGetValue(info.GUID, out FrogtownModDetails frogDetails))
                {
                    info.frogtownModDetails = frogDetails;
                    frogDetails.modDetails = info;

                    string raw = config.Wrap("mods", info.GUID, "", "true").Value;
                    bool.TryParse(raw, out bool shouldBeActive);

                    if (shouldBeActive)
                    {
                        ToggleMod(info.GUID, true);
                    }
                }
                else
                {
                    //Do not call ToggleMod because it will try to move DLLs around and shit like that.
                    //This is intializing the enabled properties and active mod count to reflect the actual
                    //state of what is loading.
                    info.enabled = true;
                    info.initialEnabled = true;
                    info.afterToggle?.Invoke(info);
                }
                info.afterToggle += AfterModToggle;
                modDetails.Add(info.GUID, info);
            }
        }

        private static void InitMods()
        {
            var allModDetails =
                (from a in AppDomain.CurrentDomain.GetAssemblies()
                 from t in a.GetTypes()
                 let attributeList = t.GetCustomAttributes(typeof(BepInPlugin), true).Take(1)
                 where attributeList != null && attributeList.Count() > 0
                 let attribute = attributeList.First() as BepInPlugin
                 select new ModDetails(attribute, t.Assembly.Location)).ToArray();

            foreach (var info in allModDetails)
            {
                if (frogtownDetails.TryGetValue(info.GUID, out FrogtownModDetails frogDetails))
                {
                    info.frogtownModDetails = frogDetails;
                }
            }
        }

        public static bool CanToggleMod(string GUID, out bool enabled)
        {
            if(modDetails.TryGetValue(GUID, out ModDetails details))
            {
                if(details.frogtownModDetails != null)
                {
                    enabled = details.enabled;
                    return !details.frogtownModDetails.noDisable;
                }
                else
                {
                    enabled = details.enabled;
                    return true;
                }
            }

            enabled = false;
            return false;
        }

        public static void ToggleMod(string GUID, bool enable)
        {
            if (modDetails.TryGetValue(GUID, out ModDetails details))
            {
                if(CanToggleMod(GUID, out bool curEnabled))
                {
                    if(curEnabled != enable)
                    {
                        if (details.frogtownModDetails != null)
                        {
                            details.enabled = enable;
                            details.afterToggle?.Invoke(details);
                            var config = FrogtownShared.GetConfig();
                            config.Wrap("mods", GUID, "", "true").Value = enable.ToString();
                            config.Save();
                        }
                        else
                        {
                            SetNonFrogtownModStatus(details, enable);
                        }
                    }
                }
            }
        }

        private static void SetNonFrogtownModStatus(ModDetails details, bool enable)
        {
            string src, dst;
            if (enable)
            {
                src = DISABLED_MOD_FOLDER + "\\" + details.dllFileName;
                dst = ENABLED_MOD_FOLDER + "\\" + details.dllFileName;
            }
            else
            {
                src = details.dllFileFullPath;
                if (string.IsNullOrEmpty(src))
                {
                    src = ENABLED_MOD_FOLDER + "\\" + details.dllFileName;
                }
                dst = DISABLED_MOD_FOLDER + "\\" + details.dllFileName;
            }
            System.IO.File.Move(src, dst);

            details.enabled = enable;
            //Don't call afterToggle because they aren't actually changed until restart
        }
    }
}
