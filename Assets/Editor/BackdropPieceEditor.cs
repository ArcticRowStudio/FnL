using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Platformer.View;

namespace Platformer.EditorExtensions
{
    [CustomEditor(typeof(BackdropPiece))]
    public class BackdropPieceEditor : Editor
    {
        static readonly Color OutlineColor = new Color(1f, 0.85f, 0.1f, 1f);
        static readonly Color FillColor = new Color(1f, 0.85f, 0.1f, 0.08f);

        BackdropPiece piece;

        void OnEnable()
        {
            piece = (BackdropPiece)target;
            // The piece root is pinned to the backdrop origin; its move gizmo would only confuse.
            Tools.hidden = true;
        }

        void OnDisable()
        {
            Tools.hidden = false;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "노란 박스 = 원본 배경에서 잘라낸 영역. 씬 뷰에서 모서리 네모를 끌면 크기, 가운데 원을 끌면 위치가 바뀝니다 (픽셀 단위로 맞춰짐).\n" +
                "잘라낸 조각은 자식 'Visual'입니다. Visual을 선택해서 W로 옮기거나, 끄거나, 정렬 순서를 바꾸세요.\n" +
                "'원본에서 지우기'가 켜져 있으면 원래 자리에는 구멍이 남습니다.\n" +
                "'충돌도 따라가기'가 켜져 있으면 충돌도 Visual과 같이 움직이고, Visual을 끄면 충돌도 사라집니다.", MessageType.Info);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("source"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pixelRect"), new GUIContent("Pixel Rect"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("hideOriginal"), new GUIContent("원본에서 지우기"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("affectCollision"), new GUIContent("충돌도 따라가기"));

            if (serializedObject.ApplyModifiedProperties())
            {
                piece.Rebuild();
                MarkDirty();
            }

            var region = piece.LocalRegion;
            EditorGUILayout.LabelField("영역 크기 (유닛)", $"{region.width:0.##} x {region.height:0.##}");

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("영역", EditorStyles.boldLabel);
            if (GUILayout.Button("📍 씬 뷰 중앙으로 영역 이동", GUILayout.Height(24)))
                MoveRegionToSceneView();

            if (piece.visual == null) return;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("잘라낸 조각 (Visual)", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            int order = EditorGUILayout.IntField(new GUIContent("정렬 순서",
                "가릴 대상보다 높으면 앞에, 낮으면 뒤에 그려짐. 원본 배경은 " + (piece.source ? piece.source.sortingOrder : 0)),
                piece.visual.sortingOrder);
            bool shown = EditorGUILayout.Toggle(new GUIContent("보이기", "끄면 조각이 사라지고 원래 자리는 구멍으로 남음"),
                piece.visual.gameObject.activeSelf);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(piece.visual, "Change Piece Order");
                Undo.RecordObject(piece.visual.gameObject, "Toggle Piece");
                piece.visual.sortingOrder = order;
                piece.visual.gameObject.SetActive(shown);
                MarkDirty();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("✂ Visual 선택 (이동용)", GUILayout.Height(24)))
                Selection.activeGameObject = piece.visual.gameObject;
            GUI.enabled = piece.visual.transform.localPosition != Vector3.zero
                       || piece.visual.transform.localScale != Vector3.one
                       || piece.visual.transform.localRotation != Quaternion.identity;
            if (GUILayout.Button("↺ 원위치", GUILayout.Height(24)))
            {
                Undo.RecordObject(piece.visual.transform, "Reset Piece Offset");
                piece.visual.transform.localPosition = Vector3.zero;
                piece.visual.transform.localRotation = Quaternion.identity;
                piece.visual.transform.localScale = Vector3.one;
                MarkDirty();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            var off = piece.visual.transform.localPosition;
            if (off != Vector3.zero)
                EditorGUILayout.LabelField("원위치에서 이동한 양", $"({off.x:0.###}, {off.y:0.###})");
        }

        void OnSceneGUI()
        {
            var src = piece.source;
            if (src == null || src.sprite == null) return;
            float ppu = src.sprite.pixelsPerUnit;

            using (new Handles.DrawingScope(src.transform.localToWorldMatrix))
            {
                Rect r = piece.LocalRegion;
                var corners = new Vector3[]
                {
                    new Vector3(r.xMin, r.yMin), new Vector3(r.xMin, r.yMax),
                    new Vector3(r.xMax, r.yMax), new Vector3(r.xMax, r.yMin)
                };
                Handles.DrawSolidRectangleWithOutline(corners, FillColor, OutlineColor);

                Handles.color = OutlineColor;
                int xMin = piece.pixelRect.xMin, yMin = piece.pixelRect.yMin;
                int xMax = piece.pixelRect.xMax, yMax = piece.pixelRect.yMax;
                bool changed = false;

                for (int i = 0; i < 4; i++)
                {
                    float size = HandleUtility.GetHandleSize(corners[i]) * 0.07f;
                    EditorGUI.BeginChangeCheck();
                    var moved = Handles.FreeMoveHandle(corners[i], size, Vector3.zero, Handles.RectangleHandleCap);
                    if (!EditorGUI.EndChangeCheck()) continue;

                    var px2 = piece.LocalToPixel(moved);
                    int px = px2.x, py = px2.y;
                    bool left = i < 2, bottom = i == 0 || i == 3;
                    if (left) xMin = Mathf.Min(px, xMax - 1); else xMax = Mathf.Max(px, xMin + 1);
                    if (bottom) yMin = Mathf.Min(py, yMax - 1); else yMax = Mathf.Max(py, yMin + 1);
                    changed = true;
                }

                Vector3 center = r.center;
                float centerSize = HandleUtility.GetHandleSize(center) * 0.1f;
                EditorGUI.BeginChangeCheck();
                var newCenter = Handles.FreeMoveHandle(center, centerSize, Vector3.zero, Handles.CircleHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    int dx = Mathf.RoundToInt((newCenter.x - center.x) * ppu);
                    int dy = Mathf.RoundToInt((newCenter.y - center.y) * ppu);
                    xMin += dx; xMax += dx; yMin += dy; yMax += dy;
                    changed = true;
                }

                if (changed)
                {
                    Undo.RecordObject(piece, "Edit Backdrop Piece Region");
                    piece.pixelRect = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
                    piece.Rebuild();
                    MarkDirty();
                }

                Handles.Label(new Vector3(r.xMin, r.yMax), $"  {piece.pixelRect.width}x{piece.pixelRect.height}px", EditorStyles.whiteMiniLabel);
            }
        }

        void MoveRegionToSceneView()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null || piece.source == null || piece.source.sprite == null) return;
            var c = piece.LocalToPixel(piece.source.transform.InverseTransformPoint(sv.pivot));
            Undo.RecordObject(piece, "Move Backdrop Piece Region");
            var r = piece.pixelRect;
            piece.pixelRect = new RectInt(c.x - r.width / 2, c.y - r.height / 2, r.width, r.height);
            piece.Rebuild();
            MarkDirty();
        }

