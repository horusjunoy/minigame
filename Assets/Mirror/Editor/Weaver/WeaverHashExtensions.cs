namespace Mirror.Weaver
{
    internal static class WeaverHashExtensions
    {
        // Same hashing as Mirror.Extensions.GetStableHashCode but localized for the weaver.
        public static int GetStableHashCode(this string text)
        {
            unchecked
            {
                uint hash = 0x811c9dc5;
                uint prime = 0x1000193;

                for (int i = 0; i < text.Length; ++i)
                {
                    hash = (hash ^ (byte)text[i]) * prime;
                }

                return (int)hash;
            }
        }
    }
}
