#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public class CirclePillarPlacerWindow : EditorWindow
{
    enum PlacementSide
    {
        InnerWall,  // 벽 안쪽 면에 파묻힘 (사진 속 방식)
        OuterWall,  // 벽 바깥쪽 면에 파묻힘
        WallCenter  // 벽 두께 정중앙에 배치
    }

    GameObject circleWallObj;    // 원형 벽 (또는 콜로세움 최상위 오브젝트)
    GameObject pillarSourceObj;  // 기둥 오브젝트 (씬의 샘플 기둥 또는 프리팹)

    Vector3 circleCenter;
    float innerWallRadius = 20f;
    float outerWallRadius = 21f;
    float floorY = 0f;

    PlacementSide placementSide = PlacementSide.InnerWall;
    float embedPercent = 40f;    // 파묻힘 정도 (0% = 벽 표면에 딱 닿음, 50% = 기둥 절반이 벽에 파묻힘)
    float radiusOffset = 0f;     // 반지름 미세 보정 (m)
    float heightOffset = 0f;     // 바닥 높이 미세 보정 (m)

    int pillarCount = 24;        // 배치할 기둥 총 개수
    float desiredSpacing = 6.0f; // 간격(m) 기준 자동 계산용
    float startAngle = 0f;       // 시작 각도 (기둥 위치 회전 정렬)
    float yawFix = 0f;           // 기둥 자체 회전 보정 (Y각도)

    int doorGapCount = 0;        // 출입구로 비워둘 칸 수
    float doorStartAngle = 0f;

    bool hideSourceSceneObject = true; // 씬의 샘플 기둥을 등록했을 때 중복 방지를 위해 숨김

    const string ContainerName = "Generated_Circle_Pillars";
    const string OptimizedName = "Optimized_Circle_Pillars";

    [MenuItem("Tools/Circle Pillar Placer")]
    public static void ShowWindow()
    {
        GetWindow<CirclePillarPlacerWindow>("Circle Pillar Placer");
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    // 씬 뷰에서 각 기둥이 배치될 위치와 벽에 파묻히는 정도를 초록색 원으로 미리보기
    void OnSceneGUI(SceneView sv)
    {
        if (circleWallObj == null) return;

        float targetR = CalculateTargetRadius(out float pillarR);
        float y = floorY + heightOffset;
        Vector3 center = new Vector3(circleCenter.x, y, circleCenter.z);

        Handles.color = new Color(0.25f, 1f, 0.55f, 0.9f);
        Handles.DrawWireDisc(center, Vector3.up, targetR);

        int count = Mathf.Max(3, pillarCount);
        float angleStep = 360f / count;
        float previewR = Mathf.Max(0.15f, pillarR);

        for (int i = 0; i < count; i++)
        {
            float deg = startAngle + i * angleStep;
            if (IsInDoorGap(deg, angleStep)) continue;

            float rad = deg * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            Vector3 p = center + dir * targetR;

            Handles.DrawWireDisc(p, Vector3.up, previewR);
            Handles.DrawLine(p, p + Vector3.up * 2.5f);
        }
    }

    void OnGUI()
    {
        GUILayout.Label("원형 벽 기둥(Pillar) 일정 간격 배치 도구", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // 1. 대상 등록
        EditorGUI.BeginChangeCheck();
        circleWallObj = (GameObject)EditorGUILayout.ObjectField("원형 벽 오브젝트", circleWallObj, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck() && circleWallObj != null)
        {
            AnalyzeWallGeometry();
            SceneView.RepaintAll();
        }

        EditorGUI.BeginChangeCheck();
        pillarSourceObj = (GameObject)EditorGUILayout.ObjectField("기둥 오브젝트 / 프리팹", pillarSourceObj, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck() && pillarSourceObj != null)
        {
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
        if (GUILayout.Button("원형 벽 크기 자동 분석", GUILayout.Height(26)))
        {
            AnalyzeWallGeometry();
            SceneView.RepaintAll();
        }

        // 이미 씬에 샘플 기둥을 하나 배치해 둔 경우 그 파묻힘 깊이와 각도를 그대로 가져옴
        if (pillarSourceObj != null && !PrefabUtility.IsPartOfPrefabAsset(pillarSourceObj))
        {
            if (GUILayout.Button("샘플 기둥 깊이·각도 가져오기", GUILayout.Height(26)))
            {
                SyncFromSamplePillar();
                SceneView.RepaintAll();
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        GUILayout.Label("벽 파묻힘 깊이 및 위치 설정", EditorStyles.boldLabel);

        placementSide = (PlacementSide)EditorGUILayout.EnumPopup("배치 방향", placementSide);

        if (placementSide != PlacementSide.WallCenter)
        {
            embedPercent = EditorGUILayout.Slider("벽에 파묻히는 비율 (%)", embedPercent, 0f, 100f);
        }

        radiusOffset = EditorGUILayout.FloatField("반지름 미세 오프셋 (m)", radiusOffset);
        heightOffset = EditorGUILayout.FloatField("바닥 높이 미세 오프셋 (Y)", heightOffset);

        EditorGUILayout.Space(8);
        GUILayout.Label("간격 및 개수 설정", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        desiredSpacing = EditorGUILayout.FloatField("목표 기둥 간격 (m)", desiredSpacing);
        if (GUILayout.Button("간격으로 개수 계산", GUILayout.Width(125)))
        {
            float r = CalculateTargetRadius(out _);
            float circ = 2f * Mathf.PI * Mathf.Max(1f, r);
            pillarCount = Mathf.Clamp(Mathf.RoundToInt(circ / Mathf.Max(0.5f, desiredSpacing)), 3, 200);
        }
        EditorGUILayout.EndHorizontal();

        pillarCount = EditorGUILayout.IntSlider("기둥 총 개수", pillarCount, 3, 200);
        startAngle = EditorGUILayout.Slider("시작 각도 (전체 회전)", startAngle, 0f, 360f);
        yawFix = EditorGUILayout.Slider("기둥 자체 회전 (Y각도)", yawFix, -180f, 180f);

        doorGapCount = EditorGUILayout.IntSlider("출입구 비우기 (칸 수)", doorGapCount, 0, Mathf.Max(0, pillarCount - 3));
        if (doorGapCount > 0)
        {
            doorStartAngle = EditorGUILayout.Slider("출입구 방향 각도", doorStartAngle, 0f, 360f);
        }

        if (pillarSourceObj != null && !PrefabUtility.IsPartOfPrefabAsset(pillarSourceObj))
        {
            hideSourceSceneObject = EditorGUILayout.Toggle("원본 샘플 기둥 숨기기 (중복 방지)", hideSourceSceneObject);
        }

        EditorGUILayout.Space(12);

        GUI.backgroundColor = new Color(0.45f, 0.85f, 0.55f);
        if (GUILayout.Button("1. 원형 벽 따라 기둥 일괄 배치", GUILayout.Height(36)))
        {
            GeneratePillars();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(1f, 0.82f, 0.35f);
        if (GUILayout.Button("2. 기둥 메시 하나로 병합 (최적화)", GUILayout.Height(28)))
        {
            MergePillarsForPerformance();
        }
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("생성된 기둥 삭제", GUILayout.Height(28), GUILayout.Width(120)))
        {
            ClearContainer(ContainerName);
            ClearContainer(OptimizedName);
        }
        EditorGUILayout.EndHorizontal();

        if (GUI.changed)
        {
            SceneView.RepaintAll();
        }
    }

    // 원형 벽 메쉬의 중간 높이 버텍스를 스캔해 덩굴을 제외한 실제 돌벽 내경/외경 자동 추출
    void AnalyzeWallGeometry()
    {
        if (circleWallObj == null) return;
        if (!TryGetCombinedBounds(circleWallObj, out Bounds totalBounds)) return;

        circleCenter = new Vector3(totalBounds.center.x, totalBounds.center.y, totalBounds.center.z);
        floorY = totalBounds.min.y;

        float minY = totalBounds.min.y;
        float maxY = totalBounds.max.y;
        float wallH = maxY - minY;
        float bandLow = minY + wallH * 0.25f;
        float bandHigh = minY + wallH * 0.75f;

        float minR = float.MaxValue;
        float maxR = float.MinValue;
        bool foundVerts = false;

        foreach (var mf in circleWallObj.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var verts = mf.sharedMesh.vertices;
            Matrix4x4 l2w = mf.transform.localToWorldMatrix;

            // 버텍스가 매우 많을 경우 일정 간격으로 샘플링해 빠르게 측정
            int step = Mathf.Max(1, verts.Length / 4000);
            for (int i = 0; i < verts.Length; i += step)
            {
                Vector3 wp = l2w.MultiplyPoint3x4(verts[i]);
                if (wp.y < bandLow || wp.y > bandHigh) continue;

                float dx = wp.x - circleCenter.x;
                float dz = wp.z - circleCenter.z;
                float r = Mathf.Sqrt(dx * dx + dz * dz);

                if (r < minR) minR = r;
                if (r > maxR) maxR = r;
                foundVerts = true;
            }
        }

        if (foundVerts && maxR > minR)
        {
            innerWallRadius = minR;
            outerWallRadius = maxR;
        }
        else
        {
            outerWallRadius = (totalBounds.extents.x + totalBounds.extents.z) * 0.5f;
            innerWallRadius = Mathf.Max(0.5f, outerWallRadius - 1.0f);
        }
    }

    // 씬에 이미 놓아둔 샘플 기둥의 현재 위치로부터 파묻힘 비율과 시작 각도를 역산
    void SyncFromSamplePillar()
    {
        if (circleWallObj == null || pillarSourceObj == null) return;
        AnalyzeWallGeometry();

        if (!TryGetCombinedBounds(pillarSourceObj, out Bounds pb)) return;

        float dx = pb.center.x - circleCenter.x;
        float dz = pb.center.z - circleCenter.z;
        float currentDist = Mathf.Sqrt(dx * dx + dz * dz);

        startAngle = Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;
        if (startAngle < 0f) startAngle += 360f;

        heightOffset = pb.min.y - floorY;

        float pillarR = Mathf.Max(pb.extents.x, pb.extents.z);
        float wallMidR = (innerWallRadius + outerWallRadius) * 0.5f;

        if (currentDist <= wallMidR)
        {
            placementSide = PlacementSide.InnerWall;
            float touchR = innerWallRadius - pillarR;
            embedPercent = Mathf.Clamp(((currentDist - touchR) / Mathf.Max(0.01f, pillarR * 2f)) * 100f, 0f, 100f);
            radiusOffset = 0f;
        }
        else
        {
            placementSide = PlacementSide.OuterWall;
            float touchR = outerWallRadius + pillarR;
            embedPercent = Mathf.Clamp(((touchR - currentDist) / Mathf.Max(0.01f, pillarR * 2f)) * 100f, 0f, 100f);
            radiusOffset = 0f;
        }
    }

    float CalculateTargetRadius(out float pillarRadius)
    {
        pillarRadius = 0.5f;
        if (pillarSourceObj != null && TryGetCombinedBounds(pillarSourceObj, out Bounds pb))
        {
            pillarRadius = Mathf.Max(pb.extents.x, pb.extents.z);
        }

        float baseR;
        float embedDepth = (pillarRadius * 2f) * (embedPercent * 0.01f);

        switch (placementSide)
        {
            case PlacementSide.InnerWall:
                baseR = (innerWallRadius - pillarRadius) + embedDepth;
                break;
            case PlacementSide.OuterWall:
                baseR = (outerWallRadius + pillarRadius) - embedDepth;
                break;
            default:
                baseR = (innerWallRadius + outerWallRadius) * 0.5f;
                break;
        }

        return baseR + radiusOffset;
    }

    bool IsInDoorGap(float currentDeg, float angleStep)
    {
        if (doorGapCount <= 0) return false;
        float diff = Mathf.DeltaAngle(doorStartAngle, currentDeg);
        return diff >= 0f && diff < doorGapCount * angleStep;
    }

    void GeneratePillars()
    {
        if (circleWallObj == null || pillarSourceObj == null)
        {
            EditorUtility.DisplayDialog("알림", "'원형 벽 오브젝트'와 '기둥 오브젝트'를 모두 등록해주세요.", "확인");
            return;
        }

        ClearContainer(ContainerName);

        GameObject container = new GameObject(ContainerName);
        container.transform.position = new Vector3(circleCenter.x, floorY, circleCenter.z);
        Undo.RegisterCreatedObjectUndo(container, "Generate Circle Pillars");

        float targetRadius = CalculateTargetRadius(out _);
        float targetY = floorY + heightOffset;
        float angleStep = 360f / pillarCount;
        bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(pillarSourceObj);

        for (int i = 0; i < pillarCount; i++)
        {
            float deg = startAngle + i * angleStep;
            if (IsInDoorGap(deg, angleStep)) continue;

            float rad = deg * Mathf.Deg2Rad;
            Vector3 outwardDir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            Vector3 ringPos = new Vector3(
                circleCenter.x + outwardDir.x * targetRadius,
                targetY,
                circleCenter.z + outwardDir.z * targetRadius
            );

            GameObject instance;
            if (isPrefabAsset)
                instance = (GameObject)PrefabUtility.InstantiatePrefab(pillarSourceObj, container.transform);
            else
                instance = Instantiate(pillarSourceObj, container.transform);

            instance.SetActive(true);
            instance.name = $"{pillarSourceObj.name}_Ring_{i:D2}";
            Undo.RegisterCreatedObjectUndo(instance, "Generate Circle Pillars");

            // 원 중심을 향하도록 정렬 + Y축 미세 회전
            instance.transform.rotation = Quaternion.LookRotation(-outwardDir, Vector3.up) * Quaternion.Euler(0f, yawFix, 0f);
            instance.transform.position = ringPos;

            // 기둥 메시의 중심을 원 반지름에 맞추고, 기둥 바닥을 지면에 밀착
            if (TryGetCombinedBounds(instance, out Bounds instBounds))
            {
                Vector3 centerOffsetXZ = new Vector3(
                    ringPos.x - instBounds.center.x,
                    0f,
                    ringPos.z - instBounds.center.z
                );
                float bottomOffsetY = targetY - instBounds.min.y;
                instance.transform.position += centerOffsetXZ + new Vector3(0f, bottomOffsetY, 0f);
            }
        }

        if (!isPrefabAsset && hideSourceSceneObject && pillarSourceObj.activeSelf)
        {
            Undo.RecordObject(pillarSourceObj, "Hide Sample Pillar");
            pillarSourceObj.SetActive(false);
        }

        Selection.activeGameObject = container;
        Debug.Log($"[Circle Pillar Placer] 원형 벽을 따라 총 {container.transform.childCount}개의 기둥이 배치되었습니다.");
    }

    // 배치된 기둥들을 하나의 메시 에셋으로 병합하여 프레임 드랍 방지
    void MergePillarsForPerformance()
    {
        GameObject target = GameObject.Find(ContainerName);
        if (target == null)
        {
            EditorUtility.DisplayDialog("알림", "먼저 기둥을 일괄 배치해주세요.", "확인");
            return;
        }

        var meshFilters = target.GetComponentsInChildren<MeshFilter>();
        if (meshFilters.Length == 0) return;

        var matToCombines = new Dictionary<Material, List<CombineInstance>>();
        Matrix4x4 rootW2L = target.transform.worldToLocalMatrix;

        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || !mr.enabled) continue;

            var mats = mr.sharedMaterials;
            int subCount = Mathf.Min(mf.sharedMesh.subMeshCount, mats.Length);

            for (int s = 0; s < subCount; s++)
            {
                Material mat = mats[s];
                if (mat == null) continue;
                if (!mat.enableInstancing) mat.enableInstancing = true;

                if (!matToCombines.TryGetValue(mat, out var list))
                {
                    list = new List<CombineInstance>();
                    matToCombines[mat] = list;
                }

                list.Add(new CombineInstance
                {
                    mesh = mf.sharedMesh,
                    subMeshIndex = s,
                    transform = rootW2L * mf.transform.localToWorldMatrix
                });
            }
        }

        var subMeshes = new List<CombineInstance>();
        var finalMats = new List<Material>();

        foreach (var kvp in matToCombines)
        {
            Mesh sub = new Mesh { indexFormat = IndexFormat.UInt32 };
            sub.CombineMeshes(kvp.Value.ToArray(), true, true);
            subMeshes.Add(new CombineInstance { mesh = sub, subMeshIndex = 0, transform = Matrix4x4.identity });
            finalMats.Add(kvp.Key);
        }

        Mesh finalMesh = new Mesh { name = OptimizedName + "_Mesh", indexFormat = IndexFormat.UInt32 };
        finalMesh.CombineMeshes(subMeshes.ToArray(), false, false);
        finalMesh.RecalculateBounds();
        MeshUtility.Optimize(finalMesh);

        for (int i = 0; i < subMeshes.Count; i++) DestroyImmediate(subMeshes[i].mesh);

        if (!AssetDatabase.IsValidFolder("Assets/GeneratedMeshes"))
            AssetDatabase.CreateFolder("Assets", "GeneratedMeshes");

        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"Assets/GeneratedMeshes/{OptimizedName}.asset");
        AssetDatabase.CreateAsset(finalMesh, assetPath);
        AssetDatabase.SaveAssets();

        Vector3 pos = target.transform.position;
        Quaternion rot = target.transform.rotation;
        Transform parent = target.transform.parent;

        Undo.DestroyObjectImmediate(target);

        GameObject optGo = new GameObject(OptimizedName);
        optGo.transform.SetParent(parent, false);
        optGo.transform.position = pos;
        optGo.transform.rotation = rot;
        optGo.isStatic = true;

        optGo.AddComponent<MeshFilter>().sharedMesh = finalMesh;
        optGo.AddComponent<MeshRenderer>().sharedMaterials = finalMats.ToArray();
        optGo.AddComponent<MeshCollider>().sharedMesh = finalMesh;

        Undo.RegisterCreatedObjectUndo(optGo, "Merge Circle Pillars");
        Selection.activeGameObject = null;

        Debug.Log($"[Circle Pillar Placer] {meshFilters.Length}개의 기둥이 1개의 최적화된 메시로 병합되었습니다.");
    }

    static bool TryGetCombinedBounds(GameObject go, out Bounds combined)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            combined = new Bounds(go.transform.position, Vector3.one);
            return false;
        }

        combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combined.Encapsulate(renderers[i].bounds);
        }
        return true;
    }

    static void ClearContainer(string name)
    {
        var existing = GameObject.Find(name);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }
    }
}
#endif