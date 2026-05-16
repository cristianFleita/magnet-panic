using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace MagnetPanic.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class LeaderboardController : MonoBehaviour
    {
        public const int TopCount = 5;

        [SerializeField] string leaderboardUrl = "http://localhost:3000/leaderboard";
        [SerializeField, Min(0.1f)] float requestTimeoutSeconds = 3f;
        [SerializeField] bool loadOnEnable = true;

        static readonly LeaderboardEntry[] FallbackEntries =
        {
            new LeaderboardEntry(1, "CRIS", 99450),
            new LeaderboardEntry(2, "CLAUDE", 82100),
            new LeaderboardEntry(3, "JAMMER42", 55000),
            new LeaderboardEntry(4, "PLAYER1", 32400),
            new LeaderboardEntry(5, "ANON", 12000),
        };

        UIDocument document;
        Coroutine activeRoutine;

        public string LeaderboardUrl => leaderboardUrl;

        void Awake()
        {
            document = GetComponent<UIDocument>();
        }

        void OnEnable()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            RenderTopFive(FallbackEntries, null, 0L);

            if (loadOnEnable && !string.IsNullOrWhiteSpace(leaderboardUrl))
                Refresh();
        }

        void OnDisable()
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }
        }

        public void Refresh()
        {
            StartRoutine(LoadThenRender());
        }

        public void FetchTopFive(Action<LeaderboardEntry[]> onResult)
        {
            StartRoutine(Fetch(onResult));
        }

        public void SubmitScore(string name, long score, Action<LeaderboardEntry[]> onResult)
        {
            StartRoutine(Post(name, score, onResult));
        }

        public void RenderTopFive(LeaderboardEntry[] entries, string highlightName, long highlightScore)
        {
            VisualElement table = ResolveTable();
            if (table == null || entries == null)
                return;

            table.Clear();

            int highlightIndex = FindHighlightIndex(entries, highlightName, highlightScore);
            int count = Mathf.Min(entries.Length, TopCount);
            for (int i = 0; i < count; i++)
                table.Add(BuildRow(entries[i], i == highlightIndex));
        }

        public void RenderMissedTopFive(LeaderboardEntry[] entries, string playerName, long playerScore)
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null)
                return;

            VisualElement table = root.Q<VisualElement>("leaderboard-table");
            int existing = 0;

            if (table != null)
            {
                table.Clear();
                int count = entries != null ? Mathf.Min(entries.Length, TopCount) : 0;
                for (int i = 0; i < count; i++)
                    table.Add(BuildRow(entries[i], false));
                existing = count;

                int ourRank = existing + 1;
                LeaderboardEntry ourEntry = new LeaderboardEntry(ourRank, playerName, playerScore);
                table.Add(BuildRow(ourEntry, true));
            }

            long cutoff = entries != null && entries.Length >= TopCount
                ? entries[TopCount - 1].score
                : 0L;
            long needed = Math.Max(0L, (cutoff + 1L) - playerScore);

            if (UiDocumentQuery.TryGetLabel(root, "top-five-cutoff-value", out Label cutoffLabel))
                cutoffLabel.text = cutoff.ToString("N0", CultureInfo.InvariantCulture) + " PTS";

            if (UiDocumentQuery.TryGetLabel(root, "points-needed-value", out Label neededLabel))
                neededLabel.text = "+" + needed.ToString("N0", CultureInfo.InvariantCulture) + " PTS";
        }

        IEnumerator LoadThenRender()
        {
            LeaderboardEntry[] entries = null;
            yield return Fetch(result => entries = result);
            if (entries != null)
                RenderTopFive(entries, null, 0L);
        }

        IEnumerator Fetch(Action<LeaderboardEntry[]> onResult)
        {
            using UnityWebRequest request = UnityWebRequest.Get(leaderboardUrl);
            request.timeout = Mathf.Max(1, Mathf.CeilToInt(requestTimeoutSeconds));
            yield return request.SendWebRequest();
            activeRoutine = null;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Leaderboard] GET failed: {request.error}", this);
                onResult?.Invoke(null);
                yield break;
            }

            onResult?.Invoke(TryParse(request.downloadHandler.text));
        }

        IEnumerator Post(string name, long score, Action<LeaderboardEntry[]> onResult)
        {
            LeaderboardSubmission submission = new LeaderboardSubmission
            {
                name = string.IsNullOrWhiteSpace(name) ? "PLAYER" : name,
                score = score < 0L ? 0L : score
            };
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(submission));

            using UnityWebRequest request = new UnityWebRequest(leaderboardUrl, "POST")
            {
                uploadHandler = new UploadHandlerRaw(body),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Mathf.Max(1, Mathf.CeilToInt(requestTimeoutSeconds))
            };
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();
            activeRoutine = null;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Leaderboard] POST failed: {request.error}", this);
                onResult?.Invoke(null);
                yield break;
            }

            onResult?.Invoke(TryParse(request.downloadHandler.text));
        }

        void StartRoutine(IEnumerator routine)
        {
            if (!isActiveAndEnabled)
                return;

            if (activeRoutine != null)
                StopCoroutine(activeRoutine);

            activeRoutine = StartCoroutine(routine);
        }

        VisualElement ResolveTable()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            return root?.Q<VisualElement>("leaderboard-table");
        }

        static int FindHighlightIndex(LeaderboardEntry[] entries, string name, long score)
        {
            if (string.IsNullOrEmpty(name) || entries == null)
                return -1;

            for (int i = 0; i < entries.Length; i++)
            {
                if (string.Equals(entries[i].name, name, StringComparison.OrdinalIgnoreCase)
                    && entries[i].score == score)
                    return i;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                if (string.Equals(entries[i].name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        static VisualElement BuildRow(LeaderboardEntry entry, bool isPlayer)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("leaderboard-row");
            if (isPlayer)
                row.AddToClassList("player-row");

            Label rank = new Label("#" + (entry.rank > 0 ? entry.rank : 1));
            rank.AddToClassList("rank-cell");
            row.Add(rank);

            Label name = new Label(string.IsNullOrWhiteSpace(entry.name) ? "ANON" : entry.name.ToUpperInvariant());
            name.AddToClassList("name-cell");
            row.Add(name);

            Label score = new Label(entry.score.ToString("N0", CultureInfo.InvariantCulture) + " pts");
            score.AddToClassList("points-cell");
            row.Add(score);

            if (isPlayer)
            {
                Label tag = new Label("YOU");
                tag.AddToClassList("highlight-tag");
                row.Add(tag);
            }

            return row;
        }

        static LeaderboardEntry[] TryParse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                LeaderboardPayload payload = JsonUtility.FromJson<LeaderboardPayload>(json);
                return payload != null && payload.entries != null && payload.entries.Length > 0
                    ? payload.entries
                    : null;
            }
            catch
            {
                return null;
            }
        }

        [Serializable]
        public sealed class LeaderboardEntry
        {
            public int rank;
            public string name;
            public long score;

            public LeaderboardEntry() { }

            public LeaderboardEntry(int rank, string name, long score)
            {
                this.rank = rank;
                this.name = name;
                this.score = score;
            }
        }

        [Serializable]
        sealed class LeaderboardPayload
        {
            public LeaderboardEntry[] entries;
        }

        [Serializable]
        sealed class LeaderboardSubmission
        {
            public string name;
            public long score;
        }
    }
}
