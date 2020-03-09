using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Physics;
using Unity.Burst;
using Unity.Jobs;
using Unity.Rendering;
using Unity.Transforms;
using RaycastHit = Unity.Physics.RaycastHit;

[DisableAutoCreation]
public class BuildMeshSystem : ComponentSystem
{
    private enum Cubeside { BOTTOM, TOP, LEFT, FRONT, BACK, RIGHT };

    private const float ATLAS_SIZE = 0.03125f;

    private readonly Vector2[,] blockUVs = {
        /*GRASS TOP*/ {
                new Vector2(19 * ATLAS_SIZE, 29 * ATLAS_SIZE), new Vector2(20 * ATLAS_SIZE, 29 * ATLAS_SIZE),
                                    new Vector2(19 * ATLAS_SIZE, 28 * ATLAS_SIZE), new Vector2(20 * ATLAS_SIZE, 28 * ATLAS_SIZE)},
        /*GRASS SIDE*/ {
                new Vector2(19 * ATLAS_SIZE, 28 * ATLAS_SIZE), new Vector2(20 * ATLAS_SIZE, 28 * ATLAS_SIZE),
                                new Vector2(19 * ATLAS_SIZE, 27 * ATLAS_SIZE), new Vector2(20 * ATLAS_SIZE, 27 * ATLAS_SIZE)},
        /*DIRT*/ {
                new Vector2(18 * ATLAS_SIZE, 32 * ATLAS_SIZE), new Vector2(19 * ATLAS_SIZE, 32 * ATLAS_SIZE),
                                new Vector2(18 * ATLAS_SIZE, 31 * ATLAS_SIZE), new Vector2(19 * ATLAS_SIZE, 31 * ATLAS_SIZE)},
        /*STONE*/ {
                new Vector2(20 * ATLAS_SIZE, 26 * ATLAS_SIZE), new Vector2(21 * ATLAS_SIZE, 26 * ATLAS_SIZE),
                                new Vector2(20 * ATLAS_SIZE, 25 * ATLAS_SIZE), new Vector2(21 * ATLAS_SIZE, 25 * ATLAS_SIZE)},
        /*DIAMOND*/ {
                new Vector2(0 * ATLAS_SIZE, 1 * ATLAS_SIZE), new Vector2(1 * ATLAS_SIZE, 1 * ATLAS_SIZE),
                                new Vector2(1 * ATLAS_SIZE, 0 * ATLAS_SIZE), new Vector2(1 * ATLAS_SIZE, 1 * ATLAS_SIZE)}
        };

