using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public class Utils
{
    private static int maxHeight = 150;
    private static float smooth = 0.01f;
    private static int octaves = 4;
    private static float persistence = 0.5f;

    public static int GenerateHeight(float x, float z)
    {
        float height = Map(0, maxHeight, 0, 1, FBM(x * smooth, z * smooth, octaves, persistence));
        return (int)height;
    }

    private static float Map(float newmin, float newmax, float originalMin, float originalMax, float value)
    {
        return Mathf.Lerp(newmin, newmax, Mathf.InverseLerp(originalMin, originalMax, value));
    }

    private static float FBM(float x, float z, int oct, float pers)
    {
        float total = 0;
        float frequency = 1;
        float amplitude = 1;
        float maxValue = 0;

        for(int i = 0; i < oct; i++)
        {
            total += Mathf.PerlinNoise(x * frequency, z * frequency) * amplitude;

            maxValue += amplitude;

            amplitude *= pers;
            frequency *= 2;
        }

        return total / maxValue;
    }

}
