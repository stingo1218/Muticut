using UnityEngine;
using System.Collections.Generic;

namespace TerrainSystem
{
    // 生物群系映射器（对应 JS 中的 biomeTileset 和生物群系分类逻辑）
    public class BiomeMapper
    {
        // 瓦片集映射（对应 JS 中的 biomeTileset）
        [System.Serializable]
        public class BiomeTileMapping
        {
            public HexCoordinateSystem.BiomeType biomeType;
            public Vector2Int tileCoords; // 瓦片集中的坐标 (x, y)
            public int elevation; // 高度等级 (z)
            public string tileName;

            public BiomeTileMapping(HexCoordinateSystem.BiomeType biomeType, int x, int y, int z, string tileName)
            {
                this.biomeType = biomeType;
                this.tileCoords = new Vector2Int(x, y);
                this.elevation = z;
                this.tileName = tileName;
            }
        }

        // 河流瓦片映射（对应 JS 中的 riverTileset）
        [System.Serializable]
        public class RiverTileMapping
        {
            public string riverCode; // 河流代码（如 "01", "02" 等）
            public Vector2Int tileCoords;

            public RiverTileMapping(string riverCode, int x, int y)
            {
                this.riverCode = riverCode;
                this.tileCoords = new Vector2Int(x, y);
            }
        }

        private Dictionary<HexCoordinateSystem.BiomeType, BiomeTileMapping> biomeMappings;
        private Dictionary<string, RiverTileMapping> riverMappings;
        private NoiseGenerator noiseGenerator;

        public BiomeMapper(NoiseGenerator noiseGenerator)
        {
            this.noiseGenerator = noiseGenerator;
            InitializeBiomeMappings();
            InitializeRiverMappings();
        }

