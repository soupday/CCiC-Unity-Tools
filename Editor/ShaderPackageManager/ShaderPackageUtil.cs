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

using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager;
using Object = UnityEngine.Object;

namespace Reallusion.Import
{
    public static class ShaderPackageUtil
    {
        public const string emptyVersion = "0.0.0";
        public const string urpType = "UnityEngine.Rendering.Universal.UniversalRenderPipeline";
        public const string urpPackage = "com.unity.render-pipelines.universal";
        public const string hdrpType = "UnityEngine.Rendering.HighDefinition.HDRenderPipeline";
        public const string hdrpPackage = "com.unity.render-pipelines.high-definition";

        //public static bool isWaiting = false;
        //public static bool hasFinished = false;

        public static void UpdateManagerUpdateCheck()
        {
            //Debug.LogWarning("STARTING ShaderPackageUtil CHECKS");
            //ShaderPackageUtil.GetInstalledPipelineVersion();
            FrameTimer.CreateTimer(10, FrameTimer.initShaderUpdater, ShaderPackageUtil.ImporterWindowInitCallback);
        }

        public static void UpdaterWindowCheckStatus()
        {
            PackageCheckDone -= UpdaterWindowCheckStatusDone;
            PackageCheckDone += UpdaterWindowCheckStatusDone;
            FrameTimer.CreateTimer(10, FrameTimer.initShaderUpdater, ShaderPackageUtil.ImporterWindowInitCallback);
        }

        public static void UpdaterWindowCheckStatusDone(object sender, EventArgs e)
        {
            PackageCheckDone -= UpdaterWindowCheckStatusDone;
            DetermineShaderAction();
            DetermineRuntimeAction();
            if (UpdateManager.determinedShaderAction != null && UpdateManager.determinedRuntimeAction != null)
            {
                // some abbreviated determination to open the action pane if needed
                bool force = UpdateManager.determinedShaderAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.UninstallReinstall_force || UpdateManager.determinedShaderAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.Error;
                bool optional = UpdateManager.determinedShaderAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.UninstallReinstall_optional;

                bool runForce = UpdateManager.determinedRuntimeAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.UninstallReinstall_force || UpdateManager.determinedRuntimeAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.Error || UpdateManager.determinedRuntimeAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.NothingInstalled_Install_force;
                bool runOptional = UpdateManager.determinedRuntimeAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.UninstallReinstall_optional;

                //Debug.LogWarning("runForce " + runForce);

                bool shaderActionRequired = force || optional; 
                bool runtimeActionRequired = runForce || runOptional;
                if (ShaderPackageUpdater.Instance != null)
                {
                    ShaderPackageUpdater.Instance.shaderActionRequired = shaderActionRequired;
                    ShaderPackageUpdater.Instance.runtimeActionRequired = runtimeActionRequired;
                }
            }
        }

        public static void ImporterWindowInitCallback(object obj, FrameTimerArgs args)
        {
            if (args.ident == FrameTimer.initShaderUpdater)
            {
                ShaderPackageUtilInit(true);
                FrameTimer.OnFrameTimerComplete -= ImporterWindowInitCallback;
            }
        }

        public static void ShaderPackageUtilInit(bool callback = false)
        {
            UpdateManager.determinedShaderAction = new ActionRules();
            ImporterWindow.SetGeneralSettings(RLSettings.FindRLSettingsObject(), false);
            if (ImporterWindow.generalSettings != null)
            {
                if (ImporterWindow.generalSettings.performPostInstallationCheck) ShaderPackageUtil.PostImportShaderPackageItemCompare();
                ImporterWindow.generalSettings.performPostInstallationCheck = false;

                if (ImporterWindow.generalSettings.performPostInstallationRuntimeCheck) ShaderPackageUtil.PostImportRuntimePackageItemCompare();
                ImporterWindow.generalSettings.performPostInstallationRuntimeCheck = false;
            }
            OnPipelineDetermined -= PipelineDetermined;
            OnPipelineDetermined += PipelineDetermined;
            GetInstalledPipelineVersion();
            
            if (callback) FrameTimer.OnFrameTimerComplete -= ShaderPackageUtil.ImporterWindowInitCallback;

            if (InitCompleted != null)
                InitCompleted.Invoke(null, null);
        }

        // async wait for UpdateManager to be updated
        public static event EventHandler OnPipelineDetermined;
        public static event EventHandler PackageCheckDone;
        public static event EventHandler InitCompleted;
        
        public static void PipelineDetermined(object sender, EventArgs e)
        {
            if (ImporterWindow.GeneralSettings != null)
            {
                //UpdateManager.availablePackages = BuildPackageMap();
                //Debug.LogWarning("Running ValidateInstalledShader");
                BuildPackageMaps();
                ValidateInstalledShader();
                ValidateInstalledRuntimes();
            }
            if (PackageCheckDone != null)
                PackageCheckDone.Invoke(null, null);

            OnPipelineDetermined -= PipelineDetermined;
        }
                
        public static void ValidateInstalledShader()
        {
            string[] manifestGUIDS = AssetDatabase.FindAssets("_RL_shadermanifest", new string[] { "Assets" });
            string guid = string.Empty;

            UpdateManager.currentPackageManifest = GetCurrentShaderForPipeline();
            UpdateManager.currentLegacyPackageManifest = GetCurrentLegacyShaderForPipeline();
            UpdateManager.missingShaderPackageItems = new List<ShaderPackageItem>();

            // consider simplest cases 'Nothing installed' 'One Shader Installed' 'Multiple shaders installed'
            // nothing and multiple are immediately returned - a single shader install is further examined
            if (manifestGUIDS.Length == 0)
            {
                UpdateManager.installedPackageStatus = InstalledPackageStatus.Absent;
                UpdateManager.shaderPackageValid = PackageVailidity.Absent;
                return; // action rule: Status: Absent  Vailidity: Absent
            }
            else if (manifestGUIDS.Length == 1)
            {
                guid = manifestGUIDS[0];
            }
            else if (manifestGUIDS.Length > 1)
            {
                UpdateManager.installedPackageStatus = InstalledPackageStatus.Multiple;
                UpdateManager.shaderPackageValid = PackageVailidity.Invalid;
                return; // action rule: Status: Multiple  Vailidity: Invalid
            }
            
            // examination of single installed shader
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            ShaderPackageManifest shaderPackageManifest = ReadJson(assetPath);
            if (shaderPackageManifest != null)
            {
                shaderPackageManifest.ManifestPath = assetPath;
                UpdateManager.installedShaderPipelineVersion = shaderPackageManifest.Pipeline;
                UpdateManager.installedShaderVersion = new Version(shaderPackageManifest.Version);
                // check shader is for the currently active pipeline
                if (shaderPackageManifest.Pipeline != UpdateManager.activePipelineVersion)
                {
                    UpdateManager.installedPackageStatus = InstalledPackageStatus.Mismatch;
                    UpdateManager.shaderPackageValid = PackageVailidity.Invalid;
                    return; // action rule: Status: Mismatch  Vailidity: Invalid
                }
            }
            else // check shader is real and is for the currently active pipeline
            {
                UpdateManager.installedPackageStatus = InstalledPackageStatus.Absent;
                UpdateManager.shaderPackageValid = PackageVailidity.Absent;
                return; // action rule: Status: Absent  Vailidity: Absent
            }
            // shader is for the correct pipeline
            // check the integrity of the installed shader
            shaderPackageManifest.ManifestPath = assetPath;
            //Debug.LogWarning("ValidateInstalledShader");
            UpdateManager.installedShaderPipelineVersion = shaderPackageManifest.Pipeline;
            UpdateManager.installedShaderVersion = new Version(shaderPackageManifest.Version);

            foreach (ShaderPackageItem item in shaderPackageManifest.Items)
            {
                item.Validated = false;
                string itemPath = string.Empty;
                string relToDataPath = string.Empty;
                string fullPath = string.Empty;

                itemPath = AssetDatabase.GUIDToAssetPath(string.IsNullOrEmpty(item.InstalledGUID) ? item.GUID : item.InstalledGUID);

                if (itemPath.Length > 6)
                    relToDataPath = itemPath.Remove(0, 6); // remove "Assets\"
                fullPath = Application.dataPath + relToDataPath;


                if (File.Exists(fullPath))
                {
                    item.Validated = true;
                }

                if (!item.Validated)
                {
                    UpdateManager.missingShaderPackageItems.Add(item);
                }
            }

            int invalidItems = shaderPackageManifest.Items.FindAll(x => x.Validated == false).Count();
            if (invalidItems == 0 && UpdateManager.missingShaderPackageItems.Count == 0)
            {
                // no missing or invalid items -- determine whether an upgrade is available
                UpdateManager.shaderPackageValid = PackageVailidity.Valid;

                if (UpdateManager.installedShaderPipelineVersion == UpdateManager.activePipelineVersion)
                {
                    // compare current release version with installed version
                    Version maxVersion = UpdateManager.currentPackageManifest.Version.ToVersion();
                    if (UpdateManager.installedShaderVersion == maxVersion)
                        UpdateManager.installedPackageStatus = InstalledPackageStatus.Current; // action rule: Status: Current  Vailidity: Valid
                    else if (UpdateManager.installedShaderVersion < maxVersion)
                        UpdateManager.installedPackageStatus = InstalledPackageStatus.Upgradeable; // action rule: Status: Upgradeable  Vailidity: Valid
                    else if (UpdateManager.installedShaderVersion > maxVersion)
                        UpdateManager.installedPackageStatus = InstalledPackageStatus.VersionTooHigh; // action rule: Status: VersionTooHigh  Vailidity: Valid
                }
                else // mismatch between installed and active shader pipeline version
                {
                    UpdateManager.installedPackageStatus = InstalledPackageStatus.Mismatch;
                    return; // action rule: Status: Mismatch  Vailidity: Valid
                }
            }
            else
            {
                // shader has missing files
                UpdateManager.installedPackageStatus = InstalledPackageStatus.MissingFiles;
                UpdateManager.shaderPackageValid = PackageVailidity.Invalid;
                return;  // action rule: Status: MissingFiles  Vailidity: Invalid
            }

            // required rules summary (the only state combinations that can be returned NB: versioning is only examined when the package is valid):
            // action rule: Status: Absent  Vailidity: Absent
            // action rule: Status: Multiple  Vailidity: Invalid
            // action rule: Status: Current  Vailidity: Valid
            // action rule: Status: Upgradeable  Vailidity: Valid
            // action rule: Status: VersionTooHigh  Vailidity: Valid
            // action rule: Status: Mismatch  Vailidity: Valid
            // action rule: Status: Mismatch  Vailidity: Invalid
            // action rule: Status: MissingFiles  Vailidity: Invalid
        }

