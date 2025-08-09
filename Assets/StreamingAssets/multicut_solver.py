import sys
import json
from gurobipy import Model, GRB

# 读取输入文件
def read_input(input_path):
    with open(input_path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    nodes = data['nodes']
    edges = [(e['u'], e['v'], e['weight']) for e in data['edges']]
    return nodes, edges

# 写入输出文件
def write_output(output_path, cut_edges, cost):
    result = {
        "cut_edges": [{"u": u, "v": v} for (u, v) in cut_edges],
        "cost": cost
    }
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(result, f, ensure_ascii=False, indent=2)

# 求解标准多割问题
def solve_multicut(nodes, edges):
    model = Model()
    model.Params.OutputFlag = 0  # 不输出求解日志

    # 决策变量：每条边是否被切割
    edge_vars = {}
    for u, v, w in edges:
        key = (min(u, v), max(u, v))
        edge_vars[key] = model.addVar(vtype=GRB.BINARY, obj=w, name=f"x_{u}_{v}")

    model.ModelSense = GRB.MINIMIZE
    model.update()

    # 懒约束：防止伪分割
    def find_components(edge_sol):
        parent = {n: n for n in nodes}
        def find(x):
            while parent[x] != x:
                parent[x] = parent[parent[x]]
                x = parent[x]
            return x
        def union(x, y):
            parent[find(x)] = find(y)
        for (u, v, _w) in edges:
            key = (min(u, v), max(u, v))
            if edge_sol[key] < 0.5:
                union(u, v)
        comps = {}
        for n in nodes:
            root = find(n)
            if root not in comps:
                comps[root] = []
            comps[root].append(n)
        return comps

    def callback(model, where):
        if where == GRB.Callback.MIPSOL:
            sol = model.cbGetSolution([edge_vars[e] for e in edge_vars])
            edge_sol = {e: sol[i] for i, e in enumerate(edge_vars)}
            comps = find_components(edge_sol)
            for comp in comps.values():
                comp_set = set(comp)
                for (u, v, _w) in edges:
                    key_uv = (min(u, v), max(u, v))
                    if u in comp_set and v in comp_set and edge_sol[key_uv] > 0.5:
                        from collections import deque
                        prev = {u: None}
                        queue = deque([u])
                        found = False
                        while queue and not found:
                            curr = queue.popleft()
                            for (x, y, _w2) in edges:
                                key_xy = (min(x, y), max(x, y))
                                if (curr == x and y in comp_set and edge_sol[key_xy] < 0.5 and y not in prev):
                                    prev[y] = curr
                                    if y == v:
                                        found = True
                                        break
                                    queue.append(y)
                                elif (curr == y and x in comp_set and edge_sol[key_xy] < 0.5 and x not in prev):
                                    prev[x] = curr
                                    if x == v:
                                        found = True
                                        break
                                    queue.append(x)
                        if found:
                            path = []
                            node = v
                            while node != u:
                                path.append((prev[node], node))
                                node = prev[node]
                            expr = edge_vars[key_uv]
                            for a, b in path:
                                key_ab = (min(a, b), max(a, b))
                                expr -= edge_vars[key_ab]
                            model.cbLazy(expr <= 0)
    model.Params.LazyConstraints = 1
    model.optimize(callback)

    cut_edges = []
    cost = 0
    if model.status == GRB.OPTIMAL or model.status == GRB.TIME_LIMIT:
        for (u, v, w) in edges:
            key = (min(u, v), max(u, v))
            if edge_vars[key].X > 0.5:
                cut_edges.append((u, v))
                cost += w
    return cut_edges, cost

if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("用法: python multicut_solver.py input.json output.json")
        sys.exit(1)
    input_path = sys.argv[1]
    output_path = sys.argv[2]
    nodes, edges = read_input(input_path)
    cut_edges, cost = solve_multicut(nodes, edges)
    write_output(output_path, cut_edges, cost) 