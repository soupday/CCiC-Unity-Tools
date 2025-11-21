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

using UnityEngine;
using UnityEditor;
using Unity.Plastic.Newtonsoft.Json;
using Unity.Plastic.Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.IO;

namespace Reallusion.Import
{
    public class BoneEditor : EditorWindow
    {
        #region Test Menu
        [MenuItem("Reallusion/Bone Driver", priority = 100)]
        public static void OpenBoneEditorWindow()
        {
            InitWindow();
        }

        [MenuItem("Reallusion/Bone Driver", priority = 100, validate = true)]
        public static bool ValidateBoneEditorWindow()
        {
            return !HasOpenInstances<BoneEditor>();
        }
        #endregion Test Menu

        #region Test Vars
        public static BoneEditor Instance;
        [SerializeField]
        public GameObject model;
        [SerializeField]
        public TextAsset jsonObject;
        [SerializeField]
        public GameObject cCBaseBody;
        #endregion Test Vars

        #region Test UI
        public static void InitWindow()
        {
            Instance = ScriptableObject.CreateInstance<BoneEditor>();
            Instance.ShowUtility();
        }

        private void OnGUI()
        {
            model = EditorGUILayout.ObjectField(model, typeof(GameObject), true) as GameObject;
            jsonObject = EditorGUILayout.ObjectField(jsonObject, typeof(TextAsset), true) as TextAsset;

            EditorGUILayout.ObjectField(cCBaseBody, typeof(GameObject), true);

            if (GUILayout.Button("Do"))
            {
                TestSetupDriver();
            }

            if (GUILayout.Button("Query"))
            {
                TestFetchBoneArray();
                TestFindExtraShapes();
            }
        }

        private void TestSetupDriver()
        {
            TestFindBaseBody(model.transform);
            SkinnedMeshRenderer smr = cCBaseBody.GetComponent<SkinnedMeshRenderer>();

            UnityEngine.Object parentObject = PrefabUtility.GetCorrespondingObjectFromSource(model);
            string path = AssetDatabase.GetAssetPath(parentObject);
            Debug.Log("prefab path:" + path);

            SetupBoneDriverReflection(cCBaseBody, smr, AssetDatabase.GetAssetPath(jsonObject), model, true, true);
        }

        void TestFindExtraShapes()
        {
            if (cCBaseBody == null) TestFindBaseBody(model.transform);
            Dictionary<string, List<string>>  extraShapes = FindExcessBlendShapes(cCBaseBody);

            int count = 0;
            foreach (var entry in extraShapes)
            {
                Debug.Log($"Renderer: {entry.Key} Count: {entry.Value.Count}");
                foreach (var extra in entry.Value)
                {
                    //Debug.Log($"    {extra}");
                    count++;
                }
            }
            Debug.Log($"Total extra count: {count}");
        }

        void TestFetchBoneArray()
        {
            if (cCBaseBody == null) TestFindBaseBody(model.transform);
            string[] bonesBeingDriven = RetrieveBoneArray(cCBaseBody);

            foreach (string bone in bonesBeingDriven)
            {
                Debug.Log($"Bone: {bone} is being driven by expressions");  //so animation tracks for these bones arent needed
            }
        }

