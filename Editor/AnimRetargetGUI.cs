/* 
 * Copyright (C) 2021 Victor Soupday
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
using System.Text.RegularExpressions;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using System.IO;
using System.Runtime.Remoting.Metadata;

namespace Reallusion.Import
{
    public static class AnimRetargetGUI
    {
        // GUI variables
        private static Texture2D handImage;
        private static Texture2D closedMouthImage;
        private static Texture2D openMouthImage;
        private static Texture2D blendshapeImage;
        private static Texture2D saveImage;
        private static Texture2D resetImage;
        private static Texture2D unlockedImage;
        private static Texture2D lockedImage;

        private static float width = 313f;
        private static float height = 240f;
        //private static float baseControlWidth = 168f;
        private static float sliderWidth = 295f;
        private static float textWidth = 66f;
        private static float textHeight = 18f;
        private static float largeIconDim = 60f;
        private static float smallIconDim = 30f;

        private static float shRange = 30f; // Angular Ranges in degrees
        private static float aRange = 30f;
        private static float lRange = 30f;
        private static float hRange = 30f;

        private static float yRange = 0.2f; //Raw y input range

        // GUI Control variables (Reset to this state)

        private static bool holdValues = false;

        private static int handPose = 0;
        private static bool closeMouth = false;
        private static float shoulderOffset = 0f;
        private static float armOffset = 0f;
        private static float armFBOffset = 0f;
        private static float backgroundArmOffset = 0f;
        private static float legOffset = 0f;
        private static float heelOffset = 0f;
        private static float heightOffset = 0f;

        private static Styles styles;
        private static bool expressionDrivenBones = true;
        private static bool expressionBlendShapeTranspose = true;
        private static bool expressionConstrain = true;
        private static bool createFullAnimationTrack = false;
        private static bool logOnce = false;

        private static AnimationClip OriginalClip => AnimPlayerGUI.OriginalClip;
        private static AnimationClip WorkingClip => AnimPlayerGUI.WorkingClip;
        private static Animator CharacterAnimator => AnimPlayerGUI.CharacterAnimator;

        private static Vector3 animatorPosition;
        private static Quaternion animatorRotation;

        // Function variables        
        public const string ANIM_FOLDER_NAME = "Animations";
        public const string RETARGET_FOLDER_NAME = "Retargeted";
        public const string RETARGET_SOURCE_PREFIX = "Imported";

        private static Dictionary<string, EditorCurveBinding> shoulderBindings;
        private static Dictionary<string, EditorCurveBinding> armBindings;
        private static Dictionary<string, EditorCurveBinding> armFBBindings;
        private static Dictionary<string, EditorCurveBinding> legBindings;
        private static Dictionary<string, EditorCurveBinding> heelBindings;
        private static Dictionary<string, EditorCurveBinding> heightBindings;

        public static void OpenRetargeter()//(PreviewScene ps, GameObject fbx)
        {
            if (!IsPlayerShown())
            {
#if SCENEVIEW_OVERLAY_COMPATIBLE
                //2021.2.0a17+  When GUI.Window is called from a static SceneView delegate, it is broken in 2021.2.0f1 - 2021.2.1f1
                //so we switch to overlays starting from an earlier version
                AnimRetargetOverlay.ShowAll();
#else
                //2020 LTS            
                AnimRetargetWindow.ShowPlayer();
#endif

                //Common
                Init();

                SceneView.RepaintAll();
            }
        }

        public static void CloseRetargeter()
        {
            if (IsPlayerShown())
            {
                //EditorApplication.update -= UpdateDelegate;

#if SCENEVIEW_OVERLAY_COMPATIBLE
                //2021.2.0a17+          
                AnimRetargetOverlay.HideAll();
#else
                //2020 LTS            
                AnimRetargetWindow.HidePlayer();
#endif

                //Common
                CleanUp();

                SceneView.RepaintAll();
            }
        }

        public static bool IsPlayerShown()
        {
#if SCENEVIEW_OVERLAY_COMPATIBLE
            //2021.2.0a17+
            return AnimRetargetOverlay.Visibility;
#else
            //2020 LTS            
            return AnimRetargetWindow.isShown;
#endif
        }

        static void Init()
        {
            string[] folders = new string[] { "Assets", "Packages" };
            closedMouthImage = Reallusion.Import.Util.FindTexture(folders, "RLIcon_Mask_Closed");
            openMouthImage = Reallusion.Import.Util.FindTexture(folders, "RLIcon_Mask_Open");
            handImage = Reallusion.Import.Util.FindTexture(folders, "RLIcon_Hand");
            blendshapeImage = Reallusion.Import.Util.FindTexture(folders, "RLIcon_Masks");
            saveImage = Reallusion.Import.Util.FindTexture(folders, "RLIcon_Save");
            resetImage = Reallusion.Import.Util.FindTexture(folders, "RLIcon_ActionReset");
            lockedImage = Reallusion.Import.Util.FindTexture(folders, "RLIcon_Locked");
            unlockedImage = Reallusion.Import.Util.FindTexture(folders, "RLIcon_Unlocked");

            RebuildClip();

            // reset all the clip flags to their default vaules            
            // set the animation player's Foot IK to off
            AnimPlayerGUI.ForceSettingsReset();
            AnimPlayerGUI.UpdateAnimator();
        }

        static void CleanUp()
        {
            // reset the player fully with the currently selected clip
            AnimPlayerGUI.SetupCharacterAndAnimation();
        }

        public static void ResetClip()
        {
            AnimPlayerGUI.ReCloneClip();
            holdValues = false;
            RebuildClip();
        }

        // Return all values to start - rebuild all bindings dicts
        public static void RebuildClip()
        {
            if (WorkingClip && CanClipLoop(WorkingClip))
            {
                AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(WorkingClip);
                if (!clipSettings.loopTime)
                {
                    clipSettings.loopTime = true;
                    AnimationUtility.SetAnimationClipSettings(WorkingClip, clipSettings);
                }
            }

            if (OriginalClip)
            {
                EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(OriginalClip);

                shoulderBindings = new Dictionary<string, EditorCurveBinding>();

                for (int i = 0; i < curveBindings.Length; i++)
                {
                    if (shoulderCurveNames.Contains(curveBindings[i].propertyName))
                    {
                        shoulderBindings.Add(curveBindings[i].propertyName, curveBindings[i]);
                    }
                }

                armBindings = new Dictionary<string, EditorCurveBinding>();

                for (int i = 0; i < curveBindings.Length; i++)
                {
                    if (armCurveNames.Contains(curveBindings[i].propertyName))
                    {
                        armBindings.Add(curveBindings[i].propertyName, curveBindings[i]);
                    }
                }

                armFBBindings = new Dictionary<string, EditorCurveBinding>();

                for (int i = 0; i < curveBindings.Length; i++)
                {
                    if (armFBCurveNames.Contains(curveBindings[i].propertyName))
                    {
                        armFBBindings.Add(curveBindings[i].propertyName, curveBindings[i]);
                    }
                }

                legBindings = new Dictionary<string, EditorCurveBinding>();

                for (int i = 0; i < curveBindings.Length; i++)
                {
                    if (legCurveNames.Contains(curveBindings[i].propertyName))
                    {
                        legBindings.Add(curveBindings[i].propertyName, curveBindings[i]);
                    }
                }

                heelBindings = new Dictionary<string, EditorCurveBinding>();

                for (int i = 0; i < curveBindings.Length; i++)
                {
                    if (heelCurveNames.Contains(curveBindings[i].propertyName))
                    {
                        heelBindings.Add(curveBindings[i].propertyName, curveBindings[i]);
                    }
                }

                heightBindings = new Dictionary<string, EditorCurveBinding>();

                for (int i = 0; i < curveBindings.Length; i++)
                {
                    if (heightCurveNames.Contains(curveBindings[i].propertyName))
                    {
                        heightBindings.Add(curveBindings[i].propertyName, curveBindings[i]);
                    }
                }
            }

            if (!holdValues)
            {
                handPose = 0;
                closeMouth = false;
                shoulderOffset = 0f;
                armOffset = 0f;
                armFBOffset = 0f;
                backgroundArmOffset = 0f;
                legOffset = 0f;
                heelOffset = 0f;
                heightOffset = 0f;
            }

            OffsetALL();
        }

        public class Styles
        {
            public GUIStyle textFieldStyle;
            public GUIStyle smallTitleLabel;

            public Styles()
            {
                textFieldStyle = new GUIStyle(EditorStyles.textField);
                textFieldStyle.wordWrap = true;

                smallTitleLabel = new GUIStyle(GUI.skin.label);
                smallTitleLabel.fontStyle = FontStyle.BoldAndItalic;
            }
        }

        public static void DrawRetargeter(Rect position)
        {
            if (!(OriginalClip && WorkingClip)) GUI.enabled = false;
            else if (!AnimPlayerGUI.CharacterAnimator) GUI.enabled = false;
            else GUI.enabled = true;

            if (styles == null) styles = new Styles();
            if (tabStyles == null) tabStyles = new TabStyles();
            if (tabCont == null) tabCont = new TabContents();

            // original rect (x:0.00, y:0.00, width:313.00, height:248.00)
            Rect areaRect = new Rect(0f, 0f, width, height);

            GUILayout.BeginVertical(); // full window in vertical

            activeTab = TabbedArea(activeTab, areaRect, tabCont.tabCount, TAB_HEIGHT, tabCont.toolTips, tabCont.icons, 20f, 20f, true, tabCont.overrideTab, tabCont.overrideIcons, false);

            GUILayout.Space(TAB_HEIGHT);

            GUILayout.BeginHorizontal(); // horizontal spacer to force window size
            GUILayout.Space(areaRect.width);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(); // horizontal container

            GUILayout.BeginVertical(); // vertical spacer to force window size
            GUILayout.Space(areaRect.height - TAB_HEIGHT);
            GUILayout.EndVertical();

            GUILayout.BeginVertical(); // vertical layout of content

            switch (activeTab)
            {
                case 0:
                    {
                        DrawAnimationadjustmentControls();
                        break;
                    }
                case 1:
                    {
                        DrawBlendShapeRetargetControls();
                        break;
                    }
            }

            GUILayout.EndVertical();

            GUILayout.EndHorizontal(); // end horizontal container

            LowerControlGUI();

            GUILayout.EndVertical(); // end full window in vertical
        }

        public static void DrawBlendShapeRetargetControls()
        {
            GUILayout.BeginVertical();

            // Content
            GUILayout.Label("Character Expression Controls", styles.smallTitleLabel);
            EditorGUI.BeginChangeCheck();
            expressionDrivenBones = GUILayout.Toggle(expressionDrivenBones, new GUIContent("Expressions Animate Facial Bones", "Use expression blend shapes to to displace the bones of all face parts (The mechanim animation system otherwise won't animate all of them)"));
            if (EditorGUI.EndChangeCheck())
            {
                if (expressionDrivenBones)
                {
                    createFullAnimationTrack = false;
                    SceneView.RepaintAll();
                }
            }
            EditorGUI.BeginChangeCheck();
            expressionBlendShapeTranspose = GUILayout.Toggle(expressionBlendShapeTranspose, new GUIContent("Expression Blendshapes Transposed at Runtime", "Instead of using a very large number of animation tracks to animate the blend shapes on face objects (e.g. eyebrows, beards etc), the blend shape values on the head are instead copied to all of the applicable objects on the model. This has a lower performance overhead and means that the animations are considerably smaller."));
            if (EditorGUI.EndChangeCheck())
            {
                if (expressionBlendShapeTranspose)
                {
                    createFullAnimationTrack = false;
                    SceneView.RepaintAll();
                }
            }
            EditorGUI.BeginChangeCheck();
            expressionConstrain = GUILayout.Toggle(expressionConstrain, new GUIContent("Constraint Blendshapes Calculated at Runtime", "The constraint blendshapes (those beginning with 'C_') will be calculated from the values of the corresponding source blend shapes. Limits will also be applied to certain blend shapes based on the limit definitions in the CC5 facial profile editor."));
            if (EditorGUI.EndChangeCheck())
            {
                if (expressionBlendShapeTranspose)
                {
                    createFullAnimationTrack = false;
                    SceneView.RepaintAll();
                }
            }

            GUILayout.Space(4f);

            GUILayout.Label("Legacy Method", styles.smallTitleLabel);
            EditorGUI.BeginChangeCheck();
            createFullAnimationTrack = GUILayout.Toggle(createFullAnimationTrack, new GUIContent("Animate all face objects individually", "This will construct animation tracks for every blend shape on every applicable face object (e.g. eyebrows, beards etc). This reults in a very large animation with a higher performance overhead than the runtime transpose method."));
            if (EditorGUI.EndChangeCheck())
            {
                if (createFullAnimationTrack)
                {
                    expressionDrivenBones = false;
                    expressionBlendShapeTranspose = false;
                    expressionConstrain = false;
                    SceneView.RepaintAll();
                }
            }

            GUILayout.Space(4f);

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            string message = string.Empty;
            int lines = 0;
            message += expressionDrivenBones ? "- Expressions will directly control all face bones at runtime.\n" : string.Empty;
            lines += expressionDrivenBones ? 2 : 0;
            message += expressionBlendShapeTranspose ? "- Expressions will be copied to all face parts at runtime.\n" : string.Empty;
            lines += expressionBlendShapeTranspose ? 2 : 0;
            message += expressionConstrain ? "- Expression Constraints will be calculated at runtime." : string.Empty;
            lines += expressionConstrain ? 2 : 0;
            message += createFullAnimationTrack ? "- Animation tracks will be created for each blendshape on each face part (legacy method)." : "";
            lines += createFullAnimationTrack ? 5 : 0;
            bool noSelection = !expressionDrivenBones && !expressionBlendShapeTranspose && !createFullAnimationTrack;
            if (noSelection)
            {
                message += "No action selected, please select Blend Shape retargetting method";
                lines += 2;
            }

            EditorGUILayout.SelectableLabel(message, styles.textFieldStyle, GUILayout.Width(220f), GUILayout.Height(15f * lines));//(EditorGUIUtility.singleLineHeight * lines));

            GUILayout.EndVertical();

            GUILayout.BeginVertical("box"); // Blendshapes control box
            Color backgroundColor = GUI.backgroundColor;
            Color tint = Color.green;
            FacialProfile mfp = AnimPlayerGUI.MeshFacialProfile;
            FacialProfile cfp = AnimPlayerGUI.ClipFacialProfile;
            if (!mfp.HasFacialShapes || !cfp.HasFacialShapes || noSelection)
            {
                GUI.enabled = false;
                tint = backgroundColor;
            }
            if (!mfp.IsSameProfileFrom(cfp))
            {
                if (mfp.expressionProfile != ExpressionProfile.None &&
                    cfp.expressionProfile != ExpressionProfile.None)
                {
                    // ExpPlus or Extended to Standard will not retarget well, show a red warning color
                    if (mfp.expressionProfile == ExpressionProfile.Std)
                        tint = Color.red;
                    // retargeting from CC3 standard should work with everything
                    else if (cfp.expressionProfile == ExpressionProfile.Std)
                        tint = Color.green;
                    // otherwise show a yellow warning color
                    else
                        tint = Color.yellow;
                }

                if (mfp.visemeProfile != cfp.visemeProfile)
                {
                    if (mfp.visemeProfile == VisemeProfile.Direct || cfp.visemeProfile == VisemeProfile.Direct)
                    {
                        // Direct to Paired visemes won't work.
                        tint = Color.red;
                    }
                }
            }

            GUI.backgroundColor = Color.Lerp(backgroundColor, tint, 0.25f);
            if (GUILayout.Button(new GUIContent(blendshapeImage, "Retarget Blendshapes."), GUILayout.Width(largeIconDim), GUILayout.Height(largeIconDim)))
            {
                logOnce = true;
                RetargetBlendShapes(OriginalClip, WorkingClip, CharacterAnimator.gameObject, null, false, expressionDrivenBones, expressionBlendShapeTranspose, expressionConstrain, createFullAnimationTrack);
                AnimPlayerGUI.UpdateAnimator();
            }
            GUI.backgroundColor = backgroundColor;
            GUI.enabled = true;

            GUILayout.EndVertical();

            GUILayout.Space(10f);

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        public static void DrawAnimationadjustmentControls()
        {
            GUILayout.BeginVertical();// All retarget controls
            GUILayout.Space(4f);
            // Horizontal Group of 3 controls `Hand` `Jaw` and `Blendshapes`
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(); // ("box", GUILayout.Width(baseControlWidth));  // Hand control box - Width used to impose layout footprint for overlay
            GUILayout.BeginHorizontal();
            GUILayout.Space(12f);

            if (GUILayout.Button(new GUIContent(handImage, "Switch between hand modes - Original animation info - Static open hand pose - Static closed hand pose. (This only affects pose of the fingers)."), GUILayout.Width(largeIconDim), GUILayout.Height(largeIconDim)))
            {
                handPose++;
                if (handPose > 2) handPose = 0;
                ApplyPose(handPose);
            }
            GUILayout.BeginVertical();

            GUIStyle radioSelectionStyle = new GUIStyle(EditorStyles.radioButton);
            radioSelectionStyle.padding = new RectOffset(24, 0, 0, 0);
            GUIContent[] contents = new GUIContent[]
            {
                new GUIContent("Original", "Use the hand pose/animation from the original animation clip."),
                new GUIContent("Open", "Use a static neutral open hand pose for the full animation clip."),
                new GUIContent("Closed", "Use a static neutral closed hand pose for the full animation clip.")
            };
            EditorGUI.BeginChangeCheck();
            handPose = GUILayout.SelectionGrid(handPose, contents, 1, radioSelectionStyle);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyPose(handPose);
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical(); // End of Hand control

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();// ("box"); // Jaw control box       
            if (GUILayout.Button(new GUIContent(closeMouth ? closedMouthImage : openMouthImage, string.Format("STATUS: " + (closeMouth ? "ON" : "OFF") + ":  Toggle to CLOSE THE JAW of any animation imported without proper jaw information.  Toggling this ON will overwrite any jaw animation.  Toggling OFF will use the jaw animation from the selected animation clip.")), GUILayout.Width(largeIconDim), GUILayout.Height(largeIconDim)))
            {
                closeMouth = !closeMouth;
                CloseMouthToggle(closeMouth);
            }
            GUILayout.EndVertical(); // End of Jaw control

            GUILayout.Space(10f);

            GUILayout.EndHorizontal(); // End of Blendshapes control

            // Control box for animation curve adjustment sliders
            GUILayout.BeginVertical("box");

            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal(GUILayout.Width(sliderWidth));
            GUILayout.Label(new GUIContent("Shoulder", "Adjust the Up-Down displacement of the Shoulders across the whole animation."), GUILayout.Width(textWidth), GUILayout.Height(textHeight));
            shoulderOffset = EditorGUILayout.Slider(shoulderOffset, -shRange, shRange);
            GUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                OffsetShoulders();
            }

            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal(GUILayout.Width(sliderWidth));
            GUILayout.Label(new GUIContent("Arm", "Adjust the Upper Arm Up-Down rotation. Controls the 'lift' of the arms."), GUILayout.Width(textWidth), GUILayout.Height(textHeight));
            armOffset = EditorGUILayout.Slider(armOffset, -aRange, aRange);
            GUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                OffsetArms();
            }

            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal(GUILayout.Width(sliderWidth));
            GUILayout.Label(new GUIContent("(Flexion)", "Adjust the Upper Arm Front-Back rotation. Controls the 'Flexion' or 'Extension' of the arms."), GUILayout.Width(textWidth), GUILayout.Height(textHeight));
            armFBOffset = EditorGUILayout.Slider(armFBOffset, -aRange, aRange);
            GUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                OffsetArmsFB();
            }

            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal(GUILayout.Width(sliderWidth));
            GUILayout.Label(new GUIContent("Leg", "Adjust the Upper Leg In-Out rotation. Controls the width of the character's stance."), GUILayout.Width(textWidth), GUILayout.Height(textHeight));
            legOffset = EditorGUILayout.Slider(legOffset, -lRange, lRange);
            GUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                OffsetLegs();
            }

            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal(GUILayout.Width(sliderWidth));
            GUILayout.Label(new GUIContent("Heel", "Ajdust the angle of the Foot Up-Down rotation. Controls the angle of the heel."), GUILayout.Width(textWidth), GUILayout.Height(textHeight));
            heelOffset = EditorGUILayout.Slider(heelOffset, -hRange, hRange);
            GUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                OffsetHeel();
            }

            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal(GUILayout.Width(sliderWidth));
            GUILayout.Label(new GUIContent("Height", "Adjust the vertical 'y' displacement of the character."), GUILayout.Width(textWidth), GUILayout.Height(textHeight));
            heightOffset = EditorGUILayout.Slider(heightOffset, -yRange, yRange);
            GUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                OffsetHeight();
            }
            GUILayout.EndVertical(); // End of animation curve adjustment sliders

            GUILayout.EndVertical(); // All retarget controls
            // End of retarget controls
        }

        public static void LowerControlGUI()
        {
            // Lower close, reset and save controls
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Space(36f);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();// ("box");  // close button
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_clear").image, "Close this window."), GUILayout.Width(smallIconDim), GUILayout.Height(smallIconDim)))
            {
                CloseRetargeter();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();// ("box");  // hold button
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent(holdValues ? lockedImage : unlockedImage, string.Format("STATUS: " + (holdValues ? "LOCKED VALUES : slider settings are retained when animation is changed." : "UNLOCKED VALUES : slider settings are reset when animation is changed."))), GUILayout.Width(smallIconDim), GUILayout.Height(smallIconDim)))
            {
                holdValues = !holdValues;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();// ("box");  // reset button
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent(resetImage, "Reset all slider settings and applied modifications."), GUILayout.Width(smallIconDim), GUILayout.Height(smallIconDim)))
            {
                ResetClip();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();// ("box"); // save button
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent(saveImage, "Save the modified animation to the 'Project Assets'.  This will create a new animation in the 'Home Directory' of the selected model named <Model Name>_<Animation Name>.anim"), GUILayout.Width(smallIconDim), GUILayout.Height(smallIconDim)))
            {
                GameObject scenePrefab = AnimPlayerGUI.CharacterAnimator.gameObject;
                GameObject fbxAsset = Util.FindRootPrefabAssetFromSceneObject(scenePrefab);
                if (fbxAsset)
                {
                    string characterFbxPath = AssetDatabase.GetAssetPath(fbxAsset);
                    string assetPath = GenerateClipAssetPath(OriginalClip, characterFbxPath);
                    WriteAnimationToAssetDatabase(WorkingClip, assetPath, true);
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal(); // End of reset and save controls
        }

        private static void LogBoneDriverSettingsChanges(GameObject root, GameObject bd, bool drive, bool transpose, bool constrain, bool legacy)
        {
            if (logOnce)
            {
                string conj = drive && transpose ? " and " : string.Empty;
                string driveStr = drive ? "'Expressions Drive Face Bones' is ENABLED" : string.Empty;
                string transposeStr = transpose ? "'Expressions are copied to all face parts' is ENABLED" : string.Empty;
                string legacyStr = legacy ? "Both 'Expressions Drive Face Bones' and Expressions are copied to all face parts' are now DISABLED in the existing BoneDriver" : string.Empty;
                string constrainStr = constrain ? "\nExpression 'Constraints' and 'Limits' will be applied." : string.Empty;
                string text = $"Settings in the BoneDriver on {bd.name} in the {root.name} prefab will be changed and applied to the prefab.\n{driveStr}{conj}{transposeStr}{legacyStr}{constrainStr}";
                Debug.Log(text);
                logOnce = false;
            }
        }

        public static bool CanClipLoop(AnimationClip clip)
        {
            bool canLoop = true;
            EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(clip);
            foreach (EditorCurveBinding binding in curveBindings)
            {
                Keyframe[] testKeys = AnimationUtility.GetEditorCurve(clip, binding).keys;
                if (Math.Round(testKeys[0].value, 2) != Math.Round(testKeys[testKeys.Length - 1].value, 2))
                {
                    canLoop = false;
                }
            }
            return canLoop;
        }

        static void CloseMouthToggle(bool close)
        {
            if (!(OriginalClip && WorkingClip)) return;

            bool found = false;
            EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(OriginalClip);
            EditorCurveBinding targetBinding = new EditorCurveBinding();
            AnimationCurve jawCurve = new AnimationCurve();
            Keyframe[] jawKeys;

            foreach (EditorCurveBinding binding in curveBindings)
            {
                if (binding.propertyName.Equals(jawClose))
                {
                    targetBinding = binding;
                    found = true;
                }
            }

            if (found)
            {
                jawCurve = AnimationUtility.GetEditorCurve(OriginalClip, targetBinding);
            }
            else
            {
                targetBinding = new EditorCurveBinding() { propertyName = jawClose, type = typeof(Animator) };
                jawKeys = new Keyframe[] {
                    new Keyframe( 0f, 0f ),
                    new Keyframe( OriginalClip.length, 0f )
                };
                jawCurve.keys = jawKeys;
            }

            if (close)
            {
                jawKeys = jawCurve.keys;
                for (int i = 0; i < jawKeys.Length; i++)
                {
                    jawKeys[i].value = 1;
                }
                jawCurve.keys = jawKeys;
            }
            AnimationClip swapClip = AnimPlayerGUI.CloneClip(WorkingClip);
            AnimationUtility.SetEditorCurve(swapClip, targetBinding, jawCurve);
            AnimPlayerGUI.SelectOverrideAnimationWithoutReset(swapClip, AnimPlayerGUI.animatorOverrideController);
            AnimPlayerGUI.UpdateAnimator();
        }

        static void ApplyPose(int mode)
        {
            if (!(OriginalClip && WorkingClip)) return;

            switch (mode)
            {
                case 0:
                    {
                        ResetPose();
                        break;
                    }
                case 1:
                    {
                        SetPose(openHandPose);
                        break;
                    }
                case 2:
                    {
                        SetPose(closedHandPose);
                        break;
                    }
            }
        }

        static void SetPose(Dictionary<string, float> pose)
        {
            if (!(OriginalClip && WorkingClip)) return;

            AnimationClip swapClip = AnimPlayerGUI.CloneClip(WorkingClip);

            EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(OriginalClip);
            foreach (EditorCurveBinding binding in curveBindings)
            {
                foreach (KeyValuePair<string, float> p in pose)
                {
                    if (binding.propertyName.Equals(p.Key))
                    {
                        AnimationCurve curve = AnimationUtility.GetEditorCurve(OriginalClip, binding);
                        Keyframe[] keys = curve.keys;
                        for (int i = 0; i < keys.Length; i++)
                        {
                            keys[i].value = p.Value;
                        }
                        curve.keys = keys;
                        AnimationUtility.SetEditorCurve(swapClip, binding, curve);
                    }
                }
            }
            AnimPlayerGUI.SelectOverrideAnimationWithoutReset(swapClip, AnimPlayerGUI.animatorOverrideController);
            AnimPlayerGUI.UpdateAnimator();
        }

        static void ResetPose()
        {
            if (!(OriginalClip && WorkingClip)) return;

            AnimationClip swapClip = AnimPlayerGUI.CloneClip(WorkingClip);

            EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(OriginalClip);
            foreach (EditorCurveBinding binding in curveBindings)
            {
                if (handCurves.Contains(binding.propertyName))
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(OriginalClip, binding);
                    AnimationUtility.SetEditorCurve(swapClip, binding, curve);
                }
            }
            AnimPlayerGUI.SelectOverrideAnimationWithoutReset(swapClip, AnimPlayerGUI.animatorOverrideController);
            AnimPlayerGUI.UpdateAnimator();
        }

        static void OffsetALL()
        {
            OffsetShoulders();
            OffsetArms();
            OffsetArmsFB();
            OffsetLegs();
            OffsetHeel();
            OffsetHeight();
            CloseMouthToggle(closeMouth);
            ApplyPose(handPose);
        }

        static void SetEditorCurves(AnimationClip clip, List<EditorCurveBinding> bindings, List<AnimationCurve> curves)
        {
#if UNITY_2020_3_OR_NEWER
            AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
#else
            int numClips = bindings.Count;
            for (int i = 0; i < numClips; i++)
            {
                AnimationUtility.SetEditorCurve(clip, bindings[i], curves[i]);
            }
#endif
        }

        static void OffsetShoulders()
        {
            if (!(OriginalClip && WorkingClip)) return;

            List<EditorCurveBinding> applicableBindings = new List<EditorCurveBinding>();
            List<AnimationCurve> applicableCurves = new List<AnimationCurve>();

            foreach (KeyValuePair<string, EditorCurveBinding> bind in shoulderBindings)
            {
                float scale = 0f;
                bool eval = false;
                bool subtract = true;
                bool update = false;
                AnimationCurve curve = AnimationUtility.GetEditorCurve(OriginalClip, bind.Value);
                Keyframe[] keys = curve.keys;

                switch (bind.Key)
                {
                    case lShoulder:
                        {
                            scale = srScale;
                            eval = true;
                            subtract = true;
                        }
                        break;
                    case rShoulder:
                        {
                            scale = srScale;
                            eval = true;
                            subtract = true;
                        }
                        break;
                    case lArm:
                    case lArmFB:
                        {
                            scale = arScale;
                            eval = true;
                            subtract = false;
                            update = true;
                        }
                        break;
                    case rArm:
                    case rArmFB:
                        {
                            scale = arScale;
                            eval = true;
                            subtract = false;
                            update = true;
                        }
                        break;
                    case lArmTwist:
                        {
                            scale = atScale;
                            eval = false;
                            subtract = true;
                        }
                        break;
                    case rArmTwist:
                        {
                            scale = atScale;
                            eval = false;
                            subtract = true;
                        }
                        break;
                }

                float diff = shoulderOffset * scale;
                if (update)
                {
                    backgroundArmOffset = diff / arScale;
                    diff = (backgroundArmOffset + armOffset) * scale;
                }

                for (int a = 0; a < keys.Length; a++)
                {
                    keys[a].value = eval ? EvaluateValue(keys[a].value, subtract ? -diff : diff) : keys[a].value + (subtract ? -diff : diff);
                }
                curve.keys = keys;
                for (int b = 0; b < keys.Length; b++)
                {
                    curve.SmoothTangents(b, 0.0f);
                }
                applicableBindings.Add(bind.Value);
                applicableCurves.Add(curve);
            }
            AnimationClip swapClip = AnimPlayerGUI.CloneClip(WorkingClip);
            SetEditorCurves(swapClip, applicableBindings, applicableCurves);
            AnimPlayerGUI.SelectOverrideAnimationWithoutReset(swapClip, AnimPlayerGUI.animatorOverrideController);
            AnimPlayerGUI.UpdateAnimator();
        }

        static void OffsetArms()
        {
            if (!(OriginalClip && WorkingClip)) return;

            List<EditorCurveBinding> applicableBindings = new List<EditorCurveBinding>();
            List<AnimationCurve> applicableCurves = new List<AnimationCurve>();

            foreach (KeyValuePair<string, EditorCurveBinding> bind in armBindings)
            {
                float scale = 0f;
                bool eval = false;
                bool subtract = true;
                bool includeBackgroundVal = false;
                AnimationCurve curve = AnimationUtility.GetEditorCurve(OriginalClip, bind.Value);
                Keyframe[] keys = curve.keys;

                switch (bind.Key)
                {
                    case lArm:
                        {
                            scale = arScale;
                            eval = true;
                            subtract = false;
                            includeBackgroundVal = true;
                        }
                        break;
                    case rArm:
                        {
                            scale = arScale;
                            eval = true;
                            subtract = false;
                            includeBackgroundVal = true;
                        }
                        break;
                    case lArmTwist:
                        {
                            scale = atScale;
                            eval = false;
                            subtract = true;
                        }
                        break;
                    case rArmTwist:
                        {
                            scale = atScale;
                            eval = false;
                            subtract = true;
                        }
                        break;
                }

                float diff = armOffset * scale;
                if (includeBackgroundVal)
                {
                    diff = (backgroundArmOffset + armOffset) * scale;
                }

                for (int a = 0; a < keys.Length; a++)
                {

                    keys[a].value = eval ? EvaluateValue(keys[a].value, subtract ? -diff : diff) : keys[a].value + (subtract ? -diff : diff);
                }
                curve.keys = keys;
                for (int b = 0; b < keys.Length; b++)
                {
                    curve.SmoothTangents(b, 0.0f);
                }
                applicableBindings.Add(bind.Value);
                applicableCurves.Add(curve);
            }
            AnimationClip swapClip = AnimPlayerGUI.CloneClip(WorkingClip);
            SetEditorCurves(swapClip, applicableBindings, applicableCurves);
            AnimPlayerGUI.SelectOverrideAnimationWithoutReset(swapClip, AnimPlayerGUI.animatorOverrideController);
            AnimPlayerGUI.UpdateAnimator();
        }

        static void OffsetArmsFB()
        {
            if (!(OriginalClip && WorkingClip)) return;

            List<EditorCurveBinding> applicableBindings = new List<EditorCurveBinding>();
            List<AnimationCurve> applicableCurves = new List<AnimationCurve>();

            foreach (KeyValuePair<string, EditorCurveBinding> bind in armFBBindings)
            {
                float scale = 0f;
                bool eval = false;
                bool subtract = true;
                bool includeBackgroundVal = false;
                AnimationCurve curve = AnimationUtility.GetEditorCurve(OriginalClip, bind.Value);
                Keyframe[] keys = curve.keys;

                switch (bind.Key)
                {
                    case lArmFB:
                        {
                            scale = arScale;
                            eval = true;
                            subtract = false;
                            includeBackgroundVal = false;
                        }
                        break;
                    case rArmFB:
                        {
                            scale = arScale;
                            eval = true;
                            subtract = false;
                            includeBackgroundVal = false;
                        }
                        break;
                }

                float diff = armFBOffset * scale;
                if (includeBackgroundVal)
                {
                    diff = (backgroundArmOffset + armFBOffset) * scale;
                }

                for (int a = 0; a < keys.Length; a++)
                {

                    keys[a].value = eval ? EvaluateValue(keys[a].value, subtract ? -diff : diff) : keys[a].value + (subtract ? -diff : diff);
                }
                curve.keys = keys;
                for (int b = 0; b < keys.Length; b++)
                {
                    curve.SmoothTangents(b, 0.0f);
                }
                applicableBindings.Add(bind.Value);
                applicableCurves.Add(curve);
            }
            AnimationClip swapClip = AnimPlayerGUI.CloneClip(WorkingClip);
            SetEditorCurves(swapClip, applicableBindings, applicableCurves);
            AnimPlayerGUI.SelectOverrideAnimationWithoutReset(swapClip, AnimPlayerGUI.animatorOverrideController);
            AnimPlayerGUI.UpdateAnimator();
        }

        static void OffsetLegs()
        {
            if (!(OriginalClip && WorkingClip)) return;

            List<EditorCurveBinding> applicableBindings = new List<EditorCurveBinding>();
            List<AnimationCurve> applicableCurves = new List<AnimationCurve>();

            foreach (KeyValuePair<string, EditorCurveBinding> bind in legBindings)
            {
                float scale = 0f;
                bool eval = false;
                bool subtract = true;

                AnimationCurve curve = AnimationUtility.GetEditorCurve(OriginalClip, bind.Value);
                Keyframe[] keys = curve.keys;

                switch (bind.Key)
                {
                    case lLeg:
                        {
                            scale = arScale;
                            eval = true;
                            subtract = false;
                        }
                        break;
                    case rLeg:
                        {
                            scale = arScale;
                            eval = true;
                            subtract = false;
                        }
                        break;
                    case lFootTwist:
                        {
                            scale = ftScale;
                            eval = false;
                            subtract = true;
                        }
                        break;
                    case rFootTwist:
                        {
                            scale = ftScale;
                            eval = false;
                            subtract = true;
                        }
                        break;
                }

                float diff = legOffset * scale;

                for (int a = 0; a < keys.Length; a++)
                {

                    keys[a].value = eval ? EvaluateValue(keys[a].value, subtract ? -diff : diff) : keys[a].value + (subtract ? -diff : diff);
                }
                curve.keys = keys;
                for (int b = 0; b < keys.Length; b++)
                {
                    curve.SmoothTangents(b, 0.0f);
                }
                applicableBindings.Add(bind.Value);
                applicableCurves.Add(curve);
            }
            AnimationClip swapClip = AnimPlayerGUI.CloneClip(WorkingClip);
            SetEditorCurves(swapClip, applicableBindings, applicableCurves);
            AnimPlayerGUI.SelectOverrideAnimationWithoutReset(swapClip, AnimPlayerGUI.animatorOverrideController);
            AnimPlayerGUI.UpdateAnimator();
        }

        static void OffsetHeel()
        {
            if (!(OriginalClip && WorkingClip)) return;

            List<EditorCurveBinding> applicableBindings = new List<EditorCurveBinding>();
            List<AnimationCurve> applicableCurves = new List<AnimationCurve>();

            foreach (KeyValuePair<string, EditorCurveBinding> bind in heelBindings)
            {
                float scale = 0f;
                bool eval = false;
                bool subtract = true;

                AnimationCurve curve = AnimationUtility.GetEditorCurve(OriginalClip, bind.Value);
                Keyframe[] keys = curve.keys;

                switch (bind.Key)
                {
                    case lFoot:
                        {
                            scale = arScale;
                            eval = true;
                            subtract = false;
                        }
                        break;
                    case rFoot:
                        {
                            scale = arScale;
                            eval = true;
                            subtract = false;
                        }
                        break;
                    case lToes:
                        {
                            scale = trScale;
                            eval = false;
                            subtract = true;
                        }
                        break;
                    case rToes:
                        {
                            scale = trScale;
                            eval = false;
                            subtract = true;
                        }
                        break;
                }

                float diff = heelOffset * scale;

                for (int a = 0; a < keys.Length; a++)
                {

                    keys[a].value = eval ? EvaluateValue(keys[a].value, subtract ? -diff : diff) : keys[a].value + (subtract ? -diff : diff);
                }
                curve.keys = keys;
                for (int b = 0; b < keys.Length; b++)
                {
                    curve.SmoothTangents(b, 0.0f);
                }
                applicableBindings.Add(bind.Value);
                applicableCurves.Add(curve);
            }
            AnimationClip swapClip = AnimPlayerGUI.CloneClip(WorkingClip);
            SetEditorCurves(swapClip, applicableBindings, applicableCurves);
            AnimPlayerGUI.SelectOverrideAnimationWithoutReset(swapClip, AnimPlayerGUI.animatorOverrideController);
            AnimPlayerGUI.UpdateAnimator();
        }

        static void OffsetHeight()
        {
            if (!(OriginalClip && WorkingClip)) return;

            List<EditorCurveBinding> applicableBindings = new List<EditorCurveBinding>();
            List<AnimationCurve> applicableCurves = new List<AnimationCurve>();

            foreach (KeyValuePair<string, EditorCurveBinding> bind in heightBindings)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(OriginalClip, bind.Value);
                Keyframe[] keys = curve.keys;

                float diff = heightOffset;

                for (int a = 0; a < keys.Length; a++)
                {
                    keys[a].value = keys[a].value + diff;
                }
                curve.keys = keys;
                for (int b = 0; b < keys.Length; b++)
                {
                    curve.SmoothTangents(b, 0.0f);
                }
                applicableBindings.Add(bind.Value);
                applicableCurves.Add(curve);
            }
            AnimationClip swapClip = AnimPlayerGUI.CloneClip(WorkingClip);
            SetEditorCurves(swapClip, applicableBindings, applicableCurves);
            AnimPlayerGUI.SelectOverrideAnimationWithoutReset(swapClip, AnimPlayerGUI.animatorOverrideController);
            AnimPlayerGUI.UpdateAnimator();
        }

        static float EvaluateValue(float currentKeyValue, float deltaValue)
        {
            //if currently above zero   
            if (currentKeyValue >= 0f)
            {
                //if it ends up below zero then the negative contribution must be x2
                if ((currentKeyValue + deltaValue) < 0f)
                {
                    return (currentKeyValue + deltaValue) * 2f;
                }
                else
                //if it ends up above zero then return sum
                {
                    return currentKeyValue + deltaValue;
                }
            }

            //if currently bleow zero
            if (currentKeyValue < 0f)
            {
                //if both are negative then double the contribution from delta and return
                if (deltaValue < 0f)
                {
                    return currentKeyValue + deltaValue * 2f;
                }
                else
                {
                    //if delta is positive then we have to consider where it will end up with a below zero contribution * 2
                    if ((currentKeyValue + deltaValue * 2f) < 0f)
                    {
                        //where the value simply ends up still negative then we can return that
                        return currentKeyValue + deltaValue * 2f;
                    }
                    else
                    {
                        //where the value ends up positive we must return half the positive value
                        return (currentKeyValue + deltaValue * 2f) / 2f;
                    }
                }
            }
            return 3f;  // go wrong spectacularly
        }

        static float logtime = 0f;

        public static void CopyCurve(AnimationClip originalClip, AnimationClip workingClip, string goName, string targetPropertyName, EditorCurveBinding sourceCurveBinding)
        {
            float time = Time.realtimeSinceStartup;

            EditorCurveBinding workingBinding = new EditorCurveBinding()
            {
                path = goName,
                type = typeof(SkinnedMeshRenderer),
                propertyName = targetPropertyName
            };

            if (AnimationUtility.GetEditorCurve(workingClip, workingBinding) == null ||
                targetPropertyName != sourceCurveBinding.propertyName)
            {
                AnimationCurve workingCurve = AnimationUtility.GetEditorCurve(originalClip, sourceCurveBinding);
                AnimationUtility.SetEditorCurve(workingClip, workingBinding, workingCurve);
            }

            logtime += Time.realtimeSinceStartup - time;
        }

        public static bool CurveHasData(EditorCurveBinding binding, AnimationClip clip)
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);

            if (curve != null)
            {
                //if (curve.length > 2) return true;
                for (int i = 0; i < curve.length; i++)
                {
                    if (Mathf.Abs(curve.keys[i].value) > 0.001f) return true;
                }
            }

            return false;
        }

        public static void RetargetBlendShapes(AnimationClip originalClip, AnimationClip workingClip,
            GameObject targetCharacterModel, CharacterInfo info = null, bool log = true, bool FeatureUseBoneDriver = false, bool FeatureUseExpressionTranspose = false, bool FeatureUseConstraintData = false, bool legacyFeatureOverride = false)
        {
            if (!(originalClip && workingClip)) return;

            FacialProfile meshProfile = FacialProfileMapper.GetMeshFacialProfile(targetCharacterModel);
            if (!meshProfile.HasFacialShapes)
            {
                if (log) Util.LogWarn("Character has no facial blend shapes!");
                return;
            }
            FacialProfile animProfile = FacialProfileMapper.GetAnimationClipFacialProfile(workingClip);
            if (!animProfile.HasFacialShapes)
            {
                if (log) Util.LogWarn("Animation has no facial blend shapes!");
                return;
            }

            if (log)
            {
                if (!meshProfile.IsSameProfileFrom(animProfile))
                {
                    Util.LogWarn("Retargeting to Facial Profile: " + meshProfile + ", From: " + animProfile + "\n" +
                                     "Warning: Character mesh facial profile does not match the animation facial profile.\n" +
                                     "Facial expression retargeting may not have the expected or desired results.\n");
                }
                else
                {
                    Util.LogAlways("Retargeting to Facial Profile: " + meshProfile + ", From: " + animProfile + "\n");
                }
            }

            bool useBoneDriver = (info != null && info.FeatureUseBoneDriver) || FeatureUseBoneDriver;
            bool useBlendTranspose = (info != null && info.FeatureUseExpressionTranspose) || FeatureUseExpressionTranspose;
            bool useConstraintData = (info != null && info.FeatureUseConstraintData) || FeatureUseConstraintData;

            if (legacyFeatureOverride)
            {
                RetargetBlendShapesToAllMeshes(originalClip, workingClip, targetCharacterModel, meshProfile, animProfile);
            }
            else
            {
                //RetargetBlendShapesToAllMeshes(originalClip, workingClip, targetCharacterModel, meshProfile, animProfile);

                if (useBoneDriver)
                {
                    PruneTargettedMechanimTracks(originalClip, workingClip, targetCharacterModel, useBoneDriver, useBlendTranspose, useConstraintData);
                }

                if (useBlendTranspose)
                {
                    PruneBlendShapesToSourceMeshes(workingClip, targetCharacterModel, meshProfile, animProfile, useConstraintData);
                    //PruneBlendShapeTargets(originalClip, workingClip, targetCharacterModel, meshProfile, animProfile, useBoneDriver, useBlendTranspose, useConstraintData);
                }
                /*
                if ((info != null && !info.FeatureUseExpressionTranspose && !info.FeatureUseExpressionTranspose) && !FeatureUseExpressionTranspose && !FeatureUseBoneDriver)
                {
                    RetargetBlendShapesToAllMeshes(originalClip, workingClip, targetCharacterModel, meshProfile, animProfile);
                }
                */
            }
            logOnce = false;
        }

        public static void PruneTargettedMechanimTracks(AnimationClip originalClip, AnimationClip workingClip, GameObject targetCharacterModel, bool drive = false, bool transpose = false, bool constrain = false)
        {
            // needs a set up bonedriver component to interrogate for the expression glossary
            GameObject bd = BoneEditor.GetBoneDriverGameObjectReflection(targetCharacterModel);
            if (bd == null)
            {
                BoneEditor.AddBoneDriverToBaseBody(targetCharacterModel, drive, transpose);
            }
            if (bd == null) return;

            BoneEditor.SetupBoneDriverFlags(bd, drive, transpose, constrain);
            LogBoneDriverSettingsChanges(targetCharacterModel, bd, drive, transpose, constrain, false);
            Util.ApplyIfPrefabInstance(targetCharacterModel);

            SkinnedMeshRenderer smr = bd.GetComponent<SkinnedMeshRenderer>();
            if (smr == null) return;

            Dictionary<string, List<string>> dict = BoneEditor.RetrieveBoneDictionary(bd);
            // check CC_Base_Body (implicitly the bonedriver bearing gameobject) for blendshapes -  if 
            // all blendshapes are present which influence a bone then purge the mechanim tracks
            // associated with that bone - to allow only the expression to deform the bone

            EditorCurveBinding[] sourceCurveBindings = AnimationUtility.GetCurveBindings(workingClip);

            string[] bonesToEvaluate = new string[] { "CC_Base_JawRoot", "CC_Base_L_Eye", "CC_Base_R_Eye", "CC_Base_Head" };

            string[] jawCurves = new string[] { "Jaw Close", "Jaw Left-Right" };
            string[] lEyeCurves = new string[] { "Left Eye Down-Up", "Left Eye In-Out" };
            string[] rEyeCurves = new string[] { "Right Eye Down-Up", "Right Eye In-Out" };
            string[] headCurves = new string[] { "Head Nod Down-Up", "Head Tilt Left-Right", "Head Turn Left-Right" };

            // Identify all curve bindings for constraint blendshapes (starting with "C_") and purge them
            List<string> animatedConstraints = new List<string>();
            try
            {
                int n = 0;
                foreach (var binding in sourceCurveBindings)
                {
                    n++;
                    float progress = (float)n / (float)sourceCurveBindings.Length;
                    EditorUtility.DisplayProgressBar($"Determining Animated Constraints...", $"Working on {binding.propertyName} ", progress);
                    if (binding.propertyName.ToLower().StartsWith("blendshape.c_"))
                        animatedConstraints.Add(binding.propertyName);
                }
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
            }
            //foreach (var binding in animatedConstraints) Debug.Log($"Purging: {binding}");
            PurgeBindings(sourceCurveBindings, animatedConstraints.ToArray(), workingClip);

            foreach (var boneToEvaluate in bonesToEvaluate)
            {
                //Debug.Log($"boneToEvaluate {boneToEvaluate}");
                bool complete = true;
                dict.TryGetValue(boneToEvaluate, out List<string> blendShapes);
                if (blendShapes != null)
                {
                    foreach (var blendShape in blendShapes)
                    {
                        //Debug.Log($"testing blendShape = {blendShape}");
                        if (smr.sharedMesh.GetBlendShapeIndex(blendShape) == -1) complete = false;
                    }
                }
                //Debug.Log($"boneToEvaluate {boneToEvaluate} complete = {complete}");
                if (complete)
                {
                    switch (boneToEvaluate)
                    {
                        case "CC_Base_JawRoot":
                            {
                                PurgeBindings(sourceCurveBindings, jawCurves, workingClip);
                                break;
                            }
                        case "CC_Base_L_Eye":
                            {
                                PurgeBindings(sourceCurveBindings, lEyeCurves, workingClip);
                                break;
                            }
                        case "CC_Base_R_Eye":
                            {
                                PurgeBindings(sourceCurveBindings, rEyeCurves, workingClip);
                                break;
                            }
                        case "CC_Base_Head":
                            {
                                //PurgeBindings(sourceCurveBindings, headCurves, workingClip);
                                break;
                            }
                    }
                }
            }
            EditorUtility.ClearProgressBar();
        }

        public static void PurgeBindings(EditorCurveBinding[] sourceCurveBindings, string[] bindings, AnimationClip clip)
        {
            int n = 0;
            foreach (string binding in bindings)
            {
                n++;
                float progress = (float)n / (float)sourceCurveBindings.Length;
                EditorUtility.DisplayProgressBar($"Purging unnecessary tracks...", $"Working on {binding} ", progress);
                try
                {
                    var targetBindings = sourceCurveBindings.ToList().FindAll(x => x.propertyName == binding);
                    if (targetBindings.Count > 0)
                    {
                        foreach (var tgt in targetBindings)
                        {
                            //Debug.Log($"Purging {tgt.propertyName}");
                            AnimationUtility.SetEditorCurve(clip, tgt, null);
                        }
                    }
                    else
                    {
                        Debug.Log($"Cannot Find {binding}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Purging Error {e.Message}");
                }
            }
            EditorUtility.ClearProgressBar();
        }

        public static void PruneBlendShapeTargets(AnimationClip originalClip, AnimationClip workingClip, GameObject targetCharacterModel, FacialProfile meshProfile, FacialProfile animProfile, bool drive = false, bool transpose = false, bool constrain = false)
        {
            const string blendShapePrefix = "blendShape.";

            EditorCurveBinding[] sourceCurveBindings = AnimationUtility.GetCurveBindings(workingClip);
            // Data looks like this:
            // path: "Circle_Sparse"  propertyName: "blendShape.Tongue_Twist_R"  for blendshapes on a mesh
            // path: "" propertyName: "Jaw Close"
            // path: "" propertyName: "Jaw Left-Right" for mechanim tracks


            // get a dictionary of blend shapes that are not contained in the CC_Base_Body or CC_Base_Tongue meshes
            GameObject bd = BoneEditor.GetBoneDriverGameObjectReflection(targetCharacterModel);
            if (bd == null)
            {
                BoneEditor.AddBoneDriverToBaseBody(targetCharacterModel, drive, transpose);

            }
            if (bd == null) return;

            BoneEditor.SetupBoneDriverFlags(bd, drive, transpose, constrain);
            LogBoneDriverSettingsChanges(targetCharacterModel, bd, drive, transpose, constrain, false);
            Util.ApplyIfPrefabInstance(targetCharacterModel);

            Dictionary<string, List<string>> excessBlendhsapes = BoneEditor.FindExcessBlendShapes(bd);
            // this is a list to keep

            List<(string, string)> keepMe = new List<(string, string)>();
            foreach (var path in excessBlendhsapes)
            {
                foreach (string prop in path.Value)
                {
                    (string, string) entry = (path.Key, prop);
                    keepMe.Add(entry);
                }
            }

            // Get a list of EditorCurveBindings in the animation clip that should be kept
            List<EditorCurveBinding> bindingFilter = new List<EditorCurveBinding>();
            foreach (var entry in keepMe)
            {
                EditorCurveBinding bindingToKeep = sourceCurveBindings.FirstOrDefault(p => p.path == entry.Item1 && p.propertyName == blendShapePrefix + entry.Item2);
                if (!string.IsNullOrEmpty(bindingToKeep.path) && !string.IsNullOrEmpty(bindingToKeep.propertyName))
                {
                    //Debug.Log($"Do Not purge: {bindingToKeep.path} {bindingToKeep.propertyName}  ----  {entry.Item1} {blendShapePrefix + entry.Item2}");
                    bindingFilter.Add(bindingToKeep);
                }
            }

            foreach (EditorCurveBinding binding in sourceCurveBindings)
            {
                bool purge = false;
                purge = !CurveHasData(binding, workingClip);

                //if (binding.path != "CC_Base_Body" && binding.path != "CC_Base_Tongue")
                if (binding.path != bd.name)
                {
                    if (binding.propertyName.StartsWith(blendShapePrefix))
                    {
                        if (!bindingFilter.Contains(binding))
                        {
                            //Debug.Log($"Purging {binding.path} {binding.propertyName}");
                            purge = true;
                        }
                    }
                }
                if (purge) AnimationUtility.SetEditorCurve(workingClip, binding, null);
            }
            // Need to transpose any blendhapes from the animations facial profile to the mesh's profile

            // build a cache of remapped (from the anim profile to the mesh profile) blend shape names in the animation and their original curve bindings:
            EditorCurveBinding[] workingCurveBindings = AnimationUtility.GetCurveBindings(workingClip);
            string report = "";

            Dictionary<string, EditorCurveBinding> cache = new Dictionary<string, EditorCurveBinding>();
            for (int i = 0; i < workingCurveBindings.Length; i++)
            {
                if (CurveHasData(workingCurveBindings[i], workingClip) &&
                    workingCurveBindings[i].propertyName.StartsWith(blendShapePrefix))
                {
                    string animBlendShapeName = workingCurveBindings[i].propertyName.Substring(blendShapePrefix.Length);
                    string meshProfileBlendShapeName = meshProfile.GetMappingFrom(animBlendShapeName, animProfile);
                    if (!string.IsNullOrEmpty(meshProfileBlendShapeName))
                    {
                        List<string> multiProfileName = FacialProfileMapper.GetMultiShapeNames(meshProfileBlendShapeName);
                        if (multiProfileName.Count == 1)
                        {
                            if (!cache.ContainsKey(meshProfileBlendShapeName))
                            {
                                cache.Add(meshProfileBlendShapeName, workingCurveBindings[i]);

                                report += "Mapping: " + meshProfileBlendShapeName + " from " + animBlendShapeName + "\n";
                            }
                        }
                        else
                        {
                            foreach (string multiShapeName in multiProfileName)
                            {
                                if (!cache.ContainsKey(multiShapeName))
                                {
                                    cache.Add(multiShapeName, workingCurveBindings[i]);

                                    report += "Mapping (multi): " + multiShapeName + " from " + animBlendShapeName + "\n";
                                }
                            }
                        }
                    }
                }
            }

            List<string> uniqueAnimPaths = new List<string>();
            foreach (EditorCurveBinding binding in workingCurveBindings)
            {
                if (!uniqueAnimPaths.Contains(binding.path))
                {
                    uniqueAnimPaths.Add(binding.path);
                }
            }

            List<string> mappedBlendShapes = new List<string>();

            Transform[] targetAssetData = targetCharacterModel.GetComponentsInChildren<Transform>();

            foreach (string path in uniqueAnimPaths)
            {
                SkinnedMeshRenderer smr = null;
                Transform t = targetAssetData.FirstOrDefault(t => t.name == path);
                if (t != null)
                    smr = t.gameObject.GetComponent<SkinnedMeshRenderer>();

                if (smr && smr.sharedMesh && smr.sharedMesh.blendShapeCount > 0)
                {
                    for (int j = 0; j < smr.sharedMesh.blendShapeCount; j++)
                    {
                        string blendShapeName = smr.sharedMesh.GetBlendShapeName(j);
                        string targetPropertyName = blendShapePrefix + blendShapeName;

                        if (cache.TryGetValue(blendShapeName, out EditorCurveBinding sourceCurveBinding))
                        {
                            report += $"Copying curve for {blendShapeName} to {targetPropertyName}\n";
                            CopyCurve(originalClip, workingClip, path, targetPropertyName, sourceCurveBinding);

                            if (!mappedBlendShapes.Contains(blendShapeName))
                                mappedBlendShapes.Add(blendShapeName);
                        }
                        else
                        {
                            //report += "Could not map blendshape: " + blendShapeName + " in object: " + go.name + "\n";
                        }
                    }
                }
            }

            report += "\n";
            int curvesFailedToMap = 0;
            foreach (string shape in cache.Keys)
            {
                if (!mappedBlendShapes.Contains(shape))
                {
                    curvesFailedToMap++;
                    report += "Could not find BlendShape: " + shape + " in target character.\n";
                }
            }

            string reportHeader = "Blendshape Mapping report:\n";
            if (curvesFailedToMap == 0) reportHeader += "All " + cache.Count + " BlendShape curves retargeted!\n\n";
            else reportHeader += curvesFailedToMap + " out of " + cache.Count + " BlendShape curves could not be retargeted!\n\n";

            bool log = true;
            if (log) Util.LogAlways(reportHeader + report);
        }

        public static void PruneBlendShapesToSourceMeshes(AnimationClip workingClip, GameObject targetCharacterModel, FacialProfile meshProfile, FacialProfile animProfile, bool driveConstraints = false)
        {
            const string blendShapePrefix = "blendShape.";
            // purge all blendshape curves from working anim
            // copy all remappable curves from original to working for the source mesh only

            GameObject source = RL.FindExpressionSourceMesh(targetCharacterModel);

            List<EditorCurveBinding> workingClipBindings = AnimationUtility.GetCurveBindings(workingClip).ToList();
            // Data looks like this:
            // path: "Circle_Sparse"  propertyName: "blendShape.Tongue_Twist_R"  for blendshapes on a mesh
            // path: "" propertyName: "Jaw Close"
            // path: "" propertyName: "Jaw Left-Right" for mechanim tracks

            // get all meshes into a list prioritized by the source meshes
            SkinnedMeshRenderer[] allSmrs = targetCharacterModel.GetComponentsInChildren<SkinnedMeshRenderer>();
            List<SkinnedMeshRenderer> targetSmrs = new List<SkinnedMeshRenderer>();
            string[] sourceMeshes = new string[] { source.name, "CC_Base_Tongue", "CC_Base_Eye", "CC_Base_EyeOcclusion", "CC_Base_TearLine", };
            try
            {
                foreach (string meshName in sourceMeshes)
                {
                    var s = allSmrs.FirstOrDefault(x => x.name == meshName);
                    if (s)
                        targetSmrs.Add(s);
                }

                foreach (var smr in allSmrs)
                {
                    EditorUtility.DisplayProgressBar($"Analyzing SkinnedMeshRenderers...", $"Working on {smr.name} ", 0.45f);
                    if (smr.sharedMesh && smr.sharedMesh.blendShapeCount > 0)
                    {
                        if (!targetSmrs.Contains(smr))
                            targetSmrs.Add(smr);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
            }
            // match the path of each binding in the working clip to a list member of allMeshes and store the binding of the first instance of that blendshape.
            Dictionary<string, EditorCurveBinding> uniqueBindings = new Dictionary<string, EditorCurveBinding>();

            //try
            //{
            for (int i = 0; i < workingClipBindings.Count; i++)
            {
                EditorCurveBinding binding = workingClipBindings[i];
                float progress = (float)i / (float)workingClipBindings.Count;
                //EditorUtility.DisplayProgressBar($"Analyzing EditorCurveBindings...", $"Working on {binding.propertyName} ", progress);
                bool isConstraint = binding.propertyName.StartsWith($"{blendShapePrefix}C_");
                if (isConstraint && driveConstraints) continue;

                if (binding.propertyName.StartsWith(blendShapePrefix))
                {
                    string targetPath = string.Empty;
                    string blendShapeName = binding.propertyName.Substring(blendShapePrefix.Length);

                    string targetBlendShapeName = meshProfile.GetMappingFrom(blendShapeName, animProfile);
                    List<string> targetBlendshapeNames = null;
                    if (!string.IsNullOrEmpty(targetBlendShapeName))
                    {
                        targetBlendshapeNames = FacialProfileMapper.GetMultiShapeNames(targetBlendShapeName);
                    }
                    if (targetBlendshapeNames != null)
                    {
                        for (int j = 0; j < targetBlendshapeNames.Count; j++)
                        {
                            targetBlendShapeName = targetBlendshapeNames[j];
                            string targetPropertyName = $"{blendShapePrefix}{targetBlendShapeName}";

                            if (!uniqueBindings.ContainsKey(targetPropertyName))
                            {
                                foreach (var smr in targetSmrs)
                                {
                                    int index = smr.sharedMesh.GetBlendShapeIndex(targetBlendShapeName);
                                    if (index != -1)
                                    {
                                        targetPath = smr.name;
                                        break;
                                    }
                                }
                                if (!string.IsNullOrEmpty(targetPath))
                                {
                                    // copy the binding into a new curve
                                    if (binding.path != targetPath && binding.propertyName != targetPropertyName)
                                    {
                                        EditorCurveBinding newBinding = DuplicateClipBindingOrSomat(workingClip, binding, targetPath, targetPropertyName);
                                        uniqueBindings.Add(newBinding.propertyName, newBinding);
                                        //if (j == 0) workingClipBindings[i] = newBinding;
                                        //else 
                                        workingClipBindings.Add(newBinding);
                                    }
                                    else
                                    {
                                        uniqueBindings.Add(binding.propertyName, binding);
                                    }

                                }
                            }
                        }
                    }
                }
            }
            //}
            //catch { }            

            try
            {
                int n = 0;
                // now remove everything not on the unique list
                foreach (var binding in workingClipBindings)
                {
                    n++;
                    float progress = (float)n / (float)workingClipBindings.Count;
                    EditorUtility.DisplayProgressBar($"Cleaning EditorCurveBindings...", $"Working on {binding.propertyName} ", progress);
                    if (!uniqueBindings.ContainsValue(binding))
                    {
                        if (binding.propertyName.StartsWith(blendShapePrefix))
                            AnimationUtility.SetEditorCurve(workingClip, binding, null);
                    }
                    else
                    {
                        Debug.Log("Keeping this one" + binding.propertyName);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
            }
            EditorUtility.ClearProgressBar();
        }

        public static EditorCurveBinding DuplicateClipBindingOrSomat(AnimationClip workingClip, EditorCurveBinding binding, string targetPath, string targetPropertyName)
        {
            EditorCurveBinding newBinding = new EditorCurveBinding()
            {
                path = targetPath,
                propertyName = targetPropertyName,
                type = typeof(SkinnedMeshRenderer)
            };

            AnimationCurve curve = AnimationUtility.GetEditorCurve(workingClip, binding);
            try
            {
                AnimationUtility.SetEditorCurve(workingClip, newBinding, curve);
            }
            catch (Exception e)
            {
                Debug.Log($"{workingClip.name} {curve.length}");
                Debug.Log(e.Message);
            }
            return newBinding;
        }

        public static void RemapOnlyExistingBlendShapeCurves(AnimationClip originalClip, AnimationClip workingClip, FacialProfile meshProfile, FacialProfile animProfile)
        {
            const string blendShapePrefix = "blendShape.";

            EditorCurveBinding[] sourceCurveBindings = AnimationUtility.GetCurveBindings(workingClip);

            string report = "";

            // build a cache of the blend shape names and their curve bindings:
            Dictionary<string, EditorCurveBinding> remapBindings = new Dictionary<string, EditorCurveBinding>();
            for (int i = 0; i < sourceCurveBindings.Length; i++)
            {
                if (CurveHasData(sourceCurveBindings[i], workingClip) &&
                    sourceCurveBindings[i].propertyName.StartsWith(blendShapePrefix))
                {
                    string blendShapeName = sourceCurveBindings[i].propertyName.Substring(blendShapePrefix.Length);
                    string profileBlendShapeName = meshProfile.GetMappingFrom(blendShapeName, animProfile);
                    if (!string.IsNullOrEmpty(profileBlendShapeName))
                    {
                        List<string> multiProfileName = FacialProfileMapper.GetMultiShapeNames(profileBlendShapeName);
                        if (multiProfileName.Count == 1)
                        {
                            if (!remapBindings.ContainsKey(profileBlendShapeName))
                            {
                                remapBindings.Add(profileBlendShapeName, sourceCurveBindings[i]);
                                report += "Mapping: " + profileBlendShapeName + " from " + blendShapeName + "\n";
                            }
                        }
                        else
                        {
                            foreach (string multiShapeName in multiProfileName)
                            {
                                if (!remapBindings.ContainsKey(multiShapeName))
                                {
                                    remapBindings.Add(multiShapeName, sourceCurveBindings[i]);
                                    report += "Mapping (multi): " + multiShapeName + " from " + blendShapeName + "\n";
                                }
                            }
                        }
                    }
                }
            }

            foreach (var binding in Array.FindAll(sourceCurveBindings, x => x.propertyName.StartsWith(blendShapePrefix)))
            {

            }

        }

        public static void RetargetBlendShapesToAllMeshes(AnimationClip originalClip, AnimationClip workingClip, GameObject targetCharacterModel, FacialProfile meshProfile, FacialProfile animProfile, bool log = true)
        {
            Debug.Log("RetargetBlendShapesToAllMeshes");
            GameObject bd = BoneEditor.GetBoneDriverGameObjectReflection(targetCharacterModel);
            if (bd != null)
            {
                BoneEditor.SetupBoneDriverFlags(bd, false, false, false);
                LogBoneDriverSettingsChanges(targetCharacterModel, bd, false, false, false, true);
                Util.ApplyIfPrefabInstance(targetCharacterModel);
            }
            else { Debug.Log("No Bonedriver found"); }

            EditorCurveBinding[] sourceCurveBindings = AnimationUtility.GetCurveBindings(workingClip);
            Transform[] targetAssetData = targetCharacterModel.GetComponentsInChildren<Transform>();

            const string blendShapePrefix = "blendShape.";

            // Find all of the blendshape relevant binding paths that are not needed in the target animation        
            List<string> uniqueSourcePaths = new List<string>();
            foreach (EditorCurveBinding binding in sourceCurveBindings)
            {
                if (binding.propertyName.StartsWith(blendShapePrefix))
                {
                    if (!uniqueSourcePaths.Contains(binding.path))
                        uniqueSourcePaths.Add(binding.path);
                }
            }

            List<string> validTargetPaths = new List<string>();
            foreach (Transform t in targetAssetData)
            {
                GameObject go = t.gameObject;
                if (go.GetComponent<SkinnedMeshRenderer>())
                {
                    if (go.GetComponent<SkinnedMeshRenderer>().sharedMesh.blendShapeCount > 0)
                    {
                        validTargetPaths.Add(go.name);
                    }
                }
            }

            List<string> pathsToPurge = new List<string>();
            foreach (string path in uniqueSourcePaths)
            {
                if (!validTargetPaths.Contains(path))
                {
                    pathsToPurge.Add(path);
                }
            }

            logtime = 0f;
            string report = "";

            // build a cache of the blend shape names and their curve bindings:
            Dictionary<string, EditorCurveBinding> cache = new Dictionary<string, EditorCurveBinding>();
            for (int i = 0; i < sourceCurveBindings.Length; i++)
            {
                if (CurveHasData(sourceCurveBindings[i], workingClip) &&
                    sourceCurveBindings[i].propertyName.StartsWith(blendShapePrefix))
                {
                    string blendShapeName = sourceCurveBindings[i].propertyName.Substring(blendShapePrefix.Length);
                    string profileBlendShapeName = meshProfile.GetMappingFrom(blendShapeName, animProfile);
                    if (!string.IsNullOrEmpty(profileBlendShapeName))
                    {
                        List<string> multiProfileName = FacialProfileMapper.GetMultiShapeNames(profileBlendShapeName);
                        if (multiProfileName.Count == 1)
                        {
                            if (!cache.ContainsKey(profileBlendShapeName))
                            {
                                cache.Add(profileBlendShapeName, sourceCurveBindings[i]);
                                report += "Mapping: " + profileBlendShapeName + " from " + blendShapeName + "\n";
                            }
                        }
                        else
                        {
                            foreach (string multiShapeName in multiProfileName)
                            {
                                if (!cache.ContainsKey(multiShapeName))
                                {
                                    cache.Add(multiShapeName, sourceCurveBindings[i]);
                                    report += "Mapping (multi): " + multiShapeName + " from " + blendShapeName + "\n";
                                }
                            }
                        }
                    }
                }
            }

            List<string> mappedBlendShapes = new List<string>();

            // apply the curves to the target animation
            foreach (Transform t in targetAssetData)
            {
                GameObject go = t.gameObject;
                SkinnedMeshRenderer smr = go.GetComponent<SkinnedMeshRenderer>();
                if (smr && smr.sharedMesh && smr.sharedMesh.blendShapeCount > 0)
                {
                    for (int j = 0; j < smr.sharedMesh.blendShapeCount; j++)
                    {
                        string blendShapeName = smr.sharedMesh.GetBlendShapeName(j);
                        string targetPropertyName = blendShapePrefix + blendShapeName;

                        if (cache.TryGetValue(blendShapeName, out EditorCurveBinding sourceCurveBinding))
                        {
                            CopyCurve(originalClip, workingClip, go.name, targetPropertyName, sourceCurveBinding);

                            if (!mappedBlendShapes.Contains(blendShapeName))
                                mappedBlendShapes.Add(blendShapeName);
                        }
                        else
                        {
                            //report += "Could not map blendshape: " + blendShapeName + " in object: " + go.name + "\n";
                        }
                    }
                }
            }

            report += "\n";
            int curvesFailedToMap = 0;
            foreach (string shape in cache.Keys)
            {
                if (!mappedBlendShapes.Contains(shape))
                {
                    curvesFailedToMap++;
                    report += "Could not find BlendShape: " + shape + " in target character.\n";
                }
            }

            string reportHeader = "Blendshape Mapping report:\n";
            if (curvesFailedToMap == 0) reportHeader += "All " + cache.Count + " BlendShape curves retargeted!\n\n";
            else reportHeader += curvesFailedToMap + " out of " + cache.Count + " BlendShape curves could not be retargeted!\n\n";

            if (log) Util.LogAlways(reportHeader + report);

            bool PURGE = true;
            // Purge all curves from the animation that dont have a valid path in the target object                    
            if (PURGE)
            {
                EditorCurveBinding[] targetCurveBindings = AnimationUtility.GetCurveBindings(workingClip);
                for (int k = 0; k < targetCurveBindings.Length; k++)
                {
                    if (pathsToPurge.Contains(targetCurveBindings[k].path))
                    {
                        AnimationUtility.SetEditorCurve(workingClip, targetCurveBindings[k], null);
                    }
                    else
                    {
                        // purge all extra blend shape animations
                        if (targetCurveBindings[k].propertyName.StartsWith(blendShapePrefix))
                        {
                            string blendShapeName = targetCurveBindings[k].propertyName.Substring(blendShapePrefix.Length);
                            if (!cache.ContainsKey(blendShapeName))
                            {
                                AnimationUtility.SetEditorCurve(workingClip, targetCurveBindings[k], null);
                            }
                        }
                    }
                }
            }
        }

        static string GenerateClipAssetPath(AnimationClip originalClip, string characterFbxPath, string prefix = "", bool overwrite = false)
        {
            if (!originalClip || string.IsNullOrEmpty(characterFbxPath)) return null;

            string characterName = Path.GetFileNameWithoutExtension(characterFbxPath);
            string fbxFolder = Path.GetDirectoryName(characterFbxPath);
            string animFolder = Path.Combine(fbxFolder, ANIM_FOLDER_NAME, characterName);
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

            string animName = NameAnimation(characterName, clipName, prefix);
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

        public static AnimationClip WriteAnimationToAssetDatabase(AnimationClip workingClip, string assetPath, bool originalSettings = false)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;

            Util.LogDetail("Writing Asset: " + assetPath);

            var output = Object.Instantiate(workingClip);  // clone so that workingClip isn't locked to an on-disk asset
            AnimationClip outputClip = output as AnimationClip;

            if (originalSettings)
            {
                // **Addition** for the edit mode animator player: the clip settings of the working clip
                // may contain user set flags that are for evaluation purposes only (e.g. loopBlendPositionXZ)
                // the original clip's settings should be copied to the output clip and the loop flag set as
                // per the user preference to auto loop the animation.

                // record the user preferred loop status 
                AnimationClipSettings outputClipSettings = AnimationUtility.GetAnimationClipSettings(outputClip);
                bool isLooping = outputClipSettings.loopTime;

                // obtain the original settings
                AnimationClipSettings originalClipSettings = AnimationUtility.GetAnimationClipSettings(OriginalClip);

                // re-impose the loop status            
                originalClipSettings.loopTime = isLooping;

                //update the output clip with the looping modified original settings
                AnimationUtility.SetAnimationClipSettings(outputClip, outputClipSettings);

                // the correct settings can now be written to disk - but the in memory copy used by the
                // player/re-tartgeter will be untouched so end users dont see a behaviour change after saving

                // **End of addition**
            }

            AnimationClip asset = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (asset == null)
            {
                // New
                Util.LogDetail("Writing New Asset: " + assetPath);
                AssetDatabase.CreateAsset(outputClip, assetPath);
            }
            else
            {
                Util.LogDetail("Updating Existing Asset: " + assetPath);
                outputClip.name = asset.name;
                EditorUtility.CopySerialized(outputClip, asset);
                AssetDatabase.SaveAssets();
            }

            asset = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            Selection.objects = new Object[] { asset };
            return asset;
        }

        public static string NameAnimation(string characterName, string clipName, string prefix)
        {
            string animName;
            if (string.IsNullOrEmpty(prefix))
                animName = characterName + "_" + clipName;
            else
                animName = characterName + "_" + prefix + "_" + clipName;
            string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            Regex r = new Regex(string.Format("[{0}]", Regex.Escape(invalid)));
            return r.Replace(animName, "_");
        }

        // Curve Master Data

        // Jaw curve
        const string jawClose = "Jaw Close";

        // Shoulder, Six curves to consider
        const string lShoulder = "Left Shoulder Down-Up";
        const string lArm = "Left Arm Down-Up";
        const string lArmFB = "Left Arm Front-Back";
        const string lArmTwist = "Left Arm Twist In-Out";

        const string rShoulder = "Right Shoulder Down-Up";
        const string rArm = "Right Arm Down-Up";
        const string rArmFB = "Right Arm Front-Back";
        const string rArmTwist = "Right Arm Twist In-Out";

        // Arm, Four Curves to consider
        // lArm lArmTwist rArm rArmTwist

        // Leg, Four Curves to consider
        const string lLeg = "Left Upper Leg In-Out";
        const string lFootTwist = "Left Foot Twist In-Out";

        const string rLeg = "Right Upper Leg In-Out";
        const string rFootTwist = "Right Foot Twist In-Out";

        // Heel, Four Curves to consider
        const string lFoot = "Left Foot Up-Down";
        const string lToes = "Left Toes Up-Down";

        const string rFoot = "Right Foot Up-Down";
        const string rToes = "Right Toes Up-Down";

        // Height, One Curve to consider
        const string yRoot = "RootT.y";

        static string[] shoulderCurveNames = new string[]
                {
                    lShoulder,
                    lArm,
                    lArmTwist,
                    rShoulder,
                    rArm,
                    rArmTwist
                };

        static string[] armCurveNames = new string[]
                {
                    lArm,
                    lArmTwist,
                    rArm,
                    rArmTwist
                };

        static string[] armFBCurveNames = new string[]
                {
                    lArmFB,
                    rArmFB,
                };

        static string[] legCurveNames = new string[]
                {
                    lLeg,
                    lFootTwist,
                    rLeg,
                    rFootTwist
                };

        static string[] heelCurveNames = new string[]
                {
                    lFoot,
                    lToes,
                    rFoot,
                    rToes
                };

        static string[] heightCurveNames = new string[]
                {
                    yRoot
                };

        //Translation ratios to convert angles to Mechanim values
        const float srScale = 12f / 360f; // Shoulder Rotation scale
        const float arScale = 3.6f / 360f; // Arm Rotation scale
        const float atScale = 1f / 360f; // Arm Twist scale
        const float ftScale = 8f / 360f; // Foot Twist scale
        const float trScale = 4f / 360f; // Toe rotation scale

        // Pose Master Data
        private static void ExtractPose()
        {
            string dictName = "openHandPose";
            string filename = "pose";
            string extension = ".cs";

            string searchString = "hand.";
            float timeStamp = 0.1f;

            EditorCurveBinding[] sourceCurveBindings = AnimationUtility.GetCurveBindings(WorkingClip);

            string pathString = "Dictionary<string, float> " + dictName + " = new Dictionary<string, float>()\r";
            pathString += "{\r";
            foreach (EditorCurveBinding binding in sourceCurveBindings)
            {
                if (binding.propertyName.ToLower().Contains(searchString))
                {
                    pathString += "\t{ \"" + binding.propertyName + "\", ";
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(WorkingClip, binding);
                    float value = curve.Evaluate(timeStamp);
                    pathString += value + "f },\r";
                }
            }
            pathString += "};";
            string path = "Assets/" + filename + extension;
            System.IO.File.WriteAllText(path, pathString);
        }

        public static List<AnimationClip> GenerateCharacterTargetedAnimations(string motionAssetPath,
            GameObject targetCharacterModel, CharacterInfo info, bool replaceIfExists, string motionPrefix = null)
        {

            List<AnimationClip> animationClips = new List<AnimationClip>();

            AnimationClip[] clips = Util.GetAllAnimationClipsFromCharacter(motionAssetPath);

            if (info.FeatureUseExtractGeneric)
            {
                Debug.Log("Extracting generic animation data.");
                clips = GenericAnimProcessing.ProcessGenericClips(info, clips, motionAssetPath);
            }

            if (!targetCharacterModel) targetCharacterModel = Util.FindCharacterPrefabAsset(motionAssetPath);
            if (!targetCharacterModel) return null;

            string firstPath = null;

            if (clips.Length > 0)
            {
                int index = 0;
                foreach (AnimationClip clip in clips)
                {
                    string clipPrefix = string.IsNullOrEmpty(motionPrefix) ? RETARGET_SOURCE_PREFIX : motionPrefix;
                    string assetPath = GenerateClipAssetPath(clip, motionAssetPath, clipPrefix, true);
                    if (string.IsNullOrEmpty(firstPath)) firstPath = assetPath;
                    if (File.Exists(assetPath) && !replaceIfExists)
                    {
                        //Debug.Log("FAIL CASE");
                        continue;
                    }
                    AnimationClip workingClip = AnimPlayerGUI.CloneClip(clip);
                    RetargetBlendShapes(clip, workingClip, targetCharacterModel, info, false);
                    AnimationClip asset = WriteAnimationToAssetDatabase(workingClip, assetPath, false);
                    animationClips.Add(asset);
                    index++;
                }

                if (!string.IsNullOrEmpty(firstPath))
                    AnimPlayerGUI.UpdateAnimatorClip(CharacterAnimator,
                                                     AssetDatabase.LoadAssetAtPath<AnimationClip>(firstPath));
            }
            else
            {
                Util.LogInfo("No animation clips found.");
            }

            return animationClips;
        }

        /// <summary>
        /// Tries to get the retargeted version of the animation clip from the given source animation clip, 
        /// usually from the original character fbx.
        /// </summary>
        public static AnimationClip TryGetRetargetedAnimationClip(GameObject fbxAsset, AnimationClip clip)
        {
            try
            {
                if (clip)
                {
                    string fbxPath = AssetDatabase.GetAssetPath(fbxAsset);
                    string fbxGuid = AssetDatabase.AssetPathToGUID(fbxPath);

                    string prefix = RETARGET_SOURCE_PREFIX;
                    try
                    {
                        CharacterInfo characterInfo = WindowManager.ValidImports.FirstOrDefault(x => x.guid == fbxGuid);
                        if (characterInfo != null)
                        {
                            if (!string.IsNullOrEmpty(characterInfo.motionPrefix))
                                prefix = characterInfo.motionPrefix;
                        }
                    }
                    catch (Exception ex) { Util.LogError(ex.Message); }

                    string characterName = Path.GetFileNameWithoutExtension(fbxPath);
                    string fbxFolder = Path.GetDirectoryName(fbxPath);
                    string animFolder = Path.Combine(fbxFolder, ANIM_FOLDER_NAME, characterName);

                    string animName = NameAnimation(characterName, clip.name, prefix);
                    string assetPath = Path.Combine(animFolder, animName + ".anim");
                    AnimationClip retargetedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                    if (retargetedClip) return retargetedClip;
                }
                return clip;
            }
            catch (Exception)
            {
                return clip;
            }
        }


        static Dictionary<string, float> openHandPose = new Dictionary<string, float>()
        {
            { "LeftHand.Thumb.1 Stretched", -1.141453f },
            { "LeftHand.Thumb.Spread", -0.4620222f },
            { "LeftHand.Thumb.2 Stretched", 0.5442108f },
            { "LeftHand.Thumb.3 Stretched", 0.4577243f },
            { "LeftHand.Index.1 Stretched", 0.3184956f },
            { "LeftHand.Index.Spread", -0.4479268f },
            { "LeftHand.Index.2 Stretched", 0.2451891f },
            { "LeftHand.Index.3 Stretched", 0.6176971f },
            { "LeftHand.Middle.1 Stretched", 0.09830929f },
            { "LeftHand.Middle.Spread", -0.5679846f },
            { "LeftHand.Middle.2 Stretched", 0.3699116f },
            { "LeftHand.Middle.3 Stretched", 0.3705207f },
            { "LeftHand.Ring.1 Stretched", 0.09632754f },
            { "LeftHand.Ring.Spread", -0.5876712f },
            { "LeftHand.Ring.2 Stretched", 0.1289254f },
            { "LeftHand.Ring.3 Stretched", 0.3732445f },
            { "LeftHand.Little.1 Stretched", 0.09448492f },
            { "LeftHand.Little.Spread", -0.4517526f },
            { "LeftHand.Little.2 Stretched", -0.003889897f },
            { "LeftHand.Little.3 Stretched", -0.04161567f },
            { "RightHand.Thumb.1 Stretched", -1.135697f },
            { "RightHand.Thumb.Spread", -0.4576517f },
            { "RightHand.Thumb.2 Stretched", 0.5427816f },
            { "RightHand.Thumb.3 Stretched", 0.4549177f },
            { "RightHand.Index.1 Stretched", 0.3184868f },
            { "RightHand.Index.Spread", -0.4478924f },
            { "RightHand.Index.2 Stretched", 0.2451727f },
            { "RightHand.Index.3 Stretched", 0.617752f },
            { "RightHand.Middle.1 Stretched", 0.09830251f },
            { "RightHand.Middle.Spread", -0.5680417f },
            { "RightHand.Middle.2 Stretched", 0.3699542f },
            { "RightHand.Middle.3 Stretched", 0.3705046f },
            { "RightHand.Ring.1 Stretched", 0.09632745f },
            { "RightHand.Ring.Spread", -0.5876312f },
            { "RightHand.Ring.2 Stretched", 0.1288746f },
            { "RightHand.Ring.3 Stretched", 0.3732805f },
            { "RightHand.Little.1 Stretched", 0.09454078f },
            { "RightHand.Little.Spread", -0.4516154f },
            { "RightHand.Little.2 Stretched", -0.04165318f },
            { "RightHand.Little.3 Stretched", -0.04163568f },
        };

        static Dictionary<string, float> closedHandPose = new Dictionary<string, float>()
        {
            { "LeftHand.Thumb.1 Stretched", -1.141455f },
            { "LeftHand.Thumb.Spread", -0.4620211f },
            { "LeftHand.Thumb.2 Stretched", 0.3974656f },
            { "LeftHand.Thumb.3 Stretched", -0.0122656f },
            { "LeftHand.Index.1 Stretched", -0.4441552f },
            { "LeftHand.Index.Spread", -0.3593751f },
            { "LeftHand.Index.2 Stretched", -0.8875571f },
            { "LeftHand.Index.3 Stretched", -0.3460926f },
            { "LeftHand.Middle.1 Stretched", -0.5940282f },
            { "LeftHand.Middle.Spread", -0.4824f },
            { "LeftHand.Middle.2 Stretched", -0.7796204f },
            { "LeftHand.Middle.3 Stretched", -0.3495999f },
            { "LeftHand.Ring.1 Stretched", -0.5579048f },
            { "LeftHand.Ring.Spread", -1.060186f },
            { "LeftHand.Ring.2 Stretched", -1.001659f },
            { "LeftHand.Ring.3 Stretched", -0.1538185f },
            { "LeftHand.Little.1 Stretched", -0.5157003f },
            { "LeftHand.Little.Spread", -0.5512691f },
            { "LeftHand.Little.2 Stretched", -0.6109533f },
            { "LeftHand.Little.3 Stretched", -0.4368959f },
            { "RightHand.Thumb.1 Stretched", -1.141842f },
            { "RightHand.Thumb.Spread", -0.4619166f },
            { "RightHand.Thumb.2 Stretched", 0.3966853f },
            { "RightHand.Thumb.3 Stretched", -0.01453214f },
            { "RightHand.Index.1 Stretched", -0.4441575f },
            { "RightHand.Index.Spread", -0.3588968f },
            { "RightHand.Index.2 Stretched", -0.887614f },
            { "RightHand.Index.3 Stretched", -0.3457543f },
            { "RightHand.Middle.1 Stretched", -0.5940221f },
            { "RightHand.Middle.Spread", -0.4824342f },
            { "RightHand.Middle.2 Stretched", -0.7796109f },
            { "RightHand.Middle.3 Stretched", -0.3495855f },
            { "RightHand.Ring.1 Stretched", -0.557913f },
            { "RightHand.Ring.Spread", -1.060112f },
            { "RightHand.Ring.2 Stretched", -1.001655f },
            { "RightHand.Ring.3 Stretched", -0.1538157f },
            { "RightHand.Little.1 Stretched", -0.5156479f },
            { "RightHand.Little.Spread", -0.5513764f },
            { "RightHand.Little.2 Stretched", -0.64873f },
            { "RightHand.Little.3 Stretched", -0.4367864f },
        };

        static string[] handCurves = new string[]
        {
            "LeftHand.Thumb.1 Stretched",
            "LeftHand.Thumb.Spread",
            "LeftHand.Thumb.2 Stretched",
            "LeftHand.Thumb.3 Stretched",
            "LeftHand.Index.1 Stretched",
            "LeftHand.Index.Spread",
            "LeftHand.Index.2 Stretched",
            "LeftHand.Index.3 Stretched",
            "LeftHand.Middle.1 Stretched",
            "LeftHand.Middle.Spread",
            "LeftHand.Middle.2 Stretched",
            "LeftHand.Middle.3 Stretched",
            "LeftHand.Ring.1 Stretched",
            "LeftHand.Ring.Spread",
            "LeftHand.Ring.2 Stretched",
            "LeftHand.Ring.3 Stretched",
            "LeftHand.Little.1 Stretched",
            "LeftHand.Little.Spread",
            "LeftHand.Little.2 Stretched",
            "LeftHand.Little.3 Stretched",
            "RightHand.Thumb.1 Stretched",
            "RightHand.Thumb.Spread",
            "RightHand.Thumb.2 Stretched",
            "RightHand.Thumb.3 Stretched",
            "RightHand.Index.1 Stretched",
            "RightHand.Index.Spread",
            "RightHand.Index.2 Stretched",
            "RightHand.Index.3 Stretched",
            "RightHand.Middle.1 Stretched",
            "RightHand.Middle.Spread",
            "RightHand.Middle.2 Stretched",
            "RightHand.Middle.3 Stretched",
            "RightHand.Ring.1 Stretched",
            "RightHand.Ring.Spread",
            "RightHand.Ring.2 Stretched",
            "RightHand.Ring.3 Stretched",
            "RightHand.Little.1 Stretched",
            "RightHand.Little.Spread",
            "RightHand.Little.2 Stretched",
            "RightHand.Little.3 Stretched"
        };

        public static int activeTab = 0;
        public static float TAB_HEIGHT = 26f;

        public static TabStyles tabStyles;
        public static TabContents tabCont;

        public class TabStyles
        {
            public Vector4 activeBorder;
            public Vector4 inactiveBorder;
            public Vector4 ghostBorder;
            public Vector4 contentBorder;

            public Color outline;
            public Color ghost;

            public Texture2D activeTex;
            public Texture2D inactiveTex;

            public GUIStyle iconStyle;

            public TabStyles()
            {
                outline = Color.black;
                ghost = Color.gray * 0.4f;

                activeBorder = new Vector4(1, 1, 1, 0);
                inactiveBorder = new Vector4(0, 0, 0, 1);
                ghostBorder = new Vector4(1, 1, 1, 0);
                contentBorder = new Vector4(1, 0, 1, 1);

                activeTex = TexCol(Color.gray * 0.55f);
                inactiveTex = TexCol(Color.gray * 0.35f);

                iconStyle = new GUIStyle();

                FixMeh();
            }

            private Texture2D TexCol(Color color)
            {
                const int size = 32;
                Texture2D texture = new Texture2D(size, size);
                Color[] pixels = texture.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = color;
                }
                texture.SetPixels(pixels);
                texture.Apply(true);
                return texture;
            }

            public void FixMeh()
            {
                if (!activeTex)
                {
                    activeTex = TexCol(Color.gray * 0.55f);
                }
                if (!inactiveTex)
                {
                    inactiveTex = TexCol(Color.gray * 0.35f);
                }
            }
        }

        public class TabContents
        {
            private Texture2D iconAnimTab;
            private Texture2D iconPropTab;
            private Texture2D iconBlendTab;
            private Texture2D iconLinkConnected;
            private Texture2D iconLinkDisconnected;
            private Texture2D iconSettingsTab;

            public int tabCount;
            public string[] toolTips;
            public Texture[] icons;
            public int overrideTab;
            public Texture[] overrideIcons;

            public TabContents()
            {
                string[] folders = new string[] { "Assets", "Packages" };

                //iconAnimTab = Util.FindTexture(folders, "RLIcon-Avatar_G");
                iconAnimTab = (Texture2D)EditorGUIUtility.IconContent("AnimationClip Icon").image;
                iconBlendTab = (Texture2D)EditorGUIUtility.IconContent("SkinnedMeshRenderer Icon").image;

                tabCount = 2;
                toolTips = new string[] { "Animation Adjustment", "Blendshape retargeting" };
                icons = new Texture[]
                {
                    iconAnimTab,
                    iconBlendTab,
                };
                overrideTab = -1;
                overrideIcons = new Texture[]
                {

                };
            }
        }

        // can override a single tab with icons based on a bool
        public static int TabbedArea(int TabId, Rect area, int tabCount, float tabHeight, string[] toolTips, Texture[] icons, float iconWidth, float iconHeight, bool fullWindow, int overrideTab = -1, Texture[] overrideIcons = null, bool overrideBool = false, Func<Rect, int, bool> RectHandler = null)
        {
            if (tabStyles == null) tabStyles = new TabStyles();
            if (tabStyles.activeTex == null || tabStyles.inactiveTex == null) tabStyles = new TabStyles();
            Rect areaRect;
            if (!fullWindow)
            {
                // round width down to an integer multiple of tabCount
                float width = (float)Math.Round(area.width / tabCount, MidpointRounding.AwayFromZero) * tabCount;

                areaRect = new Rect(area.x, area.y, width, area.height);
            }
            else
            {
                areaRect = area;
            }

            Rect[] tabRects = new Rect[tabCount];
            float tabWidth = (float)Math.Round(areaRect.width / tabCount, mode: MidpointRounding.AwayFromZero);
            for (int i = 0; i < tabCount; i++)
            {
                tabRects[i] = new Rect(tabWidth * i, 0f, tabWidth, tabHeight);
                if (RectHandler != null) RectHandler(tabRects[i], i); // callback to handle interaction with the tab rect, used for drag and drop
            }

            int TAB_ID = TabId;
            GUILayout.BeginArea(areaRect, GUI.skin.box);
            for (int i = 0; i < tabCount; i++)
            {
                Rect rect = tabRects[i];
                Rect centre = new Rect(rect.x + ((rect.width / 2) - (iconWidth / 2)), rect.y + ((rect.height / 2) - (iconHeight / 2)), iconWidth, iconHeight);

                Texture icon = i == overrideTab ? (overrideBool ? overrideIcons[0] : overrideIcons[1]) : icons[i];
                // if we arent overriding the icons on a single tab, then the default is icon = icons[i]
                if (i == TAB_ID)
                {
                    GUI.DrawTexture(rect, tabStyles.activeTex);
                    GUI.DrawTexture(rect, tabStyles.activeTex, ScaleMode.StretchToFill, false, 1f, tabStyles.outline, tabStyles.activeBorder, Vector4.zero);
                    GUI.Box(centre, new GUIContent(icon, toolTips[i]), tabStyles.iconStyle);
                }
                else
                {
                    GUI.DrawTexture(rect, tabStyles.inactiveTex);
                    GUI.DrawTexture(rect, tabStyles.inactiveTex, ScaleMode.StretchToFill, false, 1f, tabStyles.outline, tabStyles.inactiveBorder, Vector4.zero);
                    GUI.DrawTexture(rect, tabStyles.inactiveTex, ScaleMode.StretchToFill, false, 1f, tabStyles.ghost, tabStyles.ghostBorder, Vector4.zero);
                    GUI.Box(centre, new GUIContent(icon, toolTips[i]), tabStyles.iconStyle);
                }

                Event mouseEvent = Event.current;
                if (rect.Contains(mouseEvent.mousePosition))
                {
                    if (mouseEvent.type == EventType.MouseDown && mouseEvent.clickCount == 1)
                    {
                        TAB_ID = i;
                        SceneView.RepaintAll();
                    }
                }
            }
            Rect contentRect = new Rect(0, tabHeight, areaRect.width, areaRect.height - tabHeight);
            GUI.DrawTexture(contentRect, tabStyles.activeTex);
            if (!fullWindow)
                GUI.DrawTexture(contentRect, tabStyles.activeTex, ScaleMode.StretchToFill, false, 1f, tabStyles.outline, tabStyles.contentBorder, Vector4.zero);

            GUILayout.EndArea();
            return TAB_ID;
        }
    }
}