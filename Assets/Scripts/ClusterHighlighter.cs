using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class CH_ClustersAfterCutData
{
    public CH_CutEdge[] cut_edges;
    public int cost;
    public CH_ClusterInfo[] clusters;
    public int cluster_count;
    public string timestamp;
}

[System.Serializable]
public class CH_ClusterInfo
{
    public int[] cells;
}

[System.Serializable]
public class CH_CutEdge
{
    public int u;
    public int v;
}

public class ClusterHighlighter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MonoBehaviour terrainManager; // 需提供具有 tilemap 属性的组件（如 TerrainManager）
    [SerializeField] private MonoBehaviour gameManager;    // GameManager，用于读取 _cells

    [Header("Behavior")]
    [SerializeField] private string clusterDataPath = "clusters_after_cut.json";
    [SerializeField] private Color initialBaseColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
    [SerializeField] private int recolorBatchSize = 600;
    [SerializeField] private bool showOnStart = true;
    [SerializeField] private bool updateColorsOnCostChange = false;

    [Header("Colors")]
    [SerializeField] private Color[] predefinedColors = new Color[]
    {
        // 深色系调色板（半透明，便于叠加显示）
        new Color(0.15f, 0.20f, 0.35f, 0.6f), // 深海军蓝
        new Color(0.18f, 0.28f, 0.18f, 0.6f), // 深森林绿
        new Color(0.30f, 0.15f, 0.15f, 0.6f), // 深砖红
        new Color(0.22f, 0.25f, 0.30f, 0.6f), // 石板灰蓝
        new Color(0.20f, 0.16f, 0.24f, 0.6f), // 深紫罗兰
        new Color(0.20f, 0.30f, 0.32f, 0.6f), // 深青蓝
        new Color(0.35f, 0.25f, 0.18f, 0.6f), // 深棕褐
        new Color(0.28f, 0.22f, 0.22f, 0.6f), // 暗酒红
        new Color(0.20f, 0.22f, 0.26f, 0.6f), // 深石墨
        new Color(0.12f, 0.18f, 0.22f, 0.6f), // 暗青灰
        new Color(0.22f, 0.18f, 0.24f, 0.6f), // 茄皮紫
        new Color(0.26f, 0.28f, 0.18f, 0.6f), // 暗橄榄
        new Color(0.18f, 0.24f, 0.18f, 0.6f), // 墨绿
        new Color(0.23f, 0.20f, 0.16f, 0.6f), // 深赭石
        new Color(0.16f, 0.20f, 0.26f, 0.6f), // 深钢蓝
        new Color(0.30f, 0.22f, 0.28f, 0.6f), // 葡萄酒紫
        new Color(0.22f, 0.30f, 0.22f, 0.6f), // 苔藓绿
        new Color(0.28f, 0.30f, 0.32f, 0.6f), // 暗银灰
        new Color(0.18f, 0.16f, 0.20f, 0.6f), // 木炭灰
        new Color(0.32f, 0.28f, 0.18f, 0.6f), // 深卡其
        new Color(0.20f, 0.24f, 0.30f, 0.6f), // 蓝灰
        new Color(0.24f, 0.20f, 0.28f, 0.6f), // 梅子紫
        new Color(0.28f, 0.24f, 0.20f, 0.6f), // 焦糖褐
        new Color(0.20f, 0.27f, 0.26f, 0.6f), // 石青
        new Color(0.24f, 0.28f, 0.20f, 0.6f), // 苦艾绿
        new Color(0.22f, 0.22f, 0.28f, 0.6f), // 深暮蓝
        new Color(0.27f, 0.20f, 0.22f, 0.6f), // 深玫瑰红
        new Color(0.20f, 0.18f, 0.18f, 0.6f), // 深烟灰
        new Color(0.24f, 0.26f, 0.30f, 0.6f), // 冷灰蓝
        new Color(0.30f, 0.30f, 0.30f, 0.6f), // 中深灰
    };

    private List<Cell> cells = new List<Cell>();
    private Dictionary<Cell, List<Vector3Int>> cellTileAssignment = new Dictionary<Cell, List<Vector3Int>>();
    private Dictionary<Vector3Int, Color> lastTileColors = new Dictionary<Vector3Int, Color>();
    private Coroutine recolorCoroutine;
    private bool isHighlightVisible = false;
    
    // 后台计算相关
    private bool isDataInitialized = false;
    private Coroutine backgroundCalculationCoroutine;
    private Dictionary<Vector3Int, Color> cachedTileColors = new Dictionary<Vector3Int, Color>();
    private CH_ClustersAfterCutData cachedClusterData = null;
    private string cachedClustersSignature = null;

    private void Awake()
    {
        // 自动查找引用（可选）
        if (terrainManager == null)
        {
            var all = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            terrainManager = all.FirstOrDefault(m => m != null && m.GetType().Name == "TerrainManager");
        }
        if (gameManager == null)
        {
            var all = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            gameManager = all.FirstOrDefault(m => m != null && m.GetType().Name == "GameManager");
        }
    }
    
    private void Start()
    {
        // 启动后台计算协程
        StartCoroutine(BackgroundEcoZoneCalculation());

        // 在显示之前，尝试用现有JSON预热缓存，避免开局几秒后颜色跳变
        PrewarmColorsFromExistingData();

        // 开局默认显示生态区
        if (showOnStart)
        {
            ShowEcoZones();
        }
    }

    private void PrewarmColorsFromExistingData()
    {
        try
        {
            var tilemap = GetTilemap();
            if (tilemap == null) return;

            // 获取Cells并建立分配
            GetCellsFromGameManager();
            if (cells.Count == 0) return;
            if (cellTileAssignment.Count == 0)
            {
                AssignTilesToCells(tilemap);
            }

            var data = LoadClustersData();
            bool hasNewData = data != null && data.clusters != null && data.clusters.Length > 0;
            if (hasNewData)
            {
                // 生成并缓存颜色，设置签名，避免协程首次触发"数据变化"
                var colors = BuildColorsByClusters(tilemap, data);
                cachedTileColors.Clear();
                foreach (var kv in colors) cachedTileColors[kv.Key] = kv.Value;
                cachedClusterData = data;
                cachedClustersSignature = BuildClustersSignature(data);
            }
        }
        catch (System.Exception ex)
        {
            // Debug.LogWarning($"⚠️ ClusterHighlighter: 预热现有簇数据失败: {ex.Message}");
        }
    }

    [ContextMenu("刷新簇显示(从JSON)")]
    public void RefreshFromJson()
    {
        if (!isHighlightVisible)
        {
            // 未开启显示时不进行着色，仅忽略更新请求
            return;
        }
        var tilemap = GetTilemap();
        if (tilemap == null)
        {
            // Debug.LogWarning("⚠️ ClusterHighlighter: Tilemap 未就绪");
            return;
        }

        // 使用缓存的数据或重新计算
        Dictionary<Vector3Int, Color> newTileColors;
        if (cachedTileColors.Count > 0)
        {
            newTileColors = new Dictionary<Vector3Int, Color>(cachedTileColors);
            // Debug.Log("🌍 使用缓存数据刷新显示");
        }
        else
        {
            // 重新计算
            GetCellsFromGameManager();
            if (cells.Count == 0)
            {
                // Debug.LogWarning("⚠️ ClusterHighlighter: 未获取到Cells");
                return;
            }

            if (cellTileAssignment.Count == 0)
            {
                AssignTilesToCells(tilemap);
            }

            var data = LoadClustersData();
            newTileColors = (data != null && data.clusters != null && data.clusters.Length > 0)
                ? BuildColorsByClusters(tilemap, data)
                : BuildUniformColors(tilemap);
        }

        StartIncrementalRecolor(tilemap, newTileColors);
    }

    [ContextMenu("显示生态区(高亮)")]
    public void ShowEcoZones()
    {
        isHighlightVisible = true;
        var tilemap = GetTilemap();
        if (tilemap == null) return;
        
        // 使用缓存的数据立即显示
        if (cachedTileColors.Count > 0)
        {
            // Debug.Log("🌍 使用缓存数据立即显示生态区");
            StartIncrementalRecolor(tilemap, cachedTileColors);
        }
        else
        {
            // 如果没有缓存数据，先用统一底色
            // Debug.Log("🌍 使用统一底色显示生态区");
            var uniform = BuildUniformColors(tilemap);
            StartIncrementalRecolor(tilemap, uniform);
        }
    }

    [ContextMenu("隐藏生态区(清除高亮)")]
    public void HideEcoZones()
    {
        var tilemap = GetTilemap();
        if (tilemap == null)
        {
            isHighlightVisible = false;
            return;
        }
        // 停止进行中的协程
        if (recolorCoroutine != null)
        {
            StopCoroutine(recolorCoroutine);
            recolorCoroutine = null;
        }
        // 恢复所有已着色的tile为白色
        var keys = new List<Vector3Int>(lastTileColors.Keys);
        foreach (var pos in keys)
        {
            tilemap.SetTileFlags(pos, TileFlags.None);
            tilemap.SetColor(pos, Color.white);
        }
        lastTileColors.Clear();
        isHighlightVisible = false;
    }

    // 供Toggle的 OnValueChanged(bool) 直接绑定
    public void OnEcoZonesToggleChanged(bool isOn)
    {
        if (isOn) ShowEcoZones(); else HideEcoZones();
    }

    private Tilemap GetTilemap()
    {
        if (terrainManager == null) return null;
        var prop = terrainManager.GetType().GetProperty("tilemap");
        return prop != null ? prop.GetValue(terrainManager) as Tilemap : null;
    }

    private void GetCellsFromGameManager()
    {
        cells.Clear();
        if (gameManager == null) return;
        try
        {
            var cellsField = gameManager.GetType().GetField("_cells", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var listObj = cellsField != null ? cellsField.GetValue(gameManager) as System.Collections.IList : null;
            if (listObj != null)
            {
                foreach (var obj in listObj)
                {
                    if (obj is Cell c && c != null)
                    {
                        cells.Add(c);
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            // Debug.LogWarning($"⚠️ ClusterHighlighter: 读取GameManager._cells失败: {ex.Message}");
        }
    }

    private void AssignTilesToCells(Tilemap tilemap)
    {
        cellTileAssignment.Clear();
        foreach (var c in cells)
        {
            cellTileAssignment[c] = new List<Vector3Int>();
        }

        var landTiles = GetLandTiles(tilemap);
        foreach (var tilePos in landTiles)
        {
            Vector3 world = tilemap.CellToWorld(tilePos);
            Cell nearest = FindNearestCell(world);
            if (nearest != null)
            {
                cellTileAssignment[nearest].Add(tilePos);
            }
        }
    }

    private List<Vector3Int> GetLandTiles(Tilemap tilemap)
    {
        List<Vector3Int> res = new List<Vector3Int>();
        if (tilemap == null) return res;
        BoundsInt bounds = tilemap.cellBounds;
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                var pos = new Vector3Int(x, y, 0);
                if (!tilemap.HasTile(pos)) continue;
                int biome = GetBiomeAtTile(pos);
                if (biome > 1) res.Add(pos); // 排除深水/浅水
            }
        }
        return res;
    }

    private int GetBiomeAtTile(Vector3Int tilePos)
    {
        if (terrainManager == null) return -1;
        try
        {
            var m = terrainManager.GetType().GetMethod("GetBiomeAtTile");
            if (m != null)
            {
                var result = m.Invoke(terrainManager, new object[] { tilePos });
                if (result != null) return (int)result;
            }
        }
        catch { }
        return -1;
    }

    private Cell FindNearestCell(Vector3 position)
    {
        Cell nearest = null;
        float minDist = float.MaxValue;
        foreach (var c in cells)
        {
            float d = Vector3.Distance(position, c.transform.position);
            if (d < minDist)
            {
                minDist = d;
                nearest = c;
            }
        }
        return nearest;
    }

    private CH_ClustersAfterCutData LoadClustersData()
    {
        try
        {
            string path = System.IO.Path.Combine(Application.dataPath, "..", clusterDataPath);
            if (!System.IO.File.Exists(path)) return null;
            string json = System.IO.File.ReadAllText(path);
            return JsonUtility.FromJson<CH_ClustersAfterCutData>(json);
        }
        catch (System.Exception ex)
        {
            // Debug.LogWarning($"⚠️ ClusterHighlighter: 读取JSON失败: {ex.Message}");
            return null;
        }
    }

    private bool HasClustersJson()
    {
        try
        {
            string path = System.IO.Path.Combine(Application.dataPath, "..", clusterDataPath);
            return System.IO.File.Exists(path) && new System.IO.FileInfo(path).Length > 0;
        }
        catch { return false; }
    }

    private Dictionary<Vector3Int, Color> BuildColorsByClusters(Tilemap tilemap, CH_ClustersAfterCutData data)
    {
        Dictionary<Vector3Int, Color> colors = new Dictionary<Vector3Int, Color>();
        // 建立编号->Cell映射
        Dictionary<int, Cell> numberToCell = new Dictionary<int, Cell>();
        foreach (var c in cells)
        {
            numberToCell[c.Number] = c;
        }

        for (int i = 0; i < data.clusters.Length; i++)
        {
            var info = data.clusters[i];
            Color clusterColor = GetStableClusterColor(info);
            if (info?.cells == null) continue;
            foreach (int num in info.cells)
            {
                if (!numberToCell.TryGetValue(num, out var cell)) continue;
                if (!cellTileAssignment.TryGetValue(cell, out var tiles)) continue;
                foreach (var pos in tiles)
                {
                    colors[pos] = clusterColor;
                }
            }
        }
        return colors;
    }

    private Color GetStableClusterColor(CH_ClusterInfo info)
    {
        int key = 0;
        if (info?.cells != null && info.cells.Length > 0)
        {
            int min = info.cells[0];
            for (int i = 1; i < info.cells.Length; i++)
            {
                if (info.cells[i] < min) min = info.cells[i];
            }
            key = min;
        }
        var colors = predefinedColors != null && predefinedColors.Length > 0 ? predefinedColors : new[] { initialBaseColor };
        return colors[Mathf.Abs(key) % colors.Length];
    }

    private string BuildClustersSignature(CH_ClustersAfterCutData data)
    {
        if (data == null || data.clusters == null) return string.Empty;
        var parts = new List<string>(data.clusters.Length);
        for (int i = 0; i < data.clusters.Length; i++)
        {
            var c = data.clusters[i];
            if (c?.cells == null || c.cells.Length == 0)
            {
                parts.Add("0:0");
                continue;
            }
            var sorted = c.cells.ToArray();
            System.Array.Sort(sorted);
            int min = sorted[0];
            int hash = 0;
            for (int j = 0; j < sorted.Length; j++)
            {
                unchecked { hash = hash * 31 + sorted[j]; }
            }
            parts.Add($"{min}:{sorted.Length}:{hash}");
        }
        parts.Sort();
        return string.Join("|", parts);
    }

    private Dictionary<Vector3Int, Color> BuildUniformColors(Tilemap tilemap)
    {
        Dictionary<Vector3Int, Color> colors = new Dictionary<Vector3Int, Color>();
        foreach (var pos in GetLandTiles(tilemap))
        {
            colors[pos] = initialBaseColor;
        }
        return colors;
    }

    private void StartIncrementalRecolor(Tilemap tilemap, Dictionary<Vector3Int, Color> newColors)
    {
        if (recolorCoroutine != null)
        {
            StopCoroutine(recolorCoroutine);
            recolorCoroutine = null;
        }
        recolorCoroutine = StartCoroutine(RecolorTilesIncrementally(tilemap, newColors));
    }

    private IEnumerator RecolorTilesIncrementally(Tilemap tilemap, Dictionary<Vector3Int, Color> newTileColors)
    {
        int ops = 0;
        // 应用变化/新增
        foreach (var kv in newTileColors)
        {
            if (!lastTileColors.TryGetValue(kv.Key, out var prev) || prev != kv.Value)
            {
                tilemap.SetTileFlags(kv.Key, TileFlags.None);
                tilemap.SetColor(kv.Key, kv.Value);
                lastTileColors[kv.Key] = kv.Value;
            }
            ops++;
            if (ops % recolorBatchSize == 0) yield return null;
        }

        // 清理不再需要着色的
        var toClear = new List<Vector3Int>();
        foreach (var old in lastTileColors)
        {
            if (!newTileColors.ContainsKey(old.Key))
            {
                toClear.Add(old.Key);
            }
        }
        foreach (var pos in toClear)
        {
            tilemap.SetTileFlags(pos, TileFlags.None);
            tilemap.SetColor(pos, Color.white);
            lastTileColors.Remove(pos);
            ops++;
            if (ops % recolorBatchSize == 0) yield return null;
        }
    }
    
    /// <summary>
    /// 后台生态区计算协程
    /// </summary>
    private IEnumerator BackgroundEcoZoneCalculation()
    {
        // Debug.Log("🔄 启动后台生态区计算...");
        
        while (true)
        {
            yield return new WaitForSeconds(2.0f); // 每2秒检查一次
            
            try
            {
                // 检查是否有必要的数据
                var tilemap = GetTilemap();
                if (tilemap == null)
                {
                    continue;
                }
                
                // 获取Cells
                GetCellsFromGameManager();
                if (cells.Count == 0)
                {
                    continue;
                }
                
                // 分配Tiles给Cells
                if (cellTileAssignment.Count == 0)
                {
                    AssignTilesToCells(tilemap);
                }
                
                // 检查是否有新的簇数据
                var newClusterData = LoadClustersData();
                bool hasNewData = newClusterData != null &&
                                   newClusterData.clusters != null &&
                                   newClusterData.clusters.Length > 0;

                bool dataChanged = false;
                if (hasNewData)
                {
                    string newSig = BuildClustersSignature(newClusterData);
                    bool clustersChanged = cachedClustersSignature == null || cachedClustersSignature != newSig;
                    bool costChanged = cachedClusterData != null && cachedClusterData.cost != newClusterData.cost;
                    if (clustersChanged || (updateColorsOnCostChange && costChanged))
                    {
                        dataChanged = true;
                        cachedClusterData = newClusterData;
                        cachedClustersSignature = newSig;
                    }
                }
                else if (cachedClusterData != null || !string.IsNullOrEmpty(cachedClustersSignature))
                {
                    dataChanged = true;
                    cachedClusterData = null;
                    cachedClustersSignature = null;
                }
                
                // 如果数据有变化，重新计算颜色
                if (dataChanged)
                {
                    // Debug.Log("🔄 检测到簇数据变化，重新计算生态区颜色...");
                    
                    Dictionary<Vector3Int, Color> newColors;
                    if (hasNewData)
                    {
                        newColors = BuildColorsByClusters(tilemap, newClusterData);
                        // Debug.Log($"✅ 使用簇数据计算颜色，共{newColors.Count}个瓦片");
                    }
                    else
                    {
                        newColors = BuildUniformColors(tilemap);
                        // Debug.Log($"✅ 使用统一颜色，共{newColors.Count}个瓦片");
                    }
                    
                    // 更新缓存
                    cachedTileColors.Clear();
                    foreach (var kv in newColors)
                    {
                        cachedTileColors[kv.Key] = kv.Value;
                    }
                    
                    // 如果当前正在显示，立即更新显示
                    if (isHighlightVisible)
                    {
                        StartIncrementalRecolor(tilemap, newColors);
                    }
                }
                
                isDataInitialized = true;
            }
            catch (System.Exception ex)
            {
                // Debug.LogWarning($"⚠️ 后台生态区计算出错: {ex.Message}");
            }
        }
    }
}


