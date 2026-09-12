"""Render approved morality data and compare it to runtime CSV; never write runtime data."""
import csv, json
from pathlib import Path
from decimal import Decimal
from PIL import Image, ImageDraw, ImageFont

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
fields=['offer_min_rate','offer_max_rate','include_min','include_max','is_accepted','adult_morality_point','child_elderly_morality_point']
poor_values=[(0,720,0,1,1,6,9),(720,900,0,0,1,3,4.5),(900,900,1,1,1,0,0),(900,945,0,1,1,-1.5,-2.25),(945,990,0,1,1,-3,-4.5),(990,1080,0,1,1,-4.5,-6.75),(1080,1150,0,1,1,-6,-9),(1150,0,0,0,0,-6,-9)]
wealthy_values=[(0,800,0,1,1,.5,1),(800,1000,0,0,1,.25,.5),(1000,1000,1,1,1,0,0),(1000,1200,0,1,1,-.1,-.2),(1200,1400,0,1,1,-.2,-.4),(1400,1600,0,1,1,-.3,-.6),(1600,1800,0,1,1,-.5,-1),(1800,0,0,0,0,-.5,-1)]
poor=[dict(idx=14021+i,**dict(zip(fields,r))) for i,r in enumerate(poor_values)]
wealthy=[dict(idx=14029+i,**dict(zip(fields,r))) for i,r in enumerate(wealthy_values)]
with (ROOT/'Assets/Datas/MoralityData.csv').open(encoding='utf-8-sig',newline='') as f:
    actual={int(r['idx']):r for r in csv.DictReader(f)}
normal=[{k:float(r[k]) for k in fields} for r in actual.values() if r['customer_disposition_type']=='1']
for kind,rows in [(5,poor),(4,wealthy)]:
    for row in rows:
        assert int(actual[row['idx']]['customer_disposition_type'])==kind
        for key in fields:
            assert Decimal(actual[row['idx']][key])==Decimal(str(row[key]))

def matches(row,rate):
    return (rate>row['offer_min_rate'] or rate==row['offer_min_rate'] and row['include_min']) and (row['offer_max_rate']==0 or rate<row['offer_max_rate'] or rate==row['offer_max_rate'] and row['include_max'])

def score(rows,rate,column='adult_morality_point'):
    found=[r for r in rows if matches(r,rate)]
    assert len(found)==1,(rate,found)
    return found[0][column]

for rows,limit in [(poor,1150),(wealthy,1800)]:
    boundaries=sorted({r[k] for r in rows for k in fields[:2]}|{3000})
    probes=[Decimal(x) for x in boundaries if x>0]+[(Decimal(a)+Decimal(b))/2 for a,b in zip(boundaries,boundaries[1:])]
    for rate in probes:
        matched=[r for r in rows if matches(r,rate)]
        assert len(matched)==1 and bool(matched[0]['is_accepted'])==(rate<=limit)
assert score(poor,900)==0 and score(poor,1000)==-4.5
assert score(wealthy,1000)==0 and score(wealthy,1800)==-.5 and score(wealthy,1801)==-.5

