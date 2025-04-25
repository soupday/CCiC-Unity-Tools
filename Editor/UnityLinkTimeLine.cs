using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace Reallusion.Import
{
    public class UnityLinkTimeLine
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
        public static void CreateTimelineAsset()
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
                    CheckUnityPath(timelineFolder);//(timelineFolderPath);
                    AssetDatabase.CreateAsset(timeline, timelinePath);
                }
                else
                    timeline = t;
                director.playableAsset = timeline;
            }
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
            director = null;
            
            return false;
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

        // https://discussions.unity.com/t/how-can-i-access-an-animation-playable-asset-from-script/920958
        // TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);

        public static void AddToSceneAndTimeLine((GameObject, List<AnimationClip>, bool) objectTuple)
        {
            Debug.LogWarning("Instantiating " + objectTuple.Item1.name);
            GameObject sceneObject = objectTuple.Item3 ? GameObject.Instantiate(objectTuple.Item1) : objectTuple.Item1;
            sceneObject.transform.position = Vector3.zero;
            sceneObject.transform.rotation = Quaternion.identity;
           
            if (UnityLinkManager.SCENE_TIMELINE_ASSET == null)
            {
                Debug.LogWarning("Cannot add to timeline.");
                return;
            }

            PlayableDirector director = UnityLinkManager.SCENE_TIMELINE_ASSET;
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
            Debug.LogWarning("SetGenericTimelineBinding " + objectTuple.Item1.name + " - " + clipToUse.name);
            director.SetGenericBinding(newTrack, sceneObject);
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

    }
}
