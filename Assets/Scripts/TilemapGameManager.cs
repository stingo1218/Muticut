using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Linq;
using TerrainSystem;
using TMPro;

/// <summary>
/// TilemapGameManager - 地形节点生成管理器
/// 
/// 渲染顺序设置：
/// - Edges (LineRenderer): sortingOrder = 5 (最底层)
/// - Cell Background (SpriteRenderer): sortingOrder = 15 (中间层)
/// - Cell Text (TMP_Text): sortingOrder = 35 (文本层)
/// - Weights (TextMesh/TextMeshPro): sortingOrder = 25 (权重层)
/// 
/// 确保cells和weights始终显示在edges之上，cell文本显示在cell背景之上。
/// </summary>
public class TilemapGameManager : MonoBehaviour
{
    [Header("组件引用")]
    public TerrainManager terrainManager;
    public GameManager gameManager;
    public GameObject cellPrefab;
    public GameObject weightLabelPrefab;
    public Material lineMaterial;

    [Header("节点生成设置")]
    public float nodeRadius = 1.0f;
    public int maxNodes = 50;
    public float samplingRadius = 2.0f;

    [Header("调试设置")]
    [SerializeField] private bool enableGlobalClickDetection = true; // 全局点击检测开关

    // 移除重复的TerrainWeights定义，使用GameManager的权重设置
    // [System.Serializable]
    // public class TerrainWeights
    // {
    //     public int grassWeight = 5;
    //     public int plainsWeight = 4;
    //     public int shallowWaterWeight = 3;
    //     public int forestWeight = -6;
    //     public int deepWaterWeight = -8;
    //     public int mountainWeight = -10;
    //     public int highMountainWeight = -15;
    //     public int volcanoWeight = -20;
    //     public int riverWeight = -12;
    //     public int defaultWeight = 0;

    //     public int GetWeightForBiome(HexCoordinateSystem.BiomeType biome)
    //     {
    //         switch (biome)
    //         {
    //             case HexCoordinateSystem.BiomeType.FlatGrass: return grassWeight;
    //             case HexCoordinateSystem.BiomeType.FlatDesert1: 
    //             case HexCoordinateSystem.BiomeType.FlatDesert2: return plainsWeight;
    //             case HexCoordinateSystem.BiomeType.ShallowWater: return shallowWaterWeight;
    //             case HexCoordinateSystem.BiomeType.FlatForest: 
    //             case HexCoordinateSystem.BiomeType.FlatForestSwampy: return forestWeight;
    //             case HexCoordinateSystem.BiomeType.DeepWater: return deepWaterWeight;
    //             case HexCoordinateSystem.BiomeType.MountainDesert:
    //             case HexCoordinateSystem.BiomeType.MountainShrubland1:
    //             case HexCoordinateSystem.BiomeType.MountainShrubland2:
    //             case HexCoordinateSystem.BiomeType.MountainAlpine1:
    //             case HexCoordinateSystem.BiomeType.MountainAlpine2:
    //             case HexCoordinateSystem.BiomeType.MountainImpassable1:
    //             case HexCoordinateSystem.BiomeType.MountainImpassable2: return mountainWeight;
    //             case HexCoordinateSystem.BiomeType.HillDesert:
    //             case HexCoordinateSystem.BiomeType.HillGrass:
    //             case HexCoordinateSystem.BiomeType.HillForest:
    //             case HexCoordinateSystem.BiomeType.HillForestNeedleleaf: return highMountainWeight;
    //             case HexCoordinateSystem.BiomeType.Volcano: return volcanoWeight;
    //             case HexCoordinateSystem.BiomeType.FlatSparseTrees1:
    //             case HexCoordinateSystem.BiomeType.FlatSparseTrees2: return riverWeight; // 临时用河流权重
    //             default: return defaultWeight;
    //         }
    //     }
    // }
    // public TerrainWeights terrainWeights = new TerrainWeights();

