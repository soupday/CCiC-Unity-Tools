#if PLASTIC_NEWTONSOFT_AVAILABLE
using Unity.Plastic.Newtonsoft.Json;
using Unity.Plastic.Newtonsoft.Json.Linq;
#else
using Newtonsoft.Json;  // com.unity.collab-proxy (plastic scm) versions prior to 1.14.12
using Newtonsoft.Json.Linq;
using Formatting = Newtonsoft.Json.Formatting;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.Reflection;
using UnityEngine.SceneManagement;
#if HDRP_10_5_0_OR_NEWER
using UnityEngine.Rendering.HighDefinition;
using UnityEditor.Rendering;
#elif URP_10_5_0_OR_NEWER
using UnityEngine.Rendering.Universal;
using UnityEditor.Rendering;
#elif UNITY_POST_PROCESSING_3_1_1
using UnityEngine.Rendering.PostProcessing;
#endif

namespace Reallusion.Import
{
    public class UnityLinkImporter
    {
        int waitForFramesBeforeStartingImport = 10;

        ModelImporter importer;
        UnityLinkManager.QueueItem QueueItem;
        UnityLinkManager.OpCodes opCode;

        // folders for import
        //string ROOT_FOLDER = "Assets";
        //string IMPORT_PARENT = "Reallusion";
        //string IMPORT_FOLDER = "DataLink_Imports";

        // parent folder for asset saving (in a sub folder) - this should include the created scene
        string SAVE_FOLDER_PATH = string.Empty;
        string SCENE_NAME = string.Empty; // new scene would be SAVE_FOLDER_PATH/SCENE_NAME.unity
                                          // associated assets would be in SAVE_FOLDER_PATH/SCENE_NAME/<asset files>

        string fbxPath = string.Empty;
        bool importMotion = false;
        bool importAvatar = false;
        bool importProp = false;
        bool importStaging = false;
        bool importIntoScene = false;
        bool addToTimeLine = false;
        PlayableDirector selectedTimeline = null;
        string saveFolder = string.Empty;
        string importDestinationFolder = string.Empty;
        string assetImportDestinationPath = string.Empty;

        // types for reflection
        Type HDAdditionalLightData = null;
        Type URPAdditionalLightData = null;
        Type LightProxyType = null;
        Type HDAdditionalCameraData = null;
        Type URPAdditionalCameraData = null;
        Type CameraProxyType = null;
        MethodInfo SetupLightMethod = null;
        MethodInfo SetupCameraMethod = null;

        // animated property flags
        // transform specific
        public bool pos_delta = false, rot_delta = false, scale_delta = false;
        
        // enabled specific - use in building an activation track
        public bool active_delta = false;

        // light specific
#if HDRP_17_0_0_OR_NEWER
        public const float HDRP_INTENSITY_SCALE = 6000f;
#else
        public const float HDRP_INTENSITY_SCALE = 25000f;
#endif
        public const float URP_INTENSITY_SCALE = 1f;
        public const float PP_INTENSITY_SCALE = 0.12f;
        public const float BASE_INTENSITY_SCALE = 0.12f;
        public const float RANGE_SCALE = 0.01f;

        public bool color_delta = false, mult_delta = false, range_delta = false, angle_delta = false, fall_delta = false, att_delta = false, dark_delta = false;

        // camera specific
        public bool dof_delta = false;

        PackageType packageType = PackageType.NONE;

        #region Import Preparation
        public UnityLinkImporter(UnityLinkManager.QueueItem item)
        {
            QueueItem = item;
            opCode = QueueItem.OpCode;
            //importDestinationFolder = UnityLinkManager.IMPORT_DESTINATION_FOLDER;
            importDestinationFolder = string.IsNullOrEmpty(UnityLinkManager.IMPORT_DESTINATION_FOLDER) ? UnityLinkManager.IMPORT_DEFAULT_DESTINATION_FOLDER : UnityLinkManager.IMPORT_DESTINATION_FOLDER;
            importIntoScene = UnityLinkManager.IMPORT_INTO_SCENE;
            addToTimeLine = UnityLinkManager.ADD_TO_TIMELINE;
            selectedTimeline = UnityLinkManager.SCENE_TIMELINE_ASSET;

            if (importIntoScene)
            {
                if (addToTimeLine)
                {
                    if (selectedTimeline == null || selectedTimeline.playableAsset == null)
                    {
                        Debug.LogWarning("UnityLinkImporter selectedTimeline || selectedTimeline.playableAsset = null");
                        if (UnityLinkSceneManagement.TryGetSceneTimeLine(out PlayableDirector sceneTimeLine)) // if there isnt one - find one in scene
                        {
                            UnityLinkManager.SCENE_TIMELINE_ASSET = sceneTimeLine;
                            selectedTimeline = sceneTimeLine;
                        }
                        else if (UnityLinkSceneManagement.TryCreateTimeLine(out PlayableDirector newTimeLine)) // if nothing in scene create one in default save location
                        {
                            UnityLinkManager.SCENE_TIMELINE_ASSET = newTimeLine;
                            selectedTimeline = newTimeLine;
                        }
                        else
                        {
                            // fail
                        }
                    }
                }
                                
                // scene-relevant dependencies such as global volume profiles for HDRP will be saved in a 
                // folder in the same parent folder as the scene.unity file called Scene Assets/<scene name>/
                bool haveSavedScene = false;
                Scene current = EditorSceneManager.GetActiveScene();

                if (current != null)
                {
                    if (!string.IsNullOrEmpty(current.name) && !string.IsNullOrEmpty(current.path))
                    {
                        haveSavedScene = true;
                    }
                }

                if (haveSavedScene)
                {
                    UnityLinkManager.SCENE_NAME = current.name;
                    UnityLinkManager.SCENE_FOLDER = Path.Combine(Path.GetDirectoryName(current.path), UnityLinkManager.SCENE_ASSETS, UnityLinkManager.SCENE_NAME);
                }
                else
                {
                    UnityLinkManager.SCENE_NAME = UnityLinkManager.SCENE_UNSAVED_NAME + "-" + UnityLinkSceneManagement.TimeStampString();
                    UnityLinkManager.SCENE_FOLDER = Path.Combine(UnityLinkManager.IMPORT_DEFAULT_DESTINATION_FOLDER.FullPathToUnityAssetPath(), UnityLinkManager.SCENE_ASSETS, UnityLinkManager.SCENE_NAME);                    
                }

                UnityLinkManager.SCENE_FOLDER.Replace("\\", "/");                

                if (string.IsNullOrEmpty(item.RemoteId))
                {
                    packageType = PackageType.DISK;
                }
                else
                {
                    packageType = PackageType.ZIP;
                }
            }
        }

        public enum PackageType
        {
            NONE = 0,
            DISK = 1,
            ZIP = 2,
        }

        public static event EventHandler ImportStarted;

        public void Import()
        {
            if (ImportStarted != null) ImportStarted.Invoke(null, null);
            string assetPath = string.Empty;
            string name = string.Empty;
            switch (opCode)
            {
                case UnityLinkManager.OpCodes.MOTION:
                    {
                        if (packageType == PackageType.DISK) { assetPath = QueueItem.Motion.Path; }
                        name = QueueItem.Motion.Name;
                        importMotion = true;
                        break;
                    }
                case UnityLinkManager.OpCodes.CHARACTER:
                    {
                        if (packageType == PackageType.DISK) { assetPath = QueueItem.Character.Path; }
                        name = QueueItem.Character.Name;
                        importAvatar = true;
                        break;
                    }
                case UnityLinkManager.OpCodes.PROP:
                    {
                        if (packageType == PackageType.DISK) { assetPath = QueueItem.Prop.Path; }
                        name = QueueItem.Prop.Name;
                        importProp = true;
                        break;
                    }
                case UnityLinkManager.OpCodes.STAGING:
                    {
                        if (packageType == PackageType.DISK) { assetPath = QueueItem.Staging.Path; }
                        name = Path.GetFileName(Path.GetDirectoryName(QueueItem.Staging.Path));
                        importStaging = true;
                        break;
                    }
            }

            if (packageType == PackageType.DISK)
            {
                if (!string.IsNullOrEmpty(assetPath))
                {
                    string assetFolder = Path.GetDirectoryName(assetPath);
                    fbxPath = RetrieveDiskAsset(assetFolder, name);
                    Directory.Delete(assetFolder, true);
                }
            }
            else if (packageType == PackageType.ZIP)
            {
                string zipPath = Path.Combine(UnityLinkManager.EXPORTPATH, QueueItem.RemoteId + ".zip");
                string zipFolder = Path.Combine(UnityLinkManager.EXPORTPATH, QueueItem.RemoteId);
                ZipFile.ExtractToDirectory(zipPath, zipFolder);
                File.Delete(zipPath);
                fbxPath = RetrieveDiskAsset(zipFolder, name);
                Directory.Delete(zipFolder, true);
            }
            
            EditorApplication.update -= WaitForFrames;
            EditorApplication.update += WaitForFrames;
        }
                
        void WaitForFrames()
        {
            if (waitForFramesBeforeStartingImport > 0)
            {
                waitForFramesBeforeStartingImport--;
                return;
            }
            EditorApplication.update -= WaitForFrames;
            
            DoImport();
        }

