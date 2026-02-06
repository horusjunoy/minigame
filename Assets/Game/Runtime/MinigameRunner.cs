using Game.Core;

namespace Game.Runtime
{
    public sealed class MinigameRunner
    {
        private readonly IMinigame _minigame;
        private readonly IMinigameContext _context;

        public MinigameRunner(IMinigame minigame, IMinigameContext context)
        {
            _minigame = minigame;
            _context = context;
        }

        public void Load()
        {
            SafeInvoke("minigame_loaded", () => _minigame.OnLoad(_context));
        }

        public void Start()
        {
            SafeInvoke("match_started", _minigame.OnGameStart);
        }

        public void Tick(float dt)
        {
            SafeInvoke("tick", () => _minigame.OnTick(dt));
        }

        public void End(GameResult result)
        {
            SafeInvoke("match_ended", () => _minigame.OnGameEnd(result));
        }

        private void SafeInvoke(string eventName, System.Action action)
        {
            try
            {
                action();
            }
            catch (System.Exception ex)
            {
                _context.Logger.Log(LogLevel.Error, "minigame_error", ex.Message, ex, _context.Telemetry);
                _context.Logger.Log(LogLevel.Error, eventName, "Minigame lifecycle failed", null, _context.Telemetry);
                throw;
            }
        }
    }
}
