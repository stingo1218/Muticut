using UnityEngine;
using System;

namespace TerrainSystem
{
    public class NoiseGenerator
    {
        private string seed;
        private System.Random random;

        public NoiseGenerator(string seed = null)
        {
            this.seed = seed ?? GenerateRandomSeed();
            this.random = new System.Random(this.seed.GetHashCode());
        }

        // 生成随机种子
        private string GenerateRandomSeed()
        {
            byte[] bytes = new byte[20];
            using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
            {
                rng.GetBytes(bytes);
            }
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        // 获取种子
        public string GetSeed()
        {
            return seed;
        }

        // 设置种子
        public void SetSeed(string newSeed)
        {
            seed = newSeed;
            random = new System.Random(seed.GetHashCode());
        }

        // 生成高度图（对应 JS 中的 heightMap）
        public float[,] GenerateHeightMap(int width, int height, TerrainSettings settings)
        {
            float[,] elevation = new float[width, height];
            float freq = settings.frequencyElevation;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float nx = (float)x / width * freq;
                    float ny = (float)y / height * freq;

                    // 4层八度噪声叠加（对应 JS 中的 octaves）
                    float e = settings.elevationOctaves_0 * Mathf.PerlinNoise(nx, ny)
                            + settings.elevationOctaves_1 * Mathf.PerlinNoise(4 * nx, 4 * ny)
                            + settings.elevationOctaves_2 * Mathf.PerlinNoise(8 * nx, 8 * ny)
                            + settings.elevationOctaves_3 * Mathf.PerlinNoise(16 * nx, 16 * ny);

                    e = (e + 1) / 2; // 从 -1 到 1 转换为 0 到 1

                    // 岛屿生成（对应 JS 中的 createIsland）
                    if (settings.createIsland)
                    {
                        float xp = (float)x / width;
                        float yp = (float)y / height;
                        float d = Mathf.Sqrt(Mathf.Pow(0.5f - xp, 2) + Mathf.Pow(0.5f - yp, 2));
                        e = (1 + e - (d * 3.5f)) / 2;
                    }

                    e = Mathf.Clamp01(e);
                    elevation[x, y] = Mathf.Pow(e, settings.redistributionElevation);
                }
            }

            return elevation;
        }

        // 生成湿度图（对应 JS 中的 moistureMap）
        public float[,] GenerateMoistureMap(int width, int height, TerrainSettings settings)
        {
            float[,] moisture = new float[width, height];
            float freq = settings.frequencyMoisture;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float nx = (float)x / width * freq;
                    float ny = (float)y / height * freq;

                    // 4层八度噪声叠加
                    float m = settings.moistureOctaves_0 * Mathf.PerlinNoise(nx, ny)
                            + settings.moistureOctaves_1 * Mathf.PerlinNoise(4 * nx, 4 * ny)
                            + settings.moistureOctaves_2 * Mathf.PerlinNoise(8 * nx, 8 * ny)
                            + settings.moistureOctaves_3 * Mathf.PerlinNoise(16 * nx, 16 * ny);

                    m = (m + 1) / 2; // 从 -1 到 1 转换为 0 到 1
                    m = Mathf.Clamp01(m);
                    moisture[x, y] = Mathf.Pow(m, settings.redistributionMoisture);
                }
            }

            return moisture;
        }

        // 生成随机数（对应 JS 中的 lookup 函数）
        public int GetRandomValue()
        {
            return random.Next(0, 101);
        }

        // 生成随机 ID（对应 JS 中的 generateId）
        public string GenerateId(int length = 40)
        {
            byte[] bytes = new byte[length / 2];
            random.NextBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }
    }

    // 地形设置（对应 JS 中的 settings）
    [System.Serializable]
    public class TerrainSettings
    {
        [Header("网格设置")]
        public int hexColumns = 20;
        public int hexRows = 15;
        public float hexSize = 16f;

        [Header("高度噪声设置")]
        public string elevationSeed = "fdc9a9ca516c78d1f830948badf1875d88424406";
        public bool setElevationSeed = false;
        public float frequencyElevation = 0.05f;
        public float redistributionElevation = 1.2f;
        public float elevationOctaves_0 = 1f;
        public float elevationOctaves_1 = 0.5f;
        public float elevationOctaves_2 = 0.25f;
        public float elevationOctaves_3 = 0.12f;
        public bool createIsland = false;

        [Header("湿度噪声设置")]
        public string moistureSeed = "d049b358d128cb265740a90fce37904ce07cd653";
        public bool setMoistureSeed = false;
        public bool drawMoisture = true;
        public float frequencyMoisture = 0.08f;
        public float redistributionMoisture = 1.1f;
        public float moistureOctaves_0 = 1f;
        public float moistureOctaves_1 = 0.5f;
        public float moistureOctaves_2 = 0.25f;
        public float moistureOctaves_3 = 0.12f;

        [Header("轮廓间隔")]
        public float contourInterval_0 = 0.3f; // 深水
        public float contourInterval_1 = 0.4f; // 浅水  
        public float contourInterval_2 = 0.6f; // 平地
        public float contourInterval_3 = 0.75f; // 丘陵
        public float contourInterval_4 = 0.85f; // 山地
    }
} 