        // 初始化生物群系映射（对应 JS 中的 biomeTileset）
        private void InitializeBiomeMappings()
        {
            biomeMappings = new Dictionary<HexCoordinateSystem.BiomeType, BiomeTileMapping>
            {
                { HexCoordinateSystem.BiomeType.DeepWater, new BiomeTileMapping(HexCoordinateSystem.BiomeType.DeepWater, 4, 5, 0, "DeepWater") },
                { HexCoordinateSystem.BiomeType.ShallowWater, new BiomeTileMapping(HexCoordinateSystem.BiomeType.ShallowWater, 0, 5, 1, "ShallowWater") },
                { HexCoordinateSystem.BiomeType.FlatDesert1, new BiomeTileMapping(HexCoordinateSystem.BiomeType.FlatDesert1, 1, 2, 2, "FlatDesert1") },
                { HexCoordinateSystem.BiomeType.FlatDesert2, new BiomeTileMapping(HexCoordinateSystem.BiomeType.FlatDesert2, 1, 1, 2, "FlatDesert2") },
                { HexCoordinateSystem.BiomeType.FlatGrass, new BiomeTileMapping(HexCoordinateSystem.BiomeType.FlatGrass, 2, 0, 2, "FlatGrass") },
                { HexCoordinateSystem.BiomeType.FlatSparseTrees1, new BiomeTileMapping(HexCoordinateSystem.BiomeType.FlatSparseTrees1, 3, 0, 2, "FlatSparseTrees1") },
                { HexCoordinateSystem.BiomeType.FlatSparseTrees2, new BiomeTileMapping(HexCoordinateSystem.BiomeType.FlatSparseTrees2, 4, 0, 2, "FlatSparseTrees2") },
                { HexCoordinateSystem.BiomeType.FlatForest, new BiomeTileMapping(HexCoordinateSystem.BiomeType.FlatForest, 5, 0, 2, "FlatForest") },
                { HexCoordinateSystem.BiomeType.FlatForestSwampy, new BiomeTileMapping(HexCoordinateSystem.BiomeType.FlatForestSwampy, 7, 1, 2, "FlatForestSwampy") },
                { HexCoordinateSystem.BiomeType.HillDesert, new BiomeTileMapping(HexCoordinateSystem.BiomeType.HillDesert, 9, 2, 3, "HillDesert") },
                { HexCoordinateSystem.BiomeType.HillGrass, new BiomeTileMapping(HexCoordinateSystem.BiomeType.HillGrass, 7, 0, 3, "HillGrass") },
                { HexCoordinateSystem.BiomeType.HillForest, new BiomeTileMapping(HexCoordinateSystem.BiomeType.HillForest, 6, 0, 3, "HillForest") },
                { HexCoordinateSystem.BiomeType.HillForestNeedleleaf, new BiomeTileMapping(HexCoordinateSystem.BiomeType.HillForestNeedleleaf, 10, 0, 3, "HillForestNeedleleaf") },
                { HexCoordinateSystem.BiomeType.MountainDesert, new BiomeTileMapping(HexCoordinateSystem.BiomeType.MountainDesert, 8, 2, 4, "MountainDesert") },
                { HexCoordinateSystem.BiomeType.MountainShrubland1, new BiomeTileMapping(HexCoordinateSystem.BiomeType.MountainShrubland1, 8, 0, 4, "MountainShrubland1") },
                { HexCoordinateSystem.BiomeType.MountainShrubland2, new BiomeTileMapping(HexCoordinateSystem.BiomeType.MountainShrubland2, 9, 0, 4, "MountainShrubland2") },
                { HexCoordinateSystem.BiomeType.MountainAlpine1, new BiomeTileMapping(HexCoordinateSystem.BiomeType.MountainAlpine1, 10, 0, 4, "MountainAlpine1") },
                { HexCoordinateSystem.BiomeType.MountainAlpine2, new BiomeTileMapping(HexCoordinateSystem.BiomeType.MountainAlpine2, 11, 0, 4, "MountainAlpine2") },
                { HexCoordinateSystem.BiomeType.MountainImpassable1, new BiomeTileMapping(HexCoordinateSystem.BiomeType.MountainImpassable1, 10, 6, 5, "MountainImpassable1") },
                { HexCoordinateSystem.BiomeType.MountainImpassable2, new BiomeTileMapping(HexCoordinateSystem.BiomeType.MountainImpassable2, 0, 6, 5, "MountainImpassable2") },
                { HexCoordinateSystem.BiomeType.Lake1, new BiomeTileMapping(HexCoordinateSystem.BiomeType.Lake1, 12, 0, 0, "lake1") },
                { HexCoordinateSystem.BiomeType.Lake2, new BiomeTileMapping(HexCoordinateSystem.BiomeType.Lake2, 3, 1, 0, "lake2") },
                { HexCoordinateSystem.BiomeType.Lake3, new BiomeTileMapping(HexCoordinateSystem.BiomeType.Lake3, 2, 1, 0, "lake3") },
                { HexCoordinateSystem.BiomeType.Lake4, new BiomeTileMapping(HexCoordinateSystem.BiomeType.Lake4, 8, 1, 0, "lake4") },
                { HexCoordinateSystem.BiomeType.Volcano, new BiomeTileMapping(HexCoordinateSystem.BiomeType.Volcano, 3, 6, 5, "Volcano") },
                { HexCoordinateSystem.BiomeType.Lair, new BiomeTileMapping(HexCoordinateSystem.BiomeType.Lair, 0, 8, 0, "lair") },
                { HexCoordinateSystem.BiomeType.LairSnow, new BiomeTileMapping(HexCoordinateSystem.BiomeType.LairSnow, 1, 8, 0, "lairSnow") },
                { HexCoordinateSystem.BiomeType.LairDesert, new BiomeTileMapping(HexCoordinateSystem.BiomeType.LairDesert, 2, 8, 0, "lairDesert") }
            };
        }

