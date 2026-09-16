using UnityEngine;
namespace SignalLost
{
    public enum Command
    {
        Forward, Left, Right, Stop
    }
    public static class RescueRules
    {
        // 朝向顺序：北、东、南、西。
        public static readonly Vector2Int[] Directions =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };
        public static readonly Vector2Int Port = new Vector2Int(1, 1);
        public static readonly Vector2Int[] People =
        {
            new Vector2Int(3, 6), new Vector2Int(7, 7)
        };
        public static readonly Vector2Int[] Rocks =
        {
            new Vector2Int(2, 3), new Vector2Int(3, 3), new Vector2Int(4, 3), new Vector2Int(5, 5), new Vector2Int(5, 6), new Vector2Int(7, 4), new Vector2Int(8, 4)
        };
        // 快速频道的转向有 50% 概率反转。
        public const double InversionChance = .5;
        // 越界或遇到礁石时禁止前进。
        public static bool Blocked(Vector2Int p)
        {
            if (p.x < 0 || p.x > 9 || p.y < 0 || p.y > 8)
            {
                return true;
            }
            foreach (Vector2Int rock in Rocks)
            {
                if (rock == p)
                {
                    return true;
                }
            }
            return false;
        }
        // 确认频道不误传，只有左右转向可能反转。
        public static Command Decode(Command command, double roll, bool reliable)
        {
            if (reliable || roll >= InversionChance)
            {
                return command;
            }
            if (command == Command.Left)
            {
                return Command.Right;
            }
            if (command == Command.Right)
            {
                return Command.Left;
            }
            return command;
        }
        public static int Turn(int heading, Command command)
        {
            if (command == Command.Left)
            {
                heading = heading + 3;
            }
            else if (command == Command.Right)
            {
                heading = heading + 1;
            }
            return heading%4;
        }
    }
}
