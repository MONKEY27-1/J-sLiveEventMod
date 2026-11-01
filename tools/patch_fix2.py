def rd(f): return open(f, encoding='utf-8').read()
def wr(f, s): open(f, 'w', encoding='utf-8').write(s)
def rep(s, a, n, cnt=1):
    assert a in s, 'missing: ' + a[:110]
    return s.replace(a, n, cnt)

# ================================================================ Blender: end21_world.py
w = rd('tools/end21_world.py')
w = rep(w, "    bx(cx, y0 - 0.15, cz, w + 0.6, 0.3, d + 0.6, floor)\n", "    ROOMLOG.append((cx, cz, w, d, h, y0))\n    bx(cx, y0 - 0.15, cz, w + 0.6, 0.3, d + 0.6, floor)\n")
w = rep(w, "NOCEIL = bool(os.environ.get('NOCEIL'))", "ROOMLOG = []\nNOCEIL = bool(os.environ.get('NOCEIL'))")
helpers = '''
import random as _rnd, mathutils as _mu
def rbox(x, y, z, sx, sy, sz, rot, m):
    """a rotated box (unity centre x, y, z; size width sx, height sy, depth sz; euler rot in blender axes)"""
    c = Bz(x, y, z); s = (sx, sz, sy); e = _mu.Euler(rot, 'XYZ').to_matrix(); P = {}
    for ix in (-1, 1):
        for iy in (-1, 1):
            for iz in (-1, 1):
                v = e @ _mu.Vector((ix * s[0] / 2, iy * s[1] / 2, iz * s[2] / 2)); P[(ix, iy, iz)] = (c[0] + v.x, c[1] + v.y, c[2] + v.z)
    for ax in range(3):
        for sg in (-1, 1):
            o1, o2 = [a for a in range(3) if a != ax]; q = []
            for (u, vv) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                k = [0, 0, 0]; k[ax] = sg; k[o1] = u; k[o2] = vv; q.append(P[tuple(k)])
            b.quad(q[0], q[1], q[2], q[3], m, [(0, 0), (1, 0), (1, 1), (0, 1)]); b.quad(q[3], q[2], q[1], q[0], m, [(0, 1), (1, 1), (1, 0), (0, 0)])
def _d(a): return math.radians(a)
def wreck_rooms(seed, k=1.0):
    """the Seven are tearing the island over: every box room gets hanging ceiling panels, fallen beams and rubble along the walls (never in the middle lane), scorch marks, glowing wall breaches and leaning pillars"""
    for ri, (cx, cz, w_, d_, h_, y0) in enumerate(list(ROOMLOG)):
        R = _rnd.Random(seed * 100 + ri); alongz = d_ >= w_; L = max(w_, d_); hw = min(w_, d_) / 2
        def P(u, lat): return (cx + lat, cz + u) if alongz else (cx + u, cz + lat)
        for i in range(int(L / 4 * k) + 1):      # hanging ceiling panels
            u = R.uniform(-L / 2 + 1, L / 2 - 1); lat = R.uniform(-hw + 0.8, hw - 0.8); x, z = P(u, lat)
            rbox(x, y0 + h_ - R.uniform(0.35, 0.8), z, R.uniform(1.2, 2.4), 0.12, R.uniform(1.0, 2.0), (_d(R.uniform(10, 38)) * R.choice((-1, 1)), _d(R.uniform(-15, 15)), _d(R.uniform(0, 180))), R.choice((MS, MD, MWR)))
        for i in range(int(L * 1.3 * k) + 2):    # rubble along both walls
            side = R.choice((-1, 1)); u = R.uniform(-L / 2 + 0.5, L / 2 - 0.5); lat = side * R.uniform(hw - 1.4, hw - 0.35); s = R.uniform(0.15, 0.7); x, z = P(u, lat)
            rbox(x, y0 + s * 0.35, z, s, s * R.uniform(0.6, 1.2), s * 0.8, (_d(R.uniform(0, 35)), _d(R.uniform(0, 35)), _d(R.uniform(0, 180))), R.choice((MS, MD, MWB, MWR)))
        for i in range(int(L / 6 * k) + 1):      # fallen beams
            side = R.choice((-1, 1)); u = R.uniform(-L / 2 + 1.5, L / 2 - 1.5); lat = side * (hw - 0.7); x, z = P(u, lat)
            rbox(x, y0 + 0.3, z, 0.3, 0.3, R.uniform(2.2, 4.0), (_d(R.uniform(-6, 6)), 0, _d(R.uniform(-12, 12) + (90 if not alongz else 0))), MD)
        for i in range(int(L / 5 * k) + 1):      # scorch marks
            u = R.uniform(-L / 2 + 1, L / 2 - 1); lat = R.uniform(-hw + 0.6, hw - 0.6); x, z = P(u, lat); rbox(x, y0 + 0.02, z, R.uniform(1.2, 3.0), 0.02, R.uniform(1.0, 2.2), (0, 0, _d(R.uniform(0, 180))), MD)
        for i in range(int(L / 8 * k) + 1):      # glowing breaches in the walls
            side = R.choice((-1, 1)); u = R.uniform(-L / 2 + 1.5, L / 2 - 1.5); wd = R.uniform(1.0, 2.0); ht = R.uniform(0.9, 1.8); yy = y0 + R.uniform(1.0, 2.0)
            xa, za = P(u, side * (hw - 0.2)); xb, zb = P(u, side * (hw - 0.26))
            if alongz: rbox(xa, yy, za, 0.1, ht + 0.3, wd + 0.3, (0, 0, 0), LA); rbox(xb, yy, zb, 0.06, ht, wd, (0, 0, 0), MD)
            else: rbox(xa, yy, za, wd + 0.3, ht + 0.3, 0.1, (0, 0, 0), LA); rbox(xb, yy, zb, wd, ht, 0.06, (0, 0, 0), MD)
        for i in range(int(L / 10 * k) + 1):     # leaning pillars
            side = R.choice((-1, 1)); u = R.uniform(-L / 2 + 1, L / 2 - 1); x, z = P(u, side * (hw - 0.8)); lean = _d(R.uniform(8, 18)) * (1 if (side > 0) == alongz else -1)
            rbox(x, y0 + h_ / 2 - 0.1, z, 0.35, h_ - 0.2, 0.35, (0, lean, 0) if alongz else (lean, 0, 0), MD)
def wreck_round(cx, cz, Rr, h_, n, seed, skip=None):
    R = _rnd.Random(seed)
    for i in range(n):                           # rubble round the wall
        a = R.uniform(0, 360)
        if skip is not None and skip(a % 360): continue
        r = Rr - R.uniform(0.5, 3.2); s = R.uniform(0.2, 1.1); rbox(cx + math.cos(_d(a)) * r, s * 0.35, cz + math.sin(_d(a)) * r, s, s * R.uniform(0.6, 1.2), s * 0.8, (_d(R.uniform(0, 35)), _d(R.uniform(0, 35)), _d(R.uniform(0, 180))), R.choice((MS, MD, MWP, MCR)))
    for i in range(int(n / 5)):                   # hanging ceiling slabs
        a = R.uniform(0, 360); r = Rr * R.uniform(0.2, 0.92); rbox(cx + math.cos(_d(a)) * r, h_ - R.uniform(0.6, 1.6), cz + math.sin(_d(a)) * r, R.uniform(2.0, 4.5), 0.18, R.uniform(1.6, 3.5), (_d(R.uniform(12, 40)) * R.choice((-1, 1)), _d(R.uniform(-20, 20)), _d(R.uniform(0, 180))), R.choice((MS, MD)))
    for i in range(int(n / 9) + 2):               # long beams fallen from the wall to the floor
        a = R.uniform(0, 360)
        if skip is not None and skip(a % 360): continue
        r = Rr - 2.5; rbox(cx + math.cos(_d(a)) * r, 1.6, cz + math.sin(_d(a)) * r, 0.4, 0.4, R.uniform(4, 8), (_d(R.uniform(35, 55)), 0, _d(a + 90)), MD)
    for i in range(int(n / 6) + 2):               # scorch marks
        a = R.uniform(0, 360); r = Rr * R.uniform(0.2, 0.9); rbox(cx + math.cos(_d(a)) * r, 0.03, cz + math.sin(_d(a)) * r, R.uniform(2, 5), 0.02, R.uniform(1.6, 3.5), (0, 0, _d(R.uniform(0, 180))), MD)
# the I.O. complex is tilted: the Seven are heaving the island over above it. Walls, ceilings and fittings lean sideways (shear x += k * height), floors stay flat so nobody walks on a slope
SHEAR = {'hall': 0.05, 'gyro': 0.06, 'base': 0.07}.get(kind, 0.0)
def shear_all():
    if SHEAR == 0: return
    for o in bpy.data.objects:
        if o.type == 'MESH' and o.name in ('Hall', 'Gyro', 'Base'):
            for v in o.data.vertices: v.co.x += SHEAR * max(0.0, v.co.z)
        else: o.location.x += SHEAR * o.location.z
_export0 = export
def export(out_): shear_all(); _export0(out_)
'''
w = rep(w, "WPS = []\ndef wp(x, y, z)", helpers + "WPS = []\ndef wp(x, y, z)")
# hall
w = rep(w, "    b.build('Hall')", "    wreck_rooms(11, 1.1)\n    b.build('Hall')")
# gyro: corridors, no cones, wreck
w = rep(w, "    b.cyl(Bz(0, 6.6, 7.0), 0.05, 2.8, MCR2, 8, r2=1.7); b.cyl(Bz(0, 9.4, 7.0), 1.7, 2.8, MCR, 8, r2=0.05)      # the Zero Point crystal at the centre of the ring (two cones)\n", "")
w = rep(w, "    wp(0, 0, -6); wp(0, 0, 1.5)\n    b.build('Gyro')", """    for sgn in (-1, 1):      # the two corridors the guards come down (door frames at x = +-15); a closed far end with a small doorway
        room(sgn * 27.8, 0, 25, 10, 5.0, MWB if sgn > 0 else MWR, [('xm' if sgn > 0 else 'xp', 0, 10.0, 4.0), ('xp' if sgn > 0 else 'xm', 0, 4.0, 3.0)], strip=LC if sgn > 0 else LR)
    wreck_round(0, 0, R, 16.0, 80, 7, lambda d: min(d, 360 - d) < 20 or abs(d - 180) < 20); wreck_rooms(12, 1.3)
    wp(0, 0, -6); wp(0, 0, 1.5)
    b.build('Gyro')""")