        // 初始化河流瓦片映射（对应 JS 中的 riverTileset）
        private void InitializeRiverMappings()
        {
            riverMappings = new Dictionary<string, RiverTileMapping>
            {
                { "SOURCE", new RiverTileMapping("SOURCE", 0, 2) },
                { "01", new RiverTileMapping("01", 1, 1) },
                { "02", new RiverTileMapping("02", 5, 2) },
                { "03", new RiverTileMapping("03", 2, 2) },
                { "04", new RiverTileMapping("04", 2, 1) },
                { "05", new RiverTileMapping("05", 4, 2) },
                { "10", new RiverTileMapping("10", 1, 1) },
                { "12", new RiverTileMapping("12", 4, 1) },
                { "13", new RiverTileMapping("13", 6, 1) },
                { "14", new RiverTileMapping("14", 3, 1) },
                { "15", new RiverTileMapping("15", 0, 1) },
                { "20", new RiverTileMapping("20", 5, 2) },
                { "21", new RiverTileMapping("21", 4, 1) },
                { "23", new RiverTileMapping("23", 3, 2) },
                { "24", new RiverTileMapping("24", 5, 1) },
                { "25", new RiverTileMapping("25", 1, 2) },
                { "30", new RiverTileMapping("30", 2, 2) },
                { "31", new RiverTileMapping("31", 6, 1) },
                { "32", new RiverTileMapping("32", 3, 2) },
                { "34", new RiverTileMapping("34", 7, 1) },
                { "35", new RiverTileMapping("35", 6, 2) },
                { "40", new RiverTileMapping("40", 2, 1) },
                { "41", new RiverTileMapping("41", 3, 1) },
                { "42", new RiverTileMapping("42", 5, 1) },
                { "43", new RiverTileMapping("43", 7, 1) },
                { "45", new RiverTileMapping("45", 7, 2) },
                { "50", new RiverTileMapping("50", 4, 2) },
                { "51", new RiverTileMapping("51", 0, 1) },
                { "52", new RiverTileMapping("52", 1, 2) },
                { "53", new RiverTileMapping("53", 6, 2) },
                { "54", new RiverTileMapping("54", 7, 2) }
            };
        }

        // 确定生物群系（对应 JS 中的生物群系分类逻辑）
        public void DetermineBiome(HexCoordinateSystem.HexTile hex, TerrainSettings settings)
        {
            // 深水
            if (hex.elevation < settings.contourInterval_0)
            {
                hex.biome = HexCoordinateSystem.BiomeType.DeepWater;
                hex.tileName = "DeepWater";
            }
            // 浅水
            else if (hex.elevation < settings.contourInterval_1)
            {
                hex.biome = HexCoordinateSystem.BiomeType.ShallowWater;
                hex.tileName = "ShallowWater";
            }
            // 平地
            else if (hex.elevation < settings.contourInterval_2)
            {
                DetermineFlatBiome(hex);
            }
            // 丘陵
            else if (hex.elevation < settings.contourInterval_3)
            {
                DetermineHillBiome(hex);
            }
            // 山地
            else if (hex.elevation < settings.contourInterval_4)
            {
                DetermineMountainBiome(hex);
            }
            // 最高山地
            else
            {
                hex.biome = HexCoordinateSystem.BiomeType.MountainImpassable1;
                hex.tileName = "MountainImpassable1";
            }
        }

        // 确定平地生物群系
        private void DetermineFlatBiome(HexCoordinateSystem.HexTile hex)
        {
            if (hex.moisture < 0.10f)
            {
                hex.biome = HexCoordinateSystem.BiomeType.FlatDesert1;
                hex.tileName = "FlatDesert1";
            }
            else if (hex.moisture < 0.25f)
            {
                hex.biome = HexCoordinateSystem.BiomeType.FlatDesert2;
                hex.tileName = "FlatDesert2";
            }
            else if (hex.moisture < 0.40f)
            {
                hex.biome = HexCoordinateSystem.BiomeType.FlatGrass;
                hex.tileName = "FlatGrass";
            }
            else if (hex.moisture < 0.65f)
            {
                hex.biome = HexCoordinateSystem.BiomeType.FlatSparseTrees1;
                hex.tileName = noiseGenerator.GetRandomValue() <= 10 ? "FlatSparseTrees2" : "FlatSparseTrees1";
            }
            else if (hex.moisture < 0.95f)
            {
                hex.biome = HexCoordinateSystem.BiomeType.FlatForest;
                hex.tileName = "FlatForest";
            }
            else
            {
                hex.biome = HexCoordinateSystem.BiomeType.FlatForestSwampy;
                hex.tileName = "FlatForestSwampy";
            }
        }

