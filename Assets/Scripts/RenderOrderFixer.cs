using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 渲染顺序修复器 - 解决LineRenderer被Tilemap遮挡的问题
/// </summary>
public class RenderOrderFixer : MonoBehaviour
{
    [Header("调试选项")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool autoFixOnStart = true;
    
    void Start()
    {
        if (autoFixOnStart)
        {
            CheckAndFixRenderOrder();
        }
    }
    
    [ContextMenu("检查并修复渲染顺序")]
    public void CheckAndFixRenderOrder()
    {
        // Debug.Log("🔍 开始检查渲染顺序...");
        
        // 检查Tilemap的渲染设置
        CheckTilemapRenderSettings();
        
        // 检查LineRenderer的渲染设置
        CheckLineRendererSettings();
        
        // 尝试修复渲染顺序
        FixRenderOrder();
    }
    
    private void CheckTilemapRenderSettings()
    {
        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        // Debug.Log($"🔍 找到 {tilemaps.Length} 个Tilemap");
        
        foreach (var tilemap in tilemaps)
        {
            var renderer = tilemap.GetComponent<Renderer>();
            if (renderer != null)
            {
                            // Debug.Log($"🔍 Tilemap '{tilemap.name}':");
            // Debug.Log($"  - Sorting Layer: {renderer.sortingLayerName}");
            // Debug.Log($"  - Order in Layer: {renderer.sortingOrder}");
            // Debug.Log($"  - GameObject Layer: {tilemap.gameObject.layer}");
            }
        }
    }
    
    private void CheckLineRendererSettings()
    {
        LineRenderer[] lineRenderers = FindObjectsByType<LineRenderer>(FindObjectsSortMode.None);
        // Debug.Log($"🔍 找到 {lineRenderers.Length} 个LineRenderer");
        
        foreach (var lineRenderer in lineRenderers)
        {
            // Debug.Log($"🔍 LineRenderer '{lineRenderer.name}':");
            // Debug.Log($"  - Sorting Layer: {lineRenderer.sortingLayerName}");
            // Debug.Log($"  - Order in Layer: {lineRenderer.sortingOrder}");
            // Debug.Log($"  - GameObject Layer: {lineRenderer.gameObject.layer}");
        }
    }
    
    private void FixRenderOrder()
    {
        // Debug.Log("🔧 开始修复渲染顺序...");
        
        // 获取Tilemap的最高sortingOrder
        int maxTilemapOrder = 0;
        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        foreach (var tilemap in tilemaps)
        {
            var renderer = tilemap.GetComponent<Renderer>();
            if (renderer != null && renderer.sortingOrder > maxTilemapOrder)
            {
                maxTilemapOrder = renderer.sortingOrder;
            }
        }
        
        // Debug.Log($"🔍 Tilemap最高sortingOrder: {maxTilemapOrder}");
        
        // 设置LineRenderer的sortingOrder低于Tilemap，确保在cells之下
        LineRenderer[] lineRenderers = FindObjectsByType<LineRenderer>(FindObjectsSortMode.None);
        int newOrder = 1; // 设置为较低的排序顺序
        
        foreach (var lineRenderer in lineRenderers)
        {
            lineRenderer.sortingOrder = newOrder;
            lineRenderer.sortingLayerName = "Default"; // 使用与Tilemap相同的层
            lineRenderer.gameObject.layer = LayerMask.NameToLayer("Default");
            
            // Debug.Log($"🔧 修复LineRenderer '{lineRenderer.name}':");
            // Debug.Log($"  - 新Sorting Order: {newOrder}");
            // Debug.Log($"  - 新Sorting Layer: Default");
        }
        
        // Debug.Log("✅ 渲染顺序修复完成");
    }
    
    [ContextMenu("重置LineRenderer为UI层")]
    public void ResetLineRenderersToUI()
    {
        LineRenderer[] lineRenderers = FindObjectsByType<LineRenderer>(FindObjectsSortMode.None);
        
        foreach (var lineRenderer in lineRenderers)
        {
            lineRenderer.sortingOrder = 1; // 设置较低的排序顺序，确保在cells之下
            lineRenderer.sortingLayerName = "Default"; // 设置为Default层，与cells保持一致
            lineRenderer.gameObject.layer = LayerMask.NameToLayer("Default"); // 设置GameObject的Layer为Default
        }
        
        // Debug.Log("🔄 LineRenderer已重置为UI层");
    }
} 