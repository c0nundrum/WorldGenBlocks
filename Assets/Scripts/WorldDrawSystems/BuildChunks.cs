using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using Unity.Physics;
using System.Collections.Generic;

[DisableAutoCreation]
public class BuildMegaChunk : JobComponentSystem
{
    private EntityArchetype archetype;
    private Camera mainCamera;

    private void BuildMegaChunkAt(float3 position, int chunkSize) {

        Entity en = EntityManager.CreateEntity(archetype);
        EntityManager.SetComponentData(en, new MegaChunk { center = new float3(position.x * chunkSize, position.y * chunkSize, position.z * chunkSize), spawnCubes = true });

    }

    private void BuildInitialCube(float3 position, int chunkSize, int radius)
    {
        int diameter =  (int)math.floor(radius / 2);

        for (int x = (int)math.floor(position.x - diameter); x < (int)math.floor(position.x + diameter); x++)
            for (int y = (int)math.floor(position.y - diameter); y < (int)math.floor(position.y + diameter); y++)
                for (int z = (int)math.floor(position.z - diameter); z < (int)math.floor(position.z + diameter); z++)
                    BuildMegaChunkAt(new int3(x, y, z), MeshComponents.chunkSize);
    }

    protected override void OnCreate()
    {
        mainCamera = Camera.main;
        base.OnCreate();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        archetype = EntityManager.CreateArchetype(typeof(MegaChunk));

        BuildInitialCube(new int3((int)mainCamera.transform.position.x / MeshComponents.chunkSize, 
            (int)mainCamera.transform.position.y / MeshComponents.chunkSize, 
            (int)mainCamera.transform.position.z / MeshComponents.chunkSize), MeshComponents.chunkSize, MeshComponents.radius);
        
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        return inputDeps;
    }
}

public class FillChunks : JobComponentSystem
{
    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;
    private EntityArchetype archetype;

    [BurstCompile]
    public struct SpawnCubes : IJobForEachWithEntity<MegaChunk>
    {
        [ReadOnly]
        public EntityArchetype archetype;
        [ReadOnly]
        public int chunk;

        public EntityCommandBuffer.Concurrent commandBuffer;

        public void Execute(Entity en, int index, ref MegaChunk megaChunk)
        {
            if (megaChunk.spawnCubes)
            {
                BuildChunkCubes(megaChunk.center, index, chunk);

                megaChunk.spawnCubes = false;
            }
        }

        private void BuildChunkCubes(float3 position, int index, int chunkSize)
        {
            int diameter = (int)math.floor(chunkSize / 2);

            for (int x = (int)math.floor(position.x - diameter); x < (int)math.floor(position.x + diameter); x++)
                for (int y = (int)math.floor(position.y - diameter); y < (int)math.floor(position.y + diameter); y++)
                    for (int z = (int)math.floor(position.z - diameter); z < (int)math.floor(position.z + diameter); z++)
                        if(GetBlock(new float3(x, y, z)) != BlockType.AIR && ShouldDraw(new float3(x, y, z)))
                            BuildCubeAt(new int3(x, y, z), index);
        }

        private bool ShouldDraw(float3 position)
        {
            if (GetBlock(new float3(position.x, position.y, position.z + 1)) == BlockType.AIR || GetBlock(new float3(position.x, position.y, position.z - 1)) == BlockType.AIR)
                return true;
            if (GetBlock(new float3(position.x, position.y + 1, position.z)) == BlockType.AIR || GetBlock(new float3(position.x, position.y - 1, position.z)) == BlockType.AIR)
                return true;
            if (GetBlock(new float3(position.x + 1, position.y, position.z)) == BlockType.AIR || GetBlock(new float3(position.x - 1, position.y, position.z)) == BlockType.AIR)
                return true;

            return false;
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

            return math.unlerp(-1, 1, ((XY + YZ + XZ + YX + ZY + ZX) / 6.0f));
        }

        private void BuildCubeAt(float3 position, int index)
        {

            Entity en = commandBuffer.CreateEntity(index, archetype);
            commandBuffer.SetComponent(index, en, new CubePosition { position = position, type = BlockType.DIRT, HasCube = false } );

        }

