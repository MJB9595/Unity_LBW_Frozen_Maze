import json, pathlib

buf = pathlib.Path(".last_body").read_text(errors="replace")
frames = []
cur = []
for line in buf.splitlines():
    if line.startswith("data: "):
        cur.append(line[6:])
    elif line == "" and cur:
        frames.append("\n".join(cur)); cur = []
if cur:
    frames.append("\n".join(cur))

obj = None
for f in frames:
    try:
        o = json.loads(f)
    except Exception:
        continue
    if isinstance(o, dict) and "result" in o:
        obj = o

res = obj["result"]
sc = res.get("structuredContent", {})
data = sc.get("data", sc)
inner = data.get("result", data)
print(json.dumps(inner, indent=2, ensure_ascii=False))
