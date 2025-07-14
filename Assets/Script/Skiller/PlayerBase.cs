using UnityEngine;

public class PlayerBase : MonoBehaviour
{
    private bool isProtected = false;

    public void SetSabotageProtection(bool state)
    {
        isProtected = state;
    }

    public bool IsProtected() => isProtected;
}