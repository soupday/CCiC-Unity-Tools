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

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

using UnityEngine.Rendering;
using Object = UnityEngine.Object;

#if HDRP_10_5_0_OR_NEWER
using UnityEngine.Rendering.HighDefinition;
#elif URP_10_5_0_OR_NEWER
using UnityEngine.Rendering.Universal;
#elif UNITY_POST_PROCESSING_3_1_1
using UnityEngine.Rendering.PostProcessing;
#else

#endif

namespace Reallusion.Import
{
    public class UnityLinkSceneManagement
    {
#region Timeline Asset
        // initiated from ui
        public static PlayableDirector CreateTimelineAsset()
        {
            string gameObjectName = "RL_TimeLine_Object (" + UnityLinkManager.TIMELINE_REFERENCE_STRING + ")";
            GameObject existingTimelineObject = GameObject.Find(gameObjectName);
            GameObject timelineObject;
            if (existingTimelineObject == null)
                timelineObject = new GameObject(gameObjectName);
            else
                timelineObject = existingTimelineObject;

            DataLinkActorData data = timelineObject.GetComponent<DataLinkActorData>();
            if (data == null) data = timelineObject.AddComponent<DataLinkActorData>();
            data.linkId = Util.RandomString(20);
            data.createdTimeStamp = DateTime.Now.Ticks;

            PlayableDirector director = timelineObject.GetComponent<PlayableDirector>();
            if (director == null)
                director = timelineObject.AddComponent<PlayableDirector>();

            string timelineFolder = Path.Combine(UnityLinkManager.TIMELINE_SAVE_FOLDER.FullPathToUnityAssetPath(), UnityLinkManager.TIMELINE_REFERENCE_STRING);

            string timelinePath = timelineFolder + "/" + UnityLinkManager.TIMELINE_REFERENCE_STRING + ".playable";
            timelinePath = timelinePath.Replace('\\', '/');

            bool createAsset = true;
            TimelineAsset timeline;
            if (director.playableAsset != null)
            {
                if (AssetDatabase.GetAssetPath(director.playableAsset).Equals(timelinePath))
                {
                    createAsset = false;
                }
            }

            if (createAsset)
            {
                // if there is an asset at the expected path
                TimelineAsset t = (TimelineAsset)AssetDatabase.LoadAssetAtPath(timelinePath, typeof(TimelineAsset));
                if (t == null)
                {
                    timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                    //string timelineFolderPath = Path.GetDirectoryName(timelinePath);
                    UnityLinkImporter.CheckUnityPath(timelineFolder);//(timelineFolderPath);
                    AssetDatabase.CreateAsset(timeline, timelinePath);
                }
                else
                    timeline = t;
                director.playableAsset = timeline;
            }
            return director;
        }

        public static bool TryGetSceneTimeLine(out PlayableDirector director)
        {
            //Debug.LogWarning("TryGetSceneTimeLine");
            director = null;

#if UNITY_2023_OR_NEWER
            PlayableDirector[] directors = GameObject.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else       
            PlayableDirector[] directors = GameObject.FindObjectsOfType<PlayableDirector>();
#endif
            if (directors.Length > 0)
            {
                var valids = directors.ToList().FindAll(y => y.playableAsset != null);
                if (valids.Count > 0)
                {
                    List<DataLinkActorData> actorData = new List<DataLinkActorData>();
                    foreach (var pd  in valids)
                    {
                        var data = pd.gameObject.GetComponent<DataLinkActorData>();
                        if (data != null)
                        {
                            actorData.Add(data);
                        }
                    }
                    if (actorData.Count > 0)
                    {
                        actorData.OrderByDescending(x => x.createdTimeStamp).ToList();
                        director = actorData[0].gameObject.GetComponent<PlayableDirector>();
                        return true;
                    }
                }

                foreach (var d in directors) // return first playable director with a valid timeline asset
                {
                    if (d.playableAsset != null)
                    {
                        director = d;
                        return true;
                    }
                }
                return false;
            }
            else
            {
                return false;
            }
        }

        public static bool TryCreateTimeLine(out PlayableDirector director)
        {
            //Debug.LogWarning("TryCreateTimeLine");

            DateTime now = DateTime.Now;
            string stamp = TimeStampString();

            UnityLinkManager.TIMELINE_REFERENCE_STRING = "Timeline Object (" + stamp + ")";
            
            string validatedDestFolder = string.IsNullOrEmpty(UnityLinkManager.IMPORT_DESTINATION_FOLDER) ? UnityLinkManager.IMPORT_DEFAULT_DESTINATION_FOLDER : UnityLinkManager.IMPORT_DESTINATION_FOLDER;

            UnityLinkManager.TIMELINE_SAVE_FOLDER = Path.Combine(validatedDestFolder, UnityLinkManager.SCENE_ASSETS);
            UnityLinkImporter.CheckUnityPath(UnityLinkManager.TIMELINE_SAVE_FOLDER.FullPathToUnityAssetPath());
            UnityLinkManager.TIMELINE_REFERENCE_STRING = "Timeline" + "-" + stamp;
            Util.LogInfo("Creating timeline asset in: " + UnityLinkManager.TIMELINE_SAVE_FOLDER.FullPathToUnityAssetPath());
            director = CreateTimelineAsset();
            return (director != null);
        }

