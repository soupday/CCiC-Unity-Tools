#if PLASTIC_NEWTONSOFT_AVAILABLE
using Unity.Plastic.Newtonsoft.Json;
#else
using Newtonsoft.Json;  // com.unity.collab-proxy (plastic scm) versions prior to 1.14.12
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
using System.Drawing.Printing;
using static UnityEngine.Rendering.DebugUI.Table;

namespace Reallusion.Import
{
    public class UnityLinkManager : Editor
    {
        #region Menu options
        [MenuItem("Example/Import test")]
        static void MenuProcessImport()
        {
            ProcessImport();
        }

        [MenuItem("Example/TCP Client connect")]
        static void SendSomething()
        {            
            InitConnection();
        }

        [MenuItem("Example/TCP Client disconnect")]
        static void MenuDisconnect()
        {
            Disconnect();
        }

        [MenuItem("Example/TCP Client disconnect and stop server")]
        static void MenuDisconnectStopServer()
        {
            DisconnectAndStopServer();
        }

        [MenuItem("Example/TCP Send message")]
        static void MessageServer()
        {
            SendMessage(OpCodes.TEST);
        }

        [MenuItem("Example/Code test")]
        static void Test()
        {
            //SendMessage(OpCodes.HELLO, ClientHelloMessage());

            Debug.LogWarning(Application.productName);
        }
        #endregion Menu options

        #region Setup
        static void InitConnection()
        {
            StartQueue();
            StartClient();
            UnityLinkManagerWindow.OpenWindow();
            CleanupBeforeAssemblyReload();
        }

        #endregion Setup

        #region Client
        static TcpClient client = null;
        static NetworkStream stream = null;
        static Thread clientThread;
        static bool reconnect = false;
        static bool listening = false;
        private static readonly int chunkSize = 1024;
        
        static void StartClient()
        {
            clientThread = new Thread(new ThreadStart(ClientThread));
            clientThread.Start();
        }

        static void ClientThread()
        {
            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            int port = 9333;

            client = new TcpClient();
            try
            {
                client.Connect(ipAddress, port);
            }
            catch (Exception e)
            {
                Debug.Log(e);
                StopQueue();
                return;
            }

            string a = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            string b = ((IPEndPoint)client.Client.RemoteEndPoint).Port.ToString();
            Debug.Log("Client Connected to : " + a + ":" + b);

            stream = client.GetStream();
            reconnect = true;
            listening = true;

            byte[] receivedData = new byte[0];
            int bytesRead = 0;
            int bytesToRead = 0;
            bool gotHeader = false;
            int size = 0;
            OpCodes opCode;
                        
            while (listening)
            {
                if (!gotHeader)
                {
                    bytesToRead = 8;
                }
                else
                {
                    bytesToRead = size;
                }

                byte[] chunkBuffer = new byte[bytesToRead];

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

                try
                {
                    if (stream.CanRead)
                        bytesRead = stream.Read(chunkBuffer, 0, bytesToRead);
                }
                catch (Exception ex)
                {
                    Debug.Log(ex);
                }

                if (!gotHeader)
                {
                    Debug.Log("Getting Header.");
                    byte[] opCodeBytes = ExtractBytes(chunkBuffer, 0, 4);
                    byte[] sizeBytes = ExtractBytes(chunkBuffer, 4, 4);
                    opCode = OpCode(GetCurrentEndianWord(opCodeBytes, SourceEndian.BigEndian));
                    size = GetCurrentEndianWord(sizeBytes, SourceEndian.BigEndian);
                    Debug.Log("Op Code: " + opCode.ToString() + ", Expected Size: " + size);

                    gotHeader = true;
                }

                Array.Resize(ref receivedData, receivedData.Length + bytesRead);
                Buffer.BlockCopy(chunkBuffer, 0, receivedData, receivedData.Length - bytesRead, bytesRead);

                Debug.Log("receivedData " + receivedData.Length + ", bytesRead " + bytesRead);

                if (receivedData.Length >= size + 8)
                {
                    Debug.Log("Handling recieved data");
                    HandleRecivedData(receivedData);
                    receivedData = new byte[0];
                    gotHeader = false;                    
                }
            }
            stream.Close();
            client.Close();
        }
        #endregion Client

