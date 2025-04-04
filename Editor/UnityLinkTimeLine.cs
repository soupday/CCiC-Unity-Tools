using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        #region Scene and Timeline

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

        public static void CreateExampleScene()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            }

            if (EditorSceneManager.GetActiveScene().name.Equals(UnityLinkManager.SCENE_REFERENCE_STRING))
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

        public static void CreateTimelineInScene()
        {

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
            Debug.LogWarning("SetGenericTimelineBinding " + objectTuple.Item1.name + " - " + clipToUse.name);
            director.SetGenericBinding(newTrack, sceneObject);

        }
        #endregion Scene and Timeline

    }
}
