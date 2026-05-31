import json, sys
for label, f in [
    ('WallMerge', '.kiro/scratch/gobj_wallmerge.json'),
    ('ProjectorMovement', '.kiro/scratch/gobj_projmove.json'),
    ('RaySearch', '.kiro/scratch/gobj_raysearch.json'),
    ('Mario64Camera', '.kiro/scratch/gobj_mario64.json'),
    ('GapTrigger', '.kiro/scratch/gobj_gap.json'),
]:
    try:
        d = json.load(open(f))
    except Exception as e:
        print(f'{label}: error {e}')
        continue
    content = d.get('result', {}).get('content', [])
    if not content:
        print(f'{label}: empty result')
        continue
    txt = content[0].get('text', '')
    try:
        inner = json.loads(txt)
    except Exception:
        print(f'{label}: raw={txt[:200]}')
        continue
    data = inner.get('data', {}) or {}
    ids = data.get('instanceIDs', []) or []
    print(f'{label}: total={data.get("totalCount")} ids={ids[:30]}')
