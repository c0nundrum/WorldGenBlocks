using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Burst;

[DisableAutoCreation]
[UpdateBefore(typeof(Drawchunk))]
public class BuildChunkJob : JobComponentSystem
{
    private NativeHashMap<int3, ChunkValue> chunkValueMap;
    private Camera mainCamera;
    private float3 lastbuildPos;

    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;

    private struct BuildChunkAt : IJobParallelFor
    {
        [ReadOnly]
        [DeallocateOnJobCompletion]public NativeArray<int3> positionArray;
        [ReadOnly]
        public EntityArchetype archetype;

        [NativeDisableParallelForRestriction]
        public BufferFromEntity<BlockTypeBuffer> bufferFromEntity;
        
        public NativeHashMap<int3, ChunkValue>.ParallelWriter chunkValueMapWriter;

        public EntityCommandBuffer.Concurrent commandBuffer;

        public void Execute(int index)
        {
            float3 chunkPosition = new float3(positionArray[index].x * MeshComponents.chunkSize, positionArray[index].y * MeshComponents.chunkSize, positionArray[index].z * MeshComponents.chunkSize);
            int3 position = new int3((int3)math.floor(chunkPosition));
            
            ChunkValue c = new ChunkValue { position = chunkPosition };

            chunkValueMapWriter.TryAdd(position, c);
                 
            Entity entity = commandBuffer.CreateEntity(index, archetype);

            commandBuffer.SetComponent(index, entity, new WorldChunk
            {
                position = new int3((int)c.position.x, (int)c.position.y, (int)c.position.z),
                status = ChunkStatus.KEEP
            });

            var buffer = commandBuffer.AddBuffer<BlockTypeBuffer>(index, entity);
            buffer.Reinterpret<BlockType>().AddRange(c.GetBlockArray());

        }
    }

    protected override void OnCreate()
    {
        endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        chunkValueMap = new NativeHashMap<int3, ChunkValue>(64, Allocator.Persistent);

        mainCamera = Camera.main;

        lastbuildPos = mainCamera.transform.position;

        NativeArray<int3> PositionArray = new NativeArray<int3>(1, Allocator.TempJob);
        PositionArray[0] = new int3(0, 0, 0);

        BuildChunkAt job = new BuildChunkAt
        {
            archetype = EntityManager.CreateArchetype(typeof(WorldChunk)),
            bufferFromEntity = GetBufferFromEntity<BlockTypeBuffer>(false),
            chunkValueMapWriter = chunkValueMap.AsParallelWriter(),
            commandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
            positionArray = PositionArray
        };

        var handle = job.Schedule(PositionArray.Length, 8);

        endSimulationEntityCommandBufferSystem.AddJobHandleForProducer(handle);

        base.OnCreate();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        //Next step, create the recursive function to add all the positions to build to the array
        //float3 movement = lastbuildPos - new float3(mainCamera.transform.position);

        //if (math.length(movement) > chunkSize * bufferSize)
        //{
        //    lastbuildPos = mainCamera.transform.position;
        //    BuildNearPlayer();
        //}
        return inputDeps;
    }
}

[DisableAutoCreation]
[UpdateBefore(typeof(Drawchunk))]
public class BuildChunks : ComponentSystem
{
    private int chunkSize = 16;
    private int radius = 4;
    public ConcurrentDictionary<int3, Chunk> chunkMap;
    public ConcurrentDictionary<int3, ChunkValue> chunkValueMap;
    private Material textureAtlas;
    private Camera mainCamera;

    private float3 lastbuildPos;
    private int bufferSize = 1;

    private void BuildChunkValueAt(int x, int y, int z)
    {
        float3 chunkPosition = new float3(x * MeshComponents.chunkSize, y * MeshComponents.chunkSize, z * MeshComponents.chunkSize);
        int3 position = new int3((int3)math.floor(chunkPosition));
        if (!chunkValueMap.TryGetValue(position, out ChunkValue e))
        {
            ChunkValue c = new ChunkValue { position = chunkPosition };
            BuildChunkentity(c);
            chunkValueMap.TryAdd(position, c);

        }
    }

    private void BuildChunkAt(int x, int y, int z)
    {
        Vector3 chunkPosition = new Vector3(x * chunkSize, y * chunkSize, z * chunkSize);
        int3 position = new int3((int3)math.floor(chunkPosition));
        if (!chunkMap.TryGetValue(position, out Chunk e))
        {
            Chunk c = new Chunk(chunkPosition);
            DrawChunk(c);
            chunkMap.TryAdd(position, c);            
        }
    }

