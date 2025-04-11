using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Reallusion.Import
{

    public class UnityLinkManagerWindow : EditorWindow
    {
        #region Menu (disused)
        /*
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
            
        }
        */

        #endregion Menu (disused)

        #region Init/Shutdown
        public static UnityLinkManagerWindow Instance;
        static RLSettingsObject settings;
        private void OnEnable()
        {
            EditorApplication.quitting -= QuitCleanup;
            EditorApplication.quitting += QuitCleanup;

            FetchSettings();

            UnityLinkManager.ClientConnected -= ConnectedToserver;
            UnityLinkManager.ClientConnected += ConnectedToserver;
            UnityLinkManager.ClientDisconnected -= DisconnectedFromServer;
            UnityLinkManager.ClientDisconnected += DisconnectedFromServer;

            Instance = this;
            foldoutControlArea = true;

            UnityLinkManager.AttemptAutoReconnect();
            UnityLinkManager.CleanupBeforeAssemblyReload();
        }

        private void OnDestroy()
        {
            UnityLinkManager.DisconnectFromServer();
        }

        private void QuitCleanup()
        {
            UnityLinkManager.DisconnectFromServer();
        }

        private static void RepaintOnUpdate(bool stop = false)
        {
            if (stop)
            {
                EditorApplication.update -= UpdateDelegate;
            }
            else
            {
                EditorApplication.update -= UpdateDelegate;
                EditorApplication.update += UpdateDelegate;
            }
        }

        static double updateInterval = 0.05f;
        static double lastUpdate;
        private static void UpdateDelegate()
        {
            double current = EditorApplication.timeSinceStartup;
            if (current > lastUpdate + updateInterval)
            {
                ImporterWindow.Current.Repaint();
                lastUpdate = current;
            }
        }

        private void FetchSettings()
        {
            if (ImporterWindow.Current != null)
            {
                if (ImporterWindow.GeneralSettings != null)
                    settings = ImporterWindow.GeneralSettings;
            }

            if (settings != null)
            {
                // control area
                if (!string.IsNullOrEmpty(settings.lastExportPath))               
                    UnityLinkManager.EXPORTPATH = settings.lastExportPath;

                UnityLinkManager.IS_CLIENT_LOCAL = settings.isClientLocal;

                if (!string.IsNullOrEmpty(settings.lastSuccessfulHost))
                    remoteHost = settings.lastSuccessfulHost;
                
                if (settings.lastTriedHosts != null)
                    lastTriedHosts = settings.lastTriedHosts;
                else
                    lastTriedHosts = new string[0];

                // scene area
                UnityLinkManager.IMPORT_INTO_SCENE = settings.importIntoScene;
                UnityLinkManager.USE_CURRENT_SCENE = settings.useCurrentScene;
                UnityLinkManager.ADD_TO_TIMELINE = settings.addToTimeline;

                if (!string.IsNullOrEmpty(settings.sceneReference))
                    UnityLinkManager.SCENE_REFERENCE_STRING = settings.sceneReference;

                if (settings.recentSceneRefs != null)
                    recentSceneRefs = settings.recentSceneRefs;
                else
                    recentSceneRefs = new string[0];

                if (!string.IsNullOrEmpty(settings.lastSaveFolder))
                    UnityLinkManager.TIMELINE_SAVE_FOLDER = settings.lastSaveFolder;



                // SCENE_REFERENCE_STRING
            }
        }

        private static void SaveSettings()
        {
            RLSettings.SaveRLSettingsObject(settings);
        }
        #endregion Init/Shutdown

        #region Connection Management
        static string remoteHost = string.Empty;
        static string[] lastTriedHosts = new string[0];
        static string[] recentSceneRefs = new string[0];

        static bool connectInProgress = false;

        static void StartClient()
        {
            RepaintOnUpdate();

            UnityLinkManager.ClientConnected -= ConnectedToserver;
            UnityLinkManager.ClientConnected += ConnectedToserver;

            UnityLinkManager.ClientDisconnected -= DisconnectedFromServer;
            UnityLinkManager.ClientDisconnected += DisconnectedFromServer;

            UnityLinkImporter.ImportStarted -= OnImportStarted;
            UnityLinkImporter.ImportStarted += OnImportStarted;

            UnityLinkManager.StartQueue();

            StartConnection();
        }

        static void StartConnection()
        {
            Debug.LogWarning("Starting StartConnection ");
            // if a remote connection is needed then validate the address string as IP
            // if the address string cant be validated check if it can be resolved by Dns
            //UnityLinkManager.IS_CLIENT_LOCAL = isClientLocal;
            if (!UnityLinkManager.IS_CLIENT_LOCAL)
            {
                UnityLinkManager.NotifyInternalQueue("Attempting connection to " + remoteHost);
                bool valid = false;
                control = Control.Validating;

                Debug.LogWarning("Testing host");

                // validate IP address
                if (IPAddress.TryParse(remoteHost, out IPAddress ip))
                {
                    Debug.LogWarning("IP address is an ip address");
                    valid = true;
                    UnityLinkManager.REMOTE_HOST = remoteHost;
                }
                else
                {
                    valid = false;
                    control = Control.InValid;

                    // attempt dns resolution - using async method (client hangs otherwise)
                    // https://stackoverflow.com/questions/41348873/limit-dns-gethostaddresses-by-time
                    try
                    {
                        Debug.LogWarning("try Dns resolution async");
                        var addr = Dns.GetHostAddressesAsync(remoteHost);
                        bool isTaskComplete = addr.Wait(3000);
                        if (isTaskComplete)
                        {
                            IPAddress[] hosts = addr.Result;
                            if (hosts.Length > 0)
                            {
                                Debug.LogWarning("Dns name resolves async");
                                valid = true;
                                UnityLinkManager.REMOTE_HOST = hosts[0].MapToIPv4().ToString();
                                UnityLinkManager.NotifyInternalQueue("Hostname " + remoteHost + " resolves to " + UnityLinkManager.REMOTE_HOST);
                            }
                        }
                        else // Task timed out
                        {
                            UnityLinkManager.NotifyInternalQueue(remoteHost + " does not resolve to a vaid IP address");
                            Debug.LogWarning("Dns name resolution timed out async");
                        }
                    }
                    catch
                    {
                        UnityLinkManager.NotifyInternalQueue("Dns cannot resolve " + remoteHost);
                        Debug.LogWarning("Dns name does not resolve async");
                        valid = false;
                        control = Control.InValid;
                    }

                    /*
                    try
                    {
                        IPAddress[] hostaddrs = Dns.GetHostAddresses(remoteHost);

                        if(hostaddrs.Length>0)
                        {
                            Debug.LogWarning("Dns name resolves");
                            valid = true;                            
                            UnityLinkManager.remoteHost = hostaddrs[0].MapToIPv4().ToString();
                        }
                    }
                    catch
                    {
                        Debug.LogWarning("Dns name does not resolve");
                        valid = false;
                        control = Control.InValid;
                    }
                    */

                }
                connectInProgress = valid;
                Debug.LogWarning("connectInProgress = " + connectInProgress);
            }
            if (connectInProgress)
            {
                // tell the gui to show a cancel button in case the IP is wrong
                control = Control.Connecting;
                UnityLinkManager.InitConnection();
            }
        }

        static void ConnectedToserver(object sender, EventArgs e)
        {
            UnityLinkManager.ClientConnected -= ConnectedToserver;

            control = Control.Connected;
            connectInProgress = false;

            RepaintOnUpdate(stop: true);

            if (settings != null)
            {
                if (!UnityLinkManager.IS_CLIENT_LOCAL)
                {
                    // since the last remote ip connected properly, save it in settings
                    settings.lastSuccessfulHost = UnityLinkManager.REMOTE_HOST;
                    SaveSettings();
                }
            }
            else
            {
                Debug.LogWarning("No settings available");
            }

            Debug.LogWarning("ConnectedToserver");
        }

        static void DisconnectedFromServer(object sender, EventArgs e)
        {
            UnityLinkManager.ClientDisconnected -= DisconnectedFromServer;

            UnityLinkManager.NotifyInternalQueue("Connection returning to idle state.");
            control = Control.Idle;
            connectInProgress = false;

            RepaintOnUpdate(stop: true);
            UnityLinkManager.StopQueue();

            Debug.LogWarning("DisconnectedFromServer");
        }

        static void OnImportStarted(object sender, EventArgs e)
        {
            UnityLinkImporter.ImportStarted -= OnImportStarted;
            Debug.LogWarning("Import Started Event");
            if (UnityLinkManager.ADD_TO_TIMELINE)
            {
                if (settings != null)
                {
                    settings.sceneReference = UnityLinkManager.SCENE_REFERENCE_STRING;
                    settings.recentSceneRefs = recentSceneRefs;
                    settings.lastSaveFolder = UnityLinkManager.TIMELINE_SAVE_FOLDER;
                    Instance.RecordSceneRefHistory(UnityLinkManager.SCENE_REFERENCE_STRING);
                }
            }
        }

        #endregion Connection Management

        #region GUI
        bool foldoutControlArea;
        bool foldoutSceneArea;
        bool foldoutLogArea;

        float PADDING = 4f;
        float CONTROL_HEIGHT = 100f;
        float SCENE_HEIGHT = 100f;
        float MESSAGE_HEIGHT = 100f;

        static Control control = Control.Idle;
        Rect remoteHostTextField; // rect of control area remote host text field for history dropdown;
        Rect sceneRefTextField; // rect of scene area scene reference text field for history dropdown;

        bool createSceneAfterGUI = false;

        public Styles styles;

        public class Styles
        {
            public Texture2D toggleLeft;
            public Texture2D toggleRight;
            public Texture2D linkOn;
            public Texture2D linkOff;
            public Texture2D delimTex;

            public GUIStyle messageItemStyle;
            public GUIStyle FoldoutTitleLabel;
            public GUIStyle unselectedLabel;
            public GUIStyle selectedLabel;
            public GUIStyle statusLabel;
            public GUIStyle minimalButton;
            public GUIStyle normalTextField;
            public GUIStyle errorTextField;
            public GUIStyle delimBox;

            public Styles()
            {
                string[] folders = new string[] { "Assets", "Packages" };
                toggleLeft = Util.FindTexture(folders, "RLIcon_Toggle_L");
                toggleRight = Util.FindTexture(folders, "RLIcon_Toggle_R");
                linkOn = Util.FindTexture(folders, "RLIcon_Link_ON");
                linkOff = Util.FindTexture(folders, "RLIcon_Link_OFF");

                messageItemStyle = new GUIStyle(GUI.skin.box);
                messageItemStyle.normal.textColor = Color.yellow;
                messageItemStyle.onNormal.textColor = Color.yellow;
                messageItemStyle.hover.textColor = Color.yellow;
                messageItemStyle.onHover.textColor = Color.yellow;
                messageItemStyle.wordWrap = false;
               
                FoldoutTitleLabel = new GUIStyle(EditorStyles.foldout);
                FoldoutTitleLabel.fontSize = 14;
                FoldoutTitleLabel.fontStyle = FontStyle.BoldAndItalic;
                FoldoutTitleLabel.onFocused.textColor = Color.white;      
                FoldoutTitleLabel.focused.textColor = Color.white;

                unselectedLabel = new GUIStyle(GUI.skin.label);
                unselectedLabel.normal.textColor = Color.white * 0.75f;

                selectedLabel = new GUIStyle(GUI.skin.label);
                selectedLabel.normal.textColor = new Color(0.5f, 0.75f,  0.06f);

                statusLabel = new GUIStyle(GUI.skin.label);
                statusLabel.normal.textColor = new Color(0.27f, 0.57f, 0.78f);
                statusLabel.onNormal.textColor = new Color(0.27f, 0.57f, 0.78f);
                statusLabel.hover.textColor = new Color(0.27f, 0.57f, 0.78f);
                statusLabel.onHover.textColor = new Color(0.27f, 0.57f, 0.78f);

                minimalButton = new GUIStyle(GUI.skin.label);
                minimalButton.padding = new RectOffset(0, 0, 1, 0);

                normalTextField = new GUIStyle(EditorStyles.textField);

                errorTextField = new GUIStyle(EditorStyles.textField);
                errorTextField.normal.background = ImporterWindow.TextureColor(Color.red * 0.35f);
                errorTextField.onNormal.background = ImporterWindow.TextureColor(Color.red * 0.35f);

                delimTex = ImporterWindow.TextureColor(new Color(0f, 0f, 0f, 0.25f));                
                delimBox = new GUIStyle();
                delimBox.normal.background = delimTex;
                delimBox.stretchHeight = true;
                delimBox.stretchWidth = true;
            }
        }
        
        public enum Control
        {
            Idle,
            Validating,
            InValid,
            Aborting,
            Connecting,
            Connected
        }

        public void ShowGUI(Rect containerRect)
        {
            if (settings == null) FetchSettings();

            if (styles == null)
                styles = new Styles();

            if (UnityLinkManager.activityQueue == null)
                UnityLinkManager.activityQueue = new List<UnityLinkManager.QueueItem>();

            GUILayout.Space(PADDING);
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Space(PADDING);
            GUILayout.BeginVertical(GUI.skin.box);
            foldoutControlArea = EditorGUILayout.Foldout(foldoutControlArea, "Connection controls", true, styles.FoldoutTitleLabel);
            if (foldoutControlArea)
            {
                ControlAreaGUI();
            }
            GUILayout.Space(PADDING);
            GUILayout.EndVertical();
            GUILayout.Space(PADDING);
            GUILayout.EndHorizontal();

            GUILayout.Space(PADDING);

            GUILayout.BeginHorizontal();
            GUILayout.Space(PADDING);
            GUILayout.BeginVertical(GUI.skin.box);
            foldoutSceneArea = EditorGUILayout.Foldout(foldoutSceneArea, "Scenebuilding tools", true, styles.FoldoutTitleLabel);
            if (foldoutSceneArea)
            {
                SceneToolsGUI();
            }
            GUILayout.Space(PADDING);
            GUILayout.EndVertical();
            GUILayout.Space(PADDING);
            GUILayout.EndHorizontal();

            GUILayout.Space(PADDING);

            GUILayout.BeginHorizontal();
            GUILayout.Space(PADDING);
            GUILayout.BeginVertical(GUI.skin.box);
            foldoutLogArea = EditorGUILayout.Foldout(foldoutLogArea, "Message logs", true, styles.FoldoutTitleLabel);
            if (foldoutLogArea)
            {
                LogAreaGUI();
            }
            GUILayout.Space(PADDING);
            GUILayout.EndVertical();
            GUILayout.Space(PADDING);
            GUILayout.EndHorizontal();

            GUILayout.Space(PADDING);
            GUILayout.EndVertical();

            if (createSceneAfterGUI)
            {
                EditorApplication.delayCall += UnityLinkTimeLine.CreateExampleScene;
                createSceneAfterGUI = false;
            }
        }

        void ControlAreaGUI()
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            GUILayout.Space(18f);
            bool disableButton = false;

            if (connectInProgress) disableButton = true;
            else if (!UnityLinkManager.IS_CLIENT_LOCAL && string.IsNullOrEmpty(remoteHost)) disableButton = true;

            EditorGUI.BeginDisabledGroup(disableButton);
            GUI.SetNextControlName("connectButton");
            if (GUILayout.Button(new GUIContent(UnityLinkManager.IsClientThreadActive ? styles.linkOn : styles.linkOff,
                                                UnityLinkManager.IsClientThreadActive ? "Disconnect from Cc/iClone" : "Connect to Cc/iClone"),
                                                new GUIStyle(), GUILayout.Width(64f), GUILayout.Height(64f)))
            {
                if (UnityLinkManager.IsClientThreadActive)
                {
                    UnityLinkManager.DisconnectFromServer();
                    Repaint();
                }
                else
                {
                    // tell the gui to disable the connect button
                    connectInProgress = true;
                    RecordConnectionHistory(remoteHost);
                    GUI.FocusControl("connectButton");
                    if (!UnityLinkManager.IS_CLIENT_LOCAL) control = Control.Validating;
                    Repaint();

                    EditorApplication.delayCall += StartClient;
                }
            }
            EditorGUI.EndDisabledGroup();
            GUILayout.Space(12f);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Space(6f);
            GUILayout.Box("", styles.delimBox, GUILayout.Width(2f), GUILayout.Height(95f));
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Space(6f);
            bool maskControls = true;
            if (control == Control.Idle) maskControls = false;
            else if (control == Control.InValid) maskControls = false; ;

            EditorGUI.BeginDisabledGroup(maskControls);

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Export Folder", "Folder to use as a working directory for file exchange (Try to keep this folder OUTSIDE the Unity project)."), GUILayout.Width(85f));
            if (GUILayout.Button(EditorGUIUtility.IconContent("d_FolderOpened Icon").image, GUILayout.Width(20f), GUILayout.Height(20f)))
            {   
                string proposed = EditorUtility.SaveFolderPanel("Working directory for file export (KEEP OUTISDE UNITY PROJECT)", Path.GetDirectoryName(UnityLinkManager.EXPORTPATH), UnityLinkManager.EXPORTPATH);//
                if (!string.IsNullOrEmpty(proposed)) UnityLinkManager.EXPORTPATH = proposed; 
            }
            UnityLinkManager.EXPORTPATH = GUILayout.TextField(UnityLinkManager.EXPORTPATH, GUILayout.MinWidth(100f), GUILayout.MaxWidth(150f));            
            GUILayout.EndHorizontal();

            GUILayout.Space(3f);
            GUILayout.BeginHorizontal();
            GUILayout.Space(10f);
            GUILayout.Box("", styles.delimBox, GUILayout.Width(250f), GUILayout.Height(2f));
            GUILayout.EndHorizontal();
            GUILayout.Space(3f);

            GUILayout.BeginHorizontal();
            GUIStyle singleLabelText = UnityLinkManager.IS_CLIENT_LOCAL ? styles.selectedLabel : styles.unselectedLabel;
            GUILayout.Label("Local Server", singleLabelText, GUILayout.Width(80f));

            Texture2D toggleImg = UnityLinkManager.IS_CLIENT_LOCAL ? styles.toggleLeft : styles.toggleRight;
            if (GUILayout.Button(toggleImg, GUI.skin.label, GUILayout.Width(30f), GUILayout.Height(20f)))
            {
                UnityLinkManager.IS_CLIENT_LOCAL = !UnityLinkManager.IS_CLIENT_LOCAL;
                settings.isClientLocal = UnityLinkManager.IS_CLIENT_LOCAL;
                SaveSettings();
            }

            GUIStyle conLabelText = UnityLinkManager.IS_CLIENT_LOCAL ? styles.unselectedLabel : styles.selectedLabel;
            GUILayout.Label("Remote Server", conLabelText, GUILayout.Width(110f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(UnityLinkManager.IS_CLIENT_LOCAL);
            GUILayout.Label("Remote host");
            GUILayout.Space(8f);
            EditorGUI.BeginChangeCheck();
            remoteHost = EditorGUILayout.TextField(remoteHost, control == Control.InValid ? styles.errorTextField : styles.normalTextField, GUILayout.Width(120f));
            if (Event.current.type == EventType.Repaint)
            {
                remoteHostTextField = GUILayoutUtility.GetLastRect();
            }
            if (EditorGUI.EndChangeCheck())
            {
                control = Control.Idle;
                Repaint();
            }
            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_dropdown_toggle").image, "Show previous connection attempts"), styles.minimalButton))
            {
                TextFieldHistory.ShowAtPosition(new Rect(remoteHostTextField.x, remoteHostTextField.y, remoteHostTextField.width, remoteHostTextField.height), UpdateRemoteHostName, lastTriedHosts);
            }

            GUILayout.FlexibleSpace();
            EditorGUI.EndDisabledGroup();

            EditorGUI.EndDisabledGroup();
            GUILayout.EndHorizontal();


            GUILayout.BeginHorizontal();
            string controlText = "Status: ";
            string statusText = string.Empty;
            switch (control)
            {
                case Control.Idle:
                    {
                        statusText += "Connection idle...";
                        break;
                    }
                case Control.Validating:
                    {
                        statusText += "Validatig IP...";
                        break;
                    }
                case Control.InValid:
                    {
                        statusText += "Invalid IP...";
                        break;
                    }
                case Control.Aborting:
                    {
                        statusText += "Aborting connection...";
                        break;
                    }
                case Control.Connecting:
                    {
                        statusText += "Connecting...";
                        break;
                    }
                case Control.Connected:
                    {
                        statusText += "Connected...";
                        break;
                    }
            }

            GUILayout.Label(controlText);
            GUILayout.Label(statusText, styles.statusLabel);
            if (control == Control.Connecting)
            {
                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("TestFailed").image, "Abort connection attempt."), styles.minimalButton))
                {
                    control = Control.Aborting;
                    Repaint();
                    UnityLinkManager.AbortConnectionAttempt();
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        void SceneToolsGUI()
        {
            // IMPORT_INTO_SCENE
            GUILayout.BeginHorizontal();            
            Texture2D sceneImpToggleImg = UnityLinkManager.IMPORT_INTO_SCENE ? styles.toggleRight : styles.toggleLeft;
            if (GUILayout.Button(sceneImpToggleImg, GUI.skin.label, GUILayout.Width(30f), GUILayout.Height(20f)))
            {
                UnityLinkManager.IMPORT_INTO_SCENE = !UnityLinkManager.IMPORT_INTO_SCENE;
                settings.importIntoScene = UnityLinkManager.IMPORT_INTO_SCENE;
                SaveSettings();
            }
            GUIStyle sceneImp = UnityLinkManager.IMPORT_INTO_SCENE ? styles.selectedLabel : styles.unselectedLabel;
            GUILayout.Label("Import Into Scene", sceneImp, GUILayout.Width(110f));
            GUILayout.EndHorizontal();

            // USE_CURRENT_SCENE
            if (UnityLinkManager.IMPORT_INTO_SCENE)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(12f);

                GUILayout.BeginVertical();
                GUILayout.BeginHorizontal();
                GUIStyle useSceneLabelText = UnityLinkManager.USE_CURRENT_SCENE ? styles.selectedLabel : styles.unselectedLabel;
                GUILayout.Label("Current scene", useSceneLabelText, GUILayout.Width(90f));

                Texture2D useSceneToggleImg = UnityLinkManager.USE_CURRENT_SCENE ? styles.toggleLeft : styles.toggleRight;
                if (GUILayout.Button(useSceneToggleImg, GUI.skin.label, GUILayout.Width(30f), GUILayout.Height(20f)))
                {
                    UnityLinkManager.USE_CURRENT_SCENE = !UnityLinkManager.USE_CURRENT_SCENE;
                    settings.useCurrentScene = UnityLinkManager.USE_CURRENT_SCENE;
                    SaveSettings();
                }
                GUIStyle conLabelText = UnityLinkManager.USE_CURRENT_SCENE ? styles.unselectedLabel : styles.selectedLabel;
                GUILayout.Label("Create new scene", conLabelText, GUILayout.Width(110f));
                GUILayout.EndHorizontal();


                //ADD_TO_TIMELINE
                GUILayout.BeginHorizontal();
                Texture2D timelineToggleImg = UnityLinkManager.ADD_TO_TIMELINE ? styles.toggleLeft : styles.toggleRight;
                if (GUILayout.Button(timelineToggleImg, GUI.skin.label, GUILayout.Width(30f), GUILayout.Height(20f)))
                {
                    UnityLinkManager.ADD_TO_TIMELINE = !UnityLinkManager.ADD_TO_TIMELINE;
                    settings.isClientLocal = UnityLinkManager.ADD_TO_TIMELINE;
                    SaveSettings();
                }
                GUIStyle timelineImp = UnityLinkManager.ADD_TO_TIMELINE ? styles.unselectedLabel : styles.selectedLabel;
                GUILayout.Label("Add to TimeLine", timelineImp, GUILayout.Width(100f));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Scene Ref", GUILayout.Width(90f));
                UnityLinkManager.SCENE_REFERENCE_STRING = GUILayout.TextField(UnityLinkManager.SCENE_REFERENCE_STRING, GUILayout.Width(180f));
                if (Event.current.type == EventType.Repaint)
                {
                    sceneRefTextField = GUILayoutUtility.GetLastRect();
                }
                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_dropdown_toggle").image, "Show previously used scene referecnes"), styles.minimalButton))
                {
                    TextFieldHistory.ShowAtPosition(new Rect(sceneRefTextField.x, sceneRefTextField.y, sceneRefTextField.width, sceneRefTextField.height), UpdateSceneRef, recentSceneRefs);
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Save Folder", GUILayout.Width(90f));
                if (GUILayout.Button(EditorGUIUtility.IconContent("d_FolderOpened Icon").image, GUILayout.Width(20f), GUILayout.Height(20f)))
                {
                    string initialFolder = string.IsNullOrEmpty(UnityLinkManager.TIMELINE_SAVE_FOLDER) ? "Assets" : UnityLinkManager.TIMELINE_SAVE_FOLDER;
                    string proposed = EditorUtility.OpenFolderPanel("Parent Folder For Scene and Assets", initialFolder, "");
                    if (!string.IsNullOrEmpty(proposed)) UnityLinkManager.TIMELINE_SAVE_FOLDER = proposed;
                }
                //EditorGUI.BeginDisabledGroup(true);
                string savePath = string.IsNullOrEmpty(UnityLinkManager.UNITY_FOLDER_PATH) ? "Assets" : UnityLinkManager.UNITY_FOLDER_PATH;
                EditorGUILayout.SelectableLabel(savePath, EditorStyles.textField, GUILayout.Width(180f), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                //GUILayout.TextField(UnityLinkManager.UNITY_FOLDER_PATH, GUILayout.Width(180f));
                //EditorGUI.EndDisabledGroup();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Create New Scene"))
                {
                    createSceneAfterGUI = true;
                }
                GUILayout.Space(8f);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
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
                GUILayout.Label(q.OpCode.ToString() + " " + q.EntryTime + " " + q.Exchange, styles.messageItemStyle);
                GUILayout.EndHorizontal();

                if (q.OpCode == UnityLinkManager.OpCodes.NOTIFY)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("  Notify message: " + q.Notify.Message, styles.messageItemStyle);
                    GUILayout.EndHorizontal();
                }

            }
            GUILayout.EndScrollView();
        }
        #endregion GUI

        #region TextFieldHistory
        public string[] RecordHistory(string[] array,  string newEntry, int max)
        {
            if (array == null)
            {
                array = new string[0];
            }

            if (array.Contains(newEntry))
            {
                string[] reorderedArray = new string[array.Length];
                reorderedArray[0] = newEntry;
                int pos = 1;
                foreach (string item in array)
                {
                    if (!item.Equals(newEntry))
                    {
                        reorderedArray[pos++] = item;
                    }
                }
                return reorderedArray;
            }

            if (array.Length < max) Array.Resize(ref array, array.Length + 1);

            string[] shiftedArray = new string[array.Length];
            shiftedArray[0] = newEntry;
            for (int i = 1; i < array.Length; i++)
            {
                shiftedArray[i] = array[i - 1];
            }
            return shiftedArray;
        }

        public void RecordConnectionHistory(string newEntry)
        {
            if (!UnityLinkManager.IS_CLIENT_LOCAL) lastTriedHosts = RecordHistory(lastTriedHosts, newEntry, 3);
            if (settings != null)
            {
                settings.lastTriedHosts = lastTriedHosts;
                settings.lastExportPath = UnityLinkManager.EXPORTPATH;
                SaveSettings();
            }
        }

        public void RecordSceneRefHistory(string newEntry)
        {
            if (UnityLinkManager.ADD_TO_TIMELINE) recentSceneRefs = RecordHistory(recentSceneRefs, newEntry, 6);
            if (settings != null)
            {
                settings.recentSceneRefs = recentSceneRefs;
                SaveSettings();
            }
        }
        
        public bool UpdateRemoteHostName(string newName) // a Func<string, bool> to use as a callback for the field history
        {
            remoteHost = newName;
            GUI.FocusControl("");
            if (control == Control.InValid) { control = Control.Idle; }
            Repaint();
            return true;
        }

        public bool UpdateSceneRef(string newName) // a Func<string, bool> to use as a callback for the field history
        {
            UnityLinkManager.SCENE_REFERENCE_STRING = newName;
            GUI.FocusControl("");
            Repaint();
            return true;
        }
    }

    public class TextFieldHistory : EditorWindow
    {
        static TextFieldHistory textFieldHistory = null;
        static long lastClosedTime;
        static Func<string, bool> CallBack;
        static string[] Contents;

        void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Close;
            hideFlags = HideFlags.DontSave;
        }

        void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Close;
            textFieldHistory = null;            
        }

        public static bool ShowAtPosition(Rect buttonRect, Func<string, bool> callBack, string[] contents) 
        {
            CallBack = callBack;
            Contents = contents;
            long nowMilliSeconds = System.DateTime.Now.Ticks / System.TimeSpan.TicksPerMillisecond;
            bool justClosed = nowMilliSeconds < lastClosedTime + 50;
            if (!justClosed)
            {
                //Event.current.Use();
                if (textFieldHistory == null)
                    textFieldHistory = ScriptableObject.CreateInstance<TextFieldHistory>();
                else
                {
                    textFieldHistory.Cancel();
                    return false;
                }

                textFieldHistory.Init(buttonRect);
                return true;
            }
            return false;
        }
        
        void Init(Rect buttonRect)
        {
            // Has to be done before calling Show / ShowWithMode
            buttonRect = GUIUtility.GUIToScreenRect(buttonRect);
            
            float width = buttonRect.width;
            float height = (Contents.Length > 0 ? buttonRect.height * Contents.Length : buttonRect.height) + 4f;

            Vector2 windowSize = new Vector2(width, height);
            ShowAsDropDown(buttonRect, windowSize);
        }

        void Cancel()
        {
            Close();
            GUI.changed = true;
            GUIUtility.ExitGUI();
        }

        public Styles styles;

        public class Styles
        {
            public GUIStyle linestyle;
            public Texture2D lineTex;
            public Vector4 border;

            public Styles()
            {
                lineTex = ImporterWindow.TextureColor(new Color(0.81f, 0.81f, 0.81f));
                border = new Vector4(1, 1, 1, 1);

                linestyle = new GUIStyle(GUI.skin.label);                
                linestyle.normal.textColor = Color.black;
                linestyle.active.textColor = Color.blue * 0.75f;
                linestyle.hover.textColor = Color.gray;
            }
        }

        public bool RunCallback(Func<string, bool> func, string imput)
        {
            bool b = func(imput);
            return b;
        }

        private void OnGUI()
        {
            if (styles == null)
                styles = new Styles();
            Rect r = new Rect(0,0,position.width,position.height);  
            GUI.DrawTexture(r, styles.lineTex, ScaleMode.StretchToFill, false, 1f, Color.black, styles.border, Vector4.zero);
            GUI.DrawTexture(r, styles.lineTex);

            GUILayout.BeginVertical();
            if (Contents.Length > 0)
            {
                foreach (string content in Contents)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(content, styles.linestyle))
                    {
                        RunCallback(CallBack, content);
                        Close();
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndVertical();
        }
    }
    #endregion TextFieldHistory
}