using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Attached to the child PassengerCabinTrigger GameObject to forward OnTrigger callbacks
    /// reliably to the parent FreightElevatorController.
    /// </summary>
    public class ElevatorCabinTrigger : MonoBehaviour
    {
        public FreightElevatorController controller;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<FreightElevatorController>();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (controller != null)
            {
                controller.NotifyTriggerEnter(other);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (controller != null)
            {
                controller.NotifyTriggerExit(other);
            }
        }
    }
}
