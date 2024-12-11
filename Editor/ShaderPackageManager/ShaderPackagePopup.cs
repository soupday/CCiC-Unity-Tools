using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Reallusion.Import
{
    public class ShaderPackagePopup : EditorWindow
    {
        public static ShaderPackagePopup Instance;        
        private float BUTTON_WIDTH = 110f;
        private static string popupMessage = "";
        private static PopupType WindowPopupType;
        
        public static bool OpenPopupWindow(PopupType popupType, string message)
        {            
            if (EditorWindow.HasOpenInstances<ShaderPackagePopup>())
                Instance = GetWindow<ShaderPackagePopup>();
            else
            {
                string titleString = string.Empty;
                switch (popupType)
                {
                    case PopupType.DefaultInstall:
                        {
                            titleString = DefaultInstallStr;
                            break;
                        }
                    case PopupType.Completion:
                        {
                            titleString = CompletionStr;
                            break;
                        }
                }

                WindowPopupType = PopupType.Completion;
                CreateWindow(titleString, message, true);
            }
            Instance.Focus();
            return WindowPopupType == popupType;
        }
        /*
        public static bool OpenInitialInstallWindow(string message)
        {
            if (EditorWindow.HasOpenInstances<ShaderPackagePopup>())
                Instance = GetWindow<ShaderPackagePopup>();
            else
            {
                popupType = PopupType.DefaultInstall;
                CreateWindow("No Shaders Correctly Installed...", message, true);
            }
            Instance.Focus();
            return popupType == PopupType.DefaultInstall;
        }
        */
        private static void CreateWindow(string title, string message, bool showUtility)
        {
            float width = 330f;
            float height = 120f;            
            Rect centerPosition = Util.GetRectToCenterWindow(width, height);            
            Instance = ScriptableObject.CreateInstance<ShaderPackagePopup>();
            
            Instance.titleContent = new GUIContent(title);
            Instance.minSize = new Vector2(width, height);
            Instance.maxSize = new Vector2(width, height);
            popupMessage = message;

            if (showUtility)
                Instance.ShowUtility();
            else
                Instance.Show();

            Instance.position = centerPosition;
        }

        private void OnGUI()
        {
            if (WindowPopupType == PopupType.Completion)
            {
                CompletionGUI();
                return;
            }

            if (WindowPopupType == PopupType.DefaultInstall)
            {
                DefaultInstallGUI();
                return;
            }
        }

        private void CompletionGUI()
        {
            GUILayout.BeginVertical();

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            GUILayout.Label(popupMessage);

            GUILayout.FlexibleSpace();

            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Show Updater", GUILayout.Width(BUTTON_WIDTH)))
            {
                UpdateManager.TryPerformUpdateChecks(true);
                this.Close();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("OK", GUILayout.Width(BUTTON_WIDTH)))
            {
                this.Close();
            }

            GUILayout.FlexibleSpace();

            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            GUILayout.EndVertical();
        }

        private void DefaultInstallGUI()
        {

        }

        private const string DefaultInstallStr = "No Shaders Correctly Installed...";
        private const string CompletionStr = "Shader Installation Complete...";

        public enum PopupType
        {
            DefaultInstall,
            Completion
        }

    }
}