        List<(UnityLinkSceneManagement.TrackType, GameObject, List<AnimationClip>, bool, string)> timelineKitList;

        void DoImport()
        {
            AssetDatabase.Refresh();
            /*
            if (importIntoScene && !UnityLinkManager.timelineSceneCreated)
            {
                Debug.LogWarning("Creating new scene");
                CreateSceneAndTimeline();
            }
            */

            timelineKitList = new List<(UnityLinkSceneManagement.TrackType, GameObject, List<AnimationClip>, bool, string)>();

            if (importMotion)
            {
                ImportMotion(fbxPath, QueueItem.Motion.LinkId);
            }

            if (importAvatar)
            {
                ImportAvatar(fbxPath, QueueItem.Character.LinkId);
            }

            if (importProp)
            {
                ImportProp(fbxPath, QueueItem.Prop.LinkId);
            }

            if (importStaging)
            {                
                ImportStaging(fbxPath, QueueItem.Staging);
            }

            if (importIntoScene)
            {
                foreach (var item in timelineKitList)
                {
                    UnityLinkSceneManagement.AddToSceneAndTimeLine(item);
                }
                //AddToSceneAndTimeLine(timelineKit);
                //SelectTimeLineObjectAndShowWindow();
            }
        }
        #endregion Import Preparation

        #region Asset Retrieval
        public string RetrieveDiskAsset(string assetFolder, string name)
        {
            string inProjectAssetPath = string.Empty;

            //string assetFolder = Path.GetDirectoryName(assetPath);
            string assetFolderName = name; // Path.GetFileName(assetFolder);
            Debug.LogWarning("RetrieveDiskAsset - assetFolder " + assetFolder + " name " + name);
            
            // for FileUtil.CopyFileOrDirectory the target directory must not have any contents
            // UnityLinkManager.IMPORT_DESTINATION_FOLDER is obtained as a full path from EditorUtility.OpenFolderPanel

            // case where the IMPORT_DESTINATION_FOLDER is still null without user setting it
            /*
            string validatedDestFolder = string.Empty;
            if (string.IsNullOrEmpty(UnityLinkManager.IMPORT_DESTINATION_FOLDER))            
                validatedDestFolder = UnityLinkManager.IMPORT_DEFAULT_DESTINATION_FOLDER;            
            else            
                validatedDestFolder = UnityLinkManager.IMPORT_DESTINATION_FOLDER;
            */
            string validatedDestFolder = string.IsNullOrEmpty(UnityLinkManager.IMPORT_DESTINATION_FOLDER) ? UnityLinkManager.IMPORT_DEFAULT_DESTINATION_FOLDER : UnityLinkManager.IMPORT_DESTINATION_FOLDER;

            string proposedDestinationFolder = Path.Combine(validatedDestFolder.FullPathToUnityAssetPath(), assetFolderName);
            Debug.LogWarning("RetrieveDiskAsset - proposedDestinationFolder " + proposedDestinationFolder);

            string destinationFolder = GetNonDuplicateFolderName(proposedDestinationFolder, true);//string.Empty;
            Debug.LogWarning("RetrieveDiskAsset - destinationFolder " + destinationFolder);

            if (string.IsNullOrEmpty(destinationFolder))
            {
                Debug.LogWarning("Cannot find a folder to import into - " + proposedDestinationFolder + " has too many copies");
                return string.Empty;
            }

            if (Directory.GetFiles(assetFolder).Length == 0) { Debug.LogWarning("Asset source folder is empty!"); return string.Empty; }
            //if (Directory.GetFiles(destinationFolder).Length > 0) { Debug.LogWarning("Destination folder: " + destinationFolder +  " has files in it!"); return string.Empty; }
            try
            {
                Debug.Log("FileUtil.CopyFileOrDirectory " + assetFolder + " to " + destinationFolder);
                FileUtil.CopyFileOrDirectory(assetFolder, destinationFolder);
                AssetDatabase.Refresh();
                assetImportDestinationPath = destinationFolder;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Cannot copy asset to AssetDatabase! " + ex.Message); return string.Empty;
            }
            

            if (opCode == UnityLinkManager.OpCodes.STAGING) // non-fbx imports
            {
                // return the containing folder in the unity project 
                inProjectAssetPath = destinationFolder;
            }
            else // fbx imports
            {
                string[] guids = AssetDatabase.FindAssets("t:Model", new string[] { destinationFolder });

                foreach (string g in guids)
                {
                    string projectAssetPath = AssetDatabase.GUIDToAssetPath(g);
                    if (opCode == UnityLinkManager.OpCodes.CHARACTER)
                    {
                        if (Util.IsCC3CharacterAtPath(projectAssetPath))
                        {
                            string charName = Path.GetFileNameWithoutExtension(projectAssetPath);
                            Debug.Log("Valid CC character: " + charName + " found.");
                            inProjectAssetPath = AssetDatabase.GUIDToAssetPath(g);
                            break;
                        }
                    }
                    else if (opCode == UnityLinkManager.OpCodes.MOTION)
                    {
                        string modelName = Path.GetFileNameWithoutExtension(projectAssetPath);
                        if (modelName.EndsWith("_motion", System.StringComparison.InvariantCultureIgnoreCase))
                        {
                            Debug.Log("Valid motion: " + modelName + " found.");
                            inProjectAssetPath = AssetDatabase.GUIDToAssetPath(g);
                            break;
                        }
                    }
                    else if (opCode == UnityLinkManager.OpCodes.PROP)
                    {
                        string propExt = Path.GetExtension(projectAssetPath);
                        string propName = Path.GetFileName(projectAssetPath);
                        if (propExt.Equals(".fbx", System.StringComparison.InvariantCultureIgnoreCase))
                        {
                            Debug.Log("FBX: " + propName + " found.");
                            inProjectAssetPath = AssetDatabase.GUIDToAssetPath(g);
                        }
                    }
                }
            }

            Debug.Log("RetrieveDiskAsset: inProjectAssetPath" + inProjectAssetPath);
            return inProjectAssetPath;
        }

        public string GetUniqueImportAssetFolder(string name)
        {
            string proposedDestinationFolder = Path.Combine(UnityLinkManager.IMPORT_DESTINATION_FOLDER.FullPathToUnityAssetPath(), name);
            string destinationFolder = GetNonDuplicateFolderName(proposedDestinationFolder, true);
            return destinationFolder;
        }

        public string GetNonDuplicateFolderName(string folderName, bool insideAssetDatabase)
        {
            for (int i = 0; i < 999; i++)
            {
                string suffix = (i > 0) ? ("." + i.ToString("D3")) : "";
                string testFolder = folderName + suffix;
                bool exists = insideAssetDatabase ? AssetDatabase.IsValidFolder(testFolder) : Directory.Exists(testFolder);
                if (exists)
                    continue;
                else
                {
                    return testFolder;
                }
            }
            return string.Empty;
        }

        public void ClearStagingAssetsFolder(string path)
        {
            // requires a unity asset path
            string fullCleanupPath = path.UnityAssetPathToFullPath();
            string cleanupMetaFile = fullCleanupPath + ".meta";
            Debug.LogWarning("CLEANING UP: " + fullCleanupPath + " & Meta " + cleanupMetaFile);
            Directory.Delete(fullCleanupPath, true);
            File.Delete(cleanupMetaFile);
            AssetDatabase.Refresh();
        }
        #endregion Asset Retrieval

