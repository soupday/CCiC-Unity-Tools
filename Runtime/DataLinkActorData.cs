using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Reallusion.Import
{
    public class DataLinkActorData : MonoBehaviour
    {
        public string linkId;
        public string prefabGuid;
        public string fbxGuid;
        public long createdTimeStamp;

#if UNITY_EDITOR
        public void Set(string linkId, GameObject prefabAsset, GameObject fbxAsset)
        {
            this.linkId = linkId;
            if (prefabAsset)
                prefabGuid = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(prefabAsset)).ToString();
            if (fbxAsset)
                fbxGuid = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(fbxAsset)).ToString();
            createdTimeStamp = DateTime.Now.Ticks;
        }

        public void Set(string linkId)
        {
            this.linkId = linkId;
            createdTimeStamp = DateTime.Now.Ticks;
        }

        public void Set(string linkId, GameObject prefabAsset, string fbxPath)
        {
            this.linkId = linkId;
            if (prefabAsset)
                prefabGuid = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(prefabAsset)).ToString();
            fbxGuid = AssetDatabase.GUIDFromAssetPath(fbxPath).ToString();
            createdTimeStamp = DateTime.Now.Ticks;
        }

        public void UpdateTimeStamp()
        {
            createdTimeStamp = DateTime.Now.Ticks;
        }
#endif

    }
}