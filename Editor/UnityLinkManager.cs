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

#if PLASTIC_NEWTONSOFT_AVAILABLE
using Unity.Plastic.Newtonsoft.Json;
using Unity.Plastic.Newtonsoft.Json.Linq;
#else
using Newtonsoft.Json;  // com.unity.collab-proxy (plastic scm) versions prior to 1.14.12
using Newtonsoft.Json.Linq;
using Formatting = Newtonsoft.Json.Formatting;
#endif
using UnityEngine;
using UnityEditor;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine.Playables;
using NUnit.Framework;
//using System.Drawing.Printing;
//using static UnityEngine.Rendering.DebugUI.Table;

namespace Reallusion.Import
{
    public class UnityLinkManager : Editor
    {
        #region Import
        public static bool SIMPLE_MODE;
        public static string IMPORT_DESTINATION_FOLDER = string.Empty;
        public static string IMPORT_DEFAULT_DESTINATION_FOLDER { get {  return GetDefaultFullFolderPath(); } }
        public static string STAGING_IMPORT_SUBFOLDER = "Staging Imports";
        public static string SCENE_ASSETS = "Scene Assets";
        public static string SCENE_FOLDER = string.Empty;
        public static string SCENE_NAME = string.Empty;
        public static string SCENE_UNSAVED_NAME = "Unsaved Scene";
        #endregion Import

        #region TimeLine vars
        // timeline creation only
        public static string TIMELINE_SAVE_FOLDER = string.Empty;
        public static bool LOCK_TIMELINE_TO_LAST_USED;
        public static string TIMELINE_DEFAULT_SAVE_FOLDER { get { return GetDefaultFullFolderPath(); } }        
        public static string TIMELINE_DEFAULT_REFERENCE_STRING = "Timeline Name"; // retain default so UI can see it has been changed before allowing creation
        public static string TIMELINE_REFERENCE_STRING = TIMELINE_DEFAULT_REFERENCE_STRING;
        // selected timeline asset - updated by the rowgui level selection in TimeLineTreeView
        [SerializeField]
        public static PlayableDirector SCENE_TIMELINE_ASSET;
        [SerializeField]
        public static string SCENE_TIMELINE_ASSET_NAME = "[Automatically Created]"; // this updates the info text field at the top of the Import controls

        //public static string UNITY_FOLDER_PATH { get { return GetUnityFolderPath(); } }
        //public static string UNITY_SCENE_PATH { get { return GetUnityScenePath(); } }
        //public static string TIMELINE_ASSET_PATH { get { return GetUnityTimelineAssetPath(); } }

        //public static GameObject timelineObject;

        private static string GetUnityFolderPath()
        {
            if (string.IsNullOrEmpty(TIMELINE_SAVE_FOLDER)) return string.Empty;
            string dataPath = Application.dataPath;
            string fullPath = Path.Combine(TIMELINE_SAVE_FOLDER, TIMELINE_REFERENCE_STRING);
            string UnityPath = fullPath.Substring(dataPath.Length - 6, fullPath.Length - dataPath.Length + 6);
            return UnityPath.Replace('\\', '/');
        }

        private static string GetDefaultFullFolderPath()
        {
            string defaultPath = "Assets/Reallusion/DataLink_Imports";
            string fullPath = defaultPath.UnityAssetPathToFullPath();
            //Debug.LogWarningFormat("GetDefaultFullFolderPath " + fullPath);
            return fullPath;

        }

        private static string GetUnityScenePath()
        {
            string dataPath = Application.dataPath;
            string fullPath = Path.Combine(TIMELINE_SAVE_FOLDER, TIMELINE_REFERENCE_STRING + ".unity");
            string UnityPath = fullPath.Substring(dataPath.Length - 6, fullPath.Length - dataPath.Length + 6);
            return UnityPath.Replace('\\', '/');
        }

        private static string GetUnityTimelineAssetPath()
        {
            string dataPath = Application.dataPath;
            string fullPath = Path.Combine(TIMELINE_SAVE_FOLDER, TIMELINE_REFERENCE_STRING, TIMELINE_REFERENCE_STRING + ".playable");
            string UnityPath = fullPath.Substring(dataPath.Length - 6, fullPath.Length - dataPath.Length + 6);
            return UnityPath.Replace('\\', '/');
        }
        #endregion TimeLine vars

        #region Setup
        public static void InitConnection()
        {
            //Debug.LogWarning("Starting InitConnection ");
            SetupUpdateWorker();
            SetupLogging();
            //StartQueue();
            StartClient();
            //UnityLinkManagerWindow.OpenWindow(); // window OnEnable will add the delegates for cleanup 
        }
        #endregion Setup

        #region Client
        public static string PLUGIN_VERSION = "2.2.5";
        public static string DEFAULT_EXPORTPATH { get { return GetDefaultExportPath(); } }
        private static string GetDefaultExportPath()
        {
            string datapath = Application.dataPath;
            string defaultpath = Path.Combine(Path.GetDirectoryName(datapath), "RL_DataLink");
            if (!Directory.Exists(defaultpath)) { Directory.CreateDirectory(defaultpath); }
            return defaultpath;
        }
        public static string EXPORTPATH = "";        
        public static bool IS_CLIENT_LOCAL = true; // need to recall this for auto reconnecting ... tbd
        public const string LOCAL_HOST = "127.0.0.1";
        public static string REMOTE_HOST = string.Empty;
                
        public static bool IMPORT_INTO_SCENE = true;
        public static bool USE_CURRENT_SCENE = true;
        public static bool ADD_TO_TIMELINE = true;
        
        public static bool timelineSceneCreated = false; // hmm


        //public static bool validHost = false;
        static TcpClient client = null;
        static NetworkStream stream = null;
        static Thread clientThread;
        static bool clientThreadActive = false;
        public static bool IsClientThreadActive {  get {  return clientThreadActive; } }
        static bool retryConnection = true;
        public static bool reconnect = false;
        static bool listening = false;

        private static bool queueIsActive = false;

        public static event EventHandler ClientConnected;
        public static event EventHandler ClientDisconnected;        

        static void StartClient()
        {
            clientThread = new Thread(new ThreadStart(ClientThread));
            clientThread.Start();
        }

        static void ClientThread()
        {
            clientThreadActive = true;
            retryConnection = true;
            //Debug.LogWarning("Parsing: " + (IS_CLIENT_LOCAL ? LOCAL_HOST : REMOTE_HOST));
            IPAddress ipAddress = IPAddress.Parse(IS_CLIENT_LOCAL ? LOCAL_HOST : REMOTE_HOST);
            int port = 9334;

            client = new TcpClient();
            
            #region connection retry
            int retryCount = 100;
            while (retryCount > 0 && retryConnection)
            {
                // https://stackoverflow.com/questions/17118632/how-to-set-the-timeout-for-a-tcpclient
                /*
                try
                {
                    var result = client.BeginConnect(ipAddress, port, null, null); 
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));

                    if (!success)
                    {
                        throw new Exception("Failed to connect.");
                    }

                    // we have connected
                    client.EndConnect(result);
                }
                catch (Exception e)
                {
                    NotifyInternalQueue("Attempting connection... " + e.Message);
                }
                */
                
                try
                {
                    client.Connect(ipAddress, port);
                }
                catch (Exception e)
                {
                    NotifyInternalQueue("Attempting connection... " + e.Message);
                }
                
                if (client.Connected)
                {
                    retryCount = 0;
                    break;
                }
                else
                {
                    retryCount--;
                }

                Thread.Sleep(500);
            }

            if (client == null || !client.Connected) // clean up
            {
                if (client != null) client.Close();
                retryConnection = false;
                listening = false;
                reconnect = false;
                clientThreadActive = false;
                NotifyInternalQueue("Unable to connect... ");
                if (ClientDisconnected != null) ClientDisconnected.Invoke(null, null);
                return;
            }
            #endregion connection retry

            string addr = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            string pt = ((IPEndPoint)client.Client.RemoteEndPoint).Port.ToString();
            NotifyInternalQueue("Client Connected to : " + addr + ":" + pt);

            stream = client.GetStream();
            reconnect = true;
            listening = true;

            while (listening)
            {
                try
                {
                    // rate limit
                    if (stream.CanRead)
                    {
                        if (!stream.DataAvailable)
                        {
                            Thread.Sleep(100);
                            continue;
                        }
                    }
                    else
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    RecvData();
                }
                catch (Exception e)
                {
                    Debug.Log(e.ToString());
                }
            }

            try
            {
                client.Close();
                stream.Close();
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
            }

            clientThreadActive = false;
            if (ClientDisconnected != null) ClientDisconnected.Invoke(null, null);
        }

        static OpCodes opCode = OpCodes.NONE;
        static byte[] header = new byte[8];
        static byte[] data = new byte[0];
        static int headerBytesRead = 0;
        static int bytesRead = 0;
        static int chunkSize = 0;
        static int size = 0;
        static int MAX_CHUNK_SIZE = 32768;

