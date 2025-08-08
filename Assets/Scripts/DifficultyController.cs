using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 难度控制器 - 提供UI界面来调整游戏难度设置
/// </summary>
public class DifficultyController : MonoBehaviour
{
    [Header("UI组件")]
    public Slider randomFactorSlider;
    
    [Header("显示组件")]
    public TextMeshProUGUI randomFactorText;
    
    [Header("按钮")]
    public Button applyButton;
    public Button resetButton;
    public Button testButton;
    
    private GameManager gameManager;
    private int originalLevelIndex;
    
    private void Start()
    {
        gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            // Debug.LogError("❌ 无法找到GameManager");
            return;
        }
        
        // 保存原始关卡号
        originalLevelIndex = gameManager.levelIndex;
        
        // 初始化UI
        InitializeUI();
        
        // 绑定事件
        BindEvents();
    }
    
    private void InitializeUI()
    {
        if (randomFactorSlider != null)
        {
            randomFactorSlider.minValue = 1f;
            randomFactorSlider.maxValue = 50f;
            randomFactorSlider.wholeNumbers = true;
            randomFactorSlider.value = gameManager.levelIndex;
        }
        
        UpdateDisplayTexts();
    }
    
    private void BindEvents()
    {
        if (randomFactorSlider != null)
            randomFactorSlider.onValueChanged.AddListener(OnRandomFactorChanged);
        
        if (applyButton != null)
            applyButton.onClick.AddListener(ApplySettings);
        
        if (resetButton != null)
            resetButton.onClick.AddListener(ResetToOriginal);
        
        if (testButton != null)
            testButton.onClick.AddListener(TestCurrentSettings);
    }
    
    private void OnRandomFactorChanged(float value)
    {
        gameManager.levelIndex = Mathf.RoundToInt(value);
        UpdateDisplayTexts();
    }
    
    private void UpdateDisplayTexts()
    {
        if (randomFactorText != null)
            randomFactorText.text = $"关卡: {gameManager.levelIndex}";
    }
    
    public void ApplySettings()
    {
        // Debug.Log("✅ 应用难度设置...");
        gameManager.RecalculateAllEdgeWeights();
    }
    
    public void ResetToOriginal()
    {
        // Debug.Log("🔄 重置为原始设置...");
        gameManager.levelIndex = originalLevelIndex;
        InitializeUI();
        gameManager.RecalculateAllEdgeWeights();
    }
    
    public void TestCurrentSettings()
    {
        // Debug.Log("🧪 测试当前设置...");
        gameManager.TestLevelWeightEffects();
    }
    
    [ContextMenu("快速设置 - 纯地形模式")]
    public void SetTerrainOnlyMode()
    {
        gameManager.levelIndex = 1;
        InitializeUI();
        ApplySettings();
    }
    
    [ContextMenu("快速设置 - 混合模式")]
    public void SetMixedMode()
    {
        gameManager.levelIndex = 10;
        InitializeUI();
        ApplySettings();
    }
    
    [ContextMenu("快速设置 - 纯随机模式")]
    public void SetRandomOnlyMode()
    {
        gameManager.levelIndex = 30;
        InitializeUI();
        ApplySettings();
    }
} 