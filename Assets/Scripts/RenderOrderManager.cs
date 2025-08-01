using UnityEngine;

public class RenderOrderManager : MonoBehaviour
{
    [System.Serializable]
    public class LayerSettings
    {
        public string layerName;
        public int baseOrder;
        public Color debugColor = Color.white;
    }
    
    [Header("渲染层设置")]
    public LayerSettings[] layerSettings = new LayerSettings[]
    {
        new LayerSettings { layerName = "Background", baseOrder = 0, debugColor = Color.gray },
        new LayerSettings { layerName = "Terrain", baseOrder = 100, debugColor = Color.green },
        new LayerSettings { layerName = "Water", baseOrder = 200, debugColor = Color.blue },
        new LayerSettings { layerName = "Objects", baseOrder = 300, debugColor = Color.yellow },
        new LayerSettings { layerName = "UI", baseOrder = 400, debugColor = Color.red }
    };
    
    [Header("调试选项")]
    public bool showDebugInfo = true;
    public bool autoSetupOnStart = true;
    
    void Start()
    {
        if (autoSetupOnStart)
        {
            SetupRenderOrder();
        }
    }
    
    [ContextMenu("设置渲染顺序")]
    public void SetupRenderOrder()
    {
        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        
        foreach (SpriteRenderer sr in renderers)
        {
            // 根据对象名称或标签自动分配层
            string layerName = DetermineLayerName(sr.gameObject);
            int order = GetOrderForLayer(layerName);
            
            sr.sortingLayerName = layerName;
            sr.sortingOrder = order;
            
            if (showDebugInfo)
            {
                Debug.Log($"设置 {sr.gameObject.name} 的渲染层为 {layerName}, 顺序为 {order}");
            }
        }
    }
    
    private string DetermineLayerName(GameObject obj)
    {
        string objName = obj.name.ToLower();
        
        if (objName.Contains("background") || objName.Contains("bg"))
            return "Background";
        else if (objName.Contains("terrain") || objName.Contains("hex") || objName.Contains("tile"))
            return "Terrain";
        else if (objName.Contains("water") || objName.Contains("river"))
            return "Water";
        else if (objName.Contains("ui") || objName.Contains("button"))
            return "UI";
        else
            return "Objects";
    }
    
    private int GetOrderForLayer(string layerName)
    {
        foreach (LayerSettings setting in layerSettings)
        {
            if (setting.layerName == layerName)
                return setting.baseOrder;
        }
        return 0;
    }
    
    [ContextMenu("创建排序层")]
    public void CreateSortingLayers()
    {
        Debug.Log("请在 Project Settings > Tags and Layers 中手动创建以下排序层：");
        foreach (LayerSettings setting in layerSettings)
        {
            Debug.Log($"- {setting.layerName}");
        }
    }
    
    void OnDrawGizmos()
    {
        if (!showDebugInfo) return;
        
        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        foreach (SpriteRenderer sr in renderers)
        {
            string layerName = sr.sortingLayerName;
            Color debugColor = GetDebugColorForLayer(layerName);
            
            Gizmos.color = debugColor;
            Vector3 pos = sr.transform.position;
            Gizmos.DrawWireCube(pos, Vector3.one * 0.5f);
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(pos + Vector3.up * 0.6f, 
                $"{sr.gameObject.name}\n{sr.sortingLayerName}:{sr.sortingOrder}");
            #endif
        }
    }
    
    private Color GetDebugColorForLayer(string layerName)
    {
        foreach (LayerSettings setting in layerSettings)
        {
            if (setting.layerName == layerName)
                return setting.debugColor;
        }
        return Color.white;
    }
} 