im=Image.new('RGB',(1500,800),'white'); dr=ImageDraw.Draw(im)
def text(x,y,value,size=18,color='#172b45',anchor=None):
    dr.text((x,y),str(value),font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',size),fill=color,anchor=anchor)
text(50,25,'도덕성 반영값 · 가난 기획표와 부자 승인 초안',32)
text(50,80,'실선: 가난 / 점선: 부자 / 회색: 평범 성인 비교 / 음영: 거래 거절 구간',18)
for index,(name,rows,limit,color) in enumerate([('가난 · 기획표 반영',poor,1150,'#b45309'),('부자 · 승인 초안 반영',wealthy,1800,'#2563eb')]):
    l=90+index*740; r=l+610; t=200; b=635
    X=lambda rate:l+(rate-700)/1200*(r-l)
    Y=lambda v:b-(v+10)/20*(b-t)
    text(l,140,name,25)
    text(l,174,'거래당 도덕성 변화',16)
    dr.rectangle((X(limit),t,r,b),fill='#f1f5f9')
    for value in [-10,-5,0,5,10]:
        dr.line((l,Y(value),r,Y(value)),fill='#d5dee9',width=2 if value==0 else 1)
        text(l-10,Y(value),value,15,anchor='rm')
    for rate in [700,900,1000,1150,1400,1600,1800,1900]:
        text(X(rate),b+13,f'{rate/10:g}',14,anchor='mt')
    for rate,label in [(900,'90%'),(1000,'100%')]:
        dr.line((X(rate),t,X(rate),b),fill='#c5cdd6',width=1)
    dr.line([(X(rate),Y(score(normal,rate))) for rate in range(700,1901)],fill='#94a3b8',width=2)
    for column,width in [('child_elderly_morality_point',2),('adult_morality_point',4)]:
        points=[(X(rate),Y(score(rows,rate,column))) for rate in range(700,1901)]
        if index:
            for start in range(0,len(points)-1,20): dr.line(points[start:start+13],fill=color,width=width)
        else: dr.line(points,fill=color,width=width)
    zero=900 if index==0 else 1000
    x,y=X(zero),Y(0);dr.ellipse((x-5,y-5,x+5,y+5),fill=color)
    text(X(limit)-6,t+15,f'{limit/10:g}%까지 수락',16,color,anchor='rt')
    text((l+r)/2,b+55,'제시 총액 / 현재 기준 총액 (%)',18,anchor='mt')
text(70,735,'굵은 선: 성인 / 가는 선: 아이·노인. 경계의 포함 여부와 정확한 수치는 아래 표가 기준입니다.',19)
im.save(HERE/'morality-review-20260912.png')

def table(rows):
    lines=['| 제시가 비율 | 거래 | 성인 | 아이·노인 |','|---|---|---:|---:|']
    for r in rows:
        lo,hi=r['offer_min_rate']/10,r['offer_max_rate']/10
        boundary=f'정확히 {lo:g}%' if lo==hi else f'{lo:g}% 초과' if hi==0 else f'{hi:g}% 이하' if lo==0 else f'{lo:g}% 초과~{hi:g}% '+('이하' if r['include_max'] else '미만')
        lines.append(f"| {boundary} | {'수락' if r['is_accepted'] else '거절'} | {r['adult_morality_point']:+g} | {r['child_elderly_morality_point']:+g} |")
    return '\n'.join(lines)

report='''# 가난·부자 도덕성 데이터 반영

2026-09-12. 사용자가 데이터 추가와 예약 범위 확장을 승인했다. **가난14021~14028, 부자14029~14036의16행을 런타임 MoralityData에 추가했다.** 기존20행은 보존했다. 실행 검증 결과는 [작업 기록](../balancing-preparation.md)을 따른다.

![점수 곡선](morality-review-20260912.png)

## 가난: 기획 확정표 8행

근거는 기획서의 [거래·도덕성 확정표](https://docs.google.com/document/d/1lCzaQRmFRWrxfIhWr2UZy64-7A8v1N9fAvlOorMF77E/edit?tab=t.jxxow3f6hpes)다. 새 기획 제안이 아니며 경계·점수·연령 배율을 그대로 옮긴다. 제시 총액과 현재 기준 총액은 양수다.

''' + table(poor) + '''

도덕성 중립점은90%지만 기준가 판매 판정은100%다. 따라서 가난 성인에게100%로 팔면 거래는 기준가 판매 성공, 도덕성은−4.5다. 이전 미평가(null) 상태에서 이번에 실제 점수를 평가하도록 데이터를 연결했다.

예를 들어 전원 성인·하루8건·가난 비중10%·전부100% 판매라는 통제 가정이면 도덕성 기대 변화는 하루−3.6,31일−111.6이다. 실제 플레이 예측이나 새 일일 제한이 아니다. 고정 가격 정책이 Good 엔딩의0 이상 조건을 자동 보장하지 않는다는 의미다. 기존 무이벤트 현금흐름 그래프는 도덕성을 계산하지 않았으므로 해당 그래프로 엔딩까지 검증됐다고 해석하면 안 된다.

## 부자: 사용자 승인 초안 8행

원문에는180% 구매 상한이 있지만 도덕성 점수표가 없다. 아래는 사용자 승인을 받아 반영한 검증 초안이며 기획 원문의 확정값으로 소급하지 않는다. 할인 보상은 일반의 절반,100%는0,100% 초과에는 작은 음수, 아이·노인은 성인의2배다.

''' + table(wealthy) + '''

할인 보상을 낮춰 부자에게만 할인해서 점수를 쉽게 얻는 유인을 줄이고, 비싸게 팔 수 있다는 이유로 도덕성 비용을0으로 만들지는 않는다. 180% 초과 거절도 직전 구간과 같은 음수여서 고의 거절로 벌점을 없애지 못한다. 소액 거래 반복·손님 선별에 의한 점수 획득 가능성은 현 거래당 고정 점수 계약에 남아 있으며 이번 수치만으로 방지했다고 주장하지 않는다. 금액 비례 계산이나 일일 제한은 추가하지 않는다.

## ID 예약과 데이터 반영

Google Docs의 [CSV 종류 ID](https://docs.google.com/document/d/1lCzaQRmFRWrxfIhWr2UZy64-7A8v1N9fAvlOorMF77E/edit?tab=t.ccpln6m1g4kv)에서 Morality 예약을14001~14036으로 수정했다. 해당 탭의 다른17문단과67개 탭의 제목·순서·부모 관계 보존을 읽기 확인했다. 사용 가능한 Git 이력과 현재CSV에서 신규PK 중복이 없음을 확인했다. 새 테이블·컬럼·제품코드는 추가하지 않았다.

## 검증 범위

기존 스키마의 min/max·include_min/max·is_accepted와 소수 점수를 사용한다. 경계점과 모든 인접 구간 중간점을 검사해 두 성향 모두 양수 제시비율이 정확히 한 행에 해당하고, 수락 범위가115%·180%와 일치함을 확인했다. 가난90%/100%·부자100%/180%/180%초과 사례와 실제CSV16행의 값도 대조했다. 이 스크립트의 검사는 오프라인 정적 검사이며 Unity 실행 증거는 작업 기록에 구분해 적는다.

morality-review-20260912.json에는 반영한 ID와 점수의 근거 구분을 저장한다. 이 파일은 분석자료이며 게임 로더에 등록하지 않는다.
'''
(HERE/'morality-review-20260912.md').write_text(report,encoding='utf-8')
(HERE/'morality-review-20260912.json').write_text(json.dumps({'poor':{'status':'source-confirmed, applied','rows':poor},'wealthy':{'status':'user-approved draft, applied','rows':wealthy},'idsAssigned':True},ensure_ascii=False,indent=2),encoding='utf-8')
print('16 applied rows match runtime CSV; boundaries and acceptance coverage PASS')
