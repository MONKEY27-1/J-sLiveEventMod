// Compile-check stubs of the Gorilla Tag / Photon types the two mods use. Members are added only as existing code uses them
// (that code compiles against the real game), so new code checked against these stubs stays on the same proven API surface.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GorillaLocomotion
{
    public class GTPlayer : MonoBehaviour
    {
        public static GTPlayer Instance;
        public static bool hasInstance;
        public static LayerMask LocomotionEnabledLayers;
        public Rigidbody playerRigidBody;
        public Collider headCollider, bodyCollider;
        public GameObject turnParent;
        public Camera mainCamera;
        public Vector3 HeadCenterPosition;
        public bool forcedUnderwater, disableMovement;
        public float scale, jumpMultiplier, maxJumpSpeed, maxArmLength, slideControl;
        public HandState LeftHand, RightHand;
        public bool IsGroundedHand, BodyOnGround;
        public void ApplyKnockback(Vector3 dir, float speed, bool forceOffTheGround = false) { }
        public void TeleportTo(Vector3 p, Quaternion r, bool keepVelocity = false, bool center = false) { }
        public void SetMaximumSlipThisFrame() { }
        public void SetScaleMultiplier(float s) { }

        public class HandState
        {
            public bool isColliding, wasColliding;
            public RaycastHit lastHitInfo;
        }
    }
}
namespace GorillaLocomotion.Swimming
{
    public class WaterVolume : MonoBehaviour { public List<Collider> volumeColliders = new List<Collider>(); }
}
namespace GorillaTagScripts
{
    public class LurkerGhost : MonoBehaviour { }
}
namespace GorillaGameModes
{
    public enum GameModeType { Casual, Infection, HuntDown, Paintbrawl, FreezeTag, Ghost, Ambush, Guardian, SuperInfect }
    public class GameMode
    {
        public static List<string> gameModeNames = new List<string>();
        public static GorillaGameManager ActiveGameMode;
        public static List<NetPlayer> ParticipatingPlayers = new List<NetPlayer>();
        public static bool LocalIsTagged(NetPlayer p) { return false; }
        public static void ReportTag(NetPlayer p) { }
        public static GorillaGameManager GetGameModeInstance(GameModeType t) { return null; }
    }
}
namespace GorillaNetworking
{
    public class PhotonNetworkController : MonoBehaviour { public static PhotonNetworkController Instance; public bool disableAFKKick; public GorillaNetworkJoinTrigger currentJoinTrigger; public void AttemptToJoinPublicRoom(GorillaNetworkJoinTrigger t) { } }
    public class GorillaNetworkJoinTrigger : MonoBehaviour { }
    public class GorillaComputer : MonoBehaviour { public static GorillaComputer instance; public string savedName, currentName, roomToJoin; public bool NametagsEnabled; public void UpdateColor(float r, float g, float b) { } public void ProcessNameState(GorillaKeyboardBindings b) { } }
}
namespace Photon.Pun
{
    public enum RpcTarget { All, Others, MasterClient, AllBuffered, OthersBuffered, AllViaServer, AllBufferedViaServer }
    public static class PhotonNetwork
    {
        public static Photon.Realtime.Room CurrentRoom;
        public static int SerializationRate;
        public static string CloudRegion;
        public static int GetPing() { return 0; }
    }
}
namespace Photon.Realtime
{
    public class Room { public bool IsOpen, IsVisible; public bool SetCustomProperties(ExitGames.Client.Photon.Hashtable h) { return true; } }
}
namespace ExitGames.Client.Photon
{
    public class Hashtable : Dictionary<object, object> { }
}
namespace Photon.Voice.Unity
{
    public class Recorder : MonoBehaviour
    {
        public enum InputSourceType { Microphone, AudioClip }
        public InputSourceType SourceType;
        public AudioClip AudioClip;
        public bool LoopAudioClip;
        public void RestartRecording(bool force = false) { }
    }
    public class VoiceConnection : MonoBehaviour { public Recorder PrimaryRecorder; }
}

