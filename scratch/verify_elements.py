with open(r"TradeStorm.Api/wwwroot/details.html", "r", encoding="utf-8") as f:
    text = f.read()

assert 'id="servicePresetTitleDisplay"' in text
assert 'id="servicePresetDescDisplay"' in text
assert 'id="servicePresetAddressText"' in text
assert 'id="serviceFixedNoticeBox"' in text
assert 'id="serviceHourlyControlsBox"' in text
print("All elements verified in details.html!")
