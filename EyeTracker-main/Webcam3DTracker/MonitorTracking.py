import cv2
import numpy as np
import os
import mediapipe as mp
import time
import math
from scipy.spatial.transform import Rotation as Rscipy
from collections import deque
import pyautogui
import threading
import keyboard
import subprocess
import sys

# ============================================================
#  SCREEN / MOUSE SETUP
# ============================================================
MONITOR_WIDTH, MONITOR_HEIGHT = pyautogui.size()
CENTER_X = MONITOR_WIDTH // 2
CENTER_Y = MONITOR_HEIGHT // 2
mouse_control_enabled = False
filter_length = 10
gaze_length = 350

# ============================================================
#  ORBIT CAMERA STATE (debug view)
# ============================================================
orbit_yaw    = -151.0
orbit_pitch  =    0.0
orbit_radius = 1500.0
orbit_fov_deg = 50.0

debug_world_frozen   = False
orbit_pivot_frozen   = None

gaze_markers = []

# ============================================================
#  3-D MONITOR PLANE STATE
# ============================================================
monitor_corners   = None
monitor_center_w  = None
monitor_normal_w  = None
units_per_cm      = None

# ============================================================
#  MOUSE TARGET
# ============================================================
mouse_target = [CENTER_X, CENTER_Y]
mouse_lock   = threading.Lock()

calibration_offset_yaw   = 0
calibration_offset_pitch = 0

calib_step = 0

combined_gaze_directions = deque(maxlen=filter_length)

R_ref_nose     = [None]
R_ref_forehead = [None]
calibration_nose_scale = None

# ============================================================
#  MONITOR-CORNER TUNING
# ============================================================
CORNER_LABELS = [
    "bottom-right (0,0)",
    "bottom-left  (0,1)->X",
    "top-left     (1,1)",
    "top-right    (1,0)->Y",
]
corner_world_pts   = []
corner_calib_step  = 0
monitor_tuned      = False


def get_monitor_coords_tuned(P_world):
    if len(corner_world_pts) < 4:
        return None
    origin = np.asarray(corner_world_pts[0], dtype=float)
    x_end  = np.asarray(corner_world_pts[1], dtype=float)
    y_end  = np.asarray(corner_world_pts[3], dtype=float)
    u = x_end - origin
    v = y_end - origin
    u2 = float(np.dot(u, u))
    v2 = float(np.dot(v, v))
    if u2 < 1e-9 or v2 < 1e-9:
        return None
    P = np.asarray(P_world, dtype=float)
    w_vec = P - origin
    mx = float(np.dot(w_vec, u) / u2)
    my = float(np.dot(w_vec, v) / v2)
    return mx, my


def intersect_gaze_with_tuned_monitor(O, D):
    if len(corner_world_pts) < 4:
        return None
    origin = np.asarray(corner_world_pts[0], dtype=float)
    x_end  = np.asarray(corner_world_pts[1], dtype=float)
    y_end  = np.asarray(corner_world_pts[3], dtype=float)
    u = x_end - origin
    v = y_end - origin
    N = np.cross(u, v)
    N_len = np.linalg.norm(N)
    if N_len < 1e-9:
        return None
    N = N / N_len
    D = np.asarray(D, dtype=float)
    D_len = np.linalg.norm(D)
    if D_len < 1e-9:
        return None
    D = D / D_len
    denom = float(np.dot(N, D))
    if abs(denom) < 1e-6:
        return None
    t = float(np.dot(N, origin - O) / denom)
    if t <= 0:
        return None
    P = np.asarray(O, dtype=float) + t * D
    return get_monitor_coords_tuned(P)


# ============================================================
#  60-s RECORDING
# ============================================================
recording_active   = False
recording_start_t  = None
RECORDING_DURATION = 60.0
last_record_second = -1
recorded_coords    = []

OUTPUT_DIR = os.path.dirname(os.path.abspath(__file__))

# ============================================================
#  STIMULUS - PHASE CONSTANTS
# ============================================================
PHASE1_DURATION = 30.0   # primele 30s: imaginea NYC
PHASE2_DURATION = 30.0   # ultimele 30s: puncte miscatoare

STIMULUS_WIN_NAME = "Stimulus"
STIMULUS_IMG_PATH = os.path.join(OUTPUT_DIR, "NYCSquare.jpg")
_stimulus_img_raw  = None
_stimulus_img_show = None

# ============================================================
#  MOVING DOTS
# ============================================================
NUM_DOTS          = 12
DOT_RADIUS        = 18
DOT_COLOR_BLUE    = (200, 80, 0)
DOT_COLOR_ORANGE  = (0, 140, 255)
DOT_MOVE_INTERVAL = 2.5
DOT_MOVE_DURATION = 1.2
DOT_ORANGE_DURATION = 2.0
MARGIN            = 80

_dots_pos         = []
_dots_target      = []
_dots_start_pos   = []
_dots_move_start  = 0.0
_dot_orange_idx   = 0
_dot_orange_start = 0.0

_rng = np.random.default_rng(42)


def _random_pos():
    x = int(_rng.integers(MARGIN, MONITOR_WIDTH  - MARGIN))
    y = int(_rng.integers(MARGIN, MONITOR_HEIGHT - MARGIN))
    return float(x), float(y)


def _init_dots():
    global _dots_pos, _dots_target, _dots_start_pos
    global _dots_move_start, _dot_orange_idx, _dot_orange_start
    _dots_pos       = [_random_pos() for _ in range(NUM_DOTS)]
    _dots_target    = [_random_pos() for _ in range(NUM_DOTS)]
    _dots_start_pos = list(_dots_pos)
    _dots_move_start  = time.time()
    _dot_orange_idx   = 0
    _dot_orange_start = time.time()


