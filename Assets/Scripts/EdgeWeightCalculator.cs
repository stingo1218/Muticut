using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Tilemaps;
using System.Linq;
using TMPro;

/// <summary>
/// Edge权重计算器 - 计算每个edge的总权重
/// </summary>
public class EdgeWeightCalculator : MonoBehaviour
{
    [Header("计算设置")]
    [SerializeField] private bool calculateOnStart = true;
    [SerializeField] private float calculateDelay = 1f;
    
    [Header("权重设置")]
    [SerializeField] private bool useGameManagerWeights = true; // 是否使用GameManager的权重设置
    
    private void Start()
    {
        // Debug.Log("🚀 EdgeWeightCalculator 已启动");
        if (calculateOnStart)
        {
            // Debug.Log($"⏰ 将在 {calculateDelay} 秒后自动计算权重");
            Invoke(nameof(CalculateAllEdgeWeights), calculateDelay);
        }
        else
        {
            // Debug.Log("⚠️ 自动计算已禁用，请手动触发");
        }
    }
    
    [ContextMenu("计算所有Edge权重")]
    public void CalculateAllEdgeWeights()
    {
        // Debug.Log("🔢 开始计算所有Edge权重...");
        
        // 获取GameManager
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            // Debug.LogError("❌ 无法找到GameManager");
            return;
        }
        
