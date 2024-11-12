using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Reallusion.Import
{
    public class UpdateManager
    {
        public static bool checkIsLocked = false;

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

        public static void TryPerformUpdateChecks()
        {
            if (!checkIsLocked)
                PerformUpdateChecks();
        }

        public static void PerformUpdateChecks()
        {
            Debug.LogWarning("STARTING UPDATE CHECKS");
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
            Debug.LogWarning("ALL UPDATE CHECKS COMPLETED");

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
            ShaderPackageUtil.OnPipelineDetermined -= PackageCheckDone;
            ShaderPackageUtil.OnPipelineDetermined += PackageCheckDone;

            ShaderPackageUtil.GetInstalledPipelineVersion();
            FrameTimer.CreateTimer(10, FrameTimer.initShaderUpdater, ShaderPackageUtil.ImporterWindowInitCallback);
            ShaderPackageUtil.UpdateManagerUpdateCheck();
            SetDeterminationStatusFlag(ActivityStatus.DeterminingPackages, true);
        }

        public static void PackageCheckDone(object sender, object e)
        {
            SetDeterminationStatusFlag(ActivityStatus.DonePackages, true);
            ShaderPackageUtil.OnPipelineDetermined -= PackageCheckDone;
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

        public static void ShowUpdateUtilityWindow()
        {
            if (UpdateManager.determinedShaderAction != null)
            {
                //bool error = UpdateManager.shaderPackageValid == PackageVailidity.Invalid;
                bool sos = false;
                bool swUpdateAvailable = UpdateManager.determinedSoftwareAction == RLToolUpdateUtil.DeterminedSoftwareAction.Software_update_available;
                if (swUpdateAvailable) Debug.LogWarning("A software update is available.");
                if (ImporterWindow.GeneralSettings != null) sos = ImporterWindow.GeneralSettings.showOnStartup;
                bool valid = UpdateManager.determinedShaderAction.DeterminedAction == ShaderPackageUtil.DeterminedShaderAction.CurrentValid;
                bool force = UpdateManager.determinedShaderAction.DeterminedAction == ShaderPackageUtil.DeterminedShaderAction.UninstallReinstall_force || UpdateManager.determinedShaderAction.DeterminedAction == ShaderPackageUtil.DeterminedShaderAction.Error;
                bool optional = UpdateManager.determinedShaderAction.DeterminedAction == ShaderPackageUtil.DeterminedShaderAction.UninstallReinstall_optional;
                bool shaderActionRequired = force || valid || optional;
                bool showWindow = false;
                if (optional) Debug.LogWarning("An optional shader package is available.");
                else if (valid) Debug.LogWarning("Problem with shader installation.");

                if (valid || optional)
                    showWindow = sos;

                if (sos || force || swUpdateAvailable)
                    showWindow = true;

                if (showWindow)
                {
                    Debug.LogWarning("Can show ShaderPackageUpdater.");
                    if (!Application.isPlaying) ShaderPackageUpdater.CreateWindow();

                    if (ShaderPackageUpdater.Instance != null)
                    {
                        ShaderPackageUpdater.Instance.actionRequired = shaderActionRequired;
                        ShaderPackageUpdater.Instance.softwareActionRequired = swUpdateAvailable;
                    }
                }
            }
        }
    }
}
