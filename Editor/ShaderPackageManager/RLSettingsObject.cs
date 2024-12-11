using System.Collections.Generic;
using UnityEngine;

namespace Reallusion.Import
{
    public class RLSettingsObject : ScriptableObject
    {
        public bool showOnStartup;
        public bool ignoreAllErrors;
        public bool checkForUpdates;
        public bool updateWindowShownOnce;
        public bool postInstallShowUpdateWindow;
        public bool postInstallShowPopupNotWindow;
        public bool performPostInstallationCheck;
        public bool updateAvailable;
        public long lastUpdateCheck;
        public string jsonTagName;
        public string jsonHtmlUrl;
        public string jsonPublishedAt;
        public string [] jsonBodyLines;
        //public List<RLToolUpdateUtil.JsonFragment> fullJsonFragment;
        public string fullJsonFragment;
        public string lastPath;
        public string toolVersion;
    }
}
