/* 
 * Copyright (C) 2026 Victor Soupday
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reallusion.Import
{
    public class GenericAnimProcessing
    {
        [MenuItem("Assets/Quick Animation Processing/Merge Generic Animations with Machanim", priority = 2010)]
        public static void InitAssetProcessing()
        {
            ProcessGenericModel(Selection.activeObject);
        }

        [MenuItem("Assets/Quick Animation Processing/Merge Generic Animations with Machanim", true)]
        public static bool ValidateInitAssetProcessing()
        {
            return IsModel(Selection.activeObject);
        }

        private static string[] modelFileExtensions = new string[] { ".fbx", ".blend", ".dae", ".obj" };

        public static bool IsModel(Object o)
        {
            string assetPath = AssetDatabase.GetAssetPath(o).ToLower();
            if (string.IsNullOrEmpty(assetPath)) return false;

            string extension = Path.GetExtension(assetPath);
            foreach (string ext in modelFileExtensions)
            {
                if (extension.Equals(ext, System.StringComparison.InvariantCultureIgnoreCase))
                {
                    //only check against file extension on the right-click menu
                    return true;
                }
            }
            return false;
        }

        public static Avatar humanoidAvatar;
        public static ModelImporterAnimationType animType;


        public static AnimationClip[] ProcessGenericClips(CharacterInfo info, AnimationClip[] humanoidClips, string motionAssetPath)
        {
            List<AnimationClip> clips = new List<AnimationClip>();
            try
            {
                ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(motionAssetPath);
                animType = importer.animationType;
                if (animType != ModelImporterAnimationType.Human) return humanoidClips;

                EditorUtility.DisplayProgressBar("Processing...1", "Working..", 0.75f);

                string duplicatePath = DuplicateModelAsset(animType, motionAssetPath);
                if (string.IsNullOrEmpty(duplicatePath)) return null;

                EditorUtility.DisplayProgressBar("Processing...2", "Working..", 0.75f);

                if (!SetImporterAnimationSettings(duplicatePath)) return null;

                EditorUtility.DisplayProgressBar("Processing...3", "Working..", 0.75f);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayProgressBar("Processing...4", "Working..", 0.75f);

                humanoidAvatar = FetchHumanAvatar(motionAssetPath);
                GameObject humanoidModel = AssetDatabase.LoadAssetAtPath<GameObject>(motionAssetPath);
                Object[] genericData = AssetDatabase.LoadAllAssetRepresentationsAtPath(duplicatePath);
                AnimationClip humanoidClip = null;
                foreach (var clip in humanoidClips)
                {
                    Debug.Log($"Available Clip: {clip.name}");
                }

                foreach (Object subObject in genericData)
                {
                    if (subObject.GetType().Equals(typeof(AnimationClip)))
                    {
                        if (subObject.name.iContains("T-Pose")) continue;

                        humanoidClip = humanoidClips.FirstOrDefault(x => x.name == subObject.name);
                        if (humanoidClip == null)
                        {
                            Debug.Log($"Cannot find humanoid clip corresponding to {subObject.name}");
                        }
                        else
                        {
                            AnimationClip mergedClip = MergeClip(humanoidModel, subObject as AnimationClip, humanoidClip);
                            clips.Add(mergedClip);
                        }
                    }
                }

                EditorUtility.DisplayProgressBar("Cleaning up...", "Working..", 0.75f);
                AssetDatabase.DeleteAsset(duplicatePath);

            }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message);
            }
            EditorUtility.ClearProgressBar();

            return clips.ToArray();
        }

        public static void ProcessGenericModel(Object o)
        {
            try
            {
                string assetPath = AssetDatabase.GetAssetPath(o);
                ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(assetPath);
                animType = importer.animationType;

                string duplicatePath = DuplicateModelAsset(animType, assetPath);
                if (string.IsNullOrEmpty(duplicatePath)) return;

                string genericAssetPath = animType == ModelImporterAnimationType.Generic ? assetPath : duplicatePath;
                string humanoidAssetPath = animType == ModelImporterAnimationType.Human ? assetPath : duplicatePath;

                EditorUtility.DisplayProgressBar("Processing...2", "Working..", 0.75f);

                if (animType == ModelImporterAnimationType.Human)
                    SetImporterAnimationSettings(genericAssetPath);

                EditorUtility.DisplayProgressBar("Processing...3", "Working..", 0.75f);

                if (animType == ModelImporterAnimationType.Generic)
                    SetImporterAnimationSettings(humanoidAssetPath);

                EditorUtility.DisplayProgressBar("Processing...4", "Working..", 0.75f);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"generic path: {genericAssetPath}  humanoid path: {humanoidAssetPath}");

                humanoidAvatar = FetchHumanAvatar(humanoidAssetPath);
                GameObject humanoidModel = AssetDatabase.LoadAssetAtPath<GameObject>(humanoidAssetPath);
                AnimationClip[] humanoidClips = Util.GetAllAnimationClipsFromCharacter(humanoidAssetPath);
                Object[] genericData = AssetDatabase.LoadAllAssetRepresentationsAtPath(genericAssetPath);
                foreach (Object subObject in genericData)
                {
                    if (subObject.GetType().Equals(typeof(AnimationClip)))
                    {
                        if (subObject.name.iContains("T-Pose")) continue;

                        AnimationClip humanoidClip = humanoidClips.FirstOrDefault(x => x.name == subObject.name);
                        if (humanoidClip == null)
                        {
                            Debug.Log($"Cannot find humanoid clip corresponding to {subObject.name}");
                        }
                        else
                        {
                            AnimationClip mergedClip = MergeClip(humanoidModel, subObject as AnimationClip, humanoidClip);

                            string workingDirectory = Path.GetDirectoryName(assetPath);
                            string animName = SanitizeName(subObject.name + " - Generic Merge - " + humanoidClip.name);
                            string fullOutputPath = workingDirectory + "/" + animName + ".anim";
                            EditorUtility.DisplayProgressBar($"Writing animation {animName}", "Working..", 0.75f);
                            AssetDatabase.CreateAsset(mergedClip, fullOutputPath);
                        }
                    }
                }

                EditorUtility.DisplayProgressBar("Cleaning up...", "Working..", 0.75f);
                AssetDatabase.DeleteAsset(duplicatePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message);
            }
            EditorUtility.ClearProgressBar();
        }

        public static string DuplicateModelAsset(ModelImporterAnimationType animType, string assetPath)
        {
            // Will deal with the right-click case where a newly imported generic model can be processed.
            // From the importer, the animType should *always* be human.
            string assetExt = Path.GetExtension(assetPath).ToLower();
            string assetName = Path.GetFileName(assetPath).Replace(assetExt, "");

            string tmpGenericName = "_RL_generic_extract_tmp_";
            string tmpGenericAssetPath = Path.GetDirectoryName(assetPath) + "/" + tmpGenericName + assetExt;

            string tmpHumanoidName = "_RL_humanoid_extract_tmp_";
            string tmpHumanoidAssetPath = Path.GetDirectoryName(assetPath) + "/" + tmpHumanoidName + assetExt;

            Debug.Log("Processing: " + assetName);
            Debug.Log($"Please ignore animation import warnings about files {tmpGenericName} or {tmpHumanoidName}");

            if (animType == ModelImporterAnimationType.Human)
            {
                if (!AssetDatabase.CopyAsset(assetPath, tmpGenericAssetPath))
                {
                    Debug.LogWarning("Temporary Asset could not be created: Generic animation extraction failed.");
                    return string.Empty;
                }
                else
                {
                    return tmpGenericAssetPath;
                }
            }
            else if (animType == ModelImporterAnimationType.Generic)
            {
                if (!AssetDatabase.CopyAsset(assetPath, tmpHumanoidAssetPath))
                {
                    Debug.LogWarning("Temporary Asset could not be created: Generic animation extraction failed.");
                    return string.Empty;
                }
                else
                {
                    return tmpHumanoidAssetPath;
                }
            }
            else
            {
                return string.Empty;
            }
        }

        public static bool SetImporterAnimationSettings(string assetPath)
        {
            ModelImporter genericImporter = (ModelImporter)AssetImporter.GetAtPath(assetPath);

            if (genericImporter != null)
            {
                genericImporter.animationType = ModelImporterAnimationType.Generic;
                ModelImporterClipAnimation[] animations = genericImporter.clipAnimations;
                if (animations == null) return false;

                foreach (ModelImporterClipAnimation anim in animations)
                {
                    anim.keepOriginalOrientation = true;
                    anim.keepOriginalPositionY = true;
                    anim.keepOriginalPositionXZ = true;
                    anim.lockRootRotation = true;
                    anim.lockRootHeightY = true;
                }
                AssetDatabase.WriteImportSettingsIfDirty(assetPath);
                return true;
            }
            return false;
        }

        public static Avatar FetchHumanAvatar(string assetPath)
        {
            Object[] humanoidData = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            foreach (Object subObject in humanoidData)
            {
                if (subObject.GetType().Equals(typeof(Avatar)))
                {
                    return subObject as Avatar;
                }
            }
            return null;
        }

        private static string SanitizeName(string inputName)
        {
            inputName = inputName.Replace("(Clone)", "");
            string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            Regex r = new Regex(string.Format("[{0}]", Regex.Escape(invalid)));
            return r.Replace(inputName, " - ");
        }

        public static AnimationClip MergeClip(GameObject humanoidModel, AnimationClip genericClip, AnimationClip humanoidClip)
        {
            AnimationClip workingHumanoidClip = (AnimationClip)Object.Instantiate(humanoidClip);
            workingHumanoidClip.name = humanoidClip.name;
            Transform[] humanoidTransforms = humanoidModel.GetComponentsInChildren<Transform>();
            List<string> nonAvatarTransforms = new List<string>();
            List<HumanBone> humanBones = humanoidAvatar.humanDescription.human.ToList();

            foreach (Transform t in humanoidTransforms)
            {
                if (humanBones.FindIndex(x => x.boneName == t.name) == -1)
                    nonAvatarTransforms.Add(t.name);
            }

            EditorCurveBinding[] genericCurveBindings = AnimationUtility.GetCurveBindings(genericClip);
            int counter = 0;
            foreach (var curve in genericCurveBindings)
            {
                counter++;
                if (curve.propertyName.ToLower().Contains("blendshape")) continue;
                bool bind = false;

                string[] splits = curve.path.Split('/');
                string last = splits[splits.Length - 1];
                if (nonAvatarTransforms.Contains(last))
                    bind = true;

                float progress = (float)counter / (float)genericCurveBindings.Length;
                EditorUtility.DisplayProgressBar($"Merging Animation {genericClip.name}...", $"{counter}/{genericCurveBindings.Length} Working on {last} ", progress);

                if (bind)
                {
                    AnimationCurve genericCurve = AnimationUtility.GetEditorCurve(genericClip, curve);
                    AnimationUtility.SetEditorCurve(workingHumanoidClip, curve, genericCurve);
                }
            }
            return workingHumanoidClip;
        }
    }
}