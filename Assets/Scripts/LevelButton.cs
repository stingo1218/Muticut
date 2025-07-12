using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelCell : MonoBehaviour
{
    public TextMeshProUGUI levelText;      // 关卡编号文本
    public Image iconImage;                // 奖牌/锁等图标
    public Button button;                  // 按钮组件
    // 可扩展：public Image[] starImages;  // 星级显示

    private int levelIndex;
    private bool isUnlocked;

    /// <summary>
    /// 初始化关卡Cell
    /// </summary>
    /// <param name="index">关卡编号</param>
    /// <param name="icon">奖牌/锁图标</param>
    /// <param name="unlocked">是否解锁</param>
    /// <param name="onClick">点击回调</param>
    public void Init(int index, Sprite icon, bool unlocked, System.Action<int> onClick)
    {
        levelIndex = index;
        isUnlocked = unlocked;
        if (levelText != null)
            levelText.text = index.ToString();

        if (iconImage != null && icon != null)
            iconImage.sprite = icon;

        button.interactable = unlocked;
        // 可选：显示锁图标/灰色处理
        if (!unlocked && iconImage != null)
            iconImage.color = Color.gray;
        else if (iconImage != null)
            iconImage.color = Color.white;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke(levelIndex));
    }

    // 可扩展：设置星级显示
    // public void SetStars(int starCount) { ... }
}