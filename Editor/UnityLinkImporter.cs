using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

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

        public UnityLinkImporter(UnityLinkManager.QueueItem item)
        {
            QueueItem = item;
            opCode = QueueItem.OpCode;


        }

        public void Import()
        {
            string assetPath = string.Empty;
            
            switch (opCode)
            {
                case UnityLinkManager.OpCodes.MOTION:
                    {
                        assetPath = QueueItem.Motion.Path;
                        importMotion = true;
                        break;
                    }
                case UnityLinkManager.OpCodes.CHARACTER:
                    {
                        assetPath = QueueItem.Character.Path;
                        importAvatar = true;
                        break;
                    }
            }

            if (!string.IsNullOrEmpty(assetPath))
            {
                fbxPath = RetrieveDiskAsset(assetPath);
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

            if (importMotion)
            {
                ImportMotion(fbxPath, QueueItem.Motion.LinkId);
            }

            if (importAvatar)
            {
                ImportAvatar(fbxPath, QueueItem.Character.LinkId);
            }
        }

        public string RetrieveDiskAsset(string assetPath)
        {
            string inProjectAssetPath = string.Empty;

            string assetFolder = Path.GetDirectoryName(assetPath);
            string assetFolderName = Path.GetFileName(assetFolder);            

            string PARENT_FOLDER = Path.Combine(new string[] { ROOT_FOLDER, IMPORT_PARENT });
            if (!AssetDatabase.IsValidFolder(PARENT_FOLDER)) AssetDatabase.CreateFolder(ROOT_FOLDER, IMPORT_PARENT);
            string IMPORT_PATH = Path.Combine(new string[] { ROOT_FOLDER, IMPORT_PARENT, IMPORT_FOLDER });
            if (!AssetDatabase.IsValidFolder(IMPORT_PATH)) AssetDatabase.CreateFolder(PARENT_FOLDER, IMPORT_FOLDER);

            string proposedDestinationFolder = Path.Combine(new string[] { ROOT_FOLDER, IMPORT_PARENT, IMPORT_FOLDER, assetFolderName });
            string destinationFolder = string.Empty;

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
            
            FileUtil.CopyFileOrDirectory(assetFolder, destinationFolder);
            AssetDatabase.Refresh();
            

            string[] guids = AssetDatabase.FindAssets("t:Model", new string[] { destinationFolder });
            string guid = string.Empty;

            foreach (string g in guids)
            {
                string projectAssetPath = AssetDatabase.GUIDToAssetPath(g);
                if (opCode == UnityLinkManager.OpCodes.CHARACTER)
                {
                    if (Util.IsCC3CharacterAtPath(projectAssetPath))
                    {
                        string name = Path.GetFileNameWithoutExtension(projectAssetPath);
                        Debug.Log("Valid CC character: " + name + " found.");
                        guid = g;
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
                        guid = g;
                        inProjectAssetPath = AssetDatabase.GUIDToAssetPath(g);
                        break;
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

        public void ImportAvatar(string fbxPath, string linkId)
        {
            string guid = AssetDatabase.AssetPathToGUID(fbxPath);
            Debug.Log("Creating new characterinfo with guid " + guid);
            Debug.Log("Guid path " + AssetDatabase.AssetPathToGUID(fbxPath));
            CharacterInfo c = new CharacterInfo(guid);

            c.linkId = linkId;
            c.exportType = CharacterInfo.ExportType.AVATAR;
            c.projectName = "iclone project name";
            //c.sceneid = add this later

            c.BuildQuality = MaterialQuality.High;
            Importer import = new Importer(c);
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
        }

        public void ImportMotion(string fbxPath, string linkId)
        {
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


    }
}
