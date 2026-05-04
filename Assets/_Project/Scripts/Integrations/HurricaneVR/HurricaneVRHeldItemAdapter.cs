using System;
using System.Reflection;
using UnityEngine;
using VRGame.Runtime;

namespace VRGame.Integration.HurricaneVR
{
    public sealed class HurricaneVRHeldItemAdapter : MonoBehaviour, IVRHandItemSpawner
    {
        private const string HvrGrabbableTypeName = "HurricaneVR.Framework.Core.HVRGrabbable, HurricaneVR.Framework";

        [Header("Hurricane Hand Grabbers")]
        [Tooltip("Assign an HVRHandGrabber or compatible HVRGrabberBase for the left hand.")]
        [SerializeField]
        private MonoBehaviour leftHandGrabber = null;

        [Tooltip("Assign an HVRHandGrabber or compatible HVRGrabberBase for the right hand.")]
        [SerializeField]
        private MonoBehaviour rightHandGrabber = null;

        [Header("Spawn Fallbacks")]
        [SerializeField]
        private Transform leftHandSpawnTransform = null;

        [SerializeField]
        private Transform rightHandSpawnTransform = null;

        [SerializeField]
        private Transform fallbackSpawnTransform = null;

        [SerializeField]
        private bool addHvrGrabbableIfMissing = false;

        [SerializeField]
        private bool parentToHandWhenGrabFails = false;

        [SerializeField]
        private bool verboseLogging = false;

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

            GameObject spawned = Instantiate(request.WorldPrefab, spawnPosition, spawnRotation, request.OptionalParent);
            worldItemView = EnsureWorldItemView(spawned, request.Binding);

            HurricaneWorldItemAdapter worldAdapter = spawned.GetComponent<HurricaneWorldItemAdapter>();
            if (worldAdapter == null)
            {
                worldAdapter = spawned.AddComponent<HurricaneWorldItemAdapter>();
            }

            worldAdapter.Attach(worldItemView);
            worldAdapter.TryEnsureHvrGrabbable(addHvrGrabbableIfMissing, out Component hvrGrabbable, out string hvrMessage);

            MonoBehaviour handGrabber = ResolveHandGrabber(request.RequestedHandId);
            bool grabbed = TryGrabWithHurricane(handGrabber, hvrGrabbable, out string grabMessage);
            if (!grabbed && parentToHandWhenGrabFails && spawnTransform != null)
            {
                spawned.transform.SetParent(spawnTransform, true);
            }

            message = grabbed
                ? $"Spawned item and transferred to Hurricane grabber. {hvrMessage}"
                : $"Spawned item without Hurricane auto-grab. {hvrMessage} {grabMessage}";

            Log(message);
            return true;
        }

        private MonoBehaviour ResolveHandGrabber(string requestedHandId)
        {
            string handId = (requestedHandId ?? string.Empty).Trim();
            if (handId.Equals("left", StringComparison.OrdinalIgnoreCase) ||
                handId.Equals("left_hand", StringComparison.OrdinalIgnoreCase))
            {
                return leftHandGrabber;
            }

            if (handId.Equals("right", StringComparison.OrdinalIgnoreCase) ||
                handId.Equals("right_hand", StringComparison.OrdinalIgnoreCase))
            {
                return rightHandGrabber;
            }

            return rightHandGrabber != null ? rightHandGrabber : leftHandGrabber;
        }

        private Transform ResolveSpawnTransform(string requestedHandId, Transform requestOrigin)
        {
            string handId = (requestedHandId ?? string.Empty).Trim();
            if (handId.Equals("left", StringComparison.OrdinalIgnoreCase) ||
                handId.Equals("left_hand", StringComparison.OrdinalIgnoreCase))
            {
                return leftHandSpawnTransform != null ? leftHandSpawnTransform : requestOrigin ?? fallbackSpawnTransform;
            }

            if (handId.Equals("right", StringComparison.OrdinalIgnoreCase) ||
                handId.Equals("right_hand", StringComparison.OrdinalIgnoreCase))
            {
                return rightHandSpawnTransform != null ? rightHandSpawnTransform : requestOrigin ?? fallbackSpawnTransform;
            }

            return requestOrigin != null ? requestOrigin : fallbackSpawnTransform;
        }

        private bool TryGrabWithHurricane(MonoBehaviour handGrabber, Component hvrGrabbable, out string message)
        {
            if (handGrabber == null)
            {
                message = "No Hurricane hand grabber assigned for requested hand.";
                return false;
            }

            if (hvrGrabbable == null)
            {
                message = "Spawned item has no HVRGrabbable.";
                return false;
            }

            Type grabbableType = hvrGrabbable.GetType();
            Type handType = handGrabber.GetType();
            MethodInfo tryGrabWithForce = handType.GetMethod("TryGrab", new[] { grabbableType, typeof(bool) });
            if (tryGrabWithForce != null)
            {
                object result = tryGrabWithForce.Invoke(handGrabber, new object[] { hvrGrabbable, true });
                bool success = result is bool boolResult && boolResult;
                message = success ? "Hurricane TryGrab(grabbable, true) succeeded." : "Hurricane TryGrab(grabbable, true) returned false.";
                return success;
            }

            MethodInfo tryGrab = handType.GetMethod("TryGrab", new[] { grabbableType });
            if (tryGrab != null)
            {
                object result = tryGrab.Invoke(handGrabber, new object[] { hvrGrabbable });
                bool success = result is bool boolResult && boolResult;
                message = success ? "Hurricane TryGrab(grabbable) succeeded." : "Hurricane TryGrab(grabbable) returned false.";
                return success;
            }

            Type hvrType = Type.GetType(HvrGrabbableTypeName);
            if (hvrType != null && hvrType != grabbableType)
            {
                MethodInfo inheritedTryGrab = handType.GetMethod("TryGrab", new[] { hvrType, typeof(bool) });
                if (inheritedTryGrab != null)
                {
                    object result = inheritedTryGrab.Invoke(handGrabber, new object[] { hvrGrabbable, true });
                    bool success = result is bool boolResult && boolResult;
                    message = success ? "Hurricane inherited TryGrab succeeded." : "Hurricane inherited TryGrab returned false.";
                    return success;
                }
            }

            message = "No compatible Hurricane TryGrab method found.";
            return false;
        }

        private static WorldItemView EnsureWorldItemView(GameObject spawned, WorldItemBinding binding)
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

            if (binding != null)
            {
                view.Bind(binding);
            }

            return view;
        }

        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log(message, this);
            }
        }
    }
}
