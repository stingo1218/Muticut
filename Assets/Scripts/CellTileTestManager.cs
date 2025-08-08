using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using TerrainSystem;
using System.Linq;

/// <summary>
/// 簇数据结构
/// </summary>
[System.Serializable]
public class ClusterData
{
    public Cluster[] clusters;
}

[System.Serializable]
public class Cluster
{
    public int[] cells; // Cell编号数组
}

/// <summary>
/// 切割边数据结构（用于解析output.json）
/// </summary>
[System.Serializable]
public class CutEdgeData
{
    public CutEdge[] cut_edges;
    public int cost;
}

[System.Serializable]
public class CutEdge
{
    public int u;
    public int v;
}

/// <summary>
/// 简化版Cell-Tile高亮管理器
/// 功能：给每个Cell分配陆地Tiles，并用不同颜色高亮显示
/// </summary>
public class CellTileTestManager : MonoBehaviour
{
    [Header("基础设置")]
    [SerializeField] private bool useGameManagerCells = true; // 使用GameManager生成的Cells
    [SerializeField] private MonoBehaviour gameManager; // GameManager引用
    [SerializeField] private MonoBehaviour terrainManager; // 地形管理器引用
    
    [Header("边界设置")]
    [SerializeField] private float lineWidth = 0.2f; // 边界线宽度
    [SerializeField] private int sortingOrder = 10; // 渲染层级（数值越大越靠前）
    [SerializeField] private float zOffset = -0.5f; // Z轴偏移（负值表示靠前）
    
    [Header("自动设置")]
    [SerializeField] private bool autoFindGameManager = true; // 自动查找GameManager
    [SerializeField] private bool autoFindTerrainManager = true; // 自动查找TerrainManager
    
    [Header("UI控制")]
    [SerializeField] private UnityEngine.UI.Toggle showEcoZonesToggle; // 生态区显示开关
    
    [Header("簇合并设置")]
    [SerializeField] private bool enableClusterMode = false; // 启用簇合并模式
    [SerializeField] private string clusterDataPath = "output.json"; // 簇数据文件路径
    [SerializeField] private bool autoMonitorCost = true; // 自动监听cost变化
    [SerializeField] private float costCheckInterval = 1.0f; // cost检查间隔（秒）
    
    // 私有变量
    private List<Cell> cells = new List<Cell>();
    private Dictionary<Cell, List<Vector3Int>> cellTileAssignment = new Dictionary<Cell, List<Vector3Int>>();
    private Dictionary<Cell, Color> cellColors = new Dictionary<Cell, Color>();
    private List<GameObject> highlightObjects = new List<GameObject>();
    
    // 缓存标志
    private bool isDataInitialized = false;
    private bool isHighlightVisible = false;
    
    // 簇合并相关
    private Dictionary<Cell, int> cellClusterAssignment = new Dictionary<Cell, int>(); // Cell到簇的映射
    private Dictionary<int, Color> clusterColors = new Dictionary<int, Color>(); // 簇到颜色的映射
    private bool useClusterMode = false; // 是否使用簇模式
    
    // Cost监听相关
    private int lastKnownCost = int.MaxValue; // 上次检查的cost值
    private float lastCostCheckTime = 0f; // 上次检查cost的时间
    
    // 预定义淡色
    private Color[] predefinedColors = {
        new Color(0.8f, 0.3f, 0.3f, 0.6f), // 淡红色
        new Color(0.3f, 0.8f, 0.3f, 0.6f), // 淡绿色
        new Color(0.3f, 0.3f, 0.8f, 0.6f), // 淡蓝色
        new Color(0.8f, 0.8f, 0.3f, 0.6f), // 淡黄色
        new Color(0.3f, 0.8f, 0.8f, 0.6f), // 淡青色
        new Color(0.8f, 0.3f, 0.8f, 0.6f), // 淡紫色
        new Color(0.8f, 0.5f, 0.3f, 0.6f), // 淡橙色
        new Color(0.5f, 0.3f, 0.8f, 0.6f), // 淡紫罗兰
        new Color(0.8f, 0.3f, 0.5f, 0.6f), // 淡粉色
        new Color(0.3f, 0.8f, 0.5f, 0.6f), // 淡青绿
        new Color(0.5f, 0.8f, 0.3f, 0.6f), // 淡黄绿
        new Color(0.6f, 0.6f, 0.6f, 0.6f)  // 淡灰色
    };
    
    private void Start()
    {
        // 自动设置
        if (autoFindGameManager && gameManager == null)
        {
            AutoFindGameManager();
        }
        
        if (autoFindTerrainManager && terrainManager == null)
        {
            AutoFindTerrainManager();
        }
        
        // 设置Toggle监听
        SetupToggleListener();
        
        // 初始化生态区数据（但不立即显示）
        InitializeEcoZones();
    }
    
    private void Update()
    {
        // 自动监听cost变化
        if (autoMonitorCost && enableClusterMode)
        {
            CheckCostChange();
        }
    }
    
    /// <summary>
    /// 自动查找GameManager
    /// </summary>
    private void AutoFindGameManager()
    {
        MonoBehaviour[] allComponents = FindObjectsOfType<MonoBehaviour>();
        foreach (var component in allComponents)
        {
            if (component.GetType().Name == "GameManager")
            {
                gameManager = component;
                Debug.Log($"🔍 自动找到GameManager: {component.name}");
                return;
            }
        }
        Debug.LogWarning("⚠️ 未找到GameManager，请手动设置");
    }
    
    /// <summary>
    /// 自动查找TerrainManager
    /// </summary>
    private void AutoFindTerrainManager()
    {
        MonoBehaviour[] allComponents = FindObjectsOfType<MonoBehaviour>();
        foreach (var component in allComponents)
        {
            if (component.GetType().Name == "TerrainManager")
            {
                terrainManager = component;
                Debug.Log($"🔍 自动找到TerrainManager: {component.name}");
                return;
            }
        }
        Debug.LogWarning("⚠️ 未找到TerrainManager，请手动设置");
    }
    
