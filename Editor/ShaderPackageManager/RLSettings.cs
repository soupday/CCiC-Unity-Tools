using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Reallusion.Import
{
    public class RLSettings : Editor
    {
        public const string defaultParent = "Reallusion";
        public const string defaultChild = "CCiC Unity Tools";

        //public const string defaultPath = "Assets/Reallusion/CCiC Unity Tools";
        public const string defaultSettingsName = "RLSettingsObject.asset";
        
        public static RLSettingsObject FindRLSettingsObject()
        {
            string search = "RLSettingsObject";
            string packages = "Packages/";
            string assetExt = ".asset";
            string csExt = ".cs";

            string[] guids = AssetDatabase.FindAssets(search, new string[] { "Assets" });

            // worst case this will return all of the assets in "Packages/" along with matching assets in "Assets/" in Unity 2021.3.14f1
            // expected case the source file "RLSettingsObject.cs" will be returned in "Packages/" if correctly installed and in "Assets/" if not
            // the settings file "RLSettingsObject.asset" should only be found in "Assets/".

            List<string> applicableGuids = new List<string>();

            // iterate through the found guids to eliminate entries from "Packages/" and exclude any .cs files
            // flag any matching .cs files in assets as indicating that the tool is incorrectly installed

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                //Debug.Log(" *** " + path);

                if (!path.StartsWith(packages, StringComparison.InvariantCultureIgnoreCase))
                {
                    //Debug.Log(" *** After Packages/ check: " + path + " Allowed through.");

                    if (Path.GetExtension(path).Equals(assetExt))
                    {
                        //Debug.Log(" ### Accepting: " + path + " it has " + Path.GetExtension(path) + " extension");
                        applicableGuids.Add(guid);
                    }
                    else if (Path.GetExtension(path).Equals(csExt))
                    {
                        //Debug.Log(" ### Rejecting: " + path + " it has " + Path.GetExtension(path) + " extension");
                        // flagging
                        Debug.LogWarning(path + " Found -- CC/iC tools are incorrectly installed -- see documentation for details");
                    }
                }
                else
                {
                    //Debug.Log(" *** After Packages/ check: " + path + " Rejected.");
                }
            }

            bool valid = false;
            string objGuid = "";
            int c = applicableGuids.Count;
            RLSettingsObject result = null;
            if (c > 0)
            {
                if (c == 1)
                {
                    valid = TryGetValidObject(applicableGuids[0], out RLSettingsObject validObj);
                    if (valid)
                    {
                        objGuid = applicableGuids[0];
                        result = UpdateSettingsPath(validObj, applicableGuids[0]);
                    }
                    else
                        MoveGuidToTrash(applicableGuids[0]);
                }
                else // multiple RLSettingsObject.asset files found
                {
                    // store first valid one
                    // check all for a path match
                    // use the matching path one otherwise use the first
                    
                    foreach (string aguid in applicableGuids)
                    {
                        valid = TryGetValidObject(aguid, out RLSettingsObject validObj);
                        if (valid)
                        {
                            if (result == null)
                            { 
                                result = validObj;
                                objGuid = aguid;
                            }
                            if (validObj.lastPath == AssetDatabase.GUIDToAssetPath(aguid))
                            {
                                PurgeAllExcept(applicableGuids, aguid);
                                result = UpdateSettingsPath(validObj, aguid);
                            }
                        }
                    }
                }
            }

            if (applicableGuids.Count == 0 || !valid)
            {
                return CreateSettingsObject();
            }
            else
            {
                if (Version.TryParse(Pipeline.VERSION, out Version pVer) && Version.TryParse(result.toolVersion, out Version tVer))
                {
                    if (pVer == tVer)
                    {
                        return result;
                    }
                    else
                    {
                        MoveGuidToTrash(objGuid);
                        return CreateSettingsObject();
                    }
                }
                else
                {
                    MoveGuidToTrash(objGuid);
                    return CreateSettingsObject();
                }
            }
        }

        public static void PurgeAllExcept(List<string> guids, string guid)
        {
            foreach (string g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                bool valid = Path.GetFileName(path) == defaultSettingsName;
                if (g != guid && valid) MoveGuidToTrash(g);
            }
        }

        public static void MoveGuidToTrash(string guid) // unless the Unity version is before 2021.3
        {
#if UNITY_2021_3_OR_NEWER
            Debug.Log("Removing (file will be moved to the operating system's trashcan): " + AssetDatabase.GUIDToAssetPath(guid));
            AssetDatabase.MoveAssetToTrash(AssetDatabase.GUIDToAssetPath(guid));
#else
            Debug.Log("Deleteing: " + AssetDatabase.GUIDToAssetPath(guid));
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
#endif
        }

        public static RLSettingsObject CreateSettingsObject()
        {
            RLSettingsObject obj = CreateInstance<RLSettingsObject>();
            obj.showOnStartup = true;
            obj.ignoreAllErrors = false;
            obj.checkForUpdates = true;
            obj.updateWindowShownOnce = false;
            obj.postInstallShowUpdateWindow = false;
            obj.performPostInstallationCheck = false;
            obj.updateAvailable = false;
            obj.lastUpdateCheck = 0;
            obj.jsonTagName = string.Empty;
            obj.jsonHtmlUrl = string.Empty;
            obj.jsonPublishedAt = string.Empty;
            obj.jsonBodyLines = null;
            obj.lastPath = "Assets/" + defaultParent + "/" + defaultChild + "/" + defaultSettingsName;
            obj.toolVersion = Pipeline.VERSION;
            SaveRLSettingsObject(obj);
            return obj;
        }

        public static bool TryGetValidObject(string guid, out RLSettingsObject obj)
        {
            obj = null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                return false;

            obj = AssetDatabase.LoadAssetAtPath<RLSettingsObject>(path);
            if (obj == null)
                return false;
            else
                return true;
        }

        public static RLSettingsObject UpdateSettingsPath(RLSettingsObject obj, string guid)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (obj.lastPath != assetPath)
                obj.lastPath = assetPath;
            return obj;
        }

        public static void SaveRLSettingsObject(RLSettingsObject obj)
        {
            string assetPath = AssetDatabase.GetAssetPath(obj);

            if (string.IsNullOrEmpty(assetPath))
            {
                // no corresponding asset exists, create new one
                if (!AssetDatabase.IsValidFolder("Assets/" + defaultParent))
                    AssetDatabase.CreateFolder("Assets", defaultParent);
                if (!AssetDatabase.IsValidFolder("Assets/" + defaultParent + "/" + defaultChild))
                    AssetDatabase.CreateFolder("Assets/" + defaultParent, defaultChild);

                AssetDatabase.CreateAsset(obj, obj.lastPath);
            }
            else
            {
                //Debug.Log("Attempting to write RLSettingsObject: " + obj.lastPath);
                if (assetPath != obj.lastPath)
                {
                    // object has moved - update last path
                    Debug.Log("Asset has moved updating paths from: " + obj.lastPath + " to: " + assetPath);
                    obj.lastPath = assetPath;
                }

                EditorUtility.SetDirty(obj);
#if UNITY_2021_1_OR_NEWER
                AssetDatabase.SaveAssetIfDirty(obj);
#else
                AssetDatabase.SaveAssets();
#endif
            }
        }
    }
}