        void TestFindBaseBody(Transform t)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                if (t.GetChild(i).name == "CC_Base_Body")
                    cCBaseBody = t.GetChild(i).gameObject;
                else
                    TestFindBaseBody(t.GetChild(i));
            }
        }

        void TestLogGlossary(ExpressionGlossary glossary)
        {
            foreach (ExpressionByBone ebb in glossary.ExpressionsByBone)
            {
                Debug.Log($"BoneName {ebb.BoneName}");
                foreach (Expression e in ebb.Expressions)
                {
                    Debug.Log($"    {e.ExpressionName} idx: {e.BlendShapeIndex}");
                }
            }
        }
        #endregion Test UI

        #region Utils
        public static void SetupBoneDriverReflection(GameObject obj, SkinnedMeshRenderer smr, string jsonPath, GameObject sourceObject, bool bonesEnable, bool expressionEnable)
        {
            TextAsset jsonAsset = null;

            if (File.Exists(jsonPath))
                jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
            else { Debug.Log($"Provided jsonPath: {jsonPath} is incorrect"); return; }

            ExpressionGlossary glossary = BuildExpressionGlossary(sourceObject, smr, jsonAsset.text);
            string jsonSetupString = JsonConvert.SerializeObject(glossary);

            SetupBoneDriver(AddBoneDriver(obj), jsonSetupString, bonesEnable, expressionEnable);
        }

        private static Component AddBoneDriver(GameObject obj)
        {
            Component boneDriver = null;

            Type BoneDriver = null;
            if (BoneDriver == null)
            {
                BoneDriver = Physics.GetTypeInAssemblies("Reallusion.Runtime.BoneDriver");
                if (BoneDriver == null)
                {
                    Debug.LogWarning("SetupBoneDriver cannot find the <BoneDriver> class.");
                    return boneDriver;
                }
            }

            boneDriver = obj.GetComponent(BoneDriver);
            if (boneDriver == null)
                boneDriver = obj.AddComponent(BoneDriver);

            return boneDriver;
        }

        private static void SetupBoneDriver(Component boneDriver, string jsonSetupString, bool bonesEnable, bool expressionEnable)
        {
            MethodInfo SetupBoneDriver = null;
            if (boneDriver != null)
            {
                SetupBoneDriver = boneDriver.GetType().GetMethod("SetupFromJson",
                                    BindingFlags.Public | BindingFlags.Instance,
                                    null,
                                    CallingConventions.Any,
                                    new Type[] { typeof(string), typeof(bool), typeof(bool) },
                                    null);

                if (SetupBoneDriver == null)
                {
                    Debug.LogWarning("SetupBoneDriver MethodInfo cannot be determined");
                    return;
                }
            }
            else
            {
                Debug.LogWarning("SetupBoneDriver cannot find the <BoneDriver> component.");
                return;
            }

            SetupBoneDriver.Invoke(boneDriver, new object[] { jsonSetupString, bonesEnable, expressionEnable });
        }

        public static string[] RetrieveBoneArray(GameObject obj)
        {
            string[] strings = new string[0];

            Type BoneDriver = null;
            if (BoneDriver == null)
            {
                BoneDriver = Physics.GetTypeInAssemblies("Reallusion.Runtime.BoneDriver");
                if (BoneDriver == null)
                {
                    Debug.LogWarning("SetupLight cannot find the <BoneDriver> class.");
                    return strings;
                }
                else
                {
                    Debug.LogWarning("Found " + BoneDriver.GetType().ToString());
                }
            }

            Component boneDriver = obj.GetComponent(BoneDriver);
            if (boneDriver != null)
            {
                MethodInfo QueryBoneDriver = null;
                if (boneDriver != null)
                {
                    QueryBoneDriver = boneDriver.GetType().GetMethod("RetrieveBoneArray",
                                        BindingFlags.Public | BindingFlags.Instance,
                                        null,
                                        CallingConventions.Any,
                                        new Type[] { },
                                        null);

                    if (QueryBoneDriver == null)
                    {
                        Debug.LogWarning("QueryBoneDriver MethodInfo cannot be determined");
                        return strings;
                    }
                }
                else
                {
                    Debug.LogWarning("QueryBoneDriver cannot find the <BoneDriver> component.");
                    return strings;
                }


                var result = QueryBoneDriver.Invoke(boneDriver, new object[] { });
                if (result != null)
                {
                    strings = (string[])result;
                    return strings;
                }
            }
            else
            {
                Debug.LogWarning("Cannot find the <BoneDriver> component on CC_Base_Body.");
            }
            return strings;
        }

        public static Dictionary<string, List<string>> RetrieveBoneDictionary(GameObject obj)
        {
            Dictionary<string, List<string>> dict = new Dictionary<string, List<string>>();

            Type BoneDriver = null;
            if (BoneDriver == null)
            {
                BoneDriver = Physics.GetTypeInAssemblies("Reallusion.Runtime.BoneDriver");
                if (BoneDriver == null)
                {
                    Debug.LogWarning("SetupLight cannot find the <BoneDriver> class.");
                    return dict;
                }
                else
                {
                    Debug.LogWarning("Found " + BoneDriver.GetType().ToString());
                }
            }

            Component boneDriver = obj.GetComponent(BoneDriver);
            if (boneDriver != null)
            {
                MethodInfo QueryBoneDriver = null;
                if (boneDriver != null)
                {
                    QueryBoneDriver = boneDriver.GetType().GetMethod("RetrieveBoneDictionary",
                                        BindingFlags.Public | BindingFlags.Instance,
                                        null,
                                        CallingConventions.Any,
                                        new Type[] { },
                                        null);

                    if (QueryBoneDriver == null)
                    {
                        Debug.LogWarning("QueryBoneDriver MethodInfo cannot be determined");
                        return dict;
                    }
                }
                else
                {
                    Debug.LogWarning("QueryBoneDriver cannot find the <BoneDriver> component.");
                    return dict;
                }


                var result = QueryBoneDriver.Invoke(boneDriver, new object[] { });
                if (result != null)
                {
                    dict = (Dictionary<string, List<string>>)result;
                    return dict;
                }
            }
            else
            {
                Debug.LogWarning("Cannot find the <BoneDriver> component on CC_Base_Body.");
            }
            return dict;
        }

        public static GameObject GetBoneDriverGameObjectReflection(GameObject obj)
        {
            Type BoneDriver = null;
            if (BoneDriver == null)
            {
                BoneDriver = Physics.GetTypeInAssemblies("Reallusion.Runtime.BoneDriver");
                if (BoneDriver == null)
                {
                    Debug.LogWarning("SetupLight cannot find the <BoneDriver> class.");
                    return null;
                }
                else
                {
                    Debug.LogWarning("Found " + BoneDriver.GetType().ToString());
                }
            }

            Component boneDriver = obj.GetComponentInChildren(BoneDriver);
            if (boneDriver != null)
            {
                return boneDriver.gameObject;
            }
            return null;
        }

        public static Dictionary<string, Dictionary<string, BoneData>> ExtractExpressionData(string jsonString)
        {
            var result = new Dictionary<string, Dictionary<string, BoneData>>();
            var root = JObject.Parse(jsonString);

            foreach (var characterProp in root.Properties())
            {
                var characterObject = characterProp.Value["Object"];
                if (characterObject == null) continue;

                foreach (var modelProp in characterObject.Children<JProperty>())
                {
                    var expressionToken = modelProp.Value["Expression"];
                    if (expressionToken == null) continue;

                    foreach (var expressionProp in expressionToken.Children<JProperty>())
                    {
                        string expressionName = expressionProp.Name;
                        var bones = expressionProp.Value["Bones"] as JObject;
                        if (bones == null) continue;

                        var boneDict = new Dictionary<string, BoneData>();

                        foreach (var boneProp in bones.Properties())
                        {
                            string boneName = boneProp.Name;
                            var boneData = boneProp.Value;

                            var translate = boneData["Translate"]?.ToObject<float[]>();
                            var rotation = boneData["Rotation"]?.ToObject<float[]>();

                            if (translate?.Length == 3 && rotation?.Length == 4)
                            {
                                boneDict[boneName] = new BoneData
                                {
                                    Translate = new Vector3(-translate[0], translate[1], translate[2]) * 0.01f,
                                    Rotation = new Vector4(rotation[0], -rotation[1], -rotation[2], rotation[3])
                                };
                            }
                        }

                        if (boneDict.Count > 0)
                            result[expressionName] = boneDict;
                    }
                }
            }
            return result;
        }

        static ExpressionGlossary BuildExpressionGlossary(GameObject sourceObject, SkinnedMeshRenderer smr, string json)
        {
            Dictionary<string, Dictionary<string, BoneData>> expressions = ExtractExpressionData(json);

            SkeletonBone[] skeleton = new SkeletonBone[0];
            Animator anim = sourceObject.GetComponentInChildren<Animator>();
            if (anim != null && anim.avatar != null)
            {
                Avatar avatar = anim.avatar;
                skeleton = avatar.humanDescription.skeleton;
            }

            ExpressionGlossary glossary = new ExpressionGlossary();

            // build list of implicated bones and create a single ExpressionByBone for each bone with cached bindpose
            foreach (var expression in expressions)
            {
                foreach (var bone in expression.Value)
                {
                    try
                    {
                        Vector3 skeletonPosition = skeleton.FirstOrDefault(x => x.name == bone.Key).position;
                        Quaternion skeletonRotation = skeleton.FirstOrDefault(x => x.name == bone.Key).rotation;

                        var matches = glossary.ExpressionsByBone.Where(p => p.BoneName == bone.Key);
                        if (matches.Count() == 0)
                            glossary.ExpressionsByBone.Add(new ExpressionByBone(bone.Key, skeletonPosition, skeletonRotation));
                    }
                    catch { Debug.Log("Error building ExpressionGlossary"); }
                }
            }

            // add each instance of Expression data for each bone as a list
            foreach (ExpressionByBone ebb in glossary.ExpressionsByBone)
            {
                foreach (var expression in expressions)
                {
                    foreach (var bone in expression.Value)
                    {
                        if (ebb.BoneName == bone.Key)
                        {
                            bool isViseme = expression.Key.StartsWith("V_");
                            int index = smr.sharedMesh.GetBlendShapeIndex(expression.Key);
                            ebb.Expressions.Add(new Expression(expression.Key, index, isViseme, bone.Value.Translate, bone.Value.Rotation));
                        }
                    }
                }
            }
            return glossary;
        }

        public static Dictionary<string, List<string>> FindExcessBlendShapes(GameObject obj)
        {            
            Dictionary<string, List<string>> dict = new Dictionary<string, List<string>>();

            SkinnedMeshRenderer[] renderers = obj.transform.parent.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();

            List<string> drivenBlendShapes = new List<string>();
            string[] drivers = new string[] { "CC_Base_Body", "CC_Base_Tongue" };
            foreach (string driver in drivers)
            {
                var smr = renderers.First(r => r.name == driver);
                for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                {
                    drivenBlendShapes.Add(smr.sharedMesh.GetBlendShapeName(i));                    
                }
            }
                        
            foreach (var renderer in renderers)
            {
                List<string> extraBlendShapes = new List<string>();
                for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                {
                    if (!drivenBlendShapes.Contains(renderer.sharedMesh.GetBlendShapeName(i)))
                    {
                        extraBlendShapes.Add(renderer.sharedMesh.GetBlendShapeName(i));
                    }
                }
                dict.Add(renderer.name, extraBlendShapes);
            }
            return dict;
        }

        public static void LogBeautifiedJson(string jsonString)
        {
            JToken parsedJson = JToken.Parse(jsonString);
            string beautifiedJson = parsedJson.ToString(Formatting.Indented);
            Debug.Log(beautifiedJson);
        }
        #endregion Utils

        #region Class Data
        public class BoneData
        {
            public Vector3 Translate { get; set; }
            public Vector4 Rotation { get; set; }
        }

        // to be serialized to JSON
        [Serializable]
        public class ExpressionGlossary
        {
            public List<ExpressionByBone> ExpressionsByBone;

            public ExpressionGlossary()
            {
                ExpressionsByBone = new List<ExpressionByBone>();
            }
        }

        [Serializable]
        public class ExpressionByBone
        {
            public string BoneName;
            [JsonIgnore]
            public Transform BoneTransform;

            // bind pose data from skeleton avatar.humanDescription.skeleton
            public float[] RefPositionArr;
            public float[] RefRotationArr;

            [JsonIgnore]
            public Vector3 RefPosition;
            [JsonIgnore]
            public Quaternion RefRotation;

            public List<Expression> Expressions;

            public ExpressionByBone(string name, Vector3 position, Quaternion rotation)
            {
                Expressions = new List<Expression>();
                BoneName = name;
                RefPositionArr = new float[] { position.x, position.y, position.z };
                RefRotationArr = new float[] { rotation.x, rotation.y, rotation.z, rotation.w };
            }

            public Vector3 GetRefPosition()
            {
                return new Vector3(RefPositionArr[0], RefPositionArr[1], RefPositionArr[2]);
            }

            public Quaternion GetRefRotaion()
            {
                return new Quaternion(RefRotationArr[0], RefRotationArr[1], RefRotationArr[2], RefRotationArr[3]);
            }
        }

        [Serializable]
        public class Expression
        {
            public string ExpressionName;
            public int BlendShapeIndex;
            public bool isViseme;

            public float[] TranslateArr;
            public float[] RotationArr;

            [JsonIgnore]
            public Vector3 Translate;
            [JsonIgnore]
            public Quaternion Rotation;

            public Expression(string name, int index, bool viseme, Vector3 translate, Vector4 rotation)
            {
                ExpressionName = name;
                BlendShapeIndex = index;
                isViseme = viseme;
                TranslateArr = new float[] { translate.x, translate.y, translate.z };
                RotationArr = new float[] { rotation.x, rotation.y, rotation.z, rotation.w };
            }

            public Vector3 GetTranslate()
            {
                return new Vector3(TranslateArr[0], TranslateArr[1], TranslateArr[2]);
            }

            public Quaternion GetRotaion()
            {
                return new Quaternion(RotationArr[0], RotationArr[1], RotationArr[2], RotationArr[3]);
            }
        }
        #endregion Class Data
    }
}