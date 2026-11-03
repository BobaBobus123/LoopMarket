with open(r"TradeStorm.Api/wwwroot/details.html", "r", encoding="utf-8") as f:
    lines = f.readlines()

for i in range(2700, 2860):
    if 0 <= i < len(lines):
        safe_l = lines[i].encode('ascii', 'replace').decode()
        print(f"{i+1}: {safe_l}", end="")
