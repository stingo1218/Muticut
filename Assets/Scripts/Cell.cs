using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Cell : MonoBehaviour
{
    // Public properties
    [HideInInspector]
    public int Number
    {
        get
        {
            return _number;
        }
        set
        {
            _number = value; // 设置数字
            if (_numberText != null)
            {
                _numberText.text = _number.ToString(); // 更新显示的文本
            }
        }
    }

    // Serialized fields (visible in Unity Inspector)
    [SerializeField] private TMP_Text _numberText;    // Text component for displaying number
    [SerializeField] private SpriteRenderer _cellSprite; // Background sprite renderer

    private int _number; // Backing field for Number (note naming convention difference)

    // 新增：记录与其它Cell的权重
    public Dictionary<Cell, float> EdgeWeights = new Dictionary<Cell, float>();

    public void Init(int number, bool isWeightLabel = false)
    {
        // 设置单元格的数字
        Number = number;

        // 设置单元格的名称为数字，方便调试
        gameObject.name = $"Cell {Number}";

        // 根据用途设置不同的渲染层级
        if (_cellSprite != null)
        {
            if (isWeightLabel)
            {
                _cellSprite.sortingOrder = 20; // 权重标签背景层
            }
            else
            {
                _cellSprite.sortingOrder = 15; // 普通Cell背景层
            }
            _cellSprite.sortingLayerName = "Default";
        }

        if (_numberText != null)
        {
            // 对于TMP_Text，需要通过其父级Canvas设置sortingOrder
            var canvas = _numberText.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                if (isWeightLabel)
                {
                    canvas.sortingOrder = 40; // 权重标签文本层
                }
                else
                {
                    canvas.sortingOrder = 35; // 普通Cell文本层
                }
                canvas.sortingLayerName = "Default";
            }
        }

        // 确保Cell有Collider2D组件
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<BoxCollider2D>();
        }

        // 根据用途设置不同的碰撞器大小
        if (isWeightLabel)
        {
            // Weight标签使用与Cell预制件一致的大小
            // Cell预制件的Sprite大小是2x2，缩放是0.82，所以实际大小是1.64x1.64
            float cellSize = 2f * 0.82f; // 1.64
            collider.size = new Vector2(cellSize, cellSize);
            collider.isTrigger = true;
        }
        else
        {
            // 普通Cell也使用2x2的碰撞器大小
            collider.size = new Vector2(2f, 2f);
        }
    }

    // 新增：支持权重的AddEdge
    public void AddEdge(Cell otherCell, float weight = 1f)
    {
        // 记录权重
        EdgeWeights[otherCell] = weight;
        // 只调用原有的CreateOrUpdateEdge（暂不传权重）
        GameManager.Instance.CreateOrUpdateEdge(this, otherCell);
        GameManager.Instance.CreateOrUpdateEdge(otherCell, this);
    }

    // 保留原有无权重AddEdge（兼容旧代码）
    public void AddEdge(Cell otherCell)
    {
        AddEdge(otherCell, 1f);
    }

    // 移除边时同步移除权重
    public void RemoveEdge(Cell otherCell)
    {
        if (EdgeWeights.ContainsKey(otherCell))
            EdgeWeights.Remove(otherCell);
        GameManager.Instance.RemoveEdge(this, otherCell);
        GameManager.Instance.RemoveEdge(otherCell, this);
    }

    public void RemoveAllEdges()
    {
        GameManager.Instance.RemoveAllEdges();
    }

    // 可选：获取与某Cell的权重
    public float GetEdgeWeight(Cell otherCell)
    {
        if (EdgeWeights.TryGetValue(otherCell, out float w))
            return w;
        return 1f;
    }

    /// <summary>
    /// 调整Cell的渲染顺序，确保TMP文本显示在背景之上
    /// </summary>
    [ContextMenu("调整渲染顺序")]
    public void AdjustRenderingOrder()
    {
        // 判断是否为权重标签（通过名称或大小判断）
        bool isWeightLabel = gameObject.name.Contains("Weight") || gameObject.name.Contains("EdgeWeight") || 
                           (_cellSprite != null && _cellSprite.transform.localScale.x < 1f);

        if (_cellSprite != null)
        {
            if (isWeightLabel)
            {
                _cellSprite.sortingOrder = 20; // 权重标签背景层
            }
            else
            {
                _cellSprite.sortingOrder = 15; // 普通Cell背景层
            }
            _cellSprite.sortingLayerName = "Default";
        }

        if (_numberText != null)
        {
            // 对于TMP_Text，需要通过其父级Canvas设置sortingOrder
            var canvas = _numberText.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                if (isWeightLabel)
                {
                    canvas.sortingOrder = 40; // 权重标签文本层
                }
                else
                {
                    canvas.sortingOrder = 35; // 普通Cell文本层
                }
                canvas.sortingLayerName = "Default";
            }
        }

        string type = isWeightLabel ? "权重标签" : "Cell";
        string backgroundOrder = isWeightLabel ? "20" : "15";
        string textOrder = isWeightLabel ? "40" : "35";
        Debug.Log($"✅ {type} {Number} 渲染顺序已调整：背景({backgroundOrder}) < 文本({textOrder})");
    }

    private void ChangeSpriteSize(SpriteRenderer sprite, float size)
    {
        // TODO: Implement logic to change the size of a sprite
    }

    public bool IsValidCell(Cell cell, int direction)
    {
        // TODO: Implement validation logic for Muticut game
        return true; // Placeholder for actual validation logic
    }
}