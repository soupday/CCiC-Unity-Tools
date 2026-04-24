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

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using UnityEngine;
using Debug = UnityEngine.Debug;
using FileMode = System.IO.FileMode;

namespace Reallusion.Import
{
    public static class RLToolUpdateUtil
    {
        public const string gitHubReleaseUrl = "https://api.github.com/repos/soupday/ccic_unity_tools_all/releases";
        public const string gitHubTagName = "tag_name";
        public const string gitHubHtmlUrl = "html_url";
        public const string name = "name";
        public const string draft = "draft";
        public const string preRelease = "prerelease";
        public const string gitHubPublishedAt = "published_at";
        public const string gitHubBody = "body";

        public const string gitHubPluginReleaseUrl = "https://api.github.com/repos/soupday/CCIC-Unity-Pipeline-Plugin/releases";
        public const string gitHubPluginDlBaseUrl = "https://github.com/soupday/CCIC-Unity-Pipeline-Plugin/archive/refs/tags";
        public const string pluginFolder = "Unity Pipeline Plugin";
        public const string pluginTargetParentFolder = "OpenPlugin";
        public const string pluginProjectParentFolder = "Plugin";
        public const string pluginFileName = "tmpFile.zip";
        public const string pluginInstaller = "Install.bat";
        public const string gitHubPluginTagName = "tag_name";
        public const string gitHubPluginHtmlUrl = "html_url";
        public const string gitHubPluginPublishedAt = "published_at";
        public const string gitHubPluginBody = "body";

        public static event EventHandler HttpVersionChecked;
        public static event EventHandler PluginHttpVersionChecked;

        public static void UpdateManagerUpdateCheck()
        {
            //Util.LogWarn("STARTING RLToolUpdateUtil CHECKS");
            InitUpdateCheck();
        }

        public static void UpdaterWindowCheckForUpdates()
        {
            HttpVersionChecked -= UpdaterWindowCheckForUpdatesDone;
            HttpVersionChecked += UpdaterWindowCheckForUpdatesDone;
            InitUpdateCheck();
        }

        public static void UpdaterWindowCheckForUpdatesDone(object sender, EventArgs e)
        {

            HttpVersionChecked -= UpdaterWindowCheckForUpdatesDone;
        }

        public static void InitUpdateCheck()
        {
            RLSettingsObject currentSettings = (ImporterWindow.GeneralSettings == null) ? RLSettings.FindRLSettingsObject() : ImporterWindow.GeneralSettings;
            if (currentSettings != null)
            {
                if (currentSettings.checkForUpdates)
                {
                    TimeSpan checkInterval = new TimeSpan(0, 0, 5, 0, 0);//TimeSpan(0, 0, 5, 0, 0);
                    DateTime now = DateTime.Now.ToLocalTime();

                    long univ = currentSettings.lastUpdateCheck;
                    DateTime last = new DateTime(univ);

                    if (TimeCheck(univ, checkInterval))
                    {
                        Util.LogInfo("Checking GitHub for 'CC/iC Unity Tools' update.");
                        currentSettings.lastUpdateCheck = now.Ticks;
                        ImporterWindow.SetGeneralSettings(currentSettings, true);
                        GitHubHttpVersionCheck();
                    }
                    else
                    {
                        if (currentSettings.updateAvailable)
                        {
                            UpdateManager.determinedSoftwareAction = DeterminedSoftwareAction.Software_update_available;
                            Util.LogWarn("Settings object shows update availabe.");
                        }
                        if (HttpVersionChecked != null)
                            HttpVersionChecked.Invoke(null, null);
                        //Debug.Log("TIME NOT ELAPSED " + last.Ticks + "    now: " + now.Ticks + "  last: " + last + "  now: " + now);
                    }
                }
                else
                {
                    // not checking http for updates - but invoke event to complete the init process
                    if (HttpVersionChecked != null)
                        HttpVersionChecked.Invoke(null, null);
                }
            }
        }

