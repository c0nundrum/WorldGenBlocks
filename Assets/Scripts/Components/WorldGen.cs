using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using System;

public enum ChunkStatus { DRAW, DONE, KEEP };

public struct WorldChunk : IComponentData
{
    public int3 position;
    public ChunkStatus status;
}

public struct CurrentChunkFlag : IComponentData { };
public struct DrawChunkFlag : IComponentData { };
public struct BuildCollisionMeshFlag : IComponentData { };
public struct DoneChunkFlag : IComponentData { };
public struct BuildMeshFlag : IComponentData { };
