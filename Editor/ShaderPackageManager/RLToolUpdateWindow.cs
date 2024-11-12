using System;
using UnityEditor;
using UnityEngine;

namespace Reallusion.Import
{
    public class RLToolUpdateWindow : EditorWindow
    {
        public static RLToolUpdateWindow Instance;
        private static bool showUtility = true;

        /*
        [MenuItem("Reallusion/Processing Tools/Check for Updates", priority = 800)]
        public static void CreateWindow()
        {
            Debug.Log("ShaderPackageUpdater.CreateWindow()");
            if (!EditorWindow.HasOpenInstances<RLToolUpdateWindow>())
                Instance = OpenWindow();

            Instance.DoVersionCheck();
        }

        [MenuItem("Reallusion/Processing Tools/Check for Updates", true)]
        public static bool ValidateWindow()
        {
            return !EditorWindow.HasOpenInstances<RLToolUpdateWindow>();
        }
        */

        public static RLToolUpdateWindow OpenWindow()
        {
            RLToolUpdateWindow window = ScriptableObject.CreateInstance<RLToolUpdateWindow>();

            if (EditorWindow.HasOpenInstances<RLToolUpdateWindow>())
                window = GetWindow<RLToolUpdateWindow>();
            else
            {
                window = ScriptableObject.CreateInstance<RLToolUpdateWindow>();
                if (showUtility)
                    window.ShowUtility();
                else
                    window.Show();
            }           
                
            window.minSize = new Vector2(600f, 300f);

            return window;
        }

        public static bool ShowAtPosition(Rect buttonRect)
        {
            long nowMilliSeconds = System.DateTime.Now.Ticks / System.TimeSpan.TicksPerMillisecond;
            bool justClosed = nowMilliSeconds < lastClosedTime + 50;
            if (!justClosed)
            {
                Event.current.Use();
                if (rlToolUpdateWindow == null)
                    rlToolUpdateWindow = ScriptableObject.CreateInstance<RLToolUpdateWindow>();
                else
                {
                    rlToolUpdateWindow.Cancel();
                    return false;
                }

                rlToolUpdateWindow.Init(buttonRect);
                return true;
            }
            return false;
        }

        public static float DROPDOWN_WIDTH = 500f;
        public static float INITIAL_DROPDOWN_HEIGHT = 200f;
        public static float CURRENT_DROPDOWN_HEIGHT;
        public static float CURRENT_DROPDOWN_WIDTH;
        public static float DRAGBAR_WIDTH = 4f;
        public static float DRAGBAR_HEIGHT = 4f;

        void Init(Rect buttonRect)
        {
            // Has to be done before calling Show / ShowWithMode
            buttonRect = GUIUtility.GUIToScreenRect(buttonRect);
            CURRENT_DROPDOWN_HEIGHT = INITIAL_DROPDOWN_HEIGHT;
            CURRENT_DROPDOWN_WIDTH = DROPDOWN_WIDTH;
            Vector2 windowSize = new Vector2(DROPDOWN_WIDTH, INITIAL_DROPDOWN_HEIGHT);
            ShowAsDropDown(buttonRect, windowSize);
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private void OnUpdate()
        {
            if (dragging) { Repaint(); }
        }

        void Cancel()
        {
            EditorApplication.update -= OnUpdate;
            Close();
            GUI.changed = true;
            GUIUtility.ExitGUI();
        }

        static RLToolUpdateWindow rlToolUpdateWindow = null;
        static long lastClosedTime;

        private bool initInfo = false;
        //private bool waitingForCheck = false;
        //private bool doCheck = false;
        //private bool linkClicked = false;
        private RLSettingsObject settings;
        //Version gitHubLatestVersion;
        //DateTime gitHubPublishedDateTime;

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
            /*
            gitHubLatestVersion = RLToolUpdateUtil.TagToVersion(settings.jsonTagName);
            RLToolUpdateUtil.TryParseISO8601toDateTime(settings.jsonPublishedAt, out gitHubPublishedDateTime);
            */
            initInfo = true;
        }

        /*
        public void DoVersionCheck()
        {
            waitingForCheck = true;
            RLToolUpdateUtil.HttpVersionChecked -= OnHttpVersionChecked;
            RLToolUpdateUtil.HttpVersionChecked += OnHttpVersionChecked;

            RLToolUpdateUtil.InitUpdateCheck();
        }

        public void OnHttpVersionChecked(object sender, EventArgs e)
        {
            waitingForCheck = false;
            InitInfo();
            RLToolUpdateUtil.HttpVersionChecked -= OnHttpVersionChecked;
        }
        */

        private void OnEnable()
        {
            //waitingForCheck = false;
            //initInfo = false;
        }

        void OnDestroy()
        {
            
        }

        Styles style;

        public class Styles
        {
            public GUIStyle infoText;
            public GUIStyle infoBoxD;
            public GUIStyle infoBoxL;
            public GUIStyle httpText;
            public GUIStyle httpTextClicked;
            public GUIStyle outlineStyle;

