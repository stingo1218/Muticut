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

    [System.Serializable]
    public class TerrainWeights
    {
        public int grassWeight = 5;
        public int plainsWeight = 4;
        public int shallowWaterWeight = 3;
        public int forestWeight = -6;
        public int deepWaterWeight = -8;
        public int mountainWeight = -10;
        public int highMountainWeight = -15;
        public int volcanoWeight = -20;
        public int riverWeight = -12;
        public int defaultWeight = 0;

        public int GetWeightForBiome(HexCoordinateSystem.BiomeType biome)
        {
            switch (biome)
            {
                case HexCoordinateSystem.BiomeType.FlatGrass: return grassWeight;
                case HexCoordinateSystem.BiomeType.FlatDesert1: 
                case HexCoordinateSystem.BiomeType.FlatDesert2: return plainsWeight;
                case HexCoordinateSystem.BiomeType.ShallowWater: return shallowWaterWeight;
                case HexCoordinateSystem.BiomeType.FlatForest: 
                case HexCoordinateSystem.BiomeType.FlatForestSwampy: return forestWeight;
                case HexCoordinateSystem.BiomeType.DeepWater: return deepWaterWeight;
                case HexCoordinateSystem.BiomeType.MountainDesert:
                case HexCoordinateSystem.BiomeType.MountainShrubland1:
                case HexCoordinateSystem.BiomeType.MountainShrubland2:
                case HexCoordinateSystem.BiomeType.MountainAlpine1:
                case HexCoordinateSystem.BiomeType.MountainAlpine2:
                case HexCoordinateSystem.BiomeType.MountainImpassable1:
                case HexCoordinateSystem.BiomeType.MountainImpassable2: return mountainWeight;
                case HexCoordinateSystem.BiomeType.HillDesert:
                case HexCoordinateSystem.BiomeType.HillGrass:
                case HexCoordinateSystem.BiomeType.HillForest:
                case HexCoordinateSystem.BiomeType.HillForestNeedleleaf: return highMountainWeight;
                case HexCoordinateSystem.BiomeType.Volcano: return volcanoWeight;
                case HexCoordinateSystem.BiomeType.FlatSparseTrees1:
                case HexCoordinateSystem.BiomeType.FlatSparseTrees2: return riverWeight; // 临时用河流权重
                default: return defaultWeight;
            }
        }
    }
    public TerrainWeights terrainWeights = new TerrainWeights();

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
        linesRoot.hideFlags = HideFlags.DontSave;
        
        cellsRoot = new GameObject("TilemapCellsRoot");
        cellsRoot.hideFlags = HideFlags.DontSave;
        
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
        // 详细的调试信息
        if (terrainManager == null)
        {
            Debug.LogError("❌ TerrainManager 为 null！请检查组件引用。");
            return;
        }
        
        var hexTiles = terrainManager.GetHexTiles();
        if (hexTiles == null)
        {
            Debug.LogWarning("⚠️ 地形数据为空，尝试自动生成地形...");
            
            // 尝试自动生成地形
            try
            {
                terrainManager.GenerateTerrain();
                hexTiles = terrainManager.GetHexTiles();
                
                if (hexTiles == null || hexTiles.Count == 0)
                {
                    Debug.LogError("❌ 自动生成地形失败！");
                    Debug.Log("💡 解决方案：请手动在 TerrainManager 组件上右键选择'生成地形'");
                    return;
                }
                
                Debug.Log($"✅ 自动生成地形成功！生成了 {hexTiles.Count} 个六边形");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ 自动生成地形时发生错误：{e.Message}");
                Debug.Log("💡 解决方案：请手动在 TerrainManager 组件上右键选择'生成地形'");
                return;
            }
        }
        
        if (hexTiles.Count == 0)
        {
            Debug.LogError("❌ 地形数据为空！请先生成地形。");
            Debug.Log("💡 提示：右键点击 TerrainManager 组件，选择'生成地形'");
            return;
        }
        
        Debug.Log($"✅ 找到地形数据：{hexTiles.Count} 个六边形");

        ClearGeneratedContent();

        // 获取地形边界
        var terrainBounds = CalculateTerrainBounds(hexTiles);
        Debug.Log($"地形边界: {terrainBounds.min} 到 {terrainBounds.max}");

        // 🎯 新方案：先生成点，找到外界矩形，拉伸到地图80%大小居中，调整点位置，然后连线

        // 步骤1: 先生成点（使用较大的边界确保有足够的点）
        var expandedBounds = terrainBounds;
        expandedBounds.Expand(2.0f); // 扩大边界以获得更多点
        var rawNodePositions = PoissonDiskSampling(expandedBounds, samplingRadius, maxNodes);
        Debug.Log($"步骤1完成: 生成了 {rawNodePositions.Count} 个原始点");

        // 步骤2: 找到外界矩形
        var pointBounds = CalculatePointBounds(rawNodePositions);
        Debug.Log($"步骤2完成: 点集边界: {pointBounds.min} 到 {pointBounds.max}");

        // 步骤3: 计算目标边界（地形80%大小，居中）
        var targetBounds = CalculateTargetBounds(terrainBounds, 0.9f);
        Debug.Log($"步骤3完成: 目标边界: {targetBounds.min} 到 {targetBounds.max}");

        // 步骤4: 调整点位置（拉伸和居中）
        var adjustedPositions = AdjustPointPositions(rawNodePositions, pointBounds, targetBounds);
        Debug.Log($"步骤4完成: 调整了 {adjustedPositions.Count} 个点的位置");

        // 步骤5: 创建Cell对象
        foreach (var position in adjustedPositions)
        {
            CreateCellAtPosition(position);
        }

        // 步骤6: 生成Delaunay三角剖分连线
        GenerateDelaunayTriangulation();

        Debug.Log($"✅ 完成！生成了 {generatedCells.Count} 个节点和 {generatedEdges.Count} 条边");
        
        // 自动调整渲染顺序，确保cells和weights显示在edges之上
        AdjustRenderingOrder();
        
        // 验证最终结果
        var finalBounds = CalculatePointBounds(adjustedPositions);
        Debug.Log($"最终点集边界: {finalBounds.min} 到 {finalBounds.max}");
        Debug.Log($"覆盖率: X={finalBounds.size.x/terrainBounds.size.x:F2}, Y={finalBounds.size.y/terrainBounds.size.y:F2}");
    }

    private void CreateCellAtPosition(Vector3 worldPosition)
    {
        if (cellPrefab == null)
        {
            Debug.LogError("Cell预制体未设置！");
            return;
        }

        GameObject cellObj = Instantiate(cellPrefab, worldPosition, Quaternion.identity);
        cellObj.hideFlags = HideFlags.DontSave;
        
        // 将Cell对象设置为TilemapCellsRoot的子对象
        cellObj.transform.SetParent(cellsRoot.transform);
        
        Cell cell = cellObj.GetComponent<Cell>();
        if (cell != null)
        {
            // 调用Cell的Init方法，传入false表示这是普通Cell而不是权重标签
            cell.Init(generatedCells.Count, false);
            generatedCells.Add(cell);
        }
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
        if (generatedCells.Count < 3) 
        {
            Debug.LogWarning("节点数量不足，无法生成三角剖分");
            return;
        }

        Debug.Log($"开始生成Delaunay三角剖分，节点数: {generatedCells.Count}");

        // 简化的三角剖分
        var points = generatedCells.Select(c => new Vector2(c.transform.position.x, c.transform.position.y)).ToList();
        var triangles = DelaunayTriangulation(points);

        Debug.Log($"生成了 {triangles.Count} 个三角形");

        // 创建边
        int edgesCreated = 0;
        foreach (var triangle in triangles)
        {
            Debug.Log($"处理三角形: ({triangle.Item1}, {triangle.Item2}, {triangle.Item3})");
            
            // 安全检查
            if (triangle.Item1 >= 0 && triangle.Item1 < generatedCells.Count &&
                triangle.Item2 >= 0 && triangle.Item2 < generatedCells.Count &&
                triangle.Item3 >= 0 && triangle.Item3 < generatedCells.Count)
            {
                CreateEdge(generatedCells[triangle.Item1], generatedCells[triangle.Item2]);
                CreateEdge(generatedCells[triangle.Item2], generatedCells[triangle.Item3]);
                CreateEdge(generatedCells[triangle.Item3], generatedCells[triangle.Item1]);
                edgesCreated += 3;
            }
            else
            {
                Debug.LogError($"三角形索引越界: {triangle.Item1}, {triangle.Item2}, {triangle.Item3}, 节点数: {generatedCells.Count}");
            }
        }
        
        Debug.Log($"成功创建了 {edgesCreated} 条边");
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

    private void CreateEdge(Cell cellA, Cell cellB)
    {
        if (cellA == cellB) 
        {
            Debug.LogWarning("尝试创建自环边，跳过");
            return;
        }

        var edge = GetCanonicalEdge(cellA, cellB);
        if (generatedEdges.Contains(edge)) 
        {
            Debug.Log($"边已存在: {cellA.Number} - {cellB.Number}");
            return;
        }

        generatedEdges.Add(edge);
        Debug.Log($"创建新边: {cellA.Number} - {cellB.Number}");

        // 获取或创建权重（类似GameManager.cs）
        int weight = GetOrCreateEdgeWeight(cellA, cellB);

        // 创建可视化线条
        CreateEdgeLine(cellA, cellB, weight);
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

        if (crossedBiomes.Count == 0) return terrainWeights.defaultWeight;

        int totalWeight = 0;
        int minWeight = int.MaxValue;

        foreach (var biome in crossedBiomes)
        {
            int biomeWeight = terrainWeights.GetWeightForBiome(biome);
            totalWeight += biomeWeight;
            minWeight = Mathf.Min(minWeight, biomeWeight);
        }

        int avgWeight = totalWeight / crossedBiomes.Count;
        int finalWeight = Mathf.RoundToInt(0.7f * minWeight + 0.3f * avgWeight);
        
        // 限制权重范围，避免过大的负值
        return Mathf.Clamp(finalWeight, -20, 10);
    }

    private void CreateEdgeLine(Cell cellA, Cell cellB, int weight)
    {
        GameObject lineObj = new GameObject($"Line_{cellA.Number}_{cellB.Number}");
        lineObj.hideFlags = HideFlags.DontSave;
        lineObj.transform.SetParent(linesRoot.transform);

        LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(lineMaterial); // 创建独立的材质实例
        
        // 根据权重调整线条样式（类似GameManager.cs）
        float lineWidth = Mathf.Clamp(Mathf.Abs(weight) * lineWidthMultiplier + 0.05f, 0.05f, 0.5f);
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;

        // 根据权重选择线条样式（不设置颜色）
        if (weight > 0)
        {
            // 正权重：实线
            lineRenderer.sharedMaterial.mainTextureScale = new Vector2(1, 1); // 实线
        }
        else if (weight < 0)
        {
            // 负权重：虚线
            lineRenderer.sharedMaterial.mainTextureScale = new Vector2(5, 1); // 虚线
        }
        else
        {
            // 零权重：点线
            lineRenderer.sharedMaterial.mainTextureScale = new Vector2(10, 1); // 点线
        }

        lineRenderer.SetPosition(0, cellA.transform.position);
        lineRenderer.SetPosition(1, cellB.transform.position);

        // 设置线条在第三层的Edge层
        lineObj.layer = 2; // 第三层（索引为2）
        
        // 设置渲染层级，确保线条显示在地形之上，但在cells和weights之下
        lineRenderer.sortingOrder = 5; // 降低排序顺序，让cells和weights显示在上方
        lineRenderer.sortingLayerName = "Default"; // 确保在正确的排序层

        edgeLines[(cellA, cellB)] = lineRenderer;

        // 添加权重标签
        if (showWeightLabels)
        {
            CreateWeightLabel(cellA.transform.position, cellB.transform.position, weight);
        }
    }

    private void CreateWeightLabel(Vector3 posA, Vector3 posB, int weight)
    {
        Vector3 midPoint = (posA + posB) * 0.5f;
        
        // 检查是否有权重标签预制件
        if (weightLabelPrefab == null)
        {
            Debug.LogWarning("⚠️ 权重标签预制件未设置，将使用动态创建的TextMesh");
            CreateDynamicWeightLabel(midPoint, weight);
            return;
        }
        
        Debug.Log($"🔍 使用权重标签预制件: {weightLabelPrefab.name}");
        
        // 实例化权重标签预制件
        GameObject labelObj = Instantiate(weightLabelPrefab, midPoint, Quaternion.identity);
        labelObj.hideFlags = HideFlags.DontSave;
        labelObj.transform.SetParent(linesRoot.transform);
        labelObj.name = $"EdgeWeightText_{weight}";
        
        // 如果权重标签预制件使用的是Cell脚本，需要正确初始化
        Cell cellComponent = labelObj.GetComponent<Cell>();
        if (cellComponent != null)
        {
            // 调用Cell的Init方法，传入true表示这是权重标签
            cellComponent.Init(weight, true);
            cellComponent.Number = weight; // 设置权重值作为数字
        }
        
        // 尝试获取TextMeshProUGUI组件（UI版本，优先）
        TextMeshProUGUI textMeshProUGUI = labelObj.GetComponent<TextMeshProUGUI>();
        if (textMeshProUGUI == null)
        {
            textMeshProUGUI = labelObj.GetComponentInChildren<TextMeshProUGUI>();
            if (textMeshProUGUI != null)
            {
                Debug.Log($"✅ 在子对象中找到TextMeshProUGUI组件: {textMeshProUGUI.name}");
            }
        }
        else
        {
            Debug.Log($"✅ 在根对象中找到TextMeshProUGUI组件: {textMeshProUGUI.name}");
        }
        
        // 如果找到TextMeshProUGUI，使用它
        if (textMeshProUGUI != null)
        {
            textMeshProUGUI.text = weight.ToString();
            
            // 对于UI元素，通过Canvas设置渲染层级
            Canvas canvas = labelObj.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.sortingOrder = 25; // 提高排序顺序，确保显示在最上层
                canvas.sortingLayerName = "Default";
            }
            
            // 稍微向上偏移，避免与线条重叠
            labelObj.transform.position += Vector3.up * 0.3f;
            return;
        }
        
        // 尝试获取TextMeshPro组件（3D版本，作为备选）
        TextMeshPro textMeshPro = labelObj.GetComponent<TextMeshPro>();
        if (textMeshPro == null)
        {
            textMeshPro = labelObj.GetComponentInChildren<TextMeshPro>();
            if (textMeshPro != null)
            {
                Debug.Log($"✅ 在子对象中找到TextMeshPro组件: {textMeshPro.name}");
            }
        }
        else
        {
            Debug.Log($"✅ 在根对象中找到TextMeshPro组件: {textMeshPro.name}");
        }
        
        // 如果找到TextMeshPro，使用它
        if (textMeshPro != null)
        {
            textMeshPro.text = weight.ToString();
            
            // 设置渲染层级
            textMeshPro.sortingOrder = 25; // 提高排序顺序，确保显示在最上层
            // TextMeshPro的sortingLayerName需要通过Renderer组件设置
            Renderer renderer = textMeshPro.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sortingLayerName = "Default";
            }
            
            // 稍微向上偏移，避免与线条重叠
            labelObj.transform.position += Vector3.up * 0.3f;
            return;
        }
        
        // 尝试获取TextMesh组件
        TextMesh textMesh = labelObj.GetComponent<TextMesh>();
        if (textMesh == null)
        {
            textMesh = labelObj.GetComponentInChildren<TextMesh>();
        }
        
        // 如果找到TextMesh，使用它
        if (textMesh != null)
        {
            textMesh.text = weight.ToString();
            textMesh.fontSize = 20;
            textMesh.characterSize = 0.1f;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            
            // 设置渲染层级
            textMesh.GetComponent<MeshRenderer>().sortingOrder = 25; // 提高排序顺序，确保显示在最上层
            textMesh.GetComponent<MeshRenderer>().sortingLayerName = "Default";
            
            // 稍微向上偏移，避免与线条重叠
            labelObj.transform.position += Vector3.up * 0.3f;
            return;
        }
        
        // 如果预制件中没有找到文本组件，回退到动态创建
        Debug.LogWarning("⚠️ 权重标签预制件中没有找到TextMesh、TextMeshPro或TextMeshProUGUI组件，将使用动态创建");
        DestroyImmediate(labelObj);
        CreateDynamicWeightLabel(midPoint, weight);
    }
    
    private void CreateDynamicWeightLabel(Vector3 position, int weight)
    {
        GameObject labelObj = new GameObject($"EdgeWeightText_{weight}");
        labelObj.hideFlags = HideFlags.DontSave;
        labelObj.transform.SetParent(linesRoot.transform);
        labelObj.transform.position = position;

        // 添加TextMesh组件来显示权重
        TextMesh textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = weight.ToString();
        textMesh.fontSize = 20;
        textMesh.characterSize = 0.1f;
        textMesh.alignment = TextAlignment.Center;
        textMesh.anchor = TextAnchor.MiddleCenter;
        
        // 设置渲染层级，确保文本显示在线条之上
        textMesh.GetComponent<MeshRenderer>().sortingOrder = 25; // 提高排序顺序，确保显示在最上层
        textMesh.GetComponent<MeshRenderer>().sortingLayerName = "Default";
        
        // 稍微向上偏移，避免与线条重叠
        labelObj.transform.position += Vector3.up * 0.3f;
    }

    [ContextMenu("显示地形权重信息")]
    public void ShowTerrainWeightInfo()
    {
        Debug.Log($"地形权重配置:");
        Debug.Log($"草地: {terrainWeights.grassWeight}");
        Debug.Log($"平原: {terrainWeights.plainsWeight}");
        Debug.Log($"浅水: {terrainWeights.shallowWaterWeight}");
        Debug.Log($"森林: {terrainWeights.forestWeight}");
        Debug.Log($"深水: {terrainWeights.deepWaterWeight}");
        Debug.Log($"山地: {terrainWeights.mountainWeight}");
        Debug.Log($"高山: {terrainWeights.highMountainWeight}");
        Debug.Log($"火山: {terrainWeights.volcanoWeight}");
        Debug.Log($"河流: {terrainWeights.riverWeight}");
    }

    [ContextMenu("切换权重标签显示")]
    public void ToggleWeightLabels()
    {
        showWeightLabels = !showWeightLabels;
        Debug.Log($"权重标签显示: {(showWeightLabels ? "开启" : "关闭")}");
    }

    [ContextMenu("分析边权重分布")]
    public void AnalyzeEdgeWeightDistribution()
    {
        if (generatedEdges.Count == 0)
        {
            Debug.Log("没有生成的边可以分析");
            return;
        }

        var weights = new List<int>();
        foreach (var edge in generatedEdges)
        {
            int weight = CalculateTerrainBasedWeight(edge.Item1.transform.position, edge.Item2.transform.position);
            weights.Add(weight);
        }

        Debug.Log($"边权重分布分析:");
        Debug.Log($"总边数: {weights.Count}");
        Debug.Log($"平均权重: {weights.Average():F2}");
        Debug.Log($"最大权重: {weights.Max()}");
        Debug.Log($"最小权重: {weights.Min()}");
        Debug.Log($"正权重边数: {weights.Count(w => w > 0)}");
        Debug.Log($"负权重边数: {weights.Count(w => w < 0)}");
        Debug.Log($"零权重边数: {weights.Count(w => w == 0)}");
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
        
        linesRoot = new GameObject("TilemapLinesRoot");
        linesRoot.hideFlags = HideFlags.DontSave;
        
        cellsRoot = new GameObject("TilemapCellsRoot");
        cellsRoot.hideFlags = HideFlags.DontSave;
    }

    [ContextMenu("强制生成地形")]
    public void ForceGenerateTerrain()
    {
        Debug.Log("🔧 强制生成地形...");
        
        if (terrainManager == null)
        {
            Debug.LogError("❌ terrainManager 引用为空");
            return;
        }
        
        try
        {
            terrainManager.GenerateTerrain();
            var hexTiles = terrainManager.GetHexTiles();
            
            if (hexTiles != null && hexTiles.Count > 0)
            {
                Debug.Log($"✅ 地形生成成功！生成了 {hexTiles.Count} 个六边形");
            }
            else
            {
                Debug.LogError("❌ 地形生成失败");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 生成地形时发生错误：{e.Message}");
        }
    }

    [ContextMenu("检查TerrainManager状态")]
    public void CheckTerrainManagerStatus()
    {
        Debug.Log("🔍 检查 TerrainManager 状态...");
        
        if (terrainManager == null)
        {
            Debug.LogError("❌ terrainManager 引用为空");
            Debug.Log("💡 解决方案：在 Inspector 中拖入 TerrainManager 组件");
            return;
        }
        
        Debug.Log($"✅ terrainManager 引用正常：{terrainManager.name}");
        
        var hexTiles = terrainManager.GetHexTiles();
        if (hexTiles == null)
        {
            Debug.LogWarning("⚠️ GetHexTiles() 返回 null - 地形尚未生成");
            Debug.Log("💡 解决方案：");
            Debug.Log("   1. 在 TerrainManager 组件上右键选择'生成地形'");
            Debug.Log("   2. 或者设置 TerrainManager 的 autoGenerateOnStart = true");
            Debug.Log("   3. 或者调用 GenerateNodesOnTerrain() 会自动尝试生成地形");
            return;
        }
        
        Debug.Log($"✅ 地形数据：{hexTiles.Count} 个六边形");
        
        if (hexTiles.Count > 0)
        {
            var firstHex = hexTiles[0];
            Debug.Log($"✅ 第一个六边形：坐标({firstHex.coord.q}, {firstHex.coord.r}), 生物群系：{firstHex.biome}");
        }
        
        if (terrainManager.tilemap == null)
        {
            Debug.LogError("❌ terrainManager.tilemap 为空");
        }
        else
        {
            Debug.Log($"✅ tilemap 引用正常");
        }
        
        // 检查 TerrainManager 的设置
        var settings = terrainManager.GetTerrainSettings();
        if (settings != null)
        {
            Debug.Log($"✅ 地形设置：{settings.hexColumns} × {settings.hexRows} 网格");
        }
    }

    [ContextMenu("检查边界计算")]
    public void CheckBoundsCalculation()
    {
        if (terrainManager == null || terrainManager.GetHexTiles() == null)
        {
            Debug.LogError("❌ TerrainManager 不可用");
            return;
        }

        var hexTiles = terrainManager.GetHexTiles();
        var bounds = CalculateTerrainBounds(hexTiles);
        
        Debug.Log($"🔍 边界计算结果:");
        Debug.Log($"边界中心: {bounds.center}");
        Debug.Log($"边界大小: {bounds.size}");
        Debug.Log($"边界最小值: {bounds.min}");
        Debug.Log($"边界最大值: {bounds.max}");
        
        // 检查几个样本点的世界坐标
        for (int i = 0; i < Mathf.Min(5, hexTiles.Count); i++)
        {
            var hex = hexTiles[i];
            Vector3Int tilePos = terrainManager.ConvertHexToTilePosition(hex);
            Vector3 worldPos = terrainManager.tilemap.CellToWorld(tilePos);
            Debug.Log($"样本{i}: 六边形({hex.coord.q},{hex.coord.r}) -> 瓦片({tilePos.x},{tilePos.y}) -> 世界({worldPos.x:F2},{worldPos.y:F2})");
        }
    }

    [ContextMenu("检查地形分布")]
    public void CheckTerrainDistribution()
    {
        if (terrainManager == null || terrainManager.GetHexTiles() == null)
        {
            Debug.LogError("❌ TerrainManager 不可用");
            return;
        }

        var hexTiles = terrainManager.GetHexTiles();
        
        // 统计地形分布
        var biomeCounts = new Dictionary<HexCoordinateSystem.BiomeType, int>();
        var coordRanges = new Dictionary<string, (int min, int max)>();
        
        foreach (var hex in hexTiles)
        {
            // 统计生物群系
            if (!biomeCounts.ContainsKey(hex.biome))
                biomeCounts[hex.biome] = 0;
            biomeCounts[hex.biome]++;
            
            // 统计坐标范围
            if (!coordRanges.ContainsKey("q"))
                coordRanges["q"] = (hex.coord.q, hex.coord.q);
            else
                coordRanges["q"] = (Mathf.Min(coordRanges["q"].min, hex.coord.q), Mathf.Max(coordRanges["q"].max, hex.coord.q));
                
            if (!coordRanges.ContainsKey("r"))
                coordRanges["r"] = (hex.coord.r, hex.coord.r);
            else
                coordRanges["r"] = (Mathf.Min(coordRanges["r"].min, hex.coord.r), Mathf.Max(coordRanges["r"].max, hex.coord.r));
        }
        
        Debug.Log($"🔍 地形分布分析:");
        Debug.Log($"总六边形数: {hexTiles.Count}");
        Debug.Log($"Q坐标范围: {coordRanges["q"].min} 到 {coordRanges["q"].max}");
        Debug.Log($"R坐标范围: {coordRanges["r"].min} 到 {coordRanges["r"].max}");
        
        Debug.Log($"生物群系分布:");
        foreach (var kvp in biomeCounts.OrderByDescending(x => x.Value))
        {
            Debug.Log($"  {kvp.Key}: {kvp.Value} 个");
        }
    }

    [ContextMenu("检查采样点分布")]
    public void CheckSamplingDistribution()
    {
        if (terrainManager == null || terrainManager.GetHexTiles() == null)
        {
            Debug.LogError("❌ TerrainManager 不可用");
            return;
        }

        var hexTiles = terrainManager.GetHexTiles();
        var bounds = CalculateTerrainBounds(hexTiles);
        var nodePositions = PoissonDiskSampling(bounds, samplingRadius, maxNodes);
        
        Debug.Log($"🔍 采样点分布分析:");
        Debug.Log($"边界: {bounds.min} 到 {bounds.max}");
        Debug.Log($"采样半径: {samplingRadius}");
        Debug.Log($"最大节点数: {maxNodes}");
        Debug.Log($"实际生成节点数: {nodePositions.Count}");
        
        if (nodePositions.Count > 0)
        {
            var minX = nodePositions.Min(p => p.x);
            var maxX = nodePositions.Max(p => p.x);
            var minY = nodePositions.Min(p => p.y);
            var maxY = nodePositions.Max(p => p.y);
            
            Debug.Log($"节点X范围: {minX:F2} 到 {maxX:F2}");
            Debug.Log($"节点Y范围: {minY:F2} 到 {maxY:F2}");
            Debug.Log($"节点分布范围: {maxX - minX:F2} x {maxY - minY:F2}");
            Debug.Log($"边界范围: {bounds.size.x:F2} x {bounds.size.y:F2}");
        }
    }

    [ContextMenu("删除场景内所有LineRenderer")]
    public void DeleteAllLineRenderers()
    {
        Debug.Log("🧹 开始删除场景内所有LineRenderer...");
        
        // 查找场景内所有的LineRenderer组件
        var allLineRenderers = FindObjectsByType<LineRenderer>(FindObjectsSortMode.None);
        
        if (allLineRenderers.Length == 0)
        {
            Debug.Log("✅ 场景内没有找到LineRenderer");
            return;
        }
        
        Debug.Log($"找到 {allLineRenderers.Length} 个LineRenderer");
        
        int deletedCount = 0;
        foreach (var lineRenderer in allLineRenderers)
        {
            if (lineRenderer != null)
            {
                Debug.Log($"删除LineRenderer: {lineRenderer.name}");
                DestroyImmediate(lineRenderer.gameObject);
                deletedCount++;
            }
        }
        
        Debug.Log($"✅ 删除完成！共删除了 {deletedCount} 个LineRenderer");
        
        // 清理本地的边线缓存
        edgeLines.Clear();
        generatedEdges.Clear();
        Debug.Log("🗑️ 已清理本地边线缓存");
    }

    [ContextMenu("检查权重标签预制件状态")]
    public void CheckWeightLabelPrefabStatus()
    {
        Debug.Log("🔍 检查权重标签预制件状态...");
        
        if (weightLabelPrefab == null)
        {
            Debug.LogWarning("⚠️ 权重标签预制件未设置");
            return;
        }
        
        Debug.Log($"✅ 权重标签预制件已设置: {weightLabelPrefab.name}");
        
        // 检查预制件中的TextMeshPro组件
        TextMeshPro textMeshPro = weightLabelPrefab.GetComponent<TextMeshPro>();
        if (textMeshPro == null)
        {
            textMeshPro = weightLabelPrefab.GetComponentInChildren<TextMeshPro>();
        }
        
        if (textMeshPro != null)
        {
            Debug.Log($"✅ 找到TextMeshPro组件: {textMeshPro.name}");
            Debug.Log($"   字体大小: {textMeshPro.fontSize}");
            Debug.Log($"   颜色: {textMeshPro.color}");
        }
        else
        {
            Debug.LogWarning("⚠️ 未找到TextMeshPro组件");
        }
        
        // 检查预制件中的TextMesh组件
        TextMesh textMesh = weightLabelPrefab.GetComponent<TextMesh>();
        if (textMesh == null)
        {
            textMesh = weightLabelPrefab.GetComponentInChildren<TextMesh>();
        }
        
        if (textMesh != null)
        {
            Debug.Log($"✅ 找到TextMesh组件: {textMesh.name}");
            Debug.Log($"   字体大小: {textMesh.fontSize}");
            Debug.Log($"   字符大小: {textMesh.characterSize}");
        }
        else
        {
            Debug.LogWarning("⚠️ 未找到TextMesh组件");
        }
        
        if (textMeshPro == null && textMesh == null)
        {
            Debug.LogError("❌ 权重标签预制件中没有找到任何文本组件！");
        }
    }

    [ContextMenu("删除场景内所有权重标签")]
    public void DeleteAllWeightLabels()
    {
        Debug.Log("🧹 开始删除场景内所有权重标签...");
        
        // 查找场景内所有的TextMesh组件
        var allTextMeshes = FindObjectsByType<TextMesh>(FindObjectsSortMode.None);
        
        // 查找场景内所有的TextMeshPro组件
        var allTextMeshPros = FindObjectsByType<TextMeshPro>(FindObjectsSortMode.None);
        
        int totalFound = allTextMeshes.Length + allTextMeshPros.Length;
        
        if (totalFound == 0)
        {
            Debug.Log("✅ 场景内没有找到权重标签");
            return;
        }
        
        Debug.Log($"找到 {totalFound} 个权重标签 (TextMesh: {allTextMeshes.Length}, TextMeshPro: {allTextMeshPros.Length})");
        
        int deletedCount = 0;
        
        // 删除TextMesh权重标签
        foreach (var textMesh in allTextMeshes)
        {
            if (textMesh != null && textMesh.name.StartsWith("EdgeWeightText_"))
            {
                Debug.Log($"删除TextMesh权重标签: {textMesh.name}");
                DestroyImmediate(textMesh.gameObject);
                deletedCount++;
            }
        }
        
        // 删除TextMeshPro权重标签
        foreach (var textMeshPro in allTextMeshPros)
        {
            if (textMeshPro != null && textMeshPro.name.StartsWith("EdgeWeightText_"))
            {
                Debug.Log($"删除TextMeshPro权重标签: {textMeshPro.name}");
                DestroyImmediate(textMeshPro.gameObject);
                deletedCount++;
            }
        }
        
        Debug.Log($"✅ 删除完成！共删除了 {deletedCount} 个权重标签");
    }

    [ContextMenu("清空所有节点和边")]
    public void ClearAllNodesAndEdges()
    {
        Debug.Log("🧹 开始清空所有节点和边...");
        
        // 清空节点
        int cellCount = 0;
        foreach (var cell in generatedCells)
        {
            if (cell != null)
            {
                DestroyImmediate(cell.gameObject);
                cellCount++;
            }
        }
        generatedCells.Clear();
        
        // 清空边
        int edgeCount = 0;
        foreach (var line in edgeLines.Values)
        {
            if (line != null)
            {
                DestroyImmediate(line.gameObject);
                edgeCount++;
            }
        }
        edgeLines.Clear();
        generatedEdges.Clear();
        
        // 清空权重标签
        var allTextMeshes = FindObjectsByType<TextMesh>(FindObjectsSortMode.None);
        var allTextMeshPros = FindObjectsByType<TextMeshPro>(FindObjectsSortMode.None);
        int labelCount = 0;
        
        // 删除TextMesh权重标签
        foreach (var textMesh in allTextMeshes)
        {
            if (textMesh != null && textMesh.name.StartsWith("EdgeWeightText_"))
            {
                DestroyImmediate(textMesh.gameObject);
                labelCount++;
            }
        }
        
        // 删除TextMeshPro权重标签
        foreach (var textMeshPro in allTextMeshPros)
        {
            if (textMeshPro != null && textMeshPro.name.StartsWith("EdgeWeightText_"))
            {
                DestroyImmediate(textMeshPro.gameObject);
                labelCount++;
            }
        }
        
        // 清空权重缓存
        _edgeWeightCache.Clear();
        
        // 重新创建根对象
        if (linesRoot != null)
            DestroyImmediate(linesRoot);
        
        if (cellsRoot != null)
            DestroyImmediate(cellsRoot);
        
        linesRoot = new GameObject("TilemapLinesRoot");
        linesRoot.hideFlags = HideFlags.DontSave;
        
        cellsRoot = new GameObject("TilemapCellsRoot");
        cellsRoot.hideFlags = HideFlags.DontSave;
        
        Debug.Log($"✅ 清空完成！");
        Debug.Log($"🗑️ 清空了 {cellCount} 个节点");
        Debug.Log($"🗑️ 清空了 {edgeCount} 条边");
        Debug.Log($"🗑️ 清空了 {edgeCount} 条线条");
        Debug.Log($"🗑️ 清空了 {labelCount} 个权重标签");
    }



    [ContextMenu("调整渲染顺序")]
    public void AdjustRenderingOrder()
    {
        Debug.Log("🎨 调整渲染顺序...");
        
        // 调整所有edges的渲染顺序
        if (edgeLines != null)
        {
            foreach (var kvp in edgeLines)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.sortingOrder = 5; // 设置较低的排序顺序
                    kvp.Value.sortingLayerName = "Default";
                }
            }
            Debug.Log($"✅ 调整了 {edgeLines.Count} 个edges的渲染顺序");
        }
        
        // 调整所有cells的渲染顺序
        if (generatedCells != null)
        {
            foreach (var cell in generatedCells)
            {
                if (cell != null)
                {
                    // 调用Cell的渲染顺序调整方法，确保TMP文本显示在背景之上
                    cell.AdjustRenderingOrder();
                }
            }
            Debug.Log($"✅ 调整了 {generatedCells.Count} 个cells的渲染顺序");
        }
        
        // 调整所有权重标签的渲染顺序
        var weightLabels = linesRoot.GetComponentsInChildren<TextMesh>();
        foreach (var textMesh in weightLabels)
        {
            if (textMesh != null)
            {
                textMesh.GetComponent<MeshRenderer>().sortingOrder = 25;
                textMesh.GetComponent<MeshRenderer>().sortingLayerName = "Default";
            }
        }
        
        var weightLabelsPro = linesRoot.GetComponentsInChildren<TextMeshPro>();
        foreach (var textMeshPro in weightLabelsPro)
        {
            if (textMeshPro != null)
            {
                textMeshPro.sortingOrder = 25;
                Renderer renderer = textMeshPro.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sortingLayerName = "Default";
                }
            }
        }
        
        // 处理使用Cell脚本的权重标签
        var weightLabelCells = linesRoot.GetComponentsInChildren<Cell>();
        foreach (var cell in weightLabelCells)
        {
            if (cell != null && cell.gameObject.name.Contains("EdgeWeight"))
            {
                cell.AdjustRenderingOrder();
            }
        }
        
        Debug.Log($"✅ 调整了 {weightLabels.Length + weightLabelsPro.Length + weightLabelCells.Length} 个权重标签的渲染顺序");
        Debug.Log("🎨 渲染顺序调整完成：Edges(5) < Cell背景(15) < Weights(20/25) < Cell文本(35/40)");
    }

    void OnDestroy()
    {
        ClearGeneratedContent();
    }
} 