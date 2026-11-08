using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Platformer.EditorTools
{
    /// <summary>
    /// One click setup for the pixel art resolution. Nothing runs automatically: the camera
    /// is only touched when the menu item is used, so the current build keeps working until
    /// the first pixel sprites land.
    /// </summary>
    public static class PixelArtSetup
    {
        /// <summary>
        /// 960x540 upscales to 1920x1080 exactly twice, so pixels never smear. With a 96px
        /// character that leaves about five and a half character heights of screen, the
        /// framing measured from INARI.
        /// </summary>
        const int ReferenceWidth = 960;
        const int ReferenceHeight = 540;

        /// <summary>
        /// 48 pixel tiles, so the 96px character is exactly two tiles tall.
        /// </summary>
        const int AssetsPixelsPerUnit = 48;

        [MenuItem("Tools/Pixel art/Configure camera (960x540, 48 PPU)")]
        static void ConfigureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("Pixel art setup: no camera tagged MainCamera in the open scene.");
                return;
            }

            var pixelPerfect = camera.GetComponent<PixelPerfectCamera>();
            if (pixelPerfect == null)
            {
                pixelPerfect = Undo.AddComponent<PixelPerfectCamera>(camera.gameObject);
            }
            else
            {
                Undo.RecordObject(pixelPerfect, "Configure pixel perfect camera");
            }

            pixelPerfect.assetsPPU = AssetsPixelsPerUnit;
            pixelPerfect.refResolutionX = ReferenceWidth;
            pixelPerfect.refResolutionY = ReferenceHeight;
            pixelPerfect.cropFrame = PixelPerfectCamera.CropFrame.None;
            pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;

            Undo.RecordObject(camera, "Configure pixel perfect camera");
            camera.orthographic = true;
            camera.orthographicSize = ReferenceHeight * 0.5f / AssetsPixelsPerUnit;

            EditorUtility.SetDirty(pixelPerfect);
            EditorUtility.SetDirty(camera);

            Debug.Log($"Pixel art setup: {ReferenceWidth}x{ReferenceHeight} at {AssetsPixelsPerUnit} PPU, " +
                      $"orthographic size {camera.orthographicSize}. " +
                      "Cinemachine users: add the Pixel Perfect extension to the virtual camera as well.");
        }

        [MenuItem("Tools/Pixel art/Create art folders")]
        static void CreateFolders()
        {
            CreateFolder("Assets", "Art");
            CreateFolder("Assets/Art", "Pixel");
            CreateFolder("Assets/Art/Pixel", "Characters");
            CreateFolder("Assets/Art/Pixel", "Tiles");
            CreateFolder("Assets/Art/Pixel", "Vfx");
            CreateFolder("Assets/Art/Pixel", "Ui");

            AssetDatabase.Refresh();
            Debug.Log("Pixel art setup: drop sprites into Assets/Art/Pixel to get the import " +
                      "settings applied automatically.");
        }

        static void CreateFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
