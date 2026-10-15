#!/usr/bin/env python3
"""Builds src/LanMessengerX.App/Assets/lang/<code>.json.

  keys          every English text the app shows: Loc.T("..") / Loc.F("..") in the C# code and {loc:T '..'} in the XAML
                (plus extra-keys.txt for texts that are produced in a way the scanner cannot see)
  translations  1. the translation files (.ts) of the original LAN Messenger, matched by their English source text
                2. manual/<code>.json - our own translations (they win), written for texts the original never had

Usage:  python3 build_lang.py [--ts <dir with the original *.ts files>] [--report]
"""
import json, os, re, sys, glob, html
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
APP = os.path.normpath(os.path.join(HERE, "..", "..", "src", "LanMessengerX.App"))
ANDROID = os.path.normpath(os.path.join(HERE, "..", "..", "..", "AndroidClient", "app", "src", "main", "kotlin"))
OUT = os.path.join(APP, "Assets", "lang")
DEFAULT_TS = os.path.normpath(os.path.join(HERE, "..", "..", "..", "legacy-qt", "Windows", "lmc", "src"))

LANGS = ["ar_SA", "bg_BG", "de_DE", "en_US", "es_ES", "fr_FR", "hu_HU", "it_IT", "ja_JP", "ko_KR", "ml_IN",
         "pl_PL", "pt_BR", "ro_RO", "ru_RU", "sk_SK", "sl_SI", "sv_SE", "tr_TR"]


def cs_literals(src):
    """All string literals inside Loc.T( ... ) / Loc.F( ... ) calls."""
    out = []
    for m in re.finditer(r"\bLoc\.[TFtf]\(", src):
        i, depth = m.end(), 1
        while i < len(src) and depth:
            c = src[i]
            if c == '"':
                j = i + 1
                buf = []
                while src[j] != '"':
                    if src[j] == "\\":
                        buf.append({"n": "\n", "t": "\t", '"': '"', "\\": "\\"}.get(src[j + 1], src[j + 1]))
                        j += 2
                    else:
                        buf.append(src[j]); j += 1
                out.append("".join(buf))
                i = j
            elif c == "(":
                depth += 1
            elif c == ")":
                depth -= 1
            i += 1
    return out


def collect_keys():
    keys = []
    for path in glob.glob(os.path.join(APP, "**", "*.cs"), recursive=True):
        if os.sep + "obj" + os.sep in path or os.sep + "bin" + os.sep in path:
            continue
        text = open(path, encoding="utf-8").read()
        keys += cs_literals(text)
        # help page helpers: H("heading"); P("paragraph");
        for m in re.finditer(r'^\s*[HP]\(("(?:[^"\\]|\\.)*")\);', text, re.M):
            keys.append(json.loads(m.group(1)))
    for path in glob.glob(os.path.join(ANDROID, "**", "*.kt"), recursive=True):
        keys += cs_literals(open(path, encoding="utf-8").read())
    for path in glob.glob(os.path.join(APP, "**", "*.axaml"), recursive=True):
        txt = open(path, encoding="utf-8").read()
        for m in re.finditer(r"\{loc:T '((?:[^'\\]|\\.)*)'\}", txt):
            keys.append(html.unescape(m.group(1)))
        for m in re.finditer(r"\{loc:T ([^'}][^}]*)\}", txt):
            keys.append(html.unescape(m.group(1).strip()))
    extra = os.path.join(HERE, "extra-keys.txt")
    if os.path.exists(extra):
        keys += [l.rstrip("\n") for l in open(extra, encoding="utf-8") if l.strip() and not l.startswith("#")]
    seen, ordered = set(), []
    for k in keys:
        if k and k not in seen and not re.fullmatch(r"[\W\d_]*", k):
            seen.add(k); ordered.append(k)
    return ordered


def norm(s):
    """Match key: no mnemonics, no trailing dots/colons, {0} == %1, case-insensitive."""
    s = re.sub(r"\{(\d+)\}", lambda m: "%" + str(int(m.group(1)) + 1), s)
    s = s.replace("&", "")
    s = s.strip().rstrip(".:…").strip()
    return s.lower()


def load_ts(directory, code):
    path = os.path.join(directory, code + ".ts")
    table = {}
    if not os.path.exists(path):
        return table
    root = ET.parse(path).getroot()
    for msg in root.iter("message"):
        src = msg.findtext("source") or ""
        tr = msg.find("translation")
        if tr is None or tr.get("type") in ("unfinished", "obsolete", "vanished") or not (tr.text or "").strip():
            continue
        table.setdefault(norm(src), tr.text)
    return table


def adapt(key, translated):
    """Give the translation the shape of our key: placeholders {0}, trailing punctuation, no mnemonics."""
    t = translated.replace("&&", "\0").replace("&", "").replace("\0", "&")
    t = re.sub(r"%(\d+)", lambda m: "{" + str(int(m.group(1)) - 1) + "}", t) if "{" in key else t
    m = re.search(r"(\.\.\.|…|:)$", key)
    stripped = t.rstrip().rstrip(".:…").rstrip()
    t = stripped + (m.group(1) if m else "")
    if key[:1].isupper() is False and key[:1].isalpha():
        pass
    return t


def main():
    ts_dir = DEFAULT_TS
    if "--ts" in sys.argv:
        ts_dir = sys.argv[sys.argv.index("--ts") + 1]
    keys = collect_keys()
    os.makedirs(OUT, exist_ok=True)
    report = []
    for code in LANGS:
        manual_path = os.path.join(HERE, "manual", code + ".json")
        manual = json.load(open(manual_path, encoding="utf-8")) if os.path.exists(manual_path) else {}
        table = load_ts(ts_dir, code)
        out, from_ts, from_manual = {}, 0, 0
        for k in keys:
            if code == "en_US":
                break
            if k in manual and manual[k]:
                out[k] = manual[k]; from_manual += 1
            elif norm(k) in table:
                out[k] = adapt(k, table[norm(k)]); from_ts += 1
        with open(os.path.join(OUT, code + ".json"), "w", encoding="utf-8") as f:
            json.dump(out, f, ensure_ascii=False, indent=0, sort_keys=True)
        report.append((code, len(keys), from_ts, from_manual))
    print(f"{len(keys)} texts")
    for code, n, a, b in report:
        print(f"  {code}: {a + b}/{n}  (original {a}, manual {b})")
    if "--report" in sys.argv:
        hu = json.load(open(os.path.join(OUT, "hu_HU.json"), encoding="utf-8"))
        print("\nmissing in hu_HU:")
        for k in keys:
            if k not in hu:
                print("  ", repr(k))


if __name__ == "__main__":
    main()
