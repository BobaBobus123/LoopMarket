import re

path = r"D:\LoopMarket\TradeStorm.Api\wwwroot\images\bg_tech_constellation.svg"
with open(path, "r", encoding="utf-8") as f:
    content = f.read()

# Remove solid fill rect and glow rects
content = re.sub(r'<rect width="100%" height="100%" fill="[^"]+"/>\s*', '', content)

with open(path, "w", encoding="utf-8") as f:
    f.write(content)

print("SVG successfully modernized to transparent background!")
