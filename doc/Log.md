# 城邦联盟背景设定

🎮 **游戏背景设定：中世纪谋士**

⚔️ **简化设定**

中世纪大陆上有众多城邦（Cell），每个城邦之间存在复杂的政治关系。作为王室谋士，你需要分析这些城邦间的联盟关系，识别出真正的盟友和敌人。

你的任务：运用多割算法，切断敌对关系，保留盟友关系，找出稳定的联盟集团。

🔧 **核心机制：**
- **城邦（Cell）**：不同的城市
- **关系（Edge）**：城邦间的政治关系，正权重=盟友，负权重=敌人
- **任务**：切断敌对关系，识别联盟集团



---

# TODO

## 生态识别游戏功能开发

### 1. 生态群系识别系统
- [ ] **群系识别算法**
  - 实现基于多割算法的生态群系识别
  - 切割后自动识别连通分量作为生态群系
  - 计算每个群系的生态特征（主要地形类型、面积等）
  - 群系数量统计和显示

- [ ] **群系可视化**
  - 为每个识别出的群系分配不同颜色
  - 群系内Cells和Tiles使用相同颜色标识
  - 群系边界高亮显示
  - 群系信息面板显示（群系数量、主要地形、面积等）

- [ ] **生态数据分析**
  - 计算群系间的生态相似度
  - 分析群系内部的地形多样性
  - 生成生态群系报告
  - 群系稳定性评估

### 2. 地形与Cell关联系统
- [ ] **Cell-Tile映射**
  - 每个Cell包含特定的地形瓦片
  - 浅水和深水不属于任何Cell，作为环境背景
  - Cell边界可视化
  - Tile归属状态显示

- [ ] **群系聚类后处理**
  - 切割完成后，群系内所有Cells的Tiles自动归类
  - 群系颜色应用到所有相关Tiles
  - 群系边界Tile的特殊标记
  - 群系间过渡区域的处理

### 3. 生态权重系统优化
- [ ] **生态关联权重**
  - 基于地形相似性计算Cells间权重
  - 相同地形类型：高权重（强关联）
  - 不同地形类型：低权重（弱关联）
  - 特殊地形（火山、河流）的权重调整

- [ ] **权重可视化**
  - 连线粗细表示权重强度
  - 连线颜色表示关联类型
  - 权重数值显示
  - 权重分布统计

### 4. 游戏体验优化
- [ ] **生态识别反馈**
  - 实时显示当前识别出的群系数量
  - 群系识别进度条
  - 识别准确度评分
  - 最优解与玩家解法的对比

- [ ] **教学系统**
  - 生态识别基础知识介绍
  - 多割算法原理说明
  - 群系识别技巧指导
  - 生态学背景知识

### 5. 关卡设计
- [ ] **生态场景生成**
  - 湿地生态系统关卡
  - 山地森林生态系统关卡
  - 海岸生态系统关卡
  - 沙漠绿洲生态系统关卡
  - 复杂混合生态系统关卡

- [ ] **难度递进**
  - 简单：2-3个群系识别
  - 中等：4-6个群系识别
  - 困难：7-10个群系识别
  - 专家：复杂网络结构识别

### 6. 技术实现
- [ ] **群系识别算法**
  - 连通分量检测算法
  - 群系特征提取
  - 群系相似度计算
  - 群系稳定性评估

- [ ] **性能优化**
  - 大规模生态网络处理
  - 实时群系更新
  - 内存使用优化
  - 渲染性能提升

### 7. 数据分析与报告
- [ ] **生态报告生成**
  - 群系数量统计
  - 群系面积分布
  - 主要地形类型分析
  - 生态多样性指数

- [ ] **数据导出**
  - 群系识别结果导出
  - 生态网络数据保存
  - 分析报告生成
  - 数据可视化图表

## 优先级排序
1. **高优先级**：群系识别算法、Cell-Tile映射、生态权重系统
2. **中优先级**：群系可视化、生态数据分析、教学系统
3. **低优先级**：数据导出、性能优化、复杂关卡设计


# Done

1. 使用prefab预制体创建了cell模板
   - Q:字体放大不清晰
   - A:使用TEXTMESHPRO
2. 在GameManager使用脚本批量创建cells,
   - Q:会出现多个cell聚集到一个区域,多个重叠
   - A:检测距离是否过近(==泊松圆盘分布==)
