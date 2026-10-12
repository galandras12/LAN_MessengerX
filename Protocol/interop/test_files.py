#!/usr/bin/env python3
"""File transfer stress against the original client, both directions. CLIENT=kotlin|csharp  (default kotlin).
The original receiver/sender are timing sensitive - see Protocol/PROTOCOL.md, section "File transfer"."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lib import *

KIND = os.environ.get("CLIENT", "kotlin")
N = int(os.environ.get("ROUNDS", "5"))
subprocess.run("pkill -x legacyd; pkill -f 'com.lanmessengerx.core.Cli[K]t'; pkill -f 'dotnet .*LanMessengerX.Cl[i].dll'", shell=True); time.sleep(1)
A = start_legacy("a", "lab_a", "alice", "10.9.0.1"); time.sleep(1)
K = start_kotlin("k", "lab_b", "bob", "AABBCCDDEE01") if KIND == "kotlin" else start_csharp("k", "lab_b", "bob", "AABBCCDDEE01")
ln, _ = A.wait_for(r"READY id=(\S+)"); aid = re.search(r"id=(\S+)", ln).group(1); kid = "AABBCCDDEE01bob"
A.wait_for(r"EVT announce " + kid); time.sleep(1)
sha = lambda p: hashlib.sha256(open(p, "rb").read()).hexdigest()
RECV = "/tmp/lmx-interop-home-a/Documents/Received Files"
try:
    sizes = [3 * 1024 * 1024 + 17] * N + [65535 * 7, 65535 * 7 + 1, 1, 70000, 40 * 1024 * 1024 + 3]
    for i, size in enumerate(sizes):           # new client -> legacy
        path = f"{DATA}/s{i}.bin"; open(path, "wb").write(os.urandom(size))
        mk, mA = K.mark(), A.mark()
        K.send(f"file {aid} {path}")
        ln, _ = K.wait_for(r"FILEID (\S+)", start=mk); fid = ln.split()[1]
        A.wait_for(r"EVT file " + kid + r".*<fileop>request</fileop>", start=mA)
        A.send(f"accept {kid} {fid} {size}")
        ln, _ = A.wait_for(r"EVT file .*<fileop>(complete|error)</fileop>", timeout=60, start=mA)
        got = f"{RECV}/s{i}.bin"
        check(f"new -> legacy {size} bytes", ln and "complete" in ln and os.path.exists(got) and sha(got) == sha(path), (ln or "")[:160])
        try: os.remove(got)
        except Exception: pass
    for i in range(N * 4):                      # legacy -> new client
        size = 1500001 + i * 7
        path = f"{DATA}/r{i}.bin"; open(path, "wb").write(os.urandom(size))
        mk = K.mark()
        A.send(f"file {kid} {path}")
        ln, _ = K.wait_for(r"TransferInfo\(id=\S+, userId=" + aid + r", name=r%d.bin.*REQUESTED" % i, start=mk)
        fid = re.search(r"id=(\S+),", ln).group(1)
        out = f"{DATA}/rxo{i}.bin"
        try: os.remove(out)
        except FileNotFoundError: pass
        K.send(f"accept {aid} {fid} {out}")
        ln, _ = K.wait_for(r"TransferInfo\(id=" + fid + r".*state=(COMPLETE|ERROR)", timeout=30, start=mk)
        check(f"legacy -> new {size} bytes", ln and "COMPLETE" in ln and os.path.exists(out) and sha(out) == sha(path), (ln or "")[-120:])
finally:
    A.kill(); K.kill()
fails = [n for n, ok in results if not ok]
print("RESULT:", "ALL PASS" if not fails else f"{len(fails)} FAILED of {len(results)}")
sys.exit(1 if fails else 0)
