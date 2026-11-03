with open(r"TradeStorm.Api/wwwroot/edit.html", "r", encoding="utf-8") as f:
    text = f.read()

import re
pattern = r'<div class="form-group" style="margin-bottom: 14px;">\s*<label style="font-size: 0.88rem; font-weight: 700; color: var\(--text-main\); margin-bottom: 6px;">Вид та специфіка робіт</label>.*?</select>\s*</div>'
text = re.sub(pattern, "", text, flags=re.DOTALL)

with open(r"TradeStorm.Api/wwwroot/edit.html", "w", encoding="utf-8") as f:
    f.write(text)
print("edit.html serviceKind stripped")