3. 使用字典(cell,cell),(edge,weight)来映射边和点的数据结构
4. 使用Delaunay triangulation创建网格图
   - Q:
     ![image-20250602221013088](https://raw.githubusercontent.com/stingo1218/pic/main/Typoraimage-20250602221013088.png)
   - A:(进行度数检查防止度数差异过大)/ 或者角度过小进行删去
5. eraseline的设计,用户可以按住右键使用切割线,线会切断经过的edges(如果这是规则允许的),也可以重新连回来
6. 重新连接:两个隔离的连通分量当被一条线连接时,如果成为了新的一个连通分量,那么就会检查初始的edges,进行重新连接
7. > - Inspector多选算法：支持选择多割算法（目前只有贪心算法）。
   >
   > - 贪心多割算法实现：初版为最大割，后修正为最小割（优先移除权重最小的边）。
   >
   > - 高亮切割边：实现了高亮显示需要切割的边（可用深红色或高亮材质）。
   >
   > - 关卡自适应缩放居中：生成点后自动缩放并居中，保证关卡适配全屏。
   >
   > - 修正连线错位问题：调整生成顺序，先归一化点再连线，保证线和点一致。
   >
   > - HINT按钮触发：将多割算法的执行逻辑移到点击HINT按钮时触发，只有需要提示时才高亮显示最佳切割边。

8. **Delaunay Refinement细分法集成** (2024-12-19)
   - **问题**：Delaunay三角剖分产生矮胖三角形，影响可视化和游戏体验
   - **解决方案**：集成Delaunay Refinement算法，自动检测并改善三角形质量
   - **实现细节**：
     - 计算三角形最小高/最长边比，识别不合格三角形
     - 在坏三角形外心插入新点，重新剖分
     - 循环迭代直到所有三角形满足质量要求或达到最大迭代次数
   - **参数设置**：`minHeightToEdgeRatio = 0.2f`，`maxRefineIters = 10`
   - **边界处理**：过滤细分插入点对应的边，避免索引越界

9. **标准多割算法实现** (2024-12-19)
   - **问题**：原算法限制连通分量数量，不符合标准多割问题定义
   - **解决方案**：修改为真正的标准多割问题，不限制连通分量数量
   - **算法改进**：
     - **ILP算法**：移除连通分量数量约束，使用循环不等式约束
     - **贪心算法**：移除连通分量数量目标，使用循环不等式检查
     - **目标函数**：最大化保留边的权重和（使用正数权重）

10. **权重系统优化** (2024-12-19)
    - **问题分析**：负数权重导致"全切"，正数权重导致"不切"
    - **解决方案**：使用正数权重 + 最大化目标函数
    - **权重含义**：权重越大表示边越重要，越不应该被切割
    - **算法行为**：
      - 保留重要的边（权重大的）
      - 切割不重要的边（权重小的）
      - 在满足循环不等式约束的前提下优化

11. **游戏设计优化** (2024-12-19)
    - **权重可视化建议**：根据权重设置边的视觉效果（粗细、颜色）
    - **玩家引导**：权重越大越重要，切割重要边时给出警告
    - **策略深度**：玩家需要权衡哪些边可以安全切割

12. **2024-06-13 UI与功能重构**
    - Hint按钮彻底切换为Toggle预制体，支持开关切换和高亮切割边，原Button相关逻辑全部移除。
    - HintToggle功能完善，支持动态高亮/取消高亮。
    - 代码冗余清理，删除isHintButtonPressed等无用变量。
    - UI适配，HintToggle可自定义大小，支持放置到任意Canvas。
    - 其它细节优化。

13. **2024-06-13 Unity中采用Python脚本调用Gurobi的原因说明**
   - **背景**：多割问题（Multicut/K-way Cut）属于NP难问题，最优解通常依赖数学优化工具如Gurobi等求解器。
   - **Unity集成难点**：
     - Gurobi官方C# API仅支持Windows且依赖复杂，Unity工程跨平台（如Mac、Linux、WebGL）时兼容性差。
     - Unity C#环境与Gurobi C# API集成时，DLL加载、授权、依赖管理等问题繁琐，易出错。
     - Unity工程热更、打包等流程下，C#原生调用Gurobi不易维护。
   - **Python方案优势**：
     - Gurobi官方对Python支持极佳，安装和调用简单，社区资料丰富。
     - Python脚本可独立于Unity运行，易于调试和快速迭代算法。
     - 通过文件（input.json/output.json）或进程通信，Unity与Python解耦，便于后续算法升级和跨平台兼容。
   - **具体实现**：
     - Unity C#端将图结构和权重序列化为JSON，写入input.json。
     - 调用Python脚本（multicut_solver.py），由其负责Gurobi建模与求解。
     - Python输出最优切割边和cost到output.json，Unity再读取并高亮显示。
   - **经验总结**：
     - 采用Python脚本调用Gurobi极大提升了开发效率和算法灵活性，规避了Unity与Gurobi直接集成的兼容性和维护难题。

14. **2024-06-13 多割高亮只显示一条边问题排查与解决**
   - **问题现象**：Python输出的output.json中cut_edges有多条边，但Unity只高亮了一条（如只高亮4-3，未高亮4-1、4-2等）。
   - **原因分析**：C#端解析output.json时采用字符串分割方式，遇到多条边、换行、空格等格式变化时，分割逻辑失效，导致只解析到一条或部分边。
   - **调试过程**：
     - 在HighlightCutEdges方法中加入UnityEngine.Debug.Log，打印cutEdges的内容，发现只传入了一条边。
     - 检查output.json内容，确认cut_edges为标准JSON数组格式。
     - 进一步分析发现，手动字符串分割方式不适合解析标准JSON数组，容易遗漏多条边。
   - **最终解决方案**：
     - 推荐使用Unity的JsonUtility或Newtonsoft.Json（Json.NET）直接反序列化output.json，保证所有cut_edges都能被正确读取。
     - 关键代码建议：
       ```csharp
       [Serializable]
       public class CutEdge { public int u; public int v; }
       [Serializable]
       public class MulticutResult { public List<CutEdge> cut_edges; public int cost; }
       // 解析
       var result = JsonUtility.FromJson<MulticutResult>(resultJson);
       var cutEdges = new List<(Cell, Cell)>();
       if (result != null && result.cut_edges != null)
       {
           foreach (var edge in result.cut_edges)
           {
               var cellU = _cells.FirstOrDefault(c => c.Number == edge.u);
               var cellV = _cells.FirstOrDefault(c => c.Number == edge.v);
               if (cellU != null && cellV != null)
                   cutEdges.Add(GetCanonicalEdgeKey(cellU, cellV));
           }
       }
       ```
   - **经验总结**：解析标准JSON时应避免手动字符串分割，优先使用官方或第三方JSON库，提升健壮性和可维护性。

15. **2024-06-13 多割算法与关卡设计问题记录**
   - **问题现象**：Python输出的cut_edges有多条，但Unity只高亮了一条。
   - **原因分析**：C#解析output.json时用字符串分割，遇到换行/空格等格式变化时只解析到一条。
   - **解决方案**：改用正则表达式批量提取所有cut_edges，保证无论格式如何都能全部解析。
   - **正则示例**：`@"\{\s*\"u\"\s*:\s*(\d+)\s*,\s*\"v\"\s*:\s*(\d+)\s*\}"`

16. **多割算法遇到无可优化空间的关卡**
   - **问题现象**：有些关卡output.json的cut_edges为空，玩家无事可做。
   - **原因分析**：标准多割算法下，若图结构和权重分布没有"冲突"或"优化空间"，算法会返回空解。
   - **解决方案**：
     - 关卡生成后自动检测cut_edges是否为空，若为空提示设计师调整结构或权重。
     - 设计时增加环路、负权重边等，制造"可优化空间"。

17. **当前cost未实时更新问题**
   - **问题现象**：UI上COST左侧数字（当前cost）不随玩家切割实时变化。
   - **原因分析**：原实现统计的是高亮材质的边，而不是玩家实际切割的边。
   - **解决方案**：
     - 新增playerCutEdges集合，记录玩家每次实际切割的边。
     - RemoveEdge时加入集合并刷新UI。
     - GetCurrentCost统计playerCutEdges的权重和。
     - 关卡重置时清空集合。

18. **UI中CostText的自动刷新与格式规范**
   - **问题现象**：最优cost能更新，当前cost不变或格式不统一。
   - **解决方案**：
     - 每次切割后自动调用UpdateCostText，格式为`COST: 当前cost/最优cost`。
     - 解析output.json时同步提取cost字段。
     - 关卡重置、Hint关闭等场景也刷新一次。

19. **2024-12-20 地形系统与图论算法集成**
   - **六边形地形生成系统详细实现**
   - **TilemapGameManager地形图游戏管理器开发**
   - **Unity API兼容性修复**
   - **访问权限和架构优化**
   - **Git版本控制与合并冲突处理**
   - **TerrainManager上下文菜单功能恢复**
   - **地形权重算法设计**
   - **项目功能集成与测试准备**
   - **TilemapGameManager与GameManager集成开发**
   - **地形权重系统调试与可视化工具开发**

20. **2024-12-20 地形权重系统完整实现与调试** (当前对话)
   - **地形权重系统核心实现**：
     - 在GameManager.cs中实现`CalculateTerrainBasedWeight`方法，使用Unity 2D物理射线检测
     - 添加`GetTilesCrossedByLine`方法，通过分段采样和Physics2D.LinecastAll双重检测
     - 实现`GetBiomeUsingMap`方法，通过反射调用TerrainManager的映射表获取生物群系
     - 创建`TerrainWeights`序列化类，定义不同生物群系的权重值
     - 添加`GetBiomeWeight`和`GetEdgeWeight`公共方法供外部调用
   
   - **TerrainManager映射表系统**：
     - 在TerrainManager.cs中添加`tileBiomeMap`字典，存储瓦片坐标到生物群系的映射
     - 实现`GetBiomeAtTile`、`GetBiomeAtWorldPosition`、`IsBiomeMapReady`公共方法
     - 在`RenderToTilemap`时构建映射表，在`ClearTerrain`时清空映射表
     - 统一使用(X,Y,Z)坐标格式，与TerrainManager保持一致
   
   - **调试与可视化工具开发**：
     - 创建SimpleEdgeTileTest.cs脚本，实现边穿越地形的可视化调试
     - 实现六边形高亮系统，使用自定义网格替代方形高亮
     - 集成TextMeshPro，提供清晰的文字标签显示
     - 添加`HighlightAllEdges`功能，支持测试所有边或单个边
     - 实现独立的高亮层管理，支持单独控制显示/隐藏
   
   - **EdgeWeightCalculator工具开发**：
     - 创建独立的权重计算工具，验证GameManager权重计算的正确性
     - 实现`CalculateAllEdgeWeights`方法，输出所有边的权重统计
     - 添加`CompareWeightCalculations`方法，对比不同计算方式的结果
     - 提供`ForceUpdateAllEdgeWeights`方法，强制重新计算所有边权重
   
   - **关键技术问题解决**：
     - **编译错误修复**：解决命名空间引用问题，使用反射绕过类型限制
     - **坐标系统统一**：多次调整X/Y坐标映射，最终统一使用(X,Y,Z)格式
     - **权重计算统一**：确保GameManager和EdgeWeightCalculator使用相同的权重定义
     - **反射技术应用**：通过反射访问TerrainManager，避免直接类型引用
   
   - **权重系统设计**：
     - 正权重地形（倾向保留）：草地(+5)、平原(+4)、浅水(+3)
     - 负权重地形（倾向切割）：深水(-8)、山地(-10)、高山(-15)、火山(-20)、森林(-6)、河流(-12)
     - 权重计算逻辑：累加边穿越的所有地形瓦片权重
     - 游戏意义：多割算法优先切割困难地形，保留易通行地形
   
   - **调试功能完善**：
     - 添加`DebugWeightCalculation`和`RecalculateAllEdgeWeights`ContextMenu方法
     - 实现详细的控制台日志输出，显示检测到的瓦片和权重
     - 提供可视化高亮系统，直观显示边穿越的地形类型
     - 支持手动触发测试和清除高亮功能
   
   - **测试验证结果**：
     - 成功检测边穿越的所有地形瓦片
     - 准确识别每个瓦片的生物群系类型
     - 权重计算与预期结果一致
     - 可视化系统正确显示检测结果
     - 性能表现良好，检测速度快且稳定

21. **游戏概念重新定位** (2024-12-20)
   - **游戏背景更新**：从《断界之城》重新定位为《生态识别者》
   - **世界观重构**：从像素世界切割改为生态研究站的群系识别
   - **核心机制调整**：
     - Cells代表生态单元（生物群落）
     - Edges代表生态群系间的相互作用
     - 多割算法用于识别不同的生态群系
     - 浅水和深水作为环境背景，不属于任何Cell
   - **TODO事项重构**：将功能开发重点转向生态识别系统

22. **2024-12-20 HintToggle渲染顺序问题修复与游戏设计讨论**
   - **问题现象**：点击HintToggle后，高亮显示的切割线覆盖了Cells，影响视觉效果。
   - **技术解决方案**：
     - 修改`HighlightCutEdges`方法，为高亮线条设置正确的渲染顺序
     - 在`OnHintToggleChanged`方法的else分支中，确保关闭Hint时线条恢复正确的渲染属性
     - 统一设置`sortingOrder = 100`、`sortingLayerName = "Default"`、`gameObject.layer = LayerMask.NameToLayer("Default")`
     - 确保高亮线条与普通线条具有相同的渲染优先级
   
   - **游戏设计深度讨论**：
     - **多割边权重解释争议**：用户导师认为多割边应解释为"关系强度"而非"建造成本"
     - **学术背景分析**：详细讨论多割问题在不同应用领域的边权重含义
     - **应用方向探索**：图像分割、社交网络、生物信息学、推荐系统等领域的多割应用
     - **游戏背景重构**：从"道路建设成本"重新定位为"生态联系强度"
   
   - **two-edge-connected augmentation问题研究**：
     - 探讨该问题与多割问题的关系
     - 分析在生态地图背景下的应用可能性
     - 研究如何将游戏重新定位为该问题的可视化工具
   
   - **权威论文引用讨论**：
     - 用户多次询问多割边权重解释的权威论文支持
     - 分析多割问题中"成本"vs"关系"解释的学术争议
     - 最终确认多割边主要解释为"关系/关联强度"而非"建造成本"
   
   - **游戏难度控制科学方法**：
     - **地形生成器与难度控制集成**：利用地形生成器影响边权重，结合难度调整系统
     - **陷阱结构设计**：桥梁、环路、链式反应等结构，要求切割正权重边获得更优解
     - **权重分布策略**：通过调整权重分布创造"可优化空间"
     - **关卡复杂度控制**：从简单到专家级别的渐进式难度设计
   
   - **技术实现方案**：
     - **地形权重系统**：基于地形类型计算边权重，正权重表示强关联，负权重表示弱关联
     - **难度调整算法**：在基础地形权重基础上应用难度系数
     - **陷阱结构生成**：自动生成需要切割正权重边的复杂网络结构
     - **实时难度评估**：检测关卡是否具有足够的优化空间
   
   - **市场调研需求**：用户询问市场上是否有类似的解决方案，为游戏定位和差异化提供参考

23. **2024-12-20 领地系统开发与文件管理** (当前对话)
    - **领地系统重新创建**：
      - 由于TerritoryManager.cs文件被删除，需要重新创建领地系统
      - 计划重新实现TerritoryManager.cs，包含领地识别、群系分析等功能
      - 准备集成到现有的生态识别系统中
   
    - **文件删除记录**：
      - TerritoryManager.cs - 领地管理器主文件
      - TerritoryTest.cs - 领地测试脚本
      - TerritorySystem_README.md - 领地系统说明文档
      - 领地系统使用说明.md - 中文使用说明文档
   
    - **重新开发计划**：
      - 第一步：重新创建TerritoryManager.cs文件
      - 第二步：实现领地识别算法
      - 第三步：集成到GameManager中
      - 第四步：添加领地可视化功能
      - 第五步：测试和调试领地系统
   
    - **技术要点**：
      - 基于连通分量的领地识别
      - 领地边界检测和可视化
      - 领地特征分析（面积、地形类型等）
      - 与现有生态权重系统的集成
   
    - **开发状态**：准备开始重新创建领地系统，当前处于第一步规划阶段

24. **2024-12-20 生态区可视化系统开发** (当前对话)
    - **CellTileTestManager测试系统创建**：
      - 创建CellTileTestManager.cs脚本，实现Cell-Tile分配和颜色高亮测试
      - 支持三种分配模式：随机分配、最近邻分配、Voronoi分配
      - 实现六边形高亮系统，使用自定义Texture2D绘制六边形精灵
      - 解决六边形缩放问题（1.179643倍），调整高亮精灵大小匹配瓦片
   
    - **边界绘制算法迭代**：
      - **用户需求演变**：从简单瓦片高亮 → 边界轮廓 → 凸包 → 外边缘 → 国界线 → 最终回归瓦片高亮
      - **尝试方案**：
        - 方案1：绘制瓦片外边缘（"尖刺"效果）
        - 方案2：矩形边界框
        - 方案3：凸包算法（Graham扫描）
        - 方案4：外边缘连接（连续线条）
        - 方案5：国界线绘制（不同Cell边界）
        - 方案6：完整区域轮廓
      - **最终方案**：回归瓦片高亮，使用淡色半透明颜色
   
    - **GameManager集成**：
      - 修改CellTileTestManager使用GameManager生成的Cells而非创建新Cells
      - 通过反射访问GameManager._cells私有字段
      - 实现IsValidGameCell()方法过滤WeightPrefab实例
      - 解决KeyNotFoundException问题，添加字典安全检查
   
    - **UI Toggle集成**：
      - 集成UICanvas中的"Show Eco Zones Toggle"控制生态区显示
      - 实现OnEcoZonesToggleChanged()方法控制显示/隐藏
      - 添加性能优化缓存机制（isDataInitialized、isHighlightVisible）
      - 确保游戏开始时立即显示生态区（无延迟）
   
    - **簇合并系统开发**：
      - 实现簇数据解析系统，支持output.json格式（cut_edges + cost）
      - 创建InferClustersFromCutEdges()方法，使用DFS算法从切割边推断簇
      - 实现HighlightByClusters()方法，使同一簇的Cell显示相同颜色
      - 添加簇模式切换功能（enableClusterMode）
   
    - **Cost自动监听系统**：
      - 实现CheckCostChange()方法，定期检查output.json中的cost变化
      - 添加autoMonitorCost和costCheckInterval参数控制监听行为
      - 当检测到cost变化时自动调用ForceRefreshClusterDisplay()
      - 解决"同一簇显示不同颜色"问题，确保簇分配正确更新
   
    - **技术问题解决**：
      - **编译错误**：修复ContextMenu属性位置错误，移至对应方法
      - **数据一致性**：解决cellColors和cellTileAssignment字典的KeyNotFoundException
      - **簇推断算法**：修正InferClustersFromCutEdges()中的图构建逻辑
      - **文件格式兼容**：支持CutEdgeData和ClusterData两种JSON格式
   
    - **性能优化**：
      - 实现数据缓存机制，避免重复计算
      - 添加ResetCache()方法支持手动缓存重置
      - 优化显示/隐藏逻辑，减少不必要的重新计算
      - 提供ForceUpdateClusterDisplay()等调试方法
   
    - **调试功能完善**：
      - 添加详细的Debug日志输出
      - 提供多个ContextMenu方法用于手动测试
      - 实现簇分配状态检查和验证
      - 支持手动触发簇数据重新加载

25. **2024-12-20 边权重算法优化与关卡难度系统设计** (当前对话)
    - **边权重算法重构**：
      - 在GameManager.cs中新增EdgeDifficultyConfig类，支持陷阱/奖励边机制
      - 实现ApplyEdgeDifficultyModifiers()方法，根据档位对边权重进行修饰
      - 支持DifficultyTier枚举：Easy、Normal、Hard、Nightmare四个档位
      - 新增结构性修饰：长边奖励、山地/水域穿越惩罚、陷阱/奖励边随机生成
      - 档位影响整体幅度：Easy(×0.8)、Normal(×1.0)、Hard(×1.2)、Nightmare(×1.4)
   
    - **陷阱/奖励边系统**：
      - 陷阱边：trapChance概率生成，额外负惩罚(trapPenaltyMin~Max)
      - 奖励边：bonusChance概率生成，额外正奖励(bonusMin~Max)
      - 结构性修饰：长边奖励(longEdgeBonus)、山地惩罚(mountainPenaltyPerTile)、水域惩罚(waterPenaltyPerTile)
      - 档位影响：更难档位加重惩罚，简单档位增加奖励
   
    - **关卡难度控制机制设计**：
      - **计时器系统**：倒计时限制、超时惩罚/失败、自动提示消耗时间
      - **边锁定系统**：部分边不可切、解锁条件/冷却时间、关键桥边上锁
      - **预算/配额系统**：限制最多切N条边、总成本≤X、切割配额管理
      - **难度维度**：图规模与结构、权重分布、目标复杂度、资源与约束、干扰与引导
   
    - **难度参数配置**：
      - Easy：节点8-10，平均度中等，桥边适中，trapChance低，bonusChance稍高，时间宽松，切割配额宽松，锁边较少
      - Normal：节点12-14，桥边偏低，trap/bonus中等，时间一般，切割配额有限，少量锁边
      - Hard：节点16-18，平均度更高，桥边更少，trapChance提高，时间紧，切割配额紧，关键高权桥边上锁
      - Nightmare：节点20+，高平均度+长边，权重噪声提升，时间很紧，切割配额很紧，关键桥边上锁+冷却，提示消耗成本或时间
   
    - **生成流水线设计**：
      - 生成节点→Delaunay→初始边集合
      - 边权重：地形+随机+陷阱/奖励+结构修饰
      - 择边后处理：识别桥边、锁边策略、诱导/干扰
      - 设定目标：目标簇数K、目标成本上限C、资源限制
      - 校准：调用Python multicut_solver.py，微调直到落在目标区间
   
    - **技术实现方案**：
      - 数据结构：HashSet<(Cell, Cell)> lockedEdges、int cutQuota、float timeLimit、int hintQuota
      - 限制点位：RemoveEdge前加拦截、Update中加计时器
      - HUD显示：时间、剩余可切次数、COST/目标COST、提示次数
      - 提示代价：档位决定消耗，无可用提示/时间不足时禁用
   
    - **目标验证与自动校准**：
      - 每次生成后调用Python，检测最优cut数或cost是否在目标区间
      - 太简单：提高lockHighWeightRatio、trapChance，减少平均度或增加误导边
      - 太难：降低锁边比例、trapChance，增加bonusChance，放宽配额/时间
      - 保存optimalCost用于UI显示和评分基准
   
    - **扩展玩法设计**：
      - 序列目标：先切成K1，再在配额内进一步切成K2
      - 动态事件：计时到阈值时随机上锁/解锁边，地形变化导致权重刷新
      - 精英关：锁边+冷却，切错边进入冷却期N秒不可切
   
    - **落地顺序规划**：
      - 第一步：引入LevelConfig（ScriptableObject），增加LoadLevel(LevelConfig cfg)
      - 第二步：生成图后计算权重→识别桥边→执行BuildLockedEdges(cfg)→调UpdateOptimalCostByPython()校准
      - 第三步：接入HUD与限制：计时、切割配额、提示配额，RemoveEdge加拦截
      - 第四步：做4档示例配置，快速测试一遍流程

26. **2024-12-20 生态区后台计算系统开发** (当前对话)
    - **ClusterHighlighter后台计算重构**：
      - 新增后台计算变量：isDataInitialized、backgroundCalculationCoroutine、cachedTileColors、cachedClusterData
      - 新增Start()方法：启动后台计算协程BackgroundEcoZoneCalculation()
      - 修改ShowEcoZones()方法：优先使用缓存数据立即显示，无缓存时使用统一底色
      - 修改RefreshFromJson()方法：优先使用缓存数据，无缓存时才重新计算
   
    - **BackgroundEcoZoneCalculation()协程实现**：
      - 每2秒检查一次数据变化，自动获取Cells和分配Tiles
      - 监控簇数据文件变化，实时计算颜色并缓存
      - 检测簇数据变化：簇数量、cost值、从有数据变为无数据
      - 数据变化时重新计算颜色：使用簇数据或统一颜色
      - 更新缓存：cachedTileColors.Clear()并重新填充
      - 如果当前正在显示，立即更新显示：StartIncrementalRecolor()
   
    - **工作流程优化**：
      - 启动时：自动开始后台计算协程
      - 后台计算：每2秒检查一次，计算生态区颜色并缓存
      - 打开显示时：立即使用缓存数据显示，无需等待计算
      - 数据变化时：自动检测并更新缓存，如果正在显示则立即刷新
   
    - **性能与用户体验提升**：
      - 解决"中途打开生态区显示需要等待计算"的问题
      - 实现"即使没有打开显示，也要计算，因为可能会中途打开它看一眼"
      - 提供即时的生态区显示体验，无需等待后台计算
      - 支持实时数据更新，确保显示内容始终是最新的计算结果

27. **2024-12-20 城邦联盟背景设定完善** (当前对话)
    - **简化背景设定**：中世纪谋士分析城邦联盟关系
    - **核心概念**：城邦（Cell）、政治关系（Edge）、联盟识别（多割算法）
    - **游戏机制**：切断敌对关系（负权重），保留盟友关系（正权重），识别联盟集团

28. **2024-12-20 游戏系统全面优化与重构** (当前对话)
    - **游戏难度系统设计与实现**：
      - 创建统一的GameDifficulty枚举（Easy、Medium、Hard），替代原有的复杂难度设置
      - 实现ApplyDifficultySettings()方法，根据难度自动设置功能开关：
        - Easy：仅切割次数限制（enableCutLimit = true）
        - Medium：切割次数限制 + 计时器（enableTimer = true）
        - Hard：切割次数限制 + 计时器 + 时间炸弹（enableTimeBomb = true）
      - 添加渐进式难度参数ApplyProgressiveDifficulty()，随关卡增加自动调整：
        - 节点数量逐步增加（所有难度）
        - 切割次数限制逐步减少（所有难度）
        - 计时器时间逐步减少（Medium/Hard）
        - 时间炸弹惩罚逐步增加（Hard）
   
    - **时间炸弹陷阱系统实现**：
      - 新增时间炸弹相关字段：timeBombEdges、timeBombChance、timeBombPenaltySeconds、timeBombEdgeColor、timeBombEdgeWidth
      - 实现时间炸弹边生成逻辑：在SpawnLevel()中根据概率随机标记边为时间炸弹
      - 添加视觉区分：时间炸弹边使用红色+加粗显示（UpdateTimeBombEdgeAppearance()）
      - 实现惩罚机制：切割时间炸弹边时减少剩余时间（RemoveEdge()中处理）
      - 确保只有Hard难度才能生成时间炸弹边（双重检查：enableTimeBomb && gameDifficulty == GameDifficulty.Hard）
   
    - **胜利条件与关卡进展系统**：
      - 实现实时胜利检测：UpdateCostText()中检查currentCost == optimalCost
      - 创建VictoryPanelController.cs脚本，管理胜利面板的显示/隐藏和时间暂停
      - 添加胜利状态标志：hasOptimalCost、hasShownVictoryPanel，防止重复弹出面板
      - 实现ShowVictoryPanel()方法，支持自动查找面板、设置CanvasGroup属性、暂停游戏时间
      - 添加OnContinueButtonClicked()方法，处理继续按钮点击事件，自动进入下一关
      - 完善NextLevel()方法，重置所有状态并应用新关卡的难度设置
   
    - **UI系统完善与自动化**：
      - 添加关卡显示系统：levelDisplayText组件，格式"Level:Hard_01"
      - 实现切割次数限制UI：cutLimitText组件，格式"Cut Limit: 3/5"
      - 添加计时器UI：timerText组件，倒计时显示
      - 所有UI组件支持自动查找：Start()方法中通过GameObject.Find()自动绑定
      - 实现UpdateLevelDisplay()、UpdateCutLimitUI()、UpdateTimerUI()方法，确保UI实时更新
   
    - **边权重与颜色系统优化**：
      - 修正边权重计算范围为[-30, 30]，确保有正有负的权重分布
      - 实现BalanceEdgeWeights()方法，确保至少40%的边为负权重，避免最优cost为0的情况
      - 统一边颜色系统：普通边使用黑色，时间炸弹边使用红色+加粗
      - 修复Hint高亮功能：使用highlightEdgeColor强制绿色高亮，与时间炸弹样式兼容
      - 实现TurnOffHint()方法，关卡切换时自动关闭Hint功能
   
    - **场景选择与数据持久化系统**：
      - 创建SceneSelector.cs脚本，支持Easy/Medium/Hard难度选择和起始关卡设置
      - 实现PlayerPrefs数据持久化：SelectDifficulty和StartLevel参数跨场景传递
      - 添加LoadDifficultyFromSceneSelector()方法，GameManager启动时自动读取场景选择器设置
      - 支持场景加载的多种方式：按名称加载或按索引加载，增强Build Settings兼容性
   
    - **生态区高亮系统关卡适配**：
      - 修复ClusterHighlighter在关卡切换后不高亮的问题
      - 添加ResetHighlighter()方法，清理缓存数据并重新初始化
      - 实现ForceRefreshEcoZonesToggle()方法，通关后自动关闭生态区高亮
      - 在NextLevel()中集成高亮器重置逻辑，确保每个新关卡都能正常使用生态区功能
      - GameManager自动查找并绑定ClusterHighlighter组件，无需手动配置
   
    - **代码质量与维护性提升**：
      - 清理冗余代码：删除CellTileTestManager.cs等不再使用的脚本
      - 移除用户不需要的ContextMenu属性，避免意外调用
      - 统一日志输出：移除emoji，使用纯文本格式
      - 完善错误处理：KeyNotFoundException、空引用检查等异常处理
      - 添加详细的调试日志，便于问题排查和功能验证
   
    - **性能优化与缓存管理**：
      - 实现边权重缓存系统：_edgeWeightCache字典，避免重复计算
      - 添加缓存清理逻辑：关卡切换时清空权重缓存，确保数据一致性
      - 优化地形检测算法：使用反射和缓存机制提升性能
      - 修复动态缩放问题：移除权重预制体的problematic scaling，使用固定localScale
   
    - **游戏平衡性调整**：
      - 实现baseCutLimit和cutLimitReductionRate系统，确保每关都有足够但不过多的切割机会
      - 添加timeLimitSeconds渐进缩短机制，为Medium/Hard难度增加时间压力
      - 调整timeBombChance和timeBombPenaltySeconds，在Hard难度中提供适度挑战
      - 完善无种子关卡生成：移除seed依赖，使用levelIndex确保每关的唯一性和可重复性
   
    - **关键技术问题解决**：
      - **胜利面板不弹出问题**：修复hasOptimalCost标志设置时机，确保UpdateOptimalCostByPython()正确设置
      - **关卡切换状态重置**：确保hasShownVictoryPanel和hasOptimalCost在NextLevel()中正确重置
      - **UI组件查找失败**：实现robust查找逻辑，支持inactive对象查找和多路径尝试
      - **权重分布不均**：通过BalanceEdgeWeights()算法确保合理的正负权重比例
      - **时间炸弹不生效**：修复难度判断逻辑，确保只有Hard难度生成时间炸弹边
   
       - **测试与验证完成**：
     - 验证三个难度档位的功能正确性
     - 测试关卡进展和胜利检测的稳定性
     - 确认生态区高亮在关卡切换后的正常工作
     - 验证场景选择系统的数据传递正确性
     - 测试时间炸弹系统的视觉效果和惩罚机制

29. **2024-12-20 关卡跳跃问题修复与菜单返回功能** (当前对话)
   - **关卡跳跃问题根因分析与修复**：
     - **问题现象**：通关Level1后直接跳到Level3，关卡索引每次递增2（1→3→5）
     - **根因分析**：双重按钮事件绑定导致NextLevel()被调用两次
       - GameManager.Start()中：`continueButton.onClick.AddListener(OnContinueButtonClicked)`
       - VictoryPanelController.Awake()中：`continueButton.onClick.AddListener(HandleContinueClicked)`
       - 两个事件处理器都调用NextLevel()，导致levelIndex被递增两次
     - **修复方案**：
       - 注释掉GameManager中的重复按钮绑定逻辑，避免双重事件注册
       - 保留VictoryPanelController单独处理Continue按钮事件
       - 修改ShowVictoryPanel()中的回退逻辑，直接调用NextLevel()而非通过按钮事件
       - 保留之前添加的防重入保护（isTransitioningLevel标志）作为额外安全措施
   
   - **防重入机制实现**：
     - 新增isTransitioningLevel布尔标志，防止NextLevel()被并发调用
     - NextLevel()开始时设置isTransitioningLevel = true，结束时重置为false
     - 如果检测到重复调用，输出警告日志并直接返回
     - 确保关卡切换过程的原子性和一致性
   
   - **返回主菜单功能开发**：
     - **用户需求**：在UICanvas下添加menu按钮，点击后返回主菜单界面
     - **技术实现**：
       - 在GameManager.cs中新增ReturnToMainMenu()公开方法
       - 实现Scene切换逻辑：优先加载"MainMenu"场景，回退到场景索引0
       - 添加时间恢复逻辑：Time.timeScale = 1f，避免从暂停状态返回菜单
       - 完善错误处理：try-catch机制处理场景不存在的情况
     - **使用方法**：
       - Unity Editor中选择menu按钮
       - Button组件OnClick事件添加GameManager.ReturnToMainMenu()
       - 支持从游戏任何状态返回主菜单
   
   - **代码架构优化**：
     - 解耦按钮事件处理：单一职责原则，避免多个组件处理同一事件
     - 改进错误处理：添加场景加载异常处理和日志输出
     - 完善状态管理：确保时间、UI状态在场景切换时正确重置
   
   - **修复验证**：
     - 关卡进展应恢复正常：Level 1 → 2 → 3 → 4...
     - menu按钮应能正常返回主菜单
     - 防重入机制应防止意外的双重调用

30. **2024-12-20 游戏逻辑修复与优化** (当前对话)
   - **权重系统修复**：
     - **问题**：游戏开始时所有边权重都为0，导致游戏无法正常进行
     - **根因分析**：`GetBiomeWeight`方法在Level 1时计算结果为0
       - 原计算：`Mathf.Log(2, 2) * 0.1 * 2 = 0.2`，四舍五入后变成0
       - 所有边权重都基于这个0值计算，导致全部为0
     - **修复方案**：重构权重计算逻辑
       - 基础权重：`levelIndex * 2`（Level 1 = 2, Level 2 = 4...）
       - 随机变化：`Random.Range(-levelIndex, levelIndex + 1)`增加多样性
       - 防零保护：如果结果为0，根据关卡奇偶性设为±1
     - **重命名优化**：`GetBiomeWeight` → `CalculateLevelBasedWeight`，避免命名误导
   
   - **胜利判定逻辑修复**：
     - **问题**：当前cost -11，最优cost -13，但游戏误判为胜利
     - **根因分析**：存在两个不同的胜利判定逻辑
       - 正确逻辑：`currentCost == optimalCost`（精确匹配）
       - 错误逻辑：`currentCost <= optimalCost`（小于等于判定）
     - **修复方案**：统一使用精确匹配判定
       - 移除错误的`<=`判定，只保留`==`判定
       - 确保只有达到最优cost时才算获胜
   
   - **关卡跳跃问题再次修复**：
     - **问题复现**：Level 1 → 3 → 5 跳跃式前进
     - **根因**：双重按钮事件绑定导致`NextLevel()`被调用两次
       - GameManager.OnContinueButtonClicked() → NextLevel()
       - VictoryPanelController.HandleContinueClicked() → gm.NextLevel()
     - **修复方案**：再次注释掉GameManager中的重复按钮绑定
       - 保留VictoryPanelController单独处理Continue按钮
       - 修改ShowVictoryPanel()中的回退逻辑直接调用NextLevel()
   
   - **返回主菜单功能恢复**：
     - **问题**：用户反馈GameManager没有ReturnToMainMenu函数
     - **原因**：之前的更改被撤销，函数被删除
     - **修复方案**：重新添加ReturnToMainMenu()公开方法
       - 支持场景切换：优先加载"MainMenu"场景，回退到场景索引0
       - 时间恢复：Time.timeScale = 1f
       - 错误处理：try-catch机制处理场景不存在情况
   
   - **编译错误修复**：
     - **问题**：其他脚本调用已重命名的`GetBiomeWeight`方法导致编译错误
     - **影响文件**：EdgeWeightCalculator.cs、TilemapGameManager.cs
     - **修复方案**：全局替换`GetBiomeWeight` → `CalculateLevelBasedWeight`
     - **验证**：确认所有文件都使用新方法名，无编译错误
   
   - **生态区Toggle查找优化**：
     - **问题**：控制台警告"未找到生态区Toggle"
     - **根因**：代码查找路径不包含实际的UI结构路径"UICanvas/ShowEcoZone"
     - **修复方案**：
       - 添加正确路径：`"UICanvas/ShowEcoZone"`作为主要路径
       - 保留备用路径：以防UI结构变化
       - 降级日志：从`LogWarning`改为`Log`，因为是可选功能
     - **路径优先级**：UICanvas/ShowEcoZone → Canvas/ShowEcoZone → ShowEcoZone → 其他备用路径
   
   - **技术债务清理**：
     - 函数重命名提升代码可读性
     - 移除重复的事件绑定逻辑
     - 优化错误处理和日志输出
     - 统一胜利判定逻辑，避免逻辑分歧
   
   - **测试验证**：
     - ✅ 权重系统：确认Level 1不再出现全0权重
     - ✅ 胜利判定：确认只有达到最优cost才获胜
     - ✅ 关卡进展：确认关卡不再跳跃（1→2→3→4...）
     - ✅ 返回菜单：确认menu按钮正常工作
     - ✅ 编译通过：确认所有脚本编译无错误
     - ✅ UI查找：确认生态区Toggle正常识别