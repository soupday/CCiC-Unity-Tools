using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace Reallusion.Import
{
    public class ExampleTabbedUIWindow : EditorWindow
    {
        private void OnGUI()
        {
            GUILayout.Label("Some text in OnGUI");
            ShowGUI();
        }
        public bool createAfterGUI = false;
        public string ASTRING = "SDFDSF";
        public void ShowGUI()
        {
            GUILayout.Label("Some text - from: ShowGUI" + ASTRING);


            if (createAfterGUI)
            {
                EditorApplication.delayCall += UnityLinkTimeLine.CreateExampleScene;
                createAfterGUI = false;
            }
        }
    }
}