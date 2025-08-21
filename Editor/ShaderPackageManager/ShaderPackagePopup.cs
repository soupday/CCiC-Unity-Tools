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

        private void OnDestroy()
        {
            //UpdateManager.updateMessage = string.Empty;
            if (ImporterWindow.GeneralSettings != null)
            {
                ImporterWindow.GeneralSettings.updateMessage = string.Empty;
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