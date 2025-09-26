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
using Object = UnityEngine.Object;
using System.Reflection;

namespace Reallusion.Import
{
    public class ShaderPackageUpdater : EditorWindow
    {
        #region SETUP
        [SerializeField]
        public static ShaderPackageUpdater Instance;
        private static RLSettingsObject currentSettings;
        private static bool showUtility = true;

        [MenuItem("Reallusion/Check For Updates", priority = 6000)]
        public static void MenuCreateWindow()
        {
            UpdateManager.TryPerformUpdateChecks(true);
        }

        [MenuItem("Reallusion/Check For Updates", true)]
        public static bool MenuValidateWindow()
        {
            return !EditorWindow.HasOpenInstances<ShaderPackageUpdater>() && ImporterWindow.Current != null;
        }

        public static void CreateWindow()
        {
            if (!EditorWindow.HasOpenInstances<ShaderPackageUpdater>())
                Instance = OpenWindow();
        }

        public static ShaderPackageUpdater OpenWindow()
        {
            float width = 600f;
            float height = 600f;
            Rect centerPosition = Util.GetRectToCenterWindow(width, height);

            ShaderPackageUpdater window = ScriptableObject.CreateInstance<ShaderPackageUpdater>();
            if (showUtility)
                window.ShowUtility();
            else
                window.Show();
            window.minSize = new Vector2(width, height);
            window.maxSize = new Vector2(width, height);
            window.position = centerPosition;
            return window;
        }

