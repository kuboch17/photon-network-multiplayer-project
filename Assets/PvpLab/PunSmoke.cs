using System;
using UnityEngine;

namespace PvpLab
{
    // Opt-in executable smoke test. Normal launches never enter this path.
    public static class PunSmoke
    {
        public static readonly string Role = Argument("-duel-role");
        public static readonly string Room = Argument("-duel-room");
        public static readonly string Screenshot = Argument("-duel-screenshot");
        public static bool Enabled { get { return Role == "host" || Role == "guest"; } }
        public static bool Done { get; private set; }
        private static readonly float deadline = Time.realtimeSinceStartup + 90;
        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
            return "";
        }
        public static void CheckDeadline()
        {
            if (Enabled && !Done && Time.realtimeSinceStartup > deadline) Finish(false, "Timed out");
        }
        public static void Finish(bool success, string detail)
        {
            if (!Enabled || Done) return;
            Done = true;
            Debug.Log("PUN-SMOKE " + Role + " " + (success ? "PASS " : "FAIL ") + detail);
            Application.Quit(success ? 0 : 3);
        }
    }
}
