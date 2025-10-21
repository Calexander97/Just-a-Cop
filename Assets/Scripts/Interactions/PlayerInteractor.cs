using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private Camera viewCam;
    [SerializeField] private float interactRange = 3f;
    [SerializeField] private LayerMask interactMask = ~0;

    private IInteractable _hover;

    void Update()
    {
        Ray r = new Ray(viewCam.transform.position, viewCam.transform.forward);
        if (Physics.Raycast(r, out var hit, interactRange, interactMask, QueryTriggerInteraction.Ignore))
        {
            _hover = hit.collider.GetComponentInParent<IInteractable>();
        }
        else _hover = null;

        if (_hover != null && _hover.CanInteract(gameObject))
        {
            InteractPromptUI.Show(_hover.GetPrompt());
            if (Input.GetKeyDown(KeyCode.E))
                _hover.Interact(gameObject);
        }
        else
        {
            InteractPromptUI.Hide();
        }
    }
}