        // Debug.Log("✅ 找到GameManager");
        
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
            // Debug.LogError("❌ 无法找到TerrainManager");
            return;
        }
        
        // Debug.Log("✅ 找到TerrainManager");
        
        // 获取Tilemap
        var tilemapProperty = terrainManager.GetType().GetProperty("tilemap");
        Tilemap tilemap = null;
        if (tilemapProperty != null)
        {
            tilemap = tilemapProperty.GetValue(terrainManager) as Tilemap;
        }
        
        if (tilemap == null)
        {
            // Debug.LogError("❌ 无法获取Tilemap");
            return;
        }
        
        // Debug.Log("✅ 找到Tilemap");
        
        // 获取所有edges
        var edgesField = typeof(GameManager).GetField("_edges", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Dictionary<(Cell, Cell), (LineRenderer, int, TextMeshProUGUI, GameObject)> edges = null;
        if (edgesField != null)
        {
            edges = edgesField.GetValue(gameManager) as Dictionary<(Cell, Cell), (LineRenderer, int, TextMeshProUGUI, GameObject)>;
        }
        
        if (edges == null || edges.Count == 0)
        {
            // Debug.LogWarning("⚠️ 没有找到edges");
            return;
        }
        
        // Debug.Log($"📊 找到 {edges.Count} 个edges，开始计算权重...");
        
        // 计算每个edge的权重
        int edgeCount = 0;
        int totalWeight = 0;
        
        foreach (var edgePair in edges)
        {
            var edgeKey = edgePair.Key;
            Cell cellA = edgeKey.Item1;
            Cell cellB = edgeKey.Item2;
            
            if (cellA == null || cellB == null) continue;
            
            string edgeName = $"edge{cellA.Number}_{cellB.Number}";
            
            // 检测瓦片
            var crossedTiles = GetTilesCrossedByLine(cellA.transform.position, cellB.transform.position, tilemap);
            
            if (crossedTiles.Count > 0)
            {
                edgeCount++;
                int edgeWeight = CalculateEdgeWeight(crossedTiles, terrainManager);
                totalWeight += edgeWeight;
                
                // Debug.Log($"📊 {edgeName}: {crossedTiles.Count}个tiles, 权重 = {edgeWeight}");
            }
        }
        
        // Debug.Log($"✅ 计算完成！共 {edgeCount} 个edges，总权重 = {totalWeight}");
    }
    
    [ContextMenu("立即计算权重")]
    public void CalculateWeightsImmediately()
    {
        // Debug.Log("⚡ 立即计算权重...");
        CalculateAllEdgeWeights();
    }
    
    [ContextMenu("强制更新所有Edge权重")]
    public void ForceUpdateAllEdgeWeights()
    {
        // Debug.Log("🔄 强制更新所有Edge权重...");
        
        // 获取GameManager
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            // Debug.LogError("❌ 无法找到GameManager");
            return;
        }
        
        // 获取所有edges
        var edgesField = typeof(GameManager).GetField("_edges", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Dictionary<(Cell, Cell), (LineRenderer, int, TextMeshProUGUI, GameObject)> edges = null;
        if (edgesField != null)
        {
            edges = edgesField.GetValue(gameManager) as Dictionary<(Cell, Cell), (LineRenderer, int, TextMeshProUGUI, GameObject)>;
        }
        
        if (edges == null || edges.Count == 0)
        {
            // Debug.LogWarning("⚠️ 没有找到edges");
            return;
        }
        
        // Debug.Log($"📊 找到 {edges.Count} 个edges，开始强制更新权重...");
        
        int updatedCount = 0;
        
        foreach (var edgePair in edges)
        {
            var edgeKey = edgePair.Key;
            Cell cellA = edgeKey.Item1;
            Cell cellB = edgeKey.Item2;
            
            if (cellA == null || cellB == null) continue;
            
            string edgeName = $"edge{cellA.Number}_{cellB.Number}";
            
            // 使用GameManager的方法重新计算权重
            int newWeight = gameManager.GetEdgeWeight(cellA, cellB);
            
            // 更新edge的显示
            gameManager.CreateOrUpdateEdge(cellA, cellB, newWeight);
            
            updatedCount++;
            // Debug.Log($"🔄 {edgeName}: 权重已更新为 {newWeight}");
        }
        
        // Debug.Log($"✅ 强制更新完成！共更新 {updatedCount} 个edges");
    }
    
    [ContextMenu("比较权重计算")]
    public void CompareWeightCalculations()
    {
        // Debug.Log("🔍 开始比较权重计算...");
        
        // 获取GameManager
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            // Debug.LogError("❌ 无法找到GameManager");
            return;
        }
        
        // 获取所有edges
        var edgesField = typeof(GameManager).GetField("_edges", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Dictionary<(Cell, Cell), (LineRenderer, int, TextMeshProUGUI, GameObject)> edges = null;
        if (edgesField != null)
        {
            edges = edgesField.GetValue(gameManager) as Dictionary<(Cell, Cell), (LineRenderer, int, TextMeshProUGUI, GameObject)>;
        }
        
        if (edges == null || edges.Count == 0)
        {
            // Debug.LogWarning("⚠️ 没有找到edges");
            return;
        }
        
        // Debug.Log($"📊 找到 {edges.Count} 个edges，开始比较权重计算...");
        
        int matchCount = 0;
        int totalCount = 0;
        
        foreach (var edgePair in edges)
        {
            var edgeKey = edgePair.Key;
            Cell cellA = edgeKey.Item1;
            Cell cellB = edgeKey.Item2;
            
            if (cellA == null || cellB == null) continue;
            
            string edgeName = $"edge{cellA.Number}_{cellB.Number}";
            
            // GameManager的权重
            int gameManagerWeight = gameManager.GetEdgeWeight(cellA, cellB);
            
            // EdgeWeightCalculator的权重
            var tilemapProperty = gameManager.GetType().GetField("terrainManager", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            MonoBehaviour terrainManager = null;
            if (tilemapProperty != null)
            {
                terrainManager = tilemapProperty.GetValue(gameManager) as MonoBehaviour;
            }
            
            Tilemap tilemap = null;
            if (terrainManager != null)
            {
                var tilemapProp = terrainManager.GetType().GetProperty("tilemap");
                if (tilemapProp != null)
                {
                    tilemap = tilemapProp.GetValue(terrainManager) as Tilemap;
                }
            }
            
            int calculatorWeight = 0;
            if (tilemap != null)
            {
                var crossedTiles = GetTilesCrossedByLine(cellA.transform.position, cellB.transform.position, tilemap);
                calculatorWeight = CalculateEdgeWeight(crossedTiles, terrainManager);
            }
            
            totalCount++;
            
            if (gameManagerWeight == calculatorWeight)
            {
                matchCount++;
                // Debug.Log($"✅ {edgeName}: GameManager={gameManagerWeight}, Calculator={calculatorWeight} ✓");
            }
            else
            {
                // Debug.LogWarning($"❌ {edgeName}: GameManager={gameManagerWeight}, Calculator={calculatorWeight} ✗");
            }
        }
        
        // Debug.Log($"📊 比较完成！匹配: {matchCount}/{totalCount} ({matchCount * 100f / totalCount:F1}%)");
    }
    
    /// <summary>
    /// 计算单个edge的权重
    /// </summary>
    private int CalculateEdgeWeight(HashSet<Vector3Int> tiles, MonoBehaviour terrainManager)
    {
        int totalWeight = 0;
        
        foreach (Vector3Int tilePos in tiles)
        {
            // 获取生物群系权重
            int biomeType = GetBiomeUsingMap(terrainManager, tilePos);
            int tileWeight = CalculateLevelBasedWeight(biomeType);
            totalWeight += tileWeight;
            
            // 移除详细的瓦片输出
            // Debug.Log($"  🎯 瓦片{tilePos}: 生物群系{biomeType}({GetEnglishBiomeName(biomeType)}), 权重{tileWeight}");
        }
        
        return totalWeight;
    }
    
    /// <summary>
    /// 获取线段经过的瓦片 - 使用正确的检测方法
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
            
            // 关键：检查瓦片是否存在
            if (tilemap.HasTile(tilePos))
            {
                crossedTiles.Add(tilePos);
            }
        }
        
        return crossedTiles;
    }
    
    /// <summary>
    /// 使用映射表获取生物群系
    /// </summary>
    private int GetBiomeUsingMap(MonoBehaviour terrainManager, Vector3Int tilePos)
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
            
            // Debug.LogWarning($"无法使用映射表获取瓦片 {tilePos} 的生物群系");
            return -1;
        }
        catch (System.Exception)
        {
            // Debug.LogWarning($"获取生物群系时出错: {ex.Message}");
            return -1;
        }
    }
    
    /// <summary>
    /// 获取生物群系权重
    /// </summary>
    private int CalculateLevelBasedWeight(int biomeType)
    {
        if (useGameManagerWeights)
        {
            // 使用GameManager的权重设置
            GameManager gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                return gameManager.CalculateLevelBasedWeight(biomeType);
            }
        }
        
        // 如果无法获取GameManager，使用默认权重设置
        // 这些权重值应该与GameManager中的TerrainWeights保持一致
        switch (biomeType)
        {
            case 0: return -8;   // 深水
            case 1: return 3;    // 浅水
            case 2: return 4;    // 平地沙漠1
            case 3: return 4;    // 平地沙漠2
            case 4: return 5;    // 平地草原
            case 5: return -12;  // 平地稀疏树木1
            case 6: return -12;  // 平地稀疏树木2
            case 7: return -6;   // 平地森林
            case 8: return -6;   // 平地沼泽森林
            case 9: return -15;  // 丘陵沙漠
            case 10: return -15; // 丘陵草原
            case 11: return -15; // 丘陵森林
            case 12: return -15; // 丘陵针叶林
            case 13: return -10; // 山地沙漠
            case 14: return -10; // 山地灌木丛1
            case 15: return -10; // 山地灌木丛2
            case 16: return -10; // 山地高山1
            case 17: return -10; // 山地高山2
            case 18: return -10; // 山地不可通行1
            case 19: return -10; // 山地不可通行2
            case 20: return -8;  // 湖泊1
            case 21: return -8;  // 湖泊2
            case 22: return -8;  // 湖泊3
            case 23: return -8;  // 湖泊4
            case 24: return -20; // 火山
            case 25: return 0;   // 巢穴
            case 26: return 0;   // 雪地巢穴
            case 27: return 0;   // 沙漠巢穴
            case -1: return 0;   // 未知生物群系
            default: return 0;   // 默认权重
        }
    }
    
    /// <summary>
    /// 获取英文生物群系名称
    /// </summary>
    private string GetEnglishBiomeName(int biomeType)
    {
        switch (biomeType)
        {
            case 0: return "DeepWater";
            case 1: return "ShallowWater";
            case 2: return "FlatDesert1";
            case 3: return "FlatDesert2";
            case 4: return "FlatGrass";
            case 5: return "FlatSparseTrees1";
            case 6: return "FlatSparseTrees2";
            case 7: return "FlatForest";
            case 8: return "FlatForestSwampy";
            case 9: return "HillDesert";
            case 10: return "HillGrass";
            case 11: return "HillForest";
            case 12: return "HillForestNeedleleaf";
            case 13: return "MountainDesert";
            case 14: return "MountainShrubland1";
            case 15: return "MountainShrubland2";
            case 16: return "MountainAlpine1";
            case 17: return "MountainAlpine2";
            case 18: return "MountainImpassable1";
            case 19: return "MountainImpassable2";
            case 20: return "Lake1";
            case 21: return "Lake2";
            case 22: return "Lake3";
            case 23: return "Lake4";
            case 24: return "Volcano";
            case 25: return "Lair";
            case 26: return "LairSnow";
            case 27: return "LairDesert";
            case -1: return "Unknown";
            default: return $"Unknown({biomeType})";
        }
    }
} 