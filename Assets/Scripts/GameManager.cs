using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    [SerializeField] private Cell _cellPrefab; // 单元格预制体

    [HideInInspector] public bool hasgameFinished;

    [SerializeField] private SpriteRenderer _bgSprite;
    [SerializeField] private SpriteRenderer _highlightSprite;
    [SerializeField] private Vector2 _highlightSize;
    [SerializeField] private LevelData _levelData;
    [SerializeField] private float _cellGap;
    [SerializeField] private float _levelGap;
    [SerializeField] private List<Vector2> _cellPositions; // 自定义单元格位置列表
    [SerializeField] private int _cellCount = 10; // 要生成的单元格数量
    [SerializeField] private float _cellSize = 1.5f; // 单元格的最小间距

    private Cell[,] _cellGrid; // 存储生成的单元格


    private int[,] levelgrid;
    private Cell[,] cellGrid;
    private Cell startCell;
    private Vector2 startPos;
    private LineRenderer previewEdge;

    [SerializeField] private Material previewEdgeMaterial; // 预览线材质
    [SerializeField] private Material _lineMaterial; // 用于连线的材质
    [SerializeField] private Material _eraseLineMaterial; // 用于擦除线的材质
    private Dictionary<(Cell, Cell), LineRenderer> _edges = new Dictionary<(Cell, Cell), LineRenderer>(); // 存储所有的连线
    private Transform linesRoot; // 用于组织所有连线的父物体

    private Vector2 eraseLineStart;
    private Vector2 eraseLineEnd;
    private bool isErasing = false;
    private LineRenderer eraseLineRenderer; // 用于显示擦除线

    private List<Vector2> erasePath = new List<Vector2>();

    private void Awake()
    {
        // TODO: Implement initialization logic
        Instance = this;
        // 创建一个空物体来组织所有的连线
        linesRoot = new GameObject("LinesRoot").transform;
        linesRoot.SetParent(transform);
        SpawnLevel(); // 生成网格
    }

    private List<Vector2> GenerateCellPositions()
    {
        List<Vector2> cellPositions = new List<Vector2>(); // 初始化位置列表
        int maxAttempts = 5; // 最大尝试次数，避免死循环

        // 获取 Main Camera 的视口边界
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("Main Camera not found!");
            return cellPositions;
        }

        // 计算相机的视口边界（以世界坐标表示）
        float cameraHeight = 2f * mainCamera.orthographicSize;
        float cameraWidth = cameraHeight * mainCamera.aspect;

        // 缩小范围到 80% 的区域
        float minX = mainCamera.transform.position.x - cameraWidth * 0.4f; // 左边界
        float maxX = mainCamera.transform.position.x + cameraWidth * 0.4f; // 右边界
        float minY = mainCamera.transform.position.y - cameraHeight * 0.4f; // 下边界
        float maxY = mainCamera.transform.position.y + cameraHeight * 0.4f; // 上边界

        for (int i = 0; i < _cellCount; i++) // 根据指定数量生成位置
        {
            bool isValidPosition = false;
            int attempts = 0;
            Vector2 newPosition = Vector2.zero; // 在循环外声明变量

            while (!isValidPosition && attempts < maxAttempts)
            {
                // 在缩小的相机视口范围内随机生成一个位置
                float randomX = UnityEngine.Random.Range(minX, maxX);
                float randomY = UnityEngine.Random.Range(minY, maxY);
                newPosition = new Vector2(randomX, randomY);

                // 检查是否与已有位置过于接近
                isValidPosition = true;
                foreach (Vector2 existingPosition in cellPositions)
                {
                    if (Vector2.Distance(newPosition, existingPosition) < _cellSize)
                    {
                        isValidPosition = false;
                        break;
                    }
                }
                Debug.Log($"Attempting to place cell at {newPosition}, valid: {isValidPosition}"); // 输出调试信息

                attempts++;
            }

            // 如果找到有效位置，则添加到列表
            if (isValidPosition)
            {
                cellPositions.Add(newPosition);
            }
            else
            {
                Debug.LogWarning("Failed to find a valid position after maximum attempts.");
            }
        }

        return cellPositions; // 返回生成的单元格位置列表
    }

    private void SpawnLevel()
    {
        _cellPositions = GenerateCellPositions(); // 生成单元格位置

        _cellGrid = new Cell[_cellPositions.Count, 1]; // 初始化网格数组

        for (int i = 0; i < _cellPositions.Count; i++)
        {
            // 获取自定义位置
            Vector2 position = _cellPositions[i];

            // 实例化单元格
            Cell newCell = Instantiate(_cellPrefab, position, Quaternion.identity, transform);

            // 分配唯一数字
            newCell.Number = i + 1; // 从 1 开始递增
            // 初始化单元格
            newCell.Init(i + 1);

            // 设置单元格名称（可选）
            newCell.gameObject.name = $"Cell {newCell.Number}";

            // 存储到网格数组中
            _cellGrid[i, 0] = newCell;
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
            Vector2 start = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            erasePath.Add(start);
            ShowEraseLine(start);
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
            var toRemove = _edges.FirstOrDefault(pair => pair.Value.gameObject == hitEdge.collider.gameObject).Key;
            if (!toRemove.Equals(default((Cell, Cell))))
            {
                RemoveEdge(toRemove.Item1, toRemove.Item2);
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
        if (!IsValidConnection(fromCell, toCell))
        {
            Debug.LogWarning("Invalid connection attempt between cells!");
            return;
        }

        var key = (fromCell, toCell);
        if (_edges.ContainsKey(key))
        {
            // 如果已经存在连线，更新它的位置
            UpdateEdge(fromCell, toCell);
        }
        else
        {
            // 创建新的连线
            GameObject lineObject = new GameObject($"Line_{fromCell.Number}_to_{toCell.Number}");
            lineObject.transform.SetParent(linesRoot); // 设置父物体
            LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();

            // 配置 LineRenderer
            lineRenderer.material = _lineMaterial;
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;

            // 设置连线的起点和终点
            lineRenderer.SetPosition(0, fromCell.transform.position);
            lineRenderer.SetPosition(1, toCell.transform.position);

            // 添加EdgeCollider2D用于点击检测
            EdgeCollider2D edge = lineObject.AddComponent<EdgeCollider2D>();
            Vector2 start = fromCell.transform.position;
            Vector2 end = toCell.transform.position;
            edge.points = new Vector2[] {
                lineObject.transform.InverseTransformPoint(start),
                lineObject.transform.InverseTransformPoint(end)
            };
            edge.edgeRadius = 0.1f; // 可调宽度
            edge.isTrigger = true;  // 推荐设为Trigger

            // 将连线存储到字典中
            _edges[key] = lineRenderer;

            // 设置连线的Layer为Edge
            lineObject.layer = LayerMask.NameToLayer("Edge");
        }
    }

    private void UpdateEdge(Cell fromCell, Cell toCell)
    {
        var key = (fromCell, toCell);
        if (_edges.TryGetValue(key, out LineRenderer lineRenderer))
        {
            lineRenderer.SetPosition(0, fromCell.transform.position);
            lineRenderer.SetPosition(1, toCell.transform.position);
        }
    }

    public void RemoveEdge(Cell fromCell, Cell toCell)
    {
        var key = (fromCell, toCell);
        if (_edges.TryGetValue(key, out LineRenderer lineRenderer))
        {
            Destroy(lineRenderer.gameObject);
            _edges.Remove(key);
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

    // 检查两个Cell之间是否可以连线
    private bool IsValidConnection(Cell fromCell, Cell toCell)
    {

        return true;
    }

    // 移除与指定Cell相连的所有连线
    private void RemoveConnectedEdges(Cell cell)
    {
        var edgesToRemove = _edges.Keys.Where(k => k.Item1 == cell || k.Item2 == cell).ToList();
        foreach (var edge in edgesToRemove)
        {
            RemoveEdge(edge.Item1, edge.Item2);
        }
    }

    // 获取与指定Cell相连的所有Cell
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
            eraseLineRenderer.textureMode = LineTextureMode.Tile; // 启用纹理平铺
            eraseLineRenderer.sortingOrder = 10; // 确保显示在最上层
        }
        // 关键：每次新轨迹都重置为1个点
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
        foreach (var pair in _edges.ToList())
        {
            var lineRenderer = pair.Value;
            Vector2 edgeStart = lineRenderer.GetPosition(0);
            Vector2 edgeEnd = lineRenderer.GetPosition(1);

            // 检查轨迹的每一段与edge是否相交
            for (int i = 0; i < path.Count - 1; i++)
            {
                if (LineSegmentsIntersect(path[i], path[i + 1], edgeStart, edgeEnd))
                {
                    RemoveEdge(pair.Key.Item1, pair.Key.Item2);
                    break; // 一旦相交就删除，不用再检测
                }
            }
        }
    }

    // 判断两线段是否相交的工具函数
    private bool LineSegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        // 利用叉积判断
        float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        Vector2 r = p2 - p1;
        Vector2 s = q2 - q1;
        float denominator = Cross(r, s);
        if (denominator == 0) return false; // 平行
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