using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using TMPro;

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
    private Dictionary<(Cell, Cell), (LineRenderer renderer, float weight)> _edges = new Dictionary<(Cell, Cell), (LineRenderer, float)>(); // 存储所有的连线
    private Transform linesRoot; // 用于组织所有连线的父物体

    private bool isErasing = false;
    private LineRenderer eraseLineRenderer; // 用于显示擦除线

    private List<Vector2> erasePath = new List<Vector2>();

    private const float EPSILON = 1e-6f; // 用于浮点数比较

    [SerializeField]
    private bool useWeightedEdges = false; // 这个就是一个开关

    // 唯一权重缓存
    private Dictionary<(Cell, Cell), float> _edgeWeightCache = new Dictionary<(Cell, Cell), float>();
    [SerializeField] private float minEdgeWeight = 1f;
    [SerializeField] private float maxEdgeWeight = 10f;

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

    private void Awake()
    {
        Instance = this;
        linesRoot = new GameObject("LinesRoot").transform;
        linesRoot.SetParent(transform);
        SpawnLevel(_cellNumbers);
    }

    public void LoadLevelAndSpawnNodes(int numberOfCells)
    {
        _cellNumbers = numberOfCells;
        SpawnLevel(numberOfCells);
    }

    private List<Vector2> GenerateCellPositions(int numberOfPoints)
    {
        List<Vector2> cellPositions = new List<Vector2>();
        int maxAttempts = 5; // 最大尝试次数，避免死循环

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("Main Camera not found!");
            return cellPositions;
        }

        float cameraHeight = 2f * mainCamera.orthographicSize;
        float cameraWidth = cameraHeight * mainCamera.aspect;

        // 缩小范围到 80% 的区域
        float minX = mainCamera.transform.position.x - cameraWidth * 0.4f; // 左边界
        float maxX = mainCamera.transform.position.x + cameraWidth * 0.4f; // 右边界
        float minY = mainCamera.transform.position.y - cameraHeight * 0.4f; // 下边界
        float maxY = mainCamera.transform.position.y + cameraHeight * 0.4f; // 上边界

        float minDistance = 1.5f; // 最小间距，可以根据需要调整

        for (int i = 0; i < numberOfPoints; i++)
        {
            bool isValidPosition = false;
            int attempts = 0;
            Vector2 newPosition = Vector2.zero;

            while (!isValidPosition && attempts < maxAttempts)
            {
                float randomX = UnityEngine.Random.Range(minX, maxX);
                float randomY = UnityEngine.Random.Range(minY, maxY);
                newPosition = new Vector2(randomX, randomY);

                isValidPosition = true;
                foreach (Vector2 existingPosition in cellPositions)
                {
                    if (Vector2.Distance(newPosition, existingPosition) < minDistance)
                    {
                        isValidPosition = false;
                        break;
                    }
                }
                Debug.Log($"Attempting to place cell at {newPosition}, valid: {isValidPosition}");

                attempts++;
            }

            if (isValidPosition)
            {
                cellPositions.Add(newPosition);
            }
            else
            {
                Debug.LogWarning("Failed to find a valid position after maximum attempts.");
            }
        }

        return cellPositions;
    }

    private void SpawnLevel(int numberOfPoints)
    {
        // 清除现有的单元格和连线
        foreach (var cell in _cells)
        {
            if (cell != null)
            {
                Destroy(cell.gameObject);
            }
        }
        _cells.Clear();
        RemoveAllEdges();

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

        // Generate Delaunay Triangulation
        if (_cells.Count >= 3) // Need at least 3 points for triangulation
        {
            List<DelaunayEdge> delaunayEdges = PerformDelaunayTriangulation(pointsForTriangulation);
            foreach (var edge in delaunayEdges)
            {
                CreateOrUpdateEdge(_cells[edge.P1Index], _cells[edge.P2Index]);
            }
        }
        else if (_cells.Count == 2) // If only two points, connect them directly
        {
            CreateOrUpdateEdge(_cells[0], _cells[1]);
        }
        // If 0 or 1 cell, do nothing
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

    private List<DelaunayEdge> PerformDelaunayTriangulation(List<Vector2> points)
    {
        if (points == null || points.Count < 3)
        {
            Debug.LogWarning("Delaunay triangulation requires at least 3 points.");
            // For 2 points, handle outside or return empty and let caller handle.
            // For now, SpawnLevel handles 2 points separately.
             return new List<DelaunayEdge>();
        }

        List<DelaunayTriangle> triangles = new List<DelaunayTriangle>();

        // 1. Create a "super triangle" that encloses all input points
        List<Vector2> superTriangleVertices = GetSuperTriangleVertices(points);
        // Assign large negative indices to super triangle vertices to distinguish them
        var st = new DelaunayTriangle(superTriangleVertices[0], superTriangleVertices[1], superTriangleVertices[2], -1, -2, -3);
        triangles.Add(st);

        // 2. Add each point one by one to the triangulation
        for (int pointIndex = 0; pointIndex < points.Count; pointIndex++)
        {
            Vector2 point = points[pointIndex];
            List<DelaunayTriangle> badTriangles = new List<DelaunayTriangle>();
            List<DelaunayEdge> polygonHole = new List<DelaunayEdge>();

            // Find all triangles whose circumcircle contains the point
            foreach (var triangle in triangles)
            {
                if (triangle.IsPointInCircumcircle(point))
                {
                    badTriangles.Add(triangle);
                }
            }

            // Remove bad triangles, and form the polygon hole
            foreach (var triangle in badTriangles)
            {
                // Add edges of the bad triangle to the polygon hole if not shared by another bad triangle
                DelaunayEdge[] edges = {
                    new DelaunayEdge(triangle.Index1, triangle.Index2), // These indices are from original point set or super triangle
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
                        if (triangle.Equals(otherBadTriangle)) continue; // Don't compare with self

                        // Check if otherBadTriangle shares this edge
                        // An edge is (v_current, v_next)
                        if (otherBadTriangle.ContainsVertex(v_current) && otherBadTriangle.ContainsVertex(v_next))
                        {
                            isShared = true;
                            break;
                        }
                    }
                    if (!isShared)
                    {
                         // Store edge by original indices
                        polygonHole.Add(new DelaunayEdge(triIndices[i], triIndices[(i+1)%3]));
                    }
                }
            }
            triangles.RemoveAll(t => badTriangles.Contains(t));


            // Re-triangulate the polygonal hole by connecting the new point to all its vertices
            // The vertices of the polygonHole are formed by the edges.
            // Each edge in polygonHole connects to the new point.
            // The indices in polygonHole edges are original point indices (or super triangle negative indices).
            foreach (var edge in polygonHole)
            {
                // Determine the actual vertices for the new triangle from the edge's original indices
                Vector2 p1 = (edge.P1Index < 0) ? superTriangleVertices[-edge.P1Index -1] : points[edge.P1Index];
                Vector2 p2 = (edge.P2Index < 0) ? superTriangleVertices[-edge.P2Index -1] : points[edge.P2Index];
                triangles.Add(new DelaunayTriangle(point, p1, p2, pointIndex, edge.P1Index, edge.P2Index));
            }
        }

        // 3. Remove all triangles that share a vertex with the original super triangle
        triangles.RemoveAll(triangle =>
            triangle.Index1 < 0 || triangle.Index2 < 0 || triangle.Index3 < 0 ||
            triangle.ContainsVertex(superTriangleVertices[0]) ||
            triangle.ContainsVertex(superTriangleVertices[1]) ||
            triangle.ContainsVertex(superTriangleVertices[2])
        );
        
        // 4. Collect unique edges from the final triangulation
        HashSet<DelaunayEdge> finalEdges = new HashSet<DelaunayEdge>();
        foreach (var triangle in triangles)
        {
            // Ensure indices are valid (not from super triangle)
            if (triangle.Index1 >= 0 && triangle.Index2 >= 0) finalEdges.Add(new DelaunayEdge(triangle.Index1, triangle.Index2));
            if (triangle.Index2 >= 0 && triangle.Index3 >= 0) finalEdges.Add(new DelaunayEdge(triangle.Index2, triangle.Index3));
            if (triangle.Index3 >= 0 && triangle.Index1 >= 0) finalEdges.Add(new DelaunayEdge(triangle.Index3, triangle.Index1));
        }
        Debug.Log($"Delaunay triangulation resulted in {finalEdges.Count} unique edges.");
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
            Debug.Log("Raycast 命中 Cell: " + hitCell.collider.gameObject.name);
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
            Debug.Log("Raycast 未命中 Cell");
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
            Debug.Log("点击到连线，准备删除: " + hitEdge.collider.gameObject.name);
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
                    Debug.Log("不能删除此边：删除后不会增加连通分量数量。");
                }
            }
        }
        else
        {
            Debug.Log("Raycast 未命中 Line");
        }
    }

    private void HandlePreviewDrag()
    {
        UpdatePreviewLine(Camera.main.ScreenToWorldPoint(Input.mousePosition));
    }

    private void HandleMouseUp()
    {
        var endCell = RaycastCell();
        Debug.Log("Mouse Up, Raycast Cell: " + (endCell != null ? endCell.Number.ToString() : "null"));
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
            Debug.Log("Hit Collider: " + hit.collider.name);
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

    public void CreateOrUpdateEdge(Cell fromCell, Cell toCell, float weight = 1f)
    {
        var key = GetCanonicalEdgeKey(fromCell, toCell);
        if (_edges.ContainsKey(key))
        {
            var (renderer, _) = _edges[key];
            renderer.SetPosition(0, fromCell.transform.position);
            renderer.SetPosition(1, toCell.transform.position);
            _edges[key] = (renderer, weight); // 更新权重
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
            tmp.fontSize = 4;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.black;

            // 设置文本内容
            tmp.text = weight.ToString("0.0");

            // 设置位置为边的中点
            Vector3 midPoint = (fromCell.transform.position + toCell.transform.position) / 2f;
            textObj.transform.position = midPoint;

            // 可选：让文本始终朝向摄像机
            textObj.transform.rotation = Quaternion.identity;

            _edges[key] = (lineRenderer, weight);
        }
    }

    public void CreateOrUpdateEdge(Cell fromCell, Cell toCell)
    {
        float weight = GetOrCreateEdgeWeight(fromCell, toCell);
        CreateOrUpdateEdge(fromCell, toCell, weight);
    }

    public void RemoveEdge(Cell fromCell, Cell toCell)
    {
        var key = GetCanonicalEdgeKey(fromCell, toCell);
        if (_edges.TryGetValue(key, out var edge))
        {
            Destroy(edge.renderer.gameObject);
            _edges.Remove(key);
        }
    }

    public void RemoveAllEdges()
    {
        foreach (var edge in _edges.Values)
        {
            Destroy(edge.renderer.gameObject);
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
            Debug.Log("不能擦除：此次操作不会增加连通分量数量。");
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
    private float GetOrCreateEdgeWeight(Cell a, Cell b)
    {
        var key = GetCanonicalEdgeKey(a, b);
        if (!_edgeWeightCache.TryGetValue(key, out float weight))
        {
            weight = UnityEngine.Random.Range(minEdgeWeight, maxEdgeWeight);
            _edgeWeightCache[key] = weight;
        }
        return weight;
    }
}
