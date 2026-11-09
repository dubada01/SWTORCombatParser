"""Run under xvfb-run in a disposable Debian container, with a publish at /app.

The test temporarily hides the container's OpenSSL 3 library, then restores it.
Never run against a workstation's /usr: a container marker and root are required.
"""
import ctypes as c
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time

assert Path('/.dockerenv').exists() and os.geteuid() == 0, 'Disposable root Docker container required'
app = Path('/app/Orbs')
ssl = Path('/usr/lib/x86_64-linux-gnu/libssl.so.3')
assert ssl.exists(), 'Expected Debian x64 OpenSSL path'
hidden = ssl.with_name(ssl.name + '.orbs-test-hidden')
assert not hidden.exists()

x = c.CDLL('libX11.so.6')
xt = c.CDLL('libXtst.so.6')


class WindowAttributes(c.Structure):
    _fields_ = [(name, kind) for names, kind in [
        ('x y width height border_width depth', c.c_int), ('visual', c.c_void_p),
        ('root', c.c_ulong), ('window_class bit_gravity win_gravity backing_store', c.c_int),
        ('backing_planes backing_pixel', c.c_ulong), ('save_under', c.c_int),
        ('colormap', c.c_ulong), ('map_installed map_state', c.c_int),
        ('all_event_masks your_event_mask do_not_propagate_mask', c.c_long),
        ('override_redirect', c.c_int), ('screen', c.c_void_p)
    ] for name in names.split()]


x.XOpenDisplay.argtypes, x.XOpenDisplay.restype = [c.c_char_p], c.c_void_p
x.XDefaultRootWindow.argtypes, x.XDefaultRootWindow.restype = [c.c_void_p], c.c_ulong
x.XQueryTree.argtypes = [c.c_void_p, c.c_ulong, c.POINTER(c.c_ulong), c.POINTER(c.c_ulong), c.POINTER(c.POINTER(c.c_ulong)), c.POINTER(c.c_uint)]
x.XFetchName.argtypes = [c.c_void_p, c.c_ulong, c.POINTER(c.c_void_p)]
x.XGetWindowAttributes.argtypes = [c.c_void_p, c.c_ulong, c.POINTER(WindowAttributes)]
x.XFree.argtypes = [c.c_void_p]
x.XSetInputFocus.argtypes = [c.c_void_p, c.c_ulong, c.c_int, c.c_ulong]
x.XKeysymToKeycode.argtypes, x.XKeysymToKeycode.restype = [c.c_void_p, c.c_ulong], c.c_uint
x.XSync.argtypes = [c.c_void_p, c.c_int]
xt.XTestFakeKeyEvent.argtypes = [c.c_void_p, c.c_uint, c.c_int, c.c_ulong]
display = x.XOpenDisplay(None)
assert display, 'Start with xvfb-run'
root = x.XDefaultRootWindow(display)


def windows():
    root_out, parent, count = c.c_ulong(), c.c_ulong(), c.c_uint()
    children = c.POINTER(c.c_ulong)()
    x.XQueryTree(display, root, c.byref(root_out), c.byref(parent), c.byref(children), c.byref(count))
    found = []
    for i in range(count.value):
        title = c.c_void_p()
        if x.XFetchName(display, children[i], c.byref(title)) and title.value:
            found.append((children[i], c.string_at(title).decode('utf-8', errors='replace')))
            x.XFree(title)
    if children:
        x.XFree(children)
    return found


def wait_for_window(process, predicate, logfile):
    deadline = time.monotonic() + 35
    while time.monotonic() < deadline:
        assert process.poll() is None, logfile.read_text(errors='replace')
        for window, title in windows():
            attributes = WindowAttributes()
            # A title can appear before XMapWindow; focusing an unmapped window
            # produces BadMatch even though the application is still starting.
            if predicate(title) and x.XGetWindowAttributes(display, window, c.byref(attributes)) and attributes.map_state == 2:
                return window
        time.sleep(.2)
    raise AssertionError(f'Window missing: {windows()}\n{logfile.read_text(errors="replace")}')


def stop(process):
    if process.poll() is None:
        process.terminate()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()


with tempfile.TemporaryDirectory(prefix='orbs-startup-') as directory:
    profile = Path(directory)
    environment = dict(os.environ, HOME=str(profile), XDG_DATA_HOME=str(profile / 'data'), ORBS_SOFTWARE_RENDERING='1')
    result = subprocess.run([app, '--check-runtime'], env=environment, capture_output=True, text=True, timeout=15)
    assert result.returncode == 0, result.stderr
    print('PASS: installed OpenSSL is detected', flush=True)
    ssl.rename(hidden)
    process = None
    try:
        result = subprocess.run([app, '--check-runtime'], env=environment, capture_output=True, text=True, timeout=15)
        assert result.returncode == 1 and 'openssl3-libs' in result.stderr, result
        print('PASS: missing OpenSSL returns an actionable error without aborting', flush=True)
        data = profile / 'data/DubaTech/SWTORCombatParser'
        data.mkdir(parents=True, exist_ok=True)
        phase_file = data / 'phase_info.json'
        phase_file.write_text('')  # Exact condition from the second VM crash.
        logfile = profile / 'startup.log'
        with logfile.open('w') as output:
            process = subprocess.Popen([app], env=environment, stdout=output, stderr=subprocess.STDOUT)
            prompt = wait_for_window(process, lambda title: 'online features unavailable' in title, logfile)
            x.XSetInputFocus(display, prompt, 1, 0)
            key = x.XKeysymToKeycode(display, 0xff0d)  # Return activates Continue offline.
            xt.XTestFakeKeyEvent(display, key, 1, 0)
            xt.XTestFakeKeyEvent(display, key, 0, 0)
            x.XSync(display, 0)
            wait_for_window(process, lambda title: title.startswith('Orbs v'), logfile)
            settings = json.loads((data / 'general_settings.json').read_text())
            assert str(settings['offline_mode']).lower() == 'true', settings
            assert json.loads(phase_file.read_text()), 'Built-in phases were not restored'
            assert len(list(data.glob('phase_info.json.invalid-*.bak'))) == 1
            time.sleep(3)
            assert process.poll() is None, logfile.read_text()
            print('PASS: Continue offline saves the choice, recovers empty phases, and opens the main window', flush=True)
            stop(process)
        with logfile.open('w') as output:
            process = subprocess.Popen([app], env=environment, stdout=output, stderr=subprocess.STDOUT)
            wait_for_window(process, lambda title: title.startswith('Orbs v'), logfile)
            assert not any('online features unavailable' in title for _, title in windows())
            print('PASS: saved offline mode bypasses the dependency prompt on restart', flush=True)
            stop(process)
    finally:
        if process is not None:
            stop(process)
        hidden.rename(ssl)

print('ALL STARTUP CHECKS PASSED', flush=True)
