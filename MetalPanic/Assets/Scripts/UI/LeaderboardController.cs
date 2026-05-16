using System.Collections;
using System.Collections.Generic;
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
        const int RowCount = 5;
        const string DefaultPlayerName = "PLAYER";

        [SerializeField] string leaderboardUrl = "http://localhost:3000/leaderboard";
        [SerializeField, Min(0.1f)] float requestTimeoutSeconds = 3f;
        [SerializeField] bool loadOnEnable = true;
        [SerializeField] string playerName = DefaultPlayerName;

        static readonly LeaderboardEntry[] FallbackEntries =
        {
            new LeaderboardEntry(1, "CRIS", 99450),
            new LeaderboardEntry(2, "CLAUDE", 82100),
            new LeaderboardEntry(3, "JAMMER42", 55000),
            new LeaderboardEntry(4, "PLAYER1", 32400),
            new LeaderboardEntry(5, "ANON_", 12000),
        };

        UIDocument document;
        Coroutine requestRoutine;

        void Awake()
        {
            document = GetComponent<UIDocument>();
        }

        void OnEnable()
        {
            ApplyEntries(FallbackEntries);

            if (loadOnEnable && !string.IsNullOrWhiteSpace(leaderboardUrl))
                Refresh();
        }

        void OnDisable()
        {
            if (requestRoutine != null)
            {
                StopCoroutine(requestRoutine);
                requestRoutine = null;
            }
        }

        public void Refresh()
        {
            if (!isActiveAndEnabled)
                return;

            if (requestRoutine != null)
                StopCoroutine(requestRoutine);

            requestRoutine = StartCoroutine(LoadLeaderboard());
        }

        public void SubmitScore(long score)
        {
            SubmitScore(playerName, score);
        }

        public void SubmitScore(string name, long score)
        {
            if (!isActiveAndEnabled || string.IsNullOrWhiteSpace(leaderboardUrl))
                return;

            if (requestRoutine != null)
                StopCoroutine(requestRoutine);

            requestRoutine = StartCoroutine(PostScore(name, score));
        }

        IEnumerator LoadLeaderboard()
        {
            using UnityWebRequest request = UnityWebRequest.Get(leaderboardUrl);
            request.timeout = Mathf.Max(1, Mathf.CeilToInt(requestTimeoutSeconds));
            yield return request.SendWebRequest();

            requestRoutine = null;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Leaderboard request failed: {request.error}", this);
                yield break;
            }

            if (TryParsePayload(request.downloadHandler.text, out LeaderboardEntry[] entries))
                ApplyEntries(entries);
        }

        IEnumerator PostScore(string name, long score)
        {
            LeaderboardSubmission submission = new LeaderboardSubmission
            {
                name = string.IsNullOrWhiteSpace(name) ? DefaultPlayerName : name,
                score = score < 0 ? 0 : score
            };

            string json = JsonUtility.ToJson(submission);
            byte[] body = Encoding.UTF8.GetBytes(json);

            using UnityWebRequest request = new UnityWebRequest(leaderboardUrl, "POST")
            {
                uploadHandler = new UploadHandlerRaw(body),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Mathf.Max(1, Mathf.CeilToInt(requestTimeoutSeconds))
            };
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            requestRoutine = null;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Leaderboard submit failed: {request.error}", this);
                yield break;
            }

            if (TryParsePayload(request.downloadHandler.text, out LeaderboardEntry[] entries))
                ApplyEntries(entries);
        }

        void ApplyEntries(IReadOnlyList<LeaderboardEntry> entries)
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null || entries == null)
                return;

            for (int i = 0; i < RowCount; i++)
            {
                int row = i + 1;
                bool hasEntry = i < entries.Count;
                LeaderboardEntry entry = hasEntry ? entries[i] : null;

                Label rankLabel = QueryRowLabel(root, "rank", row);
                Label nameLabel = QueryRowLabel(root, "name", row);
                Label scoreLabel = QueryRowLabel(root, "score", row);
                VisualElement rowElement = rankLabel?.parent ?? nameLabel?.parent ?? scoreLabel?.parent;

                if (rowElement != null)
                    rowElement.style.display = hasEntry ? DisplayStyle.Flex : DisplayStyle.None;

                if (!hasEntry)
                    continue;

                int rankValue = entry.rank > 0 ? entry.rank : row;

                if (rankLabel != null)
                    rankLabel.text = "#" + rankValue;
                if (nameLabel != null)
                    nameLabel.text = string.IsNullOrWhiteSpace(entry.name) ? "ANON" : entry.name.ToUpperInvariant();
                if (scoreLabel != null)
                    scoreLabel.text = entry.score.ToString("N0", CultureInfo.InvariantCulture) + " pts";
            }
        }

        static Label QueryRowLabel(VisualElement root, string field, int row)
        {
            Label label = root?.Q<Label>($"leaderboard-{field}-{row}");
            if (label == null && row == 4)
                label = root?.Q<Label>($"leaderboard-{field}-player");

            return label;
        }

        static bool TryParsePayload(string json, out LeaderboardEntry[] entries)
        {
            entries = null;

            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                LeaderboardPayload payload = JsonUtility.FromJson<LeaderboardPayload>(json);
                if (payload != null && payload.entries != null && payload.entries.Length > 0)
                {
                    entries = payload.entries;
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        [System.Serializable]
        sealed class LeaderboardPayload
        {
            public LeaderboardEntry[] entries;
        }

        [System.Serializable]
        sealed class LeaderboardEntry
        {
            public int rank;
            public string name;
            public long score;

            public LeaderboardEntry(int rank, string name, long score)
            {
                this.rank = rank;
                this.name = name;
                this.score = score;
            }
        }

        [System.Serializable]
        sealed class LeaderboardSubmission
        {
            public string name;
            public long score;
        }
    }
}
