using UnityEngine;

namespace Game.Player
{
    public class PlayerUI : MonoBehaviour
    {
        [SerializeField] private GameObject winUI;

        public void ShowWinUI()
        {
            if (winUI != null)
            {
                winUI.SetActive(true);
            }
        }
    }
}