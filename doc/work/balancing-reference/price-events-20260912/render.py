"""Standalone figures and review text for the existing-schema price-event draft."""
import json, math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
D = json.loads((HERE / 'results.json').read_text(encoding='utf-8'))
COLORS = ['#64748b', '#b45309', '#2563eb', '#059669']
NAMES = ['기본', '식량 선반', '약품 보관장', '공구대', '전력·통신', '핵보호', '정밀장비']

def text(dr, xy, value, size=18, color='#172b45', anchor=None):
    dr.text(xy, str(value), font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',size), fill=color, anchor=anchor)

def plot(dr, box, curves, xmin, xmax, title, ylabel):
    l,t,r,b=box
    values=[v for line in curves for v in line]
    lo=min(0,math.floor(min(values)/10000)*10)
    hi=max(10,math.ceil(max(values)/10000)*10)
    X=lambda i:l+(i-xmin)/(xmax-xmin)*(r-l)
    Y=lambda v:b-(v/1000-lo)/(hi-lo)*(b-t)
    text(dr,(l,t-60),title,22)
    text(dr,(l,t-30),ylabel,16,'#52657b')
    for i in range(5):
        v=lo+(hi-lo)*i/4; y=Y(v*1000)
        dr.line((l,y,r,y),fill='#e2e8f0',width=1)
        text(dr,(l-8,y),f'{v:,.0f}',14,anchor='rm')
    dr.line((l,Y(0),r,Y(0)),fill='#8996a7',width=2)
    for x in ([1,10,20,31] if xmax==31 else [0,3,7]):
        text(dr,(X(x),b+12),x,15,anchor='mt')
    for line,c in zip(curves,COLORS):
        dr.line([(X(xmin+i),Y(v)) for i,v in enumerate(line)],fill=c,width=4)

im=Image.new('RGB',(1500,790),'white');dr=ImageDraw.Draw(im)
text(dr,(50,25),'가격 이벤트와 31일 자금 곡선',32)
text(dr,(50,78),'동일 난수 1,000회 잔액 중앙값 · 날짜 선호/자유 구매는 v0.1 가정 · 거래 8건 고정',19)
for i,s in enumerate(D['strategies']):
    plot(dr,(85+i*495,200,475+i*495,615),[[d['p50'] for d in v['days']] for v in s['variants']],1,31,s['name'],'잔액 · 천 통화')
for i,v in enumerate(D['variants']):
    x=65+(i%2)*730;y=700+(i//2)*40
    dr.line((x,y,x+35,y),fill=COLORS[i],width=5);text(dr,(x+45,y),v['name'],19,anchor='lm')
im.save(HERE/'cashflow.png')

im=Image.new('RGB',(1500,1080),'white');dr=ImageDraw.Draw(im)
text(dr,(50,25),'설비별 구매 후 7영업일 투자금 회수 비교',32)
text(dr,(50,78),'곡선: 평균 추가매출 − 설비비 · 0선: 회수 · 다른 설비 보유 고정 · 구매 시점은 대표 상황',19)
for i,r in enumerate(D['recoveries']):
    col,row=i%3,i//3
    plot(dr,(85+col*495,205+row*395,475+col*495,465+row*395),[[v['mean'] for v in case['days']] for case in r['variants']],0,7,f"{NAMES[r['f']]} · {r['day']}일 구매",'누적 순증분 · 천 통화')
for i,v in enumerate(D['variants']):
    x=65+(i%2)*730;y=985+(i//2)*40
    dr.line((x,y,x+35,y),fill=COLORS[i],width=5);text(dr,(x+45,y),v['name'],19,anchor='lm')
im.save(HERE/'payback.png')

def table(headers, rows):
    return '| '+' | '.join(headers)+' |\n|'+'|'.join(['---']*len(headers))+'|\n'+'\n'.join('| '+' | '.join(map(str,r))+' |' for r in rows)+'\n'

strategy_rows=[]
for s in D['strategies']:
    for v in s['variants']:
        f=v['final'];delta=v['pairedDelta']
        strategy_rows.append([s['name'],v['name'],f"{f['p50']:,.0f}",f"{f['p10']:,.0f}–{f['p90']:,.0f}",f"{delta['mean']:+,.0f}"])
roi_rows=[]
for r in D['recoveries']:
    roi_rows.append([NAMES[r['f']],f"{r['cost']:,}"]+[f"{v['withinSeven']:.1%}" for v in r['variants']])
report='''# 가격 이벤트 데이터 초안 v0.1

2026-09-12. 사용자가 승인한 순차 데이터 작업의 첫 항목이다. 기존 사건4개·일정5개·문구8개의 ID와 스키마를 유지한다. 아래 빈도는 기획 원문의 확정값이 아니라 이번 검증용 제안이다.

## 적용안

| 사건 | 효과 | 표시 영업일 / 후보 가중치 |
|---|---|---|
| 식료품 공급 확대9001 | 식료품2 기본가 −10% | 신문3·10·17·24·31일 |
| 지역 소식9003 | 가격 영향 없음 | 신문6·13·20·27일 |
| 생수 운송 지연9002 | 물1001 기본가 +15 | 라디오1~31일, 가중치1 |
| 의약품 수요 증가9004 | 의약품3 기본가 +15% | 라디오1~31일, 가중치1 |
| 지역 소식9003 | 가격 영향 없음 | 라디오1~31일, 가중치4 |

신문은9일 보도되고22일은 없다. 라디오는1~31일 매일 예약되며 가격 영향 있는 뉴스가 선택될 확률은1/3이다. 나머지2/3도 무영향 뉴스가 예약되는 것이며 방송 부재가 아니다. 실제 방송 여부는 영업시간에 달려 있다. 일자32일부터는 이 초안의 후보가 없다.

CSV 날짜는 경과일0부터 시작한다. end_day는 포함이다. 신문 repeat_days=7은7일간 지속이 아니라7일마다 당일 적용이다. 매일 BasePrice에서 초기화되므로 효과가 영구 누적되지 않는다. 라디오 효과는 방송 후 제출한 거래부터 적용된다. 방송 전 거래와 완료된 거래는 소급 변경되지 않는다.

현재 구현은 라디오를0~60초에 방송한다. 180초 영업에서는 예약 방송이 모두 도달하고, 30초 영업에서는 절반가량이 종료 전에 도달하지 않는다. 후자의 무방송을 무영향 뉴스 확률과 혼동하지 않는다. 방송 전/후 거래량에 따른 영향 차이는 아래30초 민감도와 함께 본다.

## 상품 현재가 예시

| 상품 | 기본가 | 해당 사건 적용 후 |
|---|---:|---:|
| 물 | 100 | 115 |
| 통조림 | 250 | 225 |
| 분말 수프 | 350 | 315 |
| 영양바 | 450 | 405 |
| 붕대 | 300 | 345 |
| 연고 | 500 | 575 |
| 해열제 | 700 | 805 |
| 응급 주사 | 900 | 1,035 |

대상 범위를 추가하지 않았으므로 공구·전력통신·핵보호·정밀장비 상품에는 직접 가격 효과가 없다. 그러나 저가 상품을 대체하는 정도와 구매 시점 변화 때문에 해당 설비의 순증분에도 간접 영향은 생긴다. 원가 차감은 계산에 넣지 않는다.

## 선택 근거\n\n빈도만 줄인 첫 비교에서는 식량 설비의7일 내 회수가45.3%→35.5%, 약품은53.3%→62.5%였다. 효과를 절반으로 낮춘 적용안은40.4%·57.3%로 설비 간 편향이 줄었다. 같은 모델/seed의 frequency-only-results.json에 첫 비교 결과를 남겼다. 기존 사건을 폐기하거나 설비비를 다시 맞추기보다 작은 변동을 먼저 플레이 검증한다.\n\n## 비교 결과

![31일 자금 곡선](cashflow.png)

''' + table(['경로','조건','31일 잔액 중앙값','P10–P90','무이벤트 대비 짝지은 차이 평균'],strategy_rows) + '''
각 조건의 구매 시점은 자금에 따라 다시 결정한다. 동일 조건의 손님·상품 난수를 공유해 비교하지만 설비 구매 후 카탈로그는 달라질 수 있다. 중앙값끼리 뺀 값을 평균 효과로 취급하지 않는다. 직행 경로는 v0.1의 무구매 상태와 중첩되어 표에서 생략했다.

![설비 회수 곡선](payback.png)

''' + table(['설비','비용','무이벤트 180초','기존180초','초안180초','초안30초'],roi_rows) + '''
위 표는 대표 구매 후7영업일 안에 처음 투자금을 회수한 비율이다. 7일째에도 계속 회수 상태일 확률은 아니다. 설비를 산 경우와 사지 않은 경우의 추가매출 차이로 계산하며, 다른 설비 보유는 고정한다. 곡선은 그 순증분의 평균이다. 1,000회 표본의 확률 차이는 약1~2%p 표본 변동이 있어 작은 차이만으로 설비비를 다시 맞추지 않는다.

## 재현과 한계

- inputs.json은 반영 전 일정과 적용 제안 일정·사건·상품·설비비를 보존한다. compare.js는 기존 draft-20260912/model.js의 수요 샘플러를 재사용한다. node compare.js 이후 번들 Python으로 render.py를 실행한다.
- 1,000회, 고정 seed20260912. 손님·카탈로그 난수와 이벤트 난수를 분리하고 조건 간 공유한다. C#과 분포 계약을 맞춘 분석이며 같은 seed의 실제 플레이 재현은 아니다.
- 거래8건이 영업시간에 균등하게 분산되어 각 구간 중간에 제출된다고 가정한다. 방송 시점과 각 제출 시점을 비교해 현재가를 적용한다. 정확한 실제 처리 시점은 아직 측정값이 없다.
- 날짜별 선호·자유 구매·처리8건·유지비·설비가격은 기존 v0.1 비교 조건이다. 30초 비교도 처리8건을 고정하므로 현재 게임의 실제 매출 예측이 아니다.
- 할인/거절/이탈·명성 변화·벌금·치료비·편의설비·원가 차감 제외. 구매는 정산 때 하루1개, 다음날 유지비 유보,24일까지다. 실제 게임의 구매 제한을 뜻하지 않는다.
- 180초 영업·날짜별 선호·자유 구매를 이번 데이터 작업으로 구현하지 않는다. 현재 게임 UI에서 뉴스 표시 및 라디오 체감 검수는 별도다.
- 근거: doc/PRICE_EVENT_INTEGRATION.md, 실제 PriceEventScheduler/PriceEventScheduleData, 기획 스냅샷 t.wymf2xvsgena 및 t.798h5ciwg4ih. 사건 수치·일정은 제안이며 기존 사건 효과의 크기를 절반으로 낮추고 빈도를 줄였다.
'''
(HERE/'draft.md').write_text(report,encoding='utf-8')
print('Created draft.md, cashflow.png, payback.png')