public class NetPlayer { public string NickName; }
public class NetworkSystem : MonoBehaviour
{
    public static NetworkSystem Instance;
    public bool InRoom, SessionIsPrivate, IsMasterClient;
    public string RoomName, GameModeString;
    public int RoomPlayerCount;
    public NetPlayer LocalPlayer, MasterClient;
    public Photon.Voice.Unity.VoiceConnection VoiceConnection;
    public void ReturnToSinglePlayer() { }
}
public class GorillaGameManager : MonoBehaviour { public virtual string GameTypeName() { return ""; } }
public class GorillaTagManager : GorillaGameManager
{
    public List<NetPlayer> currentInfected = new List<NetPlayer>();
    public void AddInfectedPlayer(NetPlayer p) { }
    public void UpdateInfectionState() { }
    public void ClearInfectionState() { }
    public void ResetGame() { }
}
public class GorillaTagger : MonoBehaviour
{
    public static GorillaTagger Instance;
    public VRRig offlineVRRig, myVRRig;
    public GameObject thirdPersonCamera;
    public Transform leftHandTransform, rightHandTransform;
    public Collider headCollider, bodyCollider;
    public float maxTagDistance;
    public void StartVibration(bool forLeftController, float amplitude, float duration) { }
    public void UpdateColor(float r, float g, float b) { }
}
public class VRMap { public Transform rigTarget; public Vector3 syncPos; }
public class VRRig : MonoBehaviour
{
    public static VRRig LocalRig;
    public bool isOfflineVRRig, isLocal;
    public NetPlayer Creator;
    public string playerNameVisible;
    public Transform leftHandTransform, rightHandTransform;
    public GameObject headMesh;
    public VRMap head, leftHand, rightHand;
    public SkinnedMeshRenderer mainSkin;
    public Color playerColor;
    public float scaleFactor = 1f;
    public TextMesh playerText1;
    public void PostTick() { }
    public void ChangeMaterialLocal(int i) { }
    public void PlayHandTapLocal(int sound, bool left, float volume) { }
    public void SendRPC(string m, Photon.Pun.RpcTarget t, params object[] a) { }
}
public static class VRRigCache { public static List<VRRig> ActiveRigs = new List<VRRig>(); }
public class RigContainer : MonoBehaviour
{
    public enum MuteReason { Manual }
    public void SetMuted(MuteReason r, bool on) { }
    public bool IsMutedFor(MuteReason r) { return false; }
}
public class GorillaPlayerScoreboardLine : MonoBehaviour { public NetPlayer linePlayer; public GameObject reportButton; }
public static class GorillaScoreboardTotalUpdater { public static List<GorillaPlayerScoreboardLine> allScoreboardLines = new List<GorillaPlayerScoreboardLine>(); }
public class GorillaSpeakerLoudness : MonoBehaviour { public float LoudnessNormalized; }
public class BetterDayNightManager : MonoBehaviour
{
    public static BetterDayNightManager instance;
    public enum WeatherType { None, Raining }
    public void SetTimeOfDay(int i, bool fixedTime = false) { }
    public void ClearTimeOfDay(bool b = false) { }
    public string GetTimeOfDayString() { return ""; }
    public void SetFixedWeather(WeatherType w, bool b = false) { }
    public void ClearFixedWeather(bool b = false) { }
}
public class ControllerInputPoller : MonoBehaviour
{
    public static ControllerInputPoller instance;
    public float leftControllerGripFloat, rightControllerGripFloat, leftControllerIndexFloat, rightControllerIndexFloat;
    public bool leftControllerPrimaryButton, rightControllerPrimaryButton, leftControllerSecondaryButton, rightControllerSecondaryButton;
    public Vector2 leftControllerPrimary2DAxis, rightControllerPrimary2DAxis;
    public Vector3 leftVelocity, rightVelocity;
}
public enum GTZone { forest, city, canyon, beach, cave, mountain, basement, sky, rotating, mines, metropolis, arcade, bayou, none }
public class ZoneManagement : MonoBehaviour { public static bool IsInZone(GTZone z) { return false; } }
public enum GorillaKeyboardBindings { enter }
public enum EKIDFeatures { Groups, Multiplayer }
public static class KIDManager { public static bool HasPermissionToUseFeature(EKIDFeatures f) { return true; } }
public class ForceVolume : MonoBehaviour { }
