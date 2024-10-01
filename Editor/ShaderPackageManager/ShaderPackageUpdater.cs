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

        [MenuItem("Reallusion/Processing Tools/Shader Package Updater", priority = 800)]
        public static void CreateWindow()
        {
            Debug.Log("ShaderPackageUpdater.CreateWindow()");
            if (!EditorWindow.HasOpenInstances<ShaderPackageUpdater>())
                Instance = OpenWindow();
        }

        [MenuItem("Reallusion/Processing Tools/Shader Package Updater", true)]
        public static bool ValidateWindow()
        {
            return !EditorWindow.HasOpenInstances<ShaderPackageUpdater>() && ImporterWindow.Current != null;
        }

        public static ShaderPackageUpdater OpenWindow()
        {
            ShaderPackageUpdater window = ScriptableObject.CreateInstance<ShaderPackageUpdater>();
            if (showUtility)
                window.ShowUtility();
            else
                window.Show();
            window.minSize = new Vector2(600f, 300f);

            return window;
        }

        private void OnEnable()
        {
            Debug.Log("ShaderPackageUpdater.OnEnable");
            Debug.Log(WindowManager.activePipelineVersion);
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
            Debug.Log("ShaderPackageUpdater.OnDestroy");
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
        }

        private void OnDisable()
        {
            Debug.Log("ShaderPackageUpdater.OnDisable");
        }

        public void OnBeforeAssemblyReload()
        {
            Debug.Log("ShaderPackageUpdater.OnBeforeAssemblyReload");
        }

        public void OnAfterAssemblyReload()
        {
            Debug.Log("ShaderPackageUpdater.OnAfterAssemblyReload");
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
        private const string titleString = "Shader Package Updater";
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
            public GUIStyle ActiveLabel;
            public GUIStyle VersionLabel;
            public GUIStyle shCurrentLabel;
            public GUIStyle shUpgradeableLabel;
            public GUIStyle shTooHighLabel;
            public GUIStyle shMismatchLabel;
            public GUIStyle FoldoutTitleLabel;
            public GUIStyle FoldoutTitleErrorLabel;
            public GUIStyle WrappedInfoLabel;
            public GUIStyle WrappedInfoLabelColor;

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
                FoldoutTitleLabel.fontStyle = FontStyle.BoldAndItalic;

                FoldoutTitleErrorLabel = new GUIStyle(EditorStyles.foldout);
                FoldoutTitleErrorLabel.onNormal.textColor = colYellow;
                FoldoutTitleErrorLabel.fontSize = 14;
                FoldoutTitleErrorLabel.fontStyle = FontStyle.BoldAndItalic;

                WrappedInfoLabel = new GUIStyle(GUI.skin.label);
                WrappedInfoLabel.wordWrap = true;

                WrappedInfoLabelColor = new GUIStyle(GUI.skin.label);
                WrappedInfoLabelColor.wordWrap = true;
                WrappedInfoLabelColor.fontSize = 13;
                WrappedInfoLabelColor.normal.textColor = colYellow;
                WrappedInfoLabelColor.hover.textColor = colYellow;
            }
        }

        bool initGUI = true;
        private Texture2D iconInstallShaderG;
        private Texture2D iconInstallShaderY;
        private Texture2D iconInstallShaderR;
        public void InitGUI()
        {
            string[] folders = new string[] { "Assets", "Packages" };
            iconInstallShaderG = Util.FindTexture(folders, "RLIcon_Install_Shader_G");
            iconInstallShaderY = Util.FindTexture(folders, "RLIcon_Install_Shader_Y");
            iconInstallShaderR = Util.FindTexture(folders, "RLIcon_Install_Shader_R");
            initGUI = false;
        }

        public void UpdateGUI()
        {
            currentTarget = EditorUserBuildSettings.activeBuildTarget;
            ShaderPackageUtil.ShaderPackageUtilInit();
        }

        private void OnGUI()
        {
            if (initGUI) InitGUI();

            if (guiStyles == null)
                guiStyles = new Styles();

            if (currentSettings == null) currentSettings = ImporterWindow.GeneralSettings;

            // insulation against undetermined pipeline and packages
            if (WindowManager.shaderPackageValid == ShaderPackageUtil.PackageVailidity.Waiting) return;

            if (WindowManager.shaderPackageValid == ShaderPackageUtil.PackageVailidity.None)
            {
                //UpdateGUI();

                return;
            }

            //if (WindowManager.determinedAction == null)
            //{
            //    ShaderPackageUtil.DetermineAction();
            //}

            titleContent = new GUIContent(titleString + " - " + PipelineVersionString(true));

            GUILayout.BeginVertical(); // whole window contents

            GUILayout.Space(SECTION_SPACER);

            CurrentSoftwareVersionFoldoutGUI();

            GUILayout.Space(SECTION_SPACER);

            AllInstalledPipelinesFoldoutGUI();

            GUILayout.Space(SECTION_SPACER);

            CurrentBuildPlatformGUI();

            GUILayout.Space(SECTION_SPACER);

            InstalledShaderFoldoutGUI();

            GUILayout.Space(SECTION_SPACER);

            //if (WindowManager.installedPackageStatus != ShaderPackageUtil.InstalledPackageStatus.Current)
            //    actionRequired = true;
            //else
            //    actionRequired = false;

            actionToFollowFoldoutGUI();

            GUILayout.FlexibleSpace();

            // test functions
            FoldoutTestSection();
            GUILayout.Space(SECTION_SPACER);
            // test functions ends 

            ShowOnStartupGUI();

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

        bool currentSoftwareVersionFoldout = false;
        private void CurrentSoftwareVersionFoldoutGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // all installed pipelines

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            currentSoftwareVersionFoldout = EditorGUILayout.Foldout(currentSoftwareVersionFoldout, new GUIContent("Current Software Version", "Tooltip"), true, guiStyles.FoldoutTitleLabel);

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
        Rect prev = new Rect();

        private void CurrentSoftwareVersionGUI()
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Find Things"))
            {
                Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly a in assemblies)
                {
                    if (a.FullName.iContains("soupday"))
                    {
                        System.Type[] types = a.GetTypes();
                        foreach (System.Type t in types)
                        {
                            if (t.FullName.iContains("wrink"))
                                Debug.Log(a.FullName + " --- " + t.FullName);
                        }
                    }
                }
            }

            GUILayout.EndHorizontal();


            GUILayout.BeginHorizontal();
            
            GUILayout.FlexibleSpace();

            GUILayout.Space(HORIZ_INDENT);

            if (Event.current.type == EventType.Repaint)
                prev = GUILayoutUtility.GetLastRect();

            EditorGUI.BeginDisabledGroup(RLToolUpdateUtil.fullJsonFragment == null);

            if (EditorGUILayout.DropdownButton(
                content: new GUIContent("Previous Releases", "Show all previous releases on github."),
                focusType: FocusType.Passive,
                options: GUILayout.Width(DROPDOWN_BTN_WIDTH)))
            {
                RLToolUpdateWindow.ShowAtPosition(new Rect(prev.x - RLToolUpdateWindow.DROPDOWN_WIDTH + DROPDOWN_BTN_WIDTH + 3 * HORIZ_INDENT, prev.y + 20f, prev.width, prev.height));
            }

            EditorGUI.EndDisabledGroup();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

        }



        bool allInstPipeFoldout = false;
        private void AllInstalledPipelinesFoldoutGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // all installed pipelines

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            string foldoutLabel = "Current Render Pipeline: " + WindowManager.activePipeline.ToString() + (WindowManager.activeVersion.Equals(new Version(emptyVersion)) ? "" : " version: " + WindowManager.activeVersion.ToString());
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

            if (WindowManager.platformRestriction != ShaderPackageUtil.PlatformRestriction.None)
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

            if (WindowManager.platformRestriction != ShaderPackageUtil.PlatformRestriction.None)
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

        bool instShaderFoldout = false;

        private string GetShaderLabel()
        {
            string shaderVersion = WindowManager.installedShaderPipelineVersion != ShaderPackageUtil.PipelineVersion.None ? (" v" + WindowManager.installedShaderVersion.ToString()) : "";
            return WindowManager.installedShaderPipelineVersion.ToString() + shaderVersion;
        }

        private void InstalledShaderFoldoutGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // all installed pipelines

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            bool error = WindowManager.shaderPackageValid == ShaderPackageUtil.PackageVailidity.Invalid;
            if (error)
            {
                instShaderFoldout = true;
            }
            //string shaderVersion = WindowManager.installedShaderPipelineVersion != ShaderPackageUtil.PipelineVersion.None ?  (" v" + WindowManager.installedShaderVersion.ToString()) : "";
            //string shaderLabel = WindowManager.installedShaderPipelineVersion.ToString() + shaderVersion;
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

        private void InstalledPipelineGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box); // current pipeline

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            GUIContent pipeLabel = new GUIContent("Currently Installed Render Pipeline:  " + WindowManager.activePipeline.ToString() + (WindowManager.activeVersion.Equals(new Version(emptyVersion)) ? "" : " version: " + WindowManager.activeVersion.ToString()));
            GUILayout.Label(pipeLabel);

            GUILayout.FlexibleSpace();

            if (!string.IsNullOrEmpty(WindowManager.activePackageString))
            {
                GUIContent buttonContent = new GUIContent("Open In Package Manager", "Open the package manager to check for updates");
                if (GUILayout.Button(buttonContent, GUILayout.Width(PACKAGE_UPDATE_W)))
                {
                    UnityEditor.PackageManager.UI.Window.Open(WindowManager.activePackageString);
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

            GUIContent titleLabel = new GUIContent(WindowManager.installedPipelines.Count > 1 ? "Currently Installed Render Pipelines:" : "Currently Installed Render Pipeline:", "");
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

            foreach (ShaderPackageUtil.InstalledPipelines pipe in WindowManager.installedPipelines)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Space(HORIZ_INDENT * 3);

                string activeTip = "";
                string versionLabel = pipe.InstalledPipeline.ToString() + (pipe.Version.Equals(new Version(emptyVersion)) ? "" : " version: " + pipe.Version.ToString());
                GUIStyle pipLabelStyle = guiStyles.InactiveLabel;

                if (WindowManager.activePipeline != ShaderPackageUtil.InstalledPipeline.None)
                {
                    if (pipe.InstalledPipeline == WindowManager.activePipeline)
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
            GUILayout.Label(new GUIContent(WindowManager.activePipelineVersion.ToString(), "The current active render pipeline requires the " + WindowManager.activePipelineVersion.ToString() + ""), guiStyles.ActiveLabel);

            GUILayout.FlexibleSpace();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.Label("Currently Installed Shader Package:  ");
            GUIStyle shaderLabelStyle = GUI.skin.label;
            string shaderLabelTooltip = "";
            switch (WindowManager.installedPackageStatus)
            {
                case ShaderPackageUtil.InstalledPackageStatus.Mismatch:
                    shaderLabelStyle = guiStyles.shMismatchLabel;
                    shaderLabelTooltip = "Installed shaders are for a different pipeline.";
                    break;
                case ShaderPackageUtil.InstalledPackageStatus.Current:
                    shaderLabelStyle = guiStyles.shCurrentLabel;
                    shaderLabelTooltip = "Installed shaders match the current pipeline.";
                    break;
            }

            //string shaderLabel = WindowManager.installedShaderPipelineVersion.ToString() + " v" + WindowManager.installedShaderVersion.ToString();
            GUILayout.Label(new GUIContent(GetShaderLabel(), shaderLabelTooltip), shaderLabelStyle);

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

            //ShaderPackageUtil.DetermineAction(out string result);
            string result = string.Empty;
            //if (WindowManager.determinedAction == null)
            //{
            //    ShaderPackageUtil.DetermineAction();
            //    result = "NO RULE FOR :: " + WindowManager.installedPackageStatus + " :: " + WindowManager.shaderPackageValid;
            //}            
            //else
            //{
                result = WindowManager.determinedAction.ResultString + "(" + WindowManager.installedPackageStatus + " :: " + WindowManager.shaderPackageValid + ")";
            //}

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

            if (WindowManager.missingShaderPackageItems.Count > 0)
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

                if (WindowManager.missingShaderPackageItems.Count < 6)
                    scrollHeight = lineHeight * WindowManager.missingShaderPackageItems.Count + 27f;
                else
                    scrollHeight = 112f;

                scrollPosShaderPackage = GUILayout.BeginScrollView(scrollPosShaderPackage, GUILayout.Height(scrollHeight));
                foreach (ShaderPackageUtil.ShaderPackageItem item in WindowManager.missingShaderPackageItems)
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

            foreach (ShaderPackageUtil.ShaderPackageManifest manifest in WindowManager.availablePackages)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Space(HORIZ_INDENT * 3);

                bool relevant = manifest.Pipeline == WindowManager.activePipelineVersion;

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

        public bool actionRequired = false;
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

            //GUILayout.Label("Content...");
            if (WindowManager.determinedAction != null)
            {
                ShaderPackageUtil.DeterminedAction action = WindowManager.determinedAction.DeterminedAction;
                Texture2D picture = null;
                switch (action)
                {
                    case ShaderPackageUtil.DeterminedAction.None:
                        {
                            picture = iconInstallShaderY;
                            break;
                        }
                    case ShaderPackageUtil.DeterminedAction.currentValid:
                        {
                            picture = iconInstallShaderG;
                            break;
                        }
                    case ShaderPackageUtil.DeterminedAction.Error:
                        {
                            picture = iconInstallShaderR;
                            break;
                        }
                    case ShaderPackageUtil.DeterminedAction.uninstallReinstall_optional:
                        {
                            picture = iconInstallShaderY;
                            break;
                        }
                    case ShaderPackageUtil.DeterminedAction.uninstallReinstall_force:
                        {
                            picture = iconInstallShaderY;
                            break;
                        }
                }
                GUILayout.Button(picture, GUILayout.Width(100f), GUILayout.Height(100f));
            }            
            GUILayout.FlexibleSpace();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical();
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
                    ImporterWindow.SetGeneralSettings(currentSettings, true);
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
                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_P4_DeletedLocal").image, "UnInstall"), GUILayout.Width(24f)))
                {
                    ShaderPackageUtil.UnInstallPackage();
                    UpdateGUI();
                }
                string shaderLabel = WindowManager.installedShaderPipelineVersion.ToString() + " v" + WindowManager.installedShaderVersion.ToString();
                GUILayout.Label(shaderLabel, EditorStyles.largeLabel);
                GUILayout.EndHorizontal();

                if (WindowManager.availablePackages != null)
                {
                    if (WindowManager.availablePackages.Count == 0) return;
                }
                else
                {
                    return;
                }

                GUILayout.Label("Available Distribution Packages:", EditorStyles.largeLabel);
                foreach (ShaderPackageUtil.ShaderPackageManifest manifest in WindowManager.availablePackages)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_P4_AddedRemote").image, "Install " + Path.GetFileNameWithoutExtension(manifest.SourcePackageName)), GUILayout.Width(24f)))
                    {
                        Debug.Log("Installing Package: " + manifest.SourcePackageName);
                        ShaderPackageUtil.InstallPackage(manifest);
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
                return WindowManager.activePipeline.ToString() + (WindowManager.activeVersion.Equals(new Version(emptyVersion)) ? "" : " v" + WindowManager.activeVersion.ToString());
            else
                return WindowManager.activePipeline.ToString() + (WindowManager.activeVersion.Equals(new Version(emptyVersion)) ? "" : " version: " + WindowManager.activeVersion.ToString());
        }

        public string GetPlatformRestrictionText()
        {
            string noPlatformMessage = string.Empty;
            string urpPlatformMessage = "Defaulting to the UPR12 shader set.  There are some incompatabilities between WebGL and the shaders for URP versions higher than 12.";

            switch (WindowManager.platformRestriction)
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