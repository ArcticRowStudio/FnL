using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Platformer.Mechanics;

namespace Platformer.EditorExtensions
{
    [CustomEditor(typeof(LadderZone))]
    public class LadderZoneEditor : Editor
    {
        private LadderZone ladder;

        private void OnEnable()
        {
            ladder = (LadderZone)target;
            Tools.hidden = false;
        }

        private void OnDisable()
        {
            Tools.hidden = false;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox("💡 씬 뷰에서 이동 도구(단축키 'W')로 빨간색/초록색 화살표를 드래그하여 원하는 위치로 옮긴 뒤 Ctrl+S를 누르시면 됩니다.", MessageType.Info);

            // Quick Presets
            EditorGUILayout.LabelField("Quick Placement", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📍 현재 씬 뷰 중앙으로", GUILayout.Height(28)))
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv != null)
                {
                    Undo.RecordObject(ladder.transform, "Move Ladder to View");
                    Vector3 viewPos = sv.pivot;
                    viewPos.z = 0f;
                    ladder.transform.position = viewPos;
                    EditorUtility.SetDirty(ladder.transform);
                    MarkDirty();
                }
            }
            if (GUILayout.Button("🏃 플레이어 앞으로", GUILayout.Height(28)))
            {
                var player = Object.FindAnyObjectByType<PlayerController>();
                if (player != null)
                {
                    Undo.RecordObject(ladder.transform, "Move Ladder to Player");
                    ladder.transform.position = new Vector3(player.transform.position.x + 1.5f, player.transform.position.y - 0.5f, 0f);
                    EditorUtility.SetDirty(ladder.transform);
                    MarkDirty();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // Ladder Configuration
            EditorGUILayout.LabelField("Ladder Configuration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ladderHeight"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("climbSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("snapToCenterSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("allowTopDismount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("topDismountOffset"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Quick Height Presets", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Short (2.5m)")) SetHeight(2.5f);
            if (GUILayout.Button("Standard (4.0m)")) SetHeight(4.0f);
            if (GUILayout.Button("Tall (6.5m)")) SetHeight(6.5f);
            if (GUILayout.Button("Tower (10.0m)")) SetHeight(10.0f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Sprite", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ladderSprite"));

            if (serializedObject.ApplyModifiedProperties())
            {
                ladder.RebuildVisualsAndCollider();
                MarkDirty();
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("💾 지금 씬 저장 (Ctrl+S)", GUILayout.Height(28)))
            {
                MarkDirty();
                if (!Application.isPlaying)
                {
                    var scene = ladder.gameObject.scene;
                    if (scene.IsValid())
                    {
                        EditorSceneManager.SaveScene(scene);
                        Debug.Log($"✅ [성공] 씬 저장 완료! 사다리 현재 위치: {ladder.transform.position}");
                    }
                }
            }
        }

        private void SetHeight(float h)
        {
            Undo.RecordObject(ladder, "Change Ladder Height Preset");
            ladder.ladderHeight = h;
            ladder.RebuildVisualsAndCollider();
            EditorUtility.SetDirty(ladder);
            MarkDirty();
            SceneView.RepaintAll();
        }

        private void MarkDirty()
        {
            if (ladder == null) return;
            EditorUtility.SetDirty(ladder.gameObject);
            EditorUtility.SetDirty(ladder.transform);
            if (!Application.isPlaying)
            {
                var scene = ladder.gameObject.scene;
                if (scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }
        }

        private void OnSceneGUI()
        {
            if (ladder == null) return;

            Vector3 basePos = ladder.transform.position;
            Vector3 topPos = new Vector3(basePos.x, ladder.TopY, basePos.z);

            // Only draw the top height adjustment handle (Cyan Sphere)
            EditorGUI.BeginChangeCheck();
            Handles.color = Color.cyan;
            float topHandleSize = HandleUtility.GetHandleSize(topPos) * 0.15f;
            Vector3 newTopPos = Handles.FreeMoveHandle(
                topPos,
                topHandleSize,
                Vector3.zero,
                Handles.SphereHandleCap
            );
            Handles.Label(topPos + Vector3.right * 0.35f, $"높이: {ladder.WorldHeight:F1}m (드래그하여 조절)");

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ladder, "Adjust Ladder Height");
                // The handle works in world units; ladderHeight is local, so undo the Y scale.
                float scaleY = Mathf.Max(0.0001f, Mathf.Abs(ladder.transform.lossyScale.y));
                float newHeight = Mathf.Max(0.5f, (newTopPos.y - ladder.transform.position.y) / scaleY);
                ladder.ladderHeight = Mathf.Round(newHeight * 10f) / 10f;
                ladder.RebuildVisualsAndCollider();
                EditorUtility.SetDirty(ladder);
                MarkDirty();
            }
        }
    }
}
