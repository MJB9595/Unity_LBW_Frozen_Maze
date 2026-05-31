"""Compare player forward vs camera->player vector to confirm camera is behind."""
import json, math, sys

P = json.loads(json.load(open('/tmp/_p.json'))['result']['contents'][0]['text'])['data']
C = json.loads(json.load(open('/tmp/_c.json'))['result']['contents'][0]['text'])['data']

p = P['transform']['position']
c = C['transform']['position']
# Player rotation as quaternion (Unity's world rotation)
qx = P['transform']['rotation']['x']
qy = P['transform']['rotation']['y']
qz = P['transform']['rotation']['z']
qw = P['transform']['rotation'].get('w', 1.0)

# Quaternion -> Forward vector (Unity convention: forward = (0,0,1) rotated by q)
# v' = q * v * q^-1, with v = (0,0,1)
# Standard formula:
fx = 2 * (qx*qz + qw*qy)
fy = 2 * (qy*qz - qw*qx)
fz = 1 - 2 * (qx*qx + qy*qy)
flen = math.sqrt(fx*fx + fz*fz)
if flen > 0:
    fx_h = fx / flen
    fz_h = fz / flen
else:
    fx_h = 0; fz_h = 1

# camera -> player horizontal direction
dx = p['x'] - c['x']
dz = p['z'] - c['z']
horiz = math.sqrt(dx*dx + dz*dz)
if horiz > 0:
    cx_h = dx / horiz
    cz_h = dz / horiz
else:
    cx_h = 0; cz_h = 0

# dot product: 1.0 means camera is exactly behind (camera->player matches player forward)
dot = fx_h * cx_h + fz_h * cz_h
angle_deg = math.degrees(math.acos(max(-1, min(1, dot))))

dy = c['y'] - p['y']
print(f"plyr pos = ({p['x']:7.2f}, {p['y']:6.2f}, {p['z']:7.2f})")
print(f"cam  pos = ({c['x']:7.2f}, {c['y']:6.2f}, {c['z']:7.2f})")
print(f"horiz distance = {horiz:.2f}m  height diff = {dy:.2f}m")
print(f"plyr forward(horiz)   = ({fx_h:+.3f}, {fz_h:+.3f})")
print(f"cam->plyr  (horiz)    = ({cx_h:+.3f}, {cz_h:+.3f})")
print(f"dot = {dot:+.3f}  angle = {angle_deg:.1f}deg  (0=perfectly behind, 180=in front)")
