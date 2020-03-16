using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using Unity.Transforms;

[DisableAutoCreation]
[UpdateBefore(typeof(BuildMegaChunk))]
public class DeleteMegaChunk : JobComponentSystem
{
    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;
    private Camera mainCamera;

    private float3 lastbuildPos;

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
                commandBuffer.DestroyEntity(index, entity);
            }
                
        }
    }

    protected override void OnCreate()
    {
        mainCamera = Camera.main;
        endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        lastbuildPos = mainCamera.transform.position;

        base.OnCreate();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        float3 movement = lastbuildPos - new float3(mainCamera.transform.position);

        if (math.length(movement) > MeshComponents.chunkSize)
        {
            lastbuildPos = mainCamera.transform.position;
            DeleteMegaChunkJob deletingQueueJob = new DeleteMegaChunkJob
            {
                cameraPosition = mainCamera.transform.position,
                chunkSize = MeshComponents.chunkSize,
                radius = MeshComponents.radius,
                commandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
            };

            inputDeps = deletingQueueJob.Schedule(this, inputDeps);

            endSimulationEntityCommandBufferSystem.AddJobHandleForProducer(inputDeps);
        }

        return inputDeps;
    }
}

[DisableAutoCreation]
public class DeleteCubes : JobComponentSystem
{
    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;
    private Camera mainCamera;

    private float3 lastbuildPos;

    private readonly int bufferSize = 1;

    [BurstCompile]
    [RequireComponentTag(typeof(CubeFlag))]
    private struct DeletingQueue : IJobForEachWithEntity<Translation>
    {
        [ReadOnly]
        public float3 cameraPosition;
        [ReadOnly]
        public int radius;
        [ReadOnly]
        public int chunkSize;

        public EntityCommandBuffer.Concurrent commandBuffer;

        public void Execute(Entity entity, int index, ref Translation translation)
        {
            if (math.distance(cameraPosition, translation.Value) > radius * chunkSize)
                commandBuffer.DestroyEntity(index, entity);
        }
    }

    protected override void OnCreate()
    {
        mainCamera = Camera.main;
        endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        lastbuildPos = mainCamera.transform.position;

        base.OnCreate();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        float3 movement = lastbuildPos - new float3(mainCamera.transform.position);

        if (math.length(movement) > MeshComponents.radius * MeshComponents.chunkSize * bufferSize)
        {
            lastbuildPos = mainCamera.transform.position;
            DeletingQueue deletingQueueJob = new DeletingQueue
            {
                cameraPosition = mainCamera.transform.position,
                chunkSize = MeshComponents.chunkSize,
                radius = MeshComponents.radius,
                commandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
            };

            inputDeps = deletingQueueJob.Schedule(this);

            endSimulationEntityCommandBufferSystem.AddJobHandleForProducer(inputDeps);
        }

        return inputDeps;
    }
}

[DisableAutoCreation]
[UpdateBefore(typeof(BuildChunkJob))]
public class DeleteChunks : JobComponentSystem
{
    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;
    private Camera mainCamera;

    private float3 lastbuildPos;

    private readonly int bufferSize = 1;

    [BurstCompile]
    private struct DeletingQueue : IJobForEachWithEntity<WorldChunk>
    {
        [ReadOnly]
        public float3 cameraPosition;
        [ReadOnly]
        public int radius;
        [ReadOnly]
        public int chunkSize;

        public EntityCommandBuffer.Concurrent commandBuffer;

        public void Execute(Entity entity, int index, ref WorldChunk worldChunk)
        {
            if (math.distance(cameraPosition, worldChunk.position) > radius * chunkSize)
                commandBuffer.DestroyEntity(index, entity);
        }
    }


    protected override void OnCreate()
    {
        mainCamera = Camera.main;
        endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        lastbuildPos = mainCamera.transform.position;

        base.OnCreate();
    }

    protected override void OnDestroy()
    {
        //chunkEngine.Dispose();
        base.OnDestroy();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        DeletingQueue deletingQueueJob = new DeletingQueue
        {
            cameraPosition = mainCamera.transform.position,
            chunkSize = MeshComponents.chunkSize,
            radius = MeshComponents.radius,
            commandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
        };

        inputDeps = deletingQueueJob.Schedule(this);

        endSimulationEntityCommandBufferSystem.AddJobHandleForProducer(inputDeps);

        return inputDeps;
    }
}
