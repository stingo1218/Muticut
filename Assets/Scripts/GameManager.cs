using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using TMPro;
using UnityEngine.UI;
using Gurobi;
using UnityEngine.EventSystems;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    [SerializeField] private Cell _cellPrefab; // 单元格预制体

    [HideInInspector] public bool hasgameFinished;

    [SerializeField] private int _cellNumbers = 10; // 要生成的单元格数量

    private List<Cell> _cells = new List<Cell>(); // 改用List存储单元格，更灵活

    private Cell startCell;
    private LineRenderer previewEdge;

    [SerializeField] private Material previewEdgeMaterial; // 预览线材质
    [SerializeField] private Material _lineMaterial; // 用于连线的材质
    [SerializeField] private Material _eraseLineMaterial; // 用于擦除线的材质
    [SerializeField] private GameObject bgPrefab; // 用于权重背景的BG prefab
    private Dictionary<(Cell, Cell), (LineRenderer renderer, int weight, TextMeshPro tmp, GameObject bg)> _edges = new Dictionary<(Cell, Cell), (LineRenderer, int, TextMeshPro, GameObject)>(); // 存储所有的连线
    private Transform linesRoot; // 用于组织所有连线的父物体

    private bool isErasing = false;
    private LineRenderer eraseLineRenderer; // 用于显示擦除线

    private List<Vector2> erasePath = new List<Vector2>();

    private const float EPSILON = 1e-6f; // 用于浮点数比较

    [SerializeField]
    private bool useWeightedEdges = false; // 这个就是一个开关

    // 唯一权重缓存
    private Dictionary<(Cell, Cell), int> _edgeWeightCache = new Dictionary<(Cell, Cell), int>();
    [SerializeField] private int minEdgeWeight = 1;
    [SerializeField] private int maxEdgeWeight = 10;

    private Button debugButton;

    private HashSet<(Cell, Cell)> _initialEdges = new HashSet<(Cell, Cell)>(); // 记录初始边

    public enum MulticutAlgorithm
    {
        Greedy,
        ILP
    }

    [SerializeField]
    private MulticutAlgorithm multicutAlgorithm = MulticutAlgorithm.Greedy;

    // Delaunay Triangulation Structures
    private struct DelaunayEdge
    {
        public int P1Index, P2Index; // Indices into the original points list

        public DelaunayEdge(int p1Index, int p2Index)
        {
            // Ensure P1Index < P2Index for consistent hashing/equality
            if (p1Index < p2Index)
            {
                P1Index = p1Index;
                P2Index = p2Index;
            }
            else
            {
                P1Index = p2Index;
                P2Index = p1Index;
            }
        }

        public override bool Equals(object obj)
        {
            if (!(obj is DelaunayEdge)) return false;
            DelaunayEdge other = (DelaunayEdge)obj;
            return P1Index == other.P1Index && P2Index == other.P2Index;
        }

        public override int GetHashCode()
        {
            return P1Index.GetHashCode() ^ (P2Index.GetHashCode() << 2);
        }
    }

    private struct DelaunayTriangle
    {
        public Vector2 V1, V2, V3; // Actual coordinates
        public int Index1, Index2, Index3; // Indices in the original points list

        public Vector2 Circumcenter;
        public float CircumradiusSq;

        public DelaunayTriangle(Vector2 v1, Vector2 v2, Vector2 v3, int idx1, int idx2, int idx3)
        {
            V1 = v1; V2 = v2; V3 = v3;
            Index1 = idx1; Index2 = idx2; Index3 = idx3;

            // Calculate circumcircle
            // Using the formula from Wikipedia: https://en.wikipedia.org/wiki/Circumscribed_circle#Cartesian_coordinates_2
            float D = 2 * (V1.x * (V2.y - V3.y) + V2.x * (V3.y - V1.y) + V3.x * (V1.y - V2.y));

            if (Mathf.Abs(D) < EPSILON) // Collinear or very small triangle
            {
                Circumcenter = Vector2.positiveInfinity; // Invalid
                CircumradiusSq = float.PositiveInfinity;
                return;
            }

            float v1Sq = V1.x * V1.x + V1.y * V1.y;
            float v2Sq = V2.x * V2.x + V2.y * V2.y;
            float v3Sq = V3.x * V3.x + V3.y * V3.y;

            Circumcenter = new Vector2(
                (v1Sq * (V2.y - V3.y) + v2Sq * (V3.y - V1.y) + v3Sq * (V1.y - V2.y)) / D,
                (v1Sq * (V3.x - V2.x) + v2Sq * (V1.x - V3.x) + v3Sq * (V2.x - V1.x)) / D
            );

            CircumradiusSq = (V1 - Circumcenter).sqrMagnitude;
        }

        public bool ContainsVertex(Vector2 v, float tolerance = EPSILON)
        {
            return (V1 - v).sqrMagnitude < tolerance ||
                   (V2 - v).sqrMagnitude < tolerance ||
                   (V3 - v).sqrMagnitude < tolerance;
        }

        public bool IsPointInCircumcircle(Vector2 point)
        {
            if (float.IsInfinity(CircumradiusSq)) return false; // Invalid triangle
            return (point - Circumcenter).sqrMagnitude < CircumradiusSq;
        }
    }

    [SerializeField] private Material highlightEdgeMaterial;

    [SerializeField] private UnityEngine.UI.Toggle pixelHintTogglePrefab; // Inspector拖引用的PixelHintToggle预制体

    private void Awake()
    {
        Instance = this;
        linesRoot = new GameObject("LinesRoot").transform;
        linesRoot.SetParent(transform);
        SpawnLevel(_cellNumbers);
    }

    private void Start()
    {
        // CreateDebugButton(); // 移除左边HINT按钮
        CreatePixelHintButton();
    }

    private void CreatePixelHintButton()
    {
        // 查找UICanvas
        GameObject canvasObj = GameObject.Find("UICanvas");
        if (canvasObj == null)
        {
            UnityEngine.Debug.LogError("UICanvas未找到，无法创建像素Hint按钮");
            return;
        }
        // 使用Inspector拖引用的Toggle预制体
        if (pixelHintTogglePrefab == null)
        {
            UnityEngine.Debug.LogError("pixelHintTogglePrefab未在Inspector中赋值，请拖入Toggle预制体");
            return;
        }
        // 实例化Toggle
        var toggle = Instantiate(pixelHintTogglePrefab, canvasObj.transform);
        toggle.name = "PixelHintToggle";
        // 设置位置和大小
        RectTransform rect = toggle.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(0, 0);
        rect.pivot = new Vector2(0, 0);
        rect.anchoredPosition = new Vector2(160, 20);
        // rect.sizeDelta = new Vector2(120, 40);
        // 设置TMP文字
        var tmp = toggle.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = "HINT";
        }
        // 监听Toggle状态变化
        toggle.onValueChanged.RemoveAllListeners();
        toggle.onValueChanged.AddListener((isOn) => {
            if (isOn)
            {
                // 1. 构造输入数据
                var nodes = _cells.Select(cell => cell.Number).ToList();
                var edgeList = new List<Dictionary<string, object>>();
                foreach (var edge in _edges.Keys)
                {
                    int u = edge.Item1.Number;
                    int v = edge.Item2.Number;
                    int w = _edgeWeightCache[edge];
                    edgeList.Add(new Dictionary<string, object> { {"u", u}, {"v", v}, {"weight", w} });
                }
                // JsonUtility不支持复杂嵌套，这里手动拼json字符串
                string nodesStr = string.Join(",", nodes);
                string edgesStr = string.Join(",", edgeList.Select(e => $"{{\"u\":{e["u"]},\"v\":{e["v"]},\"weight\":{e["weight"]}}}"));
                string jsonData = $"{{\"nodes\":[{nodesStr}],\"edges\":[{edgesStr}]}}";

                // 2. 路径
                string pythonExe = "python";
                string scriptPath = "Assets/Scripts/multicut_solver.py";
                string inputPath = "input.json";
                string outputPath = "output.json";

                // 3. 调用Python
                RunPythonMulticut(pythonExe, scriptPath, inputPath, outputPath, jsonData);

                // 4. 读取结果
                string resultJson = System.IO.File.ReadAllText(outputPath);
                var cutEdges = new List<(Cell, Cell)>();
                try
                {
                    // 使用正则提取所有 {"u": x, "v": y}
                    var matches = Regex.Matches(resultJson, @"\{\s*""u""\s*:\s*(\d+)\s*,\s*""v""\s*:\s*(\d+)\s*\}");
                    foreach (Match match in matches)
                    {
                        int u = int.Parse(match.Groups[1].Value);
                        int v = int.Parse(match.Groups[2].Value);
                        var cellU = _cells.FirstOrDefault(c => c.Number == u);
                        var cellV = _cells.FirstOrDefault(c => c.Number == v);
                        if (cellU != null && cellV != null)
                            cutEdges.Add(GetCanonicalEdgeKey(cellU, cellV));
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError("解析Python输出失败: " + ex.Message);
                }
                HighlightCutEdges(cutEdges);
                UnityEngine.Debug.Log("Hint: Python多割已高亮最佳切割");
            }
            else
            {
                // 关闭时取消高亮（可选：这里简单重置所有边材质）
                foreach (var edgeInfo in _edges.Values)
                {
                    if (_lineMaterial != null)
                        edgeInfo.renderer.material = _lineMaterial;
                }
                UnityEngine.Debug.Log("像素Hint Toggle状态: 关闭");
            }
        });
    }

    public void LoadLevelAndSpawnNodes(int numberOfCells)
    {
        _cellNumbers = numberOfCells;
        SpawnLevel(numberOfCells);
    }

    private List<Vector2> GenerateCellPositions(int numberOfPoints)
    {
        List<Vector2> cellPositions = new List<Vector2>();
        
        // 获取相机视野范围
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            UnityEngine.Debug.LogError("Main Camera not found!");
            return cellPositions;
        }

        float cameraHeight = 2f * mainCamera.orthographicSize;
        float cameraWidth = cameraHeight * mainCamera.aspect;

        // 缩小范围到 80% 的区域
        float minX = mainCamera.transform.position.x - cameraWidth * 0.4f;
        float maxX = mainCamera.transform.position.x + cameraWidth * 0.4f;
        float minY = mainCamera.transform.position.y - cameraHeight * 0.4f;
        float maxY = mainCamera.transform.position.y + cameraHeight * 0.4f;

        float minDistance = 1.2f; // 最小间距
        float cellSize = minDistance / Mathf.Sqrt(2); // 网格大小

        // 创建网格
        int cols = Mathf.CeilToInt((maxX - minX) / cellSize);
        int rows = Mathf.CeilToInt((maxY - minY) / cellSize);
        int?[,] grid = new int?[cols, rows];

        // 活动点列表
        List<Vector2> activePoints = new List<Vector2>();

        // 添加第一个点
        Vector2 firstPoint = new Vector2(
            UnityEngine.Random.Range(minX, maxX),
            UnityEngine.Random.Range(minY, maxY)
        );
        cellPositions.Add(firstPoint);
        activePoints.Add(firstPoint);

        // 将点添加到网格
        int gridX = Mathf.FloorToInt((firstPoint.x - minX) / cellSize);
        int gridY = Mathf.FloorToInt((firstPoint.y - minY) / cellSize);
        grid[gridX, gridY] = cellPositions.Count - 1;

        while (activePoints.Count > 0 && cellPositions.Count < numberOfPoints)
        {
            // 随机选择一个活动点
            int activeIndex = UnityEngine.Random.Range(0, activePoints.Count);
            Vector2 point = activePoints[activeIndex];

            bool foundValidPoint = false;

            // 尝试在活动点周围生成新点
            for (int i = 0; i < 30; i++) // 每个点尝试30次
            {
                float angle = UnityEngine.Random.Range(0, 2 * Mathf.PI);
                float distance = UnityEngine.Random.Range(minDistance, 2 * minDistance);
                Vector2 newPoint = point + new Vector2(
                    Mathf.Cos(angle) * distance,
                    Mathf.Sin(angle) * distance
                );

                // 检查新点是否在有效范围内
                if (newPoint.x < minX || newPoint.x > maxX || 
                    newPoint.y < minY || newPoint.y > maxY)
                    continue;

                // 检查新点是否与现有点距离足够
                int newGridX = Mathf.FloorToInt((newPoint.x - minX) / cellSize);
                int newGridY = Mathf.FloorToInt((newPoint.y - minY) / cellSize);

                bool isValid = true;

                // 检查周围网格
                for (int x = -2; x <= 2; x++)
                {
                    for (int y = -2; y <= 2; y++)
                    {
                        int checkX = newGridX + x;
                        int checkY = newGridY + y;

                        if (checkX >= 0 && checkX < cols && checkY >= 0 && checkY < rows)
                        {
                            int? pointIndex = grid[checkX, checkY];
                            if (pointIndex.HasValue)
                            {
                                Vector2 existingPoint = cellPositions[pointIndex.Value];
                                if (Vector2.Distance(newPoint, existingPoint) < minDistance)
                                {
                                    isValid = false;
                                    break;
                                }
                            }
                        }
                    }
                    if (!isValid) break;
                }

                if (isValid)
                {
                    cellPositions.Add(newPoint);
                    activePoints.Add(newPoint);
                    grid[newGridX, newGridY] = cellPositions.Count - 1;
                    foundValidPoint = true;
                    break;
                }
            }

            if (!foundValidPoint)
            {
                activePoints.RemoveAt(activeIndex);
            }
        }

        UnityEngine.Debug.Log($"Generated {cellPositions.Count} points using Poisson Disk Sampling");
        return cellPositions;
    }

    void StretchAndCenterCells(List<Cell> cells)
    {
        // 1. 计算包围盒
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var cell in cells)
        {
            Vector2 pos = cell.transform.position;
            if (pos.x < minX) minX = pos.x;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.y < minY) minY = pos.y;
            if (pos.y > maxY) maxY = pos.y;
        }

        // 2. 计算目标区域
        Camera cam = Camera.main;
        float camHeight = cam.orthographicSize * 2f * 0.8f;
        float camWidth = camHeight * cam.aspect;

        // 3. 计算缩放比例（分别计算水平和垂直）
        float width = Mathf.Max(maxX - minX, 0.01f);
        float height = Mathf.Max(maxY - minY, 0.01f);
        float scaleX = camWidth / width;
        float scaleY = camHeight / height;

        // 4. 以中心为基准，拉伸并居中
        Vector2 center = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);
        Vector2 screenCenter = cam.transform.position;
        foreach (var cell in cells)
        {
            Vector2 pos = cell.transform.position;
            // 先平移到原中心，再分别缩放，再平移到屏幕中心
            Vector2 newPos = new Vector2(
                (pos.x - center.x) * scaleX,
                (pos.y - center.y) * scaleY
            ) + screenCenter;
            cell.transform.position = new Vector3(newPos.x, newPos.y, cell.transform.position.z);
        }
    }

    private void SpawnLevel(int numberOfPoints)
    {
        // 清理之前的关卡
        foreach (var cell in _cells)
        {
            if (cell != null)
                Destroy(cell.gameObject);
        }
        _cells.Clear();
        RemoveAllEdges();
        _initialEdges.Clear(); // 清空初始边集合

        List<Vector2> cellPositions = GenerateCellPositions(numberOfPoints);
        // Assign positions to cells and collect Vector2 for triangulation
        List<Vector2> pointsForTriangulation = new List<Vector2>();

        for (int i = 0; i < cellPositions.Count; i++)
        {
            Vector2 position = cellPositions[i];

            Cell newCell = Instantiate(_cellPrefab, position, Quaternion.identity, transform);
            newCell.Number = i + 1; // Cell.Number is 1-indexed for display/logic
            newCell.Init(i + 1);
            newCell.gameObject.name = $"Cell {newCell.Number}";
            _cells.Add(newCell);
            pointsForTriangulation.Add(position);
        }

        // 先归一化/缩放/居中所有Cell
        StretchAndCenterCells(_cells);

        // 归一化后重新收集点坐标用于三角剖分
        pointsForTriangulation.Clear();
        foreach (var cell in _cells)
        {
            pointsForTriangulation.Add(cell.transform.position);
        }

        // Generate Delaunay Triangulation
        if (_cells.Count >= 3) // Need at least 3 points for triangulation
        {
            List<DelaunayTriangle> _;
            List<DelaunayEdge> delaunayEdges = PerformDelaunayTriangulationWithRefinement(pointsForTriangulation, 0.2f, 10, out _);
            foreach (var edge in delaunayEdges)
            {
                // 只处理原始点集对应的边，过滤掉包含细分插入点的边
                if (edge.P1Index < _cells.Count && edge.P2Index < _cells.Count)
                {
                    CreateOrUpdateEdge(_cells[edge.P1Index], _cells[edge.P2Index]);
                    // 记录初始边（规范化key）
                    var key = GetCanonicalEdgeKey(_cells[edge.P1Index], _cells[edge.P2Index]);
                    _initialEdges.Add(key);
                }
            }
        }
        else if (_cells.Count == 2) // If only two points, connect them directly
        {
            CreateOrUpdateEdge(_cells[0], _cells[1]);
            var key = GetCanonicalEdgeKey(_cells[0], _cells[1]);
            _initialEdges.Add(key);
        }
        // If 0 or 1 cell, do nothing

        // 生成图后不再自动调用多割算法
    }

    private List<Vector2> GetSuperTriangleVertices(List<Vector2> points)
    {
        float minX = points[0].x, minY = points[0].y, maxX = points[0].x, maxY = points[0].y;
        foreach (var p in points)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }

        float dx = maxX - minX;
        float dy = maxY - minY;
        float deltaMax = Mathf.Max(dx, dy) * 2; // Increased multiplier for safety

        // Center of the bounding box
        float centerX = minX + dx * 0.5f;
        float centerY = minY + dy * 0.5f;

        // Vertices of the super triangle
        // These need to be far enough to surely encompass all points and their circumcircles
        Vector2 p1 = new Vector2(centerX - 20 * deltaMax, centerY - deltaMax);
        Vector2 p2 = new Vector2(centerX + 20 * deltaMax, centerY - deltaMax);
        Vector2 p3 = new Vector2(centerX, centerY + 20 * deltaMax);
        
        return new List<Vector2> { p1, p2, p3 };
    }

    // Delaunay Refinement（细分法）集成，带out参数重载
    private List<DelaunayEdge> PerformDelaunayTriangulationWithRefinement(List<Vector2> points, float minHeightToEdgeRatio, int maxRefineIters, out List<DelaunayTriangle> trianglesOut)
    {
        List<Vector2> refinedPoints = new List<Vector2>(points);
        int iter = 0;
        List<DelaunayTriangle> triangles = null;
        while (iter < maxRefineIters)
        {
            iter++;
            List<DelaunayEdge> edges = PerformDelaunayTriangulation(refinedPoints, out triangles);
            bool hasBadTriangle = false;
            Vector2? insertPoint = null;
            foreach (var tri in triangles)
            {
                float a = Vector2.Distance(tri.V1, tri.V2);
                float b = Vector2.Distance(tri.V2, tri.V3);
                float c = Vector2.Distance(tri.V3, tri.V1);
                float maxEdge = Mathf.Max(a, Mathf.Max(b, c));
                float s = (a + b + c) / 2f;
                float area = Mathf.Sqrt(Mathf.Max(s * (s - a) * (s - b) * (s - c), 0f));
                float ha = 2 * area / a;
                float hb = 2 * area / b;
                float hc = 2 * area / c;
                float minHeight = Mathf.Min(ha, Mathf.Min(hb, hc));
                if (maxEdge < 1e-6f) continue;
                float ratio = minHeight / maxEdge;
                if (ratio < minHeightToEdgeRatio)
                {
                    insertPoint = tri.Circumcenter;
                    hasBadTriangle = true;
                    break;
                }
            }
            if (!hasBadTriangle || !insertPoint.HasValue)
                break;
            refinedPoints.Add(insertPoint.Value);
        }
        trianglesOut = triangles ?? new List<DelaunayTriangle>();
        return PerformDelaunayTriangulation(refinedPoints, out trianglesOut);
    }

    // 保留无out参数的简化重载
    private List<DelaunayEdge> PerformDelaunayTriangulationWithRefinement(List<Vector2> points, float minHeightToEdgeRatio = 0.2f, int maxRefineIters = 10)
    {
        List<DelaunayTriangle> _;
        return PerformDelaunayTriangulationWithRefinement(points, minHeightToEdgeRatio, maxRefineIters, out _);
    }

    // 重载：返回三角形列表
    private List<DelaunayEdge> PerformDelaunayTriangulation(List<Vector2> points, out List<DelaunayTriangle> trianglesOut)
    {
        if (points == null || points.Count < 3)
        {
            trianglesOut = new List<DelaunayTriangle>();
            return new List<DelaunayEdge>();
        }
        List<DelaunayTriangle> triangles = new List<DelaunayTriangle>();
        // 1. Create a "super triangle" that encloses all input points
        List<Vector2> superTriangleVertices = GetSuperTriangleVertices(points);
        var st = new DelaunayTriangle(superTriangleVertices[0], superTriangleVertices[1], superTriangleVertices[2], -1, -2, -3);
        triangles.Add(st);
        for (int pointIndex = 0; pointIndex < points.Count; pointIndex++)
        {
            Vector2 point = points[pointIndex];
            List<DelaunayTriangle> badTriangles = new List<DelaunayTriangle>();
            List<DelaunayEdge> polygonHole = new List<DelaunayEdge>();
            foreach (var triangle in triangles)
            {
                if (triangle.IsPointInCircumcircle(point))
                {
                    badTriangles.Add(triangle);
                }
            }
            foreach (var triangle in badTriangles)
            {
                DelaunayEdge[] edges = {
                    new DelaunayEdge(triangle.Index1, triangle.Index2),
                    new DelaunayEdge(triangle.Index2, triangle.Index3),
                    new DelaunayEdge(triangle.Index3, triangle.Index1)
                };
                Vector2[] triVertices = {triangle.V1, triangle.V2, triangle.V3};
                int[] triIndices = {triangle.Index1, triangle.Index2, triangle.Index3};
                for(int i=0; i<3; ++i)
                {
                    DelaunayEdge edge = new DelaunayEdge(triIndices[i], triIndices[(i+1)%3]);
                    Vector2 v_current = triVertices[i];
                    Vector2 v_next = triVertices[(i+1)%3];
                    bool isShared = false;
                    foreach (var otherBadTriangle in badTriangles)
                    {
                        if (triangle.Equals(otherBadTriangle)) continue;
                        if (otherBadTriangle.ContainsVertex(v_current) && otherBadTriangle.ContainsVertex(v_next))
                        {
                            isShared = true;
                            break;
                        }
                    }
                    if (!isShared)
                    {
                        polygonHole.Add(new DelaunayEdge(triIndices[i], triIndices[(i+1)%3]));
                    }
                }
            }
            triangles.RemoveAll(t => badTriangles.Contains(t));
            foreach (var edge in polygonHole)
            {
                Vector2 p1 = (edge.P1Index < 0) ? superTriangleVertices[-edge.P1Index -1] : points[edge.P1Index];
                Vector2 p2 = (edge.P2Index < 0) ? superTriangleVertices[-edge.P2Index -1] : points[edge.P2Index];
                triangles.Add(new DelaunayTriangle(point, p1, p2, pointIndex, edge.P1Index, edge.P2Index));
            }
        }
        triangles.RemoveAll(triangle =>
            triangle.Index1 < 0 || triangle.Index2 < 0 || triangle.Index3 < 0 ||
            triangle.ContainsVertex(superTriangleVertices[0]) ||
            triangle.ContainsVertex(superTriangleVertices[1]) ||
            triangle.ContainsVertex(superTriangleVertices[2])
        );
        HashSet<DelaunayEdge> finalEdges = new HashSet<DelaunayEdge>();
        foreach (var triangle in triangles)
        {
            if (triangle.Index1 >= 0 && triangle.Index2 >= 0) finalEdges.Add(new DelaunayEdge(triangle.Index1, triangle.Index2));
            if (triangle.Index2 >= 0 && triangle.Index3 >= 0) finalEdges.Add(new DelaunayEdge(triangle.Index2, triangle.Index3));
            if (triangle.Index3 >= 0 && triangle.Index1 >= 0) finalEdges.Add(new DelaunayEdge(triangle.Index3, triangle.Index1));
        }
        trianglesOut = triangles;
        return new List<DelaunayEdge>(finalEdges);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (!HandleCellClick()) // 只有没点中cell时才检测edge
            {
                HandleEdgeClick();
            }
        }
        else if (Input.GetMouseButton(0) && startCell != null)
        {
            HandlePreviewDrag();
        }
        else if (Input.GetMouseButtonUp(0) && startCell != null)
        {
            HandleMouseUp();
        }

        // 按下右键，开始擦除
        if (Input.GetMouseButtonDown(1))
        {
            erasePath.Clear();
            Vector2 startPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            erasePath.Add(startPos);
            ShowEraseLine(startPos);
            isErasing = true;
        }
        // 拖动右键，持续记录轨迹
        else if (Input.GetMouseButton(1) && isErasing)
        {
            Vector2 point = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            // 只在鼠标移动一定距离时才添加新点，避免过多点
            if (erasePath.Count == 0 || Vector2.Distance(erasePath[erasePath.Count - 1], point) > 0.05f)
            {
                erasePath.Add(point);
                UpdateEraseLinePath(erasePath);
            }
        }
        // 松开右键，检测并删除被轨迹划过的edge
        else if (Input.GetMouseButtonUp(1) && isErasing)
        {
            HideEraseLine();
            EraseEdgesCrossedByPath(erasePath);
            isErasing = false;
        }
    }

    private bool HandleCellClick()
    {
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 mousePos2D = new Vector2(mouseWorld.x, mouseWorld.y);

        int cellLayer = LayerMask.GetMask("Cell");
        RaycastHit2D hitCell = Physics2D.Raycast(mousePos2D, Vector2.zero, 0, cellLayer);
        if (hitCell.collider != null)
        {
            UnityEngine.Debug.Log("Raycast 命中 Cell: " + hitCell.collider.gameObject.name);
            var cell = hitCell.collider.GetComponent<Cell>();
            if (cell != null)
            {
                startCell = cell;
                ShowPreviewLine(cell.transform.position);
                return true; // 命中cell
            }
        }
        else
        {
            UnityEngine.Debug.Log("Raycast 未命中 Cell");
        }
        return false; // 没命中cell
    }

    private void HandleEdgeClick()
    {
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 mousePos2D = new Vector2(mouseWorld.x, mouseWorld.y);

        int edgeLayer = LayerMask.GetMask("Edge");
        RaycastHit2D hitEdge = Physics2D.Raycast(mousePos2D, Vector2.zero, 0, edgeLayer);
        if (hitEdge.collider != null && hitEdge.collider.gameObject.name.StartsWith("Line_"))
        {
            UnityEngine.Debug.Log("点击到连线，准备删除: " + hitEdge.collider.gameObject.name);
            var toRemoveKey = _edges.FirstOrDefault(pair => pair.Value.renderer.gameObject == hitEdge.collider.gameObject).Key;
            
            if (!toRemoveKey.Equals(default((Cell, Cell))))
            {
                List<(Cell, Cell)> edgeAsList = new List<(Cell, Cell)> { toRemoveKey };

                int initialComponents = CalculateNumberOfConnectedComponents();
                int componentsAfterRemoval = CalculateNumberOfConnectedComponents(edgeAsList);

                if (componentsAfterRemoval > initialComponents)
                {
                    RemoveEdge(toRemoveKey.Item1, toRemoveKey.Item2);
                }
                else
                {
                    UnityEngine.Debug.Log("不能删除此边：删除后不会增加连通分量数量。");
                }
            }
        }
        else
        {
            UnityEngine.Debug.Log("Raycast 未命中 Line");
        }
    }

    private void HandlePreviewDrag()
    {
        UpdatePreviewLine(Camera.main.ScreenToWorldPoint(Input.mousePosition));
    }

    private void HandleMouseUp()
    {
        var endCell = RaycastCell();
        UnityEngine.Debug.Log("Mouse Up, Raycast Cell: " + (endCell != null ? endCell.Number.ToString() : "null"));
        if (endCell != null && endCell != startCell)
        {
            startCell.AddEdge(endCell);
        }
        HidePreviewLine();
        startCell = null;
    }

    private void CheckWin()
    {
        // TODO: Implement win condition check logic
    }

    private int GetDirectionIndex(Vector2Int offsetDirection)
    {
        // TODO: Implement logic to get direction index
        return 0;
    }

    private float GetOffset(Vector2 offset, Vector2Int offsetDirection)
    {
        // TODO: Implement logic to calculate offset
        return 0f;
    }

    private float GetUniversalOffset(Vector2 offset)
    {
        // TODO: Implement logic to calculate universal offset
        return 0f;
    }

    private Vector2Int GetDirection(Vector2 offset)
    {
        // TODO: Implement logic to determine direction
        return Vector2Int.zero;
    }

    private Vector2 GetUniversalDirection(Vector2 offset)
    {
        // TODO: Implement logic to calculate universal direction
        return Vector2.zero;
    }

    public Cell GetAdjacentCell(int row, int col, int direction)
    {
        // TODO: Implement logic to get adjacent cell
        return null;
    }

    public bool Isvalid(Vector2Int pos)
    {
        // TODO: Implement logic to check if position is valid
        return false;
    }

    Cell RaycastCell()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        int cellLayer = LayerMask.GetMask("Cell"); // 只检测Cell层
        RaycastHit2D hit = Physics2D.Raycast(ray.origin, new Vector2(ray.direction.x, ray.direction.y), 100f, cellLayer);
        if (hit.collider != null)
        {
            UnityEngine.Debug.Log("Hit Collider: " + hit.collider.name);
            return hit.collider.GetComponent<Cell>();
        }
        return null;
    }

    void ShowPreviewLine(Vector3 startPosition)
    {
        if (previewEdge == null)
        {
            GameObject lineObj = new GameObject("PreviewLine");
            lineObj.layer = LayerMask.NameToLayer("PreviewEdge");
            previewEdge = lineObj.AddComponent<LineRenderer>();
            previewEdge.material = _lineMaterial;
            previewEdge.startWidth = 0.15f;
            previewEdge.endWidth = 0.15f;
            previewEdge.positionCount = 2;
            previewEdge.useWorldSpace = true;
            previewEdge.startColor = Color.black;
            previewEdge.endColor = Color.black;
        }
        previewEdge.SetPosition(0, startPosition);
        previewEdge.SetPosition(1, startPosition);
        previewEdge.enabled = true;
    }

    void UpdatePreviewLine(Vector3 endPosition)
    {
        if (previewEdge != null && previewEdge.enabled)
        {
            endPosition.z = 0; // 保证2D
            previewEdge.SetPosition(1, endPosition);
        }
    }

    void HidePreviewLine()
    {
        if (previewEdge != null)
        {
            previewEdge.enabled = false;
        }
    }

    public void CreateOrUpdateEdge(Cell fromCell, Cell toCell, int weight = 1)
    {
        var key = GetCanonicalEdgeKey(fromCell, toCell);

        // 新增：记录添加前的连通分量数量
        int before = CalculateNumberOfConnectedComponents();

        // 如果不开启权重边，强制权重为1
        if (!useWeightedEdges)
            weight = 1;

        if (_edges.ContainsKey(key))
        {
            var (renderer, _, tmp, bg) = _edges[key];
            renderer.SetPosition(0, fromCell.transform.position);
            renderer.SetPosition(1, toCell.transform.position);

            Vector3 midPoint = (fromCell.transform.position + toCell.transform.position) / 2f;

            // 权重数字和背景只在开启权重时显示
            if (useWeightedEdges)
            {
                tmp.gameObject.SetActive(true);
                bg.SetActive(true);
                tmp.transform.position = midPoint;
                bg.transform.position = midPoint;
                tmp.text = weight.ToString();
                Vector2 textSize = tmp.GetPreferredValues(tmp.text);
                float baseWidth = bg.GetComponent<SpriteRenderer>().size.x;
                float baseHeight = bg.GetComponent<SpriteRenderer>().size.y;
                bg.transform.localScale = new Vector3((textSize.x + 0.1f) / baseWidth, (textSize.y + 0.1f) / baseHeight, 1f);
            }
            else
            {
                tmp.gameObject.SetActive(false);
                bg.SetActive(false);
            }

            _edges[key] = (renderer, weight, tmp, bg);
            renderer.sortingOrder = 0;
            if (bg.TryGetComponent<SpriteRenderer>(out var bgRenderer))
                bgRenderer.sortingOrder = renderer.sortingOrder + 1;
            tmp.sortingOrder = bgRenderer.sortingOrder + 1;
        }
        else
        {
            GameObject lineObject = new GameObject($"Line_{fromCell.Number}_to_{toCell.Number}");
            lineObject.transform.SetParent(linesRoot);
            LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
            lineRenderer.material = _lineMaterial;
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;
            lineRenderer.SetPosition(0, fromCell.transform.position);
            lineRenderer.SetPosition(1, toCell.transform.position);

            EdgeCollider2D edgeCollider = lineObject.AddComponent<EdgeCollider2D>();
            Vector2[] points = new Vector2[2];
            points[0] = lineObject.transform.InverseTransformPoint(fromCell.transform.position);
            points[1] = lineObject.transform.InverseTransformPoint(toCell.transform.position);
            edgeCollider.points = points;
            edgeCollider.edgeRadius = 0.1f;
            edgeCollider.isTrigger = true;
            lineObject.layer = LayerMask.NameToLayer("Edge");

            // 创建TextMeshPro
            GameObject textObj = new GameObject("EdgeWeightText");
            textObj.transform.SetParent(lineObject.transform);
            TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
            tmp.fontSize = 2;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.text = weight.ToString();
            Vector3 midPoint = (fromCell.transform.position + toCell.transform.position) / 2f;
            textObj.transform.position = midPoint;
            textObj.transform.rotation = Quaternion.identity;

            // 实例化BG prefab作为背景
            GameObject bg = Instantiate(bgPrefab, lineObject.transform);
            bg.transform.position = midPoint;
            Vector2 textSize = tmp.GetPreferredValues(tmp.text);
            float baseWidth = bg.GetComponent<SpriteRenderer>().size.x;
            float baseHeight = bg.GetComponent<SpriteRenderer>().size.y;
            bg.transform.localScale = new Vector3((textSize.x + 0.1f) / baseWidth, (textSize.y + 0.1f) / baseHeight, 1f);

            // 根据开关决定是否显示权重
            tmp.gameObject.SetActive(useWeightedEdges);
            bg.SetActive(useWeightedEdges);

            _edges[key] = (lineRenderer, weight, tmp, bg);

            lineRenderer.sortingOrder = 0;
            if (bg.TryGetComponent<SpriteRenderer>(out var bgRenderer))
                bgRenderer.sortingOrder = lineRenderer.sortingOrder + 1;
            tmp.sortingOrder = bgRenderer.sortingOrder + 1;
        }

        // 新增：记录添加后的连通分量数量
        int after = CalculateNumberOfConnectedComponents();
        // 如果连通分量数量减少，说明有两个分量被合并
        if (after < before)
        {
            // 获取fromCell所在新分量的所有cell
            var allCells = GetAllCellsInSameComponent(fromCell);
            // 恢复初始状态下这些点之间的所有边
            foreach (var edge in _initialEdges)
            {
                if (allCells.Contains(edge.Item1) && allCells.Contains(edge.Item2))
                {
                    if (!_edges.ContainsKey(GetCanonicalEdgeKey(edge.Item1, edge.Item2)))
                    {
                        CreateOrUpdateEdge(edge.Item1, edge.Item2);
                    }
                }
            }
        }
    }

    public void CreateOrUpdateEdge(Cell fromCell, Cell toCell)
    {
        int weight = GetOrCreateEdgeWeight(fromCell, toCell);
        CreateOrUpdateEdge(fromCell, toCell, weight);
    }

    public void RemoveEdge(Cell fromCell, Cell toCell)
    {
        var key = GetCanonicalEdgeKey(fromCell, toCell);
        if (_edges.TryGetValue(key, out var edge))
        {
            var (renderer, _, tmp, bg) = edge;
            Destroy(renderer.gameObject); // 这会同时销毁所有子物体（包括文本和BG）
            _edges.Remove(key);
        }
    }

    public void RemoveAllEdges()
    {
        foreach (var edge in _edges.Values)
        {
            var (renderer, _, tmp, bg) = edge;
            Destroy(renderer.gameObject);
        }
        _edges.Clear();
    }

    public List<Cell> GetConnectedCells(Cell cell)
    {
        var connectedCells = new List<Cell>();
        foreach (var edge in _edges.Keys)
        {
            if (edge.Item1 == cell)
            {
                connectedCells.Add(edge.Item2);
            }
            else if (edge.Item2 == cell)
            {
                connectedCells.Add(edge.Item1);
            }
        }
        return connectedCells;
    }

    private void ShowEraseLine(Vector2 start)
    {
        if (eraseLineRenderer == null)
        {
            GameObject obj = new GameObject("EraseLine");
            eraseLineRenderer = obj.AddComponent<LineRenderer>();
            eraseLineRenderer.material = _eraseLineMaterial;
            eraseLineRenderer.startWidth = 0.2f;
            eraseLineRenderer.endWidth = 0.2f;
            eraseLineRenderer.useWorldSpace = true;
            eraseLineRenderer.textureMode = LineTextureMode.Tile; 
            eraseLineRenderer.sortingOrder = 10; 
        }
        eraseLineRenderer.positionCount = 1;
        eraseLineRenderer.SetPosition(0, start);
        eraseLineRenderer.enabled = true;
    }

    private void UpdateEraseLinePath(List<Vector2> path)
    {
        if (eraseLineRenderer == null) return;
        eraseLineRenderer.positionCount = path.Count;
        for (int i = 0; i < path.Count; i++)
        {
            eraseLineRenderer.SetPosition(i, path[i]);
        }
    }

    private void HideEraseLine()
    {
        if (eraseLineRenderer != null)
        {
            eraseLineRenderer.enabled = false;
        }
    }

    private void EraseEdgesCrossedByPath(List<Vector2> path)
    {
        if (path.Count < 2) return;

        List<(Cell, Cell)> edgesToRemove = new List<(Cell, Cell)>();
        foreach (var pair in _edges.ToList()) 
        {
            var lineRenderer = pair.Value.renderer;
            Vector2 edgeStart = lineRenderer.GetPosition(0);
            Vector2 edgeEnd = lineRenderer.GetPosition(1);

            for (int i = 0; i < path.Count - 1; i++)
            {
                if (LineSegmentsIntersect(path[i], path[i + 1], edgeStart, edgeEnd))
                {
                    edgesToRemove.Add(pair.Key);
                    break; 
                }
            }
        }

        if (edgesToRemove.Count == 0) return;

        UnityEngine.Debug.Log($"检测到{edgesToRemove.Count}条边被轨迹划过");

        int initialComponents = CalculateNumberOfConnectedComponents();
        int componentsAfterRemoval = CalculateNumberOfConnectedComponents(edgesToRemove);

        if (componentsAfterRemoval > initialComponents)
        {
            foreach (var edge in edgesToRemove)
            {
                RemoveEdge(edge.Item1, edge.Item2);
            }
        }
        else
        {
            UnityEngine.Debug.Log("不能擦除：此次操作不会增加连通分量数量。");
        }
    }

    // 计算当前图中（或忽略某些边后）的连通分量数量
    private int CalculateNumberOfConnectedComponents(List<(Cell, Cell)> ignoreEdges = null)
    {
        if (_cells.Count == 0) return 0;

        Dictionary<Cell, HashSet<Cell>> graph = new Dictionary<Cell, HashSet<Cell>>();
        foreach (var cell in _cells)
        {
            graph[cell] = new HashSet<Cell>();
        }

        foreach (var pair in _edges)
        {
            if (ignoreEdges != null && ignoreEdges.Contains(pair.Key))
            {
                continue;
            }
            graph[pair.Key.Item1].Add(pair.Key.Item2);
            graph[pair.Key.Item2].Add(pair.Key.Item1);
        }

        HashSet<Cell> visited = new HashSet<Cell>();
        int componentCount = 0;
        foreach (var cell in _cells)
        {
            if (!visited.Contains(cell))
            {
                componentCount++;
                Queue<Cell> queue = new Queue<Cell>();
                queue.Enqueue(cell);
                visited.Add(cell);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var neighbor in graph[current])
                    {
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }
        }
        return componentCount;
    }

    private bool LineSegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        Vector2 r = p2 - p1;
        Vector2 s = q2 - q1;
        float denominator = Cross(r, s);
        if (denominator == 0) return false; 
        float t = Cross(q1 - p1, s) / denominator;
        float u = Cross(q1 - p1, r) / denominator;
        return t >= 0 && t <= 1 && u >= 0 && u <= 1;
    }

    // 辅助方法：返回规范化的边key
    private (Cell, Cell) GetCanonicalEdgeKey(Cell cell1, Cell cell2)
    {
        return cell1.GetInstanceID() < cell2.GetInstanceID() ? (cell1, cell2) : (cell2, cell1);
    }

    // 获取或生成唯一权重
    private int GetOrCreateEdgeWeight(Cell a, Cell b)
    {
        var key = GetCanonicalEdgeKey(a, b);
        if (!_edgeWeightCache.TryGetValue(key, out int weight))
        {
            // 生成正数权重：表示边的"重要性"
            // 权重越大，表示边越重要，越不应该被切割
            // 权重越小，表示边越不重要，越容易被切割
            weight = UnityEngine.Random.Range((int)minEdgeWeight, (int)maxEdgeWeight + 1);
            _edgeWeightCache[key] = weight;
        }
        return weight;
    }

    // 获取与指定cell连通的所有cell
    private HashSet<Cell> GetAllCellsInSameComponent(Cell cell)
    {
        HashSet<Cell> visited = new HashSet<Cell>();
        Queue<Cell> queue = new Queue<Cell>();
        queue.Enqueue(cell);
        visited.Add(cell);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in GetConnectedCells(current))
            {
                if (!visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }
        return visited;
    }

    // 贪心多割算法实现 - 标准多割问题（不限制连通分量数量）
    private List<(Cell, Cell)> GreedyMulticut(Dictionary<Cell, List<Cell>> graph, Dictionary<(Cell, Cell), int> edgeWeightCache)
    {
        // 复制边集合
        var allEdges = edgeWeightCache.Keys.ToList();
        // 按权重从小到大排序（优先切割不重要的边）
        // 权重越小，表示边越不重要，越容易被切割
        allEdges.Sort((a, b) => edgeWeightCache[a].CompareTo(edgeWeightCache[b]));

        // 当前图的边集合
        var currentEdges = new HashSet<(Cell, Cell)>(allEdges);
        // 记录被割掉的边
        var cutEdges = new List<(Cell, Cell)>();

        // 标准多割：移除边直到没有违反的循环不等式
        bool hasViolation = true;
        int maxIterations = allEdges.Count; // 最多移除所有边
        int iteration = 0;

        while (hasViolation && iteration < maxIterations)
        {
            iteration++;
            hasViolation = false;

            // 构建当前图（移除被切割的边）
            var currentGraph = new Dictionary<Cell, List<Cell>>();
            foreach (var cell in graph.Keys)
                currentGraph[cell] = new List<Cell>();

            foreach (var edge in graph.Keys)
            {
                foreach (var neighbor in graph[edge])
                {
                    var edgeKey = GetCanonicalEdgeKey(edge, neighbor);
                    if (currentEdges.Contains(edgeKey))
                    {
                        currentGraph[edge].Add(neighbor);
                    }
                }
            }

            // 计算连通分量
            var nodeLabeling = new Dictionary<Cell, int>();
            var visited = new HashSet<Cell>();
            int componentId = 0;

            foreach (var cell in graph.Keys)
            {
                if (!visited.Contains(cell))
                {
                    var queue = new Queue<Cell>();
                    queue.Enqueue(cell);
                    visited.Add(cell);
                    nodeLabeling[cell] = componentId;

                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        foreach (var neighbor in currentGraph[current])
                        {
                            if (!visited.Contains(neighbor))
                            {
                                visited.Add(neighbor);
                                nodeLabeling[neighbor] = componentId;
                                queue.Enqueue(neighbor);
                            }
                        }
                    }
                    componentId++;
                }
            }

            // 检查是否有违反的循环不等式
            foreach (var edge in allEdges)
            {
                if (currentEdges.Contains(edge) && nodeLabeling[edge.Item1] == nodeLabeling[edge.Item2])
                {
                    // 找到从edge.Item1到edge.Item2的最短路径
                    var path = FindShortestPath(currentGraph, edge.Item1, edge.Item2);
                    if (path != null && path.Count >= 2)
                    {
                        // 检查路径上的所有边是否都被保留
                        bool pathIntact = true;
                        for (int i = 0; i < path.Count - 1; i++)
                        {
                            var pathEdge = GetCanonicalEdgeKey(path[i], path[i + 1]);
                            if (!currentEdges.Contains(pathEdge))
                            {
                                pathIntact = false;
                                break;
                            }
                        }

                        if (pathIntact)
                        {
                            // 违反循环不等式，移除这条边
                            currentEdges.Remove(edge);
                            cutEdges.Add(edge);
                            hasViolation = true;
                            break; // 一次只移除一条边
                        }
                    }
                }
            }
        }

        UnityEngine.Debug.Log($"贪心标准多割完成，切割边数: {cutEdges.Count}, 迭代次数: {iteration}");
        return cutEdges;
    }

    // ILP 多割算法实现 - 标准多割问题（不限制连通分量数量）
    private List<(Cell, Cell)> ILPMulticut(Dictionary<Cell, List<Cell>> graph, Dictionary<(Cell, Cell), int> edgeWeightCache)
    {
        try
        {
            // 创建 Gurobi 环境
            GRBEnv env = new GRBEnv();
            GRBModel model = new GRBModel(env);

            // 设置求解参数
            model.Parameters.OutputFlag = 0; // 不显示求解过程
            model.Parameters.TimeLimit = 30.0; // 30秒时间限制

            // 创建决策变量：每条边是否被切割
            var edgeVars = new Dictionary<(Cell, Cell), GRBVar>();
            foreach (var edge in edgeWeightCache.Keys)
            {
                edgeVars[edge] = model.AddVar(0.0, 1.0, edgeWeightCache[edge], GRB.BINARY, 
                                             $"edge_{edge.Item1.Number}_{edge.Item2.Number}");
            }

            // 设置目标函数：最大化保留边的权重和
            // 等价于最小化切割边的权重和，但使用正数权重更直观
            GRBLinExpr objective = 0.0;
            foreach (var edge in edgeWeightCache.Keys)
            {
                objective.AddTerm(edgeWeightCache[edge], edgeVars[edge]);
            }
            model.SetObjective(objective, GRB.MAXIMIZE); // 最大化保留边的权重和

            // 由于C# API的懒约束实现复杂，我们使用简化的方法：
            // 1. 先求解一个松弛版本
            // 2. 检查解的有效性
            // 3. 如果无效，添加必要的约束并重新求解

            // 第一轮求解
            model.Optimize();

            // 检查解的有效性并添加必要的约束
            bool validSolution = false;
            int maxIterations = 10; // 最多迭代10次
            int iteration = 0;

            while (!validSolution && iteration < maxIterations)
            {
                iteration++;
                
                // 获取当前解
                var currentSolution = new Dictionary<(Cell, Cell), double>();
                foreach (var edge in edgeWeightCache.Keys)
                {
                    currentSolution[edge] = edgeVars[edge].X;
                }

                // 构建当前图（移除被切割的边）
                var currentGraph = new Dictionary<Cell, List<Cell>>();
                foreach (var cell in _cells)
                    currentGraph[cell] = new List<Cell>();

                foreach (var edge in _edges.Keys)
                {
                    if (currentSolution.ContainsKey(edge) && currentSolution[edge] < 0.5)
                    {
                        currentGraph[edge.Item1].Add(edge.Item2);
                        currentGraph[edge.Item2].Add(edge.Item1);
                    }
                }

                // 计算连通分量
                var nodeLabeling = new Dictionary<Cell, int>();
                var visited = new HashSet<Cell>();
                int componentId = 0;

                foreach (var cell in _cells)
                {
                    if (!visited.Contains(cell))
                    {
                        var queue = new Queue<Cell>();
                        queue.Enqueue(cell);
                        visited.Add(cell);
                        nodeLabeling[cell] = componentId;

                        while (queue.Count > 0)
                        {
                            var current = queue.Dequeue();
                            foreach (var neighbor in currentGraph[current])
                            {
                                if (!visited.Contains(neighbor))
                                {
                                    visited.Add(neighbor);
                                    nodeLabeling[neighbor] = componentId;
                                    queue.Enqueue(neighbor);
                                }
                            }
                        }
                        componentId++;
                    }
                }

                // 检查是否有违反的循环不等式
                bool hasViolation = false;
                foreach (var edge in edgeWeightCache.Keys)
                {
                    if (currentSolution[edge] > 0.5 && nodeLabeling[edge.Item1] == nodeLabeling[edge.Item2])
                    {
                        // 找到从edge.Item1到edge.Item2的最短路径
                        var path = FindShortestPath(currentGraph, edge.Item1, edge.Item2);
                        if (path != null && path.Count >= 2)
                        {
                            // 添加循环不等式：x_uv <= sum(x_ij for all edges ij in path)
                            GRBLinExpr pathSum = 0.0;
                            for (int i = 0; i < path.Count - 1; i++)
                            {
                                var pathEdge = GetCanonicalEdgeKey(path[i], path[i + 1]);
                                if (edgeVars.ContainsKey(pathEdge))
                                {
                                    pathSum.AddTerm(1.0, edgeVars[pathEdge]);
                                }
                            }
                            model.AddConstr(edgeVars[edge] <= pathSum, $"cycle_{iteration}_{edge.Item1.Number}_{edge.Item2.Number}");
                            hasViolation = true;
                        }
                    }
                }

                if (!hasViolation)
                {
                    validSolution = true;
                }
                else
                {
                    // 重新求解
                    model.Optimize();
                }
            }

            // 获取最终结果
            var cutEdges = new List<(Cell, Cell)>();
            if (model.Status == GRB.Status.OPTIMAL || model.Status == GRB.Status.TIME_LIMIT)
            {
                foreach (var edge in edgeVars.Keys)
                {
                    if (edgeVars[edge].X > 0.5) // 如果变量值接近1
                    {
                        cutEdges.Add(edge);
                    }
                }
                UnityEngine.Debug.Log($"标准多割求解完成，目标值: {model.ObjVal}, 切割边数: {cutEdges.Count}, 迭代次数: {iteration}");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"标准多割求解失败，状态: {model.Status}");
            }

            // 清理资源
            model.Dispose();
            env.Dispose();

            return cutEdges;
        }
        catch (GRBException e)
        {
            UnityEngine.Debug.LogError($"Gurobi 错误: {e.Message}");
            return new List<(Cell, Cell)>();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"标准多割求解错误: {e.Message}");
            return new List<(Cell, Cell)>();
        }
    }

    // 查找最短路径的辅助方法
    private List<Cell> FindShortestPath(Dictionary<Cell, List<Cell>> graph, Cell start, Cell end)
    {
        var queue = new Queue<Cell>();
        var visited = new HashSet<Cell>();
        var parent = new Dictionary<Cell, Cell>();

        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == end)
            {
                // 重建路径
                var path = new List<Cell>();
                var node = end;
                while (node != start)
                {
                    path.Add(node);
                    node = parent[node];
                }
                path.Add(start);
                path.Reverse();
                return path;
            }

            foreach (var neighbor in graph[current])
            {
                if (!visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    parent[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }
        }

        return null; // 没有找到路径
    }

    // 高亮显示需要切割的边
    private void HighlightCutEdges(List<(Cell, Cell)> cutEdges)
    {
        // 调试：打印cutEdges数量和内容
        UnityEngine.Debug.Log($"[HighlightCutEdges] cutEdges.Count = {cutEdges.Count}");
        foreach (var edge in cutEdges)
        {
            UnityEngine.Debug.Log($"[HighlightCutEdges] cutEdge: {edge.Item1.Number}-{edge.Item2.Number}, InstanceID: {edge.Item1.GetInstanceID()}-{edge.Item2.GetInstanceID()}");
        }
        // 调试：打印_edges字典所有key
        UnityEngine.Debug.Log($"[HighlightCutEdges] _edges.Keys.Count = {_edges.Keys.Count}");
        foreach (var key in _edges.Keys)
        {
            UnityEngine.Debug.Log($"[HighlightCutEdges] _edges key: {key.Item1.Number}-{key.Item2.Number}, InstanceID: {key.Item1.GetInstanceID()}-{key.Item2.GetInstanceID()}");
        }
        // 1. 先全部恢复成普通材质
        foreach (var edgeInfo in _edges.Values)
        {
            if (_lineMaterial != null)
                edgeInfo.renderer.material = _lineMaterial;
        }
        // 2. 只把需要切割的边高亮
        foreach (var edge in cutEdges)
        {
            // 调试输出：打印每条需要高亮的边的编号
            UnityEngine.Debug.Log($"高亮边: {edge.Item1.Number}-{edge.Item2.Number}");
            if (_edges.TryGetValue(edge, out var edgeInfo))
            {
                if (highlightEdgeMaterial != null)
                    edgeInfo.renderer.material = highlightEdgeMaterial;
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[HighlightCutEdges] 未找到对应的边: {edge.Item1.Number}-{edge.Item2.Number}");
            }
        }
    }

    public void RunPythonMulticut(string pythonExe, string scriptPath, string inputPath, string outputPath, string jsonData)
    {
        // 写入输入文件
        File.WriteAllText(inputPath, jsonData);

        // 调用Python脚本
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = pythonExe; // 比如 "python"
        psi.Arguments = $"{scriptPath} \"{inputPath}\" \"{outputPath}\"";
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.CreateNoWindow = true;

        using (Process process = Process.Start(psi))
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrEmpty(error))
                UnityEngine.Debug.LogError(error);
        }

        // 读取Python输出
        string resultJson = File.ReadAllText(outputPath);
        // 你可以用JsonUtility/Json.NET等解析resultJson
    }
}
