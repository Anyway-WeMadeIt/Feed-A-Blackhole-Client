"""노드 단계 세팅 생성기. Python 3 표준 라이브러리만 쓴다.
레포 루트에서: python Tools/QA/build_node_suite.py

입력: Docs/QA/NodeSuite/zones.json(그림의 단계 1~9 소속), 노드 CSV 4개, NodeCatalog.asset, HqGrowthSetup.asset.
출력: Assets/Playtest/Scenarios/qa-nodes-1~8.json(+.meta), PlaytestLibrary 시나리오 목록, Docs/QA/NodeSuite/zone-map.svg.
N단계 세팅 = 1~N단계 노드를 모두 산 상태(누적). 9단계(가운데 반복 노드)는 아직 만들지 않는다.
"""
import csv, hashlib, html, json, pathlib, re

ROOT = pathlib.Path(__file__).resolve().parents[2]
DOC = ROOT / 'Docs/QA/NodeSuite'
OUT = ROOT / 'Assets/Playtest/Scenarios'
LIBRARY = ROOT / 'Assets/Playtest/PlaytestLibrary.asset'
LAST_STAGE = 8
# 이 도구가 만든(또는 예전에 만들던) 파일 이름. 이번에 만들지 않은 것은 지운다.
GENERATED = re.compile(r'^qa-(nodes-\d+|zone\d\d-(off|on|full)|budget-zone\d\d(-\d)?|mark\d\d-(before|after)|node-first)\.json$')

def read_csv(path):
    with path.open(encoding='utf-8-sig', newline='') as f:
        return list(csv.DictReader(f))

def layout(path):
    text = path.read_text(encoding='utf-8-sig').replace('\r\n', '\n')
    result = []
    for block in re.findall(r'^    - Id: (.*?)(?=^    - Id: |\Z)', text, re.M | re.S):
        result.append({'Id': block.splitlines()[0].strip(),
                       'Start': re.search(r'^      Start: (\d+)$', block, re.M)[1] == '1',
                       'X': int(re.search(r'^      X: (-?\d+)$', block, re.M)[1]),
                       'Y': int(re.search(r'^      Y: (-?\d+)$', block, re.M)[1]),
                       'Links': re.findall(r'^      - (.+)$', block, re.M)})
    if not result:
        raise ValueError('NodeCatalog 배치를 찾지 못함')
    return result

def write_text(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(text.encode('utf-8'))

def guid_of(meta):
    m = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(encoding='utf-8'), re.M) if meta.exists() else None
    return m[1] if m else None

def ensure_meta(json_path):
    meta = json_path.with_name(json_path.name + '.meta')
    if guid_of(meta):
        return
    # 같은 파일 이름이면 같은 GUID(다시 생성해도 Unity 참조가 유지된다).
    guid = hashlib.md5(('BlackHole.NodeSuite/' + json_path.name).encode('utf-8')).hexdigest()
    write_text(meta, f'fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n  externalObjects: {{}}\n'
                     f'  userData: \n  assetBundleName: \n  assetBundleVariant: \n')

def collect_library():
    # PlaytestLibrary.Collect와 같은 규칙: 시나리오 JSON 전부(ai-draft 제외), 이름 ordinal 순.
    entries = []
    for path in OUT.glob('*.json'):
        if path.name == 'ai-draft.json':
            continue
        guid = guid_of(path.with_name(path.name + '.meta'))
        if guid is None:
            raise ValueError('.meta 없음: ' + path.name)
        entries.append((path.stem.encode('utf-16-be'), guid))
    entries.sort()
    text = LIBRARY.read_text(encoding='utf-8')
    block = ''.join(f'  - {{fileID: 4900000, guid: {g}, type: 3}}\n' for _, g in entries)
    new, count = re.subn(r'(  _scenarios:\n)(?:  - \{fileID: 4900000, guid: [0-9a-f]{32}, type: 3\}\n)*', lambda m: m[1] + block, text)
    if count != 1:
        raise ValueError('PlaytestLibrary의 _scenarios 목록을 찾지 못함')
    write_text(LIBRARY, new)

def fmt(v):
    for s, u in ((10**15, 'P'), (10**12, 'T'), (10**9, 'B'), (10**6, 'M'), (10**3, 'K')):
        if v >= s:
            return f'{v / s:.3g}{u}'
    return str(v)

COLORS = {1: '#4fa3e0', 2: '#57c785', 3: '#c77dd8', 4: '#e07b4f', 5: '#5fc9c0', 6: '#d9607a', 7: '#8f9be8', 8: '#b5c95a', 9: '#e2b93b'}

