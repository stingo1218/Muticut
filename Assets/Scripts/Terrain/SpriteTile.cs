using UnityEngine;
using UnityEngine.Tilemaps;

namespace TerrainSystem
{
    [CreateAssetMenu(fileName = "New Sprite Tile", menuName = "Terrain/Sprite Tile")]
    public class SpriteTile : TileBase
    {
        [SerializeField] private Sprite sprite;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private Matrix4x4 transform = Matrix4x4.identity;

        public Sprite Sprite
        {
            get => sprite;
            set => sprite = value;
        }

        public Color Color
        {
            get => color;
            set => color = value;
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            tileData.transform = transform;
            tileData.color = color;
            tileData.sprite = sprite;
        }
    }
} 