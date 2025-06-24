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

        public static void UpdateManagerUpdateCheck()
        {
            //Debug.LogWarning("STARTING ShaderPackageUtil CHECKS");
            //ShaderPackageUtil.GetInstalledPipelineVersion();
            FrameTimer.CreateTimer(10, FrameTimer.initShaderUpdater, ShaderPackageUtil.ImporterWindowInitCallback);
        }

        public static void UpdaterWindowCheckStatus()
        {
            PackageCheckDone -= UpdaterWindowCheckStatusDone;
            PackageCheckDone += UpdaterWindowCheckStatusDone;
            FrameTimer.CreateTimer(10, FrameTimer.initShaderUpdater, ShaderPackageUtil.ImporterWindowInitCallback);
        }

        public static void UpdaterWindowCheckStatusDone(object sender, EventArgs e)
        {
            PackageCheckDone -= UpdaterWindowCheckStatusDone;
            DetermineShaderAction();
            // some abbreviated determination to open the action pane if needed
            bool force = UpdateManager.determinedShaderAction.DeterminedAction == ShaderPackageUtil.DeterminedShaderAction.UninstallReinstall_force || UpdateManager.determinedShaderAction.DeterminedAction == ShaderPackageUtil.DeterminedShaderAction.Error;
            bool optional = UpdateManager.determinedShaderAction.DeterminedAction == ShaderPackageUtil.DeterminedShaderAction.UninstallReinstall_optional;
            bool shaderActionRequired = force || optional;
            if (ShaderPackageUpdater.Instance != null)
            {
                ShaderPackageUpdater.Instance.shaderActionRequired = shaderActionRequired;
            }
        }

        public static void ImporterWindowInitCallback(object obj, FrameTimerArgs args)
        {
            if (args.ident == FrameTimer.initShaderUpdater)
            {
                ShaderPackageUtilInit(true);
                FrameTimer.OnFrameTimerComplete -= ImporterWindowInitCallback;
            }
        }

        public static void ShaderPackageUtilInit(bool callback = false)
        {
            UpdateManager.determinedShaderAction = new ShaderActionRules();
            ImporterWindow.SetGeneralSettings(RLSettings.FindRLSettingsObject(), false);
            if (ImporterWindow.generalSettings != null)
            {
                if (ImporterWindow.generalSettings.performPostInstallationCheck) ShaderPackageUtil.PostImportPackageItemCompare();
                ImporterWindow.generalSettings.performPostInstallationCheck = false;
            }
            OnPipelineDetermined -= PipelineDetermined;
            OnPipelineDetermined += PipelineDetermined;
            GetInstalledPipelineVersion();
            
            if (callback) FrameTimer.OnFrameTimerComplete -= ShaderPackageUtil.ImporterWindowInitCallback;

            if (InitCompleted != null)
                InitCompleted.Invoke(null, null);
        }

        // async wait for UpdateManager to be updated
        public static event EventHandler OnPipelineDetermined;
        public static event EventHandler PackageCheckDone;
        public static event EventHandler InitCompleted;
        
        public static void PipelineDetermined(object sender, EventArgs e)
        {
            if (ImporterWindow.GeneralSettings != null)
            {
                UpdateManager.availablePackages = BuildPackageMap();
                //Debug.LogWarning("Running ValidateInstalledShader");
                ValidateInstalledShader();
            }
            if (PackageCheckDone != null)
                PackageCheckDone.Invoke(null, null);

            OnPipelineDetermined -= PipelineDetermined;
        }
                
        public static void ValidateInstalledShader()
        {
            string[] manifestGUIDS = AssetDatabase.FindAssets("_RL_shadermanifest", new string[] { "Assets" });
            string guid = string.Empty;

            UpdateManager.currentPackageManifest = GetCurrentShaderForPipeline();
            UpdateManager.missingShaderPackageItems = new List<ShaderPackageItem>();

            // consider simplest cases 'Nothing installed' 'One Shader Installed' 'Multiple shaders installed'
            // nothing and multiple are immediately returned - a single shader install is further examined
            if (manifestGUIDS.Length == 0)
            {
                UpdateManager.installedPackageStatus = InstalledPackageStatus.Absent;
                UpdateManager.shaderPackageValid = PackageVailidity.Absent;
                return; // action rule: Status: Absent  Vailidity: Absent
            }
            else if (manifestGUIDS.Length == 1)
            {
                guid = manifestGUIDS[0];
            }
            else if (manifestGUIDS.Length > 1)
            {
                UpdateManager.installedPackageStatus = InstalledPackageStatus.Multiple;
                UpdateManager.shaderPackageValid = PackageVailidity.Invalid;
                return; // action rule: Status: Multiple  Vailidity: Invalid
            }
            
            // examination of single installed shader
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            ShaderPackageManifest shaderPackageManifest = ReadJson(assetPath);
            if (shaderPackageManifest != null)
            {
                shaderPackageManifest.ManifestPath = assetPath;
                UpdateManager.installedShaderPipelineVersion = shaderPackageManifest.Pipeline;
                UpdateManager.installedShaderVersion = new Version(shaderPackageManifest.Version);
                // check shader is for the currently active pipeline
                if (shaderPackageManifest.Pipeline != UpdateManager.activePipelineVersion)
                {
                    UpdateManager.installedPackageStatus = InstalledPackageStatus.Mismatch;
                    UpdateManager.shaderPackageValid = PackageVailidity.Invalid;
                    return; // action rule: Status: Mismatch  Vailidity: Invalid
                }
            }
            else // check shader is real and is for the currently active pipeline
            {
                UpdateManager.installedPackageStatus = InstalledPackageStatus.Absent;
                UpdateManager.shaderPackageValid = PackageVailidity.Absent;
                return; // action rule: Status: Absent  Vailidity: Absent
            }
            // shader is for the correct pipeline
            // check the integrity of the installed shader
            shaderPackageManifest.ManifestPath = assetPath;
            //Debug.LogWarning("ValidateInstalledShader");
            UpdateManager.installedShaderPipelineVersion = shaderPackageManifest.Pipeline;
            UpdateManager.installedShaderVersion = new Version(shaderPackageManifest.Version);

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
                    UpdateManager.missingShaderPackageItems.Add(item);
                }
            }

            int invalidItems = shaderPackageManifest.Items.FindAll(x => x.Validated == false).Count();
            if (invalidItems == 0 && UpdateManager.missingShaderPackageItems.Count == 0)
            {
                // no missing or invalid items -- determine whether an upgrade is available
                UpdateManager.shaderPackageValid = PackageVailidity.Valid;

                if (UpdateManager.installedShaderPipelineVersion == UpdateManager.activePipelineVersion)
                {
                    // compare current release version with installed version
                    Version maxVersion = UpdateManager.currentPackageManifest.Version.ToVersion();
                    if (UpdateManager.installedShaderVersion == maxVersion)
                        UpdateManager.installedPackageStatus = InstalledPackageStatus.Current; // action rule: Status: Current  Vailidity: Valid
                    else if (UpdateManager.installedShaderVersion < maxVersion)
                        UpdateManager.installedPackageStatus = InstalledPackageStatus.Upgradeable; // action rule: Status: Upgradeable  Vailidity: Valid
                    else if (UpdateManager.installedShaderVersion > maxVersion)
                        UpdateManager.installedPackageStatus = InstalledPackageStatus.VersionTooHigh; // action rule: Status: VersionTooHigh  Vailidity: Valid
                }
                else // mismatch between installed and active shader pipeline version
                {
                    UpdateManager.installedPackageStatus = InstalledPackageStatus.Mismatch;
                    return; // action rule: Status: Mismatch  Vailidity: Valid
                }
            }
            else
            {
                // shader has missing files
                UpdateManager.installedPackageStatus = InstalledPackageStatus.MissingFiles;
                UpdateManager.shaderPackageValid = PackageVailidity.Invalid;
                return;  // action rule: Status: MissingFiles  Vailidity: Invalid
            }

            // required rules summary (the only state combinations that can be returned NB: versioning is only examined when the package is valid):
            // action rule: Status: Absent  Vailidity: Absent
            // action rule: Status: Multiple  Vailidity: Invalid
            // action rule: Status: Current  Vailidity: Valid
            // action rule: Status: Upgradeable  Vailidity: Valid
            // action rule: Status: VersionTooHigh  Vailidity: Valid
            // action rule: Status: Mismatch  Vailidity: Valid
            // action rule: Status: Mismatch  Vailidity: Invalid
            // action rule: Status: MissingFiles  Vailidity: Invalid
        }

        public static ShaderPackageManifest GetCurrentShaderForPipeline()
        {
            if (UpdateManager.availablePackages != null)
            {
                List<ShaderPackageManifest> applicablePackages = UpdateManager.availablePackages.FindAll(x => x.Pipeline == UpdateManager.activePipelineVersion);

                if (applicablePackages.Count > 0)
                {
                    //Debug.LogWarning("Found: " + applicablePackages.Count + " packages for this pipeline");
                    // determine the max available version
                    applicablePackages.Sort((a, b) => b.Version.ToVersion().CompareTo(a.Version.ToVersion()));  // descending sort

                    //foreach (ShaderPackageManifest pkg in applicablePackages)
                    //{
                    //    Debug.Log(pkg.FileName + " " + pkg.Version);
                    //}

                    //Debug.Log("Maximum available package version for this pipeline: " + applicablePackages[0].Version);
                    return applicablePackages[0];  // set the current release for the pipeline -- this is the default to be installed
                }
                else
                {
                    Debug.LogWarning("No shader packages available to install for this pipeline");
                    // no shader packages for the current pipeline are available
                    // will become important after Unity 6000 introduction of 'global pipeline'  when older tool versions are used.
                    UpdateManager.installedPackageStatus = InstalledPackageStatus.NoPackageAvailable;
                    return null;
                }
            }
            return null;
        }

        public static void DetermineShaderAction()
        {
            Func<DeterminedShaderAction, InstalledPackageStatus, PackageVailidity, string, ShaderActionRules> ActionRule = (action, status, validity, text) => new ShaderActionRules(action, status, validity, text);

            // result cases
            string multiple = "Multiple shader packages detected. [Force] Uninstall all then install applicable package.";
            string mismatch = "Active pipeline doesnt match installed shader package. [Force] Uninstall then install applicable package.";
            string normalUpgrade = "Shader package can be upgraded. [Offer] install newer package.";
            string normalDowngrade = "Shader package is from a higher version of the tool. [Offer] install package version from this distribution.";
            string currentValid = "Current Shader is correctly installed and matches pipeline version";
            string freshInstall = "No shader is currently installed, an appropriate version will be imported.";
            string missingFiles = "Files are missing from the installed shader. Uninstall remaining files and install current shader version.";
            string incompatible = "The currently installed pipeline is incompatible with CC/iC Unity tools.  A minimum version of URP v10 or HDRP v10 is required.  Only the Built-in version is supported in this circumstance.  This will require changing the render pipeline to the built-in version to continue.";

            List<ShaderActionRules> ActionRulesList = new List<ShaderActionRules>
            {
                ActionRule(DeterminedShaderAction.NothingInstalled_Install_force, InstalledPackageStatus.Absent, PackageVailidity.Absent, freshInstall),
                ActionRule(DeterminedShaderAction.Error, InstalledPackageStatus.Multiple, PackageVailidity.Invalid, multiple),
                ActionRule(DeterminedShaderAction.CurrentValid, InstalledPackageStatus.Current, PackageVailidity.Valid, currentValid),
                ActionRule(DeterminedShaderAction.UninstallReinstall_optional, InstalledPackageStatus.Upgradeable, PackageVailidity.Valid, normalUpgrade),
                ActionRule(DeterminedShaderAction.UninstallReinstall_optional, InstalledPackageStatus.VersionTooHigh, PackageVailidity.Valid, normalDowngrade),
                ActionRule(DeterminedShaderAction.UninstallReinstall_force, InstalledPackageStatus.Mismatch, PackageVailidity.Valid, mismatch),
                ActionRule(DeterminedShaderAction.UninstallReinstall_force, InstalledPackageStatus.Mismatch, PackageVailidity.Invalid, mismatch),
                ActionRule(DeterminedShaderAction.UninstallReinstall_force, InstalledPackageStatus.MissingFiles, PackageVailidity.Invalid, missingFiles),
                ActionRule(DeterminedShaderAction.Incompatible, InstalledPackageStatus.Mismatch, PackageVailidity.Invalid, incompatible)
            };

            ShaderActionRules actionobj = null;
            List<ShaderActionRules> packageStatus = null;

            // special case where the installed pipeline version is too low to be supported 
            // resulting in UpdateManager.activePipelineVersion == PipelineVersion.Incompatible
            // do not alllow the update to try anything until the user corrects the situation
            if (UpdateManager.activePipelineVersion == PipelineVersion.Incompatible)
            {
                UpdateManager.determinedShaderAction = ActionRulesList.Find(y => y.DeterminedAction == DeterminedShaderAction.Incompatible);
                // new ShaderActionRules(DeterminedShaderAction.Incompatible, InstalledPackageStatus.Mismatch, PackageVailidity.Invalid, incompatible);
                Debug.LogWarning("Incompatible render pipeline. No shader install/update action could be determined.");
                return;
            }

            packageStatus = ActionRulesList.FindAll(x => x.InstalledPackageStatus == UpdateManager.installedPackageStatus);
            if (UpdateManager.shaderPackageValid != PackageVailidity.Waiting || UpdateManager.shaderPackageValid != PackageVailidity.None)
                actionobj = packageStatus.Find(y => y.PackageVailidity == UpdateManager.shaderPackageValid);

            if (actionobj != null)
            {
                UpdateManager.determinedShaderAction = actionobj;
                //Debug.Log(Application.dataPath + " -- " + actionobj.ResultString);
            }
            else
            {
                // action is null
                Debug.LogWarning("No shader install/update action could be determined.");
            }               
        }

        public class ShaderActionRules
        {
            // DeterminedAction InstalledPackageStatus  PackageVailidity resultString
            public DeterminedShaderAction DeterminedAction;
            public InstalledPackageStatus InstalledPackageStatus;
            public PackageVailidity PackageVailidity;
            public string ResultString;

            public ShaderActionRules()
            {
                DeterminedAction = DeterminedShaderAction.None;
                InstalledPackageStatus = InstalledPackageStatus.None;
                PackageVailidity = PackageVailidity.None;
                ResultString = "Undetermined";
            }

            public ShaderActionRules(DeterminedShaderAction determinedAction, InstalledPackageStatus installedPackageStatus, PackageVailidity packageVailidity, string resultString)
            {
                DeterminedAction = determinedAction;
                InstalledPackageStatus = installedPackageStatus;
                PackageVailidity = packageVailidity;
                ResultString = resultString;
            }
        }

        public static ShaderPackageManifest ReadJson(string assetPath)
        {
            //Debug.Log("assetPath: " + assetPath);
            Object sourceObject = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (sourceObject != null)
            {
                TextAsset sourceAsset = sourceObject as TextAsset;
                string jsonText = sourceAsset.text;
                //Debug.Log("jsonText: " + jsonText);
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

                            string selectedPackage = shaderPackages[0]; // default case
                            if (shaderPackages.Length > 1) // error case
                            {
                                Debug.LogWarning("Multiple shader packages detected for: " + packageManifest.SourcePackageName + " ... using the one in Packages/.");

                                foreach (string shaderPackage in shaderPackages)
                                {
                                    if (AssetDatabase.GUIDToAssetPath(shaderPackage).StartsWith("Packages"))
                                    {
                                        selectedPackage = shaderPackage;
                                        break;
                                    }
                                }
                            }
                            
                            string packageAssetPath = AssetDatabase.GUIDToAssetPath(selectedPackage);
                            packageManifest.referenceMainfestPath = manifestAssetPath;
                            packageManifest.referenceShaderPackagePath = packageAssetPath;
                            manifestPackageMap.Add(packageManifest);                            
                        }
                    }
                }
            }
            //Debug.Log("Returning manifestPackageMap containing: " + manifestPackageMap.Count + " entries.");
            return manifestPackageMap;
        }

        // find all currently installed render pipelines
        public static void GetInstalledPipelineVersion()
        {
            UpdateManager.installedShaderVersion = new Version(0, 0, 0);
            UpdateManager.installedShaderPipelineVersion = PipelineVersion.None;
            UpdateManager.installedPackageStatus = InstalledPackageStatus.None;
            UpdateManager.shaderPackageValid = PackageVailidity.Waiting;
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
            //Debug.Log("ShaderPackageUtil.GetInstalledPipelinesAync()");
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
                //Debug.Log("ShaderPackageUtil.WaitForRequestCompleted");
                if (OnPackageListComplete != null)
                    OnPackageListComplete.Invoke(null, null);
                EditorApplication.update -= WaitForRequestCompleted;
            }
        }

        public static void PackageListComplete(object sender, EventArgs args)
        {
            //Debug.Log("ShaderPackageUtil.PackageListComplete()");
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

            UpdateManager.installedPipelines = installed;

            PipelineVersion activePipelineVersion = DetermineActivePipelineVersion(packageList);            
            UpdateManager.activePipelineVersion = activePipelineVersion;


            if (ShaderPackageUpdater.Instance != null)
                ShaderPackageUpdater.Instance.Repaint();

            if (OnPipelineDetermined != null)
                OnPipelineDetermined.Invoke(null, null);
        }

        public static PipelineVersion DetermineActivePipelineVersion(List<UnityEditor.PackageManager.PackageInfo> packageList)
        {
            //TestVersionResponse(); // **** important to run after rule editing ****

            UnityEngine.Rendering.RenderPipeline r = RenderPipelineManager.currentPipeline;
            if (r != null)
            {
                if (r.GetType().ToString().Equals(urpType))
                {
                    string version = packageList.ToList().Find(p => p.name.Equals(urpPackage)).version;
                    UpdateManager.activePipeline = InstalledPipeline.URP;
                    UpdateManager.activeVersion = new Version(version);
                    UpdateManager.activePackageString = urpPackage;
                }
                else if (r.GetType().ToString().Equals(hdrpType))
                {
                    string version = packageList.ToList().Find(p => p.name.Equals(hdrpPackage)).version;
                    UpdateManager.activePipeline = InstalledPipeline.HDRP;
                    UpdateManager.activeVersion = new Version(version);
                    UpdateManager.activePackageString = hdrpPackage;
                }
            }
            else
            {
                UpdateManager.activePipeline = InstalledPipeline.Builtin;
                UpdateManager.activeVersion = new Version(UpdateManager.emptyVersion);
            }

            UpdateManager.platformRestriction = PlatformRestriction.None;

            switch (UpdateManager.activePipeline)
            {
                case InstalledPipeline.Builtin:
                    {
                        return PipelineVersion.BuiltIn;
                    }
                case InstalledPipeline.HDRP:
                    {
                        return GetVersion(InstalledPipeline.HDRP, UpdateManager.activeVersion.Major, UpdateManager.activeVersion.Minor);
                    }
                case InstalledPipeline.URP:
                    {
                        return GetVersion(InstalledPipeline.URP, UpdateManager.activeVersion.Major, UpdateManager.activeVersion.Minor);
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

        public static PipelineVersion GetVersion(InstalledPipeline pipe, int major, int minor)
        {
            Func<int, int, PipelineVersion, VersionLimits> Rule = (min, max, ver) => new VersionLimits(min, max, ver);

            if (pipe == InstalledPipeline.URP)
            {
                // Specific rule to limit WebGL to a maximum of URP12
                if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL && major >= 12)
                {
                    UpdateManager.platformRestriction = PlatformRestriction.URPWebGL;
                    return PipelineVersion.URP12;
                }

                if (major >= 17 && minor >= 1) return PipelineVersion.URP171;

                List<VersionLimits> urpRules = new List<VersionLimits>
                {
                    // Rule(min max, version)
                    
                    Rule(0, 9, PipelineVersion.Incompatible),
                    Rule(10, 11, PipelineVersion.URP10),
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
                    return PipelineVersion.URP10;
                }
            }

            if (pipe == InstalledPipeline.HDRP)
            {
                if (major >= 17 && minor >= 1) return PipelineVersion.HDRP171;

                List<VersionLimits> hdrpRules = new List<VersionLimits>
                {
                    // Rule(min max, version)
                    Rule(0, 9, PipelineVersion.Incompatible),
                    Rule(10, 11, PipelineVersion.HDRP10),
                    Rule(12, 13, PipelineVersion.HDRP12),
                    Rule(14, 16, PipelineVersion.HDRP14),
                    Rule(17, 100, PipelineVersion.HDRP17)
                };

                List<VersionLimits> byMax = hdrpRules.FindAll(z => major <= z.Max);
                VersionLimits result = byMax.Find(z => major >= z.Min);
                if (result != null)
                {
                    return result.Version;
                }
                else
                {
                    return PipelineVersion.HDRP10;
                }
            }
            return PipelineVersion.None;
        }

        public static void TestVersionResponse()
        {
            int minor = 0;
            for (int i = 10; i < 18; i++)
            {
                Debug.Log("Major URP Package Version: " + i + " -- " + GetVersion(InstalledPipeline.URP, i, minor));
            }

            for (int i = 10; i < 18; i++)
            {
                Debug.Log("Major HDRP Package Version: " + i + " -- " + GetVersion(InstalledPipeline.HDRP, i, minor));
            }
        }

        public static void GUIPerformShaderAction(DeterminedShaderAction action)
        {
            bool uninstall = false;
            bool install = false;

            switch (action)
            {
                case ShaderPackageUtil.DeterminedShaderAction.None:
                    {
                        uninstall = false;
                        install = false;
                        break;
                    }
                case ShaderPackageUtil.DeterminedShaderAction.CurrentValid:
                    {
                        uninstall = false;
                        install = false;
                        break;
                    }
                case ShaderPackageUtil.DeterminedShaderAction.Error:
                    {
                        uninstall = true;
                        install = true;
                        break;
                    }
                case ShaderPackageUtil.DeterminedShaderAction.UninstallReinstall_optional:
                    {
                        uninstall = true;
                        install = true;
                        break;
                    }
                case ShaderPackageUtil.DeterminedShaderAction.UninstallReinstall_force:
                    {
                        uninstall = true;
                        install = true;
                        break;
                    }
                case ShaderPackageUtil.DeterminedShaderAction.NothingInstalled_Install_force:
                    {
                        uninstall = false;
                        install = true;
                        break;
                    }
            }

            if (uninstall)
            {
                UnInstallPackage();
            }

            if (install)
            {
                InstallPackage(UpdateManager.currentPackageManifest, false);
            }
        }

        public static void InstallPackage(ShaderPackageManifest shaderPackageManifest, bool interactive = true)
        {
            // The events importPackageCompleted and onImportPackageItemsCompleted are only invoked by
            // a package installation that doesn't cause a domain reload - otherwise the subscription doesnt survive.
            // Since the inclusion of runtime .cs files in the package, a domain reload is caused requiring a manual solution.

            //AssetDatabase.importPackageCompleted -= OnImportPackageCompleted;
            //AssetDatabase.importPackageCompleted += OnImportPackageCompleted;
#if UNITY_2021_3_OR_NEWER
            //AssetDatabase.onImportPackageItemsCompleted -= OnImportPackageItemsCompleted;
            //AssetDatabase.onImportPackageItemsCompleted += OnImportPackageItemsCompleted;
#else

#endif
            if (ImporterWindow.GeneralSettings != null)
            {
                ImporterWindow.GeneralSettings.performPostInstallationCheck = true;
                ImporterWindow.GeneralSettings.postInstallShowUpdateWindow = true;
            }
            AssetDatabase.ImportPackage(shaderPackageManifest.referenceShaderPackagePath, interactive);
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

        public static void PostImportPackageItemCompare()
        {
            //Debug.Log("Performing post installation shader checks...");
            string guid = GetLatestManifestGUID();
            if (guid == string.Empty) return;

            string manifestAssetPath = AssetDatabase.GUIDToAssetPath(guid);
            string fullManifestPath = manifestAssetPath.UnityAssetPathToFullPath();
            ShaderPackageManifest manifest = ReadJson(manifestAssetPath);
            if (manifest != null)
            {
                //Debug.Log("Package installed from: " + manifest.SourcePackageName);
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

            UpdateManager.updateMessage = "Shader package: " + manifest.Pipeline.ToString() + " " + manifest.Version.ToString() + " has been imported.";
        }

        public static void UnInstallPackage()
        {            
            string manifestLabel = "_RL_shadermanifest";
            string[] searchLoc = new string[] { "Assets" };
            string[] mainifestGuids = AssetDatabase.FindAssets(manifestLabel, searchLoc);

            if (mainifestGuids.Length == 0)
            {
                //Debug.LogWarning("No shader packages have been found!!");
            }

            if (mainifestGuids.Length > 1)
            {
                Debug.LogWarning("Multiple installed shader packages have been found!! - uninstalling ALL");
            }

            foreach (string guid in mainifestGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                string[] split = assetPath.Split('_');
                Debug.Log("Trying to uninstall shader package:  Pipeline:" + split[split.Length -3] + " Version: " + split[split.Length - 4]);
                
                if (TryUnInstallPackage(guid))
                {
                    Debug.Log("Package uninstalled.");
                }
                else
                {
                    Debug.Log("Package could not be fully uninstalled.");
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
                            //check file is on disk
                            string fullPath = deletePath.UnityAssetPathToFullPath();
                            if (File.Exists(fullPath))
                            {
                                deleteList.Add(deletePath);
                                //Debug.Log("Adding " + deletePath + " to deleteList");
                            }
                            else
                            {
                                Debug.Log("Shader file delete: " + deletePath + " not found on disk ... skipping");
                            }

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
                    //Debug.Log("No Folders");
                }

                manifest = string.Empty;
            }
            else { return false; }

            // delete all paths in deleteList 
            List<string> failedPaths = new List<string>();
            bool hasFailedPaths = false;
            deleteList.Add(AssetDatabase.GUIDToAssetPath(guid));
            //bool existingFilesDeleted = false;
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
                    Debug.Log(failedPaths.Count + " paths failed to delete (usually due to missing files).");
                    //foreach (string path in failedPaths)
                    //{
                    //    Debug.LogError(path + " ...failed to delete.");
                    //}
                }
            }

            AssetDatabase.Refresh();

            UpdateManager.installedShaderVersion = new Version(0, 0, 0);
            UpdateManager.installedShaderPipelineVersion = PipelineVersion.None;
            UpdateManager.installedPackageStatus = InstalledPackageStatus.None;

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
            URP10 = 110,
            URP12 = 112,
            URP13 = 113,
            URP14 = 114,
            URP15 = 115,
            URP16 = 116,
            URP17 = 117,
            URP171 = 1171,
            URP172 = 1172,
            URP18 = 118,
            URP19 = 119,
            URP20 = 120,
            URP21 = 121,
            URP22 = 122,
            URP23 = 123,
            URP24 = 124,
            URP25 = 125,
            URP26 = 126,
            HDRP10 = 210,
            HDRP12 = 212,
            HDRP13 = 213,
            HDRP14 = 214,
            HDRP15 = 215,
            HDRP16 = 216,
            HDRP17 = 217,
            HDRP171 = 2171,
            HDRP172 = 2172,
            HDRP18 = 218,
            HDRP19 = 219,
            HDRP20 = 220,
            HDRP21 = 221,
            HDRP22 = 222,
            HDRP23 = 223,
            HDRP24 = 224,
            HDRP25 = 225,
            HDRP26 = 226,
            Incompatible = 999
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
            NoPackageAvailable,
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

        public enum DeterminedShaderAction
        {
            None,
            Error,
            CurrentValid,
            NothingInstalled_Install_force,
            UninstallReinstall_optional,
            UninstallReinstall_force,
            Incompatible
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



