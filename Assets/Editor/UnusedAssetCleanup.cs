using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Platformer.EditorTools
{
    /// <summary>
    /// Backs up and removes the tutorial and sample content the project shipped with.
    /// Nothing is deleted until every file has been copied out and the copy verified, and
    /// the run is abandoned outright if anything still in the project points into the set.
    /// </summary>
    public static class UnusedAssetCleanup
    {
        /// <summary>
        /// Folders removed whole. Each one was checked to have no incoming references from
        /// the rest of the project.
        /// </summary>
        static readonly string[] TargetFolders =
        {
            "Assets/Mod Assets",
            "Assets/Tutorials",
            "Assets/Environment/Tiles",

            // Left out on purpose, both fail the reference check:
            //
            // "Assets/Environment/Sprites"
            //     15 tile assets under Assets/Tiles and Assets/Prefabs/TilePalette.prefab
            //     read their sprites from here. Delete Assets/Tiles and the palette first
            //     if the whole tutorial tileset is going.
        };

        /// <summary>
        /// Individual files removed alongside the folders.
        /// </summary>
        static readonly string[] TargetFiles =
        {
            // Also left out, same reason:
            //
            // "Assets/Character/Sprites/PlayerJump.png"
            // "Assets/Character/Sprites/PlayerRun.png"
            // "Assets/Character/Sprites/PlayerLand.png"
            //     PlayerJump/Run/Land.anim and Player_backup.controller still point at
            //     these. Delete those clips and the backup controller first if the old
            //     tutorial character is going for good.
        };

        [MenuItem("Tools/Cleanup/Check unused asset references (dry run)")]
        static void DryRun()
        {
            List<string> doomed = CollectTargets(out List<string> missing);
            List<string> blockers = FindIncomingReferences(doomed);

            var sb = new StringBuilder();
            sb.AppendLine($"Cleanup dry run: {doomed.Count} asset(s) in the deletion set.");
            foreach (string m in missing) sb.AppendLine($"  target not found, skipped: {m}");

            if (blockers.Count == 0)
            {
                sb.AppendLine("No incoming references. Safe to delete.");
                Debug.Log(sb.ToString());
                return;
            }

            sb.AppendLine($"{blockers.Count} asset(s) outside the set still reference it:");
            foreach (string b in blockers) sb.AppendLine("  " + b);
            Debug.LogWarning(sb.ToString());
        }

        [MenuItem("Tools/Cleanup/Back up and delete tutorial assets")]
        static void BackupAndDelete()
        {
            List<string> doomed = CollectTargets(out List<string> missing);
            if (doomed.Count == 0)
            {
                EditorUtility.DisplayDialog("Cleanup", "Nothing to delete: no target path exists.", "OK");
                return;
            }

            // A reference from outside means deleting would leave a broken link behind, so
            // the whole run stops rather than half applying.
            List<string> blockers = FindIncomingReferences(doomed);
            if (blockers.Count > 0)
            {
                var report = new StringBuilder();
                report.AppendLine($"{blockers.Count} asset(s) outside the set still reference it. Nothing was deleted:");
                foreach (string b in blockers) report.AppendLine("  " + b);
                Debug.LogError(report.ToString());

                EditorUtility.DisplayDialog("Cleanup aborted",
                    $"{blockers.Count} asset(s) outside the deletion set still reference it.\n\n" +
                    "Nothing was touched. See the Console for the full list.", "OK");
                return;
            }

            long bytes = 0;
            int fileCount = 0;
            foreach (string path in EnumerateRealFiles(doomed))
            {
                bytes += new FileInfo(path).Length;
                fileCount++;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string backupRoot = Path.Combine(projectRoot,
                "_AssetBackup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            var summary = new StringBuilder();
            summary.AppendLine("Delete these, after backing them up?");
            summary.AppendLine();
            foreach (string t in doomed) summary.AppendLine("  " + t);
            summary.AppendLine();
            summary.AppendLine($"{fileCount} file(s), {bytes / 1048576f:F1} MB on disk.");
            summary.AppendLine();
            summary.AppendLine("Backup: " + backupRoot);

            if (!EditorUtility.DisplayDialog("Cleanup", summary.ToString(), "Back up and delete", "Cancel")) return;

            // --- backup, verified before anything is removed ---
            int copied;
            try
            {
                copied = Backup(doomed, projectRoot, backupRoot);
            }
            catch (Exception error)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError("Cleanup: backup failed, nothing deleted. " + error);
                EditorUtility.DisplayDialog("Cleanup aborted",
                    "The backup failed. Nothing was deleted.\n\n" + error.Message, "OK");
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (copied != fileCount)
            {
                Debug.LogError($"Cleanup: expected to back up {fileCount} file(s) but copied {copied}. Nothing deleted.");
                EditorUtility.DisplayDialog("Cleanup aborted",
                    $"Backup is incomplete ({copied}/{fileCount} files). Nothing was deleted.", "OK");
                return;
            }

            File.WriteAllText(Path.Combine(backupRoot, "RESTORE.txt"),
                "Copy the Assets folder in here back over the project's Assets folder to undo,\n" +
                "then let Unity reimport. The .meta files are included, so GUIDs and every\n" +
                "reference survive the round trip.\n", new UTF8Encoding(true));

            // --- delete ---
            var failed = new List<string>();
            foreach (string t in doomed)
            {
                if (!AssetDatabase.DeleteAsset(t)) failed.Add(t);
            }
            AssetDatabase.Refresh();

            if (failed.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Cleanup: these could not be deleted:");
                foreach (string f in failed) sb.AppendLine("  " + f);
                sb.AppendLine("The backup is complete either way: " + backupRoot);
                Debug.LogError(sb.ToString());
                return;
            }

            Debug.Log($"Cleanup: removed {doomed.Count} target(s), {fileCount} file(s), " +
                      $"{bytes / 1048576f:F1} MB. Backup at {backupRoot}");
        }

        /// <summary>
        /// Resolves the configured targets to the ones that actually exist, reporting the
        /// rest so a typo does not pass silently as "nothing to do".
        /// </summary>
        static List<string> CollectTargets(out List<string> missing)
        {
            var targets = new List<string>();
            missing = new List<string>();

            foreach (string folder in TargetFolders)
            {
                if (AssetDatabase.IsValidFolder(folder)) targets.Add(folder);
                else missing.Add(folder);
            }
            foreach (string file in TargetFiles)
            {
                if (File.Exists(file)) targets.Add(file);
                else missing.Add(file);
            }
            return targets;
        }

        /// <summary>
        /// Every asset outside the set that depends on something inside it. References from
        /// one doomed asset to another do not count: both are going.
        /// </summary>
        static List<string> FindIncomingReferences(List<string> targets)
        {
            var inside = new HashSet<string>();
            foreach (string p in AssetDatabase.GetAllAssetPaths())
            {
                if (!p.StartsWith("Assets")) continue;
                if (IsInside(p, targets)) inside.Add(p);
            }

            var blockers = new List<string>();
            foreach (string p in AssetDatabase.GetAllAssetPaths())
            {
                if (!p.StartsWith("Assets")) continue;
                if (inside.Contains(p) || AssetDatabase.IsValidFolder(p)) continue;

                foreach (string dep in AssetDatabase.GetDependencies(p, true))
                {
                    if (dep == p || !inside.Contains(dep)) continue;
                    blockers.Add($"{p}  ->  {dep}");
                    break;
                }
            }
            return blockers;
        }

        static bool IsInside(string path, List<string> targets)
        {
            foreach (string t in targets)
            {
                if (path == t || path.StartsWith(t + "/")) return true;
            }
            return false;
        }

        /// <summary>
        /// The real files behind the targets, .meta included, so the backup can be dropped
        /// straight back in.
        /// </summary>
        static IEnumerable<string> EnumerateRealFiles(List<string> targets)
        {
            foreach (string t in targets)
            {
                if (Directory.Exists(t))
                {
                    foreach (string f in Directory.GetFiles(t, "*", SearchOption.AllDirectories)) yield return f;
                    // The folder's own .meta lives beside it.
                    if (File.Exists(t + ".meta")) yield return t + ".meta";
                }
                else if (File.Exists(t))
                {
                    yield return t;
                    if (File.Exists(t + ".meta")) yield return t + ".meta";
                }
            }
        }

        /// <summary>
        /// Mirrors the targets under <paramref name="backupRoot"/>, keeping paths relative to
        /// the project root, and checks each copy's length before counting it.
        /// </summary>
        static int Backup(List<string> targets, string projectRoot, string backupRoot)
        {
            var files = new List<string>(EnumerateRealFiles(targets));
            int copied = 0;

            for (int i = 0; i < files.Count; i++)
            {
                string source = files[i];
                EditorUtility.DisplayProgressBar("Backing up", source, (float)i / files.Count);

                string full = Path.GetFullPath(source);
                string relative = full.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar);
                string destination = Path.Combine(backupRoot, relative);

                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(full, destination, true);

                if (new FileInfo(destination).Length == new FileInfo(full).Length) copied++;
                else Debug.LogError("Cleanup: size mismatch after copying " + source);
            }

            return copied;
        }
    }
}
