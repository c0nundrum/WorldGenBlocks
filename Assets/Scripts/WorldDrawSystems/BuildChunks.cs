using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using Unity.Physics;

[DisableAutoCreation]
[UpdateBefore(typeof(Drawchunk))]
public class BuildChunkJob : JobComponentSystem
{
    private Camera mainCamera;
    private float3 lastbuildPos;

    private readonly int bufferSize = 1;

    private EntityArchetype chunkArchetype;

    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;

    private Unity.Physics.Systems.BuildPhysicsWorld physicsWorldSystem;

    [BurstCompile]
    private struct BuildChunkAt : IJobParallelFor
    {
        [ReadOnly]
        [DeallocateOnJobCompletion]public NativeArray<int3> positionArray;
        [ReadOnly]
        public EntityArchetype archetype;
        [ReadOnly]
        public int chunkSize;
        [ReadOnly] public CollisionWorld world;       

        public EntityCommandBuffer.Concurrent commandBuffer;

        private bool shouldCreate;
        public unsafe void Execute(int index)
        {
            shouldCreate = true;
            if (positionArray[index].Equals(new int3(int.MinValue, 0 ,0))) return;

            float3 chunkPosition = new float3(positionArray[index].x * chunkSize, positionArray[index].y * chunkSize, positionArray[index].z * chunkSize);

            Unity.Physics.RaycastHit hit;

            //Need to set up collision layers properly
            RaycastInput input = new RaycastInput()
            {
                Filter = new CollisionFilter()
                {
                    BelongsTo = ~0u,
                    CollidesWith = ~0u, // all 1s, so all layers, collide with everything
                    GroupIndex = 0
                }
            };

            bool haveHit = world.CastRay(input, out hit);

            NativeArray<BlockType> blockTypeArray = GetBlockArray(chunkPosition);

            if (!haveHit && shouldCreate)
            {
                int3 position = new int3((int3)math.floor(chunkPosition));

                ChunkValue c = new ChunkValue { position = chunkPosition };

                Entity entity = commandBuffer.CreateEntity(index, archetype);

                commandBuffer.SetComponent(index, entity, new WorldChunk
                {
                    position = new int3((int)c.position.x, (int)c.position.y, (int)c.position.z),
                    status = ChunkStatus.KEEP
                });

                commandBuffer.SetComponent(index, entity, new Translation
                {
                    Value = chunkPosition
                });

                commandBuffer.SetComponent(index, entity, new Rotation { Value = quaternion.identity });

                BoxGeometry bm = new BoxGeometry
                {
                    BevelRadius = 0f,
                    Center = float3.zero,
                    Orientation = quaternion.identity,
                    Size = new float3(chunkSize)
                };

                commandBuffer.SetComponent(index, entity, new PhysicsCollider
                {
                    Value = Unity.Physics.BoxCollider.Create(bm)
                });

                commandBuffer.AddComponent(index, entity, new BuildMeshFlag { });

                var buffer = commandBuffer.AddBuffer<BlockTypeBuffer>(index, entity);
                buffer.Reinterpret<BlockType>().AddRange(blockTypeArray);
            }

        }

        private float Map(float newmin, float newmax, float originalMin, float originalMax, float value)
        {
            return math.lerp(newmin, newmax, math.unlerp(originalMin, originalMax, value));
        }

        private int GenerateHeight(float x, float z)
        {
            int maxHeight = 150;
            float smooth = 0.01f;
            int octaves = 4;
            float persistence = 0.5f;
            //Parameters should come in from the chunk
            float height = Map(0, maxHeight, 0, 1, FBM(x * smooth, z * smooth, octaves, persistence));
            return (int)height;
        }

        private int GenerateStoneHeight(float x, float z)
        {
            int maxHeight = 150;
            float smooth = 0.01f;
            int octaves = 4;
            float persistence = 0.5f;
            //Parameters should come in from the chunk
            float height = Map(0, maxHeight - 5, 0, 1, FBM(x * smooth * 2, z * smooth * 2, octaves + 1, persistence));
            return (int)height;
        }

        private float FBM(float x, float z, int oct, float pers)
        {
            float total = 0;
            float frequency = 1;
            float amplitude = 1;
            float maxValue = 0;
            float offset = 32000f;

            for (int i = 0; i < oct; i++)
            {
                total += noise.cnoise(new float2(x + offset * frequency, z + offset * frequency)) * amplitude;

                maxValue += amplitude;

                amplitude *= pers;
                frequency *= 2;
            }

            return total / maxValue;
        }

