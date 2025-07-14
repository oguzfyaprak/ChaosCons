using UnityEngine;

public class PlayerVisual : MonoBehaviour
{
    public void SetVisible(bool state)
    {
        foreach (var renderer in GetComponentsInChildren<Renderer>())
            renderer.enabled = state;
    }
}