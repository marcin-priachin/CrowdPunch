using System.IO;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Editor
{
    public static class ArmorShieldIconBuilder
    {
        private const string Path = "Assets/CrowdPunch/Resources/ArmorShield.png";

        [MenuItem("Crowd Punch/Enemies/Rebuild Armor Shield Icon")]
        public static void Rebuild()
        {
            const int width = 48, height = 56;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color outline = new Color(.035f, .075f, .12f, 1f);
            Color face = new Color(.78f, .91f, 1f, 1f);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    Color pixel = Color.clear;
                    float coverage = 0f;
                    for (int sampleY = 0; sampleY < 4; sampleY++)
                        for (int sampleX = 0; sampleX < 4; sampleX++)
                        {
                            float sx = x + (sampleX + .5f) / 4f;
                            float sy = y + (sampleY + .5f) / 4f;
                            Color sample = InsideShield(sx, sy, 4f) ? face
                                : InsideShield(sx, sy, 0f) ? outline : Color.clear;
                            pixel += sample;
                            coverage += sample.a;
                        }
                    pixel /= 16f;
                    if (coverage > 0f) { pixel.r /= pixel.a; pixel.g /= pixel.a; pixel.b /= pixel.a; }
                    texture.SetPixel(x, y, pixel);
                }
            texture.Apply();
            File.WriteAllBytes(Path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static bool InsideShield(float x, float y, float inset)
        {
            float top = 51f - inset;
            float bottom = 2f + inset * 1.5f;
            if (y < bottom || y > top) return false;
            float halfWidth = y > 31f ? 20f - inset
                : Mathf.Lerp(0f, 20f - inset, (y - bottom) / (31f - bottom));
            return Mathf.Abs(x - 24f) <= halfWidth;
        }
    }
}
