"""Compare six dialogue bands using frozen CSV inputs and integer hundredths of morality."""
import json
from decimal import Decimal
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
INPUT = json.loads((HERE / 'inputs.json').read_text(encoding='utf-8'))
A = INPUT['assumptions']
BOUNDS = A['candidateBoundaries']
OLD = [-20, -10, 0, 10, 20]
LABELS = ['부정 3', '부정 2', '부정 1', '긍정 1', '긍정 2', '긍정 3']
NAMES = ['정가 100%', '할인 혼합', '고가 120%', '11일차 할인 전환']
COLORS = ['#2563eb', '#15803d', '#c2410c', '#7e22ce']
WEIGHTS = ['normal_weight', 'hasty_weight', 'price_sensitive_weight', 'wealthy_weight', 'poor_weight']
RATES = [800, 1000, 1200]


def lookup(kind, rate, child):
    """Read one actual morality band; compare exact rates, including refused trades."""
    rows = []
    for row in INPUT['MoralityData']:
        if int(row['customer_disposition_type']) != kind:
            continue
        low, high = int(row['offer_min_rate']), int(row['offer_max_rate'])
        if (rate > low or rate == low and row['include_min'] == '1') and (
                high == 0 or rate < high or rate == high and row['include_max'] == '1'):
            rows.append(row)
    assert len(rows) == 1, (kind, rate, rows)
    row = rows[0]
    value = Decimal(row['child_elderly_morality_point' if child else 'adult_morality_point']) * 100
    assert value == int(value)
    return int(value), int(row['is_accepted'])


SCORES = np.array([[[lookup(kind, rate, age != 0)[0] for rate in RATES]
                    for age in range(3)] for kind in range(1, 6)])
ACCEPTED = np.array([[[lookup(kind, rate, age != 0)[1] for rate in RATES]
                      for age in range(3)] for kind in range(1, 6)])
assert lookup(5, 1000, False) == (-450, 1)
assert lookup(5, 1200, True) == (-900, 0)
assert lookup(3, 800, False) == (-200, 0)


def crossing(paths, boundary, negative=False):
    """First settlement reaching a band; never equate unreached paths with day 31."""
    hits = paths < boundary if negative else paths >= boundary
    reached = hits.any(axis=1)
    days = hits.argmax(axis=1)[reached]
    return {'reachedPercent': round(float(reached.mean() * 100), 2),
            'medianDayAmongReached': float(np.median(days)) if len(days) else None}


def simulate(balance, trades=8):
    """Fixed reputation/throughput experiment with paired customers and prices across policies."""
    rng = np.random.default_rng(A['seed'])
    shape = (A['runs'], A['days'], trades)
    weights = np.array([int(balance[k]) for k in WEIGHTS])
    assert weights.sum() == 1000
    kinds = np.searchsorted(weights.cumsum(), rng.integers(0, 1000, shape), side='right')
    ages = rng.integers(0, 3, shape)
    mixed = rng.integers(0, 2, shape)  # 80% or 100%, independently of hidden disposition.
    choices = [np.ones(shape, dtype=int), mixed, np.full(shape, 2), mixed.copy()]
    choices[3][:, :10] = 2
    outputs, paths_all = [], []
    for index, rates in enumerate(choices):
        deltas = SCORES[kinds, ages, rates].sum(axis=2)
        paths = np.column_stack([np.zeros(A['runs']), deltas.cumsum(axis=1)]) / 100
        # Independent expectation over CSV weights, all three ages and price policy.
        means = (SCORES.mean(axis=1) * weights[:, None] / 1000).sum(axis=0) / 100 * trades
        daily = [means[1], (means[0] + means[1]) / 2, means[2], None][index]
        theoretical = daily * A['days'] if daily is not None else means[2] * 10 + (means[0] + means[1]) / 2 * 21
        se = paths[:, -1].std(ddof=1) / np.sqrt(A['runs'])
        assert abs(paths[:, -1].mean() - theoretical) < 5 * se + .01
        levels = np.searchsorted(BOUNDS, paths[:, -1], side='right')
        summary = {'policy': NAMES[index], 'expectedPerDay': None if daily is None else round(float(daily), 4),
                   'expectedDay31': round(float(theoretical), 3),
                   'day31P10MedianP90': np.percentile(paths[:, -1], [10, 50, 90]).round(3).tolist(),
                   'dailyP10MedianP90': np.percentile(paths, [10, 50, 90], axis=0).round(3).tolist(),
                   'acceptancePercent': round(float(ACCEPTED[kinds, ages, rates].mean() * 100), 2),
                   'day31BandPercent': [round(float((levels == k).mean() * 100), 2) for k in range(6)],
                   'oldNegative3': crossing(paths, OLD[0], True), 'newNegative3': crossing(paths, BOUNDS[0], True),
                   'oldPositive3': crossing(paths, OLD[-1]), 'newPositive3': crossing(paths, BOUNDS[-1]),
                   'newNegative2': crossing(paths, BOUNDS[1], True), 'newPositive2': crossing(paths, BOUNDS[-2])}
        if index == 3:
            summary['recoveredToZeroAfterSwitch'] = crossing(paths[:, 11:], 0)
            d = summary['recoveredToZeroAfterSwitch']['medianDayAmongReached']
            if d is not None:
                summary['recoveredToZeroAfterSwitch']['medianDayAmongReached'] = d + 11
        outputs.append(summary)
        paths_all.append(paths)
    return outputs, paths_all