        public static void UpdateManagerPluginUpdateCheck()
        {
            //Debug.LogWarning("STARTING RLToolUpdateUtil CHECKS");
            InitPluginUpdateCheck();
        }

        // button function
        public static void UpdaterWindowCheckForPluginUpdates()
        {
            InitPluginUpdateCheck();
        }


        public static void InitPluginUpdateCheck()
        {
            RLSettingsObject currentSettings = (ImporterWindow.GeneralSettings == null) ? RLSettings.FindRLSettingsObject() : ImporterWindow.GeneralSettings;
            if (currentSettings != null)
            {
                if (currentSettings.checkForUpdates)
                {
                    TimeSpan checkInterval = new TimeSpan(0, 0, 5, 0, 0);//TimeSpan(0, 0, 5, 0, 0);
                    DateTime now = DateTime.Now.ToLocalTime();

                    long univ = currentSettings.lastPluginUpdateCheck;
                    DateTime last = new DateTime(univ);

                    if (TimeCheck(univ, checkInterval))
                    {
                        Debug.Log("Checking GitHub for 'Unity Pipeline Plugin' update.");
                        currentSettings.lastPluginUpdateCheck = now.Ticks;
                        ImporterWindow.SetGeneralSettings(currentSettings, true);
                        GitHubPluginHttpVersionCheck();
                    }
                    else
                    {
                        /*
                        if (currentSettings.updateAvailable)
                        {
                            UpdateManager.determinedSoftwareAction = DeterminedSoftwareAction.Software_update_available;
                            Debug.LogWarning("Settings object shows update availabe.");
                        }
                        */
                        if (PluginHttpVersionChecked != null)
                            PluginHttpVersionChecked.Invoke(null, null);
                        //Debug.Log("TIME NOT ELAPSED " + last.Ticks + "    now: " + now.Ticks + "  last: " + last + "  now: " + now);
                    }
                }
                else
                {
                    // not checking http for updates - but invoke event to complete the init process
                    if (PluginHttpVersionChecked != null)
                        PluginHttpVersionChecked.Invoke(null, null);
                }
            }
        }

        public static bool TimeCheck(long timeStamp, TimeSpan time)
        {
            DateTime now = DateTime.Now.ToLocalTime();
            DateTime last = new DateTime(timeStamp);

            if (last + time <= now)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        //[SerializeField]
        //public static List<JsonFragment> fullJsonFragment;

        public static async void GitHubHttpVersionCheck()
        {
            HttpVersionChecked -= OnHttpVersionChecked;
            HttpVersionChecked += OnHttpVersionChecked;

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; AcmeInc/1.0)");

            string releaseJson = string.Empty;
            try
            {
                releaseJson = await httpClient.GetStringAsync(gitHubReleaseUrl);
            }
            catch (Exception ex)
            {
                Util.LogWarn("Error accessing Github to check for new 'CC/iC Unity Tools' version. Error: " + ex);
            }

            RLSettingsObject currentSettings = ImporterWindow.GeneralSettings;
            if (!string.IsNullOrEmpty(releaseJson))
            {
                //Debug.Log(releaseJson.Substring(0, 100));
                List<JsonFragment> fragmentList = GetFragmentList<JsonFragment>(releaseJson);
                //fullJsonFragment = fragmentList;
                if (fragmentList != null && fragmentList.Count > 0)
                {
                    JsonFragment fragment = fragmentList[0];
                    if (ImporterWindow.GeneralSettings != null)
                    {
                        currentSettings = ImporterWindow.GeneralSettings;
                        if (fragment.TagName != null)
                            currentSettings.jsonTagName = fragment.TagName;
                        if (fragment.HtmlUrl != null)
                            currentSettings.jsonHtmlUrl = fragment.HtmlUrl;
                        if (fragment.PublishedAt != null)
                            currentSettings.jsonPublishedAt = fragment.PublishedAt;
                        if (fragment.Body != null)
                            currentSettings.jsonBodyLines = LineSplit(fragment.Body);

                        Version gitHubLatestVersion = TagToVersion(fragment.TagName);
                        Version installed = TagToVersion(Pipeline.VERSION);
                        if (gitHubLatestVersion > installed)
                        {
                            Util.LogWarn("A newer version of CC/iC Unity Tools is available on GitHub. Current ver: " + installed.ToString() + " Latest ver: " + gitHubLatestVersion.ToString());

                            currentSettings.updateAvailable = true;
                            UpdateManager.determinedSoftwareAction = DeterminedSoftwareAction.Software_update_available;
                        }
                        currentSettings.fullJsonFragment = releaseJson;
                        ImporterWindow.SetGeneralSettings(currentSettings, true);
                    }
                }
                else
                {
                    Util.LogWarn("Cannot parse JSON release data from GitHub - aborting version check.");

                    WriteDummyReleaseInfo(currentSettings);
                }

                // Version gitHubLatestVersion = TagToVersion(jsonTagName);
                // TryParseISO8601toDateTime(jsonPublishedAt, out DateTime gitHubPublishedDateTime);

                if (HttpVersionChecked != null)
                    HttpVersionChecked.Invoke(null, null);
            }
            else
            {
                // cant find a release json from github's api
                Util.LogWarn("Cannot find a release JSON from GitHub - aborting version check.");

                WriteDummyReleaseInfo(currentSettings);

                if (HttpVersionChecked != null)
                    HttpVersionChecked.Invoke(null, null);
            }
        }