    private void BuildRecursiveWorld(int x, int y, int z, int rad)
    {
        rad--;
        if (rad <= 0) return;

        //Build Chunk Front
        BuildChunkAt(x, y, z + 1);
        BuildRecursiveWorld(x, y, z + 1, rad);

        //Build Chunk Back
        BuildChunkAt(x, y, z - 1);
        BuildRecursiveWorld(x, y, z - 1, rad);

        //Build Chunk left
        BuildChunkAt(x - 1, y, z);
        BuildRecursiveWorld(x - 1, y, z, rad);

        //Build Chunk right
        BuildChunkAt(x + 1, y, z);
        BuildRecursiveWorld(x + 1, y, z, rad);

        //Build Chunk up
        BuildChunkAt(x, y + 1, z);
        BuildRecursiveWorld(x, y + 1, z, rad);

        //Build Chunk Down
        BuildChunkAt(x, y - 1, z);
        BuildRecursiveWorld(x, y - 1, z, rad);
    }

    private void BuildChunkentity(ChunkValue c)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        EntityArchetype archetype = entityManager.CreateArchetype(typeof(WorldChunk));

        Entity entity = entityManager.CreateEntity(archetype);

        entityManager.SetComponentData(entity, new WorldChunk
        {
            position = new int3((int)c.position.x, (int)c.position.y, (int)c.position.z),
            status = ChunkStatus.KEEP
        });

        entityManager.AddBuffer<BlockTypeBuffer>(entity);

        entityManager.GetBuffer<BlockTypeBuffer>(entity).Reinterpret<BlockType>().AddRange(c.GetBlockArray());
    }

    private void DrawChunk(Chunk c)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        List<Mesh> chunk = new List<Mesh>();

        for (int z = 0; z < MeshComponents.chunkSize; z++)
        {
            for (int y = 0; y < MeshComponents.chunkSize; y++)
            {
                for (int x = 0; x < MeshComponents.chunkSize; x++)
                {
                    if (c.chunkData[x, y, z].isSolid)
                        chunk.Add(c.chunkData[x, y, z].CreateCube(chunkMap));
                }

            }
        }

        CombineInstance[] array = new CombineInstance[chunk.Count];

        for (int i = 0; i < array.Length; i++)
        {
            array[i].mesh = chunk[i];
        }

        Mesh chunkMesh = new Mesh();
        //since our cubes are created on the correct spot already, we dont need a matrix, and so far, no light data
        chunkMesh.CombineMeshes(array, true, false, false);

        EntityArchetype archetype = entityManager.CreateArchetype(typeof(WorldChunk), typeof(LocalToWorld), typeof(RenderMesh));

        Entity entity = entityManager.CreateEntity(archetype);

        RenderMesh renderMesh = new RenderMesh
        {
            mesh = chunkMesh,
            material = textureAtlas
        };

        entityManager.SetSharedComponentData(entity, renderMesh);

        entityManager.SetComponentData(entity, new WorldChunk
        {
            position = new int3((int)c.position.x, (int)c.position.y, (int)c.position.z),
            status = ChunkStatus.KEEP
        });

        entityManager.AddComponentData(entity, new DrawChunkFlag { });
        entityManager.AddComponentData(entity, new BuildCollisionMeshFlag { });

    }

    public void BuildNearPlayer()
    {
        BuildRecursiveWorld((int)(mainCamera.transform.position.x / chunkSize), (int)(mainCamera.transform.position.y / chunkSize), (int)(mainCamera.transform.position.z / chunkSize), radius);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    protected override void OnCreate()
    {
        chunkMap = new ConcurrentDictionary<int3, Chunk>();
        chunkValueMap = new ConcurrentDictionary<int3, ChunkValue>();
        mainCamera = Camera.main;
        textureAtlas = MeshComponents.instance.textureAtlas;
        lastbuildPos = mainCamera.transform.position;

        //BuildRecursiveWorld((int)(mainCamera.transform.position.x / chunkSize), (int)(mainCamera.transform.position.y / chunkSize), (int)(mainCamera.transform.position.z / chunkSize), radius);

        BuildChunkValueAt(0, 0, 0);

        base.OnCreate();
    }

    protected override void OnUpdate()
    {
        //float3 movement = lastbuildPos - new float3(mainCamera.transform.position);

        //if (math.length(movement) > chunkSize * bufferSize)
        //{
        //    lastbuildPos = mainCamera.transform.position;
        //    BuildNearPlayer();
        //}
    }

}
