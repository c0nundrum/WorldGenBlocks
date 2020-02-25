using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using Unity.Collections;
using Unity.Physics;
using Material = UnityEngine.Material;

public class MeshComponents : MonoBehaviour
{
    public Camera mainCamera;
    public static MeshComponents instance;
    public static int columnHeight = 8;
    public static int chunkSize = 16;
    public static int worldSize = 2;
    public static int radius = 1;
    public Slider loadingAmount;
    public Button playButton;

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
    private bool firstbuild = true;
    private bool building = false;

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

    }

    // Update is called once per frame
    void Update()
    {
        if (!building && !firstbuild)
            StartCoroutine(BuildWorld());
    }

    private IEnumerator BuildWorld()
    {
        building = true;
        int posx = (int)math.floor(mainCamera.transform.position.x / chunkSize);
        int posz = (int)math.floor(mainCamera.transform.position.z / chunkSize);

        float totalChunks = (math.pow(radius * 2 + 1, 2) * columnHeight) * 2;
        int processCount = 0;

        for (int z = -radius; z <= radius; z++)
            for (int x = -radius; x <= radius; x++)
                for (int y = 0; y < columnHeight; y++)
                {
                    float3 chunkPosition = new float3((x+posx) * chunkSize, y * chunkSize, (z+posz) * chunkSize);
                    Chunk c;
                    string n = BuildChunkName(chunkPosition);
                    if(chunks.TryGetValue(n, out c))
                    {
                        c.status = ChunkStatus.KEEP;
                        break;
                    }
                    else
                    {
                        c = new Chunk(chunkPosition, textureAtlas, entityManager, archetype);
                        chunks.Add(c.chunkName, c);
                    }

                    if (firstbuild)
                    {
                        processCount++;
                        loadingAmount.value = processCount / totalChunks * 100;
                    }
                    yield return null;
                }

        foreach (KeyValuePair<string, Chunk> c in chunks)
        {
            if(c.Value.status == ChunkStatus.DONE)
            {
                c.Value.DrawChunk();
                c.Value.status = ChunkStatus.KEEP;
            }

            //Delete old chunks here

            c.Value.status = ChunkStatus.DONE;
            if (firstbuild)
            {
                processCount++;
                loadingAmount.value = processCount / totalChunks * 100;
            }
            yield return null;
        }

        if (firstbuild)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var simulationSystemGroup = world.GetOrCreateSystem<SimulationSystemGroup>();

            var countSystem = world.GetOrCreateSystem<CameraControlSystem>();

            simulationSystemGroup.AddSystemToUpdateList(countSystem);

            simulationSystemGroup.SortSystemUpdateList();

            ScriptBehaviourUpdateOrder.UpdatePlayerLoop(world);

            loadingAmount.gameObject.SetActive(false);
            playButton.gameObject.SetActive(false);
            firstbuild = false;
        }

        building = false;
    }

    public void StartBuild()
    {
        StartCoroutine(BuildWorld());
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
