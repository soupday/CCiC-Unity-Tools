using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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

            switch (opCode)
            {
                case UnityLinkManager.OpCodes.MOTION:
                    {
                        if (packageType == PackageType.DISK) { assetPath = QueueItem.Motion.Path; }
                        importMotion = true;
                        break;
                    }
                case UnityLinkManager.OpCodes.CHARACTER:
                    {
                        if (packageType == PackageType.DISK) { assetPath = QueueItem.Character.Path; }
                        importAvatar = true;
                        break;
                    }
                case UnityLinkManager.OpCodes.PROP:
                    {
                        if (packageType == PackageType.DISK) { assetPath = QueueItem.Prop.Path; }
                        importProp = true;
                        break;
                    }
            }


            if (packageType == PackageType.DISK)
            {
                if (!string.IsNullOrEmpty(assetPath))
                {
                    string assetFolder = Path.GetDirectoryName(assetPath);
                    fbxPath = RetrieveDiskAsset(assetFolder, QueueItem.Name);
                    Directory.Delete(assetFolder, true);
                }
            }
            else if (packageType == PackageType.ZIP)
            {
                string zipPath = Path.Combine(UnityLinkManager.EXPORTPATH, QueueItem.RemoteId + ".zip");
                string zipFolder = Path.Combine(UnityLinkManager.EXPORTPATH, QueueItem.RemoteId);
                ZipFile.ExtractToDirectory(zipPath, zipFolder);
                File.Delete(zipPath);
                fbxPath = RetrieveDiskAsset(zipFolder, QueueItem.Name);
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

            /*
            if (AssetDatabase.IsValidFolder(proposedDestinationFolder))
            {
                for (int i = 0; i < 999; i++)
                {
                    string suffix = "." + i.ToString();
                    string testFolder = Path.Combine(new string[] { ROOT_FOLDER, IMPORT_PARENT, IMPORT_FOLDER, assetFolderName + suffix });
                    if (AssetDatabase.IsValidFolder(testFolder))
                        continue;
                    else
                    {
                        destinationFolder = testFolder;
                        break;
                    }
                }
            }
            else
            {
                destinationFolder = proposedDestinationFolder;            
            }
            */

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

            Debug.Log("RetrieveDiskAsset: inProjectAssetPath" + inProjectAssetPath);
            return inProjectAssetPath;
        }

        public void CleanDiskAssets(string fbxPath, string queueItemPath)
        {
            // clear motion fbx (retain animations - deal with clutter later)
            // move model data to user nominated place in project
            // remove external disk assets
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

        void AddToSceneAndTimeLine((GameObject, List<AnimationClip>) objectTuple)
        {
            Debug.LogWarning("Instantiating " + objectTuple.Item1.name);
            GameObject sceneObject = GameObject.Instantiate(objectTuple.Item1);
            sceneObject.transform.position = Vector3.zero;
            sceneObject.transform.rotation = Quaternion.identity;

            PlayableDirector director = UnityLinkManager.timelineObject.GetComponent<PlayableDirector>();
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
    }
}
