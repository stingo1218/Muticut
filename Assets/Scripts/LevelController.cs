using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class LevelController : MonoBehaviour
{
    public int totalLevels = 100; // 总关卡数，可在Inspector设置
    public GameObject levelCellPrefab; // 拖入LevelCell预制体
    public Sprite[] iconSprites;       // 奖牌/锁等图标数组
    private RectTransform contentParent;
    private RectTransform viewportRect;

    void Start()
    {
        // 自动查找 Content 和 Viewport
        var scrollView = GameObject.Find("Scroll View");
        if (scrollView != null)
        {
            var viewport = scrollView.transform.Find("Viewport");
            if (viewport != null)
            {
                viewportRect = viewport.GetComponent<RectTransform>();
                var content = viewport.Find("Content");
                if (content != null)
                {
                    contentParent = content.GetComponent<RectTransform>();
                }
            }
        }
        GenerateLevelCells();
        // 强制刷新布局，确保滚动条立即可用
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent.GetComponent<RectTransform>());
    }

    void GenerateLevelCells()
    {
        if (levelCellPrefab == null)
        {
            // Debug.LogError("levelCellPrefab未赋值！");
            return;
        }
        if (contentParent == null)
        {
            // Debug.LogError("contentParent未赋值！");
            return;
        }

        for (int i = 1; i <= totalLevels; i++)
        {
            GameObject cellObj = Instantiate(levelCellPrefab, contentParent);
            var cell = cellObj.GetComponent<LevelCell>();
            if (cell == null)
            {
                // Debug.LogError("LevelCell脚本未挂在预制体上！");
                continue;
            }

            // 只解锁第一个关卡，其余未解锁
            bool isUnlocked = (i == 1);

            // 奖牌图标只给已通关关卡（这里只做示例，全部为null）
            Sprite icon = null;

            cell.Init(i, icon, isUnlocked, OnLevelCellClick);
        }
    }

    void OnLevelCellClick(int level)
    {
        // Debug.Log($"点击了关卡：{level}");
        // 这里可以加载关卡或弹窗
    }
}