        public static void ValidateInstalledRuntimes()
        {
            string[] manifestGUIDS = AssetDatabase.FindAssets("_RL_runtimemanifest", new string[] { "Assets" });
            string guid = string.Empty;

            UpdateManager.currentRuntimePackageManifest = GetMostRecentRuntimePackage();
            UpdateManager.missingRuntimePackageItems = new List<ShaderPackageItem>();

            if (manifestGUIDS.Length == 0)
            {
                UpdateManager.installedRuntimeStatus = InstalledPackageStatus.Absent;
                UpdateManager.runtimePackageValid = PackageVailidity.Absent;
                return; // action rule: Status: Absent  Vailidity: Absent
            }
            else if (manifestGUIDS.Length == 1)
            {
                guid = manifestGUIDS[0];
            }
            else if (manifestGUIDS.Length > 1)
            {
                UpdateManager.installedRuntimeStatus = InstalledPackageStatus.Multiple;
                UpdateManager.runtimePackageValid = PackageVailidity.Invalid;
                return; // action rule: Status: Multiple  Vailidity: Invalid
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            ShaderPackageManifest runtimePackageManifest = ReadJson(assetPath);
            if (runtimePackageManifest != null)
            {
                runtimePackageManifest.ManifestPath = assetPath;
                UpdateManager.installedRuntimeVersion = new Version(runtimePackageManifest.Version);

                foreach (ShaderPackageItem item in runtimePackageManifest.Items)
                {
                    item.Validated = false;
                    string itemPath = string.Empty;
                    string relToDataPath = string.Empty;
                    string fullPath = string.Empty;

                    itemPath = AssetDatabase.GUIDToAssetPath(string.IsNullOrEmpty(item.InstalledGUID) ? item.GUID : item.InstalledGUID);

                    if (itemPath.Length > 6)
                        relToDataPath = itemPath.Remove(0, 6); // remove "Assets\"
                    fullPath = Application.dataPath + relToDataPath;

                    if (File.Exists(fullPath))
                    {
                        item.Validated = true;
                    }

                    if (!item.Validated)
                    {
                        UpdateManager.missingRuntimePackageItems.Add(item);
                    }
                }

                int invalidItems = runtimePackageManifest.Items.FindAll(x => x.Validated == false).Count();

                if (invalidItems == 0 && UpdateManager.missingRuntimePackageItems.Count == 0)
                {
                    // no missing or invalid items -- determine whether an upgrade is available
                    UpdateManager.runtimePackageValid = PackageVailidity.Valid;

                    // compare current release version with installed version
                    Version maxVersion = UpdateManager.currentRuntimePackageManifest.Version.ToVersion();

                    if(UpdateManager.installedRuntimeVersion == maxVersion)
                        UpdateManager.installedRuntimeStatus = InstalledPackageStatus.Current; // action rule: Status: Current  Vailidity: Valid
                    else if (UpdateManager.installedRuntimeVersion < maxVersion)
                        UpdateManager.installedRuntimeStatus = InstalledPackageStatus.Upgradeable; // action rule: Status: Upgradeable  Vailidity: Valid
                    else if (UpdateManager.installedRuntimeVersion > maxVersion)
                        UpdateManager.installedRuntimeStatus = InstalledPackageStatus.VersionTooHigh; // action rule: Status: VersionTooHigh  Vailidity: Valid
                }
                else
                {
                    // shader has missing files
                    UpdateManager.installedRuntimeStatus = InstalledPackageStatus.MissingFiles;
                    UpdateManager.runtimePackageValid = PackageVailidity.Invalid;
                    return;  // action rule: Status: MissingFiles  Vailidity: Invalid
                }
            }
        }

        public static ShaderPackageManifest GetMostRecentRuntimePackage()
        {
            Version maxVersion = new Version(0, 0, 0);//null;
            ShaderPackageManifest maxVersionObject = null;

            foreach (var obj in UpdateManager.availableRuntimePackages)
            {
                if(Version.TryParse(obj.Version, out Version currentVersion))
                {
                    if (currentVersion > maxVersion)
                    {
                        maxVersion = currentVersion;
                        maxVersionObject = obj;
                    }
                }
                else
                {
                    Debug.Log("Error parsing version string");
                }                
            }
            return maxVersionObject;
        }

        public static ShaderPackageManifest GetCurrentShaderForPipeline()
        {
            if (UpdateManager.availablePackages != null)
            {
                List<ShaderPackageManifest> applicablePackages = UpdateManager.availablePackages.FindAll(x => x.Pipeline == UpdateManager.activePipelineVersion);

                if (applicablePackages.Count > 0)
                {
                    // determine the max available version
                    applicablePackages.Sort((a, b) => b.Version.ToVersion().CompareTo(a.Version.ToVersion()));  // descending sort

                    return applicablePackages[0];  // set the current release for the pipeline -- this is the default to be installed
                }
                else
                {
                    Debug.LogWarning("No shader packages available to install for this pipeline");
                    // no shader packages for the current pipeline are available
                    // will become important after Unity 6000 introduction of 'global pipeline'  when older tool versions are used.
                    UpdateManager.installedPackageStatus = InstalledPackageStatus.NoPackageAvailable;
                    return null;
                }
            }
            return null;
        }

        public static ShaderPackageManifest GetCurrentLegacyShaderForPipeline()
        {
            if (UpdateManager.availableLegacyShaderPackages != null)
            {
                List<ShaderPackageManifest> applicablePackages = UpdateManager.availableLegacyShaderPackages.FindAll(x => x.Pipeline == UpdateManager.activeLegacyPipelineVersion);

                if (applicablePackages.Count > 0)
                {
                    // determine the max available version
                    applicablePackages.Sort((a, b) => b.Version.ToVersion().CompareTo(a.Version.ToVersion()));  // descending sort

                    bool sortByDate = true;

                    if (sortByDate)
                    {
                        Version latestVersion = applicablePackages[0].Version.ToVersion();

                        List<ShaderPackageManifest> latestPackages = applicablePackages.FindAll(x => x.Version.ToVersion() == latestVersion);

                        if (latestPackages.Count > 1)
                        {
                            latestPackages.Sort((a, b) => (AssetImporter.GetAtPath(b.referenceShaderPackagePath).assetTimeStamp).CompareTo(AssetImporter.GetAtPath(a.referenceShaderPackagePath).assetTimeStamp));
                        }
                        return latestPackages[0];
                    }

                    return applicablePackages[0];
                }
                else
                {
                    return null;
                }
            }
            return null;
        }

        public static void DetermineShaderAction()
        {
            Func<DeterminedAction, InstalledPackageStatus, PackageVailidity, string, ActionRules> ActionRule = (action, status, validity, text) => new ActionRules(action, status, validity, text);

            // result cases
            string multiple = "Multiple shader packages detected. [Force] Uninstall all then install applicable package.";
            string mismatch = "Active pipeline doesnt match installed shader package. [Force] Uninstall then install applicable package.";
            string normalUpgrade = "Shader package can be upgraded. [Offer] install newer package.";
            string normalDowngrade = "Shader package is from a higher version of the tool. [Offer] install package version from this distribution.";
            string currentValid = "Current Shader is correctly installed and matches pipeline version";
            string freshInstall = "No shader is currently installed, an appropriate version will be imported.";
            string missingFiles = "Files are missing from the installed shader. Uninstall remaining files and install current shader version.";
            string incompatible = "The currently installed pipeline is incompatible with CC/iC Unity tools.  A minimum version of URP v10 or HDRP v10 is required.  Only the Built-in version is supported in this circumstance.  This will require changing the render pipeline to the built-in version to continue.";

            List<ActionRules> ActionRulesList = new List<ActionRules>
            {
                ActionRule(DeterminedAction.NothingInstalled_Install_force, InstalledPackageStatus.Absent, PackageVailidity.Absent, freshInstall),
                ActionRule(DeterminedAction.Error, InstalledPackageStatus.Multiple, PackageVailidity.Invalid, multiple),
                ActionRule(DeterminedAction.CurrentValid, InstalledPackageStatus.Current, PackageVailidity.Valid, currentValid),
                ActionRule(DeterminedAction.UninstallReinstall_optional, InstalledPackageStatus.Upgradeable, PackageVailidity.Valid, normalUpgrade),
                ActionRule(DeterminedAction.UninstallReinstall_optional, InstalledPackageStatus.VersionTooHigh, PackageVailidity.Valid, normalDowngrade),
                ActionRule(DeterminedAction.UninstallReinstall_force, InstalledPackageStatus.Mismatch, PackageVailidity.Valid, mismatch),
                ActionRule(DeterminedAction.UninstallReinstall_force, InstalledPackageStatus.Mismatch, PackageVailidity.Invalid, mismatch),
                ActionRule(DeterminedAction.UninstallReinstall_force, InstalledPackageStatus.MissingFiles, PackageVailidity.Invalid, missingFiles),
                ActionRule(DeterminedAction.Incompatible, InstalledPackageStatus.Mismatch, PackageVailidity.Invalid, incompatible)
            };

            ActionRules actionobj = null;
            List<ActionRules> packageStatus = null;

            // special case where the installed pipeline version is too low to be supported 
            // resulting in UpdateManager.activePipelineVersion == PipelineVersion.Incompatible
            // do not alllow the update to try anything until the user corrects the situation
            if (UpdateManager.activePipelineVersion == PipelineVersion.Incompatible)
            {
                UpdateManager.determinedShaderAction = ActionRulesList.Find(y => y.DeterminedShaderAction == DeterminedAction.Incompatible);
                // new ShaderActionRules(DeterminedShaderAction.Incompatible, InstalledPackageStatus.Mismatch, PackageVailidity.Invalid, incompatible);
                Debug.LogWarning("Incompatible render pipeline. No shader install/update action could be determined.");
                return;
            }

            packageStatus = ActionRulesList.FindAll(x => x.InstalledPackageStatus == UpdateManager.installedPackageStatus);
            if (UpdateManager.shaderPackageValid != PackageVailidity.Waiting || UpdateManager.shaderPackageValid != PackageVailidity.None)
                actionobj = packageStatus.Find(y => y.PackageVailidity == UpdateManager.shaderPackageValid);

            if (actionobj != null)
            {
                UpdateManager.determinedShaderAction = actionobj;
                //Debug.Log(Application.dataPath + " -- " + actionobj.ResultString);
            }
            else
            {
                // action is null
                Debug.LogWarning("No shader install/update action could be determined.");
            }               
        }

        public static void DetermineRuntimeAction()
        {
            Func<DeterminedAction, InstalledPackageStatus, PackageVailidity, string, ActionRules> ActionRule = (action, status, validity, text) => new ActionRules(action, status, validity, text);

            // result cases
            string multiple = "Multiple runtime packages detected. [Force] Uninstall all then install applicable package.";
            string mismatch = "mismatch";
            string normalUpgrade = "Runtime package can be upgraded. [Offer] install newer package.";
            string normalDowngrade = "Runtime package is from a higher version of the tool. [Offer] install package version from this distribution.";
            string currentValid = "Current runtime is correctly installed.";
            string freshInstall = "No runtime files currently installed, the latest version will be imported.";
            string missingFiles = "Files are missing from the installed runtime. Uninstall remaining files and install current runtime version.";
            string incompatible = "incompatible";


            List<ActionRules> ActionRulesList = new List<ActionRules>
            {
                ActionRule(DeterminedAction.NothingInstalled_Install_force, InstalledPackageStatus.Absent, PackageVailidity.Absent, freshInstall),
                ActionRule(DeterminedAction.Error, InstalledPackageStatus.Multiple, PackageVailidity.Invalid, multiple),
                ActionRule(DeterminedAction.CurrentValid, InstalledPackageStatus.Current, PackageVailidity.Valid, currentValid),
                ActionRule(DeterminedAction.UninstallReinstall_optional, InstalledPackageStatus.Upgradeable, PackageVailidity.Valid, normalUpgrade),
                ActionRule(DeterminedAction.UninstallReinstall_optional, InstalledPackageStatus.VersionTooHigh, PackageVailidity.Valid, normalDowngrade),
                ActionRule(DeterminedAction.UninstallReinstall_force, InstalledPackageStatus.Mismatch, PackageVailidity.Valid, mismatch),
                ActionRule(DeterminedAction.UninstallReinstall_force, InstalledPackageStatus.Mismatch, PackageVailidity.Invalid, mismatch),
                ActionRule(DeterminedAction.UninstallReinstall_force, InstalledPackageStatus.MissingFiles, PackageVailidity.Invalid, missingFiles),
                ActionRule(DeterminedAction.Incompatible, InstalledPackageStatus.Mismatch, PackageVailidity.Invalid, incompatible)
            };

            ActionRules actionobj = null;
            List<ActionRules> packageStatus = null;
            
            //Debug.LogWarning("UpdateManager.installedRuntimeStatus " + UpdateManager.installedRuntimeStatus);
            //Debug.LogWarning("UpdateManager.runtimePackageValid " + UpdateManager.runtimePackageValid);

            packageStatus = ActionRulesList.FindAll(x => x.InstalledPackageStatus == UpdateManager.installedRuntimeStatus);
            if (UpdateManager.runtimePackageValid != PackageVailidity.Waiting || UpdateManager.runtimePackageValid != PackageVailidity.None)
                actionobj = packageStatus.Find(y => y.PackageVailidity == UpdateManager.runtimePackageValid);

            if (actionobj != null)
            {                
                UpdateManager.determinedRuntimeAction = actionobj;
                //Debug.Log(Application.dataPath + " -- " + actionobj.ResultString);
                //Debug.LogWarning("actionobj UpdateManager.determinedRuntimeAction " + UpdateManager.determinedRuntimeAction.DeterminedShaderAction);
            }
            else
            {
                // action is null
                Debug.LogWarning("No shader install/update action could be determined.");
            }
        }

        public class ActionRules
        {
            // DeterminedAction InstalledPackageStatus  PackageVailidity resultString
            public DeterminedAction DeterminedShaderAction;
            public InstalledPackageStatus InstalledPackageStatus;
            public PackageVailidity PackageVailidity;
            public string ResultString;

            public ActionRules()
            {
                DeterminedShaderAction = DeterminedAction.None;
                InstalledPackageStatus = InstalledPackageStatus.None;
                PackageVailidity = PackageVailidity.None;
                ResultString = "Undetermined";
            }

            public ActionRules(DeterminedAction determinedAction, InstalledPackageStatus installedPackageStatus, PackageVailidity packageVailidity, string resultString)
            {
                DeterminedShaderAction = determinedAction;
                InstalledPackageStatus = installedPackageStatus;
                PackageVailidity = packageVailidity;
                ResultString = resultString;
            }
        }

        public static ShaderPackageManifest ReadJson(string assetPath)
        {
            //Debug.Log("assetPath: " + assetPath);
            Object sourceObject = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (sourceObject != null)
            {
                TextAsset sourceAsset = sourceObject as TextAsset;
                string jsonText = sourceAsset.text;
                //Debug.Log("jsonText: " + jsonText);
                return JsonUtility.FromJson<ShaderPackageManifest>(jsonText);
            }
            else
            {
                Debug.LogWarning("JSON ERROR");
                return null;
            }
        }

        // build a list of all the shader packages available for import from the distribution package
        // the distribution should contain .unitypackage files paired with *_RL_referencemanifest.json files
        private static List<ShaderPackageManifest> BuildPackageMap()
        {
            string search = "_RL_referencemanifest";
            // string[] searchLoc = new string[] { "Assets", "Packages/com.soupday.cc3_unity_tools" }; // look in assets too if the distribution is wrongly installed
            // in Unity 2021.3.14f1 ALL the assets on the "Packages/...." path are returned for some reason...
            // omiting the 'search in folders' parameter correctly finds assests matching the search term in both Assets and Packages
            string[] mainifestGuids = AssetDatabase.FindAssets(search);

            List<ShaderPackageManifest> manifestPackageMap = new List<ShaderPackageManifest>();

            foreach (string guid in mainifestGuids)
            {
                string manifestAssetPath = AssetDatabase.GUIDToAssetPath(guid);
                string searchTerm = search + ".json";

                if (manifestAssetPath.iContains(searchTerm))
                {
                    var sourceJsonObject = (Object)AssetDatabase.LoadAssetAtPath(manifestAssetPath, typeof(Object));
                    if (sourceJsonObject != null)
                    {
                        TextAsset sourceJson = null;
                        ShaderPackageManifest packageManifest = null;

                        try
                        {
                            sourceJson = sourceJsonObject as TextAsset;
                        }
                        catch (Exception exception)
                        {
                            Debug.LogWarning(exception);
                        }

                        if (sourceJson != null)
                        {
                            packageManifest = (ShaderPackageManifest)JsonUtility.FromJson(sourceJson.text, typeof(ShaderPackageManifest));
                            string packageSearchTerm = Path.GetFileNameWithoutExtension(packageManifest.SourcePackageName);
                            string[] shaderPackages = AssetDatabase.FindAssets(packageSearchTerm);

                            string selectedPackage = shaderPackages[0]; // default case
                            if (shaderPackages.Length > 1) // error case
                            {
                                Debug.LogWarning("Multiple shader packages detected for: " + packageManifest.SourcePackageName + " ... using the one in Packages/.");

                                foreach (string shaderPackage in shaderPackages)
                                {
                                    if (AssetDatabase.GUIDToAssetPath(shaderPackage).StartsWith("Packages"))
                                    {
                                        selectedPackage = shaderPackage;
                                        break;
                                    }
                                }
                            }
                            
                            string packageAssetPath = AssetDatabase.GUIDToAssetPath(selectedPackage);
                            packageManifest.referenceMainfestPath = manifestAssetPath;
                            packageManifest.referenceShaderPackagePath = packageAssetPath;
                            manifestPackageMap.Add(packageManifest);                            
                        }
                    }
                }
            }
            //Debug.Log("Returning manifestPackageMap containing: " + manifestPackageMap.Count + " entries.");
            return manifestPackageMap;
        }

        private static void BuildPackageMaps()
        {
            UpdateManager.availablePackages = BuildPackageMap("_RL_referencemanifest");
            UpdateManager.availableLegacyShaderPackages = BuildPackageMap("_RL_referencelegacymanifest");
            UpdateManager.availableRuntimePackages = BuildPackageMap("_RL_reference_runtimemanifest");
        }

        private static List<ShaderPackageManifest> BuildPackageMap(string search)
        {
            string[] mainifestGuids = AssetDatabase.FindAssets(search);

            List<ShaderPackageManifest> manifestPackageMap = new List<ShaderPackageManifest>();

            foreach (string guid in mainifestGuids)
            {
                string manifestAssetPath = AssetDatabase.GUIDToAssetPath(guid);
                string searchTerm = search + ".json";

                if (manifestAssetPath.iContains(searchTerm))
                {
                    var sourceJsonObject = (Object)AssetDatabase.LoadAssetAtPath(manifestAssetPath, typeof(Object));
                    if (sourceJsonObject != null)
                    {
                        TextAsset sourceJson = null;
                        ShaderPackageManifest packageManifest = null;

                        try
                        {
                            sourceJson = sourceJsonObject as TextAsset;
                        }
                        catch (Exception exception)
                        {
                            Debug.LogWarning(exception);
                        }

                        if (sourceJson != null)
                        {
                            packageManifest = (ShaderPackageManifest)JsonUtility.FromJson(sourceJson.text, typeof(ShaderPackageManifest));
                            string packageSearchTerm = Path.GetFileNameWithoutExtension(packageManifest.SourcePackageName);
                            string[] shaderPackages = AssetDatabase.FindAssets(packageSearchTerm);

                            if (shaderPackages.Length > 0)
                            {
                                string selectedPackage = shaderPackages[0]; // default case
                                if (shaderPackages.Length > 1) // error case
                                {
                                    //Debug.LogWarning("Multiple shader packages detected for: " + packageManifest.SourcePackageName + " ... using the one in Packages/.");

                                    foreach (string shaderPackage in shaderPackages)
                                    {
                                        if (AssetDatabase.GUIDToAssetPath(shaderPackage).StartsWith("Packages"))
                                        {
                                            selectedPackage = shaderPackage;
                                            break;
                                        }
                                    }
                                }

                                string packageAssetPath = AssetDatabase.GUIDToAssetPath(selectedPackage);
                                packageManifest.referenceMainfestPath = manifestAssetPath;
                                packageManifest.referenceShaderPackagePath = packageAssetPath;
                                manifestPackageMap.Add(packageManifest);
                            }
                        }
                    }
                }
            }
            return manifestPackageMap;
        }


        // find all currently installed render pipelines
        public static void GetInstalledPipelineVersion()
        {
            UpdateManager.installedShaderVersion = new Version(0, 0, 0);
            UpdateManager.installedShaderPipelineVersion = PipelineVersion.None;
            UpdateManager.installedPackageStatus = InstalledPackageStatus.None;
            UpdateManager.shaderPackageValid = PackageVailidity.Waiting;
            UpdateManager.runtimePackageValid = PackageVailidity.Waiting;
            //GetInstalledPipelinesAync();
#if UNITY_2021_3_OR_NEWER
            GetInstalledPipelinesDirectly();
#else
            GetInstalledPipelinesAync();
#endif
        }

#if UNITY_2021_3_OR_NEWER
        public static void GetInstalledPipelinesDirectly()
        {
            UnityEditor.PackageManager.PackageInfo[] packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            DeterminePipelineInfo(packages.ToList());
        }
#endif

        // pre UNITY_2021_3 async package listing -- START
        public static event EventHandler OnPackageListComplete;
        private static ListRequest Request;

        public static void GetInstalledPipelinesAync()
        {
            //Debug.Log("ShaderPackageUtil.GetInstalledPipelinesAync()");
            Request = Client.List(true, true);  // offline mode and includes depenencies (otherwise wont detect URP)
            OnPackageListComplete -= PackageListComplete;
            OnPackageListComplete += PackageListComplete;
            EditorApplication.update -= WaitForRequestCompleted;
            EditorApplication.update += WaitForRequestCompleted;
        }

        private static void WaitForRequestCompleted()
        {
            if (Request.IsCompleted)// && isWaiting)
            {
                //Debug.Log("ShaderPackageUtil.WaitForRequestCompleted");
                if (OnPackageListComplete != null)
                    OnPackageListComplete.Invoke(null, null);
                EditorApplication.update -= WaitForRequestCompleted;
            }
        }

        public static void PackageListComplete(object sender, EventArgs args)
        {
            //Debug.Log("ShaderPackageUtil.PackageListComplete()");
            List<UnityEditor.PackageManager.PackageInfo> packageList = Request.Result.ToList();
            if (packageList != null)
            {
                DeterminePipelineInfo(Request.Result.ToList());
                OnPackageListComplete -= PackageListComplete;
            }
            else
            {
                Debug.LogWarning("ShaderPackageUtil.PackageListComplete() Cannot retrieve installed packages.");
            }
        }
        // pre UNITY_2021_3 async package listing -- END

        // common pipeline determination
        public static void DeterminePipelineInfo(List<UnityEditor.PackageManager.PackageInfo> packageList)
        {
            List<InstalledPipelines> installed = new List<InstalledPipelines>();

            installed.Add(new InstalledPipelines(InstalledPipeline.Builtin, new Version(emptyVersion), ""));

            // find urp
            UnityEditor.PackageManager.PackageInfo urp = packageList.Find(p => p.name.Equals(urpPackage));
            if (urp != null)
            {
                installed.Add(new InstalledPipelines(InstalledPipeline.URP, new Version(urp.version), urpPackage));
            }

            // find hdrp
            UnityEditor.PackageManager.PackageInfo hdrp = packageList.ToList().Find(p => p.name.Equals(hdrpPackage));
            if (hdrp != null)
            {
                installed.Add(new InstalledPipelines(InstalledPipeline.HDRP, new Version(hdrp.version), hdrpPackage));
            }

            UpdateManager.installedPipelines = installed;

            (PipelineVersion, PipelineVersion) activePipelineVersion = DetermineActivePipelineVersion(packageList);            
            UpdateManager.activePipelineVersion = activePipelineVersion.Item1;
            UpdateManager.activeLegacyPipelineVersion = activePipelineVersion.Item2;


            if (ShaderPackageUpdater.Instance != null)
                ShaderPackageUpdater.Instance.Repaint();

            if (OnPipelineDetermined != null)
                OnPipelineDetermined.Invoke(null, null);
        }

        public static (PipelineVersion, PipelineVersion) DetermineActivePipelineVersion(List<UnityEditor.PackageManager.PackageInfo> packageList)
        {
            //TestVersionResponse(); // **** important to run after rule editing ****

            UnityEngine.Rendering.RenderPipeline r = RenderPipelineManager.currentPipeline;
            if (r != null)
            {
                if (r.GetType().ToString().Equals(urpType))
                {
                    string version = packageList.ToList().Find(p => p.name.Equals(urpPackage)).version;
                    UpdateManager.activePipeline = InstalledPipeline.URP;
                    UpdateManager.activeVersion = new Version(version);
                    UpdateManager.activePackageString = urpPackage;
                }
                else if (r.GetType().ToString().Equals(hdrpType))
                {
                    string version = packageList.ToList().Find(p => p.name.Equals(hdrpPackage)).version;
                    UpdateManager.activePipeline = InstalledPipeline.HDRP;
                    UpdateManager.activeVersion = new Version(version);
                    UpdateManager.activePackageString = hdrpPackage;
                }
            }
            else
            {
                // failover based on defines (which wont cope with simultaneous installs of pipelines for varying quality levels) for edge cases when RenderPipelineManager.currentPipeline becomes null
                Debug.LogWarning("DetermineActivePipelineVersion failing over to Pipeline.GetRenderPipeline");
                RenderPipeline pipeline = Pipeline.GetRenderPipeline();
                if (pipeline == RenderPipeline.HDRP)
                {
                    string version = packageList.ToList().Find(p => p.name.Equals(hdrpPackage)).version;
                    UpdateManager.activePipeline = InstalledPipeline.HDRP;
                    UpdateManager.activeVersion = new Version(version);
                    UpdateManager.activePackageString = hdrpPackage;
                }
                else if (pipeline == RenderPipeline.URP)
                {
                    string version = packageList.ToList().Find(p => p.name.Equals(urpPackage)).version;
                    UpdateManager.activePipeline = InstalledPipeline.URP;
                    UpdateManager.activeVersion = new Version(version);
                    UpdateManager.activePackageString = urpPackage;
                }
                else if (pipeline == RenderPipeline.Builtin)
                {
                    // now it really is builtin
                    UpdateManager.activePipeline = InstalledPipeline.Builtin;
                    UpdateManager.activeVersion = new Version(UpdateManager.emptyVersion);
                }
            }

            UpdateManager.platformRestriction = PlatformRestriction.None;

            switch (UpdateManager.activePipeline)
            {
                case InstalledPipeline.Builtin:
                    {
                        return (PipelineVersion.BuiltIn, PipelineVersion.BuiltIn);
                    }
                case InstalledPipeline.HDRP:
                    {
                        return (GetVersion(InstalledPipeline.HDRP, UpdateManager.activeVersion), GetLegacyVersion(InstalledPipeline.HDRP, UpdateManager.activeVersion));
                    }
                case InstalledPipeline.URP:
                    {
                        return (GetVersion(InstalledPipeline.URP, UpdateManager.activeVersion), GetLegacyVersion(InstalledPipeline.URP, UpdateManager.activeVersion));
                    }
                case InstalledPipeline.None:
                    {
                        return (PipelineVersion.None, PipelineVersion.None);
                    }
            }
            return (PipelineVersion.None, PipelineVersion.None);
        }

        public class VersionLimits
        {
            public Version Min;
            public Version Max;
            
            public PipelineVersion Version;

            public VersionLimits(Version min, Version max, PipelineVersion version)
            {
                Min = min;
                Max = max;
                Version = version;
            }
        }

        public static PipelineVersion GetVersion(InstalledPipeline pipe, Version version)
        {
            Func<Version, Version, PipelineVersion, VersionLimits> Rule = (min, max, ver) => new VersionLimits(min, max, ver);

            if (pipe == InstalledPipeline.URP)
            {
                // Specific rule to limit WebGL to a maximum of URP12
                if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL && version.Major >= 12)
                {
                    UpdateManager.platformRestriction = PlatformRestriction.URPWebGL;
                    return PipelineVersion.URP12;
                }

                List<VersionLimits> urpRules = new List<VersionLimits>
                {
                    // Rule(min max, version)                    
                    Rule(new Version(0, 0, 0), new Version(10, 0, 0), PipelineVersion.Incompatible),
                    Rule(new Version(10, 0, 0), new Version(12, 0, 0), PipelineVersion.URP10),
                    Rule(new Version(12, 0, 0), new Version(14, 0, 0), PipelineVersion.URP12),
                    Rule(new Version(14, 0, 0), new Version(17, 0, 0), PipelineVersion.URP14),
                    Rule(new Version(17, 0, 0), new Version(100, 99, 99), PipelineVersion.URP17)
                };

                VersionLimits result = urpRules.Find(z => version >= z.Min && version < z.Max);

                if (result != null)
                {
                    return result.Version;
                }
                else
                {
                    return PipelineVersion.URP10;
                }
            }

            if (pipe == InstalledPipeline.HDRP)
            {
                List<VersionLimits> hdrpRules = new List<VersionLimits>
                {
                    Rule(new Version(0, 0, 0), new Version(10, 0, 0), PipelineVersion.Incompatible),
                    Rule(new Version(10, 0, 0), new Version(12, 0, 0), PipelineVersion.HDRP10),
                    Rule(new Version(12, 0, 0), new Version(14, 0, 0), PipelineVersion.HDRP12),
                    Rule(new Version(14, 0, 0), new Version(17, 0, 0), PipelineVersion.HDRP14),
                    Rule(new Version(17, 0, 0), new Version(100, 9, 99), PipelineVersion.HDRP17)
                };

                VersionLimits result = hdrpRules.Find(z => version >= z.Min && version < z.Max);

                if (result != null)
                {
                    return result.Version;
                }
                else
                {
                    return PipelineVersion.HDRP10;
                }
            }
            return PipelineVersion.None;
        }

