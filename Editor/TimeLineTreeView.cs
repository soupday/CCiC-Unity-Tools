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
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace Reallusion.Import
{
#if UNITY_6000_2_OR_NEWER 
    // 'TreeViewItem' is obsolete: 'TreeViewItem is now deprecated. You can likely now use TreeViewItem<int> instead and not think more about it. But if you were using that identifier to store InstanceID data, you should instead opt to upgrade your TreeViews to use TreeViewItem<InstanceID> to get the proper typing.'
    public class TimeLineTreeView : TreeView<int>
#else
    public class TimeLineTreeView : TreeView
#endif
    {
        /*
         * Root                                                     depth = -1  
         *   |
         *   |--  <PlayableDirector> Object (playable asset name)   depth = 0   // EditorGUIUtility.IconContent("TimelineAsset Icon") 
         *                  |
         *                  |-- Animation Track                     depth = 1   // EditorGUIUtility.IconContent("Animator Icon")  typeof(Animator)
         *                  |        |-- Animation Override         depth = 2   // EditorGUIUtility.IconContent("AnimatorOverrideController Icon")
         *                  |-- Activation Track                    depth = 1   // EditorGUIUtility.IconContent("TestPassed")  typeof(GameObject)
         *                  |-- Audio Track                         depth = 1   // EditorGUIUtility.IconContent("d_AudioSource Icon") typeof(AudioSource)
         * 
         */

        PlayableDirector[] playableDirectors;
        bool timeLinesFound = false;

#if UNITY_6000_2_OR_NEWER
        public TimeLineTreeView(TreeViewState<int> treeViewState, PlayableDirector[] objs) : base(treeViewState)
#else
        public TimeLineTreeView(TreeViewState treeViewState, PlayableDirector[] objs) : base(treeViewState)
#endif
        {
            //Force Treeview to reload its data (will force BuildRoot and BuildRows to be called)
            playableDirectors = objs;
            timeLinesFound = playableDirectors.Length > 0;
            Reload();
        }

#if UNITY_6000_2_OR_NEWER
        protected override TreeViewItem<int> BuildRoot()
#else
        protected override TreeViewItem BuildRoot()
#endif
        {
            //indicies
            int mDepth = -1;//root level
            int mId = 0;

#if UNITY_6000_2_OR_NEWER
            var root = new TreeViewItem<int> { id = mId++, depth = mDepth, displayName = "Root" };
            var allItems = new List<TreeViewItem<int>>();
#else
            var root = new TreeViewItem { id = mId++, depth = mDepth, displayName = "Root" };                     
            var allItems = new List<TreeViewItem>();
#endif
            tracked = new List<TrackItemStatus>();

            if (!timeLinesFound)
            {
                mDepth = 0;
                string none = "No timeline objects found - Plase create one [below].";
                tracked.Add(new TrackItemStatus(mId, mDepth, false, false, null, none));

#if UNITY_6000_2_OR_NEWER
                allItems.Add(new TreeViewItem<int> { id = mId, depth = mDepth, displayName = none, icon = (Texture2D)EditorGUIUtility.IconContent("console.warnicon.sml").image });
#else
                allItems.Add(new TreeViewItem { id = mId, depth = mDepth, displayName = none, icon = (Texture2D)EditorGUIUtility.IconContent("console.warnicon.sml").image });
#endif
                SetupParentsAndChildrenFromDepths(root, allItems);
                return root;
            }

            showAlternatingRowBackgrounds = true;
            baseIndent = 18f;
             // retain references to all bound objects their treeview id and an 'active' status bool for ui toogle of depth 0 items

            foreach (PlayableDirector obj in playableDirectors)
            {
                mDepth = 0;//base level     

                if (obj.playableAsset == null)
                {
                    string missing = obj.name + " - has no TimeLine Asset";
                    tracked.Add(new TrackItemStatus(mId, mDepth, false, false, obj, missing));
#if UNITY_6000_2_OR_NEWER
                    allItems.Add(new TreeViewItem<int> { id = mId, depth = mDepth, displayName = missing, icon = (Texture2D)EditorGUIUtility.IconContent("console.warnicon.sml").image });
#else
                    allItems.Add(new TreeViewItem { id = mId, depth = mDepth, displayName = missing, icon = (Texture2D)EditorGUIUtility.IconContent("console.warnicon.sml").image });
#endif
                }
                else
                {
                    PlayableAsset playableAsset = obj.playableAsset;
                    tracked.Add(new TrackItemStatus(mId, mDepth, false, true, obj, playableAsset.name)); // track selected status wrt object
#if UNITY_6000_2_OR_NEWER
                    allItems.Add(new TreeViewItem<int> { id = mId++, depth = mDepth, displayName = playableAsset.name, icon = (Texture2D)EditorGUIUtility.IconContent("TimelineAsset Icon").image });
#else
                    allItems.Add(new TreeViewItem { id = mId++, depth = mDepth, displayName = playableAsset.name, icon = (Texture2D)EditorGUIUtility.IconContent("TimelineAsset Icon").image });
#endif
                    TimelineAsset timeline = playableAsset as TimelineAsset;
                    var tracks = timeline.GetOutputTracks();

                    foreach (TrackAsset track in tracks)
                    {
                        mDepth = 1;
                        string displayName = string.Empty;  // default
                        Texture icon = EditorGUIUtility.IconContent("d_SceneViewOrtho").image;  // default

                        Object bound = obj.GetGenericBinding(track);
                        bool hasBound = bound != null;
                        //Debug.LogWarning("Track type: " + track.GetType().ToString());

                        if (track.GetType() == typeof(AnimationTrack))
                        {
                            displayName += hasBound ? bound.name : "Animation Track (no object)";
                            if (hasBound)
                            {
                                var clips = track.GetClips().ToList();
                                if (clips != null)
                                    if (clips.Count > 0)
                                        displayName += " (" + clips[0].displayName + ")";
                            }
                            icon = EditorGUIUtility.IconContent("Animator Icon").image;
                            //if (hasBound) Debug.LogWarning("AnimationTrack " + bound.GetType().ToString());
                        }

                        if (track.GetType() == typeof(AudioTrack))
                        {
                            displayName += hasBound ? bound.name : "Audio Track (no object)";

                            if (hasBound)
                            {
                                AudioSource src = (AudioSource)bound;
                                if (src != null)
                                    if (src.clip != null)
                                    {
                                        displayName += " (" + src.clip.name + ")";
                                    }
                            }
                            icon = EditorGUIUtility.IconContent("d_AudioSource Icon").image;
                            //if (hasBound) Debug.LogWarning("AudioTrack " + bound.GetType().ToString());
                        }

                        if (track.GetType() == typeof(ActivationTrack))
                        {
                            displayName += hasBound ? bound.name + " (Activation Track)" : "Activation Track (no object)";
                            icon = EditorGUIUtility.IconContent("d_Prefab Icon").image;
                            //if (hasBound) Debug.LogWarning("ActivationTrack " + bound.GetType().ToString());
                        }

                        if (track.GetType() == typeof(ControlTrack))
                        {
                            displayName += hasBound ? bound.name : "Control Track (no object)";
                            icon = EditorGUIUtility.IconContent("d_ParticleSystem Icon").image;
                            //if (hasBound) Debug.LogWarning("ControlTrack " + bound.GetType().ToString());
                        }

                        if (track.GetType() == typeof(GroupTrack))
                        {
                            displayName += hasBound ? bound.name : "Group Track (no object)";
                            icon = EditorGUIUtility.IconContent("d_VerticalLayoutGroup Icon").image;
                            //if (hasBound) Debug.LogWarning("GroupTrack " + bound.GetType().ToString());
                        }

                        if (track.GetType() == typeof(PlayableTrack))
                        {
                            displayName += hasBound ? bound.name : "Playable Track (no object)";
                            icon = EditorGUIUtility.IconContent("d_PlayableDirector Icon").image;
                            //if (hasBound) Debug.LogWarning("PlayableTrack " + bound.GetType().ToString());
                        }
                        tracked.Add(new TrackItemStatus(mId, mDepth, false, false, hasBound ? bound : obj, displayName));
#if UNITY_6000_2_OR_NEWER
                        allItems.Add(new TreeViewItem<int> { id = mId++, depth = mDepth, displayName = displayName, icon = (Texture2D)icon });
#else
                        allItems.Add(new TreeViewItem { id = mId++, depth = mDepth, displayName = displayName, icon = (Texture2D)icon });
#endif
                    }
                }
            }
            SetupParentsAndChildrenFromDepths(root, allItems);
            return root;
        }
                
        public List<TrackItemStatus> tracked;

        public class TrackItemStatus
        {
            public int Id;
            public int Depth;
            public bool Active;
            public bool CanSelect;
            public Object Object;
            public string DisplayName;

            public TrackItemStatus(int id, int depth, bool active, bool canSelect, Object obj, string displayName)
            {
                Id = id;
                Depth = depth;
                Active = active;
                CanSelect = canSelect;
                Object = obj;
                DisplayName = displayName;
            }
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            if (args.item.depth == 0 && timeLinesFound)
            {
                var trackedItem = tracked.Find(x => x.Id == args.item.id);
                if (trackedItem.CanSelect)
                {
                    EditorGUI.BeginChangeCheck();
                    trackedItem.Active = GUI.Toggle(new Rect(args.rowRect.x + 2f, args.rowRect.y - 3, 22, 22), trackedItem.Active, ""); // args.rowRect.x + 18f
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (trackedItem.Active == true)
                        {
                            for (int i = 0; i < tracked.Count; i++) { tracked[i].Active = false; }
                            trackedItem.Active = true;
                            UnityLinkManager.SCENE_TIMELINE_ASSET = trackedItem.Object as PlayableDirector;
                            UnityLinkManager.SCENE_TIMELINE_ASSET_NAME = trackedItem.DisplayName;
                            args.selected = true;
                            this.CollapseAll();
                            this.SetExpanded(args.item.id, true);
                            this.SetSelection(selectedIDs: new List<int>() { args.item.id });
                            this.SetFocusAndEnsureSelectedItem();
                            Selection.activeObject = trackedItem.Object;
                        }
                        else
                        {
                            UnityLinkManager.SCENE_TIMELINE_ASSET = null;
                            UnityLinkManager.SCENE_TIMELINE_ASSET_NAME = "UNSELECTED";
                            args.selected = true;
                            args.selected = false;
                            this.CollapseAll();
                            Selection.activeObject = null;
                            GUI.FocusControl(null);
                        }
                    }
                }
                //args.rowRect.x += 0f;  // += 22f
            }
            base.RowGUI(args);            
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            Debug.Log("Selection has changed: " + selectedIds.Count + " items.");
            List<Object> selectedObjects = new List<Object>();
            foreach (int id in selectedIds)
            {
                selectedObjects.Add(tracked.Find(x => x.Id == id).Object);
            }
            Selection.activeObject = selectedObjects[0];
        }
    }
}