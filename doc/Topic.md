# MULTICUT GAME

**Supervisor**: Jannik Irmai, PhD Student  
**Chairholder**: Prof. Björn Andres  

## 1. Preliminaries

The multicut problem is a **combinatorial optimization problem**. The objective is to cut a given graph into multiple components such that the **sum of the cost of the edges that are cut is minimal**.

Let \( G = (V, E) \) be an undirected graph. A set of edges \( M \subseteq E \) is called a **multicut** of \( G \) if and only if for every cycle \( C = (V_C, E_C) \) in \( G \), it holds that \( |M \cap E_C| \ne 1 \). That is, no cycle contains exactly one edge from \( M \).

The set of all multicuts of \( G \) is denoted by \( \mathcal{M}(G) \). The multicut problem is the following optimization problem:

\[
\min_{M \in \mathcal{M}(G)} \sum_{e \in M} c_e
\]

This problem is **NP-hard**, meaning it is unlikely that an efficient algorithm solving it exactly exists. Nevertheless, it has many applications where it produces **state-of-the-art results**.

---

## The Multicut Game

The goal of this project is to **implement a game** where the player **interactively solves a multicut problem**. The game should be an **intuitive and entertaining puzzle**.

### Example Prototype

- Left: level selection
- Middle: graph to solve
- Right: intermediate clustering created by the player

---

## Requirements

1. The game should run in a **browser and/or mobile device**. Frameworks like React or React Native may be used.
2. The game should be as **intuitive as possible**, both visually and in terms of user interaction.
3. The game should include **multiple levels of varying difficulty**, which can be manually designed or generated.
4. The **optimal solution** for each level should be computed by an algorithm to notify the player upon solving it optimally.

---

## Optional Features

### Playful and Entertaining
5. **Smooth and lag-free animations**
6. **Colorful and eye-catching design**
7. **Server-side high score lists**
8. **Multiple game modes**, such as:
   - Solve levels as fast as possible
   - Solve as many levels as possible within a time limit
   - Player vs. player
   - Daily challenges

### Educational
9. **Visualize algorithms** solving the problem through animations
10. **Showcase real-world applications**, such as image segmentation
11. Include **variations** of the problem:
    - Correlation clusterin
