using UnityEngine;

public class WeightClickHandler : MonoBehaviour
{
    [Header("调试信息")]
    [SerializeField] private string cellAInfo = "";
    [SerializeField] private string cellBInfo = "";
    
    private Cell cellA;
    private Cell cellB;
    private TilemapGameManager gameManager;
    private BoxCollider2D boxCollider;

    public void Initialize(Cell cellA, Cell cellB, TilemapGameManager gameManager)
    {
        this.cellA = cellA;
        this.cellB = cellB;
        this.gameManager = gameManager;
        
        // 更新Inspector中的调试信息
        cellAInfo = cellA != null ? $"Cell {cellA.Number}" : "null";
        cellBInfo = cellB != null ? $"Cell {cellB.Number}" : "null";
        
        // 获取已有的碰撞器
        boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            // 确保碰撞器是trigger
            boxCollider.isTrigger = true;
            Debug.Log($"✅ WeightClickHandler初始化完成: Cell {cellA.Number} -> Cell {cellB.Number}, 碰撞器大小: {boxCollider.size}, 位置: {transform.position}");
            
            // 检查Layer设置
            Debug.Log($"🔍 Layer检查: {gameObject.name} 的Layer是 {LayerMask.LayerToName(gameObject.layer)}");
            
            // 检查Camera设置
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Debug.Log($"🔍 Camera检查: 主摄像机的Culling Mask包含Layer {LayerMask.LayerToName(gameObject.layer)}: {mainCamera.cullingMask == (mainCamera.cullingMask | (1 << gameObject.layer))}");
            }
        }
        else
        {
            Debug.LogError("❌ WeightClickHandler: 未找到BoxCollider2D组件");
        }
    }

    private void OnMouseDown()
    {
        Debug.Log($"🖱️ WeightClickHandler.OnMouseDown() 被调用: {gameObject.name}");
        
        // 检查组件引用
        if (cellA == null)
        {
            Debug.LogError("❌ cellA 是 null!");
            return;
        }
        
        if (cellB == null)
        {
            Debug.LogError("❌ cellB 是 null!");
            return;
        }
        
        if (gameManager == null)
        {
            Debug.LogError("❌ gameManager 是 null!");
            return;
        }
        
        Debug.Log($"✅ 所有组件引用正常，调用 ShowEdgeTileInfo...");
        
        // 调用游戏管理器显示边缘信息
        gameManager.ShowEdgeTileInfo(cellA, cellB);
    }

    private void OnMouseEnter()
    {
        // 鼠标悬停效果 - 可以添加高亮效果
        Debug.Log($"🖱️ 鼠标悬停在Weight标签上: Cell {cellA.Number} -> Cell {cellB.Number}");
    }

    private void OnMouseExit()
    {
        // 鼠标离开效果
        Debug.Log($"🖱️ 鼠标离开Weight标签: Cell {cellA.Number} -> Cell {cellB.Number}");
    }
    
    // 移除ContextMenu，改为public方法，这样会显示在Inspector中
    public void TestClickEvent()
    {
        Debug.Log($"🧪 手动测试点击事件: {gameObject.name}");
        OnMouseDown();
    }
} 