"""Render the offline draft with bundled Pillow; no network or runtime asset changes."""
import json, math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
D = json.loads((HERE / 'results.json').read_text(encoding='utf-8'))
FONT = 'C:/Windows/Fonts/malgun.ttf'
BOLD = 'C:/Windows/Fonts/malgunbd.ttf'
COLORS = ['#2563eb', '#d97706', '#0f766e', '#9333ea', '#64748b']
INK, MUTED, GRID = '#14243b', '#52657b', '#e4eaf1'
def text(draw, xy, value, size=20, fill=INK, bold=False, anchor=None):
    draw.text(xy, str(value), font=ImageFont.truetype(BOLD if bold else FONT,size),fill=fill,anchor=anchor)
def canvas(title, sub, height=760):
    im=Image.new('RGB',(1400,height),'#ffffff'); draw=ImageDraw.Draw(im)
    text(draw,(55,28),title,34,bold=True); text(draw,(55,82),sub,19,MUTED)
    return im,draw
def axes(draw,box,xmin,xmax,ymin,ymax,xlabel='영업일',ylabel='잔액 · 천 통화',xticks=None,yticks=5):
    l,t,r,b=box
    X=lambda x:l+(x-xmin)/(xmax-xmin)*(r-l)
    Y=lambda y:b-(y-ymin)/(ymax-ymin)*(b-t)
    for i in range(yticks+1):
        y=ymin+(ymax-ymin)*i/yticks;py=Y(y)
        draw.line((l,py,r,py),fill=GRID,width=1)
        text(draw,(l-12,py),f'{y:,.1f}'.rstrip('0').rstrip('.'),16,MUTED,anchor='rm')
    for x in ([1,5,10,15,20,25,31] if xticks is None else xticks):
        text(draw,(X(x),b+12),str(x),16,MUTED,anchor='mt')
    draw.line((l,t,l,b,r,b),fill='#a8b5c5',width=2)
    text(draw,(l,t-30),ylabel,17,MUTED)
    text(draw,((l+r)/2,b+48),xlabel,18,MUTED,anchor='mt')
    return X,Y
def curve(im,draw,X,Y,points,color,width=4,band=None):
    if band:
        overlay=Image.new('RGBA',im.size);od=ImageDraw.Draw(overlay)
        rgb=tuple(int(color[i:i+2],16) for i in (1,3,5))
        poly=[(X(x),Y(hi)) for x,lo,hi in band]+[(X(x),Y(lo)) for x,lo,hi in reversed(band)]
        od.polygon(poly,fill=rgb+(30,));im.paste(overlay,(0,0),overlay)
    draw.line([(X(x),Y(y)) for x,y in points],fill=color,width=width)

S=D['strategyResults']
im,dr=canvas('31일 자금 곡선 · 비교 초안 v0.1','실선: 기대매출로 진행한 경로   /   음영: 무작위 5,000회 P10–P90   /   ●: 해당 경로의 설비 구매',830)
maximum=max(max(v['p90'] for v in s['bands']) for s in S)/1000
ymax=math.ceil(max(maximum,500)/100)*100
X,Y=axes(dr,(100,175,1315,660),1,31,0,ymax)
for day in [10,20]:
    dr.line((X(day),175,X(day),660),fill='#b7c4d5',width=2)
    text(dr,(X(day)+5,140),f'{day}일 수요 전환',17,MUTED)
dr.line((100,Y(500),1315,Y(500)),fill='#b45309',width=2)
text(dr,(1310,Y(500)-8),'시민권 비교선 500천 · 제안',17,'#92400e',anchor='rb')
for i in [0,1,4]:
    s=S[i];color=COLORS[i]
    curve(im,dr,X,Y,[(v['day'],v['balance']/1000) for v in s['expected']],color,band=[(v['day'],v['p10']/1000,v['p90']/1000) for v in s['bands']])
    for p in s['expectedPurchases']:
        x,y=X(p['day']),Y(s['expected'][p['day']-1]['balance']/1000)
        dr.ellipse((x-5,y-5,x+5,y+5),fill=color,outline='white',width=2)
for x,label,color in [(100,'저가부터 재투자',COLORS[0]),(430,'중간 설비 중심',COLORS[1]),(790,'직행 2경로·무구매 (중첩)',COLORS[4])]:
    dr.line((x,765,x+35,765),fill=color,width=5);text(dr,(x+45,751),label,20)
im.save(HERE/'cashflow.png')