    [Header("可视化设置")]
    public bool showWeightLabels = true;
    public float lineWidthMultiplier = 0.02f; // 减小线条宽度倍数

    // 私有变量
    private List<Cell> generatedCells = new List<Cell>();
    private List<(Cell, Cell)> generatedEdges = new List<(Cell, Cell)>();
    private Dictionary<(Cell, Cell), LineRenderer> edgeLines = new Dictionary<(Cell, Cell), LineRenderer>();
    private GameObject linesRoot;
    private GameObject cellsRoot;
    
    // 权重缓存系统（类似GameManager.cs）
    private Dictionary<(Cell, Cell), int> _edgeWeightCache = new Dictionary<(Cell, Cell), int>();

    void Awake()
    {
        // 自动查找组件引用
        if (terrainManager == null)
            terrainManager = FindObjectsByType<TerrainManager>(FindObjectsSortMode.None).FirstOrDefault();
        
        if (gameManager == null)
            gameManager = FindObjectsByType<GameManager>(FindObjectsSortMode.None).FirstOrDefault();

        // 创建根对象
        linesRoot = new GameObject("TilemapLinesRoot");
        
        cellsRoot = new GameObject("TilemapCellsRoot");
        
        // 检查并提示地形状态
        if (terrainManager != null)
        {
            var hexTiles = terrainManager.GetHexTiles();
            if (hexTiles == null || hexTiles.Count == 0)
            {
                Debug.LogWarning("⚠️ TilemapGameManager 初始化完成，但地形尚未生成");
                Debug.Log("💡 提示：调用 GenerateNodesOnTerrain() 会自动尝试生成地形");
            }
            else
            {
                Debug.Log($"✅ TilemapGameManager 初始化完成，地形已就绪：{hexTiles.Count} 个六边形");
            }
        }
        else
        {
            Debug.LogWarning("⚠️ 未找到 TerrainManager，请在 Inspector 中手动设置");
        }
    }

    [ContextMenu("生成地形节点")]
    public void GenerateNodesOnTerrain()
    {
        Debug.Log("⚠️ TilemapGameManager 的cell生成功能已禁用，只使用 GameManager 生成的cells");
        Debug.Log("💡 请使用 GameManager 来生成和管理cells");
        return;
    }

    private void CreateCellAtPosition(Vector3 worldPosition)
    {
        Debug.Log("⚠️ CreateCellAtPosition 已禁用，只使用 GameManager 生成的cells");
        return;
    }

    private Bounds CalculateTerrainBounds(List<HexCoordinateSystem.HexTile> hexTiles)
    {
        if (hexTiles.Count == 0) return new Bounds();

        Vector3 minPos = Vector3.positiveInfinity;
        Vector3 maxPos = Vector3.negativeInfinity;

        foreach (var hex in hexTiles)
        {
            Vector3Int tilePosition = terrainManager.ConvertHexToTilePosition(hex);
            Vector3 worldPos = terrainManager.tilemap.CellToWorld(tilePosition);
            
            minPos = Vector3.Min(minPos, worldPos);
            maxPos = Vector3.Max(maxPos, worldPos);
        }

        // 添加调试信息
        Debug.Log($"地形边界: min={minPos}, max={maxPos}, size={maxPos - minPos}");
        
        Bounds bounds = new Bounds();
        bounds.SetMinMax(minPos, maxPos);
        
        // 扩大边界以确保覆盖整个地形
        bounds.Expand(1.0f);
        
        return bounds;
    }

    // 步骤2: 计算点集的边界矩形
    private Bounds CalculatePointBounds(List<Vector2> points)
    {
        if (points.Count == 0) return new Bounds();

        Vector2 minPos = Vector2.positiveInfinity;
        Vector2 maxPos = Vector2.negativeInfinity;

        foreach (var point in points)
        {
            minPos = Vector2.Min(minPos, point);
            maxPos = Vector2.Max(maxPos, point);
        }

        Bounds bounds = new Bounds();
        bounds.SetMinMax(new Vector3(minPos.x, minPos.y, 0), new Vector3(maxPos.x, maxPos.y, 0));
        
        return bounds;
    }

