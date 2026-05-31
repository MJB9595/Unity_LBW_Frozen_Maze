import json
files = [('RaySearch host 1', '.kiro/scratch/go_ray1.json'),
         ('RaySearch host 2', '.kiro/scratch/go_ray2.json'),
         ('RaySearch host 3', '.kiro/scratch/go_ray3.json'),
         ('RaySearch host 4', '.kiro/scratch/go_ray4.json'),
         ('GapTrigger host 1', '.kiro/scratch/go_gap1.json'),
         ('GapTrigger host 2', '.kiro/scratch/go_gap2.json')]
for label, f in files:
    try:
        d = json.load(open(f))
    except Exception as e:
        print(label, 'ERR', e); continue
    contents = d.get('result', {}).get('contents', [])
    if not contents: print(label, '(empty)'); continue
    inner = json.loads(contents[0]['text'])
    g = inner.get('data', {})
    name = g.get('name')
    path = g.get('path')
    parent = g.get('parent')
    pos = g.get('position') or g.get('transform',{}).get('position')
    layer = g.get('layer')
    tag = g.get('tag')
    active = g.get('activeSelf', g.get('active'))
    print(f'{label}: {name}  path={path}  layer={layer}  tag={tag}  active={active}  pos={pos}')
