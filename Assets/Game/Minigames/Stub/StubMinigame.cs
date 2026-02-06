using Game.Core;
using Game.Runtime;

namespace Game.Minigames.Stub
{
    public sealed class StubMinigame : IMinigame
    {
        private IMinigameContext _context;

        public void OnLoad(IMinigameContext context)
        {
            _context = context;
            _context.Logger.Log(LogLevel.Info, "minigame_loaded", "Stub minigame loaded", null, _context.Telemetry);
        }

        public void OnGameStart()
        {
            _context.Logger.Log(LogLevel.Info, "match_started", "Stub match started", null, _context.Telemetry);
        }

        public void OnPlayerJoin(PlayerRef player)
        {
            _context.Logger.Log(LogLevel.Info, "player_joined", $"Player joined: {player}", null, _context.Telemetry);
        }

        public void OnPlayerLeave(PlayerRef player)
        {
            _context.Logger.Log(LogLevel.Info, "player_left", $"Player left: {player}", null, _context.Telemetry);
        }

        public void OnTick(float dt)
        {
            _context.Logger.Log(LogLevel.Debug, "tick", $"Tick dt={dt}", null, _context.Telemetry);
        }

        public void OnGameEnd(GameResult result)
        {
            _context.Logger.Log(LogLevel.Info, "match_ended", $"Stub match ended: {result.Reason}", null, _context.Telemetry);
        }
    }
}
