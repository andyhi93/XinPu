import re
import glob

chars = set()

# 1. All .yarn dialogue files
for path in glob.glob("Assets/Dialogues/**/*.yarn", recursive=True):
    with open(path, encoding="utf-8") as f:
        text = f.read()
    for ch in text:
        if ord(ch) > 127:
            chars.add(ch)

# 2. All m_text: "..." values in the scene file (covers static UI/button labels), decoding \uXXXX escapes
scene_path = "Assets/Scenes/SampleScene.unity"
with open(scene_path, encoding="utf-8") as f:
    scene_text = f.read()

pattern = re.compile(r'm_text:\s*"((?:[^"\\]|\\.)*)"')
for m in pattern.finditer(scene_text):
    raw = m.group(1)
    try:
        decoded = raw.encode("utf-8").decode("unicode_escape").encode("latin1").decode("utf-8", errors="ignore")
    except Exception:
        decoded = raw
    for ch in decoded:
        if ord(ch) > 127:
            chars.add(ch)

print("Characters found in current game text:", len(chars))

# 3. 補上 Big5 Level 1（常用字）整個區段，讓圖集預先覆蓋大部分以後會用到的繁體字，
#    不用每次新增對話台詞都重新生成字體圖集。用 Python 內建的 big5 codec 解碼，
#    不是手動列表，所以結果可重現、不會抄錯字。
for lead in range(0xA4, 0xC7):
    for trail in list(range(0x40, 0x7F)) + list(range(0xA1, 0xFF)):
        try:
            ch = bytes([lead, trail]).decode("big5")
            if len(ch) == 1 and ch.isprintable():
                chars.add(ch)
        except UnicodeDecodeError:
            pass

print("Total after merging Big5 常用字:", len(chars))

# 寫進 Assets/ 底下，Unity 才會把它匯入成 TextAsset，Font Asset Creator 才能用
out_path = "Assets/Font/used_characters.txt"
with open(out_path, "w", encoding="utf-8") as f:
    f.write("".join(sorted(chars)))
print("Written to", out_path)