def _ease_in_out(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def _update_dots():
    global _dots_pos, _dots_target, _dots_start_pos, _dots_move_start
    global _dot_orange_idx, _dot_orange_start

    now = time.time()
    move_elapsed = now - _dots_move_start
    if move_elapsed >= DOT_MOVE_INTERVAL:
        _dots_start_pos  = list(_dots_target)
        _dots_target     = [_random_pos() for _ in range(NUM_DOTS)]
        _dots_move_start = now
        move_elapsed     = 0.0

    t_move = _ease_in_out(move_elapsed / DOT_MOVE_DURATION)
    for i in range(NUM_DOTS):
        sx, sy = _dots_start_pos[i]
        tx, ty = _dots_target[i]
        _dots_pos[i] = (
            sx + (tx - sx) * t_move,
            sy + (ty - sy) * t_move,
        )

    if now - _dot_orange_start >= DOT_ORANGE_DURATION:
        _dot_orange_idx   = (_dot_orange_idx + 1) % NUM_DOTS
        _dot_orange_start = now


def _build_dots_frame():
    _update_dots()
    img = np.full((MONITOR_HEIGHT, MONITOR_WIDTH, 3), 255, dtype=np.uint8)
    for i, (px, py) in enumerate(_dots_pos):
        cx, cy = int(px), int(py)
        if i == _dot_orange_idx:
            cv2.circle(img, (cx, cy), DOT_RADIUS + 8, (0, 100, 200), -1, cv2.LINE_AA)
            cv2.circle(img, (cx, cy), DOT_RADIUS + 8, (0,  60, 150),  2, cv2.LINE_AA)
            cv2.circle(img, (cx, cy), DOT_RADIUS,     DOT_COLOR_ORANGE, -1, cv2.LINE_AA)
        else:
            cv2.circle(img, (cx, cy), DOT_RADIUS,     DOT_COLOR_BLUE,   -1, cv2.LINE_AA)
            cv2.circle(img, (cx, cy), DOT_RADIUS,     (150, 50, 0),      2, cv2.LINE_AA)
    return img


def _load_stimulus_image():
    global _stimulus_img_raw, _stimulus_img_show
    if not os.path.isfile(STIMULUS_IMG_PATH):
        print(f"[Stimulus] Fisierul nu a fost gasit: {STIMULUS_IMG_PATH}")
        return False
    img = cv2.imread(STIMULUS_IMG_PATH)
    if img is None:
        print(f"[Stimulus] Nu s-a putut citi imaginea: {STIMULUS_IMG_PATH}")
        return False
    _stimulus_img_raw = img
    ih, iw = img.shape[:2]
    scale = min(MONITOR_WIDTH / iw, MONITOR_HEIGHT / ih)
    new_w = int(iw * scale)
    new_h = int(ih * scale)
    resized = cv2.resize(img, (new_w, new_h), interpolation=cv2.INTER_AREA)
    canvas = np.zeros((MONITOR_HEIGHT, MONITOR_WIDTH, 3), dtype=np.uint8)
    x_off = (MONITOR_WIDTH  - new_w) // 2
    y_off = (MONITOR_HEIGHT - new_h) // 2
    canvas[y_off:y_off+new_h, x_off:x_off+new_w] = resized
    _stimulus_img_show = canvas
    print(f"[Stimulus] Imagine incarcata: {iw}x{ih} -> afisat {new_w}x{new_h}")
    return True


def show_stimulus_window():
    global _stimulus_img_show
    _init_dots()
    if _stimulus_img_show is None:
        _load_stimulus_image()
    cv2.namedWindow(STIMULUS_WIN_NAME, cv2.WINDOW_NORMAL)
    if _stimulus_img_show is not None:
        cv2.imshow(STIMULUS_WIN_NAME, _stimulus_img_show)
    cv2.setWindowProperty(STIMULUS_WIN_NAME, cv2.WND_PROP_FULLSCREEN, cv2.WINDOW_FULLSCREEN)
    print("[Stimulus] Fereastra deschisa fullscreen.")


def hide_stimulus_window():
    try:
        cv2.destroyWindow(STIMULUS_WIN_NAME)
    except Exception:
        pass
    print("[Stimulus] Fereastra inchisa.")


def _update_stimulus_overlay():
    if not recording_active or recording_start_t is None:
        return

    elapsed   = time.time() - recording_start_t
    remaining = max(0.0, RECORDING_DURATION - elapsed)

    if elapsed < PHASE1_DURATION:
        if _stimulus_img_show is None:
            return
        overlay = _stimulus_img_show.copy()
        phase_label = "FAZA 1: Imagine"
    else:
        overlay = _build_dots_frame()
        phase_label = f"FAZA 2: Punct portocaliu #{_dot_orange_idx + 1}"

    pct    = min(1.0, elapsed / RECORDING_DURATION)
    bar_w2 = MONITOR_WIDTH - 80
    bar_x0, bar_y0, bar_h = 40, 30, 8
    bar_bg = overlay.copy()
    cv2.rectangle(bar_bg, (bar_x0, bar_y0), (bar_x0 + bar_w2, bar_y0 + bar_h), (40, 40, 40), -1)
    cv2.rectangle(bar_bg, (bar_x0, bar_y0),
                  (bar_x0 + int(pct * bar_w2), bar_y0 + bar_h), (0, 160, 255), -1)
    cv2.addWeighted(bar_bg, 0.75, overlay, 0.25, 0, overlay)

    font = cv2.FONT_HERSHEY_DUPLEX
    hud  = f"REC  {remaining:.0f}s  |  {len(recorded_coords)} pts  |  {phase_label}"
    (tw, th), _ = cv2.getTextSize(hud, font, 0.6, 1)
    tx = (MONITOR_WIDTH - tw) // 2
    ty = bar_y0 + bar_h + th + 6
    cv2.putText(overlay, hud, (tx + 1, ty + 1), font, 0.6, (0, 0, 0),      2, cv2.LINE_AA)
    cv2.putText(overlay, hud, (tx,     ty),     font, 0.6, (255, 255, 255), 1, cv2.LINE_AA)

    cv2.imshow(STIMULUS_WIN_NAME, overlay)


_load_stimulus_image()


# ============================================================
#  AUTO-SEND TO C# — trimite coordonatele automat prin stdout
# ============================================================

CSHARP_APP_PATH = os.path.join(OUTPUT_DIR, "TestFocusAI.exe")  # <-- schimba cu calea ta

def format_coords_for_csharp(coords):
    """
    Converteste lista de (sec, mx, my) in sirul  "mx,my;mx,my;..."
    exact formatul asteptat de aplicatia C#.
    """
    parts = [f"{mx:.3f},{my:.3f}" for (_, mx, my) in coords]
    return ";".join(parts)


def send_coords_to_csharp(coords):
    """
    Trimite coordonatele la aplicatia C# in doua moduri:
      1. Prin stdout (daca eye tracker-ul insusi e lansat de C# ca subprocess)
      2. Prin subprocess (lanseaza exe-ul C# si ii trimite datele pe stdin)
    """
    if not coords:
        print("[Send] Nu exista coordonate de trimis.", file=sys.stderr)
        return

    coords_str = format_coords_for_csharp(coords)
    n = len(coords)
    print(f"[Send] Trimit {n} puncte gaze catre C#...", file=sys.stderr)

    # --- Metoda 1: stdout direct (daca C# a lansat acest script ca subprocess) ---
    print(coords_str)          # C# citeste asta cu StandardOutput.ReadLine()
    sys.stdout.flush()
    print(f"[Send] Trimis pe stdout: {coords_str[:80]}{'...' if len(coords_str)>80 else ''}", file=sys.stderr)

    # --- Metoda 2: lanseaza exe-ul C# si trimite pe stdin (decommenteaza daca ai nevoie) ---
    # if os.path.isfile(CSHARP_APP_PATH):
    #     try:
    #         proc = subprocess.Popen(
    #             [CSHARP_APP_PATH],
    #             stdin=subprocess.PIPE,
    #             stdout=subprocess.PIPE,
    #             stderr=subprocess.PIPE,
    #             text=True
    #         )
    #         stdout_data, stderr_data = proc.communicate(input=coords_str, timeout=10)
    #         print(f"[Send] Raspuns C#: {stdout_data.strip()}", file=sys.stderr)
    #     except Exception as e:
    #         print(f"[Send] Eroare la lansarea C#: {e}", file=sys.stderr)
    # else:
    #     print(f"[Send] Exe C# negasit la: {CSHARP_APP_PATH}", file=sys.stderr)


def start_recording():
    global recording_active, recording_start_t, last_record_second, recorded_coords
    if not monitor_tuned:
        print("[Recording] Calibreaza mai intai colturele in ecranul de calibrare.")
        return
    recording_active   = True
    recording_start_t  = time.time()
    last_record_second = -1
    recorded_coords    = []
    print("[Recording] START — 60 de secunde (30s imagine + 30s puncte miscatoare).")
    show_stimulus_window()


def stop_recording_and_save():
    global recording_active
    recording_active = False
    hide_stimulus_window()
    print(f"[Recording] STOP — {len(recorded_coords)} coordonate salvate.")
    save_results()
    # ★ TRIMITERE AUTOMATA — se executa imediat dupa salvare
    send_coords_to_csharp(recorded_coords)


def save_results():
    if not recorded_coords:
        print("[Save] Nu exista coordonate de salvat.")
        return
    txt_path = os.path.join(OUTPUT_DIR, "gaze_coords.txt")
    with open(txt_path, "w", encoding="utf-8") as f:
        f.write("# second, mx, my\n")
        f.write("coords = [\n")
        for (sec, mx, my) in recorded_coords:
            f.write(f"    ({sec:3d}, {mx:.4f}, {my:.4f}),\n")
        f.write("]\n")
    print(f"[Save] Salvat in: {txt_path}")
    show_gaze_map(recorded_coords)


def show_gaze_map(coords):
    MAP_W, MAP_H = 900, 600
    img = np.zeros((MAP_H, MAP_W, 3), dtype=np.uint8)
    img[:] = (30, 30, 30)
    margin = 40
    ix0, iy0, ix1, iy1 = margin, margin, MAP_W-margin, MAP_H-margin
    cv2.rectangle(img, (ix0, iy0), (ix1, iy1), (80,80,80), 2)
    font = cv2.FONT_HERSHEY_SIMPLEX
    cv2.putText(img, "BR(0,0)", (ix1-70, iy1+18), font, 0.45, (120,120,120), 1)
    cv2.putText(img, "BL",      (ix0,    iy1+18), font, 0.45, (120,120,120), 1)
    cv2.putText(img, "TL",      (ix0,    iy0-6),  font, 0.45, (120,120,120), 1)
    cv2.putText(img, "TR",      (ix1-30, iy0-6),  font, 0.45, (120,120,120), 1)
    iw2, ih2 = ix1-ix0, iy1-iy0

    def to_px(mx, my):
        return ix1-int(mx*iw2), iy1-int(my*ih2)

    pts_px = [to_px(max(0.0,min(1.0,mx)), max(0.0,min(1.0,my))) for (_,mx,my) in coords]
    for i in range(1, len(pts_px)):
        cv2.line(img, pts_px[i-1], pts_px[i], (60,60,180), 1)
    ov = img.copy()
    for (px, py) in pts_px:
        cv2.circle(ov, (px, py), 28, (0,100,200), -1)
    cv2.addWeighted(ov, 0.25, img, 0.75, 0, img)
    for i, ((sec,mx,my),(px,py)) in enumerate(zip(coords, pts_px)):
        t = i/max(len(coords)-1,1)
        col = (0, int(255*(1-t)), int(255*t))
        cv2.circle(img, (px,py), 6, col, -1)
        cv2.putText(img, str(sec), (px+7,py-4), font, 0.38, col, 1)
    cv2.putText(img, "Harta Gaze (60s)", (MAP_W//2-100, 22), font, 0.7, (200,200,200), 1)
    cv2.putText(img, f"{len(coords)} puncte", (MAP_W//2-60, MAP_H-12), font, 0.5, (160,160,160), 1)
    cv2.imshow("Gaze Map", img)


# ============================================================
#  FULLSCREEN CALIBRATION UI
# ============================================================
calibration_mode    = True
calibration_stage   = 0
current_calib_index = 0
screen_calib_points_world = []

CALIB_WIN_NAME  = "Eye Tracker Calibration"
CALIB_MARGIN    = 100
ORANGE_BGR      = (0, 140, 255)
ORANGE_RING_BGR = (30, 180, 255)
WHITE_BGR       = (255, 255, 255)
DARK_BGR        = (40, 40, 40)
_calib_frame_count = 0


def _build_calib_points():
    W, H = MONITOR_WIDTH, MONITOR_HEIGHT
    M = CALIB_MARGIN
    cx, cy = W//2, H//2
    return [
        (cx,   cy  ),
        (W-M,  H-M ),
        (M,    H-M ),
        (M,    M   ),
        (W-M,  M   ),
        (cx,   M   ),
        (W-M,  cy  ),
        (cx,   H-M ),
        (M,    cy  ),
    ]

CALIB_SCREEN_POINTS = _build_calib_points()
N_CALIB_POINTS      = len(CALIB_SCREEN_POINTS)

_CALIB_POINT_NAMES = [
    "Center",
    "Bottom-Right", "Bottom-Left", "Top-Left", "Top-Right",
    "Top-Center", "Right-Center", "Bottom-Center", "Left-Center",
]

_CORNER_CALIB_INDICES = [1, 2, 3, 4]


def _draw_calibration_dot(img, cx, cy, pulse_alpha):
    ring_r = int(36 + 10 * pulse_alpha)
    ov = img.copy()
    cv2.circle(ov, (cx, cy), ring_r, ORANGE_RING_BGR, 2, cv2.LINE_AA)
    cv2.addWeighted(ov, 0.5*(1-pulse_alpha*0.5), img, 1-0.5*(1-pulse_alpha*0.5), 0, img)
    cv2.circle(img, (cx, cy), 22, ORANGE_BGR, -1, cv2.LINE_AA)
    cv2.circle(img, (cx, cy),  6, WHITE_BGR,  -1, cv2.LINE_AA)
    cv2.circle(img, (cx, cy), 22, DARK_BGR,    1, cv2.LINE_AA)


def show_calibration_screen(avg_gaze_dir=None):
    global _calib_frame_count
    _calib_frame_count += 1
    img   = np.full((MONITOR_HEIGHT, MONITOR_WIDTH, 3), 245, dtype=np.uint8)
    for gx in range(0, MONITOR_WIDTH,  60): cv2.line(img, (gx,0), (gx,MONITOR_HEIGHT), (230,230,230), 1)
    for gy in range(0, MONITOR_HEIGHT, 60): cv2.line(img, (0,gy), (MONITOR_WIDTH,gy),  (230,230,230), 1)
    pulse = (math.sin(_calib_frame_count * 0.07) + 1.0) * 0.5
    font  = cv2.FONT_HERSHEY_DUPLEX

    if calibration_stage == 0:
        cx, cy = MONITOR_WIDTH//2, MONITOR_HEIGHT//2
        _draw_calibration_dot(img, cx, cy, pulse)
        title = "EYE TRACKER CALIBRATION"
        (tw,_),_ = cv2.getTextSize(title, font, 1.0, 2)
        cv2.putText(img, title, ((MONITOR_WIDTH-tw)//2, cy-110), font, 1.0, (60,60,60), 2, cv2.LINE_AA)
        sub = "Keep your head still and focus naturally on the orange dot"
        (sw,_),_ = cv2.getTextSize(sub, font, 0.6, 1)
        cv2.putText(img, sub, ((MONITOR_WIDTH-sw)//2, cy+65), font, 0.6, (100,100,100), 1, cv2.LINE_AA)
        prompt = "Look at the center and press  C"
        (pw,ph),_ = cv2.getTextSize(prompt, font, 0.85, 2)
        px_pos = (MONITOR_WIDTH-pw)//2; py_pos = cy+105
        kw = cv2.getTextSize("C", font, 0.85, 2)[0][0]
        kx = px_pos+pw-kw-2
        cv2.rectangle(img, (kx-10,py_pos-ph-4), (kx+kw+10,py_pos+8), ORANGE_BGR, -1, cv2.LINE_AA)
        cv2.putText(img, prompt, (px_pos,py_pos), font, 0.85, DARK_BGR, 2, cv2.LINE_AA)
        footer = "Stage 1 of 2  -  Center fixation"
        (fw2,_),_ = cv2.getTextSize(footer, font, 0.5, 1)
        cv2.putText(img, footer, ((MONITOR_WIDTH-fw2)//2, MONITOR_HEIGHT-30), font, 0.5, (170,170,170), 1, cv2.LINE_AA)

    elif calibration_stage == 1:
        if current_calib_index < N_CALIB_POINTS:
            tx, ty = CALIB_SCREEN_POINTS[current_calib_index]
            _draw_calibration_dot(img, tx, ty, pulse)
            progress = current_calib_index / N_CALIB_POINTS
            bar_h, bar_y = 6, 18
            cv2.rectangle(img, (CALIB_MARGIN,bar_y), (MONITOR_WIDTH-CALIB_MARGIN,bar_y+bar_h), (210,210,210), -1, cv2.LINE_AA)
            cv2.rectangle(img, (CALIB_MARGIN,bar_y),
                          (CALIB_MARGIN+int((MONITOR_WIDTH-2*CALIB_MARGIN)*progress),bar_y+bar_h), ORANGE_BGR, -1, cv2.LINE_AA)
            prog_text = f"Point  {current_calib_index+1}  /  {N_CALIB_POINTS}"
            (pt_w,_),_ = cv2.getTextSize(prog_text, font, 0.7, 1)
            cv2.putText(img, prog_text, ((MONITOR_WIDTH-pt_w)//2, 50), font, 0.7, (80,80,80), 1, cv2.LINE_AA)
            instr = "Look at the orange dot, then press  M"
            (iw2,ih2),_ = cv2.getTextSize(instr, font, 0.75, 1)
            iy = ty-70 if ty > MONITOR_HEIGHT//2 else ty+80
            km_w   = cv2.getTextSize("M", font, 0.75, 1)[0][0]
            full_x = (MONITOR_WIDTH-iw2)//2
            kcx    = full_x+iw2-km_w-2
            cv2.rectangle(img, (kcx-8,iy-ih2-2), (kcx+km_w+8,iy+6), ORANGE_BGR, -1, cv2.LINE_AA)
            cv2.putText(img, instr, (full_x,iy), font, 0.75, DARK_BGR, 1, cv2.LINE_AA)
            lbl = _CALIB_POINT_NAMES[current_calib_index] if current_calib_index < len(_CALIB_POINT_NAMES) else ""
            (lw,_),_ = cv2.getTextSize(lbl, font, 0.5, 1)
            cv2.putText(img, lbl, (tx-lw//2, ty+42), font, 0.5, (130,130,130), 1, cv2.LINE_AA)
            if current_calib_index in _CORNER_CALIB_INDICES:
                ci  = _CORNER_CALIB_INDICES.index(current_calib_index)
                hint = f"Corner {ci+1}/4  -  {CORNER_LABELS[ci]}"
                (hw2,_),_ = cv2.getTextSize(hint, font, 0.45, 1)
                cv2.putText(img, hint, (tx-hw2//2, ty+62), font, 0.45, (0,140,255), 1, cv2.LINE_AA)
            for pi in range(current_calib_index):
                px2, py2 = CALIB_SCREEN_POINTS[pi]
                cv2.circle(img, (px2,py2), 6, (100,200,80), -1, cv2.LINE_AA)
            footer = "Stage 2 of 2  -  Multi-point calibration"
            (fw2,_),_ = cv2.getTextSize(footer, font, 0.5, 1)
            cv2.putText(img, footer, ((MONITOR_WIDTH-fw2)//2, MONITOR_HEIGHT-30), font, 0.5, (170,170,170), 1, cv2.LINE_AA)

    elif calibration_stage == 2:
        cx, cy = MONITOR_WIDTH//2, MONITOR_HEIGHT//2
        done_text = "Calibration Complete!"
        (dw,_),_ = cv2.getTextSize(done_text, font, 1.6, 3)
        cv2.putText(img, done_text, ((MONITOR_WIDTH-dw)//2, cy-20), font, 1.6, (30,160,80), 3, cv2.LINE_AA)
        sub2 = "Starting eye tracking..."
        (sw2,_),_ = cv2.getTextSize(sub2, font, 0.8, 1)
        cv2.putText(img, sub2, ((MONITOR_WIDTH-sw2)//2, cy+50), font, 0.8, (100,100,100), 1, cv2.LINE_AA)
        for pi, (px2,py2) in enumerate(CALIB_SCREEN_POINTS[:len(screen_calib_points_world)]):
            cv2.circle(img, (px2,py2), 10, (100,200,80), -1, cv2.LINE_AA)

    cv2.imshow(CALIB_WIN_NAME, img)


def get_gaze_hit_point(O, D):
    if monitor_corners is None or monitor_center_w is None or monitor_normal_w is None:
        return None
    O = np.asarray(O, dtype=float)
    D = np.asarray(D, dtype=float)
    d_norm = np.linalg.norm(D)
    if d_norm < 1e-9: return None
    D = D / d_norm
    C_pt = np.asarray(monitor_center_w, dtype=float)
    N_pt = np.asarray(monitor_normal_w, dtype=float)
    N_norm = np.linalg.norm(N_pt)
    if N_norm < 1e-9: return None
    N_pt = N_pt / N_norm
    denom = float(np.dot(N_pt, D))
    if abs(denom) < 1e-6: return None
    t = float(np.dot(N_pt, C_pt-O) / denom)
    if t <= 0: return None
    return O + t * D


def _setup_calibration_window():
    cv2.namedWindow(CALIB_WIN_NAME, cv2.WINDOW_NORMAL)
    blank = np.full((MONITOR_HEIGHT, MONITOR_WIDTH, 3), 245, dtype=np.uint8)
    cv2.imshow(CALIB_WIN_NAME, blank)
    cv2.setWindowProperty(CALIB_WIN_NAME, cv2.WND_PROP_FULLSCREEN, cv2.WINDOW_FULLSCREEN)


def advance_calib_point_stage1(O, D):
    global current_calib_index, calibration_stage, calibration_mode
    global corner_world_pts, corner_calib_step, monitor_tuned

    hit  = get_gaze_hit_point(O, D)
    name = _CALIB_POINT_NAMES[current_calib_index] if current_calib_index < len(_CALIB_POINT_NAMES) else str(current_calib_index)

    if hit is not None:
        screen_calib_points_world.append(hit)
        print(f"[Calib] Point {current_calib_index+1}/{N_CALIB_POINTS} '{name}' -> {hit.round(2)}")
        if current_calib_index in _CORNER_CALIB_INDICES and not monitor_tuned:
            corner_world_pts.append(hit.copy())
            corner_calib_step = len(corner_world_pts)
            print(f"  [Corner] {corner_calib_step}/4 '{CORNER_LABELS[corner_calib_step-1]}' inregistrat.")
            if corner_calib_step == 4:
                monitor_tuned = True
                print("  [Corner] Monitor CALIBRAT! T = start inregistrare.")
    else:
        print(f"[Calib] Point {current_calib_index+1}: fara intersectie.")
        screen_calib_points_world.append(None)

    current_calib_index += 1
    if current_calib_index >= N_CALIB_POINTS:
        _finish_calibration()


def _finish_calibration():
    global calibration_stage, calibration_mode
    calibration_stage = 2
    show_calibration_screen()
    cv2.waitKey(1800)
    calibration_mode = False
    cv2.destroyWindow(CALIB_WIN_NAME)
    print("[Calib] Calibrare terminata. Eye tracking activ.")


_setup_calibration_window()


# ============================================================
#  MEDIAPIPE + WEBCAM
# ============================================================
mp_face_mesh = mp.solutions.face_mesh
face_mesh = mp_face_mesh.FaceMesh(
    static_image_mode=False,
    max_num_faces=1,
    refine_landmarks=True,
    min_detection_confidence=0.5,
    min_tracking_confidence=0.5
)

cap = cv2.VideoCapture(1)
w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))

nose_indices = [4, 45, 275, 220, 440, 1, 5, 51, 281, 44, 274, 241,
                461, 125, 354, 218, 438, 195, 167, 393, 165, 391,
                3, 248]

screen_position_file = "C:/Storage/Google Drive/Software/EyeTracker3DPython/screen_position.txt"

def write_screen_position(x, y):
    with open(screen_position_file, 'w') as f:
        f.write(f"{x},{y}\n")

def _rot_x(a):
    ca, sa = math.cos(a), math.sin(a)
    return np.array([[1,0,0],[0,ca,-sa],[0,sa,ca]], dtype=float)

def _rot_y(a):
    ca, sa = math.cos(a), math.sin(a)
    return np.array([[ca,0,sa],[0,1,0],[-sa,0,ca]], dtype=float)

def _normalize(v):
    v = np.asarray(v, dtype=float)
    n = np.linalg.norm(v)
    return v / n if n > 1e-9 else v

def _focal_px(width, fov_deg):
    return 0.5 * width / math.tan(math.radians(fov_deg) * 0.5)


def create_monitor_plane(head_center, R_final, face_landmarks, frame_w, frame_h,
                         forward_hint=None, gaze_origin=None, gaze_dir=None):
    try:
        lm_chin = face_landmarks[152]; lm_fore = face_landmarks[10]
        chin_w = np.array([lm_chin.x*frame_w, lm_chin.y*frame_h, lm_chin.z*frame_w], dtype=float)
        fore_w = np.array([lm_fore.x*frame_w, lm_fore.y*frame_h, lm_fore.z*frame_w], dtype=float)
        upc = np.linalg.norm(fore_w - chin_w) / 15.0
    except Exception:
        upc = 5.0
    half_w = 30.0 * upc; half_h = 20.0 * upc
    head_forward = -R_final[:, 2]
    if forward_hint is not None: head_forward = forward_hint / np.linalg.norm(forward_hint)
    if gaze_origin is not None and gaze_dir is not None:
        gaze_dir = gaze_dir / np.linalg.norm(gaze_dir)
        plane_point = head_center + head_forward*(50.0*upc)
        denom = np.dot(head_forward, gaze_dir)
        if abs(denom) > 1e-6:
            t = np.dot(head_forward, plane_point-gaze_origin) / denom
            center_w = gaze_origin + t*gaze_dir
        else:
            center_w = head_center + head_forward*(50.0*upc)
    else:
        center_w = head_center + head_forward*(50.0*upc)
    world_up   = np.array([0,-1,0], dtype=float)
    head_right = np.cross(world_up, head_forward); head_right /= np.linalg.norm(head_right)
    head_up    = np.cross(head_forward, head_right); head_up   /= np.linalg.norm(head_up)
    p0 = center_w - head_right*half_w - head_up*half_h
    p1 = center_w + head_right*half_w - head_up*half_h
    p2 = center_w + head_right*half_w + head_up*half_h
    p3 = center_w - head_right*half_w + head_up*half_h
    normal_w = head_forward / (np.linalg.norm(head_forward)+1e-9)
    return [p0,p1,p2,p3], center_w, normal_w, upc


def update_orbit_from_keys():
    global orbit_yaw, orbit_pitch, orbit_radius
    ys, ps, zs = math.radians(1.5), math.radians(1.5), 12.0
    changed = False
    if keyboard.is_pressed('j'):  orbit_yaw   -= ys; changed = True
    if keyboard.is_pressed('l'):  orbit_yaw   += ys; changed = True
    if keyboard.is_pressed('i'):  orbit_pitch += ps; changed = True
    if keyboard.is_pressed('k'):  orbit_pitch -= ps; changed = True
    if keyboard.is_pressed('['):  orbit_radius += zs; changed = True
    if keyboard.is_pressed(']'):  orbit_radius = max(80.0, orbit_radius-zs); changed = True
    if keyboard.is_pressed('r'):  orbit_yaw=0.0; orbit_pitch=0.0; orbit_radius=600.0; changed=True
    orbit_pitch  = max(math.radians(-89), min(math.radians(89), orbit_pitch))
    orbit_radius = max(80.0, orbit_radius)
    if changed: print(f"[Orbit] yaw={math.degrees(orbit_yaw):.1f} pitch={math.degrees(orbit_pitch):.1f} r={orbit_radius:.0f}")


def compute_scale(points_3d):
    n = len(points_3d); total = 0; count = 0
    for i in range(n):
        for j in range(i+1,n):
            total += np.linalg.norm(points_3d[i]-points_3d[j]); count += 1
    return total/count if count>0 else 1.0


def draw_gaze(frame, eye_center, iris_center, eye_radius, color, gl):
    d = iris_center-eye_center; d /= np.linalg.norm(d)
    ep = eye_center+d*gl
    cv2.line(frame, tuple(int(v) for v in eye_center[:2]), tuple(int(v) for v in ep[:2]), color, 2)
    io_ = eye_center+d*(1.2*eye_radius)
    cv2.line(frame, (int(eye_center[0]),int(eye_center[1])), (int(io_[0]),int(io_[1])), color, 1)
    cv2.line(frame, (int(io_[0]),int(io_[1])), (int(ep[0]),int(ep[1])), color, 1)


def draw_wireframe_cube(frame, center, R, size=80):
    right=R[:,0]; up=-R[:,1]; forward=-R[:,2]; hw=hh=hd=size
    def corner(xs,ys,zs): return center+xs*hw*right+ys*hh*up+zs*hd*forward
    corners   = [corner(x,y,z) for x in [-1,1] for y in [1,-1] for z in [-1,1]]
    projected = [(int(pt[0]),int(pt[1])) for pt in corners]
    for i,j in [(0,1),(1,3),(3,2),(2,0),(4,5),(5,7),(7,6),(6,4),(0,4),(1,5),(2,6),(3,7)]:
        cv2.line(frame, projected[i], projected[j], (255,128,0), 2)


def compute_and_draw_coordinate_box(frame, face_landmarks, indices,
                                    ref_matrix_container, color=(0,255,0), size=80):
    points_3d = np.array([[face_landmarks[i].x*w, face_landmarks[i].y*h, face_landmarks[i].z*w] for i in indices])
    center = np.mean(points_3d, axis=0)
    for i in indices: cv2.circle(frame, (int(face_landmarks[i].x*w), int(face_landmarks[i].y*h)), 3, color, -1)
    centered = points_3d-center
    _, eigvecs = np.linalg.eigh(np.cov(centered.T))
    eigvecs = eigvecs[:, np.argsort(-np.linalg.eigh(np.cov(centered.T))[0])]
    if np.linalg.det(eigvecs) < 0: eigvecs[:,2] *= -1
    r = Rscipy.from_matrix(eigvecs)
    R_final = Rscipy.from_euler('zyx', r.as_euler('zyx', degrees=False)).as_matrix()
    if ref_matrix_container[0] is None:
        ref_matrix_container[0] = R_final.copy()
    else:
        R_ref = ref_matrix_container[0]
        for i in range(3):
            if np.dot(R_final[:,i], R_ref[:,i]) < 0: R_final[:,i] *= -1
    draw_wireframe_cube(frame, center, R_final, size)
    for i, (d, c) in enumerate(zip([R_final[:,0],-R_final[:,1],-R_final[:,2]], [(0,255,0),(0,0,255),(255,0,0)])):
        ep = center+d*(size*1.2)
        cv2.line(frame, (int(center[0]),int(center[1])), (int(ep[0]),int(ep[1])), c, 2)
    return center, R_final, points_3d


def convert_gaze_to_screen_coordinates(combined_gaze_direction, off_yaw, off_pitch):
    d = combined_gaze_direction / np.linalg.norm(combined_gaze_direction)
    xz = np.array([d[0],0,d[2]]); xz /= np.linalg.norm(xz)
    yaw_r = math.acos(np.clip(np.dot([0,0,-1], xz), -1, 1))
    if d[0] < 0: yaw_r = -yaw_r
    yz = np.array([0,d[1],d[2]]); yz /= np.linalg.norm(yz)
    pitch_r = math.acos(np.clip(np.dot([0,0,-1], yz), -1, 1))
    if d[1] > 0: pitch_r = -pitch_r
    yd = np.degrees(yaw_r); pd = np.degrees(pitch_r)
    if yd < 0: yd = -yd
    elif yd > 0: yd = -yd
    raw_y, raw_p = yd, pd
    yd += off_yaw; pd += off_pitch
    sx = int(((yd+15)/(30))*MONITOR_WIDTH)
    sy = int(((5-pd)/(10))*MONITOR_HEIGHT)
    return max(10,min(sx,MONITOR_WIDTH-10)), max(10,min(sy,MONITOR_HEIGHT-10)), raw_y, raw_p


def render_debug_view_orbit(fh, fw, head_center3d=None,
    sphere_world_l=None, scaled_radius_l=None,
    sphere_world_r=None, scaled_radius_r=None,
    iris3d_l=None, iris3d_r=None,
    left_locked=False, right_locked=False,
    landmarks3d=None, combined_dir=None, gaze_len=430,
    monitor_corners=None, monitor_center=None, monitor_normal=None,
    gaze_markers_arg=None, corner_world_pts_arg=None, corner_calib_step_arg=0):
    if head_center3d is None: return
    debug  = np.zeros((fh, fw, 3), dtype=np.uint8)
    head_w = np.asarray(head_center3d, dtype=float)
    global debug_world_frozen, orbit_pivot_frozen
    if debug_world_frozen and orbit_pivot_frozen is not None:
        pivot_w = np.asarray(orbit_pivot_frozen, dtype=float)
    else:
        pivot_w = (head_w+np.asarray(monitor_center))*0.5 if monitor_center is not None else head_w
    f_px = _focal_px(fw, orbit_fov_deg)
    cam_pos = pivot_w + _rot_y(orbit_yaw)@(_rot_x(orbit_pitch)@np.array([0.,0.,orbit_radius]))
    up_world = np.array([0.,-1.,0.])
    fwd = _normalize(pivot_w-cam_pos); right = _normalize(np.cross(fwd,up_world)); up = _normalize(np.cross(right,fwd))
    V = np.stack([right,up,fwd],axis=0)

    def pp(P):
        Pc = V@(np.asarray(P,dtype=float)-cam_pos)
        if Pc[2]<=1e-3: return None
        x=f_px*(Pc[0]/Pc[2])+fw*0.5; y=-f_px*(Pc[1]/Pc[2])+fh*0.5
        if not(np.isfinite(x) and np.isfinite(y)): return None
        return (int(x),int(y)),Pc[2]

    def cross3(P,sz=12,col=(255,0,255),th=2):
        r=pp(P)
        if r is None: return
        x,y=r[0]; cv2.line(debug,(x-sz,y),(x+sz,y),col,th); cv2.line(debug,(x,y-sz),(x,y+sz),col,th)

    def arr3(P0,P1,col=(0,200,255),th=3):
        a=pp(P0); b=pp(P1)
        if a is None or b is None: return
        p0p,p1p=a[0],b[0]; cv2.line(debug,p0p,p1p,col,th)
        v=np.array([p1p[0]-p0p[0],p1p[1]-p0p[1]],dtype=float); n=np.linalg.norm(v)
        if n>1e-3:
            v/=n; l=np.array([-v[1],v[0]]); ah=10
            cv2.line(debug,p1p,(int(p1p[0]-v[0]*ah+l[0]*ah*0.6),int(p1p[1]-v[1]*ah+l[1]*ah*0.6)),col,th)
            cv2.line(debug,p1p,(int(p1p[0]-v[0]*ah-l[0]*ah*0.6),int(p1p[1]-v[1]*ah-l[1]*ah*0.6)),col,th)

    if landmarks3d is not None:
        for P in landmarks3d:
            r=pp(P)
            if r: cv2.circle(debug,r[0],0,(200,200,200),-1)

    cross3(head_w,sz=12,col=(255,0,255),th=2)
    hc2d=pp(head_w)
    if hc2d: cv2.putText(debug,"Head Center",(hc2d[0][0]+12,hc2d[0][1]-12),cv2.FONT_HERSHEY_SIMPLEX,0.5,(255,0,255),1,cv2.LINE_AA)
    cross3(pivot_w,sz=8,col=(180,120,255),th=2)
    if monitor_center is not None:
        mc2d=pp(monitor_center); pv2d=pp(pivot_w)
        if mc2d and pv2d and hc2d:
            cv2.line(debug,pv2d[0],hc2d[0],(160,100,255),1)
            cv2.line(debug,pv2d[0],mc2d[0],(160,100,255),1)

    left_dir=right_dir=None
    if left_locked and sphere_world_l is not None:
        r=pp(sphere_world_l)
        if r:
            (cx2,cy2),z=r; rpx=max(2,int((scaled_radius_l or 6)*f_px/max(z,1e-3)))
            cv2.circle(debug,(cx2,cy2),rpx,(255,255,25),1)
            if iris3d_l is not None:
                left_dir=np.asarray(iris3d_l)-np.asarray(sphere_world_l)
                p1=pp(np.asarray(sphere_world_l)+_normalize(left_dir)*gaze_len)
                if p1: cv2.line(debug,(cx2,cy2),p1[0],(155,155,25),1)
    elif iris3d_l is not None:
        r=pp(iris3d_l)
        if r: cv2.circle(debug,r[0],2,(255,255,25),1)

    if right_locked and sphere_world_r is not None:
        r=pp(sphere_world_r)
        if r:
            (cx2,cy2),z=r; rpx=max(2,int((scaled_radius_r or 6)*f_px/max(z,1e-3)))
            cv2.circle(debug,(cx2,cy2),rpx,(25,255,255),1)
            if iris3d_r is not None:
                right_dir=np.asarray(iris3d_r)-np.asarray(sphere_world_r)
                p1=pp(np.asarray(sphere_world_r)+_normalize(right_dir)*gaze_len)
                if p1: cv2.line(debug,(cx2,cy2),p1[0],(25,155,155),1)
    elif iris3d_r is not None:
        r=pp(iris3d_r)
        if r: cv2.circle(debug,r[0],2,(25,255,255),1)

    if left_locked and right_locked and sphere_world_l is not None and sphere_world_r is not None:
        om=(np.asarray(sphere_world_l)+np.asarray(sphere_world_r))/2.0
        if combined_dir is None and (left_dir is not None or right_dir is not None):
            parts=[_normalize(d) for d in [left_dir,right_dir] if d is not None]
            if parts: combined_dir=_normalize(np.mean(parts,axis=0))
        if combined_dir is not None:
            p0r=pp(om); p1r=pp(om+_normalize(combined_dir)*(gaze_len*1.2))
            if p0r and p1r: cv2.line(debug,p0r[0],p1r[0],(155,200,10),2)

    if monitor_corners is not None:
        def dpoly(pts,col,th):
            projs=[pp(p) for p in pts]
            if any(p is None for p in projs): return
            p2=[p[0] for p in projs]
            for a,b in zip(p2,p2[1:]+[p2[0]]): cv2.line(debug,a,b,col,th)
        dpoly(monitor_corners,(0,200,255),2)
        dpoly([monitor_corners[0],monitor_corners[2]],(0,150,210),1)
        dpoly([monitor_corners[1],monitor_corners[3]],(0,150,210),1)
        if monitor_center is not None:
            cross3(monitor_center,sz=8,col=(0,200,255),th=2)
            if monitor_normal is not None:
                tip=np.asarray(monitor_center)+np.asarray(monitor_normal)*(20.0*(units_per_cm or 1.0))
                arr3(monitor_center,tip,col=(0,220,255),th=2)

    if gaze_markers_arg and monitor_corners is not None:
        p0,p1,p2,p3=[np.asarray(p,dtype=float) for p in monitor_corners]
        u=p1-p0; v=p3-p0; ww=float(np.linalg.norm(u))
        if ww>1e-9:
            u_hat=u/ww; rw=0.01*ww
            for (a,b) in gaze_markers_arg:
                Pm=p0+a*u+b*v; projP=pp(Pm); projR=pp(Pm+u_hat*rw)
                if projP and projR:
                    cp=projP[0]; rpx=int(max(1,np.linalg.norm(np.array(projR[0])-np.array(cp))))
                    cv2.circle(debug,cp,rpx,(0,255,0),1,lineType=cv2.LINE_AA)

    corner_colors=[(0,60,255),(255,60,0),(0,200,100),(200,0,200)]
    corner_names=["BR(0,0)","BL","TL","TR"]
    if corner_world_pts_arg:
        for ci,cpt in enumerate(corner_world_pts_arg):
            r=pp(cpt)
            if r:
                px2,py2=r[0]; cv2.circle(debug,(px2,py2),8,corner_colors[ci],-1)
                cv2.putText(debug,corner_names[ci],(px2+10,py2),cv2.FONT_HERSHEY_SIMPLEX,0.5,corner_colors[ci],1,cv2.LINE_AA)
        if len(corner_world_pts_arg)==4:
            projs=[pp(corner_world_pts_arg[i]) for i in range(4)]
            if all(p is not None for p in projs):
                pts_px=[p[0] for p in projs]
                for ai,bi in zip(range(4),[1,2,3,0]): cv2.line(debug,pts_px[ai],pts_px[bi],(80,200,255),1)

    if corner_calib_step_arg < 4:
        cv2.putText(debug,f"Colturi: {corner_calib_step_arg}/4",(10,30),cv2.FONT_HERSHEY_SIMPLEX,0.55,(0,220,255),1,cv2.LINE_AA)

    global recording_active, recording_start_t, recorded_coords
    if recording_active and recording_start_t is not None:
        elapsed=time.time()-recording_start_t; remaining=max(0.0,RECORDING_DURATION-elapsed); pct=min(1.0,elapsed/RECORDING_DURATION)
        bw=fw-20; bx0,by0=10,fh-28
        cv2.rectangle(debug,(bx0,by0),(bx0+bw,by0+14),(60,60,60),-1)
        cv2.rectangle(debug,(bx0,by0),(bx0+int(pct*bw),by0+14),(0,200,80),-1)
        phase_str = "IMG" if elapsed < PHASE1_DURATION else f"DOTS #{_dot_orange_idx+1}"
        cv2.putText(debug,f"REC {remaining:.1f}s  |  {len(recorded_coords)} pts  |  {phase_str}",(bx0+4,by0+11),cv2.FONT_HERSHEY_SIMPLEX,0.45,(255,255,255),1)

    help_text=["C = calibrate eye spheres","T = start 60s recording","S = screen center calib",
               "J/L=yaw I/K=pitch [ ]=zoom R=reset","X = add marker  Q = quit","F7 = toggle mouse"]
    fs=cv2.FONT_HERSHEY_SIMPLEX; y0=fh-len(help_text)*18-20
    for i,t in enumerate(help_text): cv2.putText(debug,t,(10,y0+i*18),fs,0.45,(200,200,200),1,cv2.LINE_AA)
    cv2.imshow("Head/Eye Debug", debug)


# ============================================================
#  MOUSE MOVER THREAD
# ============================================================
def mouse_mover():
    while True:
        if mouse_control_enabled:
            with mouse_lock: x,y=mouse_target
            pyautogui.moveTo(x, y)
        time.sleep(0.01)

threading.Thread(target=mouse_mover, daemon=True).start()

# ============================================================
#  EYE SPHERE STATE
# ============================================================
left_sphere_locked           = False
left_sphere_local_offset     = None
left_calibration_nose_scale  = None

right_sphere_locked          = False
right_sphere_local_offset    = None
right_calibration_nose_scale = None

# ============================================================
#  MAIN LOOP
# ============================================================
while cap.isOpened():
    ret, frame = cap.read()
    if not ret: break

    combined_dir=None; sphere_world_l=None; sphere_world_r=None
    scaled_radius_l=None; scaled_radius_r=None; avg_combined_direction=None

    frame_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    results   = face_mesh.process(frame_rgb)

    if calibration_mode:
        show_calibration_screen(avg_gaze_dir=avg_combined_direction)

        if results.multi_face_landmarks:
            face_landmarks = results.multi_face_landmarks[0].landmark
            left_iris  = face_landmarks[468]
            right_iris = face_landmarks[473]
            head_center, R_final, nose_points_3d = compute_and_draw_coordinate_box(
                frame, face_landmarks, nose_indices, R_ref_nose, color=(0,255,0), size=80)
            base_radius   = 20
            iris_3d_left  = np.array([left_iris.x*w,  left_iris.y*h,  left_iris.z*w])
            iris_3d_right = np.array([right_iris.x*w, right_iris.y*h, right_iris.z*w])

            if left_sphere_locked and right_sphere_locked:
                cns = compute_scale(nose_points_3d)
                sr  = cns/left_calibration_nose_scale  if left_calibration_nose_scale  else 1.0
                srr = cns/right_calibration_nose_scale if right_calibration_nose_scale else 1.0
                sphere_world_l  = head_center+R_final@(left_sphere_local_offset *sr)
                sphere_world_r  = head_center+R_final@(right_sphere_local_offset*srr)
                scaled_radius_l = int(base_radius*sr); scaled_radius_r = int(base_radius*srr)
                lgd = iris_3d_left -sphere_world_l;  lgd /= np.linalg.norm(lgd)
                rgd = iris_3d_right-sphere_world_r; rgd /= np.linalg.norm(rgd)
                rc  = (lgd+rgd)/2; rc /= np.linalg.norm(rc)
                combined_gaze_directions.append(rc)
                avg_combined_direction = np.mean(combined_gaze_directions,axis=0)
                avg_combined_direction /= np.linalg.norm(avg_combined_direction)
            update_orbit_from_keys()

        cv2.imshow("Integrated Eye Tracking", frame)
        key = cv2.waitKey(1) & 0xFF

        if key == ord('q'):
            break
        elif key == ord('c'):
            if calibration_stage == 0:
                calibration_stage = 1; current_calib_index = 0
                print(f"[Calib] Center confirmat. Incepe calibrarea {N_CALIB_POINTS} puncte.")
                if results.multi_face_landmarks and 'head_center' in dir():
                    cns2 = compute_scale(nose_points_3d)
                    cdl  = R_final.T@np.array([0,0,1]); br2 = 20
                    left_sphere_local_offset     = R_final.T@(iris_3d_left -head_center)+br2*cdl
                    left_calibration_nose_scale  = cns2; left_sphere_locked  = True
                    right_sphere_local_offset    = R_final.T@(iris_3d_right-head_center)+br2*cdl
                    right_calibration_nose_scale = cns2; right_sphere_locked = True
                    swl=head_center+R_final@left_sphere_local_offset
                    swr=head_center+R_final@right_sphere_local_offset
                    ldv=iris_3d_left -swl; ldv/=np.linalg.norm(ldv) if np.linalg.norm(ldv)>1e-9 else 1
                    rdv=iris_3d_right-swr; rdv/=np.linalg.norm(rdv) if np.linalg.norm(rdv)>1e-9 else 1
                    fwh=(ldv+rdv)*0.5
                    if np.linalg.norm(fwh)>1e-9: fwh/=np.linalg.norm(fwh)
                    else: fwh=None
                    goc=(swl+swr)/2
                    monitor_corners,monitor_center_w,monitor_normal_w,units_per_cm = create_monitor_plane(
                        head_center,R_final,face_landmarks,w,h,forward_hint=fwh,gaze_origin=goc,gaze_dir=fwh)
                    debug_world_frozen=True; orbit_pivot_frozen=monitor_center_w.copy()
                    print("[Calib] Sfere blocate + plan monitor creat.")
                else:
                    print("[Calib] Nu s-a detectat fata.")
            else:
                if results.multi_face_landmarks and 'head_center' in dir():
                    cns2=compute_scale(nose_points_3d); cdl=R_final.T@np.array([0,0,1]); br2=20
                    left_sphere_local_offset=R_final.T@(iris_3d_left -head_center)+br2*cdl
                    left_calibration_nose_scale=cns2; left_sphere_locked=True
                    right_sphere_local_offset=R_final.T@(iris_3d_right-head_center)+br2*cdl
                    right_calibration_nose_scale=cns2; right_sphere_locked=True
                    swl=head_center+R_final@left_sphere_local_offset
                    swr=head_center+R_final@right_sphere_local_offset
                    ldv=iris_3d_left-swl; ldv/=np.linalg.norm(ldv) if np.linalg.norm(ldv)>1e-9 else 1
                    rdv=iris_3d_right-swr; rdv/=np.linalg.norm(rdv) if np.linalg.norm(rdv)>1e-9 else 1
                    fwh=(ldv+rdv)*0.5
                    if np.linalg.norm(fwh)>1e-9: fwh/=np.linalg.norm(fwh)
                    else: fwh=None
                    goc=(swl+swr)/2
                    monitor_corners,monitor_center_w,monitor_normal_w,units_per_cm=create_monitor_plane(
                        head_center,R_final,face_landmarks,w,h,forward_hint=fwh,gaze_origin=goc,gaze_dir=fwh)
                    debug_world_frozen=True; orbit_pivot_frozen=monitor_center_w.copy()
                    print("[C] Sfere re-blocate.")
        elif key == ord('m') and calibration_stage == 1:
            if not (left_sphere_locked and right_sphere_locked):
                print("[Calib] Apasa C intai pentru a bloca sferele oculare.")
            elif avg_combined_direction is None:
                print("[Calib] Directia gaze nu e disponibila inca.")
            elif sphere_world_l is not None and sphere_world_r is not None:
                O = (sphere_world_l+sphere_world_r)/2
                advance_calib_point_stage1(O, avg_combined_direction)
            else:
                print("[Calib] Sferele nu sunt inca in spatiu.")
        elif key == ord('f'):
            prop = cv2.getWindowProperty(CALIB_WIN_NAME, cv2.WND_PROP_FULLSCREEN)
            if prop == cv2.WINDOW_FULLSCREEN:
                cv2.setWindowProperty(CALIB_WIN_NAME, cv2.WND_PROP_FULLSCREEN, cv2.WINDOW_NORMAL)
            else:
                cv2.setWindowProperty(CALIB_WIN_NAME, cv2.WND_PROP_FULLSCREEN, cv2.WINDOW_FULLSCREEN)
        continue

    # ============================================================
    #  NORMAL EYE TRACKING
    # ============================================================
    if results.multi_face_landmarks:
        face_landmarks = results.multi_face_landmarks[0].landmark
        left_iris  = face_landmarks[468]; right_iris = face_landmarks[473]
        head_center, R_final, nose_points_3d = compute_and_draw_coordinate_box(
            frame, face_landmarks, nose_indices, R_ref_nose, color=(0,255,0), size=80)
        base_radius = 20

        if not left_sphere_locked:
            cv2.circle(frame,(int(left_iris.x*w),int(left_iris.y*h)),10,(255,25,25),2)
        else:
            cns=compute_scale(nose_points_3d); sr=cns/left_calibration_nose_scale if left_calibration_nose_scale else 1.0
            sphere_world_l=head_center+R_final@(left_sphere_local_offset*sr); scaled_radius_l=int(base_radius*sr)
            cv2.circle(frame,(int(sphere_world_l[0]),int(sphere_world_l[1])),scaled_radius_l,(255,255,25),2)

        if not right_sphere_locked:
            cv2.circle(frame,(int(right_iris.x*w),int(right_iris.y*h)),10,(25,255,25),2)
        else:
            cns=compute_scale(nose_points_3d); srr=cns/right_calibration_nose_scale if right_calibration_nose_scale else 1.0
            sphere_world_r=head_center+R_final@(right_sphere_local_offset*srr); scaled_radius_r=int(base_radius*srr)
            cv2.circle(frame,(int(sphere_world_r[0]),int(sphere_world_r[1])),scaled_radius_r,(25,255,255),2)

        iris_3d_left  = np.array([left_iris.x*w,  left_iris.y*h,  left_iris.z*w])
        iris_3d_right = np.array([right_iris.x*w, right_iris.y*h, right_iris.z*w])

        if left_sphere_locked and right_sphere_locked:
            draw_gaze(frame,sphere_world_l,iris_3d_left, scaled_radius_l,(55,255,0),130)
            draw_gaze(frame,sphere_world_r,iris_3d_right,scaled_radius_r,(55,255,0),130)
            lgd=iris_3d_left -sphere_world_l; lgd/=np.linalg.norm(lgd)
            rgd=iris_3d_right-sphere_world_r; rgd/=np.linalg.norm(rgd)
            rc=(lgd+rgd)/2; rc/=np.linalg.norm(rc)
            combined_gaze_directions.append(rc)
            avg_combined_direction=np.mean(combined_gaze_directions,axis=0)
            avg_combined_direction/=np.linalg.norm(avg_combined_direction)
            combined_dir=avg_combined_direction

            sx,sy,ry,rp=convert_gaze_to_screen_coordinates(avg_combined_direction,calibration_offset_yaw,calibration_offset_pitch)
            if mouse_control_enabled:
                with mouse_lock: mouse_target[0]=sx; mouse_target[1]=sy
            try: write_screen_position(sx,sy)
            except: pass

            co=(sphere_world_l+sphere_world_r)/2
            ct=co+avg_combined_direction*gaze_length
            cv2.line(frame,(int(co[0]),int(co[1])),(int(ct[0]),int(ct[1])),(255,255,10),3)

            if recording_active and monitor_tuned:
                elapsed=time.time()-recording_start_t; cur_sec=int(elapsed)
                if elapsed >= RECORDING_DURATION:
                    stop_recording_and_save()   # ★ include trimitere automata
                elif cur_sec > last_record_second:
                    O=(sphere_world_l+sphere_world_r)/2
                    res2=intersect_gaze_with_tuned_monitor(O,avg_combined_direction)
                    if res2 is not None:
                        mx,my=res2
                        phase = 1 if elapsed < PHASE1_DURATION else 2
                        recorded_coords.append((cur_sec,mx,my))
                        last_record_second=cur_sec
                        dot_info = f"  dot=#{_dot_orange_idx+1}" if phase == 2 else ""
                        print(f"  [REC t={cur_sec:3d}s P{phase}]  mx={mx:.3f}  my={my:.3f}{dot_info}")

            if recording_active:
                _update_stimulus_overlay()

            texts=[f"Screen: ({sx}, {sy})"]
            if recording_active and recording_start_t is not None:
                elapsed2=time.time()-recording_start_t
                rem=max(0.0,RECORDING_DURATION-elapsed2)
                phase_str="FAZA 1: Imagine" if elapsed2 < PHASE1_DURATION else f"FAZA 2: Dot #{_dot_orange_idx+1}"
                texts.append(f"REC {rem:.1f}s  |  {phase_str}")
            for i,text in enumerate(texts):
                (tw,_),_=cv2.getTextSize(text,cv2.FONT_HERSHEY_SIMPLEX,0.7,2)
                cv2.putText(frame,text,((w-tw)//2,30+i*30),cv2.FONT_HERSHEY_SIMPLEX,0.7,(0,255,0),2)

        status = "Monitor CALIBRAT  |  T = start REC" if monitor_tuned else f"Colturi: {corner_calib_step}/4"
        cv2.putText(frame,status,(10,h-15),cv2.FONT_HERSHEY_SIMPLEX,0.55,(0,255,120) if monitor_tuned else (0,220,255),1,cv2.LINE_AA)

        for lm in face_landmarks: cv2.circle(frame,(int(lm.x*w),int(lm.y*h)),0,(255,255,255),-1)
        update_orbit_from_keys()
        landmarks3d=np.array([[p.x*w,p.y*h,p.z*w] for p in results.multi_face_landmarks[0].landmark],dtype=float)
        render_debug_view_orbit(h,w,
            head_center3d=head_center,
            sphere_world_l=sphere_world_l,scaled_radius_l=scaled_radius_l,
            sphere_world_r=sphere_world_r,scaled_radius_r=scaled_radius_r,
            iris3d_l=iris_3d_left if 'iris_3d_left' in dir() else None,
            iris3d_r=iris_3d_right if 'iris_3d_right' in dir() else None,
            left_locked=left_sphere_locked,right_locked=right_sphere_locked,
            landmarks3d=landmarks3d,combined_dir=avg_combined_direction,gaze_len=5230,
            monitor_corners=monitor_corners,monitor_center=monitor_center_w,monitor_normal=monitor_normal_w,
            gaze_markers_arg=gaze_markers,corner_world_pts_arg=corner_world_pts,corner_calib_step_arg=corner_calib_step)

    cv2.imshow("Integrated Eye Tracking", frame)

    if keyboard.is_pressed('f7'):
        mouse_control_enabled=not mouse_control_enabled
        print(f"[Mouse] {'ON' if mouse_control_enabled else 'OFF'}"); time.sleep(0.3)

    key = cv2.waitKey(1) & 0xFF
    if key == ord('q'): break
    elif key == ord('c') and 'head_center' in dir() and not (left_sphere_locked and right_sphere_locked):
        cns=compute_scale(nose_points_3d); cdl=R_final.T@np.array([0,0,1])
        left_sphere_local_offset=R_final.T@(iris_3d_left -head_center)+base_radius*cdl; left_calibration_nose_scale=cns; left_sphere_locked=True
        right_sphere_local_offset=R_final.T@(iris_3d_right-head_center)+base_radius*cdl; right_calibration_nose_scale=cns; right_sphere_locked=True
        swl=head_center+R_final@left_sphere_local_offset; swr=head_center+R_final@right_sphere_local_offset
        ldv=iris_3d_left-swl; ldv/=np.linalg.norm(ldv) if np.linalg.norm(ldv)>1e-9 else 1
        rdv=iris_3d_right-swr; rdv/=np.linalg.norm(rdv) if np.linalg.norm(rdv)>1e-9 else 1
        fwh=(ldv+rdv)*0.5
        if np.linalg.norm(fwh)>1e-9: fwh/=np.linalg.norm(fwh)
        else: fwh=None
        goc=(swl+swr)/2
        monitor_corners,monitor_center_w,monitor_normal_w,units_per_cm=create_monitor_plane(
            head_center,R_final,face_landmarks,w,h,forward_hint=fwh,gaze_origin=goc,gaze_dir=fwh)
        debug_world_frozen=True; orbit_pivot_frozen=monitor_center_w.copy()
        print("[C] Sfere blocate. Plan monitor creat.")
    elif key == ord('t'):
        if recording_active: print("[T] Inregistrarea e deja activa.")
        else: start_recording()
    elif key == ord('f') and recording_active:
        prop=cv2.getWindowProperty(STIMULUS_WIN_NAME,cv2.WND_PROP_FULLSCREEN)
        if prop==cv2.WINDOW_FULLSCREEN:
            cv2.setWindowProperty(STIMULUS_WIN_NAME,cv2.WND_PROP_FULLSCREEN,cv2.WINDOW_NORMAL)
        else:
            cv2.setWindowProperty(STIMULUS_WIN_NAME,cv2.WND_PROP_FULLSCREEN,cv2.WINDOW_FULLSCREEN)
    elif key == ord('s') and left_sphere_locked and right_sphere_locked and avg_combined_direction is not None:
        _,_,ry,rp=convert_gaze_to_screen_coordinates(avg_combined_direction,0,0)
        calibration_offset_yaw=-ry; calibration_offset_pitch=-rp
        print(f"[S] Screen calibrat. yaw={calibration_offset_yaw:.2f} pitch={calibration_offset_pitch:.2f}")
    elif key == ord('x'):
        if (monitor_corners is not None and monitor_center_w is not None and monitor_normal_w is not None
                and left_sphere_locked and right_sphere_locked and avg_combined_direction is not None):
            O=(sphere_world_l+sphere_world_r)*0.5; D=_normalize(avg_combined_direction)
            C_pt=np.asarray(monitor_center_w); N_pt=_normalize(np.asarray(monitor_normal_w))
            denom=float(np.dot(N_pt,D))
            if abs(denom)>1e-6:
                t=float(np.dot(N_pt,(C_pt-O))/denom)
                if t>0:
                    P=O+t*D; p0,p1p,p2,p3=[np.asarray(p) for p in monitor_corners]
                    u=p1p-p0; v=p3-p0; u2=float(np.dot(u,u)); v2=float(np.dot(v,v))
                    if u2>1e-9 and v2>1e-9:
                        wv=P-p0; a=float(np.dot(wv,u)/u2); b=float(np.dot(wv,v)/v2)
                        if 0<=a<=1 and 0<=b<=1:
                            gaze_markers.append((a,b)); print(f"[X] Marker a={a:.3f} b={b:.3f}")
        else: print("[X] Monitor/gaze nu e gata.")

cap.release()
cv2.destroyAllWindows()