using System;
using System.Collections.Generic;

namespace PvpLab
{
    // Pure integer-time rules, shared by the demo and executable regression checks.
    public sealed class CombatSimulation
    {
        public const int TickMs = 10, BufferMs = 20, WindupMs = 100;
        public const int InvulnerableMs = 120, RecoveryMs = 300, CooldownMs = 600;
        public enum ActionKind { Dodge, DashAttack, MoveLeft, MoveRight, Stop }
        public sealed class Command
        {
            public int Actor, Sequence, ArrivalMs;
            public ActionKind Kind;
        }
        public sealed class Fighter
        {
            public int Hp = 100, LastSequence, BusyUntil, ReadyAt;
            public int DodgeAt = -10000, AttackAt = -10000;
            public int X, AttackOrigin, Facing = 1;
            public int ActionSequence;
            public ActionKind Action;
            public int MoveDirection, MoveUntil;
            public Fighter Copy() { return (Fighter)MemberwiseClone(); }
        }
        public readonly Fighter[] Fighters = { new Fighter(), new Fighter() };
        public readonly List<string> Events = new List<string>();
        private readonly List<Command> pending = new List<Command>();
        private readonly List<int[]> hits = new List<int[]>();
        public int TimeMs { get; private set; } = -TickMs;
        public int ResolvedAt { get; private set; } = -1;
        public string Result { get; private set; } = "Pending";
        public int Rejected { get; private set; }
        public const int DashDistance = 1000, HitRange = 1500; // millimetres
        private readonly bool spatial;
        public CombatSimulation(bool spatialCombat = false, int separation = 2200)
        {
            spatial = spatialCombat;
            Fighters[1].X = separation;
            Fighters[1].Facing = -1;
        }

        public static int ExecutionTime(int arrival)
        {
            return ((arrival + BufferMs + TickMs - 1) / TickMs) * TickMs;
        }
        // Transport supplies authenticated actor + receipt time, never client hit claims.
        public void Enqueue(int authenticatedActor, int sequence, ActionKind kind, int arrivalMs)
        {
            if (authenticatedActor < 0 || authenticatedActor > 1 || sequence <= 0 ||
                arrivalMs <= TimeMs || kind < ActionKind.Dodge || kind > ActionKind.Stop || pending.Count >= 128)
            { Rejected++; return; }
            pending.Add(new Command { Actor = authenticatedActor, Sequence = sequence, Kind = kind, ArrivalMs = arrivalMs });
        }
        public void AdvanceTo(int targetMs)
        {
            while (TimeMs + TickMs <= targetMs)
            {
                TimeMs += TickMs;
                pending.Sort((a, b) => {
                    int order = ExecutionTime(a.ArrivalMs).CompareTo(ExecutionTime(b.ArrivalMs));
                    if (order != 0) return order;
                    order = a.Actor.CompareTo(b.Actor);
                    if (order != 0) return order;
                    order = a.Sequence.CompareTo(b.Sequence);
                    return order != 0 ? order : a.Kind.CompareTo(b.Kind);
                });
                while (pending.Count > 0 && ExecutionTime(pending[0].ArrivalMs) <= TimeMs)
                {
                    Command command = pending[0]; pending.RemoveAt(0);
                    Fighter fighter = Fighters[command.Actor];
                    if (command.Sequence <= fighter.LastSequence) { Rejected++; continue; }
                    fighter.LastSequence = command.Sequence;
                    if (command.Kind >= ActionKind.MoveLeft)
                    {
                        fighter.MoveDirection = command.Kind == ActionKind.MoveLeft ? -1 : command.Kind == ActionKind.MoveRight ? 1 : 0;
                        fighter.MoveUntil = TimeMs + 250; // A lost release/focus never causes endless movement.
                        continue;
                    }
                    if (fighter.Hp <= 0 || TimeMs < fighter.BusyUntil || TimeMs < fighter.ReadyAt)
                    { Rejected++; Events.Add(TimeMs + " ms: rejected busy/cooldown"); continue; }
                    fighter.BusyUntil = TimeMs + RecoveryMs;
                    fighter.ReadyAt = TimeMs + CooldownMs;
                    fighter.ActionSequence = command.Sequence;
                    fighter.Action = command.Kind;
                    if (command.Kind == ActionKind.Dodge) fighter.DodgeAt = TimeMs;
                    else
                    {
                        fighter.AttackAt = TimeMs;
                        fighter.AttackOrigin = fighter.X;
                        fighter.Facing = Fighters[1 - command.Actor].X >= fighter.X ? 1 : -1;
                        hits.Add(new[] { TimeMs + WindupMs, command.Actor });
                    }
                    Events.Add(TimeMs + " ms: P" + (command.Actor + 1) + " " + command.Kind);
                }
                if (spatial)
                    foreach (Fighter fighter in Fighters)
                    {
                        int age = TimeMs - fighter.AttackAt;
                        if (age >= 0 && age <= WindupMs)
                            fighter.X = ClampPosition(fighter.AttackOrigin + fighter.Facing * DashDistance * age / WindupMs);
                        else if (fighter.Hp > 0 && TimeMs >= fighter.BusyUntil && TimeMs < fighter.MoveUntil)
                            fighter.X = ClampPosition(fighter.X + fighter.MoveDirection * 30); // 3 m/s at 100 Hz
                    }
                // All inputs for this tick run before contacts: dodge wins an exact tie.
                // Collect damage first so simultaneous lethal attacks do not depend on list order.
                int[] damage = new int[2];
                for (int i = hits.Count - 1; i >= 0; i--)
                {
                    if (hits[i][0] > TimeMs) continue;
                    int attacker = hits[i][1], defender = 1 - attacker;
                    hits.RemoveAt(i);
                    if (Fighters[attacker].Hp <= 0) continue;
                    Fighter victim = Fighters[defender];
                    bool avoided = TimeMs >= victim.DodgeAt && TimeMs < victim.DodgeAt + InvulnerableMs;
                    int forwardDistance = (victim.X - Fighters[attacker].X) * Fighters[attacker].Facing;
                    bool inRange = !spatial || (forwardDistance >= 0 && forwardDistance <= HitRange);
                    if (inRange && !avoided) damage[defender] += 25;
                    ResolvedAt = TimeMs;
                    Result = !inRange ? "MISS - out of range" : avoided ? "DODGED - no damage" : "HIT - 25 damage";
                    Events.Add(TimeMs + " ms: " + Result);
                }
                for (int i = 0; i < 2; i++) Fighters[i].Hp = Math.Max(0, Fighters[i].Hp - damage[i]);
                if (Events.Count > 64) Events.RemoveRange(0, Events.Count - 64);
            }
        }
        private static int ClampPosition(int x) { return Math.Max(-6000, Math.Min(8000, x)); }
    }
}
