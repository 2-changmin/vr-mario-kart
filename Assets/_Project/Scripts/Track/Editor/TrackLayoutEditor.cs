using UnityEditor;
using UnityEngine;
using VRKart.AI;
using VRKart.Race;

namespace VRKart.Track.Editor
{
    // TrackLayout 인스펙터에 "체크포인트 / AI 경로 다시 배치" 버튼을 붙인다. 레이아웃을 고친 뒤 한 번 누르면 된다.
    [CustomEditor(typeof(TrackLayout))]
    public sealed class TrackLayoutEditor : UnityEditor.Editor
    {
        private const string CheckpointPrefabPath = "Assets/_Project/Prefabs/Track/Checkpoint.prefab";

        private static float s_checkpointSpacing = 25f;
        private static float s_waypointSpacing = 6f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var layout = (TrackLayout)target;
            EditorGUILayout.HelpBox($"한 바퀴 {layout.Length:F0}m", MessageType.None);
            if (layout.MaxGrade > 0.001f)
            {
                // 기준(#50): 경사 10% 이하, 꼭대기 곡률 반지름 80m 이상(20m/s, 부스트 27m/s에서 뜨지 않게)
                bool steep = layout.MaxGrade > 0.10f;
                bool sharpCrest = layout.MinCrestRadius < 80f;
                string crest = float.IsInfinity(layout.MinCrestRadius) ? "없음" : $"{layout.MinCrestRadius:F0}m";
                EditorGUILayout.HelpBox($"최대 경사 {layout.MaxGrade * 100f:F1}% (기준 10% 이하), 언덕 꼭대기 반지름 최소 {crest} (기준 80m 이상)",
                    steep || sharpCrest ? MessageType.Warning : MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("레이스 오브젝트 맞추기", EditorStyles.boldLabel);
            s_checkpointSpacing = EditorGUILayout.Slider("체크포인트 간격 (m)", s_checkpointSpacing, 10f, 80f);
            if (GUILayout.Button("RaceTrack 체크포인트 다시 배치")) RebuildCheckpoints(layout, s_checkpointSpacing);
            s_waypointSpacing = EditorGUILayout.Slider("AI 웨이포인트 간격 (m)", s_waypointSpacing, 2f, 20f);
            if (GUILayout.Button("AIPath 웨이포인트 다시 배치")) RebuildWaypoints(layout, s_waypointSpacing);
            EditorGUILayout.HelpBox("출발선·출발 위치·카트는 결승선(Finish Distance) 위치에 맞춰 직접 옮겨야 합니다.", MessageType.Info);
        }

        // 결승선(0번)부터 일정 간격으로 체크포인트를 놓는다. 진행 방향 = 중심선 방향.
        public static void RebuildCheckpoints(TrackLayout layout, float spacing)
        {
            RaceTrack raceTrack = Object.FindAnyObjectByType<RaceTrack>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CheckpointPrefabPath);
            if (raceTrack == null || prefab == null)
            {
                Debug.LogError("[TrackLayout] 씬에 RaceTrack이 없거나 Checkpoint 프리팹을 찾을 수 없습니다.");
                return;
            }

            ClearChildren(raceTrack.transform);
            int count = Mathf.Max(4, Mathf.RoundToInt(layout.Length / spacing));
            for (int i = 0; i < count; i++)
            {
                TrackLayout.Sample sample = layout.Evaluate(layout.FinishDistance + layout.Length * i / count);
                var checkpoint = (GameObject)PrefabUtility.InstantiatePrefab(prefab, raceTrack.transform);
                checkpoint.name = $"Checkpoint_{i:00}";
                checkpoint.transform.SetPositionAndRotation(sample.Position, Quaternion.LookRotation(sample.Forward, Vector3.up));
                var serialized = new SerializedObject(checkpoint.GetComponent<Checkpoint>());
                serialized.FindProperty("_index").intValue = i;
                serialized.ApplyModifiedProperties();
                Undo.RegisterCreatedObjectUndo(checkpoint, "체크포인트 다시 배치");
            }
            Debug.Log($"[TrackLayout] 체크포인트 {count}개 배치 (약 {layout.Length / count:F0}m 간격)");
        }

        // 중심선을 따라 일정 간격으로 AI 웨이포인트를 놓는다.
        public static void RebuildWaypoints(TrackLayout layout, float spacing)
        {
            WaypointPath path = Object.FindAnyObjectByType<WaypointPath>();
            if (path == null)
            {
                Debug.LogError("[TrackLayout] 씬에 WaypointPath(AIPath)가 없습니다.");
                return;
            }

            ClearChildren(path.transform);
            int count = Mathf.Max(8, Mathf.RoundToInt(layout.Length / spacing));
            for (int i = 0; i < count; i++)
            {
                TrackLayout.Sample sample = layout.Evaluate(layout.Length * i / count);
                var waypoint = new GameObject($"WP_{i:000}");
                waypoint.transform.SetParent(path.transform, false);
                waypoint.transform.position = sample.Position;
                Undo.RegisterCreatedObjectUndo(waypoint, "AI 웨이포인트 다시 배치");
            }
            Debug.Log($"[TrackLayout] AI 웨이포인트 {count}개 배치");
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
        }
    }
}
