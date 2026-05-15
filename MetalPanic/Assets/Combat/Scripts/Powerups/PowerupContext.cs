using UnityEngine;

namespace MagnetPanic.Combat.Powerups
{
    /// <summary>
    /// Shared services handed to every <see cref="IPowerupEffect"/> when it
    /// activates. Effects pull dependencies from here instead of doing their
    /// own <c>FindObjectByType</c> at runtime.
    /// </summary>
    public sealed class PowerupContext
    {
        public PowerupController Controller;
        public Transform Player;
        public ArkhamPlayerMotor Motor;
        public ArkhamEnemyManager EnemyManager;
        public Vector3 ActivationPosition;
    }
}
