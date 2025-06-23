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
    [SerializeField]
    private Color[] colorPalette = new Color[]
    {
        new Color(0.98f, 0.36f, 0.36f), // 红rgb(166, 27, 78)
        new Color(0.99f, 0.80f, 0.36f), // 黄rgb(86, 81, 27)
        new Color(0.56f, 0.89f, 0.56f), // 绿 #A6E22E
        new Color(0.36f, 0.82f, 0.98f), // 蓝rgb(61, 127, 140)
        new Color(0.82f, 0.36f, 0.98f), // 紫 #AE81FF
        new Color(0.98f, 0.56f, 0.36f), // 橙 #FD971F
        new Color(0.36f, 0.98f, 0.82f), // 青rgb(79, 119, 114)
        new Color(0.36f, 0.36f, 0.36f), // 深灰 #595959
    };

    private int _number; // Backing field for Number (note naming convention difference)

    // 新增：记录与其它Cell的权重
    public Dictionary<Cell, float> EdgeWeights = new Dictionary<Cell, float>();

    public void Init(int number)
    {
        // 设置单元格的数字
        Number = number;

        // 设置单元格的名称为数字，方便调试
        gameObject.name = $"Cell {Number}";

        // 从深色库中随机选一个颜色
        if (colorPalette != null && colorPalette.Length > 0)
        {
            _cellSprite.color = colorPalette[UnityEngine.Random.Range(0, colorPalette.Length)];
        }
        else
        {
            _cellSprite.color = new Color(Random.value, Random.value, Random.value);
        }

        // 确保Cell有Collider2D组件
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<BoxCollider2D>();
        }

        // 根据SpriteRenderer的尺寸设置Collider的size
        if (_cellSprite != null)
        {
            collider.size = _cellSprite.bounds.size * 5f;
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