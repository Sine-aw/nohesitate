#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class VineScatter : EditorWindow
{
    public GameObject[] prefabs = new GameObject[3];
    SerializedObject so;
    SerializedProperty prefabsProp;

    LayerMask wallMask = ~0;
    int count = 50;
    float areaSize = 20f;
    float surfaceOffset = 0.02f;   // 벽에서 살짝 띄우기 (Z파이팅 방지)
    float minScale = 0.8f, maxScale = 1.3f;
    float randomRoll = 15f;        // 벽면 기준 랜덤 회전(도)
    float minNormalY = -0.3f, maxNormalY = 0.3f; // 수직에 가까운 면(벽)만 허용

    [MenuItem("Tools/Vine Scatter")]
    static void Open() => GetWindow<VineScatter>("Vine Scatter");

    void OnGUI()
    {
        if (so == null)
        {
            so = new SerializedObject(this);
            prefabsProp = so.FindProperty("prefabs");
        }
        so.Update();
        EditorGUILayout.PropertyField(prefabsProp, new GUIContent("덩굴 프리팹들"), true);
        so.ApplyModifiedProperties();

        wallMask = EditorGUILayout.MaskField("벽 레이어", wallMask, UnityEditorInternal.InternalEditorUtility.layers);
        count = EditorGUILayout.IntField("생성 개수", count);
        areaSize = EditorGUILayout.FloatField("배치 범위(반경)", areaSize);
        surfaceOffset = EditorGUILayout.FloatField("벽 오프셋", surfaceOffset);
        minScale = EditorGUILayout.FloatField("최소 스케일", minScale);
        maxScale = EditorGUILayout.FloatField("최대 스케일", maxScale);
        randomRoll = EditorGUILayout.FloatField("랜덤 회전(도)", randomRoll);

        EditorGUILayout.Space();
        if (GUILayout.Button("선택한 오브젝트 중심으로 배치")) ScatterAroundSelection();
        if (GUILayout.Button("선택한 벽들에 배치")) ScatterOnSelectedWalls();
    }

    // 방법 A: 선택한 오브젝트 주변 반경 안의 벽에 랜덤 배치
    void ScatterAroundSelection()
    {
        if (!Validate()) return;
        Vector3 center = Selection.activeTransform ? Selection.activeTransform.position : Vector3.zero;
        var parent = new GameObject("Vines_Generated").transform;
        Undo.RegisterCreatedObjectUndo(parent.gameObject, "Vine Scatter");

        int placed = 0, tries = 0;
        while (placed < count && tries < count * 30)
        {
            tries++;
            // 중심 주변 랜덤 지점에서 사방으로 레이를 쏴서 벽 찾기
            Vector3 origin = center + Random.insideUnitSphere * areaSize;
            Vector3 dir = Random.onUnitSphere;
            dir.y *= 0.2f; dir.Normalize();   // 수평 방향 위주
            if (TryPlace(origin, dir, parent)) placed++;
        }
        Debug.Log($"덩굴 {placed}개 배치 완료");
    }

    // 방법 B: 선택한 벽 오브젝트들의 콜라이더 표면에 직접 배치
    void ScatterOnSelectedWalls()
    {
        if (!Validate()) return;
        var parent = new GameObject("Vines_Generated").transform;
        Undo.RegisterCreatedObjectUndo(parent.gameObject, "Vine Scatter");

        int placed = 0;
        foreach (var go in Selection.gameObjects)
        {
            var col = go.GetComponent<Collider>();
            if (!col) continue;
            Bounds b = col.bounds;
            int perWall = Mathf.Max(1, count / Selection.gameObjects.Length);

            for (int i = 0; i < perWall * 20 && perWall > 0; i++)
            {
                // 벽 바운딩 박스 바깥 랜덤 점에서 벽 중심 쪽으로 레이
                Vector3 p = new Vector3(
                    Random.Range(b.min.x, b.max.x),
                    Random.Range(b.min.y, b.max.y),
                    Random.Range(b.min.z, b.max.z));
                Vector3 dir = Random.value > 0.5f
                    ? (Random.value > 0.5f ? Vector3.right : Vector3.left)
                    : (Random.value > 0.5f ? Vector3.forward : Vector3.back);
                Vector3 origin = p - dir * (b.extents.magnitude + 1f);
                if (TryPlace(origin, dir, parent)) { placed++; perWall--; if (perWall <= 0) break; }
            }
        }
        Debug.Log($"덩굴 {placed}개 배치 완료");
    }

    bool TryPlace(Vector3 origin, Vector3 dir, Transform parent)
    {
        if (!Physics.Raycast(origin, dir, out RaycastHit hit, areaSize * 3f, wallMask)) return false;
        if (hit.normal.y < minNormalY || hit.normal.y > maxNormalY) return false; // 바닥/천장 제외

        // 프리팹 리스트에서 랜덤 선택
        var prefab = prefabs[Random.Range(0, prefabs.Length)];
        if (prefab == null) return false;

        var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(obj, "Vine Scatter");
        obj.transform.SetParent(parent);
        obj.transform.position = hit.point + hit.normal * surfaceOffset;
        // 프리팹의 forward가 벽 바깥을 향하도록 회전 (필요하면 축 조정)
        obj.transform.rotation = Quaternion.LookRotation(hit.normal, Vector3.up)
                                 * Quaternion.Euler(0, 0, Random.Range(-randomRoll, randomRoll));
        obj.transform.localScale *= Random.Range(minScale, maxScale);
        return true;
    }

    bool Validate()
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogWarning("덩굴 프리팹을 지정하세요.");
            return false;
        }
        foreach (var p in prefabs)
        {
            if (p == null)
            {
                Debug.LogWarning("비어 있는 프리팹 칸이 있습니다.");
                return false;
            }
        }
        return true;
    }
}
#endif