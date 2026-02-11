using System;

namespace Game.Runtime
{
    [Serializable]
    public sealed class MinigameManifest
    {
        public int schema_version;
        public string id;
        public string display_name;
        public string version;
        public string server_entry;
        public string client_entry;
        public AddressablesConfig addressables;
        public Settings settings;
    }

    [Serializable]
    public sealed class AddressablesConfig
    {
        public string[] scenes;
        public string[] prefabs;
    }

    [Serializable]
    public sealed class Settings
    {
        public int match_duration_s;
        public int score_to_win;
    }
}
