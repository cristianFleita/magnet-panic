using UnityEngine;
using UnityEngine.AI;

namespace MagnetPanic.Combat
{
    /// <summary>
    /// Lightweight NavMesh path follower that produces a steering direction without taking
    /// ownership of the transform. Designed to plug into ArkhamEnemy's existing
    /// CharacterController-based motor: it returns a unit vector toward the next path corner
    /// while the caller keeps using <c>MoveBy</c>, animation, strafing, and magnet routines.
    ///
    /// Recomputes are throttled (<see cref="recomputeInterval"/>) and triggered early when the
    /// target moves more than <see cref="targetMovedThreshold"/> from the last sample. The
    /// initial elapsed offset is randomized per instance to stagger CPU cost across enemies.
    /// </summary>
    public sealed class EnemyNavPath
    {
        NavMeshPath path;
        Vector3[] corners = System.Array.Empty<Vector3>();
        int cornerCount;
        int cornerIndex;

        Vector3 lastTargetSample;
        bool hasValidPath;
        float elapsed;

        public float recomputeInterval = 0.18f;
        public float targetMovedThreshold = 1.25f;
        public float arrivalThreshold = 0.55f;
        public float sampleSearchRadius = 1.5f;
        public int areaMask = NavMesh.AllAreas;

        public bool HasValidPath => hasValidPath && cornerCount > 1;

        /// <summary>Stagger initial recompute timing so a wave of enemies doesn't all plan on the same frame.</summary>
        public void Randomize()
        {
            elapsed = Random.value * recomputeInterval;
        }

        public void Reset()
        {
            hasValidPath = false;
            cornerCount = 0;
            cornerIndex = 0;
            elapsed = 0f;
            lastTargetSample = new Vector3(float.NaN, float.NaN, float.NaN);
        }

        /// <summary>
        /// Advance the throttle timer, recompute when needed, and advance the corner index as
        /// the follower catches up to each waypoint.
        /// </summary>
        public void Tick(Vector3 followerPosition, Vector3 targetPosition, float deltaTime)
        {
            elapsed += deltaTime;

            float moved = (targetPosition - lastTargetSample).sqrMagnitude;
            bool targetMoved = !hasValidPath || moved > targetMovedThreshold * targetMovedThreshold;

            if (elapsed >= recomputeInterval || targetMoved)
            {
                elapsed = 0f;
                Recompute(followerPosition, targetPosition);
            }

            AdvanceCorner(followerPosition);
        }

        /// <summary>
        /// Returns a unit steering direction (XZ plane) toward the next path corner. When no
        /// valid path is available falls back to <paramref name="fallbackDirection"/> so the
        /// caller never stalls.
        /// </summary>
        public Vector3 GetSteerDirection(Vector3 followerPosition, Vector3 fallbackDirection)
        {
            if (!HasValidPath || cornerIndex >= cornerCount)
                return fallbackDirection;

            Vector3 target = corners[cornerIndex];
            Vector3 delta = target - followerPosition;
            delta.y = 0f;

            if (delta.sqrMagnitude < 0.0001f)
                return fallbackDirection;

            return delta.normalized;
        }

        void Recompute(Vector3 followerPosition, Vector3 targetPosition)
        {
            lastTargetSample = targetPosition;

            // NavMeshPath allocates a native handle, which Unity forbids inside ctors/field
            // initializers. Defer the allocation until first use (we are guaranteed to be in
            // Update by this point).
            if (path == null)
                path = new NavMeshPath();

            if (!NavMesh.SamplePosition(followerPosition, out NavMeshHit fromHit, sampleSearchRadius, areaMask)
                || !NavMesh.SamplePosition(targetPosition, out NavMeshHit toHit, sampleSearchRadius * 1.5f, areaMask))
            {
                hasValidPath = false;
                cornerCount = 0;
                cornerIndex = 0;
                return;
            }

            if (!NavMesh.CalculatePath(fromHit.position, toHit.position, areaMask, path)
                || path.status == NavMeshPathStatus.PathInvalid)
            {
                hasValidPath = false;
                cornerCount = 0;
                cornerIndex = 0;
                return;
            }

            Vector3[] raw = path.corners;
            if (raw == null || raw.Length < 2)
            {
                hasValidPath = false;
                cornerCount = 0;
                cornerIndex = 0;
                return;
            }

            if (corners.Length < raw.Length)
                corners = new Vector3[raw.Length];

            for (int i = 0; i < raw.Length; i++)
                corners[i] = raw[i];

            cornerCount = raw.Length;
            // Skip the first corner: it's our own snapped position. Steer toward the next one.
            cornerIndex = 1;
            hasValidPath = true;
        }

        void AdvanceCorner(Vector3 followerPosition)
        {
            if (!hasValidPath)
                return;

            while (cornerIndex < cornerCount - 1)
            {
                Vector3 corner = corners[cornerIndex];
                Vector3 delta = corner - followerPosition;
                delta.y = 0f;
                if (delta.sqrMagnitude > arrivalThreshold * arrivalThreshold)
                    return;
                cornerIndex++;
            }
        }

        public void DrawDebug(Color color, float duration = 0f)
        {
            if (!hasValidPath)
                return;
            for (int i = cornerIndex; i < cornerCount - 1; i++)
                Debug.DrawLine(corners[i] + Vector3.up * 0.2f, corners[i + 1] + Vector3.up * 0.2f, color, duration);
        }
    }
}
