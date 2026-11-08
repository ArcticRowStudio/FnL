using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Platformer.EditorTools
{
    /// <summary>
    /// Builds the level's collision out of plain boxes, from the list
    /// Tools/bake-level-collision.ps1 writes.
    ///
    /// A Polygon Collider 2D generated from the level sprite sits up to 0.3 units above the
    /// art, because the sprite is imported at 2048x1024 from a 15360x7680 source and the
    /// physics shape is traced off that downsampled, block compressed alpha. Collision is
    /// authored from the source art instead, which also drops the cost from a 761 point
    /// polygon to boxes the broadphase handles well.
    ///
    /// The boxes feed a Composite Collider 2D, so the seams between them are dissolved and
    /// nothing catches on an internal edge while running across a flat floor.
    /// </summary>
    public static class LevelCollisionBaker
    {
        const string CsvPath = "level-collision.csv";
        const string LevelObjectName = "Level 0";
        const string CollisionObjectName = "Collision";

        /// <summary>
        /// Boxes per child object. The composite merges them all either way, so this only
        /// keeps the inspector from trying to draw two thousand components at once.
        /// </summary>
        const int BoxesPerChunk = 64;

        [MenuItem("Tools/Level collision/Build box colliders from CSV")]
        static void BuildMenu() => Build(true);

        static GameObject FindLevelObject()
        {
            var go = GameObject.Find("backdrop_1_0");
            if (go != null) return go;
            go = GameObject.Find("backdrop_1");
            if (go != null) return go;
            go = GameObject.Find("backdrop_0");
            if (go != null) return go;
            return GameObject.Find(LevelObjectName);
        }

        /// <summary>
        /// Rebuilds the collision object. Returns the box count, or -1 if it could not run.
        /// </summary>
        public static int Build(bool interactive)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string csv = Path.Combine(projectRoot, CsvPath);
            if (!File.Exists(csv))
            {
                Report(interactive, $"{CsvPath} not found at the project root.\n\n" +
                                    "Run Tools/bake-level-collision.ps1 first.");
                return -1;
            }

            List<Rect> boxes = ReadBoxes(csv, out List<string> problems);
            if (problems.Count > 0)
            {
                foreach (string p in problems) Debug.LogWarning("Level collision: " + p);
            }
            if (boxes.Count == 0)
            {
                Report(interactive, $"No usable rows in {CsvPath}.");
                return -1;
            }

            GameObject level = FindLevelObject();
            if (level == null)
            {
                Report(interactive, $"No level GameObject (backdrop_1_0 or '{LevelObjectName}') in the open scene.");
                return -1;
            }

            Transform existing = level.transform.Find(CollisionObjectName);
            if (interactive)
            {
                string prompt = $"Create {boxes.Count} box collider(s) under {LevelObjectName}/{CollisionObjectName}?";
                if (existing != null) prompt += "\n\nThe existing Collision object will be replaced.";
                if (!EditorUtility.DisplayDialog("Level collision", prompt, "Build", "Cancel")) return -1;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build level collision");

            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

            var root = new GameObject(CollisionObjectName);
            Undo.RegisterCreatedObjectUndo(root, "Build level collision");
            root.transform.SetParent(level.transform, false);
            root.transform.localPosition = Vector3.zero;

            // The composite needs a body to hang off; static keeps it out of the solver.
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;

            var composite = root.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Synchronous;

            try
            {
                GameObject chunk = null;
                for (int i = 0; i < boxes.Count; i++)
                {
                    if (i % BoxesPerChunk == 0)
                    {
                        EditorUtility.DisplayProgressBar("Level collision",
                            $"box {i} / {boxes.Count}", (float)i / boxes.Count);

                        chunk = new GameObject($"Boxes_{i / BoxesPerChunk:D3}");
                        chunk.transform.SetParent(root.transform, false);
                        chunk.transform.localPosition = Vector3.zero;
                    }

                    Rect r = boxes[i];
                    var box = chunk.AddComponent<BoxCollider2D>();
                    box.usedByComposite = true;
                    // The rows are world space; the chunk sits at the level's origin.
                    box.offset = (Vector2)root.transform.InverseTransformPoint(r.center);
                    box.size = r.size;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            composite.GenerateGeometry();
            Undo.CollapseUndoOperations(undoGroup);

            EditorUtility.SetDirty(root);
            Debug.Log($"Level collision: {boxes.Count} box(es) in " +
                      $"{Mathf.CeilToInt(boxes.Count / (float)BoxesPerChunk)} chunk(s), " +
                      $"composite paths: {composite.pathCount}. " +
                      $"Disable the Polygon Collider 2D on {LevelObjectName} now.");

            Selection.activeGameObject = root;
            return boxes.Count;
        }

        static void Report(bool interactive, string message)
        {
            if (interactive) EditorUtility.DisplayDialog("Level collision", message, "OK");
            else Debug.LogError("Level collision: " + message);
        }

        /// <summary>
        /// Replaces the boxes and the composite with one Polygon Collider 2D holding the
        /// merged outline.
        ///
        /// The composite already reduces the boxes to a few hundred paths, and only that
        /// merged result takes part in collision: the boxes are scaffolding. Keeping them
        /// costs about 4.9 MB of scene file, which is serialised and restored on every domain
        /// reload, so entering play mode after any script edit drags. Baking keeps the exact
        /// same geometry and drops the scaffolding. Rebuild from the CSV to get it back.
        /// </summary>
        [MenuItem("Tools/Level collision/Bake composite into one PolygonCollider2D")]
        static void BakeCompositeMenu() => BakeComposite(true);

        /// <summary>
        /// Returns the baked point count, or -1 if it could not run.
        /// </summary>
        public static int BakeComposite(bool interactive)
        {
            GameObject level = FindLevelObject();
            Transform root = level == null ? null : level.transform.Find(CollisionObjectName);
            var composite = root == null ? null : root.GetComponent<CompositeCollider2D>();
            if (composite == null)
            {
                Report(interactive, $"No CompositeCollider2D under {LevelObjectName}/{CollisionObjectName}.\n\n" +
                                    "Build the box colliders first, or it is already baked.");
                return -1;
            }

            int boxCount = root.GetComponentsInChildren<BoxCollider2D>().Length;
            int points = 0;
            for (int i = 0; i < composite.pathCount; i++) points += composite.GetPathPointCount(i);

            if (interactive && !EditorUtility.DisplayDialog("Level collision",
                $"Bake {composite.pathCount} path(s) / {points} point(s) into a single " +
                $"PolygonCollider2D and delete {boxCount} box collider(s)?\n\n" +
                "The collision shape is unchanged. Rebuilding from the CSV restores the boxes.",
                "Bake", "Cancel")) return -1;

            // Grab the merged outline before anything is torn down.
            var paths = new Vector2[composite.pathCount][];
            for (int i = 0; i < composite.pathCount; i++)
            {
                var buffer = new Vector2[composite.GetPathPointCount(i)];
                composite.GetPath(i, buffer);
                paths[i] = buffer;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Bake level collision");

            for (int i = root.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);

            Undo.DestroyObjectImmediate(composite);

            // Without a Rigidbody2D the collider joins the implicit static body, which is
            // what a level wants and one less component to simulate.
            var body = root.GetComponent<Rigidbody2D>();
            if (body != null) Undo.DestroyObjectImmediate(body);

            var polygon = Undo.AddComponent<PolygonCollider2D>(root.gameObject);
            polygon.pathCount = paths.Length;
            for (int i = 0; i < paths.Length; i++) polygon.SetPath(i, paths[i]);

            Undo.CollapseUndoOperations(undoGroup);
            EditorUtility.SetDirty(root.gameObject);

            Debug.Log($"Level collision: baked to one PolygonCollider2D, " +
                      $"{polygon.pathCount} paths / {polygon.GetTotalPointCount()} points, " +
                      $"{boxCount} boxes removed.");
            Selection.activeGameObject = root.gameObject;
            return polygon.GetTotalPointCount();
        }

        [MenuItem("Tools/Level collision/Remove generated box colliders")]
        static void Remove()
        {
            GameObject level = FindLevelObject();
            Transform existing = level == null ? null : level.transform.Find(CollisionObjectName);
            if (existing == null)
            {
                EditorUtility.DisplayDialog("Level collision", "Nothing generated to remove.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Level collision",
                $"Delete {LevelObjectName}/{CollisionObjectName}?", "Delete", "Cancel")) return;

            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        /// <summary>
        /// Reads "centerX,centerY,sizeX,sizeY" rows. A malformed row is reported and skipped
        /// rather than aborting: a partial bake is easy to spot and rebuild.
        /// </summary>
        static List<Rect> ReadBoxes(string path, out List<string> problems)
        {
            var boxes = new List<Rect>();
            problems = new List<string>();
            string[] lines = File.ReadAllLines(path);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (i == 0 && line.StartsWith("center")) continue;   // header

                string[] parts = line.Split(',');
                if (parts.Length != 4)
                {
                    problems.Add($"line {i + 1}: expected 4 values, got {parts.Length}");
                    continue;
                }

                if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float cx) ||
                    !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float cy) ||
                    !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float sx) ||
                    !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float sy))
                {
                    problems.Add($"line {i + 1}: could not parse '{line}'");
                    continue;
                }

                if (sx <= 0f || sy <= 0f)
                {
                    problems.Add($"line {i + 1}: non positive size {sx}x{sy}");
                    continue;
                }

                boxes.Add(new Rect(cx - sx * 0.5f, cy - sy * 0.5f, sx, sy));
            }
            return boxes;
        }
    }
}
