using FishNet.Object;
using UnityEngine;

namespace Game.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerCore : NetworkBehaviour
    {
        public int PlayerID { get; private set; }
        public string PlayerName { get; private set; }

        protected CharacterController characterController;

        // Dýþ sýnýflarýn eriþmesi için public sadece okunabilir property
        public CharacterController CharacterController => characterController;

        protected virtual void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (base.IsOwner)
            {
                string nameFromPrefs = PlayerPrefs.GetString("PlayerName", "Player");
                int idFromPrefs = PlayerPrefs.GetInt("PlayerID", 0);
                SetPlayerInfoServerRpc(nameFromPrefs, idFromPrefs);
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log($"[SERVER] Player {Owner.ClientId} assigned PlayerID: {PlayerID}");
        }

        [ServerRpc]
        private void SetPlayerInfoServerRpc(string name, int id)
        {
            SetPlayerInfoObserversRpc(name, id);
        }

        [ObserversRpc]
        private void SetPlayerInfoObserversRpc(string name, int id)
        {
            PlayerName = name;
            PlayerID = id;
        }
    }
}