            public Styles()
            {
                infoText = new GUIStyle(GUI.skin.label);
                infoText.fontSize = 14;
                infoText.normal.textColor = Color.white;
                infoText.wordWrap = true;

                infoBoxD = new GUIStyle(GUI.skin.box);
                infoBoxD.normal.background = TextureColor(Color.gray * 0.5f);

                infoBoxL = new GUIStyle(GUI.skin.box);
                infoBoxL.normal.background = TextureColor(Color.gray * 0.8f);


                httpText = new GUIStyle(GUI.skin.label);
                httpText.fontSize = 14;
                httpText.normal.textColor = new Color(0.035f, 0.41f, 0.85f);
                httpText.hover.textColor = Color.cyan;

                httpTextClicked = new GUIStyle(GUI.skin.label);
                httpTextClicked.fontSize = 14;
                httpTextClicked.normal.textColor = Color.magenta * 0.85f;
                httpTextClicked.hover.textColor = Color.magenta * 0.5f;

                outlineStyle = new GUIStyle();
                int borderSize = 1;
                outlineStyle.border = new RectOffset(borderSize, borderSize, borderSize, borderSize);
                outlineStyle.normal.background = OutlineTextureColor(Color.black);
            }
        }

        private float DRAG_VERT = 40f;
        private float VERT_INDENT = 2f;
        private float HORIZ_INDENT = 2f;
        private float SECTION_SPACER = 2f;

        private void OnGUI()
        {
            if (style == null) style = new Styles();

            //if (waitingForCheck) return;
            
            if (!initInfo) InitInfo();

            if (settings == null) InitInfo();

            if (string.IsNullOrEmpty(settings.jsonTagName)) return;
                        
            FullReleaseHistoryGui();
        }

        public static Texture2D TextureColor(Color color)
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

        public static Texture2D OutlineTextureColor(Color color)
        {
            const int size = 3;
            Texture2D texture = new Texture2D(size, size);
            texture.filterMode = FilterMode.Point;
            Color[] pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            pixels[4] = Color.clear;
            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        private static bool dragging = false;

        public static Rect GetPosition()
        {
            if (Instance == null)
                Instance = GetWindow<RLToolUpdateWindow>();
            return Instance.position;
        }

        static float diff = 0f;

        public static void HandleMouseDrag(Rect rect, Rect position)
        {
            Event mouseEvent = Event.current;
            if (rect.Contains(mouseEvent.mousePosition) || dragging)
            {
                if (mouseEvent.type == EventType.MouseDrag)
                {
                    dragging = true;

                    diff += mouseEvent.delta.y;
                    Debug.Log(diff);
                    if (diff > 30f || diff < -30f)
                    {
                        Instance.minSize = new Vector2(position.width, position.height + diff);
                        Instance.Repaint();
                        diff = 0f;
                    }
                }

                if (mouseEvent.type == EventType.MouseUp)
                {
                    dragging = false;
                    diff = 0f;
                }
            }
        }

        Vector2 posReleaseHistory = new Vector2();

        private void FullReleaseHistoryGui()
        {
            GUILayout.BeginVertical(style.outlineStyle);

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            posReleaseHistory = GUILayout.BeginScrollView(posReleaseHistory, GUILayout.Height(INITIAL_DROPDOWN_HEIGHT - 4f));
            GUILayout.BeginVertical();
            int index = 0;
            if (ShaderPackageUpdater.Instance != null)
                if (ShaderPackageUpdater.Instance.settings != null)
                    if (ShaderPackageUpdater.Instance.settings.fullJsonFragment != null)
                    {
                        foreach (RLToolUpdateUtil.JsonFragment fragment in ShaderPackageUpdater.fullJsonFragment)
                        {
                            GUILayout.BeginVertical(index % 2 > 0 ? style.infoBoxL : style.infoBoxD);
                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Version: ", style.infoText);
                            GUILayout.Label(RLToolUpdateUtil.TagToVersion(fragment.TagName).ToString(), style.infoText);
                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Release Date/Time: ", style.infoText);
                            RLToolUpdateUtil.TryParseISO8601toDateTime(fragment.PublishedAt, out DateTime time);
                            GUILayout.Label(time.ToString(), style.infoText);
                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Web Link: ", style.infoText);
                            if (GUILayout.Button(fragment.HtmlUrl.ToString(), style.httpText))
                            {
                                Application.OpenURL(fragment.HtmlUrl);
                            }
                            GUILayout.EndHorizontal();

                            GUILayout.Space(22f);
                            GUILayout.Label("Release Notes: ", style.infoText);

                            foreach (string line in RLToolUpdateUtil.LineSplit(fragment.Body))
                            {
                                GUILayout.Label(line, style.infoText);
                            }
                            GUILayout.Space(22f);
                            GUILayout.Label(" ------------------------------------------- ", style.infoText);
                            GUILayout.Space(22f);
                            GUILayout.EndVertical();
                        }

                    }

            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            GUILayout.FlexibleSpace();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical();

            if (Event.current.type == EventType.Repaint)
            {
                lowestRect = GUILayoutUtility.GetLastRect();
                Debug.Log(lowestRect);
            }
        }

        Rect lowestRect;
    }
}
