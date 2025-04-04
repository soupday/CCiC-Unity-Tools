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
                queueItemStyle = new GUIStyle(GUI.skin.label);
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
            if (foldoutControlArea)
            {
                SceneToolsGUI();
            }

            foldoutLogArea = EditorGUILayout.Foldout(foldoutLogArea, "Message logs");
            if (foldoutControlArea)
            {
                LogAreaGUI();
            }
            GUILayout.EndVertical();

            if (createAfterGUI)
            {
                EditorApplication.delayCall += UnityLinkTimeLine.CreateExampleScene;
                createAfterGUI = false;
            }
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
}