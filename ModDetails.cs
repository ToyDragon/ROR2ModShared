using BepInEx;
using BepInEx.Logging;
using UnityEngine.Events;

namespace Frogtown
{
    public class ModDetails
    {
        public string modName;

        public string dllFileName;
        public string dllFileFullPath;

        public string GUID { get; private set; }
        public string version { get; private set; }

        public bool enabled { get; internal set; }
        public string releaseUrl { get; internal set; }
        public bool newVersionLoading { get; internal set; }

        public bool initialEnabled { get; internal set; }

        public delegate void AfterToggleHandler(ModDetails details);
        public AfterToggleHandler afterToggle = null;
        public FrogtownModDetails frogtownModDetails = null;

        public ModDetails(BepInPlugin attribute, string dllFileFullPath)
        {
            GUID = attribute.GUID;
            modName = attribute.Name;
            version = attribute.Version.ToString();
            this.dllFileFullPath = dllFileFullPath;
            int ix = dllFileFullPath.LastIndexOf("\\") + 1;
            dllFileName = dllFileFullPath.Substring(ix, dllFileFullPath.Length - ix);
        }

        public ModDetails(string dllName)
        {
            dllFileName = dllName;
            modName = dllName.Substring(0, dllName.IndexOf("."));
            int ix = modName.LastIndexOf("\\") + 1;
            modName = modName.Substring(ix, modName.Length - ix);
            enabled = false;
        }

        internal void SetVersion(string version)
        {
            this.version = version;
        }
    }

    public class FrogtownModDetails
    {

        public string GUID;
        public string githubAuthor;
        public string githubRepo;
        public string description;
        public string newVersion;
        public bool noDisable;

        public bool isNotCheaty { get; private set; }
        public string releaseUrl { get; internal set; }
        public bool newVersionLoading { get; internal set; }
        public UnityAction OnGUI;
        public bool enabled
        {
            get { return modDetails.enabled; }
        }

        internal ModDetails modDetails;

        public FrogtownModDetails(string GUID)
        {
            this.GUID = GUID;
        }

        public void OnlyContainsBugFixesOrUIChangesThatArentContriversial()
        {
            isNotCheaty = true;
        }

        public void Log(LogLevel level, string message)
        {
            FrogtownShared.Log(GUID, level, message);
        }
    }
}
