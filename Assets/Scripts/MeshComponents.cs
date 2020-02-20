using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using Unity.Collections;
using Unity.Physics;
using Material = UnityEngine.Material;

public class MeshComponents : MonoBehaviour
{
    public static MeshComponents instance;
    public static int columnHeight = 8;
    public static int chunkSize = 16;
    public static int worldSize = 2;

    public static Dictionary<string, Chunk> chunks;

    public static string BuildChunkName(float3 f)
    {
        return (int)f.x + "_" + (int)f.y + "_" + (int)f.z;
    }

    public Material textureAtlas;

    private EntityArchetype archetype;
    private EntityCommandBuffer entityCommandBuffer;
    private EntityManager entityManager;
    private Mesh mesh;

    private const float ATLAS_SIZE = 0.03125f;
    public Block[,,] chunkData;

    private void Awake()
    {
        instance = this;
    }

    // Start is called before the first frame update
    void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        archetype = entityManager.CreateArchetype(typeof(RenderMesh), typeof(LocalToWorld), typeof(Translation), typeof(Rotation), typeof(WorldChunk), typeof(PhysicsCollider));

        chunks = new Dictionary<string, Chunk>();
        this.transform.position = Vector3.zero;
        this.transform.rotation = Quaternion.identity;

        StartCoroutine(BuildWorld());

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private IEnumerator BuildWorld()
    {

        for (int z = 0; z < worldSize; z++)
            for (int x = 0; x < worldSize; x++)
                for (int y = 0; y < columnHeight; y++)
                {
                    float3 chunkPosition = new float3(x * chunkSize, y * chunkSize, z * chunkSize);
                    Chunk c = new Chunk(chunkPosition, textureAtlas, entityManager, archetype);
                    chunks.Add(c.chunkName, c);
                }

        foreach (KeyValuePair<string, Chunk> c in chunks)
        {
            c.Value.DrawChunk();
            yield return null;
        }

    }

    private IEnumerator BuildChunkColumn()
    {
        for(int i = 0; i < columnHeight; i++)
        {
            float3 chunkPosition = new float3(this.transform.position.x, i * chunkSize, this.transform.position.z);
            Chunk c = new Chunk(chunkPosition, textureAtlas, entityManager, archetype);
            chunks.Add(c.chunkName, c);
        }

        foreach(KeyValuePair<string, Chunk> c in chunks)
        {
            c.Value.DrawChunk();
            yield return null;
        }

    }


}
