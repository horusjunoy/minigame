using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Client
{
    public sealed class MatchmakerClient : MonoBehaviour
    {
        [SerializeField] private string baseUrl = "http://127.0.0.1:8080";
        [SerializeField] private float requestTimeoutSeconds = 10f;

        public void SetBaseUrl(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                baseUrl = value.TrimEnd('/');
            }
        }

        public IEnumerator CreateMatch(string minigameId, int maxPlayers, Action<Result<CreateMatchResponse>> callback)
        {
            var payload = new CreateMatchRequest
            {
                minigame_id = minigameId,
                max_players = maxPlayers
            };
            return SendJson("POST", "/matches", payload, callback);
        }

        public IEnumerator JoinMatch(string matchId, Action<Result<JoinMatchResponse>> callback)
        {
            return SendJson("POST", $"/matches/{matchId}/join", null, callback);
        }

        public IEnumerator EndMatch(string matchId, string reason, Action<Result<EndMatchResponse>> callback)
        {
            var payload = new EndMatchRequest { reason = reason };
            return SendJson("POST", $"/matches/{matchId}/end", payload, callback);
        }

        public IEnumerator ListMatches(string minigameId, Action<Result<MatchListResponse>> callback)
        {
            var query = string.IsNullOrWhiteSpace(minigameId) ? "" : $"?minigame_id={UnityWebRequest.EscapeURL(minigameId)}";
            return SendJson("GET", $"/matches{query}", null, callback, isArrayResponse: true);
        }

        public IEnumerator VerifyToken(string token, Action<Result<TokenVerifyResponse>> callback)
        {
            var payload = new TokenVerifyRequest { token = token };
            return SendJson("POST", "/tokens/verify", payload, callback);
        }

        private IEnumerator SendJson<T>(string method, string path, object payload, Action<Result<T>> callback, bool isArrayResponse = false)
        {
            var url = baseUrl.TrimEnd('/') + path;
            var request = new UnityWebRequest(url, method);
            request.timeout = Mathf.CeilToInt(requestTimeoutSeconds);
            request.downloadHandler = new DownloadHandlerBuffer();

            if (payload != null)
            {
                var json = JsonUtility.ToJson(payload);
                var bytes = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bytes);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            yield return request.SendWebRequest();

            var result = new Result<T>
            {
                StatusCode = request.responseCode,
                Raw = request.downloadHandler != null ? request.downloadHandler.text : string.Empty
            };

            if (request.result != UnityWebRequest.Result.Success)
            {
                result.Error = string.IsNullOrWhiteSpace(request.error) ? "request_failed" : request.error;
                callback?.Invoke(result);
                yield break;
            }

            if (request.responseCode >= 400)
            {
                var error = TryParseError(result.Raw);
                result.Error = string.IsNullOrWhiteSpace(error) ? "request_failed" : error;
                callback?.Invoke(result);
                yield break;
            }

            if (typeof(T) == typeof(MatchListResponse) && isArrayResponse)
            {
                var wrapped = $"{{\"items\":{result.Raw}}}";
                var parsed = JsonUtility.FromJson<MatchListResponse>(wrapped);
                result.Payload = (T)(object)parsed;
            }
            else if (!string.IsNullOrWhiteSpace(result.Raw))
            {
                result.Payload = JsonUtility.FromJson<T>(result.Raw);
            }

            result.Success = true;
            callback?.Invoke(result);
        }

        private static string TryParseError(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var parsed = JsonUtility.FromJson<ErrorResponse>(json);
                return parsed != null ? parsed.error : null;
            }
            catch
            {
                return null;
            }
        }

        [Serializable]
        public sealed class Result<T>
        {
            public bool Success;
            public string Error;
            public long StatusCode;
            public string Raw;
            public T Payload;
        }

        [Serializable]
        private sealed class ErrorResponse
        {
            public string error;
        }

        [Serializable]
        private sealed class CreateMatchRequest
        {
            public string minigame_id;
            public int max_players;
        }

        [Serializable]
        public sealed class CreateMatchResponse
        {
            public string match_id;
            public string endpoint;
            public string join_token;
            public string status;
        }

        [Serializable]
        public sealed class JoinMatchResponse
        {
            public string endpoint;
            public string join_token;
        }

        [Serializable]
        public sealed class EndMatchResponse
        {
            public string status;
        }

        [Serializable]
        public sealed class TokenVerifyResponse
        {
            public string match_id;
            public string player_id;
            public long exp;
        }

        [Serializable]
        public sealed class MatchListResponse
        {
            public MatchListEntry[] items;
        }

        [Serializable]
        public sealed class MatchListEntry
        {
            public string match_id;
            public int players;
            public int max_players;
            public string status;
            public string minigame_id;
        }

        [Serializable]
        private sealed class EndMatchRequest
        {
            public string reason;
        }

        [Serializable]
        private sealed class TokenVerifyRequest
        {
            public string token;
        }
    }
}
