import json
import urllib.request
import os
import hashlib
from datetime import datetime
import xml.etree.ElementTree as ET
from email.utils import formatdate

def generate():
    # Fetch latest release from GitHub API
    req = urllib.request.Request("https://api.github.com/repos/villenaderic/FcmsPro/releases/latest")
    req.add_header('User-Agent', 'FcmsPro-Appcast-Generator')
    token = os.environ.get("GITHUB_TOKEN")
    if token:
        req.add_header('Authorization', f'Bearer {token}')
    try:
        with urllib.request.urlopen(req) as response:
            data = json.loads(response.read())
    except Exception as e:
        print("Failed to fetch release:", e)
        return

    version = data['tag_name'].lstrip('v')
    pub_date = data['published_at']
    # Parse ISO date and convert to RFC 2822
    dt = datetime.fromisoformat(pub_date.replace('Z', '+00:00'))
    rfc_date = formatdate(dt.timestamp())

    rss = ET.Element("rss", version="2.0", xmlns_sparkle="http://www.andymatuschak.org/xml-namespaces/sparkle")
    channel = ET.SubElement(rss, "channel")
    title = ET.SubElement(channel, "title")
    title.text = "FCMS Pro Updates"
    
    item = ET.SubElement(channel, "item")
    item_title = ET.SubElement(item, "title")
    item_title.text = data['name']
    
    item_pub = ET.SubElement(item, "pubDate")
    item_pub.text = rfc_date
    
    sparkle_release = ET.SubElement(item, "sparkle:releaseNotesLink")
    sparkle_release.text = data['html_url']
    
    for asset in data['assets']:
        name = asset['name'].lower()
        url = asset['browser_download_url']
        size = str(asset['size'])
        
        # Determine OS
        os_name = "windows"
        if name.endswith(".dmg"):
            os_name = "macos"
        elif name.endswith(".deb") or name.endswith(".appimage"):
            os_name = "linux"
        elif not name.endswith(".exe"):
            continue
            
        enclosure = ET.SubElement(item, "enclosure")
        enclosure.set("url", url)
        enclosure.set("sparkle:version", version)
        enclosure.set("sparkle:os", os_name)
        enclosure.set("length", size)
        enclosure.set("type", "application/octet-stream")

    # Write to docs/appcast.xml
    os.makedirs("docs", exist_ok=True)
    tree = ET.ElementTree(rss)
    ET.indent(tree, space="  ", level=0)
    tree.write("docs/appcast.xml", encoding="utf-8", xml_declaration=True)
    print("appcast.xml generated successfully")

if __name__ == "__main__":
    generate()
