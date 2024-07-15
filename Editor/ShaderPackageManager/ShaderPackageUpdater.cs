using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Reallusion.Import
{
    public class ShaderPackageUpdater : EditorWindow
    {
        #region SETUP
        public static ShaderPackageUpdater Instance;
        private static RLSettingsObject currentSettings;

        [MenuItem("Reallusion/Processing Tools/Shader Package Updater", priority = 800)]
        public static void CreateWindow()
        {
            if (!EditorWindow.HasOpenInstances<ShaderPackageUpdater>())
                Instance = OpenWindow();
        }

        [MenuItem("Reallusion/Processing Tools/Shader Package Updater", true)]
        public static bool ValidateWindow()
        {
            return !EditorWindow.HasOpenInstances<ShaderPackageUpdater>();
        }

        public static ShaderPackageUpdater OpenWindow()
        {
            ShaderPackageUpdater window = ScriptableObject.CreateInstance<ShaderPackageUpdater>();
            window.ShowUtility();
            window.minSize = new Vector2(600f, 300f);

            return window;
        }

        private void OnEnable()
        {
            Debug.Log("OnEnable");
            currentSettings = ImporterWindow.GeneralSettings;

            allInstPipeFoldout = false;
            buildPlatformFoldout = false;
            instShaderFoldout = false;

            Debug.Log("OnEnable");

            // RenderPipelineManager.currentPipeline is unavailable for a few frames after assembly reload (and entering play mode)
            // see: https://issuetracker.unity3d.com/issues/hdrp-renderpipelinemanager-dot-currentpipeline-is-null-for-the-first-few-frames-of-playmode
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        private void OnDestroy()
        {
            Debug.Log("OnDestroy");
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
        }

        private void OnDisable()
        {
            Debug.Log("OnDisable");
        }

        public void OnBeforeAssemblyReload()
        {
            Debug.Log("OnBeforeAssemblyReload");
        }

        public void OnAfterAssemblyReload()
        {
            Debug.Log("OnAfterAssemblyReload");
            EditorApplication.update -= WaitForFrames;
            EditorApplication.update += WaitForFrames;
        }

        private int waitCount = 10; // frames to wait after assembly reload before accessing 'currentPipline'

        private void WaitForFrames()
        {
            while (waitCount > 0)
            {
                waitCount--;
                return;
            }

            waitCount = 10;

            Debug.Log("WaitForFrames");

            // code to execute after waiting for waitCount frames
            UpdateGUI();

            if (EditorWindow.HasOpenInstances<ShaderPackageUpdater>())
                Instance = EditorWindow.GetWindow<ShaderPackageUpdater>();

            // clean up
            EditorApplication.update -= WaitForFrames;
        }
        #endregion SETUP

        #region GUI
        private const string titleString = "Shader Package Updater";
        public Styles guiStyles;
        private float VERT_INDENT = 2f;
        private float HORIZ_INDENT = 5f;
        private float SECTION_SPACER = 2f;
        private float PACKAGE_UPDATE_W = 180f;
        private bool showAllPackages = false;
        bool shaderPackageValid = false;
        List<ShaderPackageItem> shaderPackageItems;

        public class Styles
        {
            public GUIStyle SectionHeader;
            public GUIStyle SubSectionTitle;
            public GUIStyle InactiveLabel;
            public GUIStyle ActiveLabel;
            public GUIStyle VersionLabel;
            public GUIStyle shCurrentLabel;
            public GUIStyle shUpgradeableLabel;
            public GUIStyle shTooHighLabel;
            public GUIStyle shMismatchLabel;
            public GUIStyle FoldoutTitleLabel;
            public GUIStyle FoldoutTitleErrorLabel;
            public GUIStyle WrappedInfoLabel;

            public Styles()
            {
                Color activeColor = Color.cyan * 0.95f;
                Color shCurrent = Color.green * 0.75f;
                Color shUpgradeable = Color.blue * 0.75f;
                Color shTooHigh = Color.yellow * 0.75f;
                Color shMismatch = Color.red * 0.85f;

                SectionHeader = new GUIStyle(GUI.skin.label);
                SectionHeader.fontSize = 14;
                SectionHeader.fontStyle = FontStyle.BoldAndItalic;

                SubSectionTitle = new GUIStyle(GUI.skin.label);
                SubSectionTitle.fontSize = 12;
                SubSectionTitle.fontStyle = FontStyle.Italic;
                SubSectionTitle.normal.textColor = Color.gray;

                InactiveLabel = new GUIStyle(GUI.skin.label);

                ActiveLabel = new GUIStyle(GUI.skin.label);
                ActiveLabel.normal.textColor = activeColor;
                ActiveLabel.hover.textColor = activeColor;

                VersionLabel = new GUIStyle(GUI.skin.textField);
                VersionLabel.normal.textColor = Color.gray;

                shCurrentLabel = new GUIStyle(GUI.skin.label);
                shCurrentLabel.normal.textColor = shCurrent;
                shCurrentLabel.hover.textColor = shCurrent;

                shMismatchLabel = new GUIStyle(GUI.skin.label);
                shMismatchLabel.normal.textColor = shMismatch;
                shMismatchLabel.hover.textColor = shMismatch;

                shTooHighLabel = new GUIStyle(GUI.skin.label);
                shTooHighLabel.normal.textColor = shTooHigh;
                shTooHighLabel.hover.textColor = shTooHigh;

                FoldoutTitleLabel = new GUIStyle(EditorStyles.foldout);
                FoldoutTitleLabel.fontSize = 14;
                FoldoutTitleLabel.fontStyle = FontStyle.BoldAndItalic;

                FoldoutTitleErrorLabel = new GUIStyle(EditorStyles.foldout);
                FoldoutTitleErrorLabel.onNormal.textColor = shTooHigh;
                FoldoutTitleErrorLabel.fontSize = 14;
                FoldoutTitleErrorLabel.fontStyle = FontStyle.BoldAndItalic;

                WrappedInfoLabel = new GUIStyle(GUI.skin.label);
                WrappedInfoLabel.wordWrap = true;
            }
        }

        private void OnGUI()
        {
            if (guiStyles == null)
                guiStyles = new Styles();

            if (activePipelineVersion == PipelineVersion.None || distroCatalog == null || installedPiplines == null)
                UpdateGUI();

            GUILayout.BeginVertical(); // whole window contents

            GUILayout.Space(SECTION_SPACER);

            AllInstalledPipelinesFoldoutGUI();

            GUILayout.Space(SECTION_SPACER);

            CurrentBuildPlatformGUI();

            GUILayout.Space(SECTION_SPACER);

            InstalledShaderFoldoutGUI();

            GUILayout.Space(SECTION_SPACER);

            if (installedShaderStatus != InstalledPackageStatus.Current)
                actionRequired = true;
            else
                actionRequired = false;

            actionToFollowFoldoutGUI();

            // test functions
            GUILayout.FlexibleSpace();

            ShowOnStartupGUI();
            //FoldoutTestSection();

            GUILayout.Space(SECTION_SPACER);
            // end test functions

            GUILayout.EndVertical(); // whole window contents
        }

        bool xSectionFoldout = false;
        private void FoldoutSectionTemplate()
        {
            GUILayout.BeginVertical(GUI.skin.box); // all installed pipelines

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            xSectionFoldout = EditorGUILayout.Foldout(xSectionFoldout, new GUIContent("Label", "Tooltip"), true, guiStyles.FoldoutTitleLabel);

            GUILayout.FlexibleSpace();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            if (xSectionFoldout)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Space(HORIZ_INDENT);

                GUILayout.Label("Foldout Contents");

                GUILayout.Space(HORIZ_INDENT);

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical();
        }

        bool allInstPipeFoldout = false;
        private void AllInstalledPipelinesFoldoutGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // all installed pipelines

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            string foldoutLabel = "Current Render Pipeline: " + activePipeline.ToString() + (activeVersion.Equals(new Version(emptyVersion)) ? "" : " version: " + activeVersion.ToString());
            allInstPipeFoldout = EditorGUILayout.Foldout(allInstPipeFoldout, new GUIContent(foldoutLabel, "Toggle foldout to see details of the available pipelines."), true, guiStyles.FoldoutTitleLabel);

            GUILayout.FlexibleSpace();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            if (allInstPipeFoldout)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Space(HORIZ_INDENT);

                AllInstalledPipelinesGUI();

                GUILayout.Space(HORIZ_INDENT);

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical();
        }

        bool buildPlatformFoldout = false;
        private void CurrentBuildPlatformGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // current target build platform

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            buildPlatformFoldout = EditorGUILayout.Foldout(buildPlatformFoldout, new GUIContent("Current Build Platform: " + EditorUserBuildSettings.activeBuildTarget.ToString(), ""), true, guiStyles.FoldoutTitleLabel);

            GUILayout.FlexibleSpace();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            if (buildPlatformFoldout)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Space(HORIZ_INDENT);

                CurrentBuildDetailsGUI();

                GUILayout.Space(HORIZ_INDENT);

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical(); // current target build platform
        }

        string platformMessage = string.Empty;
        string urpPlatformMessage = "There are some incompatabilities between WebGL and the shaders for URP versions higher than 12.  Defaulting to the UPR12 shader set.";

        private void CurrentBuildDetailsGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // current target build platform details

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.Label("Current Build Platform: ", guiStyles.InactiveLabel);
            GUILayout.Label(new GUIContent(EditorUserBuildSettings.activeBuildTarget.ToString(), ""), guiStyles.ActiveLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Check Settings"))
            {
                EditorWindow.GetWindow(System.Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor"));
            }

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(platformMessage))
            {
                GUILayout.Space(VERT_INDENT);

                GUILayout.BeginHorizontal();

                GUILayout.Space(HORIZ_INDENT);

                GUILayout.Label(platformMessage, guiStyles.WrappedInfoLabel);

                GUILayout.FlexibleSpace();

                GUILayout.Space(HORIZ_INDENT);

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical(); // current target build platform details
        }

        bool instShaderFoldout = false;
        private void InstalledShaderFoldoutGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // all installed pipelines

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            bool error = !shaderPackageValid;
            if (error)
            {
                instShaderFoldout = true;
            }

            string shaderLabel = installedShaderPipelineVersion.ToString() + " v" + installedShaderVersion.ToString();
            string foldoutLabel = "Current Shader Package: " + shaderLabel;
            instShaderFoldout = EditorGUILayout.Foldout(instShaderFoldout, new GUIContent(foldoutLabel, "Toggle foldout to see details of the available shader packages."), true, error ? guiStyles.FoldoutTitleErrorLabel : guiStyles.FoldoutTitleLabel);

            GUILayout.FlexibleSpace();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            if (instShaderFoldout)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Space(HORIZ_INDENT);

                GUILayout.BeginVertical();
                InstalledShaderPackageGUI();
                ValidateShaderPackageGUI();
                GUILayout.EndVertical();

                GUILayout.Space(HORIZ_INDENT);

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical();
        }

        private void InstalledPipelineGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // current pipeline

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            GUIContent pipeLabel = new GUIContent("Currently Installed Render Pipeline:  " + activePipeline.ToString() + (activeVersion.Equals(new Version(emptyVersion)) ? "" : " version: " + activeVersion.ToString()));
            GUILayout.Label(pipeLabel);

            GUILayout.FlexibleSpace();

            if (!string.IsNullOrEmpty(activePackageString))
            {
                GUIContent buttonContent = new GUIContent("Open In Package Manager", "Open the package manager to check for updates");
                if (GUILayout.Button(buttonContent, GUILayout.Width(PACKAGE_UPDATE_W)))
                {
                    UnityEditor.PackageManager.UI.Window.Open(activePackageString);
                }
            }

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical(); // current pipeline
        }

        private void AllInstalledPipelinesGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // all installed pipelines

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            GUIContent titleLabel = new GUIContent(installedPiplines.Count > 1 ? "Currently Installed Render Pipelines:" : "Currently Installed Render Pipeline:", "");
            GUILayout.Label(titleLabel);

            GUILayout.FlexibleSpace();

            string setTip = "The active pipeline can be changed in the quality section of the project settings (by assigning pipleine assets to differing quality levels - thse are maintained in the graphics section.";

            if (GUILayout.Button(new GUIContent("Quality Settings", setTip)))
            {
                SettingsService.OpenProjectSettings("Project/Quality");
            }

            GUILayout.Space(HORIZ_INDENT);

            if (GUILayout.Button(new GUIContent("Gfx Settings", setTip)))
            {
                SettingsService.OpenProjectSettings("Project/Graphics");
            }

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            foreach (InstalledPipelines pipe in installedPiplines)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Space(HORIZ_INDENT * 3);

                string activeTip = "";
                string versionLabel = pipe.InstalledPipeline.ToString() + (pipe.Version.Equals(new Version(emptyVersion)) ? "" : " version: " + pipe.Version.ToString());
                GUIStyle pipLabelStyle = guiStyles.InactiveLabel;

                if (activePipeline != InstalledPipeline.None)
                {
                    if (pipe.InstalledPipeline == activePipeline)
                    {
                        pipLabelStyle = guiStyles.ActiveLabel;
                        activeTip = "Active render pipeline: " + versionLabel;
                    }
                }
                GUILayout.Label(new GUIContent(versionLabel, activeTip), pipLabelStyle);

                GUILayout.FlexibleSpace();

                if (!string.IsNullOrEmpty(pipe.PackageString))
                {
                    GUIContent buttonContent = new GUIContent("Open In Package Manager", "Open the package manager to check for updates");
                    if (GUILayout.Button(buttonContent, GUILayout.Width(PACKAGE_UPDATE_W)))
                    {
                        UnityEditor.PackageManager.UI.Window.Open(pipe.PackageString);
                    }
                }

                GUILayout.Space(HORIZ_INDENT);

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical(); // all installed pipelines
        }

        private void InstalledShaderPackageGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // current shader package

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.Label("Required Shader Version:  ", guiStyles.InactiveLabel);
            GUILayout.Label(new GUIContent(activePipelineVersion.ToString(), "The current active render pipeline requires the " + activePipelineVersion.ToString() + ""), guiStyles.ActiveLabel);

            GUILayout.FlexibleSpace();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.Label("Currently Installed Shader Package:  ");
            GUIStyle shaderLabelStyle = GUI.skin.label;
            string shaderLabelTooltip = "";
            switch (installedShaderStatus)
            {
                case InstalledPackageStatus.Mismatch:
                    shaderLabelStyle = guiStyles.shMismatchLabel;
                    shaderLabelTooltip = "Installed shaders are for a different pipeline.";
                    break;
                case InstalledPackageStatus.Current:
                    shaderLabelStyle = guiStyles.shCurrentLabel;
                    shaderLabelTooltip = "Installed shaders match the current pipeline.";
                    break;
            }

            string shaderLabel = installedShaderPipelineVersion.ToString() + " v" + installedShaderVersion.ToString();
            GUILayout.Label(new GUIContent(shaderLabel, shaderLabelTooltip), shaderLabelStyle);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Check Status"))
            {
                UpdateGUI();
                //shaderPackageValid = TryValidateShaderPackage(out shaderPackageItems);
            }

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            GUILayout.Space(VERT_INDENT * 3);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            DetermineAction(out string result);
            GUILayout.Label(result, guiStyles.WrappedInfoLabel);

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            GUILayout.EndVertical(); // current shader package
        }

        Vector2 scrollPosShaderPackage = new Vector2();
        private void ValidateShaderPackageGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // validate shader package

            GUILayout.Space(VERT_INDENT);

            if (shaderPackageItems.Count > 0)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Space(HORIZ_INDENT);

                GUILayout.Label("Some Shader Files Invalid:  ", guiStyles.shTooHighLabel);

                GUILayout.FlexibleSpace();

                GUILayout.Space(HORIZ_INDENT);

                GUILayout.EndHorizontal();

                GUILayout.Space(VERT_INDENT);

                float lineHeight = 17f;
                float scrollHeight = 0f;

                if (shaderPackageItems.Count < 6)
                    scrollHeight = lineHeight * shaderPackageItems.Count + 27f;
                else
                    scrollHeight = 112f;

                scrollPosShaderPackage = GUILayout.BeginScrollView(scrollPosShaderPackage, GUILayout.Height(scrollHeight));
                foreach (ShaderPackageItem item in shaderPackageItems)
                {
                    GUILayout.BeginHorizontal();

                    GUILayout.Space(HORIZ_INDENT);

                    if (!item.Validated)
                    {
                        GUILayout.Label("Missing file ...  ", guiStyles.shMismatchLabel);  // extend to missing file invalid file size or hash?
                        GUILayout.Label(item.ItemName, guiStyles.InactiveLabel);
                        GUILayout.FlexibleSpace();
                    }

                    GUILayout.Space(HORIZ_INDENT);

                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical(); // validate shader package
        }

        public void AvailableShaderPackagesGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // available shader package

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.Label("Available Shader Packages:  ");

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(showAllPackages ? "Show only relevant packages" : "Show All Packages"))
            {
                showAllPackages = !showAllPackages;
            }

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            foreach (ShaderPackageManifest manifest in distroCatalog)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Space(HORIZ_INDENT * 3);

                bool relevant = manifest.Pipeline == activePipelineVersion;

                if (showAllPackages)
                {
                    GUILayout.Label(manifest.Pipeline + " " + manifest.Version, relevant ? guiStyles.shCurrentLabel : guiStyles.InactiveLabel);
                }
                else
                {
                    if (relevant)
                    {
                        GUILayout.Label(manifest.Pipeline + " " + manifest.Version, guiStyles.shCurrentLabel);
                    }
                }

                GUILayout.Space(HORIZ_INDENT);

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical(); // available shader package
        }

        bool actionRequired = false;
        bool actionToFollowFoldout = false;
        private void actionToFollowFoldoutGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // all installed pipelines

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            if (actionRequired)
                actionToFollowFoldout = true;

            string actionString = actionRequired ? "Action required..." : "No action required...";
            string tooltipString = actionRequired ? "Further user action is required..." : "No action required...";
            GUIStyle actionTitle = actionRequired ? guiStyles.FoldoutTitleErrorLabel : guiStyles.FoldoutTitleLabel;
            actionToFollowFoldout = EditorGUILayout.Foldout(actionToFollowFoldout, new GUIContent(actionString, tooltipString), true, actionTitle);

            GUILayout.FlexibleSpace();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            if (actionToFollowFoldout)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Space(HORIZ_INDENT);

                actionToFollowDialogGUI();

                GUILayout.Space(HORIZ_INDENT);

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical();
        }

        private void actionToFollowDialogGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // available shader package

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.Label("Content...");

            GUILayout.FlexibleSpace();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical();
        }

        public void UpdateGUI()
        {
            currentSettings = ImporterWindow.GeneralSettings;
            activePipelineVersion = DetermineActivePipelineVersion();
            installedPiplines = GetInstalledPipelines();
            distroCatalog = BuildPackageMap();
            titleContent = new GUIContent(titleString + " - " + PipelineVersionString(true));
            shaderPackageItems = new List<ShaderPackageItem>();
            shaderPackageValid = TryValidateShaderPackage(out shaderPackageItems);
            Repaint();

        }

        private void ShowOnStartupGUI()
        {
            if (currentSettings != null)  // avoids a null ref after assembly reload while waiting for frames
            {
                EditorGUI.BeginChangeCheck();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Show this window on startup ");
                currentSettings.showOnStartup = EditorGUILayout.Toggle(currentSettings.showOnStartup);
                if (EditorGUI.EndChangeCheck())
                {
                    ImporterWindow.SetGeneralSettings(currentSettings);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
        }



        // installation/uninstallation test functions
        bool testSectionFoldout = false;
        Vector2 testPosShaderPackage = new Vector2();
        private void FoldoutTestSection()
        {
            GUILayout.BeginVertical(GUI.skin.box); // all installed pipelines

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            testSectionFoldout = EditorGUILayout.Foldout(testSectionFoldout, new GUIContent("Test Functions", "Tooltip"), true, guiStyles.FoldoutTitleLabel);

            GUILayout.FlexibleSpace();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            if (testSectionFoldout)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Space(HORIZ_INDENT * 3);

                testPosShaderPackage = GUILayout.BeginScrollView(testPosShaderPackage, GUILayout.Height(200f));

                GUILayout.Label("Installed Shader Package:", EditorStyles.largeLabel);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_winbtn_mac_close_h").image, "UnInstall"), GUILayout.Width(20f)))
                {
                    UnInstallPackage();
                    UpdateGUI();
                }
                string shaderLabel = installedShaderPipelineVersion.ToString() + " v" + installedShaderVersion.ToString();
                GUILayout.Label(shaderLabel, EditorStyles.largeLabel);
                GUILayout.EndHorizontal();

                if (distroCatalog.Count == 0)
                    return;

                GUILayout.Label("Available Distribution Packages:", EditorStyles.largeLabel);
                foreach (ShaderPackageManifest manifest in distroCatalog)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_winbtn_mac_max_h").image, "Install " + Path.GetFileNameWithoutExtension(manifest.SourcePackageName)), GUILayout.Width(20f)))
                    {
                        Debug.Log("Installing Package: " + manifest.SourcePackageName);
                        InstallPackage(manifest);
                        UpdateGUI();
                    }
                    GUILayout.Label(manifest.SourcePackageName, EditorStyles.largeLabel);
                    GUILayout.EndHorizontal();
                }

                GUILayout.EndScrollView();

                GUILayout.Space(HORIZ_INDENT);

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical();
        }

        #endregion GUI

        #region UTIL
        public string emptyVersion = "0.0.0";
        public Version activeVersion = new Version(0, 0, 0);
        public InstalledPipeline activePipeline = InstalledPipeline.None;
        public PipelineVersion activePipelineVersion = PipelineVersion.None;
        public PipelineVersion installedShaderPipelineVersion = PipelineVersion.None;
        public Version installedShaderVersion = new Version(0, 0, 0);
        public InstalledPackageStatus installedShaderStatus = InstalledPackageStatus.None;
        public List<ShaderPackageManifest> distroCatalog;
        public string activePackageString = string.Empty;
        public List<InstalledPipelines> installedPiplines;
        public const string urpType = "UnityEngine.Rendering.Universal.UniversalRenderPipeline";
        public const string urpPackage = "com.unity.render-pipelines.universal";
        public const string hdrpType = "UnityEngine.Rendering.HighDefinition.HDRenderPipeline";
        public const string hdrpPackage = "com.unity.render-pipelines.high-definition";

        // find all currently installed render pipelines
        public List<InstalledPipelines> GetInstalledPipelines()
        {
            List<InstalledPipelines> installed = new List<InstalledPipelines>();
            installed.Add(new InstalledPipelines(InstalledPipeline.Builtin, new Version(emptyVersion), ""));

            UnityEditor.PackageManager.PackageInfo[] packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();

            // find urp
            UnityEditor.PackageManager.PackageInfo urp = packages.ToList().Find(p => p.name.Equals(urpPackage));
            if (urp != null)
            {
                installed.Add(new InstalledPipelines(InstalledPipeline.URP, new Version(urp.version), urpPackage));
            }

            // find hdrp
            UnityEditor.PackageManager.PackageInfo hdrp = packages.ToList().Find(p => p.name.Equals(hdrpPackage));
            if (hdrp != null)
            {
                installed.Add(new InstalledPipelines(InstalledPipeline.HDRP, new Version(hdrp.version), hdrpPackage));
            }

            return installed;
        }

        // determine the appropriate enum PipelineVersion corresponding to the current render pipeline
        public PipelineVersion DetermineActivePipelineVersion()
        {
            UnityEngine.Rendering.RenderPipeline r = RenderPipelineManager.currentPipeline;
            if (r != null)
            {
                UnityEditor.PackageManager.PackageInfo[] packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();

                if (r.GetType().ToString().Equals(urpType))
                {
                    string version = packages.ToList().Find(p => p.name.Equals(urpPackage)).version;
                    activePipeline = InstalledPipeline.URP;
                    activeVersion = new Version(version);
                    activePackageString = urpPackage;
                }
                else if (r.GetType().ToString().Equals(hdrpType))
                {
                    string version = packages.ToList().Find(p => p.name.Equals(hdrpPackage)).version;
                    activePipeline = InstalledPipeline.HDRP;
                    activeVersion = new Version(version);
                    activePackageString = hdrpPackage;
                }
            }
            else
            {
                activePipeline = InstalledPipeline.Builtin;
                activeVersion = new Version(emptyVersion);
            }

            platformMessage = string.Empty;

            switch (activePipeline)
            {
                case InstalledPipeline.Builtin:
                    {
                        return PipelineVersion.BuiltIn;
                    }
                case InstalledPipeline.HDRP:
                    {
                        if (activeVersion.Major < 12)
                            return PipelineVersion.HDRP;
                        else if (activeVersion.Major >= 12 && activeVersion.Major < 15)
                            return PipelineVersion.HDRP12;
                        else if (activeVersion.Major >= 15)
                            return PipelineVersion.HDRP15;
                        else return PipelineVersion.HDRP;
                    }
                case InstalledPipeline.URP:
                    {
                        if (activeVersion.Major < 12)
                            return PipelineVersion.URP;
                        else if (activeVersion.Major >= 12 && activeVersion.Major < 15)
                        {
                            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
                            {
                                platformMessage = urpPlatformMessage;
                                return PipelineVersion.URP12;
                            }
                            else
                            {
                                return PipelineVersion.URP12;
                            }
                        }
                        else if (activeVersion.Major >= 15)
                        {
                            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
                            {
                                platformMessage = urpPlatformMessage;
                                return PipelineVersion.URP12;
                            }
                            else
                            {
                                return PipelineVersion.URP15;
                            }
                        }
                        else return PipelineVersion.URP;
                    }
                case InstalledPipeline.None:
                    {
                        return PipelineVersion.None;
                    }
            }
            return PipelineVersion.None;
        }

        public string PipelineVersionString(bool title = false)
        {
            if (!title)
                return activePipeline.ToString() + (activeVersion.Equals(new Version(emptyVersion)) ? "" : " version: " + activeVersion.ToString());
            else
                return activePipeline.ToString() + (activeVersion.Equals(new Version(emptyVersion)) ? "" : " v" + activeVersion.ToString());
        }

        // build a list of all the shader packages available for import from the distribution package
        // the distribution should contain .unitypackage files paired with *_RL_referencemanifest.json files
        private List<ShaderPackageManifest> BuildPackageMap()
        {
            string search = "_RL_referencemanifest";
            //string[] searchLoc = new string[] { "Assets", "Packages/com.soupday.cc3_unity_tools" }; // look in assets too if the distribution is wrongly installed
            // in Unity 2021.3.14f1 ALL the assets on the "Packages/...." path are returned for some reason...
            // omiting the 'search in folders' parameter correctly finds assests matching the search term in both Assets and Packages
            string[] mainifestGuids = AssetDatabase.FindAssets(search);//, searchLoc);

            //Debug.Log("Found: " + mainifestGuids.Length);
            /*
            foreach (string guid in mainifestGuids)
            {
                if (AssetDatabase.GUIDToAssetPath(guid).Contains(search + ".json", StringComparison.InvariantCultureIgnoreCase))
                    Debug.Log(AssetDatabase.GUIDToAssetPath(guid));
            }
            */

            List<ShaderPackageManifest> manifestPackageMap = new List<ShaderPackageManifest>();

            foreach (string guid in mainifestGuids)
            {
                string manifestAssetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (manifestAssetPath.Contains(search + ".json", StringComparison.InvariantCultureIgnoreCase))
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
                            //Debug.Log(packageSearchTerm);
                            string[] shaderPackages = AssetDatabase.FindAssets(packageSearchTerm);//, searchLoc);
                                                                                                  //Debug.Log("Found: " + shaderPackages.Length);
                            if (shaderPackages.Length > 1)
                            {
                                Debug.LogWarning("Multiple shader packages detected for: " + packageManifest.SourcePackageName + " ... Aborting.");
                                return null;
                            }
                            else
                            {
                                string packageAssetPath = AssetDatabase.GUIDToAssetPath(shaderPackages[0]);
                                //Debug.Log("Found: " + packageAssetPath + " for: " + manifestAssetPath);
                                packageManifest.referenceMainfestPath = manifestAssetPath;
                                packageManifest.referenceShaderPackagePath = packageAssetPath;
                                manifestPackageMap.Add(packageManifest);
                            }
                        }
                    }
                }
            }
            Debug.Log("Returning manifestPackageMap containing: " + manifestPackageMap.Count + " entries.");
            return manifestPackageMap;
        }

        public void InstallPackage(ShaderPackageManifest shaderPackageManifest)
        {
            AssetDatabase.importPackageCompleted -= OnImportPackageCompleted;
            AssetDatabase.importPackageCompleted += OnImportPackageCompleted;

            AssetDatabase.onImportPackageItemsCompleted -= OnImportPackageItemsCompleted;
            AssetDatabase.onImportPackageItemsCompleted += OnImportPackageItemsCompleted;

            //AssetDatabase.ImportPackage(manifestPackageMap[0].referenceShaderPackagePath, true);
            AssetDatabase.ImportPackage(shaderPackageManifest.referenceShaderPackagePath, true);
        }

        private static void OnImportPackageCompleted(string packagename)
        {
            Debug.Log($"Imported package: {packagename}");
            AssetDatabase.importPackageCompleted -= OnImportPackageCompleted;
        }

        private static void OnImportPackageItemsCompleted(string[] items)
        {
            string manifestLabel = "_RL_shadermanifest.json";
            string manifest = string.Empty;
            string fullManifestPath = string.Empty;
            foreach (string item in items)
            {
                //Debug.Log(item);
                if (item.EndsWith(manifestLabel))
                {
                    manifest = item;
                    string datapath = Application.dataPath;
                    //fullManifestPath = datapath.Remove(datapath.Length - 6, 6) + item;
                    //Debug.Log("Post Install TEST -- AssetToFullPath for " + item + "found as: " + item.AssetToFullPath());
                    fullManifestPath = item.UnityAssetPathToFullPath();
                    Debug.Log("Post Install: using shader manifest " + fullManifestPath);
                    break;
                }
            }

            if (!string.IsNullOrEmpty(manifest))
            {
                ShaderPackageManifest shaderPackageManifest = ReadJson(manifest);
                foreach (string item in items)
                {
                    string itemGUID = AssetDatabase.AssetPathToGUID(item, AssetPathToGUIDOptions.OnlyExistingAssets);
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
                    /*
                    else
                    {
                        if (!AssetDatabase.IsValidFolder(item))
                            Debug.Log(fileName + "    **************** NON DIRECTORY -- NOT FOUND ***************");
                    }
                    */
                }
                Debug.Log("Post Install: Updating manifest: " + fullManifestPath);
                string jsonString = JsonUtility.ToJson(shaderPackageManifest);
                File.WriteAllText(fullManifestPath, jsonString);
                AssetDatabase.Refresh();
            }
            Instance.UpdateGUI();
            AssetDatabase.onImportPackageItemsCompleted -= OnImportPackageItemsCompleted;
        }

        public void UnInstallPackage()
        {
            string manifestLabel = "_RL_shadermanifest";
            string[] searchLoc = new string[] { "Assets" };
            string[] mainifestGuids = AssetDatabase.FindAssets(manifestLabel, searchLoc);

            if (mainifestGuids.Length == 0)
            {
                Debug.LogWarning("No shader packages have been found!!");
            }

            if (mainifestGuids.Length > 1)
            {
                Debug.LogWarning("Multiple installed shader packages have been found!! - uninstalling ALL");
            }

            foreach (string guid in mainifestGuids)
            {
                if (TryUnInstallPackage(guid))
                {
                    Debug.Log("Package uninstalled");
                }
                else
                {
                    Debug.Log("Package could not be uninstalled");
                }
            }
        }

        private bool TryUnInstallPackage(string guid, bool toTrash = true)
        {
            Object manifestObject = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid));
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

                Debug.Log("Uninstalling files" + (toTrash ? " to OS trash folder" : "") + " from: " + " Pipeline: " + shaderPackageManifest.Pipeline + " Version: " + shaderPackageManifest.Version + " (" + shaderPackageManifest.FileName + ")");

                foreach (ShaderPackageItem thing in shaderPackageManifest.Items)
                {
                    string deletePath = AssetDatabase.GUIDToAssetPath(thing.InstalledGUID);
                    if (!AssetDatabase.IsValidFolder(deletePath))
                    {
                        //Debug.Log("Installed GUID: " + thing.InstalledGUID + " path: " + deletePath);
                        if (deletePath.StartsWith("Assets"))
                            deleteList.Add(deletePath);
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
                    Debug.Log("No Folders");
                }

                manifest = string.Empty;
            }
            else { return false; }

            // delete all paths in deleteList 
            List<string> failedPaths = new List<string>();
            bool hasFailedPaths = false;
            deleteList.Add(AssetDatabase.GUIDToAssetPath(guid));

            if (toTrash)
                hasFailedPaths = AssetDatabase.MoveAssetsToTrash(deleteList.ToArray(), failedPaths);
            else
                hasFailedPaths = AssetDatabase.DeleteAssets(deleteList.ToArray(), failedPaths);


            if (hasFailedPaths)
            {
                foreach (string path in failedPaths)
                {
                    Debug.Log(path + " ...failed to delete.");
                }
            }

            // delete the manifest
            //AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            AssetDatabase.Refresh();
            return hasFailedPaths;
        }


        public static ShaderPackageManifest ReadJson(string assetPath)
        {
            Object sourceObject = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            TextAsset sourceAsset = sourceObject as TextAsset;
            string jsonText = sourceAsset.text;

            return JsonUtility.FromJson<ShaderPackageManifest>(jsonText);
        }

        public bool TryValidateShaderPackage(out List<ShaderPackageItem> itemList)
        {
            Debug.Log("Checking Installed Shader Package");
            // find the file containing _RL_shadermanifest.json
            // compare manifest contents to whats on the disk
            string[] manifestGUIDS = AssetDatabase.FindAssets("_RL_shadermanifest", new string[] { "Assets" });
            string guid = string.Empty;
            InstalledPackageStatus status = InstalledPackageStatus.None;
            itemList = new List<ShaderPackageItem>();

            if (manifestGUIDS.Length > 1)
            {
                status = InstalledPackageStatus.Multiple; // Problem
                installedShaderStatus = status;
                return false;
            }

            if (manifestGUIDS.Length > 0)
                guid = manifestGUIDS[0];
            else
                return false;

            //foreach (string guid in manifestGUIDS)
            //{            
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            ShaderPackageManifest shaderPackageManifest = ReadJson(assetPath);
            shaderPackageManifest.ManifestPath = assetPath;
            installedShaderPipelineVersion = shaderPackageManifest.Pipeline;
            installedShaderVersion = new Version(shaderPackageManifest.Version);
            foreach (ShaderPackageItem item in shaderPackageManifest.Items)
            {
                item.Validated = false;
                string itemPath = string.Empty;
                string relToDataPath = string.Empty;
                string fullPath = string.Empty;

                itemPath = AssetDatabase.GUIDToAssetPath(string.IsNullOrEmpty(item.InstalledGUID) ? item.GUID : item.InstalledGUID);

                if (itemPath.Length > 6)
                    relToDataPath = itemPath.Remove(0, 6);
                fullPath = Application.dataPath + relToDataPath;

                //Debug.Log("Checking: " + item.ItemName + " at " + fullPath);
                if (File.Exists(fullPath))
                {
                    item.Validated = true;
                }
                //Debug.Log(fullPath + " " + (item.Validated ? "EXISTS" : "DOES NOT EXIST"));
                if (!item.Validated)
                {
                    itemList.Add(item);
                }
                /*
                if (string.IsNullOrEmpty(item.InstalledGUID))  // correct installation will fill this field with the guid that the file wast imported into
                {
                    itemPath = AssetDatabase.GUIDToAssetPath(item.GUID);
                    if (itemPath.Length > 6)
                        relToDataPath = itemPath.Remove(0, 6);
                    fullPath = Application.dataPath + relToDataPath;
                    if (File.Exists(fullPath))
                    {
                        item.Validated = true;
                    }
                    Debug.Log(fullPath + " " + (File.Exists(fullPath) ? "EXISTS" : "DOES NOT EXIST"));

                }
                else
                {
                    itemPath = AssetDatabase.GUIDToAssetPath(item.InstalledGUID);
                    if (itemPath.Length > 6)
                        relToDataPath = itemPath.Remove(0, 6);
                    fullPath = Application.dataPath + relToDataPath;
                    if (File.Exists(fullPath))
                    {
                        item.Validated = true;
                    }
                    Debug.Log(fullPath + " " + (File.Exists(fullPath) ? "EXISTS" : "DOES NOT EXIST"));
                }
                */
            }

            Debug.Log(shaderPackageManifest.Items.FindAll(x => x.Validated == false).Count() + " Missing Items. ***");
            Debug.Log(shaderPackageManifest.Items.FindAll(x => x.Validated == true).Count() + " Found Items. *** out of " + shaderPackageManifest.Items.Count);
            Debug.Log("Invalid Items List contains: " + itemList.Count + " items.");
            int missingItems = shaderPackageManifest.Items.FindAll(x => x.Validated == false).Count();
            installedShaderStatus = GetPackageStatus(itemList);
            if ((installedShaderStatus == InstalledPackageStatus.Current || installedShaderStatus == InstalledPackageStatus.VersionTooHigh) && missingItems == 0)
            {
                // current version or higher with no missing items
                return true;
            }
            else
            {
                return false;
            }
        }

        public InstalledPackageStatus GetPackageStatus(List<ShaderPackageItem> missingItemList)
        {
            List<ShaderPackageManifest> applicablePackages = distroCatalog.FindAll(x => x.Pipeline == activePipelineVersion);

            if (installedShaderPipelineVersion == activePipelineVersion)
            {
                if (applicablePackages.Count > 0)
                {
                    // determine the max available version
                    applicablePackages.Sort((a, b) => b.Version.ToVersion().CompareTo(a.Version.ToVersion()));  // descending sort

                    Debug.Log("VERSION SORTER**********");
                    foreach (ShaderPackageManifest pkg in applicablePackages)
                    {
                        Debug.Log(pkg.FileName + " " + pkg.Version);
                    }
                    Debug.Log("MAX VERSION******* " + applicablePackages[0].Version);

                    Version maxVersion = applicablePackages[0].Version.ToVersion();
                    if (installedShaderVersion == maxVersion)
                    {
                        if (missingItemList.Count == 0)
                            return InstalledPackageStatus.Current;
                        else if (missingItemList.Count > 0)
                            return InstalledPackageStatus.MissingFiles;
                    }
                    else if (installedShaderVersion < maxVersion)
                        return InstalledPackageStatus.Upgradeable;
                    else if (installedShaderVersion > maxVersion)
                        return InstalledPackageStatus.VersionTooHigh;
                }
            }
            else
            {
                return InstalledPackageStatus.Mismatch;
            }
            return InstalledPackageStatus.None;
        }

        public void DetermineAction(out string result)
        {
            result = string.Empty;

            // determine these first
            // shaderPackageValid; // bool        
            // installedShaderStatus; // InstalledPackageStatus

            // result cases
            string multiple = "Multiple shader packages detected. [Force] Uninstall both then install applicable package.";
            string mismatch = "Active pipeline doesnt match installed shader package. [Force] Uninstall then install applicable package.";
            string normalUpgrade = "Shader package can be upgraded. [Offer] install newer package.";
            string forceUpgrade = "Shader package can be upgraded - Current Installation has errors. [Force] install newer package.";
            string normalDowngrade = "Shader package is from a higher version of the tool. [Offer] install package version from this distribution.";
            string forceDowngrade = "Shader package is from a higher version of the tool - Current Installation has errors. [Force] install package version from this distribution.";
            string currentValid = "Current Shader is correctly installed and matches pipeline version";
            string currentInvalid = "Current Sahder is incorrectly installed (but does match current pipleine version";

            switch (installedShaderStatus)
            {
                case (InstalledPackageStatus.Multiple):
                    {
                        result = multiple;
                        break;
                    }
                case (InstalledPackageStatus.Mismatch):
                    {
                        result = mismatch;
                        break;
                    }
                case (InstalledPackageStatus.Upgradeable):
                    {
                        if (shaderPackageValid)
                            result = normalUpgrade;
                        else
                            result = forceUpgrade;
                        break;
                    }
                case (InstalledPackageStatus.VersionTooHigh):
                    {
                        if (shaderPackageValid)
                            result = normalDowngrade;
                        else
                            result = forceDowngrade;
                        break;
                    }
                case (InstalledPackageStatus.Current):
                    {
                        if (shaderPackageValid)
                            result = currentValid;
                        else
                            result = currentInvalid;
                        break;
                    }
            }
        }

        #endregion UTIL

        #region ENUM+CLASSES
        public enum InstalledPipeline
        {
            None,
            Builtin,
            URP,
            HDRP
        }

        public enum PipelineVersion
        {
            None = 0,
            BuiltIn = 1,
            URP = 2,
            URP12 = 3,
            URP15 = 4,
            URP16 = 5,
            URP17 = 6,
            URP18 = 7,
            URP19 = 8,
            URP20 = 9,
            URP21 = 10,
            URP22 = 11,
            URP23 = 12,
            URP24 = 13,
            URP25 = 14,
            URP26 = 15,
            HDRP = 16,
            HDRP12 = 17,
            HDRP15 = 18,
            HDRP16 = 19,
            HDRP17 = 20,
            HDRP18 = 21,
            HDRP19 = 22,
            HDRP20 = 23,
            HDRP21 = 24,
            HDRP22 = 25,
            HDRP23 = 26,
            HDRP24 = 27,
            HDRP25 = 28,
            HDRP26 = 29
        }

        public enum InstalledPackageStatus
        {
            None,
            Current,
            Upgradeable,
            VersionTooHigh,  // deal with package from a higher distro release
            MissingFiles,
            Mismatch,
            Multiple  // Treat the presence of multiple shader packages as a serious problem
        }

        [Serializable]
        public class ShaderPackageItem
        {
            public string ItemName;
            //public bool ValidItemName;
            //public long FileSize;
            //public bool ValidFileSize;
            public string GUID;
            public string InstalledGUID;
            //public bool ValidGUID;
            public bool Validated;

            public ShaderPackageItem(string itemName, string gUID)// long fileSize, string gUID)
            {
                ItemName = itemName;
                //ValidItemName = false;
                //FileSize = fileSize;
                //ValidFileSize = false;
                GUID = gUID;
                InstalledGUID = string.Empty;
                //ValidGUID = false;
                Validated = false;
            }
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
            return datapath.Remove(datapath.Length - 6, 6) + str;
        }
    }

    public static class StringFullPathToAssetPath
    {
        public static string FullPathToUnityAssetPath(this string str)
        {
            return str.Remove(0, Application.dataPath.Length - 6).Replace("\\", "/");
        }
    }
    #endregion STRING EXTENSION
}