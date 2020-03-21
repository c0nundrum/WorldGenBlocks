using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using Unity.Physics;
using Unity.Rendering;
using RaycastHit = Unity.Physics.RaycastHit;

//public class MoveMegaChunk : JobComponentSystem
//{
//    //[BurstCompile] //Burst makes this slower?!
//    [RequireComponentTag(typeof(MegaChunk))]
//    public struct MoveChunksTest : IJobForEach<Translation>
//    {
//        public float DeltaTime;

//        public void Execute(ref Translation translation)
//        {
//            translation.Value += 1 * DeltaTime;
//        }
//    }

//    protected override JobHandle OnUpdate(JobHandle inputDeps)
//    {
//        return new MoveChunksTest { DeltaTime = Time.DeltaTime }.Schedule(this, inputDeps);
//    }
//}

[DisableAutoCreation]
public class BuildMegaChunk : JobComponentSystem
{
    private EntityArchetype archetype;
    private Camera mainCamera;

    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;

    private float3 lastbuildPos;

    private BoxGeometry bm;

    [BurstCompile]
    private struct BuildParallelChunks : IJobParallelFor
    {
        [ReadOnly]
        public float3 position;
        [ReadOnly]
        public int chunkSize;
        [ReadOnly]
        public EntityArchetype archetype;
        [ReadOnly]
        public BoxGeometry bm;
        [ReadOnly]
        public int radius;

        public EntityCommandBuffer.Concurrent commandBuffer;

        public void Execute(int index)
        {
            int diameter = (int)math.floor(radius / 2);

            int2 Position2D = GetPositionFromIndex(index, radius);

            int x = (int)math.floor(position.x + Position2D.x - diameter) * chunkSize;
            int z = (int)math.floor(position.y + Position2D.y - diameter) * chunkSize;
            int y = GenerateHeight(x, z);

            BuildChunkAt(new float3(x, y, z), index);
        }

        private void BuildChunkAt(float3 chunkPos, int index)
        {
            Entity en = commandBuffer.CreateEntity(index, archetype);
            commandBuffer.SetComponent(index, en, new MegaChunk
            {
                center = chunkPos,
                spawnCubes = true,
                entity = en
            });
            commandBuffer.SetComponent(index, en, new Translation { Value = chunkPos });
            commandBuffer.SetComponent(index, en, new Rotation { Value = quaternion.identity });
            commandBuffer.SetComponent(index, en, new LocalToWorld { });
            commandBuffer.AddComponent(index, en, new PhysicsCollider
            {
                Value = Unity.Physics.BoxCollider.Create(bm)
            });
        }

