using System;
using UnityEngine;

namespace Game.Runtime
{
    public static class MinigameFactory
    {
        public static IMinigame CreateFromEntry(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                Debug.LogError("Minigame entry is empty.");
                return null;
            }

            var type = Type.GetType(entry);
            if (type == null)
            {
                Debug.LogError($"Minigame type not found: {entry}");
                return null;
            }

            if (!typeof(IMinigame).IsAssignableFrom(type))
            {
                Debug.LogError($"Type does not implement IMinigame: {entry}");
                return null;
            }

            return (IMinigame)Activator.CreateInstance(type);
        }
    }
}
