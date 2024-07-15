using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;
using Reallusion.Import;

namespace Reallusion.Import
{
    public class ShaderDistroPackager : EditorWindow
    {
        #region SETUP
        public static ShaderDistroPackager Instance;
        public const string sourceFilename = "RL_Shader_Source_Paths";
        public const string sourceSuffix = ".json";
        public const string bundleString = "DO_NOT_DELETE_";
        public string sourcePath;
        public const string outputDirectory = "Assets";
        public PipelineShaderSourcePathList sourceList;
        public bool includeDependencies = false;

        [MenuItem("Reallusion/Processing Tools/Shader Distro Packager", priority = 800)]
        public static void CreateWindow()
        {
            Instance = OpenWindow();
        }

        [MenuItem("Reallusion/Processing Tools/Shader Distro Packager", true)]
        public static bool ValidateWindow()
        {
            return !EditorWindow.HasOpenInstances<ShaderDistroPackager>();
        }

        public static ShaderDistroPackager OpenWindow()
        {
            ShaderDistroPackager window = ScriptableObject.CreateInstance<ShaderDistroPackager>();
            window.ShowUtility();
            window.minSize = new Vector2(600f, 300f);

            return window;
        }

        private void OnEnable()
        {
            sourceList = LoadSourceJson();
        }

        private void OnDisable()
        {

        }
        #endregion SETUP

        #region GUI
        public Styles guiStyles;

        public class Styles
        {
            public GUIStyle SectionHeader;
            public GUIStyle SubSectionTitle;
            public GUIStyle VersionLabel;

            public Styles()
            {
                SectionHeader = new GUIStyle(GUI.skin.label);
                SectionHeader.fontSize = 14;
                SectionHeader.fontStyle = FontStyle.BoldAndItalic;
                SectionHeader.normal.textColor = Color.gray;

                SubSectionTitle = new GUIStyle(GUI.skin.label);
                SubSectionTitle.fontSize = 12;
                SubSectionTitle.fontStyle = FontStyle.Italic;
                SubSectionTitle.normal.textColor = Color.gray;

                VersionLabel = new GUIStyle(GUI.skin.textField);
                VersionLabel.normal.textColor = Color.gray;
            }

        }

        private void OnGUI()
        {
            if (guiStyles == null)
                guiStyles = new Styles();

            if (sourceList == null)
                sourceList = LoadSourceJson();


            GUILayout.BeginVertical();
            GUILayout.Label("Shader Packager & Manifest Generator", guiStyles.SectionHeader);
            GUILayout.Space(10f);
            GUILayout.Label("Output Folder - For both the .unitypackage and manifest.json files", guiStyles.SubSectionTitle);

            GUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            if (GUILayout.Button(EditorGUIUtility.IconContent("d_FolderOpened Icon").image, GUILayout.Width(BUTTON_SIZE), GUILayout.Height(BUTTON_SIZE)))
            {
                sourceList.OutputPath = EditorUtility.OpenFolderPanel("Browse for the output folder", sourceList.OutputPath, "");
                GUI.FocusControl("");
            }
            sourceList.OutputPath = GUILayout.TextField(sourceList.OutputPath, GUILayout.Width(500f), GUILayout.Height(BUTTON_SIZE));
            if (EditorGUI.EndChangeCheck())
            {
                if (string.IsNullOrEmpty(sourceList.OutputPath) || !AssetDatabase.IsValidFolder(FullPathToUnityFormat(sourceList.OutputPath)))
                {
                    sourceList.OutputPath = "Assets";
                }
                else
                {
                    sourceList.OutputPath = FullPathToUnityFormat(sourceList.OutputPath);
                }
                AssetDatabase.Refresh();
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("SaveActive").image, "Save current packager and manifest generator settings to a json file for later use")))
            {
                SaveSourceJson();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label("Shader Folders to Package", guiStyles.SubSectionTitle);
            if (sourceList != null)
            {
                if (sourceList.pipelineShaderSourcePaths != null)
                {
                    if (sourceList.pipelineShaderSourcePaths.Count > 0)
                    {
                        DrawPiplineshaderSourceGUI();
                    }
                    else
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUILayout.Label("Please add a new folder to be packaged...", guiStyles.SubSectionTitle);
                        GUILayout.EndHorizontal();
                    }
                }
            }
            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_ol_plus").image, "Add folder To be packaged"), GUILayout.Width(BUTTON_SIZE), GUILayout.Height(BUTTON_SIZE)))
            {
                if (sourceList == null)
                {
                    sourceList = new PipelineShaderSourcePathList();
                }

                if (sourceList.pipelineShaderSourcePaths == null)
                {
                    sourceList.pipelineShaderSourcePaths = new List<PipelineShaderSourcePath>();
                }

                sourceList.pipelineShaderSourcePaths.Add(new PipelineShaderSourcePath(ShaderPackageUpdater.PipelineVersion.None, "No Name", "0.0", "None"));
            }

            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            /*
            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_CollabMoved Icon").image, "Launch test splash screen.")))
            {
                ShaderPackageUpdater.CreateWindow();
            }
            */

