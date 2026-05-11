using UnityEngine;

public class Teleporter : MonoBehaviour
{
    [Header("Teleport Target")]
    public Transform destination;

    [Header("Settings")]
    public LayerMask targetLayer;

    private void OnTriggerEnter(Collider other)
    {
        // Check if the entering object is in the target layer
        if (((1 << other.gameObject.layer) & targetLayer) != 0)
        {
            if (destination != null)
            {
                // Disable character controller if it exists to prevent physics issues during teleport
                CharacterController cc = other.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                other.transform.position = destination.position;
                other.transform.rotation = destination.rotation;

                if (cc != null) cc.enabled = true;

                Debug.Log($"Teleported {other.name} to {destination.name}");
            }
            else
            {
                Debug.LogWarning("Teleport destination is not set!");
            }
        }
    }
}
