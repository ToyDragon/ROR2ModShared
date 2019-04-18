using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace Frogtown
{
    public class UI : MonoBehaviour
    {
        public static GUIStyle button = null;

        private GUIStyle window = null;
        private GUIStyle h1 = null;
        private GUIStyle h2 = null;
        private GUIStyle richtext = null;
        private GUIStyle curVersion = null;

        private GUIStyle nodisable = null;
        private GUIStyle red = null;

        private bool hasLaunched;
        private bool hasInit;

        private Rect windowRect = new Rect(0, 0, 0, 0);
        private Vector2 windowSize = Vector2.zero;
        private Resolution lastResolution;

        private float pendingScale = 1f;
        private bool isOpen;
        private int tabId = 0;
        private Vector2[] scrollPositions = null;
        private List<Column> columns = new List<Column>();
        private bool GameCursorLocked { get; set; }

        internal UISettings uiSettings = new UISettings();
        internal static UI instance;

        private HashSet<string> collapsedAuthors = new HashSet<string>();

        void Awake()
        {
            instance = this;
            DontDestroyOnLoad(this);
            windowSize = new Vector2(960, 720);
            scrollPositions = new Vector2[3];
            LoadSettings();
        }

        internal void SaveSettings()
        {
            var config = FrogtownShared.GetConfig();
            config.Wrap("ui", "scale", "", "1").Value = uiSettings.scale.ToString();
            config.Wrap("ui", "showonstart", "", "true").Value = uiSettings.showOnStart.ToString();
            config.Save();
        }

        private void LoadSettings()
        {
            var config = FrogtownShared.GetConfig();

            string raw = config.Wrap("ui", "scale", "", "1").Value;
            if(!float.TryParse(raw, out uiSettings.scale))
            {
                uiSettings.scale = 1f;
            }
            uiSettings.scale = Mathf.Clamp(uiSettings.scale, 0.5f, 2f);

            raw = config.Wrap("ui", "showonstart", "", "true").Value;
            bool.TryParse(raw, out uiSettings.showOnStart);
        }

        void Start()
        {
            CalculateWindowPos();
            if (uiSettings.showOnStart)
            {
                ToggleWindow(true);
            }
        }

        void Update()
        {
            if (isOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
  
            bool toggle = false;
            if (Input.GetKeyUp(KeyCode.F10) && (Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftControl)))
            {
                toggle = true;
            }
            if (toggle)
            {
                ToggleWindow();
            }
            else if (isOpen && Input.GetKey(KeyCode.Escape))
            {
                ToggleWindow();
            }
        }
        
        private void PrepareGUI()
        {
            window = new GUIStyle();

            h1 = new GUIStyle();
            h1.normal.textColor = Color.white;
            h1.fontStyle = FontStyle.Bold;
            h1.alignment = TextAnchor.MiddleCenter;

            h2 = new GUIStyle();
            h2.normal.textColor = new Color(0.6f, 0.91f, 1f);
            h2.padding = new RectOffset(0, 0, 7, 0);
            h2.fontStyle = FontStyle.Bold;

            curVersion = new GUIStyle();
            curVersion.normal.textColor = Color.white;
            curVersion.padding = new RectOffset(0, 0, 10, 0);
            curVersion.fontStyle = FontStyle.Bold;

            button = new GUIStyle(GUI.skin.button);

            richtext = new GUIStyle();
            richtext.richText = true;
            richtext.normal.textColor = Color.white;

            red = new GUIStyle();
            red.normal.textColor = Color.red;

            nodisable = new GUIStyle();
            nodisable.padding = new RectOffset(9, 0, 10, 0);
            nodisable.normal.textColor = Color.white;

            red = new GUIStyle();
            red.padding = RectOffset(6);
            red.normal.textColor = Color.red;

            columns.Add(new Column { name = "", width = 50}); // Group by author
            columns.Add(new Column { name = "Author", width = 130 });
            columns.Add(new Column { name = "Name", width = 200, expand = true });
            columns.Add(new Column { name = "Version", width = 100 });
            columns.Add(new Column { name = "New Version", width = 100 });
            columns.Add(new Column { name = "On/Off", width = 50 });
            columns.Add(new Column { name = "Status", width = 50 });
        }

        private void ScaleGUI()
        {
            GUI.skin.button.padding = new RectOffset(10, 10, 3, 3);
            GUI.skin.button.margin = RectOffset(4, 2);

            GUI.skin.horizontalSlider.fixedHeight = 12;
            GUI.skin.horizontalSlider.border = RectOffset(3, 0);
            GUI.skin.horizontalSlider.padding = RectOffset(0, 0);
            GUI.skin.horizontalSlider.margin = RectOffset(4, 8);

            GUI.skin.horizontalSliderThumb.fixedHeight = 12;
            GUI.skin.horizontalSliderThumb.border = RectOffset(4, 0);
            GUI.skin.horizontalSliderThumb.padding = RectOffset(7, 0);
            GUI.skin.horizontalSliderThumb.margin = RectOffset(0);

            GUI.skin.toggle.margin.left = 10;

            window.padding = RectOffset(5);
            h1.fontSize = 16;
            h1.margin = RectOffset(0, 5);
            h2.fontSize = 13;
            h2.margin = RectOffset(0, 3);
            button.fontSize = 13;
            button.padding = RectOffset(30, 5);
        }

        private void OnGUI()
        {
            if (!hasInit)
            {
                hasInit = true;
                PrepareGUI();
            }

            if (isOpen)
            {
                if (lastResolution.width != Screen.currentResolution.width || lastResolution.height != Screen.currentResolution.height)
                {
                    lastResolution = Screen.currentResolution;
                    CalculateWindowPos();
                }
                ScaleGUI();
                var backgroundColor = GUI.backgroundColor;
                var color = GUI.color;
                GUI.backgroundColor = Color.black;
                GUI.color = Color.black;
                windowRect = GUILayout.Window(0, windowRect, WindowFunction, "", window, GUILayout.Height(windowSize.y));
                GUI.backgroundColor = backgroundColor;
                GUI.color = color;
            }
        }
        
        private void CalculateWindowPos()
        {
            windowSize = new Vector2(960, 720);
            windowRect = new Rect((Screen.width - windowSize.x) / 2f, (Screen.height - windowSize.y) / 2f + 100f, 0, 0);
        }

        string[] tabs = { "Mods", "Settings", "Log"};
        private void WindowFunction(int windowId)
        {
            if (Input.GetKey(KeyCode.LeftControl))
            {
                GUI.DragWindow(windowRect);
            }

            UnityAction buttons = () => { };

            GUILayout.BeginVertical("box");
            GUILayout.Label("Frogtown Mod Manager 2.0.3", h1);
            GUILayout.BeginVertical("box");

            GUILayout.Space(3);
            int tab = tabId;
            
            tab = GUILayout.Toolbar(tab, tabs, button, GUILayout.ExpandWidth(false));
            if (tab != tabId)
            {
                tabId = tab;
            }

            GUILayout.Space(5);

            if(tabId == 0)
            {
                DrawModTab(ref buttons);
            }
            if (tabId == 1)
            {
                DrawSettingsTab(ref buttons);
            }
            if (tabId == 2)
            {
                DrawLogTab(ref buttons);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Close", "Close the overlay."), button, GUILayout.ExpandWidth(false)))
            {
                ToggleWindow();
            }
            if (GUILayout.Button(new GUIContent("Mod Folder", "Open the plugin folder where mod DLLs should be placed."), button, GUILayout.ExpandWidth(false)))
            {
                string url = "file://" + BepInEx.Paths.PluginPath;
                FrogtownShared.Log("FrogShared", LogLevel.Info, "Openning " + url);
                Application.OpenURL(url);
            }
            buttons();

            GUILayout.Label(GUI.tooltip);

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndVertical();
        }

        private void DrawLogTab(ref UnityAction buttons)
        {
            var minWidth = GUILayout.MinWidth(windowSize.x);
            scrollPositions[2] = GUILayout.BeginScrollView(scrollPositions[2], minWidth, GUILayout.ExpandHeight(false));
            var amountWidth = columns.Where(x => !x.skip).Sum(x => x.width);
            var expandWidth = columns.Where(x => x.expand && !x.skip).Sum(x => x.width);

            var colWidth = columns.Select(x =>
                x.expand
                    ? GUILayout.Width(x.width / expandWidth * (windowSize.x - 60 + expandWidth - amountWidth))
                    : GUILayout.Width(x.width)).ToArray();

            GUILayout.BeginVertical("box");
            foreach(string s in FrogtownShared.log)
            {
                GUILayout.Label(s, richtext);
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        private void DrawModTab(ref UnityAction buttons)
        {
            var minWidth = GUILayout.MinWidth(windowSize.x);
            scrollPositions[0] = GUILayout.BeginScrollView(scrollPositions[0], minWidth, GUILayout.ExpandHeight(false));
            var amountWidth = columns.Where(x => !x.skip).Sum(x => x.width);
            var expandWidth = columns.Where(x => x.expand && !x.skip).Sum(x => x.width);

            var colWidth = columns.Select(x =>
                x.expand
                    ? GUILayout.Width(x.width / expandWidth * (windowSize.x - 60 + expandWidth - amountWidth))
                    : GUILayout.Width(x.width)).ToArray();

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal("box");
            for (int i = 0; i < columns.Count; i++)
            {
                if (columns[i].skip)
                {
                    continue;
                }
                GUILayout.Label(columns[i].name, colWidth[i]);
            }

            GUILayout.EndHorizontal();
            var GUIDs = ModManager.modDetails.Keys.ToArray();
            Array.Sort(GUIDs, (a, b) => {
                ModManager.modDetails.TryGetValue(a, out ModDetails details);
                string aAuthor = details.frogtownModDetails?.githubAuthor ?? "Unknown";

                ModManager.modDetails.TryGetValue(b, out details);
                string bAuthor = details.frogtownModDetails?.githubAuthor ?? "Unknown";

                return aAuthor.CompareTo(bAuthor);
            });

            List<ModRow> rows = new List<ModRow>();

            string lastAuthor = "";
            ModRow authorRow = null;
            foreach(string GUID in GUIDs)
            {
                ModManager.modDetails.TryGetValue(GUID, out ModDetails details);
                if (ModManager.whitelistFrameworkUnlisted.Contains(details.GUID))
                {
                    continue;
                }

                string author = details.frogtownModDetails?.githubAuthor ?? "Unknown";
                if(author != lastAuthor)
                {
                    if(authorRow != null)
                    {
                        rows.Add(authorRow);
                        authorRow = null;
                    }
                    if (collapsedAuthors.Contains(author))
                    {
                        authorRow = new ModRow();
                        ModRow rowAnchor = authorRow;
                        authorRow.canToggle = ModManager.CanToggleMod(GUID, out bool isActive);
                        if (authorRow.canToggle)
                        {
                            authorRow.isActive = isActive;
                        }
                        authorRow.githubAuthor = author;
                        if(authorRow.githubAuthor != "Unknown")
                        {
                            authorRow.url = "https://github.com/" + author;
                            authorRow.modName = "All mods from " + author;
                        }
                        else
                        {
                            authorRow.modName = "All mods from unknown authors";
                        }
                        authorRow.isAuthorCollapsed = true;
                        authorRow.description = details.modName;
                        authorRow.firstForAuthor = true;

                        if(details.frogtownModDetails == null && details.enabled != details.initialEnabled)
                        {
                            authorRow.statusStyle = red;
                            authorRow.statusMessage = "Mod will be " + (details.enabled ? "enabled" : "disabled") + " the next time the game is started.";
                        }

                        authorRow.onToggleActive += () =>
                        {
                            FrogtownShared.Log("FrogShared", LogLevel.Info, "Setting mods from " + rowAnchor.githubAuthor + " to " + !rowAnchor.isActive);
                            foreach (string otherGUID in GUIDs)
                            {
                                ModManager.modDetails.TryGetValue(otherGUID, out ModDetails otherDetails);
                                string otherAuthor = otherDetails.frogtownModDetails?.githubAuthor ?? "Unknown";
                                if (otherAuthor == author)
                                {
                                    if(ModManager.CanToggleMod(otherGUID, out bool unused))
                                    {
                                        ModManager.ToggleMod(otherGUID, !rowAnchor.isActive);
                                    }
                                }
                            }
                        };

                        authorRow.onToggleAuthor += () =>
                        {
                            collapsedAuthors.Remove(rowAnchor.githubAuthor);
                        };
                        lastAuthor = author;

                        continue;
                    }
                }

                if (authorRow != null)
                {
                    bool canToggle = ModManager.CanToggleMod(GUID, out bool isActive);
                    authorRow.canToggle = authorRow.canToggle || canToggle;
                    if (canToggle)
                    {
                        authorRow.isActive = authorRow.isActive || isActive;
                    }
                    authorRow.description += ", " + details.modName;

                    if (details.frogtownModDetails == null && details.enabled != details.initialEnabled)
                    {
                        authorRow.statusStyle = red;
                        authorRow.statusMessage = "Mod will be " + (details.enabled ? "enabled" : "disabled") + " the next time the game is started.";
                    }
                }
                else
                {
                    ModRow row = new ModRow();
                    row.canToggle = ModManager.CanToggleMod(GUID, out bool isActive);
                    row.isActive = isActive;

                    row.githubAuthor = author;
                    if (details.frogtownModDetails != null)
                    {
                        row.url = "https://github.com/" + details.frogtownModDetails.githubAuthor + "/" + details.frogtownModDetails.githubRepo;
                    }
                    row.isAuthorCollapsed = false;
                    row.modName = details.modName;
                    row.description = details.frogtownModDetails?.description ?? "";
                    row.firstForAuthor = author != lastAuthor;
                    row.version = details.version;
                    row.newVersionLoading = details.frogtownModDetails?.newVersionLoading ?? false;
                    row.newVersion = details.frogtownModDetails?.newVersion ?? "";
                    row.onToggleActive += () =>
                    {
                        ModManager.ToggleMod(GUID, !row.isActive);
                    };
                    row.onToggleAuthor += () =>
                    {
                        collapsedAuthors.Add(row.githubAuthor);
                    };

                    if (details.frogtownModDetails == null && details.enabled != details.initialEnabled)
                    {
                        row.statusStyle = red;
                        row.statusMessage = "Mod will be " + (details.enabled ? "enabled" : "disabled") + " the next time the game is started.";
                    }

                    rows.Add(row);
                }

                lastAuthor = author;
            }

            if (authorRow != null)
            {
                rows.Add(authorRow);
            }

            foreach (var row in rows)
            {
                int col = -1;
                GUILayout.BeginHorizontal("box");
                    GUILayout.BeginHorizontal(colWidth[++col]);
                        if(row.firstForAuthor)
                        {
                            bool newIsCollapsed = row.isAuthorCollapsed;
                            newIsCollapsed = GUILayout.Toggle(newIsCollapsed, new GUIContent("", "Collapse mods from " + row.githubAuthor + "."));
                            if (newIsCollapsed != row.isAuthorCollapsed)
                            {
                                row.onToggleAuthor?.Invoke();
                            }
                        }
                        else
                        {
                            GUILayout.Label(" ", GUILayout.Width(56f));
                        }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal(colWidth[++col]);
                        GUILayout.Label(new GUIContent(row.githubAuthor, row.description));
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal(colWidth[++col]);
                        GUILayout.Label(new GUIContent(row.modName, row.description));
                
                        GUILayout.FlexibleSpace();
                        if(!string.IsNullOrEmpty(row.url))
                        {
                            if (GUILayout.Button(new GUIContent("www", row.url), button))
                            {
                                Application.OpenURL(row.url);
                            }
                        }
                        else
                        {
                            GUILayout.Label(new GUIContent("---", "No repository available."));
                        }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal(colWidth[++col]);
                        GUILayout.Label(row.version, curVersion, GUILayout.ExpandWidth(false));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal(colWidth[++col]);
                        if (row.githubAuthor != "Unknown" && !row.isAuthorCollapsed)
                        {
                            if (row.newVersionLoading)
                            {
                                GUILayout.Label(new GUIContent("...", "Checking latest release..."));
                            }
                            else
                            {
                                if (!string.IsNullOrEmpty(row.newVersion))
                                {
                                    if (GUILayout.Button(row.newVersion, h2))
                                    {
                                        Application.OpenURL(row.url + "/releases");
                                    }
                                }
                                else
                                {
                                    GUILayout.Label(new GUIContent("---", "No new version found."));
                                }
                            }
                        }
                        else
                        {
                            GUILayout.Label(new GUIContent("---", "Can't load releases for " + row.modName + "."));
                        }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal(colWidth[++col]);
                        if (row.canToggle)
                        {
                            bool newIsActive = row.isActive;
                            newIsActive = GUILayout.Toggle(row.isActive, new GUIContent("", (row.isActive ? "Disable" : "Enable") + " " + row.modName + "."));
                            if (newIsActive != row.isActive)
                            {
                                row.onToggleActive?.Invoke();
                            }
                        }
                        else
                        {
                            GUILayout.Label(new GUIContent("X", row.modName + " cannot be disabled."), nodisable);
                        }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal(colWidth[++col]);
                        if (!string.IsNullOrEmpty(row.statusMessage))
                        {
                            GUILayout.Label(new GUIContent("R", row.statusMessage), row.statusStyle);
                        }
                    GUILayout.EndHorizontal();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        private void DrawSettingsTab(ref UnityAction buttons)
        {
            var minWidth = GUILayout.MinWidth(windowSize.x);
            scrollPositions[1] = GUILayout.BeginScrollView(scrollPositions[1], minWidth);

            var GUIDs = ModManager.modDetails.Keys.ToArray();
            Array.Sort(GUIDs, (a, b) => {
                ModManager.modDetails.TryGetValue(a, out ModDetails details);
                string aAuthor = details.frogtownModDetails?.githubAuthor ?? "Unknown";

                ModManager.modDetails.TryGetValue(b, out details);
                string bAuthor = details.frogtownModDetails?.githubAuthor ?? "Unknown";

                return aAuthor.CompareTo(bAuthor);
            });

            foreach (string GUID in GUIDs)
            {
                ModManager.modDetails.TryGetValue(GUID, out ModDetails details);
                if(details == null || details.frogtownModDetails == null || details.frogtownModDetails.OnGUI == null)
                {
                    continue;
                }

                GUILayout.Label(new GUIContent(details.modName, details.frogtownModDetails.description), h2);
                GUILayout.BeginVertical("box");
                details.frogtownModDetails.OnGUI();
                GUILayout.EndVertical();

            }

            GUILayout.Space(5);

            GUILayout.EndScrollView();
        }

        public void ToggleWindow()
        {
            ToggleWindow(!isOpen);
        }

        public void ToggleWindow(bool newIsOpen)
        {
            if (newIsOpen == isOpen)
            {
                return;
            }

            if (newIsOpen)
            {
                hasLaunched = true;
            }

            try
            {
                isOpen = newIsOpen;
                if (newIsOpen)
                {
                    GameCursorLocked = Cursor.lockState == CursorLockMode.Locked || !Cursor.visible;
                    if (GameCursorLocked)
                    {
                        Cursor.visible = true;
                        Cursor.lockState = CursorLockMode.None;
                    }
                }
                else
                {
                    if (GameCursorLocked)
                    {
                        Cursor.visible = false;
                        Cursor.lockState = CursorLockMode.Locked;
                    }
                }
            }
            catch (Exception e)
            {
                //Logger.LogException("ToggleWindow", e);
            }
        }

        private static RectOffset RectOffset(int value)
        {
            return new RectOffset(value, value, value, value);
        }

        private static RectOffset RectOffset(int x, int y)
        {
            return new RectOffset(x, x, y, y);
        }

        class Column
        {
            public string name;
            public float width;
            public bool expand = false;
            public bool skip = false;
        }

        class ModRow
        {
            public string githubAuthor;
            public string modName;
            public string version;
            public string url;
            public string description;
            public string newVersion;
            public bool newVersionLoading;
            public bool isActive;
            public bool canToggle;
            public bool firstForAuthor;
            public bool isAuthorCollapsed;
            public string statusMessage;
            public GUIStyle statusStyle;

            public UnityAction onToggleAuthor = null;
            public UnityAction onToggleActive = null;
        }

        internal class UISettings
        {
            public bool showOnStart;
            public float scale;
        }
    }
}
