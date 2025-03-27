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

namespace Reallusion.Import
{
    public class UnityLinkImporter
    {
        ModelImporter importer;
        UnityLinkManager.QueueItem QueueItem;
        UnityLinkManager.OpCodes opCode;
        string ROOT_FOLDER = "Assets";
        string IMPORT_PARENT = "Reallusion";
        string IMPORT_FOLDER = "DataLink_Imports";
        string fbxPath = string.Empty;
        bool importMotion = false;
        bool importAvatar = false;
        bool importProp = false;
        bool importLights = false;
        bool importIntoScene = false;
        
        PackageType packageType = PackageType.NONE;

        public UnityLinkImporter(UnityLinkManager.QueueItem item, bool sceneImport)
        {
            QueueItem = item;
            opCode = QueueItem.OpCode;
            importIntoScene = sceneImport;

            if (string.IsNullOrEmpty(item.RemoteId))
            {
                packageType = PackageType.DISK;
            }
            else
            {
                packageType = PackageType.ZIP;
            }
        }

        public enum PackageType
        {
            NONE = 0,
            DISK = 1,
            ZIP = 2,
        }

        public void Import()
        {
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
                case UnityLinkManager.OpCodes.LIGHTS:
                    {
                        if (packageType == PackageType.DISK) { assetPath = QueueItem.Lights.Path; }                        
                        name = Path.GetFileName(Path.GetDirectoryName(QueueItem.Lights.Path));
                        importLights = true;
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

        int frame = 0;
        void WaitForFrames()
        {
            if (frame < 10)
            {
                frame++;
                return;
            }
            EditorApplication.update -= WaitForFrames;
            frame = 0;
            DoImport();
        }

        void DoImport()
        {
            AssetDatabase.Refresh();
            //GameObject prefab = null;

            if (importIntoScene && !UnityLinkManager.timelineSceneCreated)
            {
                Debug.LogWarning("Creating new scene");
                CreateSceneAndTimeline();
            }

            (GameObject, List<AnimationClip>) timelineKit = (null, null);
            List<(GameObject, List<AnimationClip>)> timeLineKitList = new List<(GameObject, List<AnimationClip>)>();

            if (importMotion)
            {
                ImportMotion(fbxPath, QueueItem.Motion.LinkId);
            }

            if (importAvatar)
            {
                timelineKit = ImportAvatar(fbxPath, QueueItem.Character.LinkId);
            }

            if (importProp)
            {
                timelineKit = ImportProp(fbxPath, QueueItem.Prop.LinkId);
            }

            if (importLights)
            {
                timeLineKitList = ImportLights(fbxPath, QueueItem.Lights.LinkIds);
            }

            if (importIntoScene)
            {
                AddToSceneAndTimeLine(timelineKit);
                SelectTimeLineObjectAndShowWindow();
            }

            
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

        public string RetrieveDiskAsset(string assetFolder, string name)
        {
            string inProjectAssetPath = string.Empty;

            //string assetFolder = Path.GetDirectoryName(assetPath);
            string assetFolderName = name; // Path.GetFileName(assetFolder);            

            string PARENT_FOLDER = Path.Combine(new string[] { ROOT_FOLDER, IMPORT_PARENT });
            if (!AssetDatabase.IsValidFolder(PARENT_FOLDER)) AssetDatabase.CreateFolder(ROOT_FOLDER, IMPORT_PARENT);
            string IMPORT_PATH = Path.Combine(new string[] { ROOT_FOLDER, IMPORT_PARENT, IMPORT_FOLDER });
            if (!AssetDatabase.IsValidFolder(IMPORT_PATH)) AssetDatabase.CreateFolder(PARENT_FOLDER, IMPORT_FOLDER);

            string proposedDestinationFolder = Path.Combine(new string[] { ROOT_FOLDER, IMPORT_PARENT, IMPORT_FOLDER, assetFolderName });
            string destinationFolder = GetNonDuplicateFolderName(proposedDestinationFolder, true);//string.Empty;
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
            }
            catch (Exception ex) 
            {
                Debug.LogWarning("Cannot copy asset to AssetDatabase! " + ex.Message); return string.Empty;
            }
            FileUtil.CopyFileOrDirectory(assetFolder, destinationFolder);
            AssetDatabase.Refresh();
                        
            if (opCode == UnityLinkManager.OpCodes.LIGHTS) // non-fbx imports
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

        public void CleanDiskAssets(string fbxPath, string queueItemPath)
        {
            // clear motion fbx (retain animations - deal with clutter later)
            // move model data to user nominated place in project
            // remove external disk assets
        }

        public List<(GameObject, List<AnimationClip>)> ImportLights(string fbxPath, string[] linkIds)
        {
            if (string.IsNullOrEmpty(fbxPath)) { Debug.LogWarning("Cannot import asset..."); return new List<(GameObject, List<AnimationClip>)> { (null, null) }; }

            string dataPath = Path.GetDirectoryName(Application.dataPath);
            string fullFolderPath = Path.Combine(dataPath, fbxPath);
            string [] fileList = Directory.GetFiles(fullFolderPath);
            List<string> rlxList = fileList.ToList().FindAll(x => Path.GetExtension(x).Equals(".rlx"));
            Debug.Log("RLX files");
            foreach (string file in rlxList)
            {
                Debug.Log(file);
            }

            byte[] rlxBytes = File.ReadAllBytes(rlxList[0]);
            int bytePos = 0;
            // header code 4-bytes
            bytePos += 4;            
            int len = UnityLinkManager.GetCurrentEndianWord(UnityLinkManager.ExtractBytes(rlxBytes, bytePos, 4), UnityLinkManager.SourceEndian.BigEndian);
            Debug.Log("RLX JSON expected length " + len + " Available bytes in rlxBytes " + rlxBytes.Length);
            bytePos += 4;            
            byte[] jsonBytes = UnityLinkManager.ExtractBytes(rlxBytes, bytePos, len);
            string jsonString = Encoding.UTF8.GetString(jsonBytes);
            Debug.Log(jsonString);
            bytePos += len;
            int framesLen = UnityLinkManager.GetCurrentEndianWord(UnityLinkManager.ExtractBytes(rlxBytes, bytePos, 4), UnityLinkManager.SourceEndian.BigEndian);
            bytePos += 4;
            byte[] frameData = UnityLinkManager.ExtractBytes(rlxBytes, bytePos, framesLen);
            
            List<UnityLinkManager.DeserializedLightFrames> frames = new List<UnityLinkManager.DeserializedLightFrames>();

            Stream stream = new MemoryStream(frameData);
            stream.Position = 0;
            byte[] frameBytes = new byte[85];
            while(stream.Read(frameBytes, 0, frameBytes.Length) > 0)
            {
                frames.Add(new UnityLinkManager.DeserializedLightFrames(frameBytes));
            }

            Debug.Log("Frames processed " + frames.Count);
            //foreach(var f in frames)
            //{
            //    Debug.Log(f.ToString());
            //}
            GameObject root = new GameObject("Light Root");
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.AddComponent<Animator>();
            GameObject target = new GameObject("Animated Light");
            target.transform.position = Vector3.zero;
            target.transform.rotation = Quaternion.identity;
            target.transform.SetParent(root.transform, true);
            Light light = target.AddComponent<Light>();
            light.type = LightType.Directional;

            MakeLightAnimationFromFramesForObject(jsonString, frames, target, root);

            return new List<(GameObject, List<AnimationClip>)> { (null, null) };
        }

        public (GameObject, List<AnimationClip>) ImportProp(string fbxPath, string linkId)
        {
            if (string.IsNullOrEmpty(fbxPath)) { Debug.LogWarning("Cannot import asset..."); return (null, null); }
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
            data.prefabGuid = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(prefab)).ToString();
            data.fbxGuid = AssetDatabase.GUIDFromAssetPath(fbxPath).ToString();
            PrefabUtility.SavePrefabAsset(prefab);
            if (ImporterWindow.Current != null)
                ImporterWindow.Current.RefreshCharacterList();
            List<AnimationClip> animGuidsForTimeLine = importIntoScene ? import.clipListForTimeLine : new List<AnimationClip>();
            return (prefab, animGuidsForTimeLine);
        }

        public (GameObject, List<AnimationClip>) ImportAvatar(string fbxPath, string linkId)
        {
            if (string.IsNullOrEmpty(fbxPath)) { Debug.LogWarning("Cannot import asset..."); return (null, null); }
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
            data.prefabGuid = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(prefab)).ToString();
            data.fbxGuid = AssetDatabase.GUIDFromAssetPath(fbxPath).ToString(); 
            PrefabUtility.SavePrefabAsset(prefab);
            if (ImporterWindow.Current != null)
                ImporterWindow.Current.RefreshCharacterList();

            List<AnimationClip> animGuidsForTimeLine = importIntoScene ? import.clipListForTimeLine : new List<AnimationClip>();
            return (prefab, animGuidsForTimeLine);
        }

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


        void CreateSceneAndTimeline()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

            UnityLinkManager.timelineObject = new GameObject("RL_TimeLine_Object");
            PlayableDirector director = UnityLinkManager.timelineObject.AddComponent<PlayableDirector>();

            // PlayableGraph graph = PlayableGraph.Create(); // ...

            TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, "Assets/Timeline.playable");
            director.playableAsset = timeline;

            
            UnityLinkManager.timelineSceneCreated = true;
        }

