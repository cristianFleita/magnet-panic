using System.Text;
using UnityEngine;

namespace MagnetPanic
{
    public static class PlayerIdentity
    {
        const string Prefix = "PLAYER#";
        const int HashLength = 5;
        const string Alphabet = "0123456789ABCDEF";

        static string cachedName;

        public static string CurrentName
        {
            get
            {
                if (string.IsNullOrEmpty(cachedName))
                    cachedName = Generate();
                return cachedName;
            }
        }

        public static string NewRun()
        {
            cachedName = Generate();
            return cachedName;
        }

        static string Generate()
        {
            StringBuilder builder = new StringBuilder(Prefix.Length + HashLength);
            builder.Append(Prefix);
            for (int i = 0; i < HashLength; i++)
                builder.Append(Alphabet[Random.Range(0, Alphabet.Length)]);
            return builder.ToString();
        }
    }
}
