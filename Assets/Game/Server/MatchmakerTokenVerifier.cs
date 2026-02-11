using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Game.Server
{
    public sealed class MatchmakerTokenVerifier
    {
        private readonly byte[] _secretBytes;

        public MatchmakerTokenVerifier(string secret)
        {
            _secretBytes = Encoding.UTF8.GetBytes(secret ?? string.Empty);
        }

        public bool TryValidate(string token, out TokenPayload payload, out string reason)
        {
            payload = default;
            reason = "invalid_token";
            if (string.IsNullOrWhiteSpace(token))
            {
                reason = "token_missing";
                return false;
            }

            var parts = token.Split('.');
            if (parts.Length != 2)
            {
                reason = "token_format";
                return false;
            }

            var bodyJson = Base64UrlDecode(parts[0]);
            if (string.IsNullOrWhiteSpace(bodyJson))
            {
                reason = "token_payload";
                return false;
            }

            var signature = parts[1];
            var expected = ComputeSignature(bodyJson);
            if (!SecureEquals(signature, expected))
            {
                reason = "token_signature";
                return false;
            }

            var parsed = JsonUtility.FromJson<TokenPayload>(bodyJson);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.match_id))
            {
                reason = "token_payload";
                return false;
            }

            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (parsed.exp > 0 && nowMs > parsed.exp)
            {
                reason = "token_expired";
                return false;
            }

            payload = parsed;
            reason = null;
            return true;
        }

        private string ComputeSignature(string bodyJson)
        {
            using (var hmac = new HMACSHA256(_secretBytes))
            {
                var bytes = Encoding.UTF8.GetBytes(bodyJson);
                var hash = hmac.ComputeHash(bytes);
                return Base64UrlEncode(hash);
            }
        }

        private static bool SecureEquals(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            var diff = 0;
            for (var i = 0; i < left.Length; i++)
            {
                diff |= left[i] ^ right[i];
            }
            return diff == 0;
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string Base64UrlDecode(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            var base64 = input.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2:
                    base64 += "==";
                    break;
                case 3:
                    base64 += "=";
                    break;
            }

            try
            {
                var bytes = Convert.FromBase64String(base64);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return null;
            }
        }

        [Serializable]
        public sealed class TokenPayload
        {
            public string match_id;
            public string player_id;
            public long exp;
        }
    }
}