        public static string TimeStampString()
        {
            DateTime now = DateTime.Now;
            return now.Day.ToString("00") + "." + now.Month.ToString("00") + "-" + now.Hour.ToString("00") + "." + now.Minute.ToString("00");
        }
#endregion

#region Add to Scene and Timeline
        // https://discussions.unity.com/t/how-can-i-access-an-animation-playable-asset-from-script/920958
        // TrackType, InstantiateInScene, SourceGameObject, AddToTimeline, AnimationClipList, AnimatedStatus LinkID
        public static void AddToSceneAndTimeLine((TrackType, bool, GameObject, bool, List<AnimationClip>, AnimatedStatus, string) objectTuple)
        {
            TrackType trackType = objectTuple.Item1;
            bool importIntoScene = objectTuple.Item2;
            GameObject sourceGameObject = objectTuple.Item3;
            bool addToTimeline = objectTuple.Item4;
            List<AnimationClip> animClipList = objectTuple.Item5;
            AnimatedStatus animatedStatus = objectTuple.Item6;
            string linkId = objectTuple.Item7;            

            GameObject sceneObject = null;

            if (importIntoScene)
            {
                sceneObject = AddToScene(sourceGameObject, linkId);
            }

            if (addToTimeline)
            {
                AddToTimeline(trackType, sceneObject, animClipList, animatedStatus, linkId);
            }
        }

        public static GameObject AddToScene(GameObject sourceGameObject, string linkId)
        {
            GameObject sceneObject = null;

            // look for object with linkId in the scene and remove it if necessary
            PurgeLinkedSceneObject(linkId);

            if (PrefabUtility.GetPrefabAssetType(sourceGameObject) != PrefabAssetType.NotAPrefab)
            {
                sceneObject = PrefabUtility.InstantiatePrefab(sourceGameObject) as GameObject;
            }
            else
            {
                Debug.LogWarning("NOT A PREFAB");
                sceneObject = GameObject.Instantiate(sourceGameObject);
            }

            sceneObject.transform.position = Vector3.zero;
            sceneObject.transform.rotation = Quaternion.identity;

            return sceneObject;
        }

        public static void AddToTimeline(TrackType trackType, GameObject sceneObject, List<AnimationClip> animClipList, AnimatedStatus animatedStatus, string linkId)
        {
            // unlock the timeline window 
            LockStateTimeLineWindow(false);

            if (UnityLinkManager.SCENE_TIMELINE_ASSET == null)
            {
                Debug.LogWarning("Cannot add to timeline.");
                return;
            }
            PlayableDirector director = UnityLinkManager.SCENE_TIMELINE_ASSET;

            if (trackType.HasFlag(TrackType.AnimationTrackUpdate)) // updating the animation track - no input sceneObject needed - sceneObject should be the GameObject bound to the existing track for the linkid
            {
                if(!TryGetBoundSceneObjectByLinkId(director, linkId, out sceneObject, out bool hasTrack))
                {
                    return;
                }

                if (animatedStatus.HasFlag(AnimatedStatus.Animation))
                {
                    AddAnimationTrackToTimelineByLinkId(director, linkId, sceneObject, animClipList);
                }
                else if (hasTrack && !animatedStatus.HasFlag(AnimatedStatus.Animation))
                {
                    ClearTrackFromTimelineByLinkId<AnimationTrack>(director, linkId, sceneObject);
                }
            }

            if (trackType.HasFlag(TrackType.AnimationTrack) || trackType.HasFlag(TrackType.AnimationTrackUpdate)) // AnimationTrack permitted for this object
            {
                if (animatedStatus.HasFlag(AnimatedStatus.Animation)) // Has detected animation data
                {
                    AddAnimationTrackToTimelineByLinkId(director, linkId, sceneObject, animClipList);
                }
                else
                {
                    ClearTrackFromTimelineByLinkId<AnimationTrack>(director, linkId, sceneObject);
                }
            }
            
            if (trackType.HasFlag(TrackType.ActivationTrack)) // ActivationTrack permitted for this object
            {
                if (animatedStatus.HasFlag(AnimatedStatus.Activation)) // Has detected animation data
                {
                    AddActivationTrackToTimelineByLinkId(director, linkId, sceneObject, animClipList);
                }
                else
                {
                    ClearTrackFromTimelineByLinkId<ActivationTrack>(director, linkId, sceneObject);
                }
            }

            if (!trackType.HasFlag(TrackType.NoTrack))
            {
                RefreshCurrentTimeline(director);
                ShowTimeLineWindow(director);
                MarkSceneAsDirty();
            }
        }