        #region Server messaging
        //[SerializeField]
        public static List<QueueItem> activityQueue;

        public enum Exchange
        {
            SENT,
            RECEIVED
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
            SAVE = 60,
            MORPH = 90,
            MORPH_UPDATE = 91,
            REPLACE_MESH = 95,
            MATERIALS = 96,
            CHARACTER = 100,
            CHARACTER_UPDATE = 101,
            PROP = 102,
            PROP_UPDATE = 103,
            UPDATE_REPLACE = 108,
            RIGIFY = 110,
            TEMPLATE = 200,
            POSE = 210,
            POSE_FRAME = 211,
            SEQUENCE = 220,
            SEQUENCE_FRAME = 221,
            SEQUENCE_END = 222,
            SEQUENCE_ACK = 223,
            LIGHTS = 230,
            CAMERA_SYNC = 231,
            FRAME_SYNC = 232,
            MOTION = 240,

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

            stream.Write(message.ToArray());
        }
        #endregion Server messaging

        #region Connection
        static void Disconnect()
        {
            SetConnectedTimeStamp(true);
            EditorApplication.update -= QueueDelegate;
            SendMessage(OpCodes.DISCONNECT);

            listening = false;
            reconnect = false;

            if (client.Connected && stream.CanWrite)
            {                
                stream.Close();
                client.Close();
            }
        }

        static void DisconnectAndStopServer()
        {
            SetConnectedTimeStamp(true);
            EditorApplication.update -= QueueDelegate;
            SendMessage(OpCodes.STOP);

            listening = false;
            reconnect = false;

            if (client.Connected && stream.CanWrite)
            {
                stream.Close();
                client.Close();
            }
        }

        static void CleanupBeforeAssemblyReload()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= CleanupDelegate;
            AssemblyReloadEvents.beforeAssemblyReload += CleanupDelegate;
        }

        static void CleanupDelegate()
        {
            if (reconnect)
            {
                Debug.Log("Setting up reconnect");
                SetConnectedTimeStamp();
            }
            else
            {
                Debug.LogWarning("SetConnectedTimeStamp(true)");
                SetConnectedTimeStamp(true);
            }

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

            listening = false;
            
            
            EditorApplication.update -= QueueDelegate;
            AssemblyReloadEvents.beforeAssemblyReload -= CleanupDelegate;
            Debug.LogWarning("AssemblyReloadEvents.beforeAssemblyReload done");
        }

        // Automated reconnection for assembly reloads
        [InitializeOnLoadMethod]
        static void AutoReconnect()
        {
            if (IsConnectedTimeStampWithin(new TimeSpan(0, 5, 0)))
            {
                Debug.Log("Attempting to reconnect");
                reconnect = false;
                InitConnection();
            }
        }

        const string connectPrefString = "RL_CC_Server_Disconnect_Timestamp";

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
            Debug.Log("Writing timestamp string: " + time.ToString() + " to EditorPrefs: " + connectPrefString);
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

            byte[] opCodeBytes = ExtractBytes(recievedData, 0, 4);
            byte[] sizeBytes = ExtractBytes(recievedData, 4, 4);
            OpCodes opCode = OpCode(GetCurrentEndianWord(opCodeBytes, SourceEndian.BigEndian));
            int size = GetCurrentEndianWord(sizeBytes, SourceEndian.BigEndian);

            if (recievedData.Length < size + 8)
            {
                // incomplete data 
                return;
            }

            byte[] dataBlock = ExtractBytes(recievedData, 8, recievedData.Length - 8);
            QueueItem qItem = new QueueItem(opCode, Exchange.RECEIVED);

