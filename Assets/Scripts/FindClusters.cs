using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

/// <summary>
/// 快速查找和计算切割后的clusters信息
/// 利用GameManager中现有的连通分量计算方法
/// </summary>
public class FindClusters : MonoBehaviour
{
    [Header("引用设置")]
    [SerializeField] private MonoBehaviour gameManager; // GameManager引用
    
    [Header("调试设置")]
    [SerializeField] private bool enableDebugLogs = true; // 是否启用调试日志
    
    [Header("簇信息")]
    [SerializeField] private int currentClusterCount = 1; // 当前簇数量
    [SerializeField] private List<ClusterInfo> clusterInfos = new List<ClusterInfo>(); // 簇信息列表
    
    /// <summary>
    /// 簇信息结构
    /// </summary>
    [System.Serializable]
    public class ClusterInfo
    {
        public int clusterId; // 簇ID
        public List<int> cellNumbers = new List<int>(); // 包含的Cell编号
        public Color clusterColor; // 簇颜色
        public int cellCount => cellNumbers.Count; // Cell数量
        
        public ClusterInfo(int id, List<int> cells, Color color)
        {
            clusterId = id;
            cellNumbers = new List<int>(cells);
            clusterColor = color;
        }
    }
    
    // 预定义颜色数组
    private Color[] predefinedColors = {
        new Color(1f, 0.5f, 0.5f, 0.7f), // 淡红色
        new Color(0.5f, 1f, 0.5f, 0.7f), // 淡绿色
        new Color(0.5f, 0.5f, 1f, 0.7f), // 淡蓝色
        new Color(1f, 1f, 0.5f, 0.7f),   // 淡黄色
        new Color(1f, 0.5f, 1f, 0.7f),   // 淡紫色
        new Color(0.5f, 1f, 1f, 0.7f),   // 淡青色
        new Color(1f, 0.7f, 0.5f, 0.7f), // 淡橙色
        new Color(0.7f, 0.5f, 1f, 0.7f), // 淡紫罗兰
        new Color(0.5f, 0.8f, 0.5f, 0.7f), // 淡青绿
        new Color(1f, 0.8f, 0.8f, 0.7f)   // 淡粉红
    };
    
    private void Start()
    {
        // 自动查找GameManager
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
            if (gameManager != null && enableDebugLogs)
            {
                Debug.Log($"🔍 FindClusters: 自动找到GameManager: {gameManager.name}");
            }
        }
        
        if (gameManager == null)
        {
            Debug.LogError("❌ FindClusters: 无法找到GameManager！");
            return;
        }
        
