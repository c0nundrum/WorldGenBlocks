using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

public class Chunk
{
    public Material cubeMaterial;
    public Block[,,] chunkData;
    public float3 position;
    public string chunkName;

    private EntityManager entityManager;
    private EntityArchetype archetype;

    void BuildChunk()
    {
        chunkData = new Block[MeshComponents.chunkSize, MeshComponents.chunkSize, MeshComponents.chunkSize];
        for (int z = 0; z < MeshComponents.chunkSize; z++)
        {
            for (int y = 0; y < MeshComponents.chunkSize; y++)
            {
                for (int x = 0; x < MeshComponents.chunkSize; x++)
                {
                    float3 pos = new float3(x, y, z);
                    int worldX = (int)(x + position.x);
                    int worldY = (int)(y + position.y);
                    int worldZ = (int)(z + position.z);

                    if(Utils.FBM3D(worldX, worldY, worldZ, 0.1f, 3) < 0.42f)
                        chunkData[x, y, z] = new Block(BlockType.AIR, pos, this);
                    else if (worldY <= Utils.GenerateStoneHeight(worldX, worldZ))
                        if(Utils.FBM3D(worldX, worldY, worldZ, 0.01f, 2) < 0.38f && worldY < 40)
                            chunkData[x, y, z] = new Block(BlockType.DIAMOND, pos, this);
                        else
                            chunkData[x, y, z] = new Block(BlockType.STONE, pos, this);
                    else if (worldY == Utils.GenerateHeight(worldX, worldZ))
                        chunkData[x, y, z] = new Block(BlockType.GRASS, pos, this);
                    else if (worldY < Utils.GenerateHeight(worldX, worldZ))
                        chunkData[x, y, z] = new Block(BlockType.DIRT, pos, this);
                    else
                        chunkData[x, y, z] = new Block(BlockType.AIR, pos, this);
                }

            }
        }
    }

    public void DrawChunk()
    {
        List<Mesh> chunk = new List<Mesh>();

        for (int z = 0; z < MeshComponents.chunkSize; z++)
        {
            for (int y = 0; y < MeshComponents.chunkSize; y++)
            {
                for (int x = 0; x < MeshComponents.chunkSize; x++)
                {
                    if (chunkData[x, y, z].isSolid)
                        chunk.Add(chunkData[x, y, z].CreateCube());
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

        RenderMesh(chunkMesh, this.cubeMaterial, this.position);
    }

    private void RenderMesh(Mesh mesh, Material material, float3 pos)
    {
        RenderMesh renderMesh = new RenderMesh
        {
            mesh = mesh,
            material = material
        };

        Entity entity = entityManager.CreateEntity(archetype);

        entityManager.SetSharedComponentData(entity, renderMesh);

        entityManager.SetComponentData(entity, new Translation
        {
            Value = pos
        });

        entityManager.SetComponentData(entity, new WorldChunk
        {
            position = new int3 ((int)pos.x, (int)pos.y, (int)pos.z)
        });

    }

    public Chunk(float3 position, Material material, EntityManager entityManager, EntityArchetype entityArchetype)
    {
        this.chunkName = MeshComponents.BuildChunkName(position);
        this.position = position;
        this.cubeMaterial = material;
        this.entityManager = entityManager;
        this.archetype = entityArchetype;
        BuildChunk();
    }

}