        public static bool TryGetBoundSceneObjectByLinkId(PlayableDirector director, string linkId, out GameObject sceneObject, out bool hasTrack)
        {
            TimelineAsset timeline = director.playableAsset as TimelineAsset;
            sceneObject = null;
            hasTrack = false;

            AnimationTrack workingtrack = null;

            var tracks = timeline.GetOutputTracks();
            foreach (TrackAsset track in tracks)
            {
                if (track.name.Contains(linkId) && track.GetType().Equals(typeof(AnimationTrack)))
                {
                    workingtrack = track as AnimationTrack;
                    break;
                }
            }

            if (workingtrack == null)
            {
                // no track to update
                // 
#if UNITY_2023_OR_NEWER
                DataLinkActorData[] linkedObjects = GameObject.FindObjectsByType<DataLinkActorData>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
                DataLinkActorData[] linkedObjects = GameObject.FindObjectsOfType<DataLinkActorData>();
#endif

                if (linkedObjects.Length > 0)
                {
                    foreach (var linkedObject in linkedObjects)
                    {
                        if (linkedObject.GetComponent<Animator>() != null && linkedObject.linkId == linkId)
                        {
                            Debug.Log($"Found a character with LinkId: {linkId} in the scene. ({linkedObject.name} - {linkedObject.linkId})");
                            sceneObject = linkedObject.gameObject;
                            hasTrack = false;
                            return true;
                        }
                    }
                }                
            }
            else
            {
                // https://discussions.unity.com/t/noob-question-on-timeline-get-gameobject-reference-from-a-timeline-track/790521
                sceneObject = (GameObject)director.GetGenericBinding(workingtrack);
                hasTrack = true;
                return true;
            }
            return false;
        }

        public static void AddAnimationTrackToTimelineByLinkId(PlayableDirector director, string linkId, GameObject sceneObject, List<AnimationClip> animClipList)
        {
            TimelineAsset timeline = director.playableAsset as TimelineAsset;
            AnimationTrack workingtrack = null;

            var tracks = timeline.GetOutputTracks();
            foreach (TrackAsset track in tracks)
            {
                if (track.name.Contains(linkId) && track.GetType().Equals(typeof(AnimationTrack)))
                {
                    workingtrack = track as AnimationTrack;
                    break;
                }
            }

            if (workingtrack == null)
            {
                workingtrack = timeline.CreateTrack<AnimationTrack>(sceneObject.name + "_ANIM_" + linkId);
            }
            else
            {
#if UNITY_2020_1_OR_NEWER

                // purge bound clips unless the input clip list is empty
                if (animClipList.Count > 0)
                {
                    var clips = workingtrack.GetClips();
                    if (clips != null)
                    {
                        foreach (var c in clips)
                        {
                            workingtrack.DeleteClip(c);
                        }
                    }
                }
#endif
            }

            AnimationClip clipToUse = null;
            // find suitable aniamtion clip (should be the first non T-Pose)
            foreach (AnimationClip animClip in animClipList)
            {
                if (animClip == null) continue;

                if (animClip.name.iContains("T-Pose"))
                {
                    continue;
                }
                else
                {
                    clipToUse = animClip;
                }
            }
            if (clipToUse != null)
            {
                TimelineClip clip = workingtrack.CreateClip(clipToUse);
                clip.start = 0f;
                clip.timeScale = 1f;
                clip.duration = clip.duration / clip.timeScale;
            }
            director.SetGenericBinding(workingtrack, sceneObject);

            RefreshCurrentTimeline(director);
        }

        public static void ClearTrackFromTimelineByLinkId<T>(PlayableDirector director, string linkId, GameObject sceneObject)
        {
            TimelineAsset timeline = director.playableAsset as TimelineAsset;
            TrackAsset workingtrack = null;

            var tracks = timeline.GetOutputTracks();
            foreach (TrackAsset track in tracks)
            {
                if (track.name.Contains(linkId) && track.GetType().Equals(typeof(T)))
                {
                    workingtrack = track;
                    break;
                }
            }

            if (workingtrack != null)
            {
                timeline.DeleteTrack(workingtrack);
            }

            RefreshCurrentTimeline(director);
        }

