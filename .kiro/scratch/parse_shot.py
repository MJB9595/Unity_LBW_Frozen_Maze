import json, sys, pathlib

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

chosen = None
for f in frames:
    try:
        o = json.loads(f)
    except Exception:
        continue
    if isinstance(o, dict) and "result" in o:
        chosen = o

if not chosen:
    print("no result frame"); sys.exit(0)

res = chosen["result"]
# content text
txt = None
for c in res.get("content", []):
    if c.get("type") == "text":
        txt = c.get("text")
print("TEXT:", (txt[:300] if txt else None))
sc = res.get("structuredContent", {})
print("SC KEYS:", list(sc.keys()))

# search for any base64-ish image field recursively
def walk(o, path=""):
    if isinstance(o, dict):
        for k, v in o.items():
            walk(v, path + "/" + k)
    elif isinstance(o, list):
        for i, v in enumerate(o):
            walk(v, path + f"[{i}]")
    elif isinstance(o, str):
        if len(o) > 500:
            print("LONGSTR at", path, "len", len(o))
            pathlib.Path("/tmp/meteor_b64.txt").write_text(o)

walk(res)
# also check content for image type
for c in res.get("content", []):
    print("CONTENT TYPE:", c.get("type"), "keys", list(c.keys()))
    if c.get("type") == "image":
        data = c.get("data")
        if data:
            pathlib.Path("/tmp/meteor_b64.txt").write_text(data)
            print("IMAGE data len", len(data), "mime", c.get("mimeType"))
