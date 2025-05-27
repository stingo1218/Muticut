using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

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
    private Dictionary<(Cell, Cell), LineRenderer> _edges = new Dictionary<(Cell, Cell), LineRenderer>(); // 存储所有的连线
    private Transform linesRoot; // 用于组织所有连线的父物体

    private bool isErasing = false;
    private LineRenderer eraseLineRenderer; // 用于显示擦除线

    private List<Vector2> erasePath = new List<Vector2>();

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

        for (int i = 0; i < cellPositions.Count; i++)
        {
            Vector2 position = cellPositions[i];

            Cell newCell = Instantiate(_cellPrefab, position, Quaternion.identity, transform);
            newCell.Number = i + 1;
            newCell.Init(i + 1);
            newCell.gameObject.name = $"Cell {newCell.Number}";

            _cells.Add(newCell);
        }

        // 创建所有点之间的全连接
        for (int i = 0; i < _cells.Count; i++)
        {
            for (int j = i + 1; j < _cells.Count; j++)
            {
                CreateOrUpdateEdge(_cells[i], _cells[j]);
            }
        }
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
            var toRemoveKey = _edges.FirstOrDefault(pair => pair.Value.gameObject == hitEdge.collider.gameObject).Key;
            
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

    public void CreateOrUpdateEdge(Cell fromCell, Cell toCell)
    {
        var key = (fromCell, toCell);
        var reversedKey = (toCell, fromCell);

        if (_edges.ContainsKey(key) || _edges.ContainsKey(reversedKey)) 
        {
            UpdateEdge(fromCell, toCell);
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

            _edges[key] = lineRenderer;
            lineObject.layer = LayerMask.NameToLayer("Edge");
        }
    }

    private void UpdateEdge(Cell fromCell, Cell toCell)
    {
        var key = (fromCell, toCell);
        var reversedKey = (toCell, fromCell);

        if (_edges.TryGetValue(key, out LineRenderer lineRenderer) || _edges.TryGetValue(reversedKey, out lineRenderer))
        {
            lineRenderer.SetPosition(0, fromCell.transform.position);
            lineRenderer.SetPosition(1, toCell.transform.position);
        }
    }

    public void RemoveEdge(Cell fromCell, Cell toCell)
    {
        var key = (fromCell, toCell);
        var reversedKey = (toCell, fromCell);

        if (_edges.TryGetValue(key, out LineRenderer lineRenderer))
        {
            Destroy(lineRenderer.gameObject);
            _edges.Remove(key);
        }
        else if (_edges.TryGetValue(reversedKey, out lineRenderer))
        {
            Destroy(lineRenderer.gameObject);
            _edges.Remove(reversedKey);
        }
    }

    public void RemoveAllEdges()
    {
        foreach (var edge in _edges.Values)
        {
            Destroy(edge.gameObject);
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
            var lineRenderer = pair.Value;
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
}

[Serializable]
public struct LevelData
{
    public int row, col;
    public List<int> data;
}