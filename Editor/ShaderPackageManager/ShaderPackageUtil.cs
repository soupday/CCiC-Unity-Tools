using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager;
using Object = UnityEngine.Object;

namespace Reallusion.Import
{
    public static class ShaderPackageUtil
    {
        public const string emptyVersion = "0.0.0";
        public const string urpType = "UnityEngine.Rendering.Universal.UniversalRenderPipeline";
        public const string urpPackage = "com.unity.render-pipelines.universal";
        public const string hdrpType = "UnityEngine.Rendering.HighDefinition.HDRenderPipeline";
        public const string hdrpPackage = "com.unity.render-pipelines.high-definition";

        //public static bool isWaiting = false;
        //public static bool hasFinished = false;

        public static void ImporterWindowInitCallback(object obj, FrameTimerArgs args)
        {
            ShaderPackageUtilInit(true);            
        }

        public static void ShaderPackageUtilInit(bool callback = false)
        {
            ImporterWindow.SetGeneralSettings(RLSettings.FindRLSettingsObject(), false);
            GetInstalledPipelineVersion();
            EditorApplication.update -= WaitForPipeline;
            EditorApplication.update += WaitForPipeline;
            if (callback) FrameTimer.OnFrameTimerComplete -= ShaderPackageUtil.ImporterWindowInitCallback;
        }

        // async wait for windowmanager to be updated
        public static event EventHandler OnPipelineDetermined;
        
        public static void WaitForPipeline()
        {
            OnPipelineDetermined -= PipelineDetermined;
            OnPipelineDetermined += PipelineDetermined;
            if (WindowManager.shaderPackageValid == PackageVailidity.Waiting || WindowManager.shaderPackageValid == PackageVailidity.None)
            {
                Debug.Log("Waiting");
                return;
            }

            // waiting done
            EditorApplication.update -= WaitForPipeline;
            OnPipelineDetermined.Invoke(null, null);
        }

        public static void PipelineDetermined(object sender, EventArgs e)
        {
            if (ImporterWindow.GeneralSettings != null)
            {
                ValidateShaderPackage();
                bool error = WindowManager.shaderPackageValid == PackageVailidity.Invalid;
                if (ImporterWindow.GeneralSettings.showOnStartup || error)
                {
                    if (!Application.isPlaying) ShaderPackageUpdater.CreateWindow();
                    if (error) Debug.LogWarning("Problem with shader installation.");
                }
            }
            OnPipelineDetermined -= PipelineDetermined;
        }

