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

        public bool showProps = true;

        // live link relevant settings
        // control area
        public string lastExportPath;
        public bool isClientLocal = true;
        public string lastSuccessfulHost;
        public string[] lastTriedHosts;

        // import area
        public bool simpleMode = true;
        public string importDestinationFolder;
        public bool importIntoScene = true;
        public bool useCurrentScene;
        public bool addToTimeline = true;
        public bool lockTimelineToLast = true;

        public string sceneReference;
        public string[] recentSceneRefs;
        public string lastSaveFolder;
    }
}
