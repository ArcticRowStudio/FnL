using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Platformer.Mechanics;

namespace Platformer.EditorTools
{
    /// <summary>
    /// Builds the scene side of the basement from ArtSource/basement_layout.json, the same file
    /// Tools/build-basement.py paints the art from: sprite placement and import, one box
    /// collider per solid, ladders, lamps, and the camera confiner pocket. Re-running replaces
    /// what it made before, so editing the layout is: change the JSON, run the Python, run this.
    /// </summary>
    public static class BasementBuilder
    {
        const string LayoutPath = "ArtSource/basement_layout.json";
        const string SpritePath = "Assets/Environment/Sprites/Level 0/basement_1.png";
        const string RootName = "[Basement]";
        const float SourcePpu = 100f;
        // backdrop_1 is 15360 px shown at 2048; the basement art gets the same 7.5x downscale
        const float BackdropDownscale = 7.5f;

        [Serializable] class Solid { public string name; public float[] rect; }
        [Serializable] class Ladder { public string name; public float x, bottom, top; }
        [Serializable] class Lamp { public float x, y; }
        [Serializable]
        class Layout
        {
            public float[] origin_world;
            public float[] size;
            public float confiner_bottom_world;
            public float canvas_overlap_px;
            public Solid[] solids;
            public Ladder[] ladders;
            public Lamp[] lamps;
        }

        [MenuItem("Tools/Level/Rebuild Basement From Layout")]
        static void Menu()
        {
            string err = Build();
            if (err != null) EditorUtility.DisplayDialog("Basement", err, "OK");
        }

        /// <summary>Returns null on success, otherwise what was missing.</summary>
        public static string Build()
        {
            string json = Path.Combine(Directory.GetParent(Application.dataPath).FullName, LayoutPath);
            if (!File.Exists(json)) return $"{LayoutPath} not found.";
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(json));
            var origin = new Vector2(layout.origin_world[0], layout.origin_world[1]);

            var expansion = GameObject.Find("Level_Expansion");
            var backdrop = GameObject.Find("backdrop_1_0");
            // Renamed from Demo_Ladder on 2026-11-08; the old name is kept as a fallback.
            var ladderTemplate = GameObject.Find("CannonL_Ladder") ?? GameObject.Find("Demo_Ladder");
            var lampTemplate = GameObject.Find("Corridor_Lamp_166");
            if (expansion == null || backdrop == null) return "Level_Expansion or backdrop_1_0 is missing from the open scene.";

            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rebuild Basement");

