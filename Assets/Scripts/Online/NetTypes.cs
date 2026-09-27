using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Vamp.Online
{
    public enum NetState : byte { Lobby = 0, Loading = 1, InMatch = 2, PostMatch = 3 }

    /// <summary>One player in the lobby / match. Replicated by the host in a NetworkList.</summary>
    public struct NetMember : INetworkSerializable, IEquatable<NetMember>
    {
        public ulong ClientId;
        public FixedString32Bytes Name;
        public int Level;
        public int Team;
        public bool Ready;
        public int Kills;
        public int Deaths;
        public bool Bot;
        public bool Matched;
        public int HumanKills;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref ClientId);
            s.SerializeValue(ref Name);
            s.SerializeValue(ref Level);
            s.SerializeValue(ref Team);
            s.SerializeValue(ref Ready);
            s.SerializeValue(ref Kills);
            s.SerializeValue(ref Deaths);
            s.SerializeValue(ref Bot);
            s.SerializeValue(ref Matched);
            s.SerializeValue(ref HumanKills);
        }

        public bool Equals(NetMember o)
        {
            return ClientId == o.ClientId && Name.Equals(o.Name) && Level == o.Level && Team == o.Team && Ready == o.Ready
                   && Kills == o.Kills && Deaths == o.Deaths && Bot == o.Bot && Matched == o.Matched && HumanKills == o.HumanKills;
        }
    }

    /// <summary>Owner → everyone: where a player is and what their body is doing (drives the stick-man animation).</summary>
    public struct NetMotion : INetworkSerializable, IEquatable<NetMotion>
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Yaw;
        public float Pitch;
        public byte Flags;     // 1 grounded, 2 sliding, 4 wall running, 8 wall on right, 16 crouched
        public sbyte Weapon;   // index into the weapon catalog (-1 none)
        public byte Camo;      // WeaponCamo index of the held weapon (0 default)
        public byte Fx;        // kill effect index (Cosmetics.KillFxIndex)
        public byte Trail;     // bullet trail index (Cosmetics.TrailIndex)
        public byte Skin;      // character skin index (CosmeticFx.ToIndex CharacterSkin)

        public const byte Grounded = 1, Sliding = 2, WallRunning = 4, WallRight = 8, Crouched = 16;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Position);
            s.SerializeValue(ref Velocity);
            s.SerializeValue(ref Yaw);
            s.SerializeValue(ref Pitch);
            s.SerializeValue(ref Flags);
            s.SerializeValue(ref Weapon);
            s.SerializeValue(ref Camo);
            s.SerializeValue(ref Fx);
            s.SerializeValue(ref Trail);
            s.SerializeValue(ref Skin);
        }

        public bool Equals(NetMotion o)
        {
            return Position == o.Position && Velocity == o.Velocity && Yaw == o.Yaw && Pitch == o.Pitch && Flags == o.Flags && Weapon == o.Weapon && Camo == o.Camo && Fx == o.Fx && Trail == o.Trail && Skin == o.Skin;
        }
    }
}
