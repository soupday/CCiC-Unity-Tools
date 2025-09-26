/* 
 * Copyright (C) 2025 Victor Soupday
 * This file is part of CC_Unity_Tools <https://github.com/soupday/CC_Unity_Tools>
 * 
 * CC_Unity_Tools is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * CC_Unity_Tools is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with CC_Unity_Tools.  If not, see <https://www.gnu.org/licenses/>.
 */

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

        public bool criticalUpdateRequired;

        public bool pendingShaderUninstall;
        public bool pendingRuntimeUninstall;

        public bool pendingShaderInstall;
        public bool pendingRuntimeInstall;

        public bool performPostInstallationCheck;
        public bool performPostInstallationRuntimeCheck;

        public string updateMessage;

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
        public string shaderToolVersion;
        public string runtimeToolVersion;

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

        public List<Object> activeTimeLines;
        public bool showPlayerAfterPlayMode;
        public bool showRetargetAfterPlayMode;
    }
}
