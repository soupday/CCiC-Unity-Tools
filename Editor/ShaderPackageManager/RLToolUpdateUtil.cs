#if PLASTIC_NEWTONSOFT_AVAILABLE
using Unity.Plastic.Newtonsoft.Json;
#else
using Newtonsoft.Json;  // com.unity.collab-proxy (plastic scm) versions prior to 1.14.12
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Reallusion.Import
{
    public static class RLToolUpdateUtil
    {
        //public const string gitHubReleaseUrl = "https://api.github.com/repos/soupday/cc_blender_tools/releases";
        //public const string gitHubReleaseUrl = "https://api.github.com/repos/soupday/cc_unity_tools_HDRP/releases";
        public const string gitHubReleaseUrl = "https://api.github.com/repos/soupday/ccic_unity_tools_all/releases";
        public const string gitHubTagName = "tag_name";
        public const string gitHubHtmlUrl = "html_url";
        public const string name = "name";
        public const string draft = "draft";
        public const string preRelease = "prerelease";
        public const string gitHubPublishedAt = "published_at";
        public const string gitHubBody = "body";

        public static event EventHandler HttpVersionChecked;

        public static void UpdateManagerUpdateCheck()
        {
            //Debug.LogWarning("STARTING RLToolUpdateUtil CHECKS");
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
                    TimeSpan checkInterval = new TimeSpan(1, 0, 0, 0, 0);//TimeSpan(0, 0, 5, 0, 0);
                    DateTime now = DateTime.Now.ToLocalTime();

                    long univ = currentSettings.lastUpdateCheck;
                    DateTime last = new DateTime(univ);

                    if (TimeCheck(univ, checkInterval))
                    {
                        Debug.Log("Checking GitHub for 'CC/iC Unity Tools' update.");
                        currentSettings.lastUpdateCheck = now.Ticks;
                        ImporterWindow.SetGeneralSettings(currentSettings, true);
                        GitHubHttpVersionCheck();
                    }
                    else
                    {
                        if (currentSettings.updateAvailable)
                        {
                            UpdateManager.determinedSoftwareAction = DeterminedSoftwareAction.Software_update_available;
                            Debug.LogWarning("Settings object shows update availabe.");
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
                Debug.LogWarning("Error accessing Github to check for new 'CC/iC Unity Tools' version. Error: " + ex);
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
                            Debug.LogWarning("A newer version of CC/iC Unity Tools is available on GitHub. Current ver: " + installed.ToString() + " Latest ver: " + gitHubLatestVersion.ToString());

                            currentSettings.updateAvailable = true;
                            UpdateManager.determinedSoftwareAction = DeterminedSoftwareAction.Software_update_available;
                        }
                        currentSettings.fullJsonFragment = releaseJson;
                        ImporterWindow.SetGeneralSettings(currentSettings, true);
                    }
                }
                else
                {
                    Debug.LogWarning("Cannot parse JSON release data from GitHub - aborting version check.");

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
                Debug.LogWarning("Cannot find a release JSON from GitHub - aborting version check.");

                WriteDummyReleaseInfo(currentSettings);

                if (HttpVersionChecked != null)
                    HttpVersionChecked.Invoke(null, null);
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
            //RLToolUpdateWindow.OpenWindow();
            HttpVersionChecked -= OnHttpVersionChecked;
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
                Debug.LogWarning("Error with Github data on latest 'CC/iC Unity Tools' version. Error: " + ex);
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
                        Debug.Log("i: " + i + " date: " + date[i] + " parse: " + int.Parse(dateS[i]));
                        date[i] = int.Parse(dateS[i]);
                        time[i] = int.Parse(timeS[i]);
                    }
                    dateTime = new DateTime(date[0], date[1], date[2], time[0], time[1], time[2]);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.Log("Unable to parse date information from GitHub. Error: " + ex.ToString());
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
