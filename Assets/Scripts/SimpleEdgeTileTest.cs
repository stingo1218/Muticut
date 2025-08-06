using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Tilemaps;
using System.Linq;
using TMPro;

/// <summary>
/// 简单边瓦片测试 - 检测Cell1和Cell2之间的连线经过了哪些tiles
/// </summary>
public class SimpleEdgeTileTest : MonoBehaviour
{
    [Header("测试设置")]
    [SerializeField] private bool testOnStart = true;
    [SerializeField] private float testDelay = 3f;
    [SerializeField] private LayerMask terrainLayerMask = -1;
    [SerializeField] private bool highlightAllEdges = false; // 是否高亮所有edges
    
         [Header("高亮设置")]
     [SerializeField] private bool enableHighlight = true;
     [SerializeField] private Color highlightColor = Color.red;
     [SerializeField] private float highlightAlpha = 0.7f;
     [SerializeField] private bool showTileNumbers = true;
     [SerializeField] private bool showBiomeInfo = true;
     [SerializeField] private bool autoDestroyHighlights = false; // 是否自动销毁高亮
     [SerializeField] private float highlightDuration = 5f; // 仅在autoDestroyHighlights为true时生效
    
    private void Start()
    {
        if (testOnStart)
        {
            Invoke(nameof(TestCell1ToCell2), testDelay);
        }
    }
    
    [ContextMenu("测试Cell1到Cell2的连线")]
    public void TestCell1ToCell2()
    {
        Debug.Log("🚀 开始测试Cell1到Cell2的连线...");
        
        // 获取GameManager
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogError("❌ 无法找到GameManager");
            return;
        }
        