        private BlockType GetBlock(float3 position)
        {

            BlockType block;
                              
            int worldX = (int)(position.x);
            int worldY = (int)(position.y);
            int worldZ = (int)(position.z);

            if (FBM3D(worldX, worldY, worldZ, 0.1f, 3) < 0.48f)
            {
                block = BlockType.AIR;
                //shouldCreate = false;
            }
            else if (worldY <= GenerateStoneHeight(worldX, worldZ))
                if (FBM3D(worldX, worldY, worldZ, 0.01f, 2) < 0.38f && worldY < 40)
                    block = BlockType.DIAMOND;
                else
                    block = BlockType.STONE;
            else if (worldY == GenerateHeight(worldX, worldZ))
                block = BlockType.GRASS;
            else if (worldY < GenerateHeight(worldX, worldZ))
                block = BlockType.DIRT;
            else
            {
                block = BlockType.AIR;            
            }

            return block;
        }

    }

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        archetype = EntityManager.CreateArchetype(typeof(CubePosition));
    }

    protected override void OnCreate()
    {

        endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        base.OnCreate();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        SpawnCubes spawncubesJob = new SpawnCubes
        {
            archetype = archetype,
            chunk = MeshComponents.chunkSize,
            commandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
        };

        inputDeps = spawncubesJob.Schedule(this, inputDeps);

        endSimulationEntityCommandBufferSystem.AddJobHandleForProducer(inputDeps);

        return inputDeps;
    }
}

[DisableAutoCreation]
public class BuildCubeJob : JobComponentSystem
{
    private NativeArray<int3> PositionsOffset;
    private NativeList<int3> arrayList;

    private EntityQuery m_Group;

    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;
    private Camera mainCamera;

    private EntityArchetype archetype;

    private float3 lastbuildPos;

    private readonly int bufferSize = 1;

    [BurstCompile]
    private struct CreateCubes : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<int3> PositionsOffset;
        [ReadOnly]
        public float3 translation;
        [ReadOnly]
        public int radius;
        [ReadOnly]
        public int chunkSize;
        [ReadOnly]
        public EntityArchetype archetype;

        public EntityCommandBuffer.Concurrent commandBuffer;

        public void Execute(int index)
        {
            //if (!cubePositions.Contains(new int3(PositionsOffset[index] + math.floor(translation))) && math.distance(PositionsOffset[index] + translation, translation) < radius * chunkSize)
            if (math.distance(PositionsOffset[index] + translation, translation) < radius * chunkSize)
            {
                Entity en = commandBuffer.CreateEntity(index, archetype);
                commandBuffer.SetComponent(index, en, new CubePosition { position = PositionsOffset[index] + math.floor(translation), type = BlockType.DIRT, HasCube = false });
            }
                
        }

