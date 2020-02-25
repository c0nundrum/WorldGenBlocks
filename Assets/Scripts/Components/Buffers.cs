using Unity.Entities;
using Unity.Mathematics;

[InternalBufferCapacity(64)]
public struct VerticesBuffer : IBufferElementData
{
    // These implicit conversions are optional, but can help reduce typing.
    public static implicit operator float3(VerticesBuffer e) { return e.Value; }
    public static implicit operator VerticesBuffer(float3 e) { return new VerticesBuffer { Value = e }; }

    // Actual value each buffer element will store.
    public float3 Value;
}

[InternalBufferCapacity(64)]
public struct TrianglesBuffer : IBufferElementData
{
    // These implicit conversions are optional, but can help reduce typing.
    public static implicit operator int3(TrianglesBuffer e) { return e.Value; }
    public static implicit operator TrianglesBuffer(int3 e) { return new TrianglesBuffer { Value = e }; }

    // Actual value each buffer element will store.
    public int3 Value;
}