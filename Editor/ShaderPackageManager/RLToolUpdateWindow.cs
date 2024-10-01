using System;
using UnityEditor;
using UnityEngine;

namespace Reallusion.Import
{
    public class RLToolUpdateWindow : EditorWindow
    {
        public static RLToolUpdateWindow Instance;
        private static bool showUtility = true;

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

        void Init(Rect buttonRect)
        {
            // Has to be done before calling Show / ShowWithMode
            buttonRect = GUIUtility.GUIToScreenRect(buttonRect);

            Vector2 windowSize = new Vector2(DROPDOWN_WIDTH, INITIAL_DROPDOWN_HEIGHT);
            ShowAsDropDown(buttonRect, windowSize);
        }

        void Cancel()
        {
            Close();
            GUI.changed = true;
            GUIUtility.ExitGUI();
        }

        static RLToolUpdateWindow rlToolUpdateWindow = null;
        static long lastClosedTime;

        private bool initInfo = false;
        private bool waitingForCheck = false;
        private bool doCheck = false;
        private bool linkClicked = false;
        private RLSettingsObject settings;
        Version gitHubLatestVersion;
        DateTime gitHubPublishedDateTime;

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

            gitHubLatestVersion = RLToolUpdateUtil.TagToVersion(settings.jsonTagName);
            RLToolUpdateUtil.TryParseISO8601toDateTime(settings.jsonPublishedAt, out gitHubPublishedDateTime);

            initInfo = true;
        }

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

        private void OnEnable()
        {
            waitingForCheck = false;
            initInfo = false;
        }

        void OnDestroy()
        {
            
        }

        Styles style;

        public class Styles
        {
            public GUIStyle infoText;
            public GUIStyle httpText;
            public GUIStyle httpTextClicked;

            public Styles()
            {
                infoText = new GUIStyle(GUI.skin.label);
                infoText.fontSize = 14;
                infoText.normal.textColor = Color.white;
                infoText.wordWrap = true;

                httpText = new GUIStyle(GUI.skin.label);
                httpText.fontSize = 14;
                httpText.normal.textColor = new Color(0.035f, 0.41f, 0.85f);
                httpText.hover.textColor = Color.cyan;

                httpTextClicked = new GUIStyle(GUI.skin.label);
                httpTextClicked.fontSize = 14;
                httpTextClicked.normal.textColor = Color.magenta * 0.85f;
                httpTextClicked.hover.textColor = Color.magenta * 0.5f;
            }
        }

        private float VERT_INDENT = 2f;
        private float HORIZ_INDENT = 5f;
        private float SECTION_SPACER = 2f;


        private void OnGUI()
        {
            if (style == null) style = new Styles();

            if (waitingForCheck) return;
            
            if (!initInfo) InitInfo();

            if (settings == null) InitInfo();

            if (string.IsNullOrEmpty(settings.jsonTagName)) return;

            FullReleaseHistoryGui();
            return;

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Latest Version: ", style.infoText);
            GUILayout.Label(gitHubLatestVersion.ToString(), style.infoText);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Release Date/Time: ", style.infoText);
            GUILayout.Label(gitHubPublishedDateTime.ToString(), style.infoText);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Web Link: ", style.infoText);
            if (GUILayout.Button(settings.jsonHtmlUrl.ToString(), linkClicked ? style.httpTextClicked : style.httpText))
            {
                Application.OpenURL(settings.jsonHtmlUrl);
                linkClicked = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(22f);
            GUILayout.Label("Release Notes: ", style.infoText);
            foreach (string line in settings.jsonBodyLines)
            {
                GUILayout.Label(line, style.infoText);
            }
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            TimeSpan fiveMins = new TimeSpan(0, 0, 5, 0, 0);
            bool interval = RLToolUpdateUtil.TimeCheck(settings.lastUpdateCheck, fiveMins);
            EditorGUI.BeginDisabledGroup(!interval);
            GUILayout.Button(new GUIContent("Check For Updates", interval ? "Check GitHub for updates" : "Last update check was too recent.  GitHub restricts the rate of checks."));
            EditorGUI.EndDisabledGroup();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        Vector2 posReleaseHistory = new Vector2();

        private void FullReleaseHistoryGui()
        {
            if (RLToolUpdateUtil.fullJsonFragment == null) return;
            
            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.Space(VERT_INDENT);

            GUILayout.BeginHorizontal();

            GUILayout.Space(HORIZ_INDENT);

            posReleaseHistory = GUILayout.BeginScrollView(posReleaseHistory, GUILayout.Height(INITIAL_DROPDOWN_HEIGHT + 20f));
            GUILayout.BeginVertical();
            foreach (RLToolUpdateUtil.JsonFragment fragment in RLToolUpdateUtil.fullJsonFragment)
            {
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
                if (GUILayout.Button(fragment.HtmlUrl.ToString(), linkClicked ? style.httpTextClicked : style.httpText))
                {
                    Application.OpenURL(fragment.HtmlUrl);
                    //linkClicked = true;
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
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            GUILayout.FlexibleSpace();

            GUILayout.Space(HORIZ_INDENT);

            GUILayout.EndHorizontal();

            GUILayout.Space(VERT_INDENT);

            GUILayout.EndVertical();
        }
    }
}
