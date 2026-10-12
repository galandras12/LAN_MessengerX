#!/usr/bin/env python3
"""A new client (CLIENT=kotlin|csharp) against the ORIGINAL LAN Messenger, in both directions:
discovery, text (unicode/escaping), delivery acks, status, user info, files, avatars, group chat.
Run inside the lab:  sudo ./lab.sh up && sudo CLIENT=csharp python3 test_legacy.py"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lib import *

KIND = os.environ.get("CLIENT", "kotlin")
CACHE = "/tmp/lmx-cli-bob/cache" if KIND == "kotlin" else "/tmp/lmx-cs-cli-bob/cache"
subprocess.run("pkill -x legacyd; pkill -f 'com.lanmessengerx.core.Cli[K]t'; pkill -f 'dotnet .*LanMessengerX.Cl[i].dll'", shell=True); time.sleep(1)
shutil.rmtree(CACHE, ignore_errors=True)
A = start_legacy("a", "lab_a", "alice", "10.9.0.1")
time.sleep(1)
K = start_kotlin("k", "lab_b", "bob", "AABBCCDDEE01") if KIND == "kotlin" else start_csharp("k", "lab_b", "bob", "AABBCCDDEE01")
sha = lambda p: hashlib.sha256(open(p, "rb").read()).hexdigest()
RECV = "/tmp/lmx-interop-home-a/Documents/Received Files"
try:
    ln, _ = A.wait_for(r"READY id=(\S+)"); aid = re.search(r"id=(\S+)", ln).group(1)
    kid = "AABBCCDDEE01bob"
    check("legacy sees the new client (announce)", A.wait_for(r"EVT announce " + kid)[0], A.dump())
    check("new client sees legacy (UserOnline)", K.wait_for(r"UserOnline.*id=" + aid)[0], K.dump())
    time.sleep(1)

    mk, mA = K.mark(), A.mark()
    K.send(f"msg {aid} hello from the new client äöű ✓ <&> end")
    check("new -> legacy text (unicode/escaping)", A.wait_for(r"EVT message " + kid + r".*hello from the new client äöű ✓ &lt;&amp;> end", start=mA)[0], A.dump())
    check("new client gets the delivery ack", K.wait_for(r"MessageDelivered", start=mk)[0], K.dump())
    A.send(f"msg {kid} hi back from legacy ü")
    check("legacy -> new text", K.wait_for(r"TextMessage\(userId=" + aid + r", text=hi back from legacy ü")[0], K.dump())

    mk, mA = K.mark(), A.mark()
    K.send("status busy"); A.send("status dnd")
    check("new status -> legacy", A.wait_for(r"EVT status " + kid + r".*<status>busy</status>", start=mA)[0], A.dump())
    check("legacy status -> new", K.wait_for(r"UserUpdated.*status=dnd", start=mk)[0], K.dump())

    mk, mA = K.mark(), A.mark()
    K.send(f"query {aid}"); A.send(f"query {kid}")
    check("new queries legacy user info", K.wait_for(r"UserInfo\(userId=" + aid, start=mk)[0], K.dump())
    check("legacy queries new user info", A.wait_for(r"EVT query " + kid + r".*<logon>bob</logon>", start=mA)[0], A.dump())

    for n, size in (("f1.bin", 3 * 1024 * 1024 + 17), ("f2.bin", 1500001), ("av.png", 5000), ("av2.png", 4000)):
        open(f"{DATA}/{n}", "wb").write(os.urandom(size))
    try: shutil.rmtree(RECV)
    except FileNotFoundError: pass
    mk, mA = K.mark(), A.mark()
    K.send(f"file {aid} {DATA}/f1.bin")
    ln, _ = K.wait_for(r"FILEID (\S+)", start=mk); fid = ln.split()[1] if ln else ""
    ln, _ = A.wait_for(r"EVT file " + kid + r".*<fileop>request</fileop>", start=mA)
    check("legacy receives the file request", ln and fid in ln and "f1.bin" in ln and "3145745" in ln, A.dump())
    A.send(f"accept {kid} {fid} 3145745")
    check("legacy completes receiving", A.wait_for(r"EVT file .*<fileop>complete</fileop>", timeout=30, start=mA)[0], A.dump())
    check("new client reports send complete", K.wait_for(r"TransferInfo\(id=" + fid + r".*state=COMPLETE", timeout=30, start=mk)[0], K.dump())
    time.sleep(0.5)
    check("file content identical (new -> legacy)", os.path.exists(f"{RECV}/f1.bin") and sha(f"{RECV}/f1.bin") == sha(f"{DATA}/f1.bin"))

    mk, mA = K.mark(), A.mark()
    A.send(f"file {kid} {DATA}/f2.bin")
    ln, _ = K.wait_for(r"TransferInfo\(id=\S+, userId=" + aid + r", name=f2.bin, size=1500001.*outgoing=false, state=REQUESTED", start=mk)
    check("new client receives the file request", ln, K.dump())
    fid2 = re.search(r"id=(\S+),", ln).group(1) if ln else ""
    out = f"{DATA}/recv2.bin"
    try: os.remove(out)
    except FileNotFoundError: pass
    K.send(f"accept {aid} {fid2} {out}")
    check("new client completes receiving", K.wait_for(r"TransferInfo\(id=" + fid2 + r".*state=COMPLETE", timeout=30, start=mk)[0], K.dump())
    time.sleep(0.5)
    check("file content identical (legacy -> new)", os.path.exists(out) and sha(out) == sha(f"{DATA}/f2.bin"))

    K.send(f"avatar {DATA}/av.png"); time.sleep(3)
    got = subprocess.run("find /tmp/lmx-interop-home-a -name 'avt_AABBCCDDEE01bob*.png'", shell=True, capture_output=True, text=True).stdout.splitlines()
    check("legacy received the new client's avatar", got and sha(got[0]) == sha(f"{DATA}/av.png"), str(got))
    mk = K.mark()
    A.send(f"avatar {kid} {DATA}/av2.png")
    ln, _ = K.wait_for(r"AvatarUpdated\(userId=.*path=" + CACHE + "/avt_", start=mk, timeout=15)
    check("new client received legacy's avatar", ln and sha(CACHE + "/avt_" + aid + ".png") == sha(f"{DATA}/av2.png"), K.dump())

    mk, mA = K.mark(), A.mark()
    K.send(f"group {aid}")
    ln, _ = K.wait_for(r"THREAD (\S+)", start=mk); th = ln.split()[1] if ln else ""
    check("legacy receives the group request", A.wait_for(r"EVT groupmessage " + kid + r".*<groupmsgop>request</groupmsgop>", start=mA)[0], A.dump())
    check("legacy receives the join broadcast", A.wait_for(r"EVT groupmessage " + kid + r".*<groupmsgop>join</groupmsgop>", start=mA)[0], A.dump())
    A.send(f"group {kid} {th} join")
    check("new client sees legacy join", K.wait_for(r"RoomNotice\(threadId=" + th + r", userId=" + aid + r", joined=[Tt]rue", start=mk)[0], K.dump())
    A.send(f"group {kid} {th} message hi group ü")
    check("legacy group message -> new", K.wait_for(r"RoomMessage\(threadId=" + th + r", userId=" + aid + r", text=hi group ü", start=mk)[0], K.dump())
    K.send(f"gsay {th} the new client says hi")
    check("new group message -> legacy", A.wait_for(r"EVT groupmessage " + kid + r".*<message>the new client says hi</message>", start=mA)[0], A.dump())
    A.send(f"group {kid} {th} leave")
    check("new client sees legacy leave", K.wait_for(r"RoomNotice\(threadId=" + th + r", userId=" + aid + r", joined=[Ff]alse", start=mk)[0], K.dump())
finally:
    A.kill(); K.kill()
fails = [n for n, ok in results if not ok]
print("RESULT:", "ALL PASS" if not fails else f"{len(fails)} FAILED")
sys.exit(1 if fails else 0)
