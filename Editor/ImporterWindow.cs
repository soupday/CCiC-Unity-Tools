/* 
 * Copyright (C) 2021 Victor Soupday
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

using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;
using System.Linq;

namespace Reallusion.Import
{
    [Serializable]
    public class ImporterWindow : EditorWindow
    {
        // Settings
        [SerializeField]
        public static RLSettingsObject generalSettings;

        public static RLSettingsObject GeneralSettings
        {
            get { return generalSettings; }
        }
        public static void SetGeneralSettings(RLSettingsObject settingsObj, bool save)
        {
            generalSettings = settingsObj;
            if (save) RLSettings.SaveRLSettingsObject(generalSettings);
        }

        private static void SaveSettings()
        {
            RLSettings.SaveRLSettingsObject(generalSettings);
        }

        // Settings end

        [SerializeField]
        private static bool sceneFocus = false;

        public static bool isSceneFocus { get { return sceneFocus; } }
        public static void SetSceneFocus(bool val)
        {
            sceneFocus = val;
        }

        public enum Mode { none, single, multi }

        private static readonly string windowTitle = "CC/iC Importer " + Pipeline.FULL_VERSION;
        private static CharacterInfo contextCharacter;
        public static List<CharacterInfo> CharacterList { get; set; }
        private static bool showProps = true;
        private static string backScenePath;
        private static Mode mode;
        public static ImporterWindow Current { get; private set; }
        public CharacterInfo Character { get { return contextCharacter; } }        

        private Vector2 iconScrollView;
        private bool previewCharacterAfterGUI;
        private bool refreshAfterGUI;
        private bool buildAfterGUI;
        private bool bakeAfterGUI;
        private bool bakeHairAfterGUI;
        private bool processAnimationsAfterGUI;
        private bool restoreHairAfterGUI;
        private bool physicsAfterGUI;
        public enum ImporterWindowMode { Build, Bake, Settings }
        private ImporterWindowMode windowMode = ImporterWindowMode.Build;

        const float ICON_SIZE = 64f;
        const float WINDOW_MARGIN = 4f;
        const float TOP_PADDING = 6f; //16f;
        const float ACTION_BUTTON_SIZE = 40f;
        const float WEE_BUTTON_SIZE = 32f;
        const float ACTION_BUTTON_SPACE = 4f;
        const float BUTTON_HEIGHT = 40f;
        const float INFO_HEIGHT = 80f;
        const float OPTION_HEIGHT = 170f;
        const float ACTION_HEIGHT = 76f;
        const float ICON_WIDTH = 100f; // re-purposed below for draggable width icon area
        const float ACTION_WIDTH = ACTION_BUTTON_SIZE + 12f;
        const float TITLE_SPACE = 12f;
        const float ROW_SPACE = 4f;
        const float MIN_SETTING_WIDTH = ACTION_WIDTH;

        // additions for draggable width icon area
        const float DRAG_BAR_WIDTH = 2f;
        const float DRAG_HANDLE_PADDING = 4f;
        const float ICON_WIDTH_MIN = 100f;
        const float ICON_WIDTH_DETAIL = 140f;
        const float ICON_SIZE_SMALL = 25f;
        const float ICON_DETAIL_MARGIN = 2f;
        private float CURRENT_INFO_WIDTH = 0f;
        const float INFO_WIDTH_MIN = 0f;
        private bool dragging = false;
        private bool repaintDelegated = false;

        private Styles importerStyles;
        
        private Texture2D iconUnprocessed;
        private Texture2D iconBlenderUnprocessed;
        private Texture2D iconBasic;
        private Texture2D iconLinkedBasic;
        private Texture2D iconBlenderBasic;
        private Texture2D iconHQ;
        private Texture2D iconLinkedHQ;
        private Texture2D iconBlenderHQ;
        private Texture2D iconBaked;
        private Texture2D iconLinkedBaked;
        private Texture2D iconBlenderBaked;
        private Texture2D iconMixed;
        private Texture2D iconLinkedMixed;
        private Texture2D iconBlenderMixed;
        private Texture2D iconActionBake;
        private Texture2D iconActionBakeOn;
        private Texture2D iconActionBakeHair;
        private Texture2D iconActionBakeHairOn;
        private Texture2D iconActionPreview;
        private Texture2D iconActionPreviewOn;
        private Texture2D iconActionRefresh;
        private Texture2D iconActionAnims;
        private Texture2D iconActionPhysics;
        private Texture2D iconActionLOD;
        private Texture2D iconAction2Pass;
        private Texture2D iconAlembic;
        private Texture2D iconActionAnimPlayer;
        private Texture2D iconActionAnimPlayerOn;
        private Texture2D iconActionAvatarAlign;
        private Texture2D iconActionAvatarAlignOn;
        private Texture2D iconSettings;
        private Texture2D iconSettingsOn;
        private Texture2D iconLighting;
        private Texture2D iconCamera;
        private Texture2D iconBuildMaterials;
        private Texture2D iconProp;
        private Texture2D iconLinkedProp;
        private Texture2D iconBlenderProp;
        private Texture2D iconPropG;
        private Texture2D iconLinkedPropG;
        private Texture2D iconBlenderPropG;

        // SerializeField is used to ensure the view state is written to the window 
        // layout file. This means that the state survives restarting Unity as long as the window
        // is not closed. If the attribute is omitted then the state is still serialized/deserialized.
#if UNITY_6000_2_OR_NEWER
        [SerializeField] TreeViewState<int> treeViewState;
#else
        [SerializeField] TreeViewState treeViewState;
#endif

        //The TreeView is not serializable, so it should be reconstructed from the tree data.
        CharacterTreeView characterTreeView;

        private bool magicaCloth2Available;
        public bool MagicaCloth2Available { get { return magicaCloth2Available; } }

        private bool dynamicBoneAvailable;
        public bool DynamicBoneAvailable { get { return dynamicBoneAvailable; } }

        public static float ICON_AREA_WIDTH
        {
            get
            {
                if (EditorPrefs.HasKey("RL_Importer_IconAreaWidth"))
                    return EditorPrefs.GetFloat("RL_Importer_IconAreaWidth");
                return ICON_WIDTH;
            }

            set
            {
                EditorPrefs.SetFloat("RL_Importer_IconAreaWidth", value);
            }
        }

        public static bool SELECT_LINKED
        {
            get
            {
                if (EditorPrefs.HasKey("RL_Importer_SelectLinked"))
                    return EditorPrefs.GetBool("RL_Importer_SelectLinked");
                return true;
            }

            set
            {
                EditorPrefs.SetBool("RL_Importer_SelectLinked", value);
            }
        }

        public static void StoreBackScene()
        {
            Scene currentScene = SceneManager.GetActiveScene();
            if (currentScene.IsValid() && !string.IsNullOrEmpty(currentScene.path))
            {
                backScenePath = currentScene.path;
            }
        }

        public static void GoBackScene()
        {
            if (!string.IsNullOrEmpty(backScenePath) && File.Exists(backScenePath))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                Scene backScene = EditorSceneManager.OpenScene(backScenePath);
                if (backScene.IsValid())
                    backScenePath = null;
            }
        }

        private void SetContextCharacter(UnityEngine.Object obj)
        {
            SetContextCharacter(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj)));
        }

        private void SetContextCharacter(string guid)
        {
            CharacterInfo oldCharacter = contextCharacter;

            if (contextCharacter == null || contextCharacter.guid != guid)
            {
                if (contextCharacter != null) contextCharacter.Release();
                contextCharacter = GetCharacterState(guid);
                contextCharacter.CheckGeneration();
                CreateTreeView(oldCharacter != contextCharacter);

                if (Pipeline.isHDRP && contextCharacter.BuiltDualMaterialHair) characterTreeView.EnableMultiPass();
                else characterTreeView.DisableMultiPass();

                EditorPrefs.SetString("RL_Importer_Context_GUID", contextCharacter.guid);
            }
        }

        public static ImporterWindow Init(Mode windowMode, UnityEngine.Object characterObject)
        {
            Type hwt = Type.GetType("UnityEditor.SceneHierarchyWindow, UnityEditor.dll");
            ImporterWindow window = GetWindow<ImporterWindow>(windowTitle, hwt);
            window.minSize = new Vector2(ACTION_WIDTH + ICON_WIDTH + MIN_SETTING_WIDTH + WINDOW_MARGIN, 500f);
            Current = window;

            ClearAllData();
            window.SetActiveCharacter(characterObject, windowMode);
            window.InitData();
            window.Show();

            return window;
        }

        public void SetActiveCharacter(UnityEngine.Object obj, Mode mode)
        {
            if (Util.IsCC3Character(obj))
            {
                EditorPrefs.SetString("RL_Importer_Context_GUID", AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj)));
            }

            ImporterWindow.mode = mode;
            EditorPrefs.SetString("RL_Importer_Mode", mode.ToString());
        }

        public static void RemoteInit()
        {
            Debug.LogWarning("**** DOING REMOTEINIT ***");
            ImporterWindow window;
            if (!EditorWindow.HasOpenInstances<EditorWindow>())
            {
                window = ScriptableObject.CreateInstance<ImporterWindow>();
            }
            else
            {                
                window = EditorWindow.GetWindow<ImporterWindow>();
            }
            window.name = windowTitle;
            Current = window;

            ClearAllData();
            window.SetActiveCharacter(Selection.activeObject, Mode.multi);
            window.InitData();
        }

        private void InitData()
        {
            MonitorConnection();
            SetGeneralSettings(RLSettings.FindRLSettingsObject(), false);
            UpdateManager.TryPerformUpdateChecks();
            CheckAvailableAddons();

            string[] folders = new string[] { "Assets", "Packages" };
            iconUnprocessed = Util.FindTexture(folders, "RLIcon_UnprocessedChar");
            iconBlenderUnprocessed = Util.FindTexture(folders, "RLICon_Blender_UnprocessedChar");            
            iconBasic = Util.FindTexture(folders, "RLIcon_BasicChar");
            iconLinkedBasic = Util.FindTexture(folders, "RLIcon_Linked_BasicChar");
            iconBlenderBasic = Util.FindTexture(folders, "RLIcon_Blender_BasicChar");
            iconHQ = Util.FindTexture(folders, "RLIcon_HQChar");
            iconLinkedHQ = Util.FindTexture(folders, "RLIcon_Linked_HQChar");
            iconBlenderHQ = Util.FindTexture(folders, "RLIcon_Blender_HQChar");
            iconBaked = Util.FindTexture(folders, "RLIcon_BakedChar");
            iconLinkedBaked = Util.FindTexture(folders, "RLIcon_Linked_BakedChar");
            iconBlenderBaked = Util.FindTexture(folders, "RLIcon_Blender_BakedChar");
            iconMixed = Util.FindTexture(folders, "RLIcon_MixedChar");
            iconLinkedMixed = Util.FindTexture(folders, "RLIcon_Linked_MixedChar");
            iconBlenderMixed = Util.FindTexture(folders, "RLIcon_Blender_MixedChar");
            iconActionBake = Util.FindTexture(folders, "RLIcon_ActionBake");
            iconActionBakeOn = Util.FindTexture(folders, "RLIcon_ActionBake_Sel");
            iconActionBakeHair = Util.FindTexture(folders, "RLIcon_ActionBakeHair");
            iconActionBakeHairOn = Util.FindTexture(folders, "RLIcon_ActionBakeHair_Sel");
            iconActionPreview = Util.FindTexture(folders, "RLIcon_ActionPreview");
            iconActionPreviewOn = Util.FindTexture(folders, "RLIcon_ActionPreview_Sel");
            iconActionRefresh = Util.FindTexture(folders, "RLIcon_ActionRefresh");
            iconAction2Pass = Util.FindTexture(folders, "RLIcon_Action2Pass");
            iconAlembic = Util.FindTexture(folders, "RLIcon_Alembic");
            iconActionAnims = Util.FindTexture(folders, "RLIcon_ActionAnims");
            iconActionPhysics = Util.FindTexture(folders, "RLIcon_ActionPhysics");
            iconActionLOD = Util.FindTexture(folders, "RLIcon_ActionLOD");
            iconActionAnimPlayer = Util.FindTexture(folders, "RLIcon_AnimPlayer");
            iconActionAvatarAlign = Util.FindTexture(folders, "RLIcon_AvatarAlign");
            iconActionAnimPlayerOn = Util.FindTexture(folders, "RLIcon_AnimPlayer_Sel");
            iconActionAvatarAlignOn = Util.FindTexture(folders, "RLIcon_AvatarAlign_Sel");
            iconSettings = Util.FindTexture(folders, "RLIcon_Settings");
            iconSettingsOn = Util.FindTexture(folders, "RLIcon_Settings_Sel");
            iconLighting = Util.FindTexture(folders, "RLIcon_Lighting");
            iconCamera = Util.FindTexture(folders, "RLIcon_Camera");
            iconBuildMaterials = Util.FindTexture(folders, "RLIcon_ActionBuildMaterials");
            iconProp = Util.FindTexture(folders, "RLIcon-Prop_W");
            iconLinkedProp = Util.FindTexture(folders, "RLIcon_Linked_Prop_W");
            iconBlenderProp = Util.FindTexture(folders, "RLIcon_Blender_Prop_W");
            iconPropG = Util.FindTexture(folders, "RLIcon-Prop_G");
            iconLinkedPropG = Util.FindTexture(folders, "RLIcon_Linked_Prop_G");
            iconBlenderPropG = Util.FindTexture(folders, "RLIcon_Blender_Prop_G");
            mode = Mode.multi;

            Current = this;

            showProps = generalSettings.showProps;
            RefreshCharacterList();

            if (titleContent.text != windowTitle) titleContent.text = windowTitle;
        }

        /*
        public static void InitShaderUpdater()
        {
            if (Application.isPlaying)
            {
                if (EditorWindow.HasOpenInstances<ShaderPackageUpdater>())
                {
                    EditorWindow.GetWindow<ShaderPackageUpdater>().Close();
                }
            }
            else
            {                
                ShaderPackageUtil.GetInstalledPipelineVersion();
                FrameTimer.CreateTimer(10, FrameTimer.initShaderUpdater, ShaderPackageUtil.ImporterWindowInitCallback);
            }
        }

        public static void InitSoftwareUpdateCheck()
        {
            if (Application.isPlaying)
            {                
                if (EditorWindow.HasOpenInstances<RLToolUpdateWindow>())
                {
                    EditorWindow.GetWindow<RLToolUpdateWindow>().Close();
                }
            }
            else
            {
                RLToolUpdateUtil.InitUpdateCheck();
            }
        }

        public static void OnHttpVersionChecked(object sender, EventArgs e)
        {

            RLToolUpdateUtil.HttpVersionChecked -= OnHttpVersionChecked;
        }
        */

        private void PreviewCharacter()
        {
            StoreBackScene();

            PreviewScene ps = WindowManager.OpenPreviewScene(contextCharacter.Fbx);

            if (WindowManager.showPlayer)
                WindowManager.ShowAnimationPlayer();

            ResetAllSceneViewCamera();

            // lighting doesn't update correctly when first previewing a scene in HDRP
            EditorApplication.delayCall += ForceUpdateLighting;
        }

        public void RefreshCharacterList()
        {
            WindowManager.UpdateImportList();

            string guidFilter = null;
            if (mode == Mode.single) guidFilter = EditorPrefs.GetString("RL_Importer_Context_GUID");

            CharacterList = WindowManager.GetCharacterList(true, showProps, null, guidFilter);     
        }

        private void RestoreData()
        {
            if (CharacterList == null)
            {
                InitData();
            }
        }

        private void RestoreSelection()
        {
            if (contextCharacter == null && CharacterList.Count > 0)
            {
                string editorPrefsContextPath = EditorPrefs.GetString("RL_Importer_Context_GUID");
                if (!string.IsNullOrEmpty(editorPrefsContextPath))
                {
                    for (int i = 0; i < CharacterList.Count; i++)
                    {
                        if (CharacterList[i].path == editorPrefsContextPath)
                            SetContextCharacter(CharacterList[i].guid);
                    }
                }

                if (Selection.activeGameObject)
                {
                    string selectionPath = AssetDatabase.GetAssetPath(Selection.activeGameObject);
                    for (int i = 0; i < CharacterList.Count; i++)
                    {
                        if (CharacterList[i].path == selectionPath)
                            SetContextCharacter(CharacterList[i].guid);
                    }
                }

                if (contextCharacter == null)
                    SetContextCharacter(CharacterList[0].guid);
            }
        }

        private CharacterInfo GetCharacterState(string guid)
        {
            foreach (CharacterInfo s in CharacterList)
            {
                if (s.guid.Equals(guid)) return s;
            }

            return null;
        }

        private void CreateTreeView(bool clearSelection = false)
        {
            if (contextCharacter != null)
            {
                // Check whether there is already a serialized view state (state 
                // that survived assembly reloading)
                if (treeViewState == null)
                {
#if UNITY_6000_2_OR_NEWER
                    treeViewState = new TreeViewState<int>();
#else
                    treeViewState = new TreeViewState();
#endif
                }
                characterTreeView = new CharacterTreeView(treeViewState, contextCharacter.Fbx);

                characterTreeView.ExpandToDepth(2);
                if (clearSelection) characterTreeView.ClearSelection();
            }
        }
        public int activeTab = 0;
        public UnityLinkManagerWindow linkModule;
        public float TAB_HEIGHT = 26f;

        private void OnGUI()
        {
            if (importerStyles == null) importerStyles = new Styles();

            RestoreData();
            //RestoreSelection();  // currently suppressed to avoid auto char selection due to CC5 char size

            if (tabStyles == null) tabStyles = new TabStyles();
            if (tabCont == null) tabCont = new TabContents();

            tabStyles.FixMeh();

            Rect areaRect = new Rect(0f, 0f, position.width, position.height);
            
            activeTab = TabbedArea(activeTab, areaRect, tabCont.tabCount, TAB_HEIGHT, tabCont.toolTips, tabCont.icons, 20f, 20f, true, tabCont.overrideTab, tabCont.overrideIcons, datalinkActive, RectHandler);
            
            Rect contentRect = new Rect(0, TAB_HEIGHT, position.width, position.height - TAB_HEIGHT);
                        
            GUILayout.BeginArea(contentRect);

            switch (activeTab)
            {
                case 0:
                    {
                        ImporterOnGUI(contentRect);
                        break;

                    }
                case 1:
                    {
                        if (EditorApplication.isPlaying) break;
                        if (linkModule == null)
                        {
                            linkModule = ScriptableObject.CreateInstance<UnityLinkManagerWindow>();
                        }
                        linkModule.ShowGUI(contentRect);
                        break;
                    }
                case 2:
                    {                        
                        break;
                    }
                case 3:
                    {
                        break;
                    }
            }

            GUILayout.EndArea();
        }

        public enum RefreshMessage
        {
            NoneDetected,
            NoneSelected
        }

        private void RefreshGUI(RefreshMessage message, Rect rect)
        {
            string title = string.Empty;
            string msg = string.Empty;

            switch (message)
            {
                case RefreshMessage.NoneDetected:
                    {
                        title = "No CC/iClone Characters detected!";
                        msg = "Reload the character list, after adding or removing characters.";

                        break;
                    }
                case RefreshMessage.NoneSelected:
                    {
                        title = "No Character selected.";
                        msg = "Reload the character list, after adding or removing characters.";

                        break;
                    }
            }

            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
            GUILayout.BeginArea(rect);
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(title);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(20f);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent(iconActionRefresh, msg),
                GUILayout.Width(ACTION_BUTTON_SIZE), GUILayout.Height(ACTION_BUTTON_SIZE)))
            {
                EditorApplication.delayCall += RefreshCharacterList;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.EndArea();
            EditorGUI.EndDisabledGroup();
        }

        private void ImporterOnGUI(Rect contentRect)
        {                        
            if (CharacterList == null || CharacterList.Count == 0)
            {
                RefreshGUI(RefreshMessage.NoneDetected, contentRect);
                return;
            }            

            float width = position.width - WINDOW_MARGIN;
            float height = position.height - WINDOW_MARGIN - TAB_HEIGHT;
            float innerHeight = height - TOP_PADDING;
            float optionHeight = OPTION_HEIGHT;
            //if (Pipeline.isHDRP12) optionHeight += 14f;
            if (contextCharacter != null)
            {
                if (contextCharacter.Generation == BaseGeneration.Unknown) optionHeight += 14f;
            }
            optionHeight += 14f;

            if (width - ICON_AREA_WIDTH - ACTION_WIDTH < MIN_SETTING_WIDTH)
            {
                ICON_AREA_WIDTH = Mathf.Max(ICON_WIDTH, width - ACTION_WIDTH - MIN_SETTING_WIDTH);
            }

            if (ICON_AREA_WIDTH > width - 51f) ICON_AREA_WIDTH = Mathf.Max(ICON_WIDTH, width - 51f);

            Rect iconBlock = new Rect(0f, TOP_PADDING, ICON_AREA_WIDTH, innerHeight - 16f); // -16f to accomodate temporary show props toggle

            // additions for draggable width icon area
            Rect dragBar = new Rect(iconBlock.xMax, TOP_PADDING, DRAG_BAR_WIDTH, innerHeight);

            Rect infoBlock = new Rect(dragBar.xMax, TOP_PADDING, width - ICON_AREA_WIDTH - ACTION_WIDTH, INFO_HEIGHT);
            CURRENT_INFO_WIDTH = infoBlock.width;

            Rect refreshBlock = new Rect(dragBar.xMax, TOP_PADDING, width - dragBar.xMax, height);

            Rect optionBlock = new Rect(dragBar.xMax, infoBlock.yMax, infoBlock.width, optionHeight);
            Rect actionBlock = new Rect(dragBar.xMax + infoBlock.width, TOP_PADDING, ACTION_WIDTH, innerHeight);
            Rect treeviewBlock = new Rect(dragBar.xMax, optionBlock.yMax, infoBlock.width, height - optionBlock.yMax);
            Rect settingsBlock = new Rect(dragBar.xMax, TOP_PADDING, width - ICON_AREA_WIDTH - ACTION_WIDTH, innerHeight);

            previewCharacterAfterGUI = false;
            refreshAfterGUI = false;
            buildAfterGUI = false;
            bakeAfterGUI = false;
            bakeHairAfterGUI = false;
            restoreHairAfterGUI = false;
            physicsAfterGUI = false;
            processAnimationsAfterGUI = false;

            //CheckDragAndDrop();

            //OnGUIIconArea(iconBlock);
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
            OnGUIFlexibleIconArea(iconBlock);
            OnGUIDragBarArea(dragBar);
            EditorGUI.EndDisabledGroup();

            if (contextCharacter != null)
            {
                if (windowMode == ImporterWindowMode.Build)
                    OnGUIInfoArea(infoBlock);

                if (windowMode == ImporterWindowMode.Build)
                    OnGUIOptionArea(optionBlock);

                if (windowMode == ImporterWindowMode.Settings)
                    OnGUISettingsArea(settingsBlock);

                OnGUIActionArea(actionBlock);

                if (windowMode == ImporterWindowMode.Build)
                    OnGUITreeViewArea(treeviewBlock);
            }
            else
            {
                RefreshGUI(RefreshMessage.NoneSelected, refreshBlock);
            }

            // functions to run after the GUI has finished...             
            if (previewCharacterAfterGUI)
            {
                EditorApplication.delayCall += PreviewCharacter;
            }
            else if (refreshAfterGUI)
            {
                EditorApplication.delayCall += RefreshCharacterList;
            }
            else if (buildAfterGUI)
            {
                EditorApplication.delayCall += BuildCharacter;
            }
            else if (bakeAfterGUI)
            {
                EditorApplication.delayCall += BakeCharacter;
            }
            else if (bakeHairAfterGUI)
            {
                EditorApplication.delayCall += BakeCharacterHair;
            }
            else if (restoreHairAfterGUI)
            {
                EditorApplication.delayCall += RestoreCharacterHair;
            }
            else if (physicsAfterGUI)
            {
                EditorApplication.delayCall += RebuildCharacterPhysics;
            }
            else if (processAnimationsAfterGUI)
            {
                EditorApplication.delayCall += ProcessAnimations;
            }
        }

        bool doubleClick = false;

        private void OnGUIInfoArea(Rect infoBlock)
        {
            string importType = "Unprocessed";
            if (contextCharacter.BuiltBasicMaterials)
                importType = "Default Materials";
            if (contextCharacter.BuiltHQMaterials)
                importType = "High Quality Materials";
            if (contextCharacter.bakeIsBaked)
                importType += " + Baked";

            GUILayout.BeginArea(infoBlock);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(contextCharacter.name, importerStyles.boldStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(contextCharacter.folder, importerStyles.labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("(" + contextCharacter.Generation.ToString() + "/"
                                + contextCharacter.FaceProfile.expressionProfile + "/"
                                + contextCharacter.FaceProfile.visemeProfile
                            + ")", importerStyles.boldStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(importType, importerStyles.boldStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            //if (!string.IsNullOrEmpty(contextCharacter.linkId))
            if(contextCharacter.isLinked)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Link ID: ", importerStyles.boldStyle);
                GUILayout.Label(contextCharacter.linkId, importerStyles.linkStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else if (contextCharacter.IsBlenderProject)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Source: ", importerStyles.boldStyle);
                GUILayout.Label("Blender Project", importerStyles.blenderStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

                GUILayout.FlexibleSpace();

            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }
        Rect prev = new Rect();
        private void OnGUIOptionArea(Rect optionBlock)
        {
            GUILayout.BeginArea(optionBlock);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();

            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
            if (contextCharacter.Generation == BaseGeneration.Unknown)
            {
                if (EditorGUILayout.DropdownButton(
                    content: new GUIContent("Rig Type: " + contextCharacter.UnknownRigType.ToString()),
                    focusType: FocusType.Passive))
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Rig Type: None"), contextCharacter.UnknownRigType == CharacterInfo.RigOverride.None, RigOptionSelected, CharacterInfo.RigOverride.None);
                    menu.AddItem(new GUIContent("Rig Type: Humanoid"), contextCharacter.UnknownRigType == CharacterInfo.RigOverride.Humanoid, RigOptionSelected, CharacterInfo.RigOverride.Humanoid);
                    menu.AddItem(new GUIContent("Rig Type: Generic"), contextCharacter.UnknownRigType == CharacterInfo.RigOverride.Generic, RigOptionSelected, CharacterInfo.RigOverride.Generic);
                    menu.ShowAsContext();
                }

                GUILayout.Space(1f);
            }

            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying || contextCharacter.BasicMaterials);
            if (EditorGUILayout.DropdownButton(
                content: new GUIContent(contextCharacter.BasicMaterials ? "Basic Materials" : "High Quality Materials"),
                focusType: FocusType.Passive))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Basic Materials"), contextCharacter.BasicMaterials, MaterialOptionSelected, true);
                if (contextCharacter.CanHaveHighQualityMaterials)
                    menu.AddItem(new GUIContent("High Quality Materials"), contextCharacter.HQMaterials, MaterialOptionSelected, false);
                menu.ShowAsContext();
            }

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();

            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
            if (EditorGUILayout.DropdownButton(
                content: new GUIContent(Util.CamelCaseToSpaces(contextCharacter.QualTexSize.ToString())),
                focusType: FocusType.Passive))
            {
                GenericMenu menu = new GenericMenu();

                string[] itemNames = Enum.GetNames(typeof(CharacterInfo.TexSizeQuality));
                CharacterInfo.TexSizeQuality[] itemValues = Enum.GetValues(typeof(CharacterInfo.TexSizeQuality)).Cast<CharacterInfo.TexSizeQuality>().ToArray();
                for (int i = 0; i < itemNames.Length; i++)
                {
                    menu.AddItem(new GUIContent(Util.CamelCaseToSpaces(itemNames[i])),
                                                contextCharacter.QualTexSize == itemValues[i], 
                                                TexSizeOptionSelect, 
                                                itemValues[i]);
                }
                menu.ShowAsContext();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(1f);

            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying || contextCharacter.BasicMaterials);
            if (EditorGUILayout.DropdownButton(
                content: new GUIContent(contextCharacter.QualEyes.ToString() + " Eyes"),
                focusType: FocusType.Passive))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Basic Eyes"), contextCharacter.BasicEyes, EyeOptionSelected, CharacterInfo.EyeQuality.Basic);
                menu.AddItem(new GUIContent("Parallax Eyes"), contextCharacter.ParallaxEyes, EyeOptionSelected, CharacterInfo.EyeQuality.Parallax);
                if (Pipeline.isHDRP)
                    menu.AddItem(new GUIContent("Refractive (SSR) Eyes"), contextCharacter.RefractiveEyes, EyeOptionSelected, CharacterInfo.EyeQuality.Refractive);
                menu.ShowAsContext();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.EndVertical();
            GUILayout.BeginVertical();

            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
            if (EditorGUILayout.DropdownButton(
                content: new GUIContent(Util.CamelCaseToSpaces(contextCharacter.QualTexCompress.ToString())),
                focusType: FocusType.Passive))
            {
                GenericMenu menu = new GenericMenu();

                string[] itemNames = Enum.GetNames(typeof(CharacterInfo.TexCompressionQuality));
                CharacterInfo.TexCompressionQuality[] itemValues = Enum.GetValues(typeof(CharacterInfo.TexCompressionQuality)).Cast<CharacterInfo.TexCompressionQuality>().ToArray();
                for (int i = 0; i < itemNames.Length; i++)
                {
                    menu.AddItem(new GUIContent(Util.CamelCaseToSpaces(itemNames[i])),
                                                contextCharacter.QualTexCompress == itemValues[i], 
                                                TexCompressOptionSelect, 
                                                itemValues[i]);
                }
                menu.ShowAsContext();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(1f);

            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying || contextCharacter.BasicMaterials);
            string hairType;
            switch (contextCharacter.QualHair)
            {
                case CharacterInfo.HairQuality.TwoPass: hairType = "Two Pass Hair"; break;
                case CharacterInfo.HairQuality.Coverage: hairType = "MSAA Coverage Hair"; break;
                default:
                case CharacterInfo.HairQuality.Default: hairType = "Single Pass Hair"; break;
            }
            if (EditorGUILayout.DropdownButton(
                content: new GUIContent(hairType),
                focusType: FocusType.Passive))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Single Pass Hair"), contextCharacter.DefaultHair, HairOptionSelected, CharacterInfo.HairQuality.Default);
                menu.AddItem(new GUIContent("Two Pass Hair"), contextCharacter.DualMaterialHair, HairOptionSelected, CharacterInfo.HairQuality.TwoPass);
                //if (Importer.USE_AMPLIFY_SHADER && !Pipeline.isHDRP)
                //    menu.AddItem(new GUIContent("MSAA Coverage Hair"), contextCharacter.CoverageHair, HairOptionSelected, CharacterInfo.HairQuality.Coverage);
                menu.ShowAsContext();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            // /*
            bool showDebugEnumPopup = false;
            if (showDebugEnumPopup)
            {
                int features = 2;
                if (Pipeline.isHDRP12) features++; // tessellation
                if (Pipeline.is3D || Pipeline.isURP) features++; // Amplify

                if (features == 1)
                {
                    contextCharacter.ShaderFlags = (CharacterInfo.ShaderFeatureFlags)EditorGUILayout.EnumPopup(contextCharacter.ShaderFlags);
                }
                else if (features > 1)
                {
                    EditorGUI.BeginChangeCheck();
                    contextCharacter.ShaderFlags = (CharacterInfo.ShaderFeatureFlags)EditorGUILayout.EnumFlagsField(contextCharacter.ShaderFlags);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if ((contextCharacter.ShaderFlags & CharacterInfo.ShaderFeatureFlags.SpringBoneHair) > 0 &&
                            (contextCharacter.ShaderFlags & CharacterInfo.ShaderFeatureFlags.HairPhysics) > 0)
                        {
                            contextCharacter.ShaderFlags -= CharacterInfo.ShaderFeatureFlags.SpringBoneHair;
                        }
                    }
                }
            }
            // */
            EditorGUI.EndDisabledGroup();
            //GUI.enabled = true;

            //////////////

            if (Event.current.type == EventType.Repaint)
                prev = GUILayoutUtility.GetLastRect();

            if (EditorGUILayout.DropdownButton(
                content: new GUIContent("Features"),
                focusType: FocusType.Passive))
            {
                ImporterFeaturesWindow.ShowAtPosition(new Rect(prev.x, prev.y + 20f, prev.width, prev.height));
            }

            //////////////

            GUILayout.Space(8f);

            //if (contextCharacter.BuiltBasicMaterials) GUI.enabled = false;
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying || contextCharacter.BuiltBasicMaterials);
            if (EditorGUILayout.DropdownButton(
                content: new GUIContent(contextCharacter.BakeCustomShaders ? "Bake Custom Shaders" : "Bake Default Shaders"),
                focusType: FocusType.Passive))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Default Shaders"), !contextCharacter.BakeCustomShaders, BakeShadersOptionSelected, false);
                menu.AddItem(new GUIContent("Custom Shaders"), contextCharacter.BakeCustomShaders, BakeShadersOptionSelected, true);
                menu.ShowAsContext();
            }

            GUILayout.Space(1f);

            if (EditorGUILayout.DropdownButton(
                new GUIContent(contextCharacter.BakeSeparatePrefab ? "Bake Separate Prefab" : "Bake Overwrite Prefab"),
                FocusType.Passive
                ))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Overwrite Prefab"), !contextCharacter.BakeSeparatePrefab, BakePrefabOptionSelected, false);
                menu.AddItem(new GUIContent("Separate Baked Prefab"), contextCharacter.BakeSeparatePrefab, BakePrefabOptionSelected, true);
                menu.ShowAsContext();
            }
            EditorGUI.EndDisabledGroup();
            //GUI.enabled = true;

            GUILayout.Space(8f);

            //
            // BUILD BUTTON
            //
            GUIContent buildContent;
            if (contextCharacter.BasicMaterials)
                buildContent = new GUIContent("Build Materials", iconBuildMaterials, "Setup materials to use the default shaders.");
            else
                buildContent = new GUIContent("Build Materials", iconBuildMaterials, "Setup materials to use the high quality shaders.");

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
            if (GUILayout.Button(buildContent,
                GUILayout.Height(BUTTON_HEIGHT), GUILayout.Width(160f)))
            {
                buildAfterGUI = true;
            }
            EditorGUI.EndDisabledGroup();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();


            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();
            GUILayout.EndArea();
        }

        private void OnGUIActionArea(Rect actionBlock)
        {
            GUILayout.BeginArea(actionBlock);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();

            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
            if (false && !string.IsNullOrEmpty(backScenePath) && File.Exists(backScenePath))
            {
                if (GUILayout.Button(new GUIContent("<", "Go back to the last valid scene."),
                    GUILayout.Width(ACTION_BUTTON_SIZE), GUILayout.Height(ACTION_BUTTON_SIZE)))
                {
                    GoBackScene();
                }

                GUILayout.Space(ACTION_BUTTON_SPACE);
            }


            if (GUILayout.Button(new GUIContent(WindowManager.IsPreviewScene ? iconActionPreviewOn : iconActionPreview, "View the current character in a preview scene."),
                GUILayout.Width(ACTION_BUTTON_SIZE), GUILayout.Height(ACTION_BUTTON_SIZE)))
            {
                previewCharacterAfterGUI = true;
            }

            GUILayout.Space(ACTION_BUTTON_SPACE);

            if (mode == Mode.multi)
            {
                if (GUILayout.Button(new GUIContent(iconActionRefresh, "Reload the character list, for after adding or removing characters."),
                    GUILayout.Width(ACTION_BUTTON_SIZE), GUILayout.Height(ACTION_BUTTON_SIZE)))
                {
                    refreshAfterGUI = true;
                }

                GUILayout.Space(ACTION_BUTTON_SPACE);
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(ACTION_BUTTON_SPACE + 11f);

            //if (contextCharacter.BuiltBasicMaterials) GUI.enabled = false;
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying || contextCharacter.BuiltBasicMaterials);
            if (GUILayout.Button(new GUIContent(contextCharacter.bakeIsBaked ? iconActionBakeOn : iconActionBake, "Bake high quality materials down to compatible textures for the default shaders. i.e. HDRP/Lit, URP/Lut or Standard shader."),
                GUILayout.Width(ACTION_BUTTON_SIZE), GUILayout.Height(ACTION_BUTTON_SIZE)))
            {
                bakeAfterGUI = true;
            }
            EditorGUI.EndDisabledGroup();
            //GUI.enabled = true;

            GUILayout.Space(ACTION_BUTTON_SPACE);


            if (contextCharacter.tempHairBake)
            {
                EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
                if (GUILayout.Button(new GUIContent(iconActionBakeHairOn, "Restore original hair diffuse textures."),
                    GUILayout.Width(ACTION_BUTTON_SIZE), GUILayout.Height(ACTION_BUTTON_SIZE)))
                {
                    restoreHairAfterGUI = true;
                }
                EditorGUI.EndDisabledGroup();
            }
            else //if (!contextCharacter.BuiltBasicMaterials && contextCharacter.HasColorEnabledHair())
            {
                //if (contextCharacter.BuiltBasicMaterials || !contextCharacter.HasColorEnabledHair()) GUI.enabled = false;
                EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying || contextCharacter.BuiltBasicMaterials || !contextCharacter.HasColorEnabledHair());
                if (GUILayout.Button(new GUIContent(iconActionBakeHair, "Bake hair diffuse textures, to preview the baked results of the 'Enable Color' in the hair materials."),
                    GUILayout.Width(ACTION_BUTTON_SIZE), GUILayout.Height(ACTION_BUTTON_SIZE)))
                {
                    bakeHairAfterGUI = true;
                }
                EditorGUI.EndDisabledGroup();
            }
            //GUI.enabled = true;

            GUILayout.Space(ACTION_BUTTON_SPACE);

            //if (contextCharacter.Unprocessed) GUI.enabled = false;
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying || contextCharacter.Unprocessed);
            if (GUILayout.Button(new GUIContent(iconActionAnims, "Process, extract and rename character animations and create a default animtor controller."),
                GUILayout.Width(ACTION_BUTTON_SIZE), GUILayout.Height(ACTION_BUTTON_SIZE)))
            {
                processAnimationsAfterGUI = true;
            }
            EditorGUI.EndDisabledGroup();
            //

            GUILayout.Space(ACTION_BUTTON_SPACE);

            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
            if (GUILayout.Button(new GUIContent(iconActionPhysics, "Rebuilds the character physics."),
                GUILayout.Width(ACTION_BUTTON_SIZE), GUILayout.Height(ACTION_BUTTON_SIZE)))
            {
                physicsAfterGUI = true;
            }
            EditorGUI.EndDisabledGroup();
            //GUI.enabled = true;

