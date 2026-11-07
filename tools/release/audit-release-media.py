#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""公開済み GitHub Release の ZIP が第三者楽曲メディアを含むかを判定する (Issue #77)。

何をするか
----------
1. GitHub Releases API から公開済み Release とアセット (名前 / サイズ / ダウンロード URL) を取得する。
2. ZIP を **丸ごとダウンロードせず**、HTTP Range リクエストで ZIP 終端ディレクトリ (central directory) だけを
   読み、ZIP 内のエントリ一覧 (名前 / 非圧縮サイズ) を取得する。
   - ルーズなメディア (audio / video) が ZIP 直下にあるかどうか
   - `FlashBeat_Data/resources.resource` の非圧縮サイズ (Unity が Resources/** を焼き込む容器)
   - `version.json` / `flashbeat-media-policy.json` / `StreamingAssets/update-channel.txt` の有無
3. 上記から「第三者楽曲メディアの同梱あり / なし」を判定して表で出力する。

なぜ Range リクエストか
----------------------
`v0.1.0-win64-743c88d` の ZIP は 1.36GB で、第三者楽曲が `resources.resource` に焼き込まれている。
ZIP 全体を落とすと 1.36GB の通信とディスクが要るうえ、公開アセットのDL回数を増やしてしまう。
central directory は ZIP 末尾にあるため数 KB の範囲取得で中身の一覧が読める。
`resources.resource` は非圧縮サイズ (中央ディレクトリのフィールド) が分かるので、
焼き込み済みかどうかを **通信量 않고** 判定できる。

使い方
------
    python3 tools/release/audit-release-media.py                      # 全 Release を調査
    python3 tools/release/audit-release-media.py --tag v0.1.14-win64-617c2b6
    python3 tools/release/audit-release-media.py --json                # 機械可読 (JSON)

注意: このスクリプトは **公開・削除・タグ書き換えをしない** (調査専用)。
GitHub API のレート制限を避けるため、認証は環境変数 GITHUB_TOKEN があれば使う (無ければ匿名)。
"""

from __future__ import annotations

import argparse
import json
import os
import struct
import sys
import urllib.error
import urllib.request
from typing import Any

DEFAULT_REPO = "rebuildup/FlashBeat"

# 判定に使うメディア拡張子 (package-win64.ps1 の MediaExtensions と同じ)。
MEDIA_EXTENSIONS = (".wav", ".mp3", ".mp4", ".mov", ".ogg", ".m4a", ".webm", ".avi")

# resources.resource の非圧縮サイズがこの値を超えたら「メディアが焼き込まれている」と判定する。
# 自作の背景アニメーションだけなら約 196MB (docs/operations/update-release.md 参照)。
DEFAULT_RESOURCES_THRESHOLD = 300 * 1024 * 1024

USER_AGENT = "FlashBeat-release-media-audit/1.0 (+https://github.com/rebuildup/FlashBeat)"


# ── HTTP ────────────────────────────────────────────────────────────────
def _headers(token: str | None) -> dict[str, str]:
    headers = {"User-Agent": USER_AGENT}
    if token:
        headers["Authorization"] = "Bearer " + token
    return headers


def _open(url: str, token: str | None, start: int | None = None, end: int | None = None):
    req = urllib.request.Request(url)
    for key, value in _headers(token).items():
        req.add_header(key, value)
    if start is not None:
        req.add_header("Range", "bytes={0}-{1}".format(start, end))
    with urllib.request.urlopen(req, timeout=120) as resp:
        return resp.status, resp.read()


def api_get_json(url: str, token: str | None) -> Any:
    req = urllib.request.Request(url)
    for key, value in _headers(token).items():
        req.add_header(key, value)
    req.add_header("Accept", "application/vnd.github+json")
    with urllib.request.urlopen(req, timeout=120) as resp:
        return json.loads(resp.read().decode("utf-8"))


def fetch_range(url: str, token: str | None, start: int, end: int) -> bytes:
    """[start, end] の範囲を取得する。範囲取得に対応しないサーバーでは例外。

    注意: アセット URL は GitHub の署名付き URL へ 302 でリダイレクトされる。
    そこへ API トークンを付けたまま 따르면 401 になるため、**アセット取得では認証ヘッダを送らない**
    (トークンは api.github.com に対してだけ使う)。
    """
    status, data = _open(url, None, start, end)
    if status not in (200, 206):
        raise RuntimeError("範囲取得に失敗しました: HTTP {0}".format(status))
    return data


def remote_size(url: str, token: str | None) -> int:
    req = urllib.request.Request(url, method="HEAD")
    req.add_header("User-Agent", USER_AGENT)
    with urllib.request.urlopen(req, timeout=120) as resp:
        length = resp.headers.get("Content-Length")
        if not length:
            raise RuntimeError("Content-Length が取れません: {0}".format(url))
        return int(length)


# ── ZIP central directory 解析 ──────────────────────────────────────────
def parse_central_directory(url: str, token: str | None, size: int) -> list[dict[str, Any]]:
    """ZIP 末尾の central directory だけを範囲取得してエントリ一覧を返す。

    戻り値: [{"name": str, "size": int (非圧縮), "compressed": int}]
    """
    tail_len = min(size, 65536)
    tail = fetch_range(url, token, size - tail_len, size - 1)

    eocd = tail.rfind(b"PK\x05\x06")
    if eocd < 0:
        raise RuntimeError("ZIP の End of Central Directory が見つかりません")
    entry_count = struct.unpack_from("<H", tail, eocd + 10)[0]
    cd_size = struct.unpack_from("<I", tail, eocd + 12)[0]
    cd_offset = struct.unpack_from("<I", tail, eocd + 16)[0]

    if cd_offset == 0xFFFFFFFF or entry_count == 0xFFFF:
        locator = tail.rfind(b"PK\x06\x07")
        if locator < 0:
            raise RuntimeError("ZIP64 locator が見つかりません")
        z64_offset = struct.unpack_from("<Q", tail, locator + 8)[0]
        z64 = fetch_range(url, token, z64_offset, z64_offset + 55)
        if z64[:4] != b"PK\x06\x06":
            raise RuntimeError("ZIP64 End of Central Directory が不正です")
        entry_count = struct.unpack_from("<Q", z64, 32)[0]
        cd_size = struct.unpack_from("<Q", z64, 40)[0]
        cd_offset = struct.unpack_from("<Q", z64, 48)[0]

    if cd_size == 0:
        return []

    cd = fetch_range(url, token, cd_offset, cd_offset + cd_size - 1)
    entries: list[dict[str, Any]] = []
    pos = 0
    while pos + 46 <= len(cd) and cd[pos:pos + 4] == b"PK\x01\x02":
        compressed = struct.unpack_from("<I", cd, pos + 20)[0]
        uncompressed = struct.unpack_from("<I", cd, pos + 24)[0]
        name_len, extra_len, comment_len = struct.unpack_from("<HHH", cd, pos + 28)
        name = cd[pos + 46:pos + 46 + name_len].decode("utf-8", "replace")
        entries.append({"name": name, "size": uncompressed, "compressed": compressed})
        pos += 46 + name_len + extra_len + comment_len
    if len(entries) != entry_count:
        print(
            "[warn] エントリ数が一致しません: zip={0} parsed={1}".format(entry_count, len(entries)),
            file=sys.stderr,
        )
    return entries


# ── 判定 ────────────────────────────────────────────────────────────────
def classify(entries: list[dict[str, Any]], threshold: int) -> dict[str, Any]:
    media_entries = [
        e for e in entries
        if os.path.splitext(e["name"])[1].lower() in MEDIA_EXTENSIONS
    ]
    resources = [e for e in entries if e["name"].replace("\\", "/").endswith("FlashBeat_Data/resources.resource")]
    resources_size = resources[0]["size"] if resources else None
    names = {e["name"].replace("\\", "/") for e in entries}

    has_version_json = "version.json" in names
    has_media_manifest = "flashbeat-media-policy.json" in names
    has_channel = any(n.endswith("StreamingAssets/update-channel.txt") for n in names)

    baked = resources_size is not None and resources_size > threshold
    verdict = "third-party-media" if (baked or media_entries) else "song-free"
    return {
        "entries": len(entries),
        "mediaEntries": media_entries,
        "resourcesResourceSize": resources_size,
        "hasVersionJson": has_version_json,
        "hasMediaPolicyManifest": has_media_manifest,
        "hasUpdateChannel": has_channel,
        "verdict": verdict,
    }


def audit_zip(url: str, token: str | None, threshold: int) -> dict[str, Any]:
    size = remote_size(url, token)
    entries = parse_central_directory(url, token, size)
    result = classify(entries, threshold)
    result["zipSize"] = size
    return result


# ── メイン ──────────────────────────────────────────────────────────────
def list_releases(repo: str, token: str | None) -> list[dict[str, Any]]:
    url = "https://api.github.com/repos/{0}/releases?per_page=100".format(repo)
    return api_get_json(url, token)


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="公開 Release の ZIP にメディアが同梱されているか調査する")
    parser.add_argument("--repo", default=DEFAULT_REPO, help="owner/repo (既定: {0})".format(DEFAULT_REPO))
    parser.add_argument("--tag", action="append", default=[], help="調査するタグ (複数可)。未指定なら全 Release")
    parser.add_argument("--threshold-bytes", type=int, default=DEFAULT_RESOURCES_THRESHOLD,
                        help="resources.resource がこのバイト数を超えたらメディア焼き込みと判定 (既定: 300MB)")
    parser.add_argument("--json", action="store_true", help="JSON で出力する")
    args = parser.parse_args(argv)

    token = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN") or None

    try:
        releases = list_releases(args.repo, token)
    except urllib.error.URLError as exc:
        print("Releases API の取得に失敗しました: {0}".format(exc), file=sys.stderr)
        return 2

    wanted = set(args.tag)
    report = []
    for release in releases:
        tag = release.get("tag_name")
        if wanted and tag not in wanted:
            continue
        if release.get("draft"):
            # draft は公開されていないので公開物監査の対象外。
            continue
        entry = {
            "tag": tag,
            "name": release.get("name"),
            "draft": release.get("draft"),
            "prerelease": release.get("prerelease"),
            "publishedAt": release.get("published_at"),
            "assets": [],
        }
        for asset in release.get("assets", []):
            item = {
                "name": asset.get("name"),
                "size": asset.get("size"),
                "downloadCount": asset.get("download_count"),
                "url": asset.get("browser_download_url"),
            }
            if item["name"].lower().endswith(".zip"):
                try:
                    item["audit"] = audit_zip(item["url"], token, args.threshold_bytes)
                except Exception as exc:  # 1 件の失敗で全体を止めない (調査は最後まで走らせる)
                    item["audit"] = {"error": str(exc)}
            entry["assets"].append(item)
        report.append(entry)

    if args.json:
        print(json.dumps(report, ensure_ascii=False, indent=2))
        return 0

    for entry in report:
        print("=== {0} ===".format(entry["tag"]))
        print("  name        : {0}".format(entry["name"]))
        print("  prerelease : {0}".format(entry["prerelease"]))
        print("  published   : {0}".format(entry["publishedAt"]))
        for asset in entry["assets"]:
            print("  asset       : {0} ({1:,} bytes, DL {2})".format(
                asset["name"], asset["size"] or 0, asset["downloadCount"]))
            audit = asset.get("audit")
            if not audit:
                continue
            if "error" in audit:
                print("      [error] {0}".format(audit["error"]))
                continue
            print("      verdict        : {0}".format(audit["verdict"]))
            print("      zip entries    : {0}".format(audit["entries"]))
            print("      resources.resource : {0}".format(
                "{0:,} bytes".format(audit["resourcesResourceSize"])
                if audit["resourcesResourceSize"] is not None else "(なし)"))
            print("      loose media    : {0}".format(
                ", ".join(m["name"] for m in audit["mediaEntries"]) if audit["mediaEntries"] else "0 件"))
            print("      version.json   : {0} / media manifest: {1} / update-channel: {2}".format(
                "あり" if audit["hasVersionJson"] else "なし",
                "あり" if audit["hasMediaPolicyManifest"] else "なし",
                "あり" if audit["hasUpdateChannel"] else "なし"))
        print("")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
