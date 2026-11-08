using UnityEditor;
using UnityEngine;
using Platformer.Mechanics;

namespace Platformer.EditorTools
{
    [CustomEditor(typeof(FreightElevatorController))]
    public class FreightElevatorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            FreightElevatorController elevator = (FreightElevatorController)target;

            // Header Banner
            EditorGUILayout.Space(4);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("⚡ Open Industrial Freight Elevator (화물승강기) ⚡", headerStyle);
            EditorGUILayout.Space(4);

            // Status Box
            Color statusColor = Color.gray;
            switch (elevator.State)
            {
                case FreightElevatorController.ElevatorState.AtFloor:
                    statusColor = new Color(0.2f, 0.85f, 0.3f);
                    break;
                case FreightElevatorController.ElevatorState.PassengerBoardedDelay:
                case FreightElevatorController.ElevatorState.ArrivedSettling:
                    statusColor = new Color(1.0f, 0.75f, 0.2f);
                    break;
                case FreightElevatorController.ElevatorState.Moving:
                    statusColor = new Color(1.0f, 0.3f, 0.3f);
                    break;
                case FreightElevatorController.ElevatorState.CooldownWait:
                    statusColor = new Color(0.4f, 0.7f, 1.0f);
                    break;
            }

            GUI.backgroundColor = statusColor;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;
            EditorGUILayout.LabelField($"Current Status: {elevator.State} | Current Floor: {elevator.CurrentFloor} | Passenger: {(elevator.HasPassenger ? "YES" : "NO")}", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // Dimension Quick Presets Box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📐 규격 및 줄 길이 프리셋 (Dimension Presets)", EditorStyles.boldLabel);

            // Width Presets
            EditorGUILayout.LabelField("승강기 가로 폭(넓이) 프리셋:", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("소형 (2.8m)"))
            {
                Undo.RecordObject(elevator, "Set Width Preset");
                elevator.elevatorWidth = 2.8f;
                elevator.ApplyDimensions();
                EditorUtility.SetDirty(elevator);
            }
            if (GUILayout.Button("표준 (4.2m)"))
            {
                Undo.RecordObject(elevator, "Set Width Preset");
                elevator.elevatorWidth = 4.2f;
                elevator.ApplyDimensions();
                EditorUtility.SetDirty(elevator);
            }
            if (GUILayout.Button("대형 (6.0m)"))
            {
                Undo.RecordObject(elevator, "Set Width Preset");
                elevator.elevatorWidth = 6.0f;
                elevator.ApplyDimensions();
                EditorUtility.SetDirty(elevator);
            }
            if (GUILayout.Button("특대형 (8.0m)"))
            {
                Undo.RecordObject(elevator, "Set Width Preset");
                elevator.elevatorWidth = 8.0f;
                elevator.ApplyDimensions();
                EditorUtility.SetDirty(elevator);
            }
            EditorGUILayout.EndHorizontal();

            // Cable Length Presets
            EditorGUILayout.LabelField("케이블 총 길이(줄 길이) 프리셋:", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("짧게 (10m)"))
            {
                Undo.RecordObject(elevator, "Set Cable Preset");
                elevator.cableLength = Mathf.Max(elevator.travelHeight, 10.0f);
                elevator.ApplyDimensions();
                EditorUtility.SetDirty(elevator);
            }
            if (GUILayout.Button("표준 (16m)"))
            {
                Undo.RecordObject(elevator, "Set Cable Preset");
                elevator.cableLength = Mathf.Max(elevator.travelHeight, 16.0f);
                elevator.ApplyDimensions();
                EditorUtility.SetDirty(elevator);
            }
            if (GUILayout.Button("길게 (24m)"))
            {
                Undo.RecordObject(elevator, "Set Cable Preset");
                elevator.cableLength = Mathf.Max(elevator.travelHeight, 24.0f);
                elevator.ApplyDimensions();
                EditorUtility.SetDirty(elevator);
            }
            if (GUILayout.Button("초대형 (32m)"))
            {
                Undo.RecordObject(elevator, "Set Cable Preset");
                elevator.cableLength = Mathf.Max(elevator.travelHeight, 32.0f);
                elevator.ApplyDimensions();
                EditorUtility.SetDirty(elevator);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("🗺 맵에 맞추기 (최상층 바닥 + 천장까지 줄 길이)", GUILayout.Height(24)))
            {
                Undo.RecordObject(elevator, "Fit Elevator To Map");
                foreach (var t in elevator.GetComponentsInChildren<Transform>(true))
                    Undo.RecordObject(t, "Fit Elevator To Map");
                foreach (var sr in elevator.GetComponentsInChildren<SpriteRenderer>(true))
                    Undo.RecordObject(sr, "Fit Elevator To Map");
                bool ok = elevator.FitToMap(out string report);
                EditorUtility.SetDirty(elevator);
                Debug.Log($"[FreightElevator] {report}", elevator);
                if (!ok) EditorUtility.DisplayDialog("맵에 맞추기", report, "확인");
            }
            if (GUILayout.Button("✨ 치수 즉시 적용 (Apply Dimensions)", GUILayout.Height(24)))
            {
                Undo.RecordObject(elevator, "Apply Dimensions");
                elevator.ApplyDimensions();
                EditorUtility.SetDirty(elevator);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // Test Buttons during Play Mode
            if (Application.isPlaying)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Trigger Boarding", GUILayout.Height(28)))
                {
                    elevator.TriggerBoardedSequence();
                }
                if (GUILayout.Button("Call Top Floor", GUILayout.Height(28)))
                {
                    elevator.CallElevatorToFloor(FreightElevatorController.FloorIndex.Top);
                }
                if (GUILayout.Button("Call Bottom Floor", GUILayout.Height(28)))
                {
                    elevator.CallElevatorToFloor(FreightElevatorController.FloorIndex.Bottom);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(6);
            }

            // Draw Default Inspector properties
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck())
            {
                elevator.ApplyDimensions();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            FreightElevatorController elevator = (FreightElevatorController)target;
            Vector3 rootPos = elevator.transform.position;
            float halfWidth = elevator.elevatorWidth * 0.5f;

            // 1. Width Handle (Right railing edge)
            Vector3 rightRailPos = rootPos + new Vector3(halfWidth, 1.6f, 0f);
            EditorGUI.BeginChangeCheck();
            Vector3 newRightPos = Handles.Slider(rightRailPos, Vector3.right, HandleUtility.GetHandleSize(rightRailPos) * 0.15f, Handles.CubeHandleCap, 0.1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(elevator, "Resize Elevator Width");
                float newHalfWidth = Mathf.Max(1.0f, newRightPos.x - rootPos.x);
                elevator.elevatorWidth = Mathf.Round(newHalfWidth * 2.0f * 10f) / 10f; // Round to 0.1
                elevator.ApplyDimensions();
                EditorUtility.SetDirty(elevator);
            }
            Handles.Label(rightRailPos + new Vector3(0.3f, 0f, 0f), $"Width: {elevator.elevatorWidth:F1}m");

            // 2. Travel Height Handle (Top floor platform level)
            Vector3 topFloorPos = rootPos + new Vector3(0f, elevator.travelHeight, 0f);
            EditorGUI.BeginChangeCheck();
            Vector3 newTopPos = Handles.Slider(topFloorPos, Vector3.up, HandleUtility.GetHandleSize(topFloorPos) * 0.15f, Handles.CubeHandleCap, 0.5f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(elevator, "Adjust Travel Height");
                elevator.travelHeight = Mathf.Max(2.0f, Mathf.Round((newTopPos.y - rootPos.y) * 2f) / 2f);
                if (elevator.cableLength < elevator.travelHeight)
                {
                    elevator.cableLength = elevator.travelHeight;
                }
                elevator.ApplyDimensions();
                EditorUtility.SetDirty(elevator);
            }
            Handles.Label(topFloorPos + new Vector3(halfWidth + 0.3f, 0f, 0f), $"Travel Height: {elevator.travelHeight:F1}m");

            // 3. Cable Length / Ceiling Anchor Handle
            float anchorY = rootPos.y + 3.2f + elevator.cableLength;
            Vector3 anchorPos = new Vector3(rootPos.x, anchorY, 0f);
            EditorGUI.BeginChangeCheck();
            Vector3 newAnchorPos = Handles.Slider(anchorPos, Vector3.up, HandleUtility.GetHandleSize(anchorPos) * 0.15f, Handles.SphereHandleCap, 0.5f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(elevator, "Adjust Cable Length");
                float newCableLen = newAnchorPos.y - (rootPos.y + 3.2f);
                elevator.cableLength = Mathf.Max(elevator.travelHeight, Mathf.Round(newCableLen * 2f) / 2f);
                elevator.ApplyDimensions();
                EditorUtility.SetDirty(elevator);
            }
            Handles.Label(anchorPos + new Vector3(0.5f, 0f, 0f), $"Cable Length: {elevator.cableLength:F1}m (Ceiling Y: {anchorY:F1})");
        }
    }
}
