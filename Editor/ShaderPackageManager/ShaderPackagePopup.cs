using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Reallusion.Import
{
    public class ShaderPackagePopup : EditorWindow
    {
        public static ShaderPackagePopup Instance;
        private static bool showUtility = true;
        private float BUTTON_WIDTH = 110f;
        private static string statusMessage = "";

        public static void OpenWindow(string message)
        {            
            if (EditorWindow.HasOpenInstances<ShaderPackagePopup>())
                Instance = GetWindow<ShaderPackagePopup>();
            else
            {
                Instance = ScriptableObject.CreateInstance<ShaderPackagePopup>();
                if (showUtility)
                    Instance.ShowUtility();
                else
                    Instance.Show();
            }
            
            Instance.titleContent = new GUIContent("Shader Installation Complete...");
            Instance.minSize = new Vector2(300f, 120f);
            Instance.maxSize = new Vector2(300f, 120f);

            statusMessage = message;

            Instance.Focus();
        }

        private void OnGUI()
        {
            GUILayout.BeginVertical();

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            GUILayout.Label(statusMessage);

            GUILayout.FlexibleSpace();

            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Re-open Updater", GUILayout.Width(BUTTON_WIDTH))) 
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

    }
}