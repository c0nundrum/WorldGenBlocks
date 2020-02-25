using Unity.Entities;
using Unity.Mathematics;

public enum ChunkStatus { DRAW, DONE, KEEP };

public struct WorldChunk : IComponentData {
    public int3 position;
    public ChunkStatus status;
}
