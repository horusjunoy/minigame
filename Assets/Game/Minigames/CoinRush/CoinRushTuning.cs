using UnityEngine;

namespace Game.Minigames.CoinRush
{
    [CreateAssetMenu(menuName = "Minigames/CoinRush/Tuning", fileName = "CoinRushTuning")]
    public sealed class CoinRushTuning : ScriptableObject
    {
        public int pointsPerTick = 1;
        public float tickIntervalSeconds = 1f;
        public int scoreToWinOverride;
        public int matchDurationSecondsOverride;

        public void Validate()
        {
            pointsPerTick = Mathf.Max(1, pointsPerTick);
            tickIntervalSeconds = Mathf.Max(0.1f, tickIntervalSeconds);
            scoreToWinOverride = Mathf.Max(0, scoreToWinOverride);
            matchDurationSecondsOverride = Mathf.Max(0, matchDurationSecondsOverride);
        }

        private void OnValidate()
        {
            Validate();
        }
    }
}
