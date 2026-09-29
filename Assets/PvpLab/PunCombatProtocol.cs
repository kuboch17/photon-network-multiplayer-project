using System;

namespace PvpLab
{
    // Pure wire contract: no Unity dependencies, independently testable.
    public static class PunCombatProtocol
    {
        public const string Version = "hunting-duel-1";
        public const byte InputEvent = 71, SnapshotEvent = 72, RoundEvent = 73;
        public const int MaxInputsPerSecond = 30;
        public const string MatchKey = "duel.match", PlayerOneKey = "duel.p1", PlayerTwoKey = "duel.p2", ReadyKey = "duel.ready";

        public static int Elapsed(int serverTimestamp, int startTimestamp)
        {
            return unchecked(serverTimestamp - startTimestamp); // Photon timestamp wraparound.
        }
        public static bool TryInput(object payload, string round, out int sequence, out CombatSimulation.ActionKind kind)
        {
            sequence = 0; kind = CombatSimulation.ActionKind.Stop;
            var data = payload as object[];
            if (data == null || data.Length != 3 || !(data[0] is string) || (string)data[0] != round ||
                !(data[1] is int) || !(data[2] is byte)) return false;
            sequence = (int)data[1];
            int value = (byte)data[2];
            if (sequence <= 0 || value > (int)CombatSimulation.ActionKind.Stop) return false;
            kind = (CombatSimulation.ActionKind)value;
            return true;
        }
        public sealed class InputGate
        {
            private readonly int[] highest = new int[2], windowStart = new int[2], count = new int[2];
            public int Rejected { get; private set; }
            public bool Accept(int slot, int sequence, int now)
            {
                if (slot < 0 || slot > 1 || sequence <= highest[slot] || (long)sequence > (long)highest[slot] + 4096)
                { Rejected++; return false; }
                if (now - windowStart[slot] >= 1000) { windowStart[slot] = now; count[slot] = 0; }
                if (++count[slot] > MaxInputsPerSecond) { Rejected++; return false; }
                highest[slot] = sequence;
                return true;
            }
        }
        public sealed class Snapshot
        {
            public string Round, Result;
            public int Tick, ResolvedAt, Rejected;
            public CombatSimulation.Fighter[] Fighters;
        }
        public static object[] Encode(string round, CombatSimulation simulation, int rejected)
        {
            return new object[] { round, simulation.TimeMs, simulation.ResolvedAt, simulation.Result,
                rejected + simulation.Rejected, Pack(simulation.Fighters[0]), Pack(simulation.Fighters[1]) };
        }
        private static int[] Pack(CombatSimulation.Fighter f)
        {
            return new[] { f.Hp, f.LastSequence, f.BusyUntil, f.ReadyAt, f.DodgeAt, f.AttackAt,
                f.X, f.AttackOrigin, f.Facing, f.ActionSequence, (int)f.Action, f.MoveDirection, f.MoveUntil };
        }
        public static bool TrySnapshot(object payload, string round, int lastTick, out Snapshot snapshot)
        {
            snapshot = null;
            var data = payload as object[];
            if (data == null || data.Length != 7 || !(data[0] is string) || (string)data[0] != round ||
                !(data[1] is int) || !(data[2] is int) || !(data[3] is string) || !(data[4] is int)) return false;
            int tick = (int)data[1];
            if (tick <= lastTick || tick < 0 || tick % CombatSimulation.TickMs != 0 || ((string)data[3]).Length > 80) return false;
            CombatSimulation.Fighter first, second;
            if (!Unpack(data[5], out first) || !Unpack(data[6], out second)) return false;
            snapshot = new Snapshot { Round = round, Tick = tick, ResolvedAt = (int)data[2], Result = (string)data[3],
                Rejected = (int)data[4], Fighters = new[] { first, second } };
            return true;
        }
        private static bool Unpack(object payload, out CombatSimulation.Fighter fighter)
        {
            fighter = null;
            var v = payload as int[];
            if (v == null || v.Length != 13 || v[0] < 0 || v[0] > 100 || v[1] < 0 || v[6] < -6000 || v[6] > 8000 ||
                (v[8] != -1 && v[8] != 1) || v[9] < 0 || v[9] > v[1] || v[10] < 0 || v[10] > 1 || v[11] < -1 || v[11] > 1) return false;
            fighter = new CombatSimulation.Fighter { Hp = v[0], LastSequence = v[1], BusyUntil = v[2], ReadyAt = v[3],
                DodgeAt = v[4], AttackAt = v[5], X = v[6], AttackOrigin = v[7], Facing = v[8], ActionSequence = v[9],
                Action = (CombatSimulation.ActionKind)v[10], MoveDirection = v[11], MoveUntil = v[12] };
            return true;
        }
    }
}
