import os
import re

wwwroot = r"D:\LoopMarket\TradeStorm.Api\wwwroot"

replacements = [
    ("Студентський маркетплейс", "Маркетплейс оголошень"),
    ("студентського маркетплейсу", "маркетплейсу"),
    ("Тільки перевірені студенти", "Перевірені користувачі"),
    ("Студентські послуги", "Послуги та сервіси"),
    ("Студентська послуга", "Послуга від майстра"),
    ("студентську послугу", "послугу"),
    ("студентської послуги", "послуги"),
    ("Це ваша студентська послуга", "Це ваша послуга"),
    ("Створено для студентів", "Зручні та безпечні угоди"),
    ("Створено для студентів та безпечних угод", "Платформа зручних та безпечних онлайн-угод"),
    ("Безпечні студентські угоди", "Безпечні та надійні онлайн-угоди"),
    ("Послуги та робота від студентів", "Послуги та робота від перевірених виконавців"),
    ("студентам та користувачам", "покупцям та замовникам"),
]

for root, dirs, files in os.walk(wwwroot):
    for f in files:
        if f.endswith(".html") or f.endswith(".js"):
            fpath = os.path.join(root, f)
            with open(fpath, "r", encoding="utf-8") as file:
                content = file.read()
            
            modified = False
            for old, new in replacements:
                if old in content:
                    content = content.replace(old, new)
                    modified = True
            
            if modified:
                with open(fpath, "w", encoding="utf-8") as file:
                    file.write(content)
                print(f"Updated {f}")

print("Text neutralization complete!")
