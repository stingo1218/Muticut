# Multicut Game — Alliance Divider

> **Cut the Enemies, Unite the Allies**
>
> 一款基于组合优化与图划分问题的策略游戏，将 NP-hard 的 Multicut 问题转化为有趣的联盟博弈体验。

## 目录

- [数学背景](#数学背景)
- [游戏设定](#游戏设定)
- [游戏玩法](#游戏玩法)
- [游戏功能](#游戏功能)
- [关卡与难度系统](#关卡与难度系统)
- [技术实现](#技术实现)
- [未来展望](#未来展望)
- [参考文献](#参考文献)

## 数学背景

### Multicut 问题

Multicut 问题是一类**组合优化**问题，目标是将给定图切割成多个连通分量，使被切割边的代价之和最小。

<img src="https://raw.githubusercontent.com/stingo1218/pic/main/pic/20260407170645936.png" alt="image-20260407170645905" style="zoom:50%;" />

#### 形式化定义

设 $G = (V, E)$ 为一个无向图。边集 $M \subseteq E$ 称为 $G$ 的一个 **multicut**，当且仅当对图中的每个环 $C = (V_C, E_C)$，均满足：

$$\lvert M \cap E_C \rvert \neq 1$$

即不存在任何一个环恰好只包含 $M$ 中的一条边。$G$ 的所有 multicut 的集合记为 $\mathcal{M}(G)$。

#### 优化目标

给定图 $G = (V, E)$ 以及每条边 $e \in E$ 上的代价 $c_e \in \mathbb{R}$，Multicut 问题定义为如下优化问题：

$$
\min_{M \in \mathcal{M}(G)} \sum_{e \in M} c_e
$$


> **直觉理解**：边的代价可以为正也可以为负。正代价边代表"友好关系"（切割有代价），负代价边代表"敌对关系"（切割可获益）。玩家的目标是找到总代价最小的切割方案。

#### 环不等式约束（Cycle Inequalities）

Multicut 的核心约束在于**环不等式**：不允许任何环仅被切割一条边。这意味着：

- 如果你要切断一个环上的某条边，则该环上至少还需要切割另一条边
- 这保证了切割结果的一致性——被切入不同分量的节点之间不存在未切割的路径

#### 计算复杂度

Multicut 问题已被证明为 **NP-hard** [3, 6]，这意味着目前不太可能找到能高效精确求解该问题的算法。尽管如此，由于其在多个应用领域中能产出最先进的结果 [2, 12]，大量研究工作致力于从理论 [3, 5, 7, 11] 和算法 [1, 4, 6, 9, 10] 两个方向深入理解该问题。

#### 研究历史

Multicut 问题于 20 世纪末被首次研究：

- **Grötschel & Wakabayashi (1989)** [6]：首先针对**完全图**研究了该问题
- **Chopra & Rao (1993)** [3]：将其推广到**一般图**

该问题在不同领域有不同的名称：
- **Correlation Clustering**（相关性聚类）[1]
- **Coalition Structure Generation**（联盟结构生成）[13]

## 游戏设定

- **世界观**：多个城邦拥有各自的领地和复杂的政治关系，有的友好，有的敌对，城邦之间可能结成联盟
- **玩家角色**：皇家战略家，分析政治网络，从关系动态中发现隐藏的联盟模式
- **游戏目标**：切断敌对关系、保留友好联盟，形成稳定的联盟格局

## 游戏玩法

### 操作步骤

1. **按住鼠标右键**激活"切割模式"
2. **选择"有效且低开销"的边**进行切割
3. **观察结果**并逐步优化

### 什么是"有效切割"？

被切割后，至少能将一对目标顶点分入不同领地的边，才是有效的切割。

<img src="https://raw.githubusercontent.com/stingo1218/pic/main/pic/20260407165932012.gif" alt="Picture6" style="zoom:50%;" />

<img src="https://raw.githubusercontent.com/stingo1218/pic/main/pic/20260407165907444.gif" alt="Picture4" style="zoom:50%;" />

### 为什么选择"低开销"？

切割低开销边可以降低总消耗，帮助达到最优成本目标。

### 城邦单位

地图上的基本单位分为两种类型：

- **城镇（Urban）**：生成在陆地地形上
- **港口（Port）**：仅生成在蓝色水域地形上

<img src="https://raw.githubusercontent.com/stingo1218/pic/main/pic/20260407170046377.png" alt="image-20260407170046177" style="zoom:50%;" />

### 连接强度

边上的数值代表城邦之间的连接强度：

- **较大的值** → 更紧密的连接，更高的切割开销
- **较小的值** → 更松散的连接，更低的切割开销

<img src="https://raw.githubusercontent.com/stingo1218/pic/main/pic/20260407170103256.png" alt="image-20260407170103078" style="zoom:50%;" />

## 游戏功能

### HUD 信息

- **关卡显示**：当前关卡信息，格式为"难度 + 关卡ID"（如 `Easy_01`）
- **切割次数**：剩余可用切割数（已用 / 总数）
- **Cost 显示**：当前开销 / 最优目标开销

![Picture7](https://raw.githubusercontent.com/stingo1218/pic/main/pic/20260407170147502.gif)

### 领地显示

![image-20260407170213027](https://raw.githubusercontent.com/stingo1218/pic/main/pic/20260407170213157.png)

开启"Show Territory"后，游戏会以不同颜色区分各联盟领地：

- **直观展示**：帮助玩家快速识别联盟边界
- **体验增强**：将抽象图结构具象化为领地区域
- **颜色区分**：不同联盟使用不同颜色
- **关联判断**：同色区域中由未切割线段连接的单位属于同一联盟

### 提示功能

<img src="https://raw.githubusercontent.com/stingo1218/pic/main/pic/20260407170234458.png" alt="image-20260407170234304" style="zoom:50%;" />

开启"Hint"后，游戏会展示由 ILP 算法计算出的**最优切割方案**，帮助玩家学习和参考。

### 撤销功能

<img src="https://raw.githubusercontent.com/stingo1218/pic/main/pic/20260407170317578.gif" alt="Picture9" style="zoom:50%;" />

提供两种撤销方式：

- **重新连接**：直接点击已切割的边进行恢复
- **点击按钮**：通过 Revert 按钮撤销上一次操作

## 关卡与难度系统

游戏采用**无限关卡系统**，设有三个难度等级：

| 难度 | 城邦数量 | 特殊机制 |
|------|---------|---------|
| **Easy** | 较少节点 | 基础玩法 |
| **Medium** | 更多节点 | 加入计时器 |
| **Hard** | 大量节点 | 计时器 + 惩罚边（切割会减少剩余时间） |

### 权重平衡机制

为保障游戏可玩性，系统内置**智能翻转算法**进行权重平衡：

- **负边比例控制**：确保 35% 的负权重边
- **最小负边数量**：至少 3 条负权重边
- **智能翻转**：按权重从小到大排序，优先将最小正权重边翻转为负权重
- **保守调整**：仅翻转必要数量的边，避免过度调整

## 技术实现

### 场景构建

![image-20260407170350846](https://raw.githubusercontent.com/stingo1218/pic/main/pic/20260407170350875.png)

#### 1. 泊松圆盘采样（Poisson Disk Sampling）

生成城邦位置，在每个点周围维持最小距离，实现随机分布的同时避免重叠，确保视觉均匀性。

#### 2. Delaunay 三角剖分

连接城邦节点，生成最优三角网格，避免狭长三角形和线段交叉。

#### 3. 自动居中与缩放

计算包围盒并归一化到屏幕范围，自动适配不同尺寸的地图。

### TileMap 地形生成

![image-20260407170407925](https://raw.githubusercontent.com/stingo1218/pic/main/pic/20260407170407968.png)

使用第三方 [TiledProceduralHexTerrainGenerator](https://github.com/HextoryWorld/TiledProceduralHexTerrainGenerator) 生成六边形地形：

1. 生成随机高度图
2. 叠加多层细节
3. 分类地形类型
4. 添加湿度变化

<img src="https://raw.githubusercontent.com/stingo1218/pic/main/pic/20260407170424643.png" alt="image-20260407170424611" style="zoom:33%;" />

集成时需处理 Tilemap 坐标 `(q, r)` 到 Unity 世界坐标 `(x, y)` 的转换，并处理坐标偏移与 Y 轴反转。

### ILP 求解器（Gurobi）

Multicut 问题为 NP-hard，精确求解计算代价高昂。本项目采用 **Gurobi ILP** 求解器：

| 方案类型 | 局限性 | Gurobi ILP 优势 |
|---------|--------|-----------------|
| 启发式算法 | 速度快，但无法保证全局最优 | 同样快速 + 严格证明全局最优性 |
| 近似算法 | 有性能保证，但结果较差 | 无近似，提供理论最优解 |
| 暴力穷举 | 完全不可行 | 以工业级速度可行 |

### 系统集成：Unity ↔ Python 跨语言通信

![系统架构](doc/pic/hierarchy.png)

通过 C# `Process` 类启动 Python 进程，使用 **JSON 文件**作为数据交换媒介：

- **输入**：图结构、边权重数据
- **输出**：切割边、最优开销

### 联盟领地着色

<img src="https://raw.githubusercontent.com/stingo1218/pic/main/pic/20260407170459238.png" alt="image-20260407170459154" style="zoom:33%;" />

基于**最近邻算法**将地形瓦片分配给城邦，建立城邦与地形之间的对应关系，实现联盟领地的可视化。

## 演示视频
见release
## 未来展望

- 优化领地显示性能，解决卡顿问题
- 添加**随机事件**改变边权重
- 敌对城邦主动修复被切断的边进行对抗
- 山脉、河流、森林等地形要素影响边权重

## 参考文献

1. Bansal, N., Blum, A., & Chawla, S. (2004). Correlation clustering. *Machine Learning*, 56(1–3), 89–113.
2. Beier, T., Pape, C., et al. (2017). Multicut brings automated neurite segmentation closer to human performance. *Nature Methods*, 14(2), 101–102.
3. Chopra, S., & Rao, M. R. (1993). The partition problem. *Mathematical Programming*, 59(1–3), 87–115.
4. Demaine, E. D., Emanuel, D., Fiat, A., & Immorlica, N. (2006). Correlation clustering in general weighted graphs. *Theoretical Computer Science*, 361(2–3), 172–187.
5. Deza, M. M., Grötschel, M., & Laurent, M. (1992). Clique-web facets for multicut polytopes. *Mathematics of Operations Research*, 17(4), 981–1000.
6. Grötschel, M., & Wakabayashi, Y. (1989). A cutting plane algorithm for a clustering problem. *Mathematical Programming*, 45(1), 59–96.
7. Grötschel, M., & Wakabayashi, Y. (1990). Facets of the clique partitioning polytope. *Mathematical Programming*, 47, 367–387.
8. Horňáková, A., Lange, J.-H., & Andres, B. (2017). Analysis and optimization of graph decompositions by lifted multicuts. In *ICML*.
9. Klein, P. N., Mathieu, C., & Zhou, H. (2015). Correlation clustering and two-edge-connected augmentation for planar graphs. In *STACS*.
10. Levinkov, E., Kirillov, A., & Andres, B. (2017). A comparative study of local search algorithms for correlation clustering. In *GCPR*.
11. Oosten, M., Rutten, J. H. G. C., & Spieksma, F. C. R. (2001). The clique partitioning problem: facets and patching facets. *Networks*, 38(4), 209–226.
12. Tang, S., Andriluka, M., Andres, B., & Schiele, B. (2017). Multiple people tracking by lifted multicut and person re-identification. In *CVPR*.
13. Voice, T., Polukarov, M., & Jennings, N. R. (2012). Coalition structure generation over graphs. *Journal of Artificial Intelligence Research*, 45, 165–196.
