using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Reallusion.Import
{

    public class UnityLinkManagerWindow : EditorWindow
    {
        public static UnityLinkManagerWindow Instance;

        [MenuItem("Reallusion/Live Link Manager")]
        public static void OpenLinkManagerWindow()
        {
            OpenWindow();
        }

        [MenuItem("Reallusion/Live Link Manager", true)]
        public static bool ValidateOpenLinkManagerWindow()
        {
            return !EditorWindow.HasOpenInstances<UnityLinkManagerWindow>();
        }

        public static void OpenWindow()
        {
            if (EditorWindow.HasOpenInstances<UnityLinkManagerWindow>())
            {
                //Instance = GetWindow<UnityLinkManagerWindow>();
            }
            else
            {
                Instance = ScriptableObject.CreateInstance<UnityLinkManagerWindow>();
                Instance.minSize = new Vector2(300f, 300f);
                Instance.ShowUtility();
            }
            Instance.Focus();
        }

        public Styles styles;

        public class Styles
        {
            public GUIStyle queueItemStyle;

            public Styles()
            {
                queueItemStyle = new GUIStyle(GUI.skin.box);// (GUI.skin.label);
                queueItemStyle.normal.textColor = Color.yellow;                
            }
        }

        private void OnEnable()
        {
            Instance = this;

            UnityLinkManager.AttemptAutoReconnect();
            UnityLinkManager.CleanupBeforeAssemblyReload();            
        }

        private void OnDestroy()
        {
            UnityLinkManager.DisconnectAndStopServer();
        }

        bool foldoutControlArea;
        bool foldoutSceneArea;
        bool foldoutLogArea;
        
        private void OnGUI()
        {            
            if (styles == null)
                styles = new Styles();

            if (UnityLinkManager.activityQueue == null)
                UnityLinkManager.activityQueue = new List<UnityLinkManager.QueueItem>();

            GUILayout.BeginVertical();            

            foldoutControlArea = EditorGUILayout.Foldout(foldoutControlArea, "Connection controls");
            if (foldoutControlArea)
            {
                ControlAreaGUI();
            }

            foldoutSceneArea = EditorGUILayout.Foldout(foldoutSceneArea, "Scenebuilding tools");
            if (foldoutSceneArea)
            {
                SceneToolsGUI();
            }

            foldoutLogArea = EditorGUILayout.Foldout(foldoutLogArea, "Message logs");
            if (foldoutLogArea)
            {
                LogAreaGUI();
            }

            ExampleShowTabbedArea();

            GUILayout.EndVertical();

            if (createAfterGUI)
            {
                EditorApplication.delayCall += UnityLinkTimeLine.CreateExampleScene;
                createAfterGUI = false;
            }
        }

        int toolbarIndex = 0;
        string[] tabNames = new string[] { "One", "Two", "Three" };
        void ExampleToolbar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(2f);
            GUIStyle tabBarStyle = new GUIStyle(GUI.skin.button);
            tabBarStyle.margin = new RectOffset(0, 0, 1, 0);
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                toolbarIndex = GUILayout.Toolbar(toolbarIndex, tabNames, tabBarStyle, GUI.ToolbarButtonSize.Fixed, GUILayout.Width(position.width - 14 /3f));
                if (check.changed)
                {

                }
            }
            GUILayout.Space(4f);
            GUILayout.EndHorizontal();
        }

        Rect last;
        public TabStyles tabStyles;
        float GUTTER = 2f;
        float TAB_HEIGHT = 26f;
        int activeTab = 1;
        void ExampleShowTabbedArea()
        {
            if (Event.current.type == EventType.Repaint)
            {
                last = GUILayoutUtility.GetLastRect();
            }

            Rect areaRect = new Rect(last.xMin, last.yMax + GUTTER, position.width - (GUTTER * 2f), position.height - last.yMax + GUTTER - (GUTTER * 2f));

            string[] titles = new string[] { "One", "Two", "Three", "Four" };

            Texture[] pix = new Texture[]
            {
                EditorGUIUtility.IconContent("BoxCollider Icon").image,
                EditorGUIUtility.IconContent("CapsuleCollider Icon").image,
                EditorGUIUtility.IconContent("ConfigurableJoint Icon").image,
                EditorGUIUtility.IconContent("RelativeJoint2D Icon").image
            };

            activeTab = TabbedArea(activeTab, areaRect, 4, TAB_HEIGHT, titles, pix, 20f, 20f);
        }

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

            public TabStyles()
            {
                outline = Color.black;
                ghost = Color.gray * 0.4f;

                activeBorder = new Vector4(1, 1, 1, 0);
                inactiveBorder = new Vector4(0, 0, 0, 1);
                ghostBorder = new Vector4(1, 1, 1, 0);
                contentBorder = new Vector4(1, 0, 1, 1);

                activeTex = TextureColor(Color.gray * 0.55f);
                inactiveTex = TextureColor(Color.gray * 0.35f);
            }
        }

        public int TabbedArea(int TabId, Rect area, int tabCount, float tabHeight, string[] names, Texture[] icons, float iconWidth, float iconHeight)
        {
            if (tabStyles == null) tabStyles = new TabStyles();

            // round width down to an integer multiple of tabCount
            float width = (float)Math.Round(area.width / tabCount, MidpointRounding.AwayFromZero) * tabCount;

            Rect areaRect = new Rect(area.x, area.y, width, area.height);            

            Rect[] tabRects = new Rect[tabCount];
            float tabWidth = areaRect.width / tabCount;
            for (int i = 0; i < tabCount; i++)
            {
                tabRects[i] = new Rect(tabWidth * i, 0f, tabWidth, TAB_HEIGHT);
            }

            int TAB_ID = TabId;
            GUILayout.BeginArea(areaRect, GUI.skin.box);
            for (int i = 0; i < tabCount; i++)
            {
                Rect rect = tabRects[i];
                Rect centre = new Rect(rect.x + ((rect.width / 2) - (iconWidth / 2)), rect.y + ((rect.height / 2) - (iconHeight / 2)), iconWidth, iconHeight);

                if (i == TAB_ID)
                {
                    GUI.DrawTexture(rect, tabStyles.activeTex);
                    GUI.DrawTexture(rect, tabStyles.activeTex, ScaleMode.StretchToFill, false, 1f, tabStyles.outline, tabStyles.activeBorder, Vector4.zero);
                    GUI.Box(centre, icons[i], new GUIStyle());
                }
                else
                {
                    GUI.DrawTexture(rect, tabStyles.inactiveTex);
                    GUI.DrawTexture(rect, tabStyles.inactiveTex, ScaleMode.StretchToFill, false, 1f, tabStyles.outline, tabStyles.inactiveBorder, Vector4.zero);
                    GUI.DrawTexture(rect, tabStyles.inactiveTex, ScaleMode.StretchToFill, false, 1f, tabStyles.ghost, tabStyles.ghostBorder, Vector4.zero);
                    GUI.Box(centre, icons[i], new GUIStyle());
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
            Rect contentRect = new Rect(0, TAB_HEIGHT, areaRect.width, areaRect.height - TAB_HEIGHT);
            GUI.DrawTexture(contentRect, tabStyles.activeTex);
            GUI.DrawTexture(contentRect, tabStyles.activeTex, ScaleMode.StretchToFill, false, 1f, tabStyles.outline, tabStyles.contentBorder, Vector4.zero);

            GUILayout.EndArea();
            return TAB_ID;
        }

        public static  Texture2D TextureColor(Color color)
        {
            const int size = 3;
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

        void ControlAreaGUI()
        {
            GUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(UnityLinkManager.IsClientThreadActive);
            {
                if (GUILayout.Button("Connect"))
                {
                    UnityLinkManager.InitConnection();
                }
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(!UnityLinkManager.IsClientThreadActive);
            {
                if (GUILayout.Button("Disconnect"))
                {
                    UnityLinkManager.DisconnectAndStopServer();
                    Repaint();
                }
            }
            EditorGUI.EndDisabledGroup();
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            UnityLinkManager.IsClientLocal = GUILayout.Toggle(UnityLinkManager.IsClientLocal, "Client and server are on the same host");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            UnityLinkManager.ImportIntoCurrentScene = GUILayout.Toggle(UnityLinkManager.ImportIntoCurrentScene, "Import into current scene.");
            GUILayout.EndHorizontal();
        }

        bool createAfterGUI = false;

        void SceneToolsGUI()
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label("Scene Ref", GUILayout.Width(85f));
            UnityLinkManager.SCENE_REFERENCE_STRING = GUILayout.TextField(UnityLinkManager.SCENE_REFERENCE_STRING);
            GUILayout.Space(8f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Folder Name", GUILayout.Width(85f));
            if (GUILayout.Button(EditorGUIUtility.IconContent("d_FolderOpened Icon").image, GUILayout.Width(20f), GUILayout.Height(20f)))
            {
                string proposed = string.IsNullOrEmpty(UnityLinkManager.SAVE_FOLDER_PATH) ? "Assets" : UnityLinkManager.SAVE_FOLDER_PATH;
                string defaultFolder = string.IsNullOrEmpty(UnityLinkManager.SCENE_REFERENCE_STRING) ? "Folder" : UnityLinkManager.SCENE_REFERENCE_STRING;
                UnityLinkManager.SAVE_FOLDER_PATH = EditorUtility.OpenFolderPanel("Parent Folder For Scene and Assets", proposed, "");
            }
            UnityLinkManager.SAVE_FOLDER_PATH = GUILayout.TextField(UnityLinkManager.SAVE_FOLDER_PATH, GUILayout.MinWidth(100f));
            GUILayout.Space(8f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Create New Scene"))
            {
                createAfterGUI = true;
            }
            GUILayout.Space(8f);
            GUILayout.EndHorizontal();
        }


        Vector2 logScrollPos = new Vector2();
        void LogAreaGUI()
        {
            List<UnityLinkManager.QueueItem> guiQueue = new List<UnityLinkManager.QueueItem>();

            for (int i = 0; i < UnityLinkManager.activityQueue.Count; i++)
            {
                guiQueue.Add(UnityLinkManager.activityQueue[i]);
            }

            logScrollPos = GUILayout.BeginScrollView(logScrollPos);

            foreach (var q in guiQueue)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(q.OpCode.ToString() + " " + q.EntryTime + " " + q.Exchange, styles.queueItemStyle);
                GUILayout.EndHorizontal();

                if (q.OpCode == UnityLinkManager.OpCodes.NOTIFY)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("  Notify message: " + q.Notify.Message, styles.queueItemStyle);
                    GUILayout.EndHorizontal();
                }

            }

            GUILayout.EndScrollView();

        }
    }

    public class UnityLinkManagerHistoryDropdown : EditorWindow
    {
        // dropdown gui that detects active import references in the scene and prompts them along with a list of the previous 10 or so used ones


    }

}