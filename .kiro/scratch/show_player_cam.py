import json

def load(path):
    d = json.load(open(path))
    contents = d.get('result', {}).get('contents', [])
    if not contents: return None
    return json.loads(contents[0]['text'])

# Player position
p = load('.kiro/scratch/go_player.json')
data = p['data']
print('=== Jammo_Player ===')
print(f"  pos={data.get('position')}  rot={data.get('rotation') or data.get('eulerAngles')}")
print(f"  active={data.get('activeSelf')}  layer={data.get('layer')}  tag={data.get('tag')}")
print()

# Components on Main Camera
m = load('.kiro/scratch/comps_maincam.json')
print('=== Main Camera (62022) components ===')
for c in (m.get('data', {}) or {}).get('components', []):
    print(f"  - {c.get('type')} (idx={c.get('index')}, enabled={c.get('enabled')})")
print()

# Components on Jammo_Player
m = load('.kiro/scratch/comps_player.json')
print('=== Jammo_Player components ===')
for c in (m.get('data', {}) or {}).get('components', []):
    print(f"  - {c.get('type')} (enabled={c.get('enabled')})")
print()

m = load('.kiro/scratch/comps_proj.json')
print('=== ProjectorController components ===')
for c in (m.get('data', {}) or {}).get('components', []):
    print(f"  - {c.get('type')} (enabled={c.get('enabled')})")
