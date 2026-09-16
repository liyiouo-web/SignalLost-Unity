using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace SignalLost
{
    // 自动检查工具，不参与正常游玩。
    public static class RescueSelfTest
    {
        // 测试使用固定种子，便于复现。
        public static IEnumerator Run(RescueGame game)
        {
            yield return null;
            game.selfTesting = true;
            game.transmissionRandom = new System.Random(1);
            game.playing = true;
            game.acknowledgementSeconds = .02f;
            game.fastSeconds = .02f;
            game.moveSeconds = .03f;
            game.cell = new Vector2Int(3, 4);
            game.boat.position = RescueGame.World(game.cell);
            game.Send(Command.Left);
            while (game.busy)yield return null;
            if (game.heading != 1)
            {
                File.WriteAllText(Path.Combine(Application.dataPath, "../self-test.txt"), "FAIL inversion");
                Application.Quit(1);
                yield break;
            }
            game.Send(Command.Left);
            game.Cancel();
            while (game.busy)yield return null;
            if (game.heading != 1)
            {
                File.WriteAllText(Path.Combine(Application.dataPath, "../self-test.txt"), "FAIL cancellation");
                Application.Quit(1);
                yield break;
            }
            game.cell = new Vector2Int(1, 3);
            game.boat.position = RescueGame.World(game.cell);
            game.Send(Command.Forward);
            while (game.busy)yield return null;
            if (game.collisions != 1 || game.cell != new Vector2Int(1, 3))
            {
                File.WriteAllText(Path.Combine(Application.dataPath, "../self-test.txt"), "FAIL collision recovery");
                Application.Quit(1);
                yield break;
            }
            game.TogglePause();
            int before = game.commands;
            game.Send(Command.Forward);
            yield return null;
            if (game.commands != before)throw new Exception("Pause accepted input");
            game.TogglePause();
            game.cell = RescueRules.Port;
            game.heading = 0;
            game.collisions = 0;
            game.boat.position = RescueGame.World(game.cell);
            game.boat.rotation = Quaternion.identity;
            game.reliable = true;
            foreach (var target in new[]
            {
                RescueRules.People[0], RescueRules.People[1], RescueRules.Port
            }
            )
            {
                // 用广度优先搜索寻找可通行路线。
                var queue = new System.Collections.Generic.Queue < Vector2Int > ();
                var prev = new System.Collections.Generic.Dictionary < Vector2Int, Vector2Int > ();
                queue.Enqueue(game.cell);
                prev[game.cell] = game.cell;
                while (queue.Count > 0)
                {
                    var p = queue.Dequeue();
                    if (p == target)break;
                    foreach (var d in RescueRules.Directions)
                    {
                        var n = p + d;
                        if (!RescueRules.Blocked(n) && !prev.ContainsKey(n))
                        {
                            prev[n] = p;
                            queue.Enqueue(n);
                        }
                    }
                }
                var path = new System.Collections.Generic.List < Vector2Int > ();
                for (var p = target; p != game.cell; p = prev[p])path.Add(p);
                path.Reverse();
                foreach (var p in path)
                {
                    var delta = p - game.cell;
                    int h = Array.IndexOf(RescueRules.Directions, delta);
                    while (game.heading != h)
                    {
                        game.Send(Command.Right);
                        while (game.busy)yield return null;
                    }
                    game.Send(Command.Forward);
                    while (game.busy)yield return null;
                }
            }
            bool ok = game.finished && game.rescued == 2 && game.collisions == 0;
            File.WriteAllText(Path.Combine(Application.dataPath, "../self-test.txt"), ok ? "PASS: inversion, cancellation, collision recovery, pause input lock, rescued two survivors and returned to port." : "FAIL");
            Application.Quit(ok ? 0 : 1);
        }
    }
}
