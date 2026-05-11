using UnityEngine;

public class PlayerFootsteps : MonoBehaviour
{
    private MovementInput movementInput;
    private AudioSource audioSource;
    
    [SerializeField] private AudioClip footstepClip;
    [SerializeField] private float walkStepDistance = 1.2f;
    [SerializeField] private float runStepDistance = 2.5f;
    
    private float distanceTraveled = 0f;
    private Vector3 lastPosition;

    void Start()
    {
        movementInput = GetComponent<MovementInput>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        
        lastPosition = transform.position;
    }

    void Update()
    {
        float speed = movementInput != null ? movementInput.Speed : 0f;
        
        if (speed > 0.1f)
        {
            float distanceThisFrame = Vector3.Distance(transform.position, lastPosition);
            distanceTraveled += distanceThisFrame;
            
            float currentThreshold = (speed > 0.8f) ? runStepDistance : walkStepDistance;

            if (distanceTraveled >= currentThreshold)
            {
                audioSource.PlayOneShot(footstepClip, 0.4f);
                distanceTraveled = 0f;
            }
        }
        else
        {
            distanceTraveled = 0f;
        }
        
        lastPosition = transform.position;
    }
}
