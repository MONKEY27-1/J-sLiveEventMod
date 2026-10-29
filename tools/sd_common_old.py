"""The Final Showdown: timeline shared by the monster / robot scripts, cue list for the plugin. Times are seconds of the supplied soundtrack (303.5 s);
positions are unity-local metres around the show origin (x right, y up, z forward), standing level of the giants = y 0."""
import math
DUR = 303.5
SEQ = [0.0, 47.0, 81.5, 116.5, 153.5, 204.0, 254.0, 303.5]
SEQ_NAMES = ["The Monster Arrives", "The Forest Is Threatened", "The Robot Awakens", "The First Battle", "Ambush and the Energy Source", "Power Punch and the Hidden Sword", "The Final Clash"]

ROBOT_H = 80.0; MONSTER_H = 110.0
MON_START = (70.0, 175.0); MON_NEAR = (10.0, 118.0)
ROB_START = (100.0, 70.0)
ORB = (10.0, 72.0, 72.0)             # the energy source (the 'Zero Point')
SWORD = (-95.0, 0.0, 70.0)           # where the giant sword is hidden
ROBOT_STAND_ORB = (25.0, 60.0)

# monster peaks / hits found in the soundtrack (loud transients)
PEAKS_BATTLE = [162.1, 164.6, 168.1, 171.7, 172.9, 174.5, 175.7, 179.3, 180.9]
T_ROAR1 = 27.4; T_STOMP = (35.4, 38.6); T_BLAST = (74.4, 77.1); T_ACTIVATE = 90.2
T_MISSILE = (124.6, 127.4, 130.6); T_BEAM_HIT = 135.4; T_TACKLE = 156.6; T_TEAR = 161.0
T_KICK = 187.8; T_ABSORB0 = 192.2; T_FLASH = 205.3; T_PUNCH = 215.0; T_LAND = 219.2
T_SWORD = 237.7; T_LEAP = 244.6; T_STAB = 260.0; T_DANCE = 271.5; T_SALUTE = 282.0; T_FLY = 286.0

# robot ground path (also used by the monster script to aim at the robot)
ROBOT_PATH = [(0, 100, 70), (82, 100, 70), (106, 100, 70), (116, 60, 92), (135.4, 60, 92), (138.5, 82, 82), (150, 82, 82), (156.6, 40, 98), (160, 40, 100), (182, 40, 100),
              (190, 40, 100), (196, 25, 62), (212, 25, 62), (215, 22, 80), (222, 22, 80), (236, -80, 72), (246, -80, 72), (254, -62, 104), (260, -57, 108), (272, -54, 108), (303.5, -54, 108)]
def robot_xz(t):
    p = ROBOT_PATH
    if t <= p[0][0]: return p[0][1], p[0][2]
    for i in range(len(p) - 1):
        if p[i][0] <= t <= p[i + 1][0]:
            u = (t - p[i][0]) / max(1e-6, p[i + 1][0] - p[i][0]); return p[i][1] + (p[i + 1][1] - p[i][1]) * u, p[i][2] + (p[i + 1][2] - p[i][2]) * u
    return p[-1][1], p[-1][2]
def robot_chest(t):
    x, z = robot_xz(t)
    lying = (138.0 <= t <= 150.0) or (161.2 <= t <= 186.0)
    return (x, 8.0 if lying else 50.0, z)
