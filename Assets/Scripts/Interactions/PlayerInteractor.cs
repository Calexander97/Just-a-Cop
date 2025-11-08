using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private Camera viewCam;
    [SerializeField] private float interactRange = 3f;
    [SerializeField] private LayerMask interactMask = ~0;

    private IInteractable hover;

    void Update()
    {
        Ray r = new Ray(viewCam.transform.position, viewCam.transform.forward);
        if (Physics.Raycast(r, out var hit, interactRange, interactMask, QueryTriggerInteraction.Ignore))
            hover = hit.collider.GetComponentInParent<IInteractable>();
        else 
            hover = null;

        if (hover != null && hover.CanInteract(gameObject))
        {
            InteractPromptUI.Show(hover.GetPrompt());
            if (Input.GetKeyDown(KeyCode.E)) hover.Interact(gameObject);
        }
        else InteractPromptUI.Hide();
    }
}
