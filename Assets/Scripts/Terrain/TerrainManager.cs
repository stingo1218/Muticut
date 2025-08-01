using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace TerrainSystem
{
    // 地形管理器（主控制器，整合所有地形生成功能）
    public class TerrainManager : MonoBehaviour
    {
        [Header("地形设置")]
        public TerrainSettings settings = new TerrainSettings();
        
        [Header("位置和缩放")]
        [SerializeField] private Vector3 terrainOffset = Vector3.zero;
        [SerializeField] private float terrainScale = 1.0f;

                               [Header("渲染设置")]
        [SerializeField] private TerrainSpriteManager spriteManager;
        [SerializeField] private Material terrainMaterial;
        [SerializeField] private Material riverMaterial;
        [SerializeField] private GameObject hexTilePrefab;
        [SerializeField] private GameObject riverTilePrefab;
        
        [Header("Tilemap 设置")]
        [SerializeField] private Grid targetGrid;
        [SerializeField] private UnityEngine.Tilemaps.Tilemap terrainTilemap;
        [SerializeField] private UnityEngine.Tilemaps.Tilemap riverTilemap;
        [SerializeField] private bool useTilemap = true;

        [Header("调试选项")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool autoGenerateOnStart = false;
        [SerializeField] private bool showBiomeInfo = true;
        [SerializeField] private bool useColorMode = true;
        
        [Header("Map Hash")]
        [SerializeField] [TextArea(3, 5)] private string mapHash = ""; // Map Hash 输入/输出

        // 核心组件
        private NoiseGenerator noiseGenerator;
        private HexCoordinateSystem hexSystem;
        private BiomeMapper biomeMapper;
        private RiverGenerator riverGenerator;

        // 地形数据
        private List<HexCoordinateSystem.HexTile> hexTiles;
        private float[,] elevationMap;
        private float[,] moistureMap;

        // 渲染对象
        private Transform terrainRoot;
        private Transform riverRoot;

        private void Awake()
        {
            InitializeComponents();
            CreateRenderRoots();
        }

        private void Start()
        {
            if (autoGenerateOnStart)
            {
                GenerateTerrain();
            }
        }

        // 初始化核心组件
        private void InitializeComponents()
        {
            // 创建噪声生成器
            string elevationSeed = settings.setElevationSeed ? settings.elevationSeed : null;
            string moistureSeed = settings.setMoistureSeed ? settings.moistureSeed : null;
            
            noiseGenerator = new NoiseGenerator(elevationSeed);
            
            // 创建六边形坐标系统
            hexSystem = new HexCoordinateSystem(HexCoordinateSystem.HexOrientation.Flat, settings.hexSize);
            
            // 创建生物群系映射器
            biomeMapper = new BiomeMapper(noiseGenerator);
            
            // 创建河流生成器
            riverGenerator = new RiverGenerator(hexSystem, biomeMapper, noiseGenerator);
        }

        // 创建渲染根节点
        private void CreateRenderRoots()
        {
            // 创建地形根节点
            GameObject terrainRootObj = new GameObject("TerrainRoot");
            terrainRootObj.transform.SetParent(transform);
            terrainRoot = terrainRootObj.transform;

            // 创建河流根节点
            GameObject riverRootObj = new GameObject("RiverRoot");
            riverRootObj.transform.SetParent(transform);
            riverRoot = riverRootObj.transform;
        }

        // 生成地形（主函数）

        public void GenerateTerrain()
        {
            Debug.Log("开始生成地形...");
            
            // 确保组件已初始化
            if (noiseGenerator == null)
            {
                InitializeComponents();
            }
            
            // 清理现有地形
            ClearTerrain();
            
            // 生成噪声图
            GenerateNoiseMaps();
            
            // 生成六边形网格
            GenerateHexGrid();
            
            // 确定生物群系
            DetermineBiomes();
            
            // 生成河流
            GenerateRivers();
            
            // 渲染地形
            RenderTerrain();
            
                    Debug.Log($"地形生成完成！共生成 {hexTiles.Count} 个六边形");
    }

    // 清理生成的地形
    
    public void ClearGeneratedTerrain()
    {
        int clearedCount = 0;
        
        // 清理 Tilemap（如果使用）
        if (useTilemap)
        {
            if (terrainTilemap != null)
            {
                terrainTilemap.SetTilesBlock(terrainTilemap.cellBounds, new TileBase[terrainTilemap.cellBounds.size.x * terrainTilemap.cellBounds.size.y * terrainTilemap.cellBounds.size.z]);
            }
            if (riverTilemap != null)
            {
                riverTilemap.SetTilesBlock(riverTilemap.cellBounds, new TileBase[riverTilemap.cellBounds.size.x * riverTilemap.cellBounds.size.y * riverTilemap.cellBounds.size.z]);
            }
        }
        
        // 清理 GameObject 地形
        if (terrainRoot != null)
        {
            foreach (Transform child in terrainRoot)
            {
                if (child != null)
                {
                    DestroyImmediate(child.gameObject);
                    clearedCount++;
                }
            }
        }

        Debug.Log($"TerrainManager 清空完成！删除了 {clearedCount} 个对象");
    }

        // 生成噪声图
        private void GenerateNoiseMaps()
        {
            Debug.Log("生成噪声图...");
            
            // 设置种子
            if (!settings.setElevationSeed)
            {
                settings.elevationSeed = noiseGenerator.GenerateId();
            }
            if (!settings.setMoistureSeed)
            {
                settings.moistureSeed = noiseGenerator.GenerateId();
            }
            
            // 生成高度图和湿度图
            elevationMap = noiseGenerator.GenerateHeightMap(settings.hexColumns, settings.hexRows, settings);
            moistureMap = noiseGenerator.GenerateMoistureMap(settings.hexColumns, settings.hexRows, settings);
            
            Debug.Log($"噪声图生成完成 - 高度种子: {settings.elevationSeed}, 湿度种子: {settings.moistureSeed}");
        }

        // 生成六边形网格
        private void GenerateHexGrid()
        {
            Debug.Log("生成六边形网格...");
            
            hexTiles = hexSystem.GenerateRectangularGrid(settings.hexColumns, settings.hexRows);
            
            // 设置每个六边形的高度和湿度
            for (int q = 0; q < settings.hexColumns; q++)
            {
                for (int r = 0; r < settings.hexRows; r++)
                {
                    // 修正索引计算：外层循环是q（列），内层循环是r（行）
                    int index = q * settings.hexRows + r;
                    HexCoordinateSystem.HexTile hex = hexTiles[index];
                    
                    hex.elevation = elevationMap[q, r];
                    hex.moisture = moistureMap[q, r];
                }
            }
            
            Debug.Log($"六边形网格生成完成 - 尺寸: {settings.hexColumns}x{settings.hexRows}");
        }

        // 确定生物群系
        private void DetermineBiomes()
        {
            Debug.Log("确定生物群系...");
            
            foreach (HexCoordinateSystem.HexTile hex in hexTiles)
            {
                biomeMapper.DetermineBiome(hex, settings);
            }
            
            Debug.Log("生物群系确定完成");
        }

        // 生成河流
        private void GenerateRivers()
        {
            Debug.Log("暂时跳过河流生成...");
            
            // 暂时注释掉河流生成，避免 Sprite 错误
            // riverGenerator.GenerateRivers(hexTiles, settings, 3);
            
            Debug.Log("河流生成跳过");
        }

        // 渲染地形
        private void RenderTerrain()
        {
            Debug.Log("渲染地形...");
            
            if (useTilemap && terrainTilemap != null)
            {
                // 使用 Tilemap 渲染
                RenderToTilemap();
            }
            else
            {
                // 使用 GameObject 渲染
                foreach (HexCoordinateSystem.HexTile hex in hexTiles)
                {
                    // 创建六边形瓦片
                    CreateHexTile(hex);
                    
                    // 暂时跳过河流瓦片渲染
                    // if (hex.hasRiver)
                    // {
                    //     CreateRiverTile(hex);
                    // }
                }
            }
            
            Debug.Log("地形渲染完成");
        }

        // 渲染到 Tilemap
        private void RenderToTilemap()
        {
            Debug.Log("使用 Tilemap 渲染地形...");
            
            // 清理现有瓦片
            if (terrainTilemap != null)
            {
                terrainTilemap.SetTilesBlock(terrainTilemap.cellBounds, new TileBase[terrainTilemap.cellBounds.size.x * terrainTilemap.cellBounds.size.y]);
            }
            
            if (spriteManager == null)
            {
                Debug.LogError("需要 TerrainSpriteManager 来创建 Sprite Tiles！");
                return;
            }

            foreach (HexCoordinateSystem.HexTile hex in hexTiles)
            {
                // 转换六边形坐标到瓦片坐标
                Vector3Int tilePosition = ConvertHexToTilePosition(hex);
                
                // 创建地形瓦片
                if (terrainTilemap != null)
                {
                    SpriteTile hexTile = spriteManager.CreateHexTileForTilemap(hex);
                    if (hexTile != null)
                    {
                        terrainTilemap.SetTile(tilePosition, hexTile);
                        Debug.Log($"🗺️ 设置地形瓦片 {tilePosition}: {hex.biome} (高度:{hex.elevation:F2}, 湿度:{hex.moisture:F2})");
                    }
                }
                
                // 创建河流瓦片（如果有河流且有河流 Tilemap）
                if (hex.hasRiver && riverTilemap != null)
                {
                    SpriteTile riverTile = spriteManager.CreateRiverTileForTilemap(hex);
                    if (riverTile != null)
                    {
                        riverTilemap.SetTile(tilePosition, riverTile);
                        Debug.Log($"设置河流瓦片 {tilePosition}");
                    }
                }
            }
            
            Debug.Log("Tilemap 渲染完成");
        }

        // 转换六边形坐标到瓦片位置
        private Vector3Int ConvertHexToTilePosition(HexCoordinateSystem.HexTile hex)
        {
            // 将坐标中心化，让地形以原点为中心
            int centerX = settings.hexColumns / 2;
            int centerY = settings.hexRows / 2;
            
            // 修正：交换X和Y坐标以匹配预期的布局
            // q应该对应Y轴（垂直），r应该对应X轴（水平）
            return new Vector3Int(
                hex.coord.r - centerY,  // r坐标对应X轴（水平） 
                hex.coord.q - centerX,  // q坐标对应Y轴（垂直）
                0
            );
        }

                       // 创建六边形瓦片
               private void CreateHexTile(HexCoordinateSystem.HexTile hex)
               {
                   GameObject hexObj;
                   
                   // 计算调整后的位置
                   Vector3 adjustedPosition = (hex.worldPosition * terrainScale) + terrainOffset;
                   
                                   if (!useColorMode && spriteManager != null && spriteManager.HasValidSprites())
                {
                    // 使用 Sprite 管理器创建瓦片
                    hexObj = spriteManager.CreateHexTile(hex, terrainRoot);
                    if (hexObj != null)
                    {
                        hexObj.transform.position = adjustedPosition;
                    }
                }
                else
                   {
                       // 使用默认方法创建瓦片
                       if (hexTilePrefab != null)
                       {
                           hexObj = Instantiate(hexTilePrefab, adjustedPosition, Quaternion.identity, terrainRoot);
                       }
                       else
                       {
                           // 创建默认六边形
                           hexObj = new GameObject($"Hex_{hex.coord.q}_{hex.coord.r}");
                           hexObj.transform.SetParent(terrainRoot);
                           hexObj.transform.position = adjustedPosition;
                           
                           // 添加 SpriteRenderer
                           SpriteRenderer sr = hexObj.AddComponent<SpriteRenderer>();
                           if (terrainMaterial != null)
                           {
                               sr.material = terrainMaterial;
                           }
                           
                           // 设置颜色（根据生物群系）
                           sr.color = GetBiomeColor(hex.biome);
                       }
                       
                       // 设置标签
                       hexObj.name = $"Hex_{hex.coord.q}_{hex.coord.r}_{hex.tileName}";
                   }
                   
                   // 添加调试信息
                   if (showDebugInfo)
                   {
                       AddDebugInfo(hexObj, hex);
                   }
               }

                       // 创建河流瓦片
               private void CreateRiverTile(HexCoordinateSystem.HexTile hex)
               {
                   if (!hex.hasRiver) return;
                   
                   GameObject riverObj;
                   
                   if (spriteManager != null && spriteManager.HasValidSprites())
                   {
                       // 使用 Sprite 管理器创建河流瓦片
                       riverObj = spriteManager.CreateRiverTile(hex, riverRoot);
                   }
                   else
                   {
                       // 使用默认方法创建河流瓦片
                       if (riverTilePrefab != null)
                       {
                           Vector3 riverPos = riverGenerator.GetRiverWorldPosition(hex);
                           riverObj = Instantiate(riverTilePrefab, riverPos, Quaternion.identity, riverRoot);
                       }
                       else
                       {
                           // 创建默认河流瓦片
                           Vector3 riverPos = riverGenerator.GetRiverWorldPosition(hex);
                           riverObj = new GameObject($"River_{hex.coord.q}_{hex.coord.r}");
                           riverObj.transform.SetParent(riverRoot);
                           riverObj.transform.position = riverPos;
                           
                           // 添加 SpriteRenderer
                           SpriteRenderer sr = riverObj.AddComponent<SpriteRenderer>();
                           if (riverMaterial != null)
                           {
                               sr.material = riverMaterial;
                           }
                           sr.color = Color.blue;
                       }
                       
                       riverObj.name = $"River_{hex.coord.q}_{hex.coord.r}";
                   }
               }

        // 获取生物群系颜色
        private Color GetBiomeColor(HexCoordinateSystem.BiomeType biome)
        {
            switch (biome)
            {
                case HexCoordinateSystem.BiomeType.DeepWater:
                    return new Color(0.2f, 0.3f, 0.8f);
                case HexCoordinateSystem.BiomeType.ShallowWater:
                    return new Color(0.4f, 0.6f, 0.9f);
                case HexCoordinateSystem.BiomeType.FlatDesert1:
                case HexCoordinateSystem.BiomeType.FlatDesert2:
                    return new Color(0.9f, 0.8f, 0.6f);
                case HexCoordinateSystem.BiomeType.FlatGrass:
                    return new Color(0.4f, 0.8f, 0.4f);
                case HexCoordinateSystem.BiomeType.FlatSparseTrees1:
                case HexCoordinateSystem.BiomeType.FlatSparseTrees2:
                    return new Color(0.3f, 0.7f, 0.3f);
                case HexCoordinateSystem.BiomeType.FlatForest:
                case HexCoordinateSystem.BiomeType.FlatForestSwampy:
                    return new Color(0.2f, 0.6f, 0.2f);
                case HexCoordinateSystem.BiomeType.HillDesert:
                    return new Color(0.8f, 0.7f, 0.5f);
                case HexCoordinateSystem.BiomeType.HillGrass:
                    return new Color(0.5f, 0.9f, 0.5f);
                case HexCoordinateSystem.BiomeType.HillForest:
                case HexCoordinateSystem.BiomeType.HillForestNeedleleaf:
                    return new Color(0.3f, 0.8f, 0.3f);
                case HexCoordinateSystem.BiomeType.MountainDesert:
                    return new Color(0.7f, 0.6f, 0.4f);
                case HexCoordinateSystem.BiomeType.MountainShrubland1:
                case HexCoordinateSystem.BiomeType.MountainShrubland2:
                    return new Color(0.6f, 0.8f, 0.6f);
                case HexCoordinateSystem.BiomeType.MountainAlpine1:
                case HexCoordinateSystem.BiomeType.MountainAlpine2:
                    return new Color(0.8f, 0.9f, 1.0f);
                case HexCoordinateSystem.BiomeType.MountainImpassable1:
                case HexCoordinateSystem.BiomeType.MountainImpassable2:
                    return new Color(0.5f, 0.5f, 0.5f);
                case HexCoordinateSystem.BiomeType.Volcano:
                    return new Color(0.8f, 0.2f, 0.2f);
                default:
                    return Color.gray;
            }
        }

        // 添加调试信息
        private void AddDebugInfo(GameObject hexObj, HexCoordinateSystem.HexTile hex)
        {
            if (showBiomeInfo)
            {
                // 添加文本显示生物群系信息
                GameObject textObj = new GameObject("DebugText");
                textObj.transform.SetParent(hexObj.transform);
                textObj.transform.localPosition = Vector3.zero;
                
                // 这里可以添加 TextMesh 组件显示调试信息
                // 为了简化，暂时用 Debug.Log
                Debug.Log($"六边形 {hex.coord}: {hex.biome} - 高度: {hex.elevation:F2} - 湿度: {hex.moisture:F2}");
            }
        }

        // 清理地形

        public void ClearTerrain()
        {
            if (terrainRoot != null)
            {
                DestroyImmediate(terrainRoot.gameObject);
            }
            if (riverRoot != null)
            {
                DestroyImmediate(riverRoot.gameObject);
            }
            
            CreateRenderRoots();
            hexTiles?.Clear();
            
            Debug.Log("地形已清理");
        }

        // 重新生成地形

        public void RegenerateTerrain()
        {
            ClearTerrain();
            GenerateTerrain();
        }



        // 获取地形数据（供其他系统使用）
        public List<HexCoordinateSystem.HexTile> GetHexTiles()
        {
            return hexTiles;
        }

        // 获取特定坐标的六边形
        public HexCoordinateSystem.HexTile GetHexAt(int q, int r)
        {
            if (hexTiles == null) return null;
            
            return hexTiles.Find(hex => hex.coord.q == q && hex.coord.r == r);
        }

        // 获取世界坐标对应的六边形
        public HexCoordinateSystem.HexTile GetHexAtWorldPosition(Vector3 worldPos)
        {
            if (hexTiles == null) return null;
            
            HexCoordinateSystem.AxialCoord coord = hexSystem.WorldToAxial(worldPos);
            return GetHexAt(coord.q, coord.r);
        }

        // 设置地形设置
        public void SetTerrainSettings(TerrainSettings newSettings)
        {
            settings = newSettings;
            InitializeComponents();
        }

        // 获取当前设置
        public TerrainSettings GetTerrainSettings()
        {
            return settings;
        }

        // 重置为 JavaScript 版本默认设置

        public void ResetToJSDefaults()
        {
            Debug.Log("🔄 重置地形设置为 JavaScript 版本默认值...");
            
            settings = new TerrainSettings();
            
            Debug.Log("✅ 地形设置已重置为 JS 默认值：");
            Debug.Log($"  - 网格大小: {settings.hexColumns} × {settings.hexRows}");
            Debug.Log($"  - 六边形尺寸: {settings.hexSize}");
            Debug.Log($"  - 高度频率: {settings.frequencyElevation}");
            Debug.Log($"  - 湿度频率: {settings.frequencyMoisture}");
            Debug.Log($"  - 轮廓间隔: {settings.contourInterval_0}, {settings.contourInterval_1}, {settings.contourInterval_2}, {settings.contourInterval_3}, {settings.contourInterval_4}");
            
            // 重新初始化组件以应用新设置
            InitializeComponents();
        }
        
        // ========== Map Hash 功能 ==========
        
        /// <summary>
        /// 导出当前设置为 Map Hash
        /// </summary>

        public void ExportMapHash()
        {
            mapHash = settings.ToMapHash();
            GUIUtility.systemCopyBuffer = mapHash;
            
            Debug.Log("🔑 Map Hash 导出成功并复制到剪贴板！");
            Debug.Log($"📄 Hash 长度: {mapHash.Length} 字符");
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
        
        /// <summary>
        /// 从 Map Hash 导入设置
        /// </summary>

        public void ImportMapHash()
        {
            if (string.IsNullOrEmpty(mapHash))
            {
                Debug.LogWarning("⚠️ Map Hash 为空！");
                return;
            }
            
            try
            {
                settings = TerrainSettings.FromMapHash(mapHash);
                Debug.Log("✅ Map Hash 导入成功！");
                
                // 重新初始化并生成地形
                InitializeComponents();
                GenerateTerrain();
                
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
                #endif
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Map Hash 导入失败: {e.Message}");
            }
        }
        
        /// <summary>
        /// 从剪贴板导入 Map Hash
        /// </summary>

        public void ImportFromClipboard()
        {
            string clipboardText = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(clipboardText))
            {
                Debug.LogWarning("⚠️ 剪贴板为空！");
                return;
            }
            
            mapHash = clipboardText;
            ImportMapHash();
        }

        // 导出地形数据（供 multicut 游戏使用）
        public TerrainData ExportTerrainData()
        {
            return new TerrainData
            {
                hexTiles = hexTiles,
                elevationMap = elevationMap,
                moistureMap = moistureMap,
                settings = settings
            };
        }
    }

    // 地形数据结构（用于导出）
    [System.Serializable]
    public class TerrainData
    {
        public List<HexCoordinateSystem.HexTile> hexTiles;
        public float[,] elevationMap;
        public float[,] moistureMap;
        public TerrainSettings settings;
    }
} 