        static void RecvData()
        {            
            try
            {
                if (stream.CanRead)
                {
                    headerBytesRead = stream.Read(header, 0, 8);
                }
            }
            catch (Exception ex)
            {
                Debug.Log("Header read: " + ex);
            }

            if (headerBytesRead == 8)
            {
                (opCode, size) = HeaderUnpack(header);
                if (size > 0)
                {
                    // timeout wrapper
                    DateTime dataStartTime = DateTime.Now;
                    TimeSpan dataTimeout = TimeSpan.FromSeconds(30);
                    while (size > 0)
                    {
                        DateTime now = DateTime.Now;
                        if (now > dataStartTime + dataTimeout)
                        {
                            NotifyInternalQueue("Awaiting data: timed out.");
                            opCode = OpCodes.NONE;
                            break;
                        }

                        chunkSize = Math.Min(size, MAX_CHUNK_SIZE);
                        byte[] chunk = new byte[chunkSize];
                        try
                        {
                            if (stream.CanRead)
                            {
                                bytesRead = stream.Read(chunk, 0, chunkSize);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.Log("Data read: " + ex);
                        }
                        data = ConcatBytes(data, chunk);
                        size -= bytesRead;
                    }
                }
                if (opCode == OpCodes.FILE)
                {
                    // with FILE, the data is the remote_id (the remote id is submitted to the processing queue after the corresponding zipfile has been written to disk)
                    if (size == 0) // expected number of bytes read
                    {
                        // zip file remote id
                        string remoteId = Encoding.UTF8.GetString(data);
                        //Debug.Log("Expecting ZipFile with remoteId: " + remoteId);  // dont Util.LogInfo this since it is not on the main thread

                        // next 4 bytes is zipfile length
                        byte[] len = new byte[4];
                        bytesRead = 0;
                        try
                        {
                            if (stream.CanRead)
                            {
                                bytesRead = stream.Read(len, 0, 4);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.Log("Data read: " + ex);
                        }

                        int zipSize = GetCurrentEndianWord(len, SourceEndian.BigEndian);
                        //Debug.Log("Expected ZipFile size: " + zipSize);

                        string streamPath = Path.Combine(EXPORTPATH, remoteId + ".zip");
                        FileStream fileStream = new FileStream(streamPath, FileMode.Create, FileAccess.ReadWrite);

                        // timeout wrapper
                        DateTime zipStartTime = DateTime.Now;
                        TimeSpan zipTimeout = TimeSpan.FromSeconds(300);
                        while (zipSize > 0)
                        {
                            DateTime now = DateTime.Now;
                            if (now > zipStartTime + zipTimeout)
                            {
                                NotifyInternalQueue("Awaiting data: timed out.");
                                opCode = OpCodes.NONE;
                                break;
                            }
                            chunkSize = Math.Min(zipSize, MAX_CHUNK_SIZE);
                            byte[] chunk = new byte[chunkSize];
                            bytesRead = 0;
                            try
                            {
                                if (stream.CanRead)
                                {
                                    bytesRead = stream.Read(chunk, 0, chunkSize);
                                    fileStream.Write(chunk, 0, bytesRead); // chunk.Length);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.Log("Data read: " + ex);
                            }                            
                            zipSize -= bytesRead;
                        }
                        fileStream.Close();
                        fileStream = null;
                        if (zipSize == 0)
                        {
                            HandleRecivedData(data);
                        }
                    }
                }
                else if (opCode != OpCodes.NONE && opCode != OpCodes.FILE)
                {
                    // with all other opcodes the data is a JSON object which will be deserialized and queued
                    if (size == 0) // expected number of bytes read
                    {
                        HandleRecivedData(data);
                    }
                }
            }            

            // reset all
            opCode = OpCodes.NONE;
            header = new byte[8];
            data = new byte[0];
            headerBytesRead = 0;
            bytesRead = 0;
            chunkSize = 0;
            size = 0;
        }

        static (OpCodes, int) HeaderUnpack(byte[] headerBytes)
        {
            //Debug.Log("HeaderUnpack.");
            byte[] opCodeBytes = ExtractBytes(headerBytes, 0, 4);
            byte[] sizeBytes = ExtractBytes(headerBytes, 4, 4);
            OpCodes opCode = OpCode(GetCurrentEndianWord(opCodeBytes, SourceEndian.BigEndian));
            int size = GetCurrentEndianWord(sizeBytes, SourceEndian.BigEndian);

            return (opCode, size);
        }

        public static byte[] ConcatByteArray(byte[] first, byte[] second)
        {
            Array.Resize(ref first, first.Length + second.Length);
            Buffer.BlockCopy(second, 0, first, first.Length - second.Length, second.Length);
            return first;
        }

        // alt - may be faster
        public static byte[] ConcatBytes (byte[] first, byte[] second)
        {
            IEnumerable<byte> bytes = first.Concat(second);
            return bytes.ToArray();
        }

        public static void NotifyInternalQueue(string message)
        {
            if (queueIsActive)
            {
                if (activityQueue != null)
                {
                    var q = new QueueItem(OpCodes.NOTIFY, Exchange.INTERNAL);
                    q.Notify = new JsonNotify(message);
                    activityQueue.Add(q);
                }
            }
        }
        #endregion Client

        #region Server messaging
        //[SerializeField]
        public static List<QueueItem> activityQueue;

        public enum Exchange
        {
            NONE = 0,
            SENT = 1,
            RECEIVED = 2,
            INTERNAL = 3
        }

        public enum OpCodes
        {
            NONE = 0,
            HELLO = 1,
            PING = 2,
            STOP = 10,
            DISCONNECT = 11,
            DEBUG = 15,
            NOTIFY = 50,
            INVALID = 55,
            SAVE = 60,
            FILE = 70,
            CHARACTER = 100,
            CHARACTER_UPDATE = 101,
            PROP = 102,
            PROP_UPDATE = 103,
            STAGING = 104,
            STAGING_UPDATE = 105,
            CAMERA = 106,
            CAMERA_UPDATE = 107,
            UPDATE_REPLACE = 108,
            TEMPLATE = 200,
            POSE = 210,
            POSE_FRAME = 211,
            SEQUENCE = 220,
            SEQUENCE_FRAME = 221,
            SEQUENCE_END = 222,
            SEQUENCE_ACK = 223,
            LIGHTING = 230,
            CAMERA_SYNC = 231,
            FRAME_SYNC = 232,
            MOTION = 240,
            REQUEST = 250,
            CONFIRM = 251,

            // additions for testing
            TEST = 999,

            // error case
            UNKNOWN = 999999
        }

        static OpCodes OpCode(int code)
        {
            if (Enum.TryParse(code.ToString(), out OpCodes opCode))
            {
                return opCode;
            }
            else
            {
                return OpCodes.UNKNOWN;
            }
        }

        static void SendMessage(OpCodes opCode, string jsonString = null)
        {
            // send message using big-endian byte order (python server should expect big-endian order via "!II")
            // def recv ... op_code, size = struct.unpack("!II", header)
            try
            {
                if (stream == null) return;

                IEnumerable<byte> message = null;

                Int32 code = (Int32)opCode;
                byte[] opcode = Int32ToBigEndianBytes(code);

                byte[] data = new byte[0];
                if (!string.IsNullOrEmpty(jsonString))
                {
                    data = Encoding.UTF8.GetBytes(jsonString);
                    byte[] size = Int32ToBigEndianBytes(data.Length);
                    message = opcode.Concat(size).Concat(data);
                }
                else
                {
                    byte[] size = Int32ToBigEndianBytes(0);
                    message = opcode.Concat(size);
                }
                if (stream.CanWrite)
                {
#if UNITY_2021_1_OR_NEWER
                    stream.Write(message.ToArray());
#else
                    byte[] buffer = message.ToArray();
                    stream.Write(buffer, 0, buffer.Length);
#endif
                }
            }
            catch (Exception ex)
            {
                Debug.Log(ex.ToString());
            }
        }
        #endregion Server messaging

        #region Connection
        static void ServerDisconnect()
        {
            NotifyInternalQueue("Server disconnection received... ");
            //StopQueue();

            retryConnection = false;
            listening = false;
            reconnect = false;

            try
            {
                if (client.Connected && stream.CanWrite)
                {
                    stream.Close();
                    client.Close();
                }
            }
            catch (Exception ex)
            {
                Debug.Log(ex.ToString());
            }
        }

        public static void AbortConnectionAttempt()
        {
            // end the retry and listening loops so the task runs to the end and closes gracefully
            NotifyInternalQueue("Aborting connection attempt... ");
            //StopQueue();
            retryConnection = false;
            listening = false;
            reconnect = false;
        }

        public static void DisconnectFromServer()
        {
            SetConnectedTimeStamp(true);
            NotifyInternalQueue("Closing connection... ");
            StopQueue();
            SendMessage(OpCodes.DISCONNECT);

            retryConnection = false;
            listening = false;
            reconnect = false;

            try
            {
                if (stream != null)
                    if (stream.CanWrite) stream.Close();
                if (client != null)
                    if (client.Connected) client.Close();
            }
            catch (Exception ex)
            {
                Debug.Log(ex.ToString());
            }
        }

        public static void CleanupBeforeAssemblyReload()
        {
            //Debug.LogWarning("adding CleanupDelegate to AssemblyReloadEvents.beforeAssemblyReload");
            AssemblyReloadEvents.beforeAssemblyReload -= CleanupDelegate;
            AssemblyReloadEvents.beforeAssemblyReload += CleanupDelegate;
        }

        static void CleanupDelegate()
        {
            //Debug.LogWarning("CleanupDelegate called by AssemblyReloadEvents.beforeAssemblyReload");
            if (reconnect)
            {
                //Debug.Log("Setting up reconnect");
                SetConnectedTimeStamp();
            }
            else
            {
                //Debug.LogWarning("SetConnectedTimeStamp(true)");
                SetConnectedTimeStamp(true);
            }

            try
            {
                if (client != null && stream != null)
                {
                    if (client.Connected && stream.CanWrite)
                    {
                        Debug.Log("Disconnecting");
                        reconnect = false;
                        SendMessage(OpCodes.DISCONNECT);
                        stream.Close();
                        client.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log(ex.ToString());
            }

            listening = false;
            EditorApplication.update -= QueueDelegate;
            AssemblyReloadEvents.beforeAssemblyReload -= CleanupDelegate;
            //Debug.LogWarning("AssemblyReloadEvents.beforeAssemblyReload done");
        }

        // Automated reconnection for assembly reloads
        /*
        public static void AttemptAutoReconnect()
        {
            Debug.Log("OnEnable - AutoReconnect");
            if (IsConnectedTimeStampWithin(new TimeSpan(0, 5, 0)))
            {
                Debug.Log("OnEnable - Attempting to reconnect");
                RLSettingsObject settings = RLSettings.FindRLSettingsObject();
                if (settings != null)
                {
                    IS_CLIENT_LOCAL = settings.isClientLocal;
                    REMOTE_HOST = settings.lastSuccessfulHost;
                }
                reconnect = false;
                InitConnection();
            }
            else
            {
                Debug.Log("OnEnable - Not AutoReconnect-ing");
            }
        }
        */


        public const string connectPrefString = "RL_CC_Server_Disconnect_Timestamp";

        static void SetConnectedTimeStamp(bool disconnect = false)  
        {
            // disconnect = true will set the connected timestamp beyond the timeout limit for auto reconnection
            long time = 0;// long.MinValue;

            if (!disconnect)
            {
                DateTime currentTime = DateTime.Now.ToLocalTime();
                time = currentTime.Ticks;
            }

            EditorPrefs.SetString(connectPrefString, time.ToString());
            //Debug.Log("Writing timestamp string: " + time.ToString() + " to EditorPrefs: " + connectPrefString);
        }

        static bool IsConnectedTimeStampWithin(TimeSpan interval)
        {
            if (EditorPrefs.HasKey(connectPrefString))
            {
                string stampString = EditorPrefs.GetString(connectPrefString);
                long.TryParse(stampString, out long timestamp);
                DateTime connectedTime = new DateTime(timestamp);
                DateTime now = DateTime.Now.ToLocalTime();
                if (connectedTime + interval > now)
                {
                    EditorPrefs.SetString(connectPrefString, "0");
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
        #endregion Connection

        #region Recieved data handling
        // first 4 bytes are the 32 bit opcode
        // next 4 bytes are the size
        // https://github.com/soupday/CCiC-Blender-Pipeline-Plugin/blob/main/btp/link.py

        static void HandleRecivedData(byte[] recievedData)
        {
            if (activityQueue == null) activityQueue = new List<QueueItem>();

            QueueItem qItem = new QueueItem(opCode, Exchange.RECEIVED);
            string dataString = string.Empty;

            dataString = Encoding.UTF8.GetString(recievedData);
            bool add = true;
            switch (opCode)
            {
                case OpCodes.UNKNOWN:
                    {
                        break;
                    }

                case OpCodes.NONE:
                    {
                        break;
                    }
                case OpCodes.HELLO:
                    {
                        try
                        {
                            qItem.Hello = JsonConvert.DeserializeObject<JsonHello>(dataString);
                        }
                        catch (Exception ex) { Debug.Log(ex); add = false; }
                        break;
                    }
                case OpCodes.NOTIFY:
                    {
                        try { qItem.Notify = JsonConvert.DeserializeObject<JsonNotify>(dataString); } catch (Exception ex) { Debug.Log(ex); add = false; }
                        break;
                    }
                case OpCodes.STOP:
                    {
                        ServerDisconnect();
                        break;
                    }
                case OpCodes.DISCONNECT:
                    {
                        ServerDisconnect();
                        break;
                    }
                case OpCodes.FILE:
                    {
                        qItem.RemoteId = dataString;
                        break;
                    }
                case OpCodes.CHARACTER:
                    {
                        try { qItem.Character = JsonConvert.DeserializeObject<JsonCharacter>(dataString); } catch (Exception ex) { Debug.Log(ex); add = false; }
                        if (qItem.Character != null)
                        {
                            if (!string.IsNullOrEmpty(qItem.Character.RemoteId)) { qItem.RemoteId = qItem.Character.RemoteId; }
                            qItem.Name = qItem.Character.Name;
                        }
                        break;
                    }
                case OpCodes.CHARACTER_UPDATE:
                    {
                        try { qItem.CharacterUpdate = JsonConvert.DeserializeObject<JsonCharacterUpdate>(dataString); } catch (Exception ex) { Debug.Log(ex); add = false; }
                        break;
                    }
                case OpCodes.PROP:
                    {
                        try { qItem.Prop = JsonConvert.DeserializeObject<JsonProp>(dataString); } catch (Exception ex) { Debug.Log(ex); add = false; }
                        if (qItem.Prop != null)
                        {
                            if (!string.IsNullOrEmpty(qItem.Prop.RemoteId)) { qItem.RemoteId = qItem.Prop.RemoteId; }
                            qItem.Name = qItem.Prop.Name;
                        }
                        break;
                    }
                case OpCodes.STAGING:
                    {
                        try { qItem.Staging = JsonConvert.DeserializeObject<JsonStaging>(dataString); } catch (Exception ex) { Debug.Log(ex); add = false; }
                        if (qItem.Staging != null)
                        {
                            if (!string.IsNullOrEmpty(qItem.Staging.RemoteId)) { qItem.RemoteId = qItem.Staging.RemoteId; }
                            qItem.Name = qItem.Staging.Names[0];
                        }
                        break;
                    }
                case OpCodes.CAMERA: // ...
                    {
                        Debug.Log(dataString);
                        break;
                    }
                case OpCodes.UPDATE_REPLACE:
                    {
                        try { qItem.UpdateReplace = JsonConvert.DeserializeObject<JsonUpdateReplace>(dataString); } catch (Exception ex) { Debug.Log(ex); add = false; }
                        break;
                    }
                case OpCodes.MOTION:
                    {
                        try { qItem.Motion = JsonConvert.DeserializeObject<JsonMotion>(dataString); } catch (Exception ex) { Debug.Log(ex); add = false; }
                        if (qItem.Motion != null)
                        {
                            if (!string.IsNullOrEmpty(qItem.Motion.RemoteId)) { qItem.RemoteId = qItem.Motion.RemoteId; }
                            qItem.Name = qItem.Motion.Name;
                        }
                        break;
                    }
                case OpCodes.LIGHTING:
                    {
                        try { qItem.Lighting = JsonConvert.DeserializeObject<JsonLighting>(dataString); } catch (Exception ex) { Debug.Log(ex); add = false; }
                        break;
                    }
                case OpCodes.CAMERA_SYNC:
                    {
                        try { qItem.CameraSync = JsonConvert.DeserializeObject<JsonCameraSync>(dataString); } catch (Exception ex) { Debug.Log(ex); add = false; }
                        break;
                    }
                case OpCodes.FRAME_SYNC:
                    {
                        try { qItem.FrameSync = JsonConvert.DeserializeObject<JsonFrameSync>(dataString); } catch (Exception ex) { Debug.Log(ex); add = false; }
                        break;
                    }
                case OpCodes.REQUEST:
                    {
                        try { qItem.Request = JsonConvert.DeserializeObject<JsonRequest>(dataString); } catch (Exception ex) { Debug.Log(ex); add = false; } 
                        break;
                    }
            }

            WriteIncomingLog(dataString, add);

            if (add)
            {
                activityQueue.Add(qItem);
            }
            else
            {
                Debug.LogWarning("Broken Item: " + opCode.ToString());
                Debug.LogWarning(dataString);
            }
        }
        
        public static byte[] ExtractBytes(byte[] data, int startIndex, int length)
        {
            byte[] sizeBytes = new byte[length];
            int n = 0;
            for (int i = startIndex; i < (startIndex + length); i++)
            {
                sizeBytes[n++] = data[i];
            }
            return sizeBytes;
        }
        
        static string ClientHelloMessage()
        {
            string jsonString = string.Empty;
            JsonHello hello = new JsonHello();
            hello.Application = "Unity CCiC Importer";

            Version v;
            int[] ints = new int[0];

            if (Version.TryParse(Pipeline.VERSION, out v))
                ints = new int[] { v.Major, v.Minor, v.Build };
            else
                ints = new int[] { 2, 0, 0 };            

            hello.Version = ints;
            hello.Path = EXPORTPATH;
            hello.Plugin = PLUGIN_VERSION;
            hello.Exe = EditorApplication.applicationPath;
            hello.Package = Pipeline.VERSION;
            hello.LocalClient = IS_CLIENT_LOCAL;

            // Debug.LogWarning(Application.productName);  // update plugin to use the project name (Application.productName)

            jsonString = JsonConvert.SerializeObject(hello);
            //Debug.Log(jsonString);
            return jsonString;
        }
        #endregion Recieved data handling

        #region Activity queue handling
        public static void StartQueue()
        {
            if (!queueIsActive)
            {
                activityQueue = new List<QueueItem>();
                EditorApplication.update -= QueueDelegate;
                EditorApplication.update += QueueDelegate;
                queueIsActive = true;
            }
        }

        public static void StopQueue()
        {            
            //activityQueue.Clear();
            EditorApplication.update -= QueueDelegate;
            queueIsActive = false;
        }

        static double timer = 0f;
        static double now = 0f;
        public static void QueueDelegate()
        {
            now = EditorApplication.timeSinceStartup;
            if (now - timer < 0.1f)
            {
                return;
            }
            else
            {
                timer = now;
            }

            if (activityQueue == null) { return; }
            QueueItem next = new QueueItem(OpCodes.NONE, Exchange.RECEIVED);
            
            if (activityQueue.Count == 0) { return; }
            try
            {
                next = activityQueue.First(x => x.Processed == false);
            }
            catch
            {
                return;
            }

            //Debug.Log("Processing next queue item.");

            switch (next.OpCode)
            {
                case OpCodes.UNKNOWN:
                    {
                        break;
                    }

                case OpCodes.NONE:
                    {
                        break;
                    }
                case OpCodes.HELLO:
                    {
                        SendMessage(OpCodes.HELLO, ClientHelloMessage());
                        if (ClientConnected != null)
                            ClientConnected.Invoke(null, null); //non threaded
                        break;
                    }
                case OpCodes.NOTIFY:
                    {
                        //Debug.Log(next.Notify.ToString());
                        break;
                    }
                case OpCodes.FILE:
                    {
                        //Debug.Log("File remote id: " + next.RemoteId);
                        break;
                    }
                case OpCodes.CHARACTER:
                    {
                        next.Processed = true;
                        //Debug.Log(next.Character.ToString());
                        ImportItem(next);
                        break;
                    }
                case OpCodes.CHARACTER_UPDATE:
                    {
                        //Debug.Log(next.CharacterUpdate.ToString());
                        break;
                    }
                case OpCodes.PROP:
                    {
                        next.Processed = true;
                        //Debug.Log(next.Prop.ToString());
                        ImportItem(next);
                        break;
                    }
                case OpCodes.STAGING:
                    {
                        next.Processed = true;
                        //Debug.Log(next.Staging.ToString());
                        ImportItem(next);
                        break;
                    }
                case OpCodes.CAMERA:  // ...
                    {
                        break;
                    }
                case OpCodes.UPDATE_REPLACE:
                    {
                        //Debug.Log(next.UpdateReplace.ToString());
                        break;
                    }
                case OpCodes.MOTION:
                    {
                        next.Processed = true;
                        //Debug.Log(next.Motion.ToString());
                        ImportItem(next);
                        break;
                    }
                case OpCodes.LIGHTING:
                    {
                        next.Processed = true;
                        //Debug.Log(next.Lighting.ToString());
                        break;
                    }
                case OpCodes.CAMERA_SYNC:
                    {
                        CameraSync(next);
                        //Debug.Log(next.CameraSync.ToString());
                        break;
                    }
                case OpCodes.FRAME_SYNC:
                    {
                        FrameSync(next);
                        //Debug.Log(next.FrameSync.ToString());
                        break;
                    }
                    case OpCodes.REQUEST:
                    {
                        //Debug.LogWarning("The 'Send Scene' function is not yet fully implemented - Use with caution.");
                        RespondToSceneRequest(next);
                        break;
                    }
            }
            next.Processed = true;
            if (UnityLinkManagerWindow.Instance != null) UnityLinkManagerWindow.Instance.Focus();            
        }

        static void CameraSync(QueueItem item)
        {
            SceneView scene = SceneView.lastActiveSceneView;
            Vector3 rawPosition = item.CameraSync.Position;
            Vector3 targetPosition = item.CameraSync.Target;
            Vector3 cameraPos = new Vector3(-rawPosition.x, rawPosition.z, -rawPosition.y) * 0.01f;
            Vector3 targetPos = new Vector3(-targetPosition.x, targetPosition.z, -targetPosition.y) * 0.01f;

            Quaternion blenderQuaternion = item.CameraSync.Rotation;
            // convert blender quaternion to unity
            Quaternion unityQuaternion = new Quaternion(blenderQuaternion.x,
                                                        -blenderQuaternion.z,
                                                         blenderQuaternion.y,
                                                         blenderQuaternion.w);
            // correct rotation to point blender camera's forward -Y (in Unity space) to forward +Z
            Quaternion cameraCorrection = Quaternion.Euler(90f, -180f, 0f);
            Quaternion corrected = unityQuaternion * cameraCorrection;

            // put the scene into focus so it updates
            scene.Focus();            

            scene.cameraSettings.fieldOfView = item.CameraSync.Fov;
            float halfAngle = scene.cameraSettings.fieldOfView / 2f;

            Vector3 dir = new Vector3(0, 0, 1);
            dir = corrected * dir;
            Vector3 toPivot = targetPos - cameraPos;
            float adjacent = Vector3.Dot(dir, toPivot);
            if (adjacent < 0.00001f) adjacent = 1.0f;
            Vector3 pointToLookAt = cameraPos + dir * adjacent;
            // https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SceneView-size.html
            float size = Mathf.Sin(halfAngle * Mathf.Deg2Rad) * adjacent;
            scene.LookAt(pointToLookAt, corrected, size * 0.9f);
            //Debug.LogWarning("lookPos " + pointToLookAt + " focalLength " + adjacent);
        }
        static void OLDCameraSync(QueueItem item)
        {
            //GameObject camera = GameObject.Find("Main Camera");

            SceneView scene = SceneView.lastActiveSceneView;
            Vector3 rawPosition = item.CameraSync.Position;
            Vector3 targetPosition = item.CameraSync.Target;
            Vector3 cameraPos = new Vector3(-rawPosition.x, rawPosition.z, -rawPosition.y) * 0.01f;
            Vector3 targetPos = new Vector3(-targetPosition.x, targetPosition.z, -targetPosition.y) * 0.01f;

            Quaternion blenderQuaternion = item.CameraSync.Rotation;
            // convert blender quaternion to unity
            Quaternion unityQuaternion = new Quaternion( blenderQuaternion.x,
                                                        -blenderQuaternion.z,
                                                         blenderQuaternion.y,
                                                         blenderQuaternion.w);
            // correct rotation to point blender camera's forward -Y (in Unity space) to forward +Z
            Quaternion cameraCorrection = Quaternion.Euler(90f, -180f, 0f);
            Quaternion corrected = unityQuaternion * cameraCorrection;
            
            //camera.transform.position = cameraPos;
            //camera.transform.rotation = corrected;
            
            // put the scene into focus so it updates
            scene.Focus();

            // set the scene camera target
            // can't set camera position directly, have to calculate a lookat target
            Vector3 dir = new Vector3(0, 0, 1);
            dir = corrected * dir;
            Vector3 toPivot = targetPos - cameraPos;
            float dist = Vector3.Dot(dir, toPivot);
            if (dist < 0f) dist = 1.0f;
            Vector3 lookPos = cameraPos + (dir * dist);            
            scene.LookAt(lookPos, corrected, dist / 8.0f);
            scene.pivot = lookPos;
            scene.cameraSettings.fieldOfView = item.CameraSync.Fov;
            
            // other ways to force the scene to update
            //SceneView.lastActiveSceneView.Repaint();
            //EditorApplication.ExecuteMenuItem("Window/General/Scene");
        }
        
        static void FrameSync(QueueItem item)
        {
            UnityLinkSceneManagement.SetTimelineTimeIndex(item.FrameSync.CurrentTime / (item.FrameSync.Fps * 100f));
        }

        static void ImportItem(QueueItem item)
        {
            try
            {
                UnityLinkImporter Importer = new UnityLinkImporter(item);
                Importer.Import();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Import Failure");
                Debug.LogWarning(e.ToString());
            }
        }

        static void RespondToSceneRequest(QueueItem item)
        {
            bool importIntoScene = UnityLinkManager.IMPORT_INTO_SCENE;

            // Examine current scene contents
#if UNITY_2023_OR_NEWER
            DataLinkActorData[] linkedSceneObjects = GameObject.FindObjectsByType<DataLinkActorData>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else       
            DataLinkActorData[] linkedSceneObjects = GameObject.FindObjectsOfType<DataLinkActorData>();
#endif
            JsonRequest reply = new JsonRequest(item.Request.Type);
            
            if (item.Request != null && item.Request.Actors != null)
            {
                //Debug.Log("Listing iClone scene contents");
                foreach (var actor in item.Request.Actors)
                {
                    bool isPresent = false;
                    bool isSkinned = false;

                    try
                    {
                        var presentInScene = linkedSceneObjects.FirstOrDefault(x => x.linkId == actor.LinkId);
                        isPresent = presentInScene != null;
                        if (isPresent) isSkinned = IsSkinned(presentInScene.gameObject);
                    }
                    catch { }

                    //Debug.Log($"Name: {actor.Name}, Type: {actor.Type}, LinkID: {actor.LinkId} , Present in scene: {isPresentInScene}");

                    if (actor.Type == "AVATAR" || actor.Type == "PROP")
                    {
                        GameObject go = null;
                        if (!isPresent)
                        {
                            go = GetPrefabFromLinkId(actor.LinkId);
                            if (go != null)
                            {
                                if (importIntoScene) UnityLinkSceneManagement.AddToScene(go, actor.LinkId);
                                isPresent = true;
                                if (isPresent) isSkinned = IsSkinned(go);
                            }
                        }
                    }
                    actor.Confirm = isPresent;
                    actor.Skinned = isSkinned;
                    reply.Actors.Add(actor);
                }

                try
                {
                    string replyString = JsonConvert.SerializeObject(reply);
                    SendMessage(OpCodes.CONFIRM, replyString);
                }
                catch
                {
                    Debug.Log("Cannot format scene request reply");
                }
            }
        }

        static bool IsSkinned(GameObject go)
        {
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
            return smr != null;
        }

        static GameObject GetPrefabFromLinkId(string linkId)
        {
            WindowManager.UpdateImportList();

            CharacterInfo characterMatch = WindowManager.ValidImports.FirstOrDefault(x => x.linkId == linkId);
            if (characterMatch != null)
            {
                GameObject match = characterMatch.GetDraggablePrefab();
                if (match != null)
                {
                    return match;
                }
            }
            return null;
        }

        public static CharacterInfo GetCharacterInfoFromLinkId(string linkId)
        {
            WindowManager.UpdateImportList();

            CharacterInfo characterMatch = WindowManager.ValidImports.FirstOrDefault(x => x.linkId == linkId);
            if (characterMatch != null)
                return characterMatch;

            return null;
        }

        #endregion  Activity queue handling

        #region Class data               
        public static CharacterInfo.ExportType ParseExportType(string value)
        {
            return Enum.TryParse(value, out CharacterInfo.ExportType result) ? result : CharacterInfo.ExportType.UNKNOWN;            
        }

        public class JsonHello // HELLO: (1) - Respond to new connection with server data
        {
            public const string applicationStr = "Application";     // Application: String - Application name
            public const string versionStr = "Version";             // Version: Int list [major, minor, revision] - Version numbers
            public const string pathStr = "Path";                   // Path: String - local path where it saves exports (only used as a fallback if the clients local path doesn't exist)
            public const string pluginStr = "Plugin";               // Plugin: String - plugin version
            public const string exeStr = "Exe";
            public const string packageStr = "2.0.1";// Exe: String - path to iClone/CC executable
            public const string localClientStr = "Local";

            [JsonProperty(applicationStr)]
            public string Application { get; set; }
            [JsonProperty(versionStr)]
            public int[] Version { get; set; }
            [JsonProperty(pathStr)]
            public string Path { get; set; }
            [JsonProperty(pluginStr)]
            public string Plugin { get; set; }
            [JsonProperty(exeStr)]
            public string Exe { get; set; }
            [JsonProperty(packageStr)]
            public string Package { get; set; }
            [JsonProperty(localClientStr)]
            public bool LocalClient { get; set; } // client side only to force TCP file transmission with LocalClient = false;

            public JsonHello()
            {
                Application = string.Empty;
                Version = new int[0];
                Path = string.Empty;
                Plugin = string.Empty;
                Exe = string.Empty;
                Package = string.Empty;
                LocalClient = true;
            }

            public override string ToString()
            {
                return "Application " + this.Application + ", Version " + this.Version.ToString() + ", Path " + this.Path + ", Plugin " + this.Plugin + ", Exe " + this.Exe + ", Package " + this.Package + ", Local Client " + this.LocalClient.ToString();
            }
        }

        public class JsonNotify // NOTIFY: (50) - update client with a status message (to display to user)
        {
            public const string messageStr = "message"; // message: String

            [JsonProperty(messageStr)]
            public string Message { get; set; }

            public JsonNotify()
            {
                Message = string.Empty;
            }

            public JsonNotify(string message)
            {
                Message = message;
            }

            public override string ToString()
            {
                return "Message " + this.Message;
            }
        }

        public class JsonCharacter // CHARACTER: (100) - receive character export from server
        {
            public const string remoteIdStr = "remote_id";          // remote_id: string zipile name for remote transmission
            public const string pathStr = "path";                   // path: String - file path of fbx export file
            public const string nameString = "name";                // name: String - actor name,
            public const string typeStr = "type";                   // type: String - actor type (AVATAR, PROP, LIGHT, CAMERA) - Should be AVATAR (used to verify)
            public const string linkIdStr = "link_id";              // link_id: String - actor link id code
            public const string motionPrefixStr = "motion_prefix";  // motion_prefix: String - name to prefix animation names

            [JsonProperty(remoteIdStr)]
            public string RemoteId { get; set; }
            [JsonProperty(pathStr)]
            public string Path { get; set; }
            [JsonProperty(nameString)]
            public string Name { get; set; }
            [JsonProperty(typeStr)]
            public string Type { get; set; }
            [JsonProperty(linkIdStr)]
            public string LinkId { get; set; }
            [JsonProperty(motionPrefixStr)]
            public string MotionPrefix { get; set; }

            public JsonCharacter()
            {
                RemoteId = string.Empty;
                Path = string.Empty;
                Name = string.Empty;
                Type = string.Empty;
                LinkId = string.Empty;
                MotionPrefix = string.Empty;
            }

            public override string ToString()
            {
                return (string.IsNullOrEmpty(RemoteId) ? "" : ("Remote Id " + RemoteId + " ,")) + "Path " + this.Path + ", Name " + this.Name + ", Type " + this.Type + ", Link Id " + this.LinkId + ", Motion Prefix " + this.MotionPrefix;
            }
        }

        public class JsonCharacterUpdate // CHARACTER_UPDATE: (101) - send changes to character id data (name, link_id)
        {
            // Notes: Probably not needed for Unity.
        }

        public class JsonProp // PROP: (102) - receive prop export from server
        {
            public const string remoteIdStr = "remote_id";          // remote_id: string zipile name for remote transmission
            public const string pathStr = "path";                   // path: String - file path of fbx export file
            public const string nameString = "name";                // name: String - prop name
            public const string typeStr = "type";                   // type: String - actor type (AVATAR, PROP, LIGHT, CAMERA) - Should be PROP (used to verify)
            public const string linkIdStr = "link_id";              // link_id: String - actor link id code
            public const string motionPrefixStr = "motion_prefix";  // motion_prefix: String - name to prefix animation names

            [JsonProperty(remoteIdStr)]
            public string RemoteId { get; set; }
            [JsonProperty(pathStr)]
            public string Path { get; set; }
            [JsonProperty(nameString)]
            public string Name { get; set; }
            [JsonProperty(typeStr)]
            public string Type { get; set; }
            [JsonProperty(linkIdStr)]
            public string LinkId { get; set; }
            [JsonProperty(motionPrefixStr)]
            public string MotionPrefix { get; set; }

            public JsonProp()
            {
                RemoteId = string.Empty;
                Path = string.Empty;
                Name = string.Empty;
                Type = string.Empty;
                LinkId = string.Empty;
                MotionPrefix = string.Empty;
            }

            public override string ToString()
            {
                return (string.IsNullOrEmpty(RemoteId) ? "" : ("Remote Id " + RemoteId + " ,")) + "Path " + this.Path + ", Name " + this.Name + ", Type " + this.Type + ", Link Id " + this.LinkId + ", Motion Prefix " + this.MotionPrefix;
            }
        }

        public class JsonStaging // STAGING = 104
        {
            public const string remoteIdStr = "remote_id";
            public const string pathStr = "path";
            public const string nameString = "names";
            public const string typeStr = "types";
            public const string linkIdStr = "link_ids";
            public const string motionPrefixStr = "motion_prefix";

            [JsonProperty(remoteIdStr)]
            public string RemoteId { get; set; }
            [JsonProperty(pathStr)]
            public string Path { get; set; }
            [JsonProperty(nameString)]
            public string[] Names { get; set; }
            [JsonProperty(typeStr)]
            public string[] Types { get; set; }
            [JsonProperty(linkIdStr)]
            public string[] LinkIds { get; set; }
            [JsonProperty(motionPrefixStr)]
            public string MotionPrefix { get; set; }

            public JsonStaging()
            {
                RemoteId = string.Empty;
                Path = string.Empty;
                Names = new string[0];
                Types = new string[0];
                LinkIds = new string[0];
                MotionPrefix = string.Empty;
            }

            public override string ToString()
            {
                string allNames = string.Empty;
                for (int i = 0;  i < Names.Length; i++)
                {
                    allNames += Names[i];
                    allNames += (i == Names.Length - 1) ? " " : ", ";
                }
                
                string allTypes = string.Empty;
                for(int i = 0;i < Types.Length; i++)
                {
                    allTypes += Types[i];
                    allTypes += (i == Types.Length -1) ? " " : ", ";
                }

                string allLinkIds = string.Empty;
                for(int i =0;i < LinkIds.Length; i++)
                {
                    allLinkIds += LinkIds[i];
                    allLinkIds += (i == LinkIds.Length - 1) ? " " : ", ";
                }

                return (string.IsNullOrEmpty(RemoteId) ? "" : ("Remote Id " + RemoteId + " ,")) + "Path " + this.Path + ", Names " + allNames + ", Types " + allTypes + ", linkIds " + allLinkIds;
            }
        }
        
        public class JsonLightData
        {            
            public const string linkIdStr = "link_id";
            public const string nameStr = "name";
            public const string locStr = "loc";
            public const string rotStr = "rot";
            public const string scaStr = "sca";
            public const string activestr = "active";
            public const string colorStr = "color";
            public const string multStr = "multiplier";
            public const string typeStr = "type";
            public const string rangeStr = "range";
            public const string angleStr = "angle";
            public const string falloffStr = "falloff";
            public const string attStr = "attenuation";
            public const string inSqStr = "inverse_square";
            public const string traStr = "transmission";
            public const string istubeStr = "is_tube";
            public const string tubeLenStr = "tube_length";
            public const string tubeRadStr = "tube_radius";
            public const string tubeRadSoftStr = "tube_soft_radius";
            public const string isRectStr = "is_rectangle";
            public const string rectStr = "rect";
            public const string shadowStr = "cast_shadow";
            public const string darkStr = "darkness";
            public const string framesStr = "frame_count";

            [JsonProperty(linkIdStr)]
            public string LinkId { get; set; }
            [JsonProperty(nameStr)]
            public string Name { get; set; }
            [JsonProperty(locStr)]
            public float[] loc { get; set; }
            [JsonIgnore]
            public Vector3 Pos { get { return this.GetPosition(); } }
            [JsonProperty(rotStr)]
            public float[] rot { get; set; }
            [JsonIgnore]
            public Quaternion Rot { get { return this.GetRotation(); } }
            [JsonProperty(scaStr)]
            public float[] sca { get; set; }
            [JsonIgnore]
            public Vector3 Scale { get { return this.GetScale(); } }
            [JsonProperty(activestr)]
            public bool Active { get; set; }
            [JsonProperty(colorStr)]
            public float[] color { get; set; }
            [JsonIgnore]
            public Color Color { get { return this.GetColor(); } }
            [JsonProperty(multStr)]
            public float Multiplier { get; set; }
            [JsonProperty(typeStr)]
            public string Type { get; set; }
            [JsonProperty(rangeStr)]
            public float Range { get; set; }
            [JsonProperty(angleStr)]
            public float Angle { get; set; }
            [JsonProperty(falloffStr)]
            public float Falloff { get; set; }
            [JsonProperty(attStr)]
            public float Attenuation { get; set; }
            [JsonProperty(inSqStr)]
            public bool InverseSquare { get; set; }
            [JsonProperty(traStr)]
            public bool Transmission { get; set; }
            [JsonProperty(istubeStr)]
            public bool IsTube { get; set; }
            [JsonProperty(tubeLenStr)]
            public float TubeLength { get; set; }
            [JsonProperty(tubeRadStr)]
            public float TubeRadius { get; set; }
            [JsonProperty(tubeRadSoftStr)]
            public float TubeSoftRadius { get; set; }
            [JsonProperty(isRectStr)]
            public bool IsRect { get; set; }
            [JsonProperty(rectStr)]
            public float[] rect { get; set; }
            [JsonProperty(shadowStr)]
            public bool CastShadow { get; set; }
            [JsonProperty(darkStr)]
            public float Darkness { get; set; }
            [JsonProperty(framesStr)]
            public int FrameCount { get; set; }


            // Animated properties (determined by the importer - repackaged here to ease use of <LightProxy> setup
            public const string posDltStr = "pos_delta";
            public const string rotDltStr = "rot_delta";
            public const string scaDltStr = "sca_delta";
            public const string actDltStr = "act_delta";
            public const string colDltStr = "col_delta";
            public const string mulDltStr = "mul_delta";
            public const string ranDltStr = "ran_delta";
            public const string angDltStr = "ang_delta";
            public const string falDltStr = "fal_delta";
            public const string attDltStr = "att_delta";
            public const string darDltStr = "dar_delta";

            [JsonProperty(posDltStr)]
            public bool pos_delta { get; set; }
            [JsonProperty(rotDltStr)]
            public bool rot_delta { get; set; }
            [JsonProperty(scaDltStr)]
            public bool scale_delta { get; set; }
            [JsonProperty(actDltStr)]
            public bool active_delta { get; set; }
            [JsonProperty(colDltStr)]
            public bool color_delta { get; set; }
            [JsonProperty(mulDltStr)]
            public bool mult_delta { get; set; }
            [JsonProperty(ranDltStr)]
            public bool range_delta { get; set; }
            [JsonProperty(angDltStr)]
            public bool angle_delta { get; set; }
            [JsonProperty(falDltStr)]
            public bool fall_delta { get; set; }
            [JsonProperty(attDltStr)]
            public bool att_delta { get; set; }
            [JsonProperty(darDltStr)]
            public bool dark_delta { get; set; }

            public JsonLightData()
            {
                this.LinkId = string.Empty;
                this.Name = string.Empty;
                this.loc = new float[0];
                this.rot = new float[0];
                this.sca = new float[0];
                this.Active = false;
                this.color = new float[0];
                this.Multiplier = 0f;
                this.Type = string.Empty;
                this.Range = 0f;
                this.Angle = 0f;
                this.Falloff = 0f;
                this.Attenuation = 0f;
                this.InverseSquare = false;
                this.Transmission = false;
                this.IsTube = false;
                this.TubeLength = 0f;
                this.TubeRadius = 0f;
                this.TubeSoftRadius = 0f;
                this.IsRect = false;
                this.rect = new float[0];
                this.CastShadow = false;
                this.Darkness = 0f;
                this.FrameCount = 0;

                this.pos_delta = false;
                this.rot_delta = false;
                this.scale_delta = false;
                this.active_delta = false;
                this.color_delta = false;
                this.mult_delta = false;
                this.range_delta = false;
                this.angle_delta = false;
                this.fall_delta = false;
                this.att_delta = false;
                this.dark_delta = false;
            }

            public Vector3 GetPosition()
            {
                return new Vector3(-loc[0], loc[2], -loc[1]) * 0.01f;
            }

            public Quaternion GetRotation()
            {                
                Quaternion unCorrected = new Quaternion(rot[0], -rot[2], rot[1], rot[3]);
                Quaternion cameraCorrection = Quaternion.Euler(90f, -180f, 0f);
                Quaternion corrected = unCorrected * cameraCorrection;
                return corrected;
            }

            public Vector3 GetScale()
            {
                return new Vector3(sca[0], sca[1], sca[2]);
            }

            public Color GetColor()
            {                
                return new Color(color[0], color[1], color[2]);
            }

        }

        public class DeserializedLightFrames
        {
            /* 4 byte floats
             * frame_bytes = struct.pack("!fI?fffffffffffffffffff",
                                     time,
                                     frame,
                                     light_data["active"], # 0 or 1 char  - 1 byte
                                     light_data["loc"][0],
                                     light_data["loc"][1],
                                     light_data["loc"][2],
                                     light_data["rot"][0],
                                     light_data["rot"][1],
                                     light_data["rot"][2],
                                     light_data["rot"][3],
                                     light_data["sca"][0],
                                     light_data["sca"][1],
                                     light_data["sca"][2],
                                     light_data["color"][0],
                                     light_data["color"][1],
                                     light_data["color"][2],
                                     light_data["multiplier"],
                                     light_data["range"],
                                     light_data["angle"],
                                     light_data["falloff"],
                                     light_data["attenuation"],
                                     light_data["darkness"]) 
             */

            public int time { get; set; }
            public float Time { get {  return this.GetSeconds(); } }
            public int Frame { get; set; }
            public bool Active { get; set; }
            public float PosX { get; set; }
            public float PosY { get; set; }
            public float PosZ { get; set; }
            public Vector3 Pos { get { return this.GetPosition(); } }
            public float RotX { get; set; }
            public float RotY { get; set; }
            public float RotZ { get; set; }
            public float RotW { get; set; }
            public Quaternion Rot { get { return this.GetRotation(); } }
            public float ScaleX { get; set; }
            public float ScaleY { get; set; }
            public float ScaleZ { get; set; }
            public Vector3 Scale { get { return this.GetScale(); } }
            public float ColorR { get; set; }
            public float ColorG { get; set; }
            public float ColorB { get; set; }
            public Color Color { get { return this.GetColor(); } }
            public float Multiplier { get; set; }
            public float Range { get; set; }
            public float Angle { get; set; }
            public float Falloff { get; set; }
            public float Attenuation { get; set; }
            public float Darkness { get; set; }


            public const int FRAME_BYTE_COUNT = 85;
            public DeserializedLightFrames(byte[] data)
            {                
                time = GetCurrentEndianWord(ExtractBytes(data, 0, 4), SourceEndian.BigEndian);
                Frame = GetCurrentEndianWord(ExtractBytes(data, 4, 4), SourceEndian.BigEndian);
                Active = ByteToBool(ExtractBytes(data, 8, 1));
                PosX = GetCurrentEndianFloat(ExtractBytes(data, 9, 4), SourceEndian.BigEndian);
                PosY  = GetCurrentEndianFloat(ExtractBytes(data, 13, 4), SourceEndian.BigEndian);
                PosZ = GetCurrentEndianFloat(ExtractBytes(data, 17, 4), SourceEndian.BigEndian);
                RotX = GetCurrentEndianFloat(ExtractBytes(data, 21, 4), SourceEndian.BigEndian);
                RotY = GetCurrentEndianFloat(ExtractBytes(data, 25, 4), SourceEndian.BigEndian);
                RotZ = GetCurrentEndianFloat(ExtractBytes(data, 29, 4), SourceEndian.BigEndian);
                RotW = GetCurrentEndianFloat(ExtractBytes(data, 33, 4), SourceEndian.BigEndian);
                ScaleX = GetCurrentEndianFloat(ExtractBytes(data, 37, 4), SourceEndian.BigEndian);
                ScaleY = GetCurrentEndianFloat(ExtractBytes(data, 41, 4), SourceEndian.BigEndian);
                ScaleZ = GetCurrentEndianFloat(ExtractBytes(data, 45, 4), SourceEndian.BigEndian);
                ColorR = GetCurrentEndianFloat(ExtractBytes(data, 49, 4), SourceEndian.BigEndian);
                ColorG = GetCurrentEndianFloat(ExtractBytes(data, 53, 4), SourceEndian.BigEndian);
                ColorB = GetCurrentEndianFloat(ExtractBytes(data, 57, 4), SourceEndian.BigEndian);
                Multiplier = GetCurrentEndianFloat(ExtractBytes(data, 61, 4), SourceEndian.BigEndian);
                Range = GetCurrentEndianFloat(ExtractBytes(data, 65, 4), SourceEndian.BigEndian);
                Angle = GetCurrentEndianFloat(ExtractBytes(data, 69, 4), SourceEndian.BigEndian);
                Falloff = GetCurrentEndianFloat(ExtractBytes(data, 73, 4), SourceEndian.BigEndian);
                Attenuation = GetCurrentEndianFloat(ExtractBytes(data, 77, 4), SourceEndian.BigEndian);
                Darkness = GetCurrentEndianFloat(ExtractBytes(data, 81, 4), SourceEndian.BigEndian);
            }
            
            public float GetSeconds()
            {
                return (float)time / 6000f;
            }

            public Vector3 GetPosition()
            {
                return new Vector3(-PosX, PosZ, -PosY) * 0.01f;
            }

            public Quaternion GetRotation()
            {
                Quaternion unCorrected = new Quaternion(RotX, -RotZ, RotY, RotW);
                Quaternion cameraCorrection = Quaternion.Euler(90f, -180f, 0f);
                Quaternion corrected = unCorrected * cameraCorrection;
                return corrected;
            }

            public Vector3 GetScale()
            {
                return new Vector3(ScaleX, ScaleY, ScaleZ);
            }

            public Color GetColor()
            {                
                return new Color(ColorR, ColorG, ColorB);
            }

            public override string ToString()
            {
                return "Time: " + this.Time.ToString() + ", Frame: " + this.Frame.ToString() + ", Active: " + this.Active.ToString() + ", Pos " + this.PosX.ToString() + ", " + this.PosY.ToString() + ", " + this.PosZ.ToString() + ", Rot " + this.RotX.ToString() + ", " + this.RotY.ToString() + ", " + this.RotZ.ToString() + ", " + this.RotW.ToString() + ", Col: " + this.ColorR.ToString() + ", " + this.ColorG.ToString() + ", " + this.ColorB.ToString() + ", " + this.ColorG.ToString() + ", Mult: " + this.Multiplier.ToString() + ", Range " + this.Range.ToString() + ", Angle " + this.Angle.ToString() + ", Falloff: " + this.Falloff.ToString() + ", Att: " + this.Attenuation.ToString() + ", Dark: " + this.Darkness.ToString();
            }
        }

        public class JsonCameraData
        {
            public const string linkIdStr = "link_id";
            public const string nameStr = "name";
            public const string locStr = "loc";
            public const string rotStr = "rot";
            public const string scaStr = "sca";
            public const string fovStr = "fov";
            public const string fitStr = "fit";
            public const string widthStr = "width";
            public const string heightStr = "height";
            public const string focalStr = "focal_length";
            public const string farStr = "far_clip";
            public const string nearStr = "near_clip";
            public const string posStr = "pos";
            public const string dofEnaStr = "dof_enable";
            public const string dofWeightStr = "dof_weight";
            public const string dofDecayStr = "dof_decay";
            public const string dofFocusStr = "dof_focus";
            public const string dofRanStr = "dof_range";
            public const string dofFarBlStr = "dof_far_blur";
            public const string dofNearBlStr = "dof_near_blur";
            public const string dofFarTranStr = "dof_far_transition";
            public const string dofNearTranStr = "dof_near_transition";
            public const string dofMinBlendStr = "dof_min_blend_distance";
            public const string framesStr = "frame_count";

            [JsonProperty(linkIdStr)]
            public string LinkId { get; set; }
            [JsonProperty(nameStr)]
            public string Name { get; set; }
            [JsonProperty(locStr)]
            public float[] loc { get; set; }
            [JsonIgnore]
            public Vector3 Pos { get { return this.GetPosition(); } }
            [JsonProperty(rotStr)]
            public float[] rot { get; set; }
            [JsonIgnore]
            public Quaternion Rot { get { return this.GetRotation(); } }
            [JsonProperty(scaStr)]
            public float[] sca { get; set; }
            [JsonIgnore]
            public Vector3 Scale { get { return this.GetScale(); } }
            [JsonProperty(fovStr)]
            public float Fov { get; set; }
            [JsonProperty(fitStr)]
            public string Fit { get; set; }            
            [JsonProperty(widthStr)]
            public float Width { get; set; }
            [JsonProperty(heightStr)]
            public float Height { get; set; }
            [JsonProperty(focalStr)]
            public float FocalLength { get; set; }
            [JsonProperty(farStr)]
            public float FarClip { get; set; }
            [JsonProperty(nearStr)]
            public float NearClip { get; set; }
            [JsonProperty(posStr)]
            public float[] pos { get; set; }
            [JsonIgnore]
            public Vector3 PivotPosition { get { return this.GetPivot(); } }
            [JsonProperty(dofEnaStr)]
            public bool DofEnable { get; set; }
            [JsonProperty(dofWeightStr)]
            public float DofWeight { get; set; }
            [JsonProperty(dofDecayStr)]
            public float DofDecay { get; set; }
            [JsonProperty(dofFocusStr)]
            public float DofFocus { get; set; }
            [JsonProperty(dofRanStr)]
            public float DofRange { get; set; }
            [JsonProperty(dofFarBlStr)]
            public float DofFarBlur { get; set; }
            [JsonProperty(dofNearBlStr)]
            public float DofNearBlur { get; set; }
            [JsonProperty(dofFarTranStr)]
            public float DofFarTransition { get; set; }
            [JsonProperty(dofNearTranStr)]
            public float DofNearTransition { get; set; }
            [JsonProperty(dofMinBlendStr)]
            public float DofMinBlendDist { get; set; }
            [JsonProperty(framesStr)]
            public int FrameCount { get; set; }


            // Animated properties (determined by the importer - repackaged here to ease use of <CameraProxy> setup
            public const string dofDltStr = "dof_delta";
            public const string fovDltStr = "fov_delta";

            [JsonProperty(dofDltStr)]
            public bool dof_delta { get; set; }
            [JsonProperty(fovDltStr)]
            public bool fov_delta { get; set; }

            public JsonCameraData()
            {
                this.LinkId = string.Empty;
                this.Name = string.Empty;
                this.loc = new float[0];
                this.rot = new float[0];
                this.sca = new float[0];
                this.Fov = 0f;
                this.Fit = string.Empty;
                this.Width = 0f;
                this.Height = 0f;
                this.FocalLength = 0f;
                this.FarClip = 0f;
                this.NearClip = 0f;
                this.pos = new float[0];
                this.DofEnable = false;
                this.DofWeight = 0f;
                this.DofDecay = 0f;
                this.DofFocus = 0f;
                this.DofRange = 0f;
                this.DofFarBlur = 0f;
                this.DofNearBlur = 0f;
                this.DofFarTransition = 0f;
                this.DofNearTransition = 0f;
                this.DofMinBlendDist = 0f;
                this.FrameCount = 0;

                this.dof_delta = false;
                this.fov_delta = false;
            }

            public Vector3 GetPosition()
            {
                return new Vector3(-loc[0], loc[2], -loc[1]) * 0.01f;
            }

            public Quaternion GetRotation()
            {
                Quaternion unCorrected = new Quaternion(rot[0], -rot[2], rot[1], rot[3]);
                Quaternion cameraCorrection = Quaternion.Euler(90f, -180f, 0f);
                Quaternion corrected = unCorrected * cameraCorrection;
                return corrected;
            }

            public Vector3 GetScale()
            {
                return new Vector3(sca[0], sca[1], sca[2]);
            }

            public Vector3 GetPivot()
            {
                return new Vector3(-pos[0], pos[2], -pos[1]) * 0.01f;
            }

        }
                
        public class DeserializedCameraFrames
        {
            /* 86 bytes
            frame_bytes = struct.pack("!IIfffffffffff?fffffff?",
                                time,
                                frame,
                                camera_data["loc"][0],
                                camera_data["loc"][1],
                                camera_data["loc"][2],
                                camera_data["rot"][0],
                                camera_data["rot"][1],
                                camera_data["rot"][2],
                                camera_data["rot"][3],
                                camera_data["sca"][0],
                                camera_data["sca"][1],
                                camera_data["sca"][2],
                                camera_data["focal_length"],
                                camera_data["dof_enable"],
                                camera_data["dof_focus"], # Focus Distance
                                camera_data["dof_range"], # Perfect Focus Range
                                camera_data["dof_far_blur"],
                                camera_data["dof_near_blur"],
                                camera_data["dof_far_transition"],
                                camera_data["dof_near_transition"],
                                camera_data["dof_min_blend_distance"], 
                                camera_data["fov"]), # Blur Edge Sampling Scale,
                                camera_data["active"])
            */

            public int time { get; set; }
            public float Time { get { return this.GetSeconds(); } }
            public int Frame { get; set; }
            public float PosX { get; set; }
            public float PosY { get; set; }
            public float PosZ { get; set; }
            public Vector3 Pos { get { return this.GetPosition(); } }
            public float RotX { get; set; }
            public float RotY { get; set; }
            public float RotZ { get; set; }
            public float RotW { get; set; }
            public Quaternion Rot { get { return this.GetRotation(); } }
            public float ScaleX { get; set; }
            public float ScaleY { get; set; }
            public float ScaleZ { get; set; }
            public Vector3 Scale { get { return this.GetScale(); } }
            public float FocalLength {  get; set; }
            public bool DofEnable {  get; set; }
            public float DofFocus { get; set; }
            public float DofRange {  get; set; }
            public float DofFarBlur {  get; set; }
            public float DofNearBlur {  get; set; }
            public float DofFarTransition {  get; set; }
            public float DofNearTransition {  get; set; }
            public float DofMinBlendDistance {  get; set; }
            public float FieldOfView {  get; set; }
            public bool IsActive { get; set; }

            public const int FRAME_BYTE_COUNT = 86;
            public DeserializedCameraFrames(byte[] data)
            {
                time = GetCurrentEndianWord(ExtractBytes(data, 0, 4), SourceEndian.BigEndian);
                Frame = GetCurrentEndianWord(ExtractBytes(data, 4, 4), SourceEndian.BigEndian);
                PosX = GetCurrentEndianFloat(ExtractBytes(data, 8, 4), SourceEndian.BigEndian);
                PosY = GetCurrentEndianFloat(ExtractBytes(data, 12, 4), SourceEndian.BigEndian);
                PosZ = GetCurrentEndianFloat(ExtractBytes(data, 16, 4), SourceEndian.BigEndian);
                RotX = GetCurrentEndianFloat(ExtractBytes(data, 20, 4), SourceEndian.BigEndian);
                RotY = GetCurrentEndianFloat(ExtractBytes(data, 24, 4), SourceEndian.BigEndian);
                RotZ = GetCurrentEndianFloat(ExtractBytes(data, 28, 4), SourceEndian.BigEndian);
                RotW = GetCurrentEndianFloat(ExtractBytes(data, 32, 4), SourceEndian.BigEndian);
                ScaleX = GetCurrentEndianFloat(ExtractBytes(data, 36, 4), SourceEndian.BigEndian);
                ScaleY = GetCurrentEndianFloat(ExtractBytes(data, 40, 4), SourceEndian.BigEndian);
                ScaleZ = GetCurrentEndianFloat(ExtractBytes(data, 44, 4), SourceEndian.BigEndian);
                FocalLength = GetCurrentEndianFloat(ExtractBytes(data, 48, 4), SourceEndian.BigEndian);
                DofEnable = ByteToBool(ExtractBytes(data, 52, 1));
                DofFocus = GetCurrentEndianFloat(ExtractBytes(data, 53, 4), SourceEndian.BigEndian);
                DofRange = GetCurrentEndianFloat(ExtractBytes(data, 57, 4), SourceEndian.BigEndian);
                DofFarBlur = GetCurrentEndianFloat(ExtractBytes(data, 61, 4), SourceEndian.BigEndian);
                DofNearBlur = GetCurrentEndianFloat(ExtractBytes(data, 65, 4), SourceEndian.BigEndian);
                DofFarTransition = GetCurrentEndianFloat(ExtractBytes(data, 69, 4), SourceEndian.BigEndian);
                DofNearTransition = GetCurrentEndianFloat(ExtractBytes(data, 73, 4), SourceEndian.BigEndian);
                DofMinBlendDistance = GetCurrentEndianFloat(ExtractBytes(data, 77, 4), SourceEndian.BigEndian);
                FieldOfView = GetCurrentEndianFloat(ExtractBytes(data, 81, 4), SourceEndian.BigEndian);
                IsActive = ByteToBool(ExtractBytes(data, 85, 1));
            }

            public float GetSeconds()
            {
                return (float)time / 6000f;
            }

            public Vector3 GetPosition()
            {
                return new Vector3(-PosX, PosZ, -PosY) * 0.01f;
            }

            public Quaternion GetRotation()
            {
                Quaternion unCorrected = new Quaternion(RotX, -RotZ, RotY, RotW);
                Quaternion cameraCorrection = Quaternion.Euler(90f, -180f, 0f);
                Quaternion corrected = unCorrected * cameraCorrection;
                return corrected;
            }

            public Vector3 GetScale()
            {
                return new Vector3(ScaleX, ScaleY, ScaleZ);
            }
        }

        static bool ByteToBool(byte[] data)
        {
            if ( data.Length != 1) { Debug.LogWarning("Only byte[] of 1 byte accepted as input."); return false; }
            return (data[0] == 1);
        }

        public class JsonUpdateReplace // UPDATE_REPLACE: (108) - receive updated character or prop from server
        {
            public const string remoteIdStr = "remote_id";          // remote_id: string zipile name for remote transmission
            public const string pathStr = "path";           // path: String - file path of fbx export file
            public const string nameString = "name";        // name: String - prop name
            public const string typeStr = "type";           // type: String - actor type (AVATAR, PROP, LIGHT, CAMERA)
            public const string linkIdStr = "link_id";      // link_id: String - actor link id code
            public const string replaceStr = "replace";     // replace: Bool - replace entire actor (True) or just the selected parts (False) listed in 'objects'
            public const string objectsStr = "objects";     // objects: String list - list of object names to replace in the actor with the ones in this fbx export

            // Notes: Logistical nightmare.

            [JsonProperty(remoteIdStr)]
            public string RemoteId { get; set; }
            [JsonProperty(pathStr)]
            public string Path { get; set; }
            [JsonProperty(nameString)]
            public string Name { get; set; }
            [JsonProperty(typeStr)]
            public string Type { get; set; }
            [JsonProperty(linkIdStr)]
            public string LinkId { get; set; }
            [JsonProperty(replaceStr)]
            public bool Replace { get; set; }
            [JsonProperty(objectsStr)]
            public List<string> Objects { get; set; }

            public JsonUpdateReplace()
            {
                RemoteId = string.Empty;
                Path = string.Empty;
                Name = string.Empty;
                Type = string.Empty;
                LinkId = string.Empty;
                Replace = false;
                Objects = new List<string>();
            }

            public override string ToString()
            {
                string objList = string.Empty;
                foreach (string obj in Objects)
                {                    
                    objList += (obj + " ");
                }
                return "Path " + this.Path + ", Name " + this.Name + ", Type " + this.Type + ", Link Id " + this.LinkId + ", Replace " + this.Replace + ", Objects " + objList;
            }
        }

        public class JsonMotion // MOTION: (240) - receive animation export for actor from server
        {
            public const string remoteIdStr = "remote_id";          // remote_id: string zipile name for remote transmission
            public const string pathStr = "path";                   // path: String - file path of fbx export file
            public const string nameString = "name";                // name: String - prop name
            public const string typeStr = "type";                   // type: String - actor type (AVATAR, PROP, LIGHT, CAMERA) - Should be PROP (used to verify)
            public const string linkIdStr = "link_id";              // link_id: String - actor link id code
            public const string fpsStr = "Fps";                     // fps: Int - frame rate of iclone project
            public const string startTimeStr = "start_time";        // start_time: Float - Start time of animation (seconds)
            public const string endTimeStr = "end_time";            // end_time: Float - End time of animation (seconds)
            public const string timeStr = "time";                   // time: Float - current time in iClone scene (seconds)
            public const string frameStr = "frame";                 // frame: Int - current frame in iClone scene
            public const string motionPrefixStr = "motion_prefix";  // motion_prefix: String - name to prefix animation names

            [JsonProperty(remoteIdStr)]
            public string RemoteId { get; set; }
            [JsonProperty(pathStr)]
            public string Path { get; set; }
            [JsonProperty(nameString)]
            public string Name { get; set; }
            [JsonProperty(typeStr)]
            public string Type { get; set; }
            [JsonProperty(linkIdStr)]
            public string LinkId { get; set; }
            [JsonProperty(fpsStr)]
            public float Fps { get; set; }
            [JsonProperty(startTimeStr)]
            public float StartTime { get; set; }
            [JsonProperty(endTimeStr)]
            public float EndTime { get; set; }
            [JsonProperty(timeStr)]
            public float Time { get; set; }
            [JsonProperty(frameStr)]
            public int Frame { get; set; }
            [JsonProperty(motionPrefixStr)]
            public string MotionPrefix { get; set; }

            public JsonMotion()
            {
                RemoteId = string.Empty;
                Path = string.Empty;
                Name = string.Empty;
                Type = string.Empty;
                LinkId = string.Empty;
                Fps = 0f;
                StartTime = 0f;
                EndTime = 0f;
                Time = 0f;
                Frame = 0;
                MotionPrefix = string.Empty;
            }

            public override string ToString()
            {
                return "Path " + this.Path + ", Name " + this.Name + ", Type " + this.Type + ", Link Id " + this.LinkId + ", Fps " + Fps + ", Start Time " + this.StartTime + ", End Time " + this.EndTime + ", Time " + this.Time + ", Frame " + this.Frame + ", Motion Prefix " + this.MotionPrefix;
            }
        }

        public class JsonLighting // LIGHTING: (230) - Sync lights with iClone/CC
        {
            // Much data
            // Notes: TBD, stop gap solution for sending lights to Blender. Needs to send lights as full animated actors.
            // Ignore for now.
        }

        public class JsonCameraSync // CAMERA_SYNC: (231) - sync viewport camera with iClone/CC
        {
            public const string linkIdStr = "link_id";      // link_id: String - actor link id code
            public const string nameString = "name";        // name: String - prop name
            public const string locStr = "loc";             // loc: Float list - (Position Vector) [x, y, z]
            public const string rotStr = "rot";             // rot: Float list - (Rotation Quaternion) [x, y, z, w]
            public const string scaStr = "sca";             // sca: Float list - (Scale Vector) [x, y, z]
            public const string fovStr = "fov";             // fov: Float - Camera FOV
            public const string fitStr = "fit";             // fit: String - Camera fit FOV to viewport (HORIZONTAL, VERTICAL)
            public const string widthStr = "width";         // width: Float - Apeture width
            public const string heightStr = "height";       // height: Float - Apeture height
            public const string focalStr = "focal_length";  // focal_length: Float - Focal length of lens
            public const string targetStr = "target";       // target: Float list [x, y, z] average pos of selection (i.e. iclone camera pivot)    
            /*
                // I have no idea what the following are, but I send them anyway
                
                min: Float list - (Min Bounds Vector) [x, y, z]
                max: Float list - (Max Bounds Vector) [x, y, z]
                center: Float list - (Centre Bounds Vector) [x, y, z]
                pos: Float list - (Pivot Vector) [x, y, z]
                pivot: Float list - (Position Vector) [x,y,z] - world position the iClone camera is looking at / pivoting around

            */

            [JsonProperty(linkIdStr)]
            public string LinkId { get; set; }
            [JsonProperty(nameString)]
            public string Name { get; set; }
            [JsonProperty(locStr)]
            public List<float> loc { get; set; }
            [JsonIgnore]
            public Vector3 Position { get { return new Vector3(loc[0], loc[1], loc[2]); } }
            [JsonProperty(rotStr)]
            public List<float> rot { get; set; }
            [JsonIgnore]
            public Quaternion Rotation { get { return new Quaternion(rot[0], rot[1], rot[2], rot[3]); } }
            [JsonProperty(scaStr)]
            public List<float> sca { get; set; }
            [JsonIgnore]
            public Vector3 LocalScale { get { return new Vector3(sca[0], sca[1], sca[2]); } }
            [JsonProperty(fovStr)]
            public float Fov { get; set; }
            [JsonProperty(fitStr)]
            public string Fit { get; set; }
            [JsonProperty(widthStr)]
            public float Width { get; set; }
            [JsonProperty(heightStr)]
            public float Height { get; set; }
            [JsonProperty(focalStr)]
            public float FocalLength { get; set; }
            [JsonProperty(targetStr)]
            public List<float> target { get; set; }
            public Vector3 Target { get { return new Vector3(target[0], target[1], target[2]); } }

            public JsonCameraSync()
            {
                LinkId = string.Empty;
                Name = string.Empty;
                loc = new List<float>();
                rot = new List<float>();
                sca = new List<float>();
                Fov = 0f;
                Fit = string.Empty;
                Width = 0f;
                Height = 0f;
                FocalLength = 0f;
                target = new List<float>();
            }

            public override string ToString()
            {
                return "Link Id " + this.LinkId + ", Name " + this.Name + ", Location " + this.loc[0] + ", " + this.loc[1] + ", " + this.loc[2] + " Rotation " + this.rot[0] + ", " + this.rot[1] + ", " + this.rot[2] + ", " + this.rot[3] + ", Scale " + this.sca[0] + ", " + this.sca[1] + ", " + this.sca[2] + ", Fov " + this.Fov + ", Fit " + this.Fit + ", Width " + this.Width + ", Height" + this.Height + ", Focal Length " + this.FocalLength + ", Target " + this.Target[0] + ", " + this.Target[1] + ", " + this.Target[2];
            }
        }

        public class JsonFrameSync // FRAME_SYNC: (232) - Sync frame with iClone/CC
        {
            public const string fpsStr = "Fps";                     // fps: Int - frame rate of iclone project
            public const string startTimeStr = "start_time";        // start_time: Float - Start time of animation (seconds)
            public const string endTimeStr = "end_time";            // end_time: Float - End time of animation (seconds)
            public const string currentTimeStr = "current_time";    // current_time: Float - current time in iClone scene (seconds)
            public const string startFrameStr = "start_frame";      // start_frame: Int - Start frame of animation
            public const string endFrameStr = "end_frame";          // end_frame: Int - End frame of animation
            public const string currentFrameStr = "current_frame";  // current_frame: Int - current frame in iClone scene

            [JsonProperty(fpsStr)]
            public float Fps { get; set; }  // originally int but json contains "60.0" fps causing an exception
            [JsonProperty(startTimeStr)]
            public float StartTime { get; set; }
            [JsonProperty(endTimeStr)]
            public float EndTime { get; set; }
            [JsonProperty(currentTimeStr)]
            public float CurrentTime { get; set; }
            [JsonProperty(startFrameStr)]
            public int StartFrame { get; set; }
            [JsonProperty(endFrameStr)]
            public int EndFrame { get; set; }
            [JsonProperty(currentFrameStr)]
            public int CurrentFrame { get; set; }

            public JsonFrameSync()
            {
                Fps = 0;
                StartTime = 0f;
                EndTime = 0f;
                CurrentTime = 0f;
                StartFrame = 0;
                EndFrame = 0;
                CurrentFrame = 0;
            }

            public override string ToString()
            {
                return "Fps " + this.Fps + ", Start Time " + this.StartTime + ", End Time " + this.EndTime + ", Current Time " + this.CurrentTime + ", Start Frame " + this.StartFrame + ", End Frame " + this.EndFrame + ", Current Frame " + this.CurrentFrame;
            }
        }

        public class JsonRequest
        {
            /*
             "type": "SCENE",
             "actors": [
                {
                  "name": "Eddy",
                  "type": "AVATAR",
                  "link_id": "6224490459904"
                },
                {
                  "name": "Floor_Wood_A",
                  "type": "PROP",
                  "link_id": "6224490412288"
                }
              ]
             */

            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("actors")]
            public List<JsonRequestActors> Actors { get; set; }

            public JsonRequest(string type)
            {
                Type = type;
                Actors = new List<JsonRequestActors>();
            }
        }

        public class JsonRequestActors
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("link_id")]
            public string LinkId { get; set; }

            [JsonProperty("confirm")]
            public bool Confirm { get; set; }

            [JsonProperty("skinned")]
            public bool Skinned { get; set; }

            public JsonRequestActors(string name, string type, string linkId, bool confirm, bool skinned)
            {
                Name = name;
                Type = type;
                LinkId = linkId;
                Confirm = confirm;
                Skinned = skinned;
            }
        }

        public class QueueItem
        {
            public OpCodes OpCode { get; set; }

            public string Name { get; set; }
            public Exchange Exchange { get; set; }
            public DateTime EntryTime { get; set; }
            public bool Processed { get; set; }
            public bool Debug {  get; set; }
            public JsonNotify Notify { get; set; }
            public JsonHello Hello { get; set; }
            public JsonCharacter Character { get; set; }
            public JsonCharacterUpdate CharacterUpdate { get; set; }
            public JsonProp Prop { get; set; }
            public JsonStaging Staging { get; set; }
            public JsonUpdateReplace UpdateReplace { get; set; }
            public JsonMotion Motion { get; set; }
            public JsonLighting Lighting { get; set; }
            public JsonCameraSync CameraSync { get; set; }
            public JsonFrameSync FrameSync { get; set; }
            public JsonRequest Request { get; set; }
            public string RemoteId {  get; set; }

            public QueueItem(OpCodes opCode, Exchange direction)
            {
                OpCode = opCode;
                Name = string.Empty;
                Exchange = direction;
                EntryTime = DateTime.Now.ToLocalTime();
                Hello = null;
                Character = null;
                CharacterUpdate = null;
                Prop = null;
                UpdateReplace = null;
                Motion = null;
                Staging = null;
                CameraSync = null;
                FrameSync = null;
            }
        }

        #endregion Class data

        #region Architecture agnostic byte ordering
        public enum SourceEndian
        {
            LittleEndian = 0,
            BigEndian = 1
        }

        public static float GetCurrentEndianFloat(byte[] data, SourceEndian sourceEndian)
        {
            if (data.Length != 4)
            {
                Debug.LogWarning("Only byte[] of 4 bytes accepted as input.");
                return 0f;
            }

            if (BitConverter.IsLittleEndian)
            {
                if (sourceEndian == SourceEndian.LittleEndian)
                {
                    return BitConverter.ToSingle(data, 0);
                }
                else // serverEndian == SourceEndian.BigEndian
                {
                    return BitConverter.ToSingle(ReverseByteOrder(data), 0);
                }
            }
            else
            {
                if (sourceEndian == SourceEndian.BigEndian)
                {
                    return BitConverter.ToSingle(data, 0);
                }
                else // serverEndian == SourceEndian.LittleEndian
                {
                    return BitConverter.ToSingle(ReverseByteOrder(data), 0);
                }
            }
        }

        public static Int32 GetCurrentEndianWord(byte[] data, SourceEndian sourceEndian)
        {
            if (data.Length != 4)
            {
                Debug.LogWarning("Only byte[] of 4 bytes accepted as input.");
                return 0;
            }

            if (BitConverter.IsLittleEndian)
            {
                if (sourceEndian == SourceEndian.LittleEndian)
                {
                    return BitConverter.ToInt32(data, 0);
                }
                else // serverEndian == SourceEndian.BigEndian
                {
                    return BitConverter.ToInt32(ReverseByteOrder(data), 0);
                }
            }
            else
            {
                if (sourceEndian == SourceEndian.BigEndian)
                {
                    return BitConverter.ToInt32(data, 0);
                }
                else // serverEndian == SourceEndian.LittleEndian
                {
                    return BitConverter.ToInt32(ReverseByteOrder(data), 0);
                }
            }
        }

        public static byte[] Int32ToBigEndianBytes(Int32 int32)
        {
            byte[] bytes = BitConverter.GetBytes(int32);

            if (BitConverter.IsLittleEndian)
            {
                return ReverseByteOrder(bytes);
            }
            else
            {
                return bytes;
            }
        }

        public static byte[] ReverseByteOrder(byte[] data)
        {
            byte[] reverse = new byte[data.Length];
            int n = data.Length - 1;
            for (int i = 0; i < data.Length; i++)
            {
                reverse[n--] = data[i];
            }
            return reverse;
        }
        #endregion Architecture agnostic byte ordering
                
        #region FBX extraction
        public enum FbxTypes
        {
            GameObject,
            Mesh,
            Material,
            AnimationClip,
            Avatar
        }

        static void ExtractFbx(string guid)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid); Debug.Log(assetPath);
            string assetFolder = Path.GetDirectoryName(assetPath); Debug.Log(assetFolder);

            string assetName = Path.GetFileNameWithoutExtension(assetPath); Debug.Log(assetName);
            string extractFolderName = assetName + "_fbx"; Debug.Log(extractFolderName);
            
            string fullExtractPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder(assetFolder, extractFolderName));

            Debug.Log(AssetDatabase.GUIDToAssetPath(guid) + " Extract path: " + fullExtractPath);
            UnityEngine.Object[] contents = AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GUIDToAssetPath(guid));

            Dictionary<Type, FbxTypes> types = new Dictionary<Type, FbxTypes>()
            {
                { typeof(GameObject), FbxTypes.GameObject },
                { typeof(Mesh), FbxTypes.Mesh },
                { typeof(Material), FbxTypes.Material },
                { typeof(AnimationClip), FbxTypes.AnimationClip },
                { typeof(Avatar), FbxTypes.Avatar }
            };

            List<GameObject> list = new List<GameObject>();
            foreach (UnityEngine.Object o in contents)
            {
                UnityEngine.Object obj = UnityEngine.Object.Instantiate(o);
                obj.name = o.name;
                //Debug.Log(o.name + " " + o.GetType().ToString());
                string extension = string.Empty;
                if (types.TryGetValue(o.GetType(), out FbxTypes fbxType))
                {
                    switch (fbxType)
                    {
                        case FbxTypes.GameObject:
                            {
                                GameObject g = (GameObject)obj;
                                list.Add(g);
                                break;
                            }
                        case FbxTypes.Mesh:
                            {
                                extension = ".mesh";
                                CreateDiskAsset(obj, fullExtractPath + "/" + obj.name + extension);
                                break;
                            }
                        case FbxTypes.Material:
                            {
                                extension = ".mat";
                                CreateDiskAsset(obj, fullExtractPath + "/" + obj.name + extension);
                                break;
                            }
                        case FbxTypes.AnimationClip:
                            {
                                extension = ".anim";
                                CreateDiskAsset(obj, fullExtractPath + "/" + obj.name + extension);
                                break;
                            }
                        case FbxTypes.Avatar:
                            {
                                extension = ".avatar";
                                CreateDiskAsset(obj, fullExtractPath + "/" + obj.name + extension);
                                break;
                            }
                    }
                }
            }
            // process GameObjects last

            // preprocess the list to find the gameobjects with no smr
            List<GameObject> prelist = list.FindAll(x => x.GetComponent<SkinnedMeshRenderer>() == null);
            Transform[] hierarchy = new Transform[0];
            foreach (GameObject go in prelist)
            {                
                if (go.name.iContains("bone") && go.name.iContains("root"))
                {
                    string extension = ".prefab";
                    string prefabPath = fullExtractPath + "/" + go.name + extension;
                    
                    if (go.GetComponentsInChildren<Transform>() != null)
                    {
                        GameObject p = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                        Debug.Log("Saving: " + go.name + " as hierarchy prefab. (" + prefabPath + ")");
                        hierarchy = p.GetComponentsInChildren<Transform>();
                    }
                }                
            }

            foreach (GameObject go in list)
            {
                SkinnedMeshRenderer smr = go.GetComponent<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    Material[] sharedMaterials = smr.sharedMaterials;
                    Material[] switchMaterials = new Material[sharedMaterials.Length];

                    for (int i = 0; i < sharedMaterials.Length; i++)
                    {
                        var matMatches = AssetDatabase.FindAssets(sharedMaterials[i].name, new string[] { fullExtractPath });
                        if (matMatches.Length > 0)
                        {
                            Material m = (Material)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(matMatches[0]), typeof(Material));
                            switchMaterials[i] = m;
                        }
                    }
                    smr.sharedMaterials = switchMaterials;

                    var meshMatches = AssetDatabase.FindAssets(smr.sharedMesh.name, new string[] { fullExtractPath });
                    if (meshMatches.Length > 0)
                    {
                        Mesh m = (Mesh)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(meshMatches[0]), typeof(Mesh));
                        smr.sharedMesh = m;
                    }

                    var transformMatch = hierarchy.ToList<Transform>().FirstOrDefault(x => x.name == smr.rootBone.name);
                    smr.rootBone = transformMatch;

                    string extension = ".prefab";
                    PrefabUtility.SaveAsPrefabAsset(go, fullExtractPath + "/" + go.name + extension);
                }
            }
            foreach (GameObject go in list) { DestroyImmediate(go); }

