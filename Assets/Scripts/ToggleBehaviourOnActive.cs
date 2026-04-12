using UnityEngine;

public class ToggleBehaviourOnActive : MonoBehaviour
{
    [SerializeField] private MonoBehaviour targetBehaviour;

    private void OnEnable()
    {
        if (targetBehaviour != null)
            targetBehaviour.enabled = false;
    }

    private void OnDisable()
    {
        if (targetBehaviour != null)
            targetBehaviour.enabled = true;
    }
}