    // 步骤3: 计算目标边界（地形指定比例大小，居中）
    private Bounds CalculateTargetBounds(Bounds terrainBounds, float scale)
    {
        Vector3 center = terrainBounds.center;
        Vector3 size = terrainBounds.size * scale;
        
        Bounds targetBounds = new Bounds(center, size);
        
        Debug.Log($"目标边界计算: 中心={center}, 大小={size}, 比例={scale}");
        
        return targetBounds;
    }

    // 步骤4: 调整点位置（从原始边界拉伸到目标边界）
    private List<Vector2> AdjustPointPositions(List<Vector2> originalPoints, Bounds sourceBounds, Bounds targetBounds)
    {
        var adjustedPoints = new List<Vector2>();
        
        if (originalPoints.Count == 0) return adjustedPoints;

        // 计算缩放比例
        Vector3 scaleRatio = new Vector3(
            targetBounds.size.x / sourceBounds.size.x,
            targetBounds.size.y / sourceBounds.size.y,
            1.0f
        );
        
        Debug.Log($"缩放比例: X={scaleRatio.x:F3}, Y={scaleRatio.y:F3}");

        foreach (var point in originalPoints)
        {
            // 将点从原始坐标系转换到目标坐标系
            Vector3 normalizedPoint = new Vector3(
                (point.x - sourceBounds.min.x) / sourceBounds.size.x,
                (point.y - sourceBounds.min.y) / sourceBounds.size.y,
                0
            );
            
            // 应用缩放和偏移
            Vector3 adjustedPoint = new Vector3(
                normalizedPoint.x * targetBounds.size.x + targetBounds.min.x,
                normalizedPoint.y * targetBounds.size.y + targetBounds.min.y,
                0
            );
            
            adjustedPoints.Add(new Vector2(adjustedPoint.x, adjustedPoint.y));
        }
        
        Debug.Log($"调整了 {adjustedPoints.Count} 个点的位置");
        
        // 验证调整后的边界
        var adjustedBounds = CalculatePointBounds(adjustedPoints);
        Debug.Log($"调整后点集边界: {adjustedBounds.min} 到 {adjustedBounds.max}");
        
        return adjustedPoints;
    }

    private List<Vector2> PoissonDiskSampling(Bounds bounds, float radius, int maxPoints)
    {
        float cellSize = radius / Mathf.Sqrt(2);
        int gridWidth = Mathf.CeilToInt(bounds.size.x / cellSize);
        int gridHeight = Mathf.CeilToInt(bounds.size.y / cellSize);

        int?[,] grid = new int?[gridWidth, gridHeight];
        var points = new List<Vector2>();
        var activePoints = new List<Vector2>();

        // 添加策略性分布的初始点以确保覆盖整个区域
        int initialPoints = Mathf.Min(8, maxPoints / 6);
        
        // 在边界的关键位置添加初始点
        Vector2[] strategicPoints = {
            bounds.center, // 中心
            new Vector2(bounds.min.x + bounds.size.x * 0.25f, bounds.min.y + bounds.size.y * 0.25f), // 左下四分之一
            new Vector2(bounds.max.x - bounds.size.x * 0.25f, bounds.min.y + bounds.size.y * 0.25f), // 右下四分之一
            new Vector2(bounds.min.x + bounds.size.x * 0.25f, bounds.max.y - bounds.size.y * 0.25f), // 左上四分之一
            new Vector2(bounds.max.x - bounds.size.x * 0.25f, bounds.max.y - bounds.size.y * 0.25f), // 右上四分之一
            new Vector2(bounds.center.x, bounds.min.y + bounds.size.y * 0.1f), // 底部
            new Vector2(bounds.center.x, bounds.max.y - bounds.size.y * 0.1f), // 顶部
            new Vector2(bounds.min.x + bounds.size.x * 0.1f, bounds.center.y), // 左侧
            new Vector2(bounds.max.x - bounds.size.x * 0.1f, bounds.center.y)  // 右侧
        };
        
        for (int i = 0; i < Mathf.Min(initialPoints, strategicPoints.Length); i++)
        {
            AddPoint(strategicPoints[i], points, activePoints, grid, bounds, cellSize);
        }
        
        // 添加一些随机点填充剩余空间
        for (int i = strategicPoints.Length; i < initialPoints; i++)
        {
            Vector2 randomPoint = new Vector2(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y)
            );
            AddPoint(randomPoint, points, activePoints, grid, bounds, cellSize);
        }

