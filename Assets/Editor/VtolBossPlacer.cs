using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Platformer.Mechanics.Boss;

namespace Platformer.EditorTools
{
    /// <summary>
    /// Puts the VTOL boss prefab into the open scene over the helipad. The boss hides itself until
    /// the player steps onto the helideck, so where it sits in the scene only matters for finding
    /// it; the arena bounds on VtolBossAI decide where it fights. Re-running replaces the old one.
    /// </summary>
    public static class VtolBossPlacer
    {
        const string PrefabPath = "Assets/Prefabs/VtolBoss.prefab";
        const string InstanceName = "VtolBoss";

        [MenuItem("Tools/Level/Place VTOL Boss On Helipad")]
        static void Menu()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("VTOL Boss", $"{PrefabPath} not found.", "OK");
                return;
            }

            foreach (var old in Object.FindObjectsByType<VtolBossAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Undo.DestroyObjectImmediate(old.gameObject);

            var boss = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            boss.name = InstanceName;
            var ai = boss.GetComponent<VtolBossAI>();
            // Park it where the intro starts: centred over the helideck, above the arena ceiling.
            boss.transform.position = new Vector3((ai.arenaLeft + ai.arenaRight) * 0.5f, ai.arenaCeilingY + 3f, 0f);
            Undo.RegisterCreatedObjectUndo(boss, "Place VTOL Boss");

            Selection.activeGameObject = boss;
            EditorSceneManager.MarkSceneDirty(boss.scene);
            Debug.Log($"VTOL boss placed at {boss.transform.position}; arena x {ai.arenaLeft}..{ai.arenaRight}, floor {ai.arenaFloorY}. Save the scene to keep it.");
        }
    }
}
