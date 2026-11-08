using UnityEditor;
using UnityEngine;

namespace Platformer.EditorTools
{
    /// <summary>
    /// Applies the project's pixel art import rules to any texture dropped into
    /// <see cref="PixelArtFolder"/>, so new sprites are game ready without manual setup.
    /// Settings are only written on the very first import: anything changed by hand in the
    /// inspector afterwards is left alone.
    /// </summary>
    public class PixelArtImportSettings : AssetPostprocessor
    {
        /// <summary>
        /// Only textures under this folder are touched. Existing art elsewhere in the
        /// project keeps its own settings.
        /// </summary>
        public const string PixelArtFolder = "Assets/Art/Pixel";

        /// <summary>
        /// 48 pixels per unit means one world unit is one 48x48 tile, and the 96px
        /// character is exactly two units tall.
        /// </summary>
        public const float PixelsPerUnit = 48f;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(PixelArtFolder)) return;

            var importer = (TextureImporter)assetImporter;

            // importSettingsMissing is only true before a .meta exists, i.e. on first import.
            if (!importer.importSettingsMissing) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.spriteImportMode = SpriteImportMode.Single;

            // The default cap is 2048, which would silently resample a wider background and
            // break the one image pixel to one game pixel rule the whole look depends on.
            importer.maxTextureSize = 8192;
        }
    }
}
