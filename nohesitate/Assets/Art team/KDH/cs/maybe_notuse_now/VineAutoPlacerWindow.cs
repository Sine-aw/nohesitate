#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class VineAutoPlacerWindow : EditorWindow
{
    enum PlacementMode
    {
        TopEdge,    // 벽 최상단 모서리 (덩굴용)
        BottomEdge  // 벽 최하단 바닥 모서리 (바닥 풀용)
    }

    [SerializeField] List<GameObject> vinePrefabs = new List<GameObject>();
    string wallLayerName = "wall";
    bool selectedWallsOnly = false;
    PlacementMode mode = PlacementMode.BottomEdge;
    bool rimOnly = true;
    bool interiorCorridorsOnly = true; // 미로 내부 통로만 배치 (외벽 바깥쪽 제외)

    // 상단/하단 회전 보정값을 분리하여 모드 전환 시 값이 꼬이지 않도록 고정
    Vector3 topRotFix = new Vector3(0f, -90f, 0f);
    Vector3 bottomRotFix = new Vector3(180f, -90f, 0f);

    float spacing = 1.0f;           // 평균 간격 (m)
    float density = 0.75f;          // 배치 밀도 (0.05 ~ 1.0)
    int maxCount = 0;               // 최대 생성 개수 (0이면 제한 없음)
    float wallOffset = 0.02f;       // 벽 표면으로부터 띄울 거리
    float verticalOffset = 0.0f;    // 상하 높이 미세 보정
    Vector2 randomScaleRange = new Vector2(0.85f, 1.15f);
    int randomSeed = 42;

    const string TopContainerName = "Generated_Vines_Top";
    const string BottomContainerName = "Generated_Vines_Bottom";

    struct CandidateSpot
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector3 edgeDir;
    }

    [MenuItem("Tools/Vine Auto Placer")]
    public static void ShowWindow()
    {
        GetWindow<VineAutoPlacerWindow>("Vine Auto Placer");
    }

    void OnGUI()
    {
        GUILayout.Label("덩굴 / 바닥 풀 일괄 자동 배치 도구", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("배치할 프리팹 (pul1~3 등 등록)", EditorStyles.miniBoldLabel);
        int removeIdx = -1;
        for (int i = 0; i < vinePrefabs.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            vinePrefabs[i] = (GameObject)EditorGUILayout.ObjectField(vinePrefabs[i], typeof(GameObject), false);
            if (GUILayout.Button("X", GUILayout.Width(24))) removeIdx = i;
            EditorGUILayout.EndHorizontal();
        }
        if (removeIdx >= 0) vinePrefabs.RemoveAt(removeIdx);

        if (GUILayout.Button("+ 프리팹 슬롯 추가"))
        {
            vinePrefabs.Add(null);
        }

        EditorGUILayout.Space(8);
        GUILayout.Label("배치 위치 및 방향 설정", EditorStyles.boldLabel);

        wallLayerName = EditorGUILayout.TextField("벽 레이어 이름", wallLayerName);
        selectedWallsOnly = EditorGUILayout.Toggle("선택한 벽 오브젝트만 대상", selectedWallsOnly);
        mode = (PlacementMode)EditorGUILayout.EnumPopup("배치 위치 기준", mode);

        if (mode == PlacementMode.TopEdge)
        {
            rimOnly = EditorGUILayout.Toggle("벽 최상단 테두리만 (중간 벽돌 제외)", rimOnly);
            topRotFix = EditorGUILayout.Vector3Field("상단 회전 보정 (X, Y, Z)", topRotFix);
        }
        else
        {
            rimOnly = EditorGUILayout.Toggle("바닥과 맞닿은 맨 아랫줄만 배치", rimOnly);
            interiorCorridorsOnly = EditorGUILayout.Toggle("미로 내부 통로만 (외벽 바깥 제외)", interiorCorridorsOnly);
            bottomRotFix = EditorGUILayout.Vector3Field("바닥 회전 보정 (X, Y, Z)", bottomRotFix);
        }

        EditorGUILayout.Space(6);
        GUILayout.Label("밀도 및 간격 설정", EditorStyles.boldLabel);

        spacing = EditorGUILayout.Slider("배치 간격 (m)", spacing, 0.3f, 10.0f);
        density = EditorGUILayout.Slider("배치 밀도 (확률)", density, 0.05f, 1.0f);
        maxCount = EditorGUILayout.IntSlider("최대 생성 개수 (0 = 무제한)", maxCount, 0, 3000);
        wallOffset = EditorGUILayout.FloatField("벽 표면 오프셋", wallOffset);
        verticalOffset = EditorGUILayout.FloatField("상하 높이 오프셋", verticalOffset);
        randomScaleRange = EditorGUILayout.Vector2Field("랜덤 스케일 (최소 / 최대)", randomScaleRange);

        EditorGUILayout.BeginHorizontal();
        randomSeed = EditorGUILayout.IntField("랜덤 시드", randomSeed);
        if (GUILayout.Button("시드 변경", GUILayout.Width(80)))
        {
            randomSeed = Random.Range(1, 99999);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(12);

        string actionText = (mode == PlacementMode.TopEdge)
            ? "벽 상단(Top)에 일괄 배치"
            : "벽 하단 바닥(Bottom)에 일괄 배치";

        GUI.backgroundColor = new Color(0.45f, 0.85f, 0.55f);
        if (GUILayout.Button(actionText, GUILayout.Height(36)))
        {
            Generate();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("상단 배치 삭제", GUILayout.Height(24)))
        {
            ClearContainer(TopContainerName);
        }
        if (GUILayout.Button("바닥 배치 삭제", GUILayout.Height(24)))
        {
            ClearContainer(BottomContainerName);
        }
        EditorGUILayout.EndHorizontal();
    }

    void Generate()
    {
        var validPrefabs = vinePrefabs.FindAll(p => p != null);
        if (validPrefabs.Count == 0)
        {
            EditorUtility.DisplayDialog("알림", "배치할 프리팹을 최소 1개 이상 등록해주세요.", "확인");
            return;
        }

        string containerName = (mode == PlacementMode.TopEdge) ? TopContainerName : BottomContainerName;
        Vector3 activeRotFix = (mode == PlacementMode.TopEdge) ? topRotFix : bottomRotFix;
        ClearContainer(containerName);

        CollectMeshes(out var wallMeshes, out var allSceneMeshes, out float globalMinWallY);
        if (wallMeshes.Count == 0)
        {
            EditorUtility.DisplayDialog("알림", $"대상 벽을 찾지 못했습니다. 레이어('{wallLayerName}') 또는 선택 상태를 확인해주세요.", "확인");
            return;
        }

        Random.InitState(randomSeed);

        List<CandidateSpot> candidates = new List<CandidateSpot>();
        float sampleStep = Mathf.Max(0.3f, spacing * 0.85f);

        foreach (var wall in wallMeshes)
        {
            Mesh mesh = wall.mesh;
            Transform t = wall.transform;
            var verts = mesh.vertices;
            var tris = mesh.triangles;

            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 wA = t.TransformPoint(verts[tris[i]]);
                Vector3 wB = t.TransformPoint(verts[tris[i + 1]]);
                Vector3 wC = t.TransformPoint(verts[tris[i + 2]]);

                Vector3 geomNormal = Vector3.Cross(wB - wA, wC - wA).normalized;

                // 수직 벽면만 대상
                if (Mathf.Abs(geomNormal.y) > 0.08f) continue;

                CollectEdgeCandidates(wA, wB, wC, geomNormal, wallMeshes, allSceneMeshes, globalMinWallY, sampleStep, candidates);
                CollectEdgeCandidates(wB, wC, wA, geomNormal, wallMeshes, allSceneMeshes, globalMinWallY, sampleStep, candidates);
                CollectEdgeCandidates(wC, wA, wB, geomNormal, wallMeshes, allSceneMeshes, globalMinWallY, sampleStep, candidates);
            }
        }

        if (maxCount > 0 && candidates.Count > maxCount)
        {
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = tmp;
            }
        }

        GameObject container = new GameObject(containerName);
        Undo.RegisterCreatedObjectUndo(container, "Auto Place Foliage");

        List<Vector3> placedPositions = new List<Vector3>();
        float minDistance = spacing * 0.7f;
        int placedCount = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            if (maxCount > 0 && placedCount >= maxCount) break;
            if (Random.value > density) continue;

            var spot = candidates[i];
            if (IsTooClose(spot.position, placedPositions, minDistance)) continue;

            GameObject chosenPrefab = validPrefabs[Random.Range(0, validPrefabs.Count)];
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(chosenPrefab, container.transform);
            if (instance == null) instance = Instantiate(chosenPrefab, container.transform);

            float randScale = Random.Range(randomScaleRange.x, randomScaleRange.y);
            instance.transform.localScale = chosenPrefab.transform.localScale * randScale;

            Quaternion rot = Quaternion.LookRotation(spot.normal, Vector3.up) * Quaternion.Euler(activeRotFix);
            instance.transform.rotation = rot;
            instance.transform.position = spot.position;

            GetBoundsMetrics(instance, spot.position, spot.normal, spot.edgeDir,
                out float backDepth, out float minRelY, out float maxRelY, out float halfWidth);

            // 프리팹의 실제 좌우 끝단까지 벽이 이어져 있는지 검증 (문 입구 허공 돌출 방지)
            float checkHeight = (mode == PlacementMode.TopEdge) ? -0.15f : 0.25f;
            float checkHalfW = Mathf.Max(0.12f, halfWidth * 0.85f);
            Vector3 leftEnd = spot.position - spot.edgeDir * checkHalfW + Vector3.up * checkHeight - spot.normal * 0.05f;
            Vector3 rightEnd = spot.position + spot.edgeDir * checkHalfW + Vector3.up * checkHeight - spot.normal * 0.05f;

            if (!IsInsideWallThickness(leftEnd, spot.normal, wallMeshes) ||
                !IsInsideWallThickness(rightEnd, spot.normal, wallMeshes))
            {
                DestroyImmediate(instance);
                continue;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Auto Place Foliage");

            Vector3 finalPos = spot.position + spot.normal * (wallOffset - backDepth);
            if (mode == PlacementMode.TopEdge)
                finalPos.y += (verticalOffset - maxRelY);
            else
                finalPos.y += (verticalOffset - minRelY);

            instance.transform.position = finalPos;

            placedPositions.Add(spot.position);
            placedCount++;
        }

        Selection.activeGameObject = container;
        Debug.Log($"[Vine Auto Placer] ({mode}) 총 {placedCount}개가 배치되었습니다.");
    }

    void CollectEdgeCandidates(
        Vector3 p1, Vector3 p2, Vector3 oppositeVert, Vector3 geomNormal,
        List<MeshData> wallMeshes, List<MeshData> allSceneMeshes,
        float globalMinWallY, float step, List<CandidateSpot> candidates)
    {
        if (Mathf.Abs(p1.y - p2.y) > 0.03f) return;

        if (mode == PlacementMode.TopEdge && p1.y <= oppositeVert.y + 0.03f) return;
        if (mode == PlacementMode.BottomEdge && p1.y >= oppositeVert.y - 0.03f) return;

        if (mode == PlacementMode.BottomEdge && rimOnly && p1.y > globalMinWallY + 0.4f) return;

        Vector3 edgeVec = new Vector3(p2.x - p1.x, 0f, p2.z - p1.z);
        float edgeLength = edgeVec.magnitude;
        if (edgeLength < 0.25f) return;

        Vector3 edgeDir = edgeVec / edgeLength;
        Vector3 perp = new Vector3(-edgeDir.z, 0f, edgeDir.x);
        Vector3 outwardNormal = Vector3.Dot(perp, geomNormal) >= 0f ? perp : -perp;

        float distAlong = Random.Range(0.15f, Mathf.Min(step * 0.6f, edgeLength * 0.5f));

        while (distAlong <= edgeLength - 0.15f)
        {
            Vector3 edgePoint = p1 + edgeDir * distAlong;
            distAlong += step * Random.Range(0.8f, 1.2f);

            if (mode == PlacementMode.TopEdge)
            {
                Vector3 wallBodyProbe = edgePoint + Vector3.down * 0.15f;

                if (RaycastMeshes(new Ray(wallBodyProbe + outwardNormal * 0.03f, outwardNormal), 0.45f, wallMeshes, out _, out _)) continue;
                if (!RaycastMeshes(new Ray(wallBodyProbe - outwardNormal * 0.02f, -outwardNormal), 0.8f, wallMeshes, out _, out _)) continue;

                if (rimOnly && !IsTrueTopRim(edgePoint, outwardNormal, wallMeshes)) continue;

                candidates.Add(new CandidateSpot { position = edgePoint, normal = outwardNormal, edgeDir = edgeDir });
            }
            else
            {
                // 통로 앞쪽(35cm)에서 바닥 타일 윗면 높이를 탐지 (외벽 바깥 허공 자동 제외)
                float forwardCheckDist = interiorCorridorsOnly ? 0.35f : 0.12f;
                Vector3 floorProbeOrigin = edgePoint + outwardNormal * forwardCheckDist + Vector3.up * 0.45f;
                bool hitFloor = RaycastMeshes(
                    new Ray(floorProbeOrigin, Vector3.down),
                    0.9f,
                    allSceneMeshes,
                    out Vector3 floorPoint,
                    out Vector3 floorNormal);

                if (!hitFloor || floorNormal.y < 0.8f) continue;

                if (rimOnly)
                {
                    if (floorPoint.y > globalMinWallY + 0.4f) continue;
                    // 바닥 바로 위 첫 번째 벽돌 모서리(12cm 이내)만 허용해 2~3번째 줄 중복 방지
                    if (Mathf.Abs(edgePoint.y - floorPoint.y) > 0.12f) continue;
                }

                Vector3 groundEdgePoint = new Vector3(edgePoint.x, floorPoint.y, edgePoint.z);
                Vector3 wallBodyProbe = groundEdgePoint + Vector3.up * 0.25f;

                if (RaycastMeshes(new Ray(wallBodyProbe + outwardNormal * 0.03f, outwardNormal), 0.4f, wallMeshes, out _, out _)) continue;
                if (!RaycastMeshes(new Ray(wallBodyProbe - outwardNormal * 0.02f, -outwardNormal), 0.8f, wallMeshes, out _, out _)) continue;

                candidates.Add(new CandidateSpot { position = groundEdgePoint, normal = outwardNormal, edgeDir = edgeDir });
            }
        }
    }

    static bool IsInsideWallThickness(Vector3 pointInside, Vector3 outwardNormal, List<MeshData> wallMeshes)
    {
        return RaycastMeshes(new Ray(pointInside, outwardNormal), 0.15f, wallMeshes, out _, out _) &&
               RaycastMeshes(new Ray(pointInside, -outwardNormal), 0.8f, wallMeshes, out _, out _);
    }

    static bool IsTrueTopRim(Vector3 edgePoint, Vector3 outwardNormal, List<MeshData> wallMeshes)
    {
        Vector3 upOriginInside = edgePoint + Vector3.up * 0.04f - outwardNormal * 0.06f;
        Vector3 upOriginOutside = edgePoint + Vector3.up * 0.04f + outwardNormal * 0.05f;

        if (RaycastMeshes(new Ray(upOriginInside, Vector3.up), 100f, wallMeshes, out _, out _)) return false;
        if (RaycastMeshes(new Ray(upOriginOutside, Vector3.up), 100f, wallMeshes, out _, out _)) return false;

        Vector3 capCheckOrigin = edgePoint + Vector3.up * 0.15f - outwardNormal * 0.06f;
        if (!RaycastMeshes(new Ray(capCheckOrigin, Vector3.down), 0.25f, wallMeshes, out _, out _)) return false;

        return true;
    }

    static bool RaycastMeshes(Ray ray, float maxDist, List<MeshData> meshes, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = default;
        hitNormal = Vector3.up;
        float best = maxDist;
        bool found = false;

        Vector3 endPoint = ray.origin + ray.direction * maxDist;
        for (int i = 0; i < meshes.Count; i++)
        {
            var m = meshes[i];
            if (!m.bounds.Contains(ray.origin) && !m.bounds.Contains(endPoint) && !m.bounds.IntersectRay(ray))
                continue;

            Ray localRay = new Ray(m.transform.InverseTransformPoint(ray.origin), m.transform.InverseTransformVector(ray.direction));
            var verts = m.mesh.vertices;
            var tris = m.mesh.triangles;

            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                if (RayTriangle(localRay, a, b, c, out float d))
                {
                    Vector3 worldHit = m.transform.TransformPoint(localRay.origin + localRay.direction * d);
                    float dist = Vector3.Distance(ray.origin, worldHit);
                    if (dist <= best)
                    {
                        best = dist;
                        hitPoint = worldHit;
                        Vector3 localN = Vector3.Cross(b - a, c - a).normalized;
                        hitNormal = m.invTranspose.MultiplyVector(localN).normalized;
                        if (Vector3.Dot(hitNormal, ray.direction) > 0f) hitNormal = -hitNormal;
                        found = true;
                    }
                }
            }
        }
        return found;
    }

    static bool IsTooClose(Vector3 candidate, List<Vector3> existing, float minDist)
    {
        float sqrMin = minDist * minDist;
        for (int i = 0; i < existing.Count; i++)
        {
            if ((existing[i] - candidate).sqrMagnitude < sqrMin) return true;
        }
        return false;
    }

    static void GetBoundsMetrics(
        GameObject target, Vector3 pivotWorld, Vector3 normal, Vector3 edgeDir,
        out float minSignedDepth, out float minRelY, out float maxRelY, out float halfWidth)
    {
        minSignedDepth = float.MaxValue;
        minRelY = float.MaxValue;
        maxRelY = float.MinValue;
        halfWidth = 0f;
        bool found = false;

        foreach (var mf in target.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Bounds b = mf.sharedMesh.bounds;
            Vector3 c = b.center, ext = b.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 worldCorner = mf.transform.TransformPoint(c + new Vector3(ext.x * x, ext.y * y, ext.z * z));
                        Vector3 delta = worldCorner - pivotWorld;

                        float depth = Vector3.Dot(delta, normal);
                        float relY = delta.y;
                        float along = Mathf.Abs(Vector3.Dot(delta, edgeDir));

                        if (depth < minSignedDepth) minSignedDepth = depth;
                        if (relY < minRelY) minRelY = relY;
                        if (relY > maxRelY) maxRelY = relY;
                        if (along > halfWidth) halfWidth = along;
                        found = true;
                    }
                }
            }
        }

        if (!found)
        {
            minSignedDepth = 0f;
            minRelY = 0f;
            maxRelY = 0f;
            halfWidth = 0f;
        }
    }

    struct MeshData
    {
        public Transform transform;
        public Mesh mesh;
        public Bounds bounds;
        public Matrix4x4 invTranspose;
    }

    void CollectMeshes(out List<MeshData> wallMeshes, out List<MeshData> allSceneMeshes, out float minWallY)
    {
        wallMeshes = new List<MeshData>();
        allSceneMeshes = new List<MeshData>();
        minWallY = float.MaxValue;

        int wallLayer = string.IsNullOrEmpty(wallLayerName) ? -1 : LayerMask.NameToLayer(wallLayerName);

        HashSet<MeshRenderer> selectedSet = null;
        if (selectedWallsOnly && Selection.gameObjects.Length > 0)
        {
            selectedSet = new HashSet<MeshRenderer>();
            foreach (var go in Selection.gameObjects)
            {
                foreach (var mr in go.GetComponentsInChildren<MeshRenderer>())
                    selectedSet.Add(mr);
            }
        }

        var topContainer = GameObject.Find(TopContainerName);
        var bottomContainer = GameObject.Find(BottomContainerName);

        foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (topContainer != null && mr.transform.IsChildOf(topContainer.transform)) continue;
            if (bottomContainer != null && mr.transform.IsChildOf(bottomContainer.transform)) continue;

            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            var data = new MeshData
            {
                transform = mr.transform,
                mesh = mf.sharedMesh,
                bounds = mr.bounds,
                invTranspose = mr.transform.localToWorldMatrix.inverse.transpose
            };

            allSceneMeshes.Add(data);

            if (selectedSet != null && !selectedSet.Contains(mr)) continue;
            if (wallLayer >= 0 && mr.gameObject.layer != wallLayer) continue;

            wallMeshes.Add(data);
            if (mr.bounds.min.y < minWallY) minWallY = mr.bounds.min.y;
        }

        if (wallMeshes.Count == 0)
        {
            minWallY = 0f;
        }
    }

    static void ClearContainer(string name)
    {
        var existing = GameObject.Find(name);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }
    }

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