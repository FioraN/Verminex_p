using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class PerkZoneTrigger : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Scene PerkSelectionUI controller.")]
    public PerkSelectionUI selectionUI;

    [Tooltip("World canvas used by this perk zone.")]
    public Canvas targetCanvas;

    [Header("Settings")]
    [Tooltip("Only colliders with this player tag can trigger the zone.")]
    public string playerTag = "Player";

    [Tooltip("Disable repeated triggers after the first activation.")]
    public bool oneTimeOnly = true;

    [Tooltip("Hide the zone renderer once it has been used.")]
    public bool hideOnUsed = false;

    private bool _used;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"[PerkZoneTrigger] '{name}' collider was not marked Is Trigger. Fixed automatically.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (selectionUI == null) return;

        bool isPlayer = string.IsNullOrWhiteSpace(playerTag)
            || other.CompareTag(playerTag)
            || other.transform.root.CompareTag(playerTag);

        if (!isPlayer) return;

        _used = true;

        if (targetCanvas != null)
            selectionUI.SetHostCanvas(targetCanvas);

        selectionUI.Open();

        if (hideOnUsed)
        {
            var renderer = GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (selectionUI == null) return;

        bool isPlayer = string.IsNullOrWhiteSpace(playerTag)
            || other.CompareTag(playerTag)
            || other.transform.root.CompareTag(playerTag);

        if (!isPlayer) return;

        selectionUI.Close();
    }

    public void Reset()
    {
        _used = false;

        if (hideOnUsed)
        {
            var renderer = GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = true;
        }
    }
}