            AssetDatabase.Refresh();
        }

        static void CreateDiskAsset(UnityEngine.Object obj, string assetPath)
        {
            AssetDatabase.CreateAsset(obj, assetPath);
        }
        #endregion FBX extraction

        #region Log writer
        static string APPLICATION_DATA_PATH = string.Empty;
        static string ROOT_FOLDER = "Assets";
        static string IMPORT_PARENT = "Reallusion";
        static string IMPORT_FOLDER = "DataLink_Imports";

        static void SetupLogging()
        {
            // Unity things that cannot be accessed outside the main thread
            APPLICATION_DATA_PATH = Application.dataPath;

            string PARENT_FOLDER = Path.Combine(new string[] { ROOT_FOLDER, IMPORT_PARENT });
            if (!AssetDatabase.IsValidFolder(PARENT_FOLDER)) AssetDatabase.CreateFolder(ROOT_FOLDER, IMPORT_PARENT);
            string IMPORT_PATH = Path.Combine(new string[] { ROOT_FOLDER, IMPORT_PARENT, IMPORT_FOLDER });
            if (!AssetDatabase.IsValidFolder(IMPORT_PATH)) AssetDatabase.CreateFolder(PARENT_FOLDER, IMPORT_FOLDER);
        }

        public static void WriteIncomingLog(string dataString, bool valid)
        {
            // Logs all incoming messages from the server
            string fileName = "RL_DataLink_Server_Message_Log.txt";
            string fullSystemFilePath = Path.Combine(APPLICATION_DATA_PATH, IMPORT_PARENT, IMPORT_FOLDER, fileName);
            string fullsystemFolder = Path.Combine(APPLICATION_DATA_PATH, IMPORT_PARENT, IMPORT_FOLDER);
            string assetFilePath = Path.Combine(ROOT_FOLDER, IMPORT_PARENT, IMPORT_FOLDER, fileName);

            string beautifiedJson = string.Empty;
            if (!string.IsNullOrEmpty(dataString))
            {                
                try
                {
                    JToken parsedJson = JToken.Parse(dataString);
                    beautifiedJson = parsedJson.ToString(Formatting.Indented);
                }
                catch
                {
                    Debug.Log("JToken didn't parse");
                    beautifiedJson = dataString;
                }
            }
            else
            {
                beautifiedJson = dataString; // opportunity for a descriptive message - only STOP code will use this
            }

            string fullText = opCode.ToString() + " " + DateTime.Now.ToLocalTime().ToString() + (valid ? " VALID" : " INVALID") + Environment.NewLine + beautifiedJson + Environment.NewLine + Environment.NewLine;

            if (!Directory.Exists(fullsystemFolder))
            {
                Debug.LogWarning("Unable to write to log file (path to logfile unavailable) - Logging to console.");
                Debug.Log(fullText);
                recreateLogFolder = true;
                return;
            }

            try
            {
                if (!File.Exists(fullSystemFilePath))
                {
                    //WriteOnUpdate(fullSystemFilePath, fullText, true);

                    File.WriteAllText(fullSystemFilePath, fullText);
                    //RefreshAssetDatabase();
                }
                else
                {
                    //WriteOnUpdate(fullSystemFilePath, fullText, false);

                    File.AppendAllText(fullSystemFilePath, fullText);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Failed to write to log file: " + ex.Message);
            }
        }
        #endregion Log Writer

        #region Update worker
        static bool recreateLogFolder = false;

        private static void SetupUpdateWorker()
        {
            EditorApplication.update -= UpdateDelegate;
            EditorApplication.update += UpdateDelegate;
        }

        private static void UpdateDelegate()
        {
            if (recreateLogFolder)
            {
                Debug.LogWarning("Log folder absent - recreating");
                SetupLogging();
                recreateLogFolder = false;
            }
        }

        private static void RefreshAssetDatabase()
        {
            EditorApplication.update -= RefreshDelegate;
            EditorApplication.update += RefreshDelegate;
        }

        private static void RefreshDelegate()
        {
            EditorApplication.update -= RefreshDelegate;
            AssetDatabase.Refresh();
        }

        // Import Error Code(4) being thrown after modifying the log file
        //https://discussions.unity.com/t/onwillcreateasset-always-raise-a-import-error-code-4-warning-after-modifying-the-file/943283

        private static string fullPathToWrite = string.Empty;
        private static string contentsToWrite = string.Empty;
        private static bool createFile = false;
                
        private static void WriteOnUpdate(string path, string text, bool create = false)
        {
            EditorApplication.update -= WriterDelegate;
            EditorApplication.update += WriterDelegate;
            fullPathToWrite = path;
            contentsToWrite += text;
            createFile = create;
        }

        private static void WriterDelegate()
        {
            EditorApplication.update -= WriterDelegate;

            string importPath = fullPathToWrite.FullPathToUnityAssetPath();

            if (createFile)
            {
                File.WriteAllText(fullPathToWrite, contentsToWrite);
                if (!string.IsNullOrEmpty(importPath)) AssetDatabase.ImportAsset(importPath);
            }
            else
            {
                File.AppendAllText(fullPathToWrite, contentsToWrite);
                if (!string.IsNullOrEmpty(importPath)) AssetDatabase.ImportAsset(importPath);
            }

            fullPathToWrite = string.Empty;
            contentsToWrite = string.Empty;
            createFile = false;
        }
        #endregion Update worker
    }
}