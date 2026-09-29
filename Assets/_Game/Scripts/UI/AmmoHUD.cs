using TMPro;
using UnityEngine;

public sealed class AmmoHUD : MonoBehaviour
{
    [SerializeField] private PlayerWeapon playerWeapon;
    [SerializeField] private TMP_Text ammoText;
    [SerializeField] private TMP_Text reloadText;

    private void Awake()
    {
        if (playerWeapon == null)
        {
            playerWeapon = FindFirstObjectByType<PlayerWeapon>();
        }

        ValidateAuthoredView();
    }

    private void OnEnable()
    {
        if (playerWeapon != null)
        {
            playerWeapon.AmmoChanged += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (playerWeapon != null)
        {
            playerWeapon.AmmoChanged -= Refresh;
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Refresh()
    {
        if (playerWeapon == null)
        {
            if (ammoText != null)
            {
                ammoText.text = "-- / --";
            }

            if (reloadText != null)
            {
                reloadText.gameObject.SetActive(false);
            }

            return;
        }

        if (ammoText != null)
        {
            ammoText.text = $"{playerWeapon.CurrentAmmo} / {playerWeapon.ReserveAmmo}";
        }

        if (reloadText != null)
        {
            reloadText.gameObject.SetActive(playerWeapon.IsReloading);
        }
    }

    private void ValidateAuthoredView()
    {
        if (ammoText == null || reloadText == null)
        {
            Debug.LogWarning("AmmoHUD requires authored ammoText and reloadText references.", this);
        }
    }
}