def zone_map(placed, adj, zone_of, zones, price):
    sx, sy, pad = 185, 95, 120
    xs = [n['X'] for n in placed]; ys = [n['Y'] for n in placed]
    def px(x): return pad + (x - min(xs)) * sx
    def py(y): return pad + 140 + (max(ys) - y) * sy
    w = int(px(max(xs)) + pad + 90); h = int(py(min(ys)) + pad)
    legend = '   '.join(f'{z["id"]} {z["title"]}' for z in zones)
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" width="{w}" height="{h}" font-family="sans-serif">',
           '<rect width="100%" height="100%" fill="#101827"/>',
           f'<text x="{pad}" y="70" fill="white" font-size="36">노드 단계 1~9 (그림 기준) / {len(placed)}개 — Tools/QA/build_node_suite.py가 생성</text>',
           f'<text x="{pad}" y="120" fill="#cbd5e1" font-size="22">{html.escape(legend)}</text>']
    by_id = {n['Id']: n for n in placed}
    for a in sorted(adj):
        for b in sorted(adj[a]):
            if a < b:
                A, B = by_id[a], by_id[b]
                out.append(f'<line x1="{px(A["X"])}" y1="{py(A["Y"])}" x2="{px(B["X"])}" y2="{py(B["Y"])}" stroke="#556176" stroke-width="2"/>')
    for n in placed:
        z = zone_of[n['Id']]; x, y = px(n['X']), py(n['Y'])
        stroke = 'white' if n['Start'] else 'none'
        out.append(f'<rect x="{x - 88}" y="{y - 30}" width="176" height="60" rx="6" fill="{COLORS[z]}" fill-opacity="0.85" stroke="{stroke}" stroke-width="3"/>')
        out.append(f'<text x="{x}" y="{y - 6}" fill="#0b1020" font-size="15" text-anchor="middle">{html.escape(n["Id"])}</text>')
        out.append(f'<text x="{x}" y="{y + 18}" fill="#0b1020" font-size="15" text-anchor="middle">[{z}] {fmt(price[n["Id"]])}</text>')
    out.append('</svg>')
    return '\n'.join(out) + '\n'

def main():
    table = ROOT / 'Assets/Data/NodeTable'
    ranks = {r['NodeId']: int(r['Rank 수']) for r in read_csv(table / 'Nodes.csv')}
    cost = {}
    for r in read_csv(table / 'NodeCost.csv'):
        cost[r['NodeId'], int(r['Rank'])] = int(r['Cost'].replace(',', ''))
    placed = layout(ROOT / 'Assets/Data/NodeCatalog.asset')
    by_id = {n['Id']: n for n in placed}
    assert set(by_id) == set(ranks), '노드 CSV와 배치의 ID가 다름'
    adj = {n: set() for n in by_id}
    for n in placed:
        for other in n['Links']:
            adj[n['Id']].add(other); adj[other].add(n['Id'])
    zones = json.loads((DOC / 'zones.json').read_text(encoding='utf-8'))['zones']
    zone_of = {}
    for z in zones:
        for n in z['nodes']:
            assert n in by_id and n not in zone_of, n
            zone_of[n] = z['id']
    assert set(zone_of) == set(by_id), '단계가 없는 노드: ' + ', '.join(sorted(set(by_id) - set(zone_of)))

    growth = (ROOT / 'Assets/Data/HqGrowthSetup.asset').read_text(encoding='utf-8')
    targets = [int(g) for g in re.findall(r'- level: \d+\s+targetGold: (\d+)', growth)]

    written = set(); owned = {}; spent = 0
    for z in zones:
        if z['id'] > LAST_STAGE:
            continue
        for n in z['nodes']:
            owned[n] = ranks[n]
            spent += sum(cost[n, r] for r in range(1, ranks[n] + 1))
        # 성장 단계 추정: 지금까지 쓴 Gold가 넘은 이정표 목표 잔액 수(Lv10 690K / Lv20 5.6M / Lv30 420M).
        stage = sum(1 for t in targets if t <= spent)
        name = f'qa-nodes-{z["id"]}'
        scenario = {
            'name': name,
            'category': 'QA/노드 단계',
            'note': f'노드 1~{z["id"]}단계를 모두 산 상태(구매 비용 합계 {spent:,} Gold). 성장 단계는 이 금액이 넘은 이정표 목표 잔액 수로 추정.',
            'expected': f'{z["id"]}단계에서 새로 열리는 것: {z["unlocks"]}. 확정 정보의 수치와 실제 화면·피해·사운드가 맞는지 확인.',
            'growthStage': stage, 'gold': 0,
            'nodes': [{'nodeId': n, 'rank': r} for n, r in sorted(owned.items())],
            'autoBuyBudget': 0, 'startLevel': 0, 'seed': 1, 'freezeTime': False,
        }
        write_text(OUT / (name + '.json'), json.dumps(scenario, ensure_ascii=False, indent=2) + '\n')
        written.add(name + '.json')
        print(f'{name}: 노드 {len(owned)}개, 비용 {fmt(spent)}, 성장 단계 {stage}')

    for path in sorted(OUT.glob('qa-*.json')):
        if GENERATED.match(path.name) and path.name not in written:
            path.unlink()
            meta = path.with_name(path.name + '.meta')
            if meta.exists():
                meta.unlink()
            print('삭제:', path.name)
    for name in sorted(written):
        ensure_meta(OUT / name)
    collect_library()
    price = {n: cost[n, 1] for n in by_id}
    write_text(DOC / 'zone-map.svg', zone_map(placed, adj, zone_of, zones, price))

if __name__ == '__main__':
    main()