        public static void AddActivationTrackToTimelineByLinkId(PlayableDirector director, string linkId, GameObject sceneObject, List<AnimationClip> animClipList)
        {
            TimelineAsset timeline = director.playableAsset as TimelineAsset;
            ActivationTrack workingtrack = null;

            var tracks = timeline.GetOutputTracks();
            foreach (TrackAsset track in tracks)
            {
                if (track.name.Contains(linkId) && track.GetType().Equals(typeof(ActivationTrack)))
                {
                    workingtrack = track as ActivationTrack;
                    break;
                }
            }

            if (workingtrack == null)
            {
                workingtrack = timeline.CreateTrack<ActivationTrack>(sceneObject.name + "_ACTI_" + linkId);
            }
            else
            {
#if UNITY_2020_1_OR_NEWER
                // purge bound clips
                var clips = workingtrack.GetClips();
                if (clips != null)
                {
                    foreach (var c in clips)
                    {
                        workingtrack.DeleteClip(c);
                    }
                }
#endif
            }

            foreach (AnimationClip animClip in animClipList)
            {
                var bindings = AnimationUtility.GetCurveBindings(animClip);
                var b_enabled = bindings.ToList().FirstOrDefault(x => x.propertyName.iContains("ProxyActive"));

                var curve = AnimationUtility.GetEditorCurve(animClip, b_enabled);
                bool enabled = false;
                bool addClip = false;
                float start = 0f;
                float duration = 0f;
                int index = 0;
                foreach (Keyframe keyframe in curve.keys)
                {
                    if (!enabled)
                    {
                        if (keyframe.value == 1)
                        {
                            enabled = true;
                            start = keyframe.time;
                        }
                    }

                    if (enabled)
                    {
                        if (keyframe.value == 0 || index == curve.keys.Length - 1)
                        {
                            enabled = false;
                            duration = keyframe.time - start;
                            addClip = true;
                        }
                    }

                    if (addClip)
                    {
                        TimelineClip t = workingtrack.CreateDefaultClip();
                        t.start = start;
                        t.duration = duration;
                        addClip = false;
                    }
                    index++;
                }
            }
            director.SetGenericBinding(workingtrack, sceneObject);

            RefreshCurrentTimeline(director);
        }

        public static void PurgeLinkedSceneObject(string linkId)
        {
            DataLinkActorData existing = null;

#if UNITY_2023_OR_NEWER
            DataLinkActorData[] linkedObjects = GameObject.FindObjectsByType<DataLinkActorData>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            DataLinkActorData[] linkedObjects = GameObject.FindObjectsOfType<DataLinkActorData>();
#endif

            if (linkedObjects != null && linkedObjects.Length > 0)
            {
                existing = linkedObjects.ToList().Find(x => x.linkId == linkId);
            }

            if (existing != null)
            {
                GameObject.DestroyImmediate(existing.gameObject);
            }            
        }


        public static void ShowTimeLineWindow(PlayableDirector director)
        {
            //Debug.LogWarning("ShowTimeLineWindow");
#if UNITY_2021_1_OR_NEWER
            if (EditorWindow.HasOpenInstances<TimelineEditorWindow>())
            {
                //Debug.LogWarning("TimelineEditorWindow is open");
            }
            else
            {
                //Debug.LogWarning("TimelineEditorWindow is not open");
                EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline");
            }
            var timelineWindow = EditorWindow.GetWindow<TimelineEditorWindow>();
            timelineWindow.Show();
#else
            EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline");
#endif
            Selection.activeGameObject = director.gameObject;
            if (UnityLinkManager.LOCK_TIMELINE_TO_LAST_USED) LockStateTimeLineWindow(true);
        }
            

        public static void LockStateTimeLineWindow(bool locked)
        {
#if UNITY_2021_1_OR_NEWER
            if (EditorWindow.HasOpenInstances<TimelineEditorWindow>())
            {
                EditorWindow.GetWindow<TimelineEditorWindow>().locked = locked;
            }
#endif
        }