        #region Prop Import
        public void ImportProp(string fbxPath, string linkId)
        {
            if (string.IsNullOrEmpty(fbxPath)) { Debug.LogWarning("Cannot import asset..."); return; }
            string guid = AssetDatabase.AssetPathToGUID(fbxPath);
            Debug.Log("Creating new characterinfo with guid " + guid);
            Debug.Log("Guid path " + AssetDatabase.AssetPathToGUID(fbxPath));
            CharacterInfo c = new CharacterInfo(guid);

            c.linkId = linkId;
            c.exportType = CharacterInfo.ExportType.PROP;
            c.projectName = "iclone project name";
            //c.sceneid = add this later

            c.BuildQuality = MaterialQuality.High;
            Importer import = new Importer(c);
            import.recordMotionListForTimeLine = importIntoScene;
            GameObject prefab = import.Import();
            c.Write();

            // add link id
            var data = prefab.AddComponent<DataLinkActorData>();
            data.linkId = linkId;            
            data.prefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)).ToString();
            data.fbxGuid = AssetDatabase.AssetPathToGUID(fbxPath).ToString();
            data.createdTimeStamp = DateTime.Now.Ticks;
            PrefabUtility.SavePrefabAsset(prefab);
            if (ImporterWindow.Current != null)
                ImporterWindow.Current.RefreshCharacterList();
            List<AnimationClip> animGuidsForTimeLine = importIntoScene ? import.clipListForTimeLine : new List<AnimationClip>();
            timelineKitList.Add((UnityLinkSceneManagement.TrackType.AnimationTrack, prefab, animGuidsForTimeLine, true, linkId));
        }
        #endregion Prop Import

        #region Avatar Import
        public void ImportAvatar(string fbxPath, string linkId)
        {
            if (string.IsNullOrEmpty(fbxPath)) { Debug.LogWarning("Cannot import asset..."); return; }
            string guid = AssetDatabase.AssetPathToGUID(fbxPath);
            Debug.Log("Creating new characterinfo with guid " + guid);
            Debug.Log("Guid path " + AssetDatabase.AssetPathToGUID(fbxPath));
            CharacterInfo charInfo = new CharacterInfo(guid);

            charInfo.linkId = linkId;
            charInfo.exportType = CharacterInfo.ExportType.AVATAR;
            charInfo.projectName = "iclone project name";
            //c.sceneid = add this later

            charInfo.BuildQuality = MaterialQuality.High;
            Importer import = new Importer(charInfo);
            import.recordMotionListForTimeLine = importIntoScene;
            GameObject prefab = import.Import();
            charInfo.Write();

            // add link id
            var data = prefab.AddComponent<DataLinkActorData>();
            data.linkId = linkId;
            data.prefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)).ToString();
            data.fbxGuid = AssetDatabase.AssetPathToGUID(fbxPath).ToString();
            data.createdTimeStamp = DateTime.Now.Ticks;
            PrefabUtility.SavePrefabAsset(prefab);
            if (ImporterWindow.Current != null)
                ImporterWindow.Current.RefreshCharacterList();

            List<AnimationClip> animGuidsForTimeLine = importIntoScene ? import.clipListForTimeLine : new List<AnimationClip>();
            timelineKitList.Add((UnityLinkSceneManagement.TrackType.AnimationTrack, prefab, animGuidsForTimeLine, true, linkId));
        }
        #endregion Avatar Import

        #region Staging Import
        public void ImportStaging(string folderPath, UnityLinkManager.JsonStaging stagedManifest)
        {
            if (string.IsNullOrEmpty(fbxPath)) { Debug.LogWarning("Cannot import asset..."); return; }

            //UnityLinkSceneManagement.CreateStagingSceneDependencies();

            string dataPath = Path.GetDirectoryName(Application.dataPath);
            string fullFolderPath = Path.Combine(dataPath, fbxPath);
            string[] fileList = Directory.GetFiles(fullFolderPath);
            
            List<string> rlxList = fileList.ToList().FindAll(x => Path.GetExtension(x).Equals(".rlx", StringComparison.InvariantCultureIgnoreCase));
            foreach (string file in rlxList)
            {
                byte[] fileBytes = File.ReadAllBytes(file);
                ImportRLX(folderPath, fileBytes);
            }
            ClearStagingAssetsFolder(assetImportDestinationPath);
        }

        public void ImportRLX(string folderPath, byte[] rlxBytes)
        {
            int bytePos = 0;
            // header
            const int RLX_ID_LIGHT = 0xCC01;
            const int RLX_ID_CAMERA = 0xCC02;

            int typeWord = UnityLinkManager.GetCurrentEndianWord(UnityLinkManager.ExtractBytes(rlxBytes, 0, 4), UnityLinkManager.SourceEndian.BigEndian);

            bytePos += 4;
            //json length
            int jsonLen = UnityLinkManager.GetCurrentEndianWord(UnityLinkManager.ExtractBytes(rlxBytes, bytePos, 4), UnityLinkManager.SourceEndian.BigEndian);

            bytePos += 4;
            // json data
            byte[] jsonBytes = UnityLinkManager.ExtractBytes(rlxBytes, bytePos, jsonLen);
            string jsonString = Encoding.UTF8.GetString(jsonBytes);
            UnityLinkManager.WriteIncomingLog(jsonString, true);

            bytePos += jsonLen;
            // frame data length
            int framesLen = UnityLinkManager.GetCurrentEndianWord(UnityLinkManager.ExtractBytes(rlxBytes, bytePos, 4), UnityLinkManager.SourceEndian.BigEndian);

            bytePos += 4;
            // frame data
            byte[] frameData = UnityLinkManager.ExtractBytes(rlxBytes, bytePos, framesLen);

            switch (typeWord)
            {
                case (RLX_ID_CAMERA):
                    {
                        Debug.Log("Processing Camera");
                        MakeAnimatedCamera(folderPath, frameData, jsonString);
                        break;
                    }
                case (RLX_ID_LIGHT):
                    {
                        Debug.Log("Processing Light");
                        MakeAnimatedLight(folderPath, frameData, jsonString);
                        break;
                    }
                default:
                    {
                        Debug.LogWarning("Error identifying the staging item type.");
                        break;
                    }
            }
        }
        
        GameObject GetRootSceneObject(string linkId)
        {
            GameObject root = null;
            DataLinkActorData existing = null;

            //DataLinkActorData[] linkedObjects = GameObject.FindObjectsOfType<DataLinkActorData>(true);
            DataLinkActorData[] linkedObjects = GameObject.FindObjectsByType<DataLinkActorData>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (linkedObjects != null && linkedObjects.Length > 0)
            {
                existing = linkedObjects.ToList().Find(x => x.linkId == linkId);
            }

            if (existing != null)
            {
                root = existing.gameObject;
                if (root.transform.childCount > 0)
                {
                    for (int i = 0; i < root.transform.childCount; i++)
                    {
                        GameObject.DestroyImmediate(root.transform.GetChild(i).gameObject);
                    }
                }
                existing.createdTimeStamp = DateTime.Now.Ticks;
            }
            else
            {
                root = new GameObject();
            }
                        
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            if (root.GetComponent<Animator>() == null)
                root.AddComponent<Animator>();

            if (existing == null)
            {
                DataLinkActorData data = root.AddComponent<DataLinkActorData>();
                data.linkId = linkId;
                data.createdTimeStamp = DateTime.Now.Ticks;
            }            
            return root;
        }
        #endregion Staging Import

        #region Animated Camera
        void MakeAnimatedCamera(string folderPath, byte[] frameData, string jsonString)
        {
            UnityLinkManager.JsonCameraData jsonCameraObject = null;
            try
            {
                jsonCameraObject = JsonConvert.DeserializeObject<UnityLinkManager.JsonCameraData>(jsonString);
            }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message); return;
            }

            if (jsonCameraObject == null) { Debug.LogWarning("MakeAnimatedCamera: Could not deserialize embedded json"); return; }

            UnityLinkSceneManagement.CreateStagingSceneDependencies(jsonCameraObject.DofEnable);

            // read all frames into a list
            List<UnityLinkManager.DeserializedCameraFrames> frames = new List<UnityLinkManager.DeserializedCameraFrames>();

            Stream stream = new MemoryStream(frameData);
            stream.Position = 0;
            byte[] frameBytes = new byte[UnityLinkManager.DeserializedCameraFrames.FRAME_BYTE_COUNT];
            while (stream.Read(frameBytes, 0, frameBytes.Length) > 0)
            {
                frames.Add(new UnityLinkManager.DeserializedCameraFrames(frameBytes));
            }

            Debug.Log("Frames processed " + frames.Count);

            // construct a camera object paretented to a dolly object
            GameObject root = GetRootSceneObject(jsonCameraObject.LinkId);
            root.name = "Camera_" + jsonCameraObject.Name + "_Root";

            GameObject target = new GameObject();
            target.name = jsonCameraObject.Name;

            if (CameraProxyType == null)
            {
                CameraProxyType = Physics.GetTypeInAssemblies("Reallusion.Runtime.CameraProxy");
                if (CameraProxyType == null)
                {
                    Debug.LogWarning("Cannot create a <CameraProxy> component on the <Light> object.");
                    return;
                }
            }

            Camera camera = target.GetComponent<Camera>();
            if (camera == null) camera = target.AddComponent<Camera>();

            target.AddComponent(CameraProxyType);
            
            float alpha = ((jsonCameraObject.DofRange + jsonCameraObject.DofFarTransition + jsonCameraObject.DofNearTransition) / 16f) * 0.01f;
            float beta = 1 / ((jsonCameraObject.DofFarBlur + jsonCameraObject.DofNearBlur) / 2);
            float initialAperture = alpha * beta;
#if HDRP_14_0_0_OR_NEWER // HDRP 14 migrated focus and aperture to the <Camera> component from the <HDAdditionalCameraData> component
            camera.focusDistance = jsonCameraObject.DofFocus;
            camera.aperture = initialAperture;
#elif HDRP_10_5_0_OR_NEWER
            HDAdditionalCameraData HDCameraData = target.GetComponent<HDAdditionalCameraData>();
            if (HDCameraData == null) HDCameraData = target.AddComponent<HDAdditionalCameraData>();            

            HDCameraData.physicalParameters.focusDistance = jsonCameraObject.DofFocus;
            HDCameraData.physicalParameters.aperture = initialAperture;
#elif URP_10_5_0_OR_NEWER            
           UniversalAdditionalCameraData URPCameraData = target.GetComponent<UniversalAdditionalCameraData>();
            if (URPCameraData == null) URPCameraData = target.AddComponent<UniversalAdditionalCameraData>();  

           URPCameraData.renderPostProcessing = true;
           URPCameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
#elif UNITY_POST_PROCESSING_3_1_1
            PostProcessLayer layer = target.GetComponent<PostProcessLayer>();
            if (layer == null) layer = target.AddComponent<PostProcessLayer>();
