// Does Physics.IgnoreCollision survive a collider being disabled, or its object deactivated?
// PathMaker relied on "no" (the U-turn parts). Batch mode:
//   Unity.exe -batchmode -quit -projectPath <project> -executeMethod IgnoreCollisionCheck.Run -logFile <log>
using UnityEngine;

public static class IgnoreCollisionCheck
{
    public static void Run()
    {
        GameObject a = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Collider ca = a.GetComponent<Collider>(), cb = b.GetComponent<Collider>();
        Physics.IgnoreCollision(ca, cb, true);
        Debug.Log("[ICC] after IgnoreCollision: " + Physics.GetIgnoreCollision(ca, cb));
        cb.enabled = false;
        cb.enabled = true;
        Debug.Log("[ICC] after disabling and enabling the collider: " + Physics.GetIgnoreCollision(ca, cb));
        Physics.IgnoreCollision(ca, cb, true);
        b.SetActive(false);
        b.SetActive(true);
        Debug.Log("[ICC] after deactivating and activating its object: " + Physics.GetIgnoreCollision(ca, cb));
        Object.DestroyImmediate(a);
        Object.DestroyImmediate(b);
    }
}
