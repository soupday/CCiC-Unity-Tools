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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

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
        public static RLSettingsObject settings;
        private void OnEnable()
        {
            EditorApplication.quitting -= QuitCleanup;
            EditorApplication.quitting += QuitCleanup;

            EditorApplication.playModeStateChanged -= OnPlayModeChange;
            EditorApplication.playModeStateChanged += OnPlayModeChange;

            Instance = this;
            foldoutControlArea = true;
            foldoutSceneArea = true;

            FetchSettings();
            //CreateTreeView();

            UnityLinkManager.ClientConnected -= ConnectedToserver;
            UnityLinkManager.ClientConnected += ConnectedToserver;
            UnityLinkManager.ClientDisconnected -= DisconnectedFromServer;
            UnityLinkManager.ClientDisconnected += DisconnectedFromServer;

            AttemptAutoReconnect();
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

        private void OnPlayModeChange(PlayModeStateChange stateChange)
        {
            if (stateChange == PlayModeStateChange.ExitingEditMode)
            {
                UnityLinkManager.DisconnectFromServer();
            }
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
            if (ImporterWindow.Current == null) return;

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

            if (settings == null)
            {
                //Debug.LogWarning("getting settings directly");
                settings = RLSettings.FindRLSettingsObject();
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

                // import area
                UnityLinkManager.SIMPLE_MODE = settings.simpleMode;
                UnityLinkManager.IMPORT_DESTINATION_FOLDER = settings.importDestinationFolder;
                UnityLinkManager.IMPORT_INTO_SCENE = settings.importIntoScene;
                UnityLinkManager.USE_CURRENT_SCENE = settings.useCurrentScene;
                UnityLinkManager.ADD_TO_TIMELINE = settings.addToTimeline;
                UnityLinkManager.LOCK_TIMELINE_TO_LAST_USED = settings.lockTimelineToLast;

                if (!string.IsNullOrEmpty(settings.sceneReference))
                    UnityLinkManager.TIMELINE_REFERENCE_STRING = settings.sceneReference;

                if (settings.recentSceneRefs != null)
                    recentSceneRefs = settings.recentSceneRefs;
                else
                    recentSceneRefs = new string[0];

                if (!string.IsNullOrEmpty(settings.lastSaveFolder))
                    UnityLinkManager.TIMELINE_SAVE_FOLDER = settings.lastSaveFolder;
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

        // Automated reconnection for assembly reloads        
        public static void AttemptAutoReconnect()
        {
            //Debug.Log("OnEnable - AutoReconnect");
            if (IsConnectedTimeStampWithin(new TimeSpan(0, 5, 0)) && !EditorApplication.isPlaying)
            {
                //Debug.Log("OnEnable - Attempting to reconnect");
                // mimic of the connect button's UI control elements (connectInProgress + control vars)
                // all of the required info recoverred from settings by FetchSettings().
                UnityLinkManager.reconnect = false;
                connectInProgress = true;                
                GUI.FocusControl("connectButton");
                if (!UnityLinkManager.IS_CLIENT_LOCAL) control = Control.Validating;
                EditorApplication.delayCall += StartClient;
            }
            else
            {
                //Debug.Log("OnEnable - Not AutoReconnect-ing");
            }
        }

        static bool IsConnectedTimeStampWithin(TimeSpan interval)
        {
            if (EditorPrefs.HasKey(UnityLinkManager.connectPrefString))
            {
                string stampString = EditorPrefs.GetString(UnityLinkManager.connectPrefString);
                long.TryParse(stampString, out long timestamp);
                DateTime connectedTime = new DateTime(timestamp);
                DateTime now = DateTime.Now.ToLocalTime();
                if (connectedTime + interval > now)
                {
                    EditorPrefs.SetString(UnityLinkManager.connectPrefString, "0");
                    return true;
                } else return false;
            } else return false;
        }

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
            //Debug.LogWarning("Starting StartConnection ");
            // if a remote connection is needed then validate the address string as IP
            // if the address string cant be validated check if it can be resolved by Dns
            //UnityLinkManager.IS_CLIENT_LOCAL = isClientLocal;
            if (!UnityLinkManager.IS_CLIENT_LOCAL)
            {
                UnityLinkManager.NotifyInternalQueue($"Attempting connection to {remoteHost}");
                bool valid = false;
                control = Control.Validating;

                Debug.Log("Testing host");

                // validate IP address
                if (IPAddress.TryParse(remoteHost, out IPAddress ip))
                {
                    Debug.Log("Supplied IP address is a valid IPv4 address.");
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
                        //Debug.Log("Trying Dns resolution (async)");
                        var addr = Dns.GetHostAddressesAsync(remoteHost);
                        bool isTaskComplete = addr.Wait(3000);
                        if (isTaskComplete)
                        {
                            IPAddress[] hosts = addr.Result;
                            if (hosts.Length > 0)
                            {
                                //Debug.LogWarning("Dns name resolves (async)");

                                // ensure the IPv4 address is being used (eg 127.0.0.1 rather than ::1 - which would map to 0.0.0.1) 
                                IPAddress remote = hosts.FirstOrDefault(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).MapToIPv4();

                                if (remote != null)
                                {
                                    UnityLinkManager.REMOTE_HOST = remote.ToString();
                                    UnityLinkManager.NotifyInternalQueue($"Hostname: {remoteHost} resolves to {UnityLinkManager.REMOTE_HOST}");
                                    valid = true;
                                }
                                else
                                {
                                    UnityLinkManager.NotifyInternalQueue($"DNS resolution of hostname: {remoteHost} could not find a valid IPv4 address.");
                                    Debug.LogWarning($"Hostname: {remoteHost} does not resolve to a vaid IPv4 address.");
                                    valid = false;
                                    control = Control.InValid;
                                }
                            }
                        }
                        else // Task timed out
                        {
                            UnityLinkManager.NotifyInternalQueue($"DNS resolution of hostname: {remoteHost} timed out.");
                            Debug.LogWarning($"Dns name resolution of hostname: {remoteHost} timed out.");
                        }
                    }
                    catch
                    {
                        UnityLinkManager.NotifyInternalQueue($"Error resolving hostname: {remoteHost}");
                        Debug.LogWarning($"Error resolving hostname: {remoteHost}");
                        valid = false;
                        control = Control.InValid;
                    }
                }
                connectInProgress = valid;
                //Debug.LogWarning("connectInProgress = " + connectInProgress);
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
                    settings.lastSuccessfulHost = remoteHost; // UnityLinkManager.REMOTE_HOST; 
                    SaveSettings();
                }
            }
            else
            {
                Debug.LogWarning("No settings available");
            }

            Debug.LogWarning("Connected to server");
        }

        static void DisconnectedFromServer(object sender, EventArgs e)
        {
            UnityLinkManager.ClientDisconnected -= DisconnectedFromServer;

            UnityLinkManager.NotifyInternalQueue("Connection returning to idle state.");
            control = Control.Idle;
            connectInProgress = false;

            RepaintOnUpdate(stop: true);
            UnityLinkManager.StopQueue();

            Debug.LogWarning("Disconnected from server");
        }

        static void OnImportStarted(object sender, EventArgs e)
        {
            UnityLinkImporter.ImportStarted -= OnImportStarted;
            //Debug.LogWarning("Import Started Event");
            if (UnityLinkManager.ADD_TO_TIMELINE)
            {
                if (settings != null)
                {
                    //Debug.LogWarning("Import Started Event  settings != null");                    
                }
                else
                {
                    //Debug.LogWarning("Import Started Event  settings == null");
                }
            }
        }

        #endregion Connection Management

        #region GUI
        bool foldoutControlArea;
        bool foldoutSceneArea;
        bool foldoutLogArea;

        float PADDING = 4f;
        //float CONTROL_HEIGHT = 100f;
        //float SCENE_HEIGHT = 100f;
        //float MESSAGE_HEIGHT = 100f;

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
            public Texture2D newSceneTex;

            public GUIStyle textFieldStyle;
            public GUIStyle messageItemStyle;
            public GUIStyle FoldoutTitleLabel;
            public GUIStyle smallTitleLabel;
            public GUIStyle unselectedLabel;
            public GUIStyle selectedLabel;
            public GUIStyle whiteLabel;
            public GUIStyle highlightLabel;

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
                newSceneTex = Util.FindTexture(folders, "RLIcon-NewScene");

                textFieldStyle = new GUIStyle(EditorStyles.textField);
                textFieldStyle.wordWrap = true;

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

                smallTitleLabel = new GUIStyle(GUI.skin.label);
                smallTitleLabel.normal.textColor = new Color(0.34f, 0.61f, 0.84f);
                smallTitleLabel.onNormal.textColor = new Color(0.34f, 0.61f, 0.84f);
                smallTitleLabel.hover.textColor = new Color(0.34f, 0.61f, 0.84f);
                smallTitleLabel.onHover.textColor = new Color(0.34f, 0.61f, 0.84f);
                smallTitleLabel.fontStyle = FontStyle.BoldAndItalic;

                unselectedLabel = new GUIStyle(GUI.skin.label);
                unselectedLabel.normal.textColor = Color.white * 0.75f;

                selectedLabel = new GUIStyle(GUI.skin.label);
                selectedLabel.normal.textColor = new Color(0.5f, 0.75f,  0.06f);
                selectedLabel.onNormal.textColor = new Color(0.5f, 0.75f, 0.06f);
                selectedLabel.hover.textColor = new Color(0.5f, 0.75f, 0.06f);
                selectedLabel.onHover.textColor = new Color(0.5f, 0.75f, 0.06f);


                whiteLabel = new GUIStyle(GUI.skin.label);
                whiteLabel.normal.textColor = Color.white;
                whiteLabel.normal.background = Texture2D.grayTexture;

                highlightLabel = new GUIStyle(GUI.skin.label);
                highlightLabel.normal.textColor = Color.cyan;
                highlightLabel.onNormal.textColor = Color.cyan;
                highlightLabel.hover.textColor = Color.cyan;
                highlightLabel.onHover.textColor = Color.cyan;

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
            Disconnecting,
            Connecting,
            Connected
        }

        Vector2 globalScrollPos = new Vector2();
        public void ShowGUI(Rect containerRect)
        {
            if (settings == null)
                FetchSettings();

            if (styles == null)
                styles = new Styles();

            if (UnityLinkManager.activityQueue == null)
                UnityLinkManager.activityQueue = new List<UnityLinkManager.QueueItem>();
            
            GUILayout.BeginVertical();
            using (var scrollViewScope = new GUILayout.ScrollViewScope(globalScrollPos, GUILayout.ExpandWidth(true)))
            {
                globalScrollPos = scrollViewScope.scrollPosition;

                GUILayout.Space(PADDING);               

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
                foldoutSceneArea = EditorGUILayout.Foldout(foldoutSceneArea, "Import controls", true, styles.FoldoutTitleLabel);
                if (foldoutSceneArea)
                {
                    ImportControlsGUI();
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
                    ImportLogsGUI();
                }
                GUILayout.Space(PADDING);
                GUILayout.EndVertical();

                GUILayout.Space(PADDING);
                GUILayout.EndHorizontal();

                GUILayout.Space(PADDING);
            }
            GUILayout.EndVertical();
            
            if (createSceneAfterGUI)
            {
                EditorApplication.delayCall += OpenEmptyPreviewScene;
                createSceneAfterGUI = false;
            }
        }

        void ResetTreeviewOnSceneChange()
        {
            EditorSceneManager.activeSceneChangedInEditMode -= ResetTreeview;
            EditorSceneManager.activeSceneChangedInEditMode += ResetTreeview;
        }

        void ResetTreeview(Scene fromScene, Scene toScene)
        {
            EditorSceneManager.activeSceneChangedInEditMode -= ResetTreeview;
            CreateTreeView();
        }

        void OpenEmptyPreviewScene()
        {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            GameObject.Instantiate(Util.FindPreviewScenePrefab(), Vector3.zero, Quaternion.identity);

            WindowManager.previewSceneHandle = scene;
            WindowManager.previewScene = PreviewScene.FetchPreviewScene(scene);

            WindowManager.previewScene.PostProcessingAndLighting();            
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
                    control = Control.Disconnecting;
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
            if (string.IsNullOrEmpty(UnityLinkManager.EXPORTPATH))
            {
                UnityLinkManager.EXPORTPATH = UnityLinkManager.DEFAULT_EXPORTPATH;
                settings.lastExportPath = UnityLinkManager.DEFAULT_EXPORTPATH;
            }
            bool linkPathExists = Directory.Exists(UnityLinkManager.EXPORTPATH);
            string linkPath = linkPathExists ? UnityLinkManager.EXPORTPATH : "Please select a valid folder";
            
            GUILayout.Label(new GUIContent("Export Folder", "Folder to use as a working directory for file exchange (Try to keep this folder OUTSIDE the Unity project's Assets folder)."), GUILayout.Width(85f));
            if (GUILayout.Button(EditorGUIUtility.IconContent("d_FolderOpened Icon").image, GUILayout.Width(20f), GUILayout.Height(20f)))
            {   
                string proposed = EditorUtility.SaveFolderPanel("Working directory for file export (KEEP OUTISDE UNITY PROJECT)", Path.GetDirectoryName(UnityLinkManager.EXPORTPATH), UnityLinkManager.EXPORTPATH);//
                if (!string.IsNullOrEmpty(proposed))
                {
                    UnityLinkManager.EXPORTPATH = proposed;
                    settings.lastExportPath = proposed;
                    linkPath = proposed;
                    GUI.FocusControl("");
                }
            }
            
            EditorGUILayout.SelectableLabel(linkPath, linkPathExists ? styles.normalTextField : styles.errorTextField, GUILayout.MinWidth(100f), GUILayout.MaxWidth(165f), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            //UnityLinkManager.EXPORTPATH = GUILayout.TextField(UnityLinkManager.EXPORTPATH, GUILayout.MinWidth(100f), GUILayout.MaxWidth(150f));            
            GUILayout.EndHorizontal();

            GUILayout.Space(3f);
            GUILayout.BeginHorizontal();
            GUILayout.Space(10f);
            GUILayout.Box("", styles.delimBox, GUILayout.Width(250f), GUILayout.Height(2f));
            GUILayout.EndHorizontal();
            GUILayout.Space(3f);

            GUILayout.BeginHorizontal();
#if UNITY_2021_1_OR_NEWER

#else
            EditorGUI.BeginDisabledGroup(true);
            UnityLinkManager.IS_CLIENT_LOCAL = true;
#endif
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
#if UNITY_2021_1_OR_NEWER

#else
            EditorGUI.EndDisabledGroup();
            UnityLinkManager.IS_CLIENT_LOCAL = true;
#endif
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
                case Control.Disconnecting:
                    {
                        statusText += "Disconnecting...";
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
#if UNITY_6000_2_OR_NEWER
        TreeViewState<int> treeViewState;
#else
        TreeViewState treeViewState;
#endif
        TimeLineTreeView timeLineTreeViewState;
        bool havetreeView = false;
        float TIMELINE_BOX_W = 348f;
        float TIMELINE_BOX_H = 28f;
        public static Rect timeLinesRect;
        public static Rect timeLinesBoxRect;
        public static Rect timeLinesAssetBoxRect;
        float lastTLTreeViewHeight;

        void ImportControlsGUI()
        {
            ControlsSelectModeGUI();
            ControlsTextBoxGUI();
            if (UnityLinkManager.SIMPLE_MODE) 
                ImportSimpleControlsGUI();
            else
                ImportAdvancedControlsGUI();
        }

        void ControlsSelectModeGUI()
        {
            GUILayout.Space(2f);

            GUILayout.BeginHorizontal();
            GUIStyle simpleImp = UnityLinkManager.SIMPLE_MODE ? styles.selectedLabel : styles.unselectedLabel;
            GUILayout.Label("Simple Mode", simpleImp, GUILayout.Width(90f));
            Texture2D sceneImpToggleImg = UnityLinkManager.SIMPLE_MODE ? styles.toggleLeft : styles.toggleRight;
            if (GUILayout.Button(sceneImpToggleImg, GUI.skin.label, GUILayout.Width(30f), GUILayout.Height(20f)))
            {
                UnityLinkManager.SIMPLE_MODE = !UnityLinkManager.SIMPLE_MODE;
                settings.simpleMode = UnityLinkManager.SIMPLE_MODE;
                if (UnityLinkManager.SIMPLE_MODE)
                {
                    UnityLinkManager.IMPORT_INTO_SCENE = true;
                    settings.importIntoScene = UnityLinkManager.IMPORT_INTO_SCENE;
                    UnityLinkManager.ADD_TO_TIMELINE = true;
                    settings.addToTimeline = UnityLinkManager.ADD_TO_TIMELINE;
                    UnityLinkManager.LOCK_TIMELINE_TO_LAST_USED = true;
                    settings.lockTimelineToLast = UnityLinkManager.LOCK_TIMELINE_TO_LAST_USED;
                    
                }
                SaveSettings();
            }
            GUIStyle advImp = UnityLinkManager.SIMPLE_MODE ? styles.unselectedLabel : styles.selectedLabel;
            GUILayout.Label("Advanced Mode", advImp, GUILayout.Width(110f));
            GUILayout.EndHorizontal();
        }

        void ControlsTextBoxGUI()
        {
            GUILayout.Space(2f);

            GUILayout.BeginHorizontal();
            int lines = 2;
            string text = string.Empty;
            //string sceneText = UnityLinkManager.USE_CURRENT_SCENE ? "- Assets will be imported and placed into the current scene." : "- A new scene will be created and assets will be imported and placed into the new scene.";
            text += UnityLinkManager.IMPORT_INTO_SCENE ? "- Assets will be imported and will also be placed in the current scene." : "- Assets will be imported into the project.";
            if (UnityLinkManager.IMPORT_INTO_SCENE)
            {
                string simpleTimeline = UnityLinkManager.SIMPLE_MODE ? "\n- Imported Assets will be automatically added to a Unity Timeline object." : "\n- Imported Assets will be added to the selected Unity Timeline object, or automatically created if unselected.";
                text += UnityLinkManager.ADD_TO_TIMELINE ? simpleTimeline : "";
                lines += UnityLinkManager.ADD_TO_TIMELINE ? 2 : 0;
            }

            EditorGUILayout.SelectableLabel(text, styles.textFieldStyle, GUILayout.Width(360f), GUILayout.Height(EditorGUIUtility.singleLineHeight * lines));

            GUILayout.EndHorizontal();
        }

        void ImportSimpleControlsGUI()
        {
            GUILayout.Space(2f);

            // IMPORT_INTO_SCENE
            GUILayout.BeginHorizontal();
            Texture2D sceneImpToggleImg = UnityLinkManager.IMPORT_INTO_SCENE ? styles.toggleRight : styles.toggleLeft;
            if (GUILayout.Button(sceneImpToggleImg, GUI.skin.label, GUILayout.Width(30f), GUILayout.Height(20f)))
            {
                UnityLinkManager.IMPORT_INTO_SCENE = !UnityLinkManager.IMPORT_INTO_SCENE;
                UnityLinkManager.ADD_TO_TIMELINE = UnityLinkManager.IMPORT_INTO_SCENE;
                settings.importIntoScene = UnityLinkManager.IMPORT_INTO_SCENE;
                settings.addToTimeline = UnityLinkManager.ADD_TO_TIMELINE;
                SaveSettings();
            }
            GUIStyle sceneImp = UnityLinkManager.IMPORT_INTO_SCENE ? styles.selectedLabel : styles.unselectedLabel;
            GUILayout.Label("Import Into Scene and Timeline", sceneImp, GUILayout.Width(220f));
            GUILayout.EndHorizontal();
        }

        void ImportAdvancedControlsGUI()
        { 
            GUILayout.Space(2f);

            GUILayout.BeginHorizontal();
            string importTooltip = "Folder within the Unity project to use as the parent import folder for live link imports.";
            GUILayout.Label(new GUIContent("Import Folder", importTooltip), GUILayout.Width(85f));
            if (string.IsNullOrEmpty(UnityLinkManager.IMPORT_DESTINATION_FOLDER)) UnityLinkManager.IMPORT_DESTINATION_FOLDER = UnityLinkManager.IMPORT_DEFAULT_DESTINATION_FOLDER;
            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_FolderOpened Icon").image, importTooltip), GUILayout.Width(20f), GUILayout.Height(20f)))
            {
                string initialFolder = string.IsNullOrEmpty(UnityLinkManager.IMPORT_DESTINATION_FOLDER) ? UnityLinkManager.IMPORT_DEFAULT_DESTINATION_FOLDER : UnityLinkManager.IMPORT_DESTINATION_FOLDER;
                string proposed = EditorUtility.OpenFolderPanel("Import folder for assets [MUST BE INSIDE PROJECT]", initialFolder, "");
                if (!string.IsNullOrEmpty(proposed) && proposed.StartsWith(Application.dataPath))
                {
                    UnityLinkManager.IMPORT_DESTINATION_FOLDER = proposed;
                    settings.importDestinationFolder = proposed;
                }
            }
            // display only
            bool importPathExists = Directory.Exists(UnityLinkManager.IMPORT_DESTINATION_FOLDER);
            string importPath = UnityLinkManager.IMPORT_DESTINATION_FOLDER.StartsWith(Application.dataPath) ? UnityLinkManager.IMPORT_DESTINATION_FOLDER.FullPathToUnityAssetPath() : UnityLinkManager.IMPORT_DESTINATION_FOLDER;
            EditorGUILayout.SelectableLabel(importPath, importPathExists ? styles.normalTextField : styles.errorTextField, GUILayout.Width(247f), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            GUILayout.EndHorizontal();

            GUILayout.Space(2f);            

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
                GUILayout.Space(2f);
                GUILayout.BeginHorizontal();
                /*
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
                */
                string stdTip = "Create a new scene using Unity's default scene creation tool";
                GUILayout.Label(new GUIContent("Create new scene", stdTip), GUILayout.Width(110f));
                if (GUILayout.Button(new GUIContent(styles.newSceneTex, stdTip), styles.minimalButton, GUILayout.Height(EditorGUIUtility.singleLineHeight), GUILayout.Width(EditorGUIUtility.singleLineHeight)))
                {
                    ResetTreeviewOnSceneChange();
                    EditorApplication.ExecuteMenuItem("File/New Scene");
                }
                GUILayout.Space(20f);
                EditorGUI.BeginDisabledGroup(false);
                string exTip = "Create a new scene that mimics iClone & Character Creator's lighting setup";
                GUILayout.Label(new GUIContent("Create preview scene", exTip), GUILayout.Width(130f));
                if (GUILayout.Button(new GUIContent(styles.newSceneTex, exTip), styles.minimalButton, GUILayout.Height(EditorGUIUtility.singleLineHeight), GUILayout.Width(EditorGUIUtility.singleLineHeight)))
                {
                    ResetTreeviewOnSceneChange();
                    createSceneAfterGUI = true;
                }
                EditorGUI.EndDisabledGroup();
                GUILayout.EndHorizontal();

                GUILayout.Space(2f);

                //ADD_TO_TIMELINE
                GUILayout.BeginHorizontal();
                Texture2D timelineToggleImg = UnityLinkManager.ADD_TO_TIMELINE ? styles.toggleRight : styles.toggleLeft;
                if (GUILayout.Button(timelineToggleImg, GUI.skin.label, GUILayout.Width(30f), GUILayout.Height(20f)))
                {
                    UnityLinkManager.ADD_TO_TIMELINE = !UnityLinkManager.ADD_TO_TIMELINE;
                    settings.addToTimeline = UnityLinkManager.ADD_TO_TIMELINE;
                    SaveSettings();
                }
                GUIStyle timelineImp = UnityLinkManager.ADD_TO_TIMELINE ? styles.selectedLabel : styles.unselectedLabel;
                GUILayout.Label("Add to TimeLine", timelineImp, GUILayout.Width(100f));
                if (UnityLinkManager.ADD_TO_TIMELINE)
                {
                    GUILayout.Space(194f);
                    if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Refresh").image, "Re-detect TimeLine objects in the current scene."), styles.minimalButton))
                    {
                        CreateTreeView();
                    }
                }
                GUILayout.EndHorizontal();

                if (UnityLinkManager.ADD_TO_TIMELINE)
                {
                    //if (sceneTimelines.Count > 0)
                    //{
                        TIMELINE_BOX_H = lastTLTreeViewHeight + 28f;
                        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(TIMELINE_BOX_W), GUILayout.Height(TIMELINE_BOX_H));
                        GUILayout.Label("Existing Timelines in scene [Toggle to use]", styles.smallTitleLabel);
                        
                        //if (Event.current.type == EventType.Repaint) { timeLinesBoxRect = GUILayoutUtility.GetLastRect(); }
                        //Rect boundingBox = new Rect(timeLinesBoxRect.x -4f, timeLinesBoxRect.y -3f, TIMELINE_BOX_W, TIMELINE_BOX_H);
                        //GUI.DrawTexture(boundingBox, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 1f, Color.gray, new Vector4(1, 1, 1, 1), Vector4.zero);
                                                
                        GUILayout.Space(0f);
                        // TreeView for existing TimeLines
                        if (Event.current.type == EventType.Repaint){timeLinesRect = GUILayoutUtility.GetLastRect();}

                        if (!havetreeView || timeLineTreeViewState == null) havetreeView = CreateTreeView();
                        
                        timeLineTreeViewState.OnGUI(new Rect(timeLinesRect.x, timeLinesRect.y + 4f, TIMELINE_BOX_W - 6f, lastTLTreeViewHeight + 2f));
                        lastTLTreeViewHeight = timeLineTreeViewState.totalHeight;

                        /*
                        SceneTimelines change = null;
                        foreach(var t in sceneTimelines)  // turn into treeview
                        {
                            GUILayout.BeginHorizontal();
                            if (GUILayout.Button(t.PlayableDirector.playableAsset.name, t.IsSelected ? styles.selectedLabel : styles.whiteLabel))
                            {
                                change = t;
                                Selection.activeObject = t.PlayableDirector.playableAsset;
                            }
                            if (t.IsSelected)
                            {
                                GUILayout.Label("[Adding to this Timeline]", styles.highlightLabel);
                            }
                            GUILayout.EndHorizontal();
                        }

                        if (change != null)
                        {
                            useNewSceneRef = false;
                            foreach (var t in sceneTimelines)
                            {
                                t.IsSelected = false;
                            }
                            change.IsSelected = true;
                        }
                        */
                        // Bounding box for treeview timeline section
                        GUILayout.EndVertical();
                        if (Event.current.type == EventType.Repaint) { timeLinesBoxRect = GUILayoutUtility.GetLastRect(); }                        
                        GUI.DrawTexture(timeLinesBoxRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 1f, Color.gray * 0.85f, new Vector4(1, 1, 1, 1), Vector4.zero);
                    //}

                    // LOCK_TIMELINE_TO_LAST_USED
                    GUILayout.BeginHorizontal();
                    string lockTooltip = "This will focus the timeline window onto the timeline that was used by the last import";
                    Texture2D timelineLockImg = UnityLinkManager.LOCK_TIMELINE_TO_LAST_USED ? styles.toggleRight : styles.toggleLeft;
                    if (GUILayout.Button(new GUIContent(timelineLockImg, lockTooltip), GUI.skin.label, GUILayout.Width(30f), GUILayout.Height(20f)))
                    {
                        UnityLinkManager.LOCK_TIMELINE_TO_LAST_USED = !UnityLinkManager.LOCK_TIMELINE_TO_LAST_USED;
                        settings.lockTimelineToLast = UnityLinkManager.LOCK_TIMELINE_TO_LAST_USED;
                        SaveSettings();
                    }
                    GUIStyle timelineLock = UnityLinkManager.LOCK_TIMELINE_TO_LAST_USED ? styles.selectedLabel : styles.unselectedLabel;
                    GUILayout.Label(new GUIContent("Lock timeline to last used timeline object", lockTooltip), timelineLock, GUILayout.Width(240f));
                    GUILayout.EndHorizontal();

                    GUILayout.Space(4f);

                    //EditorGUI.BeginDisabledGroup(timeLineTreeViewState.tracked.FindAll(x => x.Active == true).Count > 0);
                    GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(TIMELINE_BOX_W));
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Timeline asset creation", styles.smallTitleLabel);
                    GUILayout.EndHorizontal();

                    GUILayout.Space(4f);

                    GUILayout.BeginHorizontal();

                    bool mustChangeName = UnityLinkManager.TIMELINE_REFERENCE_STRING == UnityLinkManager.TIMELINE_DEFAULT_REFERENCE_STRING;
                    GUILayout.Label("Timeline Name", GUILayout.Width(100f));
                    UnityLinkManager.TIMELINE_REFERENCE_STRING = GUILayout.TextField(UnityLinkManager.TIMELINE_REFERENCE_STRING, mustChangeName ? styles.errorTextField : styles.normalTextField, GUILayout.Width(180f));
                    if (Event.current.type == EventType.Repaint) { sceneRefTextField = GUILayoutUtility.GetLastRect(); }
                    if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_dropdown_toggle").image, "Show previously used scene referecnes"), styles.minimalButton))
                    {
                        TextFieldHistory.ShowAtPosition(new Rect(sceneRefTextField.x, sceneRefTextField.y, sceneRefTextField.width, sceneRefTextField.height), UpdateSceneRef, recentSceneRefs);
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.Space(2f);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Save Folder", GUILayout.Width(90f));
                    if (string.IsNullOrEmpty(UnityLinkManager.TIMELINE_SAVE_FOLDER)) UnityLinkManager.TIMELINE_SAVE_FOLDER = UnityLinkManager.TIMELINE_DEFAULT_SAVE_FOLDER;
                    if (GUILayout.Button(EditorGUIUtility.IconContent("d_FolderOpened Icon").image, GUILayout.Width(20f), GUILayout.Height(20f)))
                    {
                        string initialFolder = string.IsNullOrEmpty(UnityLinkManager.TIMELINE_SAVE_FOLDER) ? UnityLinkManager.TIMELINE_DEFAULT_SAVE_FOLDER : UnityLinkManager.TIMELINE_SAVE_FOLDER;
                        string proposed = EditorUtility.OpenFolderPanel("Folder For TimeLine Asset [MUST BE INSIDE PROJECT]", initialFolder, "");
                        if (!string.IsNullOrEmpty(proposed) && proposed.StartsWith(Application.dataPath))
                        {
                            UnityLinkManager.TIMELINE_SAVE_FOLDER = proposed;
                            settings.lastSaveFolder = proposed;
                        }
                    }
                    // display only
                    bool savePathExists = Directory.Exists(UnityLinkManager.TIMELINE_SAVE_FOLDER);
                    string savePath = UnityLinkManager.TIMELINE_SAVE_FOLDER.StartsWith(Application.dataPath) ? UnityLinkManager.TIMELINE_SAVE_FOLDER.FullPathToUnityAssetPath() : UnityLinkManager.TIMELINE_SAVE_FOLDER;
                    EditorGUILayout.SelectableLabel(savePath, savePathExists ? styles.normalTextField : styles.errorTextField, GUILayout.Width(180f), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    GUILayout.EndHorizontal();

                    GUILayout.Space(4f);

                    GUILayout.BeginHorizontal();
                    bool cantCreateTimeline = string.IsNullOrEmpty(UnityLinkManager.TIMELINE_SAVE_FOLDER) || string.IsNullOrEmpty(UnityLinkManager.TIMELINE_SAVE_FOLDER) || UnityLinkManager.TIMELINE_REFERENCE_STRING == UnityLinkManager.TIMELINE_DEFAULT_REFERENCE_STRING;
                    EditorGUI.BeginDisabledGroup(cantCreateTimeline);
                    string timTip = "Create a new timeline object in the scene with a timeline asset names and pathed as specified";
                    GUILayout.Label(new GUIContent("Create Timeline asset in scene", timTip), GUILayout.Width(180f));
                    if (GUILayout.Button(new GUIContent(styles.newSceneTex, timTip), styles.minimalButton, GUILayout.Height(EditorGUIUtility.singleLineHeight), GUILayout.Width(EditorGUIUtility.singleLineHeight)))
                    {
                        RecordAssetCreationSettings();
                        UnityLinkSceneManagement.CreateTimelineAsset();
                        CreateTreeView();
                    }
                    EditorGUI.EndDisabledGroup();
                    GUILayout.EndHorizontal();

                    GUILayout.Space(4f);
                    GUILayout.EndVertical();
                    if (Event.current.type == EventType.Repaint) { timeLinesAssetBoxRect = GUILayoutUtility.GetLastRect(); }
                    GUI.DrawTexture(timeLinesAssetBoxRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 1f, Color.gray * 0.85f, new Vector4(1, 1, 1, 1), Vector4.zero);
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
        }

        public bool CreateTreeView()
        {
#if UNITY_2023_OR_NEWER
            PlayableDirector[] playableDirectors = GameObject.FindObjectsByType<PlayableDirector>(FindObjectsSortMode.None);
#else
            PlayableDirector[] playableDirectors = GameObject.FindObjectsOfType<PlayableDirector>();
#endif

#if UNITY_6000_2_OR_NEWER
            treeViewState = new TreeViewState<int>();
#else
            treeViewState = new TreeViewState();
#endif
            timeLineTreeViewState = new TimeLineTreeView(treeViewState, playableDirectors);
            return true;
        }

        void RecordAssetCreationSettings()
        {
            settings.sceneReference = UnityLinkManager.TIMELINE_REFERENCE_STRING;            
            settings.lastSaveFolder = UnityLinkManager.TIMELINE_SAVE_FOLDER;
            Instance.RecordSceneRefHistory(UnityLinkManager.TIMELINE_REFERENCE_STRING);
        }

        Vector2 logScrollPos = new Vector2();

        void ImportLogsGUI()
        {
            List<UnityLinkManager.QueueItem> guiQueue = new List<UnityLinkManager.QueueItem>();

            for (int i = 0; i < UnityLinkManager.activityQueue.Count; i++)
            {
                guiQueue.Add(UnityLinkManager.activityQueue[i]);
            }

            GUILayout.Space(6f);

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
            //if (UnityLinkManager.ADD_TO_TIMELINE) 
            recentSceneRefs = RecordHistory(recentSceneRefs, newEntry, 6);
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
            UnityLinkManager.TIMELINE_REFERENCE_STRING = newName;
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