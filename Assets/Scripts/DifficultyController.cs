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
    private GameManager.DifficultySettings originalSettings;
    
    private void Start()
    {
        gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogError("❌ 无法找到GameManager");
            return;
        }
        
        // 保存原始设置
        originalSettings = new GameManager.DifficultySettings();
        CopySettings(gameManager.difficultySettings, originalSettings);
        
        // 初始化UI
        InitializeUI();
        
        // 绑定事件
        BindEvents();
    }
    
    private void InitializeUI()
    {
        if (randomFactorSlider != null)
        {
            randomFactorSlider.minValue = 0f;
            randomFactorSlider.maxValue = 1f;
            randomFactorSlider.value = gameManager.difficultySettings.randomFactor;
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
        gameManager.difficultySettings.randomFactor = value;
        UpdateDisplayTexts();
    }
    
    private void UpdateDisplayTexts()
    {
        if (randomFactorText != null)
            randomFactorText.text = $"随机因子: {gameManager.difficultySettings.randomFactor:F2}";
    }
    
    public void ApplySettings()
    {
        Debug.Log("✅ 应用难度设置...");
        gameManager.RecalculateAllEdgeWeights();
    }
    
    public void ResetToOriginal()
    {
        Debug.Log("🔄 重置为原始设置...");
        CopySettings(originalSettings, gameManager.difficultySettings);
        InitializeUI();
        gameManager.RecalculateAllEdgeWeights();
    }
    
    public void TestCurrentSettings()
    {
        Debug.Log("🧪 测试当前设置...");
        gameManager.TestDifficultySettings();
    }
    
    private void CopySettings(GameManager.DifficultySettings from, GameManager.DifficultySettings to)
    {
        to.randomFactor = from.randomFactor;
        to.randomRange = from.randomRange;
    }
    
    [ContextMenu("快速设置 - 纯地形模式")]
    public void SetTerrainOnlyMode()
    {
        gameManager.difficultySettings.randomFactor = 0f;
        InitializeUI();
        ApplySettings();
    }
    
    [ContextMenu("快速设置 - 混合模式")]
    public void SetMixedMode()
    {
        gameManager.difficultySettings.randomFactor = 0.3f;
        InitializeUI();
        ApplySettings();
    }
    
    [ContextMenu("快速设置 - 纯随机模式")]
    public void SetRandomOnlyMode()
    {
        gameManager.difficultySettings.randomFactor = 1f;
        InitializeUI();
        ApplySettings();
    }
} 