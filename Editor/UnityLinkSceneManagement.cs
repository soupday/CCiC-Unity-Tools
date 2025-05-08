using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace Reallusion.Import
{
    public class UnityLinkSceneManagement
    {
        #region depracated
        /*
        public static GameObject GetTimeLineObject() // find a timeline object corresponding to the current scene ref, one or make a new one.
        {
            PlayableDirector[] directors = GameObject.FindObjectsOfType<PlayableDirector>();

            if (directors.Length > 0)
            {
                foreach (var director in directors)
                {
                    if (director.playableAsset != null)
                    {
                        Debug.Log(AssetDatabase.GetAssetPath(director.playableAsset));
                        if (AssetDatabase.GetAssetPath(director.playableAsset).Equals(UnityLinkManager.TIMELINE_ASSET_PATH))
                        {
                            Debug.Log("Found existing timeline object.");
                            return director.gameObject;
                        }
                    }
                }
            }
            else
            {
                Debug.Log("Making new timeline object.");
                GameObject timelineObject = new GameObject("RL_TimeLine_Object");
                PlayableDirector playableDirector = timelineObject.AddComponent<PlayableDirector>();

                TimelineAsset timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                CheckUnityPath(UnityLinkManager.UNITY_FOLDER_PATH);
                AssetDatabase.CreateAsset(timelineAsset, UnityLinkManager.TIMELINE_ASSET_PATH);
                playableDirector.playableAsset = timelineAsset;
                return timelineObject;
            }
            return null;
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
        */
        #endregion depracated

        #region Scene and Timeline
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
            director = null;
            PlayableDirector[] directors = GameObject.FindObjectsOfType<PlayableDirector>();
            if (directors.Length > 0)
            {
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
            DateTime now = DateTime.Now;
            string stamp = TimeStampString();

            UnityLinkManager.TIMELINE_REFERENCE_STRING = "Timeline Object (" + stamp + ")";
            UnityLinkManager.TIMELINE_SAVE_FOLDER = Path.Combine(UnityLinkManager.IMPORT_DESTINATION_FOLDER, UnityLinkManager.SCENE_ASSETS);
            UnityLinkImporter.CheckUnityPath(UnityLinkManager.TIMELINE_SAVE_FOLDER.FullPathToUnityAssetPath());
            UnityLinkManager.TIMELINE_REFERENCE_STRING = "Timeline" + "-" + stamp;
            Debug.Log("Creating timeline asset in: " + UnityLinkManager.TIMELINE_SAVE_FOLDER.FullPathToUnityAssetPath());
            director = CreateTimelineAsset();
            return (director != null);            
        }

        public static string TimeStampString()
        {
            DateTime now = DateTime.Now;
            return now.Day.ToString("00") + "." + now.Month.ToString("00") + "-" + now.Hour.ToString("00") + "." + now.Minute.ToString("00");
        }

        public static void CreateExampleScene()
        {
            /*
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            }

            if (EditorSceneManager.GetActiveScene().name.Equals(UnityLinkManager.TIMELINE_REFERENCE_STRING))
            {
                // dont create a new scene or prompt for a re-use/renew of scene
                GameObject go = GetTimeLineObject();
                Debug.Log("Found " + go.name);
            }
            else
            {
                Debug.Log("Making new scene.");
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

                EditorSceneManager.SaveScene(scene, UnityLinkManager.UNITY_SCENE_PATH);
                GetTimeLineObject();
            }
            */
        }



        // https://discussions.unity.com/t/how-can-i-access-an-animation-playable-asset-from-script/920958
        // TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);

        public static void AddToSceneAndTimeLine((TrackType, GameObject, List<AnimationClip>, bool, string) objectTuple)
        {
            Debug.LogWarning("Instantiating " + objectTuple.Item2.name);
            GameObject sceneObject = objectTuple.Item4 ? GameObject.Instantiate(objectTuple.Item2) : objectTuple.Item2;
            sceneObject.transform.position = Vector3.zero;
            sceneObject.transform.rotation = Quaternion.identity;

            if (UnityLinkManager.SCENE_TIMELINE_ASSET == null)
            {
                Debug.LogWarning("Cannot add to timeline.");
                return;
            }

            PlayableDirector director = UnityLinkManager.SCENE_TIMELINE_ASSET;
            TimelineAsset timeline = director.playableAsset as TimelineAsset;

            if (objectTuple.Item1 == TrackType.AnimationTrack)
            {
                AnimationTrack workingtrack = null;

                var tracks = timeline.GetOutputTracks();
                foreach (TrackAsset track in tracks)
                {
                    if (track.name.EndsWith(objectTuple.Item5) && track.GetType().Equals(typeof(AnimationTrack)))
                    {
                        workingtrack = track as AnimationTrack;
                        break;
                    }
                }

                if (workingtrack == null)
                {
                    workingtrack = timeline.CreateTrack<AnimationTrack>(objectTuple.Item2.name + "_" + objectTuple.Item4);
                }
                else
                {
                    // purge bound clips
                    var clips = workingtrack.GetClips();
                    if (clips != null)
                    {
                        foreach (var c in clips)
                        {
                            workingtrack.DeleteClip(c);
                        }
                    }
                }

                //AnimationTrack newTrack = timeline.CreateTrack<AnimationTrack>(objectTuple.Item1.name + "_" + objectTuple.Item4);
                AnimationClip clipToUse = null;
                // find suitable aniamtion clip (should be the first non T-Pose)
                foreach (AnimationClip animClip in objectTuple.Item3)
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

                TimelineClip clip = workingtrack.CreateClip(clipToUse); //newTrack.CreateClip(clipToUse);
                clip.start = 0f;
                clip.timeScale = 1f;
                clip.duration = clip.duration / clip.timeScale;
                Debug.LogWarning("SetGenericTimelineBinding " + objectTuple.Item2.name + " - " + clipToUse.name);
                director.SetGenericBinding(workingtrack, sceneObject);
            }

            if (objectTuple.Item1 == TrackType.ActivationTrack)
            {
                ActivationTrack workingtrack = null;

                var tracks = timeline.GetOutputTracks();
                foreach (TrackAsset track in tracks)
                {
                    if (track.name.EndsWith(objectTuple.Item5) && track.GetType().Equals(typeof(ActivationTrack)))
                    {
                        workingtrack = track as ActivationTrack;
                        break;
                    }
                }

                if (workingtrack == null)
                {
                    workingtrack = timeline.CreateTrack<ActivationTrack>(objectTuple.Item2.name + "_" + objectTuple.Item4);
                }
                else
                {
                    // purge bound clips
                    var clips = workingtrack.GetClips();
                    if (clips != null)
                    {
                        foreach (var c in clips)
                        {
                            workingtrack.DeleteClip(c);
                        }
                    }
                }

                foreach (AnimationClip animClip in objectTuple.Item3)
                {
                    var bindings = AnimationUtility.GetCurveBindings(animClip);
                    var b_enabled = bindings.ToList().FirstOrDefault(x => x.propertyName.Contains("ProxyActive", System.StringComparison.InvariantCultureIgnoreCase));

                    var curve = AnimationUtility.GetEditorCurve(animClip, b_enabled);
                    bool enabled = false;
                    bool addClip = false;
                    float start = 0f;
                    float duration = 0f;
                    int index = 0;
                    foreach(Keyframe keyframe in curve.keys)
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
                            if (keyframe.value == 0 || index == curve.keys.Length -1)
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
            }

            TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);
            ShowTimeLineWindow(director);
        }

        public static void ShowTimeLineWindow(PlayableDirector director)
        {
            if (EditorWindow.HasOpenInstances<TimelineEditorWindow>())
            {
                Debug.LogWarning("TimelineEditorWindow is open");                
            }
            else
            {
                Debug.LogWarning("TimelineEditorWindow is not open");
                EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline");
            }
            EditorWindow.GetWindow<TimelineEditorWindow>().Show();
            Selection.activeGameObject = director.gameObject;
            
        }
        #endregion Scene and Timeline

        #region Scene Dependencies 
        public static void CreateStagingSceneDependencies()
        {
#if HDRP_10_5_0_OR_NEWER
            CreateHDRPVolumeAsset();
#elif URP_10_5_0_OR_NEWER
            DoURPThings();
#else
            DoBuiltinThings();
#endif
        }

#if HDRP_10_5_0_OR_NEWER
        private static void CreateHDRPVolumeAsset()
        {
            Volume global = null;
            VolumeProfile sharedProfile = null;
            Volume[] volumes = GameObject.FindObjectsOfType<Volume>();
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

            if (global == null) { Debug.LogWarning("CreateHDRPVolumeAsset global == null"); return; }

            if (global.sharedProfile == null)
            {
                Debug.LogWarning("CreateHDRPVolumeAsset sharedProfile == null");
                
                string[] volumeGuids = AssetDatabase.FindAssets("RL_previewGlobalProfile_", new string[] { "Assets" });
                Debug.LogWarning("CreateHDRPVolumeAsset volumeGuids found: " + volumeGuids.Length);
                foreach (string volumeGuid in volumeGuids)
                {
                    if (AssetDatabase.GUIDToAssetPath(volumeGuid).Contains("CinematicDark", StringComparison.InvariantCultureIgnoreCase))
                    {
                        Debug.LogWarning("CreateHDRPVolumeAsset CinematicDark found");
                        VolumeProfile found = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetDatabase.GUIDToAssetPath(volumeGuid));
                        sharedProfile = GameObject.Instantiate(found);
                        if (sharedProfile == null)
                        {
                            Debug.LogWarning("CreateHDRPVolumeAsset CinematicDark couldnt be loaded");
                        }
                    }
                }

                if (sharedProfile == null) { Debug.LogWarning("CreateHDRPVolumeAsset sharedProfile == null STILL"); return; }

                // moved saving
                string sharedProfilePath = UnityLinkManager.SCENE_FOLDER + "/" + "RL_Volume_" + UnityLinkManager.SCENE_NAME + ".asset";
                Debug.LogWarning("Creating HDRP VolumeAsset: " + sharedProfilePath);
                UnityLinkImporter.CheckUnityPath(UnityLinkManager.SCENE_FOLDER);
                AssetDatabase.CreateAsset(sharedProfile, sharedProfilePath);
                global.sharedProfile = sharedProfile;
            }

            // depth of field override
            if (!global.sharedProfile.TryGet<DepthOfField>(out DepthOfField dof))
            //if (!sharedProfile.TryGet<DepthOfField>(out DepthOfField dof))
            {
                dof = global.sharedProfile.Add<DepthOfField>(true);
                //dof = sharedProfile.Add<DepthOfField>(true);
            }
            dof.SetAllOverridesTo(true);
            DepthOfFieldModeParameter mode = new DepthOfFieldModeParameter(DepthOfFieldMode.UsePhysicalCamera, true);
            dof.focusMode = mode;
            FocusDistanceModeParameter distanceMode = new FocusDistanceModeParameter(FocusDistanceMode.Camera, true);
            dof.focusDistanceMode = distanceMode;
            // other overrides

            // override having no immediate effect
        }
#elif URP_10_5_0_OR_NEWER
        public static void DoURPThings() { }
#else
        public static void DoBuiltinThings() { }
#endif
        #endregion Scene Dependencies

        #region Enum
        public enum TrackType
        {
            AnimationTrack,
            ActivationTrack,
            AudioTrack
        }
        #endregion Enum
    }
}