        public static async void GitHubPluginHttpVersionCheck()
        {
            PluginHttpVersionChecked -= OnPluginHttpVersionChecked;
            PluginHttpVersionChecked += OnPluginHttpVersionChecked;

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; AcmeInc/1.0)");

            string releaseJson = string.Empty;
            try
            {
                releaseJson = await httpClient.GetStringAsync(gitHubPluginReleaseUrl);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Error accessing Github to check for new 'CCIC Unity Pipeline Plugin' version. Error: " + ex);
            }

            RLSettingsObject currentSettings = ImporterWindow.GeneralSettings;
            if (!string.IsNullOrEmpty(releaseJson))
            {
                List<JsonFragment> fragmentList = GetFragmentList<JsonFragment>(releaseJson);
                if (fragmentList != null && fragmentList.Count > 0)
                {
                    JsonFragment fragment = fragmentList[0];
                    if (ImporterWindow.GeneralSettings != null)
                    {
                        currentSettings = ImporterWindow.GeneralSettings;
                        if (fragment.TagName != null)
                            currentSettings.jsonPluginTagName = fragment.TagName;
                        if (fragment.HtmlUrl != null)
                            currentSettings.jsonPluginHtmlUrl = fragment.HtmlUrl;
                        if (fragment.PublishedAt != null)
                            currentSettings.jsonPluginPublishedAt = fragment.PublishedAt;
                        if (fragment.Body != null)
                            currentSettings.jsonPluginBodyLines = LineSplit(fragment.Body);

                        currentSettings.fullJsonPluginFragment = releaseJson;
                        ImporterWindow.SetGeneralSettings(currentSettings, true);
                    }
                    if (PluginHttpVersionChecked != null)
                        PluginHttpVersionChecked.Invoke(null, null);
                }
                else
                {
                    Debug.LogWarning("Cannot parse JSON release data from GitHub - aborting version check.");
                    //WriteDummyReleaseInfo(currentSettings);

                    if (PluginHttpVersionChecked != null)
                        PluginHttpVersionChecked.Invoke(null, null);
                }
            }
        }
        /*
                public static async void GitHubFetchLatestPipeline()
                {
                    string dataPath = Application.dataPath;
                    string projectRoot = Path.GetDirectoryName(dataPath);
                    string projectParentPath = Path.Combine(projectRoot, pluginProjectParentFolder);
                    if (!Directory.Exists(projectParentPath))
                        Directory.CreateDirectory(projectParentPath);

                    string extractedPath = Path.Combine(projectParentPath, pluginFolder);
                    if (Directory.Exists(extractedPath))
                        Directory.Delete(extractedPath, true);



                }
        */

