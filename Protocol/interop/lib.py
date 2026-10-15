import subprocess, threading, time, sys, os, re, shutil, hashlib, queue

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
TOOLS = os.environ.get("LMX_TOOLCHAIN", "/opt/lmx-android")
LEGACYD = os.environ.get("LEGACYD", os.path.join(HERE, "legacy-harness", "build", "legacyd"))
DATA = "/tmp/lmx-interop-data"
os.makedirs(DATA, exist_ok=True)

class Proc:
    def __init__(self, name, argv):
        self.name = name
        self.lines = []
        self.lock = threading.Lock()
        self.p = subprocess.Popen(argv, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, bufsize=1)
        threading.Thread(target=self._read, daemon=True).start()
    def _read(self):
        for line in self.p.stdout:
            with self.lock: self.lines.append(line.rstrip("\n"))
    def send(self, cmd):
        self.p.stdin.write(cmd + "\n"); self.p.stdin.flush()
    def wait_for(self, pattern, timeout=15, start=0):
        rx = re.compile(pattern)
        end = time.time() + timeout
        while time.time() < end:
            with self.lock:
                for i in range(start, len(self.lines)):
                    if rx.search(self.lines[i]): return self.lines[i], i
            time.sleep(0.1)
        return None, -1
    def mark(self):
        with self.lock: return len(self.lines)
    def dump(self, n=40):
        with self.lock: return "\n".join(self.lines[-n:])
    def kill(self):
        try: self.p.stdin.write("quit\n"); self.p.stdin.flush()
        except Exception: pass
        time.sleep(0.5); self.p.kill()

results = []
def check(name, cond, detail=""):
    results.append((name, bool(cond)))
    print(("PASS " if cond else "FAIL ") + name + ("" if cond else "  -> " + detail))

def start_legacy(tag, ns, user, ip):
    home = f"/tmp/lmx-interop-home-{tag}"; shutil.rmtree(home, ignore_errors=True); os.makedirs(home)
    env = ["env", f"HOME={home}", f"USER={user}", f"LMC_LOG=/tmp/lmx-interop-{tag}.log", f"XDG_CONFIG_HOME={home}/.config", f"XDG_DATA_HOME={home}/.local/share", f"XDG_CACHE_HOME={home}/.cache"]
    return Proc("legacy-"+tag, ["ip","netns","exec",ns]+env+[LEGACYD])

def start_kotlin(tag, ns, name, mac):
    return Proc("kotlin-"+tag, ["ip","netns","exec",ns,"java","-cp",f"{ROOT}/AndroidClient/build/cli-classes:{TOOLS}/kotlin/kotlin-stdlib-2.0.21.jar","com.lanmessengerx.core.CliKt",name,mac])

def start_csharp(tag, ns, name, mac):
    return Proc("csharp-"+tag, ["ip","netns","exec",ns,"dotnet",f"{ROOT}/DesktopClient/tools/LanMessengerX.Cli/bin/Release/net8.0/LanMessengerX.Cli.dll",name,mac])
