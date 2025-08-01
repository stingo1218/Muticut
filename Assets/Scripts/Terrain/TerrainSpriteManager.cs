using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TerrainSystem
{
    // 简化的地形 Sprite 管理器
    public class TerrainSpriteManager : MonoBehaviour
    {
        [Header("预切片 Sprite 资源")]
        [SerializeField] private Sprite[] biomeSprites; // 拖拽切片后的地形 sprites
        [SerializeField] private Sprite[] riverSprites; // 拖拽切片后的河流 sprites
        
        [Header("自动导入设置")]
        [SerializeField] private Texture2D terrainTexture; // 拖拽原始 PNG 文件，自动获取所有切片
        
        [System.Serializable]
        public class BiomeSpriteMapping
        {
            public HexCoordinateSystem.BiomeType biomeType;
            public Sprite sprite;
            public string description; // 描述，方便识别
        }
        
        [Header("手动生物群系映射")]
        [SerializeField] private BiomeSpriteMapping[] manualBiomeMappings = new BiomeSpriteMapping[]
        {
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.DeepWater, description = "深水 (4,5)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.ShallowWater, description = "浅水 (0,5)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.FlatGrass, description = "平原草地 (2,0)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.FlatSparseTrees1, description = "平原稀疏树木1 (3,0)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.FlatSparseTrees2, description = "平原稀疏树木2 (4,0)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.FlatForest, description = "平原森林 (5,0)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.HillGrass, description = "丘陵草地 (7,0)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.HillForest, description = "丘陵森林 (6,0)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.HillForestNeedleleaf, description = "丘陵针叶林 (10,0)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.MountainShrubland1, description = "山地灌木1 (8,0)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.MountainShrubland2, description = "山地灌木2 (9,0)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.MountainAlpine1, description = "高山1 (10,0)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.MountainAlpine2, description = "高山2 (11,0)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.Lake1, description = "湖泊1 (12,0)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.FlatDesert1, description = "平原沙漠1 (1,2)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.FlatDesert2, description = "平原沙漠2 (1,1)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.Lake2, description = "湖泊2 (3,1)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.Lake3, description = "湖泊3 (2,1)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.FlatForestSwampy, description = "平原沼泽森林 (7,1)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.Lake4, description = "湖泊4 (8,1)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.HillDesert, description = "丘陵沙漠 (9,2)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.MountainDesert, description = "山地沙漠 (8,2)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.MountainImpassable1, description = "不可通行山峰1 (10,6)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.MountainImpassable2, description = "不可通行山峰2 (0,6)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.Volcano, description = "火山 (3,6)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.Lair, description = "巢穴 (0,8)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.LairSnow, description = "雪地巢穴 (1,8)" },
            new BiomeSpriteMapping { biomeType = HexCoordinateSystem.BiomeType.LairDesert, description = "沙漠巢穴 (2,8)" }
        };

        [Header("渲染设置")]
        [SerializeField] private Material terrainMaterial;
        [SerializeField] private Material riverMaterial;

        [Header("地形生成")]
        [SerializeField] private TerrainManager terrainManager;
        [SerializeField] private Transform terrainParent;

        // Sprite 映射（改为直接映射到 Sprite 对象）
        private Dictionary<HexCoordinateSystem.BiomeType, Sprite> biomeSpriteMapping;
        private Dictionary<string, Sprite> riverSpriteMapping;

        private void Awake()
        {
            Debug.Log("🔧 TerrainSpriteManager Awake() 开始初始化...");
            try
            {
                InitializeSpriteMappings();
                Debug.Log("✅ Sprite 映射初始化成功");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Sprite 映射初始化失败: {e.Message}");
            }
        }

        // 确保映射字典已初始化
        private void EnsureInitialized()
        {
            if (biomeSpriteMapping == null)
            {
                Debug.LogWarning("Sprite 映射字典未初始化，正在重新初始化...");
                InitializeSpriteMappings();
            }
        }

        // 初始化 Sprite 映射
        private void InitializeSpriteMappings()
        {
            Debug.Log("🗺️ 开始初始化 Sprite 映射...");

            // 生物群系 Sprite 映射
            biomeSpriteMapping = new Dictionary<HexCoordinateSystem.BiomeType, Sprite>();
            
            // 首先尝试按名称自动映射
            if (biomeSprites != null && biomeSprites.Length > 0)
            {
                Debug.Log("🎯 尝试按名称自动映射 Sprites...");
                int autoMappedCount = 0;
                
                // 定义生物群系名称映射表（JS名称 -> Unity枚举）
                var nameMapping = new Dictionary<string, HexCoordinateSystem.BiomeType>
                {
                    // 水域
                    { "DeepWater", HexCoordinateSystem.BiomeType.DeepWater },
                    { "ShallowWater", HexCoordinateSystem.BiomeType.ShallowWater },
                    
                    // 平原
                    { "FlatGrass", HexCoordinateSystem.BiomeType.FlatGrass },
                    { "FlatSparseTrees1", HexCoordinateSystem.BiomeType.FlatSparseTrees1 },
                    { "FlatSparseTrees2", HexCoordinateSystem.BiomeType.FlatSparseTrees2 },
                    { "FlatForest", HexCoordinateSystem.BiomeType.FlatForest },
                    { "FlatForestSwampy", HexCoordinateSystem.BiomeType.FlatForestSwampy },
                    { "FlatDesert1", HexCoordinateSystem.BiomeType.FlatDesert1 },
                    { "FlatDesert2", HexCoordinateSystem.BiomeType.FlatDesert2 },
                    
                    // 丘陵
                    { "HillGrass", HexCoordinateSystem.BiomeType.HillGrass },
                    { "HillForest", HexCoordinateSystem.BiomeType.HillForest },
                    { "HillForestNeedleleaf", HexCoordinateSystem.BiomeType.HillForestNeedleleaf },
                    { "HillDesert", HexCoordinateSystem.BiomeType.HillDesert },
                    
                    // 山地
                    { "MountainShrubland1", HexCoordinateSystem.BiomeType.MountainShrubland1 },
                    { "MountainShrubland2", HexCoordinateSystem.BiomeType.MountainShrubland2 },
                    { "MountainAlpine1", HexCoordinateSystem.BiomeType.MountainAlpine1 },
                    { "MountainAlpine2", HexCoordinateSystem.BiomeType.MountainAlpine2 },
                    { "MountainDesert", HexCoordinateSystem.BiomeType.MountainDesert },
                    { "MountainImpassable1", HexCoordinateSystem.BiomeType.MountainImpassable1 },
                    { "MountainImpassable2", HexCoordinateSystem.BiomeType.MountainImpassable2 },
                    
                    // 湖泊
                    { "lake1", HexCoordinateSystem.BiomeType.Lake1 },
                    { "Lake1", HexCoordinateSystem.BiomeType.Lake1 },
                    { "lake2", HexCoordinateSystem.BiomeType.Lake2 },
                    { "Lake2", HexCoordinateSystem.BiomeType.Lake2 },
                    { "lake3", HexCoordinateSystem.BiomeType.Lake3 },
                    { "Lake3", HexCoordinateSystem.BiomeType.Lake3 },
                    { "lake4", HexCoordinateSystem.BiomeType.Lake4 },
                    { "Lake4", HexCoordinateSystem.BiomeType.Lake4 },
                    
                    // 特殊地形
                    { "Volcano", HexCoordinateSystem.BiomeType.Volcano },
                    { "lair", HexCoordinateSystem.BiomeType.Lair },
                    { "Lair", HexCoordinateSystem.BiomeType.Lair },
                    { "lairSnow", HexCoordinateSystem.BiomeType.LairSnow },
                    { "LairSnow", HexCoordinateSystem.BiomeType.LairSnow },
                    { "lairDesert", HexCoordinateSystem.BiomeType.LairDesert },
                    { "LairDesert", HexCoordinateSystem.BiomeType.LairDesert }
                };
                
                // 遍历所有 sprites，尝试按名称匹配
                foreach (var sprite in biomeSprites)
                {
                    if (sprite == null) continue;
                    
                    // 尝试直接匹配 sprite 名称
                    foreach (var kvp in nameMapping)
                    {
                        string targetName = kvp.Key;
                        HexCoordinateSystem.BiomeType biomeType = kvp.Value;
                        
                        // 检查 sprite 名称是否包含目标名称（忽略大小写）
                        if (sprite.name.IndexOf(targetName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (!biomeSpriteMapping.ContainsKey(biomeType))
                            {
                                biomeSpriteMapping[biomeType] = sprite;
                                // Debug.Log($"✅ 自动映射: {biomeType} -> {sprite.name}");
                                autoMappedCount++;
                                break; // 找到匹配后跳出内层循环
                            }
                        }
                    }
                }
                
                Debug.Log($"📊 按名称自动映射: 成功 {autoMappedCount} 个");
            }
            
            // 然后使用手动映射补充（如果有的话）
            if (manualBiomeMappings != null && manualBiomeMappings.Length > 0)
            {
                Debug.Log("🔄 使用手动映射补充未匹配的生物群系...");
                int manualMappedCount = 0;
                
                foreach (var mapping in manualBiomeMappings)
                {
                    if (mapping.sprite != null && !biomeSpriteMapping.ContainsKey(mapping.biomeType))
                    {
                        biomeSpriteMapping[mapping.biomeType] = mapping.sprite;
                        Debug.Log($"✅ 手动补充: {mapping.biomeType} -> {mapping.sprite.name}");
                        manualMappedCount++;
                    }
                }
                
                Debug.Log($"📊 手动补充映射: {manualMappedCount} 个");
            }
            
            // 最后使用循环索引映射剩余的生物群系
            var allBiomeTypes = (HexCoordinateSystem.BiomeType[])System.Enum.GetValues(typeof(HexCoordinateSystem.BiomeType));
            int fallbackMappedCount = 0;
            
            foreach (var biomeType in allBiomeTypes)
            {
                if (!biomeSpriteMapping.ContainsKey(biomeType) && biomeSprites != null && biomeSprites.Length > 0)
                {
                    // 使用循环索引分配 sprite
                    int index = ((int)biomeType) % biomeSprites.Length;
                    if (biomeSprites[index] != null)
                    {
                        biomeSpriteMapping[biomeType] = biomeSprites[index];
                        Debug.Log($"🔄 回退映射: {biomeType} -> {biomeSprites[index].name} (索引 {index})");
                        fallbackMappedCount++;
                    }
                }
            }
            
            if (fallbackMappedCount > 0)
            {
                Debug.Log($"📊 回退映射: {fallbackMappedCount} 个");
            }

            // 河流 Sprite 映射
            riverSpriteMapping = new Dictionary<string, Sprite>();
            if (riverSprites != null && riverSprites.Length > 0)
            {
                // 简化的河流映射，你可以根据需要调整
                var riverCodes = new string[] { "SOURCE", "01", "02", "03", "04", "05", "10", "12", "13", "14", "15" };
                for (int i = 0; i < riverCodes.Length && i < riverSprites.Length; i++)
                {
                    if (riverSprites[i] != null)
                    {
                        riverSpriteMapping[riverCodes[i]] = riverSprites[i];
                    }
                }
            }
            
            Debug.Log($"✅ 预切片 Sprite 映射完成 - 生物群系: {biomeSpriteMapping.Count} 个, 河流: {riverSpriteMapping.Count} 个");
        }

        // 获取生物群系的 Sprite
        public Sprite GetBiomeSprite(HexCoordinateSystem.BiomeType biomeType)
        {
            // 确保映射字典已初始化
            EnsureInitialized();
            
            if (biomeSpriteMapping.TryGetValue(biomeType, out Sprite sprite))
            {
                return sprite;
            }
            else
            {
                Debug.LogWarning($"⚠️ 未找到生物群系 {biomeType} 的 Sprite 映射！");
                // 如果找不到特定的 sprite，返回第一个可用的 sprite 作为默认值
                if (biomeSprites != null && biomeSprites.Length > 0 && biomeSprites[0] != null)
                {
                    return biomeSprites[0];
                }
            }
            
            return null;
        }

        // 获取河流的 Sprite
        public Sprite GetRiverSprite(string riverCode)
        {
            // 确保映射字典已初始化
            EnsureInitialized();
            
            if (riverSpriteMapping.TryGetValue(riverCode, out Sprite sprite))
            {
                Debug.Log($"✅ 找到河流代码 {riverCode} 的预切片 Sprite: {sprite.name}");
                return sprite;
            }
            else
            {
                Debug.LogWarning($"⚠️ 未找到河流代码 {riverCode} 的 Sprite 映射！");
                // 返回默认河流 sprite
                if (riverSprites != null && riverSprites.Length > 0 && riverSprites[0] != null)
                {
                    return riverSprites[0];
                }
            }
            
            return null;
        }

        // 从图集中创建 Sprite（预切片方式下不再需要此方法）
        /*
        private Sprite CreateSpriteFromAtlas(Sprite atlasSprite, Vector2Int coords)
        {
            // 此方法已被预切片 Sprite 方式替代
        }
        */

        // 创建六边形瓦片 GameObject
        public GameObject CreateHexTile(HexCoordinateSystem.HexTile hex, Transform parent)
        {
            GameObject hexObj = new GameObject($"Hex_{hex.coord.q}_{hex.coord.r}");
            hexObj.transform.SetParent(parent);
            hexObj.transform.position = hex.worldPosition;
            
            // 添加 SpriteRenderer
            SpriteRenderer sr = hexObj.AddComponent<SpriteRenderer>();
            if (terrainMaterial != null)
            {
                sr.material = terrainMaterial;
            }

            // 设置 Sprite
            Sprite biomeSprite = GetBiomeSprite(hex.biome);
            if (biomeSprite != null)
            {
                sr.sprite = biomeSprite;
            }

            hexObj.name = $"Hex_{hex.coord.q}_{hex.coord.r}_{hex.tileName}";
            return hexObj;
        }

        // 创建河流瓦片 GameObject
        public GameObject CreateRiverTile(HexCoordinateSystem.HexTile hex, Transform parent)
        {
            if (!hex.hasRiver) return null;

            string riverCode = GenerateRiverCode(hex);
            if (string.IsNullOrEmpty(riverCode)) return null;

            GameObject riverObj = new GameObject($"River_{hex.coord.q}_{hex.coord.r}");
            riverObj.transform.SetParent(parent);
            riverObj.transform.position = GetRiverWorldPosition(hex);
            
            // 添加 SpriteRenderer
            SpriteRenderer sr = riverObj.AddComponent<SpriteRenderer>();
            if (riverMaterial != null)
            {
                sr.material = riverMaterial;
            }

            // 设置 Sprite
            Sprite riverSprite = GetRiverSprite(riverCode);
            if (riverSprite != null)
            {
                sr.sprite = riverSprite;
            }

            riverObj.name = $"River_{hex.coord.q}_{hex.coord.r}_{riverCode}";
            return riverObj;
        }

        // 生成河流代码
        private string GenerateRiverCode(HexCoordinateSystem.HexTile hex)
        {
            if (hex.isRiverSource) return "SOURCE";
            if (hex.sideRiverEnter >= 0 && hex.sideRiverExit >= 0)
            {
                return hex.sideRiverEnter.ToString() + hex.sideRiverExit.ToString();
            }
            return null;
        }

        // 获取河流世界位置
        private Vector3 GetRiverWorldPosition(HexCoordinateSystem.HexTile hex)
        {
            if (hex.coord.q % 2 == 1)
            {
                return new Vector3(hex.coord.q * 24f, -4f + (hex.coord.r * 28f), 0);
            }
            else
            {
                return new Vector3(hex.coord.q * 24f, -18f + (hex.coord.r * 28f), 0);
            }
        }

        // 检查是否有有效的 Sprite 资源
        public bool HasValidSprites()
        {
            bool biomeValid = biomeSprites != null && biomeSprites.Length > 0;
            bool riverValid = riverSprites != null && riverSprites.Length > 0;
            
            if (!biomeValid)
            {
                Debug.LogWarning("Biome Sprites 数组未正确设置！请在 Inspector 中分配预切片的地形 sprites");
            }
            if (!riverValid)
            {
                Debug.LogWarning("River Sprites 数组未正确设置！请在 Inspector 中分配预切片的河流 sprites");
            }
            
            return biomeValid; // 河流是可选的，所以只检查地形 sprites
        }

        // 生成地形按钮
        [ContextMenu("生成地形")]
        public void GenerateTerrain()
        {
            if (terrainManager == null)
            {
                Debug.LogError("请先分配 TerrainManager！");
                return;
            }

            if (!HasValidSprites())
            {
                Debug.LogError("请先分配 Sprite 资源！");
                return;
            }

            // 清理现有地形
            ClearTerrain();

            // 生成新地形
            terrainManager.GenerateTerrain();
            Debug.Log("地形生成完成！");
        }

        // 清空地形按钮
        [ContextMenu("清空地形")]
        public void ClearTerrain()
        {
            int clearedCount = 0;
            
            // 清理 GameObject 地形
            if (terrainParent != null)
            {
                foreach (Transform child in terrainParent)
                {
                    if (child != null)
                    {
                        DestroyImmediate(child.gameObject);
                        clearedCount++;
                    }
                }
            }

            // 如果有 TerrainManager，也清理其生成的对象
            if (terrainManager != null)
            {
                terrainManager.ClearGeneratedTerrain();
            }

            Debug.Log($"地形清空完成！删除了 {clearedCount} 个对象");
        }

        // 创建生物群系 Tile
        public SpriteTile CreateBiomeTile(HexCoordinateSystem.BiomeType biomeType)
        {
            Sprite biomeSprite = GetBiomeSprite(biomeType);
            if (biomeSprite != null)
            {
                SpriteTile tile = ScriptableObject.CreateInstance<SpriteTile>();
                tile.Sprite = biomeSprite;
                tile.Color = Color.white;
                return tile;
            }
            return null;
        }

        // 创建河流 Tile
        public SpriteTile CreateRiverTile(string riverCode)
        {
            Sprite riverSprite = GetRiverSprite(riverCode);
            if (riverSprite != null)
            {
                SpriteTile tile = ScriptableObject.CreateInstance<SpriteTile>();
                tile.Sprite = riverSprite;
                tile.Color = Color.white;
                return tile;
            }
            return null;
        }

        // 为 Tilemap 创建六边形瓦片
        public SpriteTile CreateHexTileForTilemap(HexCoordinateSystem.HexTile hex)
        {
            return CreateBiomeTile(hex.biome);
        }

        // 为 Tilemap 创建河流瓦片
        public SpriteTile CreateRiverTileForTilemap(HexCoordinateSystem.HexTile hex)
        {
            if (!hex.hasRiver) return null;
            
            string riverCode = GenerateRiverCode(hex);
            if (string.IsNullOrEmpty(riverCode)) return null;
            
            return CreateRiverTile(riverCode);
        }

        // 显示当前 Sprite 映射
        [ContextMenu("显示 Sprite 映射")]
        public void ShowSpriteMapping()
        {
            Debug.Log("=== 当前 Sprite 映射 ===");
            
            if (biomeSprites == null || biomeSprites.Length == 0)
            {
                Debug.LogError("❌ Biome Sprites 数组为空！");
                return;
            }
            
            Debug.Log($"📋 共有 {biomeSprites.Length} 个 Sprites:");
            for (int i = 0; i < biomeSprites.Length; i++)
            {
                if (biomeSprites[i] != null)
                {
                    Debug.Log($"  [{i}] {biomeSprites[i].name}");
                }
                else
                {
                    Debug.LogWarning($"  [{i}] <空>");
                }
            }
            
            // 如果映射已初始化，显示映射关系
            EnsureInitialized();
            if (biomeSpriteMapping != null && biomeSpriteMapping.Count > 0)
            {
                Debug.Log($"🗺️ 生物群系映射 ({biomeSpriteMapping.Count} 个):");
                foreach (var kvp in biomeSpriteMapping)
                {
                    Debug.Log($"  {kvp.Key} -> {kvp.Value.name}");
                }
            }
        }

        // 快速填充手动映射
        [ContextMenu("快速填充映射")]
        public void QuickFillMappings()
        {
            Debug.Log("🚀 开始快速填充映射...");
            
            if (biomeSprites == null || biomeSprites.Length == 0)
            {
                Debug.LogError("❌ 请先分配 biomeSprites 数组！");
                return;
            }
            
            // 为每个手动映射分配 sprite（如果还没有分配的话）
            for (int i = 0; i < manualBiomeMappings.Length; i++)
            {
                if (manualBiomeMappings[i].sprite == null)
                {
                    // 使用循环索引分配 sprite
                    int spriteIndex = i % biomeSprites.Length;
                    manualBiomeMappings[i].sprite = biomeSprites[spriteIndex];
                    Debug.Log($"🔄 快速分配: {manualBiomeMappings[i].biomeType} -> {biomeSprites[spriteIndex].name}");
                }
            }
            
#if UNITY_EDITOR
            // 标记为已修改，以便保存
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            
            Debug.Log("✅ 快速填充完成！");
        }

        // 清空手动映射
        [ContextMenu("清空手动映射")]
        public void ClearManualMappings()
        {
            Debug.Log("🧹 清空手动映射...");
            
            for (int i = 0; i < manualBiomeMappings.Length; i++)
            {
                manualBiomeMappings[i].sprite = null;
            }
            
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            
            Debug.Log("✅ 手动映射已清空！");
        }

        // 自动导入 Sprites
        [ContextMenu("自动导入 Sprites")]
        public void AutoImportSprites()
        {
            Debug.Log("🔄 开始自动导入 Sprites...");
            
#if UNITY_EDITOR
            if (terrainTexture == null)
            {
                Debug.LogError("❌ 请先将地形 PNG 文件拖入 'Terrain Texture' 字段！");
                return;
            }
            
            // 获取纹理的资源路径
            string texturePath = UnityEditor.AssetDatabase.GetAssetPath(terrainTexture);
            Debug.Log($"🔍 纹理路径: {texturePath}");
            
            // 获取该纹理下的所有 Sprite 子资源
            Object[] sprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(texturePath);
            
            // 过滤出 Sprite 类型的资源
            var spriteList = new System.Collections.Generic.List<Sprite>();
            foreach (Object obj in sprites)
            {
                if (obj is Sprite sprite && obj != terrainTexture)
                {
                    spriteList.Add(sprite);
                }
            }
            
            if (spriteList.Count == 0)
            {
                Debug.LogError("❌ 未找到任何 Sprite！请确保：");
                Debug.LogError("1. PNG 文件的 Texture Type = Sprite (2D and UI)");
                Debug.LogError("2. Sprite Mode = Multiple");
                Debug.LogError("3. 已在 Sprite Editor 中进行切片");
                return;
            }
            
            // 按名称排序（通常是 _0, _1, _2...）
            spriteList.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            
            // 分配到数组
            biomeSprites = spriteList.ToArray();
            
            Debug.Log($"✅ 自动导入完成！找到 {biomeSprites.Length} 个 Sprites:");
            for (int i = 0; i < biomeSprites.Length; i++)
            {
                Debug.Log($"  [{i}] {biomeSprites[i].name}");
            }
            
            // 标记为已修改，以便保存
            UnityEditor.EditorUtility.SetDirty(this);
#else
            Debug.LogWarning("自动导入功能仅在编辑器中可用");
#endif
        }

        // 详细的检查方法
        [ContextMenu("检查设置")]
        public void CheckSetup()
        {
            Debug.Log("=== 预切片地形 Sprite 系统检查 ===");
            
            // 检查 Biome Sprites 数组
            if (biomeSprites == null || biomeSprites.Length == 0)
            {
                Debug.LogError("❌ Biome Sprites 数组未分配或为空！");
                Debug.LogError("请按以下步骤设置：");
                Debug.LogError("1. 选中你的地形 PNG 文件");
                Debug.LogError("2. Inspector 中设置 Texture Type = Sprite (2D and UI)");
                Debug.LogError("3. Sprite Mode = Multiple");
                Debug.LogError("4. 点击 Sprite Editor 进行切片");
                Debug.LogError("5. 将切片后的 sprites 拖入 Biome Sprites 数组");
                return;
            }
            else
            {
                Debug.Log($"✅ Biome Sprites 数组已分配: {biomeSprites.Length} 个 sprites");
                
                // 检查每个 sprite
                for (int i = 0; i < biomeSprites.Length; i++)
                {
                    if (biomeSprites[i] == null)
                    {
                        Debug.LogWarning($"⚠️ Biome Sprites[{i}] 为空");
                    }
                    else
                    {
                        Debug.Log($"  [{i}] {biomeSprites[i].name}");
                    }
                }
            }
            
            // 检查 River Sprites 数组（可选）
            if (riverSprites == null || riverSprites.Length == 0)
            {
                Debug.LogWarning("⚠️ River Sprites 数组未分配（可选）");
            }
            else
            {
                Debug.Log($"✅ River Sprites 数组已分配: {riverSprites.Length} 个 sprites");
            }
            
            // 检查映射初始化
            EnsureInitialized();
            Debug.Log($"✅ Sprite 映射: 生物群系 {biomeSpriteMapping.Count} 个");
            
            Debug.Log("🎉 预切片 Sprite 系统检查完成！");
        }

        // 简单的测试方法
        [ContextMenu("测试 Sprite 系统")]
        public void TestSpriteSystem()
        {
            Debug.Log("=== 预切片 Sprite 系统测试 ===");
            
            if (!HasValidSprites())
            {
                Debug.LogError("❌ Sprite 设置不完整，请先使用 '检查设置' 菜单");
                return;
            }
            
            Debug.Log($"Biome Sprites 数量: {biomeSprites.Length}");
            Debug.Log($"River Sprites 数量: {(riverSprites != null ? riverSprites.Length : 0)}");
            
            // 测试获取生物群系 Sprite
            Sprite testSprite = GetBiomeSprite(HexCoordinateSystem.BiomeType.FlatGrass);
            Debug.Log($"FlatGrass Sprite: {(testSprite != null ? "✅ 成功" : "❌ 失败")}");
            
            // 测试 Tile 创建
            SpriteTile testTile = CreateBiomeTile(HexCoordinateSystem.BiomeType.FlatGrass);
            Debug.Log($"FlatGrass Tile: {(testTile != null ? "✅ 成功" : "❌ 失败")}");
            
            Debug.Log("🎉 预切片 Sprite 系统测试完成！");
        }
    }
} 