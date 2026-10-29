"""The Final Showdown: timeline shared by the robot script and the cue builder. Times are seconds of the supplied soundtrack (303.5 s); positions are unity-local metres around the
show origin (x right, y up, z forward), standing level of the giants = y 0.  The monster is the real Fortnite event animation (real_monster.py); where it is at any moment is read from
cache/showdown/monster_trace.json, and every robot contact (arm tear, punch, stab) is aligned to it."""
import math
DUR = 303.5
SEQ = [0.0, 47.0, 81.5, 116.5, 153.5, 204.0, 254.0, 303.5]
SEQ_NAMES = ["The Monster Arrives", "The Forest Is Threatened", "The Robot Awakens", "The First Battle", "Ambush and the Energy Source", "Power Punch and the Hidden Sword", "The Final Clash"]

ROBOT_H = 80.0                                      # the robot's body height in metres
ROBOT_JB = 154.78; ROBOT_BODY = 150.0              # the plugin scales a model by height / (joint-bounds height of its rest pose): 163.3 units here vs the 150-unit body the scripts use, so event.json says 87.1 for an 80 m robot

# beats found in the soundtrack / in the monster's real animation (audio seconds)
PEAKS_BATTLE = [162.1, 164.6, 168.1, 171.7, 172.9, 174.5, 175.7, 179.3, 180.9]     # the monster's strikes on the fallen robot
T_ACTIVATE = 90.2
T_BLAST = (74.4, 77.1)
T_MISSILE = (124.6, 127.4, 130.6)                  # the robot's volleys hit the monster as it is thrown into the air and dives into the ground
T_BEAM_HIT = 135.4                                 # a beam erupts from the ground under the robot
T_ERUPT = 156.3                                    # the monster bursts out of the ground (real clip: 156.1)
T_LAND = 160.6                                     # ... and lands on the robot
T_TEAR = 162.4                                     # hand_r jerks away with the arm
T_KICK = 181.1                                     # the robot's palm blast throws the monster off (real clip: its violent flight 180 - 184)
T_FLASH = 205.3                                    # the robot swallows the energy source; the monster stirs at 206
T_CHARGE = 244.0                                   # the monster leaps at the robot
T_PUNCH = 245.6                                    # the powered punch meets it in the air
T_SWORD = 253.4                                    # the giant sword leaves the ground
T_STAB = 260.0                                     # the monster's real clip goes limp here
T_DANCE = 271.5; T_SALUTE = 282.0; T_FLY = 286.0

SWORD_LEN = 58.0
