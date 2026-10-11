using UnityEngine;

// Startar spelet automatiskt även om scenen saknar Game-objektet.
public static class Boot
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (Game.I == null) new GameObject("Ballongfallet").AddComponent<Game>();
    }
}
