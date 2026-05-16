using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public sealed class LeaderboardUiController : MonoBehaviour
{
    private const int RowCount = 5;

    [SerializeField] private string leaderboardUrl = "http://localhost:3000/leaderboard";
    [SerializeField, Min(1)] private int requestTimeoutSeconds = 3;
    [SerializeField] private bool loadOnEnable = true;

    private UIDocument uiDocument;
    private Coroutine loadRoutine;

    private void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();

        if (loadOnEnable)
        {
            Refresh();
        }
    }

    private void OnDisable()
    {
        if (loadRoutine != null)
        {
            StopCoroutine(loadRoutine);
            loadRoutine = null;
        }
    }

    public void Refresh()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (loadRoutine != null)
        {
            StopCoroutine(loadRoutine);
        }

        loadRoutine = StartCoroutine(LoadLeaderboard());
    }

    private IEnumerator LoadLeaderboard()
    {
        using UnityWebRequest request = UnityWebRequest.Get(leaderboardUrl);
        request.timeout = requestTimeoutSeconds;

        yield return request.SendWebRequest();

        loadRoutine = null;

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"Leaderboard request failed: {request.error}", this);
            yield break;
        }

        LeaderboardResponse response;

        try
        {
            response = JsonUtility.FromJson<LeaderboardResponse>(request.downloadHandler.text);
        }
        catch (ArgumentException exception)
        {
            Debug.LogWarning($"Leaderboard response could not be parsed: {exception.Message}", this);
            yield break;
        }

        if (response?.entries == null)
        {
            Debug.LogWarning("Leaderboard response did not include entries.", this);
            yield break;
        }

        Render(response.entries);
    }

    private void Render(LeaderboardEntry[] entries)
    {
        VisualElement root = uiDocument.rootVisualElement;

        for (int rowIndex = 1; rowIndex <= RowCount; rowIndex++)
        {
            LeaderboardEntry entry = rowIndex <= entries.Length ? entries[rowIndex - 1] : null;

            Label rankLabel = QueryRowLabel(root, "rank", rowIndex);
            Label nameLabel = QueryRowLabel(root, "name", rowIndex);
            Label scoreLabel = QueryRowLabel(root, "score", rowIndex);
            VisualElement row = rankLabel?.parent ?? nameLabel?.parent ?? scoreLabel?.parent;

            if (row != null)
            {
                row.style.display = entry == null ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (entry == null)
            {
                continue;
            }

            if (rankLabel != null)
            {
                rankLabel.text = $"#{entry.rank}";
            }

            if (nameLabel != null)
            {
                nameLabel.text = entry.name;
            }

            if (scoreLabel != null)
            {
                scoreLabel.text = $"{entry.score.ToString("N0", CultureInfo.InvariantCulture)} pts";
            }
        }
    }

    private static Label QueryRowLabel(VisualElement root, string field, int rowIndex)
    {
        Label label = root.Q<Label>($"leaderboard-{field}-{rowIndex}");

        if (label == null && rowIndex == 4)
        {
            label = root.Q<Label>($"leaderboard-{field}-player");
        }

        return label;
    }

    [Serializable]
    private sealed class LeaderboardResponse
    {
        public LeaderboardEntry[] entries;
    }

    [Serializable]
    private sealed class LeaderboardEntry
    {
        public int rank;
        public string name;
        public int score;
    }
}