        public static void MarkSceneAsDirty()
        {
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static void SetTimelineTimeIndex(float time)
        {
            PlayableDirector director = null;

            if (UnityLinkManager.SCENE_TIMELINE_ASSET == null)
            {
                if (!TryGetSceneTimeLine(out director))
                {
                    return;
                }
            }
            else
            {
                director = UnityLinkManager.SCENE_TIMELINE_ASSET;
            }

            if (director != null)
            {
                Selection.activeGameObject = director.gameObject;
                var tl = TimelineEditor.GetOrCreateWindow();
                tl.Focus();
                if (TimelineEditor.inspectedDirector != null)
                {
                    TimelineEditor.inspectedDirector.time = time;
                    TimelineEditor.inspectedDirector.Evaluate();
                }
            }
        }

        static void RefreshCurrentTimeline(PlayableDirector director)
        {
            Selection.activeGameObject = director.gameObject;
            if (TimelineEditor.inspectedDirector != null)
            {
                double timestamp = TimelineEditor.inspectedDirector.time;
                TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);
                TimelineEditor.inspectedDirector.time = timestamp;
                TimelineEditor.inspectedDirector.Evaluate();
            }
        }
#endregion Add To Scene and Timeline

#region Scene Dependencies 
        public static void CreateStagingSceneDependencies(bool dofEnabled)
        {
#if HDRP_10_5_0_OR_NEWER
            CreateHDRPVolumeAsset(dofEnabled);
#elif URP_10_5_0_OR_NEWER
            CreateURPVolumeAsset(dofEnabled);
#elif UNITY_POST_PROCESSING_3_1_1
            CreatePostProcessVolumeAsset();
#else
            DoBuiltinThings();
#endif
        }

#if HDRP_10_5_0_OR_NEWER
        private static void CreateHDRPVolumeAsset(bool dofEnabled)
        {
            string defaultProfileToClone = "CinematicDark";// "FAILOVERCHECK"; // search term for a default profile if one needs to be created
            Volume global = null;
            VolumeProfile sharedProfile = null;

#if UNITY_2023_OR_NEWER
            Volume[] volumes = GameObject.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            Volume[] volumes = GameObject.FindObjectsOfType<Volume>();
#endif

            foreach (Volume volume in volumes)
            {
                if (volume.isGlobal)
                {
                    global = volume;
                    break;
                }
            }

            if (global == null)
            {
                GameObject gameObject = new GameObject("RL_Global_Volume_Object");
                global = gameObject.AddComponent<Volume>();
                global.isGlobal = true;
            }

            if (global == null) { Debug.LogWarning("CreateHDRPVolumeAsset no global volume could be found or made."); return; }

            if (global.sharedProfile == null)
            {
                string sharedProfilePath = UnityLinkManager.SCENE_FOLDER + "/" + "RL_Volume_" + UnityLinkManager.SCENE_NAME + ".asset";

                // try to load existing scene specific volume profile (one that was previously auto created)
                // really only relevant to saved scenes - profles for unsaved scene have timestamps
                if (File.Exists(sharedProfilePath.UnityAssetPathToFullPath()))
                {
                    Debug.LogWarning("Attempting to use existing Volume Profile for scene at: " + sharedProfilePath);
                    sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(sharedProfilePath);
                }

                // if no existing asset, clone one of the preview volume profiles from the shader package
                if (sharedProfile == null)
                {
                    string[] volumeGuids = AssetDatabase.FindAssets("RL_previewGlobalProfile_", new string[] { "Assets" });
                    foreach (string volumeGuid in volumeGuids)
                    {
                        if (AssetDatabase.GUIDToAssetPath(volumeGuid).iContains(defaultProfileToClone))
                        {
                            VolumeProfile found = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetDatabase.GUIDToAssetPath(volumeGuid));
                            if (found != null)
                            {
                                sharedProfile = GameObject.Instantiate(found);
                            }
                        }
                    }
                }

                // if unable to clone a preview volume, create a default volume profile
                if (sharedProfile == null)
                {
                    if (!File.Exists(sharedProfilePath.UnityAssetPathToFullPath()))
                    {
                        Debug.LogWarning("Creating new volume profile");
                        UnityLinkImporter.CheckUnityPath(UnityLinkManager.SCENE_FOLDER);  // make sure parent folder exists
                        sharedProfile = VolumeProfileFactory.CreateVolumeProfileAtPath(sharedProfilePath);
                    }
                }

                // total failure case
                if (sharedProfile == null)
                { 
                    Debug.LogWarning("Cannot find, clone or create a default volume profile - please attempt manual creation and add to the <Volume> GameObject in the scene"); return; 
                }

                if (!File.Exists(sharedProfilePath.UnityAssetPathToFullPath()))
                {
                    Debug.LogWarning("Creating HDRP VolumeAsset: " + sharedProfilePath);
                    UnityLinkImporter.CheckUnityPath(UnityLinkManager.SCENE_FOLDER);  // make sure parent folder exists
                    // VolumeProfileFactory.CreateVolumeProfileAtPath(sharedProfilePath, sharedProfile); //Core RP 17.1+ Unity 6000.1+
                    AssetDatabase.CreateAsset(sharedProfile, sharedProfilePath);
                }
                else
                {
                    
                }          
                global.sharedProfile = sharedProfile;
                global.runInEditMode = true;
            }
            else
            {
                sharedProfile = global.sharedProfile;
                global.runInEditMode = true;
            }
            
            // From Volume.cs
            // Modifying sharedProfile changes every Volumes that uses this Profile and also changes
            // the Profile settings stored in the Project.            
            // You should not modify Profiles that sharedProfile returns. If you want
            // to modify the Profile of a Volume, use profile instead.  


            // NB changes to profile in edit mode will be lost in play mode