        public static async void InstallPlugin()
        {
            Debug.Log("Installing Plugin");

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; AcmeInc/1.0)");

            RLSettingsObject currentSettings = ImporterWindow.GeneralSettings;
            List<JsonFragment> fragmentList = GetFragmentList<JsonFragment>(currentSettings.fullJsonPluginFragment);
            string zipUrl = $"{gitHubPluginDlBaseUrl}/{fragmentList[0].TagName.Replace('_', '.')}.zip";
            Debug.Log($"{zipUrl} will be downloaded.");
            string dataPath = Application.dataPath;
            string root = Path.GetDirectoryName(dataPath);
            string dirPath = Path.Combine(root, "Plugin");

            if (Directory.Exists(dirPath))
                Directory.Delete(dirPath, true);

            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            string tmpFilePath = Path.Combine(dirPath, "tmpFile.zip");
            try
            {
                byte[] fileBytes = await httpClient.GetByteArrayAsync(zipUrl);
                if (fileBytes != null && fileBytes.Length > 0)
                {
                    Debug.Log($"{fileBytes.Length} bytes recieved");
                    FileStream fs = new FileStream(tmpFilePath, FileMode.OpenOrCreate);
                    fs.Write(fileBytes);
                    fs.Close();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Error accessing Github to retrieve zip file. Error: " + ex);
            }

            string installerPath = string.Empty;

            try
            {
                ZipFile.ExtractToDirectory(tmpFilePath, dirPath);
                Debug.Log($"Extracted {zipUrl} to {dirPath}");
                string[] files = Directory.GetFiles(dirPath, pluginInstaller, SearchOption.AllDirectories);

                if (files.Length == 0 || files.Length > 1)
                {
                    Debug.LogWarning($"Error finding installer.  Aborting please try downloading manually, extract the zip at {zipUrl} to a temporary location and run Install.bat");
                }
                else
                {
                    installerPath = files[0];
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error extracting remote zip to directory:\n" +
                                tmpFilePath + "\n" +
                                dirPath + "\n" +
                                e.Message);
            }

            if (!string.IsNullOrEmpty(installerPath))
            {
                File.Delete(tmpFilePath);
                Debug.Log($"Attempting to run {installerPath}");

                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    string args = "";
                    bool runShell = false;
                    ProcessStartInfo ps = new ProcessStartInfo(installerPath);
                    using (Process p = new Process())
                    {
                        ps.UseShellExecute = runShell;
                        if (!runShell)
                        {
                            ps.RedirectStandardOutput = true;
                            ps.RedirectStandardError = true;
                            ps.StandardOutputEncoding = System.Text.Encoding.ASCII;
                        }
                        if (args != null && args != "")
                        {
                            ps.Arguments = args;
                        }
                        p.StartInfo = ps;
                        p.Start();
                        p.WaitForExit();
                        if (!runShell)
                        {
                            string output = p.StandardOutput.ReadToEnd().Trim();
                            if (!string.IsNullOrEmpty(output))
                            {
                                Debug.Log($"{DateTime.Now} Output: {output}");
                            }

                            string errors = p.StandardError.ReadToEnd().Trim();
                            if (!string.IsNullOrEmpty(errors))
                            {
                                Debug.Log($"{DateTime.Now} Output: {errors}");
                            }
                        }
                    }
                    currentSettings.lastInstalledJsonPluginTagName = currentSettings.jsonPluginTagName;
                    ImporterWindow.SetGeneralSettings(currentSettings, true);
                }
                else
                    Debug.Log("Non Windows.");
            }
        }

        public static void WriteDummyReleaseInfo(RLSettingsObject settingsObject)
        {
            settingsObject.jsonTagName = "0.0.0";
            settingsObject.jsonHtmlUrl = "https://github.com/soupday";
            settingsObject.jsonPublishedAt = "";
            settingsObject.jsonBodyLines = new string[0];

            settingsObject.updateAvailable = false;
            UpdateManager.determinedSoftwareAction = DeterminedSoftwareAction.None;
            ImporterWindow.SetGeneralSettings(settingsObject, true);
        }

        public static void OnHttpVersionChecked(object sender, EventArgs e)
        {
            // any update code here
            HttpVersionChecked -= OnHttpVersionChecked;
        }

        public static void OnPluginHttpVersionChecked(object sender, EventArgs e)
        {
            // any update code here
            PluginHttpVersionChecked -= OnPluginHttpVersionChecked;
        }

        public static string GetJsonPropertyValue(string json, string property)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(property)) return string.Empty;
            try
            {
                int i = json.IndexOf(property);
                int c = json.IndexOf(":", i);
                int s = json.IndexOf("\"", c);
                int e = json.IndexOf("\"", s + 1);
                string result = json.Substring(s + 1, e - s - 1);
                return result;
            }
            catch (Exception ex)
            {
                Util.LogWarn("Error with Github data on latest 'CC/iC Unity Tools' version. Error: " + ex);
                return null;
            }
        }

        public class JsonFragment
        {
            [JsonProperty(gitHubTagName)]
            public string TagName { get; set; }
            [JsonProperty(gitHubHtmlUrl)]
            public string HtmlUrl { get; set; }
            [JsonProperty(name)]
            public string Name { get; set; }
            [JsonProperty(draft)]
            public string Draft { get; set; }
            [JsonProperty(preRelease)]
            public string PreRelease { get; set; }
            [JsonProperty(gitHubPublishedAt)]
            public string PublishedAt { get; set; }
            [JsonProperty(gitHubBody)]
            public string Body { get; set; }
        }

        public static List<T> GetFragmentList<T>(string json)
        {
            List<T> list = new List<T>();
            try
            {
                list = JsonConvert.DeserializeObject<List<T>>(json);
            }
            catch
            {
                return null;
            }
            if (list != null && list.Count > 0)
                return list;
            else
                return null;
        }

        public static bool BoolParse(string textString)
        {
            if (bool.TryParse(textString, out bool result)) { return result; } else { return false; }
        }

        public static Version TagToVersion(string tag)
        {
            tag = tag.Replace("_", ".");
            if (Version.TryParse(tag, out Version version))
            {
                return version;
            }
            else
            {
                //Debug.Log("Github Checker - Unable to correctly parse latest release version: " + tag);
                if (Version.TryParse(Regex.Replace(tag, "[^0-9.]", ""), out Version trimmedVersion))
                    return trimmedVersion;
                else
                    return new Version(0, 0, 0);
            }
        }

        public static bool TryParseISO8601toDateTime(string iso8601String, out DateTime dateTime)
        {
            dateTime = new DateTime();
            if (string.IsNullOrEmpty(iso8601String))
            {
                return false;
            }

            // GitHub's api uses ISO8601 for dates
            // "2024-09-05T00:03:15Z"

            if (DateTime.TryParse(iso8601String, out dateTime))
            {
                return true;
            }
            else
            {
                try
                {
                    int T = iso8601String.IndexOf('T');
                    string[] dateS = iso8601String.Substring(0, T).Split('-');
                    int Z = iso8601String.IndexOf('Z');
                    string[] timeS = iso8601String.Substring(T + 1, Z - T - 1).Split(':');
                    int[] date = new int[3];
                    int[] time = new int[3];
                    for (int i = 0; i < 3; i++)
                    {
                        Util.LogDetail("i: " + i + " date: " + date[i] + " parse: " + int.Parse(dateS[i]));
                        date[i] = int.Parse(dateS[i]);
                        time[i] = int.Parse(timeS[i]);
                    }
                    dateTime = new DateTime(date[0], date[1], date[2], time[0], time[1], time[2]);
                    return true;
                }
                catch (Exception ex)
                {
                    Util.LogError("Unable to parse date information from GitHub. Error: " + ex.ToString());
                    return false;
                }
            }
        }

        public static string[] LineSplit(string text)
        {
            return Regex.Split(text, @"(?:\r\n|\n|\r)");
        }

        public enum DeterminedSoftwareAction
        {
            None,
            Software_update_available
        }
    }
}
