using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class FailPanelController : MonoBehaviour
{
    [SerializeField] private Button backButton;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private bool hideOnAwake = true;
    [SerializeField] private string menuSceneName = "MainMenu"; // 主菜单场景名称

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (backButton == null)
            backButton = GetComponentInChildren<Button>(true);

        if (backButton != null)
            backButton.onClick.AddListener(HandleBackClicked);

        if (hideOnAwake)
            gameObject.SetActive(false);
    }

    public void Show()
    {
        var parentCanvas = GetComponentInParent<Canvas>(true);
        if (parentCanvas != null && !parentCanvas.enabled)
            parentCanvas.enabled = true;

        transform.SetAsLastSibling();
        gameObject.SetActive(true);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        var rect = GetComponent<RectTransform>();
        if (rect != null && rect.localScale == Vector3.zero)
            rect.localScale = Vector3.one;

        UnityEngine.Debug.Log("💀 失败Panel已显示");
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void HandleBackClicked()
    {
        UnityEngine.Debug.Log("🔙 玩家点击返回主菜单");
        
        // 恢复游戏时间
        Time.timeScale = 1f;
        
        // 隐藏失败Panel
        Hide();

        // 返回主菜单
        try
        {
            // 优先按名称加载
            SceneManager.LoadScene(menuSceneName);
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"无法加载场景 '{menuSceneName}': {ex.Message}");
            // 尝试按索引加载第一个场景（通常是主菜单）
            SceneManager.LoadScene(0);
        }
    }
}
