using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Reallusion.Import
{
    public class UpdateManager
    {
        public static bool checkIsLocked = false;
        public static bool calledFromMenu = false;
        public static RLSettingsObject settings;

        //shader package validation
        public static string emptyVersion = "0.0.0";
        public static Version activeVersion = new Version(0, 0, 0);
        public static ShaderPackageUtil.InstalledPipeline activePipeline = ShaderPackageUtil.InstalledPipeline.None;
        public static ShaderPackageUtil.PipelineVersion activePipelineVersion = ShaderPackageUtil.PipelineVersion.None;
        public static ShaderPackageUtil.PipelineVersion installedShaderPipelineVersion = ShaderPackageUtil.PipelineVersion.None;
        public static ShaderPackageUtil.PlatformRestriction platformRestriction = ShaderPackageUtil.PlatformRestriction.None;
        public static Version installedShaderVersion = new Version(0, 0, 0);
        public static ShaderPackageUtil.InstalledPackageStatus installedPackageStatus = ShaderPackageUtil.InstalledPackageStatus.None;
        public static List<ShaderPackageUtil.ShaderPackageManifest> availablePackages;
        public static ShaderPackageUtil.ShaderPackageManifest currentPackageManifest;
        public static string activePackageString = string.Empty;
        public static List<ShaderPackageUtil.InstalledPipelines> installedPipelines;
        public static ShaderPackageUtil.PackageVailidity shaderPackageValid = ShaderPackageUtil.PackageVailidity.None;
        public static List<ShaderPackageUtil.ShaderPackageItem> missingShaderPackageItems;
        public static ShaderPackageUtil.ShaderActionRules determinedShaderAction = null;

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
                calledFromMenu = fromMenu;
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


        public static void ShowUpdateUtilityWindow()
        {
            PlayerSettings.productGUID.ToString();

            if(ImporterWindow.GeneralSettings != null)
                    settings = ImporterWindow.GeneralSettings;
            else
                Debug.LogError("settings are null");

            // reset the shown once flag in the settings and reset when the application quits                            
            if (settings != null) settings.updateWindowShownOnce = true;

            EditorApplication.quitting -= HandleQuitEvent;
            EditorApplication.quitting += HandleQuitEvent;

            if (UpdateManager.determinedShaderAction != null)
            {
                if (UpdateManager.determinedShaderAction.DeterminedAction == ShaderPackageUtil.DeterminedShaderAction.NothingInstalled_Install_force)
                {
                    if (!IsInitialInstallCompleted())
                    {
                        if (settings != null) settings.postInstallShowPopupNotWindow = true;
                        ShaderPackageUtil.InstallPackage(UpdateManager.currentPackageManifest, false);
                        SetInitialInstallCompleted();
                        return;
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
                
                bool valid = UpdateManager.determinedShaderAction.DeterminedAction == ShaderPackageUtil.DeterminedShaderAction.CurrentValid;

                bool force = UpdateManager.determinedShaderAction.DeterminedAction == ShaderPackageUtil.DeterminedShaderAction.UninstallReinstall_force || UpdateManager.determinedShaderAction.DeterminedAction == ShaderPackageUtil.DeterminedShaderAction.Error || UpdateManager.determinedShaderAction.DeterminedAction == ShaderPackageUtil.DeterminedShaderAction.NothingInstalled_Install_force;

                bool incompatible = (determinedShaderAction.DeterminedAction == ShaderPackageUtil.DeterminedShaderAction.Incompatible);

                bool optional = UpdateManager.determinedShaderAction.DeterminedAction == ShaderPackageUtil.DeterminedShaderAction.UninstallReinstall_optional;

                bool pipelineActionRequired = incompatible;

                bool shaderActionRequired = force || (optional && sos) || incompatible;                
                
                if (optional) Debug.LogWarning("An optional shader package is available.");
                else if (!valid) Debug.LogWarning("Problem with shader installation.");

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
                        ShaderPackagePopup.OpenPopupWindow(ShaderPackagePopup.PopupType.Completion, UpdateManager.updateMessage);
                        return;
                    }
                }

                if (showWindow || calledFromMenu)
                {
                    if (!Application.isPlaying)
                    {
                        bool ignore = false;
                        if (settings != null)
                        {
                            if (!calledFromMenu)
                                ignore = settings.ignoreAllErrors;
                        }
                        if (!ignore) ShaderPackageUpdater.CreateWindow();
                    }

                    if (ShaderPackageUpdater.Instance != null)
                    {
                        ShaderPackageUpdater.Instance.pipeLineActionRequired = pipelineActionRequired;
                        ShaderPackageUpdater.Instance.shaderActionRequired = shaderActionRequired;
                        ShaderPackageUpdater.Instance.softwareActionRequired = swUpdateAvailable;
                    }
                }
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
