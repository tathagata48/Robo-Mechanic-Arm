using UnityEngine;

/// <summary>
/// Spawns red cubes randomly within a defined plane area at startup.
/// </summary>
public class CubeSpawner : MonoBehaviour
{
    [SerializeField] private int initialCubeCount = 5;
    [SerializeField] private Vector2 areaSize = new Vector2(5f, 5f);
    [SerializeField] private float spawnHeight = 0.25f;
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private Material redMaterial;

    private void Start()
    {
        SpawnCubes();
    }

    private void SpawnCubes()
    {
        for (int i = 0; i < initialCubeCount; i++)
        {
            Vector3 position = GetRandomPoint();
            GameObject cube = cubePrefab != null
                ? Instantiate(cubePrefab, position, Quaternion.identity)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);

            cube.transform.position = position;
            cube.tag = "RedCube";

            Rigidbody rb = cube.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = cube.AddComponent<Rigidbody>();
            }
            rb.mass = 1f;

            if (redMaterial != null)
            {
                var renderer = cube.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    renderer.material = redMaterial;
                }
            }
            else
            {
                var renderer = cube.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = Color.red;
                }
            }
        }
    }

    private Vector3 GetRandomPoint()
    {
        float randomX = Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f);
        float randomZ = Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f);
        return transform.position + new Vector3(randomX, spawnHeight, randomZ);
    }
}
