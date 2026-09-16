using System;
using System.Collections;
using UnityEngine;
namespace SignalLost
{
    public class RescueGame : MonoBehaviour
    {
        public Transform boat, beam;
        public GameObject[] survivors;
        public Animator animator;
        public float acknowledgementSeconds = 1.8f, fastSeconds = .75f, moveSeconds = .65f;
        public Vector2Int cell;
        public int heading, rescued, collisions, commands;
        public bool playing, finished, paused, reliable, busy, english = true;
        public Command sent, received;
        public string phase = "ready";
        public float elapsed;
        public int best;
        public event Action < string > Feedback;
        public float replyRemaining, replyDuration;
        public bool CancelRequested
        {
            get
            {
                return cancel;
            }
        }
        public bool IsAboard(int index)
        {
            return aboard[index];
        }
        // 重开时不重置随机序列。
        internal System.Random transmissionRandom = new System.Random();
        public bool inputBlocked;
        bool cancel;
        internal bool selfTesting;
        bool[] aboard = new bool[2];
        AudioSource sound;
        AudioClip ping;
        public static Vector3 World(Vector2Int position)
        {
            return new Vector3(position.x - 4.5f, 0.23f, position.y - 4);
        }
        // 碰撞、指令和耗时扣分，最低为 0。
        public int Score
        {
            get
            {
                int collisionPenalty = collisions * 150;
                int commandPenalty = commands * 5;
                int timePenalty = Mathf.RoundToInt(elapsed * 2);
                int score = 2000 - collisionPenalty - commandPenalty - timePenalty;
                return Mathf.Max(0, score);
            }
        }
        void Start()
        {
            Application.targetFrameRate = 60;
            sound = gameObject.AddComponent < AudioSource > ();
            ping = AudioClip.Create("Radio", 2205, 1, 22050, false);
            var a = new float[2205];
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = Mathf.Sin(i * 2 * Mathf.PI * 740 / 22050) * .12f * (1f - i / (float)a.Length);
            }
            ping.SetData(a, 0);
            best = PlayerPrefs.GetInt("SignalLostRandomBest", 0);
            Reset(false);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-rescueSelfTest") >= 0)
            {
                StartCoroutine(RescueSelfTest.Run(this));
            }
        }
        // 先停止旧协程，再重置本局状态。
        public void Reset(bool start)
        {
            StopAllCoroutines();
            cell = RescueRules.Port;
            heading = 0;
            rescued = collisions = commands = 0;
            elapsed = 0;
            busy = finished = paused = reliable = false;
            playing = start;
            phase = "ready";
            for (int i = 0; i < 2; i++)
            {
                aboard[i] = false;
                survivors[i].SetActive(true);
            }
            boat.position = World(cell);
            boat.rotation = Quaternion.identity;
            animator.speed = 1;
            animator.Rebind();
            animator.Update(0);
            replyRemaining = 0;
            Feedback?.Invoke("reset");
        }
        void Update()
        {
            if (playing && !finished && !paused)
            {
                elapsed += Time.deltaTime;
            }
            if (beam)
            {
                beam.Rotate(0, Time.deltaTime * 18, 0);
            }
            if (!playing || finished || inputBlocked)
            {
                return;
            }
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TogglePause();
            }
            if (paused)
            {
                return;
            }
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                Send(Command.Forward);
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                Send(Command.Left);
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                Send(Command.Right);
            }
            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                Cancel();
            }
            if (Input.GetKeyDown(KeyCode.Tab) && !busy)
            {
                reliable = !reliable;
            }
        }
        // 暂停时同时冻结动画和指令流程。
        public void TogglePause()
        {
            paused = !paused;
            if (paused)
            {
                animator.speed = 0;
            }
            else
            {
                animator.speed = 1;
            }
        }
        // 发送时抽样一次，固定实际收到的指令。
        public void Send(Command command)
        {
            if (busy || paused || !playing || finished)
            {
                return;
            }
            sent = command;
            received = RescueRules.Decode(command, transmissionRandom.NextDouble(), reliable);
            commands++;
            cancel = false;
            busy = true;
            StartCoroutine(Execute());
        }
        // 仅在等待回复时允许撤销。
        public void Cancel()
        {
            if (busy && phase == "ack" && !paused && !inputBlocked)
            {
                cancel = true;
                Feedback?.Invoke("cancel_requested");
            }
        }
        // 逐帧等待，暂停时不计时。
        IEnumerator Wait(float duration)
        {
            float t = 0;
            while (t < duration)
            {
                if (!paused)
                {
                    t += Time.deltaTime;
                }
                yield return null;
            }
        }
        // 依次处理回复、行动、救援和返港。
        IEnumerator Execute()
        {
            phase = "ack";
            if (reliable)
            {
                replyDuration = acknowledgementSeconds;
            }
            else
            {
                replyDuration = fastSeconds;
            }
            replyRemaining = replyDuration;
            Feedback?.Invoke("sent");
            sound.PlayOneShot(ping);
            while (replyRemaining > 0 && !cancel)
            {
                if (!paused)
                {
                    replyRemaining = Mathf.Max(0, replyRemaining - Time.deltaTime);
                }
                yield return null;
            }
            if (cancel)
            {
                phase = "cancelled";
                busy = false;
                replyRemaining = 0;
                Feedback?.Invoke("cancelled");
                yield break;
            }
            phase = "moving";
            Feedback?.Invoke("moving");
            if (received == Command.Forward)
            {
                yield return MoveForward();
            }
            else
            {
                yield return TurnBoat();
            }
            yield return RescuePeople();
            CheckWin();
            busy = false;
        }
        IEnumerator MoveForward()
        {
            Vector2Int target = cell + RescueRules.Directions[heading];
            if (RescueRules.Blocked(target))
            {
                collisions++;
                phase = "collision";
                Feedback?.Invoke("collision");
                animator.SetTrigger("Hit");
                yield return Wait(.5f);
                yield break;
            }
            // 移动动画结束后再更新网格位置。
            Vector3 from = boat.position;
            Vector3 to = World(target);
            float t = 0;
            animator.SetFloat("Speed", 1);
            while (t < 1)
            {
                if (!paused)
                {
                    t += Time.deltaTime / moveSeconds;
                }
                boat.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
            cell = target;
            animator.SetFloat("Speed", 0);
        }
        IEnumerator TurnBoat()
        {
            heading = RescueRules.Turn(heading, received);
            Quaternion from = boat.rotation;
            Quaternion to = Quaternion.Euler(0, heading * 90, 0);
            if (received == Command.Left)
            {
                animator.SetFloat("Turn", - 1);
            }
            else
            {
                animator.SetFloat("Turn", 1);
            }
            float t = 0;
            while (t < 1)
            {
                if (!paused)
                {
                    t += Time.deltaTime / moveSeconds;
                }
                boat.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
            animator.SetFloat("Turn", 0);
        }
        IEnumerator RescuePeople()
        {
            // 到达目标格救援，避免重复计数。
            for (int i = 0; i < aboard.Length; i++)
            {
                if (aboard[i] || cell != RescueRules.People[i])
                {
                    continue;
                }
                phase = "rescue";
                animator.SetTrigger("Rescue");
                yield return Wait(1.2f);
                aboard[i] = true;
                survivors[i].SetActive(false);
                rescued++;
                Feedback?.Invoke("rescued");
            }
        }
        void CheckWin()
        {
            // 救齐两人并返港后结算，保存本地最佳。
            if (cell == RescueRules.Port && rescued == 2)
            {
                finished = true;
                phase = "complete";
                Feedback?.Invoke("complete");
                if (Score > best && !selfTesting)
                {
                    best = Score;
                    PlayerPrefs.SetInt("SignalLostRandomBest", best);
                    PlayerPrefs.Save();
                }
            }
            else if (phase != "collision")
            {
                phase = "ready";
            }
        }
    }
}
