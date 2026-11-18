#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public static class FixMeshColliders
{
    [MenuItem("Tools/Fix Mesh Colliders (Selected)")]
    static void Fix()
    {
        int fixedCount = 0;
        foreach (var go in Selection.gameObjects)
        {
            // 선택한 오브젝트와 모든 자식의 MeshFilter를 검사
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;

                var mc = mf.GetComponent<MeshCollider>();
                if (mc == null)
                {
                    mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
                }
                else
                {
                    Undo.RecordObject(mc, "Fix Mesh Collider");
                }

                if (mc.sharedMesh != mf.sharedMesh)
                {
                    mc.sharedMesh = mf.sharedMesh;
                    fixedCount++;
                }
            }
        }
        Debug.Log($"Mesh Collider {fixedCount}개 연결 완료");
    }
}
#endif