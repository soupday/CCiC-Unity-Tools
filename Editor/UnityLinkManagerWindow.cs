using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Reallusion.Import
{

    public class UnityLinkManagerWindow : EditorWindow
    {
        public static UnityLinkManagerWindow Instance;

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

            List < UnityLinkManager.QueueItem > guiQueue = new List<UnityLinkManager.QueueItem>();
            
            for (int i = 0; i < UnityLinkManager.activityQueue.Count; i++)
            {
                guiQueue.Add(UnityLinkManager.activityQueue[i]);
            }

            GUILayout.BeginVertical();

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

            GUILayout.EndVertical();
        }


    }
}