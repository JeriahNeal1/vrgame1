using System;
using System.Reflection;
using UnityEngine;
using VRGame.Runtime;

namespace VRGame.Integration.HurricaneVR
{
    [DisallowMultipleComponent]
    public sealed class HurricaneWorldItemAdapter : MonoBehaviour
    {
        private const string HvrGrabbableTypeName = "HurricaneVR.Framework.Core.HVRGrabbable, HurricaneVR.Framework";

        [SerializeField]
        private WorldItemView worldItemView;

        [SerializeField]
        private Component hvrGrabbable;

        [SerializeField]
        private bool verboseLogging = false;

        public Component HvrGrabbable
        {
            get { return hvrGrabbable; }
        }

        public bool HasHurricaneGrabbable
        {
            get { return hvrGrabbable != null || TryFindHvrGrabbable(out _); }
        }

        public void Attach(WorldItemView view)
        {
            worldItemView = view != null ? view : GetComponent<WorldItemView>();
            TryFindHvrGrabbable(out hvrGrabbable);
        }

        public bool TryFindHvrGrabbable(out Component grabbable)
        {
            Type hvrType = Type.GetType(HvrGrabbableTypeName);
            if (hvrType != null)
            {
                grabbable = GetComponentInChildren(hvrType, true) as Component;
                if (grabbable != null)
                {
                    hvrGrabbable = grabbable;
                    return true;
                }
            }

            Component[] components = GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component != null && component.GetType().Name == "HVRGrabbable")
                {
                    hvrGrabbable = component;
                    grabbable = component;
                    return true;
                }
            }

            grabbable = null;
            return false;
        }

        public bool TryEnsureHvrGrabbable(bool addIfMissing, out Component grabbable, out string message)
        {
            if (TryFindHvrGrabbable(out grabbable))
            {
                message = "HVRGrabbable found.";
                return true;
            }

            Type hvrType = Type.GetType(HvrGrabbableTypeName);
            if (hvrType == null)
            {
                message = "Hurricane VR HVRGrabbable type is unavailable.";
                return false;
            }

            if (!addIfMissing)
            {
                message = "HVRGrabbable is missing and auto-add is disabled.";
                return false;
            }

            grabbable = gameObject.AddComponent(hvrType) as Component;
            hvrGrabbable = grabbable;
            message = grabbable != null ? "HVRGrabbable added." : "Failed to add HVRGrabbable.";
            return grabbable != null;
        }

        public void NotifyGrabbedFromHurricane()
        {
            ResolveView();
            worldItemView?.NotifyGrabbed();
            Log("Hurricane grab event forwarded.");
        }

        public void NotifyReleasedFromHurricane()
        {
            ResolveView();
            worldItemView?.NotifyReleased();
            Log("Hurricane release event forwarded.");
        }

        private void Awake()
        {
            ResolveView();
            TryFindHvrGrabbable(out hvrGrabbable);
        }

        private void OnValidate()
        {
            ResolveView();
        }

        private void ResolveView()
        {
            if (worldItemView == null)
            {
                worldItemView = GetComponent<WorldItemView>();
            }
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
