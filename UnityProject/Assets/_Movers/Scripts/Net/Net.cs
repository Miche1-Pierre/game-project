using System;
using System.IO;
using UnityEngine;

namespace Movers
{
    // Online co-op for the slice (ADR-012, 07_MULTIPLAYER/NETCODE_SLICE.md). This file is the
    // facade every gameplay gate reads. Offline is the default and keeps today's code path:
    // HasAuthority is true, IsOnline, IsHost and IsClient are false, Drives and IsLocal are true.
    public enum NetRole : byte { Offline = 0, Host = 1, Client = 2 }

    public static class NetOrder
    {
        public const int WorldApply    = -530; // client: interpolated world poses (Update)
        public const int PlayerDriver  = -520; // both: puppet pose apply (Update), local pose sample (LateUpdate)
        public const int AnchoredApply = 5;    // client: items held by the local player (Update)
        public const int Tick          = 2900; // both: sync ticks, transform stream, flush (LateUpdate)
    }

    public static class Net
    {
        public const int HostMember = 0;
        public const int ClientMember = 1;

        static NetRole role;
        static bool peerConnected, peerReady, scripted;
        static byte epoch;
        static float roundTrip;

        public static NetRole Role => role;
        public static bool IsOnline => role != NetRole.Offline;
        public static bool IsHost => role == NetRole.Host;
        public static bool IsClient => role == NetRole.Client;
        public static bool HasAuthority => role != NetRole.Client;
        public static bool PeerConnected => role != NetRole.Offline && peerConnected;
        public static bool PeerReady => role != NetRole.Offline && peerReady;
        public static byte Epoch => epoch;
        public static int LocalMember => role == NetRole.Host ? HostMember : role == NetRole.Client ? ClientMember : -1;
        public static float RoundTrip => role != NetRole.Offline ? roundTrip : 0f;
        public static bool Scripted => scripted;

        public static bool IsLocal(CrewMember m)
        {
            if (role == NetRole.Offline) return true;
            return m != null && m.index == LocalMember;
        }

        public static bool Drives(CrewMember m) => m == null || Drives(m.index);

        public static bool Drives(int memberIndex)
        {
            switch (role)
            {
                case NetRole.Host: return memberIndex != ClientMember || !peerConnected;
                case NetRole.Client: return memberIndex == ClientMember;
                default: return true;
            }
        }

        internal static void SetRole(NetRole value)
        {
            role = value;
            if (value == NetRole.Offline)
            {
                peerConnected = peerReady = false;
                roundTrip = 0f;
                epoch = 0;
            }
        }

        internal static void SetPeer(bool connected, bool ready)
        {
            peerConnected = connected;
            peerReady = connected && ready;
        }

        internal static void SetEpoch(byte value)
        {
            if (value == epoch) return;
            NetOut.CloseBatches();   // a batch never mixes epochs (4.1)
            epoch = value;
        }

        internal static void SetRoundTrip(float seconds) { roundTrip = Mathf.Max(0f, seconds); }
        internal static void SetScripted(bool on) { scripted = on; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            role = NetRole.Offline;
            peerConnected = peerReady = scripted = false;
            epoch = 0;
            roundTrip = 0f;
        }
    }

    public struct NetPose                            // PlayerController.ApplyNetPose input
    {
        public Vector3 position;
        public float yaw;                            // degrees
        public Quaternion camLocalRotation;          // pitch and drunk sway
        public float height;                         // CharacterController height
        public bool crouching, grounded, throwHeld;
        public Vector3 velocity;
    }

    // The net log: one text file per machine (-netlog <path>), read by the loopback test. Without
    // a path, lines go to the player log. Never written offline, since nothing calls it offline.
    public static class NetLog
    {
        static string path;
        static bool failed;

        public static string Path => path;

        internal static void SetPath(string value)
        {
            path = string.IsNullOrEmpty(value) ? null : value;
            failed = false;
            if (path == null) return;
            try
            {
                var dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, "");
            }
            catch (Exception e) { failed = true; Debug.LogWarning("NetLog: cannot write " + path + ": " + e.Message); }
        }

        public static void Write(string line)
        {
            string stamped = Time.realtimeSinceStartup.ToString("0.000") + " " + line;
            if (path == null || failed) { Debug.Log("[Net] " + line); return; }
            try { File.AppendAllText(path, stamped + "\n"); }
            catch (Exception e) { failed = true; Debug.LogWarning("NetLog: " + e.Message); Debug.Log("[Net] " + line); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { path = null; failed = false; }
    }
}