    /// <summary>
    /// 设置Toggle监听
    /// </summary>
    private void SetupToggleListener()
    {
        if (showEcoZonesToggle != null)
        {
            showEcoZonesToggle.onValueChanged.AddListener(OnEcoZonesToggleChanged);
            Debug.Log("🔗 已设置生态区Toggle监听");
        }
        else
        {
            Debug.LogWarning("⚠️ 未找到生态区Toggle，请手动设置");
        }
    }
    
    /// <summary>
    /// Toggle状态改变时的回调
    /// </summary>
    private void OnEcoZonesToggleChanged(bool isOn)
    {
        if (isOn)
        {
            ShowEcoZones();
        }
        else
        {
            HideEcoZones();
        }
    }
    
    /// <summary>
    /// 初始化生态区数据
    /// </summary>
    private void InitializeEcoZones()
    {
        // 如果已经初始化过，直接返回
        if (isDataInitialized)
        {
            Debug.Log("🔸 生态区数据已初始化，跳过重复初始化");
            return;
        }
        
        Debug.Log("🔸 初始化生态区数据...");
        
        // 获取Cells
        GetCells();
        
        if (cells.Count == 0)
        {
            Debug.LogError("❌ 没有找到Cell，无法初始化生态区");
            return;
        }
        
        // 确保所有Cell都有颜色分配
        EnsureCellColors();
        
        // 验证地形数据
        if (!ValidateTerrainData())
        {
            Debug.LogError("❌ 地形数据无效，请先生成地形");
            return;
        }
        
        // 分配Tiles给Cells
        AssignTilesToCells();
        
        // 如果启用簇模式，加载簇数据
        if (enableClusterMode)
        {
            LoadClusterData();
        }
        
        isDataInitialized = true;
        Debug.Log("✅ 生态区数据初始化完成");
        
        // 如果Toggle是开启状态，立即显示生态区
        if (showEcoZonesToggle != null && showEcoZonesToggle.isOn)
        {
            Debug.Log("🌍 Toggle已开启，立即显示生态区...");
            ShowEcoZones();
        }
    }
    
    /// <summary>
    /// 展示生态区
    /// </summary>
    [ContextMenu("展示生态区")]
    public void ShowEcoZones()
    {
        Debug.Log("🌍 开始展示生态区...");
        
        // 如果还没有初始化，先初始化
        if (!isDataInitialized)
        {
            InitializeEcoZones();
        }
        
        if (cells.Count == 0)
        {
            Debug.LogError("❌ 没有找到Cell，无法展示生态区");
            return;
        }
        
        // 确保所有Cell都有颜色分配
        EnsureCellColors();
        
        // 确保所有Cell都有tile分配
        if (cellTileAssignment.Count == 0)
        {
            AssignTilesToCells();
        }
        
        // 如果启用簇模式，重新加载簇数据
        if (enableClusterMode)
        {
            LoadClusterData();
        }
        
        // 高亮显示生态区
        HighlightCellTiles();
        
        isHighlightVisible = true;
        Debug.Log("✅ 生态区展示完成");
    }
    
    /// <summary>
    /// 隐藏生态区
    /// </summary>
    [ContextMenu("隐藏生态区")]
    public void HideEcoZones()
    {
        // 如果已经隐藏，直接返回
        if (!isHighlightVisible)
        {
            Debug.Log("🌍 生态区已经隐藏，跳过重复隐藏");
            return;
        }
        
        Debug.Log("🌍 隐藏生态区...");
        ClearTest();
        isHighlightVisible = false;
        Debug.Log("✅ 生态区已隐藏");
    }
    
    /// <summary>
    /// 开始测试（保留原有方法）
    /// </summary>
    [ContextMenu("开始Cell-Tile分配测试")]
    public void StartTest()
    {
        ShowEcoZones(); // 直接调用展示生态区
    }
    
