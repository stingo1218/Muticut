using UnityEngine;
using System.Collections.Generic;

namespace TerrainSystem
{
    // 河流生成器（对应 JS 中的 drawRiver 和 drawRiverTile 函数）
    public class RiverGenerator
    {
        private HexCoordinateSystem hexSystem;
        private BiomeMapper biomeMapper;
        private NoiseGenerator noiseGenerator;
        private int nextRiverId = 1;

        public RiverGenerator(HexCoordinateSystem hexSystem, BiomeMapper biomeMapper, NoiseGenerator noiseGenerator)
        {
            this.hexSystem = hexSystem;
            this.biomeMapper = biomeMapper;
            this.noiseGenerator = noiseGenerator;
        }

        // 生成河流（对应 JS 中的河流生成逻辑）
        public void GenerateRivers(List<HexCoordinateSystem.HexTile> allHexes, TerrainSettings settings, int riverCount = 3)
        {
            // 找到河流源点（高海拔区域）
            List<HexCoordinateSystem.HexTile> riverSources = FindRiverSources(allHexes, settings);
            
            // 限制河流数量
            int actualRiverCount = Mathf.Min(riverCount, riverSources.Count);
            
            for (int i = 0; i < actualRiverCount; i++)
            {
                HexCoordinateSystem.HexTile source = riverSources[i];
                source.isRiverSource = true;
                source.riverId = nextRiverId++;
                
                // 生成河流路径
                DrawRiver(source, allHexes, settings);
            }
        }

        // 找到河流源点（对应 JS 中的河流源点选择逻辑）
        private List<HexCoordinateSystem.HexTile> FindRiverSources(List<HexCoordinateSystem.HexTile> allHexes, TerrainSettings settings)
        {
            List<HexCoordinateSystem.HexTile> sources = new List<HexCoordinateSystem.HexTile>();
            
            foreach (HexCoordinateSystem.HexTile hex in allHexes)
            {
                // 选择高海拔区域作为河流源点
                if (hex.elevation > settings.contourInterval_3 && 
                    hex.elevation < settings.contourInterval_4 &&
                    !biomeMapper.IsVolcano(hex.biome))
                {
                    // 随机选择一些高海拔区域作为源点
                    if (noiseGenerator.GetRandomValue() < 20) // 20% 概率
                    {
                        sources.Add(hex);
                    }
                }
            }
            
            return sources;
        }