        // 主循环
        while (activePoints.Count > 0 && points.Count < maxPoints)
        {
            int randomIndex = Random.Range(0, activePoints.Count);
            Vector2 point = activePoints[randomIndex];
            bool foundNewPoint = false;

            for (int tries = 0; tries < 30; tries++)
            {
                float angle = Random.Range(0, 2 * Mathf.PI);
                float distance = Random.Range(radius, 2 * radius);
                Vector2 newPoint = point + new Vector2(
                    Mathf.Cos(angle) * distance,
                    Mathf.Sin(angle) * distance
                );

                if (IsValidPoint(newPoint, radius, bounds, grid, points, cellSize))
                {
                    AddPoint(newPoint, points, activePoints, grid, bounds, cellSize);
                    foundNewPoint = true;
                    break;
                }
            }

            if (!foundNewPoint)
            {
                activePoints.RemoveAt(randomIndex);
            }
        }

        Debug.Log($"泊松圆盘采样完成: 生成了 {points.Count} 个点");
        return points;
    }

    private void AddPoint(Vector2 point, List<Vector2> points, List<Vector2> activePoints, int?[,] grid, Bounds bounds, float cellSize)
    {
        points.Add(point);
        activePoints.Add(point);

        int gridX = Mathf.FloorToInt((point.x - bounds.min.x) / cellSize);
        int gridY = Mathf.FloorToInt((point.y - bounds.min.y) / cellSize);

        if (gridX >= 0 && gridX < grid.GetLength(0) && gridY >= 0 && gridY < grid.GetLength(1))
        {
            grid[gridX, gridY] = points.Count - 1;
        }
    }

    private bool IsValidPoint(Vector2 point, float radius, Bounds bounds, int?[,] grid, List<Vector2> points, float cellSize)
    {
        if (!bounds.Contains(point)) return false;

        int gridX = Mathf.FloorToInt((point.x - bounds.min.x) / cellSize);
        int gridY = Mathf.FloorToInt((point.y - bounds.min.y) / cellSize);

        int startX = Mathf.Max(0, gridX - 2);
        int endX = Mathf.Min(grid.GetLength(0) - 1, gridX + 2);
        int startY = Mathf.Max(0, gridY - 2);
        int endY = Mathf.Min(grid.GetLength(1) - 1, gridY + 2);

        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                if (grid[x, y].HasValue)
                {
                    Vector2 existingPoint = points[grid[x, y].Value];
                    if (Vector2.Distance(point, existingPoint) < radius)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private void GenerateDelaunayTriangulation()
    {
        Debug.Log("⚠️ GenerateDelaunayTriangulation 已禁用，只使用 GameManager 生成的cells和edges");
        return;
    }

    private List<(int, int, int)> DelaunayTriangulation(List<Vector2> points)
    {
        var triangles = new List<(int, int, int)>();
        
        if (points.Count < 3) 
        {
            Debug.LogWarning($"点数不足: {points.Count}");
            return triangles;
        }

        Debug.Log($"开始Delaunay三角剖分，原始点数: {points.Count}");

        // 保存原始点的数量
        int originalPointCount = points.Count;
        
        // 创建超级三角形
        var superTriangle = CreateSuperTriangle(points);
        triangles.Add(superTriangle);
        Debug.Log($"创建超级三角形: {superTriangle}");

        // 逐点插入（只处理原始点，不包括超级三角形的虚拟点）
        for (int i = 0; i < originalPointCount; i++)
        {
            var point = points[i];
            Debug.Log($"处理点 {i}: {point}");
            
            var edges = new List<(int, int)>();
            
            // 找到所有包含当前点的三角形
            for (int j = triangles.Count - 1; j >= 0; j--)
            {
                var triangle = triangles[j];
                
                // 安全检查索引
                if (triangle.Item1 >= points.Count || triangle.Item2 >= points.Count || triangle.Item3 >= points.Count)
                {
                    Debug.LogWarning($"三角形索引越界: {triangle}, 点数: {points.Count}");
                    triangles.RemoveAt(j);
                    continue;
                }
                
                var a = points[triangle.Item1];
                var b = points[triangle.Item2];
                var c = points[triangle.Item3];
                
                // 检查点是否在三角形的外接圆内
                if (IsPointInCircumcircle(point, a, b, c))
                {
                    Debug.Log($"点 {i} 在三角形 ({triangle.Item1}, {triangle.Item2}, {triangle.Item3}) 的外接圆内");
                    
                    // 添加三角形的边到边列表
                    edges.Add((triangle.Item1, triangle.Item2));
                    edges.Add((triangle.Item2, triangle.Item3));
                    edges.Add((triangle.Item3, triangle.Item1));
                    
                    // 移除这个三角形
                    triangles.RemoveAt(j);
                }
            }
            
            Debug.Log($"找到 {edges.Count} 条边需要重新三角剖分");
            
            // 移除重复的边
            for (int j = edges.Count - 1; j >= 0; j--)
            {
                for (int k = j - 1; k >= 0; k--)
                {
                    if ((edges[j].Item1 == edges[k].Item1 && edges[j].Item2 == edges[k].Item2) ||
                        (edges[j].Item1 == edges[k].Item2 && edges[j].Item2 == edges[k].Item1))
                    {
                        edges.RemoveAt(j);
                        edges.RemoveAt(k);
                        // 由于移除了两个元素，需要调整索引
                        j--;
                        break;
                    }
                }
            }
            
            Debug.Log($"去重后剩余 {edges.Count} 条边");
            
            // 用剩余的边和当前点创建新的三角形
            foreach (var edge in edges)
            {
                var newTriangle = (edge.Item1, edge.Item2, i);
                triangles.Add(newTriangle);
                Debug.Log($"创建新三角形: {newTriangle}");
            }
            
            Debug.Log($"当前三角形数量: {triangles.Count}");
        }
        
        // 移除包含超级三角形顶点的三角形
        int removedCount = 0;
        for (int i = triangles.Count - 1; i >= 0; i--)
        {
            var triangle = triangles[i];
            if (triangle.Item1 >= originalPointCount || triangle.Item2 >= originalPointCount || triangle.Item3 >= originalPointCount)
            {
                triangles.RemoveAt(i);
                removedCount++;
            }
        }
        
        Debug.Log($"Delaunay三角剖分完成，生成了 {triangles.Count} 个三角形，移除了 {removedCount} 个包含超级三角形顶点的三角形");
        return triangles;
    }

    private (int, int, int) CreateSuperTriangle(List<Vector2> points)
    {
        float minX = points.Min(p => p.x);
        float maxX = points.Max(p => p.x);
        float minY = points.Min(p => p.y);
        float maxY = points.Max(p => p.y);

        float dx = maxX - minX;
        float dy = maxY - minY;
        float dmax = Mathf.Max(dx, dy);
        float midx = (minX + maxX) * 0.5f;
        float midy = (minY + maxY) * 0.5f;

        // 创建超级三角形的虚拟点（这些点不会在points数组中）
        // 我们需要在points数组末尾添加这些虚拟点
        points.Add(new Vector2(midx - 2 * dmax, midy - dmax));
        points.Add(new Vector2(midx + 2 * dmax, midy - dmax));
        points.Add(new Vector2(midx, midy + 2 * dmax));

        return (points.Count - 3, points.Count - 2, points.Count - 1);
    }

    private bool IsPointInCircumcircle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float ax = a.x - p.x;
        float ay = a.y - p.y;
        float bx = b.x - p.x;
        float by = b.y - p.y;
        float cx = c.x - p.x;
        float cy = c.y - p.y;

        float det = ax * (by * (cx * cx + cy * cy) - cy * (bx * bx + by * by)) -
                   ay * (bx * (cx * cx + cy * cy) - cx * (bx * bx + by * by)) +
                   (ax * ax + ay * ay) * (bx * cy - cx * by);

        return det > 0;
    }

    private bool IsSharedEdge((int, int) edge, List<(int, int, int)> triangles, (int, int, int) excludeTriangle)
    {
        int count = 0;
        foreach (var triangle in triangles)
        {
            if (triangle.Equals(excludeTriangle)) continue;

            var edges = new[] { (triangle.Item1, triangle.Item2), (triangle.Item2, triangle.Item3), (triangle.Item3, triangle.Item1) };
            foreach (var triEdge in edges)
            {
                if ((triEdge.Item1 == edge.Item1 && triEdge.Item2 == edge.Item2) ||
                    (triEdge.Item1 == edge.Item2 && triEdge.Item2 == edge.Item1))
                {
                    count++;
                }
            }
        }
        return count > 0;
    }

    // 已禁用：边的创建
    private void CreateEdge(Cell cellA, Cell cellB)
    {
        Debug.Log("⚠️ CreateEdge 已禁用，只使用 GameManager 生成的edges");
        return;
    }
    
    private int GetOrCreateEdgeWeight(Cell a, Cell b)
    {
        var edgeKey = GetCanonicalEdge(a, b);
        
        if (_edgeWeightCache.ContainsKey(edgeKey))
        {
            return _edgeWeightCache[edgeKey];
        }
        
        // 计算地形权重
        int weight = CalculateTerrainBasedWeight(a.transform.position, b.transform.position);
        
        // 缓存权重
        _edgeWeightCache[edgeKey] = weight;
        
        return weight;
    }

    private (Cell, Cell) GetCanonicalEdge(Cell cellA, Cell cellB)
    {
        return cellA.Number < cellB.Number ? (cellA, cellB) : (cellB, cellA);
    }

    private int CalculateTerrainBasedWeight(Vector3 pointA, Vector3 pointB)
    {
        var crossedBiomes = new List<HexCoordinateSystem.BiomeType>();

        // 沿线段采样
        float distance = Vector3.Distance(pointA, pointB);
        int sampleCount = Mathf.Max(3, Mathf.RoundToInt(distance / 0.5f));

        for (int i = 0; i <= sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            Vector3 samplePos = Vector3.Lerp(pointA, pointB, t);

            Vector3Int cellPos = terrainManager.tilemap.WorldToCell(samplePos);
            var hex = terrainManager.GetHexTiles().FirstOrDefault(h => {
                Vector3Int hexPos = terrainManager.ConvertHexToTilePosition(h);
                return hexPos.x == cellPos.x && hexPos.y == cellPos.y;
            });

            if (hex != null)
            {
                crossedBiomes.Add(hex.biome);
            }
        }

        if (crossedBiomes.Count == 0) return gameManager.GetBiomeWeight(-1); // 使用GameManager的默认权重

        // 简单累加所有地形的权重
        int totalWeight = 0;
        foreach (var biome in crossedBiomes)
        {
            int biomeWeight = gameManager.GetBiomeWeight((int)biome); // 使用GameManager的权重
            totalWeight += biomeWeight;
        }
        
        // 返回累加的权重，不进行范围限制，让权重自然反映地形的累积效果
        return totalWeight;
    }

    // 已禁用：边的创建
    private void CreateEdgeLine(Cell cellA, Cell cellB, int weight)
    {
        // 已禁用
    }


    


    // 新增：显示edge经过的tile信息
    public void ShowEdgeTileInfo(Cell cellA, Cell cellB)
    {
        Debug.Log($"🔍 分析Edge: Cell {cellA.Number} -> Cell {cellB.Number}");
        
        var crossedBiomes = new List<HexCoordinateSystem.BiomeType>();
        var crossedTiles = new List<(Vector3Int position, HexCoordinateSystem.BiomeType biome)>();

        // 沿线段采样
        float distance = Vector3.Distance(cellA.transform.position, cellB.transform.position);
        int sampleCount = Mathf.Max(3, Mathf.RoundToInt(distance / 0.5f));

        Debug.Log($"📏 Edge长度: {distance:F2} 单位，采样点数量: {sampleCount + 1}");

        for (int i = 0; i <= sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            Vector3 samplePos = Vector3.Lerp(cellA.transform.position, cellB.transform.position, t);

            Vector3Int cellPos = terrainManager.tilemap.WorldToCell(samplePos);
            var hex = terrainManager.GetHexTiles().FirstOrDefault(h => {
                Vector3Int hexPos = terrainManager.ConvertHexToTilePosition(h);
                return hexPos.x == cellPos.x && hexPos.y == cellPos.y;
            });

            if (hex != null)
            {
                crossedBiomes.Add(hex.biome);
                crossedTiles.Add((cellPos, hex.biome));
                
                Debug.Log($"📍 采样点 {i + 1}/{sampleCount + 1}: 位置 {samplePos:F2} -> Tile坐标 ({cellPos.x}, {cellPos.y}) -> 生物群系: {hex.biome}");
            }
            else
            {
                Debug.LogWarning($"⚠️ 采样点 {i + 1}/{sampleCount + 1}: 位置 {samplePos:F2} -> Tile坐标 ({cellPos.x}, {cellPos.y}) -> 未找到对应的HexTile");
            }
        }

        if (crossedTiles.Count == 0)
        {
            Debug.LogWarning("❌ 未找到任何经过的tile！");
            return;
        }

        // 统计信息
        var biomeCounts = crossedBiomes.GroupBy(b => b).ToDictionary(g => g.Key, g => g.Count());
        
        Debug.Log("📊 Edge经过的Tile统计:");
        Debug.Log($"   总Tile数量: {crossedTiles.Count}");
        
        foreach (var kvp in biomeCounts)
        {
            int biomeWeight = gameManager.GetBiomeWeight((int)kvp.Key); // 使用GameManager的权重
            Debug.Log($"   {kvp.Key}: {kvp.Value} 个tile (权重: {biomeWeight})");
        }

        // 计算总权重
        int totalWeight = 0;
        foreach (var biome in crossedBiomes)
        {
            int biomeWeight = gameManager.GetBiomeWeight((int)biome); // 使用GameManager的权重
            totalWeight += biomeWeight;
        }
        
        Debug.Log($"🎯 Edge最终权重: {totalWeight}");
        Debug.Log("---");
    }

    // 已禁用：权重标签创建
    private void CreateWeightLabel(Vector3 posA, Vector3 posB, int weight, Cell cellA = null, Cell cellB = null)
    {
        // 已禁用
    }
    
    private void AddWeightClickHandler(GameObject labelObj, Cell cellA, Cell cellB)
    {
        Debug.Log($"🔍 AddWeightClickHandler被调用: labelObj={labelObj.name}, cellA={cellA?.Number}, cellB={cellB?.Number}");
        
        if (cellA == null || cellB == null)
        {
            Debug.LogWarning("⚠️ 无法添加WeightClickHandler：Cell引用为空");
            return;
        }
        
        // 检查是否已经有WeightClickHandler组件
        WeightClickHandler existingHandler = labelObj.GetComponent<WeightClickHandler>();
        if (existingHandler != null)
        {
            Debug.LogWarning($"⚠️ 对象 {labelObj.name} 已经有WeightClickHandler组件");
            return;
        }
        
        // 添加点击检测组件
        WeightClickHandler clickHandler = labelObj.AddComponent<WeightClickHandler>();
        if (clickHandler == null)
        {
            Debug.LogError($"❌ 无法添加WeightClickHandler组件到 {labelObj.name}");
            return;
        }
        
        clickHandler.Initialize(cellA, cellB, this);
        
        Debug.Log($"✅ 为Weight标签添加了点击检测: Cell {cellA.Number} -> Cell {cellB.Number}");
        
        // 验证组件是否真的被添加了
        WeightClickHandler verifyHandler = labelObj.GetComponent<WeightClickHandler>();
        if (verifyHandler != null)
        {
            Debug.Log($"✅ 验证成功: {labelObj.name} 现在有WeightClickHandler组件");
        }
        else
        {
            Debug.LogError($"❌ 验证失败: {labelObj.name} 没有WeightClickHandler组件");
        }
    }
    
    // 已禁用：动态权重标签创建
    private void CreateDynamicWeightLabel(Vector3 position, int weight, Cell cellA = null, Cell cellB = null)
    {
        // 已禁用
    }



    private void ClearGeneratedContent()
    {
        // 清除节点
        foreach (var cell in generatedCells)
        {
            if (cell != null)
                DestroyImmediate(cell.gameObject);
        }
        generatedCells.Clear();

        // 清除边
        foreach (var line in edgeLines.Values)
        {
            if (line != null)
                DestroyImmediate(line.gameObject);
        }
        edgeLines.Clear();
        generatedEdges.Clear();
        
        // 清除权重缓存（类似GameManager.cs）
        _edgeWeightCache.Clear();

        // 清除根对象
        if (linesRoot != null)
            DestroyImmediate(linesRoot);
        
        if (cellsRoot != null)
            DestroyImmediate(cellsRoot);
        
        // 重置为null，避免在OnDestroy时重新创建
        linesRoot = null;
        cellsRoot = null;
    }



    void OnDestroy()
    {
        ClearGeneratedContent();
    }

    // 全局点击检测功能
    private void Update()
    {
        if (!enableGlobalClickDetection) return;

        if (Input.GetMouseButtonDown(0)) // 左键点击
        {
            HandleGlobalMouseClick();
        }
    }

    private void HandleGlobalMouseClick()
    {
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 mousePos2D = new Vector2(mouseWorld.x, mouseWorld.y);

        // 检测所有碰撞器
        Collider2D[] allColliders = Physics2D.OverlapPointAll(mousePos2D);
        
        if (allColliders.Length == 0)
        {
            // Debug.Log($"🖱️ 点击位置: ({mouseWorld.x:F2}, {mouseWorld.y:F2}) - 未检测到任何对象");
            return;
        }

        Debug.Log($"🖱️ 点击位置: ({mouseWorld.x:F2}, {mouseWorld.y:F2}) - 点击到的对象: {allColliders[0].gameObject.name}");
    }

    // 处理Inspector中的按钮点击
    // void OnValidate()
    // {
    //     if (testWeightClick)
    //     {
    //         testWeightClick = false; // 重置按钮
    //         TestWeightClickFunction();
    //     }
    //     if (testBasicMouseClick)
    //     {
    //         testBasicMouseClick = false; // 重置按钮
    //         TestBasicMouseClick();
    //     }
    //     if (startDebugMouseClick)
    //     {
    //         startDebugMouseClick = false; // 重置按钮
    //         enableDebugMouseClick = true;
    //         DebugMouseClickDetection();
    //     }
    //     if (stopDebugMouseClick)
    //     {
    //         stopDebugMouseClick = false; // 重置按钮
    //         enableDebugMouseClick = false;
    //         StopDebugMouseClickDetection();
    //     }
    // }
} 