            Transform root = expansion.transform.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName).transform;
                Undo.RegisterCreatedObjectUndo(root.gameObject, "Rebuild Basement");
                root.SetParent(expansion.transform, false);
            }

            // art
            var sprite = ImportArt();
            var art = Child(root, "Basement_Structure");
            var sr = art.GetComponent<SpriteRenderer>();
            if (sr == null) sr = Undo.AddComponent<SpriteRenderer>(art.gameObject);
            var bdSr = backdrop.GetComponent<SpriteRenderer>();
            Undo.RecordObject(sr, "Rebuild Basement");
            Undo.RecordObject(art, "Rebuild Basement");
            sr.sprite = sprite;
            sr.sharedMaterial = bdSr.sharedMaterial;
            sr.sortingLayerID = bdSr.sortingLayerID;
            sr.sortingOrder = bdSr.sortingOrder + 1;   // over the backdrop's bottom border at the join
            art.position = new Vector3(origin.x, origin.y + layout.canvas_overlap_px / SourcePpu, 0f);

            // collision: one box per solid
            var colRoot = Child(root, "Basement_Collision");
            foreach (var old in colRoot.GetComponents<BoxCollider2D>()) Undo.DestroyObjectImmediate(old);
            Undo.RecordObject(colRoot, "Rebuild Basement");
            colRoot.position = Vector3.zero;
            foreach (var s in layout.solids)
            {
                var r = s.rect;
                var box = Undo.AddComponent<BoxCollider2D>(colRoot.gameObject);
                box.offset = new Vector2(origin.x + (r[0] + r[2]) * 0.5f, origin.y - (r[1] + r[3]) * 0.5f);
                box.size = new Vector2(r[2] - r[0], r[3] - r[1]);
            }

            // ladders, set up like Demo_Ladder
            var demo = ladderTemplate != null ? ladderTemplate.GetComponent<LadderZone>() : null;
            foreach (Transform t in Children(root))
                if (t.name.StartsWith("Basement_Ladder_") && Array.FindIndex(layout.ladders, l => l.name == t.name) < 0)
                    Undo.DestroyObjectImmediate(t.gameObject);
            foreach (var l in layout.ladders)
            {
                var t = Child(root, l.name);
                var lz = t.GetComponent<LadderZone>();
                if (lz == null) lz = Undo.AddComponent<LadderZone>(t.gameObject);
                Undo.RecordObject(t, "Rebuild Basement");
                Undo.RecordObject(lz, "Rebuild Basement");
                t.position = new Vector3(origin.x + l.x, origin.y - l.bottom, 0f);
                t.localScale = new Vector3(demo != null ? demo.transform.lossyScale.x : 1f, 1f, 1f);
                lz.ladderHeight = l.bottom - l.top;
                lz.allowTopDismount = true;
                if (demo != null)
                {
                    lz.climbSpeed = demo.climbSpeed;
                    lz.snapToCenterSpeed = demo.snapToCenterSpeed;
                    lz.topDismountOffset = demo.topDismountOffset;
                    lz.ladderSprite = demo.ladderSprite;
                    var so = new SerializedObject(lz);
                    so.FindProperty("sortingOrder").intValue = new SerializedObject(demo).FindProperty("sortingOrder").intValue;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                lz.RebuildVisualsAndCollider();
            }

            // lamps, copies of the corridor lamp
            foreach (Transform t in Children(root))
                if (t.name.StartsWith("Basement_Lamp_")) Undo.DestroyObjectImmediate(t.gameObject);
            if (lampTemplate != null)
            {
                for (int i = 0; i < layout.lamps.Length; i++)
                {
                    var lamp = UnityEngine.Object.Instantiate(lampTemplate, root);
                    Undo.RegisterCreatedObjectUndo(lamp, "Rebuild Basement");
                    lamp.name = $"Basement_Lamp_{i + 1}";
                    lamp.transform.position = new Vector3(origin.x + layout.lamps[i].x, origin.y - layout.lamps[i].y, 0f);
                }
            }

            ConfinerPocket(origin, layout);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log($"Basement rebuilt: {layout.solids.Length} solids, {layout.ladders.Length} ladders, {layout.lamps.Length} lamps.", root);
            return null;
        }

        static Sprite ImportArt()
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            var reference = (TextureImporter)AssetImporter.GetAtPath("Assets/Environment/Sprites/Level 0/backdrop_1.png");
            ti.GetSourceTextureWidthAndHeight(out int w, out int h);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = SourcePpu;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = new Vector2(0f, 1f);
            s.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(s);
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.filterMode = reference.filterMode;
            ti.textureCompression = reference.textureCompression;
            ti.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.RoundToInt(Mathf.Max(w, h) / BackdropDownscale));
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        /// <summary>The confiner keeps its outer box; its bottom drops to the basement floor under the basement.</summary>
        static void ConfinerPocket(Vector2 origin, Layout layout)
        {
            var go = GameObject.Find("CinemachineConfiner");
            var poly = go != null ? go.GetComponent<PolygonCollider2D>() : null;
            if (poly == null) return;
            Undo.RecordObject(poly, "Rebuild Basement");
            Vector2 c = poly.transform.position;
            float minX = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in poly.GetPath(0)) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); maxY = Mathf.Max(maxY, p.y); }
            float bottom = layout.confiner_bottom_world - c.y;
            float pocket = origin.y - layout.size[1] - c.y;
            float px0 = origin.x - c.x, px1 = origin.x + layout.size[0] - c.x;
            poly.SetPath(0, new[] {
                new Vector2(minX, bottom), new Vector2(px0, bottom), new Vector2(px0, pocket),
                new Vector2(px1, pocket), new Vector2(px1, bottom), new Vector2(maxX, bottom),
                new Vector2(maxX, maxY), new Vector2(minX, maxY) });
            var vcam = GameObject.Find("CM vcam1");
            if (vcam != null)
            {
                vcam.SendMessage("InvalidateBoundingShapeCache", SendMessageOptions.DontRequireReceiver);
                vcam.SendMessage("InvalidateCache", SendMessageOptions.DontRequireReceiver);
            }
        }

        static Transform Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return t;
            t = new GameObject(name).transform;
            Undo.RegisterCreatedObjectUndo(t.gameObject, "Rebuild Basement");
            t.SetParent(parent, false);
            return t;
        }

        static Transform[] Children(Transform parent)
        {
            var list = new Transform[parent.childCount];
            for (int i = 0; i < list.Length; i++) list[i] = parent.GetChild(i);
            return list;
        }
    }
}
