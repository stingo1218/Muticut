# [cite_start]Alliance Divider: 多切口问题教育游戏项目 (Research Report) [cite: 4, 13]

[cite_start]本项目开发了一款名为 “Alliance Divider” 的教育益智游戏 [cite: 20][cite_start]，旨在将计算机科学中典型的 **NP-hard** 组合优化问题——**多切口问题 (Multicut Problem)** 转化为直观的游戏体验 [cite: 20, 46, 78]。

## 🎯 游戏目标与背景
[cite_start]玩家扮演中世纪背景下的“皇家战略家”，管理城市国家（节点）之间复杂的政治关系（加权边） [cite: 111, 147]。
* [cite_start]**核心任务**：切断代表敌对关系的边，同时保留盟友关系 [cite: 22, 111]。
* [cite_start]**优化目标**：最小化切割的总成本，将图划分为多个稳定的连通分量（阵营） [cite: 51, 72, 149]。
* [cite_start]**数学约束**：确保没有任何回路（Cycle）中仅包含唯一一条被切断的边 [cite: 69]。

## 🛠️ 技术栈
- [cite_start]**游戏引擎**: Unity 2022.3 LTS (利用 Tilemap, URP 等系统) [cite: 236, 276, 283]
- [cite_start]**编程语言**: C# (游戏逻辑), Python (后端求解器) [cite: 236, 398]
- [cite_start]**优化求解器**: Gurobi (用于求解整数线性规划 ILP 模型) [cite: 24, 236, 391]
- [cite_start]**数据交换**: JSON (用于 C# 进程与 Python 脚本间的跨语言通信) [cite: 47, 399]

## 📐 核心几何与图论算法介绍
本项目集成了多种高性能几何算法，不仅保证了游戏生成的无限性，也确保了数学上的严谨性：

### 1. 泊松圆盘采样 (Poisson Disk Sampling)
[cite_start]用于在六边形地形上生成城市国家（节点）的初始位置 [cite: 308, 479]。
* [cite_start]**达成效果**：确保节点分布具有随机性的同时，严格遵循最小距离约束 [cite: 309, 311][cite_start]。这有效规避了节点重叠或视觉拥挤，为玩家提供了清晰、自然的初始布局 [cite: 310, 314]。


### 2. Delaunay 三角剖分 (Delaunay Triangulation)
[cite_start]用于在生成的节点之间建立初始的政治关系网络（边） [cite: 315, 482]。
* [cite_start]**达成效果**：遵循“空外接圆”准则，最大化所有三角形的最小内角，从而避免产生极度细长的无效边 [cite: 318, 319, 486][cite_start]。生成的图结构在数学上严谨且分布均匀，为多切口算法提供了理想的底层拓扑基础 [cite: 317, 329]。


### 3. 改进的最近邻领土分配 (Optimized Nearest Neighbor)
[cite_start]用于根据玩家的切割操作实时渲染各城市的领土范围 [cite: 530, 531, 533]。
* [cite_start]**达成效果**：地图地块根据几何距离自动分配给最近的城市 [cite: 533, 534]。
* [cite_start]**性能优化**：引入了**变更检测 (Change Detection)** 和**选择性更新 (Selective Updates)** 机制，系统仅重算受操作影响的局部区域而非全图重绘 [cite: 532, 535, 536][cite_start]。该优化将计算开销降低了 **60% 以上**，实现了版图颜色的丝滑演变 [cite: 25, 554]。


## 📁 项目结构
```text
multicut/
├── Assets/
[cite_start]│   ├── Scripts/          # 核心 C# 脚本 (包含数据结构 Graph, Node, Edge) [cite: 237, 260]
│   ├── Scenes/           # Unity 场景文件
│   ├── Prefabs/          # 游戏对象预制件
[cite_start]│   └── Resources/        # 包含 100+ 唯一地形瓦片组的素材 [cite: 175]
[cite_start]├── doc/                  # 项目研究报告与文档 [cite: 6]
[cite_start]├── input.json            # 序列化的图结构输入文件 [cite: 290, 405]
[cite_start]├── output.json           # 求解器返回的最优切边与成本结果 [cite: 290, 409]
[cite_start]└── multicut_solver.py    # 调用 Gurobi 的 Python 优化脚本 [cite: 409]
