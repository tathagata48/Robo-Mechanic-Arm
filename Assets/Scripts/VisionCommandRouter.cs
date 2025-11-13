using UnityEngine;

/// <summary>
/// Routes simple string commands received from the python vision system to the in-scene robot controller.
/// </summary>
public class VisionCommandRouter : MonoBehaviour
{
    [SerializeField] private RobotPickAndPlaceController robotController;

    private void Awake()
    {
        if (robotController == null)
        {
            robotController = FindObjectOfType<RobotPickAndPlaceController>();
        }
    }

    /// <summary>
    /// Handle a command originating from python. Commands are case insensitive.
    /// </summary>
    public void HandleCommand(string command)
    {
        if (string.IsNullOrEmpty(command) || robotController == null)
        {
            return;
        }

        string trimmed = command.Trim().ToLowerInvariant();
        switch (trimmed)
        {
            case "movered":
            case "pickup":
                robotController.QueuePickupOfNearestRedCube();
                break;
            case "idle":
                robotController.ClearQueuedActions();
                break;
            default:
                Debug.Log($"VisionCommandRouter received unknown command: {command}");
                break;
        }
    }
}
