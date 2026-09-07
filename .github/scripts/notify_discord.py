"""Send one Discord embed per branch push using only the Python standard library."""

import json
import os
import re
import sys
import time
from urllib.error import HTTPError, URLError
from urllib.parse import parse_qsl, urlencode, urlsplit, urlunsplit
from urllib.request import Request, urlopen


# Keep below Discord's 4096-character description / 6000-character embed limits.
DESCRIPTION_LIMIT = 3900
MAX_COMMITS = 10


def clip(value, limit):
    value = str(value or "")
    return value if len(value) <= limit else value[: limit - 1] + "…"


def plain(value, limit):
    # Escape Discord Markdown and prevent mentions in user-supplied metadata.
    value = clip(value, limit).replace("@", "@\u200b")
    return re.sub(r"([\\`*_{}\[\]()<>|~])", r"\\\1", value)


def build_payload(event):
    repo = event.get("repository", {})
    branch = event.get("ref", "").removeprefix("refs/heads/")
    pusher = (event.get("sender") or {}).get("login") or (
        event.get("pusher") or {}
    ).get("name", "알 수 없음")
    commits = event.get("commits") or []
    compare = event.get("compare") or repo.get("html_url", "")
    intro = f"**{plain(pusher, 100)}**님이 커밋 {len(commits)}개를 푸시했습니다."
    if event.get("forced"):
        intro += "\n⚠️ 강제 push"
    if event.get("created"):
        intro += "\n🌱 새 브랜치"

    # Reserve space for the omission notice and comparison link.
    footer = f"\n\n[전체 변경사항 보기]({compare})" if compare else ""
    budget = DESCRIPTION_LIMIT - len(intro) - len(footer) - 100
    blocks = []
    for commit in commits[:MAX_COMMITS]:
        message = (commit.get("message") or "(커밋 메시지 없음)").strip()
        title, _, body = message.partition("\n")
        sha = str(commit.get("id", ""))[:7]
        url = commit.get("url", "")
        author = (commit.get("author") or {}).get("name", "알 수 없음")
        heading = f"[{sha}]({url})" if url else sha
        block = f"\n\n{heading} · **{plain(title, 180)}**\n작성자: {plain(author, 80)}"
        if body.strip():
            block += "\n" + plain(body.strip(), 600)
        if len(block) > budget:
            break
        blocks.append(block)
        budget -= len(block)

    omitted = len(commits) - len(blocks)
    notice = f"\n\n외 {omitted}개 커밋은 전체 변경사항에서 확인하세요." if omitted else ""
    if not commits:
        notice = "\n\n이 push 이벤트에 포함된 커밋 설명이 없습니다."
    return {
        "username": "Cashier GitHub",
        "allowed_mentions": {"parse": []},
        "embeds": [{
            "title": clip(f"📦 {repo.get('name', 'Cashier')} · {branch}", 250),
            "description": intro + "".join(blocks) + notice + footer,
            "color": 15105570 if event.get("forced") else 5763719,
            "footer": {"text": "긴 제목·본문은 생략될 수 있습니다. 전체 내용은 커밋 링크를 확인하세요."},
        }],
    }


def send(webhook_url, payload):
    parts = urlsplit(webhook_url)
    if parts.scheme != "https" or parts.hostname not in {
        "discord.com", "www.discord.com", "canary.discord.com", "ptb.discord.com",
    } or not parts.path.startswith("/api/webhooks/"):
        raise ValueError("DISCORD_WEBHOOK_URL에 올바른 Discord HTTPS 웹훅 주소를 설정하세요.")
    query = dict(parse_qsl(parts.query))
    query["wait"] = "true"
    url = urlunsplit(parts._replace(query=urlencode(query)))
    data = json.dumps(payload, ensure_ascii=False).encode("utf-8")
    for attempt in range(3):
        request = Request(url, data=data, headers={
            "Content-Type": "application/json",
            "User-Agent": "Cashier-GitHub-Notification/1.0",
        }, method="POST")
        try:
            with urlopen(request, timeout=20) as response:
                response.read()
            return
        except HTTPError as exc:
            if exc.code == 429 and attempt < 2:
                try:
                    delay = float(json.loads(exc.read()).get("retry_after", 1))
                except (ValueError, TypeError, AttributeError):
                    delay = 1
                if not 0 <= delay <= 30:
                    raise RuntimeError("Discord 요청 제한 대기 시간이 너무 깁니다.") from None
                time.sleep(delay + 0.5)
                continue
            # Never print the exception URL: it contains the webhook token.
            raise RuntimeError(f"Discord 전송 실패: HTTP {exc.code}") from None
        except (URLError, TimeoutError, OSError):
            # An uncertain response may already have created a message; avoid duplicates.
            raise RuntimeError("Discord 연결 실패 또는 응답 시간 초과. Actions 실행을 확인하세요.") from None


def main():
    with open(os.environ["GITHUB_EVENT_PATH"], encoding="utf-8") as source:
        event = json.load(source)
    if event.get("deleted") or not event.get("ref", "").startswith("refs/heads/"):
        print("브랜치 삭제 또는 태그 이벤트: 알림 생략")
        return
    webhook_url = os.environ.get("DISCORD_WEBHOOK_URL", "").strip()
    if not webhook_url:
        raise ValueError("저장소 Actions Secret에 DISCORD_WEBHOOK_URL을 등록하세요.")
    send(webhook_url, build_payload(event))
    print("Discord push 알림 1개 전송 완료")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, RuntimeError, KeyError, OSError):
        # Keep all sensitive URLs and event contents out of CI logs.
        print("알림 실패: Secret 설정, 이벤트 파일, Discord 연결 및 요청 제한을 확인하세요.", file=sys.stderr)
        sys.exit(1)
