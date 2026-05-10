using UnityEngine;
using UnityEngine.UI;
using SkyBrawl.Player;

namespace SkyBrawl.UI
{
    /// <summary>
    /// Drives a UI Image (Filled type) to display the player plane's boost fuel.
    /// The bar empties leftward — as fuel depletes, the visible fill shrinks toward
    /// the right edge ("loading bar in reverse" — origin anchored on the right).
    /// Falls back to FindFirstObjectByType when no plane is assigned.
    /// </summary>
    public class BoostFuelHUD : MonoBehaviour
    {
        [Tooltip("The plane whose boost fuel is shown. Leave empty to auto-find at runtime.")]
        [SerializeField] private PlaneController plane;
        [Tooltip("UI Image (image type = Filled) whose fillAmount we drive 0..1.")]
        [SerializeField] private Image fillBar;
        [SerializeField] private Color fullColor  = new Color(0.30f, 0.85f, 0.55f, 1f);
        [SerializeField] private Color emptyColor = new Color(0.85f, 0.30f, 0.20f, 1f);
        [SerializeField] private float smoothing = 10f;

        private float _displayedFill = 1f;
        private float _retryTimer;

        private void Awake()
        {
            EnsureFillBarConfigured();
        }

        /// <summary>
        /// Make sure the fillBar has everything fillAmount needs in order to actually clip
        /// visually: a sprite (without one, the Image just renders a solid colored quad
        /// regardless of fillAmount), Filled type, Horizontal fill, and Right origin so
        /// the bar empties leftward.
        /// </summary>
        private void EnsureFillBarConfigured()
        {
            if (fillBar == null) return;

            if (fillBar.sprite == null)
            {
                // A 1-pixel white sprite is enough to give fillAmount something to clip.
                var tex = Texture2D.whiteTexture;
                fillBar.sprite = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit: 100f,
                    extrude: 0,
                    meshType: SpriteMeshType.FullRect);
            }

            fillBar.type = Image.Type.Filled;
            fillBar.fillMethod = Image.FillMethod.Horizontal;
            fillBar.fillOrigin = (int)Image.OriginHorizontal.Right; // empty leftwards
        }

        private void Update()
        {
            if (fillBar == null) return;

            if (plane == null)
            {
                _retryTimer -= Time.deltaTime;
                if (_retryTimer <= 0f)
                {
                    plane = FindFirstObjectByType<PlaneController>();
                    _retryTimer = 0.5f;
                }
                if (plane == null) return;
            }

            float target = plane.BoostFuelRatio01;
            _displayedFill = Mathf.Lerp(_displayedFill, target, smoothing * Time.deltaTime);
            fillBar.fillAmount = _displayedFill;
            fillBar.color = Color.Lerp(emptyColor, fullColor, _displayedFill);
        }
    }
}
