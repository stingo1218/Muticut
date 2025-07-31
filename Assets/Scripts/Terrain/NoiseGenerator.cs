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
            
            // 使用种子创建偏移量，模拟 SimplexNoise 的种子行为
            System.Random seedRandom = new System.Random(settings.elevationSeed.GetHashCode());
            float offsetX = (float)seedRandom.NextDouble() * 1000f;
            float offsetY = (float)seedRandom.NextDouble() * 1000f;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // 完全匹配 JS 版本的坐标计算：nx = (x / settings.hexColums) * freq
                    float nx = ((float)x / width) * freq;
                    float ny = ((float)y / height) * freq;

                    // 4层八度噪声叠加，添加种子偏移
                    float e = settings.elevationOctaves_0 * (Mathf.PerlinNoise(nx + offsetX, ny + offsetY) * 2f - 1f)
                            + settings.elevationOctaves_1 * (Mathf.PerlinNoise(4 * nx + offsetX, 4 * ny + offsetY) * 2f - 1f)
                            + settings.elevationOctaves_2 * (Mathf.PerlinNoise(8 * nx + offsetX, 8 * ny + offsetY) * 2f - 1f)
                            + settings.elevationOctaves_3 * (Mathf.PerlinNoise(16 * nx + offsetX, 16 * ny + offsetY) * 2f - 1f);

                    e = (e + 1) / 2; // 从 -1 到 1 转换为 0 到 1

                    // 岛屿生成（完全匹配 JS 版本）
                    if (settings.createIsland)
                    {
                        float xp = (float)x / width;
                        float yp = (float)y / height;
                        // 使用 JS 版本的 Math.hypot 等价计算
                        float d = Mathf.Sqrt((0.5f - xp) * (0.5f - xp) + (0.5f - yp) * (0.5f - yp));
                        e = (1 + e - (d * 3.5f)) / 2;
                    }

                    // 匹配 JS 版本的边界处理
                    if (e < 0) e = 0;
                    if (e > 1) e = 1;

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
            
            // 使用湿度种子创建偏移量，模拟 SimplexNoise 的种子行为
            System.Random seedRandom = new System.Random(settings.moistureSeed.GetHashCode());
            float offsetX = (float)seedRandom.NextDouble() * 1000f;
            float offsetY = (float)seedRandom.NextDouble() * 1000f;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // 完全匹配 JS 版本的坐标计算：nx = (x / settings.hexColums) * freq
                    float nx = ((float)x / width) * freq;
                    float ny = ((float)y / height) * freq;

                    // 4层八度噪声叠加，添加种子偏移，转换 PerlinNoise 输出到 SimplexNoise 范围
                    float m = settings.moistureOctaves_0 * (Mathf.PerlinNoise(nx + offsetX, ny + offsetY) * 2f - 1f)
                            + settings.moistureOctaves_1 * (Mathf.PerlinNoise(4 * nx + offsetX, 4 * ny + offsetY) * 2f - 1f)
                            + settings.moistureOctaves_2 * (Mathf.PerlinNoise(8 * nx + offsetX, 8 * ny + offsetY) * 2f - 1f)
                            + settings.moistureOctaves_3 * (Mathf.PerlinNoise(16 * nx + offsetX, 16 * ny + offsetY) * 2f - 1f);

                    m = (m + 1) / 2; // 从 -1 到 1 转换为 0 到 1
                    
                    // 匹配 JS 版本的边界处理
                    if (m < 0) m = 0;
                    if (m > 1) m = 1;
                    
                    moisture[x, y] = Mathf.Pow(m, settings.redistributionMoisture);
                }
            }

            return moisture;
        }
        
        // 调试：分析高度分布
        public void AnalyzeElevationDistribution(float[,] elevation, string label = "")
        {
            int width = elevation.GetLength(0);
            int height = elevation.GetLength(1);
            
            float min = float.MaxValue;
            float max = float.MinValue;
            float sum = 0f;
            int deepWater = 0, shallowWater = 0, flat = 0, hill = 0, mountain = 0;
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float e = elevation[x, y];
                    min = Mathf.Min(min, e);
                    max = Mathf.Max(max, e);
                    sum += e;
                    
                    // 统计生物群系分布
                    if (e <= 0.2f) deepWater++;
                    else if (e <= 0.3f) shallowWater++;
                    else if (e <= 0.5f) flat++;
                    else if (e <= 0.7f) hill++;
                    else mountain++;
                }
            }
            
            float average = sum / (width * height);
            int total = width * height;
            
            Debug.Log($"🏔️ 高度分布分析 {label}:");
            Debug.Log($"  📊 范围: {min:F3} - {max:F3}, 平均: {average:F3}");
            Debug.Log($"  🌊 深水 (<= 0.2): {deepWater} ({(deepWater * 100f / total):F1}%)");
            Debug.Log($"  💧 浅水 (0.2-0.3): {shallowWater} ({(shallowWater * 100f / total):F1}%)");
            Debug.Log($"  🌱 平地 (0.3-0.5): {flat} ({(flat * 100f / total):F1}%)");
            Debug.Log($"  🏔️ 丘陵 (0.5-0.7): {hill} ({(hill * 100f / total):F1}%)");
            Debug.Log($"  ⛰️ 山地 (> 0.7): {mountain} ({(mountain * 100f / total):F1}%)");
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
        public int hexColumns = 47;  // JS: 最大47列
        public int hexRows = 20;     // JS: 最大20行
        public float hexSize = 16f;  // JS: hexSize = 16

        [Header("高度噪声设置")]
        public string elevationSeed = "fdc9a9ca516c78d1f830948badf1875d88424406";  // JS 默认值
        public bool setElevationSeed = false;  // JS: setElevationSeed = false
        public float frequencyElevation = 0.8f;  // JS: frequencyElevation = 0.8
        public float redistributionElevation = 1.0f;  // JS: redistributionElevation = 1.0
        public float elevationOctaves_0 = 1f;    // JS: elevationOctaves_0 = 1
        public float elevationOctaves_1 = 0.5f;  // JS: elevationOctaves_1 = 0.5
        public float elevationOctaves_2 = 0.25f; // JS: elevationOctaves_2 = 0.25
        public float elevationOctaves_3 = 0.12f; // JS: elevationOctaves_3 = 0.12
        public bool createIsland = false;  // JS: createIsland = false

        [Header("湿度噪声设置")]
        public string moistureSeed = "d049b358d128cb265740a90fce37904ce07cd653";  // JS 默认值
        public bool setMoistureSeed = false;  // JS: setMoistureSeed = false
        public bool drawMoisture = true;      // JS: drawMoisture = true
        public float frequencyMoisture = 0.8f;  // JS: frequencyMoisture = 0.8
        public float redistributionMoisture = 1.0f;  // JS: redistributionMoisture = 1.0
        public float moistureOctaves_0 = 1f;    // JS: moistureOctaves_0 = 1
        public float moistureOctaves_1 = 0.5f;  // JS: moistureOctaves_1 = 0.5
        public float moistureOctaves_2 = 0.25f; // JS: moistureOctaves_2 = 0.25
        public float moistureOctaves_3 = 0.12f; // JS: moistureOctaves_3 = 0.12

        [Header("轮廓间隔")]
        public float contourInterval_0 = 0.2f; // JS: contourInterval_0 = 0.2 (深水)
        public float contourInterval_1 = 0.3f; // JS: contourInterval_1 = 0.3 (浅水)
        public float contourInterval_2 = 0.5f; // JS: contourInterval_2 = 0.5 (平地)
        public float contourInterval_3 = 0.7f; // JS: contourInterval_3 = 0.7 (丘陵)
        public float contourInterval_4 = 0.9f; // JS: contourInterval_4 = 0.9 (山地)
        
        /// <summary>
        /// 导出为 Map Hash
        /// </summary>
        public string ToMapHash()
        {
            string json = JsonUtility.ToJson(this, true);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
            return System.Convert.ToBase64String(bytes);
        }
        
        /// <summary>
        /// 从 Map Hash 导入
        /// </summary>
        public static TerrainSettings FromMapHash(string mapHash)
        {
            try
            {
                byte[] bytes = System.Convert.FromBase64String(mapHash);
                string json = System.Text.Encoding.UTF8.GetString(bytes);
                return JsonUtility.FromJson<TerrainSettings>(json) ?? new TerrainSettings();
            }
            catch
            {
                return new TerrainSettings();
            }
        }
    }
} 