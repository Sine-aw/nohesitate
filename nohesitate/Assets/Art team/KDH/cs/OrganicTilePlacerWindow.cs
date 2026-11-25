#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class OrganicTilePlacerWindow : EditorWindow
{
    enum BuildMode
    {
        FloorGrid,        // 새 바닥 그리드 생성 (가로 X × 세로 Z)
        WallGrid,         // 새 벽면 그리드 생성 (가로 길이 × 층 높이)
        ReplaceSelected   // 씬에서 선택한 기존 타일들을 군집 패턴으로 교체
    }

    BuildMode mode = BuildMode.FloorGrid;

    // 4가지 변형 타일 슬롯
    GameObject normalTile;        // 1. 기본 돌 타일 (필수)
    GameObject crackedTile;       // 2. 부서진(금 간) 돌 타일 (선택)
    GameObject mossyTile;         // 3. 이끼 낀 돌 타일 (선택)
    GameObject crackedMossyTile;  // 4. 부서짐 + 이끼 타일 (선택)

    // 생성 크기 및 방향 설정
    int countX = 12;              // 가로 타일 칸 수
    int countZ = 12;              // 세로(또는 벽 높이) 타일 칸 수
    Vector3 startOrigin = Vector3.zero;
    float directionYaw = 0f;      // 전체 타일 방향 통일 (0, 90, 180, -90)

    // 자연스러운 혼합 & 뭉침(군집화) 설정
    [Range(0f, 1f)] float mossRatio = 0.35f;        // 전체 이끼 비율
    [Range(0f, 1f)] float crackRatio = 0.25f;       // 전체 파손 비율
    [Range(1.5f, 15f)] float clumpSize = 5.0f;      // 뭉침(군집) 덩어리 크기
    [Range(0f, 0.5f)] float edgeScatter = 0.18f;    // 군집 경계면 섞임 정도

    int seed = 100;

    const string FloorContainerName = "Generated_Organic_Floor";
    const string WallContainerName = "Generated_Organic_Wall";

    [MenuItem("Tools/Organic Tile Placer")]
    public static void ShowWindow()
    {
        GetWindow<OrganicTilePlacerWindow>("Organic Tile Placer");
    }

    void OnGUI()
    {
        GUILayout.Label("자연스러운 이끼·파손 군집 배치 도구", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        mode = (BuildMode)EditorGUILayout.EnumPopup("작업 모드", mode);

        EditorGUILayout.Space(6);
        GUILayout.Label("1. 타일 변형 등록 (벽 2종만 있을 땐 기본·이끼만 등록)", EditorStyles.boldLabel);

        normalTile = (GameObject)EditorGUILayout.ObjectField("기본 타일 (Normal) *", normalTile, typeof(GameObject), true);
        crackedTile = (GameObject)EditorGUILayout.ObjectField("부서진 타일 (Cracked)", crackedTile, typeof(GameObject), true);
        mossyTile = (GameObject)EditorGUILayout.ObjectField("이끼 낀 타일 (Mossy)", mossyTile, typeof(GameObject), true);
        crackedMossyTile = (GameObject)EditorGUILayout.ObjectField("부서짐 + 이끼 타일", crackedMossyTile, typeof(GameObject), true);

        EditorGUILayout.Space(8);
        GUILayout.Label("2. 비율 및 뭉침(군집) 설정", EditorStyles.boldLabel);

        mossRatio = EditorGUILayout.Slider("이끼 비율", mossRatio, 0f, 1f);
        crackRatio = EditorGUILayout.Slider("파손(부서짐) 비율", crackRatio, 0f, 1f);
        clumpSize = EditorGUILayout.Slider("뭉침 덩어리 크기 (클수록 넓게 군집)", clumpSize, 1.5f, 15f);
        edgeScatter = EditorGUILayout.Slider("경계면 섞임 정도", edgeScatter, 0f, 0.5f);

        EditorGUILayout.BeginHorizontal();
        seed = EditorGUILayout.IntField("패턴 시드 (Seed)", seed);
        if (GUILayout.Button("다른 모양 뽑기", GUILayout.Width(105)))
        {
            seed = Random.Range(1, 99999);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        if (mode != BuildMode.ReplaceSelected)
        {
            GUILayout.Label("3. 배치 크기 · 위치 · 방향 통일", EditorStyles.boldLabel);
            countX = EditorGUILayout.IntSlider("가로 칸 수", countX, 1, 100);
            countZ = EditorGUILayout.IntSlider(mode == BuildMode.FloorGrid ? "세로 칸 수" : "높이(층) 칸 수", countZ, 1, 100);

            EditorGUILayout.BeginHorizontal();
            startOrigin = EditorGUILayout.Vector3Field("시작 기준 좌표", startOrigin);
            if (Selection.activeTransform != null && GUILayout.Button("선택 위치", GUILayout.Width(72)))
            {
                startOrigin = Selection.activeTransform.position;
            }
            EditorGUILayout.EndHorizontal();

            // 모든 타일이 동일한 방향을 바라보도록 일괄 각도 지정
            directionYaw = EditorGUILayout.Slider("타일 방향 통일 (Y각도)", directionYaw, -180f, 180f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("빠른 방향:", GUILayout.Width(65));
            if (GUILayout.Button("0°")) directionYaw = 0f;
            if (GUILayout.Button("90°")) directionYaw = 90f;
            if (GUILayout.Button("180°")) directionYaw = 180f;
            if (GUILayout.Button("-90°")) directionYaw = -90f;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            GUI.backgroundColor = new Color(0.42f, 0.85f, 0.55f);
            string btnLabel = (mode == BuildMode.FloorGrid) ? "바닥 생성 (방향 통일 · 단차 없음)" : "벽 생성 (방향 통일 · 단차 없음)";
            if (GUILayout.Button(btnLabel, GUILayout.Height(38)))
            {
                GenerateGrid();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("생성된 바닥 삭제", GUILayout.Height(24))) ClearContainer(FloorContainerName);
            if (GUILayout.Button("생성된 벽 삭제", GUILayout.Height(24))) ClearContainer(WallContainerName);
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label("3. 기존 배치 타일 일괄 변환", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Hierarchy에서 이미 깔아둔 바닥(또는 벽) 타일들이나 그 부모 오브젝트를 선택한 뒤 아래 버튼을 누르면,\n" +
                "원래 방향과 바닥 아랫면 높이를 그대로 유지한 채 군집 패턴으로 교체합니다.",
                MessageType.Info);

            EditorGUILayout.Space(8);
            GUI.backgroundColor = new Color(1f, 0.82f, 0.35f);
            if (GUILayout.Button("선택한 기존 타일들을 군집 패턴으로 교체", GUILayout.Height(38)))
            {
                ReplaceSelectedTiles();
            }
            GUI.backgroundColor = Color.white;
        }
    }

    void GenerateGrid()
    {
        if (normalTile == null)
        {
            EditorUtility.DisplayDialog("알림", "'기본 타일 (Normal)' 슬롯에 원본 타일을 넣어주세요.", "확인");
            return;
        }

        Quaternion unifiedRot = Quaternion.Euler(0f, directionYaw, 0f);
        if (!TryGetRotatedBounds(normalTile, unifiedRot, out Bounds refBounds))
        {
            EditorUtility.DisplayDialog("알림", "타일 오브젝트에 메시 렌더러가 없습니다.", "확인");
            return;
        }

        string containerName = (mode == BuildMode.FloorGrid) ? FloorContainerName : WallContainerName;
        ClearContainer(containerName);

        GameObject container = new GameObject(containerName);
        container.transform.position = startOrigin;
        Undo.RegisterCreatedObjectUndo(container, "Generate Organic Tiles");

        float stepX = Mathf.Max(0.1f, refBounds.size.x);
        float stepY = Mathf.Max(0.1f, refBounds.size.y);
        float stepZ = Mathf.Max(0.1f, refBounds.size.z);

        Vector3 wallRight = unifiedRot * Vector3.right;

        Random.InitState(seed);
        float mossOffsetX = Random.Range(0f, 1000f);
        float mossOffsetZ = Random.Range(0f, 1000f);
        float crackOffsetX = Random.Range(0f, 1000f);
        float crackOffsetZ = Random.Range(0f, 1000f);

        for (int ix = 0; ix < countX; ix++)
        {
            for (int iz = 0; iz < countZ; iz++)
            {
                Vector3 targetBottomCenter;

                if (mode == BuildMode.FloorGrid)
                {
                    targetBottomCenter = startOrigin + new Vector3(
                        (ix + 0.5f) * stepX,
                        0f,
                        (iz + 0.5f) * stepZ
                    );
                }
                else
                {
                    targetBottomCenter = startOrigin
                        + wallRight * ((ix + 0.5f) * stepX)
                        + Vector3.up * (iz * stepY);
                }

                GameObject chosen = PickVariant(
                    ix, iz,
                    mossOffsetX, mossOffsetZ,
                    crackOffsetX, crackOffsetZ);

                SpawnBottomAlignedTile(
                    chosen,
                    targetBottomCenter,
                    unifiedRot,
                    normalTile.transform.localScale,
                    container.transform,
                    $"Tile_{ix}_{iz}");
            }
        }

        Selection.activeGameObject = container;
        Debug.Log($"[Organic Tile Placer] 방향과 높이가 일치하는 {countX * countZ}개의 타일이 생성되었습니다.");
    }

    void ReplaceSelectedTiles()
    {
        if (normalTile == null)
        {
            EditorUtility.DisplayDialog("알림", "'기본 타일 (Normal)' 슬롯에 원본 타일을 넣어주세요.", "확인");
            return;
        }

        if (Selection.gameObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("알림", "Hierarchy에서 교체할 기존 타일(또는 타일들이 들어있는 부모 오브젝트)을 선택해주세요.", "확인");
            return;
        }

        var targets = new List<GameObject>();
        var visited = new HashSet<GameObject>();

        foreach (var go in Selection.gameObjects)
        {
            if (go.GetComponent<MeshRenderer>() == null && go.transform.childCount > 0)
            {
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    GameObject child = go.transform.GetChild(i).gameObject;
                    if (child.activeInHierarchy && visited.Add(child))
                        targets.Add(child);
                }
            }
            else if (visited.Add(go))
            {
                targets.Add(go);
            }
        }

        if (targets.Count == 0) return;

        TryGetCombinedBounds(normalTile, out Bounds refBounds);
        float tileSize = Mathf.Max(0.2f, Mathf.Max(refBounds.size.x, refBounds.size.z));

        Random.InitState(seed);
        float mossOffsetX = Random.Range(0f, 1000f);
        float mossOffsetZ = Random.Range(0f, 1000f);
        float crackOffsetX = Random.Range(0f, 1000f);
        float crackOffsetZ = Random.Range(0f, 1000f);

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Replace With Organic Tiles");

        for (int i = 0; i < targets.Count; i++)
        {
            GameObject oldGo = targets[i];
            if (!TryGetCombinedBounds(oldGo, out Bounds oldBounds)) continue;

            float nx = oldBounds.center.x / tileSize;
            float nz = (oldBounds.center.z + oldBounds.center.y) / tileSize;

            GameObject chosen = PickVariant(
                nx, nz,
                mossOffsetX, mossOffsetZ,
                crackOffsetX, crackOffsetZ);

            // 기존 타일의 아랫면(min.y)과 방향을 그대로 유지
            Vector3 targetBottomCenter = new Vector3(oldBounds.center.x, oldBounds.min.y, oldBounds.center.z);

            GameObject replacement = SpawnBottomAlignedTile(
                chosen,
                targetBottomCenter,
                oldGo.transform.rotation,
                oldGo.transform.localScale,
                oldGo.transform.parent,
                chosen.name + "_" + i);

            replacement.transform.SetSiblingIndex(oldGo.transform.GetSiblingIndex());
            Undo.RegisterCreatedObjectUndo(replacement, "Replace Tile");
            Undo.DestroyObjectImmediate(oldGo);
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"[Organic Tile Placer] 선택한 {targets.Count}개의 타일이 단차 없이 교체되었습니다.");
    }

    GameObject PickVariant(
        float gridX, float gridZ,
        float mossOffX, float mossOffZ,
        float crackOffX, float crackOffZ)
    {
        float freq = 1f / Mathf.Max(1f, clumpSize);

        float mNoise = Mathf.PerlinNoise(gridX * freq + mossOffX, gridZ * freq + mossOffZ);
        float cNoise = Mathf.PerlinNoise(gridX * freq + crackOffX, gridZ * freq + crackOffZ);

        mNoise += Random.Range(-edgeScatter, edgeScatter);
        cNoise += Random.Range(-edgeScatter, edgeScatter);

        bool hasMoss = (mossRatio > 0.001f) && (mNoise >= (1f - mossRatio));
        bool hasCrack = (crackRatio > 0.001f) && (cNoise >= (1f - crackRatio));

        if (hasMoss && hasCrack)
        {
            if (crackedMossyTile != null) return crackedMossyTile;
            if (mossyTile != null && crackedTile != null)
                return (mNoise - (1f - mossRatio)) >= (cNoise - (1f - crackRatio)) ? mossyTile : crackedTile;
            if (mossyTile != null) return mossyTile;
            if (crackedTile != null) return crackedTile;
            return normalTile;
        }

        if (hasMoss && mossyTile != null) return mossyTile;
        if (hasCrack && crackedTile != null) return crackedTile;
        return normalTile;
    }

    // 이끼가 위로 돌출되어도 돌바닥 높이가 어긋나지 않도록 '바닥 아랫면(min.y)' 기준으로 정렬
    static GameObject SpawnBottomAlignedTile(
        GameObject source, Vector3 targetBottomCenter, Quaternion targetRot,
        Vector3 targetScale, Transform parent, string objName)
    {
        GameObject instance;
        if (PrefabUtility.IsPartOfPrefabAsset(source))
            instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
        else
            instance = Instantiate(source, parent);

        instance.SetActive(true);
        instance.name = objName;
        instance.transform.rotation = targetRot;
        instance.transform.localScale = targetScale;
        instance.transform.position = targetBottomCenter;

        if (TryGetCombinedBounds(instance, out Bounds instBounds))
        {
            Vector3 currentBottomCenter = new Vector3(
                instBounds.center.x,
                instBounds.min.y,
                instBounds.center.z
            );
            instance.transform.position += (targetBottomCenter - currentBottomCenter);
        }

        return instance;
    }

    static bool TryGetRotatedBounds(GameObject go, Quaternion rot, out Bounds bounds)
    {
        if (PrefabUtility.IsPartOfPrefabAsset(go))
        {
            GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(go);
            temp.transform.rotation = rot;
            bool ok = TryGetCombinedBounds(temp, out bounds);
            DestroyImmediate(temp);
            return ok;
        }

        Quaternion origRot = go.transform.rotation;
        go.transform.rotation = rot;
        bool result = TryGetCombinedBounds(go, out bounds);
        go.transform.rotation = origRot;
        return result;
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