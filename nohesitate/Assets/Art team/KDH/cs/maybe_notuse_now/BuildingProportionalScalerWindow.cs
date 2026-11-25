#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class BuildingProportionalScalerWindow : EditorWindow
{
    class ModularAssembly
    {
        public string name;
        public List<GameObject> members = new List<GameObject>();
        public Bounds localBounds;
        public bool isColumn;
        public bool isHorizontalSlab;
    }

    GameObject buildingRoot;

    // 1. 확장 배율 및 바닥 들림
    float widthScale = 1.6f;          // 가로 확장 배율 (X)
    float depthScale = 1.6f;          // 세로 확장 배율 (Z)
    float heightScale = 1.4f;         // 높이 확장 배율 (Y)
    float groundFloorLift = 0.8f;     // 1층 바닥 추가 들림 높이 (m)

    // 2. 복제 채움 옵션
    float edgeMargin = 1.2f;          // 코너 보호 여백 (m)
    bool autoDetectMargin = true;     // 코너 여백 자동 계산
    bool addMidPillarsOnWideWall = true; // 넓어진 벽 빈 구간에만 중간 기둥 1개씩 보강
    bool antiZFighting = true;        // 타일 이음새 2mm 미세 교차
    bool hideOriginal = true;         // 원본 건물 비활성화(백업) 유지

    [MenuItem("Tools/Building Proportional Scaler")]
    public static void ShowWindow()
    {
        GetWindow<BuildingProportionalScalerWindow>("Building Scaler");
    }

    void OnGUI()
    {
        GUILayout.Label("원본 비율 유지 · 복제 채움(모듈러) 건물 확장 도구", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.HelpBox(
            "기둥 받침·몸통·머리를 한 세트로 묶어 뭉침을 방지하고,\n" +
            "벽과 바닥의 접합면을 유지한 채 원본 비율(1:1) 그대로 확장합니다.",
            MessageType.Info);

        EditorGUILayout.Space(4);
        EditorGUI.BeginChangeCheck();
        buildingRoot = (GameObject)EditorGUILayout.ObjectField("건물 최상위 오브젝트", buildingRoot, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck() && buildingRoot != null && autoDetectMargin)
        {
            EstimateEdgeMargin();
        }

        if (buildingRoot == null && Selection.activeGameObject != null)
        {
            if (GUILayout.Button($"선택된 '{Selection.activeGameObject.name}' 가져오기"))
            {
                buildingRoot = Selection.activeGameObject;
                if (autoDetectMargin) EstimateEdgeMargin();
            }
        }

        EditorGUILayout.Space(8);
        GUILayout.Label("1. 건물 확장 크기 설정", EditorStyles.boldLabel);

        widthScale = EditorGUILayout.Slider("가로 확장 배율 (X)", widthScale, 1.0f, 3.5f);
        depthScale = EditorGUILayout.Slider("세로 확장 배율 (Z)", depthScale, 1.0f, 3.5f);
        heightScale = EditorGUILayout.Slider("높이 확장 배율 (Y)", heightScale, 1.0f, 3.0f);
        groundFloorLift = EditorGUILayout.Slider("1층 바닥 추가 들림 (m)", groundFloorLift, 0.0f, 5.0f);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("빠른 프리셋:", GUILayout.Width(75));
        if (GUILayout.Button("1.4배 확장")) { widthScale = 1.4f; depthScale = 1.4f; heightScale = 1.3f; groundFloorLift = 0.5f; }
        if (GUILayout.Button("1.8배 대형")) { widthScale = 1.8f; depthScale = 1.8f; heightScale = 1.5f; groundFloorLift = 1.0f; }
        if (GUILayout.Button("2.2배 신전")) { widthScale = 2.2f; depthScale = 2.2f; heightScale = 1.7f; groundFloorLift = 1.5f; }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        GUILayout.Label("2. 정밀 배치 옵션", EditorStyles.boldLabel);

        autoDetectMargin = EditorGUILayout.Toggle("코너 보호 여백 자동 계산", autoDetectMargin);
        if (autoDetectMargin && buildingRoot != null)
        {
            EstimateEdgeMargin();
        }

        GUI.enabled = !autoDetectMargin;
        edgeMargin = EditorGUILayout.Slider("코너 보호 여백 (m)", edgeMargin, 0.2f, 5.0f);
        GUI.enabled = true;

        addMidPillarsOnWideWall = EditorGUILayout.Toggle("넓어진 벽 빈 공간에 중간 기둥 보강", addMidPillarsOnWideWall);
        antiZFighting = EditorGUILayout.Toggle("타일 이음새 깨짐 방지 (2mm 교차)", antiZFighting);
        hideOriginal = EditorGUILayout.Toggle("원본 건물 비활성화(백업) 유지", hideOriginal);

        EditorGUILayout.Space(12);

        GUI.backgroundColor = new Color(0.4f, 0.85f, 0.55f);
        if (GUILayout.Button("원본 비율 유지하며 복제 확장 생성", GUILayout.Height(40)))
        {
            BuildTiledExpansion();
        }
        GUI.backgroundColor = Color.white;
    }

    void EstimateEdgeMargin()
    {
        if (buildingRoot == null) return;
        Matrix4x4 w2l = buildingRoot.transform.worldToLocalMatrix;
        if (!TryGetRootLocalBounds(buildingRoot, w2l, out Bounds b)) return;

        float minHoriz = Mathf.Min(b.size.x, b.size.z);
        edgeMargin = Mathf.Clamp(minHoriz * 0.16f, 0.4f, 3.0f);
    }

    void BuildTiledExpansion()
    {
        if (buildingRoot == null)
        {
            EditorUtility.DisplayDialog("알림", "건물 최상위 오브젝트를 먼저 지정해주세요.", "확인");
            return;
        }

        Matrix4x4 rootW2L = buildingRoot.transform.worldToLocalMatrix;
        if (!TryGetRootLocalBounds(buildingRoot, rootW2L, out Bounds rootBounds))
        {
            EditorUtility.DisplayDialog("알림", "건물 안에 메시 렌더러가 없습니다.", "확인");
            return;
        }

        // 1. 개별 오브젝트 수집 후 수직 기둥 파츠(받침+몸통+머리)를 하나의 세트로 그룹핑
        var rawPieces = new List<(GameObject go, Bounds localBounds)>();
        CollectRawPieces(buildingRoot.transform, rootW2L, rootBounds, rawPieces);

        if (rawPieces.Count == 0)
        {
            EditorUtility.DisplayDialog("알림", "확장할 하위 오브젝트를 찾지 못했습니다.", "확인");
            return;
        }

        var assemblies = BuildModularAssemblies(rawPieces, rootBounds);

        string resultName = buildingRoot.name + "_Expanded";
        var existing = GameObject.Find(resultName);
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        GameObject newRoot = new GameObject(resultName);
        newRoot.transform.SetParent(buildingRoot.transform.parent, false);
        newRoot.transform.position = buildingRoot.transform.position;
        newRoot.transform.rotation = buildingRoot.transform.rotation;
        newRoot.transform.localScale = buildingRoot.transform.localScale;
        newRoot.layer = buildingRoot.layer;
        Undo.RegisterCreatedObjectUndo(newRoot, "Modular Building Expansion");

        Vector3 bMin = rootBounds.min;
        Vector3 bMax = rootBounds.max;
        Vector3 bSize = rootBounds.size;

        float extraX = bSize.x * (widthScale - 1f);
        float extraZ = bSize.z * (depthScale - 1f);

        float marginX = Mathf.Min(edgeMargin, bSize.x * 0.25f);
        float marginZ = Mathf.Min(edgeMargin, bSize.z * 0.25f);

        float coreX0 = bMin.x + marginX;
        float coreX1 = bMax.x - marginX;
        float coreZ0 = bMin.z + marginZ;
        float coreZ1 = bMax.z - marginZ;

        // 2. 1층 바닥 윗면 & 지붕 아랫면 탐색
        float floor1TopY = bMin.y;
        float roofBotY = bMax.y;

        foreach (var asm in assemblies)
        {
            if (!asm.isHorizontalSlab) continue;
            Bounds pb = asm.localBounds;
            float relY = (pb.center.y - bMin.y) / Mathf.Max(0.01f, bSize.y);

            if (relY < 0.35f && pb.max.y > floor1TopY)
                floor1TopY = pb.max.y;
            else if (relY > 0.85f && pb.min.y < roofBotY)
                roofBotY = pb.min.y;
        }

        if (roofBotY >= bMax.y - 0.02f)
        {
            roofBotY = bMax.y - Mathf.Min(0.5f, bSize.y * 0.06f);
        }

        float extraUpperH = bSize.y * (heightScale - 1f);

        float MapX(float x)
        {
            float shiftLeft = -extraX * 0.5f;
            if (x <= coreX0) return x + shiftLeft;
            if (x >= coreX1) return x + shiftLeft + extraX;
            float t = (x - coreX0) / Mathf.Max(0.001f, coreX1 - coreX0);
            return (coreX0 + shiftLeft) + t * ((coreX1 - coreX0) + extraX);
        }

        float MapZ(float z)
        {
            float shiftBack = -extraZ * 0.5f;
            if (z <= coreZ0) return z + shiftBack;
            if (z >= coreZ1) return z + shiftBack + extraZ;
            float t = (z - coreZ0) / Mathf.Max(0.001f, coreZ1 - coreZ0);
            return (coreZ0 + shiftBack) + t * ((coreZ1 - coreZ0) + extraZ);
        }

        float MapY(float y)
        {
            if (y <= bMin.y) return bMin.y;

            if (floor1TopY > bMin.y + 0.02f && y <= floor1TopY)
            {
                float t = (y - bMin.y) / (floor1TopY - bMin.y);
                return bMin.y + t * ((floor1TopY - bMin.y) + groundFloorLift);
            }

            float liftedFloor1Top = floor1TopY + groundFloorLift;
            if (y >= roofBotY)
            {
                return y + groundFloorLift + extraUpperH;
            }

            float span = Mathf.Max(0.01f, roofBotY - floor1TopY);
            float tUpper = (y - floor1TopY) / span;
            return liftedFloor1Top + tUpper * (span + extraUpperH);
        }

        // 수직 벽면들의 확장 후 안쪽 경계선 수집 (바닥 슬래브가 벽과 떨어져 틈이 생기지 않도록 스냅용)
        var wallPlanesX = new List<float>();
        var wallPlanesZ = new List<float>();
        foreach (var asm in assemblies)
        {
            if (asm.isHorizontalSlab || asm.isColumn) continue;
            Bounds wb = asm.localBounds;
            if (wb.size.z > wb.size.x * 1.5f)
            {
                wallPlanesX.Add(MapX(wb.min.x));
                wallPlanesX.Add(MapX(wb.max.x));
            }
            if (wb.size.x > wb.size.z * 1.5f)
            {
                wallPlanesZ.Add(MapZ(wb.min.z));
                wallPlanesZ.Add(MapZ(wb.max.z));
            }
        }

        var placedColumnList = new List<(ModularAssembly asm, Vector3 centerOffset, Bounds placedBounds)>();
        int totalCreated = 0;

        // 3. 각 어셈블리를 원본 비율 그대로 배치 및 복제
        foreach (var asm in assemblies)
        {
            Bounds ub = asm.localBounds;

            float newX0 = MapX(ub.min.x);
            float newX1 = MapX(ub.max.x);
            float newY0 = MapY(ub.min.y);
            float newY1 = MapY(ub.max.y);
            float newZ0 = MapZ(ub.min.z);
            float newZ1 = MapZ(ub.max.z);

            // 바닥/테라스 슬래브가 원래 벽과 인접해 있었다면 확장된 벽 면까지 정확히 연장해 틈새 제거
            if (asm.isHorizontalSlab)
            {
                newX0 = SnapToNearestPlane(newX0, wallPlanesX, 0.65f);
                newX1 = SnapToNearestPlane(newX1, wallPlanesX, 0.65f);
                newZ0 = SnapToNearestPlane(newZ0, wallPlanesZ, 0.65f);
                newZ1 = SnapToNearestPlane(newZ1, wallPlanesZ, 0.65f);
            }

            List<float> xCenters;
            List<float> zCenters;

            if (asm.isColumn)
            {
                // 기둥은 수평으로 복제해 뭉치지 않도록 원래 비례 위치 1곳에만 배치
                xCenters = new List<float> { (newX0 + newX1) * 0.5f };
                zCenters = new List<float> { (newZ0 + newZ1) * 0.5f };
            }
            else
            {
                xCenters = ComputeCleanTiledPositions(ub.size.x, newX0, newX1);
                zCenters = ComputeCleanTiledPositions(ub.size.z, newZ0, newZ1);
            }

            List<float> yCenters = ComputeCleanTiledPositions(ub.size.y, newY0, newY1);

            Vector3 origCenter = ub.center;

            for (int ix = 0; ix < xCenters.Count; ix++)
            {
                for (int iy = 0; iy < yCenters.Count; iy++)
                {
                    for (int iz = 0; iz < zCenters.Count; iz++)
                    {
                        Vector3 targetCenter = new Vector3(xCenters[ix], yCenters[iy], zCenters[iz]);
                        Vector3 delta = targetCenter - origCenter;

                        if (antiZFighting)
                        {
                            if (xCenters.Count > 1 && (ix % 2 == 1)) delta.y += 0.002f;
                            if (zCenters.Count > 1 && (iz % 2 == 1)) delta.y -= 0.002f;
                            if (yCenters.Count > 1 && (iy % 2 == 1)) delta.x += 0.002f;
                        }

                        SpawnAssemblyClone(asm, delta, newRoot.transform, $"{ix}_{iy}_{iz}",
                            xCenters.Count * yCenters.Count * zCenters.Count == 1);
                        totalCreated++;

                        if (asm.isColumn && iy == 0)
                        {
                            Bounds movedB = new Bounds(ub.center + delta, ub.size);
                            placedColumnList.Add((asm, delta, movedB));
                        }
                    }
                }
            }
        }

        // 4. 옵션 활성화 시 기존 기둥들 사이의 '넓어진 빈 공간'에만 겹치지 않게 중간 기둥 1개씩 보강
        if (addMidPillarsOnWideWall && placedColumnList.Count >= 2 && (widthScale > 1.2f || depthScale > 1.2f))
        {
            totalCreated += InsertNonOverlappingMidPillars(placedColumnList, newRoot.transform);
        }

        if (hideOriginal)
        {
            Undo.RecordObject(buildingRoot, "Hide Original Building");
            buildingRoot.SetActive(false);
        }

        Selection.activeGameObject = newRoot;
        Debug.Log($"[Building Scaler] 뭉침·틈새 없이 총 {totalCreated}개 모듈로 확장 생성되었습니다.");
    }

    static void SpawnAssemblyClone(
        ModularAssembly asm, Vector3 localDelta, Transform parent, string suffix, bool isSingle)
    {
        for (int m = 0; m < asm.members.Count; m++)
        {
            GameObject src = asm.members[m];
            GameObject clone = Instantiate(src, parent);
            clone.SetActive(true);
            clone.name = isSingle ? src.name : $"{src.name}_{suffix}";
            clone.transform.localPosition = src.transform.localPosition + localDelta;
        }
    }

    // 겹침이나 뭉침 없이 목표 구간 [newA0, newA1]을 균등하게 채우는 중심 좌표 리스트 계산
    static List<float> ComputeCleanTiledPositions(float origLen, float newA0, float newA1)
    {
        var centers = new List<float>();
        origLen = Mathf.Max(0.01f, origLen);
        float targetSpan = Mathf.Max(origLen, newA1 - newA0);

        // 늘어난 길이가 원본의 1.25배 미만이면 복제하지 않고 1개만 배치해 겹침/뭉침 방지
        if (targetSpan < origLen * 1.25f)
        {
            centers.Add((newA0 + newA1) * 0.5f);
            return centers;
        }

        // 반올림(RoundToInt)으로 꼭 필요한 최소 타일 수만 산출해 과밀 뭉침 방지
        int tileCount = Mathf.Max(2, Mathf.RoundToInt(targetSpan / origLen));
        float firstCenter = newA0 + origLen * 0.5f;
        float lastCenter = newA1 - origLen * 0.5f;

        if (lastCenter <= firstCenter + 0.01f)
        {
            centers.Add((newA0 + newA1) * 0.5f);
            return centers;
        }

        for (int k = 0; k < tileCount; k++)
        {
            float t = (float)k / (tileCount - 1);
            centers.Add(Mathf.Lerp(firstCenter, lastCenter, t));
        }

        return centers;
    }

    static float SnapToNearestPlane(float coord, List<float> planes, float maxSnapDist)
    {
        float best = coord;
        float bestDist = maxSnapDist;
        for (int i = 0; i < planes.Count; i++)
        {
            float d = Mathf.Abs(planes[i] - coord);
            if (d < bestDist)
            {
                bestDist = d;
                best = planes[i];
            }
        }
        return best;
    }

    // 동일 벽 선상의 인접 기둥 쌍 중 거리가 넓어진 구간에만 정중앙에 기둥 1개씩 안전하게 추가
    static int InsertNonOverlappingMidPillars(
        List<(ModularAssembly asm, Vector3 centerOffset, Bounds placedBounds)> columns,
        Transform parent)
    {
        int added = 0;
        var occupiedCenters = new List<Vector3>();
        foreach (var c in columns) occupiedCenters.Add(c.placedBounds.center);

        int n = columns.Count;
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                Bounds b1 = columns[i].placedBounds;
                Bounds b2 = columns[j].placedBounds;

                if (Mathf.Abs(b1.min.y - b2.min.y) > 0.3f) continue;

                float dx = Mathf.Abs(b1.center.x - b2.center.x);
                float dz = Mathf.Abs(b1.center.z - b2.center.z);
                float colDiam = Mathf.Max(b1.size.x, b1.size.z);

                bool alongX = dz < 0.25f && dx > colDiam * 3.8f;
                bool alongZ = dx < 0.25f && dz > colDiam * 3.8f;
                if (!alongX && !alongZ) continue;

                // 사이에 다른 기둥이 있으면 건너뜀
                bool hasMiddle = false;
                for (int k = 0; k < n; k++)
                {
                    if (k == i || k == j) continue;
                    Vector3 ck = columns[k].placedBounds.center;
                    if (Mathf.Abs(ck.y - b1.center.y) > 0.4f) continue;

                    if (alongX && Mathf.Abs(ck.z - b1.center.z) < 0.25f &&
                        ck.x > Mathf.Min(b1.center.x, b2.center.x) + 0.1f &&
                        ck.x < Mathf.Max(b1.center.x, b2.center.x) - 0.1f) { hasMiddle = true; break; }

                    if (alongZ && Mathf.Abs(ck.x - b1.center.x) < 0.25f &&
                        ck.z > Mathf.Min(b1.center.z, b2.center.z) + 0.1f &&
                        ck.z < Mathf.Max(b1.center.z, b2.center.z) - 0.1f) { hasMiddle = true; break; }
                }
                if (hasMiddle) continue;

                Vector3 midCenter = (b1.center + b2.center) * 0.5f;

                // 기존 기둥과 일정 거리 이상 떨어져 있을 때만 생성 (뭉침 완전 차단)
                bool tooClose = false;
                foreach (var occ in occupiedCenters)
                {
                    float horizDist = Vector2.Distance(new Vector2(occ.x, occ.z), new Vector2(midCenter.x, midCenter.z));
                    if (horizDist < colDiam * 2.0f)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                Vector3 midDelta = midCenter - columns[i].asm.localBounds.center;
                SpawnAssemblyClone(columns[i].asm, midDelta, parent, $"Mid_{added + 1}", false);
                occupiedCenters.Add(midCenter);
                added++;
            }
        }
        return added;
    }

    // 같은 (X, Z) 수직선상에 놓인 기둥 파츠(받침대, 몸통, 기둥머리)를 하나의 어셈블리로 통합
    static List<ModularAssembly> BuildModularAssemblies(
        List<(GameObject go, Bounds localBounds)> rawPieces, Bounds rootBounds)
    {
        var list = new List<ModularAssembly>();
        float maxColFootprintX = rootBounds.size.x * 0.32f;
        float maxColFootprintZ = rootBounds.size.z * 0.32f;

        for (int i = 0; i < rawPieces.Count; i++)
        {
            var item = rawPieces[i];
            Bounds b = item.localBounds;

            bool isNarrowHoriz = b.size.x < maxColFootprintX && b.size.z < maxColFootprintZ;
            bool merged = false;

            if (isNarrowHoriz)
            {
                for (int a = 0; a < list.Count; a++)
                {
                    var existing = list[a];
                    Bounds eb = existing.localBounds;
                    if (eb.size.x >= maxColFootprintX || eb.size.z >= maxColFootprintZ) continue;

                    // 수직으로 같은 기둥 축 위에 있는 파츠인지 확인
                    if (Mathf.Abs(eb.center.x - b.center.x) < 0.25f &&
                        Mathf.Abs(eb.center.z - b.center.z) < 0.25f)
                    {
                        existing.members.Add(item.go);
                        eb.Encapsulate(b);
                        existing.localBounds = eb;
                        merged = true;
                        break;
                    }
                }
            }

            if (!merged)
            {
                var asm = new ModularAssembly
                {
                    name = item.go.name,
                    localBounds = b
                };
                asm.members.Add(item.go);
                list.Add(asm);
            }
        }

        // 통합된 바운딩 박스 기준으로 최종 역할(기둥 / 수평 바닥·지붕 / 벽) 판별
        foreach (var asm in list)
        {
            Bounds b = asm.localBounds;
            float maxH = Mathf.Max(b.size.x, b.size.z);
            float minH = Mathf.Min(b.size.x, b.size.z);
            string lower = asm.name.ToLowerInvariant();

            asm.isHorizontalSlab = maxH > b.size.y * 2.0f && minH > b.size.y * 1.4f;
            asm.isColumn = !asm.isHorizontalSlab &&
                           (lower.Contains("pillar") || lower.Contains("column") || lower.Contains("gidung") ||
                            (b.size.y > maxH * 1.35f && (maxH / Mathf.Max(0.01f, minH)) < 1.85f));
        }

        return list;
    }

    static void CollectRawPieces(
        Transform parent, Matrix4x4 rootW2L, Bounds rootBounds,
        List<(GameObject go, Bounds localBounds)> results)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (!child.gameObject.activeInHierarchy) continue;
            if (!TryGetRootLocalBounds(child.gameObject, rootW2L, out Bounds cb)) continue;

            bool hasOwnRenderer = child.GetComponent<MeshRenderer>() != null;
            bool isLargeGroup = !hasOwnRenderer && child.childCount > 1 &&
                                (cb.size.x > rootBounds.size.x * 0.55f ||
                                 cb.size.z > rootBounds.size.z * 0.55f);

            if (isLargeGroup)
            {
                CollectRawPieces(child, rootW2L, rootBounds, results);
            }
            else
            {
                results.Add((child.gameObject, cb));
            }
        }
    }

    static bool TryGetRootLocalBounds(GameObject target, Matrix4x4 rootW2L, out Bounds localBounds)
    {
        var renderers = target.GetComponentsInChildren<Renderer>();
        localBounds = default;
        bool found = false;

        foreach (var r in renderers)
        {
            if (!r.enabled) continue;
            Bounds wb = r.bounds;
            Vector3 c = wb.center, ext = wb.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 lp = rootW2L.MultiplyPoint3x4(c + new Vector3(ext.x * x, ext.y * y, ext.z * z));
                        if (!found)
                        {
                            localBounds = new Bounds(lp, Vector3.zero);
                            found = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(lp);
                        }
                    }
                }
            }
        }
        return found;
    }
}
#endif