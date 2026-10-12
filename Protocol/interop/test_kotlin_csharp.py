#!/usr/bin/env python3
"""The two new implementations against each other (no original client involved)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lib import *

subprocess.run("pkill -x legacyd; pkill -f 'com.lanmessengerx.core.Cli[K]t'; pkill -f 'dotnet .*LanMessengerX.Cl[i].dll'", shell=True); time.sleep(1)
C = start_csharp("c", "lab_a", "carol", "AABBCCDDEE02")
K = start_kotlin("k", "lab_b", "bob", "AABBCCDDEE01")
cid = "AABBCCDDEE02carol"; kid = "AABBCCDDEE01bob"
sha = lambda p: hashlib.sha256(open(p, "rb").read()).hexdigest()
try:
    check("csharp sees kotlin", C.wait_for(r"UserOnline.*id=" + kid)[0], C.dump())
    check("kotlin sees csharp", K.wait_for(r"UserOnline.*id=" + cid)[0], K.dump())
    time.sleep(1)
    mk, mc = K.mark(), C.mark()
    K.send(f"msg {cid} hello csharp äöű ✓ <&>")
    check("kotlin -> csharp text", C.wait_for(r"TextMessage\(userId=" + kid + r", text=hello csharp äöű ✓ <&>", start=mc)[0], C.dump())
    check("kotlin gets the ack", K.wait_for(r"MessageDelivered", start=mk)[0])
    C.send(f"msg {kid} hello kotlin")
    check("csharp -> kotlin text", K.wait_for(r"TextMessage\(userId=" + cid + r", text=hello kotlin", start=mk)[0], K.dump())
    for src, dst, name, size in ((K, C, "k2c.bin", 5 * 1024 * 1024 + 3), (C, K, "c2k.bin", 3 * 1024 * 1024 + 11)):
        path = f"{DATA}/{name}"; open(path, "wb").write(os.urandom(size))
        ms, md = src.mark(), dst.mark()
        sid = kid if src is K else cid; did = cid if src is K else kid
        src.send(f"file {did} {path}")
        ln, _ = dst.wait_for(r"TransferInfo\(id=\S+, userId=" + sid + r", name=" + name + r".*REQUESTED", start=md)
        fid = re.search(r"id=(\S+),", ln).group(1) if ln else ""
        out = f"{DATA}/out_{name}"
        try: os.remove(out)
        except FileNotFoundError: pass
        dst.send(f"accept {sid} {fid} {out}")
        check(f"{name} received", dst.wait_for(r"TransferInfo\(id=" + fid + r".*state=COMPLETE", timeout=30, start=md)[0], dst.dump())
        check(f"{name} sender complete", src.wait_for(r"TransferInfo\(id=" + fid + r".*state=COMPLETE", timeout=30, start=ms)[0], src.dump())
        check(f"{name} identical", os.path.exists(out) and sha(out) == sha(path))
    mk, mc = K.mark(), C.mark()
    K.send(f"group {cid}")
    ln, _ = K.wait_for(r"THREAD (\S+)", start=mk); th = ln.split()[1]
    check("csharp joins the room", C.wait_for(r"RoomChanged\(room=RoomInfo\(threadId=" + th + r".*participants=\[.*" + kid, start=mc)[0], C.dump())
    time.sleep(1)
    K.send(f"gsay {th} group hello")
    check("group message delivered", C.wait_for(r"RoomMessage\(threadId=" + th + r", userId=" + kid + r", text=group hello", start=mc)[0], C.dump())
    C.send(f"gsay {th} reply from carol")
    check("group reply delivered", K.wait_for(r"RoomMessage\(threadId=" + th + r", userId=" + cid + r", text=reply from carol", start=mk)[0], K.dump())
finally:
    C.kill(); K.kill()
fails = [n for n, ok in results if not ok]
print("RESULT:", "ALL PASS" if not fails else f"{len(fails)} FAILED")
sys.exit(1 if fails else 0)
