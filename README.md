# Multicut Game

An interactive game based on Unity that transforms the complex Multicut Problem into an intuitive and engaging gaming experience.

## 🎮 Project Overview

Multicut Game is an educational puzzle game designed to help players understand and solve the multicut problem through gamification. The multicut problem is a classic combinatorial optimization problem in computer science, classified as NP-hard, with important applications in image segmentation, clustering analysis, and other domains.

## 🎯 Game Objective

Players need to find the optimal multicut solution in a given graph, which involves cutting the minimum number of edges (or edges with the lowest cost) to partition the graph into multiple connected components, while ensuring that no cycle contains exactly one cut edge.

## 🛠️ Technology Stack

- **Game Engine**: Unity 2022.3 LTS
- **Programming Languages**: C# (Unity scripts), Python (solver)
- **Optimization Solver**: Gurobi
- **Graphics Rendering**: Universal Render Pipeline (URP)
- **Input System**: Unity Input System
- **UI Framework**: Unity UI Toolkit

## 🎨 Core Features

### Game Features
- **Interactive Graph Operations**: Intuitive click and drag operations to cut edges
- **Real-time Feedback**: Instant display of cut costs and connected components
- **Multiple Difficulty Levels**: Various game levels from simple to complex
- **Optimal Solution Verification**: Uses Gurobi solver to verify the optimality of player solutions
- **Visual Clustering**: Highlight different connected components

### Technical Implementation
- **Unity-Python Integration**: External process calls to Python solver
- **Graph Algorithms**: Implementation of connected component detection and path finding
- **Terrain System**: Hexagonal grid-based terrain generation
- **UI System**: Responsive user interface design

## 📁 Project Structure

```
multicut/
├── Assets/
│   ├── Scripts/           # C# game scripts
│   ├── Scenes/           # Unity scene files
│   ├── Prefabs/          # Prefab resources
│   ├── Material/         # Material files
│   └── Resources/        # Game resources
├── doc/                  # Project documentation
│   ├── Detailed Game Design Documentation Template.md
│   ├── Topic.md         # Project topic description
│   └── tudscr/          # LaTeX document templates
├── input.json           # Solver input file
├── output.json          # Solver output file
└── multicut_solver.py   # Python solver
```

## 🚀 Quick Start

### Requirements
- Unity 2022.3 LTS or higher
- Python 3.7+
- Gurobi optimization solver

### Installation Steps
1. Clone the repository to your local machine
2. Open the project in Unity Hub
3. Install required Python dependencies: `pip install gurobipy`
4. Configure Gurobi license
5. Run the game

## 🎓 Educational Value

This project is not just an entertaining game, but also an educational tool:
- **Algorithm Visualization**: Demonstrates the solving process of multicut problems
- **Optimization Theory**: Introduces combinatorial optimization and NP-hard problems
- **Practical Applications**: Shows applications in image segmentation and other fields
- **Interactive Learning**: Learn complex concepts through gamification

## 📚 Related Research

The project is based on the following academic research:
- Multicut Problem
- Correlation Clustering
- Coalition Structure Generation
- Lifted Multicut

## 👥 Development Team

- **Supervisor**: Jannik Irmai, PhD Student
- **Chairholder**: Prof. Björn Andres
- **Institution**: TU Dresden

## 📄 License

This project follows appropriate academic and open source licenses.

## 🤝 Contributing

Issues and Pull Requests are welcome to improve this project!

---

*Transforming complex optimization problems into engaging gaming experiences* 🎮✨