#if UNITY_ALEMBIC_1_0_7
            GUILayout.Space(ACTION_BUTTON_SPACE);
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
            if (GUILayout.Button(new GUIContent(iconAlembic, "Process alembic animations with this character's materials."),
                GUILayout.Width(ACTION_BUTTON_SIZE), GUILayout.Height(ACTION_BUTTON_SIZE)))
            {
                Alembic.ProcessAlembics(contextCharacter.Fbx, contextCharacter.name, contextCharacter.folder);
            }
            EditorGUI.EndDisabledGroup();
            //GUI.enabled = true;
#endif

            /*
            GUILayout.Space(ACTION_BUTTON_SPACE);

            if (!contextCharacter.BuiltHQMaterials || contextCharacter.BuiltDualMaterialHair) GUI.enabled = false;
            if (GUILayout.Button(new GUIContent(iconAction2Pass, "Convert hair meshes to use two material passes. Two pass hair is generally higher quality, where the hair is first drawn opaque with alpha cutout and the remaing edges drawn in softer alpha blending, but can come at a performance cost."), 
                GUILayout.Width(ACTION_BUTTON_SIZE), GUILayout.Height(ACTION_BUTTON_SIZE)))
            {
                contextCharacter.DualMaterialHair = true;
                MeshUtil.Extract2PassHairMeshes(Util.FindCharacterPrefabAsset(contextCharacter.Fbx));
                contextCharacter.Write();

                ShowPreviewCharacter();

                TrySetMultiPass(true);
            }
            GUI.enabled = true;
            */

            GUILayout.Space(ACTION_BUTTON_SPACE);

            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
            if (GUILayout.Button(new GUIContent(iconActionLOD, "Run the LOD combining tool on the prefabs associated with this character."),
                GUILayout.Width(ACTION_BUTTON_SIZE), GUILayout.Height(ACTION_BUTTON_SIZE)))
            {
                string prefabsFolder = contextCharacter.GetPrefabsFolder();
                Selection.activeObject = AssetDatabase.LoadAssetAtPath(prefabsFolder, typeof(Object)) as Object;
                LodSelectionWindow.InitTool();
            }
            EditorGUI.EndDisabledGroup();
            //GUI.enabled = true;

            GUILayout.Space(ACTION_BUTTON_SPACE * 2f + 11f);

            //if (contextCharacter == null) GUI.enabled = false;
            EditorGUI.BeginDisabledGroup(contextCharacter == null);
            if (GUILayout.Button(new GUIContent(AnimPlayerGUI.IsPlayerShown() ? iconActionAnimPlayerOn : iconActionAnimPlayer, "Show animation preview player."),
                GUILayout.Width(ACTION_BUTTON_SIZE), GUILayout.Height(ACTION_BUTTON_SIZE)))
            {
                if (AnimPlayerGUI.IsPlayerShown())
                {
                    GameObject characterPrefab = WindowManager.GetSelectedOrPreviewCharacter();
                    WindowManager.HideAnimationPlayer(true);
                    ResetAllSceneViewCamera(characterPrefab);
                }
                else
                {
                    GameObject characterPrefab = WindowManager.GetSelectedOrPreviewCharacter();
                    WindowManager.ShowAnimationPlayer();
                    ResetAllSceneViewCamera(characterPrefab);
                }
            }
            EditorGUI.EndDisabledGroup();
            //GUI.enabled = true;

            GUILayout.Space(ACTION_BUTTON_SPACE);

            //if (contextCharacter == null) GUI.enabled = false;
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying || contextCharacter == null);
            if (GUILayout.Button(new GUIContent(AnimRetargetGUI.IsPlayerShown() ? iconActionAvatarAlignOn : iconActionAvatarAlign, "Animation Adjustment & Retargeting."),
                GUILayout.Width(ACTION_BUTTON_SIZE), GUILayout.Height(ACTION_BUTTON_SIZE)))
            {
                if (AnimRetargetGUI.IsPlayerShown())
                {
                    WindowManager.HideAnimationRetargeter(true);
                }
                else
                {
                    if (AnimPlayerGUI.IsPlayerShown())
                        WindowManager.ShowAnimationRetargeter();
                }
            }
            //GUI.enabled = true;
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();

            GUILayout.Space(ACTION_BUTTON_SPACE);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (!WindowManager.IsPreviewScene) GUI.enabled = false;
            if (GUILayout.Button(new GUIContent(iconLighting, "Cycle Lighting."),
                GUILayout.Width(WEE_BUTTON_SIZE), GUILayout.Height(WEE_BUTTON_SIZE)))
            {
                PreviewScene.CycleLighting();
            }
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(ACTION_BUTTON_SPACE);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (!WindowManager.IsPreviewScene) GUI.enabled = false;
            if (GUILayout.Button(new GUIContent(iconCamera, "Match main camera to scene view."),
                GUILayout.Width(WEE_BUTTON_SIZE), GUILayout.Height(WEE_BUTTON_SIZE)))
            {
                WindowManager.DoMatchSceneCameraOnce();
            }
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();



            GUILayout.Space(ACTION_BUTTON_SPACE);
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
            GUIContent settingsIconGC;
            if (windowMode != ImporterWindowMode.Settings)
                settingsIconGC = new GUIContent(iconSettings, "Settings.");
            else
                settingsIconGC = new GUIContent(iconSettingsOn, "Back.");
            if (GUILayout.Button(settingsIconGC,
                GUILayout.Width(ACTION_BUTTON_SIZE), GUILayout.Height(ACTION_BUTTON_SIZE)))
            {
                if (windowMode != ImporterWindowMode.Settings)
                    windowMode = ImporterWindowMode.Settings;
                else
                    windowMode = ImporterWindowMode.Build;
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void OnGUITreeViewArea(Rect treeviewBlock)
        {
            GUILayout.BeginArea(treeviewBlock);

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            if (contextCharacter != null)
            {
                characterTreeView.OnGUI(new Rect(0, 0, treeviewBlock.width, treeviewBlock.height - 16f));
            }
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();
            SELECT_LINKED = GUILayout.Toggle(SELECT_LINKED, "Select Linked");
            GUILayout.FlexibleSpace();

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUILayout.EndArea();
        }

        private void DropDownBox(string[] options, int value, GenericMenu.MenuFunction2 func)
        {                           
            if (EditorGUILayout.DropdownButton(
                content: new GUIContent(options[value]),
                focusType: FocusType.Passive))
            {
                GenericMenu menu = new GenericMenu();
                for (int i = 0; i < options.Length; i++)
                    menu.AddItem(new GUIContent(options[i]), value == i, func, i);
                menu.ShowAsContext();
            }
        }

        private void OnGUISettingsArea(Rect settingsBlock)
        {
            if (EditorApplication.isPlaying)
            {
                windowMode = ImporterWindowMode.Build;
                return;
            }

            GUILayout.BeginArea(settingsBlock);
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Settings", importerStyles.boldStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(TITLE_SPACE);

            /*
            if (!Pipeline.isHDRP)
            {
                Importer.USE_AMPLIFY_SHADER = GUILayout.Toggle(Importer.USE_AMPLIFY_SHADER,
                    new GUIContent("Use Amplify Shaders", "Use the more advanced Amplify shaders where possible. " +
                    "Amplify shaders are capable of subsurface scattering effects, and anisotropic hair lighting in the URP and Build-in 3D pipelines."));
                GUILayout.Space(ROW_SPACE);
            }*/
            
            string[] options = new string[] { "Import Normals", "Calculate Normals" };
            void UpdateBuildNormalsMode(object value) { Importer.BUILD_NORMALS_MODE = (int)value; }
            DropDownBox(options, Importer.BUILD_NORMALS_MODE, UpdateBuildNormalsMode);
            GUILayout.Space(ROW_SPACE);

            Importer.BUILD_MODE = GUILayout.Toggle(Importer.BUILD_MODE,
                new GUIContent("Automatically Build Animations", "Always build animations when building materials."));            
            GUILayout.Space(ROW_SPACE);

            Importer.RECONSTRUCT_FLOW_NORMALS = GUILayout.Toggle(Importer.RECONSTRUCT_FLOW_NORMALS,
                new GUIContent("Reconstruct Flow Map Normals", "Rebuild missing Normal maps from Flow Maps in hair materials. " +
                "Reconstructed Normals add extra detail to the lighting models."));
            GUILayout.Space(ROW_SPACE);

            Importer.REBAKE_BLENDER_UNITY_MAPS = GUILayout.Toggle(Importer.REBAKE_BLENDER_UNITY_MAPS,
                new GUIContent("Rebake Blender Unity Maps", "Always re-bake the blender to unity Diffuse+Alpha, HDRP Mask and Metallic+Gloss maps. " +
                "Otherwise subsequent material rebuilds will try to re-use existing bakes. Only needed if the source textures are changed."));
            GUILayout.Space(ROW_SPACE);

            Importer.REBAKE_PACKED_TEXTURE_MAPS = GUILayout.Toggle(Importer.REBAKE_PACKED_TEXTURE_MAPS,
                new GUIContent("Rebake Packed Texture Maps", "Always re-bake the packed texture maps. " +
                "Otherwise subsequent material rebuilds will try to re-use existing bakes. Only needed if the source textures are changed."));
            GUILayout.Space(ROW_SPACE);

            /*if (Pipeline.isHDRP)
            {
                Importer.USE_TESSELLATION_SHADER = GUILayout.Toggle(Importer.USE_TESSELLATION_SHADER,
                new GUIContent("Use Tessellation in Shaders", "Use tessellation enabled shaders where possible. " +
                "For HDRP 10 & 11 this means default shaders only (HDRP/LitTessellation). For HDRP 12 (Unity 2021.2+) all shader graph shaders can have tessellation enabled."));
                GUILayout.Space(ROW_SPACE);
            }*/

            Importer.ANIMPLAYER_ON_BY_DEFAULT = GUILayout.Toggle(Importer.ANIMPLAYER_ON_BY_DEFAULT,
                    new GUIContent("Animation Player On", "Always show the animation player when opening the preview scene."));
            GUILayout.Space(ROW_SPACE);

            Importer.USE_SELF_COLLISION = GUILayout.Toggle(Importer.USE_SELF_COLLISION,
                    new GUIContent("Use Self Collision", "Use the self collision distances from the Character Creator export."));
            GUILayout.Space(ROW_SPACE);

            GUILayout.Space(10f);
            GUILayout.BeginVertical(new GUIContent("", "Override mip-map bias for all textures setup for the characters."), importerStyles.labelStyle);
            GUILayout.Label("Mip-map Bias");
            GUILayout.Space(ROW_SPACE);
            GUILayout.BeginHorizontal();
            Importer.MIPMAP_BIAS = GUILayout.HorizontalSlider(Importer.MIPMAP_BIAS, -1f, 1f, GUILayout.Width(160f));
            GUILayout.Label(Importer.MIPMAP_BIAS.ToString("0.00"),
                            GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            GUILayout.Label("Hair Mip-map Bias");
            GUILayout.BeginHorizontal();
            Importer.MIPMAP_BIAS_HAIR = GUILayout.HorizontalSlider(Importer.MIPMAP_BIAS_HAIR, -1f, 1f, GUILayout.Width(160f));
            GUILayout.Label(Importer.MIPMAP_BIAS_HAIR.ToString("0.00"),
                            GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(ROW_SPACE);

            GUILayout.Space(10f);
            GUILayout.BeginVertical(new GUIContent("", "When setting up the physics capsule and sphere colliders, shrink the radius by this amount. This can help resolve colliders pushing out cloth too much during simulation."), importerStyles.labelStyle);
            GUILayout.Label("Physics Collider Shrink");
            GUILayout.Space(ROW_SPACE);
            GUILayout.BeginHorizontal();
            Physics.PHYSICS_SHRINK_COLLIDER_RADIUS = GUILayout.HorizontalSlider(Physics.PHYSICS_SHRINK_COLLIDER_RADIUS, -2, 2f, GUILayout.Width(160f));
            GUILayout.Label(Physics.PHYSICS_SHRINK_COLLIDER_RADIUS.ToString("0.00"),
                            GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(ROW_SPACE);


            if (MagicaCloth2Available)
            {
                GUILayout.Space(10f);
                GUILayout.BeginVertical(new GUIContent("", "Set global values for Magica Cloth 2 proxy mesh reduction settings. NB these settings will only be applied the next time the character physics are built."), importerStyles.labelStyle);
                GUILayout.Label("Magica Cloth 2 - Reduction Settings");
                GUILayout.Space(ROW_SPACE);
                GUILayout.Label("Cloth Objects");
                GUILayout.BeginHorizontal();
                GUILayout.Space(20f);
                GUILayout.Label("Simple Distance", GUILayout.Width(100f));
                Physics.CLOTHSIMPLEDISTANCE = (float)Math.Round(GUILayout.HorizontalSlider(Physics.CLOTHSIMPLEDISTANCE, 0f, 0.2f, GUILayout.Width(100f)), 3);
                GUILayout.Label(Physics.CLOTHSIMPLEDISTANCE.ToString("0.000"),
                                GUILayout.Width(40f));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Space(20f);
                GUILayout.Label("Shape Distance", GUILayout.Width(100f));
                Physics.CLOTHSHAPEDISTANCE = (float)Math.Round(GUILayout.HorizontalSlider(Physics.CLOTHSHAPEDISTANCE, 0f, 0.2f, GUILayout.Width(100f)), 3);
                GUILayout.Label(Physics.CLOTHSHAPEDISTANCE.ToString("0.000"),
                                GUILayout.Width(40f));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Label("Hair Objects");
                GUILayout.BeginHorizontal();
                GUILayout.Space(20f);
                GUILayout.Label("Simple Distance", GUILayout.Width(100f));
                Physics.HAIRSIMPLEDISTANCE = (float)Math.Round(GUILayout.HorizontalSlider(Physics.HAIRSIMPLEDISTANCE, 0f, 0.2f, GUILayout.Width(100f)), 3);
                GUILayout.Label(Physics.HAIRSIMPLEDISTANCE.ToString("0.000"),
                                GUILayout.Width(40f));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Space(20f);
                GUILayout.Label("Shape Distance", GUILayout.Width(100f));
                Physics.HAIRSHAPEDISTANCE = (float)Math.Round(GUILayout.HorizontalSlider(Physics.HAIRSHAPEDISTANCE, 0f, 0.2f, GUILayout.Width(100f)), 3);
                GUILayout.Label(Physics.HAIRSHAPEDISTANCE.ToString("0.000"),
                                GUILayout.Width(40f));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(ROW_SPACE);
                GUILayout.EndVertical();
                GUILayout.BeginVertical(new GUIContent("", "Set the threshold for conversion of the PhysX weightmap into the 'Fixed/Moveable' system used by Magica Cloth 2.  When a very low value is set then any slight movement allowed by PhysX will also allow movement in Magica Cloth 2."), importerStyles.labelStyle);

                GUILayout.Label("Weightmap Threshold %", GUILayout.Width(140f));
                GUILayout.BeginHorizontal();
                GUILayout.Space(12f);
                Physics.MAGICA_WEIGHTMAP_THRESHOLD_PC = (float)Math.Round(GUILayout.HorizontalSlider(Physics.MAGICA_WEIGHTMAP_THRESHOLD_PC, 0f, 20f, GUILayout.Width(214f)), 2);
                GUILayout.Label(Physics.MAGICA_WEIGHTMAP_THRESHOLD_PC.ToString("0.00") + " %",
                                GUILayout.Width(50f));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
                GUILayout.Space(ROW_SPACE);
            }

            /*
            GUILayout.Space(10f);
            GUILayout.BeginVertical(new GUIContent("", "When assigning weight maps, the system analyses the weights of the mesh to determine which colliders affect the cloth simulation.Only cloth weights above this threshold will be considered for collider detection. Note: This is the default value supplied to the WeightMapper component, it can be further modified there."), importerStyles.labelStyle);
            GUILayout.Label("Collider Detection Threshold");
            GUILayout.Space(ROW_SPACE);            
            GUILayout.BeginHorizontal();
            Physics.PHYSICS_WEIGHT_MAP_DETECT_COLLIDER_THRESHOLD = GUILayout.HorizontalSlider(Physics.PHYSICS_WEIGHT_MAP_DETECT_COLLIDER_THRESHOLD, 0f, 1f);
            GUILayout.Label(Physics.PHYSICS_WEIGHT_MAP_DETECT_COLLIDER_THRESHOLD.ToString("0.00"), 
                            GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(ROW_SPACE);
            */

            GUILayout.Space(10f);
            options = new string[] { "Log Errors Only", "Log Warnings and Errors", "Log Messages", "Log Everything" };
            void UpdateLogLevel(object value) { Util.LOG_LEVEL = (int)value; }
            DropDownBox(options, Util.LOG_LEVEL, UpdateLogLevel);            
            GUILayout.Space(ROW_SPACE);

            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Reset Options", "Reset options to defaults."),
                GUILayout.Height(BUTTON_HEIGHT), GUILayout.Width(160f)))
            {
                ResetOptions();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(ROW_SPACE);

            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();
            GUILayout.EndArea();
        }

        private void LogOptionSelected(object sel)
        {
            Util.LOG_LEVEL = (int)sel;
        }        

        private void EyeOptionSelected(object sel)
        {
            contextCharacter.QualEyes = (CharacterInfo.EyeQuality)sel;
        }

        private void RigOptionSelected(object sel)
        {
            contextCharacter.UnknownRigType = (CharacterInfo.RigOverride)sel;
        }

        private void HairOptionSelected(object sel)
        {
            contextCharacter.QualHair = (CharacterInfo.HairQuality)sel;
        }

        private void TexSizeOptionSelect(object sel)
        {
            contextCharacter.QualTexSize = (CharacterInfo.TexSizeQuality)sel;
        }

        private void TexCompressOptionSelect(object sel)
        {
            contextCharacter.QualTexCompress = (CharacterInfo.TexCompressionQuality)sel;
        }

        private void MaterialOptionSelected(object sel)
        {
            if ((bool)sel)
                contextCharacter.BuildQuality = MaterialQuality.Default;
            else
                contextCharacter.BuildQuality = MaterialQuality.High;
        }

        private void BakeShadersOptionSelected(object sel)
        {
            contextCharacter.BakeCustomShaders = (bool)sel;
        }

        private void BakePrefabOptionSelected(object sel)
        {
            contextCharacter.BakeSeparatePrefab = (bool)sel;
        }

        public static void TrySetMultiPass(bool state)
        {
            ImporterWindow window = ImporterWindow.Current;

            if (window && window.characterTreeView != null)
            {
                if (Pipeline.isHDRP && contextCharacter.BuiltDualMaterialHair)
                {
                    if (state)
                        window.characterTreeView.EnableMultiPass();
                    else
                        window.characterTreeView.DisableMultiPass();
                    return;
                }

                window.characterTreeView.DisableMultiPass();
            }
        }


        private GameObject ImportCharacter(CharacterInfo info)
        {
            Importer import = new Importer(info);
            GameObject prefab = import.Import();
            info.Write();
            return prefab;
        }

        private static void ClearAllData()
        {
            if (contextCharacter != null) contextCharacter.Release();
            contextCharacter = null;

            if (CharacterList != null)
            {
                foreach (CharacterInfo ci in CharacterList)
                {
                    ci.Release();
                }
                CharacterList.Clear();
                CharacterList = null;
            }

            if (Current && Current.characterTreeView != null)
            {
                ImporterWindow window = Current;
                window.characterTreeView.Release();
            }

            Current = null;
        }
        
        private void OnDestroy()
        {
            ClearAllData();
        }

        public void CheckDragAndDrop()
        {
            switch (Event.current.type)
            {
                case EventType.DragUpdated:
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    break;

                case EventType.DragPerform:

                    UnityEngine.Object[] refs = DragAndDrop.objectReferences;
                    if (DragAndDrop.objectReferences.Length > 0)
                    {
                        UnityEngine.Object obj = DragAndDrop.objectReferences[0];
                        if (Util.IsCC3Character(obj))
                            SetContextCharacter(obj);
                    }
                    DragAndDrop.AcceptDrag();
                    break;
            }
        }

        public bool UpdatePreviewCharacter(GameObject prefabAsset)
        {
            if (WindowManager.IsPreviewScene)
            {
                WindowManager.GetPreviewScene().UpdatePreviewCharacter(prefabAsset);
            }

            return WindowManager.IsPreviewScene;
        }

        private void BuildCharacter()
        {
            Util.LogInfo("Building materials:");

            // refresh the character info for any Json changes
            contextCharacter.Refresh();

            // default to high quality if never set before
            if (contextCharacter.BuildQuality == MaterialQuality.None)
                contextCharacter.BuildQuality = MaterialQuality.High;

            // import and build the materials from the Json data
            GameObject prefabAsset = ImportCharacter(contextCharacter);

            // refresh the tree view with the new data
            CreateTreeView(true);

            // enable / disable multipass material selection (HDRP only)
            if (Pipeline.isHDRP && contextCharacter.HQMaterials && contextCharacter.BuiltDualMaterialHair) characterTreeView.EnableMultiPass();
            else characterTreeView.DisableMultiPass();

            // update the character in the preview scene with the new prefab asset
            if (prefabAsset)
            {
                if (UpdatePreviewCharacter(prefabAsset))
                {
                    if (WindowManager.showPlayer)
                        WindowManager.ShowAnimationPlayer();
                }
            }

            Repaint();
        }

        private void BakeCharacter()
        {
            if (contextCharacter.HQMaterials)
            {
                Util.LogInfo("Baking materials:");

                WindowManager.HideAnimationPlayer(true);

                ComputeBake baker = new ComputeBake(contextCharacter.Fbx, contextCharacter);
                GameObject bakedAsset = baker.BakeHQ();

                contextCharacter.bakeIsBaked = true;
                contextCharacter.Write();

                if (bakedAsset)
                {
                    ShowBakedCharacter(bakedAsset);
                }

            }
        }

        private void BakeCharacterHair()
        {
            if (contextCharacter.HQMaterials)
            {
                Util.LogInfo("Baking hair materials:");

                WindowManager.HideAnimationPlayer(true);

                ComputeBake baker = new ComputeBake(contextCharacter.Fbx, contextCharacter, "Hair");
                baker.BakeHQHairDiffuse();

                contextCharacter.tempHairBake = true;
                contextCharacter.Write();
            }
        }

        private void RestoreCharacterHair()
        {
            if (contextCharacter.HQMaterials)
            {
                Util.LogInfo("Restoring hair materials:");

                WindowManager.HideAnimationPlayer(true);

                ComputeBake baker = new ComputeBake(contextCharacter.Fbx, contextCharacter, "Hair");
                GameObject bakedAsset = baker.RestoreHQHair();

                contextCharacter.tempHairBake = false;
                contextCharacter.Write();
            }
        }

        bool ShowBakedCharacter(GameObject bakedAsset)
        {
            if (WindowManager.IsPreviewScene)
            {
                WindowManager.GetPreviewScene().ShowBakedCharacter(bakedAsset);
            }

            return WindowManager.IsPreviewScene;
        }

        void RebuildCharacterPhysics()
        {
            WindowManager.HideAnimationPlayer(true);
            WindowManager.HideAnimationRetargeter(true);

            GameObject prefabAsset = Physics.RebuildPhysics(contextCharacter);

            if (prefabAsset)
            {
                if (UpdatePreviewCharacter(prefabAsset))
                {
                    if (WindowManager.showPlayer)
                        WindowManager.ShowAnimationPlayer();
                }
            }

            Repaint();
        }

        void ProcessAnimations()
        {
            RL.DoAnimationImport(contextCharacter);
            GameObject characterPrefab = Util.FindCharacterPrefabAsset(contextCharacter.Fbx);
            if (characterPrefab == null)
            {
                Util.LogWarn("Could not find character prefab for retargeting, using FBX instead.");
                characterPrefab = contextCharacter.Fbx;
            }

            AnimRetargetGUI.GenerateCharacterTargetedAnimations(contextCharacter.path, characterPrefab, true);
            List<string> motionGuids = contextCharacter.GetMotionGuids();
            if (motionGuids.Count > 0)
            {
                //Avatar sourceAvatar = contextCharacter.GetCharacterAvatar();
                foreach (string motionGuid in motionGuids)
                {
                    string motionPath = AssetDatabase.GUIDToAssetPath(motionGuid);
                    AnimRetargetGUI.GenerateCharacterTargetedAnimations(motionPath, characterPrefab, true);
                }
            }
            contextCharacter.UpdateAnimationRetargeting();
            contextCharacter.Write();
        }

        public static void ResetAllSceneViewCamera(GameObject targetOverride = null)
        {
            if (WindowManager.IsPreviewScene)
            {
                GameObject obj;
                if (targetOverride) obj = targetOverride;
                else obj = WindowManager.GetPreviewScene().GetPreviewCharacter();

                if (obj)
                {
                    GameObject root = Util.GetScenePrefabInstanceRoot(obj);

                    if (root)
                    {
                        //GameObject hips = MeshUtil.FindCharacterBone(root, "CC_Base_Spine02", "Spine02");
                        //GameObject head = MeshUtil.FindCharacterBone(root, "CC_Base_Head", "Head");
                        GameObject hips = MeshUtil.FindCharacterBone(root, "CC_Base_NeckTwist01", "NeckTwist01");
                        GameObject head = MeshUtil.FindCharacterBone(root, "CC_Base_Head", "Head");
                        if (hips && head)
                        {
                            Vector3 lookAt = (hips.transform.position + head.transform.position * 2f) / 3f;
                            Quaternion lookBackRot = new Quaternion();
                            Vector3 euler = lookBackRot.eulerAngles;
                            euler.y = -180f;
                            lookBackRot.eulerAngles = euler;

                            foreach (SceneView sv in SceneView.sceneViews)
                            {
                                sv.LookAt(lookAt, lookBackRot, 0.25f);
                            }
                        }
                    }
                }
            }
        }

        public static void ForceUpdateLighting()
        {
            PreviewScene.PokeLighting();
        }

        public static void ResetOptions()
        {
            Importer.MIPMAP_BIAS = 0f;
            Importer.MIPMAP_BIAS_HAIR = -0.65f;
            Importer.RECONSTRUCT_FLOW_NORMALS = false;
            Importer.REBAKE_BLENDER_UNITY_MAPS = false;
            Importer.REBAKE_PACKED_TEXTURE_MAPS = false;
            Importer.ANIMPLAYER_ON_BY_DEFAULT = false;
            Importer.USE_SELF_COLLISION = false;            
            Physics.PHYSICS_SHRINK_COLLIDER_RADIUS = 0.5f;
            Physics.PHYSICS_WEIGHT_MAP_DETECT_COLLIDER_THRESHOLD = 0.25f;

            Physics.CLOTHSIMPLEDISTANCE = Physics.CLOTHSHAPEDISTANCE_DEFAULT;
            Physics.CLOTHSHAPEDISTANCE = Physics.CLOTHSHAPEDISTANCE_DEFAULT;
            Physics.HAIRSIMPLEDISTANCE = Physics.HAIRSIMPLEDISTANCE_DEFAULT;
            Physics.HAIRSHAPEDISTANCE = Physics.HAIRSHAPEDISTANCE_DEFAULT;
            Physics.MAGICA_WEIGHTMAP_THRESHOLD_PC = Physics.MAGICA_WEIGHTMAP_THRESHOLD_PC_DEFAULT;

            Util.LOG_LEVEL = 0;
            ICON_AREA_WIDTH = ICON_WIDTH;
        }

        // additions for draggable width icon area

        private void OnGUIDragBarArea(Rect dragBar)
        {
            //Rect dragHandle = new Rect(dragBar.x - DRAG_HANDLE_PADDING, dragBar.y, 2 * DRAG_HANDLE_PADDING, dragBar.height);
            Rect dragHandle = new Rect(dragBar.x, dragBar.y, DRAG_BAR_WIDTH + DRAG_HANDLE_PADDING, dragBar.height);
            EditorGUIUtility.AddCursorRect(dragHandle, MouseCursor.ResizeHorizontal);
            HandleMouseDrag(dragHandle);

            GUILayout.BeginArea(dragBar);
            GUILayout.BeginVertical(importerStyles.dragBarStyle);
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void OnGUIFlexibleIconArea(Rect iconBlock)
        {
            if (ICON_AREA_WIDTH > ICON_WIDTH_DETAIL)
            {
                OnGUIDetailIconArea(iconBlock); // detail view icon area layout
            }
            else
            {
                OnGUILargeIconArea(iconBlock); // adapted original icon area layaout
            }

            Rect toggleRect = new Rect(iconBlock.x, iconBlock.yMax + 1f, iconBlock.width, 22f);
            GUILayout.BeginArea(toggleRect);
            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();
            EditorGUI.BeginChangeCheck();
            showProps = GUILayout.Toggle(showProps, "Show Props");
            if (EditorGUI.EndChangeCheck())
            {
                generalSettings.showProps = showProps;
                RefreshCharacterList();                
                SaveSettings();
            }
            GUILayout.FlexibleSpace();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
        
        // adapted original icon area layaout
        private void OnGUIOriginalIconArea(Rect iconBlock)
        {
            GUILayout.BeginArea(iconBlock);
            
            Event e = Event.current;
            if (e.isMouse && e.type == EventType.MouseDown)
            {
                if (e.clickCount == 2) doubleClick = true;
                else doubleClick = false;
            }
            
            using (var iconScrollViewScope = new EditorGUILayout.ScrollViewScope(iconScrollView, GUILayout.Width(iconBlock.width - 1f), GUILayout.Height(iconBlock.height - 10f)))
            {
                iconScrollView = iconScrollViewScope.scrollPosition;
                GUILayout.BeginVertical();

                for (int idx = 0; idx < CharacterList.Count; idx++)
                {
                    CharacterInfo info = CharacterList[idx];
                    Texture2D iconTexture = iconUnprocessed;
                    string name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(info.guid));
                    if (info.bakeIsBaked)
                    {
                        if (info.BuiltBasicMaterials) iconTexture = iconMixed;
                        else if (info.BuiltHQMaterials) iconTexture = iconBaked;
                    }
                    else
                    {
                        if (info.BuiltBasicMaterials) iconTexture = iconBasic;
                        else if (info.BuiltHQMaterials) iconTexture = iconHQ;
                    }

                    Color background = GUI.backgroundColor;
                    Color tint = background;
                    if (contextCharacter == info)
                        tint = Color.green;
                    GUI.backgroundColor = Color.Lerp(background, tint, 0.25f);

                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();

                    GUILayout.BeginVertical();

                    GUILayout.Box(iconTexture, GUI.skin.button,
                    GUILayout.Width(ICON_SIZE),
                    GUILayout.Height(ICON_SIZE));
                    
                    if (GUILayout.Button(iconTexture,
                        GUILayout.Width(ICON_SIZE),
                        GUILayout.Height(ICON_SIZE)))
                    {
                        SetContextCharacter(info.guid);
                        if (doubleClick)
                        {
                            previewCharacterAfterGUI = true;
                        }
                    }                    

                    GUI.backgroundColor = background;

                    GUILayout.Space(2f);

                    GUILayout.Box(name, importerStyles.iconStyle, GUILayout.Width(ICON_SIZE));
                    GUILayout.Space(2f);
                    GUILayout.EndVertical();

                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndArea();
        }

        private void OnGUILargeIconArea(Rect iconBlock)
        {
            GUILayout.Space(TOP_PADDING);

            float rowHeight = ICON_SIZE + 24;

            Rect boxRect = new Rect(0f, 0f, ICON_AREA_WIDTH - 4f, rowHeight);
            Rect posRect = new Rect(iconBlock);
            Rect viewRect = new Rect(0f, 0f, ICON_AREA_WIDTH - 14f, rowHeight * CharacterList.Count);

            iconScrollView = GUI.BeginScrollView(posRect, iconScrollView, viewRect, false, false);

            for (int idx = 0; idx < CharacterList.Count; idx++)
            {
                CharacterInfo info = CharacterList[idx];
                string name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(info.guid));

                Texture2D iconTexture = GetLargeIconTexture(info);

                Color background = GUI.backgroundColor;
                Color tint = background;
                if (contextCharacter == info)
                    tint = Color.green;
                GUI.backgroundColor = Color.Lerp(background, tint, 0.25f);

                boxRect.y = idx * rowHeight;

                GUILayout.BeginArea(boxRect);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                GUILayout.BeginVertical();

                GUILayout.Box(iconTexture, GUI.skin.button,
                GUILayout.Width(ICON_SIZE),
                GUILayout.Height(ICON_SIZE));

                GUI.backgroundColor = background;

                GUILayout.Space(2f);

                GUILayout.Box(name, importerStyles.iconStyle, GUILayout.Width(ICON_SIZE));
                GUILayout.Space(2f);
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.EndArea();

                if (HandleListClick(boxRect))
                {
                    //RepaintOnUpdate();
                    SetContextCharacter(info.guid);
                    if (fakeButtonDoubleClick)
                    {
                        previewCharacterAfterGUI = true;
                    }
                }

                HandleDrag(boxRect, info);
            }
            GUI.EndScrollView();
        }

        private Texture2D GetLargeIconTexture(CharacterInfo info)
        {
            Texture2D iconTexture = info.IsBlenderProject ? iconBlenderUnprocessed : iconUnprocessed;

            if (info.exportType == CharacterInfo.ExportType.PROP)
            {
                if (info.IsBlenderProject)
                    iconTexture = iconBlenderProp;
                else
                    iconTexture = info.isLinked ? iconLinkedProp : iconProp;
            }
            else
            {
                if (info.bakeIsBaked)
                {
                    if (info.BuiltBasicMaterials)
                    {
                        if (info.IsBlenderProject)
                            iconTexture = iconBlenderMixed;
                        else
                            iconTexture = info.isLinked ? iconLinkedMixed : iconMixed;
                    }
                    else if (info.BuiltHQMaterials)
                    {
                        if (info.IsBlenderProject)
                            iconTexture = iconBlenderBaked;
                        else
                            iconTexture = info.isLinked ? iconLinkedBaked : iconBaked;
                    }
                }
                else
                {
                    if (info.BuiltBasicMaterials)
                    {
                        if (info.IsBlenderProject)
                            iconTexture = iconBlenderBasic;
                        else
                            iconTexture = info.isLinked ? iconLinkedBasic : iconBasic;
                    }
                    else if (info.BuiltHQMaterials)
                    {
                        if (info.IsBlenderProject)
                            iconTexture = iconBlenderHQ;
                        else
                            iconTexture = info.isLinked ? iconLinkedHQ : iconHQ;
                    }
                }
            }
            return iconTexture;
        }

        // detail view icon area layout
        private void OnGUIDetailIconArea(Rect iconBlock)
        {
            importerStyles.FixMeh();

            GUILayout.Space(TOP_PADDING);

            float rowHeight = ICON_SIZE_SMALL + 2 * ICON_DETAIL_MARGIN;

            Rect boxRect = new Rect(0f, 0f, ICON_AREA_WIDTH - 4f, rowHeight);
            Rect posRect = new Rect(iconBlock);
            Rect viewRect = new Rect(0f, 0f, ICON_AREA_WIDTH - 14f, rowHeight * CharacterList.Count);

            iconScrollView = GUI.BeginScrollView(posRect, iconScrollView, viewRect, false, false);
            for (int idx = 0; idx < CharacterList.Count; idx++)
            {
                CharacterInfo info = CharacterList[idx];
                
                Texture2D iconTexture = iconUnprocessed;
                string name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(info.guid));
                if (info.exportType == CharacterInfo.ExportType.PROP)
                {
                    iconTexture = iconProp;
                }
                else
                {
                    if (info.bakeIsBaked)
                    {
                        if (info.BuiltBasicMaterials) iconTexture = iconMixed;
                        else if (info.BuiltHQMaterials) iconTexture = iconBaked;
                    }
                    else
                    {
                        if (info.BuiltBasicMaterials) iconTexture = iconBasic;
                        else if (info.BuiltHQMaterials) iconTexture = iconHQ;
                    }
                }

                float heightDelta = ICON_SIZE_SMALL + 2 * ICON_DETAIL_MARGIN;
                boxRect.y = idx * heightDelta;

                GUILayout.BeginArea(boxRect);

                GUILayout.BeginVertical(contextCharacter == info ? importerStyles.fakeButtonContext : importerStyles.fakeButton);
                GUILayout.FlexibleSpace();

                GUILayout.BeginHorizontal(); // horizontal container for image and label

                GUILayout.BeginVertical(); // vertical container for image
                GUILayout.FlexibleSpace();

                GUILayout.Box(iconTexture, new GUIStyle(),
                    GUILayout.Width(ICON_SIZE_SMALL),
                    GUILayout.Height(ICON_SIZE_SMALL));
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical(); // vertical container for image

                GUILayout.BeginVertical(); // vertical container for label
                GUILayout.FlexibleSpace();

                GUIStyle nameTextStyle = GUIStyle.none;

                if (info.IsBlenderProject)
                    nameTextStyle = importerStyles.nameTextBlenderStyle;
                else
                    nameTextStyle = info.isLinked ? importerStyles.nameTextLinkedStyle : importerStyles.nameTextStyle;

                GUILayout.Label(name, nameTextStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical(); // vertical container for label

                GUILayout.FlexibleSpace(); // fill horizontal for overall left-justify

                GUILayout.EndHorizontal(); // horizontal container for image and label

                GUILayout.FlexibleSpace();
                GUILayout.EndVertical(); //(fakeButton)

                GUILayout.EndArea();

                if (HandleListClick(boxRect))
                {
                    //RepaintOnUpdate();
                    SetContextCharacter(info.guid);
                    if (fakeButtonDoubleClick)
                    {
                        previewCharacterAfterGUI = true;
                    }
                }
                                
                HandleDrag(boxRect, info);
            }
            GUI.EndScrollView();
        }

        private void HandleMouseDrag(Rect container)
        {
            Event mouseEvent = Event.current;
            if (container.Contains(mouseEvent.mousePosition) || dragging)
            {
                if (mouseEvent.type == EventType.MouseDrag)
                {
                    dragging = true;
                    ICON_AREA_WIDTH += mouseEvent.delta.x;
                    if (ICON_AREA_WIDTH < ICON_WIDTH_MIN)
                        ICON_AREA_WIDTH = ICON_WIDTH_MIN;

                    //float INFO_WIDTH_CALC = position.width - WINDOW_MARGIN - ICON_WIDTH - ACTION_WIDTH;
                    if (CURRENT_INFO_WIDTH < INFO_WIDTH_MIN)
                        ICON_AREA_WIDTH = position.width - WINDOW_MARGIN - ACTION_WIDTH - INFO_WIDTH_MIN;

                    RepaintOnUpdate();
                }

                if (mouseEvent.type == EventType.MouseUp)
                {
                    dragging = false;

                    RepaintOnUpdate();
                }
            }
        }

        private bool fakeButtonDoubleClick = false;

        private bool HandleListClick(Rect container)
        {
            Event mouseEvent = Event.current;
            if (container.Contains(mouseEvent.mousePosition))
            {
                if (mouseEvent.type == EventType.MouseDown)
                {
                    if (mouseEvent.clickCount == 2)
                    {
                        fakeButtonDoubleClick = true;
                    }
                    else
                        fakeButtonDoubleClick = false;
                    //mouseEvent.Use();
                    return true;
                }
            }
            return false;
        }

        void RepaintOnUpdate()
        {
            if (!repaintDelegated)
            {
                repaintDelegated = true;
                EditorApplication.update -= RepaintOnceOnUpdate;
                EditorApplication.update += RepaintOnceOnUpdate;
            }
        }

        void RepaintOnceOnUpdate()
        {
            Repaint();
            EditorApplication.update -= RepaintOnceOnUpdate;
            repaintDelegated = false;
        }

        public static Texture2D TextureColor(Color color)
        {
            const int size = 32;
            Texture2D texture = new Texture2D(size, size);
            Color[] pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        public class Styles
        {
            public GUIStyle logStyle;
            public GUIStyle mainStyle;
            public GUIStyle buttonStyle;
            public GUIStyle labelStyle;
            public GUIStyle boldStyle;
            public GUIStyle linkStyle;
            public GUIStyle blenderStyle;
            public GUIStyle iconStyle;
            public GUIStyle dragBarStyle;
            public GUIStyle nameTextStyle;
            public GUIStyle nameTextLinkedStyle;
            public GUIStyle nameTextBlenderStyle;
            public GUIStyle fakeButton;
            public GUIStyle fakeButtonContext;
            public Texture2D dragTex, contextTex;

            public Styles()
            {
                logStyle = new GUIStyle();
                logStyle.wordWrap = true;
                logStyle.fontStyle = FontStyle.Italic;
                logStyle.normal.textColor = Color.grey;

                mainStyle = new GUIStyle();
                mainStyle.wordWrap = false;
                mainStyle.fontStyle = FontStyle.Normal;
                mainStyle.normal.textColor = Color.white;

                iconStyle = new GUIStyle();
                iconStyle.wordWrap = false;
                iconStyle.fontStyle = FontStyle.Normal;
                iconStyle.normal.textColor = Color.white;
                iconStyle.alignment = TextAnchor.MiddleCenter;

                boldStyle = new GUIStyle();
                boldStyle.alignment = TextAnchor.UpperLeft;
                boldStyle.wordWrap = false;
                boldStyle.fontStyle = FontStyle.Bold;
                boldStyle.normal.textColor = Color.white;

                linkStyle = new GUIStyle();
                linkStyle.alignment = TextAnchor.UpperLeft;
                linkStyle.wordWrap = false;
                linkStyle.fontStyle = FontStyle.Bold;
                linkStyle.normal.textColor = new Color(0.82f, 1.0f, 0.48f);

                blenderStyle = new GUIStyle();
                blenderStyle.alignment = TextAnchor.UpperLeft;
                blenderStyle.wordWrap = false;
                blenderStyle.fontStyle = FontStyle.Bold;
                blenderStyle.normal.textColor = new Color(0.91f, 0.46f, 0f);

                labelStyle = new GUIStyle();
                labelStyle.alignment = TextAnchor.UpperLeft;
                labelStyle.wordWrap = false;
                labelStyle.fontStyle = FontStyle.Normal;
                labelStyle.normal.textColor = Color.white;

                buttonStyle = new GUIStyle();
                buttonStyle.wordWrap = false;
                buttonStyle.fontStyle = FontStyle.Normal;
                buttonStyle.normal.textColor = Color.white;
                buttonStyle.alignment = TextAnchor.MiddleCenter;

                //color textures for the area styling


                dragBarStyle = new GUIStyle();
                dragBarStyle.normal.background = dragTex;
                dragBarStyle.stretchHeight = true;
                dragBarStyle.stretchWidth = true;

                nameTextStyle = new GUIStyle();
                nameTextStyle.alignment = TextAnchor.MiddleLeft;
                nameTextStyle.wordWrap = false;
                nameTextStyle.fontStyle = FontStyle.Normal;
                nameTextStyle.normal.textColor = Color.white;

                nameTextLinkedStyle = new GUIStyle();
                nameTextLinkedStyle.alignment = TextAnchor.MiddleLeft;
                nameTextLinkedStyle.wordWrap = false;
                nameTextLinkedStyle.fontStyle = FontStyle.Normal;
                nameTextLinkedStyle.normal.textColor = new Color(0.82f, 1.0f, 0.48f);

                nameTextBlenderStyle = new GUIStyle();
                nameTextBlenderStyle.alignment = TextAnchor.MiddleLeft;
                nameTextBlenderStyle.wordWrap = false;
                nameTextBlenderStyle.fontStyle = FontStyle.Normal;
                nameTextBlenderStyle.normal.textColor = new Color(0.91f, 0.46f, 0f);

                fakeButton = new GUIStyle();
                //fakeButton.normal.background = nonContextTex;
                fakeButton.padding = new RectOffset(1, 1, 1, 1);
                fakeButton.stretchHeight = true;
                fakeButton.stretchWidth = true;

                fakeButtonContext = new GUIStyle();
                fakeButtonContext.name = "fakeButtonContext";
                fakeButtonContext.normal.background = contextTex;
                fakeButtonContext.padding = new RectOffset(1, 1, 1, 1);
                fakeButtonContext.stretchHeight = true;
                fakeButtonContext.stretchWidth = true;

                FixMeh();
            }

            public void FixMeh()
            {
                if (!dragTex)
                {
                    dragTex = TextureColor(new Color(0f, 0f, 0f, 0.25f));
                    dragBarStyle.normal.background = dragTex;
                }
                if (!contextTex)
                {
                    contextTex = TextureColor(new Color(0.259f, 0.345f, 0.259f));
                    fakeButtonContext.normal.background = contextTex;
                }
            }
        }

        private void CheckAvailableAddons()
        {
            // init simple bools for the GUI to use to avoid repeatedly iterating through 
            // AppDomain.CurrentDomain.GetAssemblies() -- ALWAYS make these checks before any reflection code
            dynamicBoneAvailable = Physics.DynamicBoneIsAvailable();
            magicaCloth2Available = Physics.MagicaCloth2IsAvailable();
        }


        public TabStyles tabStyles;
        public TabContents tabCont;

        public class TabStyles
        {
            public Vector4 activeBorder;
            public Vector4 inactiveBorder;
            public Vector4 ghostBorder;
            public Vector4 contentBorder;

            public Color outline;
            public Color ghost;

            public Texture2D activeTex;
            public Texture2D inactiveTex;

            public GUIStyle iconStyle;

            public TabStyles()
            {
                outline = Color.black;
                ghost = Color.gray * 0.4f;

                activeBorder = new Vector4(1, 1, 1, 0);
                inactiveBorder = new Vector4(0, 0, 0, 1);
                ghostBorder = new Vector4(1, 1, 1, 0);
                contentBorder = new Vector4(1, 0, 1, 1);

                activeTex = TexCol(Color.gray * 0.55f);
                inactiveTex = TexCol(Color.gray * 0.35f);
                
                iconStyle = new GUIStyle();

                FixMeh();
            }

            private Texture2D TexCol(Color color)
            {
                const int size = 32;
                Texture2D texture = new Texture2D(size, size);
                Color[] pixels = texture.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = color;
                }
                texture.SetPixels(pixels);
                texture.Apply(true);
                return texture;
            }

            public void FixMeh()
            {
                if (!activeTex)
                {
                    activeTex = TexCol(Color.gray * 0.55f);
                }
                if (!inactiveTex)
                {
                    inactiveTex = TexCol(Color.gray * 0.35f);
                }
            }
        }

        public class TabContents
        {
            private Texture2D iconAvatarTab;
            private Texture2D iconPropTab;
            private Texture2D iconLinkTab;
            private Texture2D iconLinkConnected;
            private Texture2D iconLinkDisconnected;
            private Texture2D iconSettingsTab;

            public int tabCount;
            public string[] toolTips;
            public Texture[] icons;
            public int overrideTab;
            public Texture[] overrideIcons;
            
            public TabContents()
            {
                string[] folders = new string[] { "Assets", "Packages" };
                
                iconAvatarTab = Util.FindTexture(folders, "RLIcon-Avatar_G");
                iconPropTab = Util.FindTexture(folders, "RLIcon-Prop_G");
                iconLinkTab = Util.FindTexture(folders, "RLIcon-Link_G");
                iconLinkConnected = Util.FindTexture(folders, "RLIcon-Link_CON_G");
                iconLinkDisconnected = Util.FindTexture(folders, "RLIcon-Link_DIS_G");
                iconSettingsTab = Util.FindTexture(folders, "RLIcon_Camera");

                tabCount = 2; // was 4
                toolTips = new string[] { "Characters", "Props", "Live Link to Character Creator or iClone", "Settings" };
                icons = new Texture[]
                {
                    iconAvatarTab,
                    //iconPropTab,
                    iconLinkTab,
                    //iconSettingsTab
                };
                overrideTab = 1; // was 2
                overrideIcons = new Texture[]
                {
                    iconLinkConnected,
                    iconLinkDisconnected
                };
            }
        }
        // can override a single tab with icons based on a bool
        public int TabbedArea(int TabId, Rect area, int tabCount, float tabHeight, string[] toolTips, Texture[] icons, float iconWidth, float iconHeight, bool fullWindow, int overrideTab = -1, Texture[] overrideIcons = null, bool overrideBool = false, Func<Rect, int, bool> RectHandler = null)
        {
            if (tabStyles == null) tabStyles = new TabStyles();
            Rect areaRect;
            if (!fullWindow)
            {
                // round width down to an integer multiple of tabCount
                float width = (float)Math.Round(area.width / tabCount, MidpointRounding.AwayFromZero) * tabCount;

                areaRect = new Rect(area.x, area.y, width, area.height);
            }
            else
            {
                areaRect = area;
            }

            Rect[] tabRects = new Rect[tabCount];
            float tabWidth = (float)Math.Round (areaRect.width / tabCount, mode: MidpointRounding.AwayFromZero);
            for (int i = 0; i < tabCount; i++)
            {
                tabRects[i] = new Rect(tabWidth * i, 0f, tabWidth, tabHeight);
                if (RectHandler != null) RectHandler(tabRects[i], i); // callback to handle interaction with the tab rect, used for drag and drop
            }

            int TAB_ID = TabId;
            GUILayout.BeginArea(areaRect, GUI.skin.box);
            for (int i = 0; i < tabCount; i++)
            {
                Rect rect = tabRects[i];
                Rect centre = new Rect(rect.x + ((rect.width / 2) - (iconWidth / 2)), rect.y + ((rect.height / 2) - (iconHeight / 2)), iconWidth, iconHeight);

                Texture icon = i == overrideTab ? (overrideBool ? overrideIcons[0] : overrideIcons[1]) : icons[i];
                // if we arent overriding the icons on a single tab, then the default is icon = icons[i]
                if (i == TAB_ID)
                {
                    GUI.DrawTexture(rect, tabStyles.activeTex);
                    GUI.DrawTexture(rect, tabStyles.activeTex, ScaleMode.StretchToFill, false, 1f, tabStyles.outline, tabStyles.activeBorder, Vector4.zero);
                    GUI.Box(centre, new GUIContent(icon, toolTips[i]), tabStyles.iconStyle);
                }
                else
                {
                    GUI.DrawTexture(rect, tabStyles.inactiveTex);
                    GUI.DrawTexture(rect, tabStyles.inactiveTex, ScaleMode.StretchToFill, false, 1f, tabStyles.outline, tabStyles.inactiveBorder, Vector4.zero);
                    GUI.DrawTexture(rect, tabStyles.inactiveTex, ScaleMode.StretchToFill, false, 1f, tabStyles.ghost, tabStyles.ghostBorder, Vector4.zero);
                    GUI.Box(centre, new GUIContent(icon, toolTips[i]), tabStyles.iconStyle);
                }

                Event mouseEvent = Event.current;
                if (rect.Contains(mouseEvent.mousePosition))
                {
                    if (mouseEvent.type == EventType.MouseDown && mouseEvent.clickCount == 1)
                    {
                        TAB_ID = i;
                        Repaint();
                    }
                }
            }
            Rect contentRect = new Rect(0, tabHeight, areaRect.width, areaRect.height - tabHeight);
            GUI.DrawTexture(contentRect, tabStyles.activeTex);
            if (!fullWindow)
                GUI.DrawTexture(contentRect, tabStyles.activeTex, ScaleMode.StretchToFill, false, 1f, tabStyles.outline, tabStyles.contentBorder, Vector4.zero);

            GUILayout.EndArea();
            return TAB_ID;
        }

        public bool RectHandler(Rect rect, int TabId)
        {
            Event e = Event.current;
            if (rect.Contains(e.mousePosition))
            {
                switch (e.type)
                {
                    case EventType.DragUpdated:

                        DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                        break;

                    case EventType.DragPerform:

                        UnityEngine.Object[] refs = DragAndDrop.objectReferences;
                        if (DragAndDrop.objectReferences.Length > 0)
                        {
                            Debug.Log("Tab Id: " + TabId);
                            //CharacterInfo obj = (CharacterInfo)DragAndDrop.GetGenericData("DragTypeExampleString");
                            //Debug.Log("Tab Id: " + TabId +  " Character Name: " + obj.CharacterName);
                            //Debug.Log(refs[0].name);
                        }
                        DragAndDrop.AcceptDrag();
                        break;
                }
            }
            return false;
        }

        public void HandleDrag(Rect rect, CharacterInfo data)
        {
            Event e = Event.current;
            if (rect.Contains(e.mousePosition) && !dragging)
            {
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.MoveArrow);
            }
            
            if (e.isMouse)
            {
                if (rect.Contains(e.mousePosition) && e.type == EventType.MouseDrag && !dragging)
                {
                    GameObject obj = data.GetDraggablePrefab();
                    GUIUtility.hotControl = 0;
                    StartDrag(obj, data);
                    e.Use();
                }
            }
        }

        public const string DRAG_TYPE = "DragTypeExampleString";
        public const string DRAG_TITLE = "DragTitleString";

        public void StartDrag(GameObject obj, CharacterInfo data)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.visualMode = DragAndDropVisualMode.Link;

            DragAndDrop.SetGenericData(DRAG_TYPE, data);
            DragAndDrop.paths = null;
            DragAndDrop.objectReferences = new UnityEngine.Object[] { obj };

            DragAndDrop.StartDrag(DRAG_TITLE);
        }

        public void MonitorConnection()
        {
            UnityLinkManager.ClientConnected -= ConnectedToserver;
            UnityLinkManager.ClientConnected += ConnectedToserver;
            UnityLinkManager.ClientDisconnected -= DisconnectedFromServer;
            UnityLinkManager.ClientDisconnected += DisconnectedFromServer;
        }

        public bool datalinkActive = false;

        public void ConnectedToserver(object Sender, EventArgs e)
        {
            datalinkActive = true;
            //UnityLinkManager.ClientConnected -= ConnectedToserver;
            //Repaint();
        }

        public void DisconnectedFromServer(object Sender, EventArgs e)
        {
            datalinkActive = false;
            //UnityLinkManager.ClientDisconnected -= DisconnectedFromServer;
            //Repaint();
        }
    }
}