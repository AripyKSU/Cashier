"""Build review tables and static plots from the existing Monte Carlo output."""
import csv
import json
import math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
data = json.loads((HERE / 'estimates.json').read_text(encoding='utf-8'))
rows = data['scenarios']
lookup = {(r['demand'], r['phase'], r['mask']): r for r in rows}
phases = ['1~9일', '10~19일', '20~31일']
names = ['식량', '약품', '공구', '전력·통신', '핵보호', '정밀장비']
masks = [0, 1, 3, 7, 15, 31, 63]
stage_names = ['미구매', '+식량', '+약품', '+공구', '+전력·통신', '+핵보호', '+정밀장비']
money = lambda n: f'{n:,.0f}'
round100 = lambda x: math.floor(x / 100 + .5) * 100
combo = lambda mask: '+'.join(n for i, n in enumerate(names) if mask & (1 << i)) or '미구매'


def save_csv(name, records):
    with (HERE / name).open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.DictWriter(stream, fieldnames=list(records[0]))
        writer.writeheader()
        writer.writerows(records)


def target_limits(group):
    # 75~85% is an explicit review tolerance, not a user-confirmed rule.
    lower = math.ceil(6 * group[-1]['mean'] / .85 / 100) * 100
    upper = math.floor(6 * group[0]['mean'] / .75 / 100) * 100
    return lower, upper


bands = []
for demand in ['draft', 'flat']:
    for phase in range(3):
        selected = sorted((r for r in rows if r['demand'] == demand and r['phase'] == phase), key=lambda r: r['mean'])
        groups = []
        for row in selected:
            candidate = (groups[-1] if groups else []) + [row]
            lower, upper = target_limits(candidate)
            if not groups or lower > upper:
                groups.append([row])
            else:
                groups[-1].append(row)
        for number, group in enumerate(groups):
            lower, upper = target_limits(group)
            target = max(lower, min(upper, round100(7.5 * (group[0]['mean'] + group[-1]['mean']) / 2)))
            for r in group:
                r.update(band=number, target=target, achievement6=r['expected6'] / target)
                assert .75 <= r['achievement6'] <= .85
            bands.append(dict(demand=demand, phase=phase, band=number, count=len(group), min_mean=group[0]['mean'], max_mean=group[-1]['mean'], representative=target/7.5, target=target, min_achievement6=group[0]['achievement6'], max_achievement6=group[-1]['achievement6'], masks='_'.join(str(r['mask']) for r in group)))

reference = []
for phase in range(3):
    for stage, mask in enumerate(masks):
        r = lookup['draft', phase, mask]
        target = round100(r['exactTarget'])
        reference.append(dict(phase=phase, stage=stage, mask=mask, facilities=combo(mask), mean=r['mean'], expected6=r['expected6'], target=target, expected8=r['expected8'], achievement6=r['expected6']/target))
    seq = [r['target'] for r in reference if r['phase'] == phase]
    assert all(a < b for a, b in zip(seq, seq[1:]))

edges = []
for r in rows:
    for f in range(6):
        if r['mask'] & (1 << f):
            continue
        after = lookup[r['demand'], r['phase'], r['mask'] | (1 << f)]
        change = after['mean'] / r['mean'] - 1
        edges.append(dict(demand=r['demand'], phase=r['phase'], before_mask=r['mask'], after_mask=after['mask'], added_facility=12001+f, mean_before=r['mean'], mean_after=after['mean'], change_rate=change, target_before=r['target'], target_after=after['target']))

save_csv('reference-targets.csv', reference)
save_csv('target-bands.csv', bands)
save_csv('combination-targets.csv', [dict(demand=r['demand'], phase=r['phase'], mask=r['mask'], facility_ids='_'.join(str(12001+i) for i in range(6) if r['mask'] & 1<<i), facilities=combo(r['mask']), mean=r['mean'], mean_se=r['se'], expected6=r['expected6'], exact_target=r['exactTarget'], band=r['band'], rounded_target=r['target'], achievement6=r['achievement6'], expected8=r['expected8']) for r in rows])
save_csv('upgrade-comparisons.csv', edges)
assert len(rows) == 384 and len(edges) == 1152
assert len({(r['demand'],r['phase'],r['mask']) for r in rows}) == 384
assert all(r['mean'] > 0 and r['se'] > 0 for r in rows)
summary = dict(reference=reference, bands=bands, max_relative_se=max(r['se']/r['mean'] for r in rows), declines_over_one_percent={d:[sum(e['change_rate'] < -.01 for e in edges if e['demand']==d and e['phase']==p) for p in range(3)] for d in ['draft','flat']})
(HERE / 'tables.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')

