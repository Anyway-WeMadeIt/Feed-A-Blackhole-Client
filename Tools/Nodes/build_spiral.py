# 나선 노드 트리 생성기 — 원작 노드 트리(Tools/Nodes/original)를 40분 플레이용으로 접어 다시 배치한다.
#
#   python Tools/Nodes/build_spiral.py            # CSV 3개 + NodeCatalog 배치 + zones.json + 미리보기 SVG
#   python Tools/Nodes/build_spiral.py --check    # 쓰지 않고 검사만
#
# 원작과 다른 점:
# - 구역(우주 구역) 9개가 한 바퀴 돌아 안쪽 블랙홀로 떨어지는 나선. 구역 = 원작의 단계(zones.json 1~8 + 은하).
#   구역 입구(가장 싼 노드)에서 가지 3줄(가운데 = Breaker·성장, 한쪽 = 적 종류, 반대쪽 = 특수 성질)로 퍼지고, 가운데 줄 끝이 다음 구역 입구로 이어진다.
# - 구역 안은 섹터로 나뉜다: 같은 수치의 원작 노드를 가격 순으로 1번째·2번째·…로 보고, k번째들을 모아 섹터 k로 둔다.
#   섹터 하나 = 가운데 줄의 입구 노드 + 양옆 줄(지그재그)의 나머지 노드 → 한 섹터 안에서 여러 수치를 골고루 올린다.
#   보물(Breaker 피해·질량·수)의 k번째는 섹터 k에서 섹터 k+1로 가는 가운데 통로에 놓는다(주요 보상 = 다음 섹터로 가는 길).
#   노드는 원작 그대로 1 Rank씩(여러 Rank는 블랙홀만). 값·가격은 원작 그대로. 노드 그림은 StatId로 고르므로 그림이 더 필요하지 않다.
# - 블랙홀(가운데)은 원작 9단계 반복 노드 8개 그대로. 마지막 구역(은하핵)에서 이어진다.
# 숨은 노드를 흐리게 보여 주는 것(안개)은 NodeTreeView가 맡는다.
import argparse, collections, csv, json, os

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ORIGINAL = os.path.join(ROOT, "Tools", "Nodes", "original")
TABLE = os.path.join(ROOT, "Assets", "Data", "NodeTable")
CATALOG = os.path.join(ROOT, "Assets", "Data", "NodeCatalog.asset")
ZONES = os.path.join(ROOT, "Docs", "QA", "NodeSuite", "zones.json")
PREVIEW = os.path.join(ROOT, "Docs", "QA", "NodeSuite", "spiral-map.svg")

MAX_RANKS = 1  # 구역 노드는 접지 않는다(원작 노드 하나 = 노드 하나). 블랙홀만 10 Rank.

# 구역: (원작 단계, 이름, 여는 것, 가격 배율). 'G'는 zones.json에 없는 은하 노드. 화면 이름 열쇠(REGION_KEYS)는 UIText.csv의 node.region.<key>.
# 가격 배율: 원작은 2시간 분량이라 별 이후(6단계~)가 비싸다. 40분에 맞춰 별 이후를 당긴다(시뮬레이션으로 맞춘 값).
REGIONS = [
    (1, "먼지 띠", "Breaker·소행성 기본", 1),
    (2, "소행성대", "성장 공급·시간·재생성", 1),
    (3, "전기 성운 · 황금 광맥", "전기 소행성·황금 소행성", 0.4),
    (4, "행성계", "행성 변환·달·혜성", 0.5),
    (5, "혜성 궤도", "혜성 비·행성 공급", 0.5),
    (6, "항성 요람", "별 변환·전기 별", 0.3),
    (7, "펄서 · 초신성", "레이저 별·슈퍼노바 별", 1),
    (8, "초신성 잔해", "슈퍼노바·레이저 후반·시간", 0.5),
    ("G", "은하핵", "은하 변환·중력장", 0.1),
]
# 블랙홀 반복 노드: Rank 1 가격 × CENTER_GROWTH^(Rank-1). 원작은 10 Rank 모두 500T.
CENTER_SCALE = 0.1
CENTER_GROWTH = 1.3
CENTER_ZONE = 9
CENTER_NAME = "블랙홀"
REGION_KEYS = {1: "dust", 2: "belt", 3: "nebula", 4: "planets", 5: "comets", 6: "cradle", 7: "pulsar", 8: "remnant", "G": "core", 9: "blackhole"}

