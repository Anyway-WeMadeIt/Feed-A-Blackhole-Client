# 밝은 UI 그림(크림 #F3F1E8 바탕, 먹 #202124 선, 초록 강조)에서 다크 테마 그림을 만든다.
# - 무채색은 밝기를 뒤집어 아이보리 ↔ 어두운 표면 사이로 옮기고, 유채색(초록·빨강 등)은 그대로 둔다. 알파는 유지한다.
# - 출력: Assets/Art/GUI/Dark/<원본 상대 경로>, 배경은 Assets/Art/Backgrounds/<이름>_Dark.png
# - .meta는 원본의 임포트 설정(9-slice 테두리 포함)을 복사하고 guid·spriteID만 새로 만든다. 이미 있으면 guid를 유지한다.
# 실행: python Tools/UI/build_dark_sprites.py  (저장소 루트에서, Pillow 필요)
import os, re, sys, uuid, json
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets')
IVORY = (242, 241, 232)
SURFACE = (42, 45, 51)      # 크림 바탕 → 어두운 표면(배경보다 한 단계 밝음)
BACKDROP = (20, 23, 28)     # 화면 배경 그림의 바탕(전투 배경 키트의 #10131A와 비슷한 톤)
LIGHT_MIN, LIGHT_MAX = 32, 243   # 먹 ~ 크림

SOURCES = [
    'Art/GUI/Buttons/circle_normal', 'Art/GUI/Buttons/circle_hover', 'Art/GUI/Buttons/circle_pressed', 'Art/GUI/Buttons/circle_disabled',
    'Art/GUI/Buttons/rect_normal', 'Art/GUI/Buttons/rect_hover', 'Art/GUI/Buttons/rect_pressed', 'Art/GUI/Buttons/rect_disabled',
    'Art/GUI/Buttons/square_normal', 'Art/GUI/Buttons/square_hover', 'Art/GUI/Buttons/square_pressed', 'Art/GUI/Buttons/square_disabled',
    'Art/GUI/Buttons/hexagon_normal', 'Art/GUI/Buttons/hexagon_hover', 'Art/GUI/Buttons/hexagon_pressed', 'Art/GUI/Buttons/hexagon_disabled',
] + [f'Art/GUI/Buttons/Ready/{n}_{s}' for n in ('back','exit','home','play','restart','settings','upgrade_tree') for s in ('normal','hover','pressed','disabled')] + [
    'Art/GUI/Controls/panel', 'Art/GUI/Controls/tooltip',
    'Art/GUI/Controls/slider_track', 'Art/GUI/Controls/slider_knob', 'Art/GUI/Controls/slider_fill',
    'Art/GUI/Controls/toggle_off', 'Art/GUI/Controls/toggle_on', 'Art/GUI/Controls/checkbox_off', 'Art/GUI/Controls/checkbox_on',
    'Art/GUI/Settlement/Components/bar_track',
    'Art/Backgrounds/Background_Arcs', 'Art/Backgrounds/Background_Warm',
] + sorted(p[len(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets') + os.sep):-4].replace(os.sep, '/') for p in __import__('glob').glob(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Art', 'GUI', 'Nodes', '*.png')))
RIM_SOURCES = ['Art/GUI/Tokens/blackhole']   # 검정 실루엣: 검정 채움 + 아이보리 테두리

def lerp(a, b, t): return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))

def remap(px, dark=SURFACE):
    r, g, b, a = px
    mx, mn = max(r, g, b), min(r, g, b)
    sat = (mx - mn) / mx if mx else 0
    if sat > 0.18:            # 유채색(초록 강조 등)은 그대로
        return px
    lum = 0.299 * r + 0.587 * g + 0.114 * b
    t = min(1.0, max(0.0, (lum - LIGHT_MIN) / (LIGHT_MAX - LIGHT_MIN)))
    return lerp(IVORY, dark, t) + (a,)

def out_path(rel):
    if rel.startswith('Art/Backgrounds/'):
        return rel + '_Dark'
    return rel.replace('Art/GUI/', 'Art/GUI/Dark/', 1)

def write_meta(src_meta, dst_meta):
    s = open(src_meta, encoding='utf-8').read()
    if os.path.exists(dst_meta):
        guid = re.search(r'guid: (\w+)', open(dst_meta, encoding='utf-8').read()).group(1)
    else:
        guid = uuid.uuid4().hex
    s = re.sub(r'guid: \w+', 'guid: ' + guid, s, 1)
    s = re.sub(r'spriteID: \w+', 'spriteID: ' + uuid.uuid4().hex, s)
    open(dst_meta, 'w', encoding='utf-8', newline='\n').write(s)
    return guid

def convert(rel, rim=False):
    src = os.path.join(ROOT, rel + '.png')
    if not os.path.exists(src):
        print('skip (없음):', rel); return None
    im = Image.open(src).convert('RGBA')
    if rim:
        from PIL import ImageFilter
        alpha = im.getchannel('A')
        inner = alpha.filter(ImageFilter.MinFilter(21))   # 안쪽으로 10px 깎은 마스크
        w, h = im.size
        out = Image.new('RGBA', im.size)
        px = out.load(); ap = alpha.load(); ip = inner.load()
        for y in range(h):
            for x in range(w):
                a = ap[x, y]
                if a == 0: continue
                px[x, y] = (0, 0, 0, a) if ip[x, y] > 128 else IVORY + (a,)
    else:
        out = Image.new('RGBA', im.size)
        dark = BACKDROP if rel.startswith('Art/Backgrounds/') else SURFACE
        out.putdata([remap(p, dark) for p in im.getdata()])
    dst_rel = out_path(rel)
    dst = os.path.join(ROOT, dst_rel + '.png')
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    out.save(dst, optimize=True)
    guid = write_meta(src + '.meta', dst + '.meta')
    return dst_rel, guid

def ensure_folder_metas():
    for d in ['Art/GUI/Dark', 'Art/GUI/Dark/Buttons', 'Art/GUI/Dark/Buttons/Ready', 'Art/GUI/Dark/Controls', 'Art/GUI/Dark/Settlement', 'Art/GUI/Dark/Settlement/Components', 'Art/GUI/Dark/Tokens', 'Art/GUI/Dark/Nodes']:
        p = os.path.join(ROOT, d)
        if not os.path.isdir(p): continue
        m = p + '.meta'
        if not os.path.exists(m):
            open(m, 'w', newline='\n').write(f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')

if __name__ == '__main__':
    made = {}
    for rel in SOURCES:
        r = convert(rel)
        if r: made[rel] = r
    for rel in RIM_SOURCES:
        r = convert(rel, rim=True)
        if r: made[rel] = r
    ensure_folder_metas()
    json.dump(made, open(os.path.join(ROOT, '..', 'Tools', 'UI', 'dark_sprites.json'), 'w'), indent=1)
    print('made', len(made))