        private float3 GetStartOffset(float3 position, int radius)
        {
            int offsetRadius = radius / 2;
            return new float3(position.x - offsetRadius, position.y - offsetRadius, position.z - offsetRadius);
        }

    }

    private struct GetPositionArray : IJobParallelFor
    {
        [ReadOnly]
        [DeallocateOnJobCompletion]public NativeArray<CubePosition> cubePositions;
        [WriteOnly]
        public NativeArray<int3> occupiedPositions;

        public void Execute(int index)
        {
            occupiedPositions[index] = new int3(math.floor(cubePositions[index].position));
        }
    }

    private void BuildRecursiveWorld(int x, int y, int z, int rad)
    {
        rad--;
        if (rad <= 0) return;

        //Build Chunk Front
        arrayList.Add(new int3(x, y, z + 1));
        BuildRecursiveWorld(x, y, z + 1, rad);

        //Build Chunk Back
        arrayList.Add(new int3(x, y, z - 1));
        BuildRecursiveWorld(x, y, z - 1, rad);

        //Build Chunk left
        arrayList.Add(new int3(x - 1, y, z));
        BuildRecursiveWorld(x - 1, y, z, rad);

        //Build Chunk right
        arrayList.Add(new int3(x + 1, y, z));
        BuildRecursiveWorld(x + 1, y, z, rad);

        //Build Chunk up
        arrayList.Add(new int3(x, y + 1, z));
        BuildRecursiveWorld(x, y + 1, z, rad);

        //Build Chunk Down
        arrayList.Add(new int3(x, y - 1, z));
        BuildRecursiveWorld(x, y - 1, z, rad);

    }

    private int3[] CreateOffsetArray()
    {

        int3[] arrayOffset = new int3[(int)math.pow(MeshComponents.radius, 3)];
        
        for (int x = 0; x < MeshComponents.radius; x++)
            for (int y = 0; y < MeshComponents.radius; y++)
                for (int z = 0; z < MeshComponents.radius; z++)
                    arrayOffset[x + MeshComponents.radius * (y + MeshComponents.radius * z)] = new int3(x, y, z);

        return arrayOffset;
    }

    protected override void OnCreate()
    {

        arrayList = new NativeList<int3>(Allocator.Persistent);
        BuildRecursiveWorld(0, 0, 0, MeshComponents.radius);
        PositionsOffset = new NativeArray<int3>(arrayList.ToArray(), Allocator.Persistent);

        arrayList.Dispose();

        mainCamera = Camera.main;
        endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        lastbuildPos = mainCamera.transform.position;

        base.OnCreate();
    }

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        archetype = EntityManager.CreateArchetype(typeof(CubePosition));
    }

    protected override void OnDestroy()
    {
        PositionsOffset.Dispose();
        base.OnDestroy();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {

        float3 movement = lastbuildPos - new float3(mainCamera.transform.position);

        //if (math.length(movement) > MeshComponents.radius * bufferSize)
        if (math.length(movement) > MeshComponents.radius * MeshComponents.chunkSize * bufferSize)
        {
            lastbuildPos = mainCamera.transform.position;

            CreateCubes cubeJob = new CreateCubes
            {
                translation = mainCamera.transform.position,
                chunkSize = MeshComponents.chunkSize,
                radius = MeshComponents.radius,
                commandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
                archetype = archetype,
                PositionsOffset = PositionsOffset
            };

            inputDeps = cubeJob.Schedule(PositionsOffset.Length, 16, inputDeps);

            endSimulationEntityCommandBufferSystem.AddJobHandleForProducer(inputDeps);
        }

        return inputDeps;
    }
}

[DisableAutoCreation]
[UpdateBefore(typeof(BuildMeshSystem))]
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
        [DeallocateOnJobCompletion]
        public NativeArray<int3> positionArray;
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
                Start = chunkPosition,
                End = chunkPosition,
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
                    //Center = float3.zero,
                    Center = new float3(chunkSize / 2 - 0.5f, chunkSize / 2 - 0.5f, chunkSize / 2 - 0.5f),
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
                        {
                            blockArray[x + chunkSize * (y + chunkSize * z)] = BlockType.AIR;
                            //shouldCreate = false;
                        }                            
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

    //REEEALLY SLOW
    [BurstCompile]
    private struct FloodFill : IJob
    {
        [WriteOnly]
        public NativeList<int3> positionList;

        [ReadOnly]
        public int3 cameraPosition;
        [ReadOnly]
        public int Radius;

        public void Execute()
        {
            addList(cameraPosition.x, cameraPosition.y, cameraPosition.x, Radius);
        }

        private void addList(int x, int y, int z, int rad)
        {
            rad--;
            if (rad <= 0) return;

            //Build Chunk Front
            positionList.Add(new int3(x, y, z + 1));
            addList(x, y, z + 1, rad);

            //Build Chunk Back
            positionList.Add(new int3(x, y, z - 1));
            addList(x, y, z - 1, rad);

            //Build Chunk left
            positionList.Add(new int3(x - 1, y, z));
            addList(x - 1, y, z, rad);

            //Build Chunk right
            positionList.Add(new int3(x + 1, y, z));
            addList(x + 1, y, z, rad);

            //Build Chunk up
            positionList.Add(new int3(x, y + 1, z));
            addList(x, y + 1, z, rad);

            //Build Chunk Down
            positionList.Add(new int3(x, y - 1, z));
            addList(x, y - 1, z, rad);
        }
    };

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

        //NativeList<int3> PositionUniqueArray = new NativeList<int3>(Allocator.TempJob);

        //FloodFill floodFillJob = new FloodFill
        //{
        //    cameraPosition = new int3(x, y, z),
        //    positionList = PositionUniqueArray,
        //    Radius = MeshComponents.radius
        //};

        //var floodFillJobHandle = floodFillJob.Schedule();

        //floodFillJobHandle.Complete();

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
        //var handle = job.Schedule(PositionUniqueArray.Length, 8, floodFillJobHandle);

        handle.Complete();
        //PositionUniqueArray.Dispose();

        return ensureUniqueJobHandle;
        //return floodFillJobHandle;
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