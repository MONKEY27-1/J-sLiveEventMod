using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstronomicalEvent
{
    // Extra eye candy: fireball explosions, energy pillars, stage pyro, asteroid ring, portal, aurora, confetti. All local, all pooled.
    internal sealed class AstronomicalSpectacle
    {
        GameObject go;
        Transform root;
        Vector3 anchor;
        Material plain;
        readonly List<UnityEngine.Object> own = new List<UnityEngine.Object>();
        Transform[] exT,
            exRing,
            plT,
            rocks,
            portal,
            aur;
        Material[] exM,
            exRingM,
            plM,
            rockM,
            portalM,
            aurM;
        float[] exAge,
            exSize,
            plAge,
            rAng,
            rSpd,
            rRad,
            rY,
            rSz;
        Transform portalRoot;
        Transform portalDisc;
        Material discM;
        ParticleSystem fire,
            confetti;
        Light boomLight;
        float boomI;
        int exNext,
            plNext;
        float pillarCool,
            zapT,
            rockVis,
            lastHue;
        Camera cam;
        AstronomicalDestruction destr;
        public GameObject Root => go;

        static Color Col(float h, float s, float v)
        {
            return Color.HSVToRGB(Mathf.Repeat(h, 1f), s, v);
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3 - 2 * x);
        }

        static void Tint(Material m, Color c, float a)
        {
            c.a = Mathf.Clamp01(a);
            m.color = c;
        }

        Material M(Texture tex)
        {
            var m = new Material(plain.shader) { color = Color.white };
            if (tex != null)
                m.mainTexture = tex;
            own.Add(m);
            return m;
        }

        Transform Quad(Transform parent, Material m, Vector3 lp, Vector3 ls)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = lp;
            g.transform.localScale = ls;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return g.transform;
        }

        Transform Prim(PrimitiveType t, Transform parent, Material m)
        {
            var g = GameObject.CreatePrimitive(t);
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return g.transform;
        }

        ParticleSystem MakePS(string n, Material m, int max)
        {
            var g = new GameObject(n);
            g.transform.SetParent(root, false);
            var ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            g.GetComponent<ParticleSystemRenderer>().sharedMaterial = m;
            return ps;
        }

        public void Init(Vector3 anchorPos, Material plainMat, Material glowMat, Texture ringTex, Texture nebula, AstronomicalDestruction d)
        {
            anchor = anchorPos;
            plain = plainMat;
            destr = d;
            cam = Camera.main;
            go = new GameObject("AstronomicalSpectacle");
            root = go.transform;
            var glowTex = glowMat.mainTexture;

            int n = 8;
            exT = new Transform[n];
            exRing = new Transform[n];
            exM = new Material[n];
            exRingM = new Material[n];
            exAge = new float[n];
            exSize = new float[n];
            for (int k = 0; k < n; k++)
            {
                exM[k] = M(glowTex);
                exRingM[k] = M(ringTex);
                exT[k] = Prim(PrimitiveType.Sphere, root, exM[k]);
                exT[k].gameObject.SetActive(false);
                exRing[k] = Quad(root, exRingM[k], Vector3.zero, Vector3.one);
                exRing[k].localRotation = Quaternion.Euler(90, 0, 0);
                exRing[k].gameObject.SetActive(false);
                exAge[k] = 9f;
            }
            plT = new Transform[5];
            plM = new Material[5];
            plAge = new float[5];
            for (int k = 0; k < 5; k++)
            {
                plM[k] = M(glowTex);
                var p = new GameObject("Pillar").transform;
                p.SetParent(root, false);
                for (int c = 0; c < 2; c++)
                    Quad(p, plM[k], new Vector3(0, 0.5f, 0), Vector3.one).localRotation = Quaternion.Euler(0, c * 90, 0);
                plT[k] = p;
                p.gameObject.SetActive(false);
                plAge[k] = 9f;
            }
            int rn = 36;
            rocks = new Transform[rn];
            rAng = new float[rn];
            rSpd = new float[rn];
            rRad = new float[rn];
            rY = new float[rn];
            rSz = new float[rn];
            rockM = new[] { M(null), M(null), M(null) };
            for (int k = 0; k < rn; k++)
            {
                rocks[k] = Prim(k % 3 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere, root, rockM[k % 3]);
                rAng[k] = UnityEngine.Random.value * 6.28f;
                rSpd[k] = UnityEngine.Random.Range(0.03f, 0.14f) * (k % 2 == 0 ? 1 : -1);
                rRad[k] = UnityEngine.Random.Range(40f, 70f);
                rY[k] = UnityEngine.Random.Range(14f, 40f);
                rSz[k] = UnityEngine.Random.Range(1.5f, 5f);
                rocks[k].gameObject.SetActive(false);
            }
            portalRoot = new GameObject("Portal").transform;
            portalRoot.SetParent(root, false);
            portal = new Transform[4];
            portalM = new Material[4];
            discM = M(null);
            discM.color = new Color(0, 0, 0, 0);
            portalDisc = Quad(portalRoot, discM, new Vector3(0, 0, 0.5f), Vector3.one * 20f);
            for (int k = 0; k < 4; k++)
            {
                portalM[k] = M(ringTex);
                portal[k] = Quad(portalRoot, portalM[k], Vector3.zero, Vector3.one * (95 - k * 16));
            }
            portalRoot.gameObject.SetActive(false);
            aur = new Transform[6];
            aurM = new Material[6];
            for (int k = 0; k < 6; k++)
            {
                aurM[k] = M(nebula);
                aurM[k].mainTextureScale = new Vector2(1.5f, 1f);
                float a = k * 1.0472f;
                aur[k] = Quad(root, aurM[k], anchor + new Vector3(Mathf.Cos(a) * 105f, 50f, Mathf.Sin(a) * 105f), new Vector3(120, 55, 1));
                aur[k].position = anchor + new Vector3(Mathf.Cos(a) * 105f, 50f, Mathf.Sin(a) * 105f);
                aur[k].rotation = Quaternion.LookRotation(new Vector3(-Mathf.Cos(a), 0, -Mathf.Sin(a)) * -1f); // quad front faces the arena
                aur[k].gameObject.SetActive(false);
            }
            fire = MakePS("Fire", glowMat, 3000);
            var fm = fire.main;
            fm.gravityModifier = 0.5f;
            fm.startLifetime = 1.6f;
            var fe = fire.emission;
            fe.enabled = false;
            confetti = MakePS("Confetti", plain, 1500);
            var cm = confetti.main;
            cm.startLifetime = 9f;
            cm.startSpeed = 0f;
            cm.gravityModifier = 0.12f;
            cm.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            var g = new Gradient();
            g.colorKeys = new[]
            {
                new GradientColorKey(Color.red, 0),
                new GradientColorKey(Color.yellow, 0.25f),
                new GradientColorKey(Color.cyan, 0.5f),
                new GradientColorKey(Color.magenta, 0.75f),
                new GradientColorKey(Color.green, 1),
            };
            g.alphaKeys = new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) };
            cm.startColor = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
            var cs = confetti.shape;
            cs.shapeType = ParticleSystemShapeType.Box;
            cs.scale = new Vector3(70, 1, 70);
            confetti.transform.position = anchor + Vector3.up * 40f;
            var ce = confetti.emission;
            ce.rateOverTime = 0;
            var rv = confetti.rotationOverLifetime;
            rv.enabled = true;
            rv.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            fire.Play();
            confetti.Play();
            var lg = new GameObject("BoomLight");
            lg.transform.SetParent(root, false);
            boomLight = lg.AddComponent<Light>();
            boomLight.type = LightType.Point;
            boomLight.range = 45f;
            boomLight.intensity = 0;
            boomLight.shadows = LightShadows.None;
            if (destr != null)
                destr.Boom = (p, s) => Boom(p, s, lastHue);
        }

        void Emit(Vector3 p, int n, Color c, float speed, float up, float size)
        {
            var ep = new ParticleSystem.EmitParams();
            for (int k = 0; k < n; k++)
            {
                ep.position = p + UnityEngine.Random.insideUnitSphere * 0.6f;
                ep.velocity = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(0.3f, 1f) * speed + Vector3.up * up;
                ep.startColor = c;
                ep.startSize = size * UnityEngine.Random.Range(0.5f, 1.3f);
                ep.startLifetime = UnityEngine.Random.Range(0.8f, 1.8f);
                fire.Emit(ep, 1);
            }
        }

        public void Boom(Vector3 p, float size, float hue)
        {
            if (go == null)
                return;
            AstronomicalHazards.Blast(p, size * 2.2f, size * 0.9f, size > 40f ? 22f : 13f);
            int k = exNext;
            exNext = (exNext + 1) % exT.Length;
            exAge[k] = 0;
            exSize[k] = size;
            exT[k].position = p + Vector3.up * size * 0.3f;
            exT[k].gameObject.SetActive(true);
            exRing[k].position = new Vector3(p.x, anchor.y + 0.15f, p.z);
            exRing[k].gameObject.SetActive(true);
            Emit(p, (int)(40 * AstronomicalSettings.Particles.Value + 20), new Color(1f, 0.6f, 0.15f), size * 1.2f, size * 0.6f, 1.0f);
            boomI = Mathf.Max(boomI, 6f + size * 0.5f);
            boomLight.transform.position = p + Vector3.up * 4f;
        }

        // Massive finale blast: one giant fireball plus a ring of smaller ones, huge shockwaves, light and sparks.
        public void Mega(Vector3 p, float hue)
        {
            if (go == null)
                return;
            Boom(p, 70f, hue);
            for (int k = 0; k < 5; k++)
            {
                float a = k * 1.2566f;
                Boom(p + new Vector3(Mathf.Cos(a) * 35f, 0, Mathf.Sin(a) * 35f), 35f + UnityEngine.Random.value * 15f, hue + k * 0.15f);
            }
            Emit(p + Vector3.up * 10f, 600, new Color(1f, 0.8f, 0.4f), 60f, 25f, 3f);
            boomI = 45f;
        }

        void Pillar(Vector3 p, float hue)
        {
            int k = plNext;
            plNext = (plNext + 1) % plT.Length;
            plAge[k] = 0;
            plT[k].position = p;
            plT[k].gameObject.SetActive(true);
            Tint(plM[k], Col(hue + k * 0.13f, 0.7f, 1f), 0.7f);
            Emit(p, 25, Col(hue, 0.6f, 1f), 6f, 22f, 0.7f);
        }

        Vector3 RandomGround(float rmin, float rmax)
        {
            float a = UnityEngine.Random.value * 6.28f,
                r = UnityEngine.Random.Range(rmin, rmax);
            return new Vector3(anchor.x + Mathf.Cos(a) * r, anchor.y, anchor.z + Mathf.Sin(a) * r);
        }

        public void Tick(
            float dt,
            float t,
            int i,
            float u,
            bool hit,
            float hue,
            float dens,
            float fx,
            float cosmic,
            float kick,
            float bass,
            float water
        )
        {
            if (go == null)
                return;
            lastHue = hue;
            if (cam == null)
                cam = Camera.main;
            pillarCool -= dt;
            zapT -= dt;

            // stage pyro + pillars + mines on the beat
            if (hit && i >= 1)
            {
                var sr = root; // corners relative to stage front (stage root is rotated, so use destr-independent world offsets around the anchor)
                for (int c = 0; c < 4; c++)
                    Emit(
                        anchor + new Vector3(c < 2 ? -8f : 8f, 0.6f, c % 2 == 0 ? -3f : 3f),
                        (int)(30 * dens),
                        Col(hue + c * 0.25f, 0.6f, 1f),
                        3f,
                        20f,
                        0.6f
                    );
            }
            if (hit && i >= 2 && pillarCool <= 0)
            {
                var p = RandomGround(14f, 48f);
                Pillar(p, hue);
                if (i == 6 || (i == 4 && UnityEngine.Random.value < 0.5f))
                    Boom(p, 9f, hue);
                pillarCool = 0.5f;
            }
            if (i == 4 && zapT <= 0 && destr != null)
            {
                destr.Zap(RandomGround(10f, 55f), hue);
                zapT = UnityEngine.Random.Range(0.7f, 1.8f);
            }

            // explosions
            for (int k = 0; k < exT.Length; k++)
            {
                if (exAge[k] > 1.2f)
                {
                    if (exT[k].gameObject.activeSelf)
                    {
                        exT[k].gameObject.SetActive(false);
                        exRing[k].gameObject.SetActive(false);
                    }
                    continue;
                }
                exAge[k] += dt;
                float f = exAge[k] / 1.2f;
                exT[k].localScale = Vector3.one * exSize[k] * (1f - Mathf.Exp(-exAge[k] * 7f)) * 2f;
                Tint(exM[k], Color.Lerp(new Color(1f, 0.95f, 0.7f), new Color(1f, 0.3f, 0.05f), f), (1 - f) * (1 - f) * 0.8f * fx);
                float rr = exSize[k] * 4f * f + 2f;
                exRing[k].localScale = new Vector3(rr * 2, rr * 2, 1);
                Tint(exRingM[k], Col(hue, 0.5f, 1f), (1 - f) * 0.9f * fx);
            }
            for (int k = 0; k < plT.Length; k++)
            {
                if (plAge[k] > 1.3f)
                {
                    if (plT[k].gameObject.activeSelf)
                        plT[k].gameObject.SetActive(false);
                    continue;
                }
                plAge[k] += dt;
                float f = plAge[k] / 1.3f;
                plT[k].localScale = new Vector3(7f * (1 - f * 0.7f), 90f * (1f - Mathf.Exp(-plAge[k] * 12f)), 7f * (1 - f * 0.7f));
                var c = plM[k].color;
                c.a = (1 - f) * 0.75f * fx;
                plM[k].color = c;
            }
            boomI = Mathf.Max(0f, boomI - dt * 18f);
            boomLight.intensity = boomI * AstronomicalSettings.Lighting.Value;

            // asteroid ring
            float want = (i >= 3 ? (i == 5 ? 0.6f : 1f) : 0f) * fx;
            rockVis = Mathf.Lerp(rockVis, want, 1f - Mathf.Exp(-dt * 1.2f));
            bool showRocks = rockVis > 0.02f;
            for (int k = 0; k < rocks.Length; k++)
            {
                if (rocks[k].gameObject.activeSelf != showRocks)
                    rocks[k].gameObject.SetActive(showRocks);
                if (!showRocks)
                    continue;
                rAng[k] += rSpd[k] * dt * (1f + bass);
                rocks[k].position =
                    anchor
                    + new Vector3(
                        Mathf.Cos(rAng[k]) * rRad[k],
                        rY[k] + Mathf.Sin(t * 0.5f + k) * 2f + water * 6f,
                        Mathf.Sin(rAng[k]) * rRad[k]
                    );
                rocks[k].rotation = Quaternion.Euler(t * (10 + k), t * (7 + k % 5), k * 20);
                rocks[k].localScale = Vector3.one * rSz[k] * rockVis * (1f + 0.25f * kick);
            }
            for (int k = 0; k < 3; k++)
                Tint(rockM[k], Col(hue + 0.12f * k, 0.55f, 0.55f + 0.45f * kick), 1f);

            // portal / black hole
            bool showPortal = i == 3 || i == 4 || i == 6;
            if (portalRoot.gameObject.activeSelf != showPortal)
                portalRoot.gameObject.SetActive(showPortal);
            if (showPortal && cam != null)
            {
                float pv = (i == 3 ? Smooth(u * 3) : 1f) * fx;
                portalRoot.position = anchor + new Vector3(-75f, 70f + Mathf.Sin(t * 0.3f) * 4f, 105f);
                portalRoot.rotation = Quaternion.LookRotation(portalRoot.position - cam.transform.position);
                for (int k = 0; k < 4; k++)
                {
                    portal[k].localRotation = Quaternion.Euler(0, 0, t * (25f + k * 22f) * (k % 2 == 0 ? 1 : -1));
                    portal[k].localScale = Vector3.one * (95 - k * 16) * (1f + 0.08f * kick + 0.04f * Mathf.Sin(t * 2 + k));
                    Tint(portalM[k], Col(hue + k * 0.12f, 0.8f, 1f), pv * (0.55f + 0.4f * kick));
                }
                Tint(discM, Color.black, pv * 0.95f);
            }

            // aurora curtains
            float av =
                (
                    i == 4 ? 1f
                    : i == 6 ? 0.8f
                    : i == 3 ? 0.4f
                    : 0f
                ) * fx;
            for (int k = 0; k < aur.Length; k++)
            {
                bool on = av > 0.02f;
                if (aur[k].gameObject.activeSelf != on)
                    aur[k].gameObject.SetActive(on);
                if (!on)
                    continue;
                aurM[k].mainTextureOffset = new Vector2(t * 0.02f * (k % 2 == 0 ? 1 : -1), Mathf.Sin(t * 0.3f + k) * 0.05f);
                Tint(aurM[k], Col(hue + k * 0.11f, 0.75f, 1f), av * (0.5f + 0.3f * Mathf.Sin(t * 0.6f + k)));
            }

            // confetti
            var ce = confetti.emission;
            ce.rateOverTime = i == 6 ? 170f * dens * fx : 0f;
            confetti.transform.position = anchor + Vector3.up * 40f;
        }

        public void Reset()
        {
            if (go == null)
                return;
            for (int k = 0; k < exT.Length; k++)
            {
                exAge[k] = 9f;
                exT[k].gameObject.SetActive(false);
                exRing[k].gameObject.SetActive(false);
            }
            for (int k = 0; k < plT.Length; k++)
            {
                plAge[k] = 9f;
                plT[k].gameObject.SetActive(false);
            }
            boomI = 0;
            rockVis = 0;
        }

        public void Dispose()
        {
            if (destr != null)
                destr.Boom = null;
            foreach (var o in own)
                if (o != null)
                    UnityEngine.Object.Destroy(o);
            own.Clear();
            if (go != null)
                UnityEngine.Object.Destroy(go);
            go = null;
        }
    }
}