# base
w = rep(w, "    b.build('Base')\n    # the glass dome", "    wreck_rooms(13, 0.9); wreck_round(ccx, ccz, CR, 31.0, 90, 17, lambda d: abs(d - 270) < 12)\n    b.build('Base')\n    # the glass dome")
wr('tools/end21_world.py', w)

# ================================================================ chair + legs
r = rd('tools/end21_rooms.py')
r = rep(r, "bx(0, 0.42, 3.15, 0.9, 0.1, 0.9, D_, 1.0, top=S_)", "bx(0, 0.42, 3.22, 0.9, 0.1, 0.76, D_, 1.0, top=S_)")
wr('tools/end21_rooms.py', r)
a = rd('tools/anim_human2.py')
a = rep(a, "pel=(0, -0.46, -0.24), fl=(FX, 0, 0.19), fr=(-FX, 0, 0.19)", "pel=(0, -0.46, -0.24), fl=(FX, 0, 0.5), fr=(-FX, 0, 0.5)")
wr('tools/anim_human2.py', a)

# ================================================================ C#
c = rd('AstronomicalEnd21c.cs'); b = rd('AstronomicalEnd21b.cs')
c = rep(c, "doorX = side * 13.9f, run = 2.2f + 0.1f * (k % 3);", "run = 2.2f + 0.1f * (k % 3), arrive = t0 + run, ts = Mathf.Max(arrive - 6.2f, T_GYRO + 0.05f), doorX = side * (Mathf.Abs(tx) + Mathf.Min(30f, (arrive - ts) * 4.9f));      // they run down the corridor (30 m at 4.9 m/s)")
c = rep(c, "-1f, t0 - 0.1f, t0 + run + 7.2f); if (g == null) continue;", "-1f, ts - 0.05f, t0 + run + 7.2f); if (g == null) continue;")
c = rep(c, 'Wp(g, t0, doorX, Y_GYRO, tz * 0.5f, yw, "Run");', 'Wp(g, ts, doorX, Y_GYRO, tz, yw, "Run");')
# the Zero Point itself in the machinery chamber
b = rep(b, "            else if (zpM2 != null && zpM2.Pivot.activeSelf) zpM2.Pivot.SetActive(false);", """            else if (sc == Sc.Gyro && zpM2 != null)      // 5:56 - 6:40: the Zero Point hangs in the middle of the machine's ring (the ring is at height 8, 7 m in front of the desk; the room leans 0.06 per metre of height)
            {
                if (!zpM2.Pivot.activeSelf) zpM2.Pivot.SetActive(true); var zt = zpM2.Pivot.transform; float surge = Sm(391.5f, 392.5f, t) * (1f - Sm(396f, 400f, t)); zt.localScale = Vector3.one * 3.0f * (1f + 0.06f * Mathf.Sin(t * 6f) + 0.35f * surge); zt.localRotation = Quaternion.Euler(0f, t * 22f, 0f);
                PlaceByFile(zpM2, new Vector3(GYRO_SHEAR * 8f, Y_GYRO + 8f, 7f)); if (zpR2 != null) zpR2.Pose(FindClip(zpM2, "Animation"), t);
            }
            else if (zpM2 != null && zpM2.Pivot.activeSelf) zpM2.Pivot.SetActive(false);""")
