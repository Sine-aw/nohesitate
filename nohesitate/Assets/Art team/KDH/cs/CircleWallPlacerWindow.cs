#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class CircleWallPlacerWindow : EditorWindow
{
    enum HeightAnchor
    {
        FloorTop,    // 원형 바닥 윗면 위에 벽을 세움
        FloorBottom  // 원형 바닥 아랫면(지면) 높이에 벽을 세움
    }

    GameObject circleFloorObj;   // 원형 바닥 오브젝트
    GameObject wallSourceObj;    // 단일 벽 오브젝트 (또는 프리팹)

    Vector3 circleCenter;
    float radius = 10f;
    float baseY = 0f;
    HeightAnchor heightAnchor = HeightAnchor.FloorTop;

    int wallCount = 36;
    float gapAdjustPercent = 0f; // 0% = 겹침 없이 딱 맞닿음 (+면 겹침, -면 여유)
    bool autoFitWidth = true;    // 최소 개수로 바깥쪽 틈새만 메우도록 가로 미세 맞춤
    bool antiZFighting = true;   // 인접 벽 간 미세 높이/깊이 교차(2mm)로 면 깨짐 방지
    float radiusOffset = 0f;     // +면 바깥쪽, -면 안쪽으로 이동
    float heightOffset = 0f;     // 위/아래 미세 높이 보정
    float yawFix = 0f;           // 벽 방향 회전 보정 (0, 90, 180, -90)
    int doorGapCount = 0;        // 출입구로 비워둘 벽 칸 수 (0이면 전체 둘러쌈)
    float doorStartAngle = 0f;   // 출입구 위치 각도

    const string ContainerName = "Generated_Circle_Walls";

    [MenuItem("Tools/Circle Wall Placer")]
    public static void ShowWindow()
    {
        GetWindow<CircleWallPlacerWindow>("Circle Wall Placer");
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    void OnSceneGUI(SceneView sv)
    {
        if (circleFloorObj == null) return;

        float r = Mathf.Max(0.1f, radius + radiusOffset);
        Vector3 center = new Vector3(circleCenter.x, baseY + heightOffset, circleCenter.z);

        Handles.color = new Color(0.2f, 1f, 0.5f, 0.95f);
        Handles.DrawWireDisc(center, Vector3.up, r);

        float angleStep = 360f / Mathf.Max(3, wallCount);
        for (int i = 0; i < wallCount; i++)
        {
            float deg = i * angleStep;
            if (doorGapCount > 0)
            {
                float diff = Mathf.DeltaAngle(doorStartAngle, deg);
                if (diff >= 0f && diff < doorGapCount * angleStep) continue;
            }

            float rad = deg * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            Vector3 p = center + dir * r;
            Handles.DrawLine(p, p + Vector3.up * 1.5f);
        }
    }

    void OnGUI()
    {
        GUILayout.Label("원형 바닥 둘레 벽 자동 배치 도구", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUI.BeginChangeCheck();
        circleFloorObj = (GameObject)EditorGUILayout.ObjectField("원형 바닥 오브젝트", circleFloorObj, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck() && circleFloorObj != null)
        {
            MeasureCircleBounds();
            AutoCalculateWallCount();
            SceneView.RepaintAll();
        }

        EditorGUI.BeginChangeCheck();
        wallSourceObj = (GameObject)EditorGUILayout.ObjectField("벽 조각 (씬 오브젝트/프리팹)", wallSourceObj, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck() && wallSourceObj != null)
        {
            AutoCalculateWallCount();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(6);
        GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
        if (GUILayout.Button("원형 바닥 크기 & 최소 벽 개수 다시 계산", GUILayout.Height(28)))
        {
            AutoMeasureAll();
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(8);
        GUILayout.Label("원형 좌표 및 높이 설정", EditorStyles.boldLabel);

        circleCenter = EditorGUILayout.Vector3Field("원 중심 좌표 (X, Y, Z)", circleCenter);
        radius = EditorGUILayout.FloatField("측정된 반지름 (Radius)", radius);

        EditorGUI.BeginChangeCheck();
        heightAnchor = (HeightAnchor)EditorGUILayout.EnumPopup("높이 기준 위치", heightAnchor);
        if (EditorGUI.EndChangeCheck() && circleFloorObj != null)
        {
            MeasureCircleBounds();
        }

        baseY = EditorGUILayout.FloatField("배치 바닥 높이 (Y)", baseY);
        radiusOffset = EditorGUILayout.FloatField("반지름 오프셋 (안쪽 - / 바깥쪽 +)", radiusOffset);
        heightOffset = EditorGUILayout.FloatField("높이 미세 오프셋 (Y)", heightOffset);

        EditorGUILayout.Space(8);
        GUILayout.Label("개수 최적화 및 면 깨짐 방지 설정", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        wallCount = EditorGUILayout.IntSlider("벽 개수 (최소 권장)", wallCount, 3, 500);
        if (GUILayout.Button("개수 자동계산", GUILayout.Width(95)))
        {
            AutoCalculateWallCount();
        }
        EditorGUILayout.EndHorizontal();

        gapAdjustPercent = EditorGUILayout.Slider("모서리 맞물림 보정 (%)", gapAdjustPercent, -10f, 15f);
        autoFitWidth = EditorGUILayout.Toggle("바깥 틈새만 자동 밀착 (가로 미세 맞춤)", autoFitWidth);
        antiZFighting = EditorGUILayout.Toggle("겹침 깨짐 방지 (미세 교차 오프셋)", antiZFighting);

        EditorGUI.BeginChangeCheck();
        yawFix = EditorGUILayout.Slider("벽 방향 회전 (Y각도)", yawFix, -180f, 180f);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("빠른 회전:", GUILayout.Width(65));
        if (GUILayout.Button("0°")) yawFix = 0f;
        if (GUILayout.Button("90°")) yawFix = 90f;
        if (GUILayout.Button("180°")) yawFix = 180f;
        if (GUILayout.Button("-90°")) yawFix = -90f;
        EditorGUILayout.EndHorizontal();
        if (EditorGUI.EndChangeCheck() && wallSourceObj != null)
        {
            AutoCalculateWallCount();
        }

        EditorGUILayout.Space(6);
        doorGapCount = EditorGUILayout.IntSlider("출입구 비우기 (칸 수)", doorGapCount, 0, Mathf.Max(0, wallCount - 3));
        if (doorGapCount > 0)
        {
            doorStartAngle = EditorGUILayout.Slider("출입구 방향 각도", doorStartAngle, 0f, 360f);
        }

        EditorGUILayout.Space(12);

        GUI.backgroundColor = new Color(0.45f, 0.85f, 0.55f);
        if (GUILayout.Button("원 둘레에 벽 일괄 생성", GUILayout.Height(36)))
        {
            GenerateCircleWalls();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);
        if (GUILayout.Button("생성된 원형 벽 삭제", GUILayout.Height(24)))
        {
            ClearContainer();
        }

        if (GUI.changed)
        {
            SceneView.RepaintAll();
        }
    }

    void AutoMeasureAll()
    {
        if (circleFloorObj == null)
        {
            EditorUtility.DisplayDialog("알림", "먼저 '원형 바닥 오브젝트' 칸에 씬의 원형 바닥을 넣어주세요.", "확인");
            return;
        }

        MeasureCircleBounds();
        AutoCalculateWallCount();
    }

    void MeasureCircleBounds()
    {
        if (!TryGetCombinedBounds(circleFloorObj, out Bounds b)) return;

        circleCenter = new Vector3(b.center.x, b.center.y, b.center.z);
        radius = (b.extents.x + b.extents.z) * 0.5f;
        baseY = (heightAnchor == HeightAnchor.FloorTop) ? b.max.y : b.min.y;
    }

    void AutoCalculateWallCount()
    {
        if (wallSourceObj == null) return;

        Quaternion sampleRot = Quaternion.LookRotation(Vector3.back, Vector3.up) * Quaternion.Euler(0f, yawFix, 0f);
        if (!TryGetWallCoreDimensions(wallSourceObj, sampleRot, Vector3.forward, Vector3.right, out float wallWidth, out _))
            return;

        if (wallWidth <= 0.05f) return;

        // 원 중심 둘레 기준으로 불필요한 중복 없이 최소 벽 개수만 산출
        float effectiveRadius = Mathf.Max(0.5f, radius + radiusOffset);
        float circumference = 2f * Mathf.PI * effectiveRadius;

        wallCount = Mathf.Clamp(Mathf.RoundToInt(circumference / wallWidth), 4, 500);
    }

    void GenerateCircleWalls()
    {
        if (wallSourceObj == null)
        {
            EditorUtility.DisplayDialog("알림", "'벽 조각' 칸에 복제할 벽 오브젝트(또는 프리팹)를 넣어주세요.", "확인");
            return;
        }

        ClearContainer();

        GameObject container = new GameObject(ContainerName);
        container.transform.position = new Vector3(circleCenter.x, baseY, circleCenter.z);
        Undo.RegisterCreatedObjectUndo(container, "Generate Circle Walls");

        float effectiveRadius = radius + radiusOffset;
        float targetY = baseY + heightOffset;
        float angleStep = 360f / wallCount;

        Quaternion refRot = Quaternion.LookRotation(Vector3.back, Vector3.up) * Quaternion.Euler(0f, yawFix, 0f);
        TryGetWallCoreDimensions(wallSourceObj, refRot, Vector3.forward, Vector3.right, out float coreWidth, out float coreDepth);

        float widthScaleMultiplier = 1f;
        if (autoFitWidth && coreWidth > 0.05f)
        {
            // 외경 현(Chord) 길이에 맞춰 바깥 틈새만 메우도록 최소한으로 스케일 보정
            float outerRadius = effectiveRadius + coreDepth * 0.5f;
            float outerChord = 2f * outerRadius * Mathf.Sin(Mathf.PI / wallCount);
            float desiredWidth = outerChord * (1f + gapAdjustPercent * 0.01f);
            widthScaleMultiplier = Mathf.Clamp(desiredWidth / coreWidth, 0.8f, 1.35f);
        }

        bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(wallSourceObj);

        for (int i = 0; i < wallCount; i++)
        {
            float currentAngleDeg = i * angleStep;

            if (doorGapCount > 0)
            {
                float diff = Mathf.DeltaAngle(doorStartAngle, currentAngleDeg);
                if (diff >= 0f && diff < doorGapCount * angleStep) continue;
            }

            float rad = currentAngleDeg * Mathf.Deg2Rad;
            Vector3 outwardDir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            Vector3 tangentDir = Vector3.Cross(Vector3.up, outwardDir).normalized;

            // 짝수/홀수 벽에 2mm 교차 오프셋을 주어 동일 평면 Z-파이팅(깨짐) 방지
            float staggerSign = (antiZFighting && (i % 2 == 1)) ? 1f : 0f;
            float pieceRadius = effectiveRadius + staggerSign * 0.002f;
            float pieceY = targetY + staggerSign * 0.002f;

            Vector3 ringPos = new Vector3(
                circleCenter.x + outwardDir.x * pieceRadius,
                pieceY,
                circleCenter.z + outwardDir.z * pieceRadius
            );

            GameObject instance;
            if (isPrefabAsset)
                instance = (GameObject)PrefabUtility.InstantiatePrefab(wallSourceObj, container.transform);
            else
                instance = Instantiate(wallSourceObj, container.transform);

            instance.name = $"{wallSourceObj.name}_Ring_{i:D2}";
            Undo.RegisterCreatedObjectUndo(instance, "Generate Circle Walls");

            Quaternion lookRot = Quaternion.LookRotation(-outwardDir, Vector3.up) * Quaternion.Euler(0f, yawFix, 0f);
            instance.transform.rotation = lookRot;
            instance.transform.position = ringPos;

            if (Mathf.Abs(widthScaleMultiplier - 1f) > 0.001f)
            {
                Vector3 localTangent = Quaternion.Inverse(lookRot) * tangentDir;
                Vector3 s = instance.transform.localScale;
                if (Mathf.Abs(localTangent.x) >= Mathf.Abs(localTangent.z))
                    s.x *= widthScaleMultiplier;
                else
                    s.z *= widthScaleMultiplier;
                instance.transform.localScale = s;
            }

            if (TryGetCombinedBounds(instance, out Bounds instBounds))
            {
                Vector3 centerOffsetXZ = new Vector3(
                    ringPos.x - instBounds.center.x,
                    0f,
                    ringPos.z - instBounds.center.z
                );
                float bottomOffsetY = pieceY - instBounds.min.y;
                instance.transform.position += centerOffsetXZ + new Vector3(0f, bottomOffsetY, 0f);
            }
        }

        Selection.activeGameObject = container;
        Debug.Log($"[Circle Wall Placer] 최적화된 {container.transform.childCount}개의 벽이 배치되었습니다.");
    }

    static bool TryGetWallCoreDimensions(
        GameObject go, Quaternion targetRot, Vector3 outwardDir, Vector3 tangentDir,
        out float wallWidth, out float wallDepth)
    {
        wallWidth = 0f;
        wallDepth = 0f;

        var meshFilters = go.GetComponentsInChildren<MeshFilter>();
        if (meshFilters.Length == 0) return false;

        MeshFilter mainMf = null;
        float maxVolume = -1f;
        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;
            Vector3 sz = mf.sharedMesh.bounds.size;
            float vol = sz.x * sz.y * sz.z;
            if (vol > maxVolume)
            {
                maxVolume = vol;
                mainMf = mf;
            }
        }

        if (mainMf == null) return false;

        Bounds b = mainMf.sharedMesh.bounds;
        Vector3 c = b.center, ext = b.extents;
        Quaternion invOrigRot = Quaternion.Inverse(go.transform.rotation);
        Vector3 pivotPos = go.transform.position;

        float minT = float.MaxValue, maxT = float.MinValue;
        float minR = float.MaxValue, maxR = float.MinValue;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 worldCorner = mainMf.transform.TransformPoint(c + new Vector3(ext.x * x, ext.y * y, ext.z * z));
                    Vector3 rotatedCorner = targetRot * (invOrigRot * (worldCorner - pivotPos));

                    float tProj = Vector3.Dot(rotatedCorner, tangentDir);
                    float rProj = Vector3.Dot(rotatedCorner, outwardDir);

                    if (tProj < minT) minT = tProj;
                    if (tProj > maxT) maxT = tProj;
                    if (rProj < minR) minR = rProj;
                    if (rProj > maxR) maxR = rProj;
                }
            }
        }

        wallWidth = maxT - minT;
        wallDepth = maxR - minR;
        return true;
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

    static void ClearContainer()
    {
        var existing = GameObject.Find(ContainerName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }
    }
}
#endif