        // call this from the importer window after 10 frames have elapsed to ensure that
        // RenderPipelineManager.currentPipeline holds a value
        public static List<ShaderPackageItem> ValidateShaderPackage()
        {            
            WindowManager.availablePackages = BuildPackageMap();
            Debug.Log("Checking Installed Shader Package");

            // find the file containing _RL_shadermanifest.json
            // compare manifest contents to whats on the disk
            string[] manifestGUIDS = AssetDatabase.FindAssets("_RL_shadermanifest", new string[] { "Assets" });
            string guid = string.Empty;

            WindowManager.missingShaderPackageItems = new List<ShaderPackageItem>();
            List<ShaderPackageItem> missingItemList = new List<ShaderPackageItem>();

            if (manifestGUIDS.Length > 1)
            {
                WindowManager.installedPackageStatus = InstalledPackageStatus.Multiple; // Problem
                WindowManager.shaderPackageValid = PackageVailidity.Invalid;
                return missingItemList;
            }

            if (manifestGUIDS.Length > 0)
                guid = manifestGUIDS[0];
            else
            {
                WindowManager.shaderPackageValid = PackageVailidity.Invalid;
                return missingItemList;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            ShaderPackageManifest shaderPackageManifest = ReadJson(assetPath);
            shaderPackageManifest.ManifestPath = assetPath;
            WindowManager.installedShaderPipelineVersion = shaderPackageManifest.Pipeline;
            WindowManager.installedShaderVersion = new Version(shaderPackageManifest.Version);
            foreach (ShaderPackageItem item in shaderPackageManifest.Items)
            {
                item.Validated = false;
                string itemPath = string.Empty;
                string relToDataPath = string.Empty;
                string fullPath = string.Empty;

                itemPath = AssetDatabase.GUIDToAssetPath(string.IsNullOrEmpty(item.InstalledGUID) ? item.GUID : item.InstalledGUID);

                if (itemPath.Length > 6)
                    relToDataPath = itemPath.Remove(0, 6);
                fullPath = Application.dataPath + relToDataPath;

                
                if (File.Exists(fullPath))
                {
                    item.Validated = true;
                }
                
                if (!item.Validated)
                {
                    missingItemList.Add(item);
                }
            }

            Debug.Log(shaderPackageManifest.Items.FindAll(x => x.Validated == false).Count() + " Missing Items. ***");
            Debug.Log(shaderPackageManifest.Items.FindAll(x => x.Validated == true).Count() + " Found Items. *** out of " + shaderPackageManifest.Items.Count);
            Debug.Log("Invalid Items List contains: " + missingItemList.Count + " items.");
            int missingItems = shaderPackageManifest.Items.FindAll(x => x.Validated == false).Count();
            WindowManager.installedPackageStatus = GetPackageStatus(missingItemList);
            if ((WindowManager.installedPackageStatus == InstalledPackageStatus.Current || WindowManager.installedPackageStatus == InstalledPackageStatus.VersionTooHigh) && missingItems == 0)
            {
                // current version or higher with no missing items
                Debug.LogWarning("Shader Package Validated successfully...");
                WindowManager.shaderPackageValid = PackageVailidity.Valid;
                return missingItemList;
                //return true;
            }
            else
            {
                Debug.LogError("Shader Package failed to validate..." + WindowManager.installedPackageStatus.ToString() + " Missing items: " + missingItems);
                WindowManager.shaderPackageValid = PackageVailidity.Invalid;
                return missingItemList;
                //return false;
            }
        }

        public static ShaderPackageManifest ReadJson(string assetPath)
        {
            Object sourceObject = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            TextAsset sourceAsset = sourceObject as TextAsset;
            string jsonText = sourceAsset.text;

            return JsonUtility.FromJson<ShaderPackageManifest>(jsonText);
        }

        // build a list of all the shader packages available for import from the distribution package
        // the distribution should contain .unitypackage files paired with *_RL_referencemanifest.json files
        private static List<ShaderPackageManifest> BuildPackageMap()
        {
            string search = "_RL_referencemanifest";
            //string[] searchLoc = new string[] { "Assets", "Packages/com.soupday.cc3_unity_tools" }; // look in assets too if the distribution is wrongly installed
            // in Unity 2021.3.14f1 ALL the assets on the "Packages/...." path are returned for some reason...
            // omiting the 'search in folders' parameter correctly finds assests matching the search term in both Assets and Packages
            string[] mainifestGuids = AssetDatabase.FindAssets(search);

            List<ShaderPackageManifest> manifestPackageMap = new List<ShaderPackageManifest>();

            foreach (string guid in mainifestGuids)
            {
                string manifestAssetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (manifestAssetPath.Contains(search + ".json", StringComparison.InvariantCultureIgnoreCase))
                {
                    var sourceJsonObject = (Object)AssetDatabase.LoadAssetAtPath(manifestAssetPath, typeof(Object));
                    if (sourceJsonObject != null)
                    {
                        TextAsset sourceJson = null;
                        ShaderPackageManifest packageManifest = null;

                        try
                        {
                            sourceJson = sourceJsonObject as TextAsset;
                        }
                        catch (Exception exception)
                        {
                            Debug.LogWarning(exception);
                        }

                        if (sourceJson != null)
                        {
                            packageManifest = (ShaderPackageManifest)JsonUtility.FromJson(sourceJson.text, typeof(ShaderPackageManifest));
                            string packageSearchTerm = Path.GetFileNameWithoutExtension(packageManifest.SourcePackageName);                            
                            string[] shaderPackages = AssetDatabase.FindAssets(packageSearchTerm);
                                                                                                  
                            if (shaderPackages.Length > 1)
                            {
                                Debug.LogWarning("Multiple shader packages detected for: " + packageManifest.SourcePackageName + " ... Aborting.");
                                return null;
                            }
                            else
                            {
                                string packageAssetPath = AssetDatabase.GUIDToAssetPath(shaderPackages[0]);
                                //Debug.Log("Found: " + packageAssetPath + " for: " + manifestAssetPath);
                                packageManifest.referenceMainfestPath = manifestAssetPath;
                                packageManifest.referenceShaderPackagePath = packageAssetPath;
                                manifestPackageMap.Add(packageManifest);
                            }
                        }
                    }
                }
            }
            Debug.Log("Returning manifestPackageMap containing: " + manifestPackageMap.Count + " entries.");
            return manifestPackageMap;
        }

        public static PipelineVersion DetermineActivePipelineVersion()
        {
            UnityEngine.Rendering.RenderPipeline r = RenderPipelineManager.currentPipeline;
            if (r != null)
            {
                float t = Time.realtimeSinceStartup;
                UnityEditor.PackageManager.PackageInfo[] packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
                float u = Time.realtimeSinceStartup;
                Debug.Log(u - t + " Seconds elapsed for UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()");

                if (r.GetType().ToString().Equals(urpType))
                {
                    string version = packages.ToList().Find(p => p.name.Equals(urpPackage)).version;
                    WindowManager.activePipeline = InstalledPipeline.URP;
                    WindowManager.activeVersion = new Version(version);
                    WindowManager.activePackageString = urpPackage;
                }
                else if (r.GetType().ToString().Equals(hdrpType))
                {
                    string version = packages.ToList().Find(p => p.name.Equals(hdrpPackage)).version;
                    WindowManager.activePipeline = InstalledPipeline.HDRP;
                    WindowManager.activeVersion = new Version(version);
                    WindowManager.activePackageString = hdrpPackage;
                }
            }
            else
            {
                WindowManager.activePipeline = InstalledPipeline.Builtin;
                WindowManager.activeVersion = new Version(WindowManager.emptyVersion);
            }

            //platformMessage = string.Empty;

            switch (WindowManager.activePipeline)
            {
                case InstalledPipeline.Builtin:
                    {
                        return PipelineVersion.BuiltIn;
                    }
                case InstalledPipeline.HDRP:
                    {
                        if (WindowManager.activeVersion.Major < 12)
                            return PipelineVersion.HDRP;
                        else if (WindowManager.activeVersion.Major >= 12 && WindowManager.activeVersion.Major < 15)
                            return PipelineVersion.HDRP12;
                        else if (WindowManager.activeVersion.Major >= 15)
                            return PipelineVersion.HDRP15;
                        else return PipelineVersion.HDRP;
                    }
                case InstalledPipeline.URP:
                    {
                        if (WindowManager.activeVersion.Major < 12)
                            return PipelineVersion.URP;
                        else if (WindowManager.activeVersion.Major >= 12 && WindowManager.activeVersion.Major < 15)
                        {
                            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
                            {
                                //platformMessage = urpPlatformMessage;
                                return PipelineVersion.URP12;
                            }
                            else
                            {
                                return PipelineVersion.URP12;
                            }
                        }
                        else if (WindowManager.activeVersion.Major >= 15)
                        {
                            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
                            {
                                //platformMessage = urpPlatformMessage;
                                return PipelineVersion.URP12;
                            }
                            else
                            {
                                return PipelineVersion.URP15;
                            }
                        }
                        else return PipelineVersion.URP;
                    }
                case InstalledPipeline.None:
                    {
                        return PipelineVersion.None;
                    }
            }
            return PipelineVersion.None;
        }

        // find all currently installed render pipelines
        public static void GetInstalledPipelineVersion()
        {
            WindowManager.shaderPackageValid = PackageVailidity.Waiting;
            GetInstalledPipelinesAync();  // looping  
#if UNITY_2021_3_OR_NEWER
            //GetInstalledPipelinesDirectly();
#else
            GetInstalledPipelinesAync();
#endif
        }

        public static void GetInstalledPipelinesDirectly()
        {
            /*
            List<InstalledPipelines> installed = new List<InstalledPipelines>();
            installed.Add(new InstalledPipelines(InstalledPipeline.Builtin, new Version(emptyVersion), ""));

            UnityEditor.PackageManager.PackageInfo[] packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();

            // find urp
            UnityEditor.PackageManager.PackageInfo urp = packages.ToList().Find(p => p.name.Equals(urpPackage));
            if (urp != null)
            {
                installed.Add(new InstalledPipelines(InstalledPipeline.URP, new Version(urp.version), urpPackage));
            }

            // find hdrp
            UnityEditor.PackageManager.PackageInfo hdrp = packages.ToList().Find(p => p.name.Equals(hdrpPackage));
            if (hdrp != null)
            {
                installed.Add(new InstalledPipelines(InstalledPipeline.HDRP, new Version(hdrp.version), hdrpPackage));
            }

            WindowManager.installedPipelines = installed;
            //return installed;
            */
            UnityEditor.PackageManager.PackageInfo[] packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            DeterminePipelineInfo(packages.ToList());
        }

        // pre UNITY_2021_3 async package listing -- START
        public static event EventHandler OnPackageListComplete;
        private static ListRequest Request;

        public static void GetInstalledPipelinesAync()
        {
            Debug.Log("ShaderPackageUtil.GetInstalledPipelinesAync()");
            Request = Client.List(true, true);  // offline mode and includes depenencies (otherwise wont detect URP)
            OnPackageListComplete -= PackageListComplete;
            OnPackageListComplete += PackageListComplete;
            EditorApplication.update -= WaitForRequestCompleted;
            EditorApplication.update += WaitForRequestCompleted;
        }

        private static void WaitForRequestCompleted()
        {
            if (Request.IsCompleted)// && isWaiting)
            {
                Debug.Log("ShaderPackageUtil.WaitForRequestCompleted");
                OnPackageListComplete.Invoke(null, null);
                EditorApplication.update -= WaitForRequestCompleted;
                //isWaiting = false;
            }
            //else if (Request.IsCompleted && !isWaiting)
            //{
            //    Debug.Log("############### NOT WAITING TERMINATE ###############");
            //    EditorApplication.update -= WaitForRequestCompleted;
            //}
        }

        public static void PackageListComplete(object sender, EventArgs args)
        {
            /*
            List<InstalledPipelines> installed = new List<InstalledPipelines>();

            if (Request.Status == StatusCode.Success)
            {
                installed.Add(new InstalledPipelines(InstalledPipeline.Builtin, new Version(emptyVersion), ""));

                // find urp
                UnityEditor.PackageManager.PackageInfo urp = Request.Result.ToList().Find(p => p.name.Equals(urpPackage));
                if (urp != null)
                {
                    installed.Add(new InstalledPipelines(InstalledPipeline.URP, new Version(urp.version), urpPackage));
                }

                // find hdrp
                UnityEditor.PackageManager.PackageInfo hdrp = Request.Result.ToList().Find(p => p.name.Equals(hdrpPackage));
                if (hdrp != null)
                {
                    installed.Add(new InstalledPipelines(InstalledPipeline.HDRP, new Version(hdrp.version), hdrpPackage));
                }

                WindowManager.installedPipelines = installed;
            }
            else if (Request.Status >= StatusCode.Failure)
            {
                Debug.Log(Request.Error.message);
                WindowManager.installedPipelines = installed;
            }
            */
            Debug.Log("ShaderPackageUtil.PackageListComplete()");
            List<UnityEditor.PackageManager.PackageInfo> packageList = Request.Result.ToList();
            if (packageList != null)
            {
                DeterminePipelineInfo(Request.Result.ToList());
                OnPackageListComplete -= PackageListComplete;
            }
            else
            {
                Debug.LogWarning("ShaderPackageUtil.PackageListComplete() Cannot retrieve installed packages.");
            }
        }
        // pre UNITY_2021_3 async package listing -- END

        // common pipeline determination
        public static void DeterminePipelineInfo(List<UnityEditor.PackageManager.PackageInfo> packageList)
        {
            List<InstalledPipelines> installed = new List<InstalledPipelines>();

            installed.Add(new InstalledPipelines(InstalledPipeline.Builtin, new Version(emptyVersion), ""));

            // find urp
            UnityEditor.PackageManager.PackageInfo urp = packageList.Find(p => p.name.Equals(urpPackage));
            if (urp != null)
            {
                installed.Add(new InstalledPipelines(InstalledPipeline.URP, new Version(urp.version), urpPackage));
            }

            // find hdrp
            UnityEditor.PackageManager.PackageInfo hdrp = packageList.ToList().Find(p => p.name.Equals(hdrpPackage));
            if (hdrp != null)
            {
                installed.Add(new InstalledPipelines(InstalledPipeline.HDRP, new Version(hdrp.version), hdrpPackage));
            }

            WindowManager.installedPipelines = installed;
            WindowManager.activePipelineVersion = DetermineActivePipelineVersion(packageList);
            if (ShaderPackageUpdater.Instance != null)
                ShaderPackageUpdater.Instance.Repaint();

            WindowManager.shaderPackageValid = PackageVailidity.Finished;
        }

        public static PipelineVersion DetermineActivePipelineVersion(List<UnityEditor.PackageManager.PackageInfo> packageList)
        {
            UnityEngine.Rendering.RenderPipeline r = RenderPipelineManager.currentPipeline;
            if (r != null)
            {
                if (r.GetType().ToString().Equals(urpType))
                {
                    string version = packageList.ToList().Find(p => p.name.Equals(urpPackage)).version;
                    WindowManager.activePipeline = InstalledPipeline.URP;
                    WindowManager.activeVersion = new Version(version);
                    WindowManager.activePackageString = urpPackage;
                }
                else if (r.GetType().ToString().Equals(hdrpType))
                {
                    string version = packageList.ToList().Find(p => p.name.Equals(hdrpPackage)).version;
                    WindowManager.activePipeline = InstalledPipeline.HDRP;
                    WindowManager.activeVersion = new Version(version);
                    WindowManager.activePackageString = hdrpPackage;
                }
            }
            else
            {
                WindowManager.activePipeline = InstalledPipeline.Builtin;
                WindowManager.activeVersion = new Version(WindowManager.emptyVersion);
            }

            WindowManager.platformRestriction = PlatformRestriction.None;
            //platformMessage = string.Empty;

            switch (WindowManager.activePipeline)
            {
                case InstalledPipeline.Builtin:
                    {
                        return PipelineVersion.BuiltIn;
                    }
                case InstalledPipeline.HDRP:
                    {
                        if (WindowManager.activeVersion.Major < 12)
                            return PipelineVersion.HDRP;
                        else if (WindowManager.activeVersion.Major >= 12 && WindowManager.activeVersion.Major < 15)
                            return PipelineVersion.HDRP12;
                        else if (WindowManager.activeVersion.Major >= 15)
                            return PipelineVersion.HDRP15;
                        else return PipelineVersion.HDRP;
                    }
                case InstalledPipeline.URP:
                    {
                        if (WindowManager.activeVersion.Major < 12)
                            return PipelineVersion.URP;
                        else if (WindowManager.activeVersion.Major >= 12 && WindowManager.activeVersion.Major < 15)
                        {
                            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
                            {
                                WindowManager.platformRestriction = PlatformRestriction.URPWebGL;
                                //platformMessage = urpPlatformMessage;
                                return PipelineVersion.URP12;
                            }
                            else
                            {
                                return PipelineVersion.URP12;
                            }
                        }
                        else if (WindowManager.activeVersion.Major >= 15)
                        {
                            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
                            {
                                WindowManager.platformRestriction = PlatformRestriction.URPWebGL;
                                //platformMessage = urpPlatformMessage;
                                return PipelineVersion.URP12;
                            }
                            else
                            {
                                return PipelineVersion.URP15;
                            }
                        }
                        else return PipelineVersion.URP;
                    }
                case InstalledPipeline.None:
                    {
                        return PipelineVersion.None;
                    }
            }
            return PipelineVersion.None;
        }


        public static InstalledPackageStatus GetPackageStatus(List<ShaderPackageItem> missingItemList)
        {
            List<ShaderPackageManifest> applicablePackages = WindowManager.availablePackages.FindAll(x => x.Pipeline == WindowManager.activePipelineVersion);

            if (WindowManager.installedShaderPipelineVersion == WindowManager.activePipelineVersion)
            {
                if (applicablePackages.Count > 0)
                {
                    // determine the max available version
                    applicablePackages.Sort((a, b) => b.Version.ToVersion().CompareTo(a.Version.ToVersion()));  // descending sort

                    Debug.Log("VERSION SORTER**********");
                    foreach (ShaderPackageManifest pkg in applicablePackages)
                    {
                        Debug.Log(pkg.FileName + " " + pkg.Version);
                    }
                    Debug.Log("MAX VERSION******* " + applicablePackages[0].Version);

                    Version maxVersion = applicablePackages[0].Version.ToVersion();
                    if (WindowManager.installedShaderVersion == maxVersion)
                    {
                        if (missingItemList.Count == 0)
                            return InstalledPackageStatus.Current;
                        else if (missingItemList.Count > 0)
                            return InstalledPackageStatus.MissingFiles;
                    }
                    else if (WindowManager.installedShaderVersion < maxVersion)
                        return InstalledPackageStatus.Upgradeable;
                    else if (WindowManager.installedShaderVersion > maxVersion)
                        return InstalledPackageStatus.VersionTooHigh;
                }
            }
            else
            {
                return InstalledPackageStatus.Mismatch;
            }
            return InstalledPackageStatus.None;
        }

        public static void InstallPackage(ShaderPackageManifest shaderPackageManifest)
        {
            AssetDatabase.importPackageCompleted -= OnImportPackageCompleted;
            AssetDatabase.importPackageCompleted += OnImportPackageCompleted;

            AssetDatabase.onImportPackageItemsCompleted -= OnImportPackageItemsCompleted;
            AssetDatabase.onImportPackageItemsCompleted += OnImportPackageItemsCompleted;

            //AssetDatabase.ImportPackage(manifestPackageMap[0].referenceShaderPackagePath, true);
            AssetDatabase.ImportPackage(shaderPackageManifest.referenceShaderPackagePath, true);
        }

        private static void OnImportPackageCompleted(string packagename)
        {
            Debug.Log($"Imported package: {packagename}");
            AssetDatabase.importPackageCompleted -= OnImportPackageCompleted;
        }

        private static void OnImportPackageItemsCompleted(string[] items)
        {
            string manifestLabel = "_RL_shadermanifest.json";
            string manifest = string.Empty;
            string fullManifestPath = string.Empty;
            foreach (string item in items)
            {
                //Debug.Log(item);
                if (item.EndsWith(manifestLabel))
                {
                    manifest = item;
                    string datapath = Application.dataPath;
                    //fullManifestPath = datapath.Remove(datapath.Length - 6, 6) + item;
                    //Debug.Log("Post Install TEST -- AssetToFullPath for " + item + "found as: " + item.AssetToFullPath());
                    fullManifestPath = item.UnityAssetPathToFullPath();
                    Debug.Log("Post Install: using shader manifest " + fullManifestPath);
                    break;
                }
            }

            if (!string.IsNullOrEmpty(manifest))
            {
                ShaderPackageManifest shaderPackageManifest = ReadJson(manifest);
                foreach (string item in items)
                {
                    string itemGUID = AssetDatabase.AssetPathToGUID(item, AssetPathToGUIDOptions.OnlyExistingAssets);
                    string fileName = Path.GetFileName(item);
                    ShaderPackageItem it = shaderPackageManifest.Items.Find(x => x.ItemName.EndsWith(fileName));

                    if (it != null)
                    {
                        it.InstalledGUID = itemGUID;
                        if (it.InstalledGUID != it.GUID)
                        {
                            Debug.Log("Post Install: GUID reassigned for: " + it.ItemName + " (Info only)");
                        }
                        else
                        {
                            //Debug.Log(it.ItemName + "  GUID MATCH ********************");
                        }
                    }                    
                }
                Debug.Log("Post Install: Updating manifest: " + fullManifestPath);
                string jsonString = JsonUtility.ToJson(shaderPackageManifest);
                File.WriteAllText(fullManifestPath, jsonString);
                AssetDatabase.Refresh();
            }
            ShaderPackageUpdater.Instance.UpdateGUI();
            AssetDatabase.onImportPackageItemsCompleted -= OnImportPackageItemsCompleted;
        }

        public static void UnInstallPackage()
        {
            string manifestLabel = "_RL_shadermanifest";
            string[] searchLoc = new string[] { "Assets" };
            string[] mainifestGuids = AssetDatabase.FindAssets(manifestLabel, searchLoc);

            if (mainifestGuids.Length == 0)
            {
                Debug.LogWarning("No shader packages have been found!!");
            }

            if (mainifestGuids.Length > 1)
            {
                Debug.LogWarning("Multiple installed shader packages have been found!! - uninstalling ALL");
            }

            foreach (string guid in mainifestGuids)
            {
                if (TryUnInstallPackage(guid))
                {
                    Debug.Log("Package uninstalled");
                }
                else
                {
                    Debug.Log("Package could not be uninstalled");
                }
            }
        }

        private static bool TryUnInstallPackage(string guid, bool toTrash = true)
        {
            Object manifestObject = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid));
            TextAsset manifestText = manifestObject as TextAsset;
            string manifest = string.Empty;

