using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 难度控制器 - 提供UI界面来调整游戏难度设置
/// </summary>
public class DifficultyController : MonoBehaviour
{
    [Header("UI组件")]
    public Slider difficultySlider;
    public Slider randomFactorSlider;
    public Slider terrainMultiplierSlider;
    public Slider globalOffsetSlider;
    
    [Header("显示组件")]
    public TextMeshProUGUI difficultyText;
    public TextMeshProUGUI randomFactorText;
    public TextMeshProUGUI terrainMultiplierText;
    public TextMeshProUGUI globalOffsetText;
    
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
        if (difficultySlider != null)
        {
            difficultySlider.minValue = 0;
            difficultySlider.maxValue = 10;
            difficultySlider.value = gameManager.difficultySettings.difficultyLevel;
        }
        
        if (randomFactorSlider != null)
        {
            randomFactorSlider.minValue = 0f;
            randomFactorSlider.maxValue = 1f;
            randomFactorSlider.value = gameManager.difficultySettings.randomFactor;
        }
        
        if (terrainMultiplierSlider != null)
        {
            terrainMultiplierSlider.minValue = 0.1f;
            terrainMultiplierSlider.maxValue = 3f;
            terrainMultiplierSlider.value = gameManager.difficultySettings.terrainWeightMultiplier;
        }
        
        if (globalOffsetSlider != null)
        {
            globalOffsetSlider.minValue = -10;
            globalOffsetSlider.maxValue = 10;
            globalOffsetSlider.value = gameManager.difficultySettings.globalWeightOffset;
        }
        
        UpdateDisplayTexts();
    }
    
    private void BindEvents()
    {
        if (difficultySlider != null)
            difficultySlider.onValueChanged.AddListener(OnDifficultyChanged);
        
        if (randomFactorSlider != null)
            randomFactorSlider.onValueChanged.AddListener(OnRandomFactorChanged);
        
        if (terrainMultiplierSlider != null)
            terrainMultiplierSlider.onValueChanged.AddListener(OnTerrainMultiplierChanged);
        
        if (globalOffsetSlider != null)
            globalOffsetSlider.onValueChanged.AddListener(OnGlobalOffsetChanged);
        
        if (applyButton != null)
            applyButton.onClick.AddListener(ApplySettings);
        
        if (resetButton != null)
            resetButton.onClick.AddListener(ResetToOriginal);
        
        if (testButton != null)
            testButton.onClick.AddListener(TestCurrentSettings);
    }
    
    private void OnDifficultyChanged(float value)
    {
        gameManager.difficultySettings.difficultyLevel = Mathf.RoundToInt(value);
        UpdateDisplayTexts();
    }
    
    private void OnRandomFactorChanged(float value)
    {
        gameManager.difficultySettings.randomFactor = value;
        UpdateDisplayTexts();
    }
    
    private void OnTerrainMultiplierChanged(float value)
    {
        gameManager.difficultySettings.terrainWeightMultiplier = value;
        UpdateDisplayTexts();
    }
    
    private void OnGlobalOffsetChanged(float value)
    {
        gameManager.difficultySettings.globalWeightOffset = Mathf.RoundToInt(value);
        UpdateDisplayTexts();
    }
    
    private void UpdateDisplayTexts()
    {
        if (difficultyText != null)
            difficultyText.text = $"难度等级: {gameManager.difficultySettings.difficultyLevel}";
        
        if (randomFactorText != null)
            randomFactorText.text = $"随机因子: {gameManager.difficultySettings.randomFactor:F2}";
        
        if (terrainMultiplierText != null)
            terrainMultiplierText.text = $"地形倍数: {gameManager.difficultySettings.terrainWeightMultiplier:F2}";
        
        if (globalOffsetText != null)
            globalOffsetText.text = $"全局偏移: {gameManager.difficultySettings.globalWeightOffset}";
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
        to.difficultyLevel = from.difficultyLevel;
        to.randomFactor = from.randomFactor;
        to.randomRange = from.randomRange;
        to.terrainWeightMultiplier = from.terrainWeightMultiplier;
        to.globalWeightOffset = from.globalWeightOffset;
        to.easyMultiplier = from.easyMultiplier;
        to.normalMultiplier = from.normalMultiplier;
        to.hardMultiplier = from.hardMultiplier;
    }
    
    [ContextMenu("快速设置 - 简单模式")]
    public void SetEasyMode()
    {
        gameManager.difficultySettings.difficultyLevel = 2;
        gameManager.difficultySettings.randomFactor = 0.1f;
        gameManager.difficultySettings.terrainWeightMultiplier = 0.8f;
        gameManager.difficultySettings.globalWeightOffset = 2;
        InitializeUI();
        ApplySettings();
    }
    
    [ContextMenu("快速设置 - 普通模式")]
    public void SetNormalMode()
    {
        gameManager.difficultySettings.difficultyLevel = 5;
        gameManager.difficultySettings.randomFactor = 0.3f;
        gameManager.difficultySettings.terrainWeightMultiplier = 1.0f;
        gameManager.difficultySettings.globalWeightOffset = 0;
        InitializeUI();
        ApplySettings();
    }
    
    [ContextMenu("快速设置 - 困难模式")]
    public void SetHardMode()
    {
        gameManager.difficultySettings.difficultyLevel = 8;
        gameManager.difficultySettings.randomFactor = 0.5f;
        gameManager.difficultySettings.terrainWeightMultiplier = 1.3f;
        gameManager.difficultySettings.globalWeightOffset = -3;
        InitializeUI();
        ApplySettings();
    }
} 