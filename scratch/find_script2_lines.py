with open(r"TradeStorm.Api/wwwroot/details.html", "r", encoding="utf-8") as f:
    html = f.read()

import re
scripts = re.findall(r'<script>(.*?)</script>', html, re.DOTALL)
s2 = scripts[1]
lines = s2.split('\n')
for idx in range(1060, 1120):
    if idx < len(lines):
        safe_l = lines[idx].encode('ascii', 'replace').decode()
        print(f"{idx+1}: {safe_l}")
