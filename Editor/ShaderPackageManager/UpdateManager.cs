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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reallusion.Import
{
    public class UpdateManager
    {
        public static bool checkIsLocked = false;
        public static bool showOverride = false;
        public static RLSettingsObject settings;

        //shader package validation
        public static string emptyVersion = "0.0.0";
        public static Version activeVersion = new Version(0, 0, 0);
        public static ShaderPackageUtil.InstalledPipeline activePipeline = ShaderPackageUtil.InstalledPipeline.None;
        public static ShaderPackageUtil.PipelineVersion activePipelineVersion = ShaderPackageUtil.PipelineVersion.None;
        public static ShaderPackageUtil.PipelineVersion activeLegacyPipelineVersion = ShaderPackageUtil.PipelineVersion.None;
        public static ShaderPackageUtil.PipelineVersion installedShaderPipelineVersion = ShaderPackageUtil.PipelineVersion.None;
        public static ShaderPackageUtil.PlatformRestriction platformRestriction = ShaderPackageUtil.PlatformRestriction.None;
        public static Version installedShaderVersion = new Version(0, 0, 0);
        public static ShaderPackageUtil.InstalledPackageStatus installedPackageStatus = ShaderPackageUtil.InstalledPackageStatus.None;
        public static List<ShaderPackageUtil.ShaderPackageManifest> availablePackages;
        public static List<ShaderPackageUtil.ShaderPackageManifest> availableLegacyShaderPackages;
        public static ShaderPackageUtil.ShaderPackageManifest currentPackageManifest;
        public static ShaderPackageUtil.ShaderPackageManifest currentLegacyPackageManifest;
        public static string activePackageString = string.Empty;
        public static List<ShaderPackageUtil.InstalledPipelines> installedPipelines;
        public static ShaderPackageUtil.PackageVailidity shaderPackageValid = ShaderPackageUtil.PackageVailidity.None;
        public static List<ShaderPackageUtil.ShaderPackageItem> missingShaderPackageItems;
        public static ShaderPackageUtil.ActionRules determinedShaderAction = null;

        //runtime package validation
        public static ShaderPackageUtil.ShaderPackageManifest currentRuntimePackageManifest;
        public static Version installedRuntimeVersion = new Version(0, 0, 0);
        public static ShaderPackageUtil.InstalledPackageStatus installedRuntimeStatus;
        public static List<ShaderPackageUtil.ShaderPackageManifest> availableRuntimePackages;
        public static List<ShaderPackageUtil.ShaderPackageItem> missingRuntimePackageItems;
        public static ShaderPackageUtil.PackageVailidity runtimePackageValid = ShaderPackageUtil.PackageVailidity.None;
        public static ShaderPackageUtil.ActionRules determinedRuntimeAction = null;

        //software package update checker
        public static bool updateChecked = false;
        public static RLToolUpdateUtil.DeterminedSoftwareAction determinedSoftwareAction = RLToolUpdateUtil.DeterminedSoftwareAction.None;

        public static event EventHandler UpdateChecksComplete;

        private static ActivityStatus determinationStatus = ActivityStatus.None;

        public static ActivityStatus DeterminationStatus { get { return determinationStatus; } }

        public static void TryPerformUpdateChecks(bool fromMenu = false)
        {
            if (!checkIsLocked)
            {
                showOverride = fromMenu;
                PerformUpdateChecks();
            }
        }

        public static void PerformUpdateChecks()
        {
            //Debug.LogWarning("STARTING UPDATE CHECKS");
            if (Application.isPlaying)
            {
                if (EditorWindow.HasOpenInstances<ShaderPackageUpdater>())
                {
                    EditorWindow.GetWindow<ShaderPackageUpdater>().Close();
                }
            }
            else
            {                
                checkIsLocked = true;
                UpdateChecksComplete -= UpdateChecksDone;
                UpdateChecksComplete += UpdateChecksDone;
                determinationStatus = 0;
                StartUpdateMonitor();
                CheckHttp();
                CheckPackages();
            }
        }

        public static void UpdateChecksDone(object sender, object e)
        {
            //Debug.LogWarning("ALL UPDATE CHECKS COMPLETED");
            ShaderPackageUtil.DetermineShaderAction();
            ShaderPackageUtil.DetermineRuntimeAction();
            checkIsLocked = false;
            ShowUpdateUtilityWindow();

            UpdateChecksComplete -= UpdateChecksDone;
        }

        public static void CheckHttp()
        {            
            RLToolUpdateUtil.HttpVersionChecked -= HttpCheckDone;
            RLToolUpdateUtil.HttpVersionChecked += HttpCheckDone;
            SetDeterminationStatusFlag(ActivityStatus.DeterminingHttp, true);
            RLToolUpdateUtil.UpdateManagerUpdateCheck();
        }

        public static void HttpCheckDone(object sender, object e)
        {
            RLToolUpdateUtil.HttpVersionChecked -= HttpCheckDone;
            SetDeterminationStatusFlag(ActivityStatus.DoneHttp, true);
        }

        public static void CheckPackages()
        {
            ShaderPackageUtil.PackageCheckDone -= PackageCheckDone;
            ShaderPackageUtil.PackageCheckDone += PackageCheckDone;
                        
            SetDeterminationStatusFlag(ActivityStatus.DeterminingPackages, true);
            ShaderPackageUtil.UpdateManagerUpdateCheck();
            
        }

        public static void PackageCheckDone(object sender, object e)
        {
            SetDeterminationStatusFlag(ActivityStatus.DonePackages, true);
            ShaderPackageUtil.PackageCheckDone -= PackageCheckDone;
        }

        public static void StartUpdateMonitor()
        {
            EditorApplication.update -= MonitorUpdateCheck;
            EditorApplication.update += MonitorUpdateCheck;
        }

        private static void MonitorUpdateCheck()
        {
            bool gotPackages = DeterminationStatus.HasFlag(ActivityStatus.DonePackages);
            bool gotHttp = DeterminationStatus.HasFlag(ActivityStatus.DoneHttp);

            if (gotPackages && gotHttp)
            {
                if (UpdateChecksComplete != null)
                    UpdateChecksComplete.Invoke(null, null);
                EditorApplication.update -= MonitorUpdateCheck;
            }
        }

        [Flags]
        public enum ActivityStatus
        {
            None = 0,
            DeterminingPackages = 1,
            DonePackages = 2,
            DeterminingHttp = 4,
            DoneHttp = 8
        }

        public static void SetDeterminationStatusFlag(ActivityStatus flag, bool value)
        {
            if (value)
            {
                if (!determinationStatus.HasFlag(flag))
                {
                    determinationStatus |= flag; // toggle changed to ON => bitwise OR to add flag                    
                }
            }
            else
            {
                if (determinationStatus.HasFlag(flag))
                {
                    determinationStatus ^= flag; // toggle changed to OFF => bitwise XOR to remove flag
                }
            }
        }

        public static void SetInitialInstallCompleted()
        {
            string shaderKey = "RL_Inital_Shader_Installation";
            char delimiter = '|';
            string projectRef = PlayerSettings.productGUID.ToString();

            if (EditorPrefs.HasKey(shaderKey))
            {
                if ((!EditorPrefs.GetString(shaderKey).Contains(projectRef)))
                {
                    string tmp = EditorPrefs.GetString(shaderKey);
                    string[] projects = tmp.Split(delimiter);
                    int count = projects.Length;
                    if (count > 20)
                    {
                        tmp = string.Empty;
                        for (int i = 1; i < count; i++)
                        {
                            if (i > 1)
                                tmp += delimiter;

                            tmp += projects[i];
                        }
                    }                    
                    tmp += delimiter + projectRef;

                    EditorPrefs.SetString(shaderKey, tmp);
                }
            }
            else
            {
                EditorPrefs.SetString(shaderKey, projectRef);
            }
        }

        public static bool IsInitialInstallCompleted()
        {
            string shaderKey = "RL_Inital_Shader_Installation";
            string projectRef = PlayerSettings.productGUID.ToString();

            //Debug.Log("KEY: " + shaderKey);
            //Debug.Log("PREFS STRING: " + EditorPrefs.GetString(shaderKey));
            //Debug.Log("PROJECT REF: " + projectRef);

            if (EditorPrefs.HasKey(shaderKey))
            {                
                if ((EditorPrefs.GetString(shaderKey).Contains(projectRef)))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsPackageUpgradeRequired(PackageType packageType)
        {
            string lastUsedToolVersion = string.Empty;

            if (string.IsNullOrEmpty(settings.shaderToolVersion))
            {
                settings.shaderToolVersion = UpdateManager.installedShaderVersion.ToString();
            }

            if (string.IsNullOrEmpty(settings.runtimeToolVersion))
            {
                settings.runtimeToolVersion = UpdateManager.installedRuntimeVersion.ToString();
            }

            switch (packageType)
            {
                case PackageType.Shader:
                    {
                        lastUsedToolVersion = settings.shaderToolVersion;
                        break;
                    }
                case PackageType.Runtime:
                    {
                        lastUsedToolVersion = settings.runtimeToolVersion;
                        break;
                    }
            }

            if(!Version.TryParse(lastUsedToolVersion, out Version last))
            {
                last = new Version(0, 0, 0);
            }

            if(!Version.TryParse(Pipeline.VERSION, out Version current))
            {
                current = new Version(0, 0, 0);
            }

            if (last < new Version(2, 1, 0))  // essential breakpoint to move .cs files to runtime package
            {
                Debug.Log("Critical package updates for version 2.1.0 and above are required (this will be performed autoatically)");
                return true;
            }
            else
                return false;
        }

        const string variantLimit = "UnityEditor.ShaderGraph.VariantLimit";
        const string ShaderGraphProjectSettings = "ProjectSettings/ShaderGraphSettings.asset";
        const int shaderVariantLimit = 2048;

        public static bool IsShaderVariantLimitTooLow() // true if its too low
        {
            string fullassetPath = ShaderGraphProjectSettings.UnityAssetPathToFullPath();

            bool hasLimit = false;
            int inAssetVariantLimit = 0;
#if UNITY_6000_2_OR_NEWER
            bool hasOverride = false;
            int inAssetOverrideLimit = 0;
#endif
            if (File.Exists(fullassetPath))
            {
                using (StreamReader sr = new StreamReader(fullassetPath))
                {
                    string line;

                    while ((line = sr.ReadLine()) != null)
                    {
                        string trimmedLine = line.Trim();
                        if (trimmedLine.StartsWith("shaderVariantLimit:"))
                        {
                            hasLimit = true;
                            string[] strings = trimmedLine.Split(':');
                            if (!int.TryParse(strings[1].Trim(), out inAssetVariantLimit))
                            {
                                Debug.Log("Cannot parse ShaderGraphProjectSettings");
                            }
                        }
#if UNITY_6000_2_OR_NEWER
                        if (trimmedLine.StartsWith("overrideShaderVariantLimit:"))
                        {
                            hasOverride = true;
                            string[] strings = trimmedLine.Split(':');
                            if (!int.TryParse(strings[1].Trim(), out inAssetOverrideLimit))
                            {
                                Debug.Log("Cannot parse ShaderGraphProjectSettings");
                            }
                        }
#endif
                    }
                }
#if UNITY_6000_2_OR_NEWER
                if (hasLimit && hasOverride)
                { 
                    if (inAssetOverrideLimit > 0 && inAssetVariantLimit < shaderVariantLimit) return true;
                }
#else
                if (hasLimit)
                {
                    if (inAssetVariantLimit < shaderVariantLimit) return true;
                }
#endif
            }

            if (EditorPrefs.HasKey(variantLimit))
            {
                if (EditorPrefs.GetInt(variantLimit, 128) < shaderVariantLimit) return true;
            }

            return false;
        }

        //[MenuItem("Reallusion/Misc Tools/Set Shader Variant Limit", priority = 180)]
        private static void DoSetShaderVariantLimit()
        {
            SetShaderVariantLimit();
        }

        public static void SetShaderVariantLimit()
        {
            if (EditorPrefs.HasKey(variantLimit))
            {
                if (EditorPrefs.GetInt(variantLimit, 128) < shaderVariantLimit)
                {
                    EditorPrefs.SetInt(variantLimit, shaderVariantLimit);
                }
            }

            try
            {
                UnityEngine.Object[] settingsData = InternalEditorUtility.LoadSerializedFileAndForget(ShaderGraphProjectSettings);

                if (settingsData.Length > 0)
                {
                    SerializedObject shaderGraphSettings = new SerializedObject(settingsData[0]);
                    SerializedProperty m_shaderVariantLimit = shaderGraphSettings.FindProperty("shaderVariantLimit");

                    if (m_shaderVariantLimit.intValue < shaderVariantLimit)
                        m_shaderVariantLimit.intValue = shaderVariantLimit;

                    shaderGraphSettings.ApplyModifiedProperties();
                    shaderGraphSettings.Update();

                    var array = new Object[] { settingsData[0] };
                    InternalEditorUtility.SaveToSerializedFileAndForget(settingsData, ShaderGraphProjectSettings, true);

                    SettingsService.NotifySettingsProviderChanged();
                    SettingsService.RepaintAllSettingsWindow();
                }
                else
                {
                    Debug.Log(ShaderGraphProjectSettings + " not found.");
                }
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
        }

        public enum PackageType
        {
            Shader,
            Runtime
        }

        public static void ShowUpdateUtilityWindow()
        {
            //PlayerSettings.productGUID.ToString();

            if(ImporterWindow.GeneralSettings != null)
                    settings = ImporterWindow.GeneralSettings;
            else
                Debug.LogError("settings are null");

            // reset the shown once flag in the settings and reset when the application quits                            
            if (settings != null) settings.updateWindowShownOnce = true;

            EditorApplication.quitting -= HandleQuitEvent;
            EditorApplication.quitting += HandleQuitEvent;

            //bool critical = false;
            if (settings != null)
            {
                if (!settings.criticalUpdateRequired)
                {
                    if (IsPackageUpgradeRequired(PackageType.Shader) || IsPackageUpgradeRequired(PackageType.Runtime))
                    {
                        settings.updateMessage = string.Empty;
                        settings.criticalUpdateRequired = true;
                        settings.pendingShaderUninstall = true;
                        settings.pendingRuntimeUninstall = true;
                    }
                }
            }

            if (settings != null)
            {
                if (settings.criticalUpdateRequired)
                {
                    ShaderPackageUtil.ProcessPendingActions();
                }
            }

            //critical = settings.criticalUpdateRequired;

            if (UpdateManager.determinedShaderAction != null && UpdateManager.determinedRuntimeAction != null)
            {
                if (UpdateManager.determinedShaderAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.NothingInstalled_Install_force)
                {
                    if (!IsInitialInstallCompleted())
                    {
                        if (settings != null)
                        {
                            ImporterWindow.GeneralSettings.updateMessage = string.Empty;
                            settings.postInstallShowPopupNotWindow = true;
                        }
                        ShaderPackageUtil.InstallShaderPackage(UpdateManager.currentPackageManifest, false);
                        ShaderPackageUtil.InstallRuntimePackage(UpdateManager.currentRuntimePackageManifest, false);
                        SetInitialInstallCompleted();
                        return;
                    }                    
                    else
                    {
                        if (settings != null)
                        {
                            if (settings.pendingShaderInstall)
                            {
                                ShaderPackageUtil.InstallShaderPackage(UpdateManager.currentPackageManifest, false);
                                settings.pendingShaderInstall = false;
                            }

                            if (settings.pendingRuntimeInstall)
                            {
                                ShaderPackageUtil.InstallRuntimePackage(UpdateManager.currentRuntimePackageManifest, false);
                                settings.pendingRuntimeInstall = false;
                            }
                        }
                    }
                }

                bool sos = false;                
                bool shownOnce = true;
                bool postInstallShowUpdateWindow = false;
                bool showWindow = false;

                if (settings != null)
                {
                    sos = settings.showOnStartup;
                    shownOnce = settings.updateWindowShownOnce;
                    postInstallShowUpdateWindow = settings.postInstallShowUpdateWindow;
                    settings.postInstallShowUpdateWindow = false;
                }
                bool swUpdateAvailable = UpdateManager.determinedSoftwareAction == RLToolUpdateUtil.DeterminedSoftwareAction.Software_update_available;
                if (swUpdateAvailable) Debug.LogWarning("A software update is available.");
                
                bool valid = UpdateManager.determinedShaderAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.CurrentValid;

                bool force = UpdateManager.determinedShaderAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.UninstallReinstall_force || UpdateManager.determinedShaderAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.Error || UpdateManager.determinedShaderAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.NothingInstalled_Install_force || UpdateManager.determinedRuntimeAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.UninstallReinstall_force || UpdateManager.determinedRuntimeAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.Error || UpdateManager.determinedRuntimeAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.NothingInstalled_Install_force;

                bool incompatible = (determinedShaderAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.Incompatible);

                bool optional = UpdateManager.determinedShaderAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.UninstallReinstall_optional || UpdateManager.determinedRuntimeAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.UninstallReinstall_optional;

                bool shaderGraphActionRequired = IsShaderVariantLimitTooLow();

                bool pipelineActionRequired = incompatible;

                bool shaderActionRequired = force || (optional && sos) || incompatible; // || shaderGraphActionRequired; // suppressing the shadergraph error reporting for the moment - leaving the UI available          

                //if (critical) Debug.LogWarning("Critical package updates are required.");
                //else if (optional) Debug.LogWarning("An optional shader package is available.");
                //else if (!valid) Debug.LogWarning("Problem with shader installation.");

                if (valid || optional)
                    showWindow = sos && !shownOnce;

                if ((sos && !shownOnce) || force || swUpdateAvailable || postInstallShowUpdateWindow)
                    showWindow = true;

                bool popupNotUpdater = false;
                if (settings != null) popupNotUpdater = settings.postInstallShowPopupNotWindow;
                settings.postInstallShowPopupNotWindow = false;

                if (popupNotUpdater)
                {
                    if (!Application.isPlaying)
                    {
                        ShaderPackagePopup.OpenPopupWindow(ShaderPackagePopup.PopupType.Completion, settings.updateMessage);//UpdateManager.updateMessage);
                        return;
                    }
                }

                if (showWindow || showOverride)
                {
                    if (!Application.isPlaying)
                    {
                        bool ignore = false;
                        if (settings != null)
                        {
                            if (!showOverride)
                                ignore = settings.ignoreAllErrors;
                        }
                        if (!ignore) ShaderPackageUpdater.CreateWindow();
                    }

                    if (ShaderPackageUpdater.Instance != null)
                    {
                        ShaderPackageUpdater.Instance.pipeLineActionRequired = pipelineActionRequired;
                        ShaderPackageUpdater.Instance.shaderActionRequired = shaderActionRequired;
                        ShaderPackageUpdater.Instance.softwareActionRequired = swUpdateAvailable;
                        // suppressing the shadergraph error reporting for the moment - leaving the UI available
                        //ShaderPackageUpdater.Instance.shaderGraphActionRequired = shaderGraphActionRequired;
                    }
                }
            }
            else if (showOverride)
            {
                ShaderPackageUpdater.CreateWindow();
            }
        }

        public static string updateMessage = string.Empty;

        [Flags]
        enum ShowWindow
        {
            None = 0,
            DoNotShow = 1,
            ShowUpdaterWindow = 2,
            ShowPopupWindow = 4
        }

        public static void HandleQuitEvent()
        {
            settings.updateWindowShownOnce = false;
            RLSettings.SaveRLSettingsObject(settings);
        }
    }
}
