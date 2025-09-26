/*
 * Copyright (C) 2021 Victor Soupday
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

using Codice.Client.BaseCommands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using static Reallusion.Import.CharacterInfo;
using static UnityEngine.Terrain;

namespace Reallusion.Import
{
    public class ComputeBake
    {
        private readonly GameObject fbx;
        private readonly GameObject prefab;
        private GameObject clone;
        private CharacterInfo characterInfo;
        private readonly string characterName;
        private readonly string fbxPath;
        private readonly string fbxFolder;
        private readonly string bakeFolder;
        private readonly string characterFolder;
        private readonly string texturesFolder;
        private readonly string sourceMaterialsFolder;
        private readonly string materialsFolder;
        private readonly string fbmFolder;
        private readonly string texFolder;
        private readonly List<string> importAssets;

        public const int MAX_SIZE = 8192;
        public const int MIN_SIZE = 128;
        public const string COMPUTE_SHADER = "RLBakeShader";
        public const string BAKE_FOLDER = "Baked";
        public const string TEXTURES_FOLDER = "Textures";
        public const string MATERIALS_FOLDER = "Materials";

        private Texture2D maskTex = null;

        private RenderPipeline RP => Pipeline.GetRenderPipeline();
        private bool IS_3D => RP == RenderPipeline.Builtin;
        private bool IS_URP => RP == RenderPipeline.URP;
        private bool IS_HDRP => RP == RenderPipeline.HDRP;

        private bool CUSTOM_SHADERS => characterInfo.BakeCustomShaders;
        private bool BASIC_SHADERS => !characterInfo.BakeCustomShaders;
        private bool REFRACTIVE_EYES => characterInfo.RefractiveEyes;
        private bool PARALLAX_EYES => characterInfo.ParallaxEyes;
        private bool BASIC_EYES => characterInfo.BasicEyes;

        public ComputeBake(UnityEngine.Object character, CharacterInfo info, string textureFolderOverride = null)
        {
            fbx = (GameObject)character;
            fbxPath = AssetDatabase.GetAssetPath(fbx);
            prefab = Util.FindCharacterPrefabAsset(fbx);
            characterInfo = info;
            characterName = Path.GetFileNameWithoutExtension(fbxPath);
            fbxFolder = Path.GetDirectoryName(fbxPath);
            bakeFolder = Util.CreateFolder(fbxFolder, BAKE_FOLDER);
            characterFolder = Util.CreateFolder(bakeFolder, characterName);
            texturesFolder = Util.CreateFolder(characterFolder,
                string.IsNullOrEmpty(textureFolderOverride) ? TEXTURES_FOLDER : textureFolderOverride);
            materialsFolder = Util.CreateFolder(characterFolder, MATERIALS_FOLDER);
            string parentSourceMaterialsFolder = Util.CreateFolder(fbxFolder, MATERIALS_FOLDER);
            sourceMaterialsFolder = Util.CreateFolder(parentSourceMaterialsFolder, characterName);

            fbmFolder = Path.Combine(fbxFolder, characterName + ".fbm");
            texFolder = Path.Combine(fbxFolder, "textures", characterName);

            importAssets = new List<string>();
        }

        public static string BakeTexturesFolder(string fbxPath, string textureFolderOverride = null)
        {
            string characterName = Path.GetFileNameWithoutExtension(fbxPath);
            string fbxFolder = Path.GetDirectoryName(fbxPath);
            string bakeFolder = Util.CreateFolder(fbxFolder, BAKE_FOLDER);
            string characterFolder = Util.CreateFolder(bakeFolder, characterName);
            string texturesFolder = Util.CreateFolder(characterFolder,
                string.IsNullOrEmpty(textureFolderOverride) ? TEXTURES_FOLDER : textureFolderOverride);

            return texturesFolder;
        }

        private static Vector2Int GetMaxSize(Texture2D a)
        {
            Vector2Int max = new Vector2Int(MIN_SIZE, MIN_SIZE);
            if (a) max = new Vector2Int(a.width, a.height);
            if (max.x > MAX_SIZE) max.x = MAX_SIZE;
            if (max.y > MAX_SIZE) max.y = MAX_SIZE;
            return max;
        }

        private static Vector2Int GetMaxSize(Texture2D a, Texture2D b)
        {
            Vector2Int max = new Vector2Int(MIN_SIZE, MIN_SIZE);
            if (a) max = new Vector2Int(a.width, a.height);
            if (b)
            {
                if (b.width > max.x) max.x = b.width;
                if (b.height > max.y) max.y = b.height;
            }
            if (max.x > MAX_SIZE) max.x = MAX_SIZE;
            if (max.y > MAX_SIZE) max.y = MAX_SIZE;
            return max;
        }

        private static Vector2Int GetMaxSize(Texture2D a, Texture2D b, Texture2D c)
        {
            Vector2Int max = new Vector2Int(MIN_SIZE, MIN_SIZE);
            if (a) max = new Vector2Int(a.width, a.height);
            if (b)
            {
                if (b.width > max.x) max.x = b.width;
                if (b.height > max.y) max.y = b.height;
            }
            if (c)
            {
                if (c.width > max.x) max.x = c.width;
                if (c.height > max.y) max.y = c.height;
            }
            if (max.x > MAX_SIZE) max.x = MAX_SIZE;
            if (max.y > MAX_SIZE) max.y = MAX_SIZE;
            return max;
        }

        private static Vector2Int GetMaxSize(Texture2D a, Texture2D b, Texture2D c, Texture2D d)
        {
            Vector2Int max = new Vector2Int(MIN_SIZE, MIN_SIZE);
            if (a) max = new Vector2Int(a.width, a.height);
            if (b)
            {
                if (b.width > max.x) max.x = b.width;
                if (b.height > max.y) max.y = b.height;
            }
            if (c)
            {
                if (c.width > max.x) max.x = c.width;
                if (c.height > max.y) max.y = c.height;
            }
            if (d)
            {
                if (d.width > max.x) max.x = d.width;
                if (d.height > max.y) max.y = d.height;
            }
            if (max.x > MAX_SIZE) max.x = MAX_SIZE;
            if (max.y > MAX_SIZE) max.y = MAX_SIZE;
            return max;
        }

        private Texture2D GetMaterialTexture(Material mat, string shaderRef, bool isNormal = false)
        {
            Texture2D tex = null;
            if (mat.HasProperty(shaderRef))
                tex = (Texture2D)mat.GetTexture(shaderRef);
            return tex;
        }

        private Texture2DArray GetMaterialTextureArray(Material mat, string shaderRef, bool isNormal = false)
        {
            Texture2DArray texArray = null;
            if (mat.HasProperty(shaderRef))
                texArray = (Texture2DArray)mat.GetTexture(shaderRef);
            return texArray;
        }

        private Texture2D CheckHDRP(Texture2D tex)
        {
            if (tex)
                return tex;

            if (!maskTex)
            {
                maskTex = new Texture2D(8, 8, TextureFormat.ARGB32, false, true);
                Color[] pixels = maskTex.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color(0f, 1f, 1f, 0.5f);
                }
                maskTex.SetPixels(pixels);
            }

            return maskTex;
        }

        /// <summary>
        /// Checks that the texture exists, if not returns a blank white texture.
        /// </summary>
        private Texture2D CheckDiffuse(Texture2D tex)
        {
            if (tex) return tex;
            return Texture2D.whiteTexture;
        }

        /// <summary>
        /// Checks that the texture exists, if not returns a blank normal texture.
        /// </summary>
        private Texture2D CheckNormal(Texture2D tex)
        {
            if (tex) return tex;
            return Texture2D.normalTexture;
        }

        /// <summary>
        /// Checks that the texture exists, if not returns a blank white texture.
        /// </summary>
        private Texture2D CheckMask(Texture2D tex)
        {
            if (tex) return tex;
            return Texture2D.whiteTexture;
        }

        /// <summary>
        /// Checks that the texture exists, if not returns a gray texture.
        /// </summary>
        private Texture2D CheckGray(Texture2D tex)
        {
            if (tex) return tex;
            return Texture2D.grayTexture;
        }

        /// <summary>
        /// Checks that the texture exists, if not returns a blank black texture.
        /// </summary>
        private Texture2D CheckBlank(Texture2D tex)
        {
            if (tex) return tex;
            return Texture2D.blackTexture;
        }

        /// <summary>
        /// Checks that the texture exists, if not returns a blank gray texture.
        /// </summary>
        private Texture2D CheckOverlay(Texture2D tex)
        {
            if (tex) return tex;
            return Texture2D.linearGrayTexture;
        }

        public GameObject BakeHQ()
        {
            if (Util.IsCC3Character(fbx))
            {
                if (!CopyToClone()) return null;

                BakeMaterials();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return SaveAsPrefab();
            }

            return null;
        }

        public GameObject BakeHQHairDiffuse()
        {
            if (Util.IsCC3Character(fbx) && prefab)
            {
                if (!CopyToClone()) return null;

                BakeHairDiffuseTextures();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                GameObject.DestroyImmediate(clone);

                return prefab;
            }

            return null;
        }

        public GameObject RestoreHQHair()
        {
            if (Util.IsCC3Character(fbx) && prefab)
            {
                if (!CopyToClone()) return null;

                RestoreHairDiffuseTextures();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                GameObject.DestroyImmediate(clone);

                return prefab;
            }

            return null;
        }

        public void BakeMaterials()
        {
            Renderer[] renderers = clone.GetComponentsInChildren<Renderer>();
            List<Material> processed = new List<Material>(renderers.Length);

            foreach (Renderer renderer in renderers)
            {
                if (renderer)
                {
                    foreach (Material sharedMat in renderer.sharedMaterials)
                    {
                        if (!sharedMat) continue;

                        // don't process duplicates...
                        if (processed.Contains(sharedMat)) continue;
                        processed.Add(sharedMat);

                        // in case any of the materials have been renamed after a previous import, get the source name.
                        string sourceName = Util.GetSourceMaterialName(fbxPath, sharedMat);
                        string shaderName = Util.GetShaderName(sharedMat);
                        Material bakedMaterial = null;
                        Material firstPass = null;
                        Material secondPass = null;

                        if (shaderName.iContains(Pipeline.SHADER_HQ_SKIN))
                            bakedMaterial = BakeSkinMaterial(sharedMat, sourceName);

                        else if (shaderName.iContains(Pipeline.SHADER_HQ_TEETH))
                            bakedMaterial = BakeTeethMaterial(sharedMat, sourceName);

                        else if (shaderName.iContains(Pipeline.SHADER_HQ_TONGUE))
                            bakedMaterial = BakeTongueMaterial(sharedMat, sourceName);

                        else if (shaderName.iContains(Pipeline.SHADER_HQ_CORNEA) ||
                             shaderName.iContains(Pipeline.SHADER_HQ_CORNEA_PARALLAX) ||
                             shaderName.iContains(Pipeline.SHADER_HQ_CORNEA_REFRACTIVE) ||
                             shaderName.iContains(Pipeline.SHADER_HQ_EYE_REFRACTIVE))
                            bakedMaterial = BakeEyeMaterial(sharedMat, sourceName);

                        else if (shaderName.iContains(Pipeline.SHADER_HQ_EYE_OCCLUSION))
                            bakedMaterial = BakeEyeOcclusionMaterial(sharedMat, sourceName);

                        if (shaderName.iContains(Pipeline.SHADER_HQ_HAIR) ||
                            shaderName.iContains(Pipeline.SHADER_HQ_HAIR_1ST_PASS) ||
                            shaderName.iContains(Pipeline.SHADER_HQ_HAIR_COVERAGE))
                        {
                            if (sharedMat.name.iEndsWith("_2nd_Pass")) continue;

                            bakedMaterial = BakeHairMaterial(sharedMat, sourceName, out firstPass, out secondPass);
                        }

                        if (firstPass && secondPass)
                        {
                            ReplaceMaterial(sharedMat, firstPass);
                            // Get the 2nd pass shared material
                            foreach (Material secondPassMat in renderer.sharedMaterials)
                            {
                                if (secondPassMat && secondPassMat != sharedMat && secondPassMat.name.iEndsWith("_2nd_Pass"))
                                {
                                    ReplaceMaterial(secondPassMat, secondPass);
                                }
                            }
                        }
                        else if (bakedMaterial)
                        {
                            ReplaceMaterial(sharedMat, bakedMaterial);
                        }

                        // update the wrinkle manager.
                        if (bakedMaterial && shaderName.iContains(Pipeline.SHADER_HQ_SKIN) && characterInfo.BuiltFeatureWrinkleMaps)
                        {
                            if (sourceName.iContains("Skin_Head"))
                            {
                                Importer.UpdateWrinkleManager(renderer.gameObject, bakedMaterial);
                            }
                        }
                    }

                }
            }
        }

        public void BakeHairDiffuseTextures()
        {
            Renderer[] renderers = clone.GetComponentsInChildren<Renderer>();
            List<Material> processed = new List<Material>(renderers.Length);

            foreach (Renderer renderer in renderers)
            {
                if (renderer)
                {
                    foreach (Material sharedMat in renderer.sharedMaterials)
                    {
                        if (!sharedMat) continue;

                        // don't process duplicates...
                        if (processed.Contains(sharedMat)) continue;
                        processed.Add(sharedMat);

                        // in case any of the materials have been renamed after a previous import, get the source name.
                        string sourceName = Util.GetSourceMaterialName(fbxPath, sharedMat);
                        string shaderName = Util.GetShaderName(sharedMat);

                        if (shaderName.iContains(Pipeline.SHADER_HQ_HAIR) ||
                            shaderName.iContains(Pipeline.SHADER_HQ_HAIR_1ST_PASS) ||
                            shaderName.iContains(Pipeline.SHADER_HQ_HAIR_COVERAGE))
                        {
                            if (sharedMat.name.iEndsWith("_2nd_Pass")) continue;

                            if (sharedMat.GetFloatIf("BOOLEAN_ENABLECOLOR") > 0f)
                            {
                                Texture2D sourceMap = (Texture2D)sharedMat.GetTextureIf("_DiffuseMap");
                                // bake diffuse map
                                BakeHairDiffuseOnly(sharedMat, sourceName, out Texture2D bakedMap);
                                // set baked diffuse map
                                sharedMat.SetTextureIf("_DiffuseMap", bakedMap);
                                // turn off enable color
                                sharedMat.SetFloatIf("BOOLEAN_ENABLECOLOR", 0f);
                                sharedMat.DisableKeyword("BOOLEAN_ENABLECOLOR_ON");
                                Pipeline.ResetMaterial(sharedMat);
                                // add the texture switch to the character info
                                characterInfo.AddGUIDRemap(sourceMap, bakedMap);

                                if (shaderName.iContains(Pipeline.SHADER_HQ_HAIR_1ST_PASS))
                                {
                                    // replace the diffuse map on the source material (non multi-pass version)
                                    Material sourceMat = GetSourceHairMaterial(sharedMat);
                                    if (sourceMat)
                                    {
                                        // set baked diffuse map
                                        sourceMat.SetTextureIf("_DiffuseMap", bakedMap);
                                        // turn off enable color
                                        sourceMat.SetFloatIf("BOOLEAN_ENABLECOLOR", 0f);
                                        sourceMat.DisableKeyword("BOOLEAN_ENABLECOLOR_ON");
                                        Pipeline.ResetMaterial(sourceMat);
                                    }

                                    // Get the 2nd pass shared material
                                    foreach (Material secondPassMat in renderer.sharedMaterials)
                                    {
                                        if (secondPassMat && secondPassMat != sharedMat && secondPassMat.name.iEndsWith("_2nd_Pass"))
                                        {
                                            // set baked diffuse map
                                            secondPassMat.SetTextureIf("_DiffuseMap", bakedMap);
                                            // turn off enable color
                                            secondPassMat.SetFloatIf("BOOLEAN_ENABLECOLOR", 0f);
                                            secondPassMat.DisableKeyword("BOOLEAN_ENABLECOLOR_ON");
                                            Pipeline.ResetMaterial(secondPassMat);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private Material GetSourceHairMaterial(Material mat)
        {
            string materialName = mat.name;
            string[] folders = new string[] { sourceMaterialsFolder };

            if (materialName.iContains("_1st_Pass"))
            {
                materialName = materialName.Substring(0, materialName.IndexOf("_1st_Pass"));
                return Util.FindMaterial(materialName, folders);
            }
            else if (materialName.iContains("_2nd_Pass"))
            {
                materialName = materialName.Substring(0, materialName.IndexOf("_2nd_Pass"));
                return Util.FindMaterial(materialName, folders);
            }
            else
            {
                return null;
            }
        }

        public void RestoreHairDiffuseTextures()
        {
            Renderer[] renderers = clone.GetComponentsInChildren<Renderer>();
            List<Material> processed = new List<Material>(renderers.Length);

            foreach (Renderer renderer in renderers)
            {
                if (renderer)
                {
                    foreach (Material sharedMat in renderer.sharedMaterials)
                    {
                        if (!sharedMat) continue;

                        // don't process duplicates...
                        if (processed.Contains(sharedMat)) continue;
                        processed.Add(sharedMat);

                        // in case any of the materials have been renamed after a previous import, get the source name.
                        string sourceName = Util.GetSourceMaterialName(fbxPath, sharedMat);
                        string shaderName = Util.GetShaderName(sharedMat);

                        if (shaderName.iContains(Pipeline.SHADER_HQ_HAIR) ||
                            shaderName.iContains(Pipeline.SHADER_HQ_HAIR_1ST_PASS) ||
                            shaderName.iContains(Pipeline.SHADER_HQ_HAIR_COVERAGE))
                        {
                            if (sharedMat.name.iEndsWith("_2nd_Pass")) continue;

                            Texture2D bakedMap = (Texture2D)sharedMat.GetTextureIf("_DiffuseMap");
                            Texture2D sourceMap = (Texture2D)characterInfo.GetGUIDRemapFrom(bakedMap);
                            if (sourceMap)
                            {
                                // restore source diffuse map
                                sharedMat.SetTextureIf("_DiffuseMap", sourceMap);
                                // turn on enable color
                                sharedMat.SetFloatIf("BOOLEAN_ENABLECOLOR", 1f);
                                sharedMat.EnableKeyword("BOOLEAN_ENABLECOLOR_ON");
                                Pipeline.ResetMaterial(sharedMat);
                                // remove the texture switch info
                                characterInfo.RemoveGUIDRemap(sourceMap, bakedMap);

                                if (shaderName.iContains(Pipeline.SHADER_HQ_HAIR_1ST_PASS))
                                {
                                    // restore the diffuse map on the source material (non multi-pass version)
                                    Material sourceMat = GetSourceHairMaterial(sharedMat);
                                    if (sourceMat)
                                    {
                                        // set source diffuse map
                                        sourceMat.SetTextureIf("_DiffuseMap", sourceMap);
                                        // turn on enable color
                                        sourceMat.SetFloatIf("BOOLEAN_ENABLECOLOR", 1f);
                                        sourceMat.EnableKeyword("BOOLEAN_ENABLECOLOR_ON");
                                        Pipeline.ResetMaterial(sourceMat);
                                    }

                                    // Get the 2nd pass shared material
                                    foreach (Material secondPassMat in renderer.sharedMaterials)
                                    {
                                        if (secondPassMat && secondPassMat != sharedMat && secondPassMat.name.iEndsWith("_2nd_Pass"))
                                        {
                                            // restore source diffuse map
                                            secondPassMat.SetTextureIf("_DiffuseMap", sourceMap);
                                            // turn on enable color
                                            secondPassMat.SetFloatIf("BOOLEAN_ENABLECOLOR", 1f);
                                            secondPassMat.EnableKeyword("BOOLEAN_ENABLECOLOR_ON");
                                            Pipeline.ResetMaterial(secondPassMat);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ReplaceMaterial(Material from, Material to)
        {
            Renderer[] renderers = clone.GetComponentsInChildren<Renderer>();

            foreach (Renderer renderer in renderers)
            {
                for (int j = 0; j < renderer.sharedMaterials.Length; j++)
                {
                    if (renderer.sharedMaterials[j] && renderer.sharedMaterials[j] == from)
                    {
                        Material[] copy = (Material[])renderer.sharedMaterials.Clone();
                        copy[j] = to;
                        renderer.sharedMaterials = copy;
                    }
                }
            }
        }

        public bool CopyToClone()
        {
            // don't link the prefab as a variant to the original prefabs as updating the original causes the variants to be reset.
            if (prefab)
                clone = GameObject.Instantiate<GameObject>(prefab);
            else
                clone = GameObject.Instantiate<GameObject>(fbx);

            return clone != null;
        }

        public GameObject SaveAsPrefab()
        {
            string prefabFolder = Util.CreateFolder(fbxFolder, Importer.PREFABS_FOLDER);

            string prefabPath;
            if (characterInfo.BakeSeparatePrefab)
                prefabPath = Path.Combine(prefabFolder, characterName + Importer.BAKE_SUFFIX + ".prefab");
            else
                prefabPath = Path.Combine(prefabFolder, characterName + ".prefab");

            GameObject variant = PrefabUtility.SaveAsPrefabAsset(clone, prefabPath);
            Selection.activeObject = variant;
            GameObject.DestroyImmediate(clone);
            return variant;
        }

        private void RestoreHQMaterials(GameObject prefabInstance)
        {
            Renderer[] renderers = clone.GetComponentsInChildren<Renderer>();
            List<Material> processed = new List<Material>(renderers.Length);
            string[] folders = new string[] { sourceMaterialsFolder };

            foreach (Renderer renderer in renderers)
            {
                if (renderer)
                {
                    Material[] sharedMats = renderer.sharedMaterials;
                    bool replaced = false;

                    for (int i = 0; i < sharedMats.Length; i++)
                    {
                        Material sharedMat = sharedMats[i];

                        if (!sharedMat) continue;

                        // don't process duplicates...
                        if (processed.Contains(sharedMat)) continue;
                        processed.Add(sharedMat);

                        string shaderName = Util.GetShaderName(sharedMat);

                        Action<Material, int> ReplaceMat = (m, j) =>
                        {
                            Material hqMat = Util.FindMaterial(m.name, folders);
                            if (hqMat && hqMat != m)
                            {
                                sharedMats[j] = hqMat;
                                replaced = true;
                            }
                        };

                        if (shaderName.iContains("_baked_"))
                            ReplaceMat(sharedMat, i);
                    }

                    if (replaced) renderer.sharedMaterials = sharedMats;
                }
            }
        }

        private void CopyAdditionalSettings(Material source, Material dest)
        {
            string[] refs = new string[] {
                // Tessellation (HDRP 12+)
                // Custom Tessellation (URP 17+)
                "_SSSBlend", "_SSSDistortion", "_SSSTransmission",
                // AMP Tessellation (3D)
                // Custom Subsurface (URP 10+)
                // AMP Subsurface (3D)
                "_TransStrength", "_TransNormal", "_TransScattering",
                "_TransAmbient", "_TransDirect", "_TransShadow",
            };
            foreach (string shaderRef in refs)
            {
                if (source.HasProperty(shaderRef) && dest.HasProperty(shaderRef))
                {
                    float value = source.GetFloat(shaderRef);
                    dest.SetFloat(shaderRef, value);
                }
            }
        }


        // Create Materials

        private Material CreateBakedMaterial(Material sourceMaterial, Texture2D baseMap, Texture2D maskMap,
            Texture2D metallicGlossMap, Texture2D aoMap, Texture2D normalMap,
            Texture2D detailMask, Texture2D detailMap, Texture2D subsurfaceMap,
            Texture2D thicknessMap, Texture2D emissionMap, Texture2D displacementMap,
            float normalScale, float tiling, float detailScale, float displacementStrength, float displacementLevel,
            Color emissiveColor,
            string sourceName, Material templateMaterial)
        {
            Material bakedMaterial = Util.FindMaterial(sourceName, new string[] { materialsFolder });
            Shader shader = templateMaterial ? templateMaterial.shader : Pipeline.GetDefaultShader();
            bool useAmplify = characterInfo.BakeCustomShaders && sourceMaterial.shader.name.iContains("/Amplify/");
            bool useTessellation = sourceMaterial.shader.name.EndsWith("Tessellation");

            if (!bakedMaterial)
            {
                // create the remapped material and save it as an asset.
                string matPath = AssetDatabase.GenerateUniqueAssetPath(
                        Path.Combine(materialsFolder, sourceName + ".mat")
                    );

                bakedMaterial = new Material(shader);

                // save the material to the asset database.
                AssetDatabase.CreateAsset(bakedMaterial, matPath);
            }

            // if the material shader doesn't match, update the shader.
            if (bakedMaterial.shader != shader)
                bakedMaterial.shader = shader;

            // copy the template material properties to the remapped material.
            if (templateMaterial)
            {
                bakedMaterial.CopyPropertiesFromMaterial(templateMaterial);
            }

            Pipeline.UpgradeShader(bakedMaterial, useTessellation, useAmplify);

            // apply the textures...
            if (IS_HDRP)
            {
                bakedMaterial.SetTextureIf("_BaseColorMap", baseMap);
                bakedMaterial.SetTextureIf("_MaskMap", maskMap);
                bakedMaterial.SetTextureIf("_NormalMap", normalMap);
                if (normalMap) bakedMaterial.SetFloatIf("_NormalScale", normalScale);
                bakedMaterial.SetTextureIf("_EmissionColorMap", emissionMap);
                bakedMaterial.SetColorIf("_EmissionColor", emissiveColor);
                if (detailMask)
                    bakedMaterial.SetTextureIf("_DetailMask", detailMask);
                if (detailMap)
                {
                    bakedMaterial.SetTextureIf("_DetailMap", detailMap);
                    bakedMaterial.SetTextureScaleIf("_DetailMap", new Vector2(tiling, tiling));
                    bakedMaterial.SetFloatIf("_DetailNormalScale", detailScale);
                }

                if (displacementMap)
                {
                    bakedMaterial.EnableKeyword("_HEIGHTMAP");
                    if (useTessellation)
                    {
                        bakedMaterial.EnableKeyword("_TESSELLATION_DISPLACEMENT");
                        bakedMaterial.SetFloatIf("_DisplacementMode", 3f);  // 3 - tessellation displacement
                        bakedMaterial.SetTextureIf("_HeightMap", displacementMap);
                    }
                    else
                    {
                        bakedMaterial.SetFloatIf("_DisplacementMode", 1f);  // 1 - Vertex displacement, 2 - pixel displacement (bump?)
                        bakedMaterial.SetTextureIf("_HeightMap", displacementMap);
                    }
                    bakedMaterial.SetFloatIf("_HeightMapParametrization", 1f);  // 1 - amplitude
                    bakedMaterial.EnableKeyword("_VERTEX_DISPLACEMENT_LOCK_OBJECT_SCALE");
                    bakedMaterial.EnableKeyword("_DISPLACEMENT_LOCK_TILING_SCALE");
                }
            }
            else
            {
                if (IS_URP)
                    bakedMaterial.SetTextureIf("_BaseMap", baseMap);
                else
                    bakedMaterial.SetTextureIf("_MainTex", baseMap);

                bakedMaterial.SetTextureIf("_MetallicGlossMap", metallicGlossMap);
                // glossiness / smoothness should be set in the baked template materials
                bakedMaterial.SetTextureIf("_OcclusionMap", aoMap);
                bakedMaterial.SetTextureIf("_BumpMap", normalMap);
                if (normalMap) bakedMaterial.SetFloatIf("_BumpScale", normalScale);
                if (detailMask) bakedMaterial.SetTextureIf("_DetailMask", detailMask);
                if (detailMap)
                {
                    bakedMaterial.SetTextureIf("_DetailNormalMap", detailMap);
                    bakedMaterial.SetTextureScaleIf("_DetailAlbedoMap", new Vector2(tiling, tiling));
                    bakedMaterial.SetTextureScaleIf("_DetailNormalMap", new Vector2(tiling, tiling));
                    bakedMaterial.SetFloatIf("_DetailNormalMapScale", detailScale);
                }
                bakedMaterial.SetTextureIf("_EmissionMap", emissionMap);
                bakedMaterial.SetColorIf("_EmissionColor", emissiveColor);
                if (emissiveColor.r + emissiveColor.g +  emissiveColor.b > 0f)
                {
                    bakedMaterial.EnableKeyword("_EMISSION");
                    bakedMaterial.SetTextureIf("_EmissionMap", emissionMap);
                    bakedMaterial.SetColorIf("_EmissionColor", emissiveColor);
                    bakedMaterial.globalIlluminationFlags = bakedMaterial.globalIlluminationFlags | MaterialGlobalIlluminationFlags.BakedEmissive;
                }
            }

            bakedMaterial.SetTextureIf("_SubsurfaceMaskMap", subsurfaceMap);
            bakedMaterial.SetTextureIf("_ThicknessMap", thicknessMap);

            // add the path of the remapped material for later re-import.
            string remapPath = AssetDatabase.GetAssetPath(bakedMaterial);
            if (remapPath == fbxPath) Util.LogError("remapPath: " + remapPath + " is fbxPath!");
            if (remapPath != fbxPath && AssetDatabase.WriteImportSettingsIfDirty(remapPath))
                importAssets.Add(AssetDatabase.GetAssetPath(bakedMaterial));

            if (useTessellation)
            {
                // TODO copy/set tessellation settings from mat
            }

            return bakedMaterial;
        }


        // Bake Materials

        private Material BakeSkinMaterial(Material mat, string sourceName)
        {
            Texture2D diffuse = GetMaterialTexture(mat, "_DiffuseMap");
            Texture2D mask = GetMaterialTexture(mat, "_MaskMap");
            Texture2D subsurface = GetMaterialTexture(mat, "_SSSMap");
            Texture2D thickness = GetMaterialTexture(mat, "_ThicknessMap");
            Texture2D displacement = GetMaterialTexture(mat, "_DisplacementMap");
            Texture2D cavity = GetMaterialTexture(mat, "_CavityMap");
            Texture2D sssThicknessPack = GetMaterialTexture(mat, "_SSSThicknessPack");
            Texture2D normal = GetMaterialTexture(mat, "_NormalMap", true);
            Texture2D microNormal = GetMaterialTexture(mat, "_MicroNormalMap", true);
            Texture2D colorBlend = GetMaterialTexture(mat, "_ColorBlendMap");
            Texture2D cavityAO = GetMaterialTexture(mat, "_MNAOMap");
            Texture2D normalBlend = GetMaterialTexture(mat, "_NormalBlendMap", true);
            Texture2D RGBAMask = GetMaterialTexture(mat, "_RGBAMask");
            Texture2D CFULCMask = GetMaterialTexture(mat, "_CFULCMask");
            Texture2D EarNeckMask = GetMaterialTexture(mat, "_EarNeckMask");
            Texture2D sssBlurMap = GetMaterialTexture(mat, "_SubsurfaceBlurMap");
            float normalStrength = mat.GetFloatIf("_NormalStrength");
            float microNormalStrength = mat.GetFloatIf("_MicroNormalStrength");
            float microNormalTiling = mat.GetFloatIf("_MicroNormalTiling");
            float aoStrength = mat.GetFloatIf("_AOStrength");
            float smoothnessMin = mat.GetFloatIf("_SmoothnessMin");
            float smoothnessMax = mat.GetFloatIf("_SmoothnessMax");
            float smoothnessContrast = mat.GetFloatIf("_SmoothnessContrast");
            float secondarySmoothness = mat.GetFloatIf("_SecondarySmoothness");
            float secondaryMix = mat.GetFloatIf("_SecondaryMix");
            float cavityStrength = mat.GetFloatIf("_CavityStrength");
            float edgePower = mat.GetFloatIf("_EdgePower");
            float displacementLevel = mat.GetFloatIf("_DisplacementLevel");
            float displacementStrength = mat.GetFloatIf("_DisplacementStrength");
            float bumpStrength = mat.GetFloatIf("_BumpStrength");
            float wrinkleDisplacementStrength = mat.GetFloatIf("_WrinkleDisplacementStrength");
            float subsurfaceScale = mat.GetFloatIf("_SubsurfaceScale");
            float thicknessScale = mat.GetFloatIf("_ThicknessScale");
            float thicknessScaleMin = mat.GetFloatIf("_ThicknessScaleMin", 0f);
            float colorBlendStrength = mat.GetFloatIf("_ColorBlendStrength");
            float normalBlendStrength = mat.GetFloatIf("_NormalBlendStrength");
            float mouthAOPower = mat.GetFloatIf("_MouthCavityAO");
            float nostrilAOPower = mat.GetFloatIf("_NostrilCavityAO");
            float lipsAOPower = mat.GetFloatIf("_LipsCavityAO");
            float microSmoothnessMod = mat.GetFloatIf("_MicroSmoothnessMod");
            float rMSM = mat.GetFloatIf("_RSmoothnessMod");
            float gMSM = mat.GetFloatIf("_GSmoothnessMod");
            float bMSM = mat.GetFloatIf("_BSmoothnessMod");
            float aMSM = mat.GetFloatIf("_ASmoothnessMod");
            float earMSM = mat.GetFloatIf("_EarSmoothnessMod");
            float neckMSM = mat.GetFloatIf("_NeckSmoothnessMod");
            float cheekMSM = mat.GetFloatIf("_CheekSmoothnessMod");
            float foreheadMSM = mat.GetFloatIf("_ForeheadSmoothnessMod");
            float upperLipMSM = mat.GetFloatIf("_UpperLipSmoothnessMod");
            float chinMSM = mat.GetFloatIf("_ChinSmoothnessMod");
            float unmaskedMSM = mat.GetFloatIf("_UnmaskedSmoothnessMod");
            float rSS = mat.GetFloatIf("_RScatterScale");
            float gSS = mat.GetFloatIf("_GScatterScale");
            float bSS = mat.GetFloatIf("_BScatterScale");
            float aSS = mat.GetFloatIf("_AScatterScale");
            float earSS = mat.GetFloatIf("_EarScatterScale");
            float neckSS = mat.GetFloatIf("_NeckScatterScale");
            float cheekSS = mat.GetFloatIf("_CheekScatterScale");
            float foreheadSS = mat.GetFloatIf("_ForeheadScatterScale");
            float upperLipSS = mat.GetFloatIf("_UpperLipScatterScale");
            float chinSS = mat.GetFloatIf("_ChinScatterScale");
            float unmaskedSS = mat.GetFloatIf("_UnmaskedScatterScale");
            float sssNormalSoften = mat.GetFloatIf("_SubsurfaceNormalSoften", 0f);
            Texture2D emission = GetMaterialTexture(mat, "_EmissionMap");
            Color diffuseColor = mat.GetColorIf("_DiffuseColor", Color.white);
            Color emissiveColor = mat.GetColorIf("_EmissiveColor", Color.black);
            Color subsurfaceFalloff = mat.GetColorIf("_SubsurfaceFalloff", Color.white);
            if (IS_HDRP) subsurfaceFalloff = Color.white;

            // Wrinkle Maps
            Texture2DArray maskSetArray = GetMaterialTextureArray(mat, "_WrinkleMaskSetArray");
            Texture2DArray diffuseArray = GetMaterialTextureArray(mat, "_WrinkleDiffuseArray");
            Texture2DArray normalArray = GetMaterialTextureArray(mat, "_WrinkleNormalArray");
            List<Texture2D> diffuseTextures = GetTextureArraySourceFiles(diffuseArray);
            List<Texture2D> normalTextures = GetTextureArraySourceFiles(normalArray);
            Texture2D roughnessPack = GetMaterialTexture(mat, "_WrinkleRoughnessPack");
            Texture2D flowPack = GetMaterialTexture(mat, "_WrinkleFlowPack");
            Texture2D displacementPack = GetMaterialTexture(mat, "_WrinkleDisplacementPack");

            bool isHead = mat.GetFloatIf("BOOLEAN_IS_HEAD") > 0f;
            bool useCavity = mat.GetFloatIf("_UseCavity") > 0f;
            bool useBlend = mat.GetFloatIf("_UseBlend") > 0f;
            bool useTexturePacking = mat.GetFloatIf("BOOLEAN_USE_TEXTURE_PACKING") > 0f;
            float displacementMode = mat.GetFloatIf("ENUM_DISPLACEMENT_MODE");
            float wrinkleMode = mat.GetFloatIf("ENUM_WRINKLE_MODE");

            Dictionary<float, string> ENUM_DISPLACEMENT_MODE = new Dictionary<float, string>()
            {
                { 0f, "ENUM_DISPLACEMENT_MODE_NONE" },
                { 1f, "ENUM_DISPLACEMENT_MODE_BUMP" },
                { 2f, "ENUM_DISPLACEMENT_MODE_DISPLACEMENT" },
                { 3f, "ENUM_DISPLACEMENT_MODE_DISPLACEMENT_BUMP" },
            };

            Dictionary<float, string> ENUM_WRINKLE_MODE = new Dictionary<float, string>()
            {
                { 0f, "ENUM_WRINKLE_MODE_NONE" },
                { 1f, "ENUM_WRINKLE_MODE_WRINKLE" },
                { 2f, "ENUM_WRINKLE_MODE_WRINKLE_DISPLACEMENT" }
            };

            bool useAmplify = characterInfo.BakeCustomShaders && mat.shader.name.iContains("/Amplify/");
            bool useTessellation = mat.shader.name.iEndsWith("Tessellation");
            bool useWrinkleMaps = characterInfo.BakeCustomShaders && characterInfo.BuiltFeatureWrinkleMaps;
            bool useDigitalHuman = characterInfo.BakeCustomShaders && (mat.shader.name.iEndsWith("_DH")
                                                                    || mat.shader.name.iEndsWith("_DH_Tessellation"));
            bool useCustomSSS = mat.GetBooleanKeyword("BOOLEAN_USE_SSS");

            float bakeAOStrength = aoStrength;
            if (IS_HDRP) bakeAOStrength = 1f;
            sssNormalSoften = 0f;
            if (!useBlend)
            {
                normalBlendStrength = 0f;
                colorBlendStrength = 0f;
            }

            Texture2D bakedBaseMap = diffuse;
            Texture2D bakedMaskMap = mask;
            Texture2D bakedMetallicGlossMap = null;
            Texture2D bakedAOMap = null;
            Texture2D bakedNormalMap = normal;
            Texture2D bakedDetailMap = (IS_HDRP ? null : microNormal);
            Texture2D bakedDetailMask = null;
            Texture2D bakedSubsurfaceMap = subsurface;
            Texture2D bakedThicknessMap = thickness;
            Texture2D emissionMap = emission;

            Texture2D bakedBaseMap1 = null;
            Texture2D bakedBaseMap2 = null;
            Texture2D bakedBaseMap3 = null;
            Texture2D bakedNormalMap1 = null;
            Texture2D bakedNormalMap2 = null;
            Texture2D bakedNormalMap3 = null;
            Texture2D bakedSmoothnessPack = null;
            Texture2DArray bakedDiffuseArray = null;
            Texture2DArray bakedNormalArray = null;

            if (isHead)
            {
                bakedBaseMap = BakeHeadDiffuseMap(diffuse, colorBlend, cavityAO,
                    colorBlendStrength, mouthAOPower, nostrilAOPower, lipsAOPower,
                    sourceName + "_BaseMap");

                bakedNormalMap = BakeHeadNormalMap(normal, normalBlend,
                    sssThicknessPack ? sssThicknessPack : subsurface,
                    RGBAMask, CFULCMask, EarNeckMask,
                    normalStrength, normalBlendStrength, sssNormalSoften,
                    subsurfaceScale,
                    rSS, gSS, bSS, aSS, earSS, neckSS, cheekSS, foreheadSS, upperLipSS, chinSS, unmaskedSS,
                    sourceName + "_Normal");

                if (IS_HDRP)
                {
                    bakedMaskMap = BakeHeadMaskMap(mask, cavityAO,
                        sssThicknessPack ? sssThicknessPack : subsurface,
                        RGBAMask, CFULCMask, EarNeckMask,
                        bakeAOStrength, smoothnessMin, smoothnessMax, smoothnessContrast, microNormalStrength,
                        mouthAOPower, nostrilAOPower, lipsAOPower, microSmoothnessMod,
                        rMSM, gMSM, bMSM, aMSM, earMSM, neckMSM, cheekMSM, foreheadMSM, upperLipMSM, chinMSM, unmaskedMSM,
                        subsurfaceScale, sssNormalSoften,
                        rSS, gSS, bSS, aSS, earSS, neckSS, cheekSS, foreheadSS, upperLipSS, chinSS, unmaskedSS,
                        sourceName + "_Mask", "RLHeadMask");
                }
                else
                {
                    bakedMetallicGlossMap = BakeHeadMaskMap(mask, cavityAO,
                        sssThicknessPack ? sssThicknessPack : subsurface,
                        RGBAMask, CFULCMask, EarNeckMask,
                        bakeAOStrength, smoothnessMin, smoothnessMax, smoothnessContrast, microNormalStrength,
                        mouthAOPower, nostrilAOPower, lipsAOPower, microSmoothnessMod,
                        rMSM, gMSM, bMSM, aMSM, earMSM, neckMSM, cheekMSM, foreheadMSM, upperLipMSM, chinMSM, unmaskedMSM,
                        subsurfaceScale, sssNormalSoften,
                        rSS, gSS, bSS, aSS, earSS, neckSS, cheekSS, foreheadSS, upperLipSS, chinSS, unmaskedSS,
                        sourceName + "_MetallicAlpha", "RLHeadMetallicGloss");

                    bakedAOMap = BakeHeadMaskMap(mask, cavityAO,
                        sssThicknessPack ? sssThicknessPack : subsurface,
                        RGBAMask, CFULCMask, EarNeckMask,
                        bakeAOStrength, smoothnessMin, smoothnessMax, smoothnessContrast, microNormalStrength,
                        mouthAOPower, nostrilAOPower, lipsAOPower, microSmoothnessMod,
                        rMSM, gMSM, bMSM, aMSM, earMSM, neckMSM, cheekMSM, foreheadMSM, upperLipMSM, chinMSM, unmaskedMSM,
                        subsurfaceScale, sssNormalSoften,
                        rSS, gSS, bSS, aSS, earSS, neckSS, cheekSS, foreheadSS, upperLipSS, chinSS, unmaskedSS,
                        sourceName + "_Occlusion", "RLHeadAO");
                }

                bakedSubsurfaceMap = BakeHeadSubsurfaceMap(
                    sssThicknessPack ? sssThicknessPack : subsurface,
                    sssThicknessPack ? sssThicknessPack : thickness,
                    RGBAMask, CFULCMask, EarNeckMask,
                    subsurfaceScale,
                    rSS, gSS, bSS, aSS, earSS, neckSS, cheekSS, foreheadSS, upperLipSS, chinSS, unmaskedSS,
                    thicknessScaleMin, thicknessScale,
                    Color.white,
                    Texture2D.whiteTexture,
                    true,
                    sourceName + "_SSSMap");

                if (useWrinkleMaps && diffuseTextures.Count == 3 && normalTextures.Count == 3 && roughnessPack)
                {
                    // set 1
                    bakedBaseMap1 = BakeHeadDiffuseMap(diffuseTextures[0], colorBlend, cavityAO,
                        colorBlendStrength, mouthAOPower, nostrilAOPower, lipsAOPower,
                        sourceName + "_Wrinkle_BaseMap1");

                    bakedNormalMap1 = BakeHeadNormalMap(normalTextures[0], normalBlend,
                        sssThicknessPack ? sssThicknessPack : subsurface,
                        RGBAMask, CFULCMask, EarNeckMask,
                        normalStrength, normalBlendStrength, sssNormalSoften,
                        subsurfaceScale,
                        rSS, gSS, bSS, aSS, earSS, neckSS, cheekSS, foreheadSS, upperLipSS, chinSS, unmaskedSS,
                        sourceName + "_Wrinkle_Normal1");

                    // set 2
                    bakedBaseMap2 = BakeHeadDiffuseMap(diffuseTextures[1], colorBlend, cavityAO,
                        colorBlendStrength, mouthAOPower, nostrilAOPower, lipsAOPower,
                        sourceName + "_Wrinkle_BaseMap2");

                    bakedNormalMap2 = BakeHeadNormalMap(normalTextures[1], normalBlend,
                        sssThicknessPack ? sssThicknessPack : subsurface,
                        RGBAMask, CFULCMask, EarNeckMask,
                        normalStrength, normalBlendStrength, sssNormalSoften,
                        subsurfaceScale,
                        rSS, gSS, bSS, aSS, earSS, neckSS, cheekSS, foreheadSS, upperLipSS, chinSS, unmaskedSS,
                        sourceName + "_Wrinkle_Normal2");

                    // set 3
                    bakedBaseMap3 = BakeHeadDiffuseMap(diffuseTextures[2], colorBlend, cavityAO,
                        colorBlendStrength, mouthAOPower, nostrilAOPower, lipsAOPower,
                        sourceName + "_Wrinkle_BaseMap3");

                    bakedNormalMap3 = BakeHeadNormalMap(normalTextures[2], normalBlend,
                        sssThicknessPack ? sssThicknessPack : subsurface,
                        RGBAMask, CFULCMask, EarNeckMask,
                        normalStrength, normalBlendStrength, sssNormalSoften,
                        subsurfaceScale,
                        rSS, gSS, bSS, aSS, earSS, neckSS, cheekSS, foreheadSS, upperLipSS, chinSS, unmaskedSS,
                        sourceName + "_Wrinkle_Normal3");

                    // packed smoothness
                    bakedSmoothnessPack = BakeHeadWrinkleSmoothnessPack(
                        roughnessPack, roughnessPack, roughnessPack,
                        cavityAO, RGBAMask, CFULCMask, EarNeckMask,
                        smoothnessMin, smoothnessMax, smoothnessContrast,
                        mouthAOPower, nostrilAOPower, lipsAOPower, microSmoothnessMod,
                        rMSM, gMSM, bMSM, aMSM, earMSM, neckMSM, cheekMSM,
                        foreheadMSM, upperLipMSM, chinMSM, unmaskedMSM,
                        sourceName + "_Wrinkle_SmoothnessPack");

                    Texture2D[] bakedDiffuseTextures = new Texture2D[] { bakedBaseMap1, bakedBaseMap2, bakedBaseMap3 };
                    string assetFolder = Util.GetAssetFolder(bakedBaseMap1, bakedBaseMap2, bakedBaseMap3);
                    string path = Path.Combine(assetFolder, sourceName + "_Wrinkle_DiffuseArray.asset");
                    bakedDiffuseArray = CreateTextureArray(bakedDiffuseTextures, path, false);

                    Texture2D[] bakedNormalTextures = new Texture2D[] { bakedNormalMap1, bakedNormalMap2, bakedNormalMap3 };
                    assetFolder = Util.GetAssetFolder(bakedNormalMap1, bakedNormalMap2, bakedNormalMap3);
                    path = Path.Combine(assetFolder, sourceName + "_Wrinkle_NormalArray.asset");
                    bakedNormalArray = CreateTextureArray(bakedNormalTextures, path, true);
                }
            }
            else
            {
                bakedNormalMap = BakeSkinNormalMap(normal, normalBlend,
                    sssThicknessPack ? sssThicknessPack : subsurface,
                    RGBAMask,
                    normalStrength, normalBlendStrength, sssNormalSoften,
                    subsurfaceScale,
                    rSS, gSS, bSS, aSS, unmaskedSS,
                    sourceName + "_Normal");

                if (IS_HDRP)
                {
                    bakedMaskMap = BakeSkinMaskMap(mask,
                        sssThicknessPack ? sssThicknessPack : subsurface,
                        RGBAMask,
                        bakeAOStrength, smoothnessMin, smoothnessMax, smoothnessContrast, microNormalStrength, microSmoothnessMod,
                        rMSM, gMSM, bMSM, aMSM, unmaskedMSM,
                        subsurfaceScale, sssNormalSoften,
                        rSS, gSS, bSS, aSS, unmaskedSS,
                        sourceName + "_Mask", "RLSkinMask");
                }
                else
                {
                    bakedMetallicGlossMap = BakeSkinMaskMap(mask,
                        sssThicknessPack ? sssThicknessPack : subsurface,
                        RGBAMask,
                        bakeAOStrength, smoothnessMin, smoothnessMax, smoothnessContrast, microNormalStrength, microSmoothnessMod,
                        rMSM, gMSM, bMSM, aMSM, unmaskedMSM,
                        subsurfaceScale, sssNormalSoften,
                        rSS, gSS, bSS, aSS, unmaskedSS,
                        sourceName + "_MetallicAlpha", "RLSkinMetallicGloss");

                    bakedAOMap = BakeSkinMaskMap(mask,
                        sssThicknessPack ? sssThicknessPack : subsurface,
                        RGBAMask,
                        bakeAOStrength, smoothnessMin, smoothnessMax, smoothnessContrast, microNormalStrength, microSmoothnessMod,
                        rMSM, gMSM, bMSM, aMSM, unmaskedMSM,
                        subsurfaceScale, sssNormalSoften,
                        rSS, gSS, bSS, aSS, unmaskedSS,
                        sourceName + "_Occlusion", "RLSkinAO");
                }

                bakedSubsurfaceMap = BakeSkinSubsurfaceMap(
                    sssThicknessPack ? sssThicknessPack : subsurface,
                    sssThicknessPack ? sssThicknessPack : thickness,
                    RGBAMask,
                    subsurfaceScale,
                    rSS, gSS, bSS, aSS, unmaskedSS,
                    thicknessScaleMin, thicknessScale,
                    Color.white,
                    Texture2D.whiteTexture,
                    true,
                    sourceName + "_SSSMap");
            }

            if (IS_HDRP)
                // HDRP packs the detail micro normal into the YW channels, for better precision.
                bakedDetailMap = BakeDetailMap(microNormal,
                    sourceName + "_Detail");
            else
                // URP and Built-in uses the micro normal directly, but needs a separate detail mask.
                bakedDetailMask = BakeDetailMaskMap(mask,
                    microNormalStrength,
                    sourceName + "_DetailMask");

            bakedThicknessMap = BakeThicknessMap(sssThicknessPack ? sssThicknessPack : thickness,
                0f, 1.0f, Color.white,
                Texture2D.whiteTexture,
                IS_HDRP ? true : false,
                sourceName + "_Thickness");

            MaterialType materialType = MaterialType.Skin;
            if (sourceName.iContains("Skin_Head")) materialType = MaterialType.Head;

            Material templateMaterial = Pipeline.GetTemplateMaterial(sourceName, materialType,
                                            MaterialQuality.Baked, characterInfo, useDigitalHuman);

            Material result = CreateBakedMaterial(mat, bakedBaseMap, bakedMaskMap, bakedMetallicGlossMap, bakedAOMap, bakedNormalMap,
                bakedDetailMask, bakedDetailMap, bakedSubsurfaceMap, bakedThicknessMap, emissionMap, displacement,
                1.0f, microNormalTiling, microNormalStrength, displacementStrength, displacementLevel, emissiveColor,
                sourceName, templateMaterial);

            result.SetFloatIf("_AOStrength", aoStrength);
            result.SetFloatIf("_UseCavity", useCavity ? 1f : 0f);
            result.SetTextureIf("_CavityMap", cavity);
            result.SetFloatIf("_CavityStrength", cavityStrength);
            result.SetFloatIf("_EdgePower", edgePower);
            result.SetEnumKeyword("ENUM_DISPLACEMENT_MODE", displacementMode, ENUM_DISPLACEMENT_MODE);
            result.SetTextureIf("_DisplacementMap", displacement);
            result.SetFloatIf("_BumpStrength", bumpStrength);
            result.SetFloatIf("_DisplacementStrength", displacementStrength);
            // make sure only the head material gets a wrinkle mode
            result.SetEnumKeyword("ENUM_WRINKLE_MODE", (isHead && useWrinkleMaps) ? wrinkleMode : 0f, ENUM_WRINKLE_MODE);
            result.SetFloatIf("_WrinkleDisplacementStrength", (isHead && useWrinkleMaps) ? wrinkleDisplacementStrength : 0f);
            result.SetFloatIf("_SecondarySmoothness", secondarySmoothness);
            result.SetFloatIf("_SecondaryMix", secondaryMix);

            CopyAdditionalSettings(mat, result);

            result.SetColorIf("_BaseColor", diffuseColor);
            result.SetColorIf("_Color", diffuseColor);
            // skin shaders have translucency wrap of 0.2, LitSSS is 0.5, 0.4 = 0.2 / 0.5
            result.SetFloatIf("_SubsurfaceMask", 0.4f);
            result.SetFloatIf("_Thickness", thicknessScale);
            result.SetColorIf("_SubsurfaceFalloff", subsurfaceFalloff);
            result.SetRemapRange("_ThicknessRemap", thicknessScaleMin, thicknessScale);

            if (useWrinkleMaps)
            {
                result.SetTextureIf("_WrinkleMaskSetArray", maskSetArray);
                result.SetTextureIf("_WrinkleDiffuseArray", bakedDiffuseArray);
                result.SetTextureIf("_WrinkleNormalArray", bakedNormalArray);
                result.SetTextureIf("_WrinkleSmoothnessPack", bakedSmoothnessPack);
                result.SetTextureIf("_WrinkleFlowPack", flowPack);
                result.SetTextureIf("_WrinkleDisplacementPack", displacementPack);
            }
            else
            {
                result.SetTextureIf("_WrinkleMaskSetArray", null);
                result.SetTextureIf("_WrinkleDiffuseArray", null);
                result.SetTextureIf("_WrinkleNormalArray", null);
                result.SetTextureIf("_WrinkleSmoothnessPack", null);
                result.SetTextureIf("_WrinkleFlowPack", null);
                result.SetTextureIf("_WrinkleDisplacementPack", null);
            }

            // remake diffuse Blur from baked base map
            if (result.HasProperty("_SubsurfaceBlurMap") && bakedBaseMap)
            {
                result.SetBooleanKeyword("BOOLEAN_USE_SSS", useCustomSSS);
                if (useCustomSSS)
                {
                    Texture2D diffuseBlur = GuassianBlurTexture(bakedBaseMap, 512, 24, 4f, "BDiffuseBlur", sourceName);
                    if (diffuseBlur) result.SetTextureIf("_SubsurfaceBlurMap", diffuseBlur);
                }
            }

            return result;
        }

        private Material BakeTeethMaterial(Material mat, string sourceName)
        {
            Texture2D diffuse = GetMaterialTexture(mat, "_DiffuseMap");
            Texture2D mask = GetMaterialTexture(mat, "_MaskMap");
            Texture2D normal = GetMaterialTexture(mat, "_NormalMap", true);
            Texture2D microNormal = GetMaterialTexture(mat, "_MicroNormalMap", true);
            Texture2D gumsMask = GetMaterialTexture(mat, "_GumsMaskMap");
            Texture2D gradientAO = GetMaterialTexture(mat, "_GradientAOMap");
            float normalStrength = mat.GetFloatIf("_NormalStrength");
            float microNormalStrength = mat.GetFloatIf("_MicroNormalStrength");
            float microNormalTiling = mat.GetFloatIf("_MicroNormalTiling");
            float aoStrength = mat.GetFloatIf("_AOStrength");
            float smoothnessContrast = mat.GetFloatIf("_SmoothnessContrast");
            float smoothnessFront = mat.GetFloatIf("_SmoothnessFront");
            float smoothnessRear = mat.GetFloatIf("_SmoothnessRear");
            float smoothnessMax = mat.GetFloatIf("_SmoothnessMax");
            float gumsSaturation = mat.GetFloatIf("_GumsSaturation");
            float gumsBrightness = mat.GetFloatIf("_GumsBrightness");
            float teethSaturation = mat.GetFloatIf("_TeethSaturation");
            float teethBrightness = mat.GetFloatIf("_TeethBrightness");
            float frontAO = mat.GetFloatIf("_FrontAO");
            float rearAO = mat.GetFloatIf("_RearAO");
            float teethSSS = mat.GetFloatIf("_TeethSSS");
            float gumsSSS = mat.GetFloatIf("_GumsSSS");
            float teethThickness = mat.GetFloatIf("_TeethThickness");
            float gumsThickness = mat.GetFloatIf("_GumsThickness");
            float isUpperTeeth = mat.GetFloatIf("_IsUpperTeeth");
            Texture2D emission = GetMaterialTexture(mat, "_EmissionMap");
            Color emissiveColor = mat.GetColorIf("_EmissiveColor", Color.black);
            Color subsurfaceFalloff = mat.GetColorIf("_SubsurfaceFalloff", Color.white);
            if (IS_HDRP) subsurfaceFalloff = Color.white;

            bool useAmplify = characterInfo.BakeCustomShaders && mat.shader.name.iContains("/Amplify/");
            bool useTessellation = characterInfo.BuiltFeatureTessellation;
            bool useDigitalHuman = characterInfo.BakeCustomShaders && mat.shader.name.iEndsWith("_DH");

            Texture2D bakedBaseMap = diffuse;
            Texture2D bakedMaskMap = mask;
            Texture2D bakedMetallicGlossMap = null;
            Texture2D bakedAOMap = null;
            Texture2D bakedNormalMap = normal;
            Texture2D bakedDetailMap = (IS_HDRP ? null : microNormal);
            Texture2D bakedDetailMask = null;
            Texture2D bakedSubsurfaceMap = null;
            Texture2D bakedThicknessMap = null;
            Texture2D emissionMap = emission;

            bakedBaseMap = BakeTeethDiffuseMap(diffuse, gumsMask, gradientAO,
                isUpperTeeth, frontAO, rearAO, gumsSaturation, gumsBrightness, teethSaturation, teethBrightness,
                sourceName + "_BaseMap");

            if (IS_HDRP)
            {
                bakedMaskMap = BakeTeethMaskMap(mask, gradientAO,
                    isUpperTeeth, aoStrength, smoothnessFront, smoothnessRear, smoothnessMax, smoothnessContrast,
                    microNormalStrength,
                    sourceName + "_Mask", "RLTeethMask");
            }
            else
            {
                bakedMetallicGlossMap = BakeTeethMaskMap(mask, gradientAO,
                    isUpperTeeth, aoStrength, smoothnessFront, smoothnessRear, smoothnessMax, smoothnessContrast,
                    microNormalStrength,
                    sourceName + "_MetallicAlpha", "RLTeethMetallicGloss");

                bakedAOMap = BakeTeethMaskMap(mask, gradientAO,
                    isUpperTeeth, aoStrength, smoothnessFront, smoothnessRear, smoothnessMax, smoothnessContrast,
                    microNormalStrength,
                    sourceName + "_Occlusion", "RLTeethAO");
            }

            if (IS_HDRP)
                // HDRP packs the detail micro normal into the YW channels, for better precision.
                bakedDetailMap = BakeDetailMap(microNormal,
                    sourceName + "_Detail");
            else
                // URP and Built-in uses the micro normal directly, but needs a separate detail mask.
                bakedDetailMask = BakeDetailMaskMap(mask,
                    microNormalStrength,
                    sourceName + "_DetailMask");

            bakedSubsurfaceMap = BakeTeethSubsurfaceMap(gumsMask,
                gumsSSS, teethSSS, subsurfaceFalloff,
                IS_HDRP ? Texture2D.whiteTexture : bakedBaseMap,
                sourceName + "_SSSMap");

            bakedThicknessMap = BakeTeethThicknessMap(gumsMask,
                gumsThickness, teethThickness, subsurfaceFalloff,
                IS_HDRP ? Texture2D.whiteTexture : bakedBaseMap,
                sourceName + "_Thickness");

            Material result = CreateBakedMaterial(mat, bakedBaseMap, bakedMaskMap, bakedMetallicGlossMap, bakedAOMap, bakedNormalMap,
                bakedDetailMask, bakedDetailMap, bakedSubsurfaceMap, bakedThicknessMap, emissionMap, null,
                normalStrength, microNormalTiling, microNormalStrength, 0f, 0.5f, emissiveColor,
                sourceName,
                Pipeline.GetTemplateMaterial(sourceName, MaterialType.Teeth,
                                             MaterialQuality.Baked, 
                                             characterInfo, useDigitalHuman));

            CopyAdditionalSettings(mat, result);

            return result;
        }

        private Material BakeTongueMaterial(Material mat, string sourceName)
        {
            Texture2D diffuse = GetMaterialTexture(mat, "_DiffuseMap");
            Texture2D mask = GetMaterialTexture(mat, "_MaskMap");
            Texture2D normal = GetMaterialTexture(mat, "_NormalMap", true);
            Texture2D microNormal = GetMaterialTexture(mat, "_MicroNormalMap", true);
            Texture2D gradientAO = GetMaterialTexture(mat, "_GradientAOMap");
            float normalStrength = mat.GetFloatIf("_NormalStrength");
            float microNormalStrength = mat.GetFloatIf("_MicroNormalStrength");
            float microNormalTiling = mat.GetFloatIf("_MicroNormalTiling");
            float aoStrength = mat.GetFloatIf("_AOStrength");
            float smoothnessContrast = mat.GetFloatIf("_SmoothnessContrast");
            float smoothnessFront = mat.GetFloatIf("_SmoothnessFront");
            float smoothnessRear = mat.GetFloatIf("_SmoothnessRear");
            float smoothnessMax = mat.GetFloatIf("_SmoothnessMax");
            float tongueSaturation = mat.GetFloatIf("_TongueSaturation");
            float tongueBrightness = mat.GetFloatIf("_TongueBrightness");
            float frontAO = mat.GetFloatIf("_FrontAO");
            float rearAO = mat.GetFloatIf("_RearAO");
            float tongueSSS = mat.GetFloatIf("_TongueSSS");
            float tongueThickness = mat.GetFloatIf("_TongueThickness");
            Texture2D emission = GetMaterialTexture(mat, "_EmissionMap");
            Color emissiveColor = mat.GetColorIf("_EmissiveColor", Color.black);
            Color subsurfaceFalloff = mat.GetColorIf("_SubsurfaceFalloff", Color.white);
            if (IS_HDRP) subsurfaceFalloff = Color.white;

            bool useAmplify = characterInfo.BakeCustomShaders && mat.shader.name.iContains("/Amplify/");
            bool useTessellation = characterInfo.BuiltFeatureTessellation;
            bool useDigitalHuman = characterInfo.BakeCustomShaders && mat.shader.name.iEndsWith("_DH");

            Texture2D bakedBaseMap = diffuse;
            Texture2D bakedMaskMap = mask;
            Texture2D bakedMetallicGlossMap = null;
            Texture2D bakedAOMap = null;
            Texture2D bakedNormalMap = normal;
            Texture2D bakedDetailMap = (IS_HDRP ? null : microNormal);
            Texture2D bakedDetailMask = null;
            Texture2D bakedSubsurfaceMap = null;
            Texture2D bakedThicknessMap = null;
            Texture2D emissionMap = emission;

            bakedBaseMap = BakeTongueDiffuseMap(diffuse, gradientAO,
                frontAO, rearAO, tongueSaturation, tongueBrightness,
                sourceName + "_BaseMap");

            if (IS_HDRP)
            {
                bakedMaskMap = BakeTongueMaskMap(mask, gradientAO,
                    aoStrength, smoothnessFront, smoothnessRear, smoothnessMax, smoothnessContrast,
                    microNormalStrength,
                    sourceName + "_Mask", "RLTongueMask");
            }
            else
            {
                bakedMetallicGlossMap = BakeTongueMaskMap(mask, gradientAO,
                    aoStrength, smoothnessFront, smoothnessRear, smoothnessMax, smoothnessContrast,
                    microNormalStrength,
                    sourceName + "_MetallicAlpha", "RLTongueMetallicGloss");

                bakedAOMap = BakeTongueMaskMap(mask, gradientAO,
                    aoStrength, smoothnessFront, smoothnessRear, smoothnessMax, smoothnessContrast,
                    microNormalStrength,
                    sourceName + "_Occlusion", "RLTongueAO");
            }

            if (IS_HDRP)
            {
                // HDRP packs the detail micro normal into the YW channels, for better precision.
                bakedDetailMap = BakeDetailMap(microNormal,
                    sourceName + "_Detail");
            }
            else
            {
                // URP and Built-in uses the micro normal directly, but needs a separate detail mask.
                bakedDetailMask = BakeDetailMaskMap(mask,
                    microNormalStrength,
                    sourceName + "_DetailMask");

                bakedSubsurfaceMap = BakeSubsurfaceMap(Texture2D.whiteTexture, 1.0f, subsurfaceFalloff,
                    IS_HDRP ? Texture2D.whiteTexture : bakedBaseMap,
                    sourceName + "_SSSMap");

                bakedThicknessMap = BakeThicknessMap(Texture2D.whiteTexture, 0f, 1.0f, subsurfaceFalloff,
                    IS_HDRP ? Texture2D.whiteTexture : bakedBaseMap, false,
                    sourceName + "_Thickness");
            }

            Material result = CreateBakedMaterial(mat, bakedBaseMap, bakedMaskMap, bakedMetallicGlossMap, bakedAOMap, bakedNormalMap,
                bakedDetailMask, bakedDetailMap, bakedSubsurfaceMap, bakedThicknessMap, emissionMap, null,
                normalStrength, microNormalTiling, microNormalStrength, 0f, 0.5f, emissiveColor,
                sourceName,
                Pipeline.GetTemplateMaterial(sourceName, MaterialType.Tongue,
                                             MaterialQuality.Baked, characterInfo, 
                                             useDigitalHuman));

            CopyAdditionalSettings(mat, result);

            result.SetFloatIf("_SubsurfaceMask", tongueSSS);
            result.SetFloatIf("_Thickness", tongueThickness);
            return result;
        }

        private Material BakeEyeMaterial(Material mat, string sourceName)
        {
            Texture2D sclera = GetMaterialTexture(mat, "_ScleraDiffuseMap");
            Texture2D cornea = GetMaterialTexture(mat, "_CorneaDiffuseMap");
            Texture2D blend = GetMaterialTexture(mat, "_ColorBlendMap");
            Texture2D mask = GetMaterialTexture(mat, "_MaskMap");
            Texture2D microNormal = GetMaterialTexture(mat, "_ScleraNormalMap", true);
            float microNormalStrength = mat.GetFloatIf("_ScleraNormalStrength");
            float microNormalTiling = mat.GetFloatIf("_ScleraNormalTiling");
            float aoStrength = mat.GetFloatIf("_AOStrength");
            float colorBlendStrength = mat.GetFloatIf("_ColorBlendStrength");
            float scleraSmoothness = mat.GetFloatIf("_ScleraSmoothness");
            float irisSmoothness = mat.GetFloatIf("_IrisSmoothness");
            float corneaSmoothness = mat.GetFloatIf("_CorneaSmoothness");
            float irisHue = mat.GetFloatIf("_IrisHue");
            float irisSaturation = mat.GetFloatIf("_IrisSaturation");
            float irisBrightness = mat.GetFloatIf("_IrisBrightness");
            float scleraHue = mat.GetFloatIf("_ScleraHue");
            float scleraSaturation = mat.GetFloatIf("_ScleraSaturation");
            float scleraBrightness = mat.GetFloatIf("_ScleraBrightness");
            float refractionThickness = mat.GetFloatIf("_RefractionThickness");
            float shadowRadius = mat.GetFloatIf("_ShadowRadius");
            float shadowHardness = mat.GetFloatIf("_ShadowHardness");
            float scleraScale = mat.GetFloatIf("_ScleraScale");
            float limbusDarkRadius = mat.GetFloatIf("_LimbusDarkRadius");
            float limbusDarkWidth = mat.GetFloatIf("_LimbusDarkWidth");
            float limbusShading = mat.GetFloatIf("_LimbusShading");
            float limbusContrast = mat.GetFloatIf("_LimbusContrast", 1f);
            float irisRadius = mat.GetFloatIf("_IrisRadius");
            float limbusWidth = mat.GetFloatIf("_LimbusWidth");
            float ior = mat.GetFloatIf("_IOR");
            float irisDepth = mat.GetFloatIf("_IrisDepth");
            float pupilScale = mat.GetFloatIf("_PupilScale");
            float parallaxMod = mat.GetFloatIf("_PMod");
            float scleraSubsurfaceScale = mat.GetFloatIf("_ScleraSubsurfaceScale");
            float irisSubsurfaceScale = mat.GetFloatIf("_IrisSubsurfaceScale");
            float subsurfaceThickness = mat.GetFloatIf("_SubsurfaceThickness");
            float useDepth = mat.GetFloatIf("_UseDepth");
            Vector4 depthVector = mat.GetVectorIf("_DepthVector");

            Color cornerShadowColor = mat.GetColorIf("_CornerShadowColor", Color.red);
            Color irisColor = mat.GetColorIf("_IrisColor", Color.white);
            Color irisCloudyColor = mat.GetColorIf("_IrisCloudyColor", Color.black);
            Color limbusColor = mat.GetColorIf("_LimbusColor", Color.black);
            bool isCornea = sourceName.iContains("Cornea");
            bool isLeftEye = mat.GetFloatIf("_IsLeftEye") > 0f;
            Texture2D emission = GetMaterialTexture(mat, "_EmissionMap");
            Color emissiveColor = mat.GetColorIf("_EmissiveColor", Color.black);
            Color subsurfaceFalloff = mat.GetColorIf("_SubsurfaceFalloff", Color.white);

            bool useAmplify = characterInfo.BakeCustomShaders && mat.shader.name.iContains("/Amplify/");
            bool useTessellation = characterInfo.BuiltFeatureTessellation;
            bool useDigitalHuman = characterInfo.BakeCustomShaders && mat.shader.name.iEndsWith("_DH");

            Texture2D bakedBaseMap = cornea;
            Texture2D bakedSecondaryMap = null;
            Texture2D bakedMaskMap = mask;
            Texture2D bakedMetallicGlossMap = null;
            Texture2D bakedAOMap = null;
            Texture2D bakedNormalMap = null;
            Texture2D bakedDetailMap = (IS_HDRP ? null : microNormal);
            Texture2D bakedDetailMask = null;
            Texture2D bakedSubsurfaceMap = null;
            Texture2D bakedThicknessMap = null;
            Texture2D emissionMap = emission;

            if (isCornea)
            {
                bakedBaseMap = BakeCorneaDiffuseMap(cornea, sclera, blend,
                    scleraScale, scleraHue, scleraSaturation, scleraBrightness,
                    irisHue, irisSaturation, irisBrightness, irisRadius, irisColor, irisCloudyColor,
                    limbusShading, limbusContrast,
                    limbusWidth, limbusDarkRadius, limbusDarkWidth, limbusColor,
                    shadowRadius, shadowHardness, cornerShadowColor,
                    colorBlendStrength,
                    sourceName + "_BaseMap", CUSTOM_SHADERS ? "RLCorneaDiffuse" : "RLCorneaSingleDiffuse");

                if (CUSTOM_SHADERS)
                {
                    bakedSecondaryMap = BakeEyeDiffuseMap(cornea, blend,
                        scleraScale, irisRadius, irisHue, irisSaturation, irisBrightness, irisColor, irisCloudyColor,
                        limbusWidth, limbusDarkRadius, limbusDarkWidth, limbusColor,
                        limbusShading, limbusContrast,
                        colorBlendStrength,
                        sourceName + "_SecondaryMap");
                }

                if (IS_HDRP)
                {
                    bakedMaskMap = BakeCorneaMaskMap(mask, aoStrength, corneaSmoothness,
                        scleraSmoothness,
                        irisRadius, limbusWidth, microNormalStrength,
                        sourceName + "_Mask", "RLCorneaMask");
                }
                else
                {
                    // TODO if (REFRACTIVE_EYES || BASIC_SHADERS) for URP/3D ???

                    bakedMetallicGlossMap = BakeCorneaMaskMap(mask, aoStrength, corneaSmoothness,
                        scleraSmoothness,
                        irisRadius, limbusWidth, microNormalStrength,
                        sourceName + "_MetallicAlpha", "RLCorneaMetallicGloss");

                    bakedAOMap = BakeCorneaMaskMap(mask, aoStrength, corneaSmoothness,
                        scleraSmoothness,
                        irisRadius, limbusWidth, microNormalStrength,
                        sourceName + "_Occlusion", "RLCorneaAO");
                }

                if (IS_HDRP && (!CUSTOM_SHADERS || characterInfo.RefractiveEyes))
                    // HDRP packs the detail micro normal into the YW channels, for better precision.
                    bakedDetailMap = BakeDetailMap(microNormal,
                        sourceName + "_Detail");

                if (CUSTOM_SHADERS && !REFRACTIVE_EYES)
                    // various baked mask maps for the baked shaders
                    bakedDetailMask = BakeCorneaDetailMaskMap(mask,
                        scleraScale, irisRadius,
                        limbusWidth, limbusDarkRadius, limbusDarkWidth,
                        microNormalStrength,
                        sourceName + "_DetailMask");

                if (REFRACTIVE_EYES)
                {
                    // RGB textures cannot store low enough values for the refraction thickness
                    // so normalize it and use the thickness remap in the HDRP/Lit shader.
                    bakedThicknessMap = BakeCorneaThicknessMap(limbusDarkRadius,
                    limbusDarkWidth, refractionThickness / 0.025f,
                    sourceName + "_Thickness");
                }
                else
                {
                    bakedSubsurfaceMap = BakeCorneaSubsurfaceMask(IS_HDRP ? Texture2D.whiteTexture : bakedBaseMap,
                        scleraSubsurfaceScale, irisSubsurfaceScale, subsurfaceThickness,
                        IS_HDRP ? Color.white : subsurfaceFalloff,
                        sourceName + "_Subsurface");
                }
            }
            else
            {
                bakedBaseMap = BakeEyeDiffuseMap(cornea, blend,
                    scleraScale, irisRadius, irisHue, irisSaturation, irisBrightness, irisColor, irisCloudyColor,
                    limbusWidth, limbusDarkRadius, limbusDarkWidth, limbusColor,
                    limbusShading, limbusContrast, colorBlendStrength,
                    sourceName + "_BaseMap");

                bakedMaskMap = BakeEyeMaskMap(mask, aoStrength, irisSmoothness,
                    scleraSmoothness,
                    limbusDarkRadius, limbusDarkWidth, irisRadius,
                    sourceName + "_Mask");
            }

            Material result = CreateBakedMaterial(mat, bakedBaseMap, bakedMaskMap, bakedMetallicGlossMap, bakedAOMap, bakedNormalMap,
                bakedDetailMask, bakedDetailMap, bakedSubsurfaceMap, bakedThicknessMap, emissionMap, null,
                1f, microNormalTiling, microNormalStrength, 0f, 0.5f, emissiveColor,
                sourceName, isCornea ? Pipeline.GetTemplateMaterial(sourceName, MaterialType.Cornea,
                                            MaterialQuality.Baked, characterInfo,
                                            useDigitalHuman)
                                     : Pipeline.GetTemplateMaterial(sourceName, MaterialType.Eye,
                                            MaterialQuality.Baked, characterInfo));

            CopyAdditionalSettings(mat, result);

            if (bakedSecondaryMap != null)
                result.SetTextureIf("_SecondaryColorMap", bakedSecondaryMap);

            result.SetColorIf("_LimbusColor", limbusColor);
            result.SetVectorIf("_DepthVector", depthVector);

            if (isCornea)
            {
                result.SetFloatIf("_Ior", ior);
                if (REFRACTIVE_EYES)
                {
                    result.SetFloatIf("_Thickness", refractionThickness / 10f);
                    Color thicknessRemap;
                    thicknessRemap.r = 0f;
                    thicknessRemap.g = 0.025f;
                    thicknessRemap.b = 0f;
                    thicknessRemap.a = 0f;
                    result.SetColorIf("_ThicknessRemap", thicknessRemap);
                }
                else
                {
                    result.SetFloatIf("_IrisDepth", irisDepth);
                    result.SetFloatIf("_PupilScale", pupilScale);
                    result.SetTextureIf("_ScleraNormalMap", microNormal);
                    result.SetFloatIf("_ScleraNormalTiling", microNormalTiling);
                    result.SetFloatIf("_ScleraNormalStrength", microNormalStrength);
                    result.SetFloatIf("_Thickness", subsurfaceThickness);
                    result.SetFloatIf("_PMod", parallaxMod);
                    result.SetFloatIf("_UseDepth", useDepth);
                }
            }
            else
            {
                result.SetFloatIf("_IrisDepth", irisDepth);
                result.SetFloatIf("_PupilScale", pupilScale);
            }
            return result;
        }



        private Material BakeHairMaterial(Material mat, string sourceName,
            out Material firstPass, out Material secondPass)
        {
            Texture2D diffuse = GetMaterialTexture(mat, "_DiffuseMap");
            Texture2D mask = GetMaterialTexture(mat, "_MaskMap");
            Texture2D normal = GetMaterialTexture(mat, "_NormalMap", true);
            Texture2D blend = GetMaterialTexture(mat, "_BlendMap");
            Texture2D flow = GetMaterialTexture(mat, "_FlowMap");
            Texture2D id = GetMaterialTexture(mat, "_IDMap");
            Texture2D root = GetMaterialTexture(mat, "_RootMap");
            Texture2D specular = GetMaterialTexture(mat, "_SpecularMap");
            float flowMapFlipGreen = mat.GetFloatIf("_FlowMapFlipGreen");
            float translucency = mat.GetFloatIf("_Translucency");
            float aoStrength = mat.GetFloatIf("_AOStrength");
            float aoOccludeAll = mat.GetFloatIf("_AOOccludeAll");
            float diffuseStrength = mat.GetFloatIf("_DiffuseStrength");
            float blendStrength = mat.GetFloatIf("_BlendStrength");
            float vertexColorStrength = mat.GetFloatIf("_VertexColorStrength");
            float baseColorStrength = mat.GetFloatIf("_BaseColorStrength");
            float alphaStrength = mat.GetFloatIf("_AlphaStrength", 1f);
            float alphaContrast = mat.GetFloatIf("_AlphaContrast");            
            float alphaClip = mat.GetFloatIf("_AlphaClip");
            if (IS_URP) alphaClip = mat.GetFloatIf("_AlphaClip2");
            float shadowClip = mat.GetFloatIf("_ShadowClip");
            float depthPrepass = mat.GetFloatIf("_DepthPrepass", 1f);
            float depthPostpass = mat.GetFloatIf("_DepthPostpass", 0f);
            float smoothnessMin = mat.GetFloatIf("_SmoothnessMin");
            float smoothnessMax = mat.GetFloatIf("_SmoothnessMax");
            float smoothnessContrast = mat.GetFloatIf("_SmoothnessContrast");
            float globalStrength = mat.GetFloatIf("_GlobalStrength");
            float rootColorStrength = mat.GetFloatIf("_RootColorStrength");
            float endColorStrength = mat.GetFloatIf("_EndColorStrength");
            float invertRootMap = mat.GetFloatIf("_InvertRootMap");
            float highlightBlend = mat.GetFloatIf("_HighlightBlend", 1.0f);
            float highlightAStrength = mat.GetFloatIf("_HighlightAStrength");
            float highlightAOverlapEnd = mat.GetFloatIf("_HighlightAOverlapEnd");
            float highlightAOverlapInvert = mat.GetFloatIf("_HighlightAOverlapInvert");
            float highlightBStrength = mat.GetFloatIf("_HighlightBStrength");
            float highlightBOverlapEnd = mat.GetFloatIf("_HighlightBOverlapEnd");
            float highlightBOverlapInvert = mat.GetFloatIf("_HighlightBOverlapInvert");
            float rimTransmissionIntensity = mat.GetFloatIf("_RimTransmissionIntensity");
            float rimPower = mat.GetFloatIf("_RimPower");
            float specularMultiplier = mat.GetFloatIf("_SpecularMultiplier");
            float specularPowerScale = mat.GetFloatIf("_SpecularPowerScale");
            float specularMix = mat.GetFloatIf("_SpecularMix");
            float specularShiftMin = mat.GetFloatIf("_SpecularShiftMin");
            float specularShiftMax = mat.GetFloatIf("_SpecularShiftMax");
            float secondarySpecularMultiplier = mat.GetFloatIf("_SecondarySpecularMultiplier");
            float secondarySpecularShift = mat.GetFloatIf("_SecondarySpecularShift");
            float secondarySmoothness = mat.GetFloatIf("_SecondarySmoothness");
            float normalStrength = mat.GetFloatIf("_NormalStrength");
            Vector4 highlightADistribution = mat.GetVectorIf("_HighlightADistribution");
            Vector4 highlightBDistribution = mat.GetVectorIf("_HighlightBDistribution");
            Color vertexBaseColor = mat.GetColorIf("_VertexBaseColor", Color.black);
            Color rootColor = mat.GetColorIf("_RootColor", Color.black);
            Color endColor = mat.GetColorIf("_EndColor", Color.white);
            Color highlightAColor = mat.GetColorIf("_HighlightAColor", Color.white);
            Color highlightBColor = mat.GetColorIf("_HighlightBColor", Color.white);
            Color specularTint = mat.GetColorIf("_SpecularTint", Color.white);
            Color diffuseColor = mat.GetColorIf("_DiffuseColor", Color.white);
            bool enableColor = mat.GetFloatIf("BOOLEAN_ENABLECOLOR") > 0f;
            float clipQuality = 0f;
            if (mat.HasProperty("_ENUMCLIPQUALITY_ON"))
                clipQuality = mat.GetFloat("_ENUMCLIPQUALITY_ON");
            else if (mat.HasProperty("_ClipQuality"))
                clipQuality = mat.GetFloat("_ClipQuality");
            Texture2D emission = GetMaterialTexture(mat, "_EmissionMap");
            Color emissiveColor = mat.GetColorIf("_EmissiveColor", Color.black);

            firstPass = null;
            secondPass = null;

            bool useAmplify = characterInfo.BakeCustomShaders && mat.shader.name.iContains("/Amplify/");
            bool useTessellation = characterInfo.BuiltFeatureTessellation;
            bool useWrinkleMaps = characterInfo.BuiltFeatureWrinkleMaps;
            bool useDigitalHuman = characterInfo.BakeCustomShaders && mat.shader.name.iEndsWith("_DH");
            float diffuseAO = (useAmplify || (IS_URP && CUSTOM_SHADERS)) ? 0f : aoOccludeAll;

            Texture2D bakedBaseMap = diffuse;
            Texture2D bakedMaskMap = mask;
            Texture2D bakedMetallicGlossMap = null;
            Texture2D bakedAOMap = null;
            Texture2D bakedNormalMap = normal;
            Texture2D emissionMap = emission;

            if (enableColor)
            {
                bakedBaseMap = BakeHairDiffuseMap(diffuse, blend, id, root, mask,
                    diffuseStrength, alphaStrength, alphaContrast, aoStrength, diffuseAO,
                    rootColor, rootColorStrength, endColor, endColorStrength, globalStrength,
                    invertRootMap, baseColorStrength, highlightBlend,
                    highlightAColor, highlightADistribution, highlightAOverlapEnd,
                    highlightAOverlapInvert, highlightAStrength,
                    highlightBColor, highlightBDistribution, highlightBOverlapEnd,
                    highlightBOverlapInvert, highlightBStrength,
                    blendStrength, vertexBaseColor, vertexColorStrength,
                    sourceName + "_BaseMap");
            }
            else
            {
                bakedBaseMap = BakeHairDiffuseMap(diffuse, blend, mask,
                    diffuseStrength, alphaStrength, alphaContrast, aoStrength, diffuseAO,
                    blendStrength, vertexBaseColor, vertexColorStrength,
                    sourceName + "_BaseMap");
            }

            if (IS_HDRP)
            {
                bakedMaskMap = BakeHairMaskMap(mask, specular,
                    aoStrength, 0f,
                    smoothnessMin, smoothnessMax, smoothnessContrast,
                    sourceName + "_Mask", "RLHairMask");
            }
            else
            {
                bakedMetallicGlossMap = BakeHairMaskMap(mask, specular,
                    aoStrength, (useAmplify ? 0f : aoOccludeAll),
                    smoothnessMin, smoothnessMax, smoothnessContrast,
                    sourceName + "_MetallicAlpha", "RLHairMetallicGloss");

                bakedAOMap = BakeHairMaskMap(mask, specular,
                    aoStrength, (useAmplify ? 0f : aoOccludeAll),
                    smoothnessMin, smoothnessMax, smoothnessContrast,
                    sourceName + "_Occlusion", "RLHairAO");
            }

            if (CUSTOM_SHADERS)
            {
                Action<Material> SetCustom = (bakeMat) =>
                {
                    bakeMat.SetFloatIf("_AOOccludeAll", aoOccludeAll);                    
                    bakeMat.SetTextureIf("_FlowMap", flow);
                    bakeMat.SetFloatIf("_FlowMapFlipGreen", flowMapFlipGreen);
                    bakeMat.SetFloatIf("_Translucency", translucency);
                    bakeMat.SetTextureIf("_IDMap", id);
                    bakeMat.SetColorIf("_VertexBaseColor", vertexBaseColor);
                    bakeMat.SetFloatIf("_VertexColorStrength", vertexColorStrength);
                    bakeMat.SetColorIf("_SpecularTint", specularTint);
                    bakeMat.SetFloatIf("_AlphaClip", alphaClip);
                    bakeMat.SetFloatIf("_AlphaClip2", alphaClip);
                    bakeMat.SetFloatIf("_ShadowClip", shadowClip);
                    bakeMat.SetFloatIf("_DepthPrepass", depthPrepass); // Mathf.Lerp(depthPrepass, 1.0f, 0.5f));
                    bakeMat.SetFloatIf("_DepthPostpass", depthPostpass);
                    bakeMat.SetFloatIf("_RimTransmissionIntensity", rimTransmissionIntensity);
                    bakeMat.SetFloatIf("_RimPower", rimPower);
                    bakeMat.SetFloatIf("_SpecularMultiplier", specularMultiplier);
                    bakeMat.SetFloatIf("_SpecularPowerScale", specularPowerScale);
                    bakeMat.SetFloatIf("_SpecularShiftMin", specularShiftMin);
                    bakeMat.SetFloatIf("_SpecularShiftMax", specularShiftMax);
                    bakeMat.SetFloatIf("_SpecularMix", specularMix);
                    bakeMat.SetFloatIf("_SecondarySpecularMultiplier", secondarySpecularMultiplier);
                    bakeMat.SetFloatIf("_SecondarySpecularShift", secondarySpecularShift);
                    bakeMat.SetFloatIf("_SecondarySmoothness", secondarySmoothness);
                    bakeMat.SetColorIf("_BaseColor", diffuseColor);
                    bakeMat.SetColorIf("_Color", diffuseColor);
                    if (bakeMat.SetFloatIf("_ENUMCLIPQUALITY_ON", clipQuality))
                    {
                        // Shader Graph clip quality:
                        switch (clipQuality)
                        {
                            case 1f:
                                bakeMat.EnableKeyword("_ENUMCLIPQUALITY_ON_NOISE");
                                bakeMat.DisableKeyword("_ENUMCLIPQUALITY_ON_DITHER");
                                break;
                            case 2f:
                                bakeMat.DisableKeyword("_ENUMCLIPQUALITY_ON_NOISE");
                                bakeMat.EnableKeyword("_ENUMCLIPQUALITY_ON_DITHER");
                                break;
                            case 0f:
                            default:
                                bakeMat.DisableKeyword("_ENUMCLIPQUALITY_ON_NOISE");
                                bakeMat.DisableKeyword("_ENUMCLIPQUALITY_ON_DITHER");
                                break;
                        }
                    }
                    // Amplify shader clip quality:
                    if (bakeMat.SetFloatIf("_ClipQuality", clipQuality))
                    {
                        switch (clipQuality)
                        {
                            case 1f:
                                bakeMat.DisableKeyword("_CLIPQUALITY_STANDARD");
                                bakeMat.EnableKeyword("_CLIPQUALITY_DITHERED");
                                bakeMat.DisableKeyword("_CLIPQUALITY_NOISE");
                                break;
                            case 2f:
                                bakeMat.DisableKeyword("_CLIPQUALITY_STANDARD");
                                bakeMat.DisableKeyword("_CLIPQUALITY_DITHERED");
                                bakeMat.EnableKeyword("_CLIPQUALITY_NOISE");
                                break;
                            case 0f:
                            default:
                                bakeMat.EnableKeyword("_CLIPQUALITY_STANDARD");
                                bakeMat.DisableKeyword("_CLIPQUALITY_DITHERED");
                                bakeMat.DisableKeyword("_CLIPQUALITY_NOISE");
                                break;
                        }
                    }
                };

                if (mat.shader.name.iContains(Pipeline.SHADER_HQ_HAIR_1ST_PASS))
                {
                    firstPass = CreateBakedMaterial(mat, bakedBaseMap, bakedMaskMap, bakedMetallicGlossMap, bakedAOMap, bakedNormalMap,
                        null, null, null, null, emissionMap, null,
                        normalStrength, 1f, 1f, 0f, 0.5f, emissiveColor,
                        sourceName + "_1st_Pass",
                        Pipeline.GetUpgradedTemplateMaterial(sourceName, Pipeline.MATERIAL_BAKED_HAIR_CUSTOM_1ST_PASS,
                            MaterialQuality.Baked, useDigitalHuman));

                    secondPass = CreateBakedMaterial(mat, bakedBaseMap, bakedMaskMap, bakedMetallicGlossMap, bakedAOMap, bakedNormalMap,
                        null, null, null, null, emissionMap, null,
                        normalStrength, 1f, 1f, 0f, 0.5f, emissiveColor,
                        sourceName + "_2nd_Pass",
                        Pipeline.GetUpgradedTemplateMaterial(sourceName, Pipeline.MATERIAL_BAKED_HAIR_CUSTOM_2ND_PASS,
                            MaterialQuality.Baked, useDigitalHuman));

                    // multi material pass hair is custom baked shader only:
                    SetCustom(firstPass);
                    alphaClip = 0.01f;
                    depthPostpass = 0f;
                    depthPrepass = 1f;
                    SetCustom(secondPass);
                    return null;
                }
                else
                {
                    Material result = CreateBakedMaterial(mat, bakedBaseMap, bakedMaskMap, bakedMetallicGlossMap, bakedAOMap, bakedNormalMap,
                        null, null, null, null, emissionMap, null,
                        normalStrength, 1f, 1f, 0f, 0.5f, emissiveColor,
                        sourceName,
                        Pipeline.GetTemplateMaterial(sourceName, MaterialType.Hair,
                                    MaterialQuality.Baked, characterInfo,
                                    useDigitalHuman));

                    SetCustom(result);
                    return result;
                }
            }
            else // Non Custom Shaders
            {
                Action<Material> SetBasic = (bakeMat) =>
                {
                    bakeMat.SetColorIf("_SpecularColor", specularTint);
                    bakeMat.SetFloatIf("_AlphaClipThreshold", alphaClip);
                    bakeMat.SetFloatIf("_AlphaThresholdShadow", shadowClip);
                    bakeMat.SetFloatIf("_AlphaClipThresholdDepthPrepass", depthPrepass); // Mathf.Lerp(depthPrepass, 1.0f, 0.5f));
                    bakeMat.SetFloatIf("_AlphaClipThresholdDepthPostpass", depthPostpass);
                    bakeMat.SetFloatIf("_TransmissionRim", rimTransmissionIntensity);
                    bakeMat.SetFloatIf("_Specular", specularMultiplier);
                    bakeMat.SetFloatIf("_SpecularShift", (specularShiftMin + specularShiftMax) * 0.5f);
                    bakeMat.SetFloatIf("_SecondarySpecular", secondarySpecularMultiplier);
                    bakeMat.SetFloatIf("_SecondarySpecularShift", secondarySpecularShift);
                    bakeMat.SetFloatIf("_SmoothnessMin", smoothnessMin);
                    bakeMat.SetFloatIf("_SmoothnessMax", smoothnessMax);
                    bakeMat.SetColorIf("_BaseColor", diffuseColor);
                    bakeMat.SetColorIf("_Color", diffuseColor);
                };

                if (mat.shader.name.iContains(Pipeline.SHADER_HQ_HAIR_1ST_PASS))
                {
                    firstPass = CreateBakedMaterial(mat, bakedBaseMap, bakedMaskMap, bakedMetallicGlossMap, bakedAOMap, bakedNormalMap,
                        null, null, null, null, emissionMap, null,
                        normalStrength, 1f, 1f, 0f, 0.5f, emissiveColor,
                        sourceName + "_1st_Pass",
                        Pipeline.GetUpgradedTemplateMaterial(sourceName, Pipeline.MATERIAL_BAKED_HAIR_1ST_PASS,
                                MaterialQuality.Baked, useDigitalHuman));

                    secondPass = CreateBakedMaterial(mat, bakedBaseMap, bakedMaskMap, bakedMetallicGlossMap, bakedAOMap, bakedNormalMap,
                        null, null, null, null, emissionMap, null,
                        normalStrength, 1f, 1f, 0f, 0.5f, emissiveColor,
                        sourceName + "_2nd_Pass",
                        Pipeline.GetUpgradedTemplateMaterial(sourceName, Pipeline.MATERIAL_BAKED_HAIR_2ND_PASS,
                                MaterialQuality.Baked, useDigitalHuman));

                    SetBasic(firstPass);
                    alphaClip = 0.01f;
                    depthPostpass = 0f;
                    depthPrepass = 1f;
                    SetBasic(secondPass);
                    return null;
                }
                else
                {
                    Material result = CreateBakedMaterial(mat, bakedBaseMap, bakedMaskMap, bakedMetallicGlossMap, bakedAOMap, bakedNormalMap,
                        null, null, null, null, emissionMap, null,
                        normalStrength, 1f, 1f, 0f, 0.5f, emissiveColor,
                        sourceName,
                        Pipeline.GetTemplateMaterial(sourceName, MaterialType.Hair,
                                    MaterialQuality.Baked, characterInfo,
                                    useDigitalHuman));

                    SetBasic(result);
                    return result;
                }
            }
        }

        private void BakeHairDiffuseOnly(Material mat, string sourceName, out Texture2D bakedBaseMap)
        {
            Texture2D diffuse = GetMaterialTexture(mat, "_DiffuseMap");
            Texture2D id = GetMaterialTexture(mat, "_IDMap");
            Texture2D root = GetMaterialTexture(mat, "_RootMap");
            float diffuseStrength = mat.GetFloatIf("_DiffuseStrength");
            float baseColorStrength = mat.GetFloatIf("_BaseColorStrength");
            float globalStrength = mat.GetFloatIf("_GlobalStrength");
            float rootColorStrength = mat.GetFloatIf("_RootColorStrength");
            float endColorStrength = mat.GetFloatIf("_EndColorStrength");
            float invertRootMap = mat.GetFloatIf("_InvertRootMap");
            float highlightBlend = mat.GetFloatIf("_HighlightBlend", 1.0f);
            float highlightAStrength = mat.GetFloatIf("_HighlightAStrength");
            float highlightAOverlapEnd = mat.GetFloatIf("_HighlightAOverlapEnd");
            float highlightAOverlapInvert = mat.GetFloatIf("_HighlightAOverlapInvert");
            float highlightBStrength = mat.GetFloatIf("_HighlightBStrength");
            float highlightBOverlapEnd = mat.GetFloatIf("_HighlightBOverlapEnd");
            float highlightBOverlapInvert = mat.GetFloatIf("_HighlightBOverlapInvert");
            Vector4 highlightADistribution = mat.GetVectorIf("_HighlightADistribution");
            Vector4 highlightBDistribution = mat.GetVectorIf("_HighlightBDistribution");
            Color rootColor = mat.GetColorIf("_RootColor", Color.black);
            Color endColor = mat.GetColorIf("_EndColor", Color.white);
            Color highlightAColor = mat.GetColorIf("_HighlightAColor", Color.white);
            Color highlightBColor = mat.GetColorIf("_HighlightBColor", Color.white);
            bool enableColor = mat.GetFloatIf("BOOLEAN_ENABLECOLOR") > 0f;

            if (enableColor)
            {
                bakedBaseMap = BakeHairDiffuseMap(diffuse, null, id, root, null,
                    1f, 1f, 1f, 1f, 0f,
                    rootColor, rootColorStrength, endColor, endColorStrength, globalStrength,
                    invertRootMap, baseColorStrength, highlightBlend,
                    highlightAColor, highlightADistribution, highlightAOverlapEnd,
                    highlightAOverlapInvert, highlightAStrength,
                    highlightBColor, highlightBDistribution, highlightBOverlapEnd,
                    highlightBOverlapInvert, highlightBStrength,
                    0f, Color.white, 0f,
                    sourceName + "_BaseMap", "RLHairColoredDiffuseOnly");
            }
            else
            {
                bakedBaseMap = diffuse;
            }
        }

        private Material BakeEyeOcclusionMaterial(Material mat, string sourceName)
        {
            float occlusionStrength = mat.GetFloatIf("_OcclusionStrength");
            float occlusionPower = mat.GetFloatIf("_OcclusionPower");
            float topMin = mat.GetFloatIf("_TopMin");
            float topMax = mat.GetFloatIf("_TopMax");
            float topCurve = mat.GetFloatIf("_TopCurve");
            float bottomMin = mat.GetFloatIf("_BottomMin");
            float bottomMax = mat.GetFloatIf("_BottomMax");
            float bottomCurve = mat.GetFloatIf("_BottomCurve");
            float innerMin = mat.GetFloatIf("_InnerMin");
            float innerMax = mat.GetFloatIf("_InnerMax");
            float outerMin = mat.GetFloatIf("_OuterMin");
            float outerMax = mat.GetFloatIf("_OuterMax");
            float occlusionStrength2 = mat.GetFloatIf("_OcclusionStrength2");
            float top2Min = mat.GetFloatIf("_Top2Min");
            float top2Max = mat.GetFloatIf("_Top2Max");
            float tearDuctPosition = mat.GetFloatIf("_TearDuctPosition");
            float tearDuctWidth = mat.GetFloatIf("_TearDuctWidth");
            Color occlusionColor = mat.GetColorIf("_OcclusionColor", Color.black);

            float expandOut = mat.GetFloatIf("_ExpandOut");
            float expandUpper = mat.GetFloatIf("_ExpandUpper");
            float expandLower = mat.GetFloatIf("_ExpandLower");
            float expandInner = mat.GetFloatIf("_ExpandInner");
            float expandOuter = mat.GetFloatIf("_ExpandOuter");
            float expandScale = mat.GetFloatIf("_ExpandScale");

            bool useTessellation = characterInfo.BuiltFeatureTessellation;

            Texture2D bakedBaseMap = null;
            Texture2D bakedMaskMap = null;
            Texture2D bakedMetallicGlossMap = null;
            Texture2D bakedAOMap = null;
            Texture2D bakedNormalMap = null;
            Texture2D bakedDetailMap = null;
            Texture2D bakedDetailMask = null;
            Texture2D bakedSubsurfaceMap = null;
            Texture2D bakedThicknessMap = null;
            Texture2D emissionMap = null;

            bakedBaseMap = BakeEyeOcclusionDiffuseMap(
                occlusionStrength, occlusionPower, topMin, topMax, topCurve, bottomMin, bottomMax, bottomCurve,
                innerMin, innerMax, outerMin, outerMax, occlusionStrength2, top2Min, top2Max,
                tearDuctPosition, tearDuctWidth, occlusionColor,
                sourceName + "_BaseMap");

            Material result = CreateBakedMaterial(mat, bakedBaseMap, bakedMaskMap, bakedMetallicGlossMap, bakedAOMap, bakedNormalMap,
                bakedDetailMask, bakedDetailMap, bakedSubsurfaceMap, bakedThicknessMap, emissionMap, null,
                1f, 1f, 1f, 0f, 0.5f, Color.black,
                sourceName, Pipeline.GetTemplateMaterial(sourceName, MaterialType.EyeOcclusion,
                                                         MaterialQuality.Baked, characterInfo));

            result.SetFloatIf("_ExpandOut", expandOut);
            result.SetFloatIf("_ExpandUpper", expandUpper);
            result.SetFloatIf("_ExpandLower", expandLower);
            result.SetFloatIf("_ExpandInner", expandInner);
            result.SetFloatIf("_ExpandOuter", expandOuter);
            result.SetFloatIf("_ExpandScale", expandScale);

            return result;
        }

        //

        public Texture2D BakeChannelPackLinear(string folder,
            Texture2D redChannel, Texture2D greenChannel, Texture2D blueChannel, Texture2D alphaChannel,
            string name, int flags = 0,
            float defaultRed = 0f, float defaultGreen = 0f, float defaultBlue = 0f, float defaultAlpha = 0f)
        {
            if (alphaChannel) flags |= Importer.FLAG_ALPHA_DATA;

            Vector2Int maxSize = GetMaxSize(redChannel, greenChannel, blueChannel, alphaChannel);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, folder, name, flags);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                if (defaultRed == 0f)
                    redChannel = CheckBlank(redChannel);
                else if (defaultRed == 1f)
                    redChannel = CheckMask(redChannel);
                else
                    redChannel = CheckGray(redChannel);

                if (defaultGreen == 0f)
                    greenChannel = CheckBlank(greenChannel);
                else if (defaultGreen == 1f)
                    greenChannel = CheckMask(greenChannel);
                else
                    greenChannel = CheckGray(greenChannel);

                if (defaultBlue == 0f)
                    blueChannel = CheckBlank(blueChannel);
                else if (defaultBlue == 1f)
                    blueChannel = CheckMask(blueChannel);
                else
                    blueChannel = CheckGray(blueChannel);

                if (defaultAlpha == 0f)
                    alphaChannel = CheckBlank(alphaChannel);
                else if (defaultAlpha == 1f)
                    alphaChannel = CheckMask(alphaChannel);
                else
                    alphaChannel = CheckGray(alphaChannel);

                int kernel = bakeShader.FindKernel("RLChannelPackLinear");
                bakeTarget.Create(bakeShader, kernel);

                bakeShader.SetTexture(kernel, "RedChannel", redChannel);
                bakeShader.SetTexture(kernel, "GreenChannel", greenChannel);
                bakeShader.SetTexture(kernel, "BlueChannel", blueChannel);
                bakeShader.SetTexture(kernel, "AlphaChannel", alphaChannel);
                Dispatch(bakeShader, kernel, bakeTarget);                
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        public Texture2D BakeChannelPackSymmetryLinear(string folder,
            Texture2D redChannelL, Texture2D greenChannelL, Texture2D blueChannelL, Texture2D alphaChannelL,
            Texture2D redChannelR, Texture2D greenChannelR, Texture2D blueChannelR, Texture2D alphaChannelR,
            Vector4 redMaskL, Vector4 greenMaskL, Vector4 blueMaskL, Vector4 alphaMaskL,
            Vector4 redMaskR, Vector4 greenMaskR, Vector4 blueMaskR, Vector4 alphaMaskR,
            int size, string name)
        {
            Vector2Int maxSize = new Vector2Int(size, size);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, folder, name, Importer.FLAG_ALPHA_DATA);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                redChannelL = CheckBlank(redChannelL);
                greenChannelL = CheckBlank(greenChannelL);
                blueChannelL = CheckBlank(blueChannelL);
                alphaChannelL = CheckBlank(alphaChannelL);
                redChannelR = CheckBlank(redChannelR);
                greenChannelR = CheckBlank(greenChannelR);
                blueChannelR = CheckBlank(blueChannelR);
                alphaChannelR = CheckBlank(alphaChannelR);

                int kernel = bakeShader.FindKernel("RLChannelPackSymmetryLinear");
                bakeTarget.Create(bakeShader, kernel);

                bakeShader.SetTexture(kernel, "RedChannelL", redChannelL);
                bakeShader.SetTexture(kernel, "GreenChannelL", greenChannelL);
                bakeShader.SetTexture(kernel, "BlueChannelL", blueChannelL);
                bakeShader.SetTexture(kernel, "AlphaChannelL", alphaChannelL);
                bakeShader.SetTexture(kernel, "RedChannelR", redChannelR);
                bakeShader.SetTexture(kernel, "GreenChannelR", greenChannelR);
                bakeShader.SetTexture(kernel, "BlueChannelR", blueChannelR);
                bakeShader.SetTexture(kernel, "AlphaChannelR", alphaChannelR);
                bakeShader.SetVector("redMaskL", redMaskL);
                bakeShader.SetVector("greenMaskL", greenMaskL);
                bakeShader.SetVector("blueMaskL", blueMaskL);
                bakeShader.SetVector("alphaMaskL", alphaMaskL);
                bakeShader.SetVector("redMaskR", redMaskR);
                bakeShader.SetVector("greenMaskR", greenMaskR);
                bakeShader.SetVector("blueMaskR", blueMaskR);
                bakeShader.SetVector("alphaMaskR", alphaMaskR);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        public Texture2D BakeGradientMap(string folder, string name)
        {
            Vector2Int maxSize = new Vector2Int(256, 256);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, folder, name, Importer.FLAG_ALPHA_DATA);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                int kernel = bakeShader.FindKernel("RLGradient");
                bakeTarget.Create(bakeShader, kernel);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        public static Texture2D BakeMagicaWeightMap(Texture2D physXWeightMap, float threshold, Vector2Int size, string folder, string name)
        {
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(size, folder, name, Importer.FLAG_ALPHA_DATA |
                                                           Importer.FLAG_READ_WRITE |
                                                           Importer.FLAG_UNCOMPRESSED);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                int kernel = bakeShader.FindKernel("RLMagicaWeightMap");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "WeightMap", physXWeightMap);
                bakeShader.SetFloat("threshold", threshold);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        public Texture2D BakeBlenderDiffuseAlphaMap(Texture2D diffuse, Texture2D alpha, string folder, string name)
        {
            Vector2Int maxSize = GetMaxSize(diffuse, alpha);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, folder, name, Importer.FLAG_SRGB);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                diffuse = CheckDiffuse(diffuse);
                alpha = CheckMask(alpha);

                int kernel = bakeShader.FindKernel("RLDiffuseAlpha");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Diffuse", diffuse);
                bakeShader.SetTexture(kernel, "Alpha", alpha);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        public Texture2D BakeBlenderHDRPMaskMap(Texture2D metallic, Texture2D ao,
            Texture2D microNormalMask, Texture2D roughness,
            Texture2D smoothnessLUT,
            string folder, string name)
        {
            Vector2Int maxSize = GetMaxSize(metallic, ao, roughness, microNormalMask);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, folder, name, Importer.FLAG_ALPHA_DATA);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                metallic = CheckBlank(metallic);
                ao = CheckMask(ao);
                roughness = CheckGray(roughness);
                microNormalMask = CheckMask(microNormalMask);
                smoothnessLUT = CheckGray(smoothnessLUT);

                int kernel = bakeShader.FindKernel("RLHDRPMask");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Metallic", metallic);
                bakeShader.SetTexture(kernel, "AO", ao);
                bakeShader.SetTexture(kernel, "Roughness", roughness);
                bakeShader.SetTexture(kernel, "MicroNormalMask", microNormalMask);
                bakeShader.SetTexture(kernel, "SmoothnessLUT", smoothnessLUT);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        public Texture2D BakeBlenderMetallicGlossMap(Texture2D metallic, Texture2D roughness,
            Texture2D smoothnessLUT,
            string folder, string name)
        {
            Vector2Int maxSize = GetMaxSize(metallic, roughness);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, folder, name, Importer.FLAG_ALPHA_DATA);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                metallic = CheckBlank(metallic);
                roughness = CheckGray(roughness);
                smoothnessLUT = CheckGray(smoothnessLUT);

                int kernel = bakeShader.FindKernel("RLURPMetallicGloss");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Metallic", metallic);
                bakeShader.SetTexture(kernel, "Roughness", roughness);
                bakeShader.SetTexture(kernel, "SmoothnessLUT", smoothnessLUT);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        // Bake Maps

        private Texture2D BakeDiffuseBlendMap(Texture2D diffuse, Texture2D blend,
            float blendStrength,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(diffuse);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name, Importer.FLAG_SRGB);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                diffuse = CheckDiffuse(diffuse);
                blend = CheckOverlay(blend);

                int kernel = bakeShader.FindKernel("RLDiffuseBlend");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Diffuse", diffuse);
                bakeShader.SetTexture(kernel, "ColorBlend", blend);
                bakeShader.SetFloat("colorBlendStrength", blendStrength);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeMaskMap(Texture2D mask,
            float aoStrength, float smoothnessMin, float smoothnessMax, float smoothnessContrast,
            float microNormalStrength,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(mask);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                mask = CheckHDRP(mask);

                int kernel = bakeShader.FindKernel("RLMask");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetFloat("aoStrength", aoStrength);
                bakeShader.SetFloat("smoothnessMin", smoothnessMin);
                bakeShader.SetFloat("smoothnessMax", smoothnessMax);
                bakeShader.SetFloat("smoothnessContrast", smoothnessContrast);
                bakeShader.SetFloat("microNormalStrength", microNormalStrength);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeDetailMaskMap(Texture2D mask,
            float microNormalStrength,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(mask);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                mask = CheckHDRP(mask);

                int kernel = bakeShader.FindKernel("RLDetailMask");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetFloat("microNormalStrength", 1f);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeHeadDiffuseMap(Texture2D diffuse, Texture2D blend, Texture2D cavityAO,
            float blendStrength,
            float mouthAOPower, float nostrilAOPower, float lipsAOPower,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(diffuse);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name, Importer.FLAG_SRGB);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                diffuse = CheckDiffuse(diffuse);
                blend = CheckOverlay(blend);
                cavityAO = CheckMask(cavityAO);

                int kernel = bakeShader.FindKernel("RLHeadDiffuse");
                bakeTarget.Create(bakeShader, kernel);

                bakeShader.SetTexture(kernel, "Diffuse", diffuse);
                bakeShader.SetTexture(kernel, "ColorBlend", blend);
                bakeShader.SetTexture(kernel, "CavityAO", cavityAO);
                bakeShader.SetFloat("colorBlendStrength", blendStrength);
                bakeShader.SetFloat("mouthAOPower", mouthAOPower);
                bakeShader.SetFloat("nostrilAOPower", nostrilAOPower);
                bakeShader.SetFloat("lipsAOPower", lipsAOPower);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeHeadMaskMap(Texture2D mask, Texture2D cavityAO, Texture2D subsurface,
            Texture2D NMUIL, Texture2D CFULC, Texture2D earNeck,
            float aoStrength, float smoothnessMin, float smoothnessMax, float smoothnessContrast, float microNormalStrength,
            float mouthAOPower, float nostrilAOPower, float lipsAOPower, float microSmoothnessMod,
            float noseMSM, float mouthMSM, float upperLidMSM, float innerLidMSM, float earMSM,
            float neckMSM, float cheekMSM, float foreheadMSM, float upperLipMSM, float chinMSM, float unmaskedMSM,
            float subsurfaceScale, float sssNormalSoften,
            float noseSS, float mouthSS, float upperLidSS, float innerLidSS, float earSS,
            float neckSS, float cheekSS, float foreheadSS, float upperLipSS, float chinSS, float unmaskedSS,
            string name, string kernelName)
        {
            Vector2Int maxSize = GetMaxSize(mask);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                subsurface = CheckMask(subsurface);
                mask = CheckHDRP(mask);
                cavityAO = CheckMask(cavityAO);
                NMUIL = CheckBlank(NMUIL);
                CFULC = CheckBlank(CFULC);
                earNeck = CheckBlank(earNeck);

                int kernel = bakeShader.FindKernel(kernelName);
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetTexture(kernel, "CavityAO", cavityAO);
                bakeShader.SetTexture(kernel, "NMUILMask", NMUIL);
                bakeShader.SetTexture(kernel, "CFULCMask", CFULC);
                bakeShader.SetTexture(kernel, "EarNeckMask", earNeck);
                bakeShader.SetTexture(kernel, "Subsurface", subsurface);
                bakeShader.SetFloat("aoStrength", aoStrength);
                bakeShader.SetFloat("smoothnessMin", smoothnessMin);
                bakeShader.SetFloat("smoothnessMax", smoothnessMax);
                bakeShader.SetFloat("smoothnessContrast", smoothnessContrast);
                bakeShader.SetFloat("microNormalStrength", microNormalStrength);
                bakeShader.SetFloat("mouthAOPower", mouthAOPower);
                bakeShader.SetFloat("nostrilAOPower", nostrilAOPower);
                bakeShader.SetFloat("lipsAOPower", lipsAOPower);
                bakeShader.SetFloat("microSmoothnessMod", microSmoothnessMod);
                bakeShader.SetFloat("noseMSM", noseMSM);
                bakeShader.SetFloat("mouthMSM", mouthMSM);
                bakeShader.SetFloat("upperLidMSM", upperLidMSM);
                bakeShader.SetFloat("innerLidMSM", innerLidMSM);
                bakeShader.SetFloat("earMSM", earMSM);
                bakeShader.SetFloat("neckMSM", neckMSM);
                bakeShader.SetFloat("cheekMSM", cheekMSM);
                bakeShader.SetFloat("foreheadMSM", foreheadMSM);
                bakeShader.SetFloat("upperLipMSM", upperLipMSM);
                bakeShader.SetFloat("chinMSM", chinMSM);
                bakeShader.SetFloat("unmaskedMSM", unmaskedMSM);
                bakeShader.SetFloat("subsurfaceScale", subsurfaceScale);
                bakeShader.SetFloat("noseSS", noseSS);
                bakeShader.SetFloat("mouthSS", mouthSS);
                bakeShader.SetFloat("upperLidSS", upperLidSS);
                bakeShader.SetFloat("innerLidSS", innerLidSS);
                bakeShader.SetFloat("earSS", earSS);
                bakeShader.SetFloat("neckSS", neckSS);
                bakeShader.SetFloat("cheekSS", cheekSS);
                bakeShader.SetFloat("foreheadSS", foreheadSS);
                bakeShader.SetFloat("upperLipSS", upperLipSS);
                bakeShader.SetFloat("chinSS", chinSS);
                bakeShader.SetFloat("unmaskedSS", unmaskedSS);
                bakeShader.SetFloat("sssNormalSoften", sssNormalSoften);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeSkinMaskMap(Texture2D mask, Texture2D subsurface, Texture2D RGBA,
            float aoStrength, float smoothnessMin, float smoothnessMax, float smoothnessContrast,
            float microNormalStrength, float microSmoothnessMod,
            float rMSM, float gMSM, float bMSM, float aMSM, float unmaskedMSM,
            float subsurfaceScale, float sssNormalSoften,
            float rSS, float gSS, float bSS, float aSS, float unmaskedSS,
            string name, string kernelName)
        {
            Vector2Int maxSize = GetMaxSize(mask);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                subsurface = CheckMask(subsurface);
                mask = CheckHDRP(mask);
                RGBA = CheckBlank(RGBA);

                int kernel = bakeShader.FindKernel(kernelName);
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetTexture(kernel, "Subsurface", subsurface);
                bakeShader.SetTexture(kernel, "RGBAMask", RGBA);
                bakeShader.SetFloat("aoStrength", aoStrength);
                bakeShader.SetFloat("smoothnessMin", smoothnessMin);
                bakeShader.SetFloat("smoothnessMax", smoothnessMax);
                bakeShader.SetFloat("smoothnessContrast", smoothnessContrast);
                bakeShader.SetFloat("microNormalStrength", microNormalStrength);
                bakeShader.SetFloat("microSmoothnessMod", microSmoothnessMod);
                bakeShader.SetFloat("rMSM", rMSM);
                bakeShader.SetFloat("gMSM", gMSM);
                bakeShader.SetFloat("bMSM", bMSM);
                bakeShader.SetFloat("aMSM", aMSM);
                bakeShader.SetFloat("unmaskedMSM", unmaskedMSM);
                bakeShader.SetFloat("subsurfaceScale", subsurfaceScale);
                bakeShader.SetFloat("rSS", rSS);
                bakeShader.SetFloat("gSS", gSS);
                bakeShader.SetFloat("bSS", bSS);
                bakeShader.SetFloat("aSS", aSS);
                bakeShader.SetFloat("unmaskedSS", unmaskedSS);
                bakeShader.SetFloat("sssNormalSoften", sssNormalSoften);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeHeadWrinkleSmoothnessPack(Texture2D roughness1, Texture2D roughness2, Texture2D roughness3,
            Texture2D cavityAO, Texture2D NMUIL, Texture2D CFULC, Texture2D earNeck,
            float smoothnessMin, float smoothnessMax, float smoothnessContrast,
            float mouthAOPower, float nostrilAOPower, float lipsAOPower, float microSmoothnessMod,
            float noseMSM, float mouthMSM, float upperLidMSM, float innerLidMSM, float earMSM,
            float neckMSM, float cheekMSM, float foreheadMSM, float upperLipMSM, float chinMSM, float unmaskedMSM,
            string name, string kernelName = "RLHeadWrinkleSmoothnessPack")
        {
            Vector2Int maxSize = GetMaxSize(roughness1, roughness2, roughness3);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                roughness1 = CheckGray(roughness1);
                roughness2 = CheckGray(roughness2);
                roughness3 = CheckGray(roughness3);
                cavityAO = CheckMask(cavityAO);
                NMUIL = CheckBlank(NMUIL);
                CFULC = CheckBlank(CFULC);
                earNeck = CheckBlank(earNeck);

                int kernel = bakeShader.FindKernel(kernelName);
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Roughness1", roughness1);
                bakeShader.SetTexture(kernel, "Roughness2", roughness2);
                bakeShader.SetTexture(kernel, "Roughness3", roughness3);
                bakeShader.SetTexture(kernel, "CavityAO", cavityAO);
                bakeShader.SetTexture(kernel, "NMUILMask", NMUIL);
                bakeShader.SetTexture(kernel, "CFULCMask", CFULC);
                bakeShader.SetTexture(kernel, "EarNeckMask", earNeck);
                bakeShader.SetFloat("smoothnessMin", smoothnessMin);
                bakeShader.SetFloat("smoothnessMax", smoothnessMax);
                bakeShader.SetFloat("smoothnessContrast", smoothnessContrast);
                bakeShader.SetFloat("mouthAOPower", mouthAOPower);
                bakeShader.SetFloat("nostrilAOPower", nostrilAOPower);
                bakeShader.SetFloat("lipsAOPower", lipsAOPower);
                bakeShader.SetFloat("microSmoothnessMod", microSmoothnessMod);
                bakeShader.SetFloat("noseMSM", noseMSM);
                bakeShader.SetFloat("mouthMSM", mouthMSM);
                bakeShader.SetFloat("upperLidMSM", upperLidMSM);
                bakeShader.SetFloat("innerLidMSM", innerLidMSM);
                bakeShader.SetFloat("earMSM", earMSM);
                bakeShader.SetFloat("neckMSM", neckMSM);
                bakeShader.SetFloat("cheekMSM", cheekMSM);
                bakeShader.SetFloat("foreheadMSM", foreheadMSM);
                bakeShader.SetFloat("upperLipMSM", upperLipMSM);
                bakeShader.SetFloat("chinMSM", chinMSM);
                bakeShader.SetFloat("unmaskedMSM", unmaskedMSM);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeHeadWrinkleFlowPack(Texture2D flow1, Texture2D flow2, Texture2D flow3,
            string name, string kernelName = "RLHeadWrinkleFlowPack")
        {
            Vector2Int maxSize = GetMaxSize(flow1, flow2, flow3);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                flow1 = CheckGray(flow1);
                flow2 = CheckGray(flow2);
                flow3 = CheckGray(flow3);

                int kernel = bakeShader.FindKernel(kernelName);
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Flow1", flow1);
                bakeShader.SetTexture(kernel, "Flow2", flow2);
                bakeShader.SetTexture(kernel, "Flow3", flow3);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeHeadSubsurfaceMap(Texture2D subsurface, Texture2D thickness,
            Texture2D NMUIL, Texture2D CFULC, Texture2D earNeck,
            float subsurfaceScale,
            float noseSS, float mouthSS, float upperLidSS, float innerLidSS, float earSS,
            float neckSS, float cheekSS, float foreheadSS, float upperLipSS, float chinSS, float unmaskedSS,
            float thicknessScaleMin, float thicknessScale, Color subsurfaceFalloff, Texture2D baseMap, bool invertMap,
            string name, string kernelName = "RLHeadSubsurface")
        {
            Vector2Int maxSize = GetMaxSize(subsurface, thickness);
            if (maxSize.x > 1024) maxSize.x = 1024;
            if (maxSize.y > 1024) maxSize.y = 1024;
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                subsurface = CheckMask(subsurface);
                thickness = CheckMask(thickness);
                NMUIL = CheckBlank(NMUIL);
                CFULC = CheckBlank(CFULC);
                earNeck = CheckBlank(earNeck);
                baseMap = CheckDiffuse(baseMap);

                int kernel = bakeShader.FindKernel(kernelName);
                bakeTarget.Create(bakeShader, kernel);

                bakeShader.SetTexture(kernel, "Subsurface", subsurface);
                bakeShader.SetTexture(kernel, "Thickness", thickness);
                bakeShader.SetTexture(kernel, "NMUILMask", NMUIL);
                bakeShader.SetTexture(kernel, "CFULCMask", CFULC);
                bakeShader.SetTexture(kernel, "EarNeckMask", earNeck);
                bakeShader.SetTexture(kernel, "BaseMap", baseMap);
                bakeShader.SetFloat("subsurfaceScale", subsurfaceScale);
                bakeShader.SetFloat("noseSS", noseSS);
                bakeShader.SetFloat("mouthSS", mouthSS);
                bakeShader.SetFloat("upperLidSS", upperLidSS);
                bakeShader.SetFloat("innerLidSS", innerLidSS);
                bakeShader.SetFloat("earSS", earSS);
                bakeShader.SetFloat("neckSS", neckSS);
                bakeShader.SetFloat("cheekSS", cheekSS);
                bakeShader.SetFloat("foreheadSS", foreheadSS);
                bakeShader.SetFloat("upperLipSS", upperLipSS);
                bakeShader.SetFloat("chinSS", chinSS);
                bakeShader.SetFloat("unmaskedSS", unmaskedSS);
                bakeShader.SetFloat("thicknessScale", thicknessScale);
                bakeShader.SetFloat("thicknessScaleMin", thicknessScaleMin);
                bakeShader.SetFloat("invertMap", invertMap ? 1.0f : 0.0f);
                bakeShader.SetVector("subsurfaceFalloff", subsurfaceFalloff);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeHeadNormalMap(Texture2D normal, Texture2D normalBlend, Texture2D subsurface,
            Texture2D NMUIL, Texture2D CFULC, Texture2D earNeck,
            float normalStrength, float normalBlendStrength, float sssNormalSoften, float subsurfaceScale,
            float noseSS, float mouthSS, float upperLidSS, float innerLidSS, float earSS,
            float neckSS, float cheekSS, float foreheadSS, float upperLipSS, float chinSS, float unmaskedSS,
            string name, string kernelName = "RLHeadNormal")
        {
            Vector2Int maxSize = GetMaxSize(normal, normalBlend);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name, Importer.FLAG_NORMAL);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                normal = CheckNormal(normal);
                normalBlend = CheckNormal(normalBlend);
                subsurface = CheckMask(subsurface);
                NMUIL = CheckBlank(NMUIL);
                CFULC = CheckBlank(CFULC);
                earNeck = CheckBlank(earNeck);

                int kernel = bakeShader.FindKernel(kernelName);
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Normal", normal);
                bakeShader.SetTexture(kernel, "NormalBlend", normalBlend);
                bakeShader.SetTexture(kernel, "Subsurface", subsurface);
                bakeShader.SetTexture(kernel, "NMUILMask", NMUIL);
                bakeShader.SetTexture(kernel, "CFULCMask", CFULC);
                bakeShader.SetTexture(kernel, "EarNeckMask", earNeck);
                bakeShader.SetFloat("normalStrength", normalStrength);
                bakeShader.SetFloat("normalBlendStrength", normalBlendStrength);
                bakeShader.SetFloat("sssNormalSoften", sssNormalSoften);
                bakeShader.SetFloat("subsurfaceScale", subsurfaceScale);
                bakeShader.SetFloat("noseSS", noseSS);
                bakeShader.SetFloat("mouthSS", mouthSS);
                bakeShader.SetFloat("upperLidSS", upperLidSS);
                bakeShader.SetFloat("innerLidSS", innerLidSS);
                bakeShader.SetFloat("earSS", earSS);
                bakeShader.SetFloat("neckSS", neckSS);
                bakeShader.SetFloat("cheekSS", cheekSS);
                bakeShader.SetFloat("foreheadSS", foreheadSS);
                bakeShader.SetFloat("upperLipSS", upperLipSS);
                bakeShader.SetFloat("chinSS", chinSS);
                bakeShader.SetFloat("unmaskedSS", unmaskedSS);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeSkinSubsurfaceMap(Texture2D subsurface, Texture2D thickness, Texture2D RGBA,
            float subsurfaceScale,
            float rSS, float gSS, float bSS, float aSS, float unmaskedSS,
            float thicknessScaleMin, float thicknessScale, Color subsurfaceFalloff, Texture2D baseMap, bool invertMap,
            string name, string kernelName = "RLSkinSubsurface")
        {
            Vector2Int maxSize = GetMaxSize(subsurface);
            if (maxSize.x > 1024) maxSize.x = 1024;
            if (maxSize.y > 1024) maxSize.y = 1024;
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                baseMap = CheckDiffuse(baseMap);
                subsurface = CheckMask(subsurface);
                thickness = CheckMask(thickness);
                RGBA = CheckBlank(RGBA);

                int kernel = bakeShader.FindKernel(kernelName);
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Subsurface", subsurface);
                bakeShader.SetTexture(kernel, "Thickness", thickness);
                bakeShader.SetTexture(kernel, "RGBAMask", RGBA);
                bakeShader.SetTexture(kernel, "BaseMap", baseMap);
                bakeShader.SetFloat("subsurfaceScale", subsurfaceScale);
                bakeShader.SetFloat("rSS", rSS);
                bakeShader.SetFloat("gSS", gSS);
                bakeShader.SetFloat("bSS", bSS);
                bakeShader.SetFloat("aSS", aSS);
                bakeShader.SetFloat("unmaskedSS", unmaskedSS);
                bakeShader.SetFloat("thicknessScale", thicknessScale);
                bakeShader.SetFloat("thicknessScaleMin", thicknessScaleMin);
                bakeShader.SetFloat("invertMap", invertMap ? 1.0f : 0.0f);
                bakeShader.SetVector("subsurfaceFalloff", subsurfaceFalloff);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeSkinNormalMap(Texture2D normal, Texture2D normalBlend,
            Texture2D subsurface, Texture2D RGBA,
            float normalStrength, float normalBlendStrength, float sssNormalSoften, float subsurfaceScale,
            float rSS, float gSS, float bSS, float aSS, float unmaskedSS,
            string name, string kernelName = "RLSkinNormal")
        {
            Vector2Int maxSize = GetMaxSize(normal, normalBlend);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name, Importer.FLAG_NORMAL);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                normal = CheckNormal(normal);
                normalBlend = CheckNormal(normalBlend);
                subsurface = CheckMask(subsurface);
                RGBA = CheckBlank(RGBA);

                int kernel = bakeShader.FindKernel(kernelName);
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Normal", normal);
                bakeShader.SetTexture(kernel, "NormalBlend", normalBlend);
                bakeShader.SetTexture(kernel, "Subsurface", subsurface);
                bakeShader.SetTexture(kernel, "RGBAMask", RGBA);
                bakeShader.SetFloat("normalStrength", normalStrength);
                bakeShader.SetFloat("normalBlendStrength", normalBlendStrength);
                bakeShader.SetFloat("sssNormalSoften", sssNormalSoften);
                bakeShader.SetFloat("subsurfaceScale", subsurfaceScale);
                bakeShader.SetFloat("rSS", rSS);
                bakeShader.SetFloat("gSS", gSS);
                bakeShader.SetFloat("bSS", bSS);
                bakeShader.SetFloat("aSS", aSS);
                bakeShader.SetFloat("unmaskedSS", unmaskedSS);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeBlendNormalMap(Texture2D normal, Texture2D normalBlend,
            float normalBlendStrength,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(normal, normalBlend);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name, Importer.FLAG_NORMAL);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                normal = CheckNormal(normal);
                normalBlend = CheckNormal(normalBlend);

                int kernel = bakeShader.FindKernel("RLNormalBlend");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Normal", normal);
                bakeShader.SetTexture(kernel, "NormalBlend", normalBlend);
                bakeShader.SetFloat("normalBlendStrength", normalBlendStrength);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeDetailMap(Texture2D microNormal,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(microNormal);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                microNormal = CheckNormal(microNormal);

                int kernel = bakeShader.FindKernel("RLDetail");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "MicroNormal", microNormal);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }


        private Texture2D BakeSubsurfaceMap(Texture2D subsurface,
            float subsurfaceScale, Color subsurfaceFalloff, Texture2D baseMap,
            string name, string kernelName = "RLSubsurface")
        {
            Vector2Int maxSize = GetMaxSize(subsurface);
            if (maxSize.x > 1024) maxSize.x = 1024;
            if (maxSize.y > 1024) maxSize.y = 1024;
            if (maxSize.x < 128) maxSize.x = 128;
            if (maxSize.y < 128) maxSize.y = 128;
            maxSize = new Vector2Int(256, 256);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                baseMap = CheckDiffuse(baseMap);
                subsurface = CheckMask(subsurface);

                int kernel = bakeShader.FindKernel(kernelName);
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Subsurface", subsurface);
                bakeShader.SetTexture(kernel, "BaseMap", baseMap);
                bakeShader.SetFloat("subsurfaceScale", subsurfaceScale);
                bakeShader.SetVector("subsurfaceFalloff", subsurfaceFalloff);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeThicknessMap(Texture2D thickness,
            float thicknessScaleMin, float thicknessScale, Color subsurfaceFalloff, Texture2D baseMap, bool invertMap,
            string name, string kernelName = "RLThickness")
        {
            Vector2Int maxSize = GetMaxSize(thickness);
            if (maxSize.x > 1024) maxSize.x = 1024;
            if (maxSize.y > 1024) maxSize.y = 1024;
            if (maxSize.x < 128) maxSize.x = 128;
            if (maxSize.y < 128) maxSize.y = 128;
            maxSize = new Vector2Int(256, 256);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                baseMap = CheckDiffuse(baseMap);
                thickness = CheckMask(thickness);

                int kernel = bakeShader.FindKernel(kernelName);
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Thickness", thickness);
                bakeShader.SetTexture(kernel, "BaseMap", baseMap);
                bakeShader.SetFloat("thicknessScale", thicknessScale);
                bakeShader.SetFloat("thicknessScaleMin", thicknessScaleMin);
                bakeShader.SetFloat("invertMap", invertMap ? 1.0f : 0.0f);
                bakeShader.SetVector("subsurfaceFalloff", subsurfaceFalloff);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeTeethDiffuseMap(Texture2D diffuse, Texture2D gumsMask, Texture2D gradientAO,
            float isUpperTeeth, float frontAO, float rearAO, float gumsSaturation, float gumsBrightness,
            float teethSaturation, float teethBrightness,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(diffuse);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name, Importer.FLAG_SRGB);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                diffuse = CheckDiffuse(diffuse);
                gumsMask = CheckMask(gumsMask);
                gradientAO = CheckMask(gradientAO);

                int kernel = bakeShader.FindKernel("RLTeethDiffuse");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Diffuse", diffuse);
                bakeShader.SetTexture(kernel, "GumsMask", gumsMask);
                bakeShader.SetTexture(kernel, "GradientAO", gradientAO);
                bakeShader.SetFloat("isUpperTeeth", isUpperTeeth);
                bakeShader.SetFloat("frontAO", frontAO);
                bakeShader.SetFloat("rearAO", rearAO);
                bakeShader.SetFloat("gumsSaturation", gumsSaturation);
                bakeShader.SetFloat("gumsBrightness", gumsBrightness);
                bakeShader.SetFloat("teethSaturation", teethSaturation);
                bakeShader.SetFloat("teethBrightness", teethBrightness);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeTeethMaskMap(Texture2D mask, Texture2D gradientAO,
            float isUpperTeeth, float aoStrength, float smoothnessFront, float smoothnessRear, float smoothnessMax, float smoothnessContrast,
            float microNormalStrength,
            string name, string kernelName)
        {
            Vector2Int maxSize = GetMaxSize(mask);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                mask = CheckHDRP(mask);
                gradientAO = CheckMask(gradientAO);

                int kernel = bakeShader.FindKernel(kernelName);
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetTexture(kernel, "GradientAO", gradientAO);
                bakeShader.SetFloat("aoStrength", aoStrength);
                bakeShader.SetFloat("smoothnessFront", smoothnessFront);
                bakeShader.SetFloat("smoothnessRear", smoothnessRear);
                bakeShader.SetFloat("smoothnessMax", smoothnessMax);
                bakeShader.SetFloat("smoothnessContrast", smoothnessContrast);
                bakeShader.SetFloat("microNormalStrength", microNormalStrength);
                bakeShader.SetFloat("isUpperTeeth", isUpperTeeth);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeTeethSubsurfaceMap(Texture2D gumsMask,
            float gumsSSS, float teethSSS, Color subsurfaceFalloff, Texture2D baseMap,
            string name)
        {
            Vector2Int maxSize = new Vector2Int(256, 256);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                baseMap = CheckDiffuse(baseMap);
                gumsMask = CheckMask(gumsMask);

                int kernel = bakeShader.FindKernel("RLTeethSubsurface");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "GumsMask", gumsMask);
                bakeShader.SetTexture(kernel, "BaseMap", baseMap);
                bakeShader.SetFloat("gumsSSS", gumsSSS);
                bakeShader.SetFloat("teethSSS", teethSSS);
                bakeShader.SetVector("subsurfaceFalloff", subsurfaceFalloff);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }
        private Texture2D BakeTeethThicknessMap(Texture2D gumsMask,
            float gumsThickness, float teethThickness, Color subsurfaceFalloff, Texture2D baseMap,
            string name, string kernelName = "RLTeethThickness")
        {
            Vector2Int maxSize = new Vector2Int(256, 256);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                baseMap = CheckDiffuse(baseMap);
                gumsMask = CheckMask(gumsMask);

                int kernel = bakeShader.FindKernel(kernelName);
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "GumsMask", gumsMask);
                bakeShader.SetTexture(kernel, "BaseMap", baseMap);
                bakeShader.SetFloat("gumsThickness", gumsThickness);
                bakeShader.SetFloat("teethThickness", teethThickness);
                bakeShader.SetVector("subsurfaceFalloff", subsurfaceFalloff);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }


        private Texture2D BakeTongueDiffuseMap(Texture2D diffuse, Texture2D gradientAO,
            float frontAO, float rearAO, float tongueSaturation, float tongueBrightness,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(diffuse);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name, Importer.FLAG_SRGB);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                diffuse = CheckDiffuse(diffuse);
                gradientAO = CheckMask(gradientAO);

                int kernel = bakeShader.FindKernel("RLTongueDiffuse");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Diffuse", diffuse);
                bakeShader.SetTexture(kernel, "GradientAO", gradientAO);
                bakeShader.SetFloat("frontAO", frontAO);
                bakeShader.SetFloat("rearAO", rearAO);
                bakeShader.SetFloat("tongueSaturation", tongueSaturation);
                bakeShader.SetFloat("tongueBrightness", tongueBrightness);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeTongueMaskMap(Texture2D mask, Texture2D gradientAO,
            float aoStrength, float smoothnessFront, float smoothnessRear, float smoothnessMax, float smoothnessContrast,
            float microNormalStrength,
            string name, string kernelName)
        {
            Vector2Int maxSize = GetMaxSize(mask);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                mask = CheckHDRP(mask);
                gradientAO = CheckMask(gradientAO);

                int kernel = bakeShader.FindKernel(kernelName);
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetTexture(kernel, "GradientAO", gradientAO);
                bakeShader.SetFloat("aoStrength", aoStrength);
                bakeShader.SetFloat("smoothnessFront", smoothnessFront);
                bakeShader.SetFloat("smoothnessRear", smoothnessRear);
                bakeShader.SetFloat("smoothnessMax", smoothnessMax);
                bakeShader.SetFloat("smoothnessContrast", smoothnessContrast);
                bakeShader.SetFloat("microNormalStrength", microNormalStrength);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }


        private Texture2D BakeCorneaDiffuseMap(Texture2D cornea, Texture2D sclera, Texture2D colorBlend,
            float scleraScale, float scleraHue, float scleraSaturation, float scleraBrightness,
            float irisHue, float irisSaturation, float irisBrightness, float irisRadius,
            Color irisColor, Color irisCloudyColor,
            float limbusShading, float limbusContrast,
            float limbusWidth, float limbusDarkRadius, float limbusDarkWidth, Color limbusColor,
            float shadowRadius, float shadowHardness,
            Color cornerShadowColor, float colorBlendStrength,
            string name, string kernelName = "RLCorneaDiffuse")
        {
            Vector2Int maxSize = GetMaxSize(sclera);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name, Importer.FLAG_SRGB + Importer.FLAG_ALPHA_DATA);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                cornea = CheckDiffuse(cornea);
                sclera = CheckDiffuse(sclera);
                colorBlend = CheckOverlay(colorBlend);

                int kernel = bakeShader.FindKernel(kernelName);
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "CorneaDiffuse", cornea);
                bakeShader.SetTexture(kernel, "ScleraDiffuse", sclera);
                bakeShader.SetTexture(kernel, "ColorBlend", colorBlend);
                bakeShader.SetFloat("scleraScale", scleraScale);
                bakeShader.SetFloat("scleraHue", scleraHue);
                bakeShader.SetFloat("scleraSaturation", scleraSaturation);
                bakeShader.SetFloat("scleraBrightness", scleraBrightness);
                bakeShader.SetFloat("irisRadius", irisRadius);
                bakeShader.SetFloat("irisHue", irisHue);
                bakeShader.SetFloat("irisSaturation", irisSaturation);
                bakeShader.SetFloat("irisBrightness", irisBrightness);
                bakeShader.SetVector("irisColor", irisColor);
                bakeShader.SetVector("irisCloudyColor", irisCloudyColor);
                bakeShader.SetFloat("limbusDarkRadius", limbusDarkRadius);
                bakeShader.SetFloat("limbusDarkWidth", limbusDarkWidth);
                bakeShader.SetFloat("limbusShading", limbusShading);
                bakeShader.SetFloat("limbusContrast", limbusContrast);
                bakeShader.SetFloat("limbusWidth", limbusWidth);
                bakeShader.SetFloat("shadowRadius", shadowRadius);
                bakeShader.SetFloat("shadowHardness", shadowHardness);
                bakeShader.SetFloat("colorBlendStrength", colorBlendStrength);
                bakeShader.SetVector("limbusColor", limbusColor);
                bakeShader.SetVector("cornerShadowColor", cornerShadowColor);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeEyeDiffuseMap(Texture2D cornea, Texture2D colorBlend,
            float scleraScale, float irisRadius, float irisHue, float irisSaturation, float irisBrightness, Color irisColor, Color irisCloudyColor,
            float limbusWidth, float limbusDarkRadius, float limbusDarkWidth, Color limbusColor,
            float limbusShading, float limbusContrast,
            float colorBlendStrength,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(cornea);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name, Importer.FLAG_SRGB);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                cornea = CheckDiffuse(cornea);
                colorBlend = CheckOverlay(colorBlend);

                int kernel = bakeShader.FindKernel("RLEyeDiffuse");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "CorneaDiffuse", cornea);
                bakeShader.SetTexture(kernel, "ColorBlend", colorBlend);
                bakeShader.SetFloat("scleraScale", scleraScale);
                bakeShader.SetFloat("irisRadius", irisRadius);
                bakeShader.SetFloat("irisHue", irisHue);
                bakeShader.SetFloat("irisSaturation", irisSaturation);
                bakeShader.SetFloat("irisBrightness", irisBrightness);
                bakeShader.SetVector("irisColor", irisColor);
                bakeShader.SetVector("irisCloudyColor", irisCloudyColor);
                bakeShader.SetFloat("limbusWidth", limbusWidth);
                bakeShader.SetFloat("limbusDarkRadius", limbusDarkRadius);
                bakeShader.SetFloat("limbusDarkWidth", limbusDarkWidth);
                bakeShader.SetFloat("limbusShading", limbusShading);
                bakeShader.SetFloat("limbusContrast", limbusContrast);
                bakeShader.SetFloat("colorBlendStrength", colorBlendStrength);
                bakeShader.SetVector("limbusColor", limbusColor);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeCorneaMaskMap(Texture2D mask,
            float aoStrength, float corneaSmoothness, float scleraSmoothness,
            float irisRadius, float limbusWidth, float microNormalStrength,
            string name, string kernelName)
        {
            Vector2Int maxSize = GetMaxSize(mask);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                mask = CheckHDRP(mask);

                int kernel = bakeShader.FindKernel(kernelName);
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetFloat("aoStrength", aoStrength);
                bakeShader.SetFloat("corneaSmoothness", corneaSmoothness);
                bakeShader.SetFloat("scleraSmoothness", scleraSmoothness);
                bakeShader.SetFloat("microNormalStrength", microNormalStrength);
                bakeShader.SetFloat("irisRadius", irisRadius);
                bakeShader.SetFloat("limbusWidth", limbusWidth);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeCorneaDetailMaskMap(Texture2D mask,
            float scleraScale, float irisRadius,
            float limbusWidth, float limbusDarkRadius, float limbusDarkWidth,
            float microNormalStrength,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(mask);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                mask = CheckHDRP(mask);

                int kernel = bakeShader.FindKernel("RLCorneaDetailMask");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetFloat("microNormalStrength", microNormalStrength);
                bakeShader.SetFloat("irisRadius", irisRadius);
                bakeShader.SetFloat("scleraScale", scleraScale);
                bakeShader.SetFloat("limbusWidth", limbusWidth);
                bakeShader.SetFloat("limbusDarkRadius", limbusDarkRadius);
                bakeShader.SetFloat("limbusDarkWidth", limbusDarkWidth);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeEyeMaskMap(Texture2D mask,
            float aoStrength, float irisSmoothness, float scleraSmoothness,
            float limbusDarkRadius, float limbusDarkWidth,
            float irisRadius,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(mask);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                mask = CheckHDRP(mask);

                int kernel = bakeShader.FindKernel("RLEyeMask");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetFloat("aoStrength", aoStrength);
                bakeShader.SetFloat("irisSmoothness", irisSmoothness);
                bakeShader.SetFloat("scleraSmoothness", scleraSmoothness);
                bakeShader.SetFloat("limbusDarkRadius", limbusDarkRadius);
                bakeShader.SetFloat("limbusDarkWidth", limbusDarkWidth);
                bakeShader.SetFloat("irisRadius", irisRadius);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeCorneaThicknessMap(
            float limbusDarkRadius, float limbusDarkWidth, float thicknessScale,
            string name)
        {
            Vector2Int maxSize = new Vector2Int(256, 256);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                int kernel = bakeShader.FindKernel("RLCorneaThickness");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetFloat("limbusDarkRadius", limbusDarkRadius);
                bakeShader.SetFloat("limbusDarkWidth", limbusDarkWidth);
                bakeShader.SetFloat("thicknessScale", thicknessScale);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        //bakedSubsurfaceMask = BakeCorneaSubsurfaceMask(
        //scleraSubsurfaceScale, irisSubsurfaceScale, subsurfaceThickness,
        //                sourceName + "_Thickness");
        private Texture2D BakeCorneaSubsurfaceMask(Texture2D baseMap,
            float scleraSubsurfaceScale, float irisSubsurfaceScale, float thicknessScale,
            Color subsurfaceFalloff,
            string name)
        {
            Vector2Int maxSize = new Vector2Int(128, 128);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                baseMap = CheckDiffuse(baseMap);

                int kernel = bakeShader.FindKernel("RLCorneaSubsurfaceMask");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "BaseMap", baseMap);
                bakeShader.SetFloat("scleraSubsurfaceScale", scleraSubsurfaceScale);
                bakeShader.SetFloat("irisSubsurfaceScale", irisSubsurfaceScale);
                bakeShader.SetFloat("thicknessScale", thicknessScale);
                bakeShader.SetVector("subsurfaceFalloff", subsurfaceFalloff);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeHairDiffuseMap(Texture2D diffuse, Texture2D blend, Texture2D id, Texture2D root, Texture2D mask,
                        float diffuseStrength, float alphaStrength, float alphaContrast, float aoStrength, float aoOccludeAll,
                        Color rootColor, float rootColorStrength, Color endColor, float endColorStrength, float globalStrength,
                        float invertRootMap, float baseColorStrength, float highlightBlend,
                        Color highlightAColor, Vector4 highlightADistribution, float highlightAOverlapEnd,
                        float highlightAOverlapInvert, float highlightAStrength,
                        Color highlightBColor, Vector4 highlightBDistribution, float highlightBOverlapEnd,
                        float highlightBOverlapInvert, float highlightBStrength,
                        float blendStrength, Color vertexBaseColor, float vertexColorStrength,
                        string name, string kernelName = "RLHairColoredDiffuse")
        {
            Vector2Int maxSize = GetMaxSize(diffuse, id);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name,
                    Importer.FLAG_SRGB +
                    (name.iContains("hair") ? Importer.FLAG_HAIR : Importer.FLAG_ALPHA_CLIP)
                );

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                diffuse = CheckDiffuse(diffuse);
                blend = CheckMask(blend);
                id = CheckOverlay(id);
                root = CheckMask(root);
                mask = CheckMask(mask);

                int kernel = bakeShader.FindKernel(kernelName);
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Diffuse", diffuse);
                bakeShader.SetTexture(kernel, "ColorBlend", blend);
                bakeShader.SetTexture(kernel, "ID", id);
                bakeShader.SetTexture(kernel, "Root", root);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetFloat("diffuseStrength", diffuseStrength);
                bakeShader.SetFloat("alphaStrength", alphaStrength);
                bakeShader.SetFloat("alphaContrast", alphaContrast);
                bakeShader.SetFloat("aoStrength", aoStrength);
                bakeShader.SetFloat("aoOccludeAll", aoOccludeAll);
                bakeShader.SetFloat("rootColorStrength", rootColorStrength);
                bakeShader.SetFloat("endColorStrength", endColorStrength);
                bakeShader.SetFloat("globalStrength", globalStrength);
                bakeShader.SetFloat("invertRootMap", invertRootMap);
                bakeShader.SetFloat("highlightBlend", highlightBlend);
                bakeShader.SetFloat("baseColorStrength", baseColorStrength);
                bakeShader.SetFloat("highlightAOverlapEnd", highlightAOverlapEnd);
                bakeShader.SetFloat("highlightAOverlapInvert", highlightAOverlapInvert);
                bakeShader.SetFloat("highlightAStrength", highlightAStrength);
                bakeShader.SetFloat("highlightBOverlapEnd", highlightBOverlapEnd);
                bakeShader.SetFloat("highlightBOverlapInvert", highlightBOverlapInvert);
                bakeShader.SetFloat("highlightBStrength", highlightBStrength);
                bakeShader.SetFloat("colorBlendStrength", blendStrength);
                bakeShader.SetFloat("vertexColorStrength", vertexColorStrength);
                bakeShader.SetVector("rootColor", rootColor);
                bakeShader.SetVector("endColor", endColor);
                bakeShader.SetVector("highlightAColor", highlightAColor);
                bakeShader.SetVector("highlightBColor", highlightBColor);
                bakeShader.SetVector("vertexBaseColor", vertexBaseColor);
                bakeShader.SetVector("highlightADistribution", highlightADistribution);
                bakeShader.SetVector("highlightBDistribution", highlightBDistribution);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeHairDiffuseMap(Texture2D diffuse, Texture2D blend, Texture2D mask,
                        float diffuseStrength, float alphaStrength, float alphaContrast, float aoStrength, float aoOccludeAll,
                        float blendStrength, Color vertexBaseColor, float vertexColorStrength,
                        string name, string kernelName = "RLHairDiffuse")
        {
            Vector2Int maxSize = GetMaxSize(diffuse);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name,
                    Importer.FLAG_SRGB +
                    (name.iContains("hair") ? Importer.FLAG_HAIR : Importer.FLAG_ALPHA_CLIP)
                );

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                diffuse = CheckDiffuse(diffuse);
                blend = CheckMask(blend);
                mask = CheckMask(mask);

                int kernel = bakeShader.FindKernel(kernelName);
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Diffuse", diffuse);
                bakeShader.SetTexture(kernel, "ColorBlend", blend);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetFloat("diffuseStrength", diffuseStrength);
                bakeShader.SetFloat("alphaStrength", alphaStrength);
                bakeShader.SetFloat("alphaContrast", alphaContrast);
                bakeShader.SetFloat("aoStrength", aoStrength);
                bakeShader.SetFloat("aoOccludeAll", aoOccludeAll);
                bakeShader.SetFloat("colorBlendStrength", blendStrength);
                bakeShader.SetFloat("vertexColorStrength", vertexColorStrength);
                bakeShader.SetVector("vertexBaseColor", vertexBaseColor);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeHairMaskMap(Texture2D mask, Texture2D specular,
            float aoStrength, float aoOccludeAll, float smoothnessMin, float smoothnessMax, float smoothnessContrast,
            string name, string kernelName)
        {
            Vector2Int maxSize = GetMaxSize(mask);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name, Importer.FLAG_ALPHA_DATA);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                mask = CheckHDRP(mask);
                specular = CheckMask(specular);

                int kernel = bakeShader.FindKernel(kernelName);
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetTexture(kernel, "Specular", specular);
                bakeShader.SetFloat("aoStrength", aoStrength);
                bakeShader.SetFloat("aoOccludeAll", aoOccludeAll);
                bakeShader.SetFloat("smoothnessMin", smoothnessMin);
                bakeShader.SetFloat("smoothnessMax", smoothnessMax);
                bakeShader.SetFloat("smoothnessContrast", smoothnessContrast);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeEyeOcclusionDiffuseMap(float occlusionStrength, float occlusionPower,
            float topMin, float topMax, float topCurve, float bottomMin, float bottomMax, float bottomCurve,
            float innerMin, float innerMax, float outerMin, float outerMax,
            float occlusionStrength2, float top2Min, float top2Max,
            float tearDuctPosition, float tearDuctWidth, Color occlusionColor,
            string name)
        {
            Vector2Int maxSize = new Vector2Int(256, 256);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name, Importer.FLAG_SRGB);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                int kernel = bakeShader.FindKernel("RLEyeOcclusionDiffuse");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetFloat("eoOcclusionStrength", occlusionStrength);
                bakeShader.SetFloat("eoOcclusionPower", occlusionPower);
                bakeShader.SetFloat("eoTopMin", topMin);
                bakeShader.SetFloat("eoTopMax", topMax);
                bakeShader.SetFloat("eoTopCurve", topCurve);
                bakeShader.SetFloat("eoBottomMin", bottomMin);
                bakeShader.SetFloat("eoBottomMax", bottomMax);
                bakeShader.SetFloat("eoBottomCurve", bottomCurve);
                bakeShader.SetFloat("eoInnerMin", innerMin);
                bakeShader.SetFloat("eoInnerMax", innerMax);
                bakeShader.SetFloat("eoOuterMin", outerMin);
                bakeShader.SetFloat("eoOuterMax", outerMax);
                bakeShader.SetFloat("eoOcclusionStrength2", occlusionStrength2);
                bakeShader.SetFloat("eoTop2Min", top2Min);
                bakeShader.SetFloat("eoTop2Max", top2Max);
                bakeShader.SetFloat("eoTearDuctPosition", tearDuctPosition);
                bakeShader.SetFloat("eoTearDuctWidth", tearDuctWidth);
                bakeShader.SetVector("eoEyeOcclusionColor", occlusionColor);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        public Texture2D BakeCorrectedHDRPMap(Texture2D mask, Texture2D detail, string name)
        {
            Texture2D bakedThickness = BakeHDRPMap(mask, detail, name);
            return bakedThickness;
        }

        public Texture2D BakeDefaultSkinThicknessMap(Texture2D thickness, string name)
        {
            Texture2D bakedThickness = BakeThicknessMap(thickness, 0f, 1.0f, Color.white, Texture2D.whiteTexture, true, name);
            return bakedThickness;
        }

        public Texture2D BakeDefaultDetailMap(Texture2D microNormal, string name)
        {
            Texture2D bakedDetail = BakeDetailMap(microNormal, name);
            return bakedDetail;
        }

        public Texture2D BakeFlowMapToNormalMap(Texture2D flowMap, Vector3 tangentVector, bool tangentFlipY,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(flowMap);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name, Importer.FLAG_NORMAL);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                flowMap = CheckNormal(flowMap);

                int kernel = bakeShader.FindKernel("RLFlowToNormal");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Flow", flowMap);
                bakeShader.SetFloat("tangentFlipY", tangentFlipY ? 1f : 0f);
                bakeShader.SetVector("tangentVector", tangentVector);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeHDRPMap(Texture2D mask, Texture2D detail,
            string name, string kernelName = "RLHDRPCorrected")
        {
            Vector2Int maxSize = GetMaxSize(mask);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                mask = CheckMask(mask);
                detail = CheckMask(detail);

                int kernel = bakeShader.FindKernel(kernelName);
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetTexture(kernel, "Detail", detail);
                Dispatch(bakeShader, kernel, bakeTarget);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        public static void Dispatch(ComputeShader bakeShader, int kernel, ComputeBakeTexture bakeTarget)
        {
            int x = (int)Math.Ceiling((float)bakeTarget.width / 16f);
            int y = (int)Math.Ceiling((float)bakeTarget.height / 16f);
            bakeShader.Dispatch(kernel, x, y, 1);
        }

        public static Texture2DArray CreateTextureArray(Texture2D[] textures, string path, bool linear)
        {
            int sliceCount = textures.Length;
            if (sliceCount == 0) return null;

            Texture2D first = textures[0];

            // Create the array with same dims/format
            var array = new Texture2DArray(
                first.width, first.height,
                sliceCount,
                first.format,
                false,
                linear
            );

            string guidInfo = "# Texture array source files\n";

            // Copy each texture into its slice
            int i = 0;
            foreach (Texture2D tex in textures)
            {
                Graphics.CopyTexture(tex, 0, 0, array, i++, 0);
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tex)).ToString();
                guidInfo += guid + "\n";
            }
            array.Apply();

            // Save as .asset so it�s assignable in Inspector
            AssetDatabase.CreateAsset(array, path);
            AssetDatabase.SaveAssets();

            string folder = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string infoFilePath = Path.Combine(folder, name + "_info.txt");
            StreamWriter writer = new StreamWriter(infoFilePath, false);
            writer.Write(guidInfo);
            writer.Close();
            AssetDatabase.ImportAsset(infoFilePath);

            return array;
        }

        public static List<Texture2D> GetTextureArraySourceFiles(Texture2DArray texArray)
        {
            List<Texture2D> textures = new List<Texture2D>();

            if (texArray)
            {
                string arrayPath = AssetDatabase.GetAssetPath(texArray);
                string folder = Path.GetDirectoryName(arrayPath);
                string file = Path.GetFileNameWithoutExtension(arrayPath);
                string infoPath = Path.Combine(folder, file + "_info.txt");

                TextAsset infoAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(infoPath);
                string[] lineEndings = new string[] { "\r\n", "\r", "\n" };
                string[] lines = infoAsset.text.Split(lineEndings, System.StringSplitOptions.None);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (!line.StartsWith("#"))
                    {
                        string guid = line;
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!string.IsNullOrEmpty(path))
                        {
                            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                            if (tex)
                            {
                                textures.Add(tex);
                            }
                        }
                    }

                }
            }

            return textures;
        }

        static void ComputeWeights(int radius, float sigma, ComputeBuffer weightsBuffer)
        {
            var weights = new float[radius + 1];
            float twoSigmaSq = 2f * sigma * sigma;

            // 1) Unnormalized
            for (int i = 0; i <= radius; i++)
                weights[i] = Mathf.Exp(-(i * i) / twoSigmaSq);

            // 2) Sum for normalization (center + 2� others)
            float sum = weights[0];
            for (int i = 1; i <= radius; i++)
                sum += 2f * weights[i];

            // 3) Normalize
            for (int i = 0; i <= radius; i++)
                weights[i] /= sum;

            weightsBuffer.SetData(weights);
        }

        public static Texture2D GuassianBlurTexture(Texture2D tex, int size,
                                                    int radius, float sigma,
                                                    string suffix, string nameOverride=null)
        {
            RenderTexture src;
            RenderTexture dst;
            RenderTexture temp;

            int w = size;
            int h = size;
            RenderTextureFormat rtFormat = RenderTextureFormat.ARGB32;

            src = new RenderTexture(w, h, 0, rtFormat);
            src.enableRandomWrite = true;
            src.Create();

            Graphics.Blit(tex, src);

            ComputeBuffer weightsBuffer;
            weightsBuffer = new ComputeBuffer(radius + 1, sizeof(float));
            ComputeWeights(radius, sigma, weightsBuffer);

            RenderTextureDescriptor desc = src.descriptor;
            desc.enableRandomWrite = true;
            desc.autoGenerateMips = false;
            desc.useMipMap = false;

            temp = new RenderTexture(desc);
            temp.Create();

            dst = new RenderTexture(desc);
            dst.Create();

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);

            // 1) Horizontal
            int kernelH = bakeShader.FindKernel("HorizontalBlur");
            bakeShader.SetInt("_BlurRadius", radius);
            bakeShader.SetBuffer(kernelH, "_BlurWeights", weightsBuffer);
            bakeShader.SetTexture(kernelH, "_BlurSource", src);
            bakeShader.SetTexture(kernelH, "_BlurResult", temp);
            BlurDispatch(bakeShader, src.width, src.height, kernelH);

            // 2) Vertical
            int kernelV = bakeShader.FindKernel("VerticalBlur");
            bakeShader.SetInt("_BlurRadius", radius);
            bakeShader.SetBuffer(kernelV, "_BlurWeights", weightsBuffer);
            bakeShader.SetTexture(kernelV, "_BlurSource", temp);
            bakeShader.SetTexture(kernelV, "_BlurResult", dst);
            BlurDispatch(bakeShader, src.width, src.height, kernelV);

            weightsBuffer.Dispose();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = dst;
            TextureFormat texFormat = TextureFormat.ARGB32;
            Texture2D texOut = new Texture2D(dst.width, dst.height, texFormat, true, false);
            texOut.ReadPixels(new Rect(0, 0, dst.width, dst.height), 0, 0);
            texOut.Apply();
            RenderTexture.active = null;

            byte[] bytes = texOut.EncodeToPNG();

            // Write file and import

            string texPath = AssetDatabase.GetAssetPath(tex);
            string folder = Path.GetDirectoryName(texPath);
            string name = Path.GetFileNameWithoutExtension(texPath);
            string path = Path.Combine(folder, $"{name}_{suffix}.png");
            if (!string.IsNullOrEmpty(nameOverride))
                path = Path.Combine(folder, $"{nameOverride}_{suffix}.png");

            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            ComputeBakeTexture.SetTextureImport(importer, Importer.FLAG_SRGB, TexCategory.MediumDetail);
            AssetDatabase.WriteImportSettingsIfDirty(path);
            texOut = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            // Cleanup
            Debug.Log($"RenderTexture saved to {path}");

            src.Release();
            UnityEngine.Object.DestroyImmediate(src);
            temp.Release();
            UnityEngine.Object.DestroyImmediate(temp);
            dst.Release();
            UnityEngine.Object.DestroyImmediate(dst);

            return texOut;
        }

        static void BlurDispatch(ComputeShader bakeShader, int width, int height, int kernel)
        {
            uint tx, ty, tz;
            bakeShader.GetKernelThreadGroupSizes(kernel, out tx, out ty, out tz);
            int gx = Mathf.CeilToInt((float)width / tx);
            int gy = Mathf.CeilToInt((float)height / ty);
            bakeShader.Dispatch(kernel, gx, gy, 1);
        }
    }
}
