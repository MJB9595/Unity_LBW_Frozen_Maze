"""Pretty-print key fields of MCP component snapshots."""
import json, sys

def load_inner(path):
    d = json.load(open(path))
    contents = d.get('result', {}).get('contents', [])
    if not contents:
        return None
    txt = contents[0].get('text', '')
    try:
        return json.loads(txt)
    except Exception:
        return txt

KEYS_OF_INTEREST = {
    'mario': ['lookAtHeight','distance','height','minDistance','positionSmoothTime',
              'rotationDamping','heightDamping','collisionRadius','collisionDamping',
              'recoveryDamping','teleportThreshold','fieldOfView','target'],
    'wallmerge': ['mergeMode','transitionTime','decalRenderingLayerIndex',
                  'decalMovement','frameQuad','gameCam','wallCam','dofVolume',
                  'zoomVolume','mario64Cam','frameLitColor'],
    'projmove': ['movSpeed','rotSpeed','rotationLerp','distanceToTurn','isActive',
                 'movementMode','rotationMode','isGoingRight','isMoving','player',
                 'pivot','lineRef1','lineRef2','mergeParticle','exitParticle'],
    'ray': ['stepSize','offsetMargin','checkCountMax','IsClosedLoop'],
}

def show(label, path, keys):
    print(f'\n=== {label} ({path}) ===')
    inner = load_inner(path)
    if inner is None:
        print('  (empty)')
        return
    if isinstance(inner, str):
        print(' raw:', inner[:300])
        return
    data = inner.get('data') or inner
    # data may have 'properties' or be flat
    props = data.get('properties') or data.get('component', {}).get('properties') or data
    if isinstance(props, dict):
        for k in keys:
            if k in props:
                v = props[k]
                if isinstance(v, dict):
                    name = v.get('name') or v.get('referenceName') or v.get('path') or v
                    print(f'  {k}: {name}')
                else:
                    print(f'  {k}: {v}')
    else:
        print('  props:', props)

show('Mario64Camera', '.kiro/scratch/comp_mario.json', KEYS_OF_INTEREST['mario'])
show('WallMerge', '.kiro/scratch/comp_wallmerge.json', KEYS_OF_INTEREST['wallmerge'])
show('ProjectorMovement', '.kiro/scratch/comp_projmove.json', KEYS_OF_INTEREST['projmove'])
show('RaySearch[0]', '.kiro/scratch/comp_ray1.json', KEYS_OF_INTEREST['ray'])