#endif
            //Set Initial CameraProperties
            if (camera != null)
            {
                Debug.LogWarning("Camera component is NOT null");
                camera.usePhysicalProperties = true;
                camera.focalLength = jsonCameraObject.FocalLength;
                Debug.LogWarning("focalLength = " + jsonCameraObject.FocalLength);
                camera.sensorSize = new Vector2(jsonCameraObject.Width, jsonCameraObject.Height);
            }
            else
            {
                Debug.LogWarning("Camera component is null");
            }
            target.transform.position = Vector3.zero;
            target.transform.rotation = Quaternion.identity;
            target.transform.SetParent(root.transform, true);
            SetInitialCameraTransform(target.transform, jsonCameraObject);

            AnimationClip clip = MakeCameraAnimationClipFromFramesForObject(frames, target, root);
            
            if (jsonCameraObject != null)
            {
                clip.name = jsonCameraObject.LinkId;
                SaveStagingAnimationClip(jsonCameraObject.LinkId, jsonCameraObject.Name, clip);
                SetupCamera(jsonCameraObject, root, clip);
            }
        }

        public void SetInitialCameraTransform(Transform camTransform, UnityLinkManager.JsonCameraData jsonCamObject)
        {
            camTransform.localPosition = jsonCamObject.Pos;
            camTransform.localRotation = jsonCamObject.Rot;
            camTransform.localScale = jsonCamObject.Scale;
        }

        AnimationClip MakeCameraAnimationClipFromFramesForObject(List<UnityLinkManager.DeserializedCameraFrames> frames, GameObject target, GameObject root)
        {
            /*
             * This presupposes that the light is structured as follows (NB: ALL global and local positions/rotations at zero)
             * 
             * Root GameObject (with <Animator> component)            | GetAnimatableBindings ROOT object
             *       |
             *       --> Child GameObject (with <Camera> component)   | GetAnimatableBindings TARGET object                
            */

            AnimationClip clip = new AnimationClip();
            EditorCurveBinding[] bindable = AnimationUtility.GetAnimatableBindings(target, root);
            // Find binding for property

            // Transform properties
            // Rotation
            var b_rotX = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("LocalRotation.x"));
            var b_rotY = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("LocalRotation.y"));
            var b_rotZ = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("LocalRotation.z"));
            var b_rotW = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("LocalRotation.w"));

            // Position
            var b_posX = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("LocalPosition.x"));
            var b_posY = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("LocalPosition.y"));
            var b_posZ = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("LocalPosition.z"));

            // Scale
            var b_scaX = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("LocalScale.x"));
            var b_scaY = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("LocalScale.y"));
            var b_scaZ = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("LocalScale.z"));

            // focal length + fov
            var b_focalL = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("ProxyFocalLength"));
            var b_fov = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("ProxyFieldOfView"));

            // depth of field
            var b_dofEnable = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("ProxyDofEnable"));
            var b_dofFocus = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("ProxyDofFocus"));
            var b_dofRange = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("ProxyDofRange"));
            var b_dofFblur = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("ProxyDofFarBlur"));
            var b_dofNblur = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("ProxyDofNearBlur"));
            var b_dofFTran = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("ProxyDofFarTransition"));
            var b_dofFNran = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("ProxyDofNearTransition"));
            var b_dofMinDist = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("ProxyDofMinBlendDist"));

            // original version - retain
            /*
            // Transform properties
            // Rotation
            var b_rotX = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("LocalRotation.x", System.StringComparison.InvariantCultureIgnoreCase));
            var b_rotY = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("LocalRotation.y", System.StringComparison.InvariantCultureIgnoreCase));
            var b_rotZ = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("LocalRotation.z", System.StringComparison.InvariantCultureIgnoreCase));
            var b_rotW = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("LocalRotation.w", System.StringComparison.InvariantCultureIgnoreCase));

            // Position
            var b_posX = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("LocalPosition.x", System.StringComparison.InvariantCultureIgnoreCase));
            var b_posY = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("LocalPosition.y", System.StringComparison.InvariantCultureIgnoreCase));
            var b_posZ = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("LocalPosition.z", System.StringComparison.InvariantCultureIgnoreCase));

            // Scale
            var b_scaX = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("LocalScale.x", System.StringComparison.InvariantCultureIgnoreCase));
            var b_scaY = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("LocalScale.y", System.StringComparison.InvariantCultureIgnoreCase));
            var b_scaZ = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("LocalScale.z", System.StringComparison.InvariantCultureIgnoreCase));

            // focal length + fov
            var b_focalL = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("ProxyFocalLength", System.StringComparison.InvariantCultureIgnoreCase));
            var b_fov = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("ProxyFieldOfView", System.StringComparison.InvariantCultureIgnoreCase));

            // depth of field
            var b_dofEnable = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("ProxyDofEnable", System.StringComparison.InvariantCultureIgnoreCase));
            var b_dofFocus = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("ProxyDofFocus", System.StringComparison.InvariantCultureIgnoreCase));
            var b_dofRange = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("ProxyDofRange", System.StringComparison.InvariantCultureIgnoreCase));
            var b_dofFblur = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("ProxyDofFarBlur", System.StringComparison.InvariantCultureIgnoreCase));
            var b_dofNblur = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("ProxyDofNearBlur", System.StringComparison.InvariantCultureIgnoreCase));
            var b_dofFTran = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("ProxyDofFarTransition", System.StringComparison.InvariantCultureIgnoreCase));
            var b_dofFNran = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("ProxyDofNearTransition", System.StringComparison.InvariantCultureIgnoreCase));
            var b_dofMinDist = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("ProxyDofMinBlendDist", System.StringComparison.InvariantCultureIgnoreCase));
            */

            // Make keyframe[] for each bindable property

            // Transform properties
            // Rotation
            Keyframe[] f_rotX = new Keyframe[frames.Count];
            Keyframe[] f_rotY = new Keyframe[frames.Count];
            Keyframe[] f_rotZ = new Keyframe[frames.Count];
            Keyframe[] f_rotW = new Keyframe[frames.Count];

            // Position
            Keyframe[] f_posX = new Keyframe[frames.Count];
            Keyframe[] f_posY = new Keyframe[frames.Count];
            Keyframe[] f_posZ = new Keyframe[frames.Count];

            // Scale
            Keyframe[] f_scaX = new Keyframe[frames.Count];
            Keyframe[] f_scaY = new Keyframe[frames.Count];
            Keyframe[] f_scaZ = new Keyframe[frames.Count];

            // focal length + fov
            Keyframe[] f_focalL = new Keyframe[frames.Count];
            Keyframe[] f_fov = new Keyframe[frames.Count];

            // depth of field
            Keyframe[] f_dofEnable = new Keyframe[frames.Count];
            Keyframe[] f_dofFocus = new Keyframe[frames.Count];
            Keyframe[] f_dofRange = new Keyframe[frames.Count];
            Keyframe[] f_dofFblur = new Keyframe[frames.Count];
            Keyframe[] f_dofNblur = new Keyframe[frames.Count];
            Keyframe[] f_dofFTran = new Keyframe[frames.Count];
            Keyframe[] f_dofFNran = new Keyframe[frames.Count];
            Keyframe[] f_dofMinDist = new Keyframe[frames.Count];

            // Populate keyframe arrays
            for (int i = 0; i < frames.Count; i++)
            {
                // Transform curves
                // Rotation
                f_rotX[i] = new Keyframe(frames[i].Time, frames[i].Rot.x);
                f_rotY[i] = new Keyframe(frames[i].Time, frames[i].Rot.y);
                f_rotZ[i] = new Keyframe(frames[i].Time, frames[i].Rot.z);
                f_rotW[i] = new Keyframe(frames[i].Time, frames[i].Rot.w);

                // Position
                f_posX[i] = new Keyframe(frames[i].Time, frames[i].Pos.x);
                f_posY[i] = new Keyframe(frames[i].Time, frames[i].Pos.y);
                f_posZ[i] = new Keyframe(frames[i].Time, frames[i].Pos.z);

                //scale
                f_scaX[i] = new Keyframe(frames[i].Time, frames[i].Scale.x);
                f_scaY[i] = new Keyframe(frames[i].Time, frames[i].Scale.y);
                f_scaZ[i] = new Keyframe(frames[i].Time, frames[i].Scale.z);

                // focal length + fov
                f_focalL[i] = new Keyframe(frames[i].Time, frames[i].FocalLength);
                f_fov[i] = new Keyframe(frames[i].Time, frames[i].FieldOfView);

                // depth of field
                f_dofEnable[i] = new Keyframe(frames[i].Time, frames[i].DofEnable == true ? 1f : 0f);
                f_dofFocus[i] = new Keyframe(frames[i].Time, frames[i].DofFocus);
                f_dofRange[i] = new Keyframe(frames[i].Time, frames[i].DofRange);
                f_dofFblur[i] = new Keyframe(frames[i].Time, frames[i].DofFarBlur);
                f_dofNblur[i] = new Keyframe(frames[i].Time, frames[i].DofNearBlur);
                f_dofFTran[i] = new Keyframe(frames[i].Time, frames[i].DofFarTransition);
                f_dofFNran[i] = new Keyframe(frames[i].Time, frames[i].DofNearTransition);
                f_dofMinDist[i] = new Keyframe(frames[i].Time, frames[i].DofMinBlendDistance);
            }

            // bind all keyframes to the appropriate curve

            // Rotation            
            AnimationCurve c_rotX = SmoothCurve(f_rotX);//new AnimationCurve(f_rotX);
            AnimationUtility.SetEditorCurve(clip, b_rotX, c_rotX);
            AnimationCurve c_rotY = SmoothCurve(f_rotY);//new AnimationCurve(f_rotY);
            AnimationUtility.SetEditorCurve(clip, b_rotY, c_rotY);
            AnimationCurve c_rotZ = SmoothCurve(f_rotZ);//new AnimationCurve(f_rotZ);
            AnimationUtility.SetEditorCurve(clip, b_rotZ, c_rotZ);
            AnimationCurve c_rotW = SmoothCurve(f_rotW);//new AnimationCurve(f_rotW);
            AnimationUtility.SetEditorCurve(clip, b_rotW, c_rotW);

            // Position
            AnimationCurve c_posX = SmoothCurve(f_posX);//new AnimationCurve(f_posX);
            AnimationUtility.SetEditorCurve(clip, b_posX, c_posX);
            AnimationCurve c_posY = SmoothCurve(f_posY);//new AnimationCurve(f_posY);
            AnimationUtility.SetEditorCurve(clip, b_posY, c_posY);
            AnimationCurve c_posZ = SmoothCurve(f_posZ);//new AnimationCurve(f_posZ);
            AnimationUtility.SetEditorCurve(clip, b_posZ, c_posZ);

            //scale
            AnimationCurve c_scaX = ReduceCurve(f_scaX);  //new AnimationCurve(f_scaX);
            AnimationUtility.SetEditorCurve(clip, b_scaX, c_scaX);
            AnimationCurve c_scaY = ReduceCurve(f_scaY);  //new AnimationCurve(f_scaY);
            AnimationUtility.SetEditorCurve(clip, b_scaY, c_scaY);
            AnimationCurve c_scaZ = ReduceCurve(f_scaZ);  //new AnimationCurve(f_scaZ);
            AnimationUtility.SetEditorCurve(clip, b_scaZ, c_scaZ);

            // focal length + fov
            AnimationCurve c_focalL = ReduceCurve(f_focalL);  //new AnimationCurve(f_focalL);
            AnimationUtility.SetEditorCurve(clip, b_focalL, c_focalL);
            AnimationCurve c_fov = ReduceCurve(f_fov);  //new AnimationCurve(f_fov);
            AnimationUtility.SetEditorCurve(clip, b_fov, c_fov);

            // depth of field
            AnimationCurve c_dofEnable = ReduceCurve(f_dofEnable);  //new AnimationCurve(f_dofEnable);
            AnimationUtility.SetEditorCurve(clip, b_dofEnable, c_dofEnable);
            AnimationCurve c_dofFocus = ReduceCurve(f_dofFocus);  //new AnimationCurve(f_dofFocus);
            AnimationUtility.SetEditorCurve(clip, b_dofFocus, c_dofFocus);
            AnimationCurve c_dofRange = ReduceCurve(f_dofRange);  //new AnimationCurve(f_dofRange);
            AnimationUtility.SetEditorCurve(clip, b_dofRange, c_dofRange);
            AnimationCurve c_dofFblur = ReduceCurve(f_dofFblur);  //new AnimationCurve(f_dofFblur);
            AnimationUtility.SetEditorCurve(clip, b_dofFblur, c_dofFblur);
            AnimationCurve c_dofNblur = ReduceCurve(f_dofNblur);  //new AnimationCurve(f_dofNblur);
            AnimationUtility.SetEditorCurve(clip, b_dofNblur, c_dofNblur);
            AnimationCurve c_dofFTran = ReduceCurve(f_dofFTran);  //new AnimationCurve(f_f_dofFTran);
            AnimationUtility.SetEditorCurve(clip, b_dofFTran, c_dofFTran);
            AnimationCurve c_dofFNran = ReduceCurve(f_dofFNran);  //new AnimationCurve(f_dofFNran);
            AnimationUtility.SetEditorCurve(clip, b_dofFNran, c_dofFNran);
            AnimationCurve c_dofMinDist = ReduceCurve(f_dofMinDist);  //new AnimationCurve(f_dofMinDist);
            AnimationUtility.SetEditorCurve(clip, b_dofMinDist, c_dofMinDist);

            clip.frameRate = 60f;
            Debug.LogWarning("Calculated Frame Rate = " + (frames[frames.Count - 1].Frame) / frames[frames.Count - 1].Time);
            return clip;
        }

        GameObject SetupCamera(UnityLinkManager.JsonCameraData json, GameObject root, AnimationClip clip)
        {
            if (CameraProxyType == null)
            {
                CameraProxyType = Physics.GetTypeInAssemblies("Reallusion.Runtime.CameraProxy");
                if (CameraProxyType == null)
                {
                    Debug.LogWarning("SetupLight cannot find the <CameraProxy> class.");
                    return null;
                }
                else
                {
                    Debug.LogWarning("Found " + CameraProxyType.GetType().ToString());
                }
            }
            else
            {
                Debug.LogWarning("Already had " + CameraProxyType.GetType().ToString());
            }

            Component proxy = root.GetComponentInChildren(CameraProxyType);
            if (proxy != null)
            {
                SetupCameraMethod = proxy.GetType().GetMethod("SetupCamera",
                                    BindingFlags.Public | BindingFlags.Instance,
                                    null,
                                    CallingConventions.Any,
                                    new Type[] { typeof(string), typeof(AnimationClip) },
                                    null);

                if (SetupCameraMethod == null)
                {
                    Debug.LogWarning("SetupLight MethodInfo cannot be determined");
                    return null;
                }
            }
            else
            {
                Debug.LogWarning("SetupLight cannot find the <LightProxy> component.");
                return null;
            }

            json.dof_delta = dof_delta;

            string jsonString = JsonConvert.SerializeObject(json);
            SetupCameraMethod.Invoke(proxy, new object[] { jsonString, clip });

            GameObject prefab = GetPrefabAsset(json.LinkId, json.Name, root);
            GameObject.DestroyImmediate(root);

            List<AnimationClip> clips = new List<AnimationClip>();
            clips.Add(clip);
            timelineKitList.Add((UnityLinkSceneManagement.TrackType.AnimationTrack, prefab, clips, true, json.LinkId));
            //UnityLinkSceneManagement.CreateStagingSceneDependencies();
            return root;
        }
        #endregion Animated Camera

        #region Animated Light
        void MakeAnimatedLight(string folderPath, byte[] frameData, string jsonString)
        {
            UnityLinkManager.JsonLightData jsonLightObject = null;
            try
            {
                jsonLightObject = JsonConvert.DeserializeObject<UnityLinkManager.JsonLightData>(jsonString);
            }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message); return;
            }

            if (jsonLightObject == null) { Debug.LogWarning("MakeAnimatedLight: Could not deserialize embedded json"); return; }

            UnityLinkSceneManagement.CreateStagingSceneDependencies(false);

            // read all frames into a list
            List<UnityLinkManager.DeserializedLightFrames> frames = new List<UnityLinkManager.DeserializedLightFrames>();

            Stream stream = new MemoryStream(frameData);
            stream.Position = 0;
            byte[] frameBytes = new byte[UnityLinkManager.DeserializedLightFrames.FRAME_BYTE_COUNT];
            while (stream.Read(frameBytes, 0, frameBytes.Length) > 0)
            {
                frames.Add(new UnityLinkManager.DeserializedLightFrames(frameBytes));
            }

            Debug.Log("MakeAnimatedLight: Frames processed " + frames.Count);

            // construct a light object paretented to a dolly object
            GameObject root = GetRootSceneObject(jsonLightObject.LinkId);
            root.name = "Light_" + jsonLightObject.Name + "_Root";

            GameObject target = new GameObject();
            target.name = jsonLightObject.Name;

            if (LightProxyType == null)
            {
                LightProxyType = Physics.GetTypeInAssemblies("Reallusion.Runtime.LightProxy");
                if (LightProxyType == null)
                {
                    Debug.LogWarning("Cannot create a <LightProxy> component on the <Light> object.");
                    return;
                }
            }

            target.AddComponent(LightProxyType);
            Light light = target.GetComponent<Light>();
            if (light == null) light = target.AddComponent<Light>();

            const string spot = "SPOT";
            const string dir = "DIR";
            const string point = "POINT";

            switch (jsonLightObject.Type)
            {
                case (spot):
                    {
                        light.type = LightType.Spot;
                        break;
                    }
                case (dir):
                    {
                        light.type = LightType.Directional;
                        break;
                    }
                case (point):
                    {
                        light.type = LightType.Point;
                        break;
                    }
            }
