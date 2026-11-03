import urllib.request
import json

for lid in [17, 14, 13, 12]:
    try:
        req = urllib.request.Request(f"http://localhost:65083/api/listings/{lid}")
        with urllib.request.urlopen(req) as resp:
            data = json.loads(resp.read().decode())
            print(f"LID {lid}: HTTP {resp.status}, Title: {data.get('title')}, SellerId: {data.get('sellerId')}")
    except Exception as e:
        print(f"LID {lid}: ERROR {e}")
