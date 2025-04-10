using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Reallusion.Import
{

    public class UnityLinkManagerWindow : EditorWindow
    {
        public static UnityLinkManagerWindow Instance;

        //[MenuItem("Reallusion/Live Link Manager")]
        //public static void OpenLinkManagerWindow()
        //{
        //    OpenWindow();
        //}

        //[MenuItem("Reallusion/Live Link Manager", true)]
        //public static bool ValidateOpenLinkManagerWindow()
        //{
        //    return !EditorWindow.HasOpenInstances<UnityLinkManagerWindow>();
        //}

        public static void OpenWindow()
        {
            if (EditorWindow.HasOpenInstances<UnityLinkManagerWindow>())
            {
                if (EditorWindow.HasOpenInstances<ImporterWindow>())
                {
                    ImporterWindow.Current.activeTab = 2;
                    ImporterWindow.Current.Repaint();
                }
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
            public Texture2D toggleLeft;
            public Texture2D toggleRight;
            public Texture2D linkOn;
            public Texture2D linkOff;

            public GUIStyle queueItemStyle;
            public GUIStyle FoldoutTitleLabel;
            public GUIStyle unselectedLabel;
            public GUIStyle selectedLabel;
            public GUIStyle statusLabel;
            public GUIStyle minimalButton;
            public GUIStyle normalTextField;
            public GUIStyle errorTextField;

            public Styles()
            {
                string[] folders = new string[] { "Assets", "Packages" };
                toggleLeft = Util.FindTexture(folders, "RLIcon_Toggle_L");
                toggleRight = Util.FindTexture(folders, "RLIcon_Toggle_R");
                linkOn = Util.FindTexture(folders, "RLIcon_Link_ON");
                linkOff = Util.FindTexture(folders, "RLIcon_Link_OFF");

                queueItemStyle = new GUIStyle(GUI.skin.box);
                queueItemStyle.normal.textColor = Color.yellow;

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

                minimalButton = new GUIStyle(GUI.skin.label);
                minimalButton.padding = new RectOffset(0, 0, 1, 0);

                normalTextField = new GUIStyle(EditorStyles.textField);

                errorTextField = new GUIStyle(EditorStyles.textField);
                errorTextField.normal.background = ImporterWindow.TextureColor(Color.red * 0.35f);
            }
        }

        private void OnEnable()
        {
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
            UnityLinkManager.DisconnectAndStopServer();
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
                remoteHost = settings.lastSuccessfulHost;
                isClientLocal = settings.isClientLocal;
                if (settings.lastTriedHosts != null)
                    lastTriedHosts = settings.lastTriedHosts;
                else
                    lastTriedHosts = new string[0];
            }
        }

        private static void SaveSettings()
        {
            RLSettings.SaveRLSettingsObject(settings);
        }

        static bool isClientLocal = true;
        static string remoteHost = string.Empty;
        static string[] lastTriedHosts = new string[0];
        static bool connectInProgress = false;

        static RLSettingsObject settings;
        
        #region examples
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

        ExampleTabbedUIWindow exampleWindow;

        void ShowTabbedUI(Rect areaRect)
        {
            if (exampleWindow == null) 
                exampleWindow = ScriptableObject.CreateInstance<ExampleTabbedUIWindow>();
            Rect newRect = new Rect(areaRect.x, areaRect.y+TAB_HEIGHT, areaRect.width, areaRect.height); 
            GUILayout.BeginArea(newRect);
            GUILayout.Label("X");
            exampleWindow.ASTRING = " NEW STRING";
            exampleWindow.ShowGUI(); //
            GUILayout.BeginHorizontal();
            GUILayout.Space(20f);
            if (GUILayout.Button("X"))
            {
                exampleWindow.createAfterGUI = true;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }


        Rect last;
        public TabStyles tabStyles;
        float GUTTER = 2f;
        float TAB_HEIGHT = 26f;
        int activeTab = 0;
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
            ShowTabbedUI(areaRect);
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
        #endregion examples

        static Thread startThread;
        static void StartClient()
        {
            UnityLinkManager.ClientConnected -= ConnectedToserver;
            UnityLinkManager.ClientConnected += ConnectedToserver;

            UnityLinkManager.ClientDisconnected -= DisconnectedFromServer;
            UnityLinkManager.ClientDisconnected += DisconnectedFromServer;

            StartConnection();
        }

        static void StartConnection()
        {
            // if a remote connection is needed then validate the address string as IP
            // if the address string cant be validated check if it can be resolved by Dns
            UnityLinkManager.IsClientLocal = isClientLocal;
            if (!UnityLinkManager.IsClientLocal)
            {
                bool valid = false;
                control = Control.Validating;

                Debug.LogWarning("Testing host");

                // validate IP address
                if (IPAddress.TryParse(remoteHost, out IPAddress ip))
                {
                    Debug.LogWarning("IP address is an ip address");
                    valid = true;
                    UnityLinkManager.remoteHost = remoteHost;
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
                                UnityLinkManager.remoteHost = hosts[0].MapToIPv4().ToString();
                            }
                        }
                        else // Task timed out
                        {
                            Debug.LogWarning("Dns name resolution timed out async");
                        }
                    }
                    catch
                    {
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
            Debug.LogWarning("ConnectedToserver");
            UnityLinkManager.ClientConnected -= ConnectedToserver;
            control = Control.Connected;
            connectInProgress = false;
            if (settings != null)
            {
                // since the last remote ip connected properly, save it in settings
                settings.lastSuccessfulHost = UnityLinkManager.remoteHost;
                SaveSettings();
            }
            else
            { 
                Debug.LogWarning("No settings available");
            }
        }

        static void DisconnectedFromServer(object sender, EventArgs e)
        {
            Debug.LogWarning("DisconnectedFromServer");
            UnityLinkManager.ClientDisconnected -= DisconnectedFromServer;
            control = Control.Idle;
            connectInProgress = false;
        }

        bool foldoutControlArea;
        bool foldoutSceneArea;
        bool foldoutLogArea;

        float PADDING = 4f;
        float CONTROL_HEIGHT = 100f;
        float SCENE_HEIGHT = 100f;
        float MESSAGE_HEIGHT = 100f;

        static Control control = Control.Idle;
        public enum Control
        {
            Idle,
            Validating,
            InValid,
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

            if (createAfterGUI)
            {
                EditorApplication.delayCall += UnityLinkTimeLine.CreateExampleScene;
                createAfterGUI = false;
            }
        }

        Rect ctrlTextField;
        //public bool historyShown;

        

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

        public void RecordHostHistory(string newEntry)
        {
            lastTriedHosts = RecordHistory(lastTriedHosts, newEntry, 3);
            if (settings != null)
            {
                settings.lastTriedHosts = lastTriedHosts;
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

        void ControlAreaGUI()
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            bool disableButton = false;

            if (connectInProgress) disableButton = true;
            else if (!isClientLocal && string.IsNullOrEmpty(remoteHost)) disableButton = true;

            EditorGUI.BeginDisabledGroup(disableButton);
            GUI.SetNextControlName("connectButton");
            if (GUILayout.Button(new GUIContent(UnityLinkManager.IsClientThreadActive ? styles.linkOn : styles.linkOff,
                                                UnityLinkManager.IsClientThreadActive ? "Disconnect from Cc/iClone" : "Connect to Cc/iClone"),
                                                new GUIStyle(), GUILayout.Width(64f), GUILayout.Height(64f)))
            {
                if (UnityLinkManager.IsClientThreadActive)
                {
                    UnityLinkManager.DisconnectAndStopServer();
                    Repaint();
                }
                else
                {
                    // tell the gui to disable the connect button
                    connectInProgress = true;
                    RecordHostHistory(remoteHost);
                    GUI.FocusControl("connectButton");
                    Repaint();

                    EditorApplication.delayCall += StartClient; // ();
                }
            }
            EditorGUI.EndDisabledGroup();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            bool maskControls = true;
            if (control == Control.Idle) maskControls = false;
            else if (control == Control.InValid) maskControls = false; ;

            EditorGUI.BeginDisabledGroup(maskControls);

            GUILayout.BeginHorizontal();
            GUIStyle singleLabelText = isClientLocal ? styles.selectedLabel : styles.unselectedLabel;
            GUILayout.Label("Local Server", singleLabelText, GUILayout.Width(80f));

            Texture2D toggleImg = isClientLocal ? styles.toggleLeft : styles.toggleRight;
            if (GUILayout.Button(toggleImg, GUI.skin.label, GUILayout.Width(30f), GUILayout.Height(20f)))
            {
                isClientLocal = !isClientLocal;
                settings.isClientLocal = isClientLocal;
                SaveSettings();
            }

            GUIStyle conLabelText = isClientLocal ? styles.unselectedLabel : styles.selectedLabel;
            GUILayout.Label("Remote Server", conLabelText, GUILayout.Width(110f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(isClientLocal);            
            GUILayout.Label("Remote host");
            GUILayout.Space(8f);
            //GUI.SetNextControlName("remoteHostField");
            EditorGUI.BeginChangeCheck();
            remoteHost = EditorGUILayout.TextField(remoteHost, control == Control.InValid ? styles.errorTextField : styles.normalTextField, GUILayout.Width(120f));
            if (Event.current.type == EventType.Repaint)
            {
                ctrlTextField = GUILayoutUtility.GetLastRect();
            }
            if (EditorGUI.EndChangeCheck())
            {
                control = Control.Idle;
                Repaint();
            }
            /*         
            if (!isClientLocal && !historyShown && ctrlTextField.Contains(Event.current.mousePosition) && GUI.GetNameOfFocusedControl() == "remoteHostField")
            {
                historyShown = true;
                TextFieldHistory.ShowAtPosition(new Rect(ctrlTextField.x, ctrlTextField.y, ctrlTextField.width, ctrlTextField.height), Instance);
                GUI.FocusControl("remoteHostField");
            }
            */
            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_dropdown_toggle").image, "Show previous connection attempts"), styles.minimalButton))
            {
                TextFieldHistory.ShowAtPosition(new Rect(ctrlTextField.x, ctrlTextField.y, ctrlTextField.width, ctrlTextField.height), UpdateRemoteHostName, lastTriedHosts);
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
                    UnityLinkManager.AbortConnectionAttempt();
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

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

    public class TextFieldHistory : EditorWindow
    {
        static TextFieldHistory textFieldHistory = null;
        static long lastClosedTime;
        //static UnityLinkManagerWindow Instance;
        static Func<string, bool> CallBack;
        static string[] Contents;

        void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Close;
            hideFlags = HideFlags.DontSave;
        }

        void OnDisable()
        {
            //Instance.historyShown = false;
            AssemblyReloadEvents.beforeAssemblyReload -= Close;
            textFieldHistory = null;            
        }

        public static bool ShowAtPosition(Rect buttonRect, Func<string, bool> callBack, string[] contents) //UnityLinkManagerWindow instance)
        {
            //Instance = instance;
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
            
            float width = buttonRect.width;//= 100f; //= 
            float height = (Contents.Length > 0 ? buttonRect.height * Contents.Length : buttonRect.height) + 4f;//26f; //= 

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

    public class UnityLinkManagerHistoryDropdown : EditorWindow
    {
        // dropdown gui that detects active import references in the scene and prompts them along with a list of the previous 10 or so used ones


    }

}