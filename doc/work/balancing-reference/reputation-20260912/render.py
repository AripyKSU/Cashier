"""Static review charts and Korean report from the reputation simulation."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

HERE=Path(__file__).resolve().parent
D=json.loads((HERE/'results.json').read_text(encoding='utf-8'))
S={s['policy']['key']:s for s in D['scenarios'] if s['costKey']=='proposed'}
F={s:ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',s) for s in [19,22,25,30,38]}
INK,MUTED,GRID='#18273c','#52647a','#dce3ec'
COLORS={'fair':'#2675bd','light':'#198c79','frequent':'#d56a2c','repair':'#8556b5'}
LABELS={'fair':'꾸준한 정가 거래','light':'중반 실수 25%','frequent':'중반 실수 50%','repair':'실수 후 일부 할인'}
money=lambda x:f'{x:,.0f}'


def canvas(title,subtitle,height=930):
    im=Image.new('RGB',(1560,height),'white');dr=ImageDraw.Draw(im)
    dr.text((65,38),title,fill=INK,font=F[38]);dr.text((65,100),subtitle,fill=MUTED,font=F[22])
    return im,dr


def axes(dr,box,xlim,ylim,xticks,yticks,label):
    l,t,r,b=box
    xy=lambda x,y:(l+(x-xlim[0])/(xlim[1]-xlim[0])*(r-l),b-(y-ylim[0])/(ylim[1]-ylim[0])*(b-t))
    for y in yticks:
        py=xy(0,y)[1];dr.line((l,py,r,py),fill=GRID if y else MUTED,width=1 if y else 2)
        dr.text((l-15,py),money(y),fill=MUTED,font=F[19],anchor='rm')
    for x in xticks:dr.text((xy(x,0)[0],b+16),str(x),fill=MUTED,font=F[19],anchor='mt')
    dr.text((l,t-33),label,fill=MUTED,font=F[19])
    return xy


def legend(dr,keys,y=166):
    for i,key in enumerate(keys):
        x=130+i*350;dr.line((x,y+13,x+35,y+13),fill=COLORS[key],width=5)
        dr.text((x+47,y),LABELS[key],fill=INK,font=F[22])


im,dr=canvas('좋은 거래를 이어가며 쌓고, 실수 뒤 다시 회복하는 명성','현재 거래 판정·일일 명성 정산식 사용 / 하루 가격 제안 8회 / 각 경로 2,000회',1030)
legend(dr,list(COLORS))
xy=axes(dr,(130,255,1460,700),(1,31),(-25,105),[1,5,10,12,15,18,20,25,31],[-20,0,20,40,60,80,100],'정산 후 명성')
dr.rectangle((xy(12,0)[0],255,xy(18,0)[0],700),fill='#fdf2e8')
for y in [-20,0,20,40,60,80,100]:dr.line((130,xy(1,y)[1],1460,xy(1,y)[1]),fill=GRID if y else MUTED,width=1)
dr.text((xy(15,0)[0],273),'12~18일 실수 구간',fill='#a45a29',font=F[19],anchor='mt')
for key in ['fair','frequent']:
    rows=S[key]['days'];polygon=[xy(r['day'],r['repEnd']['p10']) for r in rows]+[xy(r['day'],r['repEnd']['p90']) for r in reversed(rows)]
    layer=Image.new('RGBA',im.size,(0,0,0,0));ld=ImageDraw.Draw(layer)
    ld.polygon(polygon,fill=(38,117,189,23) if key=='fair' else (213,106,44,23))
    im=Image.alpha_composite(im.convert('RGBA'),layer).convert('RGB');dr=ImageDraw.Draw(im)
for key in COLORS:
    dr.line([xy(r['day'],r['repEnd']['p50']) for r in S[key]['days']],fill=COLORS[key],width=4)
    dr.text((1470,xy(31,S[key]['finalRep']['p50'])[1]),money(S[key]['finalRep']['p50']),fill=COLORS[key],font=F[22],anchor='lm')
dr.text((65,790),'선: 날짜별 중앙값    연한 띠: 정가·실수50% 경로의 P10~P90    명성은 다음날 손님 구성에 반영',fill=MUTED,font=F[19])
dr.text((65,847),'실수: 각 제안에서 현재가의 150%를 입력. 손님별 허용 범위에 따라 수락하거나 거절합니다.',fill=INK,font=F[22])
dr.text((65,896),'할인 회복: 19일 이후, 가격민감형을 제외한 제안의 25%에 5% 할인. 명성 회복과 수입 감소를 함께 계산합니다.',fill=MUTED,font=F[19])
dr.text((65,941),'가격 이벤트는 중심 곡선에서 제외했습니다. 8회는 실제 180초 플레이에서 확인할 가정이며 성공 판매 8회를 보장하지 않습니다.',fill=MUTED,font=F[19])
im.save(HERE/'reputation.png')

im,dr=canvas('중반 실수가 성장 시점을 얼마나 늦추는가','기존 비용 조정안 사용 / 정산 때 구매 / 점은 구매일 중앙값, 막대는 P10~P90',1020)
legend(dr,['fair','light','frequent'])
x=lambda day:390+(day-7)/21*1030
for day in [8,10,12,15,18,20,22,24,26,28]:
    dr.line((x(day),235,x(day),820),fill=GRID,width=1);dr.text((x(day),845),str(day),fill=MUTED,font=F[19],anchor='mt')
for j,(id,label,target) in enumerate([(12008,'2단계 확장',10),(12010,'3단계 확장',20),(12005,'핵보호 설비',22)]):
    y=315+j*210
    dr.text((65,y-24),label,fill=INK,font=F[30]);dr.text((65,y+20),f'기준 목표 {target}일차',fill=MUTED,font=F[22])
    for offset,key in zip([-43,0,43],['fair','light','frequent']):
        m=next(m for m in S[key]['milestones'] if m['id']==id);py=y+offset
        dr.line((x(m['p10']),py,x(m['p90']),py),fill=COLORS[key],width=5)
        dr.ellipse((x(m['p50'])-9,py-9,x(m['p50'])+9,py+9),fill=COLORS[key])
        dr.text((x(m['p90'])+19,py),f"{m['p50']:.0f}일",fill=COLORS[key],font=F[22],anchor='lm')
dr.text((65,916),'식량·공구의 구매일 중앙값은 세 경로 모두 3·12일입니다. 상품 설비는 구매 다음 날부터 사용할 수 있습니다.',fill=MUTED,font=F[19])
dr.text((65,956),'실수 뒤에도 구매는 가능합니다. 날짜 잠금·자동 명성 상승·실패 보상 규칙을 추가하지 않았습니다.',fill=MUTED,font=F[19])
im.save(HERE/'growth.png')

im,dr=canvas('중반 공구 투자는 실수 구간에서 회수가 늦어짐','공구대 23,000G + 2단계 확장 23,000G / 다른 설비 고정 / 같은 명성 경로로 구매·미구매 비교')
legend(dr,['fair','light','frequent'])
xy=axes(dr,(145,260,1460,690),(0,9),(-50000,20000),list(range(10)),[-50000,-40000,-30000,-20000,-10000,0,10000,20000],'누적 추가 판매수입 - 투자비 (G)')
for key in ['fair','light','frequent']:
    roi=next(r for r in D['roi'] if r['policy']==key and r['id']==12003)
    dr.line([xy(r['elapsed'],r['mean']) for r in roi['curve']],fill=COLORS[key],width=4)
dr.text((65,787),'가로축: 12일차에 공구대를 구매했다고 가정한 뒤의 영업일 수. 선은 2,000회 평균입니다.',fill=MUTED,font=F[19])
dr.text((65,832),'실제로 그날 구매할 수 있는지는 성장 그래프에서 별도로 확인합니다. 이 그림은 설비의 한계 가치를 비교합니다.',fill=MUTED,font=F[19])
dr.text((65,876),'거절 수입 0G와 기존 상품 판매 감소를 포함합니다. 처음 손익분기를 넘은 뒤 다시 내려갈 수도 있습니다.',fill=MUTED,font=F[19])
im.save(HERE/'payback.png')

def table(headers,rows):
    return '| '+' | '.join(headers)+' |\n| '+' | '.join(['---']*len(headers))+' |\n'+'\n'.join('| '+' | '.join(map(str,r))+' |' for r in rows)


rep_rows=[];growth_rows=[];recovery_rows=[]
for key in COLORS:
    s=S[key]
    rep_rows.append([LABELS[key]]+[money(s['days'][i-1]['repEnd']['p50']) for i in [11,18,31]]+[f"{s['midRepDroppedRate']:.1%}"])
    growth_rows.append([LABELS[key]]+[money(m['p50']) for m in s['milestones']]+[money(s['finalCash']['p50'])])
    if key!='fair':recovery_rows.append([LABELS[key],s['recoveryEligibleRuns'],f"{s['recoveredDay']['p50']:.0f}일",f"{s['recoveredBy31']:.1%}"])
roi_rows=[]
for r in D['roi']:
    name={12001:'식량',12003:'공구',12005:'핵보호'}[r['id']]
    roi_rows.append([LABELS[r['policy']],name,money(r['cost']),money(r['curve'][7]['mean']),f"{r['withinSeven']:.1%}"])
mix_rows=[]
names={0:'기본 상품',3:'식량+약품',15:'식량+약품+공구+전력',31:'앞 조합+핵보호',63:'상품 설비 전부'}
for mask in names:
    rows={r['rep']:r for r in D['mix'] if r['mask']==mask}
    mix_rows.append([names[mask]]+[money(rows[rep]['mean']) for rep in [-80,0,80]]+[f"{rows[80]['mean']/rows[0]['mean']-1:+.1%}"])
cost_rows=[[name,money(D['costSets']['current'][str(id)]),money(D['costSets']['proposed'][str(id)] )] for id,name in [(12008,'2단계 확장'),(12003,'공구대'),(12010,'3단계 확장'),(12005,'핵보호')]]
report=f'''# 명성 변화에 따른 밸런싱 초안

2026-09-12. 가격 이벤트는 사용자 의견에 따라 무작위 변동 요소로 남기고, 이번 중심 계산에서는 제외했다. **현재 구현된 명성 정산과 손님 구성 변화**를 사용한다. 날짜에 맞춰 명성을 강제로 올리거나 내리는 기능을 추가하지 않는다. 런타임 CSV와 코드는 수정하지 않았다.

## 판단과 채택할 검증 경로

정확한 정가 거래를 이어가는 경로를 성장 기준으로 삼고, 중반 실수25%를 보통 실패·회복 검토,50%를 심한 실수 검토로 두는 것을 권한다. 현재 수치에서도 성실한 거래가 명성을 쌓고, 중반 실수로 낮아진 명성을 이후 거래로 되찾는 흐름이 나온다. **이번에는 명성 CSV의 보상·벌점을 강화할 근거가 없으며 기존 값을 유지하는 초안**이다.

사용자 제안은 자연스러운 상승과 중반 반복 실수라는 방향이다. 아래 기간·확률·입력 가격·할인 회복은 조정자가 구체화한 분석 가정이며 실측 플레이 행동이 아니다.

| 경로 | 1~11일 | 12~18일 | 19~31일 | 용도 |
| --- | --- | --- | --- | --- |
| 꾸준한 정가 | 정가 | 정가 | 정가 | 성장 비용의 기준 |
| 실수25% | 정가 | 각 제안마다25% 확률로150% 가격 입력 | 정가 | 일반적인 반복 실수 검토 |
| 실수50% | 정가 | 각 제안마다50% 확률로150% 가격 입력 | 정가 | 심한 실수 검토 |
| 일부 할인 회복 | 정가 | 실수50%와 동일 | 가격민감형 제외, 각 제안마다25% 확률로5% 할인 | 회복 선택의 대가 비교 |

하루 가격 제안 기회8회 중 각각 독립적으로 실수를 추첨한다. 25%는 평균2회,50%는 평균4회의 가격 과다 입력이며 고정 건수가 아니다.150%를 입력해도 성급형·부자형은 현재 허용 범위 안이라 수락할 수 있다. 거절된 거래는 수입0이며8회 중 한 기회를 소모한다. 이전의 성공 판매8건 가정을 실수 경로에 그대로 적용하지 않았다. 가격민감형은 정가만 받아들이므로 할인 회복 경로에서는 할인을 시도하지 않는다고 가정한다. 이 보조 경로는 해당 성향을 알아볼 수 있어야 하며, 실제 플레이에서 식별 가능한지는 이번에 확인하지 않았다.

## 실제 명성 규칙이 만드는 흐름

일반적인 정가 거래는 행동 점수50, 할인은100, 수락된 폭리는25, 과다 청구로 거절된 거래는0이다. **성급한 손님 정가 거래는100점에 가중치3**이어서 자연 상승에 크게 기여한다. 예를 들어 일반 정가7회와 성급형 정가1회면 일일 점수65, 중립 구간에서 명성+7이다. 성급형 없이 일반 정가8회면 점수50으로 명성이 그대로다. 자동 상승이 아니라 어떤 손님과 어떻게 거래했는지에 따른 결과다.

일일 가중 평균 점수0~19/20~39/40~60/61~80/81~100은 각각-15/-7/0/+7/+10으로 정산한다. 음수 명성 구간에서는 양수 회복량에1.5배 또는2배가 적용되고 버림·일일 상한+10을 거친다. 명성은-100~100이며 당일 점수가 다음날 손님 구성에 반영된다. 소표본5건 보정과 가격민감형의 하한 미달 거절을 중립 점수로 처리하는 규칙도 분석에 구현·검사했다.

명성 구간이 중립→상위→최상위로 오르면 부자 비중은10%→18%→25%, 성급형은15%→7%→5%가 된다. 저명성에서는 성급형이24%/33%로 늘어 정가 거래를 통한 복구 기회가 생긴다. 다만 성급형은 대기 인내시간이9초이므로 실제 플레이에서 놓치면 이번 가정보다 회복이 어려울 수 있다.

{table(['경로','11일 정산 명성','18일 정산 명성','31일 정산 명성','11→18일 실제 하락 비율'],rep_rows)}

수치는 각2,000회 결과의 날짜별 중앙값이다. 각 표본의 하락량 중앙값과 위 중앙값 두 개의 차이는 같지 않을 수 있다. 선과 띠는 실제 플레이 한 판이 아니라 결과 분포다.

![명성 곡선](reputation.png)

{table(['하락 뒤 경로','11→18일 하락한 표본 수','회복한 표본의 회복일 중앙값','하락 표본 중31일 내 회복 비율'],recovery_rows)}

회복은19일 이후 자신의11일 정산 명성에 처음 다시 도달하는 것으로 정의했다. 하락하지 않은 표본은 분모에서 제외한다. 50% 실수 경로에서는 약25%가31일까지 그 명성을 되찾지 못하므로 회복일27을 전원 회복 보장으로 읽으면 안 된다. 일부 할인은 회복을 앞당기지만 할인액과 구매 지연 때문에31일 보유금이 더 높아지는 전략은 아니었다. 따라서 할인을 최선의 거래나 필수 회복법으로 지정하지 않는다.

## 성장 비용안은 유지 가능

기존 [가게 단계 비용 조정안](../store-stages-20260912/report.md)을 자연 명성 경로로 다시 평가해도 구매일 중앙값은3·10·12·20·22일이다. 추가 가격 보정 없이 기존4개 제안값을 유지한다.

{table(['항목','현재 런타임 G','유지할 제안 G'],cost_rows)}

핵보호35,000G는3단계 확장143,000G를 먼저 지불한 뒤의 설비 가격이다. 실제1→2→3 단계 조건, 확장 즉시 적용/상품 설비 익일 활성, 초기1단계와 시작금1,000G,31일 유지비를 유지했다. 상품 가격·원가·CSV 스키마·명성 규칙 변경은 없다. **이 가격 제안은 아직 런타임 CSV에 반영하지 않았다.**

{table(['경로','식량 구매','2단계 확장','공구 구매','3단계 확장','핵보호 구매','31일 보유금 G'],growth_rows)}

단위는 구매일차이며 전부 정산 후 구매다. 기준 구매 순서는 식량→약품→2단계→공구→전력→3단계→핵보호→정밀이다. 다음날 유지비 예비금을 남기고 살 수 있으면 여러 개를 같은 정산에서 구매한다. 이 순서와 예비금은 플레이 전략 가정이며 코드의 필수 선행 설비나 날짜 잠금이 아니다. 다른 구매 순서와 거래량에서는 도달일이 달라진다.

![성장 도달 분포](growth.png)

중반25% 실수는3단계·핵보호가 각각1일 늦고,50%는3단계3일·핵보호2일 늦는다. 명성 손실뿐 아니라 거절로 인한 당일 수입 감소와 후속 투자 지연을 함께 반영한 결과다.31일 보유금 차액 전부를 명성의 직접 효과로 해석하지 않는다. 기본 가격·시작금·유지비를 낮춰 이 실수 손실을 자동 보전하는 수치는 제안하지 않는다.

## 명성 자체의 수입 효과를 분리한 비교

아래는22일차·같은 보유 설비·정가8건을 고정해 손님 구성만 바꾼각5,000회 평균이다. 중반 실수로 잃은 거래나 설비 구매 지연은 포함하지 않는다.

{table(['보유 조합','명성-80 매출 G','명성0 매출 G','명성80 매출 G','0→80 변화'],mix_rows)}

**명성이 오르면 모든 상황에서 매출이 증가하는 구조는 아니다.** 기본·식량·약품만 갖춘 경우 성급형의 약품 수요가 줄면서 매출이 조금 감소한다. 중급·고급 상품을 갖춘 경우 부자 비중 증가의 가치가 커진다. 상품진열과 선호 대체 선택이 수입 효과를 제한하므로 명성은 판매가에 직접 보너스를 주는 배율과도 다르다. 현재 시스템을 유지한 초안에서는 이 차이를 표시하고 플레이테스트에서 상승 보상을 체감하는지 검토한다.

## 투자 회수 재검토

첫 설비를 목표3/12/22일에 보유했다고 고정하고, 각 경로에서 나온 하루 시작 명성을 구매/미구매 양쪽에 동일하게 적용했다. 필요한 확장비는 한 번 합산한다. 다른 설비는 각각 미보유/식량+약품/중급까지 보유 상태로 고정했다. 이후9영업일까지 추가 판매수입을 비교하며 중반 거절 손실도 반영한다. 날짜별 구매 가능성은 앞 성장 시뮬레이션에서 따로 평가한다.

{table(['경로','첫 설비','확장 포함 비용 G','7영업일 누적 차액 평균 G','7일 내 첫 회수 비율'],roi_rows)}

자연 명성 경로는 약1주 회수 기준을 유지한다. 중반 공구 투자는 실수25%에서 약8영업일,50%에서 약9영업일까지 평균 회수가 늦어진다. 최초 확장비 지출부터 세면 추가2일이 있으며, 확률 표의 첫 회수 이후 다시 음수로 내려갈 수도 있다. 단계의 상품 설비2개 전체를 사는 묶음 회수로 확대 해석하지 않는다.

![공구 투자 회수](payback.png)

## 검토 기준과 범위

초기 플레이테스트의 기준은 다음과 같이 제안한다. 목표를 달성하지 못했다고 코드로 강제 교정하지 않고 거래 수·성향·가격 입력·거절·구매일을 확인한다.

- 정가 운영: 명성이 서서히 오르고, 비용안의3·10·12·20·22일 구매 목표 주변에 도달하는지 확인한다.
- 중반25% 실수: 실패가 수입과 명성에 보이되 정가 운영 복귀 후대부분31일 내 이전 명성을 회복하는지 확인한다.
- 중반50% 실수: 더 큰 성장 지연과 회복 부담을 허용하는 스트레스 경로로 둔다. 이를 정상 성장 속도의 가격 기준으로 삼지 않는다.
- 기본 상품만 있을 때의 작은 역효과, 성급형9초 대기, 높은 명성을 유지했을 때 부자 상품 수요의 체감을 확인한다. 새로운 UI/명성 보너스 시스템을 이번 초안에 추가하지 않는다.

분석 완료: 현행/제안2가지 비용×5개 행동 경로×2,000회=20,000개31일 시나리오. 명성별 보유 조합 비교25개×5,000일. 투자회수9개 조건×2,000회. 구현의 가격 판정 경계·성급형 가중치·명성 하한/상한·소표본 보정·회복 배율, 다음날 명성/상품 활성, 잔액 보존과유지비 납부를 검사했다. 중립 수요4개 대표 조건은 기존 추정과 통계 오차 범위 내 일치를 확인했다.

한계: 실제180초 처리량·대기열 이탈·편의설비 처리량, 가격 이벤트, 지침 벌금·도덕성/가족 선택의 후속 효과는 제외했다. 모든 계산 표본은 매일 유지비를 납부할 수 있었다. 미납을 임의로 면제하지 않았으며 미납 경로가 나오면 계산이 중단되도록 했다. 실제 게임 테스트나C#와동일seed 재생은 아니다. RNG는 날짜/표본seed를 혼합해 인접 날짜 간 단순한 난수 이동을 피했고 비교 경로끼리 같은seed를 사용한다.

검증 상태 **PARTIAL**: 오프라인 수치 검사·그래프 확인 완료, Unity 실행검증 미실행. 기획 역할에 읽기 검토를 요청했으나 회신 본문을 조회하지 못해 위 권고는 조정자 판단으로 기록한다. 런타임 수정·commit/push는 수행하지 않았다.

재현은 `calculate.js`를 Node로 실행한 뒤 `render.py`를 Pillow가 있는 Python으로 실행한다. `inputs.json`에 실제 CSV 입력·이전 비용 제안·소스 SHA256을 보관했고 이후 실행은 이 고정 입력을 사용한다. 결과는 `results.json`에 있다. 현재 시스템이 바뀌면 원본과 해시를 대조한 새 버전의 분석을 작성한다.
'''
(HERE/'report.md').write_text(report,encoding='utf-8')
print('Rendered reputation.png, growth.png, payback.png and report.md')
