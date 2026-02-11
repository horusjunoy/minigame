namespace Game.Network
{
    public struct SnapshotEntityV1
    {
        public int v;
        public string player_id;
        public float px;
        public float py;
        public float pz;
        public float rx;
        public float ry;
        public float rz;
        public float rw;

        public SnapshotEntityV1(string playerId, float px, float py, float pz, float rx, float ry, float rz, float rw, int version = 1)
        {
            v = version;
            player_id = playerId ?? string.Empty;
            this.px = px;
            this.py = py;
            this.pz = pz;
            this.rx = rx;
            this.ry = ry;
            this.rz = rz;
            this.rw = rw;
        }
    }

    public struct SnapshotV1
    {
        public const int Version = 1;
        public int v;
        public long server_time_ms;
        public SnapshotEntityV1[] entities;

        public SnapshotV1(long serverTimeMs, SnapshotEntityV1[] entities, int version = Version)
        {
            v = version;
            server_time_ms = serverTimeMs;
            this.entities = entities ?? new SnapshotEntityV1[0];
        }
    }
}