            List<string> folderList = new List<string>();
            List<string> deleteList = new List<string>();

            if (manifestText != null)
            {
                manifest = manifestText.text;
            }
            else { return false; }

            if (!string.IsNullOrEmpty(manifest))
            {

                ShaderPackageManifest shaderPackageManifest = JsonUtility.FromJson<ShaderPackageManifest>(manifest);

                Debug.Log("Uninstalling files" + (toTrash ? " to OS trash folder" : "") + " from: " + " Pipeline: " + shaderPackageManifest.Pipeline + " Version: " + shaderPackageManifest.Version + " (" + shaderPackageManifest.FileName + ")");

                foreach (ShaderPackageItem thing in shaderPackageManifest.Items)
                {
                    string deletePath = AssetDatabase.GUIDToAssetPath(thing.InstalledGUID);
                    if (!AssetDatabase.IsValidFolder(deletePath))
                    {
                        //Debug.Log("Installed GUID: " + thing.InstalledGUID + " path: " + deletePath);
                        if (deletePath.StartsWith("Assets"))
                            deleteList.Add(deletePath);
                    }
                    else
                    {
                        folderList.Add(deletePath);
                    }
                }
                if (folderList.Count > 0)
                {
                    var folderListSortedByDepth = folderList.OrderByDescending(p => p.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar));

                    foreach (string folder in folderListSortedByDepth)
                    {
                        Debug.Log(folder);
                    }
                }
                else
                {
                    Debug.Log("No Folders");
                }

