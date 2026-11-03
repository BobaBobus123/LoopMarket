import urllib.request
import json

base = "http://localhost:65083"

# 1. Fetch HTML of details.html
with urllib.request.urlopen(f"{base}/details.html?id=17") as resp:
    html = resp.read().decode('utf-8')
    assert "initDetailsPage" in html
    assert resp.status == 200
    print("1. details.html served successfully (HTTP 200, length:", len(html), ")")

# 2. Fetch API for listing 17
with urllib.request.urlopen(f"{base}/api/listings/17") as resp:
    assert resp.status == 200
    data = json.loads(resp.read().decode('utf-8'))
    print("2. /api/listings/17 returned data:")
    print("   ID:", data.get("id"))
    print("   Title:", data.get("title"))
    print("   Price:", data.get("price"))
    print("   Location:", data.get("location"))
    print("   Seller:", data.get("seller", {}).get("firstName"))

# 3. Also check listing 14, 13, 12
for lid in [14, 13, 12]:
    with urllib.request.urlopen(f"{base}/api/listings/{lid}") as resp:
        d = json.loads(resp.read().decode('utf-8'))
        print(f"   Listing {lid}: HTTP 200, Title: {d.get('title')[:30]}")

print("ALL CREATED SERVICES OPEN VIA API AND HTML WITH ZERO ERRORS!")