        void MarkDirty()
        {
            EditorUtility.SetDirty(piece);
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(piece.gameObject.scene);
        }

        // ── Creation ──────────────────────────────────────────────────────────

        const string MenuPath = "GameObject/Backdrop/배경 조각 잘라내기 (Backdrop Piece)";

        [MenuItem(MenuPath, false, 10)]
        static void CreatePiece()
        {
            var src = Selection.activeGameObject.GetComponent<SpriteRenderer>();
            var go = new GameObject("BackdropPiece");
            Undo.RegisterCreatedObjectUndo(go, "Create Backdrop Piece");
            go.transform.SetParent(src.transform, false);

            var piece = go.AddComponent<BackdropPiece>();
            piece.source = src;

            // Start as an 8x8 unit box under the scene view camera, clamped by Rebuild.
            int size = Mathf.RoundToInt(8f * src.sprite.pixelsPerUnit);
            var sv = SceneView.lastActiveSceneView;
            Vector3 local = sv != null ? src.transform.InverseTransformPoint(sv.pivot) : Vector3.zero;
            var c = piece.LocalToPixel(local);
            piece.pixelRect = new RectInt(c.x - size / 2, c.y - size / 2, size, size);

            Undo.RecordObject(src, "Create Backdrop Piece");
            piece.Rebuild();
            Undo.RegisterCreatedObjectUndo(piece.visual.gameObject, "Create Backdrop Piece");

            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        [MenuItem(MenuPath, true)]
        static bool CreatePieceValidate()
        {
            var go = Selection.activeGameObject;
            if (go == null) return false;
            var sr = go.GetComponent<SpriteRenderer>();
            return sr != null && sr.sprite != null && go.GetComponent<BackdropPiece>() == null;
        }
    }
}