            string jsonString = string.Empty;            
            if (size > 0) jsonString = Encoding.UTF8.GetString(dataBlock);
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
                        try { qItem.Hello = JsonConvert.DeserializeObject<JsonHello>(jsonString); } catch (Exception ex) { Debug.Log(ex); add = false; }
                        break;
                    }
                case OpCodes.NOTIFY:
                    {
                        try { qItem.Notify = JsonConvert.DeserializeObject<JsonNotify>(jsonString); } catch (Exception ex) { Debug.Log(ex); add = false; }
                        break;
                    }
                case OpCodes.CHARACTER:
                    {
                        try { qItem.Character = JsonConvert.DeserializeObject<JsonCharacter>(jsonString); } catch (Exception ex) { Debug.Log(ex); add = false; }
                        break;
                    }
                case OpCodes.CHARACTER_UPDATE:
                    {
                        try { qItem.CharacterUpdate = JsonConvert.DeserializeObject<JsonCharacterUpdate>(jsonString); } catch (Exception ex) { Debug.Log(ex); add = false; }
                        break;
                    }
                case OpCodes.PROP:
                    {
                        try { qItem.Prop = JsonConvert.DeserializeObject<JsonProp>(jsonString); } catch (Exception ex) { Debug.Log(ex); add = false; }
                        break;
                    }
                case OpCodes.UPDATE_REPLACE:
                    {
                        try { qItem.UpdateReplace = JsonConvert.DeserializeObject<JsonUpdateReplace>(jsonString); } catch (Exception ex) { Debug.Log(ex); add = false; }
                        break;
                    }
                case OpCodes.MOTION:
                    {
                        try { qItem.Motion = JsonConvert.DeserializeObject<JsonMotion>(jsonString); } catch (Exception ex) { Debug.Log(ex); add = false; }
                        break;
                    }
                case OpCodes.LIGHTS:
                    {
                        try { qItem.Lights = JsonConvert.DeserializeObject<JsonLights>(jsonString); } catch (Exception ex) { Debug.Log(ex); add = false; }
                        break;
                    }
                case OpCodes.CAMERA_SYNC:
                    {
                        try { qItem.CameraSync = JsonConvert.DeserializeObject<JsonCameraSync>(jsonString); } catch (Exception ex) { Debug.Log(ex); add = false; }
                        break;
                    }
                case OpCodes.FRAME_SYNC:
                    {
                        try { qItem.FrameSync = JsonConvert.DeserializeObject<JsonFrameSync>(jsonString); } catch (Exception ex) { Debug.Log(ex); add = false; }
                        break;
                    }
            }
            if (add)
                activityQueue.Add(qItem);
            else
            {
                Debug.LogWarning("Broken Item: " + opCode.ToString());
                Debug.LogWarning(jsonString);
            }
        }
        
        static byte[] ExtractBytes(byte[] data, int startIndex, int length)
        {
            byte[] sizeBytes = new byte[length];
            int n = 0;
            for (int i = startIndex; i < (startIndex + length); i++)
            {
                sizeBytes[n++] = data[i];
            }
            return sizeBytes;
        }

        static string EXPORTPATH = "F:/DataLink";
        static string PLUGIN_VERSION = "2.2.5";

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

            // Debug.LogWarning(Application.productName);  // update plugin to use the project name (Application.productName)

            jsonString = JsonConvert.SerializeObject(hello);

            return jsonString;
        }
        #endregion Recieved data handling

        #region Activity queue handling

        private static Thread queueThread;
        static bool processing = true;

        public static void StartQueue()
        {            
            activityQueue = new List<QueueItem>();
            EditorApplication.update -= QueueDelegate;
            EditorApplication.update += QueueDelegate;
        }

        public static void StopQueue()
        {
            activityQueue.Clear();
            EditorApplication.update -= QueueDelegate;
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

            //Debug.Log(EditorApplication.timeSinceStartup);

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

            Debug.Log("Processing next queue item.");

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
                        break;
                    }
                case OpCodes.NOTIFY:
                    {
                        Debug.Log(next.Notify.ToString());
                        break;
                    }
                case OpCodes.CHARACTER:
                    {
                        Debug.Log(next.Character.ToString());
                        break;
                    }
                case OpCodes.CHARACTER_UPDATE:
                    {
                        Debug.Log(next.CharacterUpdate.ToString());
                        break;
                    }
                case OpCodes.PROP:
                    {
                        Debug.Log(next.Prop.ToString());
                        break;
                    }
                case OpCodes.UPDATE_REPLACE:
                    {
                        Debug.Log(next.UpdateReplace.ToString());
                        break;
                    }
                case OpCodes.MOTION:
                    {
                        Debug.Log(next.Motion.ToString());
                        break;
                    }
                case OpCodes.LIGHTS:
                    {
                        Debug.Log(next.Lights.ToString());
                        break;
                    }
                case OpCodes.CAMERA_SYNC:
                    {
                        CameraSync(next);
                        Debug.Log(next.CameraSync.ToString());
                        break;
                    }
                case OpCodes.FRAME_SYNC:
                    {
                        Debug.Log(next.FrameSync.ToString());
                        break;
                    }
            }
            next.Processed = true;
            UnityLinkManagerWindow.Instance.Focus();            
        }

        static void CameraSync(QueueItem item)
        {
            GameObject camera = GameObject.Find("Main Camera");

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
            
            camera.transform.position = cameraPos;
            camera.transform.rotation = corrected;
            
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

        #endregion  Activity queue handling

        #region Class data
        public enum ExportType
        {
            AVATAR = 0,
            PROP = 1,
            LIGHT = 2,
            CAMERA = 3,
            UNKNOWN = 999
        }
        
        public static ExportType ParseExportType(string value)
        {
            return Enum.TryParse(value, out ExportType result) ? result : ExportType.UNKNOWN;            
        }

        public class JsonHello // HELLO: (1) - Respond to new connection with server data
        {
            public const string applicationStr = "Application";     // Application: String - Application name
            public const string versionStr = "Version";             // Version: Int list [major, minor, revision] - Version numbers
            public const string pathStr = "Path";                   // Path: String - local path where it saves exports (only used as a fallback if the clients local path doesn't exist)
            public const string pluginStr = "Plugin";               // Plugin: String - plugin version
            public const string exeStr = "Exe";                     // Exe: String - path to iClone/CC executable

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

            public JsonHello()
            {
                Application = string.Empty;
                Version = new int[0];
                Path = string.Empty;
                Plugin = string.Empty;
                Exe = string.Empty;
            }

            public override string ToString()
            {
                return "Application " + this.Application + ", Version " + this.Version.ToString() + ", Path " + this.Path + ", Plugin " + this.Plugin + ", Exe " + this.Exe;
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

            public override string ToString()
            {
                return "Message " + this.Message;
            }
        }

        public class JsonCharacter // CHARACTER: (100) - receive character export from server
        {
            public const string pathStr = "path";                   // path: String - file path of fbx export file
            public const string nameString = "name";                // name: String - actor name,
            public const string typeStr = "type";                   // type: String - actor type (AVATAR, PROP, LIGHT, CAMERA) - Should be AVATAR (used to verify)
            public const string linkIdStr = "link_id";              // link_id: String - actor link id code
            public const string motionPrefixStr = "motion_prefix";  // motion_prefix: String - name to prefix animation names

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
                Path = string.Empty;
                Name = string.Empty;
                Type = string.Empty;
                LinkId = string.Empty;
                MotionPrefix = string.Empty;
            }

            public override string ToString()
            {
                return "Path " + this.Path + ", Name " + this.Name + ", Type " + this.Type + ", Link Id " + this.LinkId + ", Motion Prefix " + this.MotionPrefix;
            }
        }

        public class JsonCharacterUpdate // CHARACTER_UPDATE: (101) - send changes to character id data (name, link_id)
        {
            // Notes: Probably not needed for Unity.
        }

        public class JsonProp // PROP: (102) - receive prop export from server
        {
            public const string pathStr = "path";                   // path: String - file path of fbx export file
            public const string nameString = "name";                // name: String - prop name
            public const string typeStr = "type";                   // type: String - actor type (AVATAR, PROP, LIGHT, CAMERA) - Should be PROP (used to verify)
            public const string linkIdStr = "link_id";              // link_id: String - actor link id code
            public const string motionPrefixStr = "motion_prefix";  // motion_prefix: String - name to prefix animation names

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
                Path = string.Empty;
                Name = string.Empty;
                Type = string.Empty;
                LinkId = string.Empty;
                MotionPrefix = string.Empty;
            }

            public override string ToString()
            {
                return "Path " + this.Path + ", Name " + this.Name + ", Type " + this.Type + ", Link Id " + this.LinkId + ", Motion Prefix " + this.MotionPrefix;
            }
        }

        public class JsonUpdateReplace // UPDATE_REPLACE: (108) - receive updated character or prop from server
        {
            public const string pathStr = "path";           // path: String - file path of fbx export file
            public const string nameString = "name";        // name: String - prop name
            public const string typeStr = "type";           // type: String - actor type (AVATAR, PROP, LIGHT, CAMERA)
            public const string linkIdStr = "link_id";      // link_id: String - actor link id code
            public const string replaceStr = "replace";     // replace: Bool - replace entire actor (True) or just the selected parts (False) listed in 'objects'
            public const string objectsStr = "objects";     // objects: String list - list of object names to replace in the actor with the ones in this fbx export

            // Notes: Logistical nightmare.

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

            [JsonProperty(pathStr)]
            public string Path { get; set; }
            [JsonProperty(nameString)]
            public string Name { get; set; }
            [JsonProperty(typeStr)]
            public string Type { get; set; }
            [JsonProperty(linkIdStr)]
            public string LinkId { get; set; }
            [JsonProperty(fpsStr)]
            public int Fps { get; set; }
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
                Path = string.Empty;
                Name = string.Empty;
                Type = string.Empty;
                LinkId = string.Empty;
                Fps = 0;
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

        public class JsonLights // LIGHTS: (230) - Sync lights with iClone/CC
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
            #
            # I have no idea what the following are, but I send them anyway
            #
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
                
        public class QueueItem
        {
            public OpCodes OpCode { get; set; }
            public Exchange Exchange { get; set; }
            public DateTime EntryTime { get; set; }
            public bool Processed { get; set; }
            public bool Debug {  get; set; }
            public JsonNotify Notify { get; set; }
            public JsonHello Hello { get; set; }
            public JsonCharacter Character { get; set; }
            public JsonCharacterUpdate CharacterUpdate { get; set; }
            public JsonProp Prop { get; set; }
            public JsonUpdateReplace UpdateReplace { get; set; }
            public JsonMotion Motion { get; set; }
            public JsonLights Lights { get; set; }
            public JsonCameraSync CameraSync { get; set; }
            public JsonFrameSync FrameSync { get; set; }

            public QueueItem(OpCodes opCode, Exchange direction)
            {
                OpCode = opCode;
                Exchange = direction;
                EntryTime = DateTime.Now.ToLocalTime();
                Hello = null;
                Character = null;
                CharacterUpdate = null;
                Prop = null;
                UpdateReplace = null;
                Motion = null;
                Lights = null;
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

        static Int32 GetCurrentEndianWord(byte[] data, SourceEndian sourceEndian)
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

        static byte[] Int32ToBigEndianBytes(Int32 int32)
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

        static byte[] ReverseByteOrder(byte[] data)
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

        #region Asset import
        static void ProcessImport()
        {
            FileUtil.CopyFileOrDirectory("D:/Development/CC3/Unity Test Chars/TestB", "Assets/TestB");
            AssetDatabase.Refresh();
            string[] guids = AssetDatabase.FindAssets("t:Model", new string[] { "Assets/TestB" });
            string guid = string.Empty;
            foreach (string g in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(g);
                if (Util.IsCC3CharacterAtPath(assetPath))
                {
                    string name = Path.GetFileNameWithoutExtension(assetPath);
                    Debug.Log("Valid CC character: " + name + " found.");
                    guid = g;
                    break;
                }
            }
            ExtractFbx(guid);

            return;
            ImporterWindow.Current.RefreshCharacterList();
            var character = ImporterWindow.ValidCharacters.Find(x => x.guid == guid);
            Importer import = new Importer(character);
            GameObject prefab = import.Import(true);
            character.Write();
            character.Release();
            ExtractFbx(guid);
        }
        #endregion Asset import

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
                if (go.name.Contains("bone", StringComparison.InvariantCultureIgnoreCase) && go.name.Contains("root", StringComparison.InvariantCultureIgnoreCase))
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
                
    }
}