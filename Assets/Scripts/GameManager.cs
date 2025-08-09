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
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    [SerializeField] private Cell _urbanCellPrefab; // 陆地单元格预制体 (Urban)
    [SerializeField] private Cell _portCellPrefab;  // 水面单元格预制体 (Port)
    [SerializeField] private MonoBehaviour terrainManager; // 地形管理器引用

    [HideInInspector] public bool hasgameFinished;

    [SerializeField] private int _cellNumbers = 10; // 要生成的单元格数量

    private List<Cell> _cells = new List<Cell>(); // 改用List存储单元格，更灵活

    private Cell startCell;
    private LineRenderer previewEdge;

    [SerializeField] private Material previewEdgeMaterial; // 预览线材质
    [SerializeField] private Material _lineMaterial; // 用于连线的材质
    [SerializeField] private float lineWidth = 0.1f; // 线条宽度
    [SerializeField] private Material _eraseLineMaterial; // 用于擦除线的材质
    [SerializeField] private GameObject WeightPrefab; // 用于权重背景的BG prefab
    private Dictionary<(Cell, Cell), (LineRenderer renderer, int weight, TextMeshProUGUI tmp, GameObject bg)> _edges = new Dictionary<(Cell, Cell), (LineRenderer, int, TextMeshProUGUI, GameObject)>(); // 存储所有的连线
    private Transform linesRoot; // 用于组织所有连线的父物体

    private bool isErasing = false;
    private LineRenderer eraseLineRenderer; // 用于显示擦除线

    private List<Vector2> erasePath = new List<Vector2>();

    private const float EPSILON = 1e-6f; // 用于浮点数比较

    [SerializeField]
    private bool useWeightedEdges = true; // 控制是否显示边的权重

    // 唯一权重缓存
    private Dictionary<(Cell, Cell), int> _edgeWeightCache = new Dictionary<(Cell, Cell), int>();
    private const int maxEdgeWeight = 30; // 权重值范围为 [-30, 30]

    private Button debugButton;

    private HashSet<(Cell, Cell)> _initialEdges = new HashSet<(Cell, Cell)>(); // 记录初始边
    private HashSet<(Cell, Cell)> playerCutEdges = new HashSet<(Cell, Cell)>();
    
    // 关卡与特性（尽量只改GameManager）
    [Header("关卡生成设置")]
    [SerializeField] public int levelIndex = 1;
    

    
    [Header("计时器（可选）")]
    [SerializeField] private bool enableTimer = false;
    [SerializeField] private float timeLimitSeconds = 120f;
    private float remainingTime = 0f;
    private TextMeshProUGUI timerText;
    
    [Header("切割次数限制")]
    [SerializeField] private bool enableCutLimit = true;
    [SerializeField] private int baseCutLimit = 8; // 基础切割次数
    [SerializeField] private float cutLimitReductionRate = 0.8f; // 每关卡减少的系数
    [SerializeField] private TextMeshProUGUI cutLimitText; // 直接拖拽UI组件
    private int currentCutLimit = 0;
    private int remainingCuts = 0;
    
    // 回退功能相关
    [System.Serializable]
    public class GameState
    {
        public HashSet<(Cell, Cell)> cutEdges;
        public int currentCost;
        
        public GameState()
        {
            cutEdges = new HashSet<(Cell, Cell)>();
            currentCost = 0;
        }
        
        public GameState(HashSet<(Cell, Cell)> cutEdges, int currentCost)
        {
            this.cutEdges = new HashSet<(Cell, Cell)>(cutEdges);
            this.currentCost = currentCost;
        }
    }
    
    // JSON DTOs for clusters_after_cut.json
    [System.Serializable]
    private class ClustersAfterCutDataDTO
    {
        public CutEdgeDTO[] cut_edges;
        public int cost;
        public ClusterInfoDTO[] clusters;
        public int cluster_count;
        public string timestamp;
        public int level_index; // 新增：关卡序号
        public string seed;     // 新增：关卡种子
    }
    
    [System.Serializable]
    private class CutEdgeDTO
    {
        public int u;
        public int v;
    }
    
    [System.Serializable]
    private class ClusterInfoDTO
    {
        public int[] cells;
    }
    
    private Stack<GameState> gameStateHistory = new Stack<GameState>();
    private const int MAX_UNDO_STEPS = 20; // 最大回退步数
    
    [Header("UI Controls")]
    public Button ReturnButton; // 回退按钮

    public enum MulticutAlgorithm
    {
        Greedy,
        ILP
    }

    [SerializeField]
    // private MulticutAlgorithm multicutAlgorithm = MulticutAlgorithm.Greedy;

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

    [Header("Hint高亮设置")]
    [SerializeField] private Material highlightEdgeMaterial;
    [SerializeField] private Color highlightEdgeColor = Color.green; // Hint功能用的高亮材质
    
    [ContextMenu("测试边颜色变化")]
    public void TestEdgeColorChange()
    {
        UnityEngine.Debug.Log("开始测试边颜色变化...");
        foreach (var edgeInfo in _edges.Values)
        {
            edgeInfo.renderer.startColor = Color.red;
            edgeInfo.renderer.endColor = Color.red;
            UnityEngine.Debug.Log($"设置边颜色为红色，当前材质: {edgeInfo.renderer.material.name}");
        }
    }
    
    [ContextMenu("测试Victory Panel")]
    public void TestVictoryPanel()
    {
        UnityEngine.Debug.Log("🧪 手动测试Victory Panel...");
        ShowVictoryPanel();
    }

    private TextMeshProUGUI costText;
    private int optimalCost = 0;
    private TextMeshProUGUI levelDisplayText;
    
    [Header("生态区高亮")]
    [SerializeField] private ClusterHighlighter clusterHighlighter; // 生态区高亮组件（可选，自动查找）

    public enum GameDifficulty { Easy, Medium, Hard }
    
    [Header("游戏难度设置")]
    [SerializeField] private GameDifficulty gameDifficulty = GameDifficulty.Medium;
    
    [Header("获胜条件与UI")]
    [SerializeField] private GameObject victoryPanel; // 获胜通知Panel
    [SerializeField] private UnityEngine.UI.Button continueButton; // 继续按钮 (可选，会自动从Panel中查找)
    private bool hasOptimalCost = false; // 是否已获得最优cost
    private bool hasShownVictoryPanel = false; // 防止重复弹出
    
    [Header("时间炸弹设置")]
    private bool enableTimeBomb = false; // 由难度系统自动控制
    [Range(0f,1f)] [SerializeField] private float timeBombChance = 0.12f;
    [SerializeField] private float timeBombPenaltySeconds = 5f; // 时间炸弹惩罚秒数
    [SerializeField] private Color timeBombEdgeColor = Color.red;
    [SerializeField] private float timeBombEdgeWidth = 0.3f; // 时间炸弹边的宽度（加粗）
    private HashSet<(Cell, Cell)> timeBombEdges = new HashSet<(Cell, Cell)>();

    [Header("节点生成设置")]
    [SerializeField] private bool enableTerrainCheck = true; // 是否启用地形检查，确保节点生成在陆地上

    [Header("权重平衡")]
    [Tooltip("目标负权重边比例，低于此比例会对所有边整体左移权重，避免最优cost为0的图")] 
    [Range(0f, 0.9f)] [SerializeField] private float targetNegativeEdgeRatio = 0.35f;
    [Tooltip("至少需要的负权重边数量")] 
    [SerializeField] private int minNegativeEdges = 3;


    /// <summary>
    /// 计算基于关卡的基础权重（忽略地形类型参数）
    /// </summary>
    /// <param name="unusedParam">未使用的参数（保持接口兼容性）</param>
    /// <returns>权重值</returns>
    public int CalculateLevelBasedWeight(int unusedParam = 0)
    {
        // 基于关卡号计算基础权重，确保不为0
        int baseWeight = levelIndex * 2; // 关卡1=2, 关卡2=4, 关卡3=6...
        
        // 添加随机变化，避免所有权重相同
        int randomOffset = UnityEngine.Random.Range(-levelIndex, levelIndex + 1);
        
        int finalWeight = baseWeight + randomOffset;
        
        // 确保权重在合理范围内且不为0
        finalWeight = Mathf.Clamp(finalWeight, -maxEdgeWeight, maxEdgeWeight);
        if (finalWeight == 0) finalWeight = levelIndex % 2 == 0 ? 1 : -1;
        
        return finalWeight;
    }

    // 更新时间炸弹边的外观（加粗+变色）
    private void UpdateTimeBombEdgeAppearance((Cell, Cell) key)
    {
        if (!_edges.TryGetValue(key, out var data)) return;
        var line = data.Item1;
        if (line == null) return;

        if (timeBombEdges.Contains(key))
        {
            // 时间炸弹边：红色+加粗
            line.startColor = timeBombEdgeColor;
            line.endColor = timeBombEdgeColor;
            line.startWidth = timeBombEdgeWidth;
            line.endWidth = timeBombEdgeWidth;
        }
        else
        {
            // 普通边：恢复正常
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            // 所有普通边都使用黑色
            line.startColor = Color.black;
            line.endColor = Color.black;
        }
    }

    private void Awake()
    {
        Instance = this;
        
        // 清空clusters_after_cut.json文件，避免开局时出现二次高亮
        ClearClustersFile();
        
        // 确保在重新开始时清理旧的边缘
        RemoveAllEdges();
        
        // 调试信息
        // UnityEngine.Debug.Log($"🔍 GameManager.Awake() - _urbanCellPrefab: {(_urbanCellPrefab != null ? "已设置" : "为 null")}");
        // UnityEngine.Debug.Log($"🔍 GameManager.Awake() - _portCellPrefab: {(_portCellPrefab != null ? "已设置" : "为 null")}");
        // UnityEngine.Debug.Log($"🔍 GameManager.Awake() - _cellNumbers: {_cellNumbers}");
        
        // 生成地形
        GenerateTerrainIfNeeded();
        
        // 设置Camera渲染
        SetupCameraForLineRenderer();
        
        linesRoot = new GameObject("LinesRoot").transform;
        linesRoot.SetParent(transform);
        SpawnLevel(_cellNumbers);
    }

    private void OnDestroy()
    {
        // 确保在GameManager销毁时清理所有边缘
        RemoveAllEdges();
    }

    private void OnApplicationQuit()
    {
        // 确保在应用退出时也清理所有边缘
        RemoveAllEdges();
    }

    private void Start()
    {
        // CreateDebugButton(); // 移除左边HINT按钮
        // CreatePixelHintButton(); // 不再自动生成HintToggle
        // 获取CostText组件
        var costTextObj = GameObject.Find("UICanvas/CostText");
        if (costTextObj != null)
            costText = costTextObj.GetComponent<TextMeshProUGUI>();
        else
            UnityEngine.Debug.LogError("找不到UICanvas下的CostText！");
            
        // 绑定ReturnButton点击事件
        if (ReturnButton == null)
        {
            var returnButtonObj = GameObject.Find("UICanvas/ReturnButton");
            if (returnButtonObj != null)
                ReturnButton = returnButtonObj.GetComponent<Button>();
        }
        
        if (ReturnButton != null)
        {
            ReturnButton.onClick.AddListener(UndoLastAction);
            UpdateReturnButtonState();
        }
        else
        {
            UnityEngine.Debug.LogError("找不到UICanvas下的ReturnButton！");
        }
        // 计时器UI（可选）
        var timerObj = GameObject.Find("UICanvas/TimerText");
        if (timerObj != null)
        {
            timerText = timerObj.GetComponent<TextMeshProUGUI>();
        }
        
        // 切割次数UI（通过Inspector拖拽绑定）
        if (cutLimitText == null)
        {
            UnityEngine.Debug.LogWarning("CutLimitText未在Inspector中绑定，切割次数UI将不会显示");
        }
        
        // 关卡显示UI（自动查找）
        var levelDisplayObj = GameObject.Find("UICanvas/LevelDisplay");
        if (levelDisplayObj != null)
        {
            levelDisplayText = levelDisplayObj.GetComponent<TextMeshProUGUI>();
            if (levelDisplayText != null)
            {
                UpdateLevelDisplay();
                UnityEngine.Debug.Log("自动找到LevelDisplay组件");
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("未找到UICanvas/LevelDisplay，关卡显示将不可用");
        }
        
        // 自动查找ClusterHighlighter组件
        if (clusterHighlighter == null)
        {
            clusterHighlighter = FindFirstObjectByType<ClusterHighlighter>();
            if (clusterHighlighter != null)
            {
                UnityEngine.Debug.Log("自动找到ClusterHighlighter组件");
            }
        }
        
        // 自动查找Victory Panel（如果未在Inspector中设置）
        if (victoryPanel == null)
        {
            // 支持查找未激活对象
            var panelObj = FindInactiveByPath("UICanvas/VictoryPanel");
            if (panelObj == null) panelObj = FindInactiveByPath("Canvas/VictoryPanel");
            if (panelObj == null) panelObj = FindInactiveByName("VictoryPanel");
            if (panelObj != null)
            {
                victoryPanel = panelObj;
                UnityEngine.Debug.Log($"自动找到Victory Panel: {panelObj.name}");
            }
            else
            {
                UnityEngine.Debug.LogWarning("未找到Victory Panel！请在Inspector中设置或确保Panel命名为VictoryPanel");
            }
        }
        
        // 注释掉重复的按钮绑定 - VictoryPanelController已经处理了Continue按钮
        // 避免重复调用NextLevel()导致关卡跳跃问题 (1→3→5)
        /*
        if (victoryPanel != null && continueButton == null)
        {
            continueButton = victoryPanel.GetComponentInChildren<UnityEngine.UI.Button>();
            if (continueButton != null)
            {
                UnityEngine.Debug.Log($"自动找到Continue Button: {continueButton.name}");
            }
        }
        
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueButtonClicked);
        }
        */
        
        // 读取场景选择器设置的难度和关卡
        LoadDifficultyFromSceneSelector();
        
        // 根据难度自动设置功能开关
        ApplyDifficultySettings();

        UpdateOptimalCostByPython();
        
        // 保存游戏初始状态
        SaveGameState();
        
        // 自动输出cell1和cell2连线的地形权重
        if (_cells != null && _cells.Count >= 2)
        {
            var cell1 = _cells[0];
            var cell2 = _cells[1];
            int weight = GetOrCreateEdgeWeight(cell1, cell2);
            // UnityEngine.Debug.Log($"Cell1({cell1.Number})-Cell2({cell2.Number}) 连线地形权重: {weight}");
        }
        
        // 初始化切割次数限制
        if (enableCutLimit)
        {
            currentCutLimit = CalculateCutLimit();
            remainingCuts = currentCutLimit;
            UnityEngine.Debug.Log($"初始切割次数限制: {currentCutLimit}");
        }
    }

    /// <summary>
    /// 从场景选择器读取难度和关卡设置
    /// </summary>
    private void LoadDifficultyFromSceneSelector()
    {
        // 读取难度设置
        if (PlayerPrefs.HasKey("SelectedDifficulty"))
        {
            int difficultyIndex = PlayerPrefs.GetInt("SelectedDifficulty", 1); // 默认Medium
            gameDifficulty = (GameDifficulty)difficultyIndex;
            UnityEngine.Debug.Log($"从场景选择器读取难度: {gameDifficulty}");
        }
        
        // 读取起始关卡
        if (PlayerPrefs.HasKey("StartLevel"))
        {
            levelIndex = PlayerPrefs.GetInt("StartLevel", 1);
            UnityEngine.Debug.Log($"从场景选择器读取起始关卡: {levelIndex}");
        }
        
        // 清除PlayerPrefs，避免下次启动时影响
        PlayerPrefs.DeleteKey("SelectedDifficulty");
        PlayerPrefs.DeleteKey("StartLevel");
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 计算当前关卡的切割次数限制
    /// </summary>
    private int CalculateCutLimit()
    {
        if (!enableCutLimit) return int.MaxValue; // 不限制
        
        // 基础次数 - 关卡增长减少
        int limit = Mathf.Max(3, baseCutLimit - Mathf.RoundToInt((levelIndex - 1) * cutLimitReductionRate));
        
        // 确保至少有3次切割机会
        return Mathf.Max(3, limit);
    }
    
    /// <summary>
    /// 根据选择的难度自动设置功能开关和参数
    /// </summary>
    private void ApplyDifficultySettings()
    {
        switch (gameDifficulty)
        {
            case GameDifficulty.Easy:
                enableCutLimit = true;
                enableTimer = false;
                enableTimeBomb = false;
                UnityEngine.Debug.Log("难度设置: Easy (切割次数限制)");
                break;
                
            case GameDifficulty.Medium:
                enableCutLimit = true;
                enableTimer = true;
                enableTimeBomb = false;
                UnityEngine.Debug.Log("难度设置: Medium (切割次数限制 + 计时器)");
                break;
                
            case GameDifficulty.Hard:
                enableCutLimit = true;
                enableTimer = true;
                enableTimeBomb = true;
                UnityEngine.Debug.Log("难度设置: Hard (切割次数限制 + 计时器 + 时间炸弹)");
                break;
        }
        
        // 应用渐进式难度参数
        ApplyProgressiveDifficulty();
    }
    
    /// <summary>
    /// 应用基于关卡的渐进式难度调整
    /// </summary>
    private void ApplyProgressiveDifficulty()
    {
        // 节点数量随关卡增加
        _cellNumbers = Mathf.Min(30, 8 + levelIndex); // 从8个节点开始，最多30个
        
        // 切割次数逐步减少 (所有难度)
        if (enableCutLimit)
        {
            baseCutLimit = Mathf.Max(3, 10 - levelIndex / 2); // 基础次数随关卡减少
            currentCutLimit = CalculateCutLimit();
            remainingCuts = currentCutLimit;
        }
        
        // 计时器逐渐减少 (Medium和Hard)
        if (enableTimer)
        {
            timeLimitSeconds = Mathf.Max(30f, 180f - levelIndex * 10f); // 从180秒开始，每关减少10秒，最少30秒
            remainingTime = timeLimitSeconds;
        }
        
        // 时间炸弹惩罚变高 (Hard)
        if (enableTimeBomb)
        {
            timeBombPenaltySeconds = 3f + levelIndex * 0.5f; // 每关增加0.5秒惩罚
            timeBombChance = Mathf.Min(0.3f, 0.08f + levelIndex * 0.02f); // 炸弹概率逐渐增加
        }
        
        UnityEngine.Debug.Log($"关卡 {levelIndex} 难度参数: 节点={_cellNumbers}, 切割次数={currentCutLimit}, 计时器={timeLimitSeconds}s, 炸弹惩罚={timeBombPenaltySeconds}s");
    }
    
    /// <summary>
    /// 检查是否达到获胜条件 (当前cost == 最优cost)
    /// </summary>
    private void CheckVictoryCondition()
    {
        int currentCost = GetCurrentCost();
        // 精确匹配最佳cost才算获胜
        if (!hasShownVictoryPanel && hasOptimalCost && currentCost == optimalCost)
        {
            ShowVictoryPanel();
            UnityEngine.Debug.Log($"获胜！当前代价: {currentCost}, 最优代价: {optimalCost}");
        }
    }
    
    /// <summary>
    /// 显示获胜通知Panel
    /// </summary>
    private void ShowVictoryPanel()
    {
        UnityEngine.Debug.Log("ShowVictoryPanel 被调用");
        
        if (victoryPanel != null)
        {
            // 若面板上挂了控制器，则交由控制器显示
            var controller = victoryPanel.GetComponent<VictoryPanelController>();
            if (controller != null)
            {
                controller.Show();
                hasShownVictoryPanel = true;
                if (enableTimer)
                {
                    Time.timeScale = 0f;
                    UnityEngine.Debug.Log("游戏时间已暂停");
                }
                return;
            }
            
            UnityEngine.Debug.Log($"显示Victory Panel: {victoryPanel.name}");
            
            // 确保父级Canvas启用
            var parentCanvas = victoryPanel.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null && !parentCanvas.enabled)
            {
                parentCanvas.enabled = true;
                UnityEngine.Debug.Log($"已启用父Canvas: {parentCanvas.name}");
            }
            
            // 置顶显示
            victoryPanel.transform.SetAsLastSibling();
            
            // 显示并确保可交互
            victoryPanel.SetActive(true);
            var cg = victoryPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
                UnityEngine.Debug.Log("已设置CanvasGroup: alpha=1, interactable=true, blocksRaycasts=true");
            }
            
            // 修正缩放
            var rt = victoryPanel.GetComponent<RectTransform>();
            if (rt != null && rt.localScale == Vector3.zero)
            {
                rt.localScale = Vector3.one;
                UnityEngine.Debug.Log("Panel缩放为0，已重置为Vector3.one");
            }
            
            hasShownVictoryPanel = true;
            
            // 暂停计时器
            if (enableTimer)
            {
                Time.timeScale = 0f; // 暂停游戏时间
                UnityEngine.Debug.Log("游戏时间已暂停");
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("Victory Panel未设置，直接进入下一关");
            // 直接调用NextLevel，而不是通过按钮事件
            Time.timeScale = 1f;
            NextLevel();
        }
    }
    
    /// <summary>
    /// 继续按钮点击事件
    /// </summary>
    private void OnContinueButtonClicked()
    {
        // 恢复游戏时间
        Time.timeScale = 1f;
        
        // 隐藏获胜Panel
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }
        
        UnityEngine.Debug.Log($"玩家选择继续，进入关卡 {levelIndex + 1}");
        NextLevel();
    }
    
    /// <summary>
    /// 返回主菜单按钮点击事件（公开方法，可在Inspector中绑定UI按钮）
    /// </summary>
    public void ReturnToMainMenu()
    {
        // 恢复游戏时间
        Time.timeScale = 1f;
        
        UnityEngine.Debug.Log("玩家选择返回主菜单");
        
        try
        {
            // 尝试加载主菜单场景
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
        catch (System.Exception)
        {
            // 如果MainMenu场景不存在，尝试加载第一个场景（通常是主菜单）
            UnityEngine.Debug.LogWarning("MainMenu场景未找到，尝试加载场景索引0");
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }
    }
    
    /// <summary>
    /// 设置初始切割次数（公开方法，可在Inspector中调用）
    /// </summary>
    /// <param name="initialCuts">初始切割次数</param>
    public void SetInitialCutLimit(int initialCuts)
    {
        if (!enableCutLimit) return;
        
        currentCutLimit = Mathf.Max(1, initialCuts); // 至少1次
        remainingCuts = currentCutLimit;
        
        UnityEngine.Debug.Log($"手动设置切割次数限制: {currentCutLimit}");
        UpdateCutLimitUI();
    }
    
    /// <summary>
    /// 重置切割次数为初始值
    /// </summary>
    public void ResetCutLimit()
    {
        if (!enableCutLimit) return;
        
        remainingCuts = currentCutLimit;
        UnityEngine.Debug.Log($"重置切割次数: {remainingCuts}/{currentCutLimit}");
        UpdateCutLimitUI();
    }

    // 新增：公开方法，供HintToggle绑定
    public void OnHintToggleChanged(bool isOn)
    {
        UnityEngine.Debug.Log($"[HintToggle] 当前值: {isOn}");
        if (isOn)
        {
            // 1. 构造输入数据
            var nodes = _cells.Select(cell => cell.Number).ToList();
            var edgeList = new List<Dictionary<string, object>>();
            foreach (var edge in _edges.Keys)
            {
                int u = edge.Item1.Number;
                int v = edge.Item2.Number;
                int w;
                if (!_edgeWeightCache.TryGetValue(edge, out w))
                {
                    // 回退：即时计算并缓存
                    w = GetOrCreateEdgeWeight(edge.Item1, edge.Item2);
                }
                edgeList.Add(new Dictionary<string, object> { {"u", u}, {"v", v}, {"weight", w} });
            }
            string nodesStr = string.Join(",", nodes);
            string edgesStr = string.Join(",", edgeList.Select(e => $"{{\"u\":{e["u"]},\"v\":{e["v"]},\"weight\":{e["weight"]}}}"));
            string jsonData = $"{{\"nodes\":[{nodesStr}],\"edges\":[{edgesStr}]}}";

            string pythonExe = "python";
            string scriptPath = "Assets/Scripts/multicut_solver.py";
            string inputPath = "input.json";
            string outputPath = "output.json";

            RunPythonMulticut(pythonExe, scriptPath, inputPath, outputPath, jsonData);

            string resultJson = System.IO.File.ReadAllText(outputPath);
            var cutEdges = new List<(Cell, Cell)>();
            int optimalCostLocal = 0;
            try
            {
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
                var costMatch = Regex.Match(resultJson, "\\\"cost\\\"\\s*:\\s*(-?\\d+)");
                if (costMatch.Success)
                    optimalCostLocal = int.Parse(costMatch.Groups[1].Value);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("解析Python输出失败: " + ex.Message);
            }
            HighlightCutEdges(cutEdges, optimalCostLocal);
            UnityEngine.Debug.Log("Hint: Python多割已高亮最佳切割");
        }
        else
        {
            // 恢复所有边的正常外观
            foreach (var edgeKey in _edges.Keys)
            {
                // 重置材质
                if (_edges.TryGetValue(edgeKey, out var edgeInfo))
                {
                    if (_lineMaterial != null)
                    {
                        edgeInfo.renderer.material = _lineMaterial;
                        UnityEngine.Debug.Log($"恢复边材质: {edgeKey.Item1.Number}-{edgeKey.Item2.Number} -> {_lineMaterial.name}");
                    }
                }
                // 恢复正确的颜色和宽度
                UpdateTimeBombEdgeAppearance(edgeKey);
            }
            UnityEngine.Debug.Log("像素Hint Toggle状态: 关闭");
            UpdateCostText();
        }
    }

    // private void CreatePixelHintButton()
    // {
    //     // 查找UICanvas
    //     GameObject canvasObj = GameObject.Find("UICanvas");
    //     if (canvasObj == null)
    //     {
    //         UnityEngine.Debug.LogError("UICanvas未找到，无法创建像素Hint按钮");
    //         return;
    //     }
    //     // 使用Inspector拖引用的Toggle预制体
    //     if (pixelHintTogglePrefab == null)
    //     {
    //         UnityEngine.Debug.LogError("pixelHintTogglePrefab未在Inspector中赋值，请拖入Toggle预制体");
    //         return;
    //     }
    //     // 实例化Toggle
    //     var toggle = Instantiate(pixelHintTogglePrefab, canvasObj.transform);
    //     toggle.name = "PixelHintToggle";
    //     // 设置位置和大小
    //     RectTransform rect = toggle.GetComponent<RectTransform>();
    //     rect.anchorMin = new Vector2(0, 0);
    //     rect.anchorMax = new Vector2(0, 0);
    //     rect.pivot = new Vector2(0, 0);
    //     rect.anchoredPosition = new Vector2(20, 20); // 修改为左下角2%,2%的位置
    //     // rect.sizeDelta = new Vector2(120, 40);
    //     // 设置TMP文字
    //     var tmp = toggle.GetComponentInChildren<TMPro.TextMeshProUGUI>();
    //     if (tmp != null)
    //     {
    //         tmp.text = "HINT";
    //     }
    //     // 监听Toggle状态变化，改为绑定公开方法
    //     // toggle.onValueChanged.RemoveAllListeners();
    //     // toggle.onValueChanged.AddListener(OnHintToggleChanged);
    // }

    public void LoadLevelAndSpawnNodes(int numberOfCells)
    {
        _cellNumbers = numberOfCells;
        SpawnLevel(numberOfCells);
    }

    // 进入下一关（最小改动：清场→levelIndex++→seed→SpawnLevel）
    public void NextLevel()
    {
        levelIndex++;
        ClearClustersFile();
        RemoveAllEdges();
        _initialEdges.Clear();
        playerCutEdges.Clear();
        
        // 清理时间炸弹状态
        timeBombEdges.Clear();
        
        ClearUndoHistory();
        
        // 根据新关卡应用难度设置
        ApplyDifficultySettings();
        
                // 重置胜利检测标志（关键：否则上一关已显示过面板，下一关将不再弹出）
        hasShownVictoryPanel = false;
        hasOptimalCost = false;

        // 自动关闭Hint功能
        TurnOffHint();

        // 重置生态区高亮器状态并自动关闭生态区显示
        if (clusterHighlighter != null)
        {
            clusterHighlighter.ResetHighlighter();
            
            // 通关后自动关闭生态区高亮，让用户自己决定是否重新开启
            ForceRefreshEcoZonesToggle();
        }

        // 清理权重缓存，确保重新计算
        _edgeWeightCache.Clear();
        
        SpawnLevel(_cellNumbers);
        
        // 更新UI显示
        UpdateCutLimitUI();
        UpdateTimerUI();
        UpdateLevelDisplay();
        
        UnityEngine.Debug.Log($"进入关卡 {levelIndex}");
    }

    /// <summary>
    /// 关闭Hint功能，恢复所有边的正常显示
    /// </summary>
    private void TurnOffHint()
    {
        // 恢复所有边的正常外观
        foreach (var edgeKey in _edges.Keys)
        {
            // 重置材质
            if (_edges.TryGetValue(edgeKey, out var edgeInfo) && _lineMaterial != null)
                edgeInfo.renderer.material = _lineMaterial;
            // 恢复正确的颜色和宽度
            UpdateTimeBombEdgeAppearance(edgeKey);
        }
        
        // 如果有Hint Toggle，将其设为关闭状态
        var hintToggle = GameObject.Find("UICanvas/HintToggle")?.GetComponent<UnityEngine.UI.Toggle>();
        if (hintToggle != null && hintToggle.isOn)
        {
            hintToggle.isOn = false;
            UnityEngine.Debug.Log("自动关闭Hint功能");
        }
    }

    /// <summary>
    /// 关闭生态区高亮（通关后自动关闭）
    /// </summary>
    private void ForceRefreshEcoZonesToggle()
    {
        // 尝试多个可能的路径查找生态区Toggle
        string[] possiblePaths = {
            "UICanvas/EcoZonesToggle",
            "Canvas/EcoZonesToggle", 
            "UICanvas/Show Eco Zones Toggle",
            "Canvas/Show Eco Zones Toggle",
            "EcoZonesToggle",
            "Show Eco Zones Toggle"
        };

        UnityEngine.UI.Toggle ecoToggle = null;
        foreach (var path in possiblePaths)
        {
            var toggleObj = GameObject.Find(path);
            if (toggleObj != null)
            {
                ecoToggle = toggleObj.GetComponent<UnityEngine.UI.Toggle>();
                if (ecoToggle != null)
                {
                    UnityEngine.Debug.Log($"找到生态区Toggle: {path}");
                    break;
                }
            }
        }

        if (ecoToggle != null)
        {
            // 通关后自动关闭生态区高亮
            if (ecoToggle.isOn)
            {
                UnityEngine.Debug.Log("通关后自动关闭生态区高亮");
                ecoToggle.isOn = false; // 触发OnEcoZonesToggleChanged(false)
            }
            else
            {
                UnityEngine.Debug.Log("生态区Toggle已为关闭状态");
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("未找到生态区Toggle，尝试的路径: " + string.Join(", ", possiblePaths));
        }
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
        float minY = mainCamera.transform.position.y - cameraHeight * 0.1f;
        float maxY = mainCamera.transform.position.y + cameraHeight * 0.1f;

        float minDistance = 1.2f; // 最小间距
        float cellSize = minDistance / Mathf.Sqrt(2); // 网格大小

        // 创建网格
        int cols = Mathf.CeilToInt((maxX - minX) / cellSize);
        int rows = Mathf.CeilToInt((maxY - minY) / cellSize);
        int?[,] grid = new int?[cols, rows];

        // 活动点列表
        List<Vector2> activePoints = new List<Vector2>();

        // 添加第一个点（不再强制要求在陆地上）
        Vector2 firstPoint = new Vector2(
            UnityEngine.Random.Range(minX, maxX),
            UnityEngine.Random.Range(minY, maxY)
        );

        cellPositions.Add(firstPoint);
        activePoints.Add(firstPoint);

        // 将点添加到网格
        int gridX = Mathf.FloorToInt((firstPoint.x - minX) / cellSize);
        int gridY = Mathf.FloorToInt((firstPoint.y - minY) / cellSize);
        if (gridX >= 0 && gridX < cols && gridY >= 0 && gridY < rows)
        {
            grid[gridX, gridY] = cellPositions.Count - 1;
        }

        int maxTotalAttempts = numberOfPoints * 100; // 总尝试次数限制
        int totalAttempts = 0;

        while (activePoints.Count > 0 && cellPositions.Count < numberOfPoints && totalAttempts < maxTotalAttempts)
        {
            totalAttempts++;
            
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

                // 不再检查是否在陆地上，允许在任何地形生成Cell

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
                    if (newGridX >= 0 && newGridX < cols && newGridY >= 0 && newGridY < rows)
                    {
                        grid[newGridX, newGridY] = cellPositions.Count - 1;
                    }
                    foundValidPoint = true;
                    break;
                }
            }

            if (!foundValidPoint)
            {
                activePoints.RemoveAt(activeIndex);
            }
        }

        if (cellPositions.Count < numberOfPoints)
        {
            UnityEngine.Debug.LogWarning($"只能生成 {cellPositions.Count} 个节点，少于请求的 {numberOfPoints} 个。可能陆地面积不足。");
        }

        UnityEngine.Debug.Log($"Generated {cellPositions.Count} points using Poisson Disk Sampling on land");
        return cellPositions;
    }

    /// <summary>
    /// 检查指定位置是否在陆地上
    /// </summary>
    /// <param name="position">要检查的位置</param>
    /// <returns>如果位置在陆地上返回true，否则返回false</returns>
    private bool IsPositionOnLand(Vector2 position)
    {
        // 如果禁用了地形检查，直接返回true
        if (!enableTerrainCheck)
        {
            return true;
        }

        if (terrainManager == null)
        {
            UnityEngine.Debug.LogWarning("TerrainManager is null, assuming position is on land");
            return true;
        }

        try
        {
            // 获取Tilemap
            var tilemapProperty = terrainManager.GetType().GetProperty("tilemap");
            Tilemap tilemap = null;
            if (tilemapProperty != null)
            {
                tilemap = tilemapProperty.GetValue(terrainManager) as Tilemap;
            }

            if (tilemap == null)
            {
                UnityEngine.Debug.LogWarning("无法获取Tilemap，假设位置在陆地上");
                return true;
            }

            // 使用tilemap.WorldToCell()进行正确的坐标转换
            Vector3Int tilePos = tilemap.WorldToCell(position);

            // 获取该位置的生物群系类型
            int biomeType = GetBiomeUsingMap(terrainManager, tilePos);
            
            // 检查是否为水域生物群系
            bool isWater = IsWaterBiome(biomeType);
            
            // 调试信息（可选，用于验证地形检查是否正常工作）
            // if (UnityEngine.Debug.isDebugBuild)
            // {
            //     UnityEngine.Debug.Log($"位置 {position} -> 瓦片 {tilePos} -> 生物群系 {biomeType} -> 是否水域 {isWater}");
            // }
            
            return !isWater;
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning($"检查地形时出错: {ex.Message}，假设位置在陆地上");
            return true;
        }
    }

    /// <summary>
    /// 检查生物群系类型是否为水域
    /// </summary>
    /// <param name="biomeType">生物群系类型</param>
    /// <returns>如果是水域返回true，否则返回false</returns>
    private bool IsWaterBiome(int biomeType)
    {
        // 根据 HexCoordinateSystem.BiomeType 枚举定义水域生物群系
        // DeepWater = 0, ShallowWater = 1, Lake1 = 20, Lake2 = 21, Lake3 = 22, Lake4 = 23
        switch (biomeType)
        {
            case 0:  // DeepWater (深水)
            case 1:  // ShallowWater (浅水)
            case 20: // Lake1 (湖泊1)
            case 21: // Lake2 (湖泊2)
            case 22: // Lake3 (湖泊3)
            case 23: // Lake4 (湖泊4)
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 根据地形类型选择合适的Cell prefab（改进版，学习自SimpleEdgeTileTest）
    /// </summary>
    /// <param name="position">Cell位置</param>
    /// <returns>对应地形的Cell prefab</returns>
    private Cell GetCellPrefabForTerrain(Vector2 position)
    {
        // 如果禁用了地形检查，默认使用urban prefab
        if (!enableTerrainCheck)
        {
            return _urbanCellPrefab;
        }

        if (terrainManager == null)
        {
            UnityEngine.Debug.LogWarning("TerrainManager is null, using urban prefab");
            return _urbanCellPrefab;
        }

        try
        {
            // 获取Tilemap
            var tilemapProperty = terrainManager.GetType().GetProperty("tilemap");
            Tilemap tilemap = null;
            if (tilemapProperty != null)
            {
                tilemap = tilemapProperty.GetValue(terrainManager) as Tilemap;
            }

            if (tilemap == null)
            {
                UnityEngine.Debug.LogWarning("无法获取Tilemap，使用urban prefab");
                return _urbanCellPrefab;
            }

            // 使用tilemap.WorldToCell()进行坐标转换
            Vector3Int tilePos = tilemap.WorldToCell(position);
            
            // 检查瓦片是否存在
            if (!tilemap.HasTile(tilePos))
            {
                UnityEngine.Debug.LogWarning($"位置 {position} 没有瓦片，使用urban prefab");
                return _urbanCellPrefab;
            }

            // 使用改进的生物群系检测方法（学习自SimpleEdgeTileTest）
            int biomeType = GetBiomeUsingAdvancedMap(terrainManager, tilePos);
            
            // 根据地形类型选择prefab
            bool isWater = IsWaterBiome(biomeType);
            string biomeName = GetBiomeDisplayName(biomeType);
            
            if (isWater)
            {
                UnityEngine.Debug.Log($"Cell位置 {position} -> 瓦片 {tilePos} -> {biomeName} -> 使用Port prefab");
                return _portCellPrefab;
            }
            else
            {
                UnityEngine.Debug.Log($"Cell位置 {position} -> 瓦片 {tilePos} -> {biomeName} -> 使用Urban prefab");
                return _urbanCellPrefab;
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning($"检查地形时出错: {ex.Message}，使用urban prefab");
            return _urbanCellPrefab;
        }
    }

    /// <summary>
    /// 使用改进的映射表获取生物群系（学习自SimpleEdgeTileTest）
    /// </summary>
    /// <param name="terrainManager">地形管理器</param>
    /// <param name="tilePos">瓦片位置</param>
    /// <returns>生物群系类型ID</returns>
    private int GetBiomeUsingAdvancedMap(MonoBehaviour terrainManager, Vector3Int tilePos)
    {
        try
        {
            // 调用TerrainManager的GetBiomeAtTile方法
            var getBiomeMethod = terrainManager.GetType().GetMethod("GetBiomeAtTile");
            if (getBiomeMethod != null)
            {
                var result = getBiomeMethod.Invoke(terrainManager, new object[] { tilePos });
                if (result != null)
                {
                    return (int)result;
                }
            }
            
            UnityEngine.Debug.LogWarning($"无法使用映射表获取瓦片 {tilePos} 的生物群系");
            return -1;
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning($"获取生物群系时出错: {ex.Message}");
            return -1;
        }
    }

    /// <summary>
    /// 获取生物群系显示名称（学习自SimpleEdgeTileTest）
    /// </summary>
    /// <param name="biomeType">生物群系类型</param>
    /// <returns>生物群系名称</returns>
    private string GetBiomeDisplayName(int biomeType)
    {
        switch (biomeType)
        {
            case 0: return "深水";
            case 1: return "浅水";
            case 2: return "平地沙漠1";
            case 3: return "平地沙漠2";
            case 4: return "平地草原";
            case 5: return "平地稀疏树木1";
            case 6: return "平地稀疏树木2";
            case 7: return "平地森林";
            case 8: return "平地沼泽森林";
            case 9: return "丘陵沙漠";
            case 10: return "丘陵草原";
            case 11: return "丘陵森林";
            case 12: return "丘陵针叶林";
            case 13: return "山地沙漠";
            case 14: return "山地灌木丛1";
            case 15: return "山地灌木丛2";
            case 16: return "山地高山1";
            case 17: return "山地高山2";
            case 18: return "山地不可通行1";
            case 19: return "山地不可通行2";
            case 20: return "湖泊1";
            case 21: return "湖泊2";
            case 22: return "湖泊3";
            case 23: return "湖泊4";
            case 24: return "火山";
            case 25: return "巢穴";
            case 26: return "雪地巢穴";
            case 27: return "沙漠巢穴";
            case -1: return "未知地形";
            default: return $"未知({biomeType})";
        }
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
            // 确保Cell的Z轴为0，避免渲染顺序问题
            cell.transform.position = new Vector3(newPos.x, newPos.y, 0);
        }
    }

    // 新增：仅计算拉伸居中后的最终位置，不直接移动对象，供生成前地形检测使用
    private List<Vector2> ComputeStretchedAndCenteredPositions(List<Vector2> originalPositions)
    {
        List<Vector2> result = new List<Vector2>(originalPositions.Count);
        if (originalPositions.Count == 0)
        {
            return result;
        }

        // 1. 计算包围盒
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var pos in originalPositions)
        {
            if (pos.x < minX) minX = pos.x;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.y < minY) minY = pos.y;
            if (pos.y > maxY) maxY = pos.y;
        }

        // 2. 目标区域（与StretchAndCenterCells一致）
        Camera cam = Camera.main;
        float camHeight = cam.orthographicSize * 2f * 0.8f;
        float camWidth = camHeight * cam.aspect;

        // 3. 计算缩放比例
        float width = Mathf.Max(maxX - minX, 0.01f);
        float height = Mathf.Max(maxY - minY, 0.01f);
        float scaleX = camWidth / width;
        float scaleY = camHeight / height;

        // 4. 以中心为基准，计算新位置
        Vector2 center = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);
        Vector2 screenCenter = cam.transform.position;
        foreach (var pos in originalPositions)
        {
            Vector2 newPos = new Vector2(
                (pos.x - center.x) * scaleX,
                (pos.y - center.y) * scaleY
            ) + screenCenter;
            result.Add(newPos);
        }

        return result;
    }

    private void SpawnLevel(int numberOfPoints)
    {
        // 检查 prefab 是否为 null
        if (_urbanCellPrefab == null || _portCellPrefab == null)
        {
            UnityEngine.Debug.LogError("❌ Cell Prefab 未设置！请在 Inspector 中设置 Urban Cell Prefab 和 Port Cell Prefab。");
            return;
        }

        // 检查当前场景名，如果是Level1则不生成关卡
        if (SceneManager.GetActiveScene().name == "Level1")
        {
            UnityEngine.Debug.Log("当前为Level1场景，不生成关卡。");
            return;
        }
        // 清理之前的关卡
        foreach (var cell in _cells)
        {
            if (cell != null)
                Destroy(cell.gameObject);
        }
        _cells.Clear();
        RemoveAllEdges();
        _initialEdges.Clear(); // 清空初始边集合
        playerCutEdges.Clear(); // 清空玩家切割记录
        ClearUndoHistory(); // 清空回退历史

        List<Vector2> cellPositions = GenerateCellPositions(numberOfPoints);
        // 预计算拉伸并居中的最终位置（避免实例化后再移动导致地形检测不准）
        List<Vector2> finalPositions = ComputeStretchedAndCenteredPositions(cellPositions);
        // Assign positions to cells and collect Vector2 for triangulation
        List<Vector2> pointsForTriangulation = new List<Vector2>();

        for (int i = 0; i < finalPositions.Count; i++)
        {
            Vector2 position = finalPositions[i];

            // 确保Cell的Z轴为0，避免渲染顺序问题
            Vector3 cellPosition = new Vector3(position.x, position.y, 0);
            
            // 根据地形类型选择合适的prefab
            Cell prefabToUse = GetCellPrefabForTerrain(position);
            Cell newCell = Instantiate(prefabToUse, cellPosition, Quaternion.identity, transform);
            newCell.Number = i + 1; // Cell.Number is 1-indexed for display/logic
            newCell.Init(i + 1);
            newCell.gameObject.name = $"Cell {newCell.Number}";
            _cells.Add(newCell);
            pointsForTriangulation.Add(position);
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
                    
                    // 生成时间炸弹边（只有Hard难度才能生成）
                    if (enableTimeBomb && gameDifficulty == GameDifficulty.Hard && UnityEngine.Random.value < timeBombChance)
                    {
                        timeBombEdges.Add(key);
                        // 更新外观（加粗+变色）
                        UpdateTimeBombEdgeAppearance(key);
                        UnityEngine.Debug.Log($"时间炸弹边已生成: Edge({_cells[edge.P1Index].Number}-{_cells[edge.P2Index].Number})");
                    }
                    else
                    {
                        // 确保普通边使用正确的外观
                        UpdateTimeBombEdgeAppearance(key);
                    }
                }
            }
        }
        else if (_cells.Count == 2) // If only two points, connect them directly
        {
            CreateOrUpdateEdge(_cells[0], _cells[1]);
            var key = GetCanonicalEdgeKey(_cells[0], _cells[1]);
            _initialEdges.Add(key);
            
            // 生成时间炸弹边（只有Hard难度才能生成）
            if (enableTimeBomb && gameDifficulty == GameDifficulty.Hard && UnityEngine.Random.value < timeBombChance)
            {
                timeBombEdges.Add(key);
                // 更新外观（加粗+变色）
                UpdateTimeBombEdgeAppearance(key);
                UnityEngine.Debug.Log($"时间炸弹边已生成: Edge({_cells[0].Number}-{_cells[1].Number})");
            }
            else
            {
                // 确保普通边使用正确的外观
                UpdateTimeBombEdgeAppearance(key);
            }
        }
        // If 0 or 1 cell, do nothing

        // 生成图后对权重进行一次平衡，避免全部为正导致最优cost为0
        BalanceEdgeWeights();
        
        // 自动计算最优cost并刷新UI
        UpdateOptimalCostByPython();

        // 关卡生成完成后，写出初始（未切割）clusters并通知可视化，这样高亮脚本初始会显示统一底色
        try
        {
                    CalculateAndSaveClustersAfterCut();
            NotifyCellTileTestManager();
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning($"⚠️ 初始写出clusters失败: {ex.Message}");
        }
    }

    // 新增：自动计算最优cost的方法
    private void UpdateOptimalCostByPython()
    {
        // 1. 构造输入数据
        var nodes = _cells.Select(cell => cell.Number).ToList();
        var edgeList = new List<Dictionary<string, object>>();
        foreach (var edge in _edges.Keys)
        {
            int u = edge.Item1.Number;
            int v = edge.Item2.Number;
            int w;
            if (!_edgeWeightCache.TryGetValue(edge, out w))
            {
                // 回退：即时计算并缓存
                w = GetOrCreateEdgeWeight(edge.Item1, edge.Item2);
            }
            edgeList.Add(new Dictionary<string, object> { {"u", u}, {"v", v}, {"weight", w} });
        }
        string nodesStr = string.Join(",", nodes);
        string edgesStr = string.Join(",", edgeList.Select(e => $"{{\"u\":{e["u"]},\"v\":{e["v"]},\"weight\":{e["weight"]}}}"));
        string jsonData = $"{{\"nodes\":[{nodesStr}],\"edges\":[{edgesStr}]}}";

        string pythonExe = "python";
        string scriptPath = "Assets/Scripts/multicut_solver.py";
        string inputPath = "input.json";
        string outputPath = "output.json";

        // 2. 调用Python
        RunPythonMulticut(pythonExe, scriptPath, inputPath, outputPath, jsonData);

        // 3. 读取结果
        string resultJson = System.IO.File.ReadAllText(outputPath);
        int optimalCostLocal = 0;
        var costMatch = Regex.Match(resultJson, "\\\"cost\\\"\\s*:\\s*(-?\\d+)");
        if (costMatch.Success)
            optimalCostLocal = int.Parse(costMatch.Groups[1].Value);
        optimalCost = optimalCostLocal;
        hasOptimalCost = true; // 关键：在未点击Hint时也标记已获得最优cost
        UnityEngine.Debug.Log($"Level {levelIndex} 最优cost = {optimalCost}");
        UpdateCostText();
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

        // 计时器
        if (enableTimer)
        {
            if (remainingTime <= 0f && _cells.Count > 0)
            {
                // 首次进入本关
                remainingTime = timeLimitSeconds;
            }
            if (remainingTime > 0f)
            {
                remainingTime -= Time.deltaTime;
                if (remainingTime < 0f) remainingTime = 0f;
                UpdateTimerUI();
                if (Mathf.Approximately(remainingTime, 0f))
                {
                    OnTimeUp();
                }
            }
        }
        
        // 切割次数UI更新
        UpdateCutLimitUI();

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
            // UnityEngine.Debug.Log("Raycast 未命中 Cell");
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
                    // 在删除边之前保存当前状态
                    SaveGameState();
                    UnityEngine.Debug.Log($"💾 保存单边删除前的状态，当前切割边数量: {playerCutEdges.Count}");
                    
                    RemoveEdge(toRemoveKey.Item1, toRemoveKey.Item2);
                    
                    UnityEngine.Debug.Log($"✂️ 单边删除完成，删除的边: {toRemoveKey.Item1.Number}-{toRemoveKey.Item2.Number}");
                }
                else
                {
                    UnityEngine.Debug.Log("不能删除此边：删除后不会增加连通分量数量。");
                }
            }
        }
        else
        {
            // UnityEngine.Debug.Log("Raycast 未命中 Line");
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
            previewEdge.material = previewEdgeMaterial != null ? previewEdgeMaterial : _lineMaterial;
            previewEdge.startWidth = 0.15f;
            previewEdge.endWidth = 0.15f;
            previewEdge.positionCount = 2;
            previewEdge.useWorldSpace = true;
            previewEdge.startColor = Color.black;
            previewEdge.endColor = Color.black;
            previewEdge.textureMode = LineTextureMode.Tile; // 新增：像素风贴图平铺
            previewEdge.sortingOrder = 1; // 设置较低的排序顺序，确保在cells之下
            previewEdge.sortingLayerName = "Default"; // 设置为Default层，与cells保持一致
            previewEdge.gameObject.layer = LayerMask.NameToLayer("Default"); // 设置GameObject的Layer为Default
        }
        // 确保预览线的Z轴为0，避免渲染顺序问题
        Vector3 startPos = new Vector3(startPosition.x, startPosition.y, 0);
        previewEdge.SetPosition(0, startPos);
        previewEdge.SetPosition(1, startPos);
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
            // 重新连接时，确保可见
            if (renderer != null && renderer.gameObject != null)
            {
                renderer.gameObject.SetActive(true);
            }
            renderer.positionCount = 2;
            renderer.SetPosition(0, fromCell.transform.position);
            renderer.SetPosition(1, toCell.transform.position);

            Vector3 midPoint = (fromCell.transform.position + toCell.transform.position) / 2f;

            // 权重数字和背景只在开启权重时显示
            if (useWeightedEdges)
            {
                tmp.gameObject.SetActive(true);
                bg.SetActive(true);
                // 确保权重文本和背景的Z轴为0，避免渲染顺序问题
                Vector3 textPos = new Vector3(midPoint.x, midPoint.y, 0);
                Vector3 bgPos = new Vector3(midPoint.x, midPoint.y, 0);
                tmp.transform.position = textPos;
                bg.transform.position = bgPos;
                tmp.text = weight.ToString();
                // 不再在这里调整背景缩放，保持预制体原始缩放，避免单位不一致导致放大
            }
            else
            {
                tmp.gameObject.SetActive(false);
                bg.SetActive(false);
            }

            // 从玩家切割集合中移除此边（完成重连）
            if (playerCutEdges.Contains(key))
            {
                playerCutEdges.Remove(key);
            }

            _edges[key] = (renderer, weight, tmp, bg);
                renderer.sortingOrder = 1; // 设置较低的排序顺序，确保在cells之下
                renderer.sortingLayerName = "Default"; // 设置为Default层，与cells保持一致
                renderer.gameObject.layer = LayerMask.NameToLayer("Default"); // 设置GameObject的Layer为Default
                if (bg.TryGetComponent<SpriteRenderer>(out var bgRenderer))
                    bgRenderer.sortingOrder = renderer.sortingOrder + 1;
                // TextMeshProUGUI的渲染顺序通过Canvas控制，这里不需要设置sortingOrder
        }
        else
        {
            GameObject lineObject = new GameObject($"Line_{fromCell.Number}_to_{toCell.Number}");
            lineObject.transform.SetParent(linesRoot);
            LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
            lineRenderer.material = _lineMaterial;
            // 设置线条宽度
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.useWorldSpace = true;
            lineRenderer.textureMode = LineTextureMode.Tile; // 新增：像素风贴图平铺
            // 设置默认颜色为黑色
            lineRenderer.startColor = Color.black;
            lineRenderer.endColor = Color.black;
            lineRenderer.positionCount = 2;
            // 确保LineRenderer的Z轴为0，避免渲染顺序问题
            Vector3 fromPos = new Vector3(fromCell.transform.position.x, fromCell.transform.position.y, 0);
            Vector3 toPos = new Vector3(toCell.transform.position.x, toCell.transform.position.y, 0);
            lineRenderer.SetPosition(0, fromPos);
            lineRenderer.SetPosition(1, toPos);

            EdgeCollider2D edgeCollider = lineObject.AddComponent<EdgeCollider2D>();
            Vector2[] points = new Vector2[2];
            points[0] = lineObject.transform.InverseTransformPoint(fromCell.transform.position);
            points[1] = lineObject.transform.InverseTransformPoint(toCell.transform.position);
            edgeCollider.points = points;
            edgeCollider.edgeRadius = 0.1f;
            edgeCollider.isTrigger = true;
            lineObject.layer = LayerMask.NameToLayer("Edge");

            // 创建权重标签：使用WeightPrefab中已有的TextMeshPro
            Vector3 midPoint = (fromCell.transform.position + toCell.transform.position) / 2f;
            
            // 实例化WeightPrefab（使用统一的固定缩放，避免受父对象或不同像素密度影响）
            GameObject weightPrefab = Instantiate(WeightPrefab, lineObject.transform);
            weightPrefab.transform.localScale = Vector3.one; // 重置缩放
            
                    // 获取WeightPrefab中的TextMeshProUGUI组件（UI版本）
        TextMeshProUGUI tmp = weightPrefab.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp == null)
        {
            UnityEngine.Debug.LogError("❌ WeightPrefab中没有找到TextMeshProUGUI组件！");
            DestroyImmediate(weightPrefab);
            _edges[key] = (lineRenderer, weight, null, null);
            return;
        }
            
            // 设置文本内容
            tmp.text = weight.ToString();
            
            // 确保权重标签的Z轴为0，避免渲染顺序问题，并设置统一的缩放
            Vector3 weightPos = new Vector3(midPoint.x, midPoint.y, 0);
            weightPrefab.transform.position = weightPos;
            weightPrefab.transform.rotation = Quaternion.identity;
            weightPrefab.transform.localScale = Vector3.one;
            
            // 根据开关决定是否显示权重
            weightPrefab.SetActive(useWeightedEdges);
            
                    // 设置排序顺序
        if (weightPrefab.TryGetComponent<SpriteRenderer>(out var bgRenderer))
            bgRenderer.sortingOrder = lineRenderer.sortingOrder + 1;
        // TextMeshProUGUI的渲染顺序通过Canvas控制，这里不需要设置sortingOrder
            
            // 从玩家切割集合中移除此边（完成重连/新增）
            if (playerCutEdges.Contains(key))
            {
                playerCutEdges.Remove(key);
            }

            _edges[key] = (lineRenderer, weight, tmp, weightPrefab);

            lineRenderer.sortingOrder = 1; // 设置较低的排序顺序，确保在cells之下
            lineRenderer.sortingLayerName = "Default"; // 设置为Default层，与cells保持一致
            lineRenderer.gameObject.layer = LayerMask.NameToLayer("Default"); // 设置GameObject的Layer为Default
            // 暂时注释掉背景和文本的排序设置
            /*
            if (bg.TryGetComponent<SpriteRenderer>(out var bgRenderer))
                bgRenderer.sortingOrder = lineRenderer.sortingOrder + 1;
            tmp.sortingOrder = bgRenderer.sortingOrder + 1;
            */
        }

        // 新增：记录添加后的连通分量数量
        int after = CalculateNumberOfConnectedComponents();
        // 如果连通分量数量减少，说明有两个分量被合并
        if (after < before)
        {
            // 恢复当前两个分量内，所有原先属于同一簇的内部边
            RestoreClusterInternalEdges(fromCell, toCell);
            // 合并后刷新cost与clusters
            UpdateCostText();
            try { CalculateAndSaveClustersAfterCut(); } catch { }
        }
    }

    // 直接创建边的方法，避免递归调用
    private void CreateEdgeDirectly(Cell fromCell, Cell toCell)
    {
        var key = GetCanonicalEdgeKey(fromCell, toCell);
        int weight = GetOrCreateEdgeWeight(fromCell, toCell);
        
        // 直接创建边，不调用CreateOrUpdateEdge避免递归
        GameObject lineObject = new GameObject($"Line_{fromCell.Number}_to_{toCell.Number}");
        lineObject.transform.SetParent(linesRoot);
        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.material = _lineMaterial;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.useWorldSpace = true;
        lineRenderer.textureMode = LineTextureMode.Tile;
        // 设置默认颜色为黑色
        lineRenderer.startColor = Color.black;
        lineRenderer.endColor = Color.black;
        
        lineRenderer.positionCount = 2;
        Vector3 fromPos = new Vector3(fromCell.transform.position.x, fromCell.transform.position.y, 0);
        Vector3 toPos = new Vector3(toCell.transform.position.x, toCell.transform.position.y, 0);
        lineRenderer.SetPosition(0, fromPos);
        lineRenderer.SetPosition(1, toPos);

        EdgeCollider2D edgeCollider = lineObject.AddComponent<EdgeCollider2D>();
        Vector2[] points = new Vector2[2];
        points[0] = lineObject.transform.InverseTransformPoint(fromCell.transform.position);
        points[1] = lineObject.transform.InverseTransformPoint(toCell.transform.position);
        edgeCollider.points = points;
        edgeCollider.edgeRadius = 0.1f;
        edgeCollider.isTrigger = true;
        lineObject.layer = LayerMask.NameToLayer("Edge");

        // 创建权重标签：使用WeightPrefab中已有的TextMeshPro
        Vector3 midPoint = (fromCell.transform.position + toCell.transform.position) / 2f;
        
        // 实例化WeightPrefab
        GameObject weightPrefab = Instantiate(WeightPrefab, lineObject.transform);
        
        // 获取WeightPrefab中的TextMeshProUGUI组件（UI版本）
        TextMeshProUGUI tmp = weightPrefab.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp == null)
        {
            UnityEngine.Debug.LogError("❌ WeightPrefab中没有找到TextMeshProUGUI组件！");
            DestroyImmediate(weightPrefab);
            _edges[key] = (lineRenderer, weight, null, null);
            return;
        }
        
        // 设置文本内容（不调整背景scale，保持预制体内的相对布局）
        tmp.text = weight.ToString();
        
        // 确保权重标签的Z轴为0，避免渲染顺序问题
        Vector3 weightPos = new Vector3(midPoint.x, midPoint.y, 0);
        weightPrefab.transform.position = weightPos;
        weightPrefab.transform.rotation = Quaternion.identity;
        
        // 根据开关决定是否显示权重
        weightPrefab.SetActive(useWeightedEdges);
        
        // 设置排序顺序
        if (weightPrefab.TryGetComponent<SpriteRenderer>(out var bgRenderer))
            bgRenderer.sortingOrder = lineRenderer.sortingOrder + 1;
        // TextMeshProUGUI的渲染顺序通过Canvas控制，这里不需要设置sortingOrder
        
        _edges[key] = (lineRenderer, weight, tmp, weightPrefab);

        lineRenderer.sortingOrder = 1; // 设置较低的排序顺序，确保在cells之下
        lineRenderer.sortingLayerName = "Default"; // 设置为Default层，与cells保持一致
        lineRenderer.gameObject.layer = LayerMask.NameToLayer("Default"); // 设置GameObject的Layer为Default
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
            // 如果是时间炸弹边，应用时间惩罚
            if (timeBombEdges.Contains(key))
            {
                timeBombEdges.Remove(key);
                
                // 应用时间惩罚
                if (enableTimer && remainingTime > 0)
                {
                    remainingTime -= timeBombPenaltySeconds;
                    if (remainingTime < 0) remainingTime = 0;
                    UpdateTimerUI();
                    UnityEngine.Debug.Log($"时间炸弹触发! Edge({fromCell.Number}-{toCell.Number}) 减少 {timeBombPenaltySeconds} 秒，剩余时间: {remainingTime}");
                }
                else
                {
                    UnityEngine.Debug.Log($"时间炸弹边被切割: Edge({fromCell.Number}-{toCell.Number}) (计时器未启用)");
                }
            }
            
            // 记录玩家切割的边
            playerCutEdges.Add(key);
            
            // 隐藏边而不是销毁，以便回退时可以恢复
            if (edge.renderer != null && edge.renderer.gameObject != null)
            {
                edge.renderer.gameObject.SetActive(false);
            }
            if (edge.bg != null)
            {
                edge.bg.SetActive(false);
            }
            
            UpdateCostText(); // 每次切割后刷新
            
            // 计算并保存clusters信息
            CalculateAndSaveClustersAfterCut();
        }
    }

    public void RemoveAllEdges()
    {
        foreach (var edge in _edges.Values)
        {
            var (renderer, _, tmp, bg) = edge;
            // 确保销毁所有相关对象
            if (renderer != null && renderer.gameObject != null)
            {
                DestroyImmediate(renderer.gameObject);
            }
            if (bg != null)
            {
                DestroyImmediate(bg);
            }
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
                eraseLineRenderer.sortingOrder = 1; // 设置较低的排序顺序，确保在cells之下
                eraseLineRenderer.sortingLayerName = "Default"; // 设置为Default层，与cells保持一致
                eraseLineRenderer.gameObject.layer = LayerMask.NameToLayer("Default"); // 设置GameObject的Layer为Default 
        }
        eraseLineRenderer.positionCount = 1;
        // 确保擦除线的Z轴为0，避免渲染顺序问题
        Vector3 startPos = new Vector3(start.x, start.y, 0);
        eraseLineRenderer.SetPosition(0, startPos);
        eraseLineRenderer.enabled = true;
    }

    private void UpdateEraseLinePath(List<Vector2> path)
    {
        if (eraseLineRenderer == null) return;
        eraseLineRenderer.positionCount = path.Count;
        for (int i = 0; i < path.Count; i++)
        {
            // 确保擦除线的Z轴为0，避免渲染顺序问题
            Vector3 pathPos = new Vector3(path[i].x, path[i].y, 0);
            eraseLineRenderer.SetPosition(i, pathPos);
        }
    }

    private void HideEraseLine()
    {
        if (eraseLineRenderer != null)
        {
            eraseLineRenderer.enabled = false;
        }
    }

    // 时间到处理（尽量轻量）
    private void OnTimeUp()
    {
        UnityEngine.Debug.Log("⏰ 时间到！自动生成下一关。");
        NextLevel();
    }

    private void UpdateTimerUI()
    {
        if (!enableTimer || timerText == null) return;
        int sec = Mathf.CeilToInt(remainingTime);
        timerText.text = $"TIME: {sec}s";
    }
    
    private void UpdateLevelDisplay()
    {
        if (levelDisplayText == null) return;
        
        // 获取难度名称
        string difficultyName = gameDifficulty.ToString();
        
        // 格式化关卡号为两位数
        string levelNumber = levelIndex.ToString("D2");
        
        // 组合格式："Level:Hard_01"
        levelDisplayText.text = $"Level:{difficultyName}_{levelNumber}";
    }
    
    private void UpdateCutLimitUI()
    {
        if (!enableCutLimit || cutLimitText == null) return;
        cutLimitText.text = $"Cut Limit: {remainingCuts}/{currentCutLimit}";
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
            // 检查切割次数限制
            if (enableCutLimit && remainingCuts <= 0)
            {
                UnityEngine.Debug.Log("切割次数已用完！");
                return;
            }
            
            // 在批量切割之前保存当前状态
            SaveGameState();
            UnityEngine.Debug.Log($"保存批量切割前的状态，当前切割边数量: {playerCutEdges.Count}");
            
            foreach (var edge in edgesToRemove)
            {
                RemoveEdge(edge.Item1, edge.Item2);
            }
            
            // 减少切割次数（整个拖拽过程算一次）
            if (enableCutLimit)
            {
                remainingCuts--;
                UnityEngine.Debug.Log($"切割次数: {remainingCuts}/{currentCutLimit}");
            }
            
            UnityEngine.Debug.Log($"批量切割完成，新增切割边数量: {edgesToRemove.Count}");
            
            // 计算并保存clusters信息
            CalculateAndSaveClustersAfterCut();
        }
        else
        {
            UnityEngine.Debug.Log("不能擦除：此次操作不会增加连通分量数量。");
        }
    }

    // 计算并保存clusters信息到JSON文件
    public void CalculateAndSaveClustersAfterCut()
    {
        try
        {
            var clusters = CalculateClustersWithBFS();
            int currentCost = GetCurrentCost();

            // Build DTO for Unity JsonUtility compatibility
            var dto = new ClustersAfterCutDataDTO();
            dto.cut_edges = playerCutEdges.Select(edge => new CutEdgeDTO { u = edge.Item1.Number, v = edge.Item2.Number }).ToArray();
            dto.cost = currentCost;
            dto.clusters = clusters
                .Select(cluster => new ClusterInfoDTO { cells = cluster.Select(c => c.Number).ToArray() })
                .ToArray();
            dto.cluster_count = clusters.Count;
            dto.timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            dto.level_index = levelIndex;
            dto.seed = levelIndex.ToString(); // 使用关卡号代替种子

            string jsonData = JsonUtility.ToJson(dto, true);
            string filePath = System.IO.Path.Combine(Application.dataPath, "..", "clusters_after_cut.json");
            System.IO.File.WriteAllText(filePath, jsonData);
            
            UnityEngine.Debug.Log($"📊 已保存clusters信息到: {filePath}");
            UnityEngine.Debug.Log($"📊 当前有 {clusters.Count} 个clusters，总cost: {currentCost}");
            foreach (var cluster in clusters)
            {
                UnityEngine.Debug.Log($"🔸 Cluster包含 {cluster.Count} 个cells: [{string.Join(", ", cluster.Select(c => c.Number))}]");
            }
            
            // 通知CellTileTestManager重新加载clusters数据
            NotifyCellTileTestManager();
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"❌ 保存clusters信息时出错: {ex.Message}");
        }
    }

    // 通知CellTileTestManager重新加载clusters数据
    private void NotifyCellTileTestManager()
    {
        try
        {
            var cellTileTestManagers = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var manager in cellTileTestManagers)
            {
                if (manager != null && (manager.GetType().Name == "CellTileTestManager" || manager.GetType().Name == "ClusterHighlighter"))
                {
                    // 优先调用 ClusterHighlighter.RefreshFromJson
                    var refreshFromJson = manager.GetType().GetMethod("RefreshFromJson");
                    if (refreshFromJson != null)
                    {
                        refreshFromJson.Invoke(manager, null);
                        UnityEngine.Debug.Log($"🔔 已通知{manager.GetType().Name}.RefreshFromJson: {manager.name}");
                        continue;
                    }

                    // 再尝试 CellTileTestManager 的 ForceRefreshClusterDisplay
                    var forceRefreshMethod = manager.GetType().GetMethod("ForceRefreshClusterDisplay");
                    if (forceRefreshMethod != null)
                    {
                        forceRefreshMethod.Invoke(manager, null);
                        UnityEngine.Debug.Log($"🔔 已通知{manager.GetType().Name}.ForceRefreshClusterDisplay: {manager.name}");
                        continue;
                    }
                    var reloadMethod = manager.GetType().GetMethod("ReloadClusterData");
                    if (reloadMethod != null)
                    {
                        reloadMethod.Invoke(manager, null);
                        UnityEngine.Debug.Log($"🔔 已通知{manager.GetType().Name}.ReloadClusterData: {manager.name}");
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"❌ 通知CellTileTestManager时出错: {ex.Message}");
        }
    }

    // 在当前边集合上随机挑选若干条锁定（最小实现，只改GameManager）


    // 使用BFS计算所有clusters
    private List<List<Cell>> CalculateClustersWithBFS()
    {
        if (_cells.Count == 0) return new List<List<Cell>>();

        Dictionary<Cell, HashSet<Cell>> graph = new Dictionary<Cell, HashSet<Cell>>();
        foreach (var cell in _cells)
        {
            graph[cell] = new HashSet<Cell>();
        }

        // 构建图（排除已切割的边）
        foreach (var pair in _edges)
        {
            if (playerCutEdges.Contains(pair.Key))
            {
                continue; // 跳过已切割的边
            }
            
            graph[pair.Key.Item1].Add(pair.Key.Item2);
            graph[pair.Key.Item2].Add(pair.Key.Item1);
        }

        List<List<Cell>> clusters = new List<List<Cell>>();
        HashSet<Cell> visited = new HashSet<Cell>();

        foreach (var cell in _cells)
        {
            if (!visited.Contains(cell))
            {
                // 使用BFS找到当前cluster的所有cells
                List<Cell> cluster = new List<Cell>();
                Queue<Cell> queue = new Queue<Cell>();
                queue.Enqueue(cell);
                visited.Add(cell);
                cluster.Add(cell);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var neighbor in graph[current])
                    {
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                            cluster.Add(neighbor);
                        }
                    }
                }
                
                clusters.Add(cluster);
            }
        }

        return clusters;
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
            // 跳过被忽略的边
            if (ignoreEdges != null && ignoreEdges.Contains(pair.Key))
            {
                continue;
            }
            
            // 跳过已经被玩家切割的边
            if (playerCutEdges.Contains(pair.Key))
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
            // 使用地形权重计算，而不是随机权重
            weight = CalculateTerrainBasedWeight(a, b);
            _edgeWeightCache[key] = weight;
        }
        return weight;
    }

    // 计算基于地形的边权重（重构版本）
    private int CalculateTerrainBasedWeight(Cell a, Cell b)
    {
        // 如果没有地形管理器，使用随机权重作为后备
        if (terrainManager == null)
        {
            return UnityEngine.Random.Range(-maxEdgeWeight, maxEdgeWeight + 1);
        }

        // 获取Tilemap
        var tilemapProperty = terrainManager.GetType().GetProperty("tilemap");
        Tilemap tilemap = null;
        if (tilemapProperty != null)
        {
            tilemap = tilemapProperty.GetValue(terrainManager) as UnityEngine.Tilemaps.Tilemap;
        }

        if (tilemap == null)
        {
            UnityEngine.Debug.LogWarning("无法获取Tilemap，使用随机权重");
            return UnityEngine.Random.Range(-maxEdgeWeight, maxEdgeWeight + 1);
        }

        // 使用SimpleEdgeTileTest的方法获取穿过的瓦片
        var crossedTiles = GetTilesCrossedByLine(a.transform.position, b.transform.position, tilemap);
        
        // 计算基础地形权重
        int baseTerrainWeight = CalculateBaseTerrainWeight(crossedTiles);

        // 应用基于关卡号的动态权重调整
        int finalWeight = ApplyLevelBasedWeight(baseTerrainWeight);
        
        return finalWeight;
    }
    
    /// <summary>
    /// 计算基础权重（简化版：忽略地形，只基于关卡号）
    /// </summary>
    private int CalculateBaseTerrainWeight(HashSet<Vector3Int> crossedTiles)
    {
        // 完全忽略地形，只基于关卡号计算基础权重
        return CalculateLevelBasedWeight(); // 计算基于关卡的权重
    }
    
    /// <summary>
    /// 应用基于关卡号的动态权重调整
    /// </summary>
    private int ApplyLevelBasedWeight(int baseWeight)
    {
        // 更温和的随机因子：早期关卡随机性很小
        float randomInfluence = Mathf.Min(0.6f, levelIndex * 0.01f); // 降低随机性增长速度
        int randomRange = Mathf.Min(8, levelIndex); // 降低随机范围
        int randomFactor = UnityEngine.Random.Range(-randomRange, randomRange + 1);
        
        // 计算最终权重：地形权重 * (1 - 随机因子) + 随机因子 * 随机值
        float finalWeight = baseWeight * (1f - randomInfluence) + randomFactor * randomInfluence;
        
        // 使用简化的权重映射：从计算出的权重映射到 [-maxEdgeWeight, maxEdgeWeight] 范围
        int mappedWeight = MapWeightToRange(Mathf.RoundToInt(finalWeight));
        
        return mappedWeight;
    }
    
                // 动态权重范围缓存
    private float actualMinWeight = float.MaxValue;
    private float actualMaxWeight = float.MinValue;
    private bool needsRangeRecalculation = true;
    
    /// <summary>
    /// 使用 Min-Max Normalization 将权重映射到 [-maxEdgeWeight, maxEdgeWeight] 范围
    /// </summary>
    private int MapWeightToRange(int weight)
    {
        // 确保范围已计算
        if (needsRangeRecalculation)
        {
            RecalculateWeightRange();
            needsRangeRecalculation = false;
        }
        
        // 如果实际范围为0，返回0
        if (Mathf.Approximately(actualMaxWeight, actualMinWeight))
        {
            return 0;
        }
        
        // Min-Max Normalization: 映射到 [-maxEdgeWeight, maxEdgeWeight]
        // 公式: newValue = newMin + (value - oldMin) * (newMax - newMin) / (oldMax - oldMin)
        float normalizedWeight = (weight - actualMinWeight) / (actualMaxWeight - actualMinWeight);
        float mappedWeight = -maxEdgeWeight + normalizedWeight * (2 * maxEdgeWeight);
        
        return Mathf.RoundToInt(mappedWeight);
    }
    
    /// <summary>
    /// 重新计算实际权重范围（在应用映射之前的原始权重）
    /// </summary>
    private void RecalculateWeightRange()
    {
        actualMinWeight = float.MaxValue;
        actualMaxWeight = float.MinValue;
        
        // 采样一些边来估算权重范围
        int sampleCount = 0;
        int maxSamples = Mathf.Min(50, _edges.Count); // 最多采样50个边
        
        foreach (var edgePair in _edges)
        {
            if (sampleCount >= maxSamples) break;
            
            var edgeKey = edgePair.Key;
            Cell cellA = edgeKey.Item1;
            Cell cellB = edgeKey.Item2;
            
            // 计算原始权重（不经过映射）
            int rawWeight = CalculateRawWeightForSampling(cellA, cellB);
            
            if (rawWeight < actualMinWeight) actualMinWeight = rawWeight;
            if (rawWeight > actualMaxWeight) actualMaxWeight = rawWeight;
            
            sampleCount++;
        }
        
        // 如果没有有效数据，使用默认范围
        if (actualMinWeight == float.MaxValue)
        {
            actualMinWeight = -maxEdgeWeight;
            actualMaxWeight = maxEdgeWeight;
        }
        
        UnityEngine.Debug.Log($"🔍 权重范围检测: [{actualMinWeight:F1}, {actualMaxWeight:F1}] -> 映射到 [-{maxEdgeWeight}, {maxEdgeWeight}]");
    }
    
    /// <summary>
    /// 为采样计算原始权重（不经过MapWeightToRange映射）
    /// </summary>
    private int CalculateRawWeightForSampling(Cell a, Cell b)
    {
        // 如果没有地形管理器，使用随机权重作为后备
        if (terrainManager == null)
        {
            return UnityEngine.Random.Range(-50, 51); // 使用估计范围
        }

        // 获取Tilemap
        var tilemapProperty = terrainManager.GetType().GetProperty("tilemap");
        Tilemap tilemap = null;
        if (tilemapProperty != null)
        {
            tilemap = tilemapProperty.GetValue(terrainManager) as UnityEngine.Tilemaps.Tilemap;
        }

        if (tilemap == null)
        {
            return UnityEngine.Random.Range(-50, 51); // 使用估计范围
        }

        // 使用SimpleEdgeTileTest的方法获取穿过的瓦片
        var crossedTiles = GetTilesCrossedByLine(a.transform.position, b.transform.position, tilemap);
        
        // 计算基础地形权重
        int baseTerrainWeight = CalculateBaseTerrainWeight(crossedTiles);
        
        // 应用基于关卡号的动态权重调整（但不进行映射）
        return ApplyLevelBasedWeightRaw(baseTerrainWeight);
    }
    
    /// <summary>
    /// 应用基于关卡号的动态权重调整（不进行映射）
    /// </summary>
    private int ApplyLevelBasedWeightRaw(int baseWeight)
    {
        // 更温和的随机因子：早期关卡随机性很小
        float randomInfluence = Mathf.Min(0.6f, levelIndex * 0.01f); // 降低随机性增长速度
        int randomRange = Mathf.Min(8, levelIndex); // 降低随机范围
        int randomFactor = UnityEngine.Random.Range(-randomRange, randomRange + 1);
        
        // 计算最终权重：地形权重 * (1 - 随机因子) + 随机因子 * 随机值
        float finalWeight = baseWeight * (1f - randomInfluence) + randomFactor * randomInfluence;
        
        return Mathf.RoundToInt(finalWeight);
    }
    
    /// <summary>
    /// 使用映射表获取生物群系（照搬SimpleEdgeTileTest的方法）
    /// </summary>
    private int GetBiomeUsingMap(MonoBehaviour terrainManager, Vector3Int tilePos)
    {
        try
        {
            // 调用TerrainManager的GetBiomeAtTile方法
            var getBiomeMethod = terrainManager.GetType().GetMethod("GetBiomeAtTile");
            if (getBiomeMethod != null)
            {
                var result = getBiomeMethod.Invoke(terrainManager, new object[] { tilePos });
                if (result != null)
                {
                    return (int)result;
                }
            }
            
            // 如果映射表方法不可用，返回-1
            UnityEngine.Debug.LogWarning($"无法使用映射表获取瓦片 {tilePos} 的生物群系");
            return -1;
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning($"获取生物群系时出错: {ex.Message}");
            return -1;
        }
    }
    
    /// <summary>
    /// 获取线段经过的瓦片（照搬SimpleEdgeTileTest的方法）
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
            
            // 使用(X,Y,Z)格式，与TerrainManager的ConvertHexToTilePosition保持一致
            Vector3Int adjustedTilePos = new Vector3Int(tilePos.x, tilePos.y, tilePos.z);
            
            if (tilemap.HasTile(tilePos))
            {
                crossedTiles.Add(adjustedTilePos);
            }
        }
        
        // 额外使用Physics2D.LinecastAll进行更精确的检测
        RaycastHit2D[] hits = Physics2D.LinecastAll(start, end, -1); // 使用默认LayerMask
        
        foreach (var hit in hits)
        {
            if (hit.collider != null)
            {
                Vector3Int tilePos = tilemap.WorldToCell(hit.point);
                // 使用(X,Y,Z)格式，与TerrainManager的ConvertHexToTilePosition保持一致
                Vector3Int adjustedTilePos = new Vector3Int(tilePos.x, tilePos.y, tilePos.z);
                
                if (tilemap.HasTile(tilePos))
                {
                    crossedTiles.Add(adjustedTilePos);
                }
            }
        }
        
        return crossedTiles;
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

    // 新增：在重连后恢复簇内所有原始边
    private void RestoreClusterInternalEdges(Cell a, Cell b)
    {
        // 获取a和b各自当前连通分量的所有cells
        var compA = GetAllCellsInSameComponent(a);
        var compB = GetAllCellsInSameComponent(b);

        // 合并两个集合，作为"新簇"的候选
        var union = new HashSet<Cell>(compA);
        foreach (var c in compB) union.Add(c);

        // 在初始边集合中，恢复所有两端都在union内的边
        foreach (var edge in _initialEdges)
        {
            if (edge.Item1 == null || edge.Item2 == null) continue;
            if (!union.Contains(edge.Item1) || !union.Contains(edge.Item2)) continue;

            var key = GetCanonicalEdgeKey(edge.Item1, edge.Item2);
            // 从切割集合移除
            if (playerCutEdges.Contains(key)) playerCutEdges.Remove(key);
            // 若未存在，则直接创建
            if (!_edges.ContainsKey(key))
            {
                CreateEdgeDirectly(edge.Item1, edge.Item2);
            }
            else
            {
                // 确保可见
                var (renderer, w, tmp, bg) = _edges[key];
                if (renderer != null && renderer.gameObject != null)
                    renderer.gameObject.SetActive(true);
                if (bg != null) bg.SetActive(useWeightedEdges);
            }
        }
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
                // UnityEngine.Debug.Log($"标准多割求解完成，目标值: {model.ObjVal}, 切割边数: {cutEdges.Count}, 迭代次数: {iteration}");
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
    private void HighlightCutEdges(List<(Cell, Cell)> cutEdges, int cost = 0)
    {
        // 调试：打印cutEdges数量和内容
        // UnityEngine.Debug.Log($"[HighlightCutEdges] cutEdges.Count = {cutEdges.Count}");
        foreach (var edge in cutEdges)
        {
            // UnityEngine.Debug.Log($"[HighlightCutEdges] cutEdge: {edge.Item1.Number}-{edge.Item2.Number}, InstanceID: {edge.Item1.GetInstanceID()}-{edge.Item2.GetInstanceID()}");
        }
        // 调试：打印_edges字典所有key
        // UnityEngine.Debug.Log($"[HighlightCutEdges] _edges.Keys.Count = {_edges.Keys.Count}");
        foreach (var key in _edges.Keys)
        {
            // UnityEngine.Debug.Log($"[HighlightCutEdges] _edges key: {key.Item1.Number}-{key.Item2.Number}, InstanceID: {key.Item1.GetInstanceID()}-{key.Item2.GetInstanceID()}");
        }
        // 1. 先全部恢复成普通材质和外观
        foreach (var edgeKey in _edges.Keys)
        {
            // 重置材质
            if (_edges.TryGetValue(edgeKey, out var edgeInfo) && _lineMaterial != null)
                edgeInfo.renderer.material = _lineMaterial;
            // 恢复正确的颜色和宽度
            UpdateTimeBombEdgeAppearance(edgeKey);
        }
        // 2. 只把需要切割的边高亮
        foreach (var edge in cutEdges)
        {
            // UnityEngine.Debug.Log($"高亮边: {edge.Item1.Number}-{edge.Item2.Number}");
            if (_edges.TryGetValue(edge, out var edgeInfo))
            {
                // 使用Inspector中设置的高亮材质
                if (highlightEdgeMaterial != null)
                {
                    edgeInfo.renderer.material = highlightEdgeMaterial;
                    // 同时设置颜色确保可见
                    edgeInfo.renderer.startColor = highlightEdgeColor;
                    edgeInfo.renderer.endColor = highlightEdgeColor;
                    UnityEngine.Debug.Log($"应用高亮材质到边: {edge.Item1.Number}-{edge.Item2.Number}, 材质名: {highlightEdgeMaterial.name}");
                }
                else
                {
                    // 如果没有设置材质，直接用颜色高亮
                    edgeInfo.renderer.startColor = highlightEdgeColor;
                    edgeInfo.renderer.endColor = highlightEdgeColor;
                    UnityEngine.Debug.Log($"未设置高亮材质，使用自定义颜色高亮边: {edge.Item1.Number}-{edge.Item2.Number}");
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[HighlightCutEdges] 未找到对应的边: {edge.Item1.Number}-{edge.Item2.Number}");
            }
        }
        // 更新最优cost（允许为负或为0）
        optimalCost = cost;
        hasOptimalCost = true;
        UpdateCostText();
    }

    private int GetCurrentCost()
    {
        int cost = 0;
        foreach (var edge in playerCutEdges)
        {
            if (_edgeWeightCache.TryGetValue(edge, out int w))
                cost += w;
        }
        return cost;
    }

    private void UpdateCostText()
    {
        if (costText != null)
        {
            int currentCost = GetCurrentCost();
            costText.text = $"COST: {currentCost}/{optimalCost}";
            
            // 每次cost更新时检查是否达到最佳cost（精确匹配）
            if (!hasShownVictoryPanel && hasOptimalCost && currentCost == optimalCost)
            {
                UnityEngine.Debug.Log($"达到最佳cost，显示通关Panel。当前: {currentCost}, 最优: {optimalCost}");
                ShowVictoryPanel();
            }
            else if (hasOptimalCost)
            {
                UnityEngine.Debug.Log($"Cost更新: {currentCost}/{optimalCost} (差值 {currentCost - optimalCost})");
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

    // 生成地形（如果需要）
    private void GenerateTerrainIfNeeded()
    {
        UnityEngine.Debug.Log("🌍 GameManager: 开始检查地形生成...");
        
        // 如果Inspector中设置了terrainManager，直接使用
        if (terrainManager != null)
        {
            UnityEngine.Debug.Log($"✅ 使用Inspector中设置的TerrainManager: {terrainManager.GetType().Name}");
            // 通过反射调用GenerateTerrain方法
            var generateTerrainMethod = terrainManager.GetType().GetMethod("GenerateTerrain");
            if (generateTerrainMethod != null)
            {
                UnityEngine.Debug.Log("✅ 找到GenerateTerrain方法，开始调用...");
                try
                {
                    generateTerrainMethod.Invoke(terrainManager, null);
                    UnityEngine.Debug.Log("✅ 地形生成完成");
                    
                    // 检查Tilemap的渲染设置
                    var tilemapComponent = terrainManager.GetType().GetProperty("tilemap")?.GetValue(terrainManager) as UnityEngine.Tilemaps.Tilemap;
                    if (tilemapComponent != null)
                    {
                        UnityEngine.Debug.Log($"🔍 Tilemap渲染设置:");
                        var renderer = tilemapComponent.GetComponent<UnityEngine.Renderer>();
                        if (renderer != null)
                        {
                            UnityEngine.Debug.Log($"  - Sorting Layer: {renderer.sortingLayerName}");
                            UnityEngine.Debug.Log($"  - Order in Layer: {renderer.sortingOrder}");
                        }
                        UnityEngine.Debug.Log($"  - GameObject Layer: {tilemapComponent.gameObject.layer}");
                    }
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"❌ 调用GenerateTerrain方法时出错: {ex.Message}");
                    UnityEngine.Debug.LogError($"❌ 错误详情: {ex.StackTrace}");
                }
            }
            else
            {
                UnityEngine.Debug.LogError("❌ TerrainManager中没有找到GenerateTerrain方法");
                // 列出所有可用的方法
                var methods = terrainManager.GetType().GetMethods();
                UnityEngine.Debug.Log($"🔍 TerrainManager中的方法列表:");
                foreach (var method in methods)
                {
                    if (method.IsPublic)
                    {
                        UnityEngine.Debug.Log($"  - {method.Name}");
                    }
                }
            }
        }
        else
        {
            UnityEngine.Debug.Log("🔍 在场景中查找TerrainManager...");
            // 在场景中查找TerrainManager
            var allMonoBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            UnityEngine.Debug.Log($"🔍 场景中找到 {allMonoBehaviours.Length} 个MonoBehaviour组件");
            
            MonoBehaviour terrainManagerInScene = null;
            foreach (var mb in allMonoBehaviours)
            {
                UnityEngine.Debug.Log($"🔍 检查组件: {mb.GetType().Name}");
                if (mb.GetType().Name == "TerrainManager")
                {
                    terrainManagerInScene = mb;
                    UnityEngine.Debug.Log($"✅ 找到TerrainManager: {mb.name}");
                    break;
                }
            }
            
            if (terrainManagerInScene != null)
            {
                UnityEngine.Debug.Log("✅ 在场景中找到TerrainManager");
                // 通过反射调用GenerateTerrain方法
                var generateTerrainMethod = terrainManagerInScene.GetType().GetMethod("GenerateTerrain");
                if (generateTerrainMethod != null)
                {
                    UnityEngine.Debug.Log("✅ 找到GenerateTerrain方法，开始调用...");
                    try
                    {
                        generateTerrainMethod.Invoke(terrainManagerInScene, null);
                        UnityEngine.Debug.Log("✅ 地形生成完成");
                        
                        // 检查Tilemap的渲染设置
                        var tilemapComponent = terrainManagerInScene.GetType().GetProperty("tilemap")?.GetValue(terrainManagerInScene) as UnityEngine.Tilemaps.Tilemap;
                        if (tilemapComponent != null)
                        {
                            UnityEngine.Debug.Log($"🔍 Tilemap渲染设置:");
                            var renderer = tilemapComponent.GetComponent<UnityEngine.Renderer>();
                            if (renderer != null)
                            {
                                UnityEngine.Debug.Log($"  - Sorting Layer: {renderer.sortingLayerName}");
                                UnityEngine.Debug.Log($"  - Order in Layer: {renderer.sortingOrder}");
                            }
                            UnityEngine.Debug.Log($"  - GameObject Layer: {tilemapComponent.gameObject.layer}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        UnityEngine.Debug.LogError($"❌ 调用GenerateTerrain方法时出错: {ex.Message}");
                        UnityEngine.Debug.LogError($"❌ 错误详情: {ex.StackTrace}");
                    }
                }
                else
                {
                    UnityEngine.Debug.LogError("❌ TerrainManager中没有找到GenerateTerrain方法");
                    // 列出所有可用的方法
                    var methods = terrainManagerInScene.GetType().GetMethods();
                    UnityEngine.Debug.Log($"🔍 TerrainManager中的方法列表:");
                    foreach (var method in methods)
                    {
                        if (method.IsPublic)
                        {
                            UnityEngine.Debug.Log($"  - {method.Name}");
                        }
                    }
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("⚠️ 场景中没有找到TerrainManager，跳过地形生成");
                UnityEngine.Debug.LogWarning("⚠️ 请确保场景中有TerrainManager组件，或者在GameManager的Inspector中设置terrainManager字段");
            }
        }
    }
    
    // 设置Camera渲染设置，确保LineRenderer可见
    private void SetupCameraForLineRenderer()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            // 确保UI层和Default层都被渲染
            int uiLayer = LayerMask.NameToLayer("UI");
            int defaultLayer = LayerMask.NameToLayer("Default");
            
            // 设置culling mask包含UI和Default层
            mainCamera.cullingMask |= (1 << uiLayer) | (1 << defaultLayer);
            
            UnityEngine.Debug.Log($"🔍 Camera设置完成:");
            UnityEngine.Debug.Log($"  - Culling Mask: {mainCamera.cullingMask}");
            UnityEngine.Debug.Log($"  - UI Layer: {uiLayer}");
            UnityEngine.Debug.Log($"  - Default Layer: {defaultLayer}");
        }
    }

    /// <summary>
    /// 公开方法：获取或计算两个Cell之间的edge权重
    /// </summary>
    /// <param name="a">第一个Cell</param>
    /// <param name="b">第二个Cell</param>
    /// <returns>权重值</returns>
    public int GetEdgeWeight(Cell a, Cell b)
    {
        return GetOrCreateEdgeWeight(a, b);
    }
    
    /// <summary>
    /// 调试方法：验证权重计算
    /// </summary>
    [ContextMenu("验证权重计算")]
    public void DebugWeightCalculation()
    {
        UnityEngine.Debug.Log("🔍 开始验证权重计算...");
        
        if (_cells == null || _cells.Count < 2)
        {
            UnityEngine.Debug.LogWarning("⚠️ 没有足够的Cell进行测试");
            return;
        }
        
        Cell cellA = _cells[0];
        Cell cellB = _cells[1];
        
        UnityEngine.Debug.Log($"🔗 测试Edge: Cell {cellA.Number} -> Cell {cellB.Number}");
        
        // 获取穿过的瓦片
        var tilemapProperty = terrainManager.GetType().GetProperty("tilemap");
        Tilemap tilemap = null;
        if (tilemapProperty != null)
        {
            tilemap = tilemapProperty.GetValue(terrainManager) as UnityEngine.Tilemaps.Tilemap;
        }
        
        if (tilemap == null)
        {
            UnityEngine.Debug.LogError("❌ 无法获取Tilemap");
            return;
        }
        
        var crossedTiles = GetTilesCrossedByLine(cellA.transform.position, cellB.transform.position, tilemap);
        UnityEngine.Debug.Log($"📊 穿过的瓦片数量: {crossedTiles.Count}");
        
        // 计算基础地形权重
        int baseTerrainWeight = CalculateBaseTerrainWeight(crossedTiles);
        UnityEngine.Debug.Log($"🌍 基础地形权重: {baseTerrainWeight}");
        
        // 显示难度设置信息
        UnityEngine.Debug.Log($"⚙️ 难度设置:");
        UnityEngine.Debug.Log($"  - 关卡因子: {Mathf.Min(0.6f, levelIndex * 0.01f):F2}");
        UnityEngine.Debug.Log($"  - 随机范围: {Mathf.Min(8, levelIndex)}");
        
        // 计算最终权重
        int finalWeight = ApplyLevelBasedWeight(baseTerrainWeight);
        UnityEngine.Debug.Log($"🎯 最终权重: {finalWeight}");
        
        // 对比缓存中的权重
        int cachedWeight = GetOrCreateEdgeWeight(cellA, cellB);
        UnityEngine.Debug.Log($"💾 缓存中的权重: {cachedWeight}");
        
        if (finalWeight == cachedWeight)
        {
            UnityEngine.Debug.Log("✅ 权重计算正确！");
        }
        else
        {
            UnityEngine.Debug.LogWarning($"⚠️ 权重不匹配！计算值: {finalWeight}, 缓存值: {cachedWeight}");
        }
    }
    
    /// <summary>
    /// 测试不同难度设置的效果
    /// </summary>
    [ContextMenu("测试关卡权重效果")]
    public void TestLevelWeightEffects()
    {
        UnityEngine.Debug.Log("🧪 开始测试难度设置效果...");
        
        if (_cells == null || _cells.Count < 2)
        {
            UnityEngine.Debug.LogWarning("⚠️ 没有足够的Cell进行测试");
            return;
        }
        
        Cell cellA = _cells[0];
        Cell cellB = _cells[1];
        
        // 获取基础地形权重
        var tilemapProperty = terrainManager.GetType().GetProperty("tilemap");
        Tilemap tilemap = null;
        if (tilemapProperty != null)
        {
            tilemap = tilemapProperty.GetValue(terrainManager) as UnityEngine.Tilemaps.Tilemap;
        }
        
        if (tilemap == null)
        {
            UnityEngine.Debug.LogError("❌ 无法获取Tilemap");
            return;
        }
        
        var crossedTiles = GetTilesCrossedByLine(cellA.transform.position, cellB.transform.position, tilemap);
        int baseWeight = CalculateBaseTerrainWeight(crossedTiles);
        
        UnityEngine.Debug.Log($"🌍 基础地形权重: {baseWeight}");
        UnityEngine.Debug.Log($"🔗 Edge: Cell {cellA.Number} -> Cell {cellB.Number}");
        
        // 测试不同关卡
        int[] testLevels = { 1, 5, 10, 20 };
        foreach (int testLevel in testLevels)
        {
            int originalLevel = levelIndex;
            levelIndex = testLevel;
            int weight = ApplyLevelBasedWeight(baseWeight);
            UnityEngine.Debug.Log($"  🎲 关卡{testLevel}: 权重{weight}");
            levelIndex = originalLevel;
        }
    }
    
    
    
    /// <summary>
    /// 重新计算所有edges的权重
    /// </summary>
    [ContextMenu("重新计算所有Edges权重")]
    public void RecalculateAllEdgeWeights()
    {
        UnityEngine.Debug.Log("🔄 开始重新计算所有Edges权重...");
        
        // 清空权重缓存
        _edgeWeightCache.Clear();
        
        // 重置权重范围缓存，强制重新计算（用于颜色映射）
        needsRangeRecalculation = true;
        
        // 重新计算所有边的权重
        foreach (var edgePair in _edges)
        {
            var edgeKey = edgePair.Key;
            Cell cellA = edgeKey.Item1;
            Cell cellB = edgeKey.Item2;
            
            if (cellA == null || cellB == null) continue;
            
            // 重新计算权重
            int newWeight = GetOrCreateEdgeWeight(cellA, cellB);
            
            // 更新边的显示
            var edgeData = edgePair.Value;
            var weightLabel = edgeData.Item3;
            if (weightLabel != null)
            {
                weightLabel.text = newWeight.ToString();
            }
            
            // 更新边的颜色（基于新权重）
            var lineRenderer = edgeData.Item1;
            if (lineRenderer != null)
            {
                Color edgeColor = GetEdgeColorByWeight(newWeight);
                lineRenderer.startColor = edgeColor;
                lineRenderer.endColor = edgeColor;
            }
        }
        
        UnityEngine.Debug.Log($"✅ 重新计算完成！共更新 {_edges.Count} 个edges");
        
        // 更新UI显示
        UpdateCostText();
    }
    
    /// <summary>
    /// 根据权重获取边的颜色（使用 maxEdgeWeight 进行颜色映射）
    /// </summary>
    private Color GetEdgeColorByWeight(int weight)
    {
        if (weight >= 0)
        {
            // 正权重：绿色系，从浅绿到深绿
            float normalizedWeight = Mathf.Clamp01(weight / (float)maxEdgeWeight);
            return Color.Lerp(Color.green, Color.yellow, normalizedWeight);
        }
        else
        {
            // 负权重：红色系，从浅红到深红
            float normalizedWeight = Mathf.Clamp01(Mathf.Abs(weight) / (float)maxEdgeWeight);
            return Color.Lerp(Color.red, Color.magenta, normalizedWeight);
        }
    }
    
    #region 回退功能实现
    
    /// <summary>
    /// 保存当前游戏状态到历史记录
    /// </summary>
    private void SaveGameState()
    {
        // 创建当前状态的深拷贝
        var currentState = new GameState(
            playerCutEdges,
            GetCurrentCost()
        );
        
        gameStateHistory.Push(currentState);
        
        // 限制历史记录数量
        if (gameStateHistory.Count > MAX_UNDO_STEPS)
        {
            var tempStack = new Stack<GameState>();
            for (int i = 0; i < MAX_UNDO_STEPS; i++)
            {
                if (gameStateHistory.Count > 0)
                    tempStack.Push(gameStateHistory.Pop());
            }
            gameStateHistory.Clear();
            while (tempStack.Count > 0)
            {
                gameStateHistory.Push(tempStack.Pop());
            }
        }
        
        UpdateReturnButtonState();
        UnityEngine.Debug.Log($"🔄 保存游戏状态，历史记录数量: {gameStateHistory.Count}");
    }
    
    /// <summary>
    /// 回退到上一步状态（一次性回退所有操作）
    /// </summary>
    public void UndoLastAction()
    {
        if (gameStateHistory.Count == 0)
        {
            UnityEngine.Debug.Log("⚠️ 没有可回退的操作");
            return;
        }
        
        var previousState = gameStateHistory.Pop();
        
        UnityEngine.Debug.Log($"🔄 开始回退操作...");
        UnityEngine.Debug.Log($"📊 当前状态: 切割了 {playerCutEdges.Count} 条边");
        UnityEngine.Debug.Log($"📊 回退到: 切割了 {previousState.cutEdges.Count} 条边");
        
        // 计算需要恢复和隐藏的边
        var edgesToRestore = new HashSet<(Cell, Cell)>(playerCutEdges);
        var edgesToHide = new HashSet<(Cell, Cell)>(previousState.cutEdges);
        
        // 恢复所有当前被切割的边
        foreach (var cutEdge in edgesToRestore)
        {
            if (_edges.TryGetValue(cutEdge, out var edgeData))
            {
                if (edgeData.renderer != null)
                {
                    edgeData.renderer.gameObject.SetActive(true);
                    UnityEngine.Debug.Log($"✅ 恢复边: {cutEdge.Item1.Number}-{cutEdge.Item2.Number}");
                }
                if (edgeData.bg != null)
                    edgeData.bg.SetActive(useWeightedEdges);
            }
        }
        
        // 恢复到之前的玩家切割状态
        playerCutEdges.Clear();
        foreach (var edge in previousState.cutEdges)
        {
            playerCutEdges.Add(edge);
        }
        
        // 隐藏之前状态中被切割的边
        foreach (var cutEdge in edgesToHide)
        {
            if (_edges.TryGetValue(cutEdge, out var edgeData))
            {
                if (edgeData.renderer != null)
                {
                    edgeData.renderer.gameObject.SetActive(false);
                    UnityEngine.Debug.Log($"❌ 隐藏边: {cutEdge.Item1.Number}-{cutEdge.Item2.Number}");
                }
                if (edgeData.bg != null)
                    edgeData.bg.SetActive(false);
            }
        }
        
        // 更新cost显示
        UpdateCostText();
        
        // 更新按钮状态
        UpdateReturnButtonState();
        
        UnityEngine.Debug.Log($"↶ 回退操作完成！剩余历史记录: {gameStateHistory.Count}");
        UnityEngine.Debug.Log($"📊 最终状态: 切割了 {playerCutEdges.Count} 条边");

        // 回退后重新计算并保存clusters，并通知可视化刷新
        try
        {
            CalculateAndSaveClustersAfterCut();
            NotifyCellTileTestManager();
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning($"⚠️ 回退后刷新簇显示时出错: {ex.Message}");
        }
    }
    

    
    /// <summary>
    /// 更新回退按钮的可用状态
    /// </summary>
    private void UpdateReturnButtonState()
    {
        if (ReturnButton != null)
        {
            ReturnButton.interactable = gameStateHistory.Count > 0;
        }
    }
    
    /// <summary>
    /// 清空回退历史
    /// </summary>
    public void ClearUndoHistory()
    {
        gameStateHistory.Clear();
        UpdateReturnButtonState();
        UnityEngine.Debug.Log("🗑️ 清空回退历史");
    }
    
    /// <summary>
    /// 保存当前操作状态（在完成一次操作后调用）
    /// </summary>
    public void SaveCurrentOperation()
    {
        SaveGameState();
        UnityEngine.Debug.Log($"💾 保存当前操作状态，切割边数量: {playerCutEdges.Count}");
    }
    
    #endregion

    /// <summary>
    /// 清空clusters_after_cut.json文件，避免开局时出现二次高亮
    /// </summary>
    private void ClearClustersFile()
    {
        try
        {
            string filePath = System.IO.Path.Combine(Application.dataPath, "..", "clusters_after_cut.json");
            
            // 创建一个空的初始状态
            var emptyData = new ClustersAfterCutDataDTO();
            emptyData.cut_edges = new CutEdgeDTO[0];
            emptyData.cost = 0;
            emptyData.clusters = new ClusterInfoDTO[0];
            emptyData.cluster_count = 0;
            emptyData.timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            string jsonData = JsonUtility.ToJson(emptyData, true);
            System.IO.File.WriteAllText(filePath, jsonData);
            
            UnityEngine.Debug.Log($"🧹 已清空clusters_after_cut.json文件，避免开局二次高亮");
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning($"⚠️ 清空clusters文件时出错: {ex.Message}");
        }
    }

    // 在场景中查找未激活对象（按路径）
    private GameObject FindInactiveByPath(string path)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var parts = path.Split('/');
        foreach (var root in roots)
        {
            if (root.name != parts[0]) continue;
            Transform current = root.transform;
            bool ok = true;
            for (int i = 1; i < parts.Length; i++)
            {
                current = current.Find(parts[i]);
                if (current == null) { ok = false; break; }
            }
            if (ok && current != null) return current.gameObject;
        }
        return null;
    }

    // 在场景所有根对象下递归查找指定名称（包含未激活）
    private GameObject FindInactiveByName(string name)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t.gameObject;
            }
        }
        return null;
    }

    /// <summary>
    /// 平衡边权重，确保存在一定比例的负权重边，避免最优cost=0的无聊局面
    /// </summary>
    private void BalanceEdgeWeights()
    {
        if (_edges.Count == 0) return;
        
        // 统计当前负边比例
        List<int> weights = new List<int>(_edges.Count);
        int negativeCount = 0;
        foreach (var key in _edges.Keys)
        {
            if (!_edgeWeightCache.TryGetValue(key, out int w))
                w = GetOrCreateEdgeWeight(key.Item1, key.Item2);
            weights.Add(w);
            if (w < 0) negativeCount++;
        }
        
        float negativeRatio = negativeCount / (float)_edges.Count;
        int targetNeg = Mathf.Max(minNegativeEdges, Mathf.CeilToInt(targetNegativeEdgeRatio * _edges.Count));
        
        if (negativeRatio >= targetNegativeEdgeRatio && negativeCount >= minNegativeEdges)
        {
            UnityEngine.Debug.Log($"权重平衡: 已满足负边比例 {negativeRatio:P0}，无需调整");
            return;
        }

        // 更保守的方法：只将最小的几个正数权重变为负数
        weights.Sort(); // 从小到大
        int needToMakeNegative = targetNeg - negativeCount;
        
        UnityEngine.Debug.Log($"权重平衡: 负边比例={negativeRatio:P0} 不足，需要将 {needToMakeNegative} 个最小正边变为负数");
        
        // 找到需要变负的权重边，并记录它们的原始值
        var edgesToFlip = new List<(Cell, Cell)>();
        var keysList = _edges.Keys.ToList();
        
        foreach (var key in keysList)
        {
            if (edgesToFlip.Count >= needToMakeNegative) break;
            
            int w = _edgeWeightCache[key];
            if (w > 0) // 只处理正权重
            {
                edgesToFlip.Add(key);
            }
        }
        
        // 按权重排序，优先翻转最小的正权重
        edgesToFlip.Sort((a, b) => _edgeWeightCache[a].CompareTo(_edgeWeightCache[b]));
        
        // 只翻转需要的数量
        for (int i = 0; i < Mathf.Min(needToMakeNegative, edgesToFlip.Count); i++)
        {
            var key = edgesToFlip[i];
            int oldW = _edgeWeightCache[key];
            int newW = -Mathf.Max(1, oldW); // 变为负数，至少为-1
            
            _edgeWeightCache[key] = newW;
            
            // 更新_edges里存的权重与文本
            var data = _edges[key];
            _edges[key] = (data.renderer, newW, data.tmp, data.bg);
            if (data.tmp != null) data.tmp.text = newW.ToString();
            
            UnityEngine.Debug.Log($"翻转边权重: {oldW} -> {newW}");
        }
    }
}
