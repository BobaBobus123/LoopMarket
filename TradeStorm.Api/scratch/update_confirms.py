import os
import re

wwwroot = r"D:\LoopMarket\TradeStorm.Api\wwwroot"

for fname in ["about.html", "safety.html"]:
    fpath = os.path.join(wwwroot, fname)
    with open(fpath, "r", encoding="utf-8") as f:
        content = f.read()
    old_logout = 'if (confirm("Ви дійсно бажаєте вийти з облікового запису?"))'
    new_logout = 'const confirmed = await LoopDialog.confirm("Ви дійсно бажаєте вийти з облікового запису?", { title: "Вихід", confirmText: "Вийти" });\n            if (confirmed)'
    if old_logout in content:
        content = content.replace(old_logout, new_logout)
        content = re.sub(r'function logout\(\)\s*\{', 'async function logout() {', content)
        with open(fpath, "w", encoding="utf-8") as f:
            f.write(content)
        print(f"Updated {fname} logout to LoopDialog.confirm")

print("All confirms successfully modernized!")
