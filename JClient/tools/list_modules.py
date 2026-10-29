"""Builds MODULES.txt from the mod registrations in src/. Usage: python tools/list_modules.py [extra_output_path ...]"""
import re, sys, pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
ORDER = ["Movement", "Visuals", "Tag", "Utilities", "Room", "Cosmetics", "Safety", "World FX", "Rig", "Fun", "Soundboard", "Master", "Events", "Settings"]
# helper -> fixed category (None = use the file's `const string c`)
CALL = re.compile(r'((?:Mods\.[TBC]|Follow|HudLine)\((c|"[^"]+")|Pool\(|Attached\(|Spawner\(|Setting\(|Pose\(|Mood\()\s*,?\s*"([^"]+)"')
FIXED = {"Pool(": "Visuals", "Attached(": "Cosmetics", "Spawner(": "World FX", "Setting(": "Settings", "Pose(": "Rig", "Mood(": "World FX"}


def parse(path):
    text = path.read_text(encoding="utf-8")
    const = re.search(r'const string c = "([^"]+)"', text)
    matches = list(CALL.finditer(text))
    for i, m in enumerate(matches):
        head = m.group(1)
        if "static " in text[max(0, m.start() - 40):m.start()]:
            continue  # helper definitions, not registrations
        cat = FIXED.get(head) or (m.group(2).strip('"') if m.group(2) and m.group(2) != "c" else const.group(1) if const else None)
        if cat in (None, "Keyboard"):
            continue
        seg = text[m.end():matches[i + 1].start() if i + 1 < len(matches) else len(text)]
        stmt = re.sub(r'Mods\.Get\("[^"]*"\)', "", seg).replace("\\\\", "\\")  # everything up to the next registration
        infos = re.findall(r'\.Info\("([^"]*)"\)', stmt) or re.findall(r'"([^"]*)"\)', stmt)
        info = infos[-1] if infos else ""
        opts = re.search(r'\.Opts\(("[^)]*)\)', stmt)
        opts = re.findall(r'"([^"]*)"', opts.group(1)) if opts else []
        kind = "button" if head.startswith(("Mods.B", "Setting(")) else "cycle" if head.startswith("Mods.C") else "toggle"
        name = m.group(3)
        if seg.lstrip().startswith("+"):
            name = name.strip() + " ..."  # name built in a loop (waypoints, sounds, modes): listed once
        others = ".OthersOnly()" in stmt or text[max(0, m.start() - 2):m.start()] == "M("
        yield cat, name, info, opts, others, kind


def main():
    seen, mods = set(), []
    for x in (x for f in sorted((ROOT / "src").rglob("*.cs")) for x in parse(f)):
        if x[1].endswith(" ...") and (x[0], x[1]) in seen: continue
        seen.add((x[0], x[1])); mods.append(x)
    assert len({(c, n) for c, n, *_ in mods}) == len(mods), "duplicate module names"
    assert len(mods) >= 100, f"only {len(mods)} modules"
    out = [f"J CLIENT - {len(mods)} modules", "Desktop: Q opens the menu. VR: hold left Y (or right B if 'Menu On Right Hand').",
           "Guns: VR right grip aims + right trigger fires; desktop hold right mouse + left click.",]
    for cat in ORDER:
        rows = [m for m in mods if m[0] == cat]
        out.append(f"== {cat.upper()} ({len(rows)}) ==")
        for _, name, info, opts, others, kind in rows:
            line = f"  {name}{' *' if others else ''}  [{kind}]"
            if info: line += f" - {info}"
            if opts: line += f"  (options: {' / '.join(opts[1:])})"
            out.append(line)
        out.append("")
    text = "\n".join(out)
    for p in [ROOT / "MODULES.txt", *map(pathlib.Path, sys.argv[1:])]:
        p.write_text(text, encoding="utf-8")
        print("wrote", p)
    print(len(mods), "modules")


if __name__ == "__main__":
    main()
