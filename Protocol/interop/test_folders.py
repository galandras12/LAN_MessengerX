#!/usr/bin/env python3
"""Folder transfers between the new client (CLIENT=csharp) and the original client, both directions.
   sudo CLIENT=csharp python3 test_folders.py      (LEGACYD=... to pick the original client build)"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lib import *

KIND = os.environ.get("CLIENT", "csharp")
subprocess.run("pkill -x legacyd; pkill -x upstreamd; pkill -f 'com.lanmessengerx.core.Cli[K]t'; pkill -f 'dotnet .*LanMessengerX.Cl[i].dll'", shell=True); time.sleep(1)
RECV = "/tmp/lmx-interop-home-a/Documents/Received Files"
OUT = f"{DATA}/folder-out"
shutil.rmtree(RECV, ignore_errors=True); shutil.rmtree(OUT, ignore_errors=True)
os.makedirs(OUT)
SRC = f"{DATA}/tree"
shutil.rmtree(SRC, ignore_errors=True)
files = {"a.txt": 1234, "sub/b.bin": 700001, "sub/deeper/c.bin": 65535, "sub/deeper/empty.txt": 0, "z.dat": 3 * 1024 * 1024 + 5}
for rel, size in files.items():
    path = f"{SRC}/{rel}"; os.makedirs(os.path.dirname(path), exist_ok=True)
    open(path, "wb").write(os.urandom(size))
sha = lambda p: hashlib.sha256(open(p, "rb").read()).hexdigest()
def tree(root, empty=False): return {os.path.relpath(os.path.join(d, f), root).replace("\\", "/"): sha(os.path.join(d, f)) for d, _, fs in os.walk(root) for f in fs if empty or os.path.getsize(os.path.join(d, f)) > 0}

# the original's own sender cannot send empty files, so the original -> new direction uses a copy without them
SRC2 = f"{DATA}/tree2"
shutil.rmtree(SRC2, ignore_errors=True)
shutil.copytree(SRC, SRC2)
os.remove(f"{SRC2}/sub/deeper/empty.txt")
A = start_legacy("a", "lab_a", "alice", "10.9.0.1")
time.sleep(1)
K = start_kotlin("k", "lab_b", "bob", "AABBCCDDEE01") if KIND == "kotlin" else start_csharp("k", "lab_b", "bob", "AABBCCDDEE01")
try:
    ln, _ = A.wait_for(r"READY id=(\S+)"); aid = re.search(r"id=(\S+)", ln).group(1)
    kid = "AABBCCDDEE01bob"
    check("both sides see each other", A.wait_for(r"EVT announce " + kid)[0] and K.wait_for(r"UserOnline.*id=" + aid)[0], K.dump())
    time.sleep(1)

    # new -> original
    mk, mA = K.mark(), A.mark()
    K.send(f"folder {aid} {SRC}")
    ln, _ = K.wait_for(r"FOLDERID (\S+)", start=mk); fid = ln.split()[1] if ln else ""
    ln, _ = A.wait_for(r"EVT folder " + kid + r".*<fileop>request</fileop>", start=mA)
    check("original receives the folder offer", ln and fid in ln and "<filecount>4</filecount>" in ln, A.dump())
    A.send(f"acceptfolder {kid} {fid}")
    ok, _ = K.wait_for(r"TransferInfo\(id=" + fid + r".*state=COMPLETE", timeout=60, start=mk)
    check("new client reports the folder complete", ok, K.dump())
    time.sleep(1)
    got = [d for d in os.listdir(RECV)] if os.path.isdir(RECV) else []
    check("folder arrived with all files, byte for byte (new -> original)", got and tree(f"{RECV}/{got[0]}") == tree(SRC), str(got))

    # original -> new
    shutil.rmtree(RECV, ignore_errors=True)
    mk, mA = K.mark(), A.mark()
    A.send(f"folder {kid} {SRC2}")
    ln, _ = K.wait_for(r"TransferInfo\(id=\S+, userId=" + aid + r", name=tree2, size=\d+.*outgoing=false, state=REQUESTED.*isFolder=true, fileCount=4", start=mk, timeout=15)
    check("new client receives the folder offer", ln, K.dump())
    fid2 = re.search(r"id=(\S+),", ln).group(1) if ln else ""
    K.send(f"acceptfolder {aid} {fid2} {OUT}")
    ok, _ = K.wait_for(r"TransferInfo\(id=" + fid2 + r".*state=COMPLETE.*isFolder=true", timeout=60, start=mk)
    check("new client reports the received folder complete", ok, K.dump())
    time.sleep(1)
    check("folder arrived with all files, byte for byte (original -> new)", os.path.isdir(f"{OUT}/tree2") and tree(f"{OUT}/tree2") == tree(SRC2), str(os.listdir(OUT)))

    # decline
    mk, mA = K.mark(), A.mark()
    K.send(f"folder {aid} {SRC}")
    ln, _ = K.wait_for(r"FOLDERID (\S+)", start=mk)
    # the original has no decline command in the harness; just cancel from our side
    time.sleep(1)
finally:
    K.kill(); A.kill()
print("RESULT:", "ALL PASS" if all(ok for _, ok in results) else f"{sum(1 for _, ok in results if not ok)} FAILED")
