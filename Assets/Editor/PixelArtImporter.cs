using UnityEditor;
using UnityEngine;

namespace GetYourPhD.EditorTools
{
    /// <summary>Imports everything under Assets/Art as crisp 32-PPU pixel-art sprites.</summary>
    public class PixelArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Art/")) return;
            if (assetPath.Contains("/Reference/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 32;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;

            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            bool bottom = assetPath.Contains("/Johannes/") || assetPath.Contains("/Bottom/");
            s.spriteAlignment = bottom ? (int)SpriteAlignment.BottomCenter : (int)SpriteAlignment.Center;
            s.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(s);
        }
    }
}