im,dr=canvas('설비별 투자금 회수 · 대표 구매 상황','0선: 투자금 회수   /   실선: 평균 누적 추가수입 - 설비비   /   음영: P10–P90   /   이후 설비 추가 구매 없음',1060)
for i,r in enumerate(D['recoveries']):
    col,row=i%3,i//3;l=85+col*450;t=210+row*440;right=l+350;bottom=t+260
    vals=r['bands'];low=math.floor(min(v['p10'] for v in vals)/10000)*10
    high=max(10,math.ceil(max(v['p90'] for v in vals)/10000)*10)
    X,Y=axes(dr,(l,t,right,bottom),0,vals[-1]['elapsed'],low,high,'구매 후 영업일','순증분 · 천 통화',sorted(set([0,7,vals[-1]['elapsed']])),4)
    dr.line((l,Y(0),right,Y(0)),fill='#7c8ba0',width=2)
    dr.line((X(7),t,X(7),bottom),fill='#b7c4d5',width=2)
    curve(im,dr,X,Y,[(v['elapsed'],v['mean']/1000) for v in vals],COLORS[i%4],band=[(v['elapsed'],v['p10']/1000,v['p90']/1000) for v in vals])
    text(dr,(l,t-83),f"{D['facilityNames'][r['f']]} · {r['day']}일 구매 가정",21,bold=True)
    text(dr,(l,t-52),f"비용 {r['cost']:,} / 7일 내 회수 {r['withinSeven']:.1%}",17,MUTED)
im.save(HERE/'payback.png')

im,dr=canvas('날짜별 수요 가중치와 실제 주문 구성','왼쪽: 같은 성향 내 선호행 선택에 쓰는 상대 가중치   /   오른쪽: 21종 전부 해금한 통제 비교',770)
labels=['기본·식량·약품','공구·전력통신','핵보호·정밀장비']
X,Y=axes(dr,(95,200,650,580),0,2,0,2,'수요 구간','상대 가중치',[],4)
for p,label in enumerate(['초기 1–9일','중기 10–19일','후기 20–31일']):text(dr,(X(p),597),label,16,MUTED,anchor='mt')
for g in range(3):
    curve(im,dr,X,Y,[(p,D['assumptions']['phases'][p][g]) for p in range(3)],COLORS[g])
    for p in range(3):
        x,y=X(p),Y(D['assumptions']['phases'][p][g]);dr.ellipse((x-5,y-5,x+5,y+5),fill=COLORS[g]);text(dr,(x+28 if p==0 else x,y+18 if g==0 else y-13),D['assumptions']['phases'][p][g],16,COLORS[g],anchor='mt' if g==0 else 'mb')
X2,Y2=axes(dr,(800,200,1310,580),0,5,0,100,'초기                중기                후기','판매 개수 비중 · %',[],4)
for phase in range(3):
    for variant,units in enumerate([D['flatDemand'][phase],D['cache'][f'63:{phase}']['units']]):
        x=835+phase*170+variant*62;total=sum(units);bottom=580
        for g,value in enumerate(units):
            height=380*value/total;dr.rectangle((x,bottom-height,x+45,bottom),fill=COLORS[g]);
            if height>40:text(dr,(x+22,bottom-height/2),f'{100*value/total:.0f}',15,'white',anchor='mm')
            bottom-=height
        text(dr,(x+22,594),'기존' if variant==0 else '초안',15,MUTED,anchor='mt')
for x,label,color in zip([100,540,980],labels,COLORS):
    dr.rectangle((x,705,x+20,725),fill=color);text(dr,(x+30,700),label,20)
im.save(HERE/'demand.png')

def money(x):return f'{x:,.0f}'
def pct(x):return f'{100*x:.1f}%'
def table(headers,rows):return '| '+' | '.join(headers)+' |\n|'+'|'.join(['---']*len(headers))+'|\n'+'\n'.join('| '+' | '.join(map(str,row))+' |' for row in rows)+'\n'
f=D['facilityNames'];a=D['assumptions']
product_table=table(['상품','현재 정가','초안 정가','원가 보관값','해금 설비'],[[p['name'],'신규' if p['originalPrice'] is None else money(p['originalPrice']),money(p['price']),money(p['cost']),f[p['facility']]] for p in D['products']])
facility_table=table(['설비','초안 비용','산정 기준 구매일','기존 보유 설비','7영업일 예상 추가매출'],[[f[r['f']],money(r['cost']),str(r['day'])+'일','·'.join(f[j] for j in range(1,7) if r['before']&(1<<(j-1))) or '없음',money(r['expectedSeven'])] for r in D['anchors']])
strategies=table(['경로','기대매출 경로의 구매일','31일 잔액 중앙값','P10–P90','50만 도달률'],[[s['name'],' → '.join(f"{p['day']}일 {f[p['f']]}" for p in s['expectedPurchases']) or '24일까지 구매 없음',money(s['final']['p50']),money(s['final']['p10'])+'–'+money(s['final']['p90']),pct(s['targetRate'])] for s in S])
recovery=table(['설비','7일 내 최초 회수','31일까지 최초 회수','회수한 표본의 기간 중앙값'],[[f[r['f']],pct(r['withinSeven']),pct(r['by31']),str(r['recovery']['p50'])+'영업일' if r['recovery'] else '미회수'] for r in D['recoveries']])
daily=table(['일','유지비']+[s['name'] for s in S],[[i+1,money(a['maintenance'][i])]+[money(s['expected'][i]['balance']) for s in S] for i in range(31)])
decomp=[]
for r in D['anchors']:
    phase=0 if r['day']+1<10 else 1 if r['day']+1<20 else 2
    before=D['cache'][f"{r['before']}:{phase}"];after=D['cache'][f"{r['after']}:{phase}"]
    new=after['byFacility'][r['f']];delta=after['mean']-before['mean']
    decomp.append([f[r['f']],money(new),money(delta-new),money(delta)])