#if HDRP_17_0_0_OR_NEWER // HDRP 17 migrated light intensity to the <Light> component from the <HDAdditionalData> component
            HDAdditionalLightData HDLightData = target.GetComponent<HDAdditionalLightData>();
            if (HDLightData == null) HDLightData = target.AddComponent<HDAdditionalLightData>();

            light.shadows = light.type != LightType.Directional ? LightShadows.Hard : LightShadows.None;
            light.intensity = jsonLightObject.Multiplier * HDRP_INTENSITY_SCALE;
#elif HDRP_10_5_0_OR_NEWER
            HDAdditionalLightData HDLightData = target.GetComponent<HDAdditionalLightData>();
            if (HDLightData == null) HDLightData = target.AddComponent<HDAdditionalLightData>();

            light.shadows = light.type != LightType.Directional ? LightShadows.Hard : LightShadows.None;
            HDLightData.intensity = jsonLightObject.Multiplier * HDRP_INTENSITY_SCALE;
#elif URP_10_5_0_OR_NEWER
            light.shadows = light.type != LightType.Directional ? LightShadows.Hard : LightShadows.None;
            light.intensity = jsonLightObject.Multiplier * URP_INTENSITY_SCALE;
#elif UNITY_POST_PROCESSING_3_1_1
            light.lightmapBakeType = LightmapBakeType.Mixed;
            light.shadows = light.type != LightType.Directional ? LightShadows.Soft : LightShadows.None;
            light.intensity = jsonLightObject.Multiplier * PP_INTENSITY_SCALE;
#else
            light.lightmapBakeType = LightmapBakeType.Mixed;
            light.shadows = light.type != LightType.Directional ? LightShadows.Soft : LightShadows.None;
            light.intensity = jsonLightObject.Multiplier * BASE_INTENSITY_SCALE;
#endif

            light.useColorTemperature = false;
            light.color = jsonLightObject.Color;
            light.spotAngle = jsonLightObject.Angle;
            light.innerSpotAngle = GetInnerAngle(jsonLightObject.Falloff, jsonLightObject.Attenuation);
            light.range = jsonLightObject.Range * RANGE_SCALE;
            
            target.transform.position = Vector3.zero;
            target.transform.rotation = Quaternion.identity;
            target.transform.SetParent(root.transform, true);

            target.transform.localPosition = jsonLightObject.Pos;
            target.transform.localRotation = jsonLightObject.Rot;
            target.transform.localScale = jsonLightObject.Scale;

            AnimationClip clip = MakeLightAnimationFromFramesForObject(jsonString, frames, target, root);

            if (jsonLightObject != null)
            {
                clip.name = jsonLightObject.LinkId;
                SaveStagingAnimationClip(jsonLightObject.LinkId, jsonLightObject.Name, clip);
                SetupLight(jsonLightObject, root, clip);
            }
        }
        
        public float GetInnerAngle(float fall, float att)
        {
            return (fall + att) / 2;
        }

        AnimationClip MakeLightAnimationFromFramesForObject(string jsonString, List<UnityLinkManager.DeserializedLightFrames> frames, GameObject target, GameObject root)
        {
            /*
             * This presupposes that the light is structured as follows (NB: ALL global and local positions/rotations at zero)
             * 
             * Root GameObject (with <Animator> component)            | GetAnimatableBindings ROOT object
             *       |
             *       --> Child GameObject (with <Light> component)    | GetAnimatableBindings TARGET object                
            */

            // check for changes across the timeline

            float threshold = 0.0001f;

            int active = frames.FindAll(x => x.Active == true).Count();
            active_delta = !(active == 0 || active == frames.Count);
            foreach (var frame in frames)
            {
                if (Math.Abs(frame.PosX - frames[0].PosX) > threshold) { pos_delta = true; }
                if (Math.Abs(frame.PosY - frames[0].PosY) > threshold) { pos_delta = true; }
                if (Math.Abs(frame.PosZ - frames[0].PosZ) > threshold) { pos_delta = true; }

                if (Math.Abs(frame.RotX - frames[0].RotX) > threshold) { rot_delta = true; }
                if (Math.Abs(frame.RotY - frames[0].RotY) > threshold) { rot_delta = true; }
                if (Math.Abs(frame.RotZ - frames[0].RotZ) > threshold) { rot_delta = true; }
                if (Math.Abs(frame.RotW - frames[0].RotW) > threshold) { rot_delta = true; }

                if (Math.Abs(frame.ScaleX - frames[0].ScaleX) > threshold) { scale_delta = true; }
                if (Math.Abs(frame.ScaleY - frames[0].ScaleY) > threshold) { scale_delta = true; }
                if (Math.Abs(frame.ScaleZ - frames[0].ScaleZ) > threshold) { scale_delta = true; }

                if (Math.Abs(frame.ColorR - frames[0].ColorR) > threshold) { color_delta = true; }
                if (Math.Abs(frame.ColorG - frames[0].ColorG) > threshold) { color_delta = true; }
                if (Math.Abs(frame.ColorB - frames[0].ColorB) > threshold) { color_delta = true; }

                if (Math.Abs(frame.Multiplier - frames[0].Multiplier) > threshold) { mult_delta = true; }
                if (Math.Abs(frame.Range - frames[0].Range) > threshold) { range_delta = true; }
                if (Math.Abs(frame.Angle - frames[0].Angle) > threshold) { angle_delta = true; }
                if (Math.Abs(frame.Falloff - frames[0].Falloff) > threshold) { fall_delta = true; }
                if (Math.Abs(frame.Attenuation - frames[0].Attenuation) > threshold) { att_delta = true; }
                if (Math.Abs(frame.Darkness - frames[0].Darkness) > threshold) { dark_delta = true; }
            }
            
            List<bool> changes = new List<bool>() { pos_delta, rot_delta, scale_delta, active_delta, color_delta, mult_delta, range_delta, angle_delta, fall_delta, att_delta, dark_delta };

            AnimationClip clip = new AnimationClip();

            if (changes.FindAll(x => x == true).Count() == 0)
            {
                // if there are no changes then no anim is needed
                clip.name = "EMPTY";
                return clip;
            }

            EditorCurveBinding[] bindable = AnimationUtility.GetAnimatableBindings(target, root);
            // Find binding for property

            // Transform properties
            // Rotation
            var b_rotX = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("LocalRotation.x"));
            var b_rotY = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("LocalRotation.y"));
            var b_rotZ = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("LocalRotation.z"));
            var b_rotW = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("LocalRotation.w"));

            // Position
            var b_posX = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("LocalPosition.x"));
            var b_posY = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("LocalPosition.y"));
            var b_posZ = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("LocalPosition.z"));

            // Scale
            var b_scaX = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("LocalScale.x"));
            var b_scaY = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("LocalScale.y"));
            var b_scaZ = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("LocalScale.z"));
                        
            // Proxy Enabled
            var b_enabled = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("ProxyActive"));

            // Proxy Color
            var b_colR = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("ProxyColor_r"));
            var b_colG = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("ProxyColor_g"));
            var b_colB = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("ProxyColor_b"));

            // Other Proxy Settings
            var b_mult = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("ProxyMultiplier"));
            var b_range = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("ProxyRange"));
            var b_angle = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("ProxyAngle"));
            var b_fall = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("ProxyFalloff"));
            var b_att = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("ProxyAttenuation"));
            var b_dark = bindable.ToList().FirstOrDefault(x => x.propertyName.iContains("ProxyDarkness"));

            // original - retain
            /*
            // Transform properties
            // Rotation
            var b_rotX = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("LocalRotation.x", System.StringComparison.InvariantCultureIgnoreCase));
            var b_rotY = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("LocalRotation.y", System.StringComparison.InvariantCultureIgnoreCase));
            var b_rotZ = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("LocalRotation.z", System.StringComparison.InvariantCultureIgnoreCase));
            var b_rotW = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("LocalRotation.w", System.StringComparison.InvariantCultureIgnoreCase));

            // Position
            var b_posX = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("LocalPosition.x", System.StringComparison.InvariantCultureIgnoreCase));
            var b_posY = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("LocalPosition.y", System.StringComparison.InvariantCultureIgnoreCase));
            var b_posZ = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("LocalPosition.z", System.StringComparison.InvariantCultureIgnoreCase));

            // Scale
            var b_scaX = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("LocalScale.x", System.StringComparison.InvariantCultureIgnoreCase));
            var b_scaY = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("LocalScale.y", System.StringComparison.InvariantCultureIgnoreCase));
            var b_scaZ = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("LocalScale.z", System.StringComparison.InvariantCultureIgnoreCase));

            // Proxy Enabled
            var b_enabled = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("ProxyActive", System.StringComparison.InvariantCultureIgnoreCase));

            // Proxy Color
            var b_colR = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("ProxyColor_r", System.StringComparison.InvariantCultureIgnoreCase));
            var b_colG = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("ProxyColor_g", System.StringComparison.InvariantCultureIgnoreCase));
            var b_colB = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("ProxyColor_b", System.StringComparison.InvariantCultureIgnoreCase));

            // Other Proxy Settings
            var b_mult = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("ProxyMultiplier", System.StringComparison.InvariantCultureIgnoreCase));
            var b_range = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("ProxyRange", System.StringComparison.InvariantCultureIgnoreCase));
            var b_angle = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("ProxyAngle", System.StringComparison.InvariantCultureIgnoreCase));
            var b_fall = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("ProxyFalloff", System.StringComparison.InvariantCultureIgnoreCase));
            var b_att = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("ProxyAttenuation", System.StringComparison.InvariantCultureIgnoreCase));
            var b_dark = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("ProxyDarkness", System.StringComparison.InvariantCultureIgnoreCase));
            */
            // Make keyframe[] for each bindable property

            // Transform properties
            // Rotation
            Keyframe[] f_rotX = new Keyframe[frames.Count];
            Keyframe[] f_rotY = new Keyframe[frames.Count];
            Keyframe[] f_rotZ = new Keyframe[frames.Count];
            Keyframe[] f_rotW = new Keyframe[frames.Count];

            // Position
            Keyframe[] f_posX = new Keyframe[frames.Count];
            Keyframe[] f_posY = new Keyframe[frames.Count];
            Keyframe[] f_posZ = new Keyframe[frames.Count];

            // Scale
            Keyframe[] f_scaX = new Keyframe[frames.Count];
            Keyframe[] f_scaY = new Keyframe[frames.Count];
            Keyframe[] f_scaZ = new Keyframe[frames.Count];

            // Enabled
            Keyframe[] f_enabled = new Keyframe[frames.Count];

            // Color
            Keyframe[] f_colR = new Keyframe[frames.Count];
            Keyframe[] f_colG = new Keyframe[frames.Count];
            Keyframe[] f_colB = new Keyframe[frames.Count];

            // Other
            Keyframe[] f_mult = new Keyframe[frames.Count];
            Keyframe[] f_range = new Keyframe[frames.Count];
            Keyframe[] f_angle = new Keyframe[frames.Count];
            Keyframe[] f_fall = new Keyframe[frames.Count];
            Keyframe[] f_att = new Keyframe[frames.Count];
            Keyframe[] f_dark = new Keyframe[frames.Count];

            // Populate keyframe arrays
            for (int i = 0; i < frames.Count; i++)
            {
                // Transform curves
                // Rotation
                f_rotX[i] = new Keyframe(frames[i].Time, frames[i].Rot.x);
                f_rotY[i] = new Keyframe(frames[i].Time, frames[i].Rot.y);
                f_rotZ[i] = new Keyframe(frames[i].Time, frames[i].Rot.z);
                f_rotW[i] = new Keyframe(frames[i].Time, frames[i].Rot.w);

                // Position
                f_posX[i] = new Keyframe(frames[i].Time, frames[i].Pos.x);
                f_posY[i] = new Keyframe(frames[i].Time, frames[i].Pos.y);
                f_posZ[i] = new Keyframe(frames[i].Time, frames[i].Pos.z);

                //scale
                f_scaX[i] = new Keyframe(frames[i].Time, frames[i].Scale.x);
                f_scaY[i] = new Keyframe(frames[i].Time, frames[i].Scale.y);
                f_scaZ[i] = new Keyframe(frames[i].Time, frames[i].Scale.z);

                // Enabled
                f_enabled[i] = new Keyframe(frames[i].Time, frames[i].Active == true ? 1f : 0f);

                // Color
                f_colR[i] = new Keyframe(frames[i].Time, frames[i].Color.r);
                f_colG[i] = new Keyframe(frames[i].Time, frames[i].Color.g);
                f_colB[i] = new Keyframe(frames[i].Time, frames[i].Color.b);

                // Other
                f_mult[i] = new Keyframe(frames[i].Time, frames[i].Multiplier);
                f_range[i] = new Keyframe(frames[i].Time, frames[i].Range);
                f_angle[i] = new Keyframe(frames[i].Time, frames[i].Angle);
                f_fall[i] = new Keyframe(frames[i].Time, frames[i].Falloff);
                f_att[i] = new Keyframe(frames[i].Time, frames[i].Attenuation);
                f_dark[i] = new Keyframe(frames[i].Time, frames[i].Darkness);
            }

            // bind all keyframes to the appropriate curve
            // only bind curves that have changes

            // Rotation           
            AnimationCurve c_rotX = SmoothCurve(f_rotX);//new AnimationCurve(f_rotX);
            AnimationUtility.SetEditorCurve(clip, b_rotX, c_rotX);
            AnimationCurve c_rotY = SmoothCurve(f_rotY);//new AnimationCurve(f_rotY);
            AnimationUtility.SetEditorCurve(clip, b_rotY, c_rotY);
            AnimationCurve c_rotZ = SmoothCurve(f_rotZ);//new AnimationCurve(f_rotZ);
            AnimationUtility.SetEditorCurve(clip, b_rotZ, c_rotZ);
            AnimationCurve c_rotW = SmoothCurve(f_rotW);//new AnimationCurve(f_rotW);
            AnimationUtility.SetEditorCurve(clip, b_rotW, c_rotW);

            // Position
            AnimationCurve c_posX = SmoothCurve(f_posX);//new AnimationCurve(f_posX);
            AnimationUtility.SetEditorCurve(clip, b_posX, c_posX);
            AnimationCurve c_posY = SmoothCurve(f_posY);//new AnimationCurve(f_posY);
            AnimationUtility.SetEditorCurve(clip, b_posY, c_posY);
            AnimationCurve c_posZ = SmoothCurve(f_posZ);//new AnimationCurve(f_posZ);
            AnimationUtility.SetEditorCurve(clip, b_posZ, c_posZ);

            //scale
            AnimationCurve c_scaX = ReduceCurve(f_scaX);  //new AnimationCurve(f_scaX);
            AnimationUtility.SetEditorCurve(clip, b_scaX, c_scaX);
            AnimationCurve c_scaY = ReduceCurve(f_scaY);  //new AnimationCurve(f_scaY);
            AnimationUtility.SetEditorCurve(clip, b_scaY, c_scaY);
            AnimationCurve c_scaZ = ReduceCurve(f_scaZ);  //new AnimationCurve(f_scaZ);
            AnimationUtility.SetEditorCurve(clip, b_scaZ, c_scaZ);

            // Enabled
            if (active_delta)
            {
                AnimationCurve c_enabled = new AnimationCurve(f_enabled);
                AnimationUtility.SetEditorCurve(clip, b_enabled, c_enabled);
            }

            // Color
            if (color_delta)
            {
                AnimationCurve c_colR = new AnimationCurve(f_colR);
                AnimationUtility.SetEditorCurve(clip, b_colR, c_colR);
                AnimationCurve c_colG = new AnimationCurve(f_colG);
                AnimationUtility.SetEditorCurve(clip, b_colG, c_colG);
                AnimationCurve c_colB = new AnimationCurve(f_colB);
                AnimationUtility.SetEditorCurve(clip, b_colB, c_colB);
            }

            if (mult_delta)
            {
                AnimationCurve c_mult = new AnimationCurve(f_mult);
                AnimationUtility.SetEditorCurve(clip, b_mult, c_mult);
            }

            if (range_delta)
            {
                AnimationCurve c_range = new AnimationCurve(f_range);
                AnimationUtility.SetEditorCurve(clip, b_range, c_range);
            }

            if (angle_delta || fall_delta || att_delta)
            {
                AnimationCurve c_angle = new AnimationCurve(f_angle);
                AnimationUtility.SetEditorCurve(clip, b_angle, c_angle);
                AnimationCurve c_fall = new AnimationCurve(f_fall);
                AnimationUtility.SetEditorCurve(clip, b_fall, c_fall);
                AnimationCurve c_att = new AnimationCurve(f_att);
                AnimationUtility.SetEditorCurve(clip, b_att, c_att);
            }

            if (dark_delta)
            {
                AnimationCurve c_dark = new AnimationCurve(f_dark);
                AnimationUtility.SetEditorCurve(clip, b_dark, c_dark);
            }

            clip.frameRate = 60f;
            return clip;
        }

        GameObject SetupLight(UnityLinkManager.JsonLightData json, GameObject root, AnimationClip clip)//, GameObject prefab = null)
        {
            if (LightProxyType == null)
            {
                LightProxyType = Physics.GetTypeInAssemblies("Reallusion.Runtime.LightProxy");
                if (LightProxyType == null)
                {
                    Debug.LogWarning("SetupLight cannot find the <LightProxy> class.");
                    return null;
                }
            }

            Component proxy = root.GetComponentInChildren(LightProxyType);
            if (proxy != null)
            {
                SetupLightMethod = proxy.GetType().GetMethod("SetupLight",
                                BindingFlags.Public | BindingFlags.Instance,
                                null,
                                CallingConventions.Any,
                                new Type[] { typeof(string) },
                                null);

                if (SetupLightMethod == null)
                {
                    Debug.LogWarning("SetupLight MethodInfo cannot be determined");
                    return null;
                }
            }
            else
            {
                Debug.LogWarning("SetupLight cannot find the <LightProxy> component.");
                return null;
            }

            json.pos_delta = pos_delta;
            json.rot_delta = rot_delta;
            json.scale_delta = scale_delta;
            json.active_delta = active_delta;
            json.color_delta = color_delta;
            json.mult_delta = mult_delta;
            json.range_delta = range_delta;
            json.angle_delta = angle_delta;
            json.fall_delta = fall_delta;
            json.att_delta = att_delta;
            json.dark_delta = dark_delta;

            string jsonString = JsonConvert.SerializeObject(json);
            SetupLightMethod.Invoke(proxy, new object[] { jsonString });
            
            GameObject prefab = GetPrefabAsset(json.LinkId, json.Name, root);            
            GameObject.DestroyImmediate(root);

            List<AnimationClip> clips = new List<AnimationClip>();
            clips.Add(clip);

            timelineKitList.Add((active_delta ? UnityLinkSceneManagement.TrackType.AnimationAndActivationTracks : UnityLinkSceneManagement.TrackType.AnimationTrack, prefab, clips, true, json.LinkId));
            //UnityLinkSceneManagement.CreateStagingSceneDependencies();
            return root;
        }
        #endregion Animated Light

        #region Asset storage
        void SaveStagingAnimationClip(string LinkId, string Name, AnimationClip clip)
        {
            if (!string.IsNullOrEmpty(importDestinationFolder))
            {
                string linkedName = Name + "_" + LinkId;
                string fullClipAssetPath = importDestinationFolder + "/" + UnityLinkManager.STAGING_IMPORT_SUBFOLDER + "/" + linkedName + "/" + linkedName + ".anim";
                string clipAssetPath = fullClipAssetPath.FullPathToUnityAssetPath();
                CheckUnityPath(Path.GetDirectoryName(clipAssetPath));

                Debug.LogWarning("Saving RLX animation to " + clipAssetPath);                
                if (File.Exists(fullClipAssetPath))
                {
                    AssetDatabase.DeleteAsset(clipAssetPath);
                    AssetDatabase.CreateAsset(clip, clipAssetPath);
                }
                else
                {
                    AssetDatabase.CreateAsset(clip, clipAssetPath);
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        void SavestagingPrefabAsset(string LinkId, string Name, GameObject toPrefab)
        {
            if (!string.IsNullOrEmpty(importDestinationFolder))
            {
                string linkedName = Name + "_" + LinkId;
                string fullPrefabAssetPath = importDestinationFolder + "/" + UnityLinkManager.STAGING_IMPORT_SUBFOLDER + "/" + linkedName + "/" + linkedName + ".prefab";
                string prefabAssetPath = fullPrefabAssetPath.FullPathToUnityAssetPath();
                CheckUnityPath(Path.GetDirectoryName(prefabAssetPath));
                PrefabUtility.SaveAsPrefabAsset(toPrefab, prefabAssetPath);
            }
        }

        GameObject GetPrefabAsset(string LinkId, string Name, GameObject toPrefab)
        {
            GameObject prefab = null;
            if (!string.IsNullOrEmpty(importDestinationFolder))
            {
                string linkedName = Name + "_" + LinkId;
                string fullPrefabAssetPath = importDestinationFolder + "/" + UnityLinkManager.STAGING_IMPORT_SUBFOLDER + "/" + linkedName + "/" + linkedName + ".prefab";
                string prefabAssetPath = fullPrefabAssetPath.FullPathToUnityAssetPath();
                CheckUnityPath(Path.GetDirectoryName(prefabAssetPath));
                prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(toPrefab, prefabAssetPath, InteractionMode.AutomatedAction);                
            }
            return prefab;
        }

        public static void CheckUnityPath(string path) // and create them in the AssetDatabase if needed
        {
            string[] strings = path.Split(new char[] { '\\', '/' });
            if (!strings[0].Equals("Assets", StringComparison.InvariantCultureIgnoreCase))
            {
                Debug.LogWarning("Not a Unity path.");
            }
            if (strings.Length == 1) return; // just Assets

            string pwd = strings[0];
            string parentFolder = pwd;
            for (int i = 1; i < strings.Length; i++)
            {
                pwd += "/" + strings[i];
                if (!AssetDatabase.IsValidFolder(pwd))
                {
                    Debug.LogWarning("Creating " + pwd);
                    AssetDatabase.CreateFolder(parentFolder, strings[i]);
                    AssetDatabase.Refresh();
                }
                parentFolder = pwd;
            }
        }
        #endregion Asset storage

        #region Keyframe Reduction
        public AnimationCurve SmoothCurve(Keyframe[] keys)
        {
            AnimationCurve curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
            {
                curve.SmoothTangents(i, 0);
            }
            return curve;
        }

        public AnimationCurve ReduceCurve(Keyframe[] keys, bool smooth = false)
        {
            Keyframe[] reduced = Reduce(keys, 0.0001f);
            AnimationCurve result = new AnimationCurve(reduced);
            if (smooth)
            {
                for (int i = 0; i < reduced.Length; i++)
                {
                    result.SmoothTangents(i, 0);
                }
            }
            return result;
        }

        public Keyframe[] Reduce(Keyframe[] keys, float threshold)
        {
            List<Keyframe> result = new List<Keyframe>();
            Keyframe lastAdded = new Keyframe();
            for (int i = 0; i < keys.Length; i++)
            {
                if (i == 0 || i == keys.Length - 1)
                {
                    result.Add(keys[i]);
                    lastAdded = keys[i];
                    continue;
                }

                if ((keys[i].value - lastAdded.value) > threshold)
                {
                    result.Add(keys[i]);
                    lastAdded = keys[i];
                }
            }
            return result.ToArray();
        }
        #endregion Keyframe Reduction
                
        #region Motion ... placeholder
        public void ImportMotion(string fbxPath, string linkId)
        {
            if (string.IsNullOrEmpty(fbxPath)) { Debug.LogWarning("Cannot import asset..."); return; }
            importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            AssetDatabase.WriteImportSettingsIfDirty(importer.assetPath);
            AssetDatabase.ImportAsset(importer.assetPath, ImportAssetOptions.ForceUpdate);

            return;
            // find linkId character in scene and use its avatar for:
            //
            // importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            // importer.sourceAvatar = avatar;
            //
            // update animator or timeline

            if (importer.defaultClipAnimations.Length > 0)
            {
                if (importer.clipAnimations == null || importer.clipAnimations.Length == 0)
                    importer.clipAnimations = importer.defaultClipAnimations;
            }

            ModelImporterClipAnimation[] animations = importer.clipAnimations;
            if (animations == null) return;

            bool changed = false;
            bool forceUpdate = true;

            foreach (ModelImporterClipAnimation anim in animations)
            {
                if (!anim.keepOriginalOrientation || !anim.keepOriginalPositionY || !anim.keepOriginalPositionXZ ||
                    !anim.lockRootRotation || !anim.lockRootHeightY)
                {
                    anim.keepOriginalOrientation = true;
                    anim.keepOriginalPositionY = true;
                    anim.keepOriginalPositionXZ = true;
                    anim.lockRootRotation = true;
                    anim.lockRootHeightY = true;
                    changed = true;
                }

                if (anim.name.iContains("idle") && !anim.lockRootPositionXZ)
                {
                    anim.lockRootPositionXZ = true;
                    changed = true;
                }

                if (anim.name.iContains("_loop") && !anim.loopTime)
                {
                    anim.loopTime = true;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.clipAnimations = animations;
                if (forceUpdate)
                {
                    AssetDatabase.WriteImportSettingsIfDirty(importer.assetPath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }
        }

        public static void GenerateCharacterTargetedAnimations(string motionAssetPath,
            GameObject targetCharacterModel, bool replaceIfExists)
        {
            AnimationClip[] clips = Util.GetAllAnimationClipsFromCharacter(motionAssetPath);

            if (!targetCharacterModel) targetCharacterModel = Util.FindCharacterPrefabAsset(motionAssetPath);
            if (!targetCharacterModel) return;

            string firstPath = null;

            if (clips.Length > 0)
            {
                int index = 0;
                foreach (AnimationClip clip in clips)
                {
                    string assetPath = GenerateClipAssetPath(clip, motionAssetPath, AnimRetargetGUI.RETARGET_SOURCE_PREFIX, true);
                    if (string.IsNullOrEmpty(firstPath)) firstPath = assetPath;
                    if (File.Exists(assetPath) && !replaceIfExists) continue;
                    AnimationClip workingClip = AnimPlayerGUI.CloneClip(clip);
                    AnimRetargetGUI.RetargetBlendShapes(clip, workingClip, targetCharacterModel, false);
                    AnimationClip asset = AnimRetargetGUI.WriteAnimationToAssetDatabase(workingClip, assetPath, false);
                    index++;
                }
                /*
                if (!string.IsNullOrEmpty(firstPath))
                    AnimPlayerGUI.UpdateAnimatorClip(CharacterAnimator,
                                                     AssetDatabase.LoadAssetAtPath<AnimationClip>(firstPath));
                */
            }
        }
        static string GenerateClipAssetPath(AnimationClip originalClip, string characterFbxPath, string prefix = "", bool overwrite = false)
        {
            if (!originalClip || string.IsNullOrEmpty(characterFbxPath)) return null;

            string characterName = Path.GetFileNameWithoutExtension(characterFbxPath);
            string fbxFolder = Path.GetDirectoryName(characterFbxPath);
            string animFolder = Path.Combine(fbxFolder, AnimRetargetGUI.ANIM_FOLDER_NAME, characterName);
            Util.EnsureAssetsFolderExists(animFolder);
            string clipName = originalClip.name;
            if (clipName.iStartsWith(characterName + "_"))
                clipName = clipName.Remove(0, characterName.Length + 1);

            if (string.IsNullOrEmpty(prefix))
            {
                string clipPath = AssetDatabase.GetAssetPath(originalClip);
                string clipFile = Path.GetFileNameWithoutExtension(clipPath);
                if (!clipPath.iEndsWith(".anim")) prefix = clipFile;
            }

            string animName = AnimRetargetGUI.NameAnimation(characterName, clipName, prefix);
            string assetPath = Path.Combine(animFolder, animName + ".anim");

            if (!overwrite)
            {
                if (!Util.AssetPathIsEmpty(assetPath))
                {
                    for (int i = 0; i < 999; i++)
                    {
                        string extension = string.Format("{0:000}", i);
                        assetPath = Path.Combine(animFolder, animName + "_" + extension + ".anim");
                        if (Util.AssetPathIsEmpty(assetPath)) break;
                    }
                }
            }

            return assetPath;
        }
        #endregion Motion...

    }
}
