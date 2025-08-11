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

This problem was first studied in the late 20th century and is also known as "correlation clustering" and "coalition structure generation". The multicut problem is **NP-hard**, meaning it is unlikely that an efficient algorithm solving it exactly exists. Nevertheless, it has many interesting applications where it produces **state-of-the-art results**, leading to significant research efforts in understanding it.

**Figure 1**: Depicted above is a multicut of a graph that cuts that graph into three induced components. The dotted edges are the edges that are cut. The shaded areas are the induced components.

---

## The Multicut Game

The goal of this project is to **implement a game** where the player **interactively tries to solve a multicut problem**. The game should be an **intuitive and entertaining puzzle game**.

**Figure 2**: Depicted above is a prototype of the game. On the left the player can choose between various levels. Depicted in the middle is a graph for which the player should find the optimal multicut. Depicted on the right is an intermediate clustering that the player has created.

---

## Requirements

1. The game should run in a **browser and/or on a mobile device**. A suitable software framework can be chosen freely (e.g., `react`, `react-native`).
2. The game should be as **intuitive as possible**, including its visual appearance and the operations available to the player to manipulate the graph.
3. The game should contain **multiple levels of varying difficulty**, which can be hand-designed or automatically generated.
4. The **optimal solution** for each level should be computed by an algorithm such that the player can be notified if an optimal solution is found.

---

## Optional Features

### Playful and Entertaining
5. **Lag-free, smooth, and graceful animations**
6. **Colorful and eye-catching design**
7. **Server-side high score lists**
8. **Different game modes**, such as:
   - Solve a level as fast as possible
   - Solve as many levels as possible in a fixed amount of time
   - Player vs. player
   - Daily challenges

### Educational
9. **Illustrate how an algorithm solves the problem** by animating the operations that the algorithm performs
10. **Illustrate how the multicut problem is used** to solve real-world tasks (e.g., image segmentation)
11. **Incorporate different problem variations**, such as:
    - Correlation clustering
    - Coalition structure generation
    - Lifted multicut

Creativity is encouraged for further game improvements.

---

## References

1. Bansal, N., Blum, A., & Chawla, S. (2004). Correlation clustering. Machine Learning, 56(1-3), 89-113.
2. Demaine, E. D., Emanuel, D., Fiat, A., & Immorlica, N. (2006). Correlation clustering in general weighted graphs. Theoretical Computer Science, 361(2-3), 172-187.
3. Charikar, M., Guruswami, V., & Wirth, A. (2005). Clustering with qualitative information. Journal of Computer and System Sciences, 71(3), 360-383.
4. Ailon, N., Charikar, M., & Newman, A. (2008). Aggregating inconsistent information: ranking and clustering. Journal of the ACM, 55(5), 1-27.
5. Grötschel, M., & Wakabayashi, Y. (1989). A cutting plane algorithm for a clustering problem. Mathematical Programming, 45(1-3), 59-96.
6. Andres, B., Kappes, J. H., Beier, T., Köthe, U., & Hamprecht, F. A. (2012). Probabilistic image segmentation with closedness constraints. In 2011 International Conference on Computer Vision (pp. 2611-2618). IEEE.
7. Andres, B., Kröger, T., Briggman, K. L., Denk, W., Korogod, N., Knott, G., ... & Hamprecht, F. A. (2012). Globally optimal closed-surface segmentation for connectomics. In European Conference on Computer Vision (pp. 778-791). Springer.
8. Beier, T., Andres, B., Köthe, U., & Hamprecht, F. A. (2016). An efficient fusion move algorithm for the minimum cost lifted multicut problem. In European Conference on Computer Vision (pp. 715-730). Springer.
9. Keuper, M., Levinkov, E., Bonneel, N., Lavoue, G., Brox, T., & Andres, B. (2015). Efficient decomposition of image and mesh graphs by lifted multicuts. In Proceedings of the IEEE International Conference on Computer Vision (pp. 1751-1759).
10. Levinkov, E., Uhrig, J., Tang, S., Omran, M., Insafutdinov, E., Kirillov, A., ... & Andres, B. (2017). Joint graph decomposition & node labeling: Problem, algorithms, applications. In Proceedings of the IEEE Conference on Computer Vision and Pattern Recognition (pp. 6012-6020).
11. Andres, B., Kappes, J. H., Beier, T., Köthe, U., & Hamprecht, F. A. (2013). The lazy flipper: MAP inference in higher-order graphical models by depth-limited exhaustive search. In International Conference on Learning Representations.
12. Andres, B., Kappes, J. H., Beier, T., Köthe, U., & Hamprecht, F. A. (2013). The lazy flipper: Efficient depth-limited exhaustive search in discrete graphical models. In European Conference on Computer Vision (pp. 154-166). Springer.
13. Andres, B., Kappes, J. H., Beier, T., Köthe, U., & Hamprecht, F. A. (2013). The lazy flipper: MAP inference in higher-order graphical models by depth-limited exhaustive search. In International Conference on Learning Representations.

---

**Contact Information**:  
Office 3032, APB, TU Dresden  
E-mail address: jannik.irmal@tu-dresden.de