        private void OnEnable()
        {
            //Debug.Log("ShaderPackageUpdater.OnEnable");
            //Debug.Log(UpdateManager.activePipelineVersion);
            currentSettings = ImporterWindow.GeneralSettings;

            initGUI = true;
            allInstPipeFoldout = false;
            buildPlatformFoldout = false;
            instShaderFoldout = false;
            actionToFollowFoldout = false;

            // RenderPipelineManager.currentPipeline is unavailable for a few frames after assembly reload (and entering play mode)
            // see: https://issuetracker.unity3d.com/issues/hdrp-renderpipelinemanager-dot-currentpipeline-is-null-for-the-first-few-frames-of-playmode
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        private void OnDestroy()
        {
            //Debug.Log("ShaderPackageUpdater.OnDestroy");
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
        }

        private void OnDisable()
        {
            //Debug.Log("ShaderPackageUpdater.OnDisable");
        }

        public void OnBeforeAssemblyReload()
        {
            //Debug.Log("ShaderPackageUpdater.OnBeforeAssemblyReload");
        }

        public void OnAfterAssemblyReload()
        {
            //Debug.Log("ShaderPackageUpdater.OnAfterAssemblyReload");
            FrameTimer.CreateTimer(15, FrameTimer.onAfterAssemblyReload, GetInstanceAfterReload);
        }
    
        public static void GetInstanceAfterReload(object obj, FrameTimerArgs args)
        {
            // broken? ???
            if (args.ident == FrameTimer.onAfterAssemblyReload)
            {
                if (EditorWindow.HasOpenInstances<ShaderPackageUpdater>())
                    Instance = EditorWindow.GetWindow<ShaderPackageUpdater>(showUtility, titleString, false);
                FrameTimer.OnFrameTimerComplete -= GetInstanceAfterReload;
            }
        }
        #endregion SETUP

        #region GUI
        private const string titleString = "CC/iC Importer - Shader Package Updater";
        public Styles guiStyles;
        private float VERT_INDENT = 2f;
        private float HORIZ_INDENT = 5f;
        private float SECTION_SPACER = 2f;
        private float PACKAGE_UPDATE_W = 180f;
        private bool showAllPackages = false;

        public class Styles
        {
            public GUIStyle SectionHeader;
            public GUIStyle SubSectionTitle;
            public GUIStyle InactiveLabel;
            public GUIStyle InactiveHighlighLabel;
            public GUIStyle ActiveLabel;
            public GUIStyle VersionLabel;
            public GUIStyle shCurrentLabel;
            public GUIStyle shUpgradeableLabel;
            public GUIStyle shTooHighLabel;
            public GUIStyle shMismatchLabel;
            public GUIStyle FoldoutTitleLabel;
            public GUIStyle FoldoutTitleErrorLabel;
            public GUIStyle FoldoutTitleStopLabel;
            public GUIStyle WrappedInfoLabel;
            public GUIStyle WrappedInfoLabelColor;

            public GUIStyle infoText;
            public GUIStyle infoTextGrn;
            public GUIStyle infoTextYel;
            public GUIStyle infoTextItal;
            public GUIStyle httpText;
            public GUIStyle httpTextClicked;

            public Styles()
            {
                Color activeColor = Color.cyan * 0.95f;
                Color colGreen = Color.green * 0.75f;
                Color colBlue = Color.blue * 0.75f;
                Color colYellow = Color.yellow * 0.75f;
                Color colRed = Color.red * 0.85f;

                SectionHeader = new GUIStyle(GUI.skin.label);
                SectionHeader.fontSize = 14;
                SectionHeader.fontStyle = FontStyle.BoldAndItalic;

                SubSectionTitle = new GUIStyle(GUI.skin.label);
                SubSectionTitle.fontSize = 12;
                SubSectionTitle.fontStyle = FontStyle.Italic;
                SubSectionTitle.normal.textColor = Color.gray;

                InactiveLabel = new GUIStyle(GUI.skin.label);
                InactiveLabel.wordWrap = true;

                InactiveHighlighLabel = new GUIStyle(GUI.skin.label);
                InactiveHighlighLabel.wordWrap = true;
                InactiveHighlighLabel.normal.textColor = colYellow;
                InactiveHighlighLabel.hover.textColor = colYellow;

                ActiveLabel = new GUIStyle(GUI.skin.label);
                ActiveLabel.normal.textColor = activeColor;
                ActiveLabel.hover.textColor = activeColor;

                VersionLabel = new GUIStyle(GUI.skin.textField);
                VersionLabel.normal.textColor = Color.gray;

                shCurrentLabel = new GUIStyle(GUI.skin.label);
                shCurrentLabel.normal.textColor = colGreen;
                shCurrentLabel.hover.textColor = colGreen;

                shMismatchLabel = new GUIStyle(GUI.skin.label);
                shMismatchLabel.normal.textColor = colRed;
                shMismatchLabel.hover.textColor = colRed;

                shTooHighLabel = new GUIStyle(GUI.skin.label);
                shTooHighLabel.normal.textColor = colYellow;
                shTooHighLabel.hover.textColor = colYellow;

                FoldoutTitleLabel = new GUIStyle(EditorStyles.foldout);
                FoldoutTitleLabel.fontSize = 14;
                Color ftlcol = EditorStyles.foldout.normal.textColor;
                FoldoutTitleLabel.fontStyle = FontStyle.BoldAndItalic;
                //FoldoutTitleLabel.active.textColor = ftlc;
                //FoldoutTitleLabel.onActive.textColor = ftlc;
                FoldoutTitleLabel.focused.textColor = ftlcol;
                FoldoutTitleLabel.onFocused.textColor = ftlcol;

                FoldoutTitleErrorLabel = new GUIStyle(EditorStyles.foldout);
                FoldoutTitleErrorLabel.onNormal.textColor = colYellow;
                FoldoutTitleErrorLabel.fontSize = 14;
                FoldoutTitleErrorLabel.fontStyle = FontStyle.BoldAndItalic;

                FoldoutTitleStopLabel = new GUIStyle(EditorStyles.foldout);
                FoldoutTitleStopLabel.onNormal.textColor = colRed;
                FoldoutTitleStopLabel.fontSize = 14;
                FoldoutTitleStopLabel.fontStyle = FontStyle.BoldAndItalic;

                WrappedInfoLabel = new GUIStyle(GUI.skin.label);
                WrappedInfoLabel.wordWrap = true;

                WrappedInfoLabelColor = new GUIStyle(GUI.skin.label);
                WrappedInfoLabelColor.wordWrap = true;
                WrappedInfoLabelColor.fontSize = 13;
                WrappedInfoLabelColor.normal.textColor = colYellow;
                WrappedInfoLabelColor.hover.textColor = colYellow;

                infoText = new GUIStyle(GUI.skin.label);
                infoText.fontSize = 14;
                infoText.normal.textColor = Color.white;
                infoText.hover.textColor = Color.white;
                infoText.wordWrap = true;

                infoTextGrn = new GUIStyle(GUI.skin.label);
                infoTextGrn.fontSize = 14;
                infoTextGrn.normal.textColor = colGreen;
                infoTextGrn.hover.textColor = colGreen;
                infoTextGrn.wordWrap = true;

                infoTextYel = new GUIStyle(GUI.skin.label);
                infoTextYel.fontSize = 14;
                infoTextYel.normal.textColor = colYellow;
                infoTextYel.hover.textColor = colYellow;
                infoTextYel.wordWrap = true;

                infoTextItal = new GUIStyle(GUI.skin.label);
                infoTextItal.fontSize = 14;
                infoTextItal.fontStyle = FontStyle.Italic;
                infoTextItal.normal.textColor = Color.white;
                infoTextItal.hover.textColor = Color.white;
                //infoTextItal.wordWrap = true;

                httpText = new GUIStyle(GUI.skin.label);
                httpText.fontSize = 14;
                httpText.normal.textColor = new Color(0.035f, 0.41f, 0.85f);
                httpText.hover.textColor = Color.cyan;

                httpTextClicked = new GUIStyle(GUI.skin.label);
                httpTextClicked.fontSize = 14;
                httpTextClicked.normal.textColor = Color.magenta * 0.85f;
                httpTextClicked.hover.textColor = Color.magenta * 0.5f;
            }
        }

        bool initGUI = true;
        private Texture2D iconInstallShaderG;
        private Texture2D iconInstallShaderY;
        private Texture2D iconInstallShaderR;
        private Texture2D iconInstallRuntimeG;
        private Texture2D iconInstallRuntimeY;
        private Texture2D iconInstallRuntimeR;
        private Texture2D iconUpgradePipelineY;
        private Texture2D iconInstallLegacyW;

        public void InitGUI()
        {
            string[] folders = new string[] { "Assets", "Packages" };
            iconInstallShaderG = Util.FindTexture(folders, "RLIcon-ShaderStatus-G");
            iconInstallShaderY = Util.FindTexture(folders, "RLIcon-ShaderStatus-Y");
            iconInstallShaderR = Util.FindTexture(folders, "RLIcon-ShaderStatus-R");
            iconInstallRuntimeG = Util.FindTexture(folders, "RLIcon-RuntimeStatus-G");
            iconInstallRuntimeY = Util.FindTexture(folders, "RLIcon-RuntimeStatus-Y");
            iconInstallRuntimeR = Util.FindTexture(folders, "RLIcon-RuntimeStatus-R");
            iconUpgradePipelineY = Util.FindTexture(folders, "RLIcon_Upgrade_Pipeline_Y");
            iconInstallLegacyW = Util.FindTexture(folders, "RLIcon_Install_Shader_W");
            
            initGUI = false;
        }

        public void UpdateGUI()
        {
            currentTarget = EditorUserBuildSettings.activeBuildTarget;
            //UpdateManager.TryPerformUpdateChecks();
        }

        public bool softwareActionRequired = false;
        bool currentSoftwareVersionFoldout = false;
        public bool pipeLineActionRequired = false;
        public bool shaderGraphActionRequired = false;
        bool allInstPipeFoldout = false;
        public bool instShaderFoldout = false;
        public bool instRuntimeFoldout = false;
        Vector2 mainScrollPos;

        private void OnGUI()
        {
            if (initGUI) InitGUI();

            if (guiStyles == null) guiStyles = new Styles();

            if (currentSettings == null) currentSettings = ImporterWindow.GeneralSettings;

            // insulation against undetermined pipeline and packages
            if (UpdateManager.shaderPackageValid == ShaderPackageUtil.PackageVailidity.Waiting || UpdateManager.shaderPackageValid == ShaderPackageUtil.PackageVailidity.None) return;

            titleContent = new GUIContent(titleString + " - " + PipelineVersionString(true));

            mainScrollPos = GUILayout.BeginScrollView(mainScrollPos);

            GUILayout.BeginVertical(); // whole window contents

            GUILayout.Space(SECTION_SPACER);

            CurrentSoftwareVersionFoldoutGUI();

            GUILayout.Space(SECTION_SPACER);

            AllInstalledPipelinesFoldoutGUI();

            GUILayout.Space(SECTION_SPACER);

            CurrentBuildPlatformGUI();

            GUILayout.Space(SECTION_SPACER);

            ShaderGraphGUI();

            GUILayout.Space(SECTION_SPACER);

            LegacyShaderGUI();

            GUILayout.Space(SECTION_SPACER);

            InstalledShaderFoldoutGUI();

            GUILayout.Space(SECTION_SPACER);

            InstalledRuntimeFoldoutGUI();

            GUILayout.Space(SECTION_SPACER);

            ActionToFollowFoldoutGUI();

            GUILayout.FlexibleSpace();

            // test functions
            bool test = true;
            if (test)
            {
                FoldoutTestSection();                
            }
            // test functions ends 

            GUILayout.EndScrollView();

            GUILayout.Space(SECTION_SPACER);

            GUILayout.EndVertical(); // whole window contents

            ShowOnStartupGUI();

            GUILayout.Space(SECTION_SPACER);
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

        private void CurrentSoftwareVersionFoldoutGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // all installed pipelines

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            GUIStyle titleLabel = newVersionAvailable ? guiStyles.FoldoutTitleErrorLabel : guiStyles.FoldoutTitleLabel;

            if (softwareActionRequired)
                currentSoftwareVersionFoldout = true;

            GUIStyle softwareActionTitle = softwareActionRequired ? guiStyles.FoldoutTitleErrorLabel : guiStyles.FoldoutTitleLabel;
            currentSoftwareVersionFoldout = EditorGUILayout.Foldout(currentSoftwareVersionFoldout, new GUIContent("Current Software Version: " + Pipeline.VERSION, "Tooltip"), true, softwareActionTitle);

            GUILayout.FlexibleSpace();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            if (currentSoftwareVersionFoldout)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Space(HORIZ_INDENT);

                CurrentSoftwareVersionGUI();

                GUILayout.Space(HORIZ_INDENT);

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical();
        }

        float DROPDOWN_BTN_WIDTH = 140f;
        float SCROLLABLE_HEIGHT = 212f;
        Rect prev = new Rect();        
        bool initInfo = false;
        public RLSettingsObject settings;
        [SerializeField]
        Version gitHubLatestVersion;
        [SerializeField]
        Version installedVersion;
        [SerializeField]
        bool newVersionAvailable = false;
        [SerializeField]
        DateTime gitHubPublishedDateTime;
        //bool linkClicked = false;
        //[SerializeField]
        public static List<RLToolUpdateUtil.JsonFragment> fullJsonFragment;
        Vector2 swVerPos;

        private void InitInfo()
        {
            if (ImporterWindow.Current != null)
            {
                if (ImporterWindow.GeneralSettings != null)
                    settings = ImporterWindow.GeneralSettings;
                else
                    settings = RLSettings.FindRLSettingsObject();
            }
            else
            {
                settings = RLSettings.FindRLSettingsObject();
            }
            if (Version.TryParse(Pipeline.VERSION, out Version ver)) { installedVersion = ver; } else { installedVersion = new Version(); }
            gitHubLatestVersion = RLToolUpdateUtil.TagToVersion(settings.jsonTagName);
            newVersionAvailable = installedVersion < gitHubLatestVersion;
            RLToolUpdateUtil.TryParseISO8601toDateTime(settings.jsonPublishedAt, out gitHubPublishedDateTime);
            fullJsonFragment = RLToolUpdateUtil.GetFragmentList<RLToolUpdateUtil.JsonFragment>(settings.fullJsonFragment);
            initInfo = true;
        }

        private void CurrentSoftwareVersionGUI()
        {
            if (!initInfo)
                InitInfo();

            GUILayout.BeginVertical();
            swVerPos = GUILayout.BeginScrollView(swVerPos, GUILayout.Height(SCROLLABLE_HEIGHT));

            GUIStyle versionStyling = newVersionAvailable ? guiStyles.infoTextYel : guiStyles.infoTextGrn;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Latest Version: ", guiStyles.infoTextItal);
            GUILayout.Label(gitHubLatestVersion.ToString(), versionStyling);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Release Date/Time: ", guiStyles.infoTextItal);
            GUILayout.Label(gitHubPublishedDateTime.ToString(), versionStyling);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Web Link: ", guiStyles.infoTextItal);
            //if (GUILayout.Button(settings.jsonHtmlUrl.ToString(), linkClicked ? guiStyles.httpTextClicked : guiStyles.httpText))
            if (GUILayout.Button(new GUIContent("Visit release webpage", "Download from the 'Source code (zip) link in the 'Assets' section, and extract the zip file to a suitable permenant location.  Use the package manager to 'Remove' the current version of the CCiC Unity Tools package, then 'Add package from disk' and navigate to the newly extracted one.")))
            {
                Application.OpenURL(settings.jsonHtmlUrl);
                //linkClicked = true;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(22f);
            GUILayout.Label("Release Notes: ", guiStyles.infoTextItal);
            if (settings.jsonBodyLines != null && settings.jsonBodyLines.Length > 0)
            {
                foreach (string line in settings.jsonBodyLines)
                {
                    GUILayout.Label(line, guiStyles.infoText);
                }
            }

            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            TimeSpan fiveMins = new TimeSpan(0, 0, 5, 0, 0);
            bool interval = RLToolUpdateUtil.TimeCheck(settings.lastUpdateCheck, fiveMins);
            EditorGUI.BeginDisabledGroup(!interval);
            if(GUILayout.Button(new GUIContent("Check For Updates", interval ? "Check GitHub for updates" : "Last update check was too recent.  GitHub restricts the rate of checks.")))
            {
                RLToolUpdateUtil.UpdaterWindowCheckForUpdates();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();            
            
            GUILayout.Space(HORIZ_INDENT);

            if (Event.current.type == EventType.Repaint)
                prev = GUILayoutUtility.GetLastRect();

            //EditorGUI.BeginDisabledGroup(RLToolUpdateUtil.fullJsonFragment == null);

            if (EditorGUILayout.DropdownButton(
                content: new GUIContent("Previous Releases", "Show all previous releases on github."),
                focusType: FocusType.Passive,
                options: GUILayout.Width(DROPDOWN_BTN_WIDTH)))
            {
                RLToolUpdateWindow.ShowAtPosition(new Rect(prev.x - RLToolUpdateWindow.DROPDOWN_WIDTH + DROPDOWN_BTN_WIDTH + 3 * HORIZ_INDENT, prev.y + 20f, prev.width, prev.height));
            }

            //EditorGUI.EndDisabledGroup();

            //GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void AllInstalledPipelinesFoldoutGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // all installed pipelines

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            if (pipeLineActionRequired)
                allInstPipeFoldout = true;

            GUIStyle foldoutTitleLabelStyle = pipeLineActionRequired ? guiStyles.FoldoutTitleErrorLabel : guiStyles.FoldoutTitleLabel;
            string foldoutLabel = "Current Render Pipeline: " + UpdateManager.activePipeline.ToString() + (UpdateManager.activeVersion.Equals(new Version(emptyVersion)) ? "" : " version: " + UpdateManager.activeVersion.ToString());
            allInstPipeFoldout = EditorGUILayout.Foldout(allInstPipeFoldout, new GUIContent(foldoutLabel, "Toggle foldout to see details of the available pipelines."), true, foldoutTitleLabelStyle);

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
        [SerializeField]
        private BuildTarget currentTarget;

        bool buildPlatformFoldout = false;
        private void CurrentBuildPlatformGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // current target build platform

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            if (currentTarget != EditorUserBuildSettings.activeBuildTarget) UpdateGUI();

            buildPlatformFoldout = EditorGUILayout.Foldout(buildPlatformFoldout, new GUIContent("Current Build Platform: " + EditorUserBuildSettings.activeBuildTarget.ToString(), ""), true, guiStyles.FoldoutTitleLabel);

            GUILayout.FlexibleSpace();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            if (UpdateManager.platformRestriction != ShaderPackageUtil.PlatformRestriction.None)
            {
                buildPlatformFoldout = true;
            }

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

            if (UpdateManager.platformRestriction != ShaderPackageUtil.PlatformRestriction.None)
            {
                GUILayout.Space(VERT_INDENT);

                GUILayout.BeginHorizontal();

                GUILayout.Space(HORIZ_INDENT);

                GUILayout.Label(GetPlatformRestrictionText(), guiStyles.WrappedInfoLabelColor);

                GUILayout.FlexibleSpace();

                GUILayout.Space(HORIZ_INDENT);

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical(); // current target build platform details
        }


        bool shaderGraphFoldout = false;
        private void ShaderGraphGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // current target build platform

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            bool error = shaderGraphActionRequired;
            if (error)
            {
                shaderGraphFoldout = true;
            }

            string foldoutLabel = "Shader Graph Settings";
            shaderGraphFoldout = EditorGUILayout.Foldout(shaderGraphFoldout, new GUIContent(foldoutLabel, "Toggle foldout to see shortcuts to the project settings and preferences for Shader Graph - with more complex shaders it is important to have a Shader Variant limit of 2048.  This has to be done manually (it can be sticky to set - move focus away from the int field to update the value)."), true, error ? guiStyles.FoldoutTitleStopLabel : guiStyles.FoldoutTitleLabel);

            GUILayout.FlexibleSpace();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();
            
            if (shaderGraphFoldout)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Space(HORIZ_INDENT);

                ShaderGraphSettingsGUI();

                GUILayout.Space(HORIZ_INDENT);

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical(); // current target build platform
        }

        private void ShaderGraphSettingsGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // current target build platform details

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();

            GUILayout.Label("Shader Variant Limit", guiStyles.ActiveLabel);

            GUILayout.Label("should be at least", guiStyles.InactiveLabel);

            GUILayout.Label("2048", guiStyles.ActiveLabel);

            GUILayout.FlexibleSpace();

            GUILayout.EndHorizontal();

#if UNITY_6000_0_OR_NEWER
            bool hasProjectSettings = true;
#elif UNITY_2023_1_OR_NEWER
            bool hasProjectSettings = false;
#elif UNITY_2022_1_OR_NEWER
            bool hasProjectSettings = true;
#else
            bool hasProjectSettings = false;
#endif
            string prefProjString = "You must manually set this value in 'Preferences'" + (hasProjectSettings ? " AND 'Project Settings'" : ".");

            GUILayout.Label(prefProjString, guiStyles.InactiveLabel);

            GUILayout.Label("After changing the value(s), use the 'Recompile Shaders' button below.");

            //GUILayout.FlexibleSpace();

            GUILayout.Space(VERT_INDENT);
            
            //EditorGUI.BeginDisabledGroup(UpdateManager.IsShaderVariantLimitTooLow());
            if (GUILayout.Button(new GUIContent("Recompile Shaders", "After correcting the variant limit, use this button to force all the shaders to recompile - any characters built using a broken shader will need to be rebuilt."), GUILayout.Width(120f)))
            {
                ShaderPackageUtil.RecompileShaders();
            }
            //EditorGUI.EndDisabledGroup();

            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();

            //GUILayout.FlexibleSpace();

            GUILayout.Space(VERT_INDENT);

            if (hasProjectSettings)
            {
                if (GUILayout.Button("Open Project Settings"))
                {
#if UNITY_6000_0_OR_NEWER
                    string psPathString = "Project/Shader Graph";
#else
                    string psPathString = "Project/ShaderGraph";
#endif
                    SettingsService.OpenProjectSettings(psPathString);
                }
            }

            GUILayout.Space(12f);

            if (GUILayout.Button("Open Preferences"))
            {
                SettingsService.OpenUserPreferences("Preferences/Shader Graph");
            }
                        
            //GUILayout.FlexibleSpace();

            GUILayout.EndVertical();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical(); // current target build platform details
        }

        bool legacyShaderFoldout = false;
        private void LegacyShaderGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            string foldoutLabel = "Legacy Shaders (Optional Extra)";
            legacyShaderFoldout = EditorGUILayout.Foldout(legacyShaderFoldout, new GUIContent(foldoutLabel, "If characters have been built using older versions of this tool, then the shader packages here will enable their use."), true, guiStyles.FoldoutTitleLabel);

            GUILayout.FlexibleSpace();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            if (legacyShaderFoldout)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Space(HORIZ_INDENT);

                LegacyShaderInstallGUI();

                GUILayout.Space(HORIZ_INDENT);

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical();
        }

        private void LegacyShaderInstallGUI()
        {
            bool disabled = UpdateManager.currentLegacyPackageManifest == null;
            EditorGUI.BeginDisabledGroup(disabled);
            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();
            GUILayout.Space(HORIZ_INDENT);

            GUILayout.Label("If characters have been built using older versions of this tool, then the shader packages here will enable their continued use.", guiStyles.InactiveLabel);

            GUILayout.Space(HORIZ_INDENT);
            GUILayout.EndHorizontal();

            GUILayout.Space(VERT_INDENT);

            if (Pipeline.isURP)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(HORIZ_INDENT);

                GUIStyle urpNoteStyle = disabled ? guiStyles.InactiveHighlighLabel : guiStyles.InactiveLabel;

                GUILayout.Label("For the 'Universal Render Pipeline', the legacy shaders are ONLY compatible with URP version 17.1.0 and BELOW.", urpNoteStyle);

                GUILayout.Space(HORIZ_INDENT);
                GUILayout.EndHorizontal();

                GUILayout.Space(VERT_INDENT);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Space(HORIZ_INDENT);
            GUILayout.Label("Required Legacy Shader Version:  ", guiStyles.InactiveLabel);
            GUILayout.Label(new GUIContent(UpdateManager.activeLegacyPipelineVersion.ToString(), "The current active render pipeline requires the legacy version " + UpdateManager.activeLegacyPipelineVersion.ToString() + ""), guiStyles.ActiveLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Space(HORIZ_INDENT);
            GUILayout.EndHorizontal();

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();
            GUILayout.Space(HORIZ_INDENT);
            if (GUILayout.Button(iconInstallLegacyW, GUILayout.Width(50f), GUILayout.Height(50f)))
            {
                ShaderPackageUtil.GUIPerformLegacyShaderInstall();
            }
            GUILayout.BeginVertical();
            GUILayout.Space(19f);
            GUIStyle instNoteStyle = disabled ? guiStyles.InactiveHighlighLabel : guiStyles.InactiveLabel;
            string installLabel = disabled ? "No legacy shader is available for this pipeline." : "Install Legacy Shader for this pipeline.";
            GUILayout.Label(installLabel, instNoteStyle);
            GUILayout.EndVertical();
            GUILayout.Space(HORIZ_INDENT);
            GUILayout.EndHorizontal();

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical();
            EditorGUI.EndDisabledGroup();
        }

        private string GetShaderLabel()
        {
            string shaderVersion = UpdateManager.installedShaderPipelineVersion != ShaderPackageUtil.PipelineVersion.None ? (UpdateManager.installedShaderPipelineVersion.ToString() + " v" + UpdateManager.installedShaderVersion.ToString()) : "Not installed";
            return shaderVersion;
        }

        private string GetRuntimeLabel()
        {
            if (UpdateManager.installedRuntimeVersion == new Version(0, 0, 0))
                return "Not installed";
            else
                return " v" + UpdateManager.installedRuntimeVersion.ToString();
        }

        private void InstalledShaderFoldoutGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // all installed pipelines

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            bool error = UpdateManager.shaderPackageValid == ShaderPackageUtil.PackageVailidity.Invalid;
            if (error)
            {
                instShaderFoldout = true;
            }
            
            string foldoutLabel = "Current Shader Package: " + GetShaderLabel();
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

        private void InstalledRuntimeFoldoutGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // all installed pipelines

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            bool error = UpdateManager.runtimePackageValid == ShaderPackageUtil.PackageVailidity.Invalid;
            if (error)
            {
                instRuntimeFoldout = true;
            }

            string foldoutLabel = "Current Runtime Package: " + GetRuntimeLabel();
            instRuntimeFoldout = EditorGUILayout.Foldout(instRuntimeFoldout, new GUIContent(foldoutLabel, "Toggle foldout to see details of the available runtime packages."), true, error ? guiStyles.FoldoutTitleErrorLabel : guiStyles.FoldoutTitleLabel);

            GUILayout.FlexibleSpace();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            if (instRuntimeFoldout)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Space(HORIZ_INDENT);

                GUILayout.BeginVertical();
                InstalledRuntimePackageGUI();
                ValidateRuntimePackageGUI();
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

            GUIContent pipeLabel = new GUIContent("Currently Installed Render Pipeline:  " + UpdateManager.activePipeline.ToString() + (UpdateManager.activeVersion.Equals(new Version(emptyVersion)) ? "" : " version: " + UpdateManager.activeVersion.ToString()));
            GUILayout.Label(pipeLabel);

            GUILayout.FlexibleSpace();

            if (!string.IsNullOrEmpty(UpdateManager.activePackageString))
            {
                GUIContent buttonContent = new GUIContent("Open In Package Manager", "Open the package manager to check for updates");
                if (GUILayout.Button(buttonContent, GUILayout.Width(PACKAGE_UPDATE_W)))
                {
                    UnityEditor.PackageManager.UI.Window.Open(UpdateManager.activePackageString);
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

            GUIContent titleLabel = new GUIContent(UpdateManager.installedPipelines.Count > 1 ? "Currently Installed Render Pipelines:" : "Currently Installed Render Pipeline:", "");
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

            foreach (ShaderPackageUtil.InstalledPipelines pipe in UpdateManager.installedPipelines)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Space(HORIZ_INDENT * 3);

                string activeTip = "";
                string versionLabel = pipe.InstalledPipeline.ToString() + (pipe.Version.Equals(new Version(emptyVersion)) ? "" : " version: " + pipe.Version.ToString());
                GUIStyle pipLabelStyle = guiStyles.InactiveLabel;

                if (UpdateManager.activePipeline != ShaderPackageUtil.InstalledPipeline.None)
                {
                    if (pipe.InstalledPipeline == UpdateManager.activePipeline)
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
            GUILayout.Label(new GUIContent(UpdateManager.activePipelineVersion.ToString(), "The current active render pipeline requires the " + UpdateManager.activePipelineVersion.ToString() + ""), guiStyles.ActiveLabel);

            GUILayout.FlexibleSpace();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.Label("Currently Installed Shader Package:  ");
            GUIStyle shaderLabelStyle = GUI.skin.label;
            string shaderLabelTooltip = "";
            switch (UpdateManager.installedPackageStatus)
            {
                case ShaderPackageUtil.InstalledPackageStatus.Absent:
                    shaderLabelStyle = guiStyles.shMismatchLabel;
                    shaderLabelTooltip = "Not Installed";
                    break;
                case ShaderPackageUtil.InstalledPackageStatus.Mismatch:
                    shaderLabelStyle = guiStyles.shMismatchLabel;
                    shaderLabelTooltip = "Installed shaders are for a different pipeline.";
                    break;
                case ShaderPackageUtil.InstalledPackageStatus.Current:
                    shaderLabelStyle = guiStyles.shCurrentLabel;
                    shaderLabelTooltip = "Installed shaders match the current pipeline.";
                    break;
            }

            //string shaderLabel = UpdateManager.installedShaderPipelineVersion.ToString() + " v" + UpdateManager.installedShaderVersion.ToString();
            GUILayout.Label(new GUIContent(GetShaderLabel(), shaderLabelTooltip), shaderLabelStyle);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Check Status"))
            {
                UpdateGUI();
                ShaderPackageUtil.UpdaterWindowCheckStatus();
            }

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            GUILayout.Space(VERT_INDENT * 3);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            string result = string.Empty;
            result = UpdateManager.determinedShaderAction.ResultString + "(" + UpdateManager.installedPackageStatus + " :: " + UpdateManager.shaderPackageValid + ")";

            GUILayout.Label(result, guiStyles.WrappedInfoLabel);

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            GUILayout.EndVertical(); // current shader package
        }

        private void InstalledRuntimePackageGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // current shader package

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.Label("Currently Installed Runtime Package:  ");
            GUIStyle shaderLabelStyle = GUI.skin.label;            
            string version = string.Empty;

            switch (UpdateManager.installedRuntimeStatus)
            {
                case ShaderPackageUtil.InstalledPackageStatus.Absent:
                    shaderLabelStyle = guiStyles.shMismatchLabel;
                    version = "Not Installed";
                    break;
                case ShaderPackageUtil.InstalledPackageStatus.Current:
                    if (UpdateManager.currentRuntimePackageManifest != null)
                    {
                        version = "v" + UpdateManager.currentRuntimePackageManifest.Version;
                        shaderLabelStyle = guiStyles.shCurrentLabel;
                    }
                    else
                    {
                        version = "Unknown or not correctly installed";
                        shaderLabelStyle = guiStyles.shMismatchLabel;
                    }
                    break;
            }
            GUILayout.Label(version, shaderLabelStyle);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Check Status"))
            {
                UpdateGUI();
                ShaderPackageUtil.UpdaterWindowCheckStatus();
            }

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            GUILayout.Space(VERT_INDENT * 3);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            string result = string.Empty;
            if (UpdateManager.determinedRuntimeAction != null)
                result = UpdateManager.determinedRuntimeAction.ResultString + "(" + UpdateManager.installedRuntimeStatus + " :: " + UpdateManager.runtimePackageValid + ")";
            else result = "No action determined for the runtime package (" + UpdateManager.installedRuntimeStatus + " :: " + UpdateManager.runtimePackageValid + ")";

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

            if (UpdateManager.missingShaderPackageItems.Count > 0)
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

                if (UpdateManager.missingShaderPackageItems.Count < 6)
                    scrollHeight = lineHeight * UpdateManager.missingShaderPackageItems.Count + 27f;
                else
                    scrollHeight = 112f;

                scrollPosShaderPackage = GUILayout.BeginScrollView(scrollPosShaderPackage, GUILayout.Height(scrollHeight));
                foreach (ShaderPackageUtil.ShaderPackageItem item in UpdateManager.missingShaderPackageItems)
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

        Vector2 scrollPosRuntimePackage = new Vector2();
        private void ValidateRuntimePackageGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // validate shader package

            GUILayout.Space(VERT_INDENT);

            if (UpdateManager.missingRuntimePackageItems.Count > 0)
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

                if (UpdateManager.missingRuntimePackageItems.Count < 6)
                    scrollHeight = lineHeight * UpdateManager.missingRuntimePackageItems.Count + 27f;
                else
                    scrollHeight = 112f;

                scrollPosRuntimePackage = GUILayout.BeginScrollView(scrollPosRuntimePackage, GUILayout.Height(scrollHeight));
                foreach (ShaderPackageUtil.ShaderPackageItem item in UpdateManager.missingRuntimePackageItems)
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

            foreach (ShaderPackageUtil.ShaderPackageManifest manifest in UpdateManager.availablePackages)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Space(HORIZ_INDENT * 3);

                bool relevant = manifest.Pipeline == UpdateManager.activePipelineVersion;

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

        public bool shaderActionRequired = false;
        public bool runtimeActionRequired = false;
        bool actionToFollowFoldout = false;

        private void ActionToFollowFoldoutGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // all installed pipelines

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            if (shaderActionRequired || runtimeActionRequired)
                actionToFollowFoldout = true;

            string actionString = shaderActionRequired ? "Action required..." : "No action required...";
            string tooltipString = shaderActionRequired ? "Further user action is required..." : "No action required...";
            GUIStyle actionTitle = shaderActionRequired ? guiStyles.FoldoutTitleErrorLabel : guiStyles.FoldoutTitleLabel;
            actionToFollowFoldout = EditorGUILayout.Foldout(actionToFollowFoldout, new GUIContent(actionString, tooltipString), true, actionTitle);

            GUILayout.FlexibleSpace();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            if (actionToFollowFoldout)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Space(HORIZ_INDENT);

                ActionGUI();

                GUILayout.Space(HORIZ_INDENT);

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical();
        }

        private void ActionGUI()
        {
            float actionwidth = (Instance.position.width - (HORIZ_INDENT * 10)) / 2;

            GUILayout.BeginVertical(GUI.skin.box);
            
            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            ShaderActionGUI(actionwidth);

            GUILayout.Space(HORIZ_INDENT);

            RuntimeActionGUI(actionwidth);

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical();
        }

        private void ShaderActionGUI(float actionwidth)
        {
            GUILayout.BeginVertical(GUILayout.Width(actionwidth));// GUI.skin.box);
            bool incompatible = false;
            if (UpdateManager.determinedShaderAction != null)
            {
                ShaderPackageUtil.DeterminedAction action = UpdateManager.determinedShaderAction.DeterminedShaderAction;
                Texture2D picture = null;
                switch (action)
                {
                    case ShaderPackageUtil.DeterminedAction.None:
                        {
                            picture = iconInstallShaderY;
                            break;
                        }
                    case ShaderPackageUtil.DeterminedAction.CurrentValid:
                        {
                            picture = iconInstallShaderG;
                            break;
                        }
                    case ShaderPackageUtil.DeterminedAction.Error:
                        {
                            picture = iconInstallShaderR;
                            break;
                        }
                    case ShaderPackageUtil.DeterminedAction.UninstallReinstall_optional:
                        {
                            picture = iconInstallShaderY;
                            break;
                        }
                    case ShaderPackageUtil.DeterminedAction.UninstallReinstall_force:
                        {
                            picture = iconInstallShaderY;
                            break;
                        }
                    case ShaderPackageUtil.DeterminedAction.NothingInstalled_Install_force:
                        {
                            picture = iconInstallShaderY;
                            break;
                        }
                    case ShaderPackageUtil.DeterminedAction.Incompatible:
                        {
                            picture = iconUpgradePipelineY;
                            break;
                        }
                }
                GUILayout.BeginHorizontal();
                
                if (UpdateManager.determinedShaderAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.Incompatible) incompatible = true;
                if (GUILayout.Button(picture, GUILayout.Width(50f), GUILayout.Height(50f)))
                {
                    if (incompatible)
                    {
                        SettingsService.OpenProjectSettings("Project/Graphics");
                    }
                    else
                    {
                        ShaderPackageUtil.GUIPerformShaderAction(action);
                    }
                }
                GUILayout.BeginVertical();
                GUILayout.Space(6f);
                GUILayout.Label(UpdateManager.determinedShaderAction.ResultString, guiStyles.WrappedInfoLabel);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("No action properly determined");
            }

            GUILayout.BeginHorizontal();

            string infoString = string.Empty;

            if (incompatible)
            {
                infoString = "Click the above icon to change the graphics pipeline settings";
            }
            else
            {
                infoString = UpdateManager.determinedShaderAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.CurrentValid || UpdateManager.determinedShaderAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.None ? "No action required" : "Please click the above icon to update/fix the shader files installation";
            }

            EditorGUILayout.HelpBox(infoString, MessageType.Info, true);

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void RuntimeActionGUI(float actionwidth)
        {
            GUILayout.BeginVertical(GUILayout.Width(actionwidth));// GUI.skin.box);

            if (UpdateManager.determinedRuntimeAction != null)
            {
                ShaderPackageUtil.DeterminedAction action = UpdateManager.determinedRuntimeAction.DeterminedShaderAction;
                Texture2D picture = null;
                switch (action)
                {
                    case ShaderPackageUtil.DeterminedAction.None:
                        {
                            picture = iconInstallRuntimeY;
                            break;
                        }
                    case ShaderPackageUtil.DeterminedAction.CurrentValid:
                        {
                            picture = iconInstallRuntimeG;
                            break;
                        }
                    case ShaderPackageUtil.DeterminedAction.Error:
                        {
                            picture = iconInstallRuntimeR;
                            break;
                        }
                    case ShaderPackageUtil.DeterminedAction.UninstallReinstall_optional:
                        {
                            picture = iconInstallRuntimeY;
                            break;
                        }
                    case ShaderPackageUtil.DeterminedAction.UninstallReinstall_force:
                        {
                            picture = iconInstallRuntimeY;
                            break;
                        }
                    case ShaderPackageUtil.DeterminedAction.NothingInstalled_Install_force:
                        {
                            picture = iconInstallRuntimeY;
                            break;
                        }
                    case ShaderPackageUtil.DeterminedAction.Incompatible:
                        {
                            picture = iconUpgradePipelineY;
                            break;
                        }
                }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(picture, GUILayout.Width(50f), GUILayout.Height(50f)))
                {
                    ShaderPackageUtil.GUIPerformRuntimeAction(action);
                }
                GUILayout.BeginVertical();
                GUILayout.Space(6f);
                GUILayout.Label(UpdateManager.determinedRuntimeAction.ResultString, guiStyles.WrappedInfoLabel);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("No action properly determined");
            }

            GUILayout.BeginHorizontal();

            string infoString = UpdateManager.determinedRuntimeAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.CurrentValid || UpdateManager.determinedRuntimeAction.DeterminedShaderAction == ShaderPackageUtil.DeterminedAction.None ? "No action required" : "Please click the above icon to update/fix the runtime files installation";

            EditorGUILayout.HelpBox(infoString, MessageType.Info, true);

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void ShowOnStartupGUI()
        {
            if (currentSettings != null)  // avoids a null ref after assembly reload while waiting for frames
            {
                GUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();                
                GUILayout.Label("Show this window on startup ");
                currentSettings.showOnStartup = EditorGUILayout.Toggle(currentSettings.showOnStartup);
                if (EditorGUI.EndChangeCheck())
                {
                    ImporterWindow.SetGeneralSettings(currentSettings, true);
                }

                EditorGUI.BeginChangeCheck();
                GUILayout.Label(new GUIContent("Check for updates ", "Toggling this on will check for a software update every 24hrs.  If disabled a manual check for updates cn be perfomed with the check for updates button in the 'Current Software Version' foldout (use the menu path 'Reallusion -> Processing Tools -> Shader Package Updater' top re-open the window)"));
                currentSettings.checkForUpdates = EditorGUILayout.Toggle(currentSettings.checkForUpdates);
                if (EditorGUI.EndChangeCheck())
                {
                    ImporterWindow.SetGeneralSettings(currentSettings, true);
                }

                EditorGUI.BeginChangeCheck();
                GUILayout.Label(new GUIContent("Ignore all ", "Toggling this on will prevent to window from being automatically shown even if errors are detected.  This window can still be shown using the menu option 'Reallusion -> Processing Tools -> Shader Package Updater'"));
                currentSettings.ignoreAllErrors = EditorGUILayout.Toggle(currentSettings.ignoreAllErrors);
                if (EditorGUI.EndChangeCheck())
                {
                    ImporterWindow.SetGeneralSettings(currentSettings, true);
                }
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
                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_P4_DeletedLocal").image, "UnInstall"), GUILayout.Width(24f)))
                {
                    ShaderPackageUtil.UnInstallShaderPackage();
                    UpdateGUI();
                }
                string shaderLabel = UpdateManager.installedShaderPipelineVersion.ToString() + " v" + UpdateManager.installedShaderVersion.ToString();
                GUILayout.Label(shaderLabel, EditorStyles.largeLabel);
                GUILayout.EndHorizontal();

                if (UpdateManager.availablePackages != null)
                {
                    if (UpdateManager.availablePackages.Count == 0) return;
                }
                else
                {
                    return;
                }

                GUILayout.Label("Available Distribution Packages:", EditorStyles.largeLabel);
                foreach (ShaderPackageUtil.ShaderPackageManifest manifest in UpdateManager.availablePackages)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_P4_AddedRemote").image, "Install " + Path.GetFileNameWithoutExtension(manifest.SourcePackageName)), GUILayout.Width(24f)))
                    {
                        Debug.Log("Installing Package: " + manifest.SourcePackageName);
                        ShaderPackageUtil.InstallShaderPackage(manifest);
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

        public string PipelineVersionString(bool title = false)
        {
            if (title)
                return UpdateManager.activePipeline.ToString() + (UpdateManager.activeVersion.Equals(new Version(emptyVersion)) ? "" : " v" + UpdateManager.activeVersion.ToString());
            else
                return UpdateManager.activePipeline.ToString() + (UpdateManager.activeVersion.Equals(new Version(emptyVersion)) ? "" : " version: " + UpdateManager.activeVersion.ToString());
        }

        public string GetPlatformRestrictionText()
        {
            string noPlatformMessage = string.Empty;
            string urpPlatformMessage = "Defaulting to the UPR12 shader set.  There are some incompatabilities between WebGL and the shaders for URP versions higher than 12.";

            switch (UpdateManager.platformRestriction)
            {
                case ShaderPackageUtil.PlatformRestriction.None:
                    {
                        return noPlatformMessage;
                    }
                case ShaderPackageUtil.PlatformRestriction.URPWebGL:
                    {
                        return urpPlatformMessage;
                    }
            }
            return string.Empty;
        }

        #endregion UTIL
    }
}