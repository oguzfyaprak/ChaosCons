using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace Game.Stats
{
    public class PlayerStats : NetworkBehaviour

    {
        public static PlayerStats LocalInstance;

        // SyncVar<T> kullanýyoruz ve hepsi readonly
        public readonly SyncVar<int> Score = new();
        public readonly SyncVar<int> Kills = new();
        public readonly SyncVar<int> Deaths = new();
        public readonly SyncVar<int> Sabotages = new();
        public readonly SyncVar<int> Repairs = new();

        [HideInInspector]
        public string PlayerName; // Steam veya baþka sistemle set edilecek

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (IsOwner)
                LocalInstance = this;
        }

        #region Server-side Functions

        [Server]
        public void AddScore(int amount)
        {
            Score.Value += amount;
        }

        [Server]
        public void AddKill()
        {
            Kills.Value++;
            AddScore(15);
        }

        [Server]
        public void AddDeath()
        {
            Deaths.Value++;
        }

        [Server]
        public void AddSabotage()
        {
            Sabotages.Value++;
            AddScore(12);
        }

        [Server]
        public void AddRepair()
        {
            Repairs.Value++;
            AddScore(10);
        }

        [Server]
        public void AddMaterialDelivery()
        {
            AddScore(5);
        }

        [Server]
        public void AddFloorBuild()
        {
            AddScore(20);
        }

        #endregion
    }
}