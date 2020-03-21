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

//[DisableAutoCreation]
//[UpdateAfter(typeof(CreateMeshJobSystem))]
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

    private RenderMesh SingleCube;
    private Mesh OriginCube;

    public Mesh MakeCubeAtZero()
    {

        List<Mesh> quads = new List<Mesh>();

        BlockType bType = BlockType.DIRT;
        Vector3 blockPosition = new Vector3(0, 0, 0);

        quads.Add(CreateQuads(Cubeside.FRONT, bType, blockPosition));
        quads.Add(CreateQuads(Cubeside.BACK, bType, blockPosition));
        quads.Add(CreateQuads(Cubeside.TOP, bType, blockPosition));
        quads.Add(CreateQuads(Cubeside.BOTTOM, bType, blockPosition));
        quads.Add(CreateQuads(Cubeside.LEFT, bType, blockPosition));
        quads.Add(CreateQuads(Cubeside.RIGHT, bType, blockPosition));


        CombineInstance[] array = new CombineInstance[quads.Count];

        for (int i = 0; i < array.Length; i++)
            array[i].mesh = quads[i];

        Mesh cube = new Mesh();
        //since our cubes are created on the correct spot already, we dont need a matrix, and so far, no light data
        cube.CombineMeshes(array, true, false, false);
        cube.Optimize();

        return cube;
    }


    protected override void OnCreate()
    {
        OriginCube = MakeCubeAtZero();

        base.OnCreate();
    }

    protected override void OnStartRunning()
    {
        SingleCube = new RenderMesh
        {
            mesh = OriginCube,
            material = MeshComponents.textureAtlas
        };
        base.OnStartRunning();
    }

    protected override void OnUpdate()
    {

        Entities.WithNone<RenderMesh>().WithAll<CubePosition>().ForEach((Entity en, ref CubePosition cube) =>
        {
            EntityManager.SetComponentData(en, new Parent { Value = cube.parent });          
            EntityManager.SetComponentData(en, new Translation { Value = cube.position });
            EntityManager.SetComponentData(en, new Rotation { Value = quaternion.identity });
            EntityManager.SetComponentData(en, new LocalToParent { });
            EntityManager.AddSharedComponentData(en, SingleCube);
            EntityManager.SetComponentData(en, new RenderBounds { Value = OriginCube.bounds.ToAABB() });
            
            
            cube.HasCube = true;

        });
    }
}

[DisableAutoCreation]
public class CreateMeshJobSystem : JobComponentSystem
{
    private EntityQuery m_Group;

    private EntityArchetype ZeroCube;
    private RenderMesh SingleCube;
    private Mesh OriginCube;

    private EndSimulationEntityCommandBufferSystem beginPresentationEntityCommandBufferSystem;

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


    private Mesh MakeCubeAtZero()
    {

        List<Mesh> quads = new List<Mesh>();

        BlockType bType = BlockType.DIRT;
        Vector3 blockPosition = new Vector3(0, 0, 0);

        quads.Add(CreateQuads(Cubeside.FRONT, bType, blockPosition));
        quads.Add(CreateQuads(Cubeside.BACK, bType, blockPosition));
        quads.Add(CreateQuads(Cubeside.TOP, bType, blockPosition));
        quads.Add(CreateQuads(Cubeside.BOTTOM, bType, blockPosition));
        quads.Add(CreateQuads(Cubeside.LEFT, bType, blockPosition));
        quads.Add(CreateQuads(Cubeside.RIGHT, bType, blockPosition));


        CombineInstance[] array = new CombineInstance[quads.Count];

        for (int i = 0; i < array.Length; i++)
            array[i].mesh = quads[i];

        Mesh cube = new Mesh();
        //since our cubes are created on the correct spot already, we dont need a matrix, and so far, no light data
        cube.CombineMeshes(array, true, false, false);
        cube.Optimize();

        return cube;
    }

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

    [BurstCompile]
    private struct CreateCubeJob : IJobChunk
    {
        //[ReadOnly]
        //public RenderMesh renderMesh;
        //[ReadOnly]
        //public AABB value;
        [ReadOnly]
        public ArchetypeChunkComponentType<CubePosition> cubePosition;

        public EntityCommandBuffer.Concurrent commandBuffer;

        private void CreateCubeAt(float3 position, Entity en, Entity parent, int chunkIndex)
        {

            commandBuffer.SetComponent(chunkIndex, en, new Parent { Value = parent });
            commandBuffer.SetComponent(chunkIndex, en, new Translation { Value = position });
            commandBuffer.SetComponent(chunkIndex, en, new Rotation { Value = quaternion.identity });
            commandBuffer.SetComponent(chunkIndex, en, new LocalToParent { });
            //commandBuffer.SetComponent(chunkIndex, en, new LocalToWorld { });
            //commandBuffer.AddSharedComponent(chunkIndex, en, renderMesh);
            //commandBuffer.AddComponent(chunkIndex, en, new RenderBounds { Value = value });
            //commandBuffer.SetComponent(chunkIndex, en, new PerInstanceCullingTag { });
            //commandBuffer.SetComponent(chunkIndex, en, new LocalToParent { });

        }

        public  void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var chunkBuild = chunk.GetNativeArray(cubePosition);

            for (var i = 0; i < chunk.Count; i++)
            {
                CreateCubeAt(chunkBuild[i].position, chunkBuild[i].owner, chunkBuild[i].parent, chunkIndex);
            }

        }
    }

    protected override void OnStartRunning()
    {
        base.OnStartRunning();

        //SingleCube = new RenderMesh
        //{
        //    mesh = OriginCube,
        //    material = MeshComponents.textureAtlas
        //};
        ZeroCube = EntityManager.CreateArchetype(typeof(Translation), typeof(Rotation), typeof(LocalToWorld));

    }

    protected override void OnCreate()
    {
        beginPresentationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        //OriginCube = MakeCubeAtZero();
        m_Group = GetEntityQuery(ComponentType.ReadOnly<CubePosition>(), ComponentType.ReadOnly<Parent>());
        base.OnCreate();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var cubePosition = GetArchetypeChunkComponentType<CubePosition>(true);
        CreateCubeJob createCubeJob = new CreateCubeJob
        {
            commandBuffer = beginPresentationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
            //value = OriginCube.bounds.ToAABB(),
            cubePosition = cubePosition
        };

        return createCubeJob.Schedule(m_Group, inputDeps);
    }
}
