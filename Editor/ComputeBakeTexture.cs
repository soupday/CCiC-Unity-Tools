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

using Reallusion.Import;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Reallusion.Import
{
    public enum TexCategory { Default = 0, MinimalDetail=1, LowDetail = 2, MediumDetail = 3, HighDetail = 4, MaxDetail = 5 }

    public class ComputeBakeTexture
    {
        private readonly RenderTexture renderTexture;
        private readonly Texture2D saveTexture;
        public readonly int width;
        public readonly int height;
        public readonly string folderPath;
        public readonly string textureName;
        public readonly int flags;

        public static List<(TexCategory, CharacterInfo.TexSizeQuality, int)> categorySizeMatrix = new List<(TexCategory, CharacterInfo.TexSizeQuality, int)>()
        {
            (TexCategory.Default, CharacterInfo.TexSizeQuality.LowTexureSize, 1024),
            (TexCategory.Default, CharacterInfo.TexSizeQuality.MediumTextureSize, 2048),
            (TexCategory.Default, CharacterInfo.TexSizeQuality.HighTextureSize, 4096),
            (TexCategory.Default, CharacterInfo.TexSizeQuality.MaxTextureSize, 8192),

            (TexCategory.MinimalDetail, CharacterInfo.TexSizeQuality.LowTexureSize, 128),
            (TexCategory.MinimalDetail, CharacterInfo.TexSizeQuality.MediumTextureSize, 256),
            (TexCategory.MinimalDetail, CharacterInfo.TexSizeQuality.HighTextureSize, 512),
            (TexCategory.MinimalDetail, CharacterInfo.TexSizeQuality.MaxTextureSize, 1024),

            (TexCategory.LowDetail, CharacterInfo.TexSizeQuality.LowTexureSize, 256),
            (TexCategory.LowDetail, CharacterInfo.TexSizeQuality.MediumTextureSize, 512),
            (TexCategory.LowDetail, CharacterInfo.TexSizeQuality.HighTextureSize, 1024),
            (TexCategory.LowDetail, CharacterInfo.TexSizeQuality.MaxTextureSize, 2048),

            (TexCategory.MediumDetail, CharacterInfo.TexSizeQuality.LowTexureSize, 512),
            (TexCategory.MediumDetail, CharacterInfo.TexSizeQuality.MediumTextureSize, 1024),
            (TexCategory.MediumDetail, CharacterInfo.TexSizeQuality.HighTextureSize, 2048),
            (TexCategory.MediumDetail, CharacterInfo.TexSizeQuality.MaxTextureSize, 4096),

            (TexCategory.HighDetail, CharacterInfo.TexSizeQuality.LowTexureSize, 1024),
            (TexCategory.HighDetail, CharacterInfo.TexSizeQuality.MediumTextureSize, 2048),
            (TexCategory.HighDetail, CharacterInfo.TexSizeQuality.HighTextureSize, 4096),
            (TexCategory.HighDetail, CharacterInfo.TexSizeQuality.MaxTextureSize, 8192),

            (TexCategory.MaxDetail, CharacterInfo.TexSizeQuality.LowTexureSize, 2048),
            (TexCategory.MaxDetail, CharacterInfo.TexSizeQuality.MediumTextureSize, 4096),
            (TexCategory.MaxDetail, CharacterInfo.TexSizeQuality.HighTextureSize, 8192),
            (TexCategory.MaxDetail, CharacterInfo.TexSizeQuality.MaxTextureSize, 16384),
        };
               
        public static List<(TexCategory, CharacterInfo.TexCompressionQuality, TextureImporterCompression)> categoryQualMatrix = new List<(TexCategory, CharacterInfo.TexCompressionQuality, TextureImporterCompression)>()
        {
            ( TexCategory.Default, CharacterInfo.TexCompressionQuality.NoCompression, TextureImporterCompression.Uncompressed ),
            ( TexCategory.Default, CharacterInfo.TexCompressionQuality.LowTextureQuality, TextureImporterCompression.CompressedLQ ),
            ( TexCategory.Default, CharacterInfo.TexCompressionQuality.MediumTextureQuality, TextureImporterCompression.Compressed ),
            ( TexCategory.Default, CharacterInfo.TexCompressionQuality.HighTextureQuality, TextureImporterCompression.CompressedHQ ),
            ( TexCategory.Default, CharacterInfo.TexCompressionQuality.MaxTextureQuality, TextureImporterCompression.CompressedHQ ),

            ( TexCategory.MinimalDetail, CharacterInfo.TexCompressionQuality.NoCompression, TextureImporterCompression.Uncompressed ),
            ( TexCategory.MinimalDetail, CharacterInfo.TexCompressionQuality.LowTextureQuality, TextureImporterCompression.CompressedLQ ),
            ( TexCategory.MinimalDetail, CharacterInfo.TexCompressionQuality.MediumTextureQuality, TextureImporterCompression.CompressedLQ ),
            ( TexCategory.MinimalDetail, CharacterInfo.TexCompressionQuality.HighTextureQuality, TextureImporterCompression.Compressed ),
            ( TexCategory.MinimalDetail, CharacterInfo.TexCompressionQuality.MaxTextureQuality, TextureImporterCompression.CompressedHQ ),

            ( TexCategory.LowDetail, CharacterInfo.TexCompressionQuality.NoCompression, TextureImporterCompression.Uncompressed ),
            ( TexCategory.LowDetail, CharacterInfo.TexCompressionQuality.LowTextureQuality, TextureImporterCompression.CompressedLQ ),
            ( TexCategory.LowDetail, CharacterInfo.TexCompressionQuality.MediumTextureQuality, TextureImporterCompression.Compressed ),
            ( TexCategory.LowDetail, CharacterInfo.TexCompressionQuality.HighTextureQuality, TextureImporterCompression.Compressed ),
            ( TexCategory.LowDetail, CharacterInfo.TexCompressionQuality.MaxTextureQuality, TextureImporterCompression.CompressedHQ ),

            ( TexCategory.MediumDetail, CharacterInfo.TexCompressionQuality.NoCompression, TextureImporterCompression.Uncompressed ),
            ( TexCategory.MediumDetail, CharacterInfo.TexCompressionQuality.LowTextureQuality, TextureImporterCompression.Compressed ),
            ( TexCategory.MediumDetail, CharacterInfo.TexCompressionQuality.MediumTextureQuality, TextureImporterCompression.Compressed ),
            ( TexCategory.MediumDetail, CharacterInfo.TexCompressionQuality.HighTextureQuality, TextureImporterCompression.Compressed ),
            ( TexCategory.MediumDetail, CharacterInfo.TexCompressionQuality.MaxTextureQuality, TextureImporterCompression.CompressedHQ ),

            ( TexCategory.HighDetail, CharacterInfo.TexCompressionQuality.NoCompression, TextureImporterCompression.Uncompressed ),
            ( TexCategory.HighDetail, CharacterInfo.TexCompressionQuality.LowTextureQuality, TextureImporterCompression.Compressed ),
            ( TexCategory.HighDetail, CharacterInfo.TexCompressionQuality.MediumTextureQuality, TextureImporterCompression.Compressed ),
            ( TexCategory.HighDetail, CharacterInfo.TexCompressionQuality.HighTextureQuality, TextureImporterCompression.CompressedHQ ),
            ( TexCategory.HighDetail, CharacterInfo.TexCompressionQuality.MaxTextureQuality, TextureImporterCompression.CompressedHQ ),

            ( TexCategory.MaxDetail, CharacterInfo.TexCompressionQuality.NoCompression, TextureImporterCompression.Uncompressed ),
            ( TexCategory.MaxDetail, CharacterInfo.TexCompressionQuality.LowTextureQuality, TextureImporterCompression.Compressed ),
            ( TexCategory.MaxDetail, CharacterInfo.TexCompressionQuality.MediumTextureQuality, TextureImporterCompression.CompressedHQ ),
            ( TexCategory.MaxDetail, CharacterInfo.TexCompressionQuality.HighTextureQuality, TextureImporterCompression.CompressedHQ ),
            ( TexCategory.MaxDetail, CharacterInfo.TexCompressionQuality.MaxTextureQuality, TextureImporterCompression.CompressedHQ ),
        };

        private const string COMPUTE_SHADER_RESULT = "Result";

        public bool IsRGB { get { return (flags & Importer.FLAG_SRGB) > 0; } }
        public bool IsNormal { get { return (flags & Importer.FLAG_NORMAL) > 0; } }
        public bool IsAlphaClip { get { return (flags & Importer.FLAG_ALPHA_CLIP) > 0; } }
        public bool IsHair { get { return (flags & Importer.FLAG_HAIR) > 0; } }
        public bool IsAlphaData { get { return (flags & Importer.FLAG_ALPHA_DATA) > 0; } }

        public ComputeBakeTexture(Vector2Int size, string folder, string name, int flags = 0)
        {
            width = size.x;
            height = size.y;
            this.flags = flags;
            // RenderTextureReadWrite.sRGB/Linear is ignored:
            //     on windows platforms it is always linear
            //     on MacOS platforms it is always gamma
            if ((flags & Importer.FLAG_FLOAT) > 0)
            {
                renderTexture = new RenderTexture(width, height, 0,
                    RenderTextureFormat.ARGBFloat,
                    IsRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
                saveTexture = new Texture2D(width, height, TextureFormat.RGBAFloat, true, !IsRGB);
            }
            else
            {
                renderTexture = new RenderTexture(width, height, 0,
                    RenderTextureFormat.ARGB32,
                    IsRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
                saveTexture = new Texture2D(width, height, TextureFormat.ARGB32, true, !IsRGB);
            }
            folderPath = folder;
            textureName = name;
        }

        private string WriteImageFile()
        {            
            bool isFloat = (flags & Importer.FLAG_FLOAT) > 0f;
            string ext = isFloat ? ".exr" : ".png";
            
            string filePath = Path.Combine(Path.GetDirectoryName(Application.dataPath), folderPath, textureName + ext);

            Util.EnsureAssetsFolderExists(folderPath);

            if (isFloat)
            {
                byte[] exrArray = saveTexture.EncodeToEXR(Texture2D.EXRFlags.CompressZIP | Texture2D.EXRFlags.OutputAsFloat);
                File.WriteAllBytes(filePath, exrArray);
            }
            else
            {
                byte[] pngArray = saveTexture.EncodeToPNG();
                File.WriteAllBytes(filePath, pngArray);
            }

            string assetPath = Util.GetRelativePath(filePath);
            if (File.Exists(filePath)) return assetPath;
            else return "";
        }

        private void ApplyImportSettings(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                AssetDatabase.Refresh();
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(filePath);
                SetTextureImport(importer, flags);
                if (AssetDatabase.WriteImportSettingsIfDirty(filePath))
                {
                    importer.SaveAndReimport();
                }
            }
        }

        private void OldApplyImportSettings(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                AssetDatabase.Refresh();

                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(filePath);
                if (importer)
                {
                    importer.textureType = IsNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    importer.sRGBTexture = IsRGB;
                    importer.alphaIsTransparency = IsRGB && !IsAlphaData;                    
                    importer.mipmapEnabled = true;
                    importer.mipMapBias = Importer.MIPMAP_BIAS;
                    if (IsHair)
                    {
                        importer.mipMapBias = Importer.MIPMAP_BIAS_HAIR;
                        importer.mipMapsPreserveCoverage = true;
                        importer.alphaTestReferenceValue = Importer.MIPMAP_ALPHA_CLIP_HAIR_BAKED;
                    }
                    else if (IsAlphaClip)
                    {
                        importer.mipMapsPreserveCoverage = true;
                        importer.alphaTestReferenceValue = 0.5f;
                    }
                    else
                    {
                        importer.mipMapsPreserveCoverage = false;
                    }
                    importer.mipmapFilter = TextureImporterMipFilter.BoxFilter;
                    if ((flags & Importer.FLAG_READ_WRITE) > 0)
                    {
                        importer.isReadable = true;
                    }
                    if ((flags & Importer.FLAG_UNCOMPRESSED) > 0)
                    {
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.compressionQuality = 0;
                    }
                    importer.SaveAndReimport();
                    //AssetDatabase.WriteImportSettingsIfDirty(filePath);
                }
            }
        }

        public static int GetCategorySize(CharacterInfo charInfo, TexCategory category, 
                                          int defaultSize=2048)
        {
            CharacterInfo.TexSizeQuality sizeQuality;
            if (charInfo == null) sizeQuality = CharacterInfo.TexSizeQuality.MaxTextureSize;
            else sizeQuality = charInfo.QualTexSize;

            foreach ((TexCategory, CharacterInfo.TexSizeQuality, int)item in categorySizeMatrix)
            {
                if (item.Item1 == category && item.Item2 == sizeQuality)
                    return item.Item3;
            }

            return defaultSize;
        }

        public static TextureImporterCompression GetCategoryQuality(CharacterInfo charInfo, TexCategory category, 
                                             TextureImporterCompression defaultQuality = TextureImporterCompression.Compressed)
        {
            CharacterInfo.TexCompressionQuality compressionQuality;

            if (charInfo == null) compressionQuality = CharacterInfo.TexCompressionQuality.HighTextureQuality;
            else compressionQuality = charInfo.QualTexCompress;
            
            foreach ((TexCategory, CharacterInfo.TexCompressionQuality, TextureImporterCompression) item in categoryQualMatrix)
            {
                if (item.Item1 == category && item.Item2 == compressionQuality)
                    return item.Item3;
            }

            return defaultQuality;
        }


        public static void SetTextureImport(TextureImporter importer, int flags = 0, TexCategory category = TexCategory.Default, CharacterInfo charInfo = null)
        {
            string name = Path.GetFileName(importer.assetPath);
            int maxSize = GetCategorySize(charInfo, category);
            TextureImporterCompression compressionQuality = GetCategoryQuality(charInfo, category);

            // apply the sRGB and alpha settings for re-import.
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.BoxFilter;
            if ((flags & Importer.FLAG_SRGB) > 0)
            {
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = true;
                importer.mipmapFilter = TextureImporterMipFilter.BoxFilter;
                if ((flags & Importer.FLAG_HAIR) > 0)
                {
                    importer.mipMapsPreserveCoverage = true;
                    importer.alphaTestReferenceValue = Importer.MIPMAP_ALPHA_CLIP_HAIR;
                }
                else if ((flags & Importer.FLAG_ALPHA_CLIP) > 0)
                {
                    importer.mipMapsPreserveCoverage = true;
                    importer.alphaTestReferenceValue = 0.5f;
                }
                else
                {
                    importer.mipMapsPreserveCoverage = false;
                }
            }
            else
            {
                importer.sRGBTexture = false;
                importer.alphaIsTransparency = false;
                importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
                importer.mipMapBias = Importer.MIPMAP_BIAS;
                importer.mipMapsPreserveCoverage = false;
            }            

            if ((flags & Importer.FLAG_HAIR) > 0)
            {
                importer.mipMapBias = Importer.MIPMAP_BIAS_HAIR;
            }
            else
            {
                importer.mipMapBias = Importer.MIPMAP_BIAS;
            }

            if ((flags & Importer.FLAG_HAIR_ID) > 0)
            {
                importer.mipMapBias = Importer.MIPMAP_BIAS_HAIR;
                importer.mipmapEnabled = true;
            }

            // apply the texture type for re-import.
            if ((flags & Importer.FLAG_NORMAL) > 0)
            {
                importer.textureType = TextureImporterType.NormalMap;
                if (name.iEndsWith("Bump"))
                {
                    importer.convertToNormalmap = true;
                    importer.heightmapScale = 0.025f;
                    importer.normalmapFilter = TextureImporterNormalFilter.Standard;
                }
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
            }

            if ((flags & Importer.FLAG_FOR_ARRAY) > 0)
            {
                importer.isReadable = true;
                importer.textureCompression = compressionQuality;
                importer.maxTextureSize = maxSize;
                importer.crunchedCompression = false;
                importer.compressionQuality = 50;
            }
            else if ((flags & Importer.FLAG_FOR_BAKE) > 0)
            {
                // turn off texture compression and unlock max size to 8k, for the best possible quality bake
                importer.textureCompression = TextureImporterCompression.Uncompressed;                
                importer.maxTextureSize = 8192;
                importer.crunchedCompression = false;
                importer.compressionQuality = 0;
            }
            else
            {
                importer.textureCompression = compressionQuality;
                importer.maxTextureSize = maxSize;
                importer.crunchedCompression = false;
                importer.compressionQuality = 50;
            }

            if ((flags & Importer.FLAG_WRAP_CLAMP) > 0)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
            }

            if ((flags & Importer.FLAG_READ_WRITE) > 0)
            {
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
            }
        }


        public void Create(ComputeShader shader, int kernel)
        {
            renderTexture.enableRandomWrite = true;
            renderTexture.Create();
            shader.SetTexture(kernel, COMPUTE_SHADER_RESULT, renderTexture);
        }

        public Texture2D SaveAndReimport()
        {
            // copy the GPU render texture to a real texture2D
            RenderTexture old = RenderTexture.active;
            RenderTexture.active = renderTexture;
            saveTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            saveTexture.Apply();
            RenderTexture.active = old;

            string filePath = WriteImageFile();            
            ApplyImportSettings(filePath);            

            Texture2D assetTex = AssetDatabase.LoadAssetAtPath<Texture2D>(filePath);
            return assetTex;
        }
    }
}
