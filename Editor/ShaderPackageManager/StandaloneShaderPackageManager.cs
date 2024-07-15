using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reallusion.Import
{
    public class StandaloneShaderPackageManager : EditorWindow
    {
        #region SETUP

        public static StandaloneShaderPackageManager Instance;

        [MenuItem("Reallusion/Processing Tools/Standalone Shader Package Manager", priority = 800)]
        public static void CreateWindow()
        {
            if (!EditorWindow.HasOpenInstances<StandaloneShaderPackageManager>())
                Instance = OpenWindow();
        }

        [MenuItem("Reallusion/Processing Tools/Standalone Shader Package Manager", true)]
        public static bool ValidateWindow()
        {
            return !EditorWindow.HasOpenInstances<StandaloneShaderPackageManager>() && EditorWindow.HasOpenInstances<ShaderPackageUpdater>();
        }

        public static StandaloneShaderPackageManager OpenWindow()
        {
            StandaloneShaderPackageManager window = ScriptableObject.CreateInstance<StandaloneShaderPackageManager>();
            window.ShowUtility();
            window.minSize = new Vector2(600f, 300f);

            return window;
        }

        public List<ShaderPackageUpdater.ShaderPackageManifest> distroCatalog;


        private void OnEnable()
        {

        }

        #endregion SETUP

        #region GUI

        private void OnGUI()
        {
            if (ShaderPackageUpdater.Instance == null)
            {
                GUILayout.Label("Please Wait", EditorStyles.largeLabel);
                return;
            }

            GUILayout.Label("Installed Shader Package:", EditorStyles.largeLabel);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_winbtn_mac_close_h").image, "UnInstall"), GUILayout.Width(20f)))
            {
                Debug.Log("Installing Package: " + ShaderPackageUpdater.Instance.activePackageString);
                ShaderPackageUpdater.Instance.UnInstallPackage();
                ShaderPackageUpdater.Instance.UpdateGUI();
            }
            string shaderLabel = ShaderPackageUpdater.Instance.installedShaderPipelineVersion.ToString() + " v" + ShaderPackageUpdater.Instance.installedShaderVersion.ToString();
            GUILayout.Label(shaderLabel, EditorStyles.largeLabel);
            GUILayout.EndHorizontal();

            if (distroCatalog == null)
            {
                distroCatalog = ShaderPackageUpdater.Instance.distroCatalog;
                return;
            }

            if (distroCatalog.Count == 0)
                return;

            GUILayout.Label("Available Distribution Packages:", EditorStyles.largeLabel);
            foreach (ShaderPackageUpdater.ShaderPackageManifest manifest in distroCatalog)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_winbtn_mac_max_h").image, "Install " + Path.GetFileNameWithoutExtension(manifest.SourcePackageName)), GUILayout.Width(20f)))
                {
                    Debug.Log("Installing Package: " + manifest.SourcePackageName);
                    ShaderPackageUpdater.Instance.InstallPackage(manifest);
                    ShaderPackageUpdater.Instance.UpdateGUI();
                }
                GUILayout.Label(manifest.SourcePackageName, EditorStyles.largeLabel);
                GUILayout.EndHorizontal();

            }



        }

        #endregion GUI

        #region UTIL

        #endregion UTIL



    }
}