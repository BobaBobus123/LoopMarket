with open(r"TradeStorm.Api/wwwroot/details.html", "r", encoding="utf-8") as f:
    html = f.read()

import re
scripts = re.findall(r'<script>(.*?)</script>', html, re.DOTALL)
print(f"Found {len(scripts)} script tags")

for idx, s in enumerate(scripts):
    print(f"--- Script {idx+1} (length {len(s)}) ---")
    # Check brace matching
    stack = []
    lines = s.split('\n')
    for line_no, line in enumerate(lines, 1):
        for char in line:
            if char in '{[(':
                stack.append((char, line_no))
            elif char in '}])':
                if not stack:
                    print(f"Extra closing '{char}' at line {line_no}")
                else:
                    open_c, o_line = stack.pop()
                    if (open_c == '{' and char != '}') or (open_c == '[' and char != ']') or (open_c == '(' and char != ')'):
                        print(f"Mismatched '{char}' at line {line_no}, expected match for '{open_c}' from line {o_line}")
    if stack:
        print(f"Unclosed braces in script {idx+1}: {len(stack)} items:")
        for open_c, o_line in stack[-5:]:
            print(f"  Unclosed '{open_c}' from line {o_line}")
