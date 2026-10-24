using UnityEngine;

public class CircularWallBuilder : MonoBehaviour
{
    [SerializeField] private int wallCount = 16;
    [SerializeField] private float radius = 15f;
    [SerializeField] private float wallHeight = 5f;
    [SerializeField] private float wallThickness = 1f;

    private void Awake()
    {
        BuildWall();
    }

    private void BuildWall()
    {
        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }

        float angleStep = 360f / wallCount;
        float wallWidth = 2f * radius * Mathf.Sin(Mathf.Deg2Rad * angleStep / 2f);

        for (int i = 0; i < wallCount; i++)
        {
            float angle = i * angleStep;
            float rad = angle * Mathf.Deg2Rad;

            Vector3 pos = new Vector3(Mathf.Sin(rad) * radius, wallHeight / 2f, Mathf.Cos(rad) * radius);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(transform);
            wall.transform.localPosition = pos;
            wall.transform.localRotation = Quaternion.Euler(0, angle, 0);
            wall.transform.localScale = new Vector3(wallWidth + 0.1f, wallHeight, wallThickness);
        }

    }
}