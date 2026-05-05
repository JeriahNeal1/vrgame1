using UnityEngine;

namespace VRGame.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SmithingStationSurfaceRelay : MonoBehaviour
    {
        [SerializeField]
        private SmithingStation station = null;

        public void Bind(SmithingStation targetStation)
        {
            station = targetStation;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (station == null || other == null)
            {
                return;
            }

            WorldItemView worldItemView = other.GetComponentInParent<WorldItemView>();
            if (worldItemView != null)
            {
                station.NotifyItemReleasedOnStation(worldItemView);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (station == null || other == null)
            {
                return;
            }

            WorldItemView worldItemView = other.GetComponentInParent<WorldItemView>();
            if (worldItemView != null)
            {
                station.ClearTargetItem(worldItemView);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (station == null || collision == null)
            {
                return;
            }

            WorldItemView worldItemView = collision.collider != null ? collision.collider.GetComponentInParent<WorldItemView>() : null;
            if (worldItemView == null)
            {
                return;
            }

            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            station.NotifyHammerStrike(worldItemView, collision.relativeVelocity.magnitude, point);
        }
    }
}
