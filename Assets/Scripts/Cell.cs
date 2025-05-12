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

    public void Init(int number)
    {
        // 设置单元格的数字
        Number = number;

        // 设置单元格的名称为数字，方便调试
        gameObject.name = $"Cell {Number}";

        // 随机设置单元格颜色（可选）
        _cellSprite.color = new Color(Random.value, Random.value, Random.value);

        // 确保Cell有Collider2D组件
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<BoxCollider2D>();
        }


        // 根据SpriteRenderer的尺寸设置Collider的size
        if (_cellSprite != null)
        {
            collider.size = _cellSprite.bounds.size*5f;
        }
    }

    public void AddEdge(Cell otherCell)
    {
        GameManager.Instance.CreateOrUpdateEdge(this, otherCell);
        GameManager.Instance.CreateOrUpdateEdge(otherCell, this);
    }

    public void RemoveEdge(Cell otherCell)
    {
        GameManager.Instance.RemoveEdge(this, otherCell);
        GameManager.Instance.RemoveEdge(otherCell, this);
    }

    public void RemoveAllEdges()
    {
        GameManager.Instance.RemoveAllEdges();
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