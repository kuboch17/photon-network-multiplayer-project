using System;
using System.Linq;
using System.Reflection;
using RPGCharacterAnims;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run with -batchmode -executeMethod FootballKickVerification.Run (without -quit).
[InitializeOnLoad]
public static class FootballKickVerification
{
    private const string Running = "FootballKickVerification.Running";
    private static RPGCharacterController[] players;
    private static ball chicken;
    private static Vector3 origin;
    private static Vector3 shotDirection;
    private static int stage;
    private static double next;
    private static readonly MethodInfo Kick = typeof(RPGCharacterController).GetMethod(
        "TryKickFootball", BindingFlags.Instance | BindingFlags.NonPublic);

    static FootballKickVerification()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    public static void Run()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Run this verification in a separate batchmode Editor.");
        EditorSceneManager.OpenScene("Assets/Scenes/footballl.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Running, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            next = EditorApplication.timeSinceStartup + 2;
            EditorApplication.update += Tick;
        }
    }

    private static bool TryKick(RPGCharacterController player)
    {
        return (bool)Kick.Invoke(player, null);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Debug.Log("FOOTBALL PASS: " + message);
    }

    private static void Prepare(RPGCharacterController player, float distance)
    {
        chicken.agent.enabled = false;
        chicken.rb.linearVelocity = Vector3.zero;
        chicken.rb.angularVelocity = Vector3.zero;
        chicken.transform.position = origin;
        player.transform.position = origin + Vector3.right * distance;
        player.canAction = true;
        player.isBlocking = false;
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (stage == 0)
            {
                players = UnityEngine.Object.FindObjectsByType<RPGCharacterController>()
                    .Where(p => p.football && p.isActiveAndEnabled).OrderByDescending(p => p.playa1).ToArray();
                Check(players.Length == 2 && players[0].playa1 && players[1].playa2,
                    "two active football players with separate player slots");
                chicken = players[0].ballScript;
                origin = chicken.transform.position;
                Check(chicken == players[1].ballScript && chicken.agent.enabled,
                    "both players reference the wandering chicken");
                Check(players.All(p => p.throwaLeg != null), "both physical kick poses assigned");
                Prepare(players[0], 5);
                Check(!TryKick(players[0]) && chicken.rb.linearVelocity == Vector3.zero,
                    "out-of-range kick does not shoot");
                next = EditorApplication.timeSinceStartup + 0.8;
            }
            else if (stage == 1 || stage == 3)
            {
                var p = players[stage == 1 ? 0 : 1];
                Prepare(p, 1);
                shotDirection = ((p.playa1 ? chicken.target1 : chicken.target0).transform.position - origin).normalized;
                Check(TryKick(p), p.name + " kicks within range");
                Check(!chicken.agent.enabled, "kick hands control from navigation to physics");
                Check(!TryKick(p), "immediate repeated kick is blocked by cooldown");
                next = EditorApplication.timeSinceStartup + 0.15;
            }
            else if (stage == 2 || stage == 4)
            {
                Check(Vector3.Dot(chicken.rb.linearVelocity, shotDirection) > 0.1f,
                    "physics moves chicken toward the correct opposing goal");
                next = EditorApplication.timeSinceStartup + 1.2;
            }
            else if (stage == 5)
            {
                Prepare(players[0], 1);
                Check(TryKick(players[0]), "player can kick again after cooldown");
                next = EditorApplication.timeSinceStartup + 1.2;
            }
            else if (stage == 6)
            {
                chicken.transform.position = origin;
                chicken.rb.linearVelocity = Vector3.zero;
                chicken.rb.angularVelocity = Vector3.zero;
                next = EditorApplication.timeSinceStartup + 0.15;
            }
            else
            {
                Check(chicken.agent.enabled && !chicken.state, "chicken resumes navigation after slowing down");
                Check(players.All(p => p.throwaLeg.cl.enabled), "both legs return to animation control");
                Finish(0);
                return;
            }
            stage++;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            Finish(1);
        }
    }

    private static void Finish(int result)
    {
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Tick;
        Debug.Log("FOOTBALL VERIFICATION EXIT " + result);
        EditorApplication.isPlaying = false;
        EditorApplication.Exit(result);
    }
}