    /// <summary>
    /// 获取Cells
    /// </summary>
    private void GetCells()
    {
        cells.Clear();
        cellColors.Clear(); // 清空颜色字典
        
        if (useGameManagerCells && gameManager != null)
        {
            // 从GameManager获取Cells
            try
            {
                var cellsField = gameManager.GetType().GetField("_cells", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (cellsField != null)
                {
                    var gameCells = cellsField.GetValue(gameManager) as System.Collections.IList;
                    if (gameCells != null && gameCells.Count > 0)
                    {
                        foreach (var cellObj in gameCells)
                        {
                            Cell cell = cellObj as Cell;
                            if (cell != null && IsValidGameCell(cell))
                            {
                                cells.Add(cell);
                            }
                        }
                        Debug.Log($"📊 从GameManager获取到{cells.Count}个Cell");
                        
                        // 分配颜色
                        for (int i = 0; i < cells.Count; i++)
                        {
                            cellColors[cells[i]] = predefinedColors[i % predefinedColors.Length];
                        }
                        return;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"⚠️ 从GameManager获取Cell时出错: {ex.Message}");
            }
        }
        
        Debug.LogWarning("⚠️ 无法从GameManager获取Cell");
    }
    
    /// <summary>
    /// 检查是否是有效的游戏Cell（过滤掉WeightPrefab实例）
    /// </summary>
    private bool IsValidGameCell(Cell cell)
    {
        if (cell == null) return false;
        
        // 检查Cell编号是否合理
        if (cell.Number <= 0) return false;
        
        // 检查名称，过滤掉WeightPrefab
        string cellName = cell.gameObject.name.ToLower();
        if (cellName.Contains("weight") || cellName.Contains("prefab")) return false;
        
        // 检查父对象，WeightPrefab通常作为边的子对象
        Transform parent = cell.transform.parent;
        if (parent != null)
        {
            string parentName = parent.name.ToLower();
            if (parentName.Contains("line") || parentName.Contains("edge") || parentName.Contains("weight"))
            {
                return false;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// 验证地形数据
    /// </summary>
    private bool ValidateTerrainData()
    {
        if (terrainManager == null)
        {
            Debug.LogError("❌ TerrainManager未设置");
            return false;
        }
        
        // 检查Tilemap
        var tilemapProperty = terrainManager.GetType().GetProperty("tilemap");
        if (tilemapProperty != null)
        {
            Tilemap tilemap = tilemapProperty.GetValue(terrainManager) as Tilemap;
            if (tilemap == null)
            {
                Debug.LogError("❌ Tilemap未设置");
                return false;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// 分配Tiles给Cells（使用最近邻算法）
    /// </summary>
    private void AssignTilesToCells()
    {
        Debug.Log("🔸 开始分配Tiles给Cells...");
        
        cellTileAssignment.Clear();
        
        // 初始化每个Cell的Tile列表
        foreach (Cell cell in cells)
        {
            cellTileAssignment[cell] = new List<Vector3Int>();
        }
        
        // 获取陆地瓦片
        List<Vector3Int> landTiles = GetLandTiles();
        Debug.Log($"📊 找到{landTiles.Count}个陆地瓦片");
        
        // 获取Tilemap用于坐标转换
        var tilemapProperty = terrainManager.GetType().GetProperty("tilemap");
        Tilemap tilemap = tilemapProperty.GetValue(terrainManager) as Tilemap;
        
        // 使用最近邻算法分配瓦片
        foreach (Vector3Int tilePos in landTiles)
        {
            Vector3 tileWorldPos = tilemap.CellToWorld(tilePos);
            Cell nearestCell = FindNearestCell(tileWorldPos);
            if (nearestCell != null)
            {
                cellTileAssignment[nearestCell].Add(tilePos);
            }
        }
        
        // 显示分配结果
        foreach (Cell cell in cells)
        {
            int tileCount = cellTileAssignment[cell].Count;
            Debug.Log($"🎯 Cell {cell.Number} 分配到{tileCount}个瓦片");
        }
    }
    
    /// <summary>
    /// 获取陆地瓦片（排除深水和浅水）
    /// </summary>
    private List<Vector3Int> GetLandTiles()
    {
        List<Vector3Int> landTiles = new List<Vector3Int>();
        
        // 获取Tilemap
        var tilemapProperty = terrainManager.GetType().GetProperty("tilemap");
        Tilemap tilemap = tilemapProperty.GetValue(terrainManager) as Tilemap;
        
        if (tilemap == null) return landTiles;
        
        // 遍历所有瓦片
        BoundsInt bounds = tilemap.cellBounds;
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int tilePos = new Vector3Int(x, y, 0);
                
                if (tilemap.HasTile(tilePos))
                {
                    int biome = GetBiomeAtTile(tilePos);
                    // 排除深水(0)和浅水(1)
                    if (biome > 1)
                    {
                        landTiles.Add(tilePos);
                    }
                }
            }
        }
        
        return landTiles;
    }
    
    /// <summary>
    /// 获取指定位置的生物群系
    /// </summary>
    private int GetBiomeAtTile(Vector3Int tilePos)
    {
        try
        {
            var getBiomeMethod = terrainManager.GetType().GetMethod("GetBiomeAtTile");
            if (getBiomeMethod != null)
            {
                var result = getBiomeMethod.Invoke(terrainManager, new object[] { tilePos });
                if (result != null)
                {
                    return (int)result;
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"获取生物群系时出错: {ex.Message}");
        }
        
        return -1;
    }
    
    /// <summary>
    /// 找到离指定位置最近的Cell
    /// </summary>
    private Cell FindNearestCell(Vector3 position)
    {
        Cell nearestCell = null;
        float minDistance = float.MaxValue;
        
        foreach (Cell cell in cells)
        {
            float distance = Vector3.Distance(position, cell.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestCell = cell;
            }
        }
        
        return nearestCell;
    }
    
    /// <summary>
    /// 确保所有Cell都有颜色分配
    /// </summary>
    private void EnsureCellColors()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (!cellColors.ContainsKey(cells[i]))
            {
                cellColors[cells[i]] = predefinedColors[i % predefinedColors.Length];
                Debug.Log($"🎨 为Cell {cells[i].Number} 分配颜色");
            }
        }
    }
    
    /// <summary>
    /// 高亮显示生态区
    /// </summary>
    private void HighlightCellTiles()
    {
        Debug.Log("🔸 开始高亮生态区...");
        
        // 清除之前的高亮
        foreach (GameObject obj in highlightObjects)
        {
            if (obj != null)
                DestroyImmediate(obj);
        }
        highlightObjects.Clear();
        
        // 获取Tilemap
        var tilemapProperty = terrainManager.GetType().GetProperty("tilemap");
        Tilemap tilemap = tilemapProperty.GetValue(terrainManager) as Tilemap;
        
        // 创建生态区高亮层
        GameObject highlightLayer = new GameObject("EcoZoneHighlights");
        highlightLayer.transform.parent = transform;
        highlightObjects.Add(highlightLayer);
        
        if (useClusterMode && cellClusterAssignment.Count > 0)
        {
            // 簇模式：按簇分组显示
            HighlightByClusters(tilemap, highlightLayer);
        }
        else
        {
            // 普通模式：按Cell显示
            HighlightByCells(tilemap, highlightLayer);
        }
        
        Debug.Log("✅ 生态区高亮完成");
    }
    
    /// <summary>
    /// 按Cell高亮显示
    /// </summary>
    private void HighlightByCells(Tilemap tilemap, GameObject highlightLayer)
    {
        Debug.Log("🔸 使用Cell模式高亮...");
        
        foreach (Cell cell in cells)
        {
            // 确保Cell有颜色分配
            if (!cellColors.ContainsKey(cell))
            {
                Debug.LogWarning($"⚠️ Cell {cell.Number} 没有颜色分配，跳过");
                continue;
            }
            
            // 确保Cell有tile分配
            if (!cellTileAssignment.ContainsKey(cell))
            {
                Debug.LogWarning($"⚠️ Cell {cell.Number} 没有tile分配，跳过");
                continue;
            }
            
            Color cellColor = cellColors[cell];
            List<Vector3Int> tiles = cellTileAssignment[cell];
            
            if (tiles.Count > 0)
            {
                // 为每个生态区创建tile高亮组
                GameObject ecoZoneGroup = new GameObject($"EcoZone{cell.Number}_Highlights");
                ecoZoneGroup.transform.parent = highlightLayer.transform;
                
                // 高亮该生态区的所有tiles
                foreach (Vector3Int tilePos in tiles)
                {
                    CreateTileHighlight(tilePos, cellColor, tilemap, ecoZoneGroup);
                }
                
                Debug.Log($"🌍 生态区 {cell.Number} 高亮了{tiles.Count}个瓦片");
            }
        }
    }
    
    /// <summary>
    /// 按簇高亮显示
    /// </summary>
    private void HighlightByClusters(Tilemap tilemap, GameObject highlightLayer)
    {
        Debug.Log("🔸 使用簇模式高亮...");
        
        // 按簇分组tiles
        Dictionary<int, List<Vector3Int>> clusterTiles = new Dictionary<int, List<Vector3Int>>();
        
        foreach (Cell cell in cells)
        {
            if (!cellClusterAssignment.ContainsKey(cell) || !cellTileAssignment.ContainsKey(cell))
            {
                continue;
            }
            
            int clusterId = cellClusterAssignment[cell];
            List<Vector3Int> cellTiles = cellTileAssignment[cell];
            
            if (!clusterTiles.ContainsKey(clusterId))
            {
                clusterTiles[clusterId] = new List<Vector3Int>();
            }
            
            clusterTiles[clusterId].AddRange(cellTiles);
        }
        
        // 为每个簇创建高亮
        foreach (var clusterEntry in clusterTiles)
        {
            int clusterId = clusterEntry.Key;
            List<Vector3Int> tiles = clusterEntry.Value;
            
            if (!clusterColors.ContainsKey(clusterId))
            {
                Debug.LogWarning($"⚠️ 簇 {clusterId} 没有颜色分配，跳过");
                continue;
            }
            
            Color clusterColor = clusterColors[clusterId];
            
            if (tiles.Count > 0)
            {
                // 为每个簇创建tile高亮组
                GameObject clusterGroup = new GameObject($"Cluster{clusterId}_Highlights");
                clusterGroup.transform.parent = highlightLayer.transform;
                
                // 高亮该簇的所有tiles
                foreach (Vector3Int tilePos in tiles)
                {
                    CreateTileHighlight(tilePos, clusterColor, tilemap, clusterGroup);
                }
                
                Debug.Log($"🌍 簇 {clusterId} 高亮了{tiles.Count}个瓦片");
            }
        }
    }
    
    /// <summary>
    /// 创建单个tile的高亮
    /// </summary>
    private void CreateTileHighlight(Vector3Int tilePos, Color color, Tilemap tilemap, GameObject parent)
    {
        Vector3 worldPos = tilemap.CellToWorld(tilePos);
        worldPos.z = zOffset;
        
        // 创建高亮对象
        GameObject highlight = new GameObject($"TileHighlight_{tilePos.x}_{tilePos.y}");
        highlight.transform.parent = parent.transform;
        highlight.transform.position = worldPos;
        
        // 添加SpriteRenderer
        SpriteRenderer renderer = highlight.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateHexagonSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        
        // 设置大小 - 和tile一样大
        Vector3 tileSize = tilemap.cellSize;
        // 调整缩放以匹配实际的tile大小
        highlight.transform.localScale = tileSize /2;
    }
    
    /// <summary>
    /// 创建六边形Sprite
    /// </summary>
    private Sprite CreateHexagonSprite()
    {
        // 创建六边形纹理
        int size = 64;
        Texture2D texture = new Texture2D(size, size);
        
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 2f;
        
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2 point = new Vector2(x, y);
                float distance = Vector2.Distance(point, center);
                
                // 检查是否在六边形内
                bool isInside = IsPointInHexagon(point, center, radius);
                
                if (isInside)
                {
                    texture.SetPixel(x, y, Color.white);
                }
                else
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }
        
        texture.Apply();
        
        // 创建Sprite
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return sprite;
    }
    
    /// <summary>
    /// 检查点是否在六边形内
    /// </summary>
    private bool IsPointInHexagon(Vector2 point, Vector2 center, float radius)
    {
        Vector2 relativePoint = point - center;
        
        // 六边形的6个顶点
        Vector2[] vertices = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            float angle = i * 60f * Mathf.Deg2Rad;
            vertices[i] = new Vector2(
                radius * Mathf.Cos(angle),
                radius * Mathf.Sin(angle)
            );
        }
        
        // 检查点是否在多边形内
        return IsPointInPolygon(relativePoint, vertices);
    }
    
    /// <summary>
    /// 检查点是否在多边形内（射线法）
    /// </summary>
    private bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        int j = polygon.Length - 1;
        
        for (int i = 0; i < polygon.Length; i++)
        {
            if (((polygon[i].y > point.y) != (polygon[j].y > point.y)) &&
                (point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x))
            {
                inside = !inside;
            }
            j = i;
        }
        
        return inside;
    }
    

    

    
    /// <summary>
    /// 获取单个瓦片的外边缘（只有不与同类瓦片相邻的边）
    /// </summary>
    private List<(Vector3, Vector3)> GetTileOuterEdges(Vector3Int tilePos, HashSet<Vector3Int> allTiles, Tilemap tilemap)
    {
        List<(Vector3, Vector3)> outerEdges = new List<(Vector3, Vector3)>();
        
        Vector3 worldPos = tilemap.CellToWorld(tilePos);
        worldPos.z = zOffset;
        
        // 六边形的6个邻居方向
        Vector3Int[] neighbors = GetHexNeighbors(tilePos);
        
        // 六边形顶点位置（相对于中心）
        Vector3[] hexVertices = new Vector3[6];
        float radius = 0.577f; // 六边形半径
        
        for (int i = 0; i < 6; i++)
        {
            float angle = i * 60f * Mathf.Deg2Rad;
            hexVertices[i] = new Vector3(
                radius * Mathf.Cos(angle),
                radius * Mathf.Sin(angle),
                0
            );
        }
        
        // 检查每条边，只有外边界的边才加入
        for (int i = 0; i < 6; i++)
        {
            Vector3Int neighbor = tilePos + neighbors[i];
            if (!allTiles.Contains(neighbor))
            {
                // 这是外边界，添加这条边
                Vector3 start = worldPos + hexVertices[i];
                Vector3 end = worldPos + hexVertices[(i + 1) % 6];
                outerEdges.Add((start, end));
            }
        }
        
        return outerEdges;
    }
    
    /// <summary>
    /// 连接边缘形成连续轮廓
    /// </summary>
    private List<Vector3> ConnectEdgesToBoundary(List<(Vector3, Vector3)> edges)
    {
        if (edges.Count == 0) return new List<Vector3>();
        
        List<Vector3> boundary = new List<Vector3>();
        List<(Vector3, Vector3)> remainingEdges = new List<(Vector3, Vector3)>(edges);
        
        // 从第一条边开始
        var currentEdge = remainingEdges[0];
        remainingEdges.RemoveAt(0);
        
        boundary.Add(currentEdge.Item1);
        boundary.Add(currentEdge.Item2);
        
        Vector3 lastPoint = currentEdge.Item2;
        float tolerance = 0.1f; // 点连接的容差
        
        // 连接相邻的边
        while (remainingEdges.Count > 0)
        {
            bool foundConnection = false;
            
            for (int i = 0; i < remainingEdges.Count; i++)
            {
                var edge = remainingEdges[i];
                
                // 检查这条边是否与当前终点连接
                if (Vector3.Distance(lastPoint, edge.Item1) < tolerance)
                {
                    boundary.Add(edge.Item2);
                    lastPoint = edge.Item2;
                    remainingEdges.RemoveAt(i);
                    foundConnection = true;
                    break;
                }
                else if (Vector3.Distance(lastPoint, edge.Item2) < tolerance)
                {
                    boundary.Add(edge.Item1);
                    lastPoint = edge.Item1;
                    remainingEdges.RemoveAt(i);
                    foundConnection = true;
                    break;
                }
            }
            
            // 如果找不到连接，可能是不连续的边界，跳出
            if (!foundConnection)
            {
                break;
            }
        }
        
        return boundary;
    }
    
    /// <summary>
    /// 获取六边形邻居（考虑奇偶行）
    /// </summary>
    private Vector3Int[] GetHexNeighbors(Vector3Int tilePos)
    {
        if (tilePos.y % 2 == 0) // 偶数行
        {
            return new Vector3Int[] {
                new Vector3Int(-1, 0, 0),   // 左
                new Vector3Int(1, 0, 0),    // 右
                new Vector3Int(-1, 1, 0),   // 左上
                new Vector3Int(0, 1, 0),    // 右上
                new Vector3Int(-1, -1, 0),  // 左下
                new Vector3Int(0, -1, 0)    // 右下
            };
        }
        else // 奇数行
        {
            return new Vector3Int[] {
                new Vector3Int(-1, 0, 0),   // 左
                new Vector3Int(1, 0, 0),    // 右
                new Vector3Int(0, 1, 0),    // 左上
                new Vector3Int(1, 1, 0),    // 右上
                new Vector3Int(0, -1, 0),   // 左下
                new Vector3Int(1, -1, 0)    // 右下
            };
        }
    }
    
    /// <summary>
    /// 创建实线边界线
    /// </summary>
    private void CreateSingleEdgeLine(Vector3 start, Vector3 end, Color color, GameObject parent)
    {
        GameObject lineObj = new GameObject("SolidEdgeLine");
        lineObj.transform.parent = parent.transform;
        
        LineRenderer line = lineObj.AddComponent<LineRenderer>();
        
        // 创建实线材质
        Material solidMaterial = new Material(Shader.Find("Sprites/Default"));
        solidMaterial.color = color;
        line.material = solidMaterial;
        
        // 设置实线效果
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.sortingLayerName = "Default";
        line.sortingOrder = sortingOrder;
        line.useWorldSpace = true;
        line.positionCount = 2;
        
        // 设置起点和终点
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }
    

    
    /// <summary>
    /// 创建简单的边界线
    /// </summary>
    private void CreateSimpleBoundaryLine(Vector3[] points, Color color, GameObject parent)
    {
        GameObject lineObj = new GameObject("CellBoundaryLine");
        lineObj.transform.parent = parent.transform;
        
        LineRenderer line = lineObj.AddComponent<LineRenderer>();
        
        // 创建并设置材质颜色
        Material lineMaterial = new Material(Shader.Find("Sprites/Default"));
        lineMaterial.color = color;
        line.material = lineMaterial;
        
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.sortingLayerName = "Default";
        line.sortingOrder = sortingOrder;
        line.useWorldSpace = true;
        line.loop = true; // 闭合轮廓
        line.positionCount = points.Length;
        
        // 设置所有点
        for (int i = 0; i < points.Length; i++)
        {
            line.SetPosition(i, points[i]);
        }
    }
    

    

    
    /// <summary>
    /// 清理生态区高亮
    /// </summary>
    [ContextMenu("清理生态区高亮")]
    public void ClearTest()
    {
        Debug.Log("🧹 清理生态区高亮...");
        
        foreach (GameObject obj in highlightObjects)
        {
            if (obj != null)
                DestroyImmediate(obj);
        }
        highlightObjects.Clear();
        
        // 不清空cellTileAssignment，保持tile分配
        // 不清空cellColors，保持颜色分配
        // 不清空cells，保持Cell列表
        
        Debug.Log("✅ 生态区高亮清理完成");
    }
    
    /// <summary>
    /// 重置缓存状态（用于强制重新计算）
    /// </summary>
    [ContextMenu("重置缓存状态")]
    public void ResetCache()
    {
        Debug.Log("🔄 重置缓存状态...");
        isDataInitialized = false;
        isHighlightVisible = false;
        Debug.Log("✅ 缓存状态已重置");
    }
    
    /// <summary>
    /// 加载簇数据
    /// </summary>
    private void LoadClusterData()
    {
        Debug.Log("🔸 加载簇数据...");
        
        try
        {
            // 读取JSON文件
            string jsonPath = System.IO.Path.Combine(Application.dataPath, "..", clusterDataPath);
            if (!System.IO.File.Exists(jsonPath))
            {
                Debug.LogWarning($"⚠️ 簇数据文件不存在: {jsonPath}");
                return;
            }
            
            string jsonContent = System.IO.File.ReadAllText(jsonPath);
            Debug.Log($"📄 读取到JSON内容: {jsonContent.Substring(0, Mathf.Min(200, jsonContent.Length))}...");
            
            // 尝试解析为切割边数据格式
            var cutEdgeData = JsonUtility.FromJson<CutEdgeData>(jsonContent);
            
            if (cutEdgeData != null && cutEdgeData.cut_edges != null)
            {
                Debug.Log($"📊 解析到切割边数据，包含{cutEdgeData.cut_edges.Length}条边，cost={cutEdgeData.cost}");
                
                // 更新lastKnownCost
                lastKnownCost = cutEdgeData.cost;
                
                // 从切割边数据推断簇
                var clusters = InferClustersFromCutEdges(cutEdgeData.cut_edges);
                
                // 清空之前的簇分配
                cellClusterAssignment.Clear();
                clusterColors.Clear();
                
                // 解析簇数据
                for (int i = 0; i < clusters.Count; i++)
                {
                    var cluster = clusters[i];
                    int clusterId = i;
                    
                    Debug.Log($"🔸 处理簇 {clusterId}，包含 {cluster.Count} 个Cell: [{string.Join(", ", cluster)}]");
                    
                    // 为簇分配颜色
                    clusterColors[clusterId] = predefinedColors[i % predefinedColors.Length];
                    
                    // 将Cell分配到簇
                    foreach (int cellNumber in cluster)
                    {
                        Cell cell = FindCellByNumber(cellNumber);
                        if (cell != null)
                        {
                            cellClusterAssignment[cell] = clusterId;
                            Debug.Log($"🎯 Cell {cellNumber} 分配到簇 {clusterId}");
                        }
                        else
                        {
                            Debug.LogWarning($"⚠️ 未找到Cell {cellNumber}");
                        }
                    }
                }
                
                useClusterMode = true;
                Debug.Log($"✅ 成功从切割边数据推断出{clusters.Count}个簇，共分配{cellClusterAssignment.Count}个Cell");
            }
            else
            {
                // 尝试解析为原始簇数据格式
                var clusterData = JsonUtility.FromJson<ClusterData>(jsonContent);
                
                if (clusterData == null || clusterData.clusters == null)
                {
                    Debug.LogWarning("⚠️ 簇数据格式无效");
                    return;
                }
                
                Debug.Log($"📊 解析到{clusterData.clusters.Length}个簇");
                
                // 清空之前的簇分配
                cellClusterAssignment.Clear();
                clusterColors.Clear();
                
                // 解析簇数据
                for (int i = 0; i < clusterData.clusters.Length; i++)
                {
                    var cluster = clusterData.clusters[i];
                    int clusterId = i;
                    
                    Debug.Log($"🔸 处理簇 {clusterId}，包含 {cluster.cells.Length} 个Cell");
                    
                    // 为簇分配颜色
                    clusterColors[clusterId] = predefinedColors[i % predefinedColors.Length];
                    
                    // 将Cell分配到簇
                    foreach (int cellNumber in cluster.cells)
                    {
                        Cell cell = FindCellByNumber(cellNumber);
                        if (cell != null)
                        {
                            cellClusterAssignment[cell] = clusterId;
                            Debug.Log($"🎯 Cell {cellNumber} 分配到簇 {clusterId}");
                        }
                        else
                        {
                            Debug.LogWarning($"⚠️ 未找到Cell {cellNumber}");
                        }
                    }
                }
                
                useClusterMode = true;
                Debug.Log($"✅ 成功加载{clusterData.clusters.Length}个簇的数据，共分配{cellClusterAssignment.Count}个Cell");
            }
            
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ 加载簇数据时出错: {ex.Message}");
            Debug.LogError($"❌ 错误详情: {ex.StackTrace}");
            useClusterMode = false;
        }
    }
    
    /// <summary>
    /// 根据编号查找Cell
    /// </summary>
    private Cell FindCellByNumber(int cellNumber)
    {
        foreach (Cell cell in cells)
        {
            if (cell.Number == cellNumber)
            {
                return cell;
            }
        }
        return null;
    }
    
    /// <summary>
    /// 切换簇模式
    /// </summary>
    [ContextMenu("切换簇模式")]
    public void ToggleClusterMode()
    {
        useClusterMode = !useClusterMode;
        Debug.Log($"🔄 簇模式已切换为: {(useClusterMode ? "开启" : "关闭")}");
        
        // 如果当前正在显示，重新显示以应用新模式
        if (isHighlightVisible)
        {
            ShowEcoZones();
        }
    }
    
    /// <summary>
    /// 强制重新加载簇数据
    /// </summary>
    [ContextMenu("重新加载簇数据")]
    public void ReloadClusterData()
    {
        Debug.Log("🔄 重新加载簇数据...");
        cellClusterAssignment.Clear();
        clusterColors.Clear();
        useClusterMode = false;
        
        if (enableClusterMode)
        {
            LoadClusterData();
        }
        
        // 如果当前正在显示，重新显示
        if (isHighlightVisible)
        {
            ShowEcoZones();
        }
    }
    
    /// <summary>
    /// 检查簇数据文件是否存在
    /// </summary>
    [ContextMenu("检查簇数据文件")]
    public void CheckClusterDataFile()
    {
        string jsonPath = System.IO.Path.Combine(Application.dataPath, "..", clusterDataPath);
        bool exists = System.IO.File.Exists(jsonPath);
        
        if (exists)
        {
            Debug.Log($"✅ 簇数据文件存在: {jsonPath}");
            try
            {
                string jsonContent = System.IO.File.ReadAllText(jsonPath);
                Debug.Log($"📄 文件内容长度: {jsonContent.Length} 字符");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ 读取文件时出错: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ 簇数据文件不存在: {jsonPath}");
        }
    }
    
    /// <summary>
    /// 强制更新簇显示（当cost达到最优时调用）
    /// </summary>
    [ContextMenu("强制更新簇显示")]
    public void ForceUpdateClusterDisplay()
    {
        Debug.Log("🔄 强制更新簇显示...");
        
        // 清除当前显示
        ClearTest();
        isHighlightVisible = false;
        
        // 重新加载簇数据
        if (enableClusterMode)
        {
            LoadClusterData();
        }
        
        // 重新显示
        ShowEcoZones();
        
        Debug.Log("✅ 簇显示已强制更新");
    }
    
    /// <summary>
    /// 自动检测并更新簇数据
    /// </summary>
    [ContextMenu("自动检测簇数据")]
    public void AutoDetectClusterData()
    {
        Debug.Log("🔍 自动检测簇数据...");
        
        string jsonPath = System.IO.Path.Combine(Application.dataPath, "..", clusterDataPath);
        if (System.IO.File.Exists(jsonPath))
        {
            Debug.Log("✅ 检测到簇数据文件，启用簇模式");
            enableClusterMode = true;
            ForceUpdateClusterDisplay();
        }
        else
        {
            Debug.LogWarning("⚠️ 未检测到簇数据文件，使用普通模式");
            enableClusterMode = false;
            useClusterMode = false;
            if (isHighlightVisible)
            {
                ShowEcoZones();
            }
        }
    }
    
    /// <summary>
    /// 强制刷新簇显示（确保使用簇颜色）
    /// </summary>
    [ContextMenu("强制刷新簇显示")]
    public void ForceRefreshClusterDisplay()
    {
        Debug.Log("🔄 强制刷新簇显示...");
        
        // 确保启用簇模式
        enableClusterMode = true;
        useClusterMode = true;
        
        // 重新加载簇数据
        LoadClusterData();
        
        // 清除当前显示
        ClearTest();
        isHighlightVisible = false;
        
        // 重新显示
        ShowEcoZones();
        
        Debug.Log("✅ 簇显示已强制刷新");
    }
    
    /// <summary>
    /// 检查cost变化
    /// </summary>
    private void CheckCostChange()
    {
        // 按间隔检查，避免过于频繁
        if (Time.time - lastCostCheckTime < costCheckInterval)
        {
            return;
        }
        
        lastCostCheckTime = Time.time;
        
        try
        {
            // 读取JSON文件获取当前cost
            string jsonPath = System.IO.Path.Combine(Application.dataPath, "..", clusterDataPath);
            if (!System.IO.File.Exists(jsonPath))
            {
                return;
            }
            
            string jsonContent = System.IO.File.ReadAllText(jsonPath);
            var cutEdgeData = JsonUtility.FromJson<CutEdgeData>(jsonContent);
            
            if (cutEdgeData != null)
            {
                int currentCost = cutEdgeData.cost;
                
                // 如果cost发生了变化
                if (currentCost != lastKnownCost)
                {
                    Debug.Log($"🔄 检测到cost变化: {lastKnownCost} -> {currentCost}");
                    lastKnownCost = currentCost;
                    
                    // 自动刷新簇显示
                    ForceRefreshClusterDisplay();
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"⚠️ 检查cost变化时出错: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 从切割边数据推断簇
    /// </summary>
    private List<List<int>> InferClustersFromCutEdges(CutEdge[] cutEdges)
    {
        Debug.Log("🔸 从切割边数据推断簇...");
        
        // 获取所有Cell编号
        HashSet<int> allCells = new HashSet<int>();
        foreach (Cell cell in cells)
        {
            allCells.Add(cell.Number);
        }
        
        Debug.Log($"🔸 所有Cell: [{string.Join(", ", allCells)}]");
        
        // 构建邻接表 - 只包含实际存在的连接
        Dictionary<int, List<int>> adjacencyList = new Dictionary<int, List<int>>();
        foreach (int cellNumber in allCells)
        {
            adjacencyList[cellNumber] = new List<int>();
        }
        
        // 从GameManager获取实际的边连接
        if (gameManager != null)
        {
            try
            {
                var edgesField = gameManager.GetType().GetField("_edges", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (edgesField != null)
                {
                    var edges = edgesField.GetValue(gameManager) as System.Collections.IList;
                    if (edges != null)
                    {
                        foreach (var edgeObj in edges)
                        {
                            // 通过反射获取边的两个端点
                            var edgeType = edgeObj.GetType();
                            var node1Field = edgeType.GetField("node1", 
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            var node2Field = edgeType.GetField("node2", 
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            
                            if (node1Field != null && node2Field != null)
                            {
                                var node1 = node1Field.GetValue(edgeObj);
                                var node2 = node2Field.GetValue(edgeObj);
                                
                                // 获取Cell编号
                                var cell1NumberField = node1.GetType().GetField("Number", 
                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                var cell2NumberField = node2.GetType().GetField("Number", 
                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                
                                if (cell1NumberField != null && cell2NumberField != null)
                                {
                                    int cell1Number = (int)cell1NumberField.GetValue(node1);
                                    int cell2Number = (int)cell2NumberField.GetValue(node2);
                                    
                                    if (allCells.Contains(cell1Number) && allCells.Contains(cell2Number))
                                    {
                                        adjacencyList[cell1Number].Add(cell2Number);
                                        adjacencyList[cell2Number].Add(cell1Number);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"⚠️ 获取边连接时出错: {ex.Message}");
            }
        }
        
        // 显示原始连接
        Debug.Log("🔸 原始连接:");
        foreach (var kvp in adjacencyList)
        {
            Debug.Log($"🔸 Cell {kvp.Key} -> [{string.Join(", ", kvp.Value)}]");
        }
        
        // 移除被切割的边
        foreach (var cutEdge in cutEdges)
        {
            int u = cutEdge.u;
            int v = cutEdge.v;
            
            if (adjacencyList.ContainsKey(u) && adjacencyList[u].Contains(v))
            {
                adjacencyList[u].Remove(v);
                Debug.Log($"🔸 移除边: {u} - {v}");
            }
            if (adjacencyList.ContainsKey(v) && adjacencyList[v].Contains(u))
            {
                adjacencyList[v].Remove(u);
            }
        }
        
        // 显示切割后的连接
        Debug.Log("🔸 切割后的连接:");
        foreach (var kvp in adjacencyList)
        {
            Debug.Log($"🔸 Cell {kvp.Key} -> [{string.Join(", ", kvp.Value)}]");
        }
        
        // 使用DFS找到连通分量
        HashSet<int> visited = new HashSet<int>();
        List<List<int>> clusters = new List<List<int>>();
        
        foreach (int cellNumber in allCells)
        {
            if (!visited.Contains(cellNumber))
            {
                List<int> cluster = new List<int>();
                DFS(cellNumber, adjacencyList, visited, cluster);
                if (cluster.Count > 0)
                {
                    clusters.Add(cluster);
                }
            }
        }
        
        Debug.Log($"🔸 推断出{clusters.Count}个簇");
        for (int i = 0; i < clusters.Count; i++)
        {
            Debug.Log($"🔸 簇 {i}: [{string.Join(", ", clusters[i])}]");
        }
        
        return clusters;
    }
    
    /// <summary>
    /// 深度优先搜索找连通分量
    /// </summary>
    private void DFS(int cellNumber, Dictionary<int, List<int>> adjacencyList, HashSet<int> visited, List<int> cluster)
    {
        visited.Add(cellNumber);
        cluster.Add(cellNumber);
        
        if (adjacencyList.ContainsKey(cellNumber))
        {
            foreach (int neighbor in adjacencyList[cellNumber])
            {
                if (!visited.Contains(neighbor))
                {
                    DFS(neighbor, adjacencyList, visited, cluster);
                }
            }
        }
    }
    
    /// <summary>
    /// 调试簇分配状态
    /// </summary>
    [ContextMenu("调试簇分配状态")]
    public void DebugClusterAssignment()
    {
        Debug.Log("🔍 调试簇分配状态...");
        
        Debug.Log($"📊 当前模式: {(useClusterMode ? "簇模式" : "普通模式")}");
        Debug.Log($"📊 启用簇模式: {enableClusterMode}");
        Debug.Log($"📊 簇分配数量: {cellClusterAssignment.Count}");
        Debug.Log($"📊 簇颜色数量: {clusterColors.Count}");
        
        // 显示每个Cell的簇分配
        foreach (var kvp in cellClusterAssignment)
        {
            Cell cell = kvp.Key;
            int clusterId = kvp.Value;
            Debug.Log($"🎯 Cell {cell.Number} -> 簇 {clusterId}");
        }
        
        // 显示每个簇的颜色
        foreach (var kvp in clusterColors)
        {
            int clusterId = kvp.Key;
            Color color = kvp.Value;
            Debug.Log($"🎨 簇 {clusterId} -> 颜色 {color}");
        }
        
        // 检查Cell 6, 8, 10的分配
        Cell cell6 = FindCellByNumber(6);
        Cell cell8 = FindCellByNumber(8);
        Cell cell10 = FindCellByNumber(10);
        
        if (cell6 != null && cellClusterAssignment.ContainsKey(cell6))
            Debug.Log($"🔍 Cell 6 -> 簇 {cellClusterAssignment[cell6]}");
        else
            Debug.LogWarning("⚠️ Cell 6 没有簇分配");
            
        if (cell8 != null && cellClusterAssignment.ContainsKey(cell8))
            Debug.Log($"🔍 Cell 8 -> 簇 {cellClusterAssignment[cell8]}");
        else
            Debug.LogWarning("⚠️ Cell 8 没有簇分配");
            
        if (cell10 != null && cellClusterAssignment.ContainsKey(cell10))
            Debug.Log($"🔍 Cell 10 -> 簇 {cellClusterAssignment[cell10]}");
        else
            Debug.LogWarning("⚠️ Cell 10 没有簇分配");
        
        // 检查是否正在使用簇模式显示
        Debug.Log($"🔍 当前显示模式: {(useClusterMode ? "簇模式" : "普通模式")}");
        Debug.Log($"🔍 是否正在显示: {isHighlightVisible}");
        
        // 检查所有Cell的当前颜色分配
        Debug.Log("🔍 当前Cell颜色分配:");
        foreach (Cell cell in cells)
        {
            if (cellColors.ContainsKey(cell))
            {
                Color cellColor = cellColors[cell];
                Debug.Log($"🔍 Cell {cell.Number} -> 颜色 {cellColor}");
            }
        }
    }
}