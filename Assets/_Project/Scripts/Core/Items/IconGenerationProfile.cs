using UnityEngine;

namespace VRGame.Items
{
    public enum IconLightingPreset
    {
        Studio,
        FrontKey,
        ThreePoint,
        SoftTop
    }

    [CreateAssetMenu(menuName = "VRGame/Items/Icon Generation Profile", fileName = "IconGenerationProfile")]
    public sealed class IconGenerationProfile : ScriptableObject
    {
        [Header("Model")]
        [SerializeField]
        private Vector3 rotation = new Vector3(25f, -35f, 0f);

        [Min(0.001f)]
        [SerializeField]
        private float scale = 1f;

        [SerializeField]
        private Vector3 modelOffset = Vector3.zero;

        [Header("Camera")]
        [SerializeField]
        private Vector3 cameraOffset = new Vector3(0f, 0f, -6f);

        [Tooltip("Set to 0 to fit the prefab bounds automatically.")]
        [Min(0f)]
        [SerializeField]
        private float orthographicSize = 0f;

        [Header("Lighting")]
        [SerializeField]
        private IconLightingPreset lightingPreset = IconLightingPreset.Studio;

        [SerializeField]
        private Color ambientColor = new Color(0.45f, 0.45f, 0.45f, 1f);

        [Header("Output")]
        [Min(32)]
        [SerializeField]
        private int outputSize = 512;

        [SerializeField]
        private bool transparentBackground = true;

        [SerializeField]
        private Color backgroundColor = new Color(0f, 0f, 0f, 0f);

        [Min(1f)]
        [SerializeField]
        private float spritePixelsPerUnit = 100f;

        public Vector3 Rotation
        {
            get { return rotation; }
        }

        public float Scale
        {
            get { return Mathf.Max(0.001f, scale); }
        }

        public Vector3 ModelOffset
        {
            get { return modelOffset; }
        }

        public Vector3 CameraOffset
        {
            get
            {
                if (cameraOffset.sqrMagnitude < 0.0001f)
                {
                    return new Vector3(0f, 0f, -6f);
                }

                return cameraOffset;
            }
        }

        public float OrthographicSize
        {
            get { return Mathf.Max(0f, orthographicSize); }
        }

        public bool UsesAutomaticOrthographicSize
        {
            get { return OrthographicSize <= 0f; }
        }

        public IconLightingPreset LightingPreset
        {
            get { return lightingPreset; }
        }

        public Color AmbientColor
        {
            get { return ambientColor; }
        }

        public int OutputSize
        {
            get { return Mathf.Clamp(outputSize, 32, 4096); }
        }

        public bool TransparentBackground
        {
            get { return transparentBackground; }
        }

        public Color BackgroundColor
        {
            get { return transparentBackground ? new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, 0f) : backgroundColor; }
        }

        public float SpritePixelsPerUnit
        {
            get { return Mathf.Max(1f, spritePixelsPerUnit); }
        }

        private void OnValidate()
        {
            scale = Mathf.Max(0.001f, scale);
            orthographicSize = Mathf.Max(0f, orthographicSize);
            outputSize = Mathf.Clamp(outputSize, 32, 4096);
            spritePixelsPerUnit = Mathf.Max(1f, spritePixelsPerUnit);
        }
    }
}
