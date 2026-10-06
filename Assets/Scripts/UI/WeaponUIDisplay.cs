using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

/// <summary>
/// Weapon UI display supporting both 2D HUD and 3D floating icons above kart.
/// Shows current weapon icon, ammo count, and weapon pickup animations.
/// </summary>
public class WeaponUIDisplay : MonoBehaviour
{
    [Header("Display Mode")]
    [SerializeField] private DisplayMode mode = DisplayMode.TwoDimensionalHUD;
    [SerializeField] private bool autoFindLocalKart = true;

    [Header("2D HUD Elements")]
    [SerializeField] private Image weaponIcon;
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.3f);

    [Header("Weapon Sprites (2D)")]
    [SerializeField] private Sprite boomerangIcon;
    [SerializeField] private Sprite mineIcon;
    [SerializeField] private Sprite emptyIcon;

    [Header("3D Floating Elements")]
    [SerializeField] private Transform floatingContainer;
    [SerializeField] private Vector3 worldSpaceOffset = new Vector3(0, 2f, 0);
    [SerializeField] private GameObject boomerangPreviewPrefab;
    [SerializeField] private GameObject minePreviewPrefab;
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private float floatAmount = 0.2f;

    [Header("Animation Settings")]
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField] private float pulseAmount = 0.1f;
    [SerializeField] private AudioClip weaponSwitchSound;

    public enum DisplayMode
    {
        TwoDimensionalHUD,
        ThreeDimensionalFloating,
        Both
    }

    private MirrorKartController localKart;
    private GameObject current3DPreview;
    private MirrorKartController.WeaponType lastWeapon = MirrorKartController.WeaponType.None;
    private int lastAmmo = 0;
    private AudioSource audioSource;
    private Vector3 initialFloatingPos;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (floatingContainer != null)
        {
            initialFloatingPos = floatingContainer.localPosition;
        }

        UpdateDisplay(MirrorKartController.WeaponType.None, 0);
    }

    private void Start()
    {
        if (autoFindLocalKart)
        {
            FindLocalKart();
        }
    }

    private void Update()
    {
        if (localKart == null && autoFindLocalKart)
        {
            FindLocalKart();
            return;
        }

        if (localKart == null) return;

        // Check for weapon or ammo changes
        var currentWeapon = localKart.CurrentWeapon;
        int currentAmmo = GetCurrentAmmo();

        if (currentWeapon != lastWeapon || currentAmmo != lastAmmo)
        {
            UpdateDisplay(currentWeapon, currentAmmo);
            lastWeapon = currentWeapon;
            lastAmmo = currentAmmo;
        }

        // Animate 3D preview
        if (mode == DisplayMode.ThreeDimensionalFloating || mode == DisplayMode.Both)
        {
            AnimateFloatingDisplay();
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

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            localKart = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<MirrorKartController>();
            if (localKart != null)
            {
                BindKartEvents();
            }
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
        if (mode == DisplayMode.TwoDimensionalHUD || mode == DisplayMode.Both)
        {
            Update2DDisplay(weapon, ammo);
        }

        if (mode == DisplayMode.ThreeDimensionalFloating || mode == DisplayMode.Both)
        {
            Update3DDisplay(weapon, ammo);
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
            ammoText.text = ammo > 0 ? ammo.ToString() : "";
            ammoText.gameObject.SetActive(ammo > 0);
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = weapon != MirrorKartController.WeaponType.None 
                ? activeColor 
                : emptyColor;
        }
    }

    private void Update3DDisplay(MirrorKartController.WeaponType weapon, int ammo)
    {
        if (floatingContainer == null) return;

        // Destroy current preview
        if (current3DPreview != null)
        {
            Destroy(current3DPreview);
            current3DPreview = null;
        }

        // Spawn new preview if we have ammo
        if (ammo > 0)
        {
            GameObject prefabToSpawn = weapon switch
            {
                MirrorKartController.WeaponType.Boomerang => boomerangPreviewPrefab,
                MirrorKartController.WeaponType.Mine => minePreviewPrefab,
                _ => null
            };

            if (prefabToSpawn != null)
            {
                current3DPreview = Instantiate(prefabToSpawn, floatingContainer);
                current3DPreview.transform.localPosition = Vector3.zero;
                current3DPreview.transform.localRotation = Quaternion.identity;

                // Disable colliders on preview
                foreach (var col in current3DPreview.GetComponentsInChildren<Collider>())
                {
                    col.enabled = false;
                }
            }
        }
    }

    private void AnimateFloatingDisplay()
    {
        if (floatingContainer == null || current3DPreview == null) return;

        // Rotate
        floatingContainer.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

        // Bob up and down
        float newY = initialFloatingPos.y + Mathf.Sin(Time.time * floatSpeed) * floatAmount;
        floatingContainer.localPosition = new Vector3(
            initialFloatingPos.x,
            newY,
            initialFloatingPos.z
        );
    }

    public void SetLocalKart(MirrorKartController kart)
    {
        localKart = kart;
        BindKartEvents();
    }

    private void BindKartEvents()
    {
        if (localKart == null) return;

        localKart.OnWeaponChanged += (weapon) =>
        {
            if (weaponSwitchSound != null && audioSource != null && weapon != MirrorKartController.WeaponType.None)
            {
                audioSource.PlayOneShot(weaponSwitchSound);
            }
        };
    }
}