            if (dofEnabled)
            {
                // depth of field override
                if (!global.sharedProfile.TryGet<DepthOfField>(out DepthOfField dof))
                {
                    //dof = global.sharedProfile.Add<DepthOfField>(true);
                    dof = VolumeProfileFactory.CreateVolumeComponent<DepthOfField>(profile: global.sharedProfile,
                                                                                   overrides: true,
                                                                                   saveAsset: true);
                }

                //dof.SetAllOverridesTo(true);
                DepthOfFieldModeParameter mode = new DepthOfFieldModeParameter(DepthOfFieldMode.UsePhysicalCamera, true);
                dof.focusMode = mode;
#if UNITY_2021_1_OR_NEWER
                FocusDistanceModeParameter distanceMode = new FocusDistanceModeParameter(FocusDistanceMode.Camera, true);
                dof.focusDistanceMode = distanceMode;
#else
                dof.focusDistance = new MinFloatParameter(10f, 0.1f, true);
                dof.physicallyBased = true;
#endif
                dof.quality.levelAndOverride = (2, false);

                //dof.nearMaxBlur = 7f;
                //dof.nearSampleCount = 8;
                //dof.farMaxBlur = 13f;
                //dof.farSampleCount = 14;

                dof.highQualityFiltering = true;
            }
            // other overrides

            // diffusion profiles
#if HDRP_14_0_0_OR_NEWER
            // prior to HDRP13 not possible via script (leave as HDRP14 define)
            // https://discussions.unity.com/t/modify-diffusion-profile-settings-from-scripting/859377/2

            string[] profiles = new string[] { "RL_Skin_Profile", "RL_Teeth_Profile", "RL_Eye_Profile", "RL_SSS_Profile" };

            if (!global.sharedProfile.TryGet<DiffusionProfileList>(out DiffusionProfileList dpl))
            {
                dpl = VolumeProfileFactory.CreateVolumeComponent<DiffusionProfileList>(
                    profile: global.sharedProfile,
                    overrides: true,
                    saveAsset: true);
            }

            bool addDiffProfiles = false;
            if (dpl != null)
            {
                if (dpl.diffusionProfiles.value != null)
                {                    
                    if (dpl.diffusionProfiles.value.Length == 0)
                    {
                        addDiffProfiles = true;
                    }
                }
                else
                {
                    addDiffProfiles = true;
                }
            }

            if (addDiffProfiles)
            {
                List<DiffusionProfileSettings> dpsList = new List<DiffusionProfileSettings>();

                foreach (string profile in profiles)
                {
                    Object asset = Util.FindAsset(profile);
                    if (asset.GetType() == typeof(DiffusionProfileSettings))
                    {
                        DiffusionProfileSettings dpAsset = (DiffusionProfileSettings)asset;
                        dpsList.Add(dpAsset);
                    }
                }
                dpl.diffusionProfiles.value = dpsList.ToArray();
            }
#endif

            AssetDatabase.SaveAssetIfDirty(sharedProfile);
            // https://discussions.unity.com/t/resource-tutorial-urp-hdrp-volumemananger-not-initialized-in-frame-1/1558540
            v = global;
            EditorApplication.update -= WaitForFrames;
            EditorApplication.update += WaitForFrames;
        }
        static Volume v;
        static int frames = 10;
        static void WaitForFrames()
        {
            frames--;
            if (frames > 0) return;

            frames = 10;
            EditorApplication.update -= WaitForFrames;
            Debug.LogWarning("Registering Volume");

#if UNITY_6000_0_OR_NEWER
            VolumeManager.instance.Unregister(v);
            VolumeManager.instance.Register(v);
#else
            VolumeManager.instance.Unregister(v, 0);
            VolumeManager.instance.Register(v, 0);
#endif
        }

#elif URP_10_5_0_OR_NEWER
        public static void CreateURPVolumeAsset(bool dofEnabled)
        {
            string defaultProfileToClone = "RL_URP Post Processing Profile";// "FAILOVERCHECK"; // search term for a default profile if one needs to be created
            Volume global = null;
            VolumeProfile sharedProfile = null;

#if UNITY_2023_OR_NEWER
            Volume[] volumes = GameObject.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            Volume[] volumes = GameObject.FindObjectsOfType<Volume>();
#endif

            foreach (Volume volume in volumes)
            {
                if (volume.isGlobal)
                {
                    global = volume;
                    break;
                }
            }

            if (global == null)
            {
                GameObject gameObject = new GameObject("RL_Global_Volume_Object");
                global = gameObject.AddComponent<Volume>();
                global.isGlobal = true;
            }

            if (global == null) { Debug.LogWarning("CreateURPVolumeAsset no global volume could be found or made."); return; }

            if (global.sharedProfile == null)
            {
                string sharedProfilePath = UnityLinkManager.SCENE_FOLDER + "/" + "RL_Volume_" + UnityLinkManager.SCENE_NAME + ".asset";

                // try to load existing scene specific volume profile (one that was previously auto created)
                // really only relevant to saved scenes - profles for unsaved scene have timestamps
                if (File.Exists(sharedProfilePath.UnityAssetPathToFullPath()))
                {
                    Debug.LogWarning("Attempting to use existing Volume Profile for scene at: " + sharedProfilePath);
                    sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(sharedProfilePath);
                }

                // if no existing asset, clone one of the preview volume profiles from the shader package
                if (sharedProfile == null)
                {
                    string[] volumeGuids = AssetDatabase.FindAssets("RL_URP Post Processing Profile", new string[] { "Assets" });
                    foreach (string volumeGuid in volumeGuids)
                    {
                        if (AssetDatabase.GUIDToAssetPath(volumeGuid).iContains(defaultProfileToClone))//, StringComparison.InvariantCultureIgnoreCase))
                        {
                            VolumeProfile found = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetDatabase.GUIDToAssetPath(volumeGuid));
                            if (found != null)
                            {
                                sharedProfile = GameObject.Instantiate(found);
                            }
                        }
                    }
                }

                // if unable to clone a preview volume, create a default volume profile
                if (sharedProfile == null)
                {
                    if (!File.Exists(sharedProfilePath.UnityAssetPathToFullPath()))
                    {
                        Debug.LogWarning("Creating new volume profile at " + UnityLinkManager.SCENE_FOLDER);
                        UnityLinkImporter.CheckUnityPath(UnityLinkManager.SCENE_FOLDER);  // make sure parent folder exists
                        sharedProfile = VolumeProfileFactory.CreateVolumeProfileAtPath(sharedProfilePath);
                    }
                }

                // total failure case
                if (sharedProfile == null)
                {
                    Debug.LogWarning("Cannot find, clone or create a default volume profile - please attempt manual creation and add to the <Volume> GameObject in the scene"); return;
                }

                if (!File.Exists(sharedProfilePath.UnityAssetPathToFullPath()))
                {
                    Util.LogInfo("Creating URP VolumeAsset: " + sharedProfilePath);
                    UnityLinkImporter.CheckUnityPath(UnityLinkManager.SCENE_FOLDER);  // make sure parent folder exists
                    // VolumeProfileFactory.CreateVolumeProfileAtPath(sharedProfilePath, sharedProfile); //Core RP 17.1+ Unity 6000.1+
                    AssetDatabase.CreateAsset(sharedProfile, sharedProfilePath);
                }
                else
                {

                }
                Util.LogInfo("Assigning URP VolumeAsset: " + sharedProfile.name);
                global.sharedProfile = sharedProfile;
                global.runInEditMode = true;
            }
            else
            {
                sharedProfile = global.sharedProfile;
                global.runInEditMode = true;
            }
            