        // 获取TerrainManager
        var terrainManagerField = typeof(GameManager).GetField("terrainManager", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        MonoBehaviour terrainManager = null;
        if (terrainManagerField != null)
        {
            terrainManager = terrainManagerField.GetValue(gameManager) as MonoBehaviour;
        }
        
        if (terrainManager == null)
        {
            Debug.LogError("❌ 无法找到TerrainManager");
            return;
        }
        
        // 获取Tilemap
        var tilemapProperty = terrainManager.GetType().GetProperty("tilemap");
        Tilemap tilemap = null;
        if (tilemapProperty != null)
        {
            tilemap = tilemapProperty.GetValue(terrainManager) as Tilemap;
        }
        
        if (tilemap == null)
        {
            Debug.LogError("❌ 无法获取Tilemap");
            return;
        }
        
        Debug.Log("✅ 组件引用获取成功");
        
        // 获取Cells
        var cellsField = typeof(GameManager).GetField("_cells", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        List<Cell> cells = null;
        if (cellsField != null)
        {
            cells = cellsField.GetValue(gameManager) as List<Cell>;
        }
        
        if (cells == null || cells.Count < 2)
        {
            Debug.LogWarning("⚠️ 没有找到足够的Cell（至少需要2个）");
            return;
        }
        
        Debug.Log($"📊 找到 {cells.Count} 个Cell");
        
        if (highlightAllEdges)
        {
            // 高亮所有edges
            HighlightAllEdges(cells, tilemap, terrainManager);
        }
        else
        {
            // 只测试Cell1和Cell2的连线
            TestSingleEdge(cells, tilemap, terrainManager);
        }
        
        Debug.Log("✅ 测试完成");
    }
    
    /// <summary>
    /// 测试单个edge
    /// </summary>
    private void TestSingleEdge(List<Cell> cells, Tilemap tilemap, MonoBehaviour terrainManager)
    {
        // 获取Cell1和Cell2
        Cell cell1 = cells[0];
        Cell cell2 = cells[1];
        
        if (cell1 == null || cell2 == null)
        {
            Debug.LogError("❌ Cell1或Cell2为空");
            return;
        }
        
        Debug.Log($"🔗 测试连线: Cell {cell1.Number} -> Cell {cell2.Number}");
        Debug.Log($"  Cell1位置: {cell1.transform.position}");
        Debug.Log($"  Cell2位置: {cell2.transform.position}");
        
        // 检测瓦片
        var crossedTiles = GetTilesCrossedByLine(cell1.transform.position, cell2.transform.position, tilemap);
        
        Debug.Log($"  经过瓦片数量: {crossedTiles.Count}");
        
        if (crossedTiles.Count > 0)
        {
            Debug.Log($"  瓦片坐标列表:");
            foreach (Vector3Int tilePos in crossedTiles)
            {
                Debug.Log($"    - {tilePos}");
            }
            
            // 使用映射表获取生物群系信息
            Debug.Log($"  生物群系信息 (使用映射表):");
            foreach (Vector3Int tilePos in crossedTiles)
            {
                int biomeType = GetBiomeUsingMap(terrainManager, tilePos);
                Debug.Log($"    瓦片 {tilePos}: {GetBiomeName(biomeType)}");
            }
            
            // 高亮检测到的瓦片
            if (enableHighlight)
            {
                HighlightTiles(crossedTiles, tilemap, terrainManager, $"edge{cell1.Number}_{cell2.Number}");
            }
        }
        else
        {
            Debug.Log("  ⚠️ 未检测到任何瓦片");
        }
    }
    
    /// <summary>
    /// 高亮所有edges
    /// </summary>
    private void HighlightAllEdges(List<Cell> cells, Tilemap tilemap, MonoBehaviour terrainManager)
    {
        Debug.Log("🔗 开始高亮所有edges...");
        
        // 创建高亮层
        GameObject highlightLayer = new GameObject("TileHighlightLayer");
        
        // 获取所有edges
        var edgesField = typeof(GameManager).GetField("_edges", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Dictionary<(Cell, Cell), (LineRenderer, int, TextMeshProUGUI, GameObject)> edges = null;
        if (edgesField != null)
        {
            edges = edgesField.GetValue(GameManager.Instance) as Dictionary<(Cell, Cell), (LineRenderer, int, TextMeshProUGUI, GameObject)>;
        }
        
        if (edges == null || edges.Count == 0)
        {
            Debug.LogWarning("⚠️ 没有找到edges");
            return;
        }
        
        Debug.Log($"📊 找到 {edges.Count} 个edges");
        
        int edgeIndex = 0;
        foreach (var edgePair in edges)
        {
            var edgeKey = edgePair.Key;
            Cell cellA = edgeKey.Item1;
            Cell cellB = edgeKey.Item2;
            
            if (cellA == null || cellB == null) continue;
            
            string edgeName = $"edge{cellA.Number}_{cellB.Number}";
            Debug.Log($"🔗 处理 {edgeName}: Cell {cellA.Number} -> Cell {cellB.Number}");
            
            // 检测瓦片
            var crossedTiles = GetTilesCrossedByLine(cellA.transform.position, cellB.transform.position, tilemap);
            
            if (crossedTiles.Count > 0)
            {
                Debug.Log($"  {edgeName} 经过瓦片数量: {crossedTiles.Count}");
                
                // 高亮这个edge的瓦片
                if (enableHighlight)
                {
                    HighlightTilesForEdge(crossedTiles, tilemap, terrainManager, edgeName, highlightLayer);
                }
            }
            
            edgeIndex++;
        }
        
        Debug.Log($"✅ 完成高亮 {edgeIndex} 个edges");
        
        // 设置自动销毁
        if (autoDestroyHighlights && highlightDuration > 0)
        {
            Destroy(highlightLayer, highlightDuration);
            Debug.Log($"⏰ 高亮将在 {highlightDuration} 秒后自动消失");
        }
        else
        {
            Debug.Log("🔒 高亮将保持显示，使用右键菜单清除高亮");
        }
    }
    
    /// <summary>
    /// 使用映射表获取生物群系
    /// </summary>
    private int GetBiomeUsingMap(MonoBehaviour terrainManager, Vector3Int tilePos)
    {
        try
        {
            // 调用TerrainManager的GetBiomeAtTile方法
            var getBiomeMethod = terrainManager.GetType().GetMethod("GetBiomeAtTile");
            if (getBiomeMethod != null)
            {
                var result = getBiomeMethod.Invoke(terrainManager, new object[] { tilePos });
                if (result != null)
                {
                    return (int)result;
                }
            }
            
            // 如果映射表方法不可用，返回-1
            Debug.LogWarning($"    无法使用映射表获取瓦片 {tilePos} 的生物群系");
            return -1;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"    获取生物群系时出错: {ex.Message}");
            return -1;
        }
    }
    
    /// <summary>
    /// 获取生物群系名称
    /// </summary>
    private string GetBiomeName(int biomeType)
    {
        switch (biomeType)
        {
            case 0: return "深水(DeepWater)";
            case 1: return "浅水(ShallowWater)";
            case 2: return "平地沙漠1(FlatDesert1)";
            case 3: return "平地沙漠2(FlatDesert2)";
            case 4: return "平地草原(FlatGrass)";
            case 5: return "平地稀疏树木1(FlatSparseTrees1)";
            case 6: return "平地稀疏树木2(FlatSparseTrees2)";
            case 7: return "平地森林(FlatForest)";
            case 8: return "平地沼泽森林(FlatForestSwampy)";
            case 9: return "丘陵沙漠(HillDesert)";
            case 10: return "丘陵草原(HillGrass)";
            case 11: return "丘陵森林(HillForest)";
            case 12: return "丘陵针叶林(HillForestNeedleleaf)";
            case 13: return "山地沙漠(MountainDesert)";
            case 14: return "山地灌木丛1(MountainShrubland1)";
            case 15: return "山地灌木丛2(MountainShrubland2)";
            case 16: return "山地高山1(MountainAlpine1)";
            case 17: return "山地高山2(MountainAlpine2)";
            case 18: return "山地不可通行1(MountainImpassable1)";
            case 19: return "山地不可通行2(MountainImpassable2)";
            case 20: return "湖泊1(Lake1)";
            case 21: return "湖泊2(Lake2)";
            case 22: return "湖泊3(Lake3)";
            case 23: return "湖泊4(Lake4)";
            case 24: return "火山(Volcano)";
            case 25: return "巢穴(Lair)";
            case 26: return "雪地巢穴(LairSnow)";
            case 27: return "沙漠巢穴(LairDesert)";
            case -1: return "未知生物群系(映射表未找到)";
            default: return $"未知生物群系({biomeType})";
        }
    }
    
    /// <summary>
    /// 获取线段经过的瓦片
    /// </summary>
    private HashSet<Vector3Int> GetTilesCrossedByLine(Vector2 start, Vector2 end, Tilemap tilemap)
    {
        HashSet<Vector3Int> crossedTiles = new HashSet<Vector3Int>();
        
        if (tilemap == null) return crossedTiles;
        
        // 分段检测
        Vector2 direction = (end - start).normalized;
        float distance = Vector2.Distance(start, end);
        
        // 每0.5单位一个检测点
        int segments = Mathf.Max(1, Mathf.CeilToInt(distance / 0.5f));
        float segmentLength = distance / segments;
        
        for (int i = 0; i <= segments; i++)
        {
            Vector2 checkPoint = start + direction * (segmentLength * i);
            Vector3Int tilePos = tilemap.WorldToCell(checkPoint);
            
            // 使用(X,Y,Z)格式，与TerrainManager的ConvertHexToTilePosition保持一致
            Vector3Int adjustedTilePos = new Vector3Int(tilePos.x, tilePos.y, tilePos.z);
            
            if (tilemap.HasTile(tilePos))
            {
                crossedTiles.Add(adjustedTilePos);
            }
        }
        
        // 额外使用Physics2D.LinecastAll进行更精确的检测
        RaycastHit2D[] hits = Physics2D.LinecastAll(start, end, terrainLayerMask);
        
        foreach (var hit in hits)
        {
            if (hit.collider != null)
            {
                Vector3Int tilePos = tilemap.WorldToCell(hit.point);
                // 使用(X,Y,Z)格式，与TerrainManager的ConvertHexToTilePosition保持一致
                Vector3Int adjustedTilePos = new Vector3Int(tilePos.x, tilePos.y, tilePos.z);
                
                if (tilemap.HasTile(tilePos))
                {
                    crossedTiles.Add(adjustedTilePos);
                }
            }
        }
        
        return crossedTiles;
    }
    
         /// <summary>
     /// 高亮指定的瓦片
     /// </summary>
     private void HighlightTiles(HashSet<Vector3Int> tiles, Tilemap tilemap, MonoBehaviour terrainManager, string edgeName)
     {
         if (tiles == null || tiles.Count == 0 || tilemap == null) return;
         
         Debug.Log($"🎨 开始高亮 {tiles.Count} 个瓦片...");
         
         // 创建高亮层（独立于terrain）
         GameObject highlightLayer = new GameObject("TileHighlightLayer");
         // 不设置父对象，让高亮层独立存在
         
         // 创建edge节点
         GameObject edgeNode = new GameObject(edgeName);
         edgeNode.transform.SetParent(highlightLayer.transform);
         
         int tileIndex = 0;
         foreach (Vector3Int tilePos in tiles)
         {
             // 获取瓦片的世界坐标
             Vector3 worldPos = tilemap.CellToWorld(tilePos);
             
             // 获取生物群系信息用于命名
             int biomeType = GetBiomeUsingMap(terrainManager, tilePos);
             string biomeName = GetBiomeName(biomeType);
             string shortBiomeName = biomeName.Split('(')[0]; // 只显示中文部分
             
             // 创建六边形高亮
             GameObject highlightHex = CreateHexagonHighlight();
             highlightHex.name = $"#{tileIndex}_{shortBiomeName}";
             highlightHex.transform.SetParent(edgeNode.transform); // 放在edge节点下
             highlightHex.transform.position = worldPos + Vector3.forward * 0.1f; // 稍微向前偏移
             
             // 设置材质和颜色
             Renderer renderer = highlightHex.GetComponent<Renderer>();
             Material highlightMaterial = new Material(Shader.Find("Sprites/Default"));
             highlightMaterial.color = new Color(highlightColor.r, highlightColor.g, highlightColor.b, highlightAlpha);
             renderer.material = highlightMaterial;
             
             // 添加文本标签
             if (showTileNumbers || showBiomeInfo)
             {
                 CreateTileLabel(highlightHex, tilePos, tileIndex, terrainManager);
             }
             
             Debug.Log($"  🎯 高亮瓦片 {tilePos} 在位置 {worldPos}");
             tileIndex++;
         }
         
         Debug.Log($"✅ 高亮完成，共高亮 {tiles.Count} 个瓦片");
         
         // 设置自动销毁（仅在启用时）
         if (autoDestroyHighlights && highlightDuration > 0)
         {
             Destroy(highlightLayer, highlightDuration);
             Debug.Log($"⏰ 高亮将在 {highlightDuration} 秒后自动消失");
         }
         else
         {
             Debug.Log("🔒 高亮将保持显示，使用右键菜单清除高亮");
         }
     }
     
     /// <summary>
     /// 为指定edge高亮瓦片（在已存在的高亮层下）
     /// </summary>
     private void HighlightTilesForEdge(HashSet<Vector3Int> tiles, Tilemap tilemap, MonoBehaviour terrainManager, string edgeName, GameObject highlightLayer)
     {
         if (tiles == null || tiles.Count == 0 || tilemap == null || highlightLayer == null) return;
         
         Debug.Log($"🎨 为 {edgeName} 高亮 {tiles.Count} 个瓦片...");
         
         // 创建edge节点
         GameObject edgeNode = new GameObject(edgeName);
         edgeNode.transform.SetParent(highlightLayer.transform);
         
         int tileIndex = 0;
         foreach (Vector3Int tilePos in tiles)
         {
             // 获取瓦片的世界坐标
             Vector3 worldPos = tilemap.CellToWorld(tilePos);
             
             // 获取生物群系信息用于命名
             int biomeType = GetBiomeUsingMap(terrainManager, tilePos);
             string biomeName = GetBiomeName(biomeType);
             string shortBiomeName = biomeName.Split('(')[0]; // 只显示中文部分
             
             // 创建六边形高亮
             GameObject highlightHex = CreateHexagonHighlight();
             highlightHex.name = $"#{tileIndex}_{shortBiomeName}";
             highlightHex.transform.SetParent(edgeNode.transform); // 放在edge节点下
             highlightHex.transform.position = worldPos + Vector3.forward * 0.1f; // 稍微向前偏移
             
             // 设置材质和颜色
             Renderer renderer = highlightHex.GetComponent<Renderer>();
             Material highlightMaterial = new Material(Shader.Find("Sprites/Default"));
             highlightMaterial.color = new Color(highlightColor.r, highlightColor.g, highlightColor.b, highlightAlpha);
             renderer.material = highlightMaterial;
             
             // 添加文本标签
             if (showTileNumbers || showBiomeInfo)
             {
                 CreateTileLabel(highlightHex, tilePos, tileIndex, terrainManager);
             }
             
             tileIndex++;
         }
         
         Debug.Log($"✅ {edgeName} 高亮完成，共高亮 {tiles.Count} 个瓦片");
     }
     
     /// <summary>
     /// 创建六边形高亮对象
     /// </summary>
     private GameObject CreateHexagonHighlight()
     {
         GameObject hexObject = new GameObject("HexHighlight");
         
         // 创建MeshFilter和MeshRenderer
         MeshFilter meshFilter = hexObject.AddComponent<MeshFilter>();
         MeshRenderer meshRenderer = hexObject.AddComponent<MeshRenderer>();
         
         // 创建六边形网格
         Mesh hexMesh = new Mesh();
         
         // 六边形的顶点（平面六边形）
         Vector3[] vertices = new Vector3[7];
         float radius = 0.5f; // 六边形半径
         
         // 中心点
         vertices[0] = Vector3.zero;
         
         // 六个顶点
         for (int i = 0; i < 6; i++)
         {
             float angle = i * 60f * Mathf.Deg2Rad;
             vertices[i + 1] = new Vector3(
                 radius * Mathf.Cos(angle),
                 radius * Mathf.Sin(angle),
                 0
             );
         }
         
         // 三角形索引（扇形三角形）
         int[] triangles = new int[18];
         for (int i = 0; i < 6; i++)
         {
             triangles[i * 3] = 0; // 中心点
             triangles[i * 3 + 1] = i + 1;
             triangles[i * 3 + 2] = (i + 1) % 6 + 1;
         }
         
         hexMesh.vertices = vertices;
         hexMesh.triangles = triangles;
         hexMesh.RecalculateNormals();
         
         meshFilter.mesh = hexMesh;
         
         return hexObject;
     }
    
         /// <summary>
     /// 创建瓦片标签
     /// </summary>
     private void CreateTileLabel(GameObject parent, Vector3Int tilePos, int index, MonoBehaviour terrainManager)
     {
         // 创建文本对象
         GameObject textObj = new GameObject($"TileLabel_{tilePos.x}_{tilePos.y}_{tilePos.z}");
         textObj.transform.SetParent(parent.transform);
         textObj.transform.localPosition = Vector3.zero + Vector3.up * 0.8f; // 稍微向上偏移
         
         // 添加TextMeshPro组件
         TextMeshPro textMeshPro = textObj.AddComponent<TextMeshPro>();
         textMeshPro.fontSize = 8; // TMP字体大小
         textMeshPro.color = Color.white;
         textMeshPro.alignment = TextAlignmentOptions.Center;
         textMeshPro.fontStyle = FontStyles.Bold; // 加粗
         
         // 构建标签文本
         string labelText = "";
         if (showTileNumbers)
         {
             labelText += $"#{index}\n";
         }
         labelText += $"({tilePos.x},{tilePos.y})";
         
         if (showBiomeInfo)
         {
             int biomeType = GetBiomeUsingMap(terrainManager, tilePos);
             string biomeName = GetBiomeName(biomeType);
             // 简化生物群系名称显示
             string shortBiomeName = biomeName.Split('(')[0]; // 只显示中文部分
             labelText += $"\n{shortBiomeName}";
         }
         
         textMeshPro.text = labelText;
         
         // 添加背景
         GameObject background = GameObject.CreatePrimitive(PrimitiveType.Quad);
         background.name = "LabelBackground";
         background.transform.SetParent(textObj.transform);
         background.transform.localPosition = Vector3.zero;
         background.transform.localScale = Vector3.one * 1.2f; // 增大背景
         
         Renderer bgRenderer = background.GetComponent<Renderer>();
         Material bgMaterial = new Material(Shader.Find("Sprites/Default"));
         bgMaterial.color = new Color(0, 0, 0, 0.9f); // 更深的背景
         bgRenderer.material = bgMaterial;
         
         DestroyImmediate(background.GetComponent<Collider>());
     }
     
     /// <summary>
     /// 清除所有高亮
     /// </summary>
     [ContextMenu("清除高亮")]
     public void ClearHighlights()
     {
         GameObject highlightLayer = GameObject.Find("TileHighlightLayer");
         if (highlightLayer != null)
         {
             DestroyImmediate(highlightLayer);
             Debug.Log("🧹 已清除所有高亮");
         }
     }
     
     /// <summary>
     /// 测试所有edges
     /// </summary>
     [ContextMenu("测试所有Edges")]
     public void TestAllEdges()
     {
         // 临时设置highlightAllEdges为true
         bool originalSetting = highlightAllEdges;
         highlightAllEdges = true;
         
         // 执行测试
         TestCell1ToCell2();
         
         // 恢复原设置
         highlightAllEdges = originalSetting;
     }
 } 