            //GUILayout.Label("Include Dependencies.");
            includeDependencies = GUILayout.Toggle(includeDependencies, "Include Dependencies");

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        float BUTTON_SIZE = 22f;
        float ENUM_SIZE = 80f;
        float NAME_SIZE = 100f;

        private void DrawPiplineshaderSourceGUI()
        {
            GUILayout.BeginVertical();

            int pathIndex = -1;
            foreach (PipelineShaderSourcePath source in sourceList.pipelineShaderSourcePaths)
            {
                GUILayout.BeginHorizontal();

                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("P4_CheckOutRemote").image, "Package this folder into a unitypackage."), GUILayout.Width(BUTTON_SIZE), GUILayout.Height(BUTTON_SIZE)))
                {
                    ExportPackage(source);
                }

                source.Pipeline = (ShaderPackageUpdater.PipelineVersion)EditorGUILayout.EnumPopup(source.Pipeline, GUILayout.Width(ENUM_SIZE), GUILayout.Height(BUTTON_SIZE));

                EditorGUI.BeginDisabledGroup(source.Path.Equals("None") || string.IsNullOrEmpty(source.Path) || source.Path == null);
                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_FolderOpened Icon").image, "Use folder name as base package name."), GUILayout.Width(BUTTON_SIZE), GUILayout.Height(BUTTON_SIZE)))
                {
                    source.Name = new DirectoryInfo(source.Path).Name;
                    GUI.FocusControl("");
                }
                EditorGUI.EndDisabledGroup();

                source.Name = EditorGUILayout.TextField(source.Name, GUILayout.Width(NAME_SIZE), GUILayout.Height(BUTTON_SIZE));

                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent(source.VersionEdit ? "d_Toggle Icon" : "d_editicon.sml").image, source.VersionEdit ? "Done editing version number" : "Edit version number"), GUILayout.Width(BUTTON_SIZE), GUILayout.Height(BUTTON_SIZE)))
                {
                    source.VersionEdit = !source.VersionEdit;
                    /*
                    if (source.VersionEdit)
                        source.UtilityString = source.Version.ToString();
                    else
                        source.Version = Version.Parse(source.UtilityString);
                    */
                    GUI.FocusControl("");
                }
                if (source.VersionEdit)
                {
                    source.Version = EditorGUILayout.TextField(source.Version, GUILayout.Width(80f), GUILayout.Height(BUTTON_SIZE));
                }
                else
                {
                    GUILayout.Label(source.Version.ToString(), guiStyles.VersionLabel, GUILayout.Width(80f), GUILayout.Height(BUTTON_SIZE));
                }

                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_FolderOpened Icon").image, "Browse for folder to package"), GUILayout.Width(BUTTON_SIZE), GUILayout.Height(BUTTON_SIZE)))
                {
                    source.Path = EditorUtility.OpenFolderPanel("Browse for the folder containing the shader to be packaged", source.Path, "");
                    GUI.FocusControl("");
                }

                source.Path = EditorGUILayout.TextField(source.Path, GUILayout.Height(BUTTON_SIZE));
                if (GUILayout.Button(EditorGUIUtility.IconContent("d_ol_minus").image, GUILayout.Width(BUTTON_SIZE), GUILayout.Height(BUTTON_SIZE)))
                {
                    pathIndex = sourceList.pipelineShaderSourcePaths.IndexOf(source);
                    break;
                }
                GUILayout.EndHorizontal();
            }
            if (pathIndex > -1)
            {
                sourceList.pipelineShaderSourcePaths.RemoveAt(pathIndex);
            }

            GUILayout.EndVertical();
        }


        #endregion GUI

        #region UTIL
        public Object FindAsset(string search, string[] folders = null)
        {
            if (folders == null) folders = new string[] { "Assets", "Packages" };

            string[] guids = AssetDatabase.FindAssets(search, folders);

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                string assetName = Path.GetFileNameWithoutExtension(assetPath);
                if (assetName.Equals(search, StringComparison.InvariantCultureIgnoreCase))
                {
                    return AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                }
            }

            return null;
        }

        public PipelineShaderSourcePathList LoadSourceJson()
        {
            var sourceObject = FindAsset(sourceFilename);//Reallusion.Import.Util.FindAsset(sourceFilename);

            if (sourceObject != null)
            {
                TextAsset sourceAsset = sourceObject as TextAsset;
                string jsonText = sourceAsset.text;
                string assetPath = AssetDatabase.GetAssetPath(sourceObject);
                //Debug.Log("Assetpath " + assetPath);
                sourcePath = Application.dataPath + assetPath.Remove(0, 6);
                return (PipelineShaderSourcePathList)JsonUtility.FromJson(jsonText, typeof(PipelineShaderSourcePathList));
            }
            else
            {
                sourcePath = Application.dataPath + "/" + sourceFilename + sourceSuffix;
                return new PipelineShaderSourcePathList();
            }
        }

        // uses full file paths 
        public void SaveSourceJson()
        {
            string jsonString = JsonUtility.ToJson(sourceList);
            File.WriteAllText(sourcePath, jsonString);
            AssetDatabase.Refresh();
        }

        // uses full file paths
        public void SaveJson(object obj, string path)
        {
            string jsonString = EditorJsonUtility.ToJson(obj);
            File.WriteAllText(path, jsonString);
            AssetDatabase.Refresh();
        }


        List<string> files;

        public void ExportPackage(PipelineShaderSourcePath source)
        {
            // find all the files to be included and put them into the list 'files'
            files = new List<string>();
            ProcessDir(source.Path);

            // initialise the manifest with header info
            ShaderPackageUpdater.ShaderPackageManifest manifest = new ShaderPackageUpdater.ShaderPackageManifest(source.Name, source.Pipeline, source.Version.ToString());

            foreach (string file in files)
            {
                // NB file is the FULL path starting with the drive letter
                // paths starting with Assets/ are required for Assetdatabase.GUIDFromAssetPath

                // to construct the manifest json some useful information is collected:
                // * path from the selected folder e.g. Folder/file.cs
                // * GUID of the file in the assetdatabase
                // * filesize

                // take the .NET API file path which contains backslashes and convert to a Unity standardized path
                // beginning with 'Assets' and containing only forward slashes
                string assetPath = file.Remove(0, Application.dataPath.Length - 6).Replace("\\", "/");
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                long fileSize = new FileInfo(file).Length;

                manifest.Items.Add(new ShaderPackageUpdater.ShaderPackageItem(assetPath, guid));//(assetPath, fileSize, guid));
            }

            // get the output path of the package to be saved and also the filename of the package to write into the manifest
            string outputPackagePath = NameUnityPackage(sourceList.OutputPath, source);
            string outputFileName = Path.GetFileName(outputPackagePath);
            manifest.SourcePackageName = outputFileName;

            // save the manifest in the selected folder (for inclusion in the unitypackage) AND in the output folder
            string bundledManifestPath = NameManifest(source.Path, source, true);
            SaveJson(manifest, bundledManifestPath);
            string outputFolderManifestPath = NameManifest(sourceList.OutputPath, source);
            SaveJson(manifest, outputFolderManifestPath);

            // determine the slected folder name starting with Assets for use by ExportPackage
            int n = source.Path.IndexOf("Assets/");
            string folderString = source.Path.Remove(0, n);
            string[] exportFolder = new string[] { folderString };
            AssetDatabase.ExportPackage(exportFolder, outputPackagePath, ExportPackageOptions.Recurse | (includeDependencies ? ExportPackageOptions.IncludeDependencies : ExportPackageOptions.Default));

            // clean up added bundled manifest to avoid multiplying litter
            // make the full path used by the json writer (File.WriteAllText) relative to the project assets
            string projectBundledManifestPath = bundledManifestPath.Remove(0, Application.dataPath.Length - 6);
            Debug.Log("Cleaning up: " + projectBundledManifestPath);
            AssetDatabase.DeleteAsset(projectBundledManifestPath);

            AssetDatabase.Refresh();
        }

        public string FullPathToUnityFormat(string path)
        {
            return path.Remove(0, Application.dataPath.Length - 6).Replace("\\", "/");
        }

        public void ProcessDir(string dirPath)
        {
            string[] fileEntries = Directory.GetFiles(dirPath);
            foreach (string fileName in fileEntries)
                if (!fileName.EndsWith("meta", StringComparison.InvariantCultureIgnoreCase))
                    files.Add(fileName);

            string[] subdirectoryEntries = Directory.GetDirectories(dirPath);
            foreach (string subdirectory in subdirectoryEntries)
                ProcessDir(subdirectory);
        }

        public string NameManifest(string folderPath, PipelineShaderSourcePath source, bool bundled = false)
        {
            if (!bundled)
                return folderPath + "/" + source.Name + "_" + source.Version + "_" + source.Pipeline.ToString() + "_RL_referencemanifest.json";
            else
                return folderPath + "/" + bundleString + source.Name + "_" + source.Version + "_" + source.Pipeline.ToString() + "_RL_shadermanifest.json";
        }

        public string NameUnityPackage(string folderPath, PipelineShaderSourcePath source)
        {
            return folderPath + "/" + source.Name + "_" + source.Version + "_" + source.Pipeline.ToString() + "_RL_shaderpackage.unitypackage";
        }
        #endregion UTIL

        #region CLASSES

        [Serializable]
        public class PipelineShaderSourcePath
        {
            public ShaderPackageUpdater.PipelineVersion Pipeline;
            public string Name;
            public string Version;
            public bool VersionEdit;
            public string UtilityString;
            public string Path;

            public PipelineShaderSourcePath(ShaderPackageUpdater.PipelineVersion pipeline, string name, string version, string path)
            {
                Pipeline = pipeline;
                Name = name;
                Version = version;
                VersionEdit = false;
                UtilityString = string.Empty;
                Path = path;
            }
        }

        [Serializable]
        public class PipelineShaderSourcePathList
        {
            public string OutputPath;
            public List<PipelineShaderSourcePath> pipelineShaderSourcePaths;

            public PipelineShaderSourcePathList()
            {
                OutputPath = outputDirectory;
                pipelineShaderSourcePaths = new List<PipelineShaderSourcePath>();
            }
        }

        #endregion CLASSES



    }
}