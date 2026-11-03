with open(r"TradeStorm.Api/wwwroot/create.html", "r", encoding="utf-8") as f:
    text = f.read()

assert 'id="serviceConfigBox"' in text
assert 'id="serviceRateType"' in text
assert 'id="serviceFormat"' in text
assert 'id="mapSearchInput"' in text
assert 'id="map"' in text
assert 'id="location"' in text
assert 'id="serviceIsOnlineOnly"' in text
assert 'specFloor' not in text
assert 'specElevator' not in text
print("All elements verified in create.html!")