    private Mesh CreateQuads(Cubeside side, BlockType bType, Vector3 worldChunkPosition)
    {
        Mesh mesh = new Mesh();
        mesh.name = "ScriptedMesh" + side.ToString();

        Vector3[] vertices = new Vector3[4];
        Vector3[] normals = new Vector3[4];
        Vector2[] uvs = new Vector2[4];

        int[] triangles = new int[6];

        Vector2 uv00;
        Vector2 uv10;
        Vector2 uv01;
        Vector2 uv11;

        if (bType == BlockType.GRASS && side == Cubeside.TOP)
        {
            uv00 = blockUVs[0, 0];
            uv10 = blockUVs[0, 1];
            uv01 = blockUVs[0, 2];
            uv11 = blockUVs[0, 3];

        }
        else if (bType == BlockType.GRASS && side == Cubeside.BOTTOM)
        {
            uv00 = blockUVs[(int)(BlockType.DIRT + 1), 0];
            uv10 = blockUVs[(int)(BlockType.DIRT + 1), 1];
            uv01 = blockUVs[(int)(BlockType.DIRT + 1), 2];
            uv11 = blockUVs[(int)(BlockType.DIRT + 1), 3];
        }
        else
        {
            uv00 = blockUVs[(int)(bType + 1), 0];
            uv10 = blockUVs[(int)(bType + 1), 1];
            uv01 = blockUVs[(int)(bType + 1), 2];
            uv11 = blockUVs[(int)(bType + 1), 3];
        }

        //All vertices
        Vector3 p0 = new Vector3(-.5f, -.5f, .5f) + worldChunkPosition;
        Vector3 p1 = new Vector3(.5f, -.5f, .5f) + worldChunkPosition;
        Vector3 p2 = new Vector3(.5f, -.5f, -.5f) + worldChunkPosition;
        Vector3 p3 = new Vector3(-.5f, -.5f, -.5f) + worldChunkPosition;
        Vector3 p4 = new Vector3(-.5f, .5f, .5f) + worldChunkPosition;
        Vector3 p5 = new Vector3(.5f, .5f, .5f) + worldChunkPosition;
        Vector3 p6 = new Vector3(.5f, .5f, -.5f) + worldChunkPosition;
        Vector3 p7 = new Vector3(-.5f, .5f, -.5f) + worldChunkPosition;

        switch (side)
        {
            case Cubeside.BOTTOM:
                vertices = new Vector3[] { p0, p1, p2, p3 };
                normals = new Vector3[]
                {
                    Vector3.down,
                    Vector3.down,
                    Vector3.down,
                    Vector3.down
                };
                uvs = new Vector2[] { uv11, uv01, uv00, uv10 };
                triangles = new int[] { 3, 1, 0, 3, 2, 1 };
                break;
            case Cubeside.TOP:
                vertices = new Vector3[] { p7, p6, p5, p4 };
                normals = new Vector3[]
                {
                    Vector3.up,
                    Vector3.up,
                    Vector3.up,
                    Vector3.up
                };
                uvs = new Vector2[] { uv11, uv01, uv00, uv10 };
                triangles = new int[] { 3, 1, 0, 3, 2, 1 };
                break;
            case Cubeside.LEFT:
                vertices = new Vector3[] { p7, p4, p0, p3 };
                normals = new Vector3[]
                {
                    Vector3.left,
                    Vector3.left,
                    Vector3.left,
                    Vector3.left
                };
                uvs = new Vector2[] { uv11, uv01, uv00, uv10 };
                triangles = new int[] { 3, 1, 0, 3, 2, 1 };
                break;
            case Cubeside.RIGHT:
                vertices = new Vector3[] { p5, p6, p2, p1 };
                normals = new Vector3[]
                {
                    Vector3.right,
                    Vector3.right,
                    Vector3.right,
                    Vector3.right
                };
                uvs = new Vector2[] { uv11, uv01, uv00, uv10 };
                triangles = new int[] { 3, 1, 0, 3, 2, 1 };
                break;
            case Cubeside.FRONT:
                vertices = new Vector3[] { p4, p5, p1, p0 };
                normals = new Vector3[]
                {
                    Vector3.forward,
                    Vector3.forward,
                    Vector3.forward,
                    Vector3.forward
                };
                uvs = new Vector2[] { uv11, uv01, uv00, uv10 };
                triangles = new int[] { 3, 1, 0, 3, 2, 1 };
                break;
            case Cubeside.BACK:
                vertices = new Vector3[] { p6, p7, p3, p2 };
                normals = new Vector3[]
                {
                    Vector3.back,
                    Vector3.back,
                    Vector3.back,
                    Vector3.back
                };
                uvs = new Vector2[] { uv11, uv01, uv00, uv10 };
                triangles = new int[] { 3, 1, 0, 3, 2, 1 };
                break;
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        mesh.RecalculateBounds();

        return mesh;

    }

    public Mesh CreateCube(BlockType bType, Entity en, float3 blockPosition, float3 worldChunkPosition)
    {
        if (bType == BlockType.AIR) return null;

        List<Mesh> quads = new List<Mesh>();

        if (!HasSolidNeighbour((int)blockPosition.x, (int)blockPosition.y, (int)blockPosition.z + 1, worldChunkPosition, blockPosition, en))
            quads.Add(CreateQuads(Cubeside.FRONT, bType, blockPosition));
        if (!HasSolidNeighbour((int)blockPosition.x, (int)blockPosition.y, (int)blockPosition.z - 1, worldChunkPosition, blockPosition, en))
            quads.Add(CreateQuads(Cubeside.BACK, bType, blockPosition));
        if (!HasSolidNeighbour((int)blockPosition.x, (int)blockPosition.y + 1, (int)blockPosition.z, worldChunkPosition, blockPosition, en))
            quads.Add(CreateQuads(Cubeside.TOP, bType, blockPosition));
        if (!HasSolidNeighbour((int)blockPosition.x, (int)blockPosition.y - 1, (int)blockPosition.z, worldChunkPosition, blockPosition, en))
            quads.Add(CreateQuads(Cubeside.BOTTOM, bType, blockPosition));
        if (!HasSolidNeighbour((int)blockPosition.x - 1, (int)blockPosition.y, (int)blockPosition.z, worldChunkPosition, blockPosition, en))
            quads.Add(CreateQuads(Cubeside.LEFT, bType, blockPosition));
        if (!HasSolidNeighbour((int)blockPosition.x + 1, (int)blockPosition.y, (int)blockPosition.z, worldChunkPosition, blockPosition, en))
            quads.Add(CreateQuads(Cubeside.RIGHT, bType, blockPosition));


        CombineInstance[] array = new CombineInstance[quads.Count];

        for (int i = 0; i < array.Length; i++)
            array[i].mesh = quads[i];

        Mesh cube = new Mesh();
        //since our cubes are created on the correct spot already, we dont need a matrix, and so far, no light data
        cube.CombineMeshes(array, true, false, false);

        return cube;
    }

    [BurstCompile]
    private struct RaycastJob : IJobParallelFor
    {
        [ReadOnly] public CollisionWorld world;
        [ReadOnly] public NativeArray<RaycastInput> inputs;
        public NativeArray<RaycastHit> results;

        public unsafe void Execute(int index)
        {
            RaycastHit hit;
            world.CastRay(inputs[index], out hit);
            results[index] = hit;
        }
    }

    private static void SingleRayCast(CollisionWorld world, RaycastInput input,
    ref RaycastHit result)
    {
        var rayCommands = new NativeArray<RaycastInput>(1, Allocator.TempJob);
        var rayResults = new NativeArray<RaycastHit>(1, Allocator.TempJob);
        rayCommands[0] = input;
        var handle = ScheduleBatchRayCast(world, rayCommands, rayResults);
        handle.Complete();
        result = rayResults[0];
        rayCommands.Dispose();
        rayResults.Dispose();
    }

    public static JobHandle ScheduleBatchRayCast(CollisionWorld world,
        NativeArray<RaycastInput> inputs, NativeArray<RaycastHit> results)
    {
        JobHandle rcj = new RaycastJob
        {
            inputs = inputs,
            results = results,
            world = world

        }.Schedule(inputs.Length, 4);
        return rcj;
    }

    public bool HasSolidNeighbour(int x, int y, int z, float3 worldChunkPosition, float3 blockPosition, Entity en)
    {
        //If this is ever is Jobified, this will be a memory sink
        //This should receive the NativeArray from the chunk entity
        //Block[,,] chunks;
        NativeArray<BlockType> ArrayFromChunk = new NativeArray<BlockType>((int)math.pow(MeshComponents.chunkSize, 3), Allocator.Temp);

        if (x < 0 || x >= MeshComponents.chunkSize ||
            y < 0 || y >= MeshComponents.chunkSize ||
            z < 0 || z >= MeshComponents.chunkSize)
        {
            //Block in another chunk
            float3 neighbourChunkPos = worldChunkPosition + new float3((x - (int)blockPosition.x) * MeshComponents.chunkSize,
                                                                     (y - (int)blockPosition.y) * MeshComponents.chunkSize,
                                                                     (z - (int)blockPosition.z) * MeshComponents.chunkSize);
            //string nName = MeshComponents.BuildChunkName(neighbourChunkPos);
            x = ConvertBlockIndexToLocal(x);
            y = ConvertBlockIndexToLocal(y);
            z = ConvertBlockIndexToLocal(z);

            var physicsWorldSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystem<Unity.Physics.Systems.BuildPhysicsWorld>();
            var collisionWorld = physicsWorldSystem.PhysicsWorld.CollisionWorld;

            RaycastInput input = new RaycastInput()
            {
                Start = neighbourChunkPos,
                End = neighbourChunkPos,
                Filter = new CollisionFilter()
                {
                    BelongsTo = ~0u,
                    CollidesWith = ~0u, // all 1s, so all layers, collide with everything
                    GroupIndex = 0
                }
            };

            RaycastHit hit = new RaycastHit();

            //Raycast Here
            Entity NeighBourentity;

            SingleRayCast(collisionWorld, input, ref hit);

            bool haveHit = collisionWorld.CastRay(input, out hit);
            if (haveHit)
            {
                // see hit.Position
                // see hit.SurfaceNormal
                NeighBourentity = physicsWorldSystem.PhysicsWorld.Bodies[hit.RigidBodyIndex].Entity;
            }
            else
            {
                NeighBourentity = Entity.Null;
            }

            if (!NeighBourentity.Equals(Entity.Null))
            {
                ArrayFromChunk = EntityManager.GetBuffer<BlockTypeBuffer>(NeighBourentity).Reinterpret<BlockType>().AsNativeArray();
            }
            else
            {
                //Edge of the known world
                return false;
            }
        }
        else
        {
            //Block in this chunk
            ArrayFromChunk = EntityManager.GetBuffer<BlockTypeBuffer>(en).Reinterpret<BlockType>().AsNativeArray();
        }

        if (ArrayFromChunk.Length != 0)
        {
            try
            {
                return ArrayFromChunk[x + MeshComponents.chunkSize * (y + MeshComponents.chunkSize * z)] == BlockType.AIR;
            }
            catch (System.IndexOutOfRangeException ex) { }
            return false;
        }
        else
        {
            return false;
        }
        
    }

    private int ConvertBlockIndexToLocal(int i)
    {
        if (i == -1)
            i = MeshComponents.chunkSize - 1;
        else if (i == MeshComponents.chunkSize)
            i = 0;
        return i;
    }

    private int3 GetPositionFromIndex(int index)
    {
         int z = index / (MeshComponents.chunkSize * MeshComponents.chunkSize);
        index -= (z * MeshComponents.chunkSize * MeshComponents.chunkSize);
        int y = index / MeshComponents.chunkSize;
        int x = index % MeshComponents.chunkSize;

        return new int3(x, y, z);
    }

    protected override void OnUpdate()
    {
        var lookupChunk = GetComponentDataFromEntity<WorldChunk>(true);
        var buffer = GetBufferFromEntity<BlockTypeBuffer>(true);

        Entities.WithAllReadOnly<BuildMeshFlag>().ForEach((Entity en) => {
            int3 WorldChunkPosition = lookupChunk[en].position;
            NativeArray<BlockType> blockTypes = buffer[en].Reinterpret<BlockType>().ToNativeArray(Allocator.Temp);

            List<Mesh> chunkMesh = new List<Mesh>();

            for (int i = 0; i < blockTypes.Length; i++)
            {
                if (blockTypes[i] == BlockType.AIR) continue;
                Mesh _chunkMesh = CreateCube(blockTypes[i], en, GetPositionFromIndex(i), WorldChunkPosition);             
                chunkMesh.Add(_chunkMesh);
            }

            CombineInstance[] array = new CombineInstance[chunkMesh.Count];

            for (int i = 0; i < array.Length; i++)
                array[i].mesh = chunkMesh[i];

            Mesh cube = new Mesh();
            //since our cubes are created on the correct spot already, we dont need a matrix, and so far, no light data
            cube.CombineMeshes(array, true, false, false);

            PostUpdateCommands.AddComponent(en, new LocalToWorld { });
            PostUpdateCommands.AddSharedComponent(en, new RenderMesh { mesh = cube, material = MeshComponents.textureAtlas });
            PostUpdateCommands.AddComponent(en, new RenderBounds { Value = cube.bounds.ToAABB() });
            PostUpdateCommands.AddComponent(en, new PerInstanceCullingTag { });
            PostUpdateCommands.RemoveComponent(en, typeof(BuildMeshFlag));

        });
    }
}
