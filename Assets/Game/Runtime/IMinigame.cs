using Game.Core;

namespace Game.Runtime
{
    public interface IMinigame
    {
        void OnLoad(IMinigameContext context);
        void OnGameStart();
        void OnPlayerJoin(PlayerRef player);
        void OnPlayerLeave(PlayerRef player);
        void OnTick(float dt);
        void OnGameEnd(GameResult result);
    }
}