        // legacy shaders have different package compatablilty to the main shaders
        public static PipelineVersion GetLegacyVersion(InstalledPipeline pipe, Version version)
        {
            Func<Version, Version, PipelineVersion, VersionLimits> Rule = (min, max, ver) => new VersionLimits(min, max, ver);

            if (pipe == InstalledPipeline.URP)
            {
                // Specific rule to limit WebGL to a maximum of URP12
                if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL && version.Major >= 12)
                {
                    UpdateManager.platformRestriction = PlatformRestriction.URPWebGL;
                    return PipelineVersion.URP12;
                }                

                List<VersionLimits> urpRules = new List<VersionLimits>
                {
                    // Rule(min max, version)                    
                    Rule(new Version(0, 0, 0), new Version(10, 0, 0), PipelineVersion.Incompatible),
                    Rule(new Version(10, 0, 0), new Version(12, 0, 0), PipelineVersion.URP10),
                    Rule(new Version(12, 0, 0), new Version(14, 0, 0), PipelineVersion.URP12),
                    Rule(new Version(14, 0, 0), new Version(17, 0, 0), PipelineVersion.URP14),
                    Rule(new Version(17, 0, 0), new Version(17, 1, 0), PipelineVersion.URP17),
                    Rule(new Version(17, 1, 0), new Version(17, 2, 0), PipelineVersion.URP17),
                    Rule(new Version(17, 2, 0), new Version(100, 99, 99), PipelineVersion.Incompatible)
                };

                VersionLimits result = urpRules.Find(z => version >= z.Min && version < z.Max);

                if (result != null)
                {
                    return result.Version;
                }
                else
                {
                    return PipelineVersion.URP10;
                }
            }