        // 初始化簇信息
        UpdateClusterInfo();
    }
    
    /// <summary>
    /// 更新簇信息（公共方法，供外部调用）
    /// </summary>
    public void UpdateClusterInfo()
    {
        if (gameManager == null)
        {
            Debug.LogError("❌ FindClusters: GameManager为null，无法更新簇信息");
            return;
        }
        
        try
        {
            // 获取切割边信息
            var playerCutEdgesField = gameManager.GetType().GetField("playerCutEdges", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (playerCutEdgesField == null)
            {
                Debug.LogError("❌ FindClusters: 无法获取playerCutEdges字段");
                return;
            }
            
            var playerCutEdges = playerCutEdgesField.GetValue(gameManager);
            if (playerCutEdges == null)
            {
                Debug.LogError("❌ FindClusters: playerCutEdges为null");
                return;
            }
            
            // 获取切割边数量
            var countProperty = playerCutEdges.GetType().GetProperty("Count");
            if (countProperty == null)
            {
                Debug.LogError("❌ FindClusters: 无法获取Count属性");
                return;
            }
            
            int cutEdgesCount = (int)countProperty.GetValue(playerCutEdges);
            
            if (enableDebugLogs)
            {
                Debug.Log($"🔍 FindClusters: 检测到 {cutEdgesCount} 条切割边");
            }
            
            // 调用GameManager的连通分量计算方法
            var calculateMethod = gameManager.GetType().GetMethod("CalculateNumberOfConnectedComponents", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (calculateMethod == null)
            {
                Debug.LogError("❌ FindClusters: 无法找到CalculateNumberOfConnectedComponents方法");
                return;
            }
            
            // 计算连通分量数量
            int componentCount = (int)calculateMethod.Invoke(gameManager, new object[] { playerCutEdges });
            currentClusterCount = componentCount;
            
            if (enableDebugLogs)
            {
                Debug.Log($"🔍 FindClusters: 计算得到 {componentCount} 个连通分量");
            }
            
            // 获取详细的簇信息
            GetDetailedClusterInfo();
            
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ FindClusters: 更新簇信息时出错: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 获取详细的簇信息
    /// </summary>
    private void GetDetailedClusterInfo()
    {
        clusterInfos.Clear();
        
        // 获取所有cells
        var cellsField = gameManager.GetType().GetField("_cells", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (cellsField == null)
        {
            Debug.LogError("❌ FindClusters: 无法获取_cells字段");
            return;
        }
        
        var cells = cellsField.GetValue(gameManager);
        if (cells == null)
        {
            Debug.LogError("❌ FindClusters: _cells为null");
            return;
        }
        
        // 获取GetAllCellsInSameComponent方法
        var getAllCellsMethod = gameManager.GetType().GetMethod("GetAllCellsInSameComponent", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (getAllCellsMethod == null)
        {
            Debug.LogError("❌ FindClusters: 无法找到GetAllCellsInSameComponent方法");
            return;
        }
        
        // 用于记录已处理的cell，避免重复计算
        HashSet<int> processedCells = new HashSet<int>();
        
        if (cells is System.Collections.IEnumerable cellsEnumerable)
        {
            int clusterId = 0;
            
            foreach (var cellObj in cellsEnumerable)
            {
                var cellNumberField = cellObj.GetType().GetField("Number", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (cellNumberField != null)
                {
                    int cellNumber = (int)cellNumberField.GetValue(cellObj);
                    
                    // 如果这个cell还没有被处理过
                    if (!processedCells.Contains(cellNumber))
                    {
                        // 获取与这个cell在同一个连通分量中的所有cells
                        var componentCells = getAllCellsMethod.Invoke(gameManager, new object[] { cellObj });
                        
                        if (componentCells is HashSet<Cell> cellSet)
                        {
                            List<int> cellNumbers = new List<int>();
                            
                            foreach (var cell in cellSet)
                            {
                                cellNumbers.Add(cell.Number);
                                processedCells.Add(cell.Number);
                            }
                            
                            // 创建簇信息
                            Color clusterColor = predefinedColors[clusterId % predefinedColors.Length];
                            var clusterInfo = new ClusterInfo(clusterId, cellNumbers, clusterColor);
                            clusterInfos.Add(clusterInfo);
                            
                            if (enableDebugLogs)
                            {
                                Debug.Log($"🔍 FindClusters: 簇 {clusterId} 包含 {cellNumbers.Count} 个cells: [{string.Join(", ", cellNumbers)}]");
                            }
                            
                            clusterId++;
                        }
                    }
                }
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"🔍 FindClusters: 完成簇信息获取，共 {clusterInfos.Count} 个簇");
        }
    }
    
    /// <summary>
    /// 获取当前簇数量
    /// </summary>
    public int GetCurrentClusterCount()
    {
        return currentClusterCount;
    }
    
    /// <summary>
    /// 获取所有簇信息
    /// </summary>
    public List<ClusterInfo> GetAllClusterInfos()
    {
        return new List<ClusterInfo>(clusterInfos);
    }
    
    /// <summary>
    /// 获取指定cell所在的簇信息
    /// </summary>
    public ClusterInfo GetClusterInfoForCell(int cellNumber)
    {
        foreach (var clusterInfo in clusterInfos)
        {
            if (clusterInfo.cellNumbers.Contains(cellNumber))
            {
                return clusterInfo;
            }
        }
        return null;
    }
    
    /// <summary>
    /// 检查是否有新的簇形成（与之前的状态比较）
    /// </summary>
    public bool HasNewClusters(int previousClusterCount)
    {
        return currentClusterCount > previousClusterCount;
    }
    
    /// <summary>
    /// 获取簇的颜色映射
    /// </summary>
    public Dictionary<int, Color> GetClusterColorMapping()
    {
        var colorMapping = new Dictionary<int, Color>();
        foreach (var clusterInfo in clusterInfos)
        {
            colorMapping[clusterInfo.clusterId] = clusterInfo.clusterColor;
        }
        return colorMapping;
    }
    
    /// <summary>
    /// 获取cell到簇的映射
    /// </summary>
    public Dictionary<int, int> GetCellToClusterMapping()
    {
        var mapping = new Dictionary<int, int>();
        foreach (var clusterInfo in clusterInfos)
        {
            foreach (int cellNumber in clusterInfo.cellNumbers)
            {
                mapping[cellNumber] = clusterInfo.clusterId;
            }
        }
        return mapping;
    }
    
    /// <summary>
    /// 调试方法：打印当前簇信息
    /// </summary>
    [ContextMenu("打印当前簇信息")]
    public void DebugPrintClusterInfo()
    {
        Debug.Log($"🔍 FindClusters 调试信息:");
        Debug.Log($"  - 当前簇数量: {currentClusterCount}");
        Debug.Log($"  - 簇信息数量: {clusterInfos.Count}");
        
        for (int i = 0; i < clusterInfos.Count; i++)
        {
            var info = clusterInfos[i];
            Debug.Log($"  - 簇 {info.clusterId}: {info.cellCount} 个cells, 颜色: {info.clusterColor}");
            Debug.Log($"    Cells: [{string.Join(", ", info.cellNumbers)}]");
        }
    }
    
    /// <summary>
    /// 强制刷新簇信息
    /// </summary>
    [ContextMenu("强制刷新簇信息")]
    public void ForceRefreshClusterInfo()
    {
        Debug.Log("🔄 FindClusters: 强制刷新簇信息...");
        UpdateClusterInfo();
        DebugPrintClusterInfo();
    }
}
