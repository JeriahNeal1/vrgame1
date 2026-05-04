using UnityEngine;

namespace VRGame.Runtime
{
    public sealed class DefaultHandItemSpawner : MonoBehaviour, IVRHandItemSpawner
    {
        [SerializeField]
        private Transform leftHandSpawnTransform = null;

        [SerializeField]
        private Transform rightHandSpawnTransform = null;

        [SerializeField]
        private Transform fallbackSpawnTransform = null;

        [SerializeField]
        private bool parentToSpawnTransformWhenNoGrabber = false;

        public bool TrySpawnIntoHand(HandItemSpawnRequest request, out WorldItemView worldItemView, out string message)
        {
            worldItemView = null;

            if (request == null)
            {
                message = "Spawn request is null.";
                return false;
            }

            if (request.WorldPrefab == null)
            {
                message = "Spawn request has no world prefab.";
                return false;
            }

            Transform spawnTransform = ResolveSpawnTransform(request.RequestedHandId, request.SpawnOrigin);
            Vector3 spawnPosition = spawnTransform != null ? spawnTransform.position : transform.position;
            Quaternion spawnRotation = spawnTransform != null ? spawnTransform.rotation : transform.rotation;
            Transform parent = parentToSpawnTransformWhenNoGrabber ? spawnTransform : request.OptionalParent;

            GameObject spawned = Instantiate(request.WorldPrefab, spawnPosition, spawnRotation, parent);
            worldItemView = EnsureWorldItemView(spawned);
            if (request.Binding != null)
            {
                worldItemView.Bind(request.Binding);
            }

            message = "Spawned item with default transform spawner.";
            return true;
        }

        private Transform ResolveSpawnTransform(string requestedHandId, Transform requestOrigin)
        {
            string handId = (requestedHandId ?? string.Empty).Trim();
            if (handId.Equals("left", System.StringComparison.OrdinalIgnoreCase) ||
                handId.Equals("left_hand", System.StringComparison.OrdinalIgnoreCase))
            {
                return leftHandSpawnTransform != null ? leftHandSpawnTransform : requestOrigin ?? fallbackSpawnTransform;
            }

            if (handId.Equals("right", System.StringComparison.OrdinalIgnoreCase) ||
                handId.Equals("right_hand", System.StringComparison.OrdinalIgnoreCase))
            {
                return rightHandSpawnTransform != null ? rightHandSpawnTransform : requestOrigin ?? fallbackSpawnTransform;
            }

            return requestOrigin != null ? requestOrigin : fallbackSpawnTransform;
        }

        private static WorldItemView EnsureWorldItemView(GameObject spawned)
        {
            WorldItemView view = spawned.GetComponent<WorldItemView>();
            if (view == null)
            {
                view = spawned.AddComponent<WorldItemView>();
            }

            if (spawned.GetComponent<WorldItemIdentity>() == null)
            {
                spawned.AddComponent<WorldItemIdentity>();
            }

            return view;
        }
    }
}