        // 绘制河流（对应 JS 中的 drawRiver 函数）
        private void DrawRiver(HexCoordinateSystem.HexTile hex, List<HexCoordinateSystem.HexTile> allHexes, TerrainSettings settings)
        {
            if (hex.isRiverEnd)
            {
                return;
            }

            List<HexCoordinateSystem.HexTile> neighbors = hexSystem.GetNeighbors(hex, allHexes);
            HexCoordinateSystem.HexTile destination = null;

            // 寻找河流目标
            for (int i = 0; i < neighbors.Count; i++)
            {
                HexCoordinateSystem.HexTile neighbor = neighbors[i];
                
                if (neighbor == null) continue;

                // 检查是否到达水体
                if (biomeMapper.IsWater(neighbor.biome) || biomeMapper.IsLake(neighbor.biome))
                {
                    hex.sideRiverExit = i;
                    hex.isRiverEnd = true;
                    destination = null;
                    hex.hasRiver = false;
                    break;
                }

                // 检查边界
                if (neighbor.coord.q >= settings.hexColumns - 1 && hex.isRiverSource) continue;
                if (neighbor.coord.r >= settings.hexRows - 1 && hex.isRiverSource) continue;

                // 避免循环
                if (neighbor.riverId == hex.riverId) continue;
                if (neighbor.riverId != 0 && neighbor.isRiverSource) continue;

                // 避免火山
                if (biomeMapper.IsVolcano(neighbor.biome)) continue;

                // 选择最佳目标
                if (destination == null)
                {
                    destination = neighbor;
                }
                else
                {
                    // 优先选择低海拔区域
                    int currentElevation = biomeMapper.GetBiomeElevation(destination.biome);
                    int neighborElevation = biomeMapper.GetBiomeElevation(neighbor.biome);
                    
                    if (neighborElevation < currentElevation)
                    {
                        destination = neighbor;
                    }
                    // 如果海拔相同，源点选择湿度高的，非源点选择海拔低的
                    else if (neighborElevation == currentElevation)
                    {
                        if (hex.isRiverSource)
                        {
                            if (neighbor.moisture > destination.moisture)
                            {
                                destination = neighbor;
                            }
                        }
                        else
                        {
                            if (neighbor.elevation < destination.elevation)
                            {
                                destination = neighbor;
                            }
                        }
                    }
                }
            }

            // 设置河流连接
            if (destination != null)
            {
                if (hex.isRiverSource)
                {
                    destination.sourceSon = true;
                }

                int destinationIndex = neighbors.IndexOf(destination);
                hex.sideRiverExit = destinationIndex;
                destination.sideRiverEnter = destinationIndex > 2 ? destinationIndex - 3 : destinationIndex + 3;

                // 检查是否到达水体
                if (biomeMapper.IsWater(destination.biome) || 
                    (biomeMapper.IsLake(destination.biome) && destination.biome.ToString().Contains("Flat")))
                {
                    destination.isRiverEnd = true;
                }
                // 检查河流汇合
                else if (destination.riverId != 0 && hex.riverId != destination.riverId && !destination.isRiverSource)
                {
                    hex.riverJoin = true;
                }
                else
                {
                    destination.riverId = hex.riverId;
                }
            }

            // 处理河流汇合
            if (hex.riverJoin)
            {
                DrawRiverTile(hex);
                DrawRiverTile(destination);
                return;
            }
            // 跳过单格河流
            else if (hex.sourceSon && hex.isRiverEnd)
            {
                return;
            }
            // 绘制河流瓦片
            else if (hex.hasRiver && !(hex.sourceSon && hex.isRiverEnd))
            {
                DrawRiverTile(hex);
            }

            // 递归绘制下一段河流
            if (destination != null)
            {
                DrawRiver(destination, allHexes, settings);
            }
        }

        // 绘制河流瓦片（对应 JS 中的 drawRiverTile 函数）
        private void DrawRiverTile(HexCoordinateSystem.HexTile hex)
        {
            string riverCode = null;
            
            // 生成河流代码
            if (hex.sideRiverEnter >= 0 && hex.sideRiverExit >= 0)
            {
                riverCode = hex.sideRiverEnter.ToString() + hex.sideRiverExit.ToString();
            }
            
            if (string.IsNullOrEmpty(riverCode)) return;

            // 获取河流瓦片坐标
            Vector2Int tileCoords = biomeMapper.GetRiverTileCoords(riverCode);
            if (tileCoords == Vector2Int.zero) return;

            // 设置河流属性
            hex.hasRiver = true;
            
            // 这里可以添加河流瓦片的渲染逻辑
            // 例如：创建河流精灵、设置材质等
            Debug.Log($"河流瓦片: {hex.coord} - 代码: {riverCode} - 坐标: {tileCoords}");
        }

        // 获取河流瓦片的世界位置（对应 JS 中的河流位置计算）
        public Vector3 GetRiverWorldPosition(HexCoordinateSystem.HexTile hex)
        {
            Vector3 basePosition = hex.worldPosition;
            
            // 根据六边形奇偶性调整位置（对应 JS 中的位置计算）
            if (hex.coord.q % 2 == 1)
            {
                return new Vector3(
                    hex.coord.q * 24f,
                    -4f + (hex.coord.r * 28f),
                    0
                );
            }
            else
            {
                return new Vector3(
                    hex.coord.q * 24f,
                    -18f + (hex.coord.r * 28f),
                    0
                );
            }
        }

        // 重置河流 ID
        public void ResetRiverIds()
        {
            nextRiverId = 1;
        }

        // 清除所有河流
        public void ClearRivers(List<HexCoordinateSystem.HexTile> allHexes)
        {
            foreach (HexCoordinateSystem.HexTile hex in allHexes)
            {
                hex.hasRiver = false;
                hex.riverId = 0;
                hex.isRiverSource = false;
                hex.isRiverEnd = false;
                hex.sideRiverExit = -1;
                hex.sideRiverEnter = -1;
                hex.riverJoin = false;
                hex.sourceSon = false;
            }
            ResetRiverIds();
        }
    }
} 