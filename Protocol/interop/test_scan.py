#!/usr/bin/env python3
"""Discovery with every incoming packet dropped on the new client's side (what a Windows firewall without exception does):
the original client was started first and announces only once, so the new client has to find it by scanning the subnet.
   sudo CLIENT=csharp python3 test_scan.py      (LEGACYD=... to pick the original client build)"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lib import *

KIND = os.environ.get("CLIENT", "kotlin")
subprocess.run("pkill -x legacyd; pkill -x upstreamd; pkill -f 'com.lanmessengerx.core.Cli[K]t'; pkill -f 'dotnet .*LanMessengerX.Cl[i].dll'", shell=True); time.sleep(1)
def ns(cmd): subprocess.run(["ip", "netns", "exec", "lab_b", "bash", "-c", cmd], check=True)
ns("iptables -F INPUT; iptables -A INPUT -i lo -j ACCEPT; iptables -A INPUT -m state --state ESTABLISHED,RELATED -j ACCEPT; iptables -A INPUT -j DROP")
A = start_legacy("a", "lab_a", "alice", "10.9.0.1")
time.sleep(2)
t0 = time.time()
K = start_kotlin("k", "lab_b", "bob", "AABBCCDDEE01") if KIND == "kotlin" else start_csharp("k", "lab_b", "bob", "AABBCCDDEE01")
try:
    ln, _ = A.wait_for(r"READY id=(\S+)"); aid = re.search(r"id=(\S+)", ln).group(1)
    found, _ = K.wait_for(r"UserOnline.*id=" + aid, timeout=45)
    check("found the original client although nothing can connect in (%.1fs)" % (time.time() - t0), found, K.dump())
    mA = A.mark()
    K.send(f"msg {aid} through the scan connection")
    check("text over the scanned connection", A.wait_for(r"EVT message AABBCCDDEE01bob.*through the scan connection", start=mA)[0], A.dump())
    A.send("msg AABBCCDDEE01bob and back")
    check("reply", K.wait_for(r"TextMessage.*and back")[0], K.dump(30).replace("Picked up JAVA_TOOL_OPTIONS","")[:3000])
finally:
    K.kill(); A.kill()
    ns("iptables -F INPUT")
print("RESULT:", "ALL PASS" if all(ok for _, ok in results) else f"{sum(1 for _, ok in results if not ok)} FAILED")