b = rep(b, "Y_PLAT_OFF = 0.7f,", "Y_PLAT_OFF = 0.7f, GYRO_SHEAR = 0.06f,")
# embers / smoke in the wrecked complex
c = rep(c, "            OceanFx(t, tp, dt, sc, cam);", """            if ((sc == Sc.Hall || sc == Sc.Gyro || sc == Sc.Base) && dt > 0f)      // the island is being heaved over: dust and embers fall from the ceiling, the complex groans
            {
                Vector3 hp = HeadNow(); if (UnityEngine.Random.value < dt * 24f) Emit(dust, hp + new Vector3(UnityEngine.Random.Range(-6f, 6f), UnityEngine.Random.Range(0.5f, 3f), UnityEngine.Random.Range(-6f, 6f)), new Vector3(0f, -UnityEngine.Random.Range(0.3f, 1.2f), 0f), new Color(0.5f, 0.45f, 0.45f, 0.35f), UnityEngine.Random.Range(0.12f, 0.5f), UnityEngine.Random.Range(2f, 4.5f));
                if (UnityEngine.Random.value < dt * 14f) Emit(fx, hp + new Vector3(UnityEngine.Random.Range(-7f, 7f), UnityEngine.Random.Range(-0.3f, 2.5f), UnityEngine.Random.Range(-7f, 7f)), new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), UnityEngine.Random.Range(0.6f, 2f), UnityEngine.Random.Range(-0.4f, 0.4f)), new Color(1f, UnityEngine.Random.Range(0.4f, 0.7f), 0.12f, 0.9f), UnityEngine.Random.Range(0.04f, 0.12f), UnityEngine.Random.Range(1.2f, 2.8f));
                if (UnityEngine.Random.value < dt * 0.35f) { shake = Mathf.Max(shake, 0.45f); Sfx(sRumble, hp, 0.5f, 1.1f, 2f); }
            }
            OceanFx(t, tp, dt, sc, cam);""")
wr('AstronomicalEnd21c.cs', c); wr('AstronomicalEnd21b.cs', b)
print('ok')
