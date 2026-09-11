"""Render the actual nine CSV dialogue pages as a review sheet, not a game screenshot."""
import csv
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]


def read(name):
    with (ROOT / 'Assets/Datas' / f'{name}.csv').open(encoding='utf-8-sig', newline='') as stream:
        return list(csv.DictReader(stream))


texts = {int(row['idx']): row['text'] for row in read('TextData')}
events = sorted(read('InspectorEventData'), key=lambda row: int(row['day']))
pages = [(event, i+1, int(idx)) for event in events for i, idx in enumerate(event['dialogue_text_idxs'].split('_'))]
assert len(texts) == 230 and len(pages) == 9
im = Image.new('RGB', (1500, 1100), '#f3f5f8')
draw = ImageDraw.Draw(im)
colors = {'1': '#2563eb', '10': '#b45309', '21': '#7e22ce'}


def label(x, y, value, size=22, color='#172b45'):
    draw.text((x, y), value, font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', size), fill=color)


label(40, 25, '감독관 대화 초안 · 1 / 10 / 21일차', 34)
label(40, 78, '기획 검토용 페이지 배열 · 한 페이지 3줄 / 실제 게임 화면은 아닙니다.', 21)
table = ['# 감독관 실제 대사 페이지', '', 'TextData에서 자동 생성한 검토표. 한 묶음이 다음 버튼 한 번에 표시되는 페이지다.', '']
for number, (event, page, idx) in enumerate(pages):
    x, y = 40 + (number % 3) * 487, 145 + (number // 3) * 285
    draw.rounded_rectangle((x, y, x+460, y+255), radius=12, fill='white', outline='#dce1e8', width=2)
    label(x+22, y+18, f"{event['day']}일차 · {page}페이지", 24, colors[event['day']])
    label(x+22, y+60, f"Event {event['idx']} / Text {idx}", 16, '#64748b')
    lines = texts[idx].splitlines()
    assert len(lines) == 3 and all(lines)
    for line_no, line in enumerate(lines):
        label(x+22, y+105+line_no*38, line, 22)
    table.extend([f"## {event['day']}일차 · {page}페이지 · Text{idx}", '',
                  '> ' + '<br>\n> '.join(lines), ''])
label(40, 1030, '첫날 4페이지 · 중반 2페이지 · 후반 3페이지 / CSV의 실제 개행을 페이지 내부 줄바꿈으로 사용', 20)
im.save(HERE / 'pages.png')
(HERE / 'pages.md').write_text('\n'.join(table), encoding='utf-8')
print('Rendered 9 pages, 27 lines from actual CSV')