def plot_curve(results):
    """Render four policy panels with actual day quantiles and old/new band boundaries."""
    im = Image.new('RGB', (1600, 1130), 'white')
    dr = ImageDraw.Draw(im)
    def text(x, y, value, size=20, color='#172b45', anchor=None):
        dr.text((x, y), str(value), font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', size), fill=color, anchor=anchor)
    text(55, 25, '딸 반응 6단계 · 판매 방식별 31일 도덕성 곡선', 32)
    text(55, 77, '하루 8회 제시 · 명성 0 구성 고정 · 연령 각 1/3 · 5,000회 / 선: 중앙값, 띠: 10~90백분위', 20)
    for i, row in enumerate(results):
        left, top = 85 + (i % 2) * 790, 200 + (i // 2) * 430
        right, bottom = left + 660, top + 285
        x = lambda day: left + day / 31 * (right-left)
        y = lambda value: bottom - (value + 850) / 1250 * (bottom-top)
        text(left, top-58, row['policy'], 25, COLORS[i])
        for value in [-800, -600, -400, -200, 0, 200, 400]:
            dr.line((left, y(value), right, y(value)), fill='#e2e8f0', width=1)
            text(left-10, y(value), value, 16, anchor='rm')
        for bound in BOUNDS:
            dr.line((left, y(bound), right, y(bound)), fill='#91a1b5', width=1)
        for bound in OLD:
            if bound:
                for xx in range(left, right, 12):
                    dr.line((xx, y(bound), xx+6, y(bound)), fill='#c2c9d1')
        p10, median, p90 = row['dailyP10MedianP90']
        overlay = Image.new('RGBA', im.size)
        od = ImageDraw.Draw(overlay)
        from PIL import ImageColor
        od.polygon([(x(d), y(v)) for d,v in enumerate(p10)] +
                   [(x(d), y(p90[d])) for d in reversed(range(32))], fill=ImageColor.getrgb(COLORS[i])+(32,))
        im.paste(overlay, (0, 0), overlay)
        dr.line([(x(d), y(v)) for d,v in enumerate(median)], fill=COLORS[i], width=4)
        for day in [0, 7, 14, 21, 31]:
            text(x(day), bottom+12, day, 17, anchor='mt')
        if i == 3:
            dr.line((x(10), top, x(10), bottom), fill=COLORS[i], width=1)
        text(left, bottom+46, f"31일 중앙값 {median[-1]:,.1f}점 / 10~90% {p10[-1]:,.1f} ~ {p90[-1]:,.1f}", 19)
    text(55, 1060, '회색 실선: 새 경계 -120 / -40 / 0 / 40 / 120 · 점선: 기존 ±10 / ±20 · 가로축: 일차', 19)
    text(55, 1092, '고정 조건 비교이며 실제 플레이 예측·신뢰구간이 아닙니다. 거절도 1회 제시로 집계하며 도덕성을 누적합니다.', 18)
    im.save(HERE / 'curves.png')


if __name__ == '__main__':
    neutral = next(r for r in INPUT['ReputationBalanceData'] if int(r['min_reputation']) <= 0 <= int(r['max_reputation']))
    main, _ = simulate(neutral)
    sensitivity = {f"reputation_{r['idx']}_8": simulate(r)[0] for r in INPUT['ReputationBalanceData'] if r != neutral}
    sensitivity.update({f'trades_{n}': simulate(neutral, n)[0] for n in [4, 12]})
    (HERE / 'results.json').write_text(json.dumps({'assumptions': A, 'main': main, 'sensitivity': sensitivity}, ensure_ascii=False, indent=2), encoding='utf-8')
    plot_curve(main)
    for row in main:
        print(row['policy'], row['expectedPerDay'], row['day31P10MedianP90'], 'old/new negative3', row['oldNegative3'], row['newNegative3'], 'old/new positive3', row['oldPositive3'], row['newPositive3'])
