using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using Unity.Transforms;

//[DisableAutoCreation]
public class DeleteSystem : JobComponentSystem
{

    private BeginPresentationEntityCommandBufferSystem beginPresentationEntityCommandBufferSystem;

    private EntityQuery m_Query;

    [BurstCompile]
    private struct ParallellDeleteQueue : IJobParallelFor
    {
        [ReadOnly]
        [DeallocateOnJobCompletion]public NativeArray<DeleteEntityEvent> eventArray;

        public EntityCommandBuffer.Concurrent commandBuffer;

        public void Execute(int index)
        {
            commandBuffer.DestroyEntity(index, eventArray[index].entity);
        }
    }

    [BurstCompile]
    private struct DeleteQueue : IJobForEachWithEntity<DeleteEntityEvent>
    {
        public EntityCommandBuffer.Concurrent commandBuffer;

        public void Execute(Entity entity, int index, ref DeleteEntityEvent deleteEvent)
        {
            commandBuffer.DestroyEntity(index, deleteEvent.entity);
        }
    }

    protected override void OnCreate()
    {
        beginPresentationEntityCommandBufferSystem = World.GetOrCreateSystem<BeginPresentationEntityCommandBufferSystem>();

        m_Query = GetEntityQuery(ComponentType.ReadOnly<DeleteEntityEvent>());

        base.OnCreate();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {

        //DeleteQueue deleteQueue = new DeleteQueue
        //{
        //    commandBuffer = beginPresentationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
        //};

        NativeArray<DeleteEntityEvent> eventArray = m_Query.ToComponentDataArray<DeleteEntityEvent>(Allocator.TempJob);

        ParallellDeleteQueue parallellDeleteQueue = new ParallellDeleteQueue
        {
            eventArray = eventArray,
            commandBuffer = beginPresentationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
        };

        inputDeps = parallellDeleteQueue.Schedule(eventArray.Length, 32, inputDeps);
        beginPresentationEntityCommandBufferSystem.AddJobHandleForProducer(inputDeps);

        return inputDeps;
    }
}

[DisableAutoCreation]
//[UpdateBefore(typeof(BuildMegaChunk))]
public class DeleteMegaChunk : JobComponentSystem
{
    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;
    private Camera mainCamera;

    private float3 lastbuildPos;

    private EntityQuery m_Query;

    [BurstCompile]
    private struct ParallellMegachunkJob : IJobParallelFor
    {
        [ReadOnly]
        [DeallocateOnJobCompletion] public NativeArray<MegaChunk> megaChunks;

        [ReadOnly]
        public float3 cameraPosition;
        [ReadOnly]
        public int radius;
        [ReadOnly]
        public int chunkSize;
        [ReadOnly]
        public BufferFromEntity<Child> lookup;

        public EntityCommandBuffer.Concurrent commandBuffer;

        public void Execute(int index)
        {
            if (math.distance(cameraPosition, megaChunks[index].center) >= radius * chunkSize)
            {
                //commandBuffer.DestroyEntity(index, entity);
                if (lookup.Exists(megaChunks[index].entity))
                {
                    NativeArray<Entity> array = lookup[megaChunks[index].entity].Reinterpret<Entity>().AsNativeArray();
                    for (int i = 0; i < array.Length; i++)
                    {
                        commandBuffer.AddComponent(index, array[i], new DeleteEntityEvent { entity = array[i] });
                    }
                }
                else
                {
                    commandBuffer.AddComponent(index, megaChunks[index].entity, new DeleteEntityEvent { entity = megaChunks[index].entity });
                }

            }
        }
    }

    private struct DeleteMegaChunkJob : IJobForEachWithEntity<MegaChunk>
    {
        [ReadOnly]
        public float3 cameraPosition;
        [ReadOnly]
        public int radius;
        [ReadOnly]
        public int chunkSize;
        [ReadOnly]
        public BufferFromEntity<Child> lookup;

        public EntityCommandBuffer.Concurrent commandBuffer;

        public void Execute(Entity entity, int index, ref MegaChunk megaChunk)
        {
            if (math.distance(cameraPosition, megaChunk.center) > radius * chunkSize)
            {
                //commandBuffer.DestroyEntity(index, entity);
                if (lookup.Exists(entity))
                {
                    NativeArray<Entity> array = lookup[entity].Reinterpret<Entity>().AsNativeArray();
                    for (int i = 0; i < array.Length; i++)
                    {
                        commandBuffer.AddComponent(index, array[i], new DeleteEntityEvent { entity = array[i] });
                    }
                }
                else
                {
                    commandBuffer.AddComponent(index, entity, new DeleteEntityEvent { entity = entity });
                }
                
            }
                
        }
    }

    protected override void OnCreate()
    {
        mainCamera = Camera.main;
        endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        lastbuildPos = mainCamera.transform.position;

        m_Query = GetEntityQuery(ComponentType.ReadOnly<MegaChunk>());

        base.OnCreate();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {

        float3 buildPosition = mainCamera.transform.position + mainCamera.transform.forward * 10;

        float3 movement = lastbuildPos - buildPosition;

        if (math.length(movement) > MeshComponents.chunkSize)
        {
            lastbuildPos = buildPosition;
            //DeleteMegaChunkJob deletingQueueJob = new DeleteMegaChunkJob
            //{
            //    cameraPosition = mainCamera.transform.position,
            //    chunkSize = MeshComponents.chunkSize,
            //    radius = MeshComponents.radius,
            //    commandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
            //    lookup = GetBufferFromEntity<Child>(true)
            //};

            //inputDeps = deletingQueueJob.Schedule(this, inputDeps);

            NativeArray<MegaChunk> megaChunks = m_Query.ToComponentDataArray<MegaChunk>(Allocator.TempJob);

            ParallellMegachunkJob parallellMegachunkJob = new ParallellMegachunkJob
            {
                cameraPosition = buildPosition,
                chunkSize = MeshComponents.chunkSize,
                radius = MeshComponents.radius,
                commandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
                lookup = GetBufferFromEntity<Child>(true),
                megaChunks = megaChunks
            };

            inputDeps = parallellMegachunkJob.Schedule(megaChunks.Length, 16, inputDeps);

            endSimulationEntityCommandBufferSystem.AddJobHandleForProducer(inputDeps);
        }

        return inputDeps;
    }
}