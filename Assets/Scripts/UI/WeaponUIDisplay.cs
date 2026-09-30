using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

/// <summary>
/// UI display for current weapon (2D icon or 3D preview), optional world-space follow.
/// </summary>
public class WeaponUIDisplay : MonoBehaviour
{
    [Header("UI Mode")]
    [SerializeField] private DisplayMode displayMode = DisplayMode.Icon2D;
    [SerializeField] private bool followLocalPlayer = true;
    [SerializeField] private Vector3 worldSpaceOffset = new Vector3(0, 2f, 0);

    [Header("2D Icon References")]
    [SerializeField] private Image weaponIcon;
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private Image backgroundImage;

    [Header("2D Icons")]
    [SerializeField] private Sprite boomerangIcon;
    [SerializeField] private Sprite mineIcon;
    [SerializeField] private Sprite emptyIcon;

    [Header("3D Preview")]
    [SerializeField] private Transform previewContainer;
    [SerializeField] private GameObject boomerangPreviewPrefab;
    [SerializeField] private GameObject minePreviewPrefab;
    [SerializeField] private float previewRotationSpeed = 45f;

    [Header("Colors")]
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color emptyColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    [Header("Animation")]
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseAmount = 0.1f;

    public enum DisplayMode
    {
        Icon2D,
        Preview3D
    }

    private MirrorKartController localKart;
    private GameObject current3DPreview;
    private MirrorKartController.WeaponType lastWeapon = MirrorKartController.WeaponType.None;
    private int lastAmmo = 0;
    private Canvas canvas;
    private RectTransform rectTransform;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        rectTransform = GetComponent<RectTransform>();
    }

    private void Start()
    {
        UpdateDisplay(MirrorKartController.WeaponType.None, 0);
    }

    private void Update()
    {
        FindLocalKart();

        if (localKart != null)
        {
            var weapon = localKart.CurrentWeapon;
            int ammo = GetCurrentAmmo();

            if (weapon != lastWeapon || ammo != lastAmmo)
            {
                lastWeapon = weapon;
                lastAmmo = ammo;
                UpdateDisplay(weapon, ammo);
            }
        }

        // Rotate 3D preview
        if (current3DPreview != null && displayMode == DisplayMode.Preview3D)
        {
            current3DPreview.transform.Rotate(Vector3.up, previewRotationSpeed * Time.deltaTime);
        }

        // World space follow
        if (followLocalPlayer && localKart != null && canvas != null && canvas.renderMode == RenderMode.WorldSpace)
        {
            transform.position = localKart.transform.position + worldSpaceOffset;
            transform.LookAt(Camera.main.transform);
            transform.Rotate(0, 180, 0);
        }

        // Pulse animation when weapon is available
        if (weaponIcon != null && lastWeapon != MirrorKartController.WeaponType.None)
        {
            float scale = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            weaponIcon.transform.localScale = Vector3.one * scale;
        }
    }

    private void FindLocalKart()
    {
        if (localKart != null) return;

        if (NetworkClient.localPlayer != null)
        {
            localKart = NetworkClient.localPlayer.GetComponent<MirrorKartController>();
        }
    }

    private int GetCurrentAmmo()
    {
        if (localKart == null) return 0;

        return localKart.CurrentWeapon switch
        {
            MirrorKartController.WeaponType.Boomerang => localKart.BoomerangAmmo,
            MirrorKartController.WeaponType.Mine => localKart.MineAmmo,
            _ => 0
        };
    }

    private void UpdateDisplay(MirrorKartController.WeaponType weapon, int ammo)
    {
        switch (displayMode)
        {
            case DisplayMode.Icon2D:
                Update2DDisplay(weapon, ammo);
                break;
            case DisplayMode.Preview3D:
                Update3DDisplay(weapon, ammo);
                break;
        }
    }

    private void Update2DDisplay(MirrorKartController.WeaponType weapon, int ammo)
    {
        if (weaponIcon != null)
        {
            weaponIcon.sprite = weapon switch
            {
                MirrorKartController.WeaponType.Boomerang => boomerangIcon,
                MirrorKartController.WeaponType.Mine => mineIcon,
                _ => emptyIcon
            };

            weaponIcon.color = weapon != MirrorKartController.WeaponType.None ? activeColor : emptyColor;
        }

        if (ammoText != null)
        {
            ammoText.text = ammo > 0 ? $"x{ammo}" : "";
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = weapon != MirrorKartController.WeaponType.None 
                ? new Color(0, 0, 0, 0.7f) 
                : new Color(0, 0, 0, 0.3f);
        }
    }

    private void Update3DDisplay(MirrorKartController.WeaponType weapon, int ammo)
    {
        // Clear previous preview
        if (current3DPreview != null)
        {
            Destroy(current3DPreview);
            current3DPreview = null;
        }

        if (previewContainer == null) return;

        // Create new preview
        GameObject prefab = weapon switch
        {
            MirrorKartController.WeaponType.Boomerang => boomerangPreviewPrefab,
            MirrorKartController.WeaponType.Mine => minePreviewPrefab,
            _ => null
        };

        if (prefab != null)
        {
            current3DPreview = Instantiate(prefab, previewContainer);
            current3DPreview.transform.localPosition = Vector3.zero;
            current3DPreview.transform.localScale = Vector3.one;

            // Disable colliders and scripts for preview
            foreach (var col in current3DPreview.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }
            foreach (var script in current3DPreview.GetComponentsInChildren<NetworkBehaviour>())
            {
                Destroy(script);
            }
        }

        // Update ammo text
        if (ammoText != null)
        {
            ammoText.text = ammo > 0 ? $"x{ammo}" : "";
        }
    }

    /// <summary>
    /// Set display mode at runtime
    /// </summary>
    public void SetDisplayMode(DisplayMode mode)
    {
        displayMode = mode;
        UpdateDisplay(lastWeapon, lastAmmo);
    }
}