# Readable review figures; no game screenshots or throughput claims.
colors = ['#2563eb', '#b56a09', '#168477']
def canvas(title, subtitle, height=820):
    im = Image.new('RGB',(1500,height),'#f6f8fb')
    d = ImageDraw.Draw(im)
    def label(x,y,s,size=23,color='#26364a'):
        d.text((x,y),s,font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',size),fill=color)
    label(45,28,title,33)
    label(45,83,subtitle,21,'#536579')
    return im,d,label

im,d,label = canvas('설비 투자 기준 경로의 하루 목표 수입','수요 가중치 초안 · 기본가 정가 판매 · 각 날짜 구간을 고정해 비교')
x0,x1,y0,y1=135,1390,160,600
X=lambda n:x0+(x1-x0)*n/6
Y=lambda amount:y1-(y1-y0)*amount/80000
for amount in range(0,80001,10000):
    y=Y(amount)
    d.line((x0,y,x1,y),fill='#dbe2eb',width=1)
    label(40,y-13,f'{amount/10000:g}만',20)
for p in range(3):
    values=[r['target'] for r in reference if r['phase']==p]
    points=[(X(0),Y(values[0]))]
    for i in range(1,7):
        points.extend([(X(i),Y(values[i-1])),(X(i),Y(values[i]))])
    d.line(points,fill=colors[p],width=4)
    for i,v in enumerate(values):
        d.ellipse((X(i)-4,Y(v)-4,X(i)+4,Y(v)+4),fill=colors[p])
    label(360+p*300,124,phases[p],22,colors[p])
for i,s in enumerate(stage_names):
    label(X(i)-55,615,s,21)
    label(X(i)-40,650,f'구간 {i}',18,'#536579')
label(45,710,'순서는 비교용입니다. 식량 → 약품 → 공구 → 전력·통신 → 핵보호 → 정밀장비를 누적 보유합니다.',21)
label(45,750,'고가 직행·혼합 조합에는 별도 표가 필요합니다. 이 7구간을 모든 구매 경로에 공통 적용하지 않습니다.',21,'#9d3b2d')
im.save(HERE / 'steps.png')

im,d,label=canvas('고가 설비 직행과 전체 보유의 기대 수입 비교','20~31일 · 성공 판매 6건 기준 · 가격사건·처리시간·거절 제외')
chosen=[0,15,16,32,48,63]
labels=['미구매','저가 4종','핵보호만','정밀장비만','핵+정밀','전체 6종']
Y=lambda amount:610-420*amount/90000
for amount in range(0,90001,15000):
    y=Y(amount)
    d.line((130,y,1420,y),fill='#dbe2eb')
    label(35,y-14,f'{amount/10000:g}만',21)
for i,mask in enumerate(chosen):
    x=190+i*215
    for j,dem in enumerate(['flat','draft']):
        val=lookup[dem,2,mask]['expected6']
        col='#9ba9ba' if j==0 else '#2563eb'
        d.rectangle((x+j*62,Y(val),x+j*62+52,610),fill=col)
        if j==1:
            label(x-2,Y(val)-35,money(val),20)
    label(x-7,625,labels[i],21)
label(420,130,'灰: 날짜 선호 가중치 없음'.replace('灰','회색'),21,'#66758a')
label(850,130,'파랑: 수요 가중치 초안',21,'#2563eb')
label(45,700,'고가 상품만으로 진열대를 채우는 조합의 수입이 더 높습니다. 전체 해금이 최고 기대 수입을 보장하지 않습니다.',21)
label(45,742,'따라서 모든 추가 구매에서 목표 상향 + 모든 조합에서 6건 ≈ 80%를 동시에 맞출 수 없습니다.',22,'#9d3b2d')
im.save(HERE / 'routes.png')

lines=['# 설비 조합별 하루 목표 수입 계산 결과','',
       '2026-09-12. 단위는 게임 화폐. 목표는 유지비 차감 전 판매수입이며 검토용 수치다. 코드·런타임 CSV에는 반영하지 않았다.','',
       '## 결과와 제약','',
       '**식량부터 시작하는 기준 구매 경로는 7개 계단으로 제시할 수 있다. 다만 모든 구매 경로에 공통 적용하는 단조 상승 목표표는 현재 분포와 충돌한다.** 고가 설비만 보유하면 제한된 진열 공간에 고가 상품이 집중되어 전체 보유보다 기대 수입이 높다. 이를 가격·확률 변경이나 목표액 보정으로 숨기지 않았다.','',
       '## 기준 경로의 목표액','',
       '각 칸은 해당 날짜 구간을 고정한 기대 거래액 ×7.5를 100 단위로 반올림했다. 행은 필수 구매 순서나 실제 구매일이 아니다. 기존 경제 초안과 비교하기 위한 순서이며 엄밀한 가격 오름차순도 아니다(전력42,000, 공구46,000). 날짜 선호 가중치를 적용한 **기획 초안**이며 현재 게임 측정값이 아니다.','',
       '| 비교 구간 | 누적 추가 설비 | 1~9일 목표 | 10~19일 목표 | 20~31일 목표 |','|---:|---|---:|---:|---:|']
for i,mask in enumerate(masks):
    vals=[next(r['target'] for r in reference if r['phase']==p and r['mask']==mask) for p in range(3)]
    lines.append(f"| {i} | {stage_names[i]} | {' | '.join(money(v) for v in vals)} |")
lines += ['', '![기준 경로 계단 그래프](steps.png)','',
          '같은 설비라도 날짜별 진열 수와 수요가 바뀌면 평균 거래액이 내려갈 수 있다. 목표를 날짜가 지날 때도 무조건 올리는 규칙은 이번 표에 넣지 않았다.','',
          '### 20~31일의 6건·8건 기대 수입','',
          '| 비교 구간 | 평균 거래액 | 6건 수입 | 하루 목표 | 8건 수입 | 6건 달성률 |','|---:|---:|---:|---:|---:|---:|']
for r in reference:
    if r['phase']==2:
        lines.append(f"| {r['stage']} | {money(r['mean'])} | {money(r['expected6'])} | {money(r['target'])} | {money(r['expected8'])} | {r['achievement6']:.1%} |")
lines += ['', '## 고가 직행과 전체 보유','', '| 조합 | 평균 거래액 | 6건 기대 수입 | 조합별 목표 원계산 |','|---|---:|---:|---:|']
for mask in [16,32,48,63]:
    r=lookup['draft',2,mask]
    lines.append(f"| {combo(mask)} | {money(r['mean'])} | {money(r['expected6'])} | {money(r['exactTarget'])} |")
ratio=lookup['draft',2,63]['mean']/lookup['draft',2,48]['mean']
lines += ['', f"핵보호+정밀장비에서 전체 보유로 바꾸면 이 모델의 평균 거래액은 **{1-ratio:.1%} 감소**한다. 핵+정밀의 목표를 그대로 유지하면 전체 보유의 6건 기대 달성률은 **{.8*ratio:.1%}**다. 목표를 더 높이면 달성률은 이보다 낮아진다. 따라서 75~85% 허용 범위로 넓혀도 이 역전은 해소되지 않는다.", '',
          '![경로별 기대 수입 비교](routes.png)', '',
          '## 전체 조합의 유사 수입 구간','',
          '64개 조합을 날짜·수요 조건별로 평균 거래액 순서로 정렬했다. 6건의 **기대 달성률 75~85%**를 유지할 수 있는 조합을 같은 구간으로 묶었다. 이 ±5%p는 “약80%”를 수치화한 검토용 제안이며 확정 기획이 아니다. 대표 B는 선택 목표÷7.5다.', '',
          '묶음은 낮은 평균부터 확장하되 100 단위 목표 중 6×최대평균÷0.85 이상, 6×최소평균÷0.75 이하인 값이 남아 있을 때만 유지한다. 목표는 양 끝 평균의 중간값×7.5를 반올림하고 허용 범위 안으로 제한한다. 조합 빈도나 실제 구매 경로 확률을 임의로 가정하지 않는다.','',
          '| 수요 조건 | 1~9일 구간 수 | 10~19일 구간 수 | 20~31일 구간 수 |','|---|---:|---:|---:|']
for dem,label_ in [('draft','수요 가중치 초안'),('flat','날짜 선호 가중치 없음')]:
    lines.append('| '+label_+' | '+' | '.join(str(sum(b['demand']==dem and b['phase']==p for b in bands)) for p in range(3))+' |')
lines += ['', '이 구간 번호는 **각 날짜·수요 조건 안에서의 기대 매출 순위**다. 구매 횟수나 영구 해금 레벨이 아니며, 추가 구매 후 낮아지는 경우도 별도 비교표에 기록했다. 기준 경로의 0~6 번호와 혼용하지 않는다. 아래는 후반 수요 초안의 전체 구간이다.','',
          '| 수입 구간 | 조합 수 | 거래액 범위 | 목표액 | 6건 기대 달성률 범위 |','|---:|---:|---:|---:|---:|']
for b in bands:
    if b['demand']=='draft' and b['phase']==2:
        lines.append(f"| {b['band']} | {b['count']} | {money(b['min_mean'])}~{money(b['max_mean'])} | {money(b['target'])} | {b['min_achievement6']:.1%}~{b['max_achievement6']:.1%} |")
lines += ['', '## 입력·가정·검증','',
          '- 기존 `draft-20260912/model.js`의 표본 추출을 재사용했다. 실제 21상품의 ID(기존 임시 키2개는1024/1025에 대응)·가격·타입·설비FK·판매일과 15성향의 구매 필드, 명성 분포가 모델 입력과 같은지 실행 시 검사했다. 상품 해금 설비6종의 종류와 요구 가게 단계1/1/2/2/3/3도 검사한다. 원본 경로와 SHA-256은 estimates.json에 남겼다.',
          '- 중립 명성의 성향 비중은 보통60%·급함15%·가격민감5%·부유10%·가난10%. 주문 1~3종, 종류별1~3개, 선호 상품 선택90%, 중복 상품 없음. 모든 상품을 기본가에 판매하고 정가 주문은 모두 수락하는 조건이다.',
          '- 날짜별 진열 후보4/6/8종, 설비 상품 가중치는 기본100, 식량·약품220, 공구·전력280, 핵보호·정밀350이다. 기본 상품만 있으면 가능한5종까지 사용한다.',
          '- 수요 초안은 저가/중가/고가 선호 가중치가 구간별 (1.6,1,0.45), (1,1.6,1), (1,1.2,1.8)이다. 다른 비교값은 같은 성향군 내부 행을 균등 추출한다. 날짜 선호 가중치는 현재 런타임에 미연결이다.',
          '- 6종 상품 해금 설비의 모든64조합 × 날짜3구간 × 수요2조건 =384조건. 각12,000일의 가상 일일 진열·8건 주문을 추출했다. 합계36,864,000건이며 영업시간이나 사람의 처리 속도를 시뮬레이션한 것은 아니다.',
          f"- 일별8건 평균을 독립 표본 단위로 표준오차를 계산했다. 최대 상대 표준오차 {summary['max_relative_se']:.2%}, 정규 근사 95% 오차한계는 최대 약 ±{1.96*summary['max_relative_se']:.2%}다. 이는 평균 추정의 Monte Carlo 오차이며 하루 매출 변동폭이나 모델 정확도 보장이 아니다. 모든 조건에 대한 동시95% 보장도 아니다.",
          '- 가격사건·할인·거절·이탈·명성 변화·편의설비 시간 단축·구매비 회수는 이 계산에서 제외했다. 원가는 판매수입에서 차감하지 않았다. 180초·평균8건은 실측되지 않았고 현재 기본 영업시간30초와 차이가 남아 있다.',
          '- 구매 자금과 가게 단계 해금 비용을 시뮬레이션하지 않았다. 현재 구현의 요구 가게 단계1/2/3 조건을 충족하고 해당 설비가 활성화됐을 때의 조건부 수입이다. 따라서 초반 고가 직행 행은 실제 그날 구매 가능성을 증명하지 않는다.',
          '- 표본 조건384개 누락·중복 없음, 구매 추가 비교1,152개, 양수 평균·표준오차, 모든 묶음의75~85% 산술 범위, 기준 경로의 목표 증가를 자동 검사했다. Unity 테스트·플레이 실측은 수행하지 않았다.', '',
          '## 데이터 파일과 다음 판단','',
          '- [기준 경로 목표액](reference-targets.csv): 21행. phase0/1/2는1~9/10~19/20~31일, stage는 비교 경로 번호다.',
          '- [전체 조합별 입력용 계산표](combination-targets.csv): 384행. demand=draft는 수요 초안, flat은 날짜 선호 가중치 없음. mask는12001부터12006까지 순서대로1/2/4/8/16/32를 합한 분석용 키다.',
          '- [수입 구간별 목표액](target-bands.csv): 조합 묶음과 목표. 구간 번호는 demand와phase 안에서만 유효하다.',
          '- [추가 설비의 수입 변화](upgrade-comparisons.csv): 모든 단일 추가 구매1,152개. change_rate<0이면 기대 수입 감소다.',
          '- 위 CSV들은 doc 아래의 분석 자료다. 런타임 테이블 등록·새 ID 발급·목표 UI 연결은 하지 않았다.',
          '- 재현: 이 폴더의 `calculate.js`를 Node로 실행하고, 이어서 `present.py`를 Pillow가 설치된 Python으로 실행한다. 첫 단계가 원시 추정값, 둘째가 CSV·표·그림을 갱신한다. 기존 원본 모델과 입력 snapshot이 필요하다.', '',
          '**권고:** 현재는 7계단 기준 경로와 전체 조합별 수입표를 검토 자료로 사용한다. 모든 경로에서 올라가는 목표표를 게임에 넣기 전에, 해금 상품이 늘 때 고가 상품의 진열·선택 확률이 희석되는 문제를 어떻게 다룰지 정해야 한다. 진열 상품 선택 기능 또는 상품 선택 규칙 변경은 별도 설계 대상이며 이번에 구현하지 않았다.','']
(HERE / 'results.md').write_text('\n'.join(lines),encoding='utf-8')
print(json.dumps(dict(reference_targets=[[r['target'] for r in reference if r['phase']==p] for p in range(3)], bands=len(bands), declines=summary['declines_over_one_percent']),ensure_ascii=False))
