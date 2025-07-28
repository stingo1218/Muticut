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
        [Header("Sprite 资源")]
        [SerializeField] private Sprite tilesetSprite;
        [SerializeField] private Sprite roadsRiversSprite;

        [Header("Sprite 设置")]
        [SerializeField] private Vector2Int spriteSize = new Vector2Int(32, 48);

        [Header("渲染设置")]
        [SerializeField] private Material terrainMaterial;
        [SerializeField] private Material riverMaterial;

        [Header("地形生成")]
        [SerializeField] private TerrainManager terrainManager;
        [SerializeField] private Transform terrainParent;

        // Sprite 映射
        private Dictionary<HexCoordinateSystem.BiomeType, Vector2Int> biomeSpriteMapping;
        private Dictionary<string, Vector2Int> riverSpriteMapping;

        private void Awake()
        {
            InitializeSpriteMappings();
        }

        // 确保映射字典已初始化
        private void EnsureInitialized()
        {
            if (biomeSpriteMapping == null || riverSpriteMapping == null)
            {
                Debug.LogWarning("Sprite 映射字典未初始化，正在重新初始化...");
                InitializeSpriteMappings();
            }
        }

        // 初始化 Sprite 映射
        private void InitializeSpriteMappings()
        {
            // 生物群系 Sprite 映射
            biomeSpriteMapping = new Dictionary<HexCoordinateSystem.BiomeType, Vector2Int>
            {
                { HexCoordinateSystem.BiomeType.DeepWater, new Vector2Int(4, 5) },
                { HexCoordinateSystem.BiomeType.ShallowWater, new Vector2Int(0, 5) },
                { HexCoordinateSystem.BiomeType.FlatDesert1, new Vector2Int(1, 2) },
                { HexCoordinateSystem.BiomeType.FlatDesert2, new Vector2Int(1, 1) },
                { HexCoordinateSystem.BiomeType.FlatGrass, new Vector2Int(2, 0) },
                { HexCoordinateSystem.BiomeType.FlatSparseTrees1, new Vector2Int(3, 0) },
                { HexCoordinateSystem.BiomeType.FlatSparseTrees2, new Vector2Int(4, 0) },
                { HexCoordinateSystem.BiomeType.FlatForest, new Vector2Int(5, 0) },
                { HexCoordinateSystem.BiomeType.FlatForestSwampy, new Vector2Int(7, 1) },
                { HexCoordinateSystem.BiomeType.HillDesert, new Vector2Int(9, 2) },
                { HexCoordinateSystem.BiomeType.HillGrass, new Vector2Int(7, 0) },
                { HexCoordinateSystem.BiomeType.HillForest, new Vector2Int(6, 0) },
                { HexCoordinateSystem.BiomeType.HillForestNeedleleaf, new Vector2Int(10, 0) },
                { HexCoordinateSystem.BiomeType.MountainDesert, new Vector2Int(8, 2) },
                { HexCoordinateSystem.BiomeType.MountainShrubland1, new Vector2Int(8, 0) },
                { HexCoordinateSystem.BiomeType.MountainShrubland2, new Vector2Int(9, 0) },
                { HexCoordinateSystem.BiomeType.MountainAlpine1, new Vector2Int(10, 0) },
                { HexCoordinateSystem.BiomeType.MountainAlpine2, new Vector2Int(11, 0) },
                { HexCoordinateSystem.BiomeType.MountainImpassable1, new Vector2Int(10, 6) },
                { HexCoordinateSystem.BiomeType.MountainImpassable2, new Vector2Int(0, 6) },
                { HexCoordinateSystem.BiomeType.Lake1, new Vector2Int(12, 0) },
                { HexCoordinateSystem.BiomeType.Lake2, new Vector2Int(3, 1) },
                { HexCoordinateSystem.BiomeType.Lake3, new Vector2Int(2, 1) },
                { HexCoordinateSystem.BiomeType.Lake4, new Vector2Int(8, 1) },
                { HexCoordinateSystem.BiomeType.Volcano, new Vector2Int(3, 6) },
                { HexCoordinateSystem.BiomeType.Lair, new Vector2Int(0, 8) },
                { HexCoordinateSystem.BiomeType.LairSnow, new Vector2Int(1, 8) },
                { HexCoordinateSystem.BiomeType.LairDesert, new Vector2Int(2, 8) }
            };

            // 河流 Sprite 映射
            riverSpriteMapping = new Dictionary<string, Vector2Int>
            {
                { "SOURCE", new Vector2Int(0, 2) },
                { "01", new Vector2Int(1, 1) }, { "02", new Vector2Int(5, 2) }, { "03", new Vector2Int(2, 2) },
                { "04", new Vector2Int(2, 1) }, { "05", new Vector2Int(4, 2) }, { "10", new Vector2Int(1, 1) },
                { "12", new Vector2Int(4, 1) }, { "13", new Vector2Int(6, 1) }, { "14", new Vector2Int(3, 1) },
                { "15", new Vector2Int(0, 1) }, { "20", new Vector2Int(5, 2) }, { "21", new Vector2Int(4, 1) },
                { "23", new Vector2Int(3, 2) }, { "24", new Vector2Int(5, 1) }, { "25", new Vector2Int(1, 2) },
                { "30", new Vector2Int(2, 2) }, { "31", new Vector2Int(6, 1) }, { "32", new Vector2Int(3, 2) },
                { "34", new Vector2Int(7, 1) }, { "35", new Vector2Int(6, 2) }, { "40", new Vector2Int(2, 1) },
                { "41", new Vector2Int(3, 1) }, { "42", new Vector2Int(5, 1) }, { "43", new Vector2Int(7, 1) },
                { "45", new Vector2Int(7, 2) }, { "50", new Vector2Int(4, 2) }, { "51", new Vector2Int(0, 1) },
                { "52", new Vector2Int(1, 2) }, { "53", new Vector2Int(6, 2) }, { "54", new Vector2Int(7, 2) }
            };
        }

        // 获取生物群系的 Sprite
        public Sprite GetBiomeSprite(HexCoordinateSystem.BiomeType biomeType)
        {
            // 确保映射字典已初始化
            EnsureInitialized();
            
            if (tilesetSprite == null) 
            {
                Debug.LogWarning("Tileset Sprite 未分配！");
                return null;
            }
            
            if (tilesetSprite.texture == null)
            {
                Debug.LogError($"Tileset Sprite '{tilesetSprite.name}' 的 texture 为 null！");
#if UNITY_EDITOR
                Debug.LogError($"Sprite 路径: {AssetDatabase.GetAssetPath(tilesetSprite)}");
#endif
                Debug.LogError("请检查 PNG 导入设置：Texture Type = Sprite (2D and UI), Pixels Per Unit = 32");
                return null;
            }
            
            if (biomeSpriteMapping.TryGetValue(biomeType, out Vector2Int coords))
            {
                Debug.Log($"🎯 为生物群系 {biomeType} 创建 Sprite，坐标: {coords}");
                return CreateSpriteFromAtlas(tilesetSprite, coords);
            }
            else
            {
                Debug.LogWarning($"⚠️ 未找到生物群系 {biomeType} 的 Sprite 映射！");
            }
            return null;
        }

        // 获取河流的 Sprite
        public Sprite GetRiverSprite(string riverCode)
        {
            // 确保映射字典已初始化
            EnsureInitialized();
            
            if (roadsRiversSprite == null) 
            {
                Debug.LogWarning("Roads Rivers Sprite 未分配！");
                return null;
            }
            
            if (roadsRiversSprite.texture == null)
            {
                Debug.LogError($"Roads Rivers Sprite '{roadsRiversSprite.name}' 的 texture 为 null！");
#if UNITY_EDITOR
                Debug.LogError($"Sprite 路径: {AssetDatabase.GetAssetPath(roadsRiversSprite)}");
#endif
                Debug.LogError("请检查 PNG 导入设置：Texture Type = Sprite (2D and UI), Pixels Per Unit = 32");
                return null;
            }
            
            if (riverSpriteMapping.TryGetValue(riverCode, out Vector2Int coords))
            {
                return CreateSpriteFromAtlas(roadsRiversSprite, coords);
            }
            return null;
        }

        // 从图集中创建 Sprite
        private Sprite CreateSpriteFromAtlas(Sprite atlasSprite, Vector2Int coords)
        {
            if (atlasSprite == null || atlasSprite.texture == null) return null;

            Rect rect = new Rect(
                coords.x * spriteSize.x,
                coords.y * spriteSize.y,
                spriteSize.x,
                spriteSize.y
            );

            return Sprite.Create(
                atlasSprite.texture,
                rect,
                new Vector2(0.5f, 0.5f),
                32f
            );
        }

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
            bool tilesetValid = tilesetSprite != null && tilesetSprite.texture != null;
            bool riverValid = roadsRiversSprite != null && roadsRiversSprite.texture != null;
            
            if (!tilesetValid)
            {
                Debug.LogWarning("Tileset Sprite 未正确设置或导入！");
            }
            if (!riverValid)
            {
                Debug.LogWarning("Roads Rivers Sprite 未正确设置或导入！");
            }
            
            return tilesetValid && riverValid;
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

        // 详细的检查方法
        [ContextMenu("检查设置")]
        public void CheckSetup()
        {
            Debug.Log("=== 地形 Sprite 系统检查 ===");
            
            // 检查 Sprite 分配
            if (tilesetSprite == null)
            {
                Debug.LogError("❌ Tileset Sprite 未分配！");
                Debug.LogError("请将 'tileset.png' 拖入 TerrainSpriteManager 的 'Tileset Sprite' 字段");
                return;
            }
            else
            {
                Debug.Log($"✅ Tileset Sprite 已分配: {tilesetSprite.name}");
            }
            
            if (roadsRiversSprite == null)
            {
                Debug.LogError("❌ Roads Rivers Sprite 未分配！");
                Debug.LogError("请将 'roads_rivers-tileset.png' 拖入 'Roads Rivers Sprite' 字段");
                return;
            }
            else
            {
                Debug.Log($"✅ Roads Rivers Sprite 已分配: {roadsRiversSprite.name}");
            }
            
            // 检查 texture
            if (tilesetSprite.texture == null)
            {
                Debug.LogError($"❌ Tileset Sprite '{tilesetSprite.name}' 的 texture 为 null！");
                Debug.LogError("PNG 导入设置有问题，请检查：");
                Debug.LogError("1. 选中 tileset.png");
                Debug.LogError("2. Inspector 中设置 Texture Type = Sprite (2D and UI)");
                Debug.LogError("3. Pixels Per Unit = 32");
                Debug.LogError("4. 点击 Apply");
                return;
            }
            else
            {
                Debug.Log($"✅ Tileset texture 正常: {tilesetSprite.texture.width}x{tilesetSprite.texture.height}");
            }
            
            if (roadsRiversSprite.texture == null)
            {
                Debug.LogError($"❌ Roads Rivers Sprite '{roadsRiversSprite.name}' 的 texture 为 null！");
                Debug.LogError("请检查 roads_rivers-tileset.png 的导入设置");
                return;
            }
            else
            {
                Debug.Log($"✅ Roads Rivers texture 正常: {roadsRiversSprite.texture.width}x{roadsRiversSprite.texture.height}");
            }
            
            Debug.Log("🎉 所有设置检查通过！现在可以生成地形了。");
        }

        // 简单的测试方法
        [ContextMenu("测试 Sprite 系统")]
        public void TestSpriteSystem()
        {
            Debug.Log("=== Sprite 系统测试 ===");
            
            if (!HasValidSprites())
            {
                Debug.LogError("❌ Sprite 设置不完整，请先使用 '检查设置' 菜单");
                return;
            }
            
            Debug.Log($"Tileset Sprite: {tilesetSprite.name}");
            Debug.Log($"Roads & Rivers Sprite: {roadsRiversSprite.name}");
            
            Sprite testSprite = GetBiomeSprite(HexCoordinateSystem.BiomeType.FlatGrass);
            Debug.Log($"FlatGrass Sprite: {(testSprite != null ? "成功" : "失败")}");
            
            // 测试 Tile 创建
            SpriteTile testTile = CreateBiomeTile(HexCoordinateSystem.BiomeType.FlatGrass);
            Debug.Log($"FlatGrass Tile: {(testTile != null ? "成功" : "失败")}");
        }
    }
} 