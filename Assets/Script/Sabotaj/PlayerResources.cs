using UnityEngine;

namespace Game.Player
{
    public class PlayerResources : MonoBehaviour
    {
        [SerializeField] private int startingResources = 100;
        private int currentResources;

        private void Awake()
        {
            currentResources = startingResources;
        }

        public bool HasEnoughResources(int amount) => currentResources >= amount;

        public bool SpendResources(int amount)
        {
            if (!HasEnoughResources(amount)) return false;
            currentResources -= amount;
            return true;
        }

        public void AddResources(int amount) => currentResources += amount;

        public int GetCurrentResources() => currentResources;
    }
}
