import os
import re

files_to_update = [
    r"D:\LoopMarket\TradeStorm.Api\wwwroot\details.html",
    r"D:\LoopMarket\TradeStorm.Api\wwwroot\messages.html",
    r"D:\LoopMarket\TradeStorm.Api\wwwroot\profile.html",
    r"D:\LoopMarket\TradeStorm.Api\wwwroot\seller.html",
    r"D:\LoopMarket\TradeStorm.Api\wwwroot\docs.html"
]

replacements = [
    ("alert('Будь ласка, увійдіть в систему, щоб залишити відгук.');", "LoopToast.info('Будь ласка, увійдіть в систему, щоб залишити відгук.');"),
    ("alert('Відгук успішно додано!');", "LoopToast.success('Відгук успішно додано!');"),
    ("alert(err.error || 'Помилка при додаванні відгуку');", "LoopToast.error(err.error || 'Помилка при додаванні відгуку');"),
    ("alert('Помилка з\'єднання з сервером');", "LoopToast.error('Помилка з\'єднання з сервером');"),
    ('alert("Помилка при видаленні.");', 'LoopToast.error("Помилка при видаленні.");'),
    ('alert("Помилка з\'єднання з сервером.");', 'LoopToast.error("Помилка з\'єднання з сервером.");'),
    ("alert('Щоб оформити замовлення, увійдіть у свій акаунт.');", "LoopToast.info('Щоб оформити замовлення, увійдіть у свій акаунт.');"),
    ("alert('Ви не можете оформити замовлення на власний товар.');", "LoopToast.warning('Ви не можете оформити замовлення на власний товар.');"),
    ('alert("Адресу не знайдено. Спробуйте вказати місто або точнішу вулицю.");', 'LoopToast.warning("Адресу не знайдено. Спробуйте вказати місто або точнішу вулицю.");'),
    ('alert("Помилка пошуку адреси");', 'LoopToast.error("Помилка пошуку адреси");'),
    ("alert('Щоб оформити послугу, увійдіть у свій акаунт.');", "LoopToast.info('Щоб оформити послугу, увійдіть у свій акаунт.');"),
    ("alert('Ви не можете оформити замовлення на власну послугу.');", "LoopToast.warning('Ви не можете оформити замовлення на власну послугу.');"),
    ('alert("Дякуємо! Ваш відгук успішно додано.");', 'LoopToast.success("Дякуємо! Ваш відгук успішно додано.");'),
    ('alert("Дякуємо за оцінку!");', 'LoopToast.success("Дякуємо за оцінку!");'),
    ("alert('Дані профілю успішно збережено!');", "LoopToast.success('Дані профілю успішно збережено!');"),
    ("alert('Помилка при збереженні');", "LoopToast.error('Помилка при збереженні');"),
    ('alert("Помилка зв\'язку з сервером");', 'LoopToast.error("Помилка зв\'язку з сервером");'),
    ("alert('Ліміт вичерпано! Ви можете змінювати фото профілю не частіше 3 разів на місяць.');", "LoopToast.warning('Ліміт вичерпано! Ви можете змінювати фото профілю не частіше 3 разів на місяць.');"),
    ('alert("Помилка з\'єднання.");', 'LoopToast.error("Помилка з\'єднання.");'),
    ("alert('Будь ласка, увійдіть в систему.');", "LoopToast.info('Будь ласка, увійдіть в систему.');"),
    ('alert("Дякуємо! Відгук успішно додано.");', 'LoopToast.success("Дякуємо! Відгук успішно додано.");'),
    ('alert("Код скопійовано в буфер обміну!");', 'LoopToast.success("Код скопійовано в буфер обміну!");')
]

for fpath in files_to_update:
    if not os.path.exists(fpath):
        continue
    with open(fpath, "r", encoding="utf-8") as f:
        text = f.read()
    orig = text
    for old, new in replacements:
        text = text.replace(old, new)
    if text != orig:
        with open(fpath, "w", encoding="utf-8") as f:
            f.write(text)
        print(f"Updated {os.path.basename(fpath)}")

print("Done cleaning alerts.")