            if (dofEnabled)
            {
                // depth of field override
                if (!global.sharedProfile.TryGet<DepthOfField>(out DepthOfField dof))
                {
                    //dof = global.profile.Add<DepthOfField>(true);
                    dof = VolumeProfileFactory.CreateVolumeComponent<DepthOfField>(profile: global.sharedProfile, overrides: true, saveAsset: true);
                }
                dof.SetAllOverridesTo(true);
                DepthOfFieldModeParameter mode = new DepthOfFieldModeParameter(DepthOfFieldMode.Bokeh, true);
                dof.mode = mode;
            }
            // other overrides

            AssetDatabase.SaveAssetIfDirty(sharedProfile);
            // https://discussions.unity.com/t/resource-tutorial-urp-hdrp-volumemananger-not-initialized-in-frame-1/1558540
            v = global;
            EditorApplication.update -= WaitForFrames;
            EditorApplication.update += WaitForFrames;
        }
        static Volume v;
        static int frames = 10;
        static void WaitForFrames()
        {
            frames--;
            if (frames > 0) return;

            frames = 10;
            EditorApplication.update -= WaitForFrames;
            //Debug.LogWarning("Registering Volume");

#if UNITY_6000_0_OR_NEWER
            VolumeManager.instance.Unregister(v);
            VolumeManager.instance.Register(v);
#else
            VolumeManager.instance.Unregister(v, 0);
            VolumeManager.instance.Register(v, 0);
#endif
        }