            if (pipe == InstalledPipeline.HDRP)
            {
                List<VersionLimits> hdrpRules = new List<VersionLimits>
                {
                    Rule(new Version(0, 0, 0), new Version(10, 0, 0), PipelineVersion.Incompatible),
                    Rule(new Version(10, 0, 0), new Version(12, 0, 0), PipelineVersion.HDRP10),
                    Rule(new Version(12, 0, 0), new Version(14, 0, 0), PipelineVersion.HDRP12),
                    Rule(new Version(14, 0, 0), new Version(17, 0, 0), PipelineVersion.HDRP14),
                    Rule(new Version(17, 0, 0), new Version(100, 9, 99), PipelineVersion.HDRP17)
                };

                VersionLimits result = hdrpRules.Find(z => version >= z.Min && version < z.Max);

                if (result != null)
                {
                    return result.Version;
                }
                else
                {
                    return PipelineVersion.HDRP10;
                }
            }
            return PipelineVersion.None;
        }

        public static void TestVersionResponse()
        {            
            for (int i = 10; i < 18; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    Debug.Log("Major URP Package Version: " + i + " -- " + GetVersion(InstalledPipeline.URP, new Version(i, j)));
                }
            }

            for (int i = 10; i < 18; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    Debug.Log("Major HDRP Package Version: " + i + " -- " + GetVersion(InstalledPipeline.HDRP, new Version(i, j)));
                }
            }
        }

        public static void GUIPerformShaderAction(DeterminedAction action)
        {
            bool uninstall = false;
            bool install = false;

            switch (action)
            {
                case ShaderPackageUtil.DeterminedAction.None:
                    {
                        uninstall = false;
                        install = false;
                        break;
                    }
                case ShaderPackageUtil.DeterminedAction.CurrentValid:
                    {
                        uninstall = false;
                        install = false;
                        break;
                    }
                case ShaderPackageUtil.DeterminedAction.Error:
                    {
                        uninstall = true;
                        install = true;
                        break;
                    }
                case ShaderPackageUtil.DeterminedAction.UninstallReinstall_optional:
                    {
                        uninstall = true;
                        install = true;
                        break;
                    }
                case ShaderPackageUtil.DeterminedAction.UninstallReinstall_force:
                    {
                        uninstall = true;
                        install = true;
                        break;
                    }
                case ShaderPackageUtil.DeterminedAction.NothingInstalled_Install_force:
                    {
                        uninstall = false;
                        install = true;
                        break;
                    }
            }

            if (uninstall)
            {
                UnInstallShaderPackage(install);
            }

            if (install)
            {
                if (UpdateManager.currentPackageManifest != null)
                {
                    InstallShaderPackage(UpdateManager.currentPackageManifest, false);
                    UpdaterWindowCheckStatus();
                }
                else
                {
                    Debug.Log("No package for the current pipeline is available.");
                }
            }
        }

        public static void GUIPerformRuntimeAction(DeterminedAction action)
        {
            bool uninstall = false;
            bool install = false;

            switch (action)
            {
                case ShaderPackageUtil.DeterminedAction.None:
                    {
                        uninstall = false;
                        install = false;
                        break;
                    }
                case ShaderPackageUtil.DeterminedAction.CurrentValid:
                    {
                        uninstall = false;
                        install = false;
                        break;
                    }
                case ShaderPackageUtil.DeterminedAction.Error:
                    {
                        uninstall = true;
                        install = true;
                        break;
                    }
                case ShaderPackageUtil.DeterminedAction.UninstallReinstall_optional:
                    {
                        uninstall = true;
                        install = true;
                        break;
                    }
                case ShaderPackageUtil.DeterminedAction.UninstallReinstall_force:
                    {
                        uninstall = true;
                        install = true;
                        break;
                    }
                case ShaderPackageUtil.DeterminedAction.NothingInstalled_Install_force:
                    {
                        uninstall = false;
                        install = true;
                        break;
                    }
            }

            if (uninstall)
            {
                UnInstallRuntimePackage(install);
            }

            if (install)
            {
                Debug.Log(UpdateManager.currentRuntimePackageManifest.referenceShaderPackagePath);
                InstallRuntimePackage(UpdateManager.currentRuntimePackageManifest, false);
                UpdaterWindowCheckStatus();
            }
        }

        public static void GUIPerformLegacyShaderInstall()
        {
            //Debug.Log(UpdateManager.currentLegacyPackageManifest.referenceShaderPackagePath);
            InstallLegacyShaderPackage(UpdateManager.currentLegacyPackageManifest, false);
        }

        public static void ProcessPendingActions()
        {
            if (ImporterWindow.GeneralSettings != null)
            {
                if (ImporterWindow.GeneralSettings.pendingShaderUninstall)
                {
                    //Debug.Log("Beginning uninstall of existing shader package");
                    UnInstallShaderPackage(true);
                }

                if (ImporterWindow.GeneralSettings.pendingShaderInstall)
                {
                    //Debug.Log("Beginning installation of current shader package");
                    InstallShaderPackage(UpdateManager.currentPackageManifest, false);
                }

                if (ImporterWindow.GeneralSettings.pendingRuntimeUninstall)
                {
                    //Debug.Log("Beginning uninstall of existing runtime package");
                    UnInstallRuntimePackage(true);
                }

                if (ImporterWindow.GeneralSettings.pendingRuntimeInstall)
                {
                    //Debug.Log("Beginning installation of current runtime package");
                    InstallRuntimePackage(UpdateManager.currentRuntimePackageManifest, false);
                }

                ImporterWindow.GeneralSettings.criticalUpdateRequired = false;
                Debug.Log("Critical package update complete");
                //ImporterWindow.GeneralSettings.postInstallShowPopupNotWindow = true;
            }
        }

        public static void InstallShaderPackage(ShaderPackageManifest shaderPackageManifest, bool interactive = true)
        {
            // The events importPackageCompleted and onImportPackageItemsCompleted are only invoked by
            // a package installation that doesn't cause a domain reload - otherwise the subscription doesnt survive.
            // Since the inclusion of runtime .cs files in the package, a domain reload is caused requiring a manual solution.

            //AssetDatabase.importPackageCompleted -= OnImportPackageCompleted;
            //AssetDatabase.importPackageCompleted += OnImportPackageCompleted;
#if UNITY_2021_3_OR_NEWER
            //AssetDatabase.onImportPackageItemsCompleted -= OnImportPackageItemsCompleted;
            //AssetDatabase.onImportPackageItemsCompleted += OnImportPackageItemsCompleted;
#else

#endif      
            //UpdateManager.SetShaderVariantLimit();

            if (ImporterWindow.GeneralSettings != null)
            {
                ImporterWindow.GeneralSettings.performPostInstallationCheck = true;
                ImporterWindow.GeneralSettings.postInstallShowUpdateWindow = true;
            }

            if (UpdateManager.currentPackageManifest != null)
            {
                if (ImporterWindow.GeneralSettings != null)
                {
                    //Debug.Log("Attempting installation of current shader package");
                    ImporterWindow.GeneralSettings.shaderToolVersion = Pipeline.VERSION;
                    ImporterWindow.GeneralSettings.pendingShaderInstall = false;
                }

                AssetDatabase.ImportPackage(shaderPackageManifest.referenceShaderPackagePath, interactive);
            }
            else
            {
                Debug.Log("No package for the current pipeline is available.");
            }
        }

        public static void InstallRuntimePackage(ShaderPackageManifest runtimePackageManifest, bool interactive = true)
        {
            if (ImporterWindow.GeneralSettings != null)
            {
                ImporterWindow.GeneralSettings.performPostInstallationRuntimeCheck = true;
            }

            if (UpdateManager.currentRuntimePackageManifest != null)
            {
                if (ImporterWindow.GeneralSettings != null)
                {
                    //Debug.Log("Attempting installation of current runtime package");
                    ImporterWindow.GeneralSettings.runtimeToolVersion = Pipeline.VERSION;
                    ImporterWindow.GeneralSettings.pendingRuntimeInstall = false;
                }

                AssetDatabase.ImportPackage(runtimePackageManifest.referenceShaderPackagePath, interactive);
            }
            else
            {
                Debug.Log("No runtime package is available.");
            }
        }

        public static void InstallLegacyShaderPackage(ShaderPackageManifest shaderPackageManifest, bool interactive = true)
        {
            if (UpdateManager.currentLegacyPackageManifest != null)
            {
                Debug.Log($"Installing 'Legacy Shader Package' for {shaderPackageManifest.Pipeline} version {shaderPackageManifest.Version} from package: '{shaderPackageManifest.SourcePackageName}'");
                AssetDatabase.ImportPackage(shaderPackageManifest.referenceShaderPackagePath, interactive);
            }
        }


        private static void OnImportPackageCompleted(string packagename)
        {            
            Debug.Log($"Imported package: {packagename}");
#if UNITY_2021_3_OR_NEWER
            // this will be handled by the callback: AssetDatabase.onImportPackageItemsCompleted
#else
            PackageType packageType = PackageType.None;
            //"_RL_shadermanifest";  Shader Package_2.0.0_BuiltIn_RL_shaderpackage
            //"_RL_runtimemanifest";  Runtime Package_2.1.0_RL_runtimepackage
            if (packagename.iContains("_RL_shaderpackage")) packageType = PackageType.Shader;
            else if (packagename.iContains("_RL_runtimepackage")) packageType = PackageType.Runtime;
            PostImportPackageItemCompare(packageType);
#endif
            AssetDatabase.importPackageCompleted -= OnImportPackageCompleted;
        }

        private static void OnImportPackageItemsCompleted(string[] items)
        {
            string mostRecentManifestPath = AssetDatabase.GUIDToAssetPath(GetLatestManifestGUID()).UnityAssetPathToFullPath(); // will flag multiple installation errors
            string manifestLabel = "_RL_shadermanifest.json";
            string manifest = string.Empty;
            string fullManifestPath = string.Empty;
            foreach (string item in items)
            {
                if (item.EndsWith(manifestLabel))
                {
                    manifest = item;
                    fullManifestPath = manifest.UnityAssetPathToFullPath();
                    Debug.Log("Post Install: using shader manifest: " + fullManifestPath);
                    Debug.Log("Post Install: most recently accessed manifest: " + mostRecentManifestPath);
                    break;
                }
            }

            if (!string.IsNullOrEmpty(manifest))
            {
                ShaderPackageManifest shaderPackageManifest = ReadJson(manifest);
                foreach (string item in items)
                {
                    string itemGUID = string.Empty;
#if UNITY_2021_3_OR_NEWER
                    itemGUID = AssetDatabase.AssetPathToGUID(item, AssetPathToGUIDOptions.OnlyExistingAssets);
#else               
                    if (File.Exists(item.UnityAssetPathToFullPath()))
                    {
                        itemGUID = AssetDatabase.AssetPathToGUID(item);
                    }
                    else
                    {
                        Debug.LogError("OnImportPackageItemsCompleted: " + item + " cannot be found on disk.");
                    }
#endif
                    string fileName = Path.GetFileName(item);
                    ShaderPackageItem it = shaderPackageManifest.Items.Find(x => x.ItemName.EndsWith(fileName));

                    if (it != null)
                    {
                        it.InstalledGUID = itemGUID;
                        if (it.InstalledGUID != it.GUID)
                        {
                            Debug.Log("Post Install: GUID reassigned for: " + it.ItemName + " (Info only)");
                        }
                        else
                        {
                            //Debug.Log(it.ItemName + "  GUID MATCH ********************");
                        }
                    }
                }
                Debug.Log("Post Install: Updating manifest: " + fullManifestPath);
                string jsonString = JsonUtility.ToJson(shaderPackageManifest);
                File.WriteAllText(fullManifestPath, jsonString);
                AssetDatabase.Refresh();
            }
            if (ShaderPackageUpdater.Instance != null) ShaderPackageUpdater.Instance.UpdateGUI();
#if UNITY_2021_3_OR_NEWER
            AssetDatabase.onImportPackageItemsCompleted -= OnImportPackageItemsCompleted;
#endif
        }

        public static void RecompileShaders()
        {
            try
            {
                string mostRecentManifestPath = AssetDatabase.GUIDToAssetPath(GetLatestManifestGUID());
                ShaderPackageManifest shaderPackageManifest = ReadJson(mostRecentManifestPath);
                List<ShaderPackageItem> shaderGraphFiles = shaderPackageManifest.Items.FindAll(x => x.ItemName.EndsWith("shadergraph"));

                foreach (ShaderPackageItem item in shaderGraphFiles)
                {
                    string path = string.Empty;

                    if (!string.IsNullOrEmpty(item.InstalledGUID))
                        path = AssetDatabase.GUIDToAssetPath(item.InstalledGUID);
                    else if (!string.IsNullOrEmpty(item.GUID))
                        path = AssetDatabase.GUIDToAssetPath(item.GUID);

                    if (!string.IsNullOrEmpty(path))
                        if (File.Exists(path.UnityAssetPathToFullPath()))
                            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
            catch (Exception ex)
            {
                Debug.Log(ex.ToString());
            }
        }

        public enum PackageType
        {
            None,
            Shader,
            Runtime
        }

        private static string GetLatestManifestGUID(PackageType packageType = PackageType.Shader)
        {
            string guid = string.Empty;
            string search = string.Empty;

            switch (packageType)
            {
                case PackageType.Shader:
                    {
                        search = "_RL_shadermanifest";
                        break;
                    }
                case PackageType.Runtime:
                    {
                        search = "_RL_runtimemanifest";
                        break;
                    }
                case PackageType.None:
                    {
                        break;
                    }
            }

            string[] manifestGUIDS = AssetDatabase.FindAssets(search, new string[] { "Assets" });

            if (manifestGUIDS.Length > 1)
            {
                // Problem that should never happen ... 
                int c = (manifestGUIDS.Length - 1);
                Debug.LogError("Shader problem: " + c + " shader package" + (c > 1 ? "s " : " ") + (c > 1 ? "are " : "is ") + "already installed");
                Dictionary<string, DateTime> timeStamps = new Dictionary<string, DateTime>();
                foreach (string g in manifestGUIDS)
                {
                    string path = AssetDatabase.GUIDToAssetPath(g);
                    string fullpath = path.UnityAssetPathToFullPath();
                    DateTime accessTime = File.GetLastAccessTime(fullpath);
                    timeStamps.Add(g, accessTime);
                }
                if (timeStamps.Count > 0)
                {
                    guid = timeStamps.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
                }
                else
                {
                    return string.Empty;
                }
            }
            else if (manifestGUIDS.Length > 0)
            {
                guid = manifestGUIDS[0];
            }
            return guid;
        }

        public static void PostImportPackageItemCompare(PackageType packageType)
        {
            Debug.Log("Performing post installation checks... " + packageType);
            string guid = GetLatestManifestGUID(packageType);
            if (guid == string.Empty) return;

            string manifestAssetPath = AssetDatabase.GUIDToAssetPath(guid);
            string fullManifestPath = manifestAssetPath.UnityAssetPathToFullPath();
            ShaderPackageManifest manifest = ReadJson(manifestAssetPath);
            if (manifest != null)
            {
                //Debug.Log("Package installed from: " + manifest.SourcePackageName);
                foreach (var item in manifest.Items)
                {
                    string fullFilename = Path.GetFileName(item.ItemName);
                    string filename = Path.GetFileNameWithoutExtension(item.ItemName);

                    string[] foundGuids = AssetDatabase.FindAssets(filename, new string[] { "Assets" });
                    if (foundGuids.Length > 0)
                    {
                        foreach (string g in foundGuids)
                        {
                            string foundAssetPath = AssetDatabase.GUIDToAssetPath(g);
                            if (Path.GetFileName(foundAssetPath).Equals(fullFilename))
                            {
                                item.InstalledGUID = g;
                                if (item.InstalledGUID == item.GUID) item.Validated = true;
                            }
                        }
                    }
                    else if (foundGuids.Length == 0)
                    {
                        Debug.LogError("PostImportPackageItemCompare: Cannot find " + filename + " in the AssetDatabase.");
                    }
                }
            }
            string jsonString = JsonUtility.ToJson(manifest);
            File.WriteAllText(fullManifestPath, jsonString);
            AssetDatabase.Refresh();

            if (ShaderPackageUpdater.Instance != null) ShaderPackageUpdater.Instance.UpdateGUI();

            string message = string.Empty;
            if (ImporterWindow.GeneralSettings != null)
            {
                message = ImporterWindow.GeneralSettings.updateMessage;
            }
            //string message = UpdateManager.updateMessage;

            switch (packageType)
            {
                case PackageType.Shader:
                    {
                        message += "Shader package: " + manifest.Pipeline.ToString() + " " + manifest.Version.ToString() + " has been imported." + System.Environment.NewLine;
                        break;
                    }
                case PackageType.Runtime:
                    {
                        message += "Runtime package: " + manifest.Version.ToString() + " has been imported." + System.Environment.NewLine;
                        break;
                    }
            }

            //UpdateManager.updateMessage = message;  // clear UpdateManager.updateMessage after closing the message.
            if (ImporterWindow.GeneralSettings != null)
            {
                ImporterWindow.GeneralSettings.updateMessage = message;
            }
        }

        public static void PostImportShaderPackageItemCompare()
        {
            PostImportPackageItemCompare(PackageType.Shader);
        }

        public static void PostImportRuntimePackageItemCompare()
        {
            PostImportPackageItemCompare(PackageType.Runtime);
        }


        public static void UnInstallShaderPackage(bool flagReinstall = false)
        {
            if (ImporterWindow.GeneralSettings != null)
                ImporterWindow.GeneralSettings.pendingShaderUninstall = false;

            if (flagReinstall)
            {
                if (ImporterWindow.GeneralSettings != null)
                {
                    //Debug.Log("Attempting uninstall of existing shader package");
                    ImporterWindow.GeneralSettings.pendingShaderInstall = true;
                }
            }

            string manifestLabel = "_RL_shadermanifest";
            string[] searchLoc = new string[] { "Assets" };
            string[] mainifestGuids = AssetDatabase.FindAssets(manifestLabel, searchLoc);

            if (mainifestGuids.Length == 0)
            {
                //Debug.LogWarning("No shader packages have been found!!");
            }

            if (mainifestGuids.Length > 1)
            {
                Debug.LogWarning("Multiple installed shader packages have been found!! - uninstalling ALL");
            }

            foreach (string guid in mainifestGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                string[] split = assetPath.Split('_');
                Debug.Log("Trying to uninstall shader package:  Pipeline:" + split[split.Length -3] + " Version: " + split[split.Length - 4]);
                
                if (TryUnInstallPackage(guid))
                {
                    Debug.Log("Package uninstalled.");
                }
                else
                {
                    Debug.Log("Package could not be fully uninstalled.");
                }
            }
        }


        public static void UnInstallRuntimePackage(bool flagReinstall = false)
        {
            if (ImporterWindow.GeneralSettings != null)
                ImporterWindow.GeneralSettings.pendingRuntimeUninstall = false;

            if (flagReinstall)
            {
                if (ImporterWindow.GeneralSettings != null)
                {
                    //Debug.Log("Attempting uninstall of existing runtime package");
                    ImporterWindow.GeneralSettings.pendingRuntimeInstall = true;
                }
            }

            string manifestLabel = "_RL_runtimemanifest";
            string[] searchLoc = new string[] { "Assets" };
            string[] mainifestGuids = AssetDatabase.FindAssets(manifestLabel, searchLoc);

            if (mainifestGuids.Length == 0)
            {
                //Debug.LogWarning("No shader packages have been found!!");
            }

            if (mainifestGuids.Length > 1)
            {
                Debug.LogWarning("Multiple installed shader packages have been found!! - uninstalling ALL");
            }

            foreach (string guid in mainifestGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                string[] split = assetPath.Split('_');
                Debug.Log("Trying to uninstall shader package:  Pipeline:" + split[split.Length - 3] + " Version: " + split[split.Length - 4]);

                if (TryUnInstallPackage(guid))
                {
                    Debug.Log("Package uninstalled.");
                }
                else
                {
                    Debug.Log("Package could not be fully uninstalled.");
                }
            }
        }

        private static bool TryUnInstallPackage(string guid, bool toTrash = true)
        {
            Object manifestObject = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid));
            Selection.activeObject = null;
            TextAsset manifestText = manifestObject as TextAsset;
            string manifest = string.Empty;

            List<string> folderList = new List<string>();
            List<string> deleteList = new List<string>();

            if (manifestText != null)
            {
                manifest = manifestText.text;
            }
            else { return false; }

            if (!string.IsNullOrEmpty(manifest))
            {
                ShaderPackageManifest shaderPackageManifest = JsonUtility.FromJson<ShaderPackageManifest>(manifest);
#if UNITY_2021_3_OR_NEWER
                Debug.Log("Uninstalling files" + (toTrash ? " to OS trash folder" : "") + " from: " + " Pipeline: " + shaderPackageManifest.Pipeline + " Version: " + shaderPackageManifest.Version + " (" + shaderPackageManifest.FileName + ")");
#else
                Debug.Log("Uninstalling files" + " from: " + " Pipeline: " + shaderPackageManifest.Pipeline + " Version: " + shaderPackageManifest.Version + " (" + shaderPackageManifest.FileName + ")");
#endif
                foreach (ShaderPackageItem thing in shaderPackageManifest.Items)
                {
                    string deleteGUID = string.Empty;

                    if (thing.InstalledGUID == null || thing.Validated == false)
                    {
                        // installedguid is null then the post install process wasnt conducted
                        // only happens with a manual install 
                        // find item from original GUID in the manifest
                        string testObjectName = Path.GetFileName(AssetDatabase.GUIDToAssetPath(thing.GUID));
                        string originalObjectName = Path.GetFileName(thing.ItemName);
                        if (testObjectName.iEquals(originalObjectName))
                        {
                            deleteGUID = thing.GUID;
                        }
                        else // original GUID points to a file with incorrect name
                        {
                            // find by filename
                            string searchName = Path.GetFileNameWithoutExtension(thing.ItemName);
                            string[] folders = { "Assets" };
                            string[] foundGUIDs = AssetDatabase.FindAssets(searchName, folders);
                            foreach (string g in foundGUIDs)
                            {
                                // ensure filename + extension matches
                                string assetPath = AssetDatabase.GUIDToAssetPath(g);
                                if (File.Exists(assetPath.UnityAssetPathToFullPath()))
                                {
                                    if (Path.GetFileName(assetPath).iEquals(originalObjectName))
                                    {
                                        deleteGUID = g;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        deleteGUID = thing.InstalledGUID;
                    }

                    string deletePath = AssetDatabase.GUIDToAssetPath(deleteGUID);
                    // validate assetpath to submit for deletion
                    if (!AssetDatabase.IsValidFolder(deletePath))
                    {
                        if (deletePath.StartsWith("Assets"))
                        {
                            //check file is on disk
                            string fullPath = deletePath.UnityAssetPathToFullPath();
                            if (File.Exists(fullPath))
                            {
                                deleteList.Add(deletePath);
                                //Debug.Log("Adding " + deletePath + " to deleteList");
                            }
                            else
                            {
                                Debug.Log("Shader file delete: " + deletePath + " not found on disk ... skipping");
                            }

                        }
                    }
                    else
                    {
                        folderList.Add(deletePath);
                    }
                }
                if (folderList.Count > 0)
                {
                    var folderListSortedByDepth = folderList.OrderByDescending(p => p.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar));

                    foreach (string folder in folderListSortedByDepth)
                    {
                        Debug.Log(folder);
                    }
                }
                else
                {
                    //Debug.Log("No Folders");
                }

                manifest = string.Empty;
            }
            else { return false; }

            // delete all paths in deleteList 
            List<string> failedPaths = new List<string>();
            bool hasFailedPaths = false;
            deleteList.Add(AssetDatabase.GUIDToAssetPath(guid));
            //bool existingFilesDeleted = false;
#if UNITY_2021_3_OR_NEWER
            if (toTrash)
                hasFailedPaths = !AssetDatabase.MoveAssetsToTrash(deleteList.ToArray(), failedPaths);
            else
                hasFailedPaths = !AssetDatabase.DeleteAssets(deleteList.ToArray(), failedPaths);
#else
            // according to the documentation DeleteAssets/MoveAssetsToTrash unsupported in 2020.3
            // & absent from 2019.4 -- use individual DeleteAsset/MoveAssetToTrash            
            foreach (string path in deleteList)
            {
                bool deleted;
                if (toTrash)
                    deleted = AssetDatabase.MoveAssetToTrash(path);
                else
                    deleted = AssetDatabase.DeleteAsset(path);

                if (!deleted)
                {
                    Debug.LogError(path + " did not uninstall.");
                    failedPaths.Add(path);
                    hasFailedPaths = true;
                }
            }
#endif

            if (hasFailedPaths)
            {
                if (failedPaths.Count > 0)
                {
                    Debug.Log(failedPaths.Count + " paths failed to delete (usually due to missing files).");
                    //foreach (string path in failedPaths)
                    //{
                    //    Debug.LogError(path + " ...failed to delete.");
                    //}
                }
            }

            AssetDatabase.Refresh();

            UpdateManager.installedShaderVersion = new Version(0, 0, 0);
            UpdateManager.installedShaderPipelineVersion = PipelineVersion.None;
            UpdateManager.installedPackageStatus = InstalledPackageStatus.None;

            if (ShaderPackageUpdater.Instance != null)
                ShaderPackageUpdater.Instance.UpdateGUI();

            return !hasFailedPaths;
        }

        #region ENUM+CLASSES
        // STANDALONE COPY FOR JSON CONSISTENCY IN ShaderDistroPackager -- START
        public enum PipelineVersion
        {
            None = 0,
            BuiltIn = 1,
            URP10 = 110,
            URP12 = 112,
            URP13 = 113,
            URP14 = 114,
            URP15 = 115,
            URP16 = 116,
            URP17 = 117,
            URP171 = 1171,
            URP172 = 1172,
            URP18 = 118,
            URP19 = 119,
            URP20 = 120,
            URP21 = 121,
            URP22 = 122,
            URP23 = 123,
            URP24 = 124,
            URP25 = 125,
            URP26 = 126,
            HDRP10 = 210,
            HDRP12 = 212,
            HDRP13 = 213,
            HDRP14 = 214,
            HDRP15 = 215,
            HDRP16 = 216,
            HDRP17 = 217,
            HDRP171 = 2171,
            HDRP172 = 2172,
            HDRP18 = 218,
            HDRP19 = 219,
            HDRP20 = 220,
            HDRP21 = 221,
            HDRP22 = 222,
            HDRP23 = 223,
            HDRP24 = 224,
            HDRP25 = 225,
            HDRP26 = 226,
            Incompatible = 999
        }

        [Serializable]
        public class ShaderPackageManifest
        {
            public string FileName;
            public PipelineVersion Pipeline;
            public string Version;
            public string SourcePackageName;
            public bool VersionEdit;
            public string ManifestPath;
            public bool Validated;
            public bool Visible;
            public string referenceMainfestPath;
            public string referenceShaderPackagePath;
            public List<ShaderPackageItem> Items;

            public ShaderPackageManifest(string name, PipelineVersion pipeline, string version)
            {
                FileName = name;
                Pipeline = pipeline;
                Version = version;
                SourcePackageName = string.Empty;
                VersionEdit = false;
                ManifestPath = string.Empty;
                Validated = false;
                Visible = false;
                Items = new List<ShaderPackageItem>();
            }
        }

        [Serializable]
        public class ShaderPackageItem
        {
            public string ItemName;
            public string GUID;
            public string InstalledGUID;
            public bool Validated;

            public ShaderPackageItem(string itemName, string gUID)
            {
                ItemName = itemName;
                GUID = gUID;
                InstalledGUID = string.Empty;
                Validated = false;
            }
        }
        // STANDALONE COPY FOR JSON CONSISTENCY IN ShaderDistroPackager -- END

        // simple class for GUI
        public class InstalledPipelines
        {
            public InstalledPipeline InstalledPipeline;
            public Version Version;
            public string PackageString;

            public InstalledPipelines(InstalledPipeline installedPipeline, Version version, string packageString)
            {
                InstalledPipeline = installedPipeline;
                Version = version;
                PackageString = packageString;
            }
        }

        public enum InstalledPipeline
        {
            None,
            Builtin,
            URP,
            HDRP
        }
        public enum InstalledPackageStatus
        {
            None,
            Current,
            Upgradeable,
            VersionTooHigh,  // deal with package from a higher distro release
            MissingFiles,
            Mismatch,
            Multiple,  // Treat the presence of multiple shader packages as a serious problem
            NoPackageAvailable,
            Absent
        }

        public enum PlatformRestriction
        {
            None,
            URPWebGL
        }

        public enum PackageVailidity
        {
            None,
            Valid,
            Invalid,
            Waiting,
            Finished,
            Absent
        }

        public enum DeterminedAction
        {
            None,
            Error,
            CurrentValid,
            NothingInstalled_Install_force,
            UninstallReinstall_optional,
            UninstallReinstall_force,
            Incompatible
        }

        #endregion ENUM+CLASSES
    }
    #region STRING EXTENSION
    public static class StringToVersionExtension
    {
        public static Version ToVersion(this string str)
        {
            return new Version(str);
        }
    }

    public static class StringAssetPathToFullPath
    {
        public static string UnityAssetPathToFullPath(this string str)
        {
            string datapath = Application.dataPath;
            string result = datapath.Remove(datapath.Length - 6, 6) + str;
            return result.Replace("\\", "/").Replace('/', Path.DirectorySeparatorChar);
        }
    }

    public static class StringFullPathToAssetPath
    {
        public static string FullPathToUnityAssetPath(this string str)
        {
            string input = str.Replace("\\", "/");
            string datapath = Application.dataPath.Replace("\\", "/");
            
            if (input.iContains(datapath))
            {
                return input.Remove(0, Application.dataPath.Length - 6).Replace("\\", "/");
            }                
            else
            {
                return string.Empty;
            }
        }
    }
    #endregion STRING EXTENSION
}



