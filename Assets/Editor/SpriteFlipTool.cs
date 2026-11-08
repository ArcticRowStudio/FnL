using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Platformer.EditorTools
{
    /// <summary>
    /// Mirrors sprite textures horizontally, in place. The character sheets were rendered
    /// facing both ways, but the movement code treats "unflipped" as facing right, so the
    /// left facing sheets have to be baked the other way round once.
    ///
    /// The pixels are rewritten on disk rather than flipped at runtime: the .meta file is
    /// untouched, so GUIDs hold and every animation clip keeps pointing at the same sprite.
    /// </summary>
    public static class SpriteFlipTool
    {
        /// <summary>
        /// The rendered originals the pixel art is generated from. They live outside Assets,
        /// so flipping only the imported copies would be undone the next time the frames are
        /// regenerated.
        /// </summary>
        const string SourceRoot = "ArtSource/Juve_128px";
        const string ImportedRoot = "Assets/Art/Pixel/Characters/Player";

        [MenuItem("Tools/Pixel art/Flip selected sprites horizontally")]
        static void FlipSelection()
        {
            List<string> targets = CollectSelectedTextures();
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("Flip sprites",
                    "Select one or more textures, or folders containing them, in the Project window.",
                    "OK");
                return;
            }

            // Every counterpart that exists is flipped too, so the two copies never disagree.
            List<string> originals = new List<string>();
            foreach (string path in targets)
            {
                string original = FindRenderedOriginal(path);
                if (original != null) originals.Add(original);
            }

            string message = $"Flip {targets.Count} sprite(s) horizontally?";
            if (originals.Count > 0)
            {
                message += $"\n\nThe {originals.Count} matching file(s) under {SourceRoot} are flipped as well.";
            }
            message += "\n\nThis rewrites the files. There is no undo, but running the tool " +
                       "a second time restores them exactly.";

            if (!EditorUtility.DisplayDialog("Flip sprites", message, "Flip", "Cancel")) return;

            int flipped = 0;
            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Flip sprites", targets[i], (float)i / targets.Count);
                    if (FlipPng(targets[i])) flipped++;
                }

                foreach (string original in originals)
                {
                    if (FlipPng(original)) flipped++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            Debug.Log($"Flip sprites: {flipped} file(s) mirrored horizontally.");
        }

        [MenuItem("Tools/Pixel art/Flip selected sprites horizontally", true)]
        static bool FlipSelectionValidate() => Selection.objects.Length > 0;

        /// <summary>
        /// Expands the Project window selection into concrete png paths. Selecting a folder
        /// takes everything under it, which is how a whole animation is flipped in one go.
        /// </summary>
        static List<string> CollectSelectedTextures()
        {
            HashSet<string> paths = new HashSet<string>();

            foreach (Object selected in Selection.GetFiltered<Object>(SelectionMode.Assets))
            {
                string path = AssetDatabase.GetAssetPath(selected);
                if (string.IsNullOrEmpty(path)) continue;

                if (AssetDatabase.IsValidFolder(path))
                {
                    foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { path }))
                    {
                        string child = AssetDatabase.GUIDToAssetPath(guid);
                        if (IsPng(child)) paths.Add(child);
                    }
                }
                else if (IsPng(path) && selected is Texture2D)
                {
                    paths.Add(path);
                }
            }

            return new List<string>(paths);
        }

        static bool IsPng(string path) => path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Maps an imported frame back to the render it was pixelised from. Returns null when
        /// the asset lives elsewhere or the original is gone.
        /// </summary>
        static string FindRenderedOriginal(string assetPath)
        {
            if (!assetPath.StartsWith(ImportedRoot)) return null;

            string relative = assetPath.Substring(ImportedRoot.Length).TrimStart('/');
            // Application.dataPath ends in /Assets, and ArtSource sits beside it.
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string original = Path.Combine(projectRoot, SourceRoot, relative);

            return File.Exists(original) ? original : null;
        }

        /// <summary>
        /// Reads the png off disk, mirrors it and writes it back. Going through the raw bytes
        /// rather than the imported Texture2D means Read/Write Enabled does not have to be
        /// turned on, and the re-encode is lossless.
        /// </summary>
        static bool FlipPng(string path)
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (IOException error)
            {
                Debug.LogError($"Flip sprites: could not read {path}. {error.Message}");
                return false;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(bytes))
                {
                    Debug.LogError($"Flip sprites: {path} is not a readable png.");
                    return false;
                }

                int width = texture.width;
                int height = texture.height;
                Color32[] pixels = texture.GetPixels32();
                Color32[] mirrored = new Color32[pixels.Length];

                for (int y = 0; y < height; y++)
                {
                    int row = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        mirrored[row + x] = pixels[row + (width - 1 - x)];
                    }
                }

                texture.SetPixels32(mirrored);
                texture.Apply(false, false);

                File.WriteAllBytes(path, texture.EncodeToPNG());
                return true;
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
