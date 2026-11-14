# 사용법: python Tools/cut_card_art.py Assets/disign Assets/PokerTower/Resources/PokerTowerCards <미리보기.png>
#   Assets/disign의 "ChatGPT*.png" 7장을 파일명 순서대로 fire,sun,earth,wood,metal,water,moon 으로 저장합니다 (새 그림이면 names 순서 확인).
# 새 카드 배경(랭크 없는 7종)의 바깥 배경을 투명하게 만든다. 원본 해상도·비율(1086x1448, 3:4) 유지, 사각형 크롭 없음.
#  1) 가장자리에서 배경색 flood fill  2) 금색 테두리로 프레임 외곽(둥근 사각형) 측정 → 바깥 그림자 제거
#  3) 프레임 바깥은 위·아래 중앙 금색 장식 돌기만 남김
import sys, glob
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
src, out_dir, preview = sys.argv[1], sys.argv[2], sys.argv[3]
names = ['fire','sun','earth','wood','metal','water','moon']   # 파일 번호 1~7 순서 (그림으로 확인)
files = sorted(glob.glob(src + '/ChatGPT*.png'))
assert len(files) == 7, files
tiles, report = [], []
for p, n in zip(files, names):
    im = Image.open(p).convert('RGB'); w, h = im.size
    marker = im.copy()
    for s in [(0,0),(w-1,0),(0,h-1),(w-1,h-1),(w//2,0),(w//2,h-1),(0,h//2),(w-1,h//2)]:
        ImageDraw.floodfill(marker, s, (255, 0, 255), thresh=28)
    m = np.array(marker)
    flood = ~((m[:,:,0]==255)&(m[:,:,1]==0)&(m[:,:,2]==255))
    hsv = np.array(im.convert('HSV')).astype(float)
    gold = hsv[:,:,1] > 70
    rows = gold[:, 300:w-300].mean(axis=1); cols = gold[300:h-300, :].mean(axis=0)
    ys = np.where(rows > 0.5)[0]; xs = np.where(cols > 0.5)[0]
    x0, x1, y0, y1 = int(xs[0]), int(xs[-1]), int(ys[0]), int(ys[-1])
    rr = Image.new('L', (w, h), 0); ImageDraw.Draw(rr).rounded_rectangle((x0 - 5, y0 - 5, x1 + 5, y1 + 5), radius=44, fill=255)  # 바깥 흰 테두리 보존용 여유 5px
    inside = np.array(rr) > 0
    band = np.zeros((h, w), bool); band[:, w//2-90:w//2+90] = True
    orn = Image.fromarray(((gold & flood & band & ~inside) * 255).astype('uint8'))
    orn = orn.filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.MinFilter(9))
    keep = (flood & inside) | ((np.array(orn) > 0) & flood)
    mask = Image.fromarray((keep * 255).astype('uint8')).filter(ImageFilter.GaussianBlur(1.0))
    out = im.convert('RGBA'); out.putalpha(mask); out.save(f'{out_dir}/{n}.png', optimize=True)
    report.append(f'{n}: frame x{x0}-{x1} y{y0}-{y1}')
    t = out.resize((260, 347), Image.LANCZOS); bg = Image.new('RGBA', t.size, (20,20,30,255)); bg.alpha_composite(t); tiles.append(bg)
sheet = Image.new('RGB', (260*7, 347)); [sheet.paste(t.convert('RGB'), (i*260, 0)) for i, t in enumerate(tiles)]
sheet.save(preview); print('\n'.join(report))
