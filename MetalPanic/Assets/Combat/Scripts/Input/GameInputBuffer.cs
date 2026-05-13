using System;

namespace MagnetPanic.Combat
{
    public enum GameInputIntent
    {
        PullToggle = 0,
        Strike = 1,
        Counter = 2,
        Dodge = 3,
        Struggle = 4,
        Upgrade1 = 5,
        Upgrade2 = 6,
        Upgrade3 = 7,
        Pause = 8
    }

    public enum GameInputState
    {
        Disabled = 0,
        Gameplay = 1,
        UI = 2
    }

    public sealed class GameInputBuffer
    {
        const int IntentCount = (int)GameInputIntent.Pause + 1;  // auto-adjusts

        readonly bool[] buffered = new bool[IntentCount];
        readonly float[] timestamps = new float[IntentCount];

        public void Record(GameInputIntent intent, float timestamp)
        {
            int index = IntentIndex(intent);
            buffered[index] = true;
            timestamps[index] = timestamp;
        }

        public bool Consume(GameInputIntent intent, float currentTime, float bufferWindow)
        {
            int index = IntentIndex(intent);
            if (!buffered[index])
                return false;

            bool valid = currentTime - timestamps[index] <= bufferWindow;
            buffered[index] = false;
            return valid;
        }

        public void Clear(GameInputIntent intent)
        {
            buffered[IntentIndex(intent)] = false;
        }

        public void Reset()
        {
            Array.Clear(buffered, 0, buffered.Length);
            Array.Clear(timestamps, 0, timestamps.Length);
        }

        static int IntentIndex(GameInputIntent intent)
        {
            int index = (int)intent;
            if (index < 0 || index >= IntentCount)
                throw new ArgumentOutOfRangeException(nameof(intent), intent, "Unknown input intent.");

            return index;
        }
    }
}
