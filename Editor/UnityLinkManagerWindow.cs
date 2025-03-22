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
                queueItemStyle = GUI.skin.label;
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
        
        private void OnGUI()
        {
            if (styles == null)
                styles = new Styles();

            if (UnityLinkManager.activityQueue == null)
                UnityLinkManager.activityQueue = new List<UnityLinkManager.QueueItem>();

            

            GUILayout.BeginVertical();

            ControlAreaGUI();

            LogAreaGUI();


            

            GUILayout.EndVertical();
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
                }
            }
            EditorGUI.EndDisabledGroup();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            UnityLinkManager.ImportIntoCurrentScene = GUILayout.Toggle(UnityLinkManager.ImportIntoCurrentScene, "Import into current scene.");
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