#elif UNITY_POST_PROCESSING_3_1_1
        public static void CreatePostProcessVolumeAsset()
        {
            // RL Preview Scene Post Processing Volume Profile 3.1.1
            string searchTerm = "RL Preview Scene";
            string defaultProfileToClone = "Post Processing Volume Profile";// "FAILOVERCHECK"; // search term for a default profile if one needs to be created
            PostProcessVolume global = null;
            PostProcessProfile sharedProfile = null;

#if UNITY_2023_OR_NEWER
            PostProcessVolume[] volumes = GameObject.FindObjectsByType<PostProcessVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            PostProcessVolume[] volumes = GameObject.FindObjectsOfType<PostProcessVolume>(true);
#endif

            foreach (PostProcessVolume volume in volumes)
            {
                if (volume.isGlobal)
                {
                    global = volume;
                    break;
                }
            }

            if (global == null)
            {
#if UNITY_2021_3_OR_NEWER
                GameObject go = GameObject.Find("RL_Global_Volume_Object");
#else
                GameObject go = GameObject.Find("RL_Global_Volume_Object");
#endif
                if (go == null)
                {
                    Debug.LogWarning("No volume object");
                    go = new GameObject("RL_Global_Volume_Object");
                }
                else
                {
                    Debug.LogWarning("FOUND volume object");
                }
                global = go.GetComponent<PostProcessVolume>();
                if (global == null)
                {
                    Debug.LogWarning("No postprocess volume on the volume object");
                    global = go.AddComponent<PostProcessVolume>();
                    if (global == null)
                    {
                        Debug.LogWarning("Failed to create postprocess volume on the volume object");
                    }
                    else
                    {
                        Debug.LogWarning("Created postprocess volume on the volume object");
                    }
                }
                else
                {
                    Debug.LogWarning("FOUND postprocess volume on the volume object");
                }
                global.isGlobal = true;
            }

            if (global == null) { Debug.LogWarning("CreatePostProcessVolumeAsset no global volume could be found or made."); return; }

            if (global.sharedProfile == null)
            {
                string sharedProfilePath = UnityLinkManager.SCENE_FOLDER + "/" + "RL_Volume_" + UnityLinkManager.SCENE_NAME + ".asset";

                // try to load existing scene specific volume profile (one that was previously auto created)
                // really only relevant to saved scenes - profles for unsaved scene have timestamps
                if (File.Exists(sharedProfilePath.UnityAssetPathToFullPath()))
                {
                    Debug.LogWarning("Attempting to use existing Volume Profile for scene at: " + sharedProfilePath);
                    sharedProfile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(sharedProfilePath);
                }

                // if no existing asset, clone one of the preview volume profiles from the shader package
                if (sharedProfile == null)
                {
                    string[] volumeGuids = AssetDatabase.FindAssets(searchTerm, new string[] { "Assets" });
                    foreach (string volumeGuid in volumeGuids)
                    {
                        if (AssetDatabase.GUIDToAssetPath(volumeGuid).iContains(defaultProfileToClone))
                        {
                            PostProcessProfile found = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(AssetDatabase.GUIDToAssetPath(volumeGuid));
                            if (found != null)
                            {
                                sharedProfile = GameObject.Instantiate(found);
                            }
                        }
                    }
                }

                // if unable to clone a preview volume, create a default volume profile
                if (sharedProfile == null)
                {
                    if (!File.Exists(sharedProfilePath.UnityAssetPathToFullPath()))
                    {
                        Debug.LogWarning("Creating new volume profile");
                        UnityLinkImporter.CheckUnityPath(UnityLinkManager.SCENE_FOLDER);  // make sure parent folder exists
                        //sharedProfile = VolumeProfileFactory.CreateVolumeProfileAtPath(sharedProfilePath);
                        sharedProfile = ScriptableObject.CreateInstance<PostProcessProfile>();
                    }
                }

                // total failure case
                if (sharedProfile == null)
                {
                    Debug.LogWarning("Cannot find, clone or create a default volume profile - please attempt manual creation and add to the <Volume> GameObject in the scene"); return;
                }

                if (!File.Exists(sharedProfilePath.UnityAssetPathToFullPath()))
                {
                    Debug.LogWarning("Creating HDRP VolumeAsset: " + sharedProfilePath);
                    UnityLinkImporter.CheckUnityPath(UnityLinkManager.SCENE_FOLDER);  // make sure parent folder exists
                    // VolumeProfileFactory.CreateVolumeProfileAtPath(sharedProfilePath, sharedProfile); //Core RP 17.1+ Unity 6000.1+
                    AssetDatabase.CreateAsset(sharedProfile, sharedProfilePath);
                }
                else
                {

                }
                global.sharedProfile = sharedProfile;
                global.runInEditMode = true;
            }
            else
            {
                sharedProfile = global.sharedProfile;
                global.runInEditMode = true;
            }
            /*
            // depth of field override
            if (!global.sharedProfile.TryGet<DepthOfField>(out DepthOfField dof))
            {
                //dof = global.sharedProfile.Add<DepthOfField>(true);
                dof = VolumeProfileFactory.CreateVolumeComponent<DepthOfField>(profile: global.sharedProfile,
                                                                               overrides: true,
                                                                               saveAsset: true);
            }
            dof.SetAllOverridesTo(true);
            DepthOfFieldModeParameter mode = new DepthOfFieldModeParameter(DepthOfFieldMode.Bokeh, true);
            dof.mode = mode;

            // other overrides
            */

            AssetDatabase.SaveAssetIfDirty(sharedProfile);
        }
#else
        public static void DoBuiltinThings() { }
#endif
        #endregion Scene Dependencies

#region Enum

        [Flags]
        public enum TrackType
        {
            None,
            NoTrack,
            AnimationTrack,
            AnimationTrackUpdate,
            ActivationTrack,
            AudioTrack
        }

        [Flags]
        public enum AnimatedStatus
        {
            None = 0,
            NotAnimated = 1,
            Animation = 2,
            Activation = 4,
        }
#endregion Enum
    }
}