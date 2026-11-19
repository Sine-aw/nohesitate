#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public class ColosseumOptimizerWindow : EditorWindow
{
    GameObject targetRoot;

    int sectorCount = 8;                  // 원형 구역 분할 수 (시야 밖 컬링 최적화용, 4~8 권장)
    bool weldVoxelVertices = true;        // 복셀 중복 버텍스 압축 (각진 모서리/색상 100% 유지)
    bool stripBottomFaces = true;         // 바닥/벽 맨 아랫면(보이지 않는 면) 폴리곤 제거
    bool disableFoliageShadows = true;    // 덩굴/풀(초록색 계통) 그림자 끄기
    bool disableAllShadows = false;       // 콜로세움 전체 그림자 끄기 (최대 성능)
    bool rebuildCleanColliders = true;    // 덩굴 콜라이더 제거 후 벽/바닥만 깔끔한 콜라이더 생성
    bool keepOriginalDisabled = true;     // 원본 오브젝트는 비활성화하여 백업 유지

    // 최적화 전/후 통계 표시용
    string lastReport = "";

    [MenuItem("Tools/Colosseum Optimizer")]
    public static void ShowWindow()
    {
        GetWindow<ColosseumOptimizerWindow>("Colosseum Optimizer");
    }

    void OnGUI()
    {
        GUILayout.Label("콜로세움 전용 메시 · 렌더링 최적화 도구", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.HelpBox(
            "원형 벽과 원형 바닥(및 덩굴/풀)을 하나의 부모 오브젝트로 묶은 뒤 아래에 등록하고 실행하세요.\n" +
            "드로우 콜, 중복 폴리곤, 덩굴 그림자 부하를 한 번에 정리합니다.",
            MessageType.Info);

        EditorGUILayout.Space(4);
        targetRoot = (GameObject)EditorGUILayout.ObjectField("콜로세움 최상위 오브젝트", targetRoot, typeof(GameObject), true);

        if (targetRoot == null && Selection.activeGameObject != null)
        {
            if (GUILayout.Button($"현재 선택된 '{Selection.activeGameObject.name}' 가져오기"))
            {
                targetRoot = Selection.activeGameObject;
            }
        }

        EditorGUILayout.Space(8);
        GUILayout.Label("최적화 옵션", EditorStyles.boldLabel);

        sectorCount = EditorGUILayout.IntSlider("구역 분할 수 (시야 컬링용)", sectorCount, 1, 16);
        weldVoxelVertices = EditorGUILayout.Toggle("복셀 중복 버텍스 압축 (추천)", weldVoxelVertices);
        stripBottomFaces = EditorGUILayout.Toggle("바닥 밑면(안 보이는 면) 제거", stripBottomFaces);
        disableFoliageShadows = EditorGUILayout.Toggle("덩굴/풀 그림자 끄기 (성능 향상)", disableFoliageShadows);
        disableAllShadows = EditorGUILayout.Toggle("모든 벽/바닥 그림자 끄기 (극한 최적화)", disableAllShadows);
        rebuildCleanColliders = EditorGUILayout.Toggle("콜라이더 경량화 (벽/바닥만 유지)", rebuildCleanColliders);
        keepOriginalDisabled = EditorGUILayout.Toggle("원본 오브젝트 백업(비활성화) 유지", keepOriginalDisabled);

        EditorGUILayout.Space(12);

        GUI.backgroundColor = new Color(0.35f, 0.85f, 0.55f);
        if (GUILayout.Button("콜로세움 한 번에 최적화 실행", GUILayout.Height(40)))
        {
            OptimizeColosseum();
        }
        GUI.backgroundColor = Color.white;

        if (!string.IsNullOrEmpty(lastReport))
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(lastReport, MessageType.None);
        }
    }

    class CachedMeshData
    {
        public Vector3[] vertices;
        public Vector3[] normals;
        public Vector2[] uvs;
        public Color32[] colors;
        public int[][] subTriangles;
    }

    struct VertexKey : IEquatable<VertexKey>
    {
        public int px, py, pz;
        public int nx, ny, nz;
        public int u, v;
        public uint color;

        public VertexKey(Vector3 pos, Vector3 norm, Vector2 uv, Color32 col)
        {
            px = Mathf.RoundToInt(pos.x * 10000f);
            py = Mathf.RoundToInt(pos.y * 10000f);
            pz = Mathf.RoundToInt(pos.z * 10000f);
            nx = Mathf.RoundToInt(norm.x * 100f);
            ny = Mathf.RoundToInt(norm.y * 100f);
            nz = Mathf.RoundToInt(norm.z * 100f);
            u = Mathf.RoundToInt(uv.x * 10000f);
            v = Mathf.RoundToInt(uv.y * 10000f);
            color = ((uint)col.r << 24) | ((uint)col.g << 16) | ((uint)col.b << 8) | col.a;
        }

        public bool Equals(VertexKey other)
        {
            return px == other.px && py == other.py && pz == other.pz &&
                   nx == other.nx && ny == other.ny && nz == other.nz &&
                   u == other.u && v == other.v && color == other.color;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + px;
                h = h * 31 + py;
                h = h * 31 + pz;
                h = h * 31 + nx;
                h = h * 31 + ny;
                h = h * 31 + nz;
                h = h * 31 + u;
                h = h * 31 + v;
                h = h * 31 + (int)color;
                return h;
            }
        }
    }

    class MeshBucket
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        public readonly List<Color32> colors = new List<Color32>();
        public readonly List<int> triangles = new List<int>();
        public readonly Dictionary<VertexKey, int> lookup = new Dictionary<VertexKey, int>();

        public void AddTriangle(
            Vector3 p0, Vector3 p1, Vector3 p2,
            Vector3 n0, Vector3 n1, Vector3 n2,
            Vector2 uv0, Vector2 uv1, Vector2 uv2,
            Color32 c0, Color32 c1, Color32 c2,
            bool weld)
        {
            triangles.Add(AddVertex(p0, n0, uv0, c0, weld));
            triangles.Add(AddVertex(p1, n1, uv1, c1, weld));
            triangles.Add(AddVertex(p2, n2, uv2, c2, weld));
        }

        int AddVertex(Vector3 p, Vector3 n, Vector2 uv, Color32 c, bool weld)
        {
            if (weld)
            {
                var key = new VertexKey(p, n, uv, c);
                if (lookup.TryGetValue(key, out int existingIdx))
                {
                    return existingIdx;
                }
                int idx = vertices.Count;
                vertices.Add(p);
                normals.Add(n);
                uvs.Add(uv);
                colors.Add(c);
                lookup[key] = idx;
                return idx;
            }
            else
            {
                int idx = vertices.Count;
                vertices.Add(p);
                normals.Add(n);
                uvs.Add(uv);
                colors.Add(c);
                return idx;
            }
        }
    }

    void OptimizeColosseum()
    {
        if (targetRoot == null)
        {
            EditorUtility.DisplayDialog("알림", "최적화할 콜로세움 최상위 오브젝트를 먼저 지정해주세요.", "확인");
            return;
        }

        var meshFilters = targetRoot.GetComponentsInChildren<MeshFilter>(false);
        if (meshFilters.Length == 0)
        {
            EditorUtility.DisplayDialog("알림", "활성화된 자식 메시가 없습니다.", "확인");
            return;
        }

        EditorUtility.DisplayProgressBar("Colosseum Optimizer", "메시 데이터 분석 및 중심 계산 중...", 0.1f);

        try
        {
            // 1. 콜로세움 중심 및 최저 높이 계산
            Bounds totalBounds = new Bounds(targetRoot.transform.position, Vector3.zero);
            bool hasBounds = false;
            int beforeRenderers = 0;
            long beforeVerts = 0;
            long beforeTris = 0;

            var meshCache = new Dictionary<Mesh, CachedMeshData>();

            foreach (var mf in meshFilters)
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled || mf.sharedMesh == null) continue;

                if (!hasBounds)
                {
                    totalBounds = mr.bounds;
                    hasBounds = true;
                }
                else
                {
                    totalBounds.Encapsulate(mr.bounds);
                }

                beforeRenderers++;
                Mesh m = mf.sharedMesh;
                beforeVerts += m.vertexCount;

                if (!meshCache.TryGetValue(m, out var cached))
                {
                    int subCount = m.subMeshCount;
                    cached = new CachedMeshData
                    {
                        vertices = m.vertices,
                        normals = m.normals,
                        uvs = m.uv,
                        colors = m.colors32,
                        subTriangles = new int[subCount][]
                    };
                    for (int s = 0; s < subCount; s++)
                    {
                        cached.subTriangles[s] = m.GetTriangles(s);
                    }
                    meshCache[m] = cached;
                }

                for (int s = 0; s < cached.subTriangles.Length; s++)
                {
                    beforeTris += cached.subTriangles[s].Length / 3;
                }
            }

            Vector3 worldCenter = totalBounds.center;
            float minWorldY = totalBounds.min.y;
            Matrix4x4 rootWorldToLocal = targetRoot.transform.worldToLocalMatrix;

            // [sectorIndex][Material] -> MeshBucket
            var sectorBuckets = new Dictionary<Material, MeshBucket>[sectorCount];
            for (int i = 0; i < sectorCount; i++)
            {
                sectorBuckets[i] = new Dictionary<Material, MeshBucket>();
            }

            EditorUtility.DisplayProgressBar("Colosseum Optimizer", "구역별 폴리곤 병합 및 버텍스 압축 중...", 0.45f);

            // 2. 모든 삼각형을 구역(각도) 및 머티리얼별로 분류하여 병합
            for (int idx = 0; idx < meshFilters.Length; idx++)
            {
                var mf = meshFilters[idx];
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled || mf.sharedMesh == null) continue;

                Mesh m = mf.sharedMesh;
                var cached = meshCache[m];
                var mats = mr.sharedMaterials;
                int subCount = Mathf.Min(cached.subTriangles.Length, mats.Length);

                Matrix4x4 localToWorld = mf.transform.localToWorldMatrix;
                Matrix4x4 normalToWorld = localToWorld.inverse.transpose;

                bool hasNormals = cached.normals != null && cached.normals.Length == cached.vertices.Length;
                bool hasUvs = cached.uvs != null && cached.uvs.Length == cached.vertices.Length;
                bool hasColors = cached.colors != null && cached.colors.Length == cached.vertices.Length;

                for (int s = 0; s < subCount; s++)
                {
                    Material mat = mats[s];
                    if (mat == null) continue;
                    if (!mat.enableInstancing) mat.enableInstancing = true;

                    int[] tris = cached.subTriangles[s];
                    for (int t = 0; t < tris.Length; t += 3)
                    {
                        int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];

                        Vector3 w0 = localToWorld.MultiplyPoint3x4(cached.vertices[i0]);
                        Vector3 w1 = localToWorld.MultiplyPoint3x4(cached.vertices[i1]);
                        Vector3 w2 = localToWorld.MultiplyPoint3x4(cached.vertices[i2]);

                        Vector3 faceN = Vector3.Cross(w1 - w0, w2 - w0);
                        if (faceN.sqrMagnitude < 1e-12f) continue; // 면적이 0인 퇴화 삼각형 제거
                        faceN.Normalize();

                        Vector3 triCenter = (w0 + w1 + w2) * (1f / 3f);

                        // 최하단 바닥 밑면(카메라에 절대 보이지 않는 면) 제거
                        if (stripBottomFaces && faceN.y < -0.95f && triCenter.y <= minWorldY + 0.05f)
                        {
                            continue;
                        }

                        // 원 중심 기준 각도로 구역(Sector) 결정
                        int sectorIdx = 0;
                        if (sectorCount > 1)
                        {
                            float angle = Mathf.Atan2(triCenter.z - worldCenter.z, triCenter.x - worldCenter.x);
                            if (angle < 0f) angle += Mathf.PI * 2f;
                            sectorIdx = Mathf.Clamp(Mathf.FloorToInt(angle / (Mathf.PI * 2f) * sectorCount), 0, sectorCount - 1);
                        }

                        if (!sectorBuckets[sectorIdx].TryGetValue(mat, out var bucket))
                        {
                            bucket = new MeshBucket();
                            sectorBuckets[sectorIdx][mat] = bucket;
                        }

                        Vector3 n0 = hasNormals ? normalToWorld.MultiplyVector(cached.normals[i0]).normalized : faceN;
                        Vector3 n1 = hasNormals ? normalToWorld.MultiplyVector(cached.normals[i1]).normalized : faceN;
                        Vector3 n2 = hasNormals ? normalToWorld.MultiplyVector(cached.normals[i2]).normalized : faceN;

                        Vector2 uv0 = hasUvs ? cached.uvs[i0] : Vector2.zero;
                        Vector2 uv1 = hasUvs ? cached.uvs[i1] : Vector2.zero;
                        Vector2 uv2 = hasUvs ? cached.uvs[i2] : Vector2.zero;

                        Color32 c0 = hasColors ? cached.colors[i0] : new Color32(255, 255, 255, 255);
                        Color32 c1 = hasColors ? cached.colors[i1] : new Color32(255, 255, 255, 255);
                        Color32 c2 = hasColors ? cached.colors[i2] : new Color32(255, 255, 255, 255);

                        // 루트 로컬 좌표계로 변환하여 저장
                        bucket.AddTriangle(
                            rootWorldToLocal.MultiplyPoint3x4(w0),
                            rootWorldToLocal.MultiplyPoint3x4(w1),
                            rootWorldToLocal.MultiplyPoint3x4(w2),
                            rootWorldToLocal.MultiplyVector(n0).normalized,
                            rootWorldToLocal.MultiplyVector(n1).normalized,
                            rootWorldToLocal.MultiplyVector(n2).normalized,
                            uv0, uv1, uv2,
                            c0, c1, c2,
                            weldVoxelVertices
                        );
                    }
                }
            }

            EditorUtility.DisplayProgressBar("Colosseum Optimizer", "최적화 메시 에셋 생성 및 저장 중...", 0.8f);

            // 3. 저장 폴더 생성
            if (!AssetDatabase.IsValidFolder("Assets/OptimizedColosseum"))
            {
                AssetDatabase.CreateFolder("Assets", "OptimizedColosseum");
            }
            string batchFolder = AssetDatabase.GenerateUniqueAssetPath($"Assets/OptimizedColosseum/{targetRoot.name}_Data");
            AssetDatabase.CreateFolder("Assets/OptimizedColosseum", System.IO.Path.GetFileName(batchFolder));

            GameObject optimizedRoot = new GameObject($"{targetRoot.name}_Optimized");
            optimizedRoot.transform.SetParent(targetRoot.transform.parent, false);
            optimizedRoot.transform.position = targetRoot.transform.position;
            optimizedRoot.transform.rotation = targetRoot.transform.rotation;
            optimizedRoot.transform.localScale = targetRoot.transform.localScale;
            optimizedRoot.layer = targetRoot.layer;
            optimizedRoot.isStatic = true;

            Undo.RegisterCreatedObjectUndo(optimizedRoot, "Optimize Colosseum");

            int afterRenderers = 0;
            long afterVerts = 0;
            long afterTris = 0;

            for (int s = 0; s < sectorCount; s++)
            {
                var matDict = sectorBuckets[s];
                if (matDict.Count == 0) continue;

                GameObject sectorGo = new GameObject($"Colosseum_Sector_{s + 1:D2}");
                sectorGo.transform.SetParent(optimizedRoot.transform, false);
                sectorGo.layer = targetRoot.layer;
                sectorGo.isStatic = true;

                foreach (var kvp in matDict)
                {
                    Material mat = kvp.Key;
                    MeshBucket bucket = kvp.Value;
                    if (bucket.triangles.Count == 0) continue;

                    bool isFoliage = IsLikelyFoliageMaterial(mat);
                    string partLabel = isFoliage ? "Foliage" : "Structure";

                    GameObject partGo = new GameObject($"{partLabel}_{mat.name}");
                    partGo.transform.SetParent(sectorGo.transform, false);
                    partGo.layer = targetRoot.layer;
                    partGo.isStatic = true;

                    Mesh mesh = new Mesh
                    {
                        name = $"{targetRoot.name}_S{s + 1}_{partLabel}_{mat.name}",
                        indexFormat = IndexFormat.UInt32
                    };
                    mesh.SetVertices(bucket.vertices);
                    mesh.SetNormals(bucket.normals);
                    mesh.SetUVs(0, bucket.uvs);
                    mesh.SetColors(bucket.colors);
                    mesh.SetTriangles(bucket.triangles, 0, true);
                    mesh.RecalculateBounds();
                    MeshUtility.Optimize(mesh);

                    string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{batchFolder}/{mesh.name}.asset");
                    AssetDatabase.CreateAsset(mesh, assetPath);

                    var mf = partGo.AddComponent<MeshFilter>();
                    mf.sharedMesh = mesh;

                    var mr = partGo.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                    mr.lightProbeUsage = LightProbeUsage.Off;
                    mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

                    if (disableAllShadows || (disableFoliageShadows && isFoliage))
                    {
                        mr.shadowCastingMode = ShadowCastingMode.Off;
                    }

                    // 덩굴/풀은 물리 콜라이더를 빼고 돌벽/바닥 본체만 콜라이더 유지
                    if (rebuildCleanColliders && !isFoliage)
                    {
                        var mc = partGo.AddComponent<MeshCollider>();
                        mc.sharedMesh = mesh;
                    }

                    afterRenderers++;
                    afterVerts += mesh.vertexCount;
                    afterTris += bucket.triangles.Count / 3;
                }
            }

            AssetDatabase.SaveAssets();

            // 원본 오브젝트 비활성화(백업) 처리
            if (keepOriginalDisabled)
            {
                Undo.RecordObject(targetRoot, "Disable Original Colosseum");
                targetRoot.SetActive(false);
            }

            // 에디터 선택 외곽선 렉 방지를 위해 선택 해제
            Selection.activeGameObject = null;

            float vertSavePct = beforeVerts > 0 ? (1f - (float)afterVerts / beforeVerts) * 100f : 0f;
            lastReport =
                $"최적화 완료!\n" +
                $"• 오브젝트(드로우 콜): {beforeRenderers:N0}개  →  {afterRenderers:N0}개\n" +
                $"• 버텍스 수: {beforeVerts:N0}개  →  {afterVerts:N0}개 ({vertSavePct:F1}% 감소)\n" +
                $"• 삼각형 수: {beforeTris:N0}개  →  {afterTris:N0}개\n" +
                $"• 저장 경로: {batchFolder}";

            Debug.Log("[Colosseum Optimizer]\n" + lastReport);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // 머티리얼 이름 또는 색상으로 덩굴/풀 재질 자동 판별
    static bool IsLikelyFoliageMaterial(Material mat)
    {
        string lower = mat.name.ToLowerInvariant();
        if (lower.Contains("vine") || lower.Contains("leaf") || lower.Contains("leaves") ||
            lower.Contains("grass") || lower.Contains("foliage") || lower.Contains("pul") ||
            lower.Contains("plant") || lower.Contains("moss"))
        {
            return true;
        }

        if (mat.HasProperty("_BaseColor"))
        {
            Color c = mat.GetColor("_BaseColor");
            if (c.g > c.r * 1.25f && c.g > c.b * 1.25f) return true;
        }
        else if (mat.HasProperty("_Color"))
        {
            Color c = mat.GetColor("_Color");
            if (c.g > c.r * 1.25f && c.g > c.b * 1.25f) return true;
        }

        return false;
    }
}
#endif