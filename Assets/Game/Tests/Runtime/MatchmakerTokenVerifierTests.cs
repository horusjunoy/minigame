using System;
using System.Security.Cryptography;
using System.Text;
using Game.Server;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Runtime
{
    public sealed class MatchmakerTokenVerifierTests
    {
        [Test]
        public void TryValidate_Rejects_MissingToken()
        {
            var verifier = new MatchmakerTokenVerifier("secret");

            var ok = verifier.TryValidate(string.Empty, out var payload, out var reason);

            Assert.IsFalse(ok);
            Assert.IsNull(payload);
            Assert.AreEqual("token_missing", reason);
        }

        [Test]
        public void TryValidate_Rejects_InvalidSignature()
        {
            var verifier = new MatchmakerTokenVerifier("secret");
            var signedProof = CreateSignedToken(
                keyMaterial: "other-secret",
                matchId: "m_local",
                playerId: "p_1",
                nbfMs: DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds(),
                expMs: DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds());

            var ok = verifier.TryValidate(signedProof, out var payload, out var reason);

            Assert.IsFalse(ok);
            Assert.IsNull(payload);
            Assert.AreEqual("token_signature", reason);
        }

        [Test]
        public void TryValidate_Rejects_ExpiredToken()
        {
            var verifier = new MatchmakerTokenVerifier("secret");
            var signedProof = CreateSignedToken(
                keyMaterial: "secret",
                matchId: "m_local",
                playerId: "p_1",
                nbfMs: DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds(),
                expMs: DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds());

            var ok = verifier.TryValidate(signedProof, out var payload, out var reason);

            Assert.IsFalse(ok);
            Assert.IsNull(payload);
            Assert.AreEqual("token_expired", reason);
        }

        [Test]
        public void TryValidate_Rejects_NotYetValidToken()
        {
            var verifier = new MatchmakerTokenVerifier("secret");
            var signedProof = CreateSignedToken(
                keyMaterial: "secret",
                matchId: "m_local",
                playerId: "p_1",
                nbfMs: DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds(),
                expMs: DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds());

            var ok = verifier.TryValidate(signedProof, out var payload, out var reason);

            Assert.IsFalse(ok);
            Assert.IsNull(payload);
            Assert.AreEqual("token_not_yet_valid", reason);
        }

        [Test]
        public void TryValidate_Accepts_ValidToken()
        {
            var verifier = new MatchmakerTokenVerifier("secret");
            var signedProof = CreateSignedToken(
                keyMaterial: "secret",
                matchId: "m_local",
                playerId: "p_1",
                nbfMs: DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds(),
                expMs: DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds());

            var ok = verifier.TryValidate(signedProof, out var payload, out var reason);

            Assert.IsTrue(ok);
            Assert.NotNull(payload);
            Assert.AreEqual("m_local", payload.match_id);
            Assert.AreEqual("p_1", payload.player_id);
            Assert.IsNull(reason);
        }

        private static string CreateSignedToken(string keyMaterial, string matchId, string playerId, long nbfMs, long expMs)
        {
            var payloadJson = JsonUtility.ToJson(new MatchmakerTokenVerifier.TokenPayload
            {
                match_id = matchId,
                player_id = playerId,
                nbf = nbfMs,
                exp = expMs
            });

            var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
            var encodedPayload = Base64UrlEncode(payloadBytes);

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(keyMaterial ?? string.Empty));
            var signatureBytes = hmac.ComputeHash(payloadBytes);
            var encodedSignature = Base64UrlEncode(signatureBytes);
            return encodedPayload + "." + encodedSignature;
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