report=f'''# Cashier 밸런싱 초안 v0.1 — 2026-09-12

검토용 수치와 곡선이다. 자유 구매·날짜별 선호는 가상 설계이며 현재 게임에 적용하지 않았다. 이전 고정 선호 분석은 참고 기준선이고 이번 결과가 날짜별 선호를 적용한 첫 초안이다.

**후속 데이터 반영:** 사용자 요청으로 상품21종·정가·설비비·시작금·31일 유지비를 실제CSV에 반영했다. 날짜별 선호·자유 구매·시민권 비교선·영업180초는 아직 코드 연결이 없다. 상세 적용 및 검증 상태는 [준비 문서](../../balancing-preparation.md)를 따른다. 아래 그래프는 검토 당시 가상 조건을 유지하며 현재 게임 동작과 동일하지 않다.

## 먼저 볼 결과

첫 설비 접근, 7영업일 회수, 여러 구매 경로의 경쟁력을 함께 진단했다. 이번에 정의한 경로 중 중간 설비 중심 경로의31일 잔액 중앙값이 가장 높았다. 두 고가 설비 직행 경로는24일 구매 마감 전까지 초안 설비비를 모으지 못해 무구매와 같은 결과가 됐다. 설비 효과가 없다는 뜻은 아니다. 최적 전략을 탐색한 결과나 자유 구매가 불가능하다는 일반 결론으로 확대하지 않는다. 최종 밸런스로 승인할 수 있는 단계는 아니다.

날짜 가중치를 도입해도 실제 주문 구성의 변화는 작았다. 단일 선호행 성향은 재가중되지 않고, 당일 상품 추첨과 선호품 부재 시 대체 주문이 효과를 약화한다. 아래 수요 그래프에서 입력 가중치와 판매 구성의 차이를 함께 확인할 수 있다.

기대매출 경로에서 저가 재투자는 첫 설비3일 목표를 충족했지만 중가 진입12일·고가 진입21일로10/20일 목표보다 늦었다. 중간 중심 경로는 중가 진입11일로 늦고 고가 진입20일은 맞췄다. 첫 설비3일 목표는 저가 재투자 경로의 기준으로 보았으며 모든 정책에서 동시에 충족한다고 주장하지 않는다.

![날짜별 자금 곡선](cashflow.png)

{strategies}

실선과 구매일은 매일 기대매출을 받는 한 경로다. 표의 잔액 중앙값·P10–P90·도달률은 별도의 무작위 {a['runs']:,}회 결과다. 서로 다른 통계이므로 31일 실선 끝과 중앙값은 일치하지 않을 수 있다. 음영은 계산 오차나 최솟값–최댓값이 아니라 이 모델 플레이의 가운데80% 범위다. 직행 두 경로와 무구매가 대부분 겹친다.

## 입력 기준과 제안값

| 항목 | 초안 | 상태·의미 |
|---|---|---|
| 상품·영업·기간 | 21종,180초,평균8건,31일 | 채택된 검증 기준. 모델은8건 모두 완료, 실제180초 처리 가능성 미검증 |
| 설비 구매 | 자금 조건만, 익일 상품 활성 | 사용자 의도를 적용한 가상안. 현행 가게 단계 제한은 분석에서 제외 |
| 원가 | 미차감 | 현행은 기록만 수행. 원가0 거래 거부 검증은 보존 |
| 시작금 | {a['start']:,} | 제안. 현행100,000에서 낮춰 첫 설비3일 목표를 비교. 첫 실행0에서는 기대 경로4일 구매여서1,000으로 조정 |
| 유지비 | 1~30일 기존200~3,100;31일3,200 | 31일 행은 현재 없음. 선형 연장 제안 |
| 시민권 비교선 | {a['citizenshipTarget']:,} | 검토용 제안. 그래프는 결제 전 잔액이며 자동 차감·승리 처리 없음 |
| 명성·판정 | 중립 성향 비율 고정, 정가 판매 | 명성 변화·가격 이벤트·할인·거절·이탈·벌금·치료비 등 제외 |
| 가게 단계 목표 | 기존10/20일은 중·고가 진입의 관측 목표 | 잠금 조건으로 적용하지 않음. 시민권31일의 도달률과 함께 검토 |

시작금 변경은 첫 설비 접근을 보기 위한 비교 변수다. 현행100,000을 유지한 경우와의 민감도는 이번 초안 범위 밖이다. 식량 가격350/450 외 기존 상품가는 유지했다. 상품가 압축으로 직행 전략이 성립하는지는 후속 비교 대상으로 남긴다.

## 날짜별 선호

| 날짜 | 기본·식량·약품 | 공구·전력통신 | 핵보호·정밀 |
|---|---:|---:|---:|
| 1~9일 | 1.6 | 1.0 | 0.45 |
| 10~19일 | 1.0 | 1.6 | 1.0 |
| 20~31일 | 1.0 | 1.2 | 1.8 |

모두 **상대 가중치 제안**이다. 명성의 손님 성향 비율은 그대로 두고, 같은 성향에 속한 선호 설정행만 재가중한다. 복수 상품 타입의 행은 해당 가중치 산술평균, 한 행뿐인 성향은 변화 없음. 개별 상품 PK 선호는 현재 모두 빈 셀이다. 현재 데이터가 바뀌면 모델도 재검토해야 한다.

![기간별 수요와 주문 구성](demand.png)

오른쪽은 모든21종을 해금한 동일 보유 상태에서의 통제 비교다. '기존'은 균등 행 선택, '초안'은 날짜 가중 행 선택이며 각 구간의4/6/8종 추첨은 양쪽 모두 적용했다. 실제 구매 경로의 날짜별 판매 비중을 뜻하지 않는다. 가중치를0으로 만들지 않았지만 상품 등장과 판매 수익을 보장하지 않는다.

핵보호·정밀의 판매 개수 비중은 초기 약39.6%→37.3%, 후기 약34.6%→38.3%였다. 초기0.45·후기1.8 가중치에 비해 실제 주문 변화는 작다. 선호 행을 재가중하는 것만으로 강한 시기별 수요 차이가 만들어졌다고 판단하지 않는다.

상품 목록 추첨100/220/280/350, 당일4/6/8종, 선호 선택90%, 주문1~3종·종류당1~3개는 유지했다. 선호 상품이 없으면 비선호 상품에서 주문한다. 따라서 선호를 바꿔도 고가품 매출 억제가 약할 수 있다. 학습·예고 시스템은 가정하지 않았다.

## 상품 가격표

{product_table}

추가5종의 신규 문자열 키는 분석용이며 런타임 ID를 발급한 것이 아니다. 방사능 측정기는 핵보호로 이동한21종안을 사용했다. 원가 보관값은 현행 또는 기존 신규50% 초안을 유지했고, 식량 정가 변경에 맞춰 원가를 재계산하지 않았다. 계산에서는 모든 원가를 차감하지 않는다.

## 설비 가격과 손익분기

대표 구매 상황에서 구매 다음 날부터7영업일의 추가매출을 합하고1,000단위 반올림했다. 보유 조합과 날짜는 **견적 산정용 가정이며 구매 선행조건이 아니다.** 모든 경로는 같은 설비비를 지불한다. 기존 가게 확장비·편의설비비는 넣지 않았다.

{facility_table}

![설비별 투자금 회수](payback.png)

{recovery}

이 그래프는 지정된 보유 상태에서 해당 설비 하나를 추가한 경로와 추가하지 않은 경로를 동일 난수로 비교한다. 이후 설비는 더 사지 않는다. 실제 경로 전체의 회수일이 아니다. 순증분이 처음0 이상인 날을 최초 회수로 셌으며, 이후에도 계속 양수임을 보장하지 않는다. 회수 기간 중앙값은31일까지 회수한 표본에 한정하므로 미회수율과 함께 읽는다. 대표 가격이7일 기대 추가매출과 비슷하므로7일 내 회수 확률은100%가 아니라 대략 절반이다.

해금 상품 총매출과 투자 효과의 차이는 다음과 같다. 각 견적 상황의 첫 영업일 기준으로, 기존 상품의 대체 손실을 함께 차감해야 한다.

{table(['설비','해금 상품 매출','기존 상품 매출 변화','하루 추가매출'],decomp)}

## 경로 정책과 일별 잔액

저가 재투자는 선반→약품→공구→전력→핵보호→정밀, 중간 중심은 약품부터 같은 순서다. 핵보호 직행은 핵보호→정밀, 정밀 직행은 정밀→핵보호다. 이는 비교할 행동 정책이며 최적 전략을 계산한 것이 아니다. 앞선 목표를 살 자금이 모일 때까지 저축하고, 다른 설비를 즉흥적으로 대체하지 않는다.

정산마다 유지비를 먼저 차감하고 다음 영업일 유지비를 남길 수 있으면 설비1개를 구매한다. 신규 상품은 다음 날부터 반영한다. 모든 경로는24일까지만 새 설비를 사도록 했다. 이는 남은7영업일을 고려한 모델 정책이며 게임 구매 제한이 아니다. 현재 유지비 외 지출을 제외해도 직행이 막히는지를 우선 본다.

아래 잔액은 기대매출 경로의 설비 구매·유지비 차감 후 값이다. 시민권 결제 전이다.

{daily}

## 검증·한계·다음 검토

- 계산: 고정 시드 {a['seed']}, 보유 상태·기간별 {a['cacheDays']:,}일 기대매출 표본, 경로별 {a['runs']:,}회 및 설비별 {a['runs']:,}회 회수 비교. 1~3개 수량도 실제 추첨했다. 실재 플레이 테스트가 아니다.
- 일일 가중 비복원 추첨은 동등 분포의 exponential-race 방식이며 원본 C# 난수열을 재현하지 않는다. 같은 거래 난수를 공유해 구매 전후를 대응시켰다. 이 대응 방식도 반사실 비교를 위한 분석 가정이다.
- 자동 확인: {', '.join(D['checks'])}. 시뮬레이터의 내부 일관성 확인이며 Unity 통합 검증으로 확대하지 않는다.
- 기술 담당은 읽기 전용으로 확률·익일 효과·현금 보존·회수 계산을 검토했다. 진열되지 않은 상품까지 선호 대기에 집계하던 보조 지표1건을 당일 진열·설비·선호의 교집합으로 수정하고 재계산했다. 매출·잔액·회수 결과는 동일했다. 그래프의 숫자 눈금·글자·범례도 이미지로 확인했다.
- 기획 담당의 결과 리뷰에 따라 경로 간 우위를 비교 정책 안으로 한정하고, 직행의 구매 마감 및 수요 변화 폭과 성장 목표 미충족을 본문에 명시했다.
- 자유 구매, 날짜 선호, 새5종, 가격 변경은 게임에 미적용. 사용자 설정·패키지·기존 작업 파일 보존. 이번에 Unity 실행·Git stage·commit·push·merge 없음.
- 첫 설비3일, 중가10일, 고가20일, 시민권31일은 대표 경로 관측 목표다. 현재 초안에서 실제 구매일·도달률이 벗어난 항목은 미충족으로 보고 조정한다.
- 다음 수치 비교는 기존 가격 보존안과 고가 상품 가격 간격 축소안을 나란히 두고, 설비비를 함께 재산정하는 것이다. 고가 설비비만 낮추면 회수가 지나치게 빨라질 수 있으므로 가격·비용을 함께 바꿔야 한다. 7일 회수는 대표 상황의 목표로 유지한다.
- 사용자는 성장 속도, 늦은 투자 회수의 부담, 중간 투자와 직행 선택의 매력을 검토하면 된다. 현재 초안은 직행 경로 미성립과 시민권 성공률 차이를 보여주는 진단 결과다.

재현: 이 폴더의 [model.js](model.js)를 Node로 실행하고, Pillow가 설치된 Python으로 [render.py](render.py)를 실행한다. 숫자 원본은 [results.json](results.json), 원문 근거는 [물품 기준안](../sources/t.mxsfe4eewdaq.md), 이전 논의는 [준비 문서](../../balancing-preparation.md)에 있다. 실제CSV 반영 후에도 검토 당시 계산을 재현하도록 inputs/에 적용 전 상품·유지비와 당시 성향·명성CSV 4개를 보존했다. 분석 모델만 읽는 스냅샷이며 runtime 테이블이 아니다. 새 밸런스 비교에서는 이 입력을 명시적으로 갱신하거나 새 초안으로 분리한다.
'''
(HERE/'draft.md').write_text(report,encoding='utf-8')
print('Rendered cashflow.png, payback.png, demand.png, draft.md')
