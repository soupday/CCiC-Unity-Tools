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
            Debug.Log("*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/* ImporterWindowInitCallback */*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*");
            if (args.ident == FrameTimer.initShaderUpdater)
            {
                ShaderPackageUtilInit(true);
                FrameTimer.OnFrameTimerComplete -= ImporterWindowInitCallback;
            }
        }

        public static void ShaderPackageUtilInit(bool callback = false)
        {
            Debug.Log("*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/* ShaderPackageUtilInit */*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*/*");
            WindowManager.determinedAction = new ActionRules();
            ImporterWindow.SetGeneralSettings(RLSettings.FindRLSettingsObject(), false);
            GetInstalledPipelineVersion();
            EditorApplication.update -= WaitForPipeline;
            EditorApplication.update += WaitForPipeline;
            if (callback) FrameTimer.OnFrameTimerComplete -= ShaderPackageUtil.ImporterWindowInitCallback;

            if (InitCompleted != null)
                InitCompleted.Invoke(null, null);
        }

        // async wait for windowmanager to be updated
        public static event EventHandler OnPipelineDetermined;
        public static event EventHandler InitCompleted;

        public static void WaitForPipeline()
        {
            OnPipelineDetermined -= PipelineDetermined;
            OnPipelineDetermined += PipelineDetermined;
            if (WindowManager.shaderPackageValid == PackageVailidity.Waiting || WindowManager.shaderPackageValid == PackageVailidity.None)
            {
                return;
            }

            // waiting done
            EditorApplication.update -= WaitForPipeline;
            if (OnPipelineDetermined != null)
                OnPipelineDetermined.Invoke(null, null);
        }

        public static void PipelineDetermined(object sender, EventArgs e)
        {
            if (ImporterWindow.GeneralSettings != null)
            {
                //WindowManager.missingShaderPackageItems = ValidateShaderPackage();
                WindowManager.availablePackages = BuildPackageMap();
                Debug.Log("Running ValidateInstalledShader");
                ValidateInstalledShader();
                DetermineAction();
                ShowUpdateUtilityWindow();
            }
            OnPipelineDetermined -= PipelineDetermined;
        }

        public static void ShowUpdateUtilityWindow()
        {
            if (WindowManager.determinedAction != null)
            {
                //bool error = WindowManager.shaderPackageValid == PackageVailidity.Invalid;
                bool sos = false;
                if (ImporterWindow.GeneralSettings != null) sos = ImporterWindow.GeneralSettings.showOnStartup;
                bool valid = WindowManager.determinedAction.DeterminedAction == DeterminedAction.currentValid;
                bool force = WindowManager.determinedAction.DeterminedAction == DeterminedAction.uninstallReinstall_force || WindowManager.determinedAction.DeterminedAction == DeterminedAction.Error;
                bool optional = WindowManager.determinedAction.DeterminedAction == DeterminedAction.uninstallReinstall_optional;
                bool actionRequired = force || valid || optional;
                bool showWindow = false;
                if (optional) Debug.LogWarning("An optional shader package is available.");
                else if (valid) Debug.LogWarning("Problem with shader installation.");

                if (valid || optional)
                    showWindow = sos;

                if (sos || force)
                    showWindow = true;

                if (showWindow)
                {
                    if (!Application.isPlaying) ShaderPackageUpdater.CreateWindow();
                    if (ShaderPackageUpdater.Instance != null) ShaderPackageUpdater.Instance.actionRequired = actionRequired;
                }
            }
        }


        // call this from the importer window after 10 frames have elapsed to ensure that
        // RenderPipelineManager.currentPipeline holds a value
        /*
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

            if (manifestGUIDS.Length == 0)
            {
                WindowManager.installedPackageStatus = InstalledPackageStatus.Absent;
                WindowManager.shaderPackageValid = PackageVailidity.Absent;
                return missingItemList;
            }

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
        */
        public static void ValidateInstalledShader()
        {
            string[] manifestGUIDS = AssetDatabase.FindAssets("_RL_shadermanifest", new string[] { "Assets" });
            string guid = string.Empty;

            WindowManager.missingShaderPackageItems = new List<ShaderPackageItem>();

            if (manifestGUIDS.Length == 0)
            {
                WindowManager.installedPackageStatus = InstalledPackageStatus.Absent;
                WindowManager.shaderPackageValid = PackageVailidity.Absent;
                return; // action rule: Status: Absent  Vailidity: Absent
            }
            else if (manifestGUIDS.Length == 1)
            {
                guid = manifestGUIDS[0];
            }
            else if (manifestGUIDS.Length > 1)
            {
                WindowManager.installedPackageStatus = InstalledPackageStatus.Multiple;
                WindowManager.shaderPackageValid = PackageVailidity.Invalid;
                return; // action rule: Status: Multiple  Vailidity: Invalid
            }
            Debug.Log("GUID: " + guid);
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
                    relToDataPath = itemPath.Remove(0, 6); // remove "Assets\"
                fullPath = Application.dataPath + relToDataPath;


                if (File.Exists(fullPath))
                {
                    item.Validated = true;
                }

                if (!item.Validated)
                {
                    WindowManager.missingShaderPackageItems.Add(item);
                }
            }

            int invalidItems = shaderPackageManifest.Items.FindAll(x => x.Validated == false).Count();
            if (invalidItems == 0 && WindowManager.missingShaderPackageItems.Count == 0)
            {
                // no missing or invalid items
                WindowManager.shaderPackageValid = PackageVailidity.Valid;

                if (WindowManager.installedShaderPipelineVersion == WindowManager.activePipelineVersion)
                {
                    List<ShaderPackageManifest> applicablePackages = WindowManager.availablePackages.FindAll(x => x.Pipeline == WindowManager.activePipelineVersion);

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
                            WindowManager.installedPackageStatus = InstalledPackageStatus.Current; // action rule: Status: Current  Vailidity: Valid
                        else if (WindowManager.installedShaderVersion < maxVersion)
                            WindowManager.installedPackageStatus = InstalledPackageStatus.Upgradeable; // action rule: Status: Upgradeable  Vailidity: Valid
                        else if (WindowManager.installedShaderVersion > maxVersion)
                            WindowManager.installedPackageStatus = InstalledPackageStatus.VersionTooHigh; // action rule: Status: VersionTooHigh  Vailidity: Valid
                    }
                }
                else // mismatch between installed and active shader pipeline version
                {
                    WindowManager.installedPackageStatus = InstalledPackageStatus.Mismatch;
                    return; // action rule: Status: Mismatch  Vailidity: Valid
                }

            }
            else
            {
                WindowManager.installedPackageStatus = InstalledPackageStatus.MissingFiles;
                WindowManager.shaderPackageValid = PackageVailidity.Invalid;
                return;  // action rule: Status: MissingFiles  Vailidity: Invalid
            }

            // required rules summary (the only state combinations that can be returned NB: versioning is only examined when the package is valid):
            // action rule: Status: Absent  Vailidity: Absent
            // action rule: Status: Multiple  Vailidity: Invalid
            // action rule: Status: Current  Vailidity: Valid
            // action rule: Status: Upgradeable  Vailidity: Valid
            // action rule: Status: VersionTooHigh  Vailidity: Valid
            // action rule: Status: Mismatch  Vailidity: Valid
            // action rule: Status: MissingFiles  Vailidity: Invalid
        }

        public static void DetermineAction()
        {
            Func<DeterminedAction, InstalledPackageStatus, PackageVailidity, string, ActionRules> ActionRule = (action, status, validity, text) => new ActionRules(action, status, validity, text);

            // result cases
            string multiple = "Multiple shader packages detected. [Force] Uninstall all then install applicable package.";
            string mismatch = "Active pipeline doesnt match installed shader package. [Force] Uninstall then install applicable package.";
            string normalUpgrade = "Shader package can be upgraded. [Offer] install newer package.";
            string normalDowngrade = "Shader package is from a higher version of the tool. [Offer] install package version from this distribution.";
            string currentValid = "Current Shader is correctly installed and matches pipeline version";
            string freshInstall = "No shader is currently installed, an appropriate version will be imported.";
            string missingFiles = "Files are missing from the installed shader. Uninstall remaining files and install current shader version.";

            List<ActionRules> ActionRulesList = new List<ActionRules>
            {
                ActionRule(DeterminedAction.uninstallReinstall_force, InstalledPackageStatus.Absent, PackageVailidity.Absent, freshInstall),
                ActionRule(DeterminedAction.Error, InstalledPackageStatus.Multiple, PackageVailidity.Invalid, multiple),
                ActionRule(DeterminedAction.currentValid, InstalledPackageStatus.Current, PackageVailidity.Valid, currentValid),
                ActionRule(DeterminedAction.uninstallReinstall_optional, InstalledPackageStatus.Upgradeable, PackageVailidity.Valid, normalUpgrade),
                ActionRule(DeterminedAction.uninstallReinstall_optional, InstalledPackageStatus.VersionTooHigh, PackageVailidity.Valid, normalDowngrade),
                ActionRule(DeterminedAction.uninstallReinstall_force, InstalledPackageStatus.Mismatch, PackageVailidity.Valid, mismatch),
                ActionRule(DeterminedAction.uninstallReinstall_force, InstalledPackageStatus.MissingFiles, PackageVailidity.Invalid, missingFiles)
            };

            ActionRules actionobj = null;
            List<ActionRules> packageStatus = null;

            packageStatus = ActionRulesList.FindAll(x => x.InstalledPackageStatus == WindowManager.installedPackageStatus);
            if (WindowManager.shaderPackageValid != PackageVailidity.Waiting || WindowManager.shaderPackageValid != PackageVailidity.None)
                actionobj = packageStatus.Find(y => y.PackageVailidity == WindowManager.shaderPackageValid);

            Debug.Log(" ================= ACTION ================= ");
            if (actionobj != null)
            {
                WindowManager.determinedAction = actionobj;
                Debug.Log(Application.dataPath + " **************** " + actionobj.ResultString);
            }
            else
                Debug.Log(" ================= NULL ================= ");
            Debug.Log(" ================= /ACTION ================= ");
        }


        public class ActionRules
        {
            // DeterminedAction InstalledPackageStatus  PackageVailidity resultString
            public DeterminedAction DeterminedAction;
            public InstalledPackageStatus InstalledPackageStatus;
            public PackageVailidity PackageVailidity;
            public string ResultString;

            public ActionRules()
            {
                DeterminedAction = DeterminedAction.None;
                InstalledPackageStatus = InstalledPackageStatus.None;
                PackageVailidity = PackageVailidity.None;
                ResultString = "Undetermined";
            }

            public ActionRules(DeterminedAction determinedAction, InstalledPackageStatus installedPackageStatus, PackageVailidity packageVailidity, string resultString)
            {
                DeterminedAction = determinedAction;
                InstalledPackageStatus = installedPackageStatus;
                PackageVailidity = packageVailidity;
                ResultString = resultString;
            }
        }


        /*
        public static DeterminedAction DetermineAction(out string resultString)
        {
            resultString = string.Empty;

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
            string currentInvalid = "Current Shader is incorrectly installed (but does match current pipleine version";

            switch (WindowManager.installedPackageStatus)
            {
                case (InstalledPackageStatus.Multiple):
                    {
                        resultString = multiple;
                        return DeterminedAction.multiple;
                    }
                case (InstalledPackageStatus.Mismatch):
                    {
                        resultString = mismatch;
                        return DeterminedAction.mismatch;
                    }
                case (InstalledPackageStatus.Upgradeable):
                    {
                        if (WindowManager.shaderPackageValid == PackageVailidity.Valid)
                        {
                            resultString = normalUpgrade;
                            return DeterminedAction.normalUpgrade;
                        }
                        else if (WindowManager.shaderPackageValid == PackageVailidity.Invalid)
                        {
                            resultString = forceUpgrade;
                            return DeterminedAction.forceUpgrade;
                        }
                        break;
                    }
                case (InstalledPackageStatus.VersionTooHigh):
                    {
                        if (WindowManager.shaderPackageValid == PackageVailidity.Valid)
                        {
                            resultString = normalDowngrade;
                            return DeterminedAction.normalDowngrade;
                        }
                        else if (WindowManager.shaderPackageValid == PackageVailidity.Invalid)
                        {
                            resultString = forceDowngrade;
                            return DeterminedAction.forceDowngrade;
                        }
                        break;
                    }
                case (InstalledPackageStatus.Current):
                    {
                        if (WindowManager.shaderPackageValid == PackageVailidity.Valid)
                        {
                            resultString = currentValid;
                            return DeterminedAction.currentValid;
                        }
                        else if (WindowManager.shaderPackageValid == PackageVailidity.Invalid)
                        {
                            resultString = currentInvalid;
                            return DeterminedAction.currentInvalid;
                        }
                        break;
                    }
            }
            return DeterminedAction.None;
        }

        */

        public static ShaderPackageManifest ReadJson(string assetPath)
        {
            Debug.Log("assetPath: " + assetPath);
            Object sourceObject = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (sourceObject != null)
            {
                TextAsset sourceAsset = sourceObject as TextAsset;
                string jsonText = sourceAsset.text;
                Debug.Log("jsonText: " + jsonText);
                return JsonUtility.FromJson<ShaderPackageManifest>(jsonText);
            }
            else
            {
                Debug.LogWarning("JSON ERROR");
                return null;
            }
        }

        // build a list of all the shader packages available for import from the distribution package
        // the distribution should contain .unitypackage files paired with *_RL_referencemanifest.json files
        private static List<ShaderPackageManifest> BuildPackageMap()
        {
            string search = "_RL_referencemanifest";
            // string[] searchLoc = new string[] { "Assets", "Packages/com.soupday.cc3_unity_tools" }; // look in assets too if the distribution is wrongly installed
            // in Unity 2021.3.14f1 ALL the assets on the "Packages/...." path are returned for some reason...
            // omiting the 'search in folders' parameter correctly finds assests matching the search term in both Assets and Packages
            string[] mainifestGuids = AssetDatabase.FindAssets(search);

            List<ShaderPackageManifest> manifestPackageMap = new List<ShaderPackageManifest>();

            foreach (string guid in mainifestGuids)
            {
                string manifestAssetPath = AssetDatabase.GUIDToAssetPath(guid);
                string searchTerm = search + ".json";

                if (manifestAssetPath.iContains(searchTerm))
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
        /*
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
        */
        // find all currently installed render pipelines
        public static void GetInstalledPipelineVersion()
        {
            WindowManager.installedShaderVersion = new Version(0, 0, 0);
            WindowManager.installedShaderPipelineVersion = PipelineVersion.None;
            WindowManager.installedPackageStatus = InstalledPackageStatus.None;
            WindowManager.shaderPackageValid = PackageVailidity.Waiting;
            //GetInstalledPipelinesAync();
#if UNITY_2021_3_OR_NEWER
            GetInstalledPipelinesDirectly();
#else
            GetInstalledPipelinesAync();
#endif
        }

#if UNITY_2021_3_OR_NEWER
        public static void GetInstalledPipelinesDirectly()
        {
            UnityEditor.PackageManager.PackageInfo[] packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            DeterminePipelineInfo(packages.ToList());
        }
#endif

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
                if (OnPackageListComplete != null)
                    OnPackageListComplete.Invoke(null, null);
                EditorApplication.update -= WaitForRequestCompleted;
            }
        }

        public static void PackageListComplete(object sender, EventArgs args)
        {
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
            TestVersionResponse();

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

            switch (WindowManager.activePipeline)
            {
                case InstalledPipeline.Builtin:
                    {
                        return PipelineVersion.BuiltIn;
                    }
                case InstalledPipeline.HDRP:
                    {
                        return GetVersion(InstalledPipeline.HDRP, WindowManager.activeVersion.Major);
                    }
                case InstalledPipeline.URP:
                    {
                        return GetVersion(InstalledPipeline.URP, WindowManager.activeVersion.Major);
                    }
                case InstalledPipeline.None:
                    {
                        return PipelineVersion.None;
                    }
            }
            return PipelineVersion.None;
        }

        public class VersionLimits
        {
            public int Min;
            public int Max;
            public PipelineVersion Version;

            public VersionLimits(int min, int max, PipelineVersion version)
            {
                Min = min;
                Max = max;
                Version = version;
            }
        }

        public static PipelineVersion GetVersion(InstalledPipeline pipe, int major)
        {
            Func<int, int, PipelineVersion, VersionLimits> Rule = (min, max, ver) => new VersionLimits(min, max, ver);

            if (pipe == InstalledPipeline.URP)
            {
                // Specific rule to limit WebGL to a maximum of URP12
                if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL && major >= 12)
                {
                    WindowManager.platformRestriction = PlatformRestriction.URPWebGL;
                    return PipelineVersion.URP12;
                }

                List<VersionLimits> urpRules = new List<VersionLimits>
                {
                    // Rule(min max, version)
                    Rule(0, 11, PipelineVersion.URP),
                    Rule(12, 13, PipelineVersion.URP12),
                    Rule(14, 16, PipelineVersion.URP14),
                    Rule(17, 100, PipelineVersion.URP17)
                };

                List<VersionLimits> byMax = urpRules.FindAll(z => major <= z.Max);
                VersionLimits result = byMax.Find(z => major >= z.Min);
                if (result != null)
                {
                    return result.Version;
                }
                else
                {
                    return PipelineVersion.URP;
                }
            }

            if (pipe == InstalledPipeline.HDRP)
            {
                List<VersionLimits> hdrpRules = new List<VersionLimits>
                {
                    // Rule(min max, version)
                    Rule(0, 11, PipelineVersion.HDRP),
                    Rule(12, 100, PipelineVersion.HDRP12)
                };

                List<VersionLimits> byMax = hdrpRules.FindAll(z => major <= z.Max);
                VersionLimits result = byMax.Find(z => major >= z.Min);
                if (result != null)
                {
                    return result.Version;
                }
                else
                {
                    return PipelineVersion.HDRP;
                }
            }
            return PipelineVersion.None;
        }

        public static void TestVersionResponse()
        {
            for (int i = 10; i < 18; i++)
            {
                Debug.Log("Major URP Package Version: " + i + " -- " + GetVersion(InstalledPipeline.URP, i));
            }

            for (int i = 10; i < 18; i++)
            {
                Debug.Log("Major HDRP Package Version: " + i + " -- " + GetVersion(InstalledPipeline.HDRP, i));
            }
        }

        /*
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
        */

        public static void InstallPackage(ShaderPackageManifest shaderPackageManifest)
        {
            AssetDatabase.importPackageCompleted -= OnImportPackageCompleted;
            AssetDatabase.importPackageCompleted += OnImportPackageCompleted;

#if UNITY_2021_3_OR_NEWER
            AssetDatabase.onImportPackageItemsCompleted -= OnImportPackageItemsCompleted;
            AssetDatabase.onImportPackageItemsCompleted += OnImportPackageItemsCompleted;
#else

#endif
            AssetDatabase.ImportPackage(shaderPackageManifest.referenceShaderPackagePath, true);
        }

        private static void OnImportPackageCompleted(string packagename)
        {
            Debug.Log($"Imported package: {packagename}");
#if UNITY_2021_3_OR_NEWER
            // this will be handled by the callback: AssetDatabase.onImportPackageItemsCompleted
#else
            PostImportPackageItemCompare();
#endif
            AssetDatabase.importPackageCompleted -= OnImportPackageCompleted;
        }

        private static void OnImportPackageItemsCompleted(string[] items)
        {
            string mostRecentManifestPath = AssetDatabase.GUIDToAssetPath(GetLatestManifestGUID()).UnityAssetPathToFullPath(); // will flag multiple installation errors
            string manifestLabel = "_RL_shadermanifest.json";
            string manifest = string.Empty;
            string fullManifestPath = string.Empty;
            foreach (string item in items)
            {
                if (item.EndsWith(manifestLabel))
                {
                    manifest = item;
                    fullManifestPath = manifest.UnityAssetPathToFullPath();
                    Debug.Log("Post Install: using shader manifest: " + fullManifestPath);
                    Debug.Log("Post Install: most recently accessed manifest: " + mostRecentManifestPath);
                    break;
                }
            }

            if (!string.IsNullOrEmpty(manifest))
            {
                ShaderPackageManifest shaderPackageManifest = ReadJson(manifest);
                foreach (string item in items)
                {
                    string itemGUID = string.Empty;
#if UNITY_2021_3_OR_NEWER
                    itemGUID = AssetDatabase.AssetPathToGUID(item, AssetPathToGUIDOptions.OnlyExistingAssets);
#else               
                    if (File.Exists(item.UnityAssetPathToFullPath()))
                    {
                        itemGUID = AssetDatabase.AssetPathToGUID(item);
                    }
                    else
                    {
                        Debug.LogError("OnImportPackageItemsCompleted: " + item + " cannot be found on disk.");
                    }
#endif
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
            if (ShaderPackageUpdater.Instance != null) ShaderPackageUpdater.Instance.UpdateGUI();
#if UNITY_2021_3_OR_NEWER
            AssetDatabase.onImportPackageItemsCompleted -= OnImportPackageItemsCompleted;
#endif
        }

        private static string GetLatestManifestGUID()
        {
            string guid = string.Empty;
            string[] manifestGUIDS = AssetDatabase.FindAssets("_RL_shadermanifest", new string[] { "Assets" });

            if (manifestGUIDS.Length > 1)
            {
                // Problem that should never happen ... 
                int c = (manifestGUIDS.Length - 1);
                Debug.LogError("Shader problem: " + c + " shader package" + (c > 1 ? "s " : " ") + (c > 1 ? "are " : "is ") + "already installed");
                Dictionary<string, DateTime> timeStamps = new Dictionary<string, DateTime>();
                foreach (string g in manifestGUIDS)
                {
                    string path = AssetDatabase.GUIDToAssetPath(g);
                    string fullpath = path.UnityAssetPathToFullPath();
                    DateTime accessTime = File.GetLastAccessTime(fullpath);
                    timeStamps.Add(g, accessTime);
                }
                if (timeStamps.Count > 0)
                {
                    guid = timeStamps.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
                }
                else
                {
                    return string.Empty;
                }
            }
            else if (manifestGUIDS.Length > 0)
            {
                guid = manifestGUIDS[0];
            }
            return guid;
        }

        private static void PostImportPackageItemCompare()
        {
            string guid = GetLatestManifestGUID();
            if (guid == string.Empty) return;

            string manifestAssetPath = AssetDatabase.GUIDToAssetPath(guid);
            string fullManifestPath = manifestAssetPath.UnityAssetPathToFullPath();
            ShaderPackageManifest manifest = ReadJson(manifestAssetPath);
            if (manifest != null)
            {
                Debug.Log(manifest.SourcePackageName);
                foreach (var item in manifest.Items)
                {
                    string fullFilename = Path.GetFileName(item.ItemName);
                    string filename = Path.GetFileNameWithoutExtension(item.ItemName);

                    string[] foundGuids = AssetDatabase.FindAssets(filename, new string[] { "Assets" });
                    if (foundGuids.Length > 0)
                    {
                        foreach (string g in foundGuids)
                        {
                            string foundAssetPath = AssetDatabase.GUIDToAssetPath(g);
                            if (Path.GetFileName(foundAssetPath).Equals(fullFilename))
                            {
                                item.InstalledGUID = g;
                                if (item.InstalledGUID == item.GUID) item.Validated = true;
                            }
                        }
                    }
                    else if (foundGuids.Length == 0)
                    {
                        Debug.LogError("PostImportPackageItemCompare: Cannot find " + filename + " in the AssetDatabase.");
                    }
                }
            }
            string jsonString = JsonUtility.ToJson(manifest);
            File.WriteAllText(fullManifestPath, jsonString);
            AssetDatabase.Refresh();
            if (ShaderPackageUpdater.Instance != null) ShaderPackageUpdater.Instance.UpdateGUI();
        }

        public static void UnInstallPackage()
        {
            Debug.Log("UnInstallPackage");
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
                Debug.Log("TryUnInstallPackage " + AssetDatabase.GUIDToAssetPath(guid));
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
            Selection.activeObject = null;
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
#if UNITY_2021_3_OR_NEWER
                Debug.Log("Uninstalling files" + (toTrash ? " to OS trash folder" : "") + " from: " + " Pipeline: " + shaderPackageManifest.Pipeline + " Version: " + shaderPackageManifest.Version + " (" + shaderPackageManifest.FileName + ")");
#else
                Debug.Log("Uninstalling files" + " from: " + " Pipeline: " + shaderPackageManifest.Pipeline + " Version: " + shaderPackageManifest.Version + " (" + shaderPackageManifest.FileName + ")");
#endif
                foreach (ShaderPackageItem thing in shaderPackageManifest.Items)
                {
                    string deleteGUID = string.Empty;

                    if (thing.InstalledGUID == null || thing.Validated == false)
                    {
                        // installedguid is null then the post install process wasnt conducted
                        // only happens with a manual install 
                        // find item from original GUID in the manifest
                        string testObjectName = Path.GetFileName(AssetDatabase.GUIDToAssetPath(thing.GUID));
                        string originalObjectName = Path.GetFileName(thing.ItemName);
                        if (testObjectName.iEquals(originalObjectName))
                        {
                            deleteGUID = thing.GUID;
                        }
                        else // original GUID points to a file with incorrect name
                        {
                            // find by filename
                            string searchName = Path.GetFileNameWithoutExtension(thing.ItemName);
                            string[] folders = { "Assets" };
                            string[] foundGUIDs = AssetDatabase.FindAssets(searchName, folders);
                            foreach (string g in foundGUIDs)
                            {
                                // ensure filename + extension matches
                                string assetPath = AssetDatabase.GUIDToAssetPath(g);
                                if (File.Exists(assetPath.UnityAssetPathToFullPath()))
                                {
                                    if (Path.GetFileName(assetPath).iEquals(originalObjectName))
                                    {
                                        deleteGUID = g;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        deleteGUID = thing.InstalledGUID;
                    }

                    string deletePath = AssetDatabase.GUIDToAssetPath(deleteGUID);
                    // validate assetpath to submit for deletion
                    if (!AssetDatabase.IsValidFolder(deletePath))
                    {
                        if (deletePath.StartsWith("Assets"))
                        {
                            //Debug.Log("Adding " + deletePath + " to deleteList");
                            deleteList.Add(deletePath);
                        }
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
#if UNITY_2021_3_OR_NEWER
            if (toTrash)
                hasFailedPaths = !AssetDatabase.MoveAssetsToTrash(deleteList.ToArray(), failedPaths);
            else
                hasFailedPaths = !AssetDatabase.DeleteAssets(deleteList.ToArray(), failedPaths);
#else
            // according to the documentation DeleteAssets/MoveAssetsToTrash unsupported in 2020.3
            // & absent from 2019.4 -- use individual DeleteAsset/MoveAssetToTrash            
            foreach (string path in deleteList)
            {
                bool deleted;
                if (toTrash)
                    deleted = AssetDatabase.MoveAssetToTrash(path);
                else
                    deleted = AssetDatabase.DeleteAsset(path);

                if (!deleted)
                {
                    Debug.LogError(path + " did not uninstall.");
                    failedPaths.Add(path);
                    hasFailedPaths = true;
                }
            }
#endif

            if (hasFailedPaths)
            {
                if (failedPaths.Count > 0)
                {
                    Debug.LogError(failedPaths.Count + " paths failed to delete.");
                    foreach (string path in failedPaths)
                    {
                        Debug.LogError(path + " ...failed to delete.");
                    }
                }
            }

            AssetDatabase.Refresh();

            WindowManager.installedShaderVersion = new Version(0, 0, 0);
            WindowManager.installedShaderPipelineVersion = PipelineVersion.None;
            WindowManager.installedPackageStatus = InstalledPackageStatus.None;

            if (ShaderPackageUpdater.Instance != null)
                ShaderPackageUpdater.Instance.UpdateGUI();

            return !hasFailedPaths;
        }

        #region ENUM+CLASSES
        // STANDALONE COPY FOR JSON CONSISTENCY IN ShaderDistroPackager -- START
        public enum PipelineVersion
        {
            None = 0,
            BuiltIn = 1,
            URP = 110,
            URP12 = 112,
            URP13 = 113,
            URP14 = 114,
            URP15 = 115,
            URP16 = 116,
            URP17 = 117,
            URP18 = 118,
            URP19 = 119,
            URP20 = 120,
            URP21 = 121,
            URP22 = 122,
            URP23 = 123,
            URP24 = 124,
            URP25 = 125,
            URP26 = 126,
            HDRP = 210,
            HDRP12 = 212,
            HDRP13 = 213,
            HDRP14 = 214,
            HDRP15 = 215,
            HDRP16 = 216,
            HDRP17 = 217,
            HDRP18 = 218,
            HDRP19 = 219,
            HDRP20 = 220,
            HDRP21 = 221,
            HDRP22 = 222,
            HDRP23 = 223,
            HDRP24 = 224,
            HDRP25 = 225,
            HDRP26 = 226
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
            Multiple,  // Treat the presence of multiple shader packages as a serious problem            
            Absent
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
            Finished,
            Absent
        }

        public enum DeterminedAction
        {
            None,
            Error,
            currentValid,
            uninstallReinstall_optional,
            uninstallReinstall_force
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