# 줄 배정: 수치 계열 → 줄. 가운데(0) Breaker·성장·시간, 한쪽(+1) 적 종류, 반대쪽(-1) 특수 성질.
LANE_OF = {
    "breaker": 0, "timer": 0, "growth": 0,
    "asteroid": 1, "planet": 1, "star": 1, "galaxy": 1, "comet": 1, "moonPlanet": 1,
    "electricAsteroid": -1, "goldenAsteroid": -1, "electricStar": -1, "laserStar": -1, "supernovaStar": -1, "gravityGalaxy": -1,
}
# 관문(구역을 여는 노드)으로 크게 그릴 수치.
KEYSTONES = {"growth.time", "electricAsteroid.spawnChance", "goldenAsteroid.spawnChance", "asteroid.toPlanet", "moonPlanet.spawnChance",
             "comet.spawnChance", "comet.rainChance", "planet.toStar", "electricStar.spawnChance", "laserStar.spawnChance",
             "supernovaStar.spawnChance", "star.toGalaxy", "gravityGalaxy.spawnChance", "timer"}


# 보물: 섹터 사이 통로에 놓는 핵심 수치(계열별로 k번째가 통로 k).
TREASURE_FAMILY = {
    "breaker.damage": "damage",
    "asteroid.count": "count",
    "asteroid.massScale": "mass", "planet.massScale": "mass", "star.massScale": "mass", "galaxy.massScale": "mass",
}


def read_original():
    zones = json.load(open(os.path.join(ORIGINAL, "zones.json"), encoding="utf-8"))
    zone = {n: z["id"] for z in zones["zones"] for n in z["nodes"]}
    ids = [r["NodeId"] for r in csv.DictReader(open(os.path.join(ORIGINAL, "Nodes.csv"), encoding="utf-8-sig"))]
    cost = collections.defaultdict(dict)
    for r in csv.DictReader(open(os.path.join(ORIGINAL, "NodeCost.csv"), encoding="utf-8-sig")):
        cost[r["NodeId"]][int(r["Rank"])] = int(float(r["Cost"].replace(",", "")))
    effect = collections.defaultdict(dict)
    for r in csv.DictReader(open(os.path.join(ORIGINAL, "NodeEffects.csv"), encoding="utf-8-sig")):
        effect[r["NodeId"]][int(r["Rank"])] = (r["StatId"], float(r["Value"]), r["단위"])
    for n in ids:
        zone.setdefault(n, "G")
    return ids, zone, cost, effect


def fold(ids, zone, cost, effect, zone_id, max_ranks=MAX_RANKS, scale=1):
    # 같은 수치의 Rank들을 가격 순으로 모아 max_ranks씩 끊는다. 반환: [(stat, unit, [values], [costs])] 첫 가격 순.
    per = collections.defaultdict(list)
    for n in ids:
        if zone[n] != zone_id:
            continue
        for rank in sorted(cost[n]):
            stat, value, unit = effect[n][rank]
            per[stat].append((cost[n][rank], value, unit))
    items = []
    for stat, rows in per.items():
        rows.sort()
        for i in range(0, len(rows), max_ranks):
            chunk = rows[i:i + max_ranks]
            items.append((stat, chunk[0][2], [v for _, v, _ in chunk], [int(round(c * scale)) for c, _, _ in chunk]))
    items.sort(key=lambda it: it[3][0])
    return items


class Builder:
    def __init__(self):
        self.counter = {}
        self.nodes = []
        self.by_id = {}
        self.cells = {}

    def add(self, stat, unit, values, costs, x, y, zone, start=False):
        if (x, y) in self.cells:
            raise SystemExit(f"칸 겹침 ({x},{y}): {self.cells[(x, y)]} / {stat}")
        n = self.counter.get(stat, 0) + 1
        self.counter[stat] = n
        node = dict(id=f"{stat}-{n:02d}", stat=stat, unit=unit, ranks=len(values), values=list(values), costs=list(costs),
                    x=x, y=y, links=[], start=start, zone=zone)
        self.nodes.append(node)
        self.by_id[node["id"]] = node
        self.cells[(x, y)] = node["id"]
        return node

    def link(self, a, b):
        if b["id"] not in a["links"] and a["id"] not in b["links"]:
            a["links"].append(b["id"])


