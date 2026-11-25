#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public static class TorchFireFXCreator
{
    // 횃불 모델 복셀 1칸의 월드 크기.
    // 씬에서 횃불 전체 높이를 재서 28로 나눈 값을 넣으세요. (모델 높이가 28칸)
    const float VoxelSize = 0.05f;

    [MenuItem("Tools/Create Voxel Torch FX")]
    static void Create()
    {
        var root = new GameObject("TorchFX");
        Undo.RegisterCreatedObjectUndo(root, "Create Torch FX");
        if (Selection.activeTransform != null)
            root.transform.SetParent(Selection.activeTransform, false);

        Material mat = GetOrCreateMaterial();
        Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

        CreateFlame(root.transform, mat, cube);
        CreateEmbers(root.transform, mat, cube);
        CreateLight(root.transform);

        Selection.activeObject = root;
        Debug.Log("TorchFX 생성 완료. 불꽃이 시작되는 위치로 TorchFX를 옮겨 주세요.");
    }

    static Material GetOrCreateMaterial()
    {
        const string path = "Assets/TorchFX_Voxel.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");

        mat = new Material(sh);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static ParticleSystem NewPS(string name, Transform parent, Material mat, Mesh mesh)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // 위(+Y)로 발사

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = mesh;
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    static void CreateFlame(Transform parent, Material mat, Mesh mesh)
    {
        var ps = NewPS("Flame", parent, mat, mesh);

        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(VoxelSize * 8f, VoxelSize * 16f);
        main.startSize = new ParticleSystem.MinMaxCurve(VoxelSize * 1.0f, VoxelSize * 2.0f);
        main.startRotation = 0f;
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 100;

        var em = ps.emission;
        em.rateOverTime = 30f;

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 4f;
        sh.radius = VoxelSize * 1.5f;

        // 색이 부드럽게 변하지 않고 단계별로 바뀌는 픽셀 느낌 그라디언트
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.mode = GradientMode.Fixed;
        g.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f, 0.95f, 0.55f), 0.25f),
                new GradientColorKey(new Color(1f, 0.70f, 0.15f), 0.55f),
                new GradientColorKey(new Color(1f, 0.40f, 0.10f), 0.80f),
                new GradientColorKey(new Color(0.75f, 0.20f, 0.08f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 1f) });
        col.color = g;

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

        ps.Play();
    }

    static void CreateEmbers(Transform parent, Material mat, Mesh mesh)
    {
        var ps = NewPS("Embers", parent, mat, mesh);

        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(VoxelSize * 10f, VoxelSize * 22f);
        main.startSize = new ParticleSystem.MinMaxCurve(VoxelSize * 0.5f, VoxelSize * 0.9f);
        main.startRotation = 0f;
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 40;

        var em = ps.emission;
        em.rateOverTime = 5f;

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 20f;
        sh.radius = VoxelSize * 1.5f;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = VoxelSize * 4f;
        noise.frequency = 0.8f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.mode = GradientMode.Fixed;
        g.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f, 0.90f, 0.40f), 0.4f),
                new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.8f),
                new GradientColorKey(new Color(0.55f, 0.15f, 0.08f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 1f) });
        col.color = g;

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ps.Play();
    }

    static void CreateLight(Transform parent)
    {
        var go = new GameObject("TorchLight");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, VoxelSize * 6f, 0f);

        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.6f, 0.2f);
        l.range = VoxelSize * 100f;
        l.intensity = 2f;
        l.shadows = LightShadows.None;

        go.AddComponent<TorchFlicker>();
    }
}
#endif