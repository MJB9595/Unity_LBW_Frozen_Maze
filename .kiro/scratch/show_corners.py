"""Inspect cornerPoints data captured on each RaySearch in the scene."""
import json, subprocess, sys

# Tower RaySearch components
RAYSEARCH_IDS = [61972, 62684, 63476, 63042]
NAMES = {61972: 'Cube', 62684: 'Cube (1)', 63476: 'Cube (2)', 63042: 'TowerB (9)'}

for rid in RAYSEARCH_IDS:
    out = subprocess.run(
        ['./.kiro/scratch/mcp.sh', 'res', f'mcpforunity://scene/gameobject/{rid}/component/RaySearch'],
        capture_output=True, text=True
    ).stdout
    try:
        d = json.loads(out)
        text = d['result']['contents'][0]['text']
        inner = json.loads(text)
        props = inner['data']['component']['properties']
        cps = props.get('cornerPoints', [])
        mps = props.get('meshPoints', [])
        print(f"--- RaySearch on host {rid} ({NAMES.get(rid)}) ---")
        print(f"   stepSize={props.get('stepSize')}  meshPoints={len(mps)}  cornerPoints={len(cps)}")
        for i, cp in enumerate(cps[:30]):
            p = cp['position']; n = cp['normal']
            print(f"   [{i}] pos=({p['x']:7.2f},{p['y']:7.2f},{p['z']:7.2f})  normal=({n['x']:6.3f},{n['y']:6.3f},{n['z']:6.3f})")
        if len(cps) > 30:
            print(f"   ... ({len(cps)-30} more corners)")
        print()
    except Exception as e:
        print(f"host {rid}: error parsing -- {e}")
        print(out[:400])
