using BepInEx;
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
        public static GUIStyle window = null;
        public static GUIStyle h1 = null;
        public static GUIStyle h2 = null;
        public static GUIStyle bold = null;
        public static GUIStyle button = null;
        public static GUIStyle settings = null;
        public static GUIStyle status = null;
        public static GUIStyle www = null;
        public static GUIStyle updates = null; 
        public static GUIStyle richtext = null;

        public bool hasLaunched;
        public bool hasInit;

        public Rect windowRect = new Rect(0, 0, 0, 0);
        public Vector2 windowSize = Vector2.zero;
        public Resolution lastResolution;

        public float pendingScale = 1f;
        public bool isOpen;
        public int tabId = 0;
        public Vector2[] scrollPositions = null;
        private List<Column> columns = new List<Column>();
        private bool GameCursorLocked { get; set; }

        private UISettings uiSettings = new UISettings();
        private ModInfo[] allModInfo;

        private HashSet<string> collapsedAuthors = new HashSet<string>();

        void Awake()
        {
            DontDestroyOnLoad(this);
            windowSize = new Vector2(960, 720);
            scrollPositions = new Vector2[3];

            allModInfo =
                (from a in AppDomain.CurrentDomain.GetAssemblies()
                 from t in a.GetTypes()
                 let attributeList = t.GetCustomAttributes(typeof(BepInPlugin), true).Take(1)
                 where attributeList != null && attributeList.Count() > 0
                 let attribute = attributeList.First() as BepInPlugin
                 select new ModInfo() {
                     GUID = attribute.GUID,
                     version = attribute.Version.ToString(),
                     modName = attribute.Name
                 }
                 ).ToArray();

            foreach(var info in allModInfo)
            {
                if(FrogtownShared.allModDetails.TryGetValue(info.GUID, out ModDetails deetz))
                {
                    info.githubAuthor = deetz.githubAuthor;
                    info.githubRepo = deetz.githubRepo;
                    info.description = deetz.description;
                    info.details = deetz;
                    deetz.SetVersion(info.version);
                }
                else
                {
                    info.githubAuthor = "Unknown";
                    info.githubRepo = "";
                    info.description = "No Description.";
                }
            }

            Array.Sort(allModInfo, (a, b) => { return a.githubAuthor.CompareTo(b.githubAuthor); });

            LoadSettings();
        }

        private void SaveSettings()
        {
            var config = FrogtownShared.GetConfig();
            config.Wrap("ui", "scale", "", "1").Value = uiSettings.scale.ToString();
            config.Wrap("ui", "showonstart", "", "true").Value = uiSettings.showOnStart.ToString();
            
            foreach (var modInfo in allModInfo)
            {
                if (!string.IsNullOrEmpty(modInfo.GUID))
                {
                    if (FrogtownShared.CanToggleMod(modInfo.GUID, out bool isActive))
                    {
                        config.Wrap("mods", modInfo.GUID, "", "true").Value = isActive.ToString();
                    }
                }
            }

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

            foreach (var modInfo in allModInfo)
            {
                if (FrogtownShared.CanToggleMod(modInfo.GUID, out bool isActive))
                {
                    raw = config.Wrap("mods", modInfo.GUID, "", "true").Value;
                    bool.TryParse(raw, out bool shouldBeActive);
                    if (isActive != shouldBeActive) {
                        FrogtownShared.SetModStatus(modInfo.GUID, shouldBeActive);
                    }
                }
            }
        }

        void Start()
        {
            CalculateWindowPos();
            if (uiSettings.showOnStart)
            {
                ToggleWindow(true);
            }
        }

        void OnDestroy()
        {

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
            window.name = "umm window";
            //window.normal.background = Textures.Window;
            //window.normal.background.wrapMode = TextureWrapMode.Repeat;

            h1 = new GUIStyle();
            h1.name = "umm h1";
            h1.normal.textColor = Color.white;
            h1.fontStyle = FontStyle.Bold;
            h1.alignment = TextAnchor.MiddleCenter;

            h2 = new GUIStyle();
            h2.name = "umm h2";
            h2.normal.textColor = new Color(0.6f, 0.91f, 1f);
            h2.fontStyle = FontStyle.Bold;

            bold = new GUIStyle(GUI.skin.label);
            bold.name = "umm bold";
            bold.normal.textColor = Color.white;
            bold.fontStyle = FontStyle.Bold;

            button = new GUIStyle(GUI.skin.button);
            button.name = "umm button";

            settings = new GUIStyle();
            settings.alignment = TextAnchor.MiddleCenter;
            settings.stretchHeight = true;

            status = new GUIStyle();
            status.alignment = TextAnchor.MiddleCenter;
            status.stretchHeight = true;

            www = new GUIStyle();
            www.alignment = TextAnchor.MiddleCenter;
            www.stretchHeight = true;

            updates = new GUIStyle();
            updates.alignment = TextAnchor.MiddleCenter;
            updates.stretchHeight = true;

            richtext = new GUIStyle();
            richtext.richText = true;
            richtext.normal.textColor = Color.white;


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
            GUI.skin.button.padding = new RectOffset(Scale(10), Scale(10), Scale(3), Scale(3));
            GUI.skin.button.margin = RectOffset(Scale(4), Scale(2));

            GUI.skin.horizontalSlider.fixedHeight = Scale(12);
            GUI.skin.horizontalSlider.border = RectOffset(3, 0);
            GUI.skin.horizontalSlider.padding = RectOffset(0, 0);
            GUI.skin.horizontalSlider.margin = RectOffset(Scale(4), Scale(8));

            GUI.skin.horizontalSliderThumb.fixedHeight = Scale(12);
            GUI.skin.horizontalSliderThumb.border = RectOffset(4, 0);
            GUI.skin.horizontalSliderThumb.padding = RectOffset(Scale(7), 0);
            GUI.skin.horizontalSliderThumb.margin = RectOffset(0);

            GUI.skin.toggle.margin.left = Scale(10);

            window.padding = RectOffset(Scale(5));
            h1.fontSize = Scale(16);
            h1.margin = RectOffset(Scale(0), Scale(5));
            h2.fontSize = Scale(13);
            h2.margin = RectOffset(Scale(0), Scale(3));
            button.fontSize = Scale(13);
            button.padding = RectOffset(Scale(30), Scale(5));

            int iconHeight = 28;
            settings.fixedWidth = Scale(24);
            settings.fixedHeight = Scale(iconHeight);
            status.fixedWidth = Scale(12);
            status.fixedHeight = Scale(iconHeight);
            www.fixedWidth = Scale(24);
            www.fixedHeight = Scale(iconHeight);
            updates.fixedWidth = Scale(26);
            updates.fixedHeight = Scale(iconHeight);
        }

        public int Scale(int value)
        {
            return (int)(value * uiSettings.scale);
        }

        private float Scale(float value)
        {
            return value * uiSettings.scale;
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
            GUILayout.Label("Frogtown Mod Manager 2.0.0", h1);
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

            GUILayout.Label(GUI.tooltip);

            buttons();
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

            List<ModRow> rows = new List<ModRow>();

            string lastAuthor = "";
            ModRow authorRow = null;
            foreach(var modInfo in allModInfo)
            {
                if(modInfo.githubAuthor != lastAuthor)
                {
                    if(authorRow != null)
                    {
                        rows.Add(authorRow);
                        authorRow = null;
                    }
                    if (collapsedAuthors.Contains(modInfo.githubAuthor))
                    {
                        authorRow = new ModRow();
                        ModRow rowAnchor = authorRow;
                        authorRow.canToggle = FrogtownShared.CanToggleMod(modInfo.GUID, out bool isActive);
                        if (authorRow.canToggle)
                        {
                            authorRow.isActive = isActive;
                        }
                        authorRow.githubAuthor = modInfo.githubAuthor;
                        if(authorRow.githubAuthor != "Unknown")
                        {
                            authorRow.url = "https://github.com/" + modInfo.githubAuthor;
                        }
                        authorRow.isAuthorCollapsed = true;
                        authorRow.modName = "All mods from " + modInfo.githubAuthor;
                        authorRow.description = modInfo.modName;
                        authorRow.firstForAuthor = true;

                        authorRow.onToggleActive += () =>
                        {
                            FrogtownShared.Log("FrogShared", LogLevel.Info, "Setting mods from " + rowAnchor.githubAuthor + " to " + !rowAnchor.isActive);
                            foreach (var modFromAuthor in allModInfo)
                            {
                                if(modFromAuthor.githubAuthor == rowAnchor.githubAuthor)
                                {
                                    if(FrogtownShared.CanToggleMod(modFromAuthor.GUID, out bool unused))
                                    {
                                        FrogtownShared.SetModStatus(modFromAuthor.GUID, !rowAnchor.isActive);
                                    }
                                }
                            }
                        };

                        authorRow.onToggleAuthor += () =>
                        {
                            collapsedAuthors.Remove(rowAnchor.githubAuthor);
                        };
                        lastAuthor = modInfo.githubAuthor;

                        continue;
                    }
                }

                if (authorRow != null)
                {
                    bool canToggle = FrogtownShared.CanToggleMod(modInfo.GUID, out bool isActive);
                    authorRow.canToggle = authorRow.canToggle || canToggle;
                    if (canToggle)
                    {
                        authorRow.isActive = authorRow.isActive || isActive;
                    }
                    authorRow.description += ", " + modInfo.modName;
                }
                else
                {
                    ModRow row = new ModRow();
                    row.canToggle = FrogtownShared.CanToggleMod(modInfo.GUID, out bool isActive);
                    row.isActive = isActive;

                    row.githubAuthor = modInfo.githubAuthor;
                    if (row.githubAuthor != "Unknown")
                    {
                        row.url = "https://github.com/" + modInfo.githubAuthor + "/" + modInfo.githubRepo;
                    }
                    row.isAuthorCollapsed = false;
                    row.modName = modInfo.modName;
                    row.description = modInfo.modName;
                    row.firstForAuthor = modInfo.githubAuthor != lastAuthor;
                    row.version = modInfo.version;
                    if(modInfo.details != null)
                    {
                        row.newVersionLoading = modInfo.details.newVersionLoading;
                        row.newVersion = modInfo.details.newVersion;
                    }
                    row.onToggleActive += () =>
                    {
                        FrogtownShared.SetModStatus(modInfo.GUID, !row.isActive);
                    };
                    row.onToggleAuthor += () =>
                    {
                        FrogtownShared.Log("FrogShared", LogLevel.Error, "Error test on collapse");
                        collapsedAuthors.Add(row.githubAuthor);
                    };
                    rows.Add(row);
                }

                lastAuthor = modInfo.githubAuthor;
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
                        GUILayout.Label(row.version, GUILayout.ExpandWidth(false));
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
                            GUILayout.Toggle(true, new GUIContent("", row.modName + " cannot be disabled."));
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


            GUILayout.BeginHorizontal();
            uiSettings.showOnStart = GUILayout.Toggle(uiSettings.showOnStart, "Show this window on startup", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            /*
            GUILayout.BeginVertical("box");
            GUILayout.Label("UI", bold, GUILayout.ExpandWidth(false));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Scale", GUILayout.ExpandWidth(false), GUILayout.ExpandWidth(false));
            pendingScale = GUILayout.HorizontalSlider(pendingScale, 0.5f, 2f, GUILayout.Width(200));
            GUILayout.Label(" " + pendingScale.ToString("f2"), GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            buttons += () =>
            {
                if (GUILayout.Button("Apply And Close", button, GUILayout.ExpandWidth(false)))
                {
                    uiSettings.scale = pendingScale;
                    ToggleWindow();
                    SaveSettings();
                }

                if (GUILayout.Button("Apply", button, GUILayout.ExpandWidth(false)))
                {
                    uiSettings.scale = pendingScale;
                    SaveSettings();
                }
            };
            GUILayout.EndVertical();
            */

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
            public string dllFile;

            public UnityAction onToggleAuthor = null;
            public UnityAction onToggleActive = null;
        }

        class UISettings
        {
            public bool showOnStart;
            public float scale;
        }

        class ModInfo
        {
            public string GUID;
            public string githubAuthor;
            public string githubRepo;
            public string modName;
            public string description;
            public string version;
            public ModDetails details;
        }
    }
}
