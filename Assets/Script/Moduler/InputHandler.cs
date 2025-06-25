using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    public class InputHandler : MonoBehaviour
    {
        private PlayerMovement movement;
        private PlayerLook look;
        private PlayerItemSystem itemSystem;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            look = GetComponent<PlayerLook>();
            itemSystem = GetComponent<PlayerItemSystem>();
        }

        public void OnMove(InputAction.CallbackContext context) => movement.OnMove(context);
        public void OnLook(InputAction.CallbackContext context) => look.OnLook(context);
        public void OnJump(InputAction.CallbackContext context) => movement.OnJump(context);
        public void OnInteract(InputAction.CallbackContext context) => itemSystem.OnInteract(context);
        public void OnSprint(InputAction.CallbackContext context) => movement.OnSprint(context);
    }
}