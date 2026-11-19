#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public static class VineSnapTool
{
    const string MenuPath = "Tools/Vine Snap Mode";
    const string PrefKey = "VineSnapMode";
    const string OffsetKey = "VineSnapOffset";
    const string LayerKey = "VineSnapLayerName";

    // 프리팹 방향 보정값 (X 0, Y -90, Z 0으로 확정)
    static readonly Vector3 RotFix = new Vector3(0f, -90f, 0f);

    // 드래그 중 초기 상대 배치를 유지하기 위한 캐시
    static Transform cachedAnchor;
    static Quaternion initialAnchorRot;
    static Transform[] cachedTargets;
    static Vector3[] initialOffsets;
    static Quaternion[] initialRots;

    static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, false);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    // 벽 레이어 이름 (비워두면 모든 오브젝트 대상)
    static string WallLayerName => EditorPrefs.GetString(LayerKey, "wall");
    static float Offset => EditorPrefs.GetFloat(OffsetKey, 0.02f);

    static VineSnapTool()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        Selection.selectionChanged += ClearCache;
    }

    static void ClearCache()
    {
        cachedAnchor = null;
        cachedTargets = null;
        initialOffsets = null;
        initialRots = null;
    }

    [MenuItem(MenuPath)]
    static void Toggle() => Enabled = !Enabled;

    [MenuItem(MenuPath, true)]
    static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    static void OnSceneGUI(SceneView sv)
    {
        if (!Enabled) return;

        var e = Event.current;
        // 최상위 선택 트랜스폼만 가져옴 (부모-자식 중복 이동 방지)
        var targets = Selection.transforms;
        if (targets == null || targets.Length == 0) return;

        // 안내 표시
        Handles.BeginGUI();
        GUI.Label(new Rect(10, 10, 460, 20),
            $"Vine Snap ON ({targets.Length}개 선택됨): [Shift] 누른 채 클릭/드래그하면 벽에 붙습니다");
        Handles.EndGUI();

        if (e.type == EventType.MouseUp || !e.shift)
        {
            ClearCache();
            return;
        }

        if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag) return;
        if (e.button != 0) return;

        // 기준점이 될 앵커 설정 (마지막으로 선택한 오브젝트 우선)
        Transform anchor = Selection.activeTransform;
        if (anchor == null || System.Array.IndexOf(targets, anchor) < 0)
            anchor = targets[0];

        // 드래그 시작 시점의 상대 위치/회전 스냅샷 저장
        if (e.type == EventType.MouseDown || cachedAnchor != anchor || cachedTargets == null || cachedTargets.Length != targets.Length)
        {
            cachedAnchor = anchor;
            initialAnchorRot = anchor.rotation;
            cachedTargets = targets;
            initialOffsets = new Vector3[targets.Length];
            initialRots = new Quaternion[targets.Length];

            Quaternion invAnchorRot = Quaternion.Inverse(initialAnchorRot);
            for (int i = 0; i < targets.Length; i++)
            {
                initialOffsets[i] = invAnchorRot * (targets[i].position - anchor.position);
                initialRots[i] = invAnchorRot * targets[i].rotation;
            }
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        if (TryRaycastVisibleMesh(ray, targets, out Vector3 point, out Vector3 normal))
        {
            Undo.RecordObjects(targets, "Vine Snap Multiple");

            // 1. 기준(앵커) 오브젝트 회전 및 벽 밀착 배치
            Quaternion targetAnchorRot = Quaternion.LookRotation(normal, Vector3.up) * Quaternion.Euler(RotFix);
            anchor.rotation = targetAnchorRot;
            anchor.position = point;

            float anchorBackDepth = GetMinSignedDistanceAlongNormal(anchor.gameObject, point, normal);
            Vector3 finalAnchorPos = point + normal * (Offset - anchorBackDepth);
            anchor.position = finalAnchorPos;

            // 2. 나머지 선택된 오브젝트들을 상대 위치 유지하며 배치 + 개별 표면 깊이 보정
            for (int i = 0; i < targets.Length; i++)
            {
                Transform t = targets[i];
                if (t == anchor) continue;

                t.rotation = targetAnchorRot * initialRots[i];
                Vector3 planePos = finalAnchorPos + targetAnchorRot * initialOffsets[i];

                // 각 오브젝트 위치에서 벽 쪽(-normal)으로 짧은 레이를 쏴 울퉁불퉁한 벽면에도 개별 밀착
                Ray subRay = new Ray(planePos + normal * 0.5f, -normal);
                if (TryRaycastVisibleMesh(subRay, targets, out Vector3 subPoint, out _) &&
                    Vector3.Distance(subRay.origin, subPoint) <= 1.5f)
                {
                    t.position = subPoint;
                    float subBackDepth = GetMinSignedDistanceAlongNormal(t.gameObject, subPoint, normal);
                    t.position = subPoint + normal * (Offset - subBackDepth);
                }
                else
                {
                    t.position = planePos;
                }
            }

            e.Use();
        }
    }

    // 오브젝트(자식 포함)의 바운딩 코너를 법선 방향으로 투영해 가장 뒤쪽 거리 계산
    static float GetMinSignedDistanceAlongNormal(GameObject target, Vector3 surfacePoint, Vector3 normal)
    {
        var meshFilters = target.GetComponentsInChildren<MeshFilter>();
        if (meshFilters.Length == 0) return 0f;

        float minSignedDist = float.MaxValue;
        bool hasBounds = false;

        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;

            Bounds b = mf.sharedMesh.bounds;
            Vector3 c = b.center;
            Vector3 ext = b.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 localCorner = c + new Vector3(ext.x * x, ext.y * y, ext.z * z);
                        Vector3 worldCorner = mf.transform.TransformPoint(localCorner);
                        float dist = Vector3.Dot(worldCorner - surfacePoint, normal);
                        if (dist < minSignedDist)
                        {
                            minSignedDist = dist;
                            hasBounds = true;
                        }
                    }
                }
            }
        }

        return hasBounds ? minSignedDist : 0f;
    }

    // 선택된 모든 오브젝트를 제외하고 렌더러 메시를 직접 검사
    static bool TryRaycastVisibleMesh(Ray ray, Transform[] ignoreTargets, out Vector3 point, out Vector3 normal)
    {
        point = default;
        normal = Vector3.up;
        float best = float.MaxValue;
        bool found = false;

        int wallLayer = string.IsNullOrEmpty(WallLayerName) ? -1 : LayerMask.NameToLayer(WallLayerName);

        foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var go = mr.gameObject;
            if (IsIgnored(go.transform, ignoreTargets)) continue;
            if (wallLayer >= 0 && go.layer != wallLayer) continue;

            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            if (!mr.bounds.IntersectRay(ray)) continue;

            if (RaycastMesh(ray, mf.sharedMesh, go.transform, out float dist, out Vector3 n))
            {
                if (dist < best)
                {
                    best = dist;
                    point = ray.origin + ray.direction * dist;
                    normal = n;
                    found = true;
                }
            }
        }
        return found;
    }

    static bool IsIgnored(Transform target, Transform[] ignoreTargets)
    {
        for (int i = 0; i < ignoreTargets.Length; i++)
        {
            if (target == ignoreTargets[i] || target.IsChildOf(ignoreTargets[i]))
                return true;
        }
        return false;
    }

    // 삼각형 단위 레이캐스트
    static bool RaycastMesh(Ray worldRay, Mesh mesh, Transform t, out float dist, out Vector3 worldNormal)
    {
        dist = float.MaxValue;
        worldNormal = Vector3.up;

        Ray ray = new Ray(t.InverseTransformPoint(worldRay.origin),
                          t.InverseTransformVector(worldRay.direction));

        var verts = mesh.vertices;
        var tris = mesh.triangles;
        bool hit = false;
        Vector3 localNormal = Vector3.up;

        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
            if (RayTriangle(ray, a, b, c, out float d) && d < dist)
            {
                dist = d;
                localNormal = Vector3.Cross(b - a, c - a).normalized;
                hit = true;
            }
        }
        if (!hit) return false;

        worldNormal = t.localToWorldMatrix.inverse.transpose.MultiplyVector(localNormal).normalized;
        if (Vector3.Dot(worldNormal, worldRay.direction) > 0) worldNormal = -worldNormal;

        Vector3 localPoint = ray.origin + ray.direction * dist;
        dist = Vector3.Distance(worldRay.origin, t.TransformPoint(localPoint));
        return true;
    }

    // Möller–Trumbore
    static bool RayTriangle(Ray r, Vector3 a, Vector3 b, Vector3 c, out float t)
    {
        t = 0;
        Vector3 e1 = b - a, e2 = c - a;
        Vector3 p = Vector3.Cross(r.direction, e2);
        float det = Vector3.Dot(e1, p);
        if (Mathf.Abs(det) < 1e-8f) return false;
        float inv = 1f / det;
        Vector3 s = r.origin - a;
        float u = Vector3.Dot(s, p) * inv;
        if (u < 0 || u > 1) return false;
        Vector3 q = Vector3.Cross(s, e1);
        float v = Vector3.Dot(r.direction, q) * inv;
        if (v < 0 || u + v > 1) return false;
        t = Vector3.Dot(e2, q) * inv;
        return t > 0;
    }
}
#endif