using UnityEngine;

/// <summary>
/// Edge点击处理器 - 处理edge的点击事件并显示相关信息
/// </summary>
public class EdgeClickHandler : MonoBehaviour
{
    private Cell cellA;
    private Cell cellB;
    private TilemapGameManager gameManager;
    private LineRenderer lineRenderer;
    
    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }
    
    public void Initialize(Cell a, Cell b, TilemapGameManager manager)
    {
        cellA = a;
        cellB = b;
        gameManager = manager;
    }
    
    void OnMouseDown()
    {
        if (gameManager != null)
        {
            // Debug.Log($"🖱️ 点击了Edge: {cellA.Number} -> {cellB.Number}");
            gameManager.ShowEdgeTileInfo(cellA, cellB);
        }
    }
    
    void OnMouseEnter()
    {
        // 鼠标悬停时高亮显示
        if (lineRenderer != null)
        {
            lineRenderer.startWidth *= 1.5f;
            lineRenderer.endWidth *= 1.5f;
        }
    }
    
    void OnMouseExit()
    {
        // 鼠标离开时恢复原状
        if (lineRenderer != null)
        {
            lineRenderer.startWidth /= 1.5f;
            lineRenderer.endWidth /= 1.5f;
        }
    }
} 