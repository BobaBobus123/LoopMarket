path = r'd:\LoopMarket\TradeStorm.Api\Migrations\20261102104924_AddFavoritesAndIndexes.cs'
with open(path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

new_lines = []
skip = False
for line in lines:
    if 'migrationBuilder.AddColumn<int>(' in line and 'ListingId' in lines[lines.index(line)+1]:
        skip = True
    elif 'migrationBuilder.AddColumn<string>(' in line and 'Status' in lines[lines.index(line)+1]:
        skip = True
    elif skip and ');' in line:
        skip = False
        continue
    if not skip:
        new_lines.append(line)

with open(path, 'w', encoding='utf-8') as f:
    f.writelines(new_lines)

print('Migration cleaned')
