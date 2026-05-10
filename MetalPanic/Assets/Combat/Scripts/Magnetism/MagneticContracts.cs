using UnityEngine;

namespace MagnetPanic.Combat
{
    public enum MagneticMarkState
    {
        Normal,
        Marked,
        Magnetized,
        Stunned
    }

    public enum MagneticObjectState
    {
        InWorld,
        Attracting,
        InOrbit,
        Projectile
    }

    public enum MagneticObjectType
    {
        LightScrap,
        Plate,
        Mine,
        Heavy
    }

    public interface IMarkable
    {
        MagneticMarkState MarkState { get; }
        void ApplyMark(int stacks);
        void SetMarkState(MagneticMarkState state);
        float GetTimeSinceLastMark();
    }

    public interface IAttractable
    {
        float MagneticMass { get; }
        MagneticObjectState MagneticState { get; }
        Transform AttractionTransform { get; }
        bool CanEnterOrbit { get; }

        void BeginAttract(Transform magnet);
        void TickAttract(Vector3 magnetPosition, float pullSpeed, float deltaTime);
        bool IsCloseEnoughForOrbit(Vector3 magnetPosition, float orbitRadius);
        void EnterOrbit(Transform orbitCenter);
        void TickOrbit(Vector3 orbitPosition, float deltaTime);
        void StopAttracting();
        void RejectFromOrbit(Vector3 magnetPosition, float rejectSpeed);
        void Repel(Vector3 direction, float speed);
        void ForcedEject(Vector3 direction, float speed);
    }
}
