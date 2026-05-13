using UnityEngine;

namespace MagnetPanic.Combat
{
    public enum StrikeTargetState
    {
        Hidden,
        Normal,
        Counterable
    }

    public abstract class StrikeTargetPresenter : MonoBehaviour
    {
        public abstract void Show(ArkhamEnemy target, StrikeTargetState state);
        public abstract void Hide();
    }
}
