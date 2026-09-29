using UnityEngine;

namespace PvpLab
{
    public sealed class CombatLab : MonoBehaviour
    {
        private CombatSimulation simulation;
        private float elapsed;
        private bool running;
        private int dodgeInputMs = 100;
        private const int OneWayMs = 60;
        private Vector2 scroll;
        private void Awake() { ResetDemo(100); }
        private void ResetDemo(int dodgeTime)
        {
            dodgeInputMs = dodgeTime;
            elapsed = 0; running = false;
            simulation = new CombatSimulation();
            simulation.Enqueue(0, 1, CombatSimulation.ActionKind.DashAttack, OneWayMs);
            simulation.Enqueue(1, 1, CombatSimulation.ActionKind.Dodge, dodgeInputMs + OneWayMs);
            simulation.AdvanceTo(0);
        }
        private void Update()
        {
            if (!running) return;
            elapsed = Mathf.Min(900, elapsed + Time.unscaledDeltaTime * 1000);
            simulation.AdvanceTo(Mathf.FloorToInt(elapsed));
            if (elapsed >= 900) running = false;
        }
        private void OnGUI()
        {
            float scale = Mathf.Min(Screen.width / 1000f, Screen.height / 720f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUI.Box(new Rect(0, 0, 1000, 720), "");
            GUILayout.BeginArea(new Rect(20, 15, 960, 690));
            GUILayout.Label("PvP COMBAT LAB | simulated 120 ms RTT | fixed 10 ms authority tick");
            GUILayout.Label("Local transport simulation. Fixed duel distance; no collision or Photon server integration.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Near-simultaneous: dodge +10 ms")) ResetDemo(10);
            if (GUILayout.Button("Exact tie: dodge +100 ms")) ResetDemo(100);
            if (GUILayout.Button("Too late: dodge +110 ms")) ResetDemo(110);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(running ? "Pause" : "Play")) running = !running;
            if (GUILayout.Button("Step 1 frame (60 fps)")) { running = false; elapsed += 1000f / 60; simulation.AdvanceTo(Mathf.FloorToInt(elapsed)); }
            if (GUILayout.Button("Reset")) ResetDemo(dodgeInputMs);
            if (GUILayout.Button("Send duplicate / cancel spam"))
            {
                int receipt = Mathf.FloorToInt(elapsed) + OneWayMs;
                simulation.Enqueue(0, 1, CombatSimulation.ActionKind.DashAttack, receipt);
                simulation.Enqueue(0, 2, CombatSimulation.ActionKind.Dodge, receipt);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Time: " + elapsed.ToString("F0") + " ms | frame " + Mathf.FloorToInt(elapsed * .06f) + " | rejected commands: " + simulation.Rejected);
            GUILayout.Space(8);
            Panel("ATTACKER CLIENT - immediate local prediction", 0);
            Panel("AUTHORITY - sole owner of damage", 1);
            Panel("DEFENDER CLIENT - immediate local prediction", 2);
            GUILayout.Space(8);
            GUILayout.Label("Rule: receipt + 20 ms buffer, rounded up to 10 ms tick. Inputs before hits; dodge wins a tie.");
            GUILayout.Label("Dash contact: +100 ms. Dodge invulnerability: [start, start +120 ms). Recovery: 300 ms; cooldown: 600 ms.");
            GUILayout.Label("Prediction is visual only. HP / hit confirmation wait for the authority result. No retroactive cancels.");
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(125));
            foreach (string entry in simulation.Events) GUILayout.Label(entry);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = previous;
        }
        private void Panel(string title, int view)
        {
            bool authority = view == 1;
            float visibleTime = elapsed - (authority ? 0 : OneWayMs);
            bool confirmed = simulation.ResolvedAt >= 0 && visibleTime >= simulation.ResolvedAt;
            int attackStart = simulation.Fighters[0].AttackAt;
            int dodgeStart = simulation.Fighters[1].DodgeAt;
            // Locally owned actions begin immediately; remote actions begin on receipt.
            float attackAge = view == 0 ? elapsed : (attackStart >= 0 && visibleTime >= attackStart ? visibleTime - attackStart : -1);
            float dodgeAge = view == 2 ? elapsed - dodgeInputMs : (dodgeStart >= 0 && visibleTime >= dodgeStart ? visibleTime - dodgeStart : -1);
            GUILayout.Label(title + " | Defender HP: " + (confirmed ? simulation.Fighters[1].Hp : 100) + " | " + (confirmed ? simulation.Result : "awaiting authority"));
            Rect arena = GUILayoutUtility.GetRect(920, 65);
            GUI.Box(arena, "");
            float dash = attackAge < 0 ? 0 : Mathf.Clamp01(attackAge / 100);
            float dodge = dodgeAge < 0 ? 0 : Mathf.Sin(Mathf.Clamp01(dodgeAge / 300) * Mathf.PI);
            GUI.color = new Color(1f, .65f, .2f);
            GUI.Box(new Rect(arena.x + 30 + dash * 390, arena.y + 17, 95, 32), "P1 DASH");
            GUI.color = dodgeAge >= 0 && dodgeAge < 120 ? Color.cyan : Color.white;
            GUI.Box(new Rect(arena.x + 530 + dodge * 100, arena.y + 17, 120, 32), dodgeAge >= 0 && dodgeAge < 120 ? "P2 DODGE" : "P2");
            GUI.color = Color.white;
        }
    }
}
