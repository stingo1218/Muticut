using UnityEngine;
using System.Collections.Generic;

namespace TerrainSystem
{
    // 六边形坐标系统（对应 JS 中的 Honeycomb 库功能）
    public class HexCoordinateSystem
    {
        public enum HexOrientation
        {
            Flat,   // 平放（对应 JS 中的 'flat'）
            Pointy  // 尖角（对应 JS 中的 'pointy'）
        }

        // 轴向坐标 (q, r)
        [System.Serializable]
        public struct AxialCoord
        {
            public int q; // 列
            public int r; // 行

            public AxialCoord(int q, int r)
            {
                this.q = q;
                this.r = r;
            }

            public override string ToString()
            {
                return $"({q}, {r})";
            }

            public override bool Equals(object obj)
            {
                if (obj is AxialCoord other)
                {
                    return q == other.q && r == other.r;
                }
                return false;
            }

            public override int GetHashCode()
            {
                return q.GetHashCode() ^ r.GetHashCode();
            }
        }

        // 六边形数据（对应 JS 中的 hex 对象）
        [System.Serializable]
        public class HexTile
        {
            public AxialCoord coord;
            public Vector3 worldPosition;
            public float elevation;
            public float moisture;
            public BiomeType biome;
            public string tileName;
            public bool hasRiver;
            public int riverId;
            public bool isRiverSource;
            public bool isRiverEnd;
            public int sideRiverExit;
            public int sideRiverEnter;
            public bool riverJoin;
            public bool sourceSon;

            public HexTile(AxialCoord coord, HexCoordinateSystem hexSystem)
            {
                this.coord = coord;
                this.worldPosition = hexSystem.AxialToWorld(coord);
            }

            public override string ToString()
            {
                return $"Hex{coord} - {biome} - Elev:{elevation:F2} - Moist:{moisture:F2}";
            }
        }

        // 生物群系类型（对应 JS 中的 biome 分类）
        public enum BiomeType
        {
            DeepWater,
            ShallowWater,
            FlatDesert1,
            FlatDesert2,
            FlatGrass,
            FlatSparseTrees1,
            FlatSparseTrees2,
            FlatForest,
            FlatForestSwampy,
            HillDesert,
            HillGrass,
            HillForest,
            HillForestNeedleleaf,
            MountainDesert,
            MountainShrubland1,
            MountainShrubland2,
            MountainAlpine1,
            MountainAlpine2,
            MountainImpassable1,
            MountainImpassable2,
            Lake1,
            Lake2,
            Lake3,
            Lake4,
            Volcano,
            Lair,
            LairSnow,
            LairDesert
        }

        private HexOrientation orientation;
        private float hexSize;

        public HexCoordinateSystem(HexOrientation orientation = HexOrientation.Flat, float hexSize = 16f)
        {
            this.orientation = orientation;
            this.hexSize = hexSize;
        }

        // 轴向坐标转世界坐标（对应 JS 中的 hex.cartesian()）
        public Vector3 AxialToWorld(AxialCoord coord)
        {
            float x, y, z;

            if (orientation == HexOrientation.Flat)
            {
                x = hexSize * (3f / 2f * coord.q);
                y = hexSize * (Mathf.Sqrt(3) / 2f * coord.q + Mathf.Sqrt(3) * coord.r);
                z = 0;
            }
            else // Pointy
            {
                x = hexSize * (Mathf.Sqrt(3) * coord.q + Mathf.Sqrt(3) / 2f * coord.r);
                y = hexSize * (3f / 2f * coord.r);
                z = 0;
            }

            return new Vector3(x, y, z);
        }

        // 世界坐标转轴向坐标
        public AxialCoord WorldToAxial(Vector3 worldPos)
        {
            int q, r;

            if (orientation == HexOrientation.Flat)
            {
                q = Mathf.RoundToInt((2f / 3f * worldPos.x) / hexSize);
                r = Mathf.RoundToInt((-1f / 3f * worldPos.x + Mathf.Sqrt(3) / 3f * worldPos.y) / hexSize);
            }
            else // Pointy
            {
                q = Mathf.RoundToInt((Mathf.Sqrt(3) / 3f * worldPos.x - 1f / 3f * worldPos.y) / hexSize);
                r = Mathf.RoundToInt((2f / 3f * worldPos.y) / hexSize);
            }

            return new AxialCoord(q, r);
        }

        // 生成矩形网格（对应 JS 中的 Grid.rectangle()）
        public List<HexTile> GenerateRectangularGrid(int width, int height)
        {
            List<HexTile> hexes = new List<HexTile>();

            for (int q = 0; q < width; q++)
            {
                for (int r = 0; r < height; r++)
                {
                    AxialCoord coord = new AxialCoord(q, r);
                    HexTile hex = new HexTile(coord, this);
                    hexes.Add(hex);
                }
            }

            return hexes;
        }

        // 获取邻居（对应 JS 中的 gr.neighborsOf(hex)）
        public List<HexTile> GetNeighbors(HexTile hex, List<HexTile> allHexes)
        {
            List<HexTile> neighbors = new List<HexTile>();
            AxialCoord[] neighborOffsets;

            if (orientation == HexOrientation.Flat)
            {
                neighborOffsets = new AxialCoord[]
                {
                    new AxialCoord(1, 0),   // 东
                    new AxialCoord(1, -1),  // 东北
                    new AxialCoord(0, -1),  // 西北
                    new AxialCoord(-1, 0),  // 西
                    new AxialCoord(-1, 1),  // 西南
                    new AxialCoord(0, 1)    // 东南
                };
            }
            else // Pointy
            {
                neighborOffsets = new AxialCoord[]
                {
                    new AxialCoord(1, 0),   // 东
                    new AxialCoord(0, -1),  // 北
                    new AxialCoord(-1, -1), // 西北
                    new AxialCoord(-1, 0),  // 西
                    new AxialCoord(0, 1),   // 南
                    new AxialCoord(1, 1)    // 东南
                };
            }

            foreach (AxialCoord offset in neighborOffsets)
            {
                AxialCoord neighborCoord = new AxialCoord(
                    hex.coord.q + offset.q,
                    hex.coord.r + offset.r
                );

                HexTile neighbor = allHexes.Find(h => h.coord.Equals(neighborCoord));
                if (neighbor != null)
                {
                    neighbors.Add(neighbor);
                }
            }

            return neighbors;
        }

        // 计算六边形中心到顶点的距离
        public float GetHexRadius()
        {
            return hexSize;
        }

        // 获取六边形的顶点位置
        public Vector3[] GetHexVertices(Vector3 center)
        {
            Vector3[] vertices = new Vector3[6];
            float radius = GetHexRadius();

            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f * Mathf.Deg2Rad;
                if (orientation == HexOrientation.Flat)
                {
                    angle += 30f * Mathf.Deg2Rad; // 平放时旋转30度
                }

                vertices[i] = center + new Vector3(
                    radius * Mathf.Cos(angle),
                    radius * Mathf.Sin(angle),
                    0
                );
            }

            return vertices;
        }

        // 设置六边形大小
        public void SetHexSize(float size)
        {
            hexSize = size;
        }

        // 获取六边形大小
        public float GetHexSize()
        {
            return hexSize;
        }
    }
} 