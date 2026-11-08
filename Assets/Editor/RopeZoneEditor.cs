using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Platformer.Mechanics;

namespace Platformer.EditorExtensions
{
    [CustomEditor(typeof(RopeZone))]
    public class RopeZoneEditor : Editor
    {
        private RopeZone rope;

        private void OnEnable()
        {
            rope = (RopeZone)target;
            Tools.hidden = false;
        }

        private void OnDisable()
        {
            Tools.hidden = false;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Placement Banner
            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("⚠️ [플레이 모드 실행 중 (게임 테스트 중)]\n유니티는 엔진 기본 규칙상 '플레이 모드(게임 실행 중)'에서는 Ctrl+S를 눌러도 씬이 저장되지 않습니다!\n대신, 플레이 모드를 끄시면(상단 ▶ 정지) 옮겨두신 위치가 에디트 모드 씬에 자동으로 영구 보존됩니다.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("💡 [에디트 모드 - 정상 저장 가능]\n로프를 원하는 곳에 배치하신 후 'Ctrl+S'를 누르거나 아래 [💾 씬에 영구 저장] 버튼을 누르시면 씬에 확실하게 저장됩니다.", MessageType.Info);
            }

            // Quick Teleport & Preset Buttons
            EditorGUILayout.LabelField("Quick Placement (원클릭 위치 이동)", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📍 현재 씬 뷰 화면 중앙으로", GUILayout.Height(30)))
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv != null)
                {
                    Undo.RecordObject(rope.transform, "Bring Rope to View");
                    Vector3 viewPos = sv.pivot;
                    viewPos.z = 0f;
                    rope.transform.position = viewPos;
                    EditorUtility.SetDirty(rope.transform);
                    MarkDirtyAndSave(true);
                }
            }
            if (GUILayout.Button("🏃 플레이어(Player) 앞으로", GUILayout.Height(30)))
            {
                var player = Object.FindAnyObjectByType<PlayerController>();
                if (player != null)
                {
                    Undo.RecordObject(rope.transform, "Move Rope to Player");
                    rope.transform.position = new Vector3(player.transform.position.x + 2.0f, player.transform.position.y + 3.0f, 0f);
                    EditorUtility.SetDirty(rope.transform);
                    MarkDirtyAndSave(true);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("⬆️ 2층 캣워크 (X: 24, Y: 33)", GUILayout.Height(24)))
            {
                Undo.RecordObject(rope.transform, "Move Rope to Catwalk");
                rope.transform.position = new Vector3(24.0f, 33.0f, 0f);
                EditorUtility.SetDirty(rope.transform);
                MarkDirtyAndSave(true);
            }
            if (GUILayout.Button("⬇️ 1층 바닥 (X: 10, Y: 3.5)", GUILayout.Height(24)))
            {
                Undo.RecordObject(rope.transform, "Move Rope to Bottom Pit");
                rope.transform.position = new Vector3(10.0f, 3.5f, 0f);
                EditorUtility.SetDirty(rope.transform);
                MarkDirtyAndSave(true);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // Nudge Buttons (fine-tuning movement)
            EditorGUILayout.LabelField("Fine Adjust Position (위치 미세 조정)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("X 이동");
            if (GUILayout.Button("◀ -1m")) Nudge(new Vector3(-1f, 0f, 0f));
            if (GUILayout.Button("◀ -0.2m")) Nudge(new Vector3(-0.2f, 0f, 0f));
            if (GUILayout.Button("+0.2m ▶")) Nudge(new Vector3(0.2f, 0f, 0f));
            if (GUILayout.Button("+1m ▶")) Nudge(new Vector3(1f, 0f, 0f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Y 이동");
            if (GUILayout.Button("▼ -1m")) Nudge(new Vector3(0f, -1f, 0f));
            if (GUILayout.Button("▼ -0.2m")) Nudge(new Vector3(0f, -0.2f, 0f));
            if (GUILayout.Button("+0.2m ▲")) Nudge(new Vector3(0f, 0.2f, 0f));
            if (GUILayout.Button("+1m ▲")) Nudge(new Vector3(0f, 1f, 0f));
            EditorGUILayout.EndHorizontal();

            // Transform & Scene Placement Section
            Vector3 currentPos = rope.transform.position;
            EditorGUI.BeginChangeCheck();
            Vector3 newPos = EditorGUILayout.Vector3Field("Current Position (X, Y, Z)", currentPos);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(rope.transform, "Change Rope Position");
                rope.transform.position = newPos;
                EditorUtility.SetDirty(rope.transform);
                MarkDirtyAndSave(false);
            }

            if (GUILayout.Button("💾 씬에 영구 저장 (Ctrl+S)", GUILayout.Height(26)))
            {
                MarkDirtyAndSave(true);
            }

            EditorGUILayout.Space(8);

            // Rope Configuration
            EditorGUILayout.LabelField("Rope Configuration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ropeLength"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("climbSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("slideSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("allowFastSlide"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("snapToCenterSpeed"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Quick Length Presets", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Short (3.0m)")) SetLength(3.0f);
            if (GUILayout.Button("Standard (5.0m)")) SetLength(5.0f);
            if (GUILayout.Button("Long (8.0m)")) SetLength(8.0f);
            if (GUILayout.Button("Deep Shaft (12.0m)")) SetLength(12.0f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Sprites", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("middleSprite"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("topAnchorSprite"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bottomStopperSprite"));

            if (serializedObject.ApplyModifiedProperties())
            {
                rope.RebuildVisualsAndCollider();
                MarkDirtyAndSave(false);
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Rebuild Rope Visuals"))
            {
                Undo.RecordObject(rope, "Rebuild Rope");
                rope.RebuildVisualsAndCollider();
                MarkDirtyAndSave(false);
                SceneView.RepaintAll();
            }
        }

        private void Nudge(Vector3 delta)
        {
            Undo.RecordObject(rope.transform, "Nudge Rope Position");
            rope.transform.position += delta;
            EditorUtility.SetDirty(rope.transform);
            MarkDirtyAndSave(true);
            SceneView.RepaintAll();
        }

        private void SetLength(float len)
        {
            Undo.RecordObject(rope, "Change Rope Length Preset");
            rope.ropeLength = len;
            rope.RebuildVisualsAndCollider();
            EditorUtility.SetDirty(rope);
            MarkDirtyAndSave(false);
            SceneView.RepaintAll();
        }

        private void MarkDirtyAndSave(bool saveNow)
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
                    if (saveNow)
                    {
                        EditorSceneManager.SaveScene(scene);
                        Debug.Log($"[RopeZoneEditor] 로프 '{rope.name}' 위치({rope.transform.position})가 씬에 영구 저장되었습니다.");
                    }
                }
            }
        }

        private void OnSceneGUI()
        {
            if (rope == null) return;

            Vector3 basePos = rope.transform.position;
            Vector3 bottomPos = basePos + Vector3.down * rope.ropeLength;

            // Bottom Length Handle (Cyan Sphere Cap at the bottom stopper)
            EditorGUI.BeginChangeCheck();
            Handles.color = new Color(0.3f, 0.8f, 1f, 1f);
            float bottomHandleSize = HandleUtility.GetHandleSize(bottomPos) * 0.15f;
            Vector3 newBottomPos = Handles.FreeMoveHandle(
                bottomPos,
                bottomHandleSize,
                Vector3.zero,
                Handles.SphereHandleCap
            );
            Handles.Label(bottomPos + Vector3.right * 0.4f, $"↕ 로프 길이: {rope.ropeLength:F1}m (드래그하여 조절)");

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(rope, "Adjust Rope Length");
                float newLength = Mathf.Max(0.5f, rope.transform.position.y - newBottomPos.y);
                rope.ropeLength = Mathf.Round(newLength * 10f) / 10f;
                rope.RebuildVisualsAndCollider();
                EditorUtility.SetDirty(rope);
                MarkDirtyAndSave(false);
            }
        }
    }
}
