with open(r"TradeStorm.Api/wwwroot/create.html", "r", encoding="utf-8") as f:
    text = f.read()

import re
old_spec_gather = re.search(r'// Збираємо специфікації послуги.*?if \(specs\.length > 0\) \{.*?\}', text, re.DOTALL)
if old_spec_gather:
    text = text[:old_spec_gather.start()] + text[old_spec_gather.end():]
    with open(r"TradeStorm.Api/wwwroot/create.html", "w", encoding="utf-8") as f:
        f.write(text)
    print("Cleaned up spec gathering in create.html")