                manifest = string.Empty;
            }
            else { return false; }

            // delete all paths in deleteList 
            List<string> failedPaths = new List<string>();
            bool hasFailedPaths = false;
            deleteList.Add(AssetDatabase.GUIDToAssetPath(guid));

            if (toTrash)
                hasFailedPaths = AssetDatabase.MoveAssetsToTrash(deleteList.ToArray(), failedPaths);
            else
                hasFailedPaths = AssetDatabase.DeleteAssets(deleteList.ToArray(), failedPaths);


            if (hasFailedPaths)
            {
                foreach (string path in failedPaths)
                {
                    Debug.Log(path + " ...failed to delete.");
                }
            }

            // delete the manifest
            //AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            AssetDatabase.Refresh();
            return hasFailedPaths;
        }
        /*
        public static ShaderPackageManifest ReadJson(string assetPath)
        {
            Object sourceObject = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            TextAsset sourceAsset = sourceObject as TextAsset;
            string jsonText = sourceAsset.text;

            return JsonUtility.FromJson<ShaderPackageManifest>(jsonText);
        }
        */

        public static void DetermineAction(out string result)
        {
            result = string.Empty;

            // determine these first
            // shaderPackageValid; // bool        
            // installedShaderStatus; // InstalledPackageStatus

            // result cases
            string multiple = "Multiple shader packages detected. [Force] Uninstall both then install applicable package.";
            string mismatch = "Active pipeline doesnt match installed shader package. [Force] Uninstall then install applicable package.";
            string normalUpgrade = "Shader package can be upgraded. [Offer] install newer package.";
            string forceUpgrade = "Shader package can be upgraded - Current Installation has errors. [Force] install newer package.";
            string normalDowngrade = "Shader package is from a higher version of the tool. [Offer] install package version from this distribution.";
            string forceDowngrade = "Shader package is from a higher version of the tool - Current Installation has errors. [Force] install package version from this distribution.";
            string currentValid = "Current Shader is correctly installed and matches pipeline version";
            string currentInvalid = "Current Sahder is incorrectly installed (but does match current pipleine version";

            switch (WindowManager.installedPackageStatus)
            {
                case (InstalledPackageStatus.Multiple):
                    {
                        result = multiple;
                        break;
                    }
                case (InstalledPackageStatus.Mismatch):
                    {
                        result = mismatch;
                        break;
                    }
                case (InstalledPackageStatus.Upgradeable):
                    {
                        if (WindowManager.shaderPackageValid == PackageVailidity.Valid)
                            result = normalUpgrade;
                        else if (WindowManager.shaderPackageValid == PackageVailidity.Invalid)
                            result = forceUpgrade;
                        break;
                    }
                case (InstalledPackageStatus.VersionTooHigh):
                    {
                        if (WindowManager.shaderPackageValid == PackageVailidity.Valid)
                            result = normalDowngrade;
                        else if (WindowManager.shaderPackageValid == PackageVailidity.Invalid)
                            result = forceDowngrade;
                        break;
                    }
                case (InstalledPackageStatus.Current):
                    {
                        if (WindowManager.shaderPackageValid == PackageVailidity.Valid)
                            result = currentValid;
                        else if (WindowManager.shaderPackageValid == PackageVailidity.Invalid)
                            result = currentInvalid;
                        break;
                    }
            }
        }

        #region ENUM+CLASSES
        // STANDALONE COPY FOR JSON CONSISTENCY IN ShaderDistroPackager -- START
        public enum PipelineVersion
        {
            None = 0,
            BuiltIn = 1,
            URP = 2,
            URP12 = 3,
            URP15 = 4,
            URP16 = 5,
            URP17 = 6,
            URP18 = 7,
            URP19 = 8,
            URP20 = 9,
            URP21 = 10,
            URP22 = 11,
            URP23 = 12,
            URP24 = 13,
            URP25 = 14,
            URP26 = 15,
            HDRP = 16,
            HDRP12 = 17,
            HDRP15 = 18,
            HDRP16 = 19,
            HDRP17 = 20,
            HDRP18 = 21,
            HDRP19 = 22,
            HDRP20 = 23,
            HDRP21 = 24,
            HDRP22 = 25,
            HDRP23 = 26,
            HDRP24 = 27,
            HDRP25 = 28,
            HDRP26 = 29
        }

        [Serializable]
        public class ShaderPackageManifest
        {
            public string FileName;
            public PipelineVersion Pipeline;
            public string Version;
            public string SourcePackageName;
            public bool VersionEdit;
            public string ManifestPath;
            public bool Validated;
            public bool Visible;
            public string referenceMainfestPath;
            public string referenceShaderPackagePath;
            public List<ShaderPackageItem> Items;

            public ShaderPackageManifest(string name, PipelineVersion pipeline, string version)
            {
                FileName = name;
                Pipeline = pipeline;
                Version = version;
                SourcePackageName = string.Empty;
                VersionEdit = false;
                ManifestPath = string.Empty;
                Validated = false;
                Visible = false;
                Items = new List<ShaderPackageItem>();
            }
        }

        [Serializable]
        public class ShaderPackageItem
        {
            public string ItemName;
            public string GUID;
            public string InstalledGUID;
            public bool Validated;

            public ShaderPackageItem(string itemName, string gUID)
            {
                ItemName = itemName;
                GUID = gUID;
                InstalledGUID = string.Empty;
                Validated = false;
            }
        }
        // STANDALONE COPY FOR JSON CONSISTENCY IN ShaderDistroPackager -- END

        // simple class for GUI
        public class InstalledPipelines
        {
            public InstalledPipeline InstalledPipeline;
            public Version Version;
            public string PackageString;

            public InstalledPipelines(InstalledPipeline installedPipeline, Version version, string packageString)
            {
                InstalledPipeline = installedPipeline;
                Version = version;
                PackageString = packageString;
            }
        }

        public enum InstalledPipeline
        {
            None,
            Builtin,
            URP,
            HDRP
        }
        public enum InstalledPackageStatus
        {
            None,
            Current,
            Upgradeable,
            VersionTooHigh,  // deal with package from a higher distro release
            MissingFiles,
            Mismatch,
            Multiple  // Treat the presence of multiple shader packages as a serious problem
        }

        public enum PlatformRestriction
        {
            None,
            URPWebGL
        }

        public enum PackageVailidity
        {
            None,
            Valid,
            Invalid,
            Waiting,
            Finished
        }

        #endregion ENUM+CLASSES
    }

    #region STRING EXTENSION
    public static class StringToVersionExtension
    {
        public static Version ToVersion(this string str)
        {
            return new Version(str);
        }
    }

    public static class StringAssetPathToFullPath
    {
        public static string UnityAssetPathToFullPath(this string str)
        {
            string datapath = Application.dataPath;
            return datapath.Remove(datapath.Length - 6, 6) + str;
        }
    }

    public static class StringFullPathToAssetPath
    {
        public static string FullPathToUnityAssetPath(this string str)
        {
            return str.Remove(0, Application.dataPath.Length - 6).Replace("\\", "/");
        }
    }
    #endregion STRING EXTENSION

}