        private int2 GetPositionFromIndex(int index, int radius)
        {
            int x = index % radius;
            int y = index / radius;

            return new int2(x, y);
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

        private float Map(float newmin, float newmax, float originalMin, float originalMax, float value)
        {
            return math.lerp(newmin, newmax, math.unlerp(originalMin, originalMax, value));
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

    }

    [BurstCompile]
    private struct BuildChunks : IJobParallelFor
    {
        [ReadOnly]
        [DeallocateOnJobCompletion]public NativeArray<float3> Positions;
        [ReadOnly]
        public EntityArchetype archetype;
        [ReadOnly]
        public int chunkSize;
        [ReadOnly]
        public BoxGeometry bm;

        public EntityCommandBuffer.Concurrent commandBuffer;

        public void Execute(int index)
        {
            Entity en = commandBuffer.CreateEntity(index, archetype);
            commandBuffer.SetComponent(index, en, new MegaChunk
            {
                center = Positions[index],
                spawnCubes = true,
                entity = en
            });
            //commandBuffer.SetComponent(index, en, new Translation { Value = new float3(Positions[index].x * chunkSize, Positions[index].y * chunkSize, Positions[index].z * chunkSize) });
            commandBuffer.SetComponent(index, en, new Translation { Value = Positions[index] });
            commandBuffer.SetComponent(index, en, new Rotation { Value = quaternion.identity });
            commandBuffer.SetComponent(index, en, new LocalToWorld { });
            commandBuffer.AddComponent(index, en, new PhysicsCollider
            {
                Value = Unity.Physics.BoxCollider.Create(bm)
            });

            //commandBuffer.AddBuffer<LinkedEntityGroup>(index, en);

        }
    }

    private void BuildMegaChunkAt(float3 position, int chunkSize) {

        Entity en = EntityManager.CreateEntity(archetype);
        EntityManager.SetComponentData(en, new MegaChunk {
            center = new float3(position.x * chunkSize, position.y * chunkSize, position.z * chunkSize),
            spawnCubes = true,
            entity = en
        });
        EntityManager.SetComponentData(en, new Translation { Value = new float3(position.x * chunkSize, position.y * chunkSize, position.z * chunkSize) });
        EntityManager.SetComponentData(en, new Rotation { Value = quaternion.identity });
        EntityManager.SetComponentData(en, new LocalToWorld {  });
        EntityManager.AddComponentData(en, new PhysicsCollider
        {
            Value = Unity.Physics.BoxCollider.Create(bm)
        });

        //EntityManager.AddBuffer<LinkedEntityGroup>(en);
    }

    private void BuildInitialCube(float3 position, int chunkSize, int radius)
    {
        int diameter =  (int)math.floor(radius / 2);

        for (int x = (int)math.floor(position.x - diameter); x < (int)math.floor(position.x + diameter); x++)
            for (int y = (int)math.floor(position.y - diameter); y < (int)math.floor(position.y + diameter); y++)
                for (int z = (int)math.floor(position.z - diameter); z < (int)math.floor(position.z + diameter); z++)
                    BuildMegaChunkAt(new int3(x, y, z), MeshComponents.chunkSize);
    }

    [BurstCompile]
    private struct BuildChunkPositionsUnder : IJob
    {
        [ReadOnly]
        public float3 position;
        [ReadOnly]
        public int chunkSize;
        [ReadOnly]
        public int radius;

        [WriteOnly]
        public NativeList<float3> positions;

        public void Execute()
        {
            int diameter = (int)math.floor(radius / 2);

            float3 actualpos = math.floor(position);

            //positions.Add(new float3(actualpos.x * chunkSize, GenerateHeight(actualpos.x * chunkSize, actualpos.z * chunkSize), actualpos.z * chunkSize));

            for (int x = (int)math.floor(actualpos.x - diameter); x < (int)math.floor(actualpos.x + diameter); x++)
                for (int z = (int)math.floor(actualpos.z - diameter); z < (int)math.floor(actualpos.z + diameter); z++)
                    positions.Add(new float3(x * chunkSize, GenerateHeight(x  * chunkSize, z * chunkSize), z * chunkSize));
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

        private float Map(float newmin, float newmax, float originalMin, float originalMax, float value)
        {
            return math.lerp(newmin, newmax, math.unlerp(originalMin, originalMax, value));
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

    };

    [BurstCompile]
    private struct BuildChunksPositions : IJob
    {
        
        public CollisionWorld collisionWorld;
        [ReadOnly]
        public float3 position;
        [ReadOnly]
        public int chunkSize;
        [ReadOnly]
        public int radius;

        [WriteOnly]
        public NativeList<float3> positions;

        public void Execute()
        {
            int diameter = (int)math.floor(radius / 2);

            for (int x = (int)math.floor(position.x - diameter); x < (int)math.floor(position.x + diameter); x += (radius - 1))
                for (int y = (int)math.floor(position.y - diameter); y < (int)math.floor(position.y + diameter); y++)
                    for (int z = (int)math.floor(position.z - diameter); z < (int)math.floor(position.z + diameter); z++)
                    {
                        RaycastInput input = new RaycastInput()
                        {
                            Start = new float3(x, y, z),
                            End = new float3(x, y, z),
                            Filter = new CollisionFilter()
                            {
                                BelongsTo = ~0u,
                                CollidesWith = ~0u, // all 1s, so all layers, collide with everything
                                GroupIndex = 0
                            }
                        };

                        RaycastHit hit = new RaycastHit();
                        bool haveHit = collisionWorld.CastRay(input, out hit);
                        if (!haveHit)
                            positions.Add(new float3(x, y, z));
                    }

            for (int z = (int)math.floor(position.z - diameter); z < (int)math.floor(position.z + diameter); z += (radius - 1))
                for (int x = (int)math.floor(position.x - diameter); x < (int)math.floor(position.x + diameter); x++)
                    for (int y = (int)math.floor(position.y - diameter); y < (int)math.floor(position.y + diameter); y++)
                    {
                        RaycastInput input = new RaycastInput()
                        {
                            Start = new float3(x, y, z),
                            End = new float3(x, y, z),
                            Filter = new CollisionFilter()
                            {
                                BelongsTo = ~0u,
                                CollidesWith = ~0u, // all 1s, so all layers, collide with everything
                                GroupIndex = 0
                            }
                        };

                        RaycastHit hit = new RaycastHit();
                        bool haveHit = collisionWorld.CastRay(input, out hit);
                        if (!haveHit)
                            positions.Add(new float3(x, y, z));
                    }

            for (int y = (int)math.floor(position.y - diameter); y < (int)math.floor(position.y + diameter); y += (radius - 1))
                for (int x = (int)math.floor(position.x - diameter); x < (int)math.floor(position.x + diameter); x++)
                    for (int z = (int)math.floor(position.z - diameter); z < (int)math.floor(position.z + diameter); z++)
                    {
                        RaycastInput input = new RaycastInput()
                        {
                            Start = new float3(x, y, z),
                            End = new float3(x, y, z),
                            Filter = new CollisionFilter()
                            {
                                BelongsTo = ~0u,
                                CollidesWith = ~0u, // all 1s, so all layers, collide with everything
                                GroupIndex = 0
                            }
                        };

                        RaycastHit hit = new RaycastHit();
                        bool haveHit = collisionWorld.CastRay(input, out hit);
                        if (!haveHit)
                            positions.Add(new float3(x, y, z));
                    }

        }
    }

    protected override void OnCreate()
    {
        mainCamera = Camera.main;
        lastbuildPos = mainCamera.transform.position;
        endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        bm = new BoxGeometry
        {
            BevelRadius = 0f,
            Center = new float3(float3.zero - 0.5f),
            Orientation = quaternion.identity,
            Size = new float3(MeshComponents.chunkSize)
        };

        base.OnCreate();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        archetype = EntityManager.CreateArchetype(typeof(MegaChunk), typeof(Translation), typeof(Rotation), typeof(LocalToWorld));

        float3 buildPosition = mainCamera.transform.position + mainCamera.transform.forward * 10;

        //BuildInitialCube(new int3((int)mainCamera.transform.position.x / MeshComponents.chunkSize,
        //    (int)mainCamera.transform.position.y / MeshComponents.chunkSize,
        //    (int)mainCamera.transform.position.z / MeshComponents.chunkSize), MeshComponents.chunkSize, MeshComponents.radius);
        //BuildInitialCube(new int3((int)buildPosition.x / MeshComponents.chunkSize,
        //    (int)buildPosition.y / MeshComponents.chunkSize,
        //    (int)buildPosition.z / MeshComponents.chunkSize), MeshComponents.chunkSize, MeshComponents.radius);

    }

    private bool OnScreen(float3 position)
    {
        Vector3 screenPoint = mainCamera.WorldToViewportPoint(position);
        Debug.Log(screenPoint);
        return screenPoint.z > 0 && screenPoint.x > 0 && screenPoint.x < 1 && screenPoint.y > 0 && screenPoint.y < 1;
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        float3 buildPosition = mainCamera.transform.position + mainCamera.transform.forward * 10;

        float3 movement = lastbuildPos - new float3(mainCamera.transform.position);
        //float3 movement = lastbuildPos - new float3(buildPosition);

        if (math.length(movement) > MeshComponents.chunkSize)
        {
            //var physicsWorldSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystem<Unity.Physics.Systems.BuildPhysicsWorld>();
            //var collisionWorld = physicsWorldSystem.PhysicsWorld.CollisionWorld;

            //NativeList<float3> positions = new NativeList<float3>(Allocator.Persistent);

            //BuildChunkPositionsUnder buildChunksPositions = new BuildChunkPositionsUnder
            //{
            //    chunkSize = MeshComponents.chunkSize,
            //    position = new int3((int)mainCamera.transform.position.x / MeshComponents.chunkSize, (int)mainCamera.transform.position.y / MeshComponents.chunkSize, (int)mainCamera.transform.position.z / MeshComponents.chunkSize),
            //    //position = new int3((int)mainCamera.transform.position.x, (int)mainCamera.transform.position.y, (int)mainCamera.transform.position.z),
            //    //position = new int3((int)buildPosition.x / MeshComponents.chunkSize, (int)buildPosition.y / MeshComponents.chunkSize, (int)buildPosition.z / MeshComponents.chunkSize),
            //    radius = MeshComponents.radius,
            //    positions = positions
            //};

            //BuildChunksPositions buildChunksPositions = new BuildChunksPositions
            //{
            //    collisionWorld = collisionWorld,
            //    chunkSize = MeshComponents.chunkSize,
            //    //position = new int3((int)mainCamera.transform.position.x / MeshComponents.chunkSize, (int)mainCamera.transform.position.y / MeshComponents.chunkSize, (int)mainCamera.transform.position.z / MeshComponents.chunkSize),
            //    position = new int3((int)buildPosition.x / MeshComponents.chunkSize, (int)buildPosition.y / MeshComponents.chunkSize, (int)buildPosition.z / MeshComponents.chunkSize),
            //    radius = MeshComponents.radius,
            //    positions = positions
            //};

            //inputDeps = buildChunksPositions.Schedule(inputDeps);

            //inputDeps.Complete();

            //NativeArray<float3> positionArray = new NativeArray<float3>(positions.ToArray(), Allocator.TempJob);

            //positions.Dispose();

            //BuildChunks buildChunks = new BuildChunks
            //{
            //    archetype = archetype,
            //    chunkSize = MeshComponents.chunkSize,
            //    commandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
            //    Positions = positionArray,
            //    bm = bm
            //};

            //inputDeps = buildChunks.Schedule(positionArray.Length, 16, inputDeps);

            BuildParallelChunks buildParallelChunks = new BuildParallelChunks
            {
                archetype = archetype,
                chunkSize = MeshComponents.chunkSize,
                commandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
                bm = bm,
                position = mainCamera.transform.position,
                radius = MeshComponents.radius
            };

            inputDeps = buildParallelChunks.Schedule(MeshComponents.radius * MeshComponents.radius, 8, inputDeps);

            endSimulationEntityCommandBufferSystem.AddJobHandleForProducer(inputDeps);

            lastbuildPos = mainCamera.transform.position;
            //lastbuildPos = buildPosition;

            
        }

        return inputDeps;
    }
}

public class FillChunks : JobComponentSystem
{
    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;
    private EntityArchetype archetype;

    [BurstCompile]
    public struct SetUpdateCubes : IJobForEachWithEntity<MegaChunk>
    {
        public bool shouldDraw;

        public void Execute(Entity entity, int index, ref MegaChunk megaChunk)
        {
            megaChunk.spawnCubes = shouldDraw;
        }

    }

    [BurstCompile]
    public struct SpawnCubesParallel : IJobParallelFor
    {
        [ReadOnly]
        [DeallocateOnJobCompletion]public MegaChunk input;

        [ReadOnly]
        public EntityArchetype archetype;
        [ReadOnly]
        public int chunk;
        [ReadOnly]
        public int chunkSize;

        public EntityCommandBuffer.Concurrent commandBuffer;
        public void Execute(int index)
        {
            if (input.spawnCubes)
            {
                int3 position = GetPositionFromIndex(index);
                //if (GetBlock(position + input.center) != BlockType.AIR && ShouldDraw(position + input.center)) //Need the worldspace
                //if (GetBlock(position + input.center) == BlockType.GRASS && ShouldDraw(position + input.center)) //Need the worldspace
                if (GetBlock(new float3(position - (chunkSize / 2) + input.center)) <= BlockType.STONE) //Need the worldspace
                    BuildCubeAt(position, index, input.entity);
            }
        }

        private int3 GetPositionFromIndex(int index)
        {
            int z = index / (chunkSize * chunkSize);
            index -= (z * chunkSize * chunkSize);
            int y = index / chunkSize;
            int x = index % chunkSize;

            return new int3(x, y, z);
        }

        private Entity BuildCubeAt(float3 position, int index, Entity parent)
        {
            Entity en = commandBuffer.CreateEntity(index, archetype);
            commandBuffer.SetComponent(index, en, new CubePosition { position = position - (chunkSize / 2), type = BlockType.DIRT, HasCube = false, owner = en, parent = parent });

            return en;
        }

        private BlockType GetBlock(float3 position)
        {

            BlockType block;

            int worldX = (int)(position.x);
            int worldY = (int)(position.y);
            int worldZ = (int)(position.z);

            //Caves
            //if (FBM3D(worldX, worldY, worldZ, 0.1f, 3) < 0.48f)
            //{
            //    block = BlockType.AIR;
            //    //shouldCreate = false;
            //}
            //else 
            if (worldY <= GenerateStoneHeight(worldX, worldZ))
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

    }

    [BurstCompile]
    public struct SpawnCubes : IJobForEachWithEntity<MegaChunk>
    {
        [ReadOnly]
        public EntityArchetype archetype;
        [ReadOnly]
        public int chunk;

        [WriteOnly]
        public NativeArray<MegaChunk> output;
        public EntityCommandBuffer.Concurrent commandBuffer;

        public void Execute(Entity en, int index, ref MegaChunk megaChunk)
        {
            if (megaChunk.spawnCubes)
            {
                //BuildChunkCubes(megaChunk.center, index, chunk, en);
                output[index] = megaChunk;
                megaChunk.spawnCubes = false;
            }
        }

        private void BuildChunkCubes(float3 position, int index, int chunkSize, Entity parent)
        {
            int diameter = (int)math.floor(chunkSize / 2);

            //Position in worldSpace, with no parenting
            //for (int x = (int)math.floor(position.x - diameter); x < (int)math.floor(position.x + diameter); x++)
            //    for (int y = (int)math.floor(position.y - diameter); y < (int)math.floor(position.y + diameter); y++)
            //        for (int z = (int)math.floor(position.z - diameter); z < (int)math.floor(position.z + diameter); z++)
            //            if(GetBlock(new float3(x, y, z)) != BlockType.AIR && ShouldDraw(new float3(x, y, z)))
            //                BuildCubeAt(new int3(x, y, z), index, parent);

            //Position in localspace, due to parenting
            for (int x = - diameter; x <  diameter; x++)
                for (int y = - diameter; y <  diameter; y++)
                    for (int z =  - diameter; z <  diameter; z++)
                        if (GetBlock(new float3(x, y, z) + position) != BlockType.AIR && ShouldDraw(new float3(x, y, z) + position)) //Need the worldspace
                            BuildCubeAt(new int3(x, y, z), index, parent);


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

        private Entity BuildCubeAt(float3 position, int index, Entity parent)
        {

            Entity en = commandBuffer.CreateEntity(index, archetype);
            commandBuffer.SetComponent(index, en, new CubePosition { position = position, type = BlockType.DIRT, HasCube = false, owner = en, parent = parent } );
            //commandBuffer.AddComponent(index, en, new Parent { Value =  parent});


            //var buffer = commandBuffer.AddBuffer<LinkedEntityGroup>(index, parent);
            //buffer.Add(en);

            return en;
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

    private EntityQuery m_Query;

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        archetype = EntityManager.CreateArchetype(typeof(CubePosition), typeof(Parent), typeof(Translation), typeof(Rotation), typeof(LocalToWorld),
            typeof(LocalToParent), typeof(RenderBounds), typeof(PerInstanceCullingTag));
    }

    protected override void OnCreate()
    {

        endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        m_Query = GetEntityQuery(typeof(MegaChunk));

        base.OnCreate();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        //SpawnCubes spawncubesJob = new SpawnCubes
        //{
        //    archetype = archetype,
        //    chunk = MeshComponents.chunkSize,
        //    commandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
        //    output = 
        //};
        //inputDeps = spawncubesJob.Schedule(this, inputDeps);

        NativeArray<MegaChunk> megaChunks = m_Query.ToComponentDataArray< MegaChunk>(Allocator.TempJob);

        for (int i = 0; i < megaChunks.Length; i++)
        {
            MegaChunk chunk = megaChunks[i];
            if (megaChunks[i].spawnCubes)
            {
                SpawnCubesParallel spawnCubesParallel = new SpawnCubesParallel
                {
                    archetype = archetype,
                    chunk = MeshComponents.chunkSize,
                    commandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
                    input = chunk,
                    chunkSize = MeshComponents.chunkSize
                };

                inputDeps = spawnCubesParallel.Schedule(MeshComponents.chunkSize * MeshComponents.chunkSize * MeshComponents.chunkSize, 8, inputDeps);

                endSimulationEntityCommandBufferSystem.AddJobHandleForProducer(inputDeps);
            }
  
        }
        SetUpdateCubes setCubeUpdateJob = new SetUpdateCubes
        {
            shouldDraw = false
        };

        inputDeps = setCubeUpdateJob.Schedule(this, inputDeps);

        megaChunks.Dispose();

        return inputDeps;
    }
}