        // 确定丘陵生物群系
        private void DetermineHillBiome(HexCoordinateSystem.HexTile hex)
        {
            if (hex.moisture < 0.10f)
            {
                hex.biome = HexCoordinateSystem.BiomeType.HillDesert;
                hex.tileName = "HillDesert";
            }
            else if (hex.moisture < 0.45f)
            {
                hex.biome = HexCoordinateSystem.BiomeType.HillGrass;
                hex.tileName = "HillGrass";
            }
            else
            {
                hex.biome = HexCoordinateSystem.BiomeType.HillForest;
                hex.tileName = "HillForest";
            }
        }

        // 确定山地生物群系
        private void DetermineMountainBiome(HexCoordinateSystem.HexTile hex)
        {
            if (hex.moisture < 0.10f)
            {
                hex.biome = HexCoordinateSystem.BiomeType.MountainDesert;
                hex.tileName = "MountainDesert";
            }
            else if (hex.moisture < 0.30f)
            {
                hex.biome = HexCoordinateSystem.BiomeType.MountainShrubland1;
                hex.tileName = noiseGenerator.GetRandomValue() <= 50 ? "MountainShrubland2" : "MountainShrubland1";
            }
            else if (hex.moisture < 0.80f)
            {
                hex.biome = HexCoordinateSystem.BiomeType.MountainAlpine1;
                hex.tileName = noiseGenerator.GetRandomValue() <= 50 ? "MountainAlpine2" : "MountainAlpine1";
            }
            else
            {
                hex.biome = HexCoordinateSystem.BiomeType.MountainImpassable1;
                hex.tileName = "MountainImpassable1";
            }
        }

        // 获取生物群系的瓦片坐标
        public Vector2Int GetBiomeTileCoords(HexCoordinateSystem.BiomeType biomeType)
        {
            if (biomeMappings.TryGetValue(biomeType, out BiomeTileMapping mapping))
            {
                return mapping.tileCoords;
            }
            return Vector2Int.zero;
        }

        // 获取河流瓦片坐标
        public Vector2Int GetRiverTileCoords(string riverCode)
        {
            if (riverMappings.TryGetValue(riverCode, out RiverTileMapping mapping))
            {
                return mapping.tileCoords;
            }
            return Vector2Int.zero;
        }

        // 获取生物群系的高度等级
        public int GetBiomeElevation(HexCoordinateSystem.BiomeType biomeType)
        {
            if (biomeMappings.TryGetValue(biomeType, out BiomeTileMapping mapping))
            {
                return mapping.elevation;
            }
            return 0;
        }

        // 检查是否是水体
        public bool IsWater(HexCoordinateSystem.BiomeType biomeType)
        {
            return biomeType == HexCoordinateSystem.BiomeType.DeepWater || 
                   biomeType == HexCoordinateSystem.BiomeType.ShallowWater ||
                   biomeType.ToString().Contains("lake");
        }

        // 检查是否是湖泊
        public bool IsLake(HexCoordinateSystem.BiomeType biomeType)
        {
            return biomeType.ToString().Contains("lake");
        }

        // 检查是否是火山
        public bool IsVolcano(HexCoordinateSystem.BiomeType biomeType)
        {
            return biomeType == HexCoordinateSystem.BiomeType.Volcano;
        }
    }
} 