        private float FBM3D(float x, float y, float z, float sm, int oct)
        {
            float XY = FBM(x * sm, y * sm, oct, 0.5f);
            float YZ = FBM(y * sm, z * sm, oct, 0.5f);
            float XZ = FBM(x * sm, z * sm, oct, 0.5f);

            float YX = FBM(y * sm, x * sm, oct, 0.5f);
            float ZY = FBM(z * sm, y * sm, oct, 0.5f);
            float ZX = FBM(z * sm, x * sm, oct, 0.5f);

            return math.unlerp(-1, 1,((XY + YZ + XZ + YX + ZY + ZX) / 6.0f));
        }

        private NativeArray<BlockType> GetBlockArray(float3 position)
        {
            NativeArray<BlockType> blockArray = new NativeArray<BlockType>((int)math.pow(chunkSize, 3), Allocator.Temp); // this gets sent to dynamic buffer

            for (int z = 0; z < chunkSize; z++)
            {
                for (int y = 0; y < chunkSize; y++)
                {
                    for (int x = 0; x < chunkSize; x++)
                    {
                        float3 pos = new float3(x, y, z);
                        int worldX = (int)(x + position.x);
                        int worldY = (int)(y + position.y);
                        int worldZ = (int)(z + position.z);

                        if (FBM3D(worldX, worldY, worldZ, 0.1f, 3) < 0.48f)
                            blockArray[x + chunkSize * (y + chunkSize * z)] = BlockType.AIR;
                        else if (worldY <= GenerateStoneHeight(worldX, worldZ))
                            if (FBM3D(worldX, worldY, worldZ, 0.01f, 2) < 0.38f && worldY < 40)
                                blockArray[x + chunkSize * (y + chunkSize * z)] = BlockType.DIAMOND;
                            else
                                blockArray[x + chunkSize * (y + chunkSize * z)] = BlockType.STONE;
                        else if (worldY == GenerateHeight(worldX, worldZ))
                            blockArray[x + chunkSize * (y + chunkSize * z)] = BlockType.GRASS;
                        else if (worldY < GenerateHeight(worldX, worldZ))
                            blockArray[x + chunkSize * (y + chunkSize * z)] = BlockType.DIRT;
                        else
                        {
                            blockArray[x + chunkSize * (y + chunkSize * z)] = BlockType.AIR;
                            shouldCreate = false;
                        }
                            
                    }

                }
            }

            return blockArray;
        }
    }

    [BurstCompile]
    private struct GetPositionList : IJobParallelFor
    {
        [ReadOnly]
        public int3 cameraPosition;
        [WriteOnly]
        public NativeArray<int3> outputFront;
        [WriteOnly]
        public NativeArray<int3> outputBack;
        [WriteOnly]
        public NativeArray<int3> outputLeft;
        [WriteOnly]
        public NativeArray<int3> outputRight;
        [WriteOnly]
        public NativeArray<int3> outputUp;
        [WriteOnly]
        public NativeArray<int3> outputDown;

        public void Execute(int index)
        {
            //List Chunk Front
            outputFront[index] = new int3(cameraPosition.x, cameraPosition.y, cameraPosition.z + index);

            //List Chunk Back
            outputBack[index] = new int3(cameraPosition.x, cameraPosition.y, cameraPosition.z - index);

            //List Chunk left
            outputLeft[index] = new int3(cameraPosition.x - index, cameraPosition.y, cameraPosition.z);

            //List Chunk right
            outputRight[index] = new int3(cameraPosition.x + index, cameraPosition.y, cameraPosition.z);

            //List Chunk up
            outputUp[index] = new int3(cameraPosition.x, cameraPosition.y + index, cameraPosition.z);

            //Build Chunk Down
            outputDown[index] = new int3(cameraPosition.x, cameraPosition.y - index, cameraPosition.z);

        }
    }

    [BurstCompile]
    private struct CombineArray : IJob
    {
        [ReadOnly]
        [DeallocateOnJobCompletion]public NativeArray<int3> inputFront;
        [ReadOnly]
        [DeallocateOnJobCompletion] public NativeArray<int3> inputBack;
        [ReadOnly]
        [DeallocateOnJobCompletion] public NativeArray<int3> inputLeft;
        [ReadOnly]
        [DeallocateOnJobCompletion] public NativeArray<int3> inputRight;
        [ReadOnly]
        [DeallocateOnJobCompletion] public NativeArray<int3> inputUp;
        [ReadOnly]
        [DeallocateOnJobCompletion] public NativeArray<int3> inputDown;

        [WriteOnly]
        public NativeArray<int3> output; // Should be the length * 6

        public void Execute()
        {

            int length = inputFront.Length; //Arbitrary, all of them have the same length

            for (int index = 0; index < length; index++)
            {
                output[index] = inputFront[index];
                output[length + index] = inputBack[index];
                output[index + (length * 2)] = inputLeft[index];
                output[index + (length * 3)] = inputRight[index];
                output[index + (length * 4)] = inputUp[index];
                output[index + (length * 5)] = inputDown[index];
            }

        }
    }

    [BurstCompile]
    public struct EnsureUnique : IJob
    {
        [ReadOnly]
        [DeallocateOnJobCompletion] public NativeArray<int3> aliasedArray;

        public NativeArray<int3> output;

        public void Execute()
        {
            int counter = 0;
            for(int i = 0; i < aliasedArray.Length; i++)
            {
                
                if (!output.Contains(aliasedArray[i]))
                {
                    output[counter] = aliasedArray[i];
                    counter++;
                }
            }
        }
    }

    private JobHandle Buildworld(int x, int y, int z)
    {
        NativeArray<int3> PositionArray = new NativeArray<int3>(MeshComponents.radius * 6, Allocator.TempJob);
        NativeArray<int3> PositionUniqueArray = new NativeArray<int3>(MeshComponents.radius * 6 - 5, Allocator.TempJob);

        NativeArray<int3> outputBack = new NativeArray<int3>(MeshComponents.radius, Allocator.TempJob);
        NativeArray<int3> outputDown = new NativeArray<int3>(MeshComponents.radius, Allocator.TempJob);
        NativeArray<int3> outputFront = new NativeArray<int3>(MeshComponents.radius, Allocator.TempJob);
        NativeArray<int3> outputLeft = new NativeArray<int3>(MeshComponents.radius, Allocator.TempJob);
        NativeArray<int3> outputRight = new NativeArray<int3>(MeshComponents.radius, Allocator.TempJob);
        NativeArray<int3> outputUp = new NativeArray<int3>(MeshComponents.radius, Allocator.TempJob);

        GetPositionList getPositionList = new GetPositionList
        {
            cameraPosition = new int3(x, y, z),
            outputBack = outputBack,
            outputDown = outputDown,
            outputFront = outputFront,
            outputLeft = outputLeft,
            outputRight = outputRight,
            outputUp = outputUp
        };

        JobHandle getPositionListHandle = getPositionList.Schedule(outputBack.Length, 8);

        CombineArray combineArray = new CombineArray
        {
            inputBack = outputBack,
            inputDown = outputDown,
            inputFront = outputFront,
            inputLeft = outputLeft,
            inputRight = outputRight,
            inputUp = outputUp,
            output = PositionArray
        };

        JobHandle combineArrayJob = combineArray.Schedule(getPositionListHandle);

        EnsureUnique ensureUniqueJob = new EnsureUnique
        {
            aliasedArray = PositionArray,
            output = PositionUniqueArray
        };

        var ensureUniqueJobHandle = ensureUniqueJob.Schedule(combineArrayJob);

        physicsWorldSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystem<Unity.Physics.Systems.BuildPhysicsWorld>();

        BuildChunkAt job = new BuildChunkAt
        {
            archetype = chunkArchetype,
            commandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
            positionArray = PositionUniqueArray,
            chunkSize = MeshComponents.chunkSize,
            world = physicsWorldSystem.PhysicsWorld.CollisionWorld
        };

        var handle = job.Schedule(PositionUniqueArray.Length, 8, ensureUniqueJobHandle);

        handle.Complete();

        return ensureUniqueJobHandle;
    }

    public JobHandle BuildNearPlayer()
    {
        return 
            Buildworld((int)(mainCamera.transform.position.x / MeshComponents.chunkSize), (int)(mainCamera.transform.position.y / MeshComponents.chunkSize), (int)(mainCamera.transform.position.z / MeshComponents.chunkSize));
    }


    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    protected override void OnCreate()
    {

        chunkArchetype = EntityManager.CreateArchetype(typeof(WorldChunk), typeof(Translation), typeof(Rotation), typeof(PhysicsCollider));

        endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        mainCamera = Camera.main;

        lastbuildPos = mainCamera.transform.position;

        endSimulationEntityCommandBufferSystem.AddJobHandleForProducer(BuildNearPlayer());

        base.OnCreate();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        float3 movement = lastbuildPos - new float3(mainCamera.transform.position);

        if (math.length(movement) > MeshComponents.chunkSize * bufferSize)
        {          
            lastbuildPos = mainCamera.transform.position;
            inputDeps = BuildNearPlayer();
        }

        endSimulationEntityCommandBufferSystem.AddJobHandleForProducer(inputDeps);

        return inputDeps;
    }
}