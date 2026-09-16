using System;
using System.Collections.Generic;
using UnityEngine;
namespace SignalLost
{
    public class RescueUI : MonoBehaviour
    {
        public RescueGame game;
        enum Overlay
        {
            Guide, Restart
        }
        readonly Stack < Overlay > screens = new Stack < Overlay > ();
        readonly List < string > focusable = new List < string > ();
        readonly Dictionary < string, int > fitted = new Dictionary < string, int > ();
        Font latin, chinese, display;
        GUIStyle textStyle, buttonStyle;
        Camera chartCamera;
        float scale = 1, offsetX, offsetY, feedbackAt;
        string feedback = "", focus = "begin", activate = "";
        bool keyboard, priorPause, largeText, reducedMotion;
        int priorWidth, priorHeight;
        bool priorPlaying;
        readonly Color ink = new Color(.055f, .115f, .14f), panel = new Color(.085f, .17f, .19f), paper = new Color(.91f, .91f, .83f), muted = new Color(.61f, .72f, .70f), amber = new Color(.98f, .65f, .28f), mint = new Color(.52f, .85f, .72f), edge = new Color(.22f, .33f, .35f);
        // 界面按 1600 × 1000 画布等比缩放。
        readonly Rect chart = new Rect(32, 152, 1064, 666), desk = new Rect(1120, 152, 448, 666);
        // 按当前语言选择文本。
        string T(string en, string zh)
        {
            if (game.english)
            {
                return en;
            }
            return zh;
        }
        void Awake()
        {
            latin = Resources.Load < Font > ("Fonts/Barlow-Regular");
            display = Resources.Load < Font > ("Fonts/BarlowCondensed-SemiBold");
            chinese = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 24);
            chartCamera = Camera.main;
        }
        void OnEnable()
        {
            game.Feedback += OnFeedback;
        }
        void OnDisable()
        {
            game.Feedback -= OnFeedback;
        }
        void OnFeedback(string value)
        {
            feedback = value;
            feedbackAt = Time.unscaledTime;
            if (value == "reset")focus = game.playing ? "forward" : "begin";
            if (value == "complete")focus = "again";
        }
        void Update()
        {
            scale = Mathf.Min(Screen.width / 1600f, Screen.height / 1000f);
            offsetX = (Screen.width - 1600 * scale) / 2;
            offsetY = (Screen.height - 1000 * scale) / 2;
            if (priorWidth != Screen.width || priorHeight != Screen.height || priorPlaying != game.playing)
            {
                priorWidth = Screen.width;
                priorHeight = Screen.height;
                priorPlaying = game.playing;
                ApplyCamera();
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (screens.Count > 0)Pop();
                else if (game.playing && !game.finished)game.TogglePause();
            }
            if (Input.GetKeyDown(KeyCode.F1) && screens.Count == 0 && !game.finished)Push(Overlay.Guide);
            int direction = 0;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                direction = 1;
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                direction = - 1;
            }
            if (direction != 0 && focusable.Count > 0)
            {
                keyboard = true;
                int i = focusable.IndexOf(focus);
                focus = focusable[(Mathf.Max(0, i) + direction + focusable.Count)%focusable.Count];
            }
            if (Input.GetKeyDown(KeyCode.Return))
            {
                keyboard = true;
                activate = focus;
            }
        }
        void ApplyCamera()
        {
            if (!chartCamera)return;
            Rect r = game.playing ? chart : new Rect(610, 150, 958, 700);
            chartCamera.rect = new Rect((offsetX + r.x * scale) / Screen.width, (Screen.height - offsetY - r.yMax * scale) / Screen.height, r.width * scale / Screen.width, r.height * scale / Screen.height);
            chartCamera.transform.position = new Vector3(0, 13, - 8);
            chartCamera.transform.LookAt(new Vector3( - .25f, 0, 0));
            chartCamera.orthographicSize = 5.0f;
        }
        // 打开弹窗时暂停并锁定游戏输入。
        void Push(Overlay next)
        {
            if (screens.Count == 0)
            {
                priorPause = game.paused;
                if (!game.paused)game.TogglePause();
            }
            screens.Push(next);
            game.inputBlocked = true;
            focus = next == Overlay.Guide ? "guideClose" : "keep";
        }
        // 关闭最后一层弹窗后恢复原暂停状态。
        void Pop()
        {
            if (screens.Count == 0)return;
            screens.Pop();
            if (screens.Count == 0)
            {
                game.inputBlocked = false;
                if (!priorPause && game.paused)game.TogglePause();
                focus = game.playing ? "forward" : "begin";
            }
            else focus = "guideClose";
        }
        void Restart()
        {
            screens.Clear();
            game.inputBlocked = false;
            game.Reset(true);
            focus = "forward";
        }
        void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
        void Line(Vector2 a, Vector2 b, Color c, float w = 2)
        {
            var matrix = GUI.matrix;
            GUI.matrix = matrix * Matrix4x4.TRS(new Vector3(a.x, a.y, 0), Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg), Vector3.one);
            Fill(new Rect(0, - w * .5f, Vector2.Distance(a, b), w), c);
            GUI.matrix = matrix;
        }
        void Border(Rect r, Color c, float w = 1)
        {
            Fill(new Rect(r.x, r.y, r.width, w), c);
            Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
            Fill(new Rect(r.x, r.y, w, r.height), c);
            Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
        }
        void Text(Rect r, string value, int size = 23, Color ? color = null, bool title = false, TextAnchor align = TextAnchor.UpperLeft)
        {
            textStyle.font = game.english && !value.Contains("中文") ? (title ? display : latin) : chinese;
            textStyle.alignment = align;
            textStyle.wordWrap = true;
            textStyle.normal.textColor = color??paper;
            int requested = size + (largeText && !title ? 3 : 0);
            string key = value + "|" + r.width + "|" + r.height + "|" + requested + "|" + title + "|" + game.english;
            if (!fitted.TryGetValue(key, out int actual))
            {
                actual = requested;
                textStyle.fontSize = actual;
                while (actual > 17 && textStyle.CalcHeight(new GUIContent(value), r.width) > r.height)
                {
                    actual--;
                    textStyle.fontSize = actual;
                }
                if (fitted.Count < 700)fitted[key] = actual;
            }
            textStyle.fontSize = actual;
            GUI.Label(r, value, textStyle);
        }
        bool Button(Rect rect, string id, string label, bool enabled = true, bool primary = false, string glyph = "", string hint = "", bool modal = false)
        {
            enabled &= (screens.Count == 0 && !game.finished) || modal;
            bool hovered = rect.Contains(Event.current.mousePosition), down = hovered && Input.GetMouseButton(0) && enabled, focused = keyboard && focus == id && enabled;
            // 每次重绘只登记一次可导航按钮。
            if (Event.current.type == EventType.Repaint && enabled)focusable.Add(id);
            Color bg = primary ? amber : paper;
            if (!enabled)bg = edge;
            else if (hovered)bg = Color.Lerp(bg, Color.white, .12f);
            Rect r = rect;
            if (down && !reducedMotion)r.y += 3;
            Fill(r, bg);
            if (focused)Border(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8), mint, 2);
            Color fg = enabled ? ink : muted;
            if (glyph != "")Icon(new Vector2(r.x + 38, r.center.y - 3), glyph, 24, fg);
            float left = glyph == "" ? 12 : 77;
            Text(new Rect(r.x + left, r.y + (hint == "" ? 7 : 10), r.width - left - 10, hint == "" ? r.height - 12 : 34), label, 25, fg, true, glyph == "" ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft);
            if (hint != "")Text(new Rect(r.x + left, r.y + 47, r.width - left - 10, 29), hint, 19, fg);
            bool old = GUI.enabled;
            GUI.enabled = enabled;
            bool clicked = GUI.Button(rect, GUIContent.none, buttonStyle);
            GUI.enabled = old;
            if (enabled && activate == id)
            {
                clicked = true;
                activate = "";
            }
            if (clicked)
            {
                focus = id;
                return true;
            }
            return false;
        }
        void Icon(Vector2 c, string kind, float size, Color color)
        {
            if (kind == "left" || kind == "right")
            {
                float d = kind == "left" ? - 1 : 1;
                Line(c + new Vector2( - d * size, size * .6f), c + new Vector2( - d * size, - size * .5f), color, 3);
                Line(c + new Vector2( - d * size, - size * .5f), c + new Vector2(d * size, - size * .5f), color, 3);
                Line(c + new Vector2(d * size, - size * .5f), c + new Vector2(d * size * .45f, - size), color, 3);
                Line(c + new Vector2(d * size, - size * .5f), c + new Vector2(d * size * .45f, 0), color, 3);
            }
            else if (kind == "forward")
            {
                Line(c + new Vector2(0, size), c + new Vector2(0, - size), color, 3);
                Line(c + new Vector2(0, - size), c + new Vector2( - size * .6f, - size * .3f), color, 3);
                Line(c + new Vector2(0, - size), c + new Vector2(size * .6f, - size * .3f), color, 3);
            }
            else if (kind == "cancel")
            {
                Line(c - new Vector2(size * .65f, size * .65f), c + new Vector2(size * .65f, size * .65f), color, 3);
                Line(c + new Vector2( - size * .65f, size * .65f), c + new Vector2(size * .65f, - size * .65f), color, 3);
            }
            else for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI / 12, b = (i + 1) * Mathf.PI / 12;
                Line(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * size, c + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * size, color, 3);
            }
        }
        string Cmd(Command c) => c == Command.Forward ? T("Forward", "前进") : c == Command.Left ? T("Turn left", "左转") : T("Turn right", "右转");
        string Glyph(Command c) => c == Command.Forward ? "forward" : c == Command.Left ? "left" : "right";
        // 将世界坐标转换为界面标注位置。
        Vector2 Project(Vector3 world)
        {
            Vector3 s = chartCamera.WorldToScreenPoint(world);
            return new Vector2((s.x - offsetX) / scale, (Screen.height - s.y - offsetY) / scale);
        }
        void Tag(Vector3 world, string value, Color color, float width = 125, bool below = false)
        {
            Vector2 p = Project(world);
            Rect r = new Rect(p.x - width / 2, below ? p.y + 30 : p.y - 56, width, 32);
            Fill(r, ink);
            Border(r, color);
            Text(r, value, 19, color, false, TextAnchor.MiddleCenter);
            Line(new Vector2(p.x, below ? r.y : r.yMax), p, color, 1);
        }
        void Map()
        {
            for (int i = 0; i < 2; i++)if (!game.IsAboard(i))Tag(RescueGame.World(RescueRules.People[i]), "0" + (i + 1), amber, 46);
            Tag(RescueGame.World(RescueRules.Port), T("H  HARBOUR", "H  港口"), mint, 132, true);
            Vector2 boat = Project(game.boat.position), nose = Project(game.boat.position + game.boat.forward * .8f), dir = (nose - boat).normalized, side = new Vector2( - dir.y, dir.x);
            Line(nose - dir * 13 - side * 10, nose, mint, 4);
            Line(nose - dir * 13 + side * 10, nose, mint, 4);
            Border(new Rect(boat.x - 28, boat.y - 22, 56, 44), mint);
            if (!reducedMotion && feedback == "sent" && Time.unscaledTime - feedbackAt < .45f)
            {
                float t = (Time.unscaledTime - feedbackAt) / .45f;
                var color = mint;
                color.a = 1 - t;
                Icon(boat, "ring", 30 + t * 25, color);
            }
        }
        // 倒计时直接读取游戏状态。
        void Radio()
        {
            Text(new Rect(1146, 220, 395, 45), T("Channel", "频道"), 28, muted, true);
            bool available = !game.busy && !game.paused;
            if (Button(new Rect(1146, 280, 188, 60), "fast", T("Fast", "快速"), available, !game.reliable))game.reliable = false;
            if (Button(new Rect(1350, 280, 190, 60), "safe", T("Confirmed", "确认"), available, game.reliable))game.reliable = true;
            Text(new Rect(1147, 360, 395, 65), game.reliable ? T("Accurate · 1.8 s", "准确 · 1.8 秒") : T("Turns may reverse · 50%", "转向可能反向 · 50%"), 23, game.reliable ? muted : amber);
            bool has = game.commands > 0;
            if (has)
            {
                Text(new Rect(1147, 455, 180, 32), T("Sent", "发出"), 23, muted);
                Text(new Rect(1360, 455, 180, 32), T("Received", "收到"), 23, muted);
                Icon(new Vector2(1226, 541), Glyph(game.sent), 28, paper);
                Icon(new Vector2(1438, 541), Glyph(game.received), 28, game.sent != game.received ? amber : mint);
                Text(new Rect(1148, 595, 180, 40), Cmd(game.sent), 27, null, true);
                Text(new Rect(1358, 595, 182, 40), Cmd(game.received), 27, game.sent != game.received ? amber : mint, true);
            }
            else Text(new Rect(1148, 500, 390, 70), T("Send your first signal.", "发送第一条指令。"), 27, muted);
            if (game.busy && game.phase == "ack")
            {
                float f = game.replyDuration > 0 ? game.replyRemaining / game.replyDuration : 0;
                Text(new Rect(1147, 680, 393, 38), T("Cancel within ", "可撤销 ") + game.replyRemaining.ToString("0.0") + T(" s", " 秒"), 26, amber, true);
                Fill(new Rect(1147, 730, 393, 6), edge);
                Fill(new Rect(1147, 730, 393 * f, 6), amber);
            }
            else
            {
                string status = game.phase == "cancelled" ? T("Cancelled", "已撤销") : game.phase == "collision" ? T("Reef strike — change course", "撞礁，请调整方向") : game.phase == "rescue" ? T("Rescuing…", "救援中……") : game.busy ? T("Moving…", "执行中……") : "";
                Text(new Rect(1147, 683, 393, 75), status, 25, game.phase == "collision" ? amber : muted);
            }
        }
        void Controls()
        {
            if (game.paused)Text(new Rect(40, 822, 700, 45), T("Paused", "已暂停"), 26, amber);
            bool enabled = !game.paused && !game.busy;
            if (Button(new Rect(180, 884, 245, 76), "left", T("Left  2", "左转  2"), enabled, false, "left"))game.Send(Command.Left);
            if (Button(new Rect(445, 884, 270, 76), "forward", T("Forward  1", "前进  1"), enabled, true, "forward"))game.Send(Command.Forward);
            if (Button(new Rect(735, 884, 245, 76), "right", T("Right  3", "右转  3"), enabled, false, "right"))game.Send(Command.Right);
            if (Button(new Rect(1020, 884, 280, 76), "cancel", T("Cancel  4", "撤销  4"), game.busy && game.phase == "ack" && !game.paused && !game.CancelRequested, false, "cancel"))game.Cancel();
        }
        void Title()
        {
            Text(new Rect(58, 280, 520, 225), T("Bring them\nhome.", "带他们\n回家。"), 88, null, true);
            Text(new Rect(58, 566, 485, 110), T("Rescue two people.\nWatch for crossed signals.", "救回两个人。\n留意被误传的指令。"), 29, muted);
            if (Button(new Rect(58, 752, 420, 80), "begin", T("Begin rescue", "开始救援"), true, true))Restart();
        }
        void Modal()
        {
            Fill(new Rect(0, 0, 1600, 1000), new Color(.025f, .055f, .07f, .88f));
            Rect r = new Rect(220, 142, 1160, 748);
            Fill(r, paper);
            Fill(new Rect(r.x, r.y, 8, r.height), amber);
            if (screens.Peek() == Overlay.Restart)
            {
                Text(new Rect(270, 216, 1050, 90), T("Start a fresh rescue?", "重新开始本次救援？"), 51, ink, true);
                Text(new Rect(273, 351, 970, 125), T("This attempt will reset. Your personal best stays saved.", "本次救援进度将重置，个人最高分会保留。"), 30, ink);
                if (Button(new Rect(275, 615, 465, 85), "keep", T("Keep playing", "继续当前救援"), true, true, "", "", true))Pop();
                if (Button(new Rect(785, 615, 525, 85), "confirmRestart", T("Restart rescue", "重新开始"), true, false, "", "", true))Restart();
                return;
            }
            Text(new Rect(270, 181, 1050, 70), T("A field guide to crossed signals", "误传信号操作手册"), 47, ink, true);
            string[] titles =
            {
                T("01  Send", "01  发送"), T("02  Read", "02  看回复"), T("03  Bring home", "03  接人返航")
            };
            string[] body =
            {
                T("1 moves one square.\n2 / 3 turn the boat in place.\nTAB changes the radio channel.", "1 前进一格。\n2 / 3 原地向左 / 右转。\nTAB 切换无线电频道。"), T("Each FAST turn has a 50% chance to reverse, anywhere. Forward stays accurate. CONFIRMED is accurate. 4 cancels.", "任何位置，快速频道每次转向都有 50% 概率反向；前进不变。确认频道准确。行动前按 4 撤销。"), T("Reach 01 and 02, then H.\nAvoid reefs. SPACE pauses.\nArrow keys select; ENTER activates.", "驶到 01 和 02，再返回 H。\n避开礁石；空格暂停。\n方向键选按钮，回车执行。")
            };
            for (int i = 0; i < 3; i++)
            {
                float x = 273 + i * 351;
                Text(new Rect(x, 300, 322, 50), titles[i], 32, ink, true);
                Text(new Rect(x, 375, 322, 177), body[i], 25, ink);
            }
            Text(new Rect(273, 574, 1050, 65), T("Score: 2000 − collisions × 150 − commands × 5 − seconds × 2. Minimum 0.", "得分：2000 − 碰撞次数 × 150 − 指令数 × 5 − 秒数 × 2，最低为 0。"), 22, ink);
            if (Button(new Rect(273, 679, 500, 58), "motion", reducedMotion ? T("Motion: reduced", "动效：减少") : T("Motion: standard", "动效：标准"), true, false, "", "", true))reducedMotion = !reducedMotion;
            if (Button(new Rect(805, 679, 518, 58), "textSize", largeText ? T("Text: large", "大字模式") : T("Text: normal", "标准文字"), true, false, "", "", true))largeText = !largeText;
            if (game.playing)Text(new Rect(273, 629, 1050, 40), T("Time ", "用时 ") + game.elapsed.ToString("0") + "s    " + T("Collisions ", "碰撞 ") + game.collisions + "    " + T("Commands ", "指令 ") + game.commands, 21, ink);
            if (Button(new Rect(273, 766, 510, 65), "restartFromGuide", T("Restart rescue", "重新开始"), true, false, "", "", true))Push(Overlay.Restart);
            if (Button(new Rect(805, 766, 518, 65), "guideClose", T("Back to the chart / ESC", "返回海图 / ESC"), true, true, "", "", true))Pop();
        }
        void Results()
        {
            Fill(new Rect(0, 0, 1600, 1000), new Color(.025f, .055f, .07f, .9f));
            Fill(new Rect(330, 185, 940, 630), paper);
            Text(new Rect(385, 236, 840, 100), T("Everyone is home.", "所有人已安全返航。"), 64, ink, true);
            Text(new Rect(389, 364, 500, 56), T("MISSION COMPLETE / 2 RESCUED", "任务完成 / 已救援 2 人"), 28, ink, true);
            Text(new Rect(385, 455, 430, 117), game.Score.ToString(), 83, ink, true);
            Text(new Rect(825, 466, 390, 99), T("PERSONAL BEST  ", "个人最佳  ") + game.best + "\n" + T("Collisions  ", "碰撞  ") + game.collisions, 27, ink);
            if (Button(new Rect(388, 650, 824, 87), "again", T("Another rescue", "再次救援"), true, true, "", "", true))Restart();
        }
        void OnGUI()
        {
            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label)
                {
                    padding = new RectOffset(0, 0, 0, 0)
                };
                buttonStyle = new GUIStyle(GUIStyle.none);
            }
            if (Event.current.type == EventType.Repaint)focusable.Clear();
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY), Quaternion.identity, Vector3.one * scale);
            Fill(new Rect(0, 0, 1600, 125), ink);
            Fill(new Rect(0, 125, 1600, 2), edge);
            Text(new Rect(40, 31, 430, 59), T("SIGNAL LOST", "断线救援"), 42, null, true);
            if (game.playing)Text(new Rect(495, 37, 540, 56), game.rescued == 2 ? T("02 / 02 → RETURN TO H", "02 / 02 → 返回 H 港口") : T("RESCUE  ", "救援  ") + game.rescued + " / 2", 29, amber, true);
            if (Button(new Rect(1070, 36, 155, 54), "language", "EN / 中文"))game.english = !game.english;
            if (game.playing && Button(new Rect(1245, 36, 145, 54), "pause", game.paused ? T("Resume", "继续") : T("Pause", "暂停")))game.TogglePause();
            if (Button(new Rect(1410, 36, 150, 54), "guide", T("Menu", "菜单")))Push(Overlay.Guide);
            if (!game.playing)Title();
            else
            {
                Map();
                Radio();
                Controls();
            }
            if (game.finished && screens.Count == 0)Results();
            if (screens.Count > 0)Modal();
            if (Event.current.type == EventType.Repaint && !focusable.Contains(focus) && focusable.Count > 0)focus = focusable[0];
        }
    }
}