DIRS = {"R": (1, 0), "D": (0, -1), "L": (-1, 0), "U": (0, 1)}


SECTOR_MAX = 8    # 섹터 하나의 노드 수 상한(보물 제외)
CORRIDOR_MAX = 3  # 통로 하나의 보물 수 상한


def sectors_of(items):
    """items(가격 순, 1 Rank씩) → [(섹터 노드들, 그 뒤 통로의 보물들)].
    보물 아닌 노드는 (수치 안의 몇 번째인가, 가격) 순으로 늘어놓고 비슷한 크기의 섹터로 자른다 — 앞 섹터는 각 수치의 1번째, 뒤로 갈수록 2번째·3번째.
    보물은 가격 순으로 통로마다 고르게 나눈다(앞 통로가 하나 더). 마지막 통로는 구역 출구다."""
    seen = collections.Counter()
    plain, treasure = [], []
    for it in items:
        if it[0] in TREASURE_FAMILY:
            treasure.append(it)
        else:
            plain.append((seen[it[0]], it[3][0], it))
            seen[it[0]] += 1
    plain.sort(key=lambda t: (t[0], t[1]))
    count = max(1, -(-len(plain) // SECTOR_MAX), -(-len(treasure) // CORRIDOR_MAX))
    sectors = [sorted([t[2] for t in plain[i * len(plain) // count:(i + 1) * len(plain) // count]], key=lambda it: it[3][0]) for i in range(count)]
    corridors = [treasure[i * len(treasure) // count:(i + 1) * len(treasure) // count] for i in range(count)]
    return list(zip(sectors, corridors))


def place_region(b, zone_id, items, d, entry, region_index):
    """섹터 배치. 가운데 줄: 섹터 입구 → 통로(보물) → 다음 섹터 입구 → … 옆줄: 섹터의 나머지 노드(지그재그).
    반환: (입구 노드, 끝 노드)."""
    ux, uy = DIRS[d]
    vx, vy = -uy, ux
    cell = lambda u, v: (entry[0] + ux * u + vx * v, entry[1] + uy * u + vy * v)
    u0 = 0
    head = None
    prev_center = None
    prev_side_ends = []
    for k, (sector, corridor) in enumerate(sectors_of(items)):
        if not sector:
            continue
        first, rest = sector[0], sector[1:]
        stat, unit, values, costs = first
        entry_node = b.add(stat, unit, values, costs, *cell(u0, 0), zone_id, start=(region_index == 0 and k == 0))
        head = head or entry_node
        if prev_center is not None:
            b.link(prev_center, entry_node)
        # 앞 섹터 옆줄 끝이 가까우면 이어 작은 고리(우회로).
        for end in prev_side_ends:
            if max(abs(end["x"] - entry_node["x"]), abs(end["y"] - entry_node["y"])) <= 2:
                b.link(end, entry_node)
        # 옆줄: 번갈아 +1/-1. u0+1에서 v=±1, 그다음 ±2/±3 지그재그.
        lanes = {1: [], -1: []}
        for i, it in enumerate(rest):
            lanes[1 if i % 2 == 0 else -1].append(it)
        side_ends = []
        for lane, lst in lanes.items():
            prev = entry_node
            for i, (st, un, va, co) in enumerate(lst):
                x, y = cell(u0 + 1, lane) if i == 0 else cell(u0 + 1 + i, (2 + (i + 1) % 2) * lane)
                node = b.add(st, un, va, co, x, y, zone_id)
                b.link(prev, node)
                prev = node
            if lst:
                side_ends.append(prev)
        # 통로: 가운데 줄, 다음 섹터 입구 바로 앞에 보물을 한 줄로(옆줄이 더 길면 그만큼 뒤로).
        side_len = max([len(lst) for lst in lanes.values()] + [0])
        span = max(len(corridor), side_len)
        prev = entry_node
        corridor_nodes = []
        for i, (st, un, va, co) in enumerate(corridor):
            node = b.add(st, un, va, co, *cell(u0 + span - len(corridor) + 1 + i, 0), zone_id)
            b.link(prev, node)
            prev = node
            corridor_nodes.append(node)
        # 옆줄 끝이 통로 보물과 가까우면 이어 고리를 닫는다(옆줄이 막다른 길로 끝나지 않게).
        for end in side_ends:
            near = [c for c in corridor_nodes if max(abs(end["x"] - c["x"]), abs(end["y"] - c["y"])) <= 2]
            if near:
                b.link(end, min(near, key=lambda c: abs(end["x"] - c["x"]) + abs(end["y"] - c["y"])))
        prev_center = prev
        prev_side_ends = [e for e in side_ends if not any(max(abs(e["x"] - c["x"]), abs(e["y"] - c["y"])) <= 2 for c in corridor_nodes)]
        u0 += span + 1
    return head, prev_center


def build():
    ids, zone, cost, effect = read_original()
    b = Builder()
    # 한 바퀴 도는 사각 나선: 위 변(R1·R2·R3 →), 오른쪽 변(R4·R5 ↓), 아래 변(R6·R7 ←), 왼쪽 변(R8 ↑), 안쪽(R9 →) → 블랙홀.
    # 구역 사이는 GAP칸 비워 "구역 사이를 건너는" 긴 선이 된다.
    GAP = 3
    turns = ["R", "R", "R", "D", "D", "L", "L", "U", "R"]
    cursor = (-20, 14)
    prev_tail = None
    prev_dir = None
    regions_out = []
    for index, ((zone_id, name, unlocks, scale), d) in enumerate(zip(REGIONS, turns)):
        items = fold(ids, zone, cost, effect, zone_id, scale=scale)
        if prev_dir is not None and d != prev_dir:
            px, py = DIRS[prev_dir]
            nx, ny = DIRS[d]
            cursor = (prev_tail["x"] + px * GAP + nx * 4, prev_tail["y"] + py * GAP + ny * 4)
        elif prev_tail is not None:
            px, py = DIRS[d]
            cursor = (prev_tail["x"] + px * (GAP + 1), prev_tail["y"] + py * (GAP + 1))
        head, tail = place_region(b, zone_id, items, d, cursor, index)
        if prev_tail is not None:
            b.link(prev_tail, head)
        prev_tail, prev_dir = tail, d
        regions_out.append(dict(id=index + 1, zone=zone_id, title=name, unlocks=unlocks))
    # 블랙홀: 원작 9단계 반복 노드 8개(10 Rank)를 3×3 고리로. 마지막 구역 끝에서 가장 가까운 칸으로 잇는다.
    merged = collections.OrderedDict()
    for stat, unit, values, costs in fold(ids, zone, cost, effect, CENTER_ZONE, max_ranks=100, scale=CENTER_SCALE):
        merged[stat] = (unit, values, [int(round(costs[0] * CENTER_GROWTH ** r)) for r in range(len(costs))])
    cx, cy = prev_tail["x"] + 4, prev_tail["y"]
    ring = [(-1, 1), (0, 1), (1, 1), (1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0)]
    center_nodes = []
    for (stat, (unit, values, costs)), (dx, dy) in zip(merged.items(), ring):
        center_nodes.append(b.add(stat, unit, values, costs, cx + dx, cy + dy, CENTER_ZONE))
    for i in range(len(center_nodes)):
        b.link(center_nodes[i], center_nodes[(i + 1) % len(center_nodes)])
    nearest = min(center_nodes, key=lambda n: abs(n["x"] - prev_tail["x"]) + abs(n["y"] - prev_tail["y"]))
    b.link(prev_tail, nearest)
    regions_out.append(dict(id=CENTER_ZONE, zone=CENTER_ZONE, title=CENTER_NAME, unlocks="10 Rank 반복 노드"))
    return b, regions_out


def check(b):
    adj = {n["id"]: set() for n in b.nodes}
    for n in b.nodes:
        for l in n["links"]:
            if l not in adj:
                raise SystemExit(f"없는 노드로 연결: {n['id']} → {l}")
            adj[n["id"]].add(l)
            adj[l].add(n["id"])
    starts = [n["id"] for n in b.nodes if n["start"]]
    if not starts:
        raise SystemExit("시작 노드가 없다.")
    seen = set(starts)
    stack = list(starts)
    while stack:
        k = stack.pop()
        for m in adj[k]:
            if m not in seen:
                seen.add(m)
                stack.append(m)
    missing = [n["id"] for n in b.nodes if n["id"] not in seen]
    if missing:
        raise SystemExit(f"시작에서 닿지 않는 노드: {missing}")
    for n in b.nodes:
        for r in range(1, n["ranks"]):
            if n["costs"][r] < n["costs"][r - 1]:
                raise SystemExit(f"{n['id']}: Rank {r + 1} 가격이 앞 Rank보다 싸다.")
    return [(n["id"], l) for n in b.nodes for l in n["links"]
            if max(abs(n["x"] - b.by_id[l]["x"]), abs(n["y"] - b.by_id[l]["y"])) > 1]


def fmt(v):
    return f"{v:g}"


def write(b, regions):
    nl = "\r\n"
    names = {r["zone"]: r["title"] for r in regions}
    with open(os.path.join(TABLE, "Nodes.csv"), "w", encoding="utf-8", newline="") as f:
        f.write("NodeId,Rank 수,Memo" + nl)
        for n in b.nodes:
            f.write(f"{n['id']},{n['ranks']},{names[n['zone']]}" + nl)
    with open(os.path.join(TABLE, "NodeCost.csv"), "w", encoding="utf-8", newline="") as f:
        f.write("NodeId,Rank,Cost" + nl)
        for n in b.nodes:
            for r in range(n["ranks"]):
                f.write(f"{n['id']},{r + 1},{n['costs'][r]}" + nl)
    with open(os.path.join(TABLE, "NodeEffects.csv"), "w", encoding="utf-8", newline="") as f:
        f.write("NodeId,Rank,StatId,Value,단위,표시" + nl)
        for n in b.nodes:
            for r in range(n["ranks"]):
                v = n["values"][r]
                shown = f"+{fmt(v)}%" if n["unit"] == "Percent" else f"+{fmt(v)}"
                f.write(f"{n['id']},{r + 1},{n['stat']},{fmt(v)},{n['unit']},{shown}" + nl)
    with open(CATALOG, encoding="utf-8") as f:
        text = f.read()
    head = text[: text.index("  _layout:")]
    lines = [head, "  _layout:\n", "    Nodes:\n"]
    for n in b.nodes:
        lines.append(f"    - Id: {n['id']}\n      Start: {1 if n['start'] else 0}\n      X: {n['x']}\n      Y: {n['y']}\n")
        lines.append("      Links:\n" + "".join(f"      - {l}\n" for l in n["links"]) if n["links"] else "      Links: []\n")
    lines.append("    Regions:\n")
    for r in regions:
        members = [n["id"] for n in b.nodes if n["zone"] == r["zone"]]
        lines.append(f"    - Id: {REGION_KEYS[r['zone']]}\n      Nodes:\n" + "".join(f"      - {m}\n" for m in members))
    with open(CATALOG, "w", encoding="utf-8", newline="\n") as f:
        f.write("".join(lines))
    zones = [dict(id=r["id"], title=r["title"], unlocks=r["unlocks"], nodes=sorted(n["id"] for n in b.nodes if n["zone"] == r["zone"]))
             for r in regions]
    os.makedirs(os.path.dirname(ZONES), exist_ok=True)
    with open(ZONES, "w", encoding="utf-8") as f:
        json.dump(dict(schema=3, interpretation="나선 노드 트리의 구역 1~9(9 = 블랙홀). Tools/Nodes/build_spiral.py가 만든다. 구역 번호 = 원작 단계 순서.",
                       zones=zones), f, ensure_ascii=False, indent=2)
        f.write("\n")


def preview(b, regions, path):
    colors = {"timer": "#9aa0a6", "growth": "#9aa0a6", "breaker": "#e8c547", "asteroid": "#c27a4a", "electricAsteroid": "#d9a06a",
              "goldenAsteroid": "#f2d16b", "planet": "#5aa0e0", "moonPlanet": "#8cc4f0", "comet": "#7fe0d8", "star": "#f0f0f0",
              "electricStar": "#d8e8ff", "laserStar": "#ffb3b3", "supernovaStar": "#ff8c69", "galaxy": "#b57ee8", "gravityGalaxy": "#d0a6f5"}
    C, pad = 30, 30
    xs = [n["x"] for n in b.nodes]
    ys = [n["y"] for n in b.nodes]
    minx, maxx, miny, maxy = min(xs), max(xs), min(ys), max(ys)
    W = (maxx - minx + 1) * C + pad * 2
    H = (maxy - miny + 1) * C + pad * 2
    P = lambda n: (pad + (n["x"] - minx) * C + C / 2, pad + (maxy - n["y"]) * C + C / 2)
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" style="font-family:sans-serif">',
           f'<rect width="{W}" height="{H}" fill="#14161a"/>']
    for n in b.nodes:
        for l in n["links"]:
            a, c = P(n), P(b.by_id[l])
            far = max(abs(n["x"] - b.by_id[l]["x"]), abs(n["y"] - b.by_id[l]["y"])) > 1
            dash = ' stroke-dasharray="5 4"' if far else ""
            out.append(f'<line x1="{a[0]:.0f}" y1="{a[1]:.0f}" x2="{c[0]:.0f}" y2="{c[1]:.0f}" stroke="{"#7a8aa0" if far else "#444a55"}" stroke-width="2"{dash}/>')
    for n in b.nodes:
        x, y = P(n)
        fam = n["stat"].split(".")[0]
        r = 9 if n["zone"] == CENTER_ZONE else 8
        fill = colors.get(fam, "#888")
        if n["stat"] in TREASURE_FAMILY:
            rr = r + 2
            out.append(f'<rect x="{x - rr:.0f}" y="{y - rr:.0f}" width="{2 * rr:.0f}" height="{2 * rr:.0f}" rx="3" fill="{fill}" stroke="#ffd36b" stroke-width="2.5"/>')
        elif n["stat"] in KEYSTONES:
            out.append(f'<polygon points="{x:.0f},{y - r - 4:.0f} {x + r + 4:.0f},{y:.0f} {x:.0f},{y + r + 4:.0f} {x - r - 4:.0f},{y:.0f}" fill="{fill}" stroke="#fff" stroke-width="2"/>')
        else:
            out.append(f'<circle cx="{x:.0f}" cy="{y:.0f}" r="{r}" fill="{fill}" stroke="{"#fff" if n["start"] else "#000"}" stroke-width="{3 if n["start"] else 1}"/>')
        if n["ranks"] > 1:
            out.append(f'<text x="{x:.0f}" y="{y + 3:.0f}" fill="#000" font-size="8" text-anchor="middle">×{n["ranks"]}</text>')
    for r in regions:
        ns = [n for n in b.nodes if n["zone"] == r["zone"]]
        cx = sum(P(n)[0] for n in ns) / len(ns)
        cy = min(P(n)[1] for n in ns) - 16
        lo = min(n["costs"][0] for n in ns)
        hi = max(n["costs"][-1] for n in ns)
        out.append(f'<text x="{cx:.0f}" y="{cy:.0f}" fill="#bbb" font-size="12" text-anchor="middle">{r["id"]} {r["title"]} · {len(ns)}노드 · {lo:.3g}~{hi:.3g}</text>')
    out.append("</svg>")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(out))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--preview", default=PREVIEW)
    args = ap.parse_args()
    b, regions = build()
    long_links = check(b)
    ranks = sum(n["ranks"] for n in b.nodes)
    print(f"노드 {len(b.nodes)} · Rank {ranks} · 칸 x {min(n['x'] for n in b.nodes)}..{max(n['x'] for n in b.nodes)} "
          f"y {min(n['y'] for n in b.nodes)}..{max(n['y'] for n in b.nodes)} · 긴 선 {len(long_links)}")
    for r in regions:
        ns = [n for n in b.nodes if n["zone"] == r["zone"]]
        print(f"  [{r['id']}] {r['title']}: 노드 {len(ns)} Rank {sum(n['ranks'] for n in ns)} 가격 {min(n['costs'][0] for n in ns):.3g}~{max(n['costs'][-1] for n in ns):.3g}")
    if not args.check:
        write(b, regions)
        preview(b, regions, args.preview)
        print("썼다:", TABLE, CATALOG, ZONES, args.preview)


if __name__ == "__main__":
    main()