        TimelineEditorWindow timelineEditorWindow = null;

        void SelectTimeLineObjectAndShowWindow()
        {
            Selection.activeObject = UnityLinkManager.timelineObject;
            
            if (EditorWindow.HasOpenInstances<TimelineEditorWindow>())
            {
                timelineEditorWindow = EditorWindow.GetWindow(typeof(TimelineEditorWindow)) as TimelineEditorWindow;
                timelineEditorWindow.locked = false;
                Selection.activeObject = UnityLinkManager.timelineObject;
                timelineEditorWindow.Repaint();
                timelineEditorWindow.locked = true;
            }
            else
            {
                EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline");
                timelineEditorWindow = EditorWindow.GetWindow(typeof(TimelineEditorWindow)) as TimelineEditorWindow;
                timelineEditorWindow.locked = false;
                Selection.activeObject = UnityLinkManager.timelineObject;
                timelineEditorWindow.Repaint();
                timelineEditorWindow.locked = true;
            }
        }

        void AddToSceneAndTimeLine((GameObject, List<AnimationClip>) objectTuple, bool createInScene = true)
        {
            GameObject sceneObject;
            if (createInScene)
            {
                Debug.LogWarning("Instantiating " + objectTuple.Item1.name);
                sceneObject = GameObject.Instantiate(objectTuple.Item1);
                sceneObject.transform.position = Vector3.zero;
                sceneObject.transform.rotation = Quaternion.identity;
            }
            else
            {
                sceneObject = objectTuple.Item1;
            }

            PlayableDirector director;
            if (UnityLinkManager.timelineObject == null)
            {
                director = (PlayableDirector)GameObject.FindFirstObjectByType(typeof(PlayableDirector));
            }
            else
            {
                director = UnityLinkManager.timelineObject.GetComponent<PlayableDirector>();
            }
            TimelineAsset timeline = director.playableAsset as TimelineAsset;
            AnimationTrack newTrack = timeline.CreateTrack<AnimationTrack>(objectTuple.Item1.name);
            AnimationClip clipToUse = null;
            // find suitable aniamtion clip (should be the first non T-Pose)
            foreach (AnimationClip animClip in objectTuple.Item2)
            {
                if (animClip.name.Contains("T-Pose", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }
                else
                {
                    clipToUse = animClip;
                }
            }

            TimelineClip clip = newTrack.CreateClip(clipToUse);
            clip.start = 0f;
            clip.timeScale = 1f;
            clip.duration = clip.duration / clip.timeScale;
            Debug.LogWarning("SetGenericBinding " + objectTuple.Item1.name + " - " + clipToUse.name);
            director.SetGenericBinding(newTrack, sceneObject);
            
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
            AnimationClip clip = new AnimationClip();

            EditorCurveBinding[] bindable = AnimationUtility.GetAnimatableBindings(target, root);
            // Find binding for property

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

            // Color
            var b_colR = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("Color.r", System.StringComparison.InvariantCultureIgnoreCase));
            var b_colG = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("Color.g", System.StringComparison.InvariantCultureIgnoreCase));
            var b_colB = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("Color.b", System.StringComparison.InvariantCultureIgnoreCase));
            var alphaColorBind = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("Color.a", System.StringComparison.InvariantCultureIgnoreCase));

            // Enabled
            var b_enabled = bindable.ToList().FirstOrDefault(x => x.propertyName.Contains("Enabled", System.StringComparison.InvariantCultureIgnoreCase));

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

            // Color
            Keyframe[] f_colR = new Keyframe[frames.Count];
            Keyframe[] f_colG = new Keyframe[frames.Count];
            Keyframe[] f_colB = new Keyframe[frames.Count];

            
            Keyframe[] f_enabled = new Keyframe[frames.Count];

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

                // Color
                f_colR[i] = new Keyframe(frames[i].Time, frames[i].Color.r);
                f_colG[i] = new Keyframe(frames[i].Time, frames[i].Color.g);
                f_colB[i] = new Keyframe(frames[i].Time, frames[i].Color.b);

                // Enabled
                f_enabled[i] = new Keyframe(frames[i].Time, frames[i].Active == true ? 1f : 0f);
            }

            // bind all keyframes to the appropriate curve
            // Transform curves
            // Rotation
            AnimationCurve c_rotX = new AnimationCurve(f_rotX);
            AnimationUtility.SetEditorCurve(clip, b_rotX, c_rotX);
            AnimationCurve c_rotY = new AnimationCurve(f_rotY);
            AnimationUtility.SetEditorCurve(clip, b_rotY, c_rotY);
            AnimationCurve c_rotZ = new AnimationCurve(f_rotZ);
            AnimationUtility.SetEditorCurve(clip, b_rotZ, c_rotZ);
            AnimationCurve c_rotW = new AnimationCurve(f_rotW);
            AnimationUtility.SetEditorCurve(clip, b_rotW, c_rotW);

            // Position
            AnimationCurve c_posX = new AnimationCurve(f_posX);
            AnimationUtility.SetEditorCurve(clip, b_posX, c_posX);
            AnimationCurve c_posY = new AnimationCurve(f_posY);
            AnimationUtility.SetEditorCurve(clip, b_posY, c_posY);
            AnimationCurve c_posZ = new AnimationCurve(f_posZ);
            AnimationUtility.SetEditorCurve(clip, b_posZ, c_posZ);

            //scale
            AnimationCurve c_scaX = new AnimationCurve(f_scaX);
            AnimationUtility.SetEditorCurve(clip, b_scaX, c_scaX);
            AnimationCurve c_scaY = new AnimationCurve(f_scaY);
            AnimationUtility.SetEditorCurve(clip, b_scaY, c_scaY);
            AnimationCurve c_scaZ = new AnimationCurve(f_scaZ);
            AnimationUtility.SetEditorCurve(clip, b_scaZ, c_scaZ);

            // Color
            AnimationCurve c_colR = new AnimationCurve(f_colR);
            AnimationUtility.SetEditorCurve(clip, b_colR, c_colR);
            AnimationCurve c_colG = new AnimationCurve(f_colG);
            AnimationUtility.SetEditorCurve(clip, b_colG, c_colG);
            AnimationCurve c_colB = new AnimationCurve(f_colB);
            AnimationUtility.SetEditorCurve(clip, b_colB, c_colB);

            clip.frameRate = 60f;

            Debug.LogWarning("Saving RLX animation to Assets/RLX_CLIP.anim");
            AssetDatabase.CreateAsset(clip, "Assets/RLX_CLIP.anim");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            List<AnimationClip> clips = new List<AnimationClip>();
            clips.Add(clip);
            Debug.LogWarning("Trying to add to timeline - will fail if no timeline is avaialble");
            try
            {
                AddToSceneAndTimeLine((root, clips), false);
            }
            catch
            {
                Debug.LogWarning("Failed");
            }
            return clip;
        }
    }
}
