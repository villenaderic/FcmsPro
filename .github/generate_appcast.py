"""Builds docs/appcast.xml from a GitHub release.

Uses the release for the tag in $TAG (set by the workflow); falls back to
/releases/latest. Exits non-zero on any problem so a broken appcast can never
be silently committed. Set RELEASE_JSON_FILE to test offline.
"""
import json
import os
import sys
import time
import urllib.request
import xml.etree.ElementTree as ET
from datetime import datetime
from email.utils import formatdate

REPO = "villenaderic/FcmsPro"
SPARKLE_NS = "http://www.andymatuschak.org/xml-namespaces/sparkle"


def fetch_release():
    local = os.environ.get("RELEASE_JSON_FILE")
    if local:
        with open(local, encoding="utf-8") as f:
            return json.load(f)
    tag = os.environ.get("TAG", "").strip()
    path = f"tags/{tag}" if tag else "latest"
    req = urllib.request.Request(f"https://api.github.com/repos/{REPO}/releases/{path}")
    req.add_header("User-Agent", "FcmsPro-Appcast-Generator")
    req.add_header("Accept", "application/vnd.github+json")
    if os.environ.get("GITHUB_TOKEN"):
        req.add_header("Authorization", f"Bearer {os.environ['GITHUB_TOKEN']}")
    last = None
    for attempt in range(1, 6):  # the release can take a moment to be readable
        try:
            with urllib.request.urlopen(req, timeout=30) as r:
                return json.loads(r.read())
        except Exception as e:  # noqa: BLE001
            last = e
            print(f"attempt {attempt} failed: {e}")
            time.sleep(5 * attempt)
    sys.exit(f"Could not fetch release: {last}")


def os_for(name):
    # Only formats the updater can hand to the OS. The AppImage is for manual
    # downloads, so it is deliberately not listed (two Linux items of the same
    # version would make the pick ambiguous).
    if name.endswith(".exe"):
        return "windows"
    if name.endswith(".dmg"):
        return "macos"
    if name.endswith(".deb"):
        return "linux"
    return None


def sign_asset(url, signer, token=None):
    req = urllib.request.Request(url)
    req.add_header("User-Agent", "FcmsPro-Appcast-Signer")
    req.add_header("Accept", "application/octet-stream")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    for attempt in range(1, 4):
        try:
            with urllib.request.urlopen(req, timeout=60) as resp:
                content = resp.read()
            import base64
            sig = signer.sign(content)
            return base64.b64encode(sig).decode("ascii")
        except Exception as e:
            print(f"Signing download attempt {attempt} failed for {url}: {e}")
            time.sleep(3 * attempt)
    sys.exit(f"Could not download and sign {url}")


def main():
    data = fetch_release()
    version = data["tag_name"].lstrip("v")
    dt = datetime.fromisoformat(data["published_at"].replace("Z", "+00:00"))
    rfc_date = formatdate(dt.timestamp())
    body = "\n".join(
        l for l in (data.get("body") or "").split("\n") if not l.startswith("**Full Changelog**:")
    ).strip()

    priv_key_raw = os.environ.get("NETSPARKLE_PRIVATE_KEY", "").strip()
    signer = None
    if priv_key_raw:
        try:
            import base64
            from cryptography.hazmat.primitives.asymmetric import ed25519
            priv_bytes = base64.b64decode(priv_key_raw)
            signer = ed25519.Ed25519PrivateKey.from_private_bytes(priv_bytes)
            print("Loaded Ed25519 private key for signing appcast items.")
        except Exception as e:
            print(f"Warning: Failed to load NETSPARKLE_PRIVATE_KEY: {e}", file=sys.stderr)
    else:
        print("Notice: NETSPARKLE_PRIVATE_KEY not set; skipping sparkle:edSignature.")

    ET.register_namespace("sparkle", SPARKLE_NS)
    rss = ET.Element("rss", {"version": "2.0"})
    channel = ET.SubElement(rss, "channel")
    ET.SubElement(channel, "title").text = "FCMS Pro Updates"

    found = set()
    for asset in data.get("assets", []):
        os_name = os_for(asset["name"].lower())
        if not os_name or os_name in found:
            continue
        found.add(os_name)
        item = ET.SubElement(channel, "item")
        ET.SubElement(item, "title").text = f"v{version} ({os_name})"
        ET.SubElement(item, "pubDate").text = rfc_date
        ET.SubElement(item, "description").text = body
        enc = ET.SubElement(item, "enclosure")
        enc.set("url", asset["browser_download_url"])
        enc.set(f"{{{SPARKLE_NS}}}version", version)
        enc.set(f"{{{SPARKLE_NS}}}os", os_name)
        enc.set("length", str(asset["size"]))
        enc.set("type", "application/octet-stream")
        if signer is not None:
            sig_b64 = sign_asset(asset["browser_download_url"], signer, os.environ.get("GITHUB_TOKEN"))
            enc.set(f"{{{SPARKLE_NS}}}edSignature", sig_b64)
            print(f"Signed {asset['name']} ({os_name}): {sig_b64[:16]}...")

    if "windows" not in found:
        sys.exit(f"Release v{version} has no .exe asset - refusing to write appcast. Assets: "
                 f"{[a['name'] for a in data.get('assets', [])]}")
    print(f"v{version}: appcast items for {sorted(found)}")

    os.makedirs("docs", exist_ok=True)
    tree = ET.ElementTree(rss)
    ET.indent(tree, space="  ")
    tree.write("docs/appcast.xml", encoding="utf-8", xml_declaration=True)
    ET.parse("docs/appcast.xml")  # sanity check: must be well-formed
    print("docs/appcast.xml written")


if __name__ == "__main__":
    main()
