"""Offline secondary motion for the dyn_* bones (dreads, necklace chains, leg straps).

Every dyn joint is a verlet particle tied to its parent particle by a fixed length and pulled softly toward the place it would be if it simply
followed the body (so the dreads keep their shape but swing, lag and settle). Gravity is world-down; the result is converted back to joint
rotations (swing only, twist inherited from the head / chest / calf the chain hangs from) and written into the pose before baking."""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pose import *

G_UNITS = 9.81 * (BODY_H / 1.75)          # mesh units per second^2 at real-world scale
SUB = 4; FPS = 24.0

class HairSim:
    def __init__(self, gravity_scale=0.6):
        inv = {v: k for k, v in s_name.items()}; self.inv = inv
        self.dyn = [j for j in s_joint_list if inv[j].startswith('dyn_')]; ds = set(self.dyn)
        self.root = {}; self.child = {}; self.L = {}; self.par = {}; self.prm = {}
        for j in self.dyn:
            p = s_par[j]; self.par[j] = p
            if p in ds: self.L[j] = vlen(vsub(BP[j], BP[p])); self.root[j] = self.root[p]
            else: self.root[j] = j; self.anchor = None
        self.anchor_bone = {}
        for j in self.dyn:
            if s_par[j] not in ds: self.anchor_bone[j] = s_par[j]
        for j in self.dyn:
            for c in self.dyn:
                if s_par[c] == j and j not in self.child: self.child[j] = c
        for j in self.dyn:
            n = inv[j]
            if 'braid_fr' in n: prm = dict(k=0.10, damp=0.955, g=1.0, dev=1800)
            elif 'braid' in n: prm = dict(k=0.055, damp=0.96, g=1.0, dev=3200)
            elif 'necklace' in n: prm = dict(k=0.16, damp=0.97, g=0.6, dev=1300)
            else: prm = dict(k=0.07, damp=0.95, g=0.9, dev=1500)          # leg straps
            self.prm[j] = prm
        self.g = G_UNITS * gravity_scale
        self.x = None
    def anchor_of(self, j):
        while j in self.L: j = self.par[j]
        return self.anchor_bone[j]
    def apply(self, P, pel_abs):
        """Overwrite the dyn joints' world rotations in P with the simulated ones (P is the frame's pose dict)."""
        kin = fk_positions(P, pel_abs)
        if self.x is None:
            self.x = {j: list(kin[j]) for j in self.dyn}; self.xp = {j: list(kin[j]) for j in self.dyn}
        dt = 1.0 / FPS / SUB; x = self.x; xp = self.xp
        for _ in range(SUB):
            for j in self.dyn:
                if j not in self.L: x[j] = list(kin[j]); xp[j] = list(kin[j]); continue            # chain roots are rigid on the head / chest / calf
                prm = self.prm[j]; pj = x[j]; pp = xp[j]; k = kin[j]
                nx = [pj[0] + (pj[0] - pp[0]) * prm['damp'] + prm['k'] * (k[0] - pj[0]),
                      pj[1] + (pj[1] - pp[1]) * prm['damp'] - self.g * prm['g'] * dt * dt + prm['k'] * (k[1] - pj[1]),
                      pj[2] + (pj[2] - pp[2]) * prm['damp'] + prm['k'] * (k[2] - pj[2])]
                xp[j] = pj; x[j] = nx
            for j in self.dyn:                                                          # parents come before children in s_joint_list
                if j not in self.L: continue
                par = x[self.par[j]]; d = vsub(x[j], par); l = vlen(d) or 1.0
                x[j] = vadd(par, vmul(d, self.L[j] / l))
                dev = vsub(x[j], kin[j]); dl = vlen(dev)
                if dl > self.prm[j]['dev']: x[j] = vadd(kin[j], vmul(dev, self.prm[j]['dev'] / dl))
        for j in self.dyn:
            anc = self.anchor_of(j); Ph = P[anc]
            c = self.child.get(j)
            if c is None:
                P[j] = P[self.par[j]] if j in self.L else Ph; continue
            d_bind = vnorm(vsub(BP[c], BP[j])); d_rest = mv(Ph, d_bind); d_sim = vnorm(vsub(x[c], x[j]))
            P[j] = m3mul(rmin(d_rest, d_sim), Ph)
