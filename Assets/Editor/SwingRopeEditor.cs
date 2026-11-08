using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Platformer.Mechanics;

namespace Platformer.EditorExtensions
{
    [CustomEditor(typeof(SwingRope))]
    public class SwingRopeEditor : Editor
    {
        private SwingRope rope;

        private void OnEnable()
        {
            rope = (SwingRope)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("🟢 [플레이 모드 실행 중]\n[F] 키로 줄을 잡고, [A/D]로 반동을 준 뒤 [F] 또는 [Space]로 놓으면 멀리 도약합니다!", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("💡 [에디트 모드]\n피봇 개수(segmentCount)를 조절하여 부드러운 다중 피봇 줄을 구성할 수 있습니다.\n씬 뷰에서 아래 핸들을 드래그하여 줄 길이를 조절하세요.", MessageType.None);
            }

            // Quick Teleport & Presets
            EditorGUILayout.LabelField("Quick Placement", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📍 현재 화면 중앙으로", GUILayout.Height(28)))
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv != null)
                {
                    Undo.RecordObject(rope.transform, "Move Swing Rope to View");
                    Vector3 viewPos = sv.pivot;
                    viewPos.z = 0f;
                    rope.transform.position = viewPos;
                    EditorUtility.SetDirty(rope.transform);
                    MarkDirtyAndSave();
                }
            }
            if (GUILayout.Button("🏃 플레이어 위로", GUILayout.Height(28)))
            {
                var player = Object.FindAnyObjectByType<PlayerController>();
                if (player != null)
                {
                    Undo.RecordObject(rope.transform, "Move Swing Rope to Player");
                    rope.transform.position = new Vector3(player.transform.position.x + 2.5f, player.transform.position.y + 4.5f, 0f);
                    EditorUtility.SetDirty(rope.transform);
                    MarkDirtyAndSave();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // Multi-Pivot Configurations
            EditorGUILayout.LabelField("Multi-Pivot Rope Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("segmentCount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ropeLength"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gravity"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("damping"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("flexResponsiveness"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("whipLagMultiplier"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pumpForce"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("brakeForce"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxAngle"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("climbSpeed"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Grab & Launch / Jump Off (멀리 점프)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("grabRadius"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("launchBoost"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("extraJumpUpward"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("minLaunchSpeed"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Visuals & Sprites", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sortingOrder"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("middleSprite"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("topAnchorSprite"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bottomStopperSprite"));

            if (serializedObject.ApplyModifiedProperties())
            {
                rope.RebuildVisualsAndCollider();
                MarkDirtyAndSave();
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("🔄 줄 리빌드 (Rebuild Visuals)", GUILayout.Height(26)))
            {
                rope.RebuildVisualsAndCollider();
                MarkDirtyAndSave();
            }
            if (GUILayout.Button("💾 씬에 저장 (Ctrl+S)", GUILayout.Height(26)))
            {
                MarkDirtyAndSave();
            }
        }

        private void MarkDirtyAndSave()
        {
            if (rope == null) return;
            EditorUtility.SetDirty(rope.gameObject);
            EditorUtility.SetDirty(rope.transform);
            if (!Application.isPlaying)
            {
                var scene = rope.gameObject.scene;
                if (scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }
        }

        private void OnSceneGUI()
        {
            if (rope == null) return;

            Vector3 anchor = rope.transform.position;
            Vector3 bottom = anchor + Vector3.down * rope.ropeLength;

            // Handle at bottom to adjust length
            EditorGUI.BeginChangeCheck();
            Handles.color = new Color(0.3f, 0.9f, 1f, 1f);
            float handleSize = HandleUtility.GetHandleSize(bottom) * 0.15f;
            Vector3 newBottom = Handles.FreeMoveHandle(
                bottom,
                handleSize,
                Vector3.zero,
                Handles.SphereHandleCap
            );
            Handles.Label(bottom + Vector3.right * 0.35f, $"Length: {rope.ropeLength:F1}m ({rope.segmentCount} Pivots)");

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(rope, "Adjust Swing Rope Length");
                float newLen = Mathf.Max(1.5f, Vector3.Distance(anchor, newBottom));
                rope.ropeLength = Mathf.Round(newLen * 10f) / 10f;
                rope.RebuildVisualsAndCollider();
                EditorUtility.SetDirty(rope);
                MarkDirtyAndSave();
            }
        }
    }
}
