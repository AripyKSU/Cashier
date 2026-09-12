"""Render the queue API comparison; reads frozen results, never changes game data."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
data = json.loads((HERE / 'results.json').read_text(encoding='utf-8'))
rows = data['scenarios']
assert len(rows) == 36 and all(r['runs'] == 500 for r in rows)
for r in rows:
    assert sum(r['baselineTypes'].values()) == sum(r['queueTypes'].values()) == r['runs'] * r['targetTrades']
    assert r['pairedDifference95Low'] <= r['pairedRevenueDifference'] <= r['pairedDifference95High']
find = lambda day, rep, n, timing='jitter': next(r for r in rows if (r['day'], r['reputation'], r['targetTrades'], r['timing']) == (day, rep, n, timing))
fonts = {s: ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', s) for s in [18, 21, 26, 34]}
im = Image.new('RGB', (1500, 850), 'white')
d = ImageDraw.Draw(im)
ink, muted, grid = '#193348', '#536778', '#dce4eb'
d.text((50, 25), '대기 이탈: 정가 판매의 명성 상승과 매출이 어떻게 달라지나', font=fonts[34], fill=ink)
d.text((50, 86), '180초 · 조건별 500일 표본 · 같은 상품/손님 추첨을 짝지어 비교 · 시작 명성 0', font=fonts[21], fill=muted)
d.text((65, 145), '일일 명성 변화량 평균 | 1일차 기본 상품', font=fonts[26], fill=ink)
d.text((790, 145), '매출 변화율 | 변동 간격 대기열', font=fonts[26], fill=ink)

def axes(left, width, low, high, ticks, suffix=''):
    x = lambda n: left + (n - 8) / 4 * width
    y = lambda v: 660 - (v - low) / (high - low) * 430
    for v in ticks:
        d.line((left, y(v), left + width, y(v)), fill='#8295a6' if v == 0 else grid, width=2 if v == 0 else 1)
        d.text((left - 12, y(v)), f'{v:g}{suffix}', font=fonts[18], fill=muted, anchor='rm')
    for n in [8, 10, 12]:
        d.text((x(n), 682), f'{n}건/일', font=fonts[21], fill=ink, anchor='mt')
    return x, y

x, y = axes(105, 540, 0, 7, range(8))
for j, (field, timing, color, label) in enumerate([('baselineReputationDelta', 'fixed', '#8b99a8', '대기 없음'), ('queueReputationDelta', 'fixed', '#247faf', '고정 간격 큐'), ('queueReputationDelta', 'jitter', '#25846b', '변동 간격 큐')]):
    pts = [(x(n), y(find(1, 0, n, timing)[field])) for n in [8, 10, 12]]
    d.line(pts, fill=color, width=4)
    for n, (px, py) in zip([8, 10, 12], pts):
        d.ellipse((px - 5, py - 5, px + 5, py + 5), fill=color)
        d.text((px, py - 12), f"{find(1, 0, n, timing)[field]:.2f}", fill=color, font=fonts[21], anchor='mb')
    d.text((85 + j * 195, 195), label, fill=color, font=fonts[21])

relevant = [r for r in rows if r['reputation'] == 0 and r['timing'] == 'jitter']
low = min(-2, int(min(r['pairedDifference95Low']/r['baselineRevenue']*100 for r in relevant)//2*2)-2)
high = max(2, int(max(r['pairedDifference95High']/r['baselineRevenue']*100 for r in relevant)//2*2)+4)
x, y = axes(850, 510, low, high, range(low, high + 1, 2), '%')
for j, (day, color) in enumerate([(1, '#247faf'), (13, '#a87523'), (23, '#a45080')]):
    points = []
    d.text((805 + j * 190, 195), f'{day}일차 설비 예시', fill=color, font=fonts[18])
    for n in [8, 10, 12]:
        r = find(day, 0, n)
        px = x(n) + (j - 1) * 8
        change = r['pairedRevenueDifference'] / r['baselineRevenue'] * 100
        lo, hi = [r[k] / r['baselineRevenue'] * 100 for k in ['pairedDifference95Low', 'pairedDifference95High']]
        d.line((px, y(lo), px, y(hi)), fill=color, width=2)
        for v in [lo, hi]: d.line((px-5, y(v), px+5, y(v)), fill=color, width=2)
        points.append((px, y(change)))
        d.ellipse((px-5, y(change)-5, px+5, y(change)+5), fill=color)
    d.line(points, fill=color, width=3)
d.text((50, 752), '세로선: 짝지은 매출 차이 평균의 근사 95% 구간 / 점 사이 선은 비교용이며 연속 난도 곡선이 아닙니다.', fill=muted, font=fonts[21])
d.text((50, 790), '가격 이벤트·벌금·설비 투자·31일 누적 성장은 제외. 실제 이용자의 처리 속도 측정 결과가 아닙니다.', fill=muted, font=fonts[21])
im.save(HERE / 'comparison.png')

table = ['| 일차/설비 예시 | 시작 명성 | 거래 수 | 간격 | 대기 없음 매출 | 대기열 매출 | 매출 차이 | 명성 변화: 없음 → 큐 |', '|---|---:|---:|---|---:|---:|---:|---:|']
for r in sorted(rows, key=lambda r: (r['day'], r['reputation'], r['targetTrades'], r['timing'])):
    percent = 100 * r['pairedRevenueDifference'] / r['baselineRevenue']
    table.append(f"| {r['day']}일/상품설비 {r['stageExample']}단계까지 | {r['reputation']} | {r['targetTrades']} | {r['timing']} | {r['baselineRevenue']:,.0f}G | {r['queueRevenue']:,.0f}G | {percent:+.2f}% | {r['baselineReputationDelta']:.2f} → {r['queueReputationDelta']:.2f} |")
(HERE / 'tables.md').write_text('# 대기 이탈 비교 원시 결과 요약\n\n모든 값은 조건별500일 평균. 0단계 표기는 상품설비 미보유라는 분석 표기이며 실제 가게 초기 단계는1이다.\n\n' + '\n'.join(table) + '\n', encoding='utf-8')
print('36 scenarios and type-count totals verified; comparison.png and tables.md generated.')
