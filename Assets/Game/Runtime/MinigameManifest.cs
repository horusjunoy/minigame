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
        public string content_version;
        public string server_entry;
        public string client_entry;
        public AddressablesConfig addressables;
        public Settings settings;
        public MinigamePermissions permissions;
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

    [Serializable]
    public sealed class MinigamePermissions
    {
        public int max_entities = 128;
        public int max_broadcasts_per_tick = 16;
        public int max_sends_per_tick = 32;
        public int max_event_name_len = 64;
        public int max_payload_len = 512;
        public double tick_budget_ms = 33.0;
        public int allocation_sample_rate = 60;
    }
}
