// Records the main camera to numbered PNGs (lossless), one per frame, with the game's own sound
// written down on the clip's clock (SoundLog; mixed afterwards by Tools/Social/mix_audio.py) - no
// music is ever added - for trailers and social clips (StoreCapture -shotVideo). Frames are rendered at the end of each
// frame (after the camera has followed the ball), on a fixed time step (Time.captureDeltaTime), so
// the clip plays at exactly that rate however slowly the Editor renders it.
//
// Output: <folder>/<name>/f_00000.png ..., <folder>/<name>/sound.jsonl
//
// Deliberately excluded from release builds.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(10000)]
public class VideoRecorder : MonoBehaviour
{
    public int Frames { get; private set; }
    // Closer in than the game's camera, by narrowing its view (-videoZoom): the clip is rendered
    // at its full size there instead of being cropped and enlarged afterwards.
    public float Zoom = 1f;

    private string dir;
    private int width, height;
    private bool keepUI, recording;
    private RenderTexture rt;
    private RenderTexture shown; // rt, 8-bit sRGB, to read back
    private Texture2D tex;
    private Camera cam;
    private List<Camera> stack;
    private readonly List<CameraClearFlags> uiClears = new List<CameraClearFlags>();
    private readonly List<Color> uiBackgrounds = new List<Color>();
    private RenderTexture uiRt;
    private Texture2D uiTex;
    private int warmUp;

    public void Begin(string folder, string name, int w, int h, bool withUI)
    {
        dir = Path.Combine(folder, name);
        Directory.CreateDirectory(dir);
        foreach (string old in Directory.GetFiles(dir, "f_*.*"))
            File.Delete(old);
        width = w;
        height = h;
        keepUI = withUI;
        cam = Camera.main;
        // HDR, so bloom works (StoreCapture.Capture); read back through an sRGB copy.
        rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf) { antiAliasing = 4 };
        shown = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        if (withUI)
        {
            uiRt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            uiTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        }

        // In a run the UI is drawn by an orthographic camera stacked on the main one (drawn by the
        // main camera instead, the 3D power-up icons would be seen in perspective). Stacked, it
        // lays the canvas out for the Editor's own small screen: for the clip it is taken off the
        // stack and draws into a see-through texture of the clip's size, as a camera of its own
        // (so the canvas is laid out for that size), which is laid over the picture here. (A base
        // camera in URP clears its target whatever its clear flags say, so it cannot simply draw
        // over the main camera's picture.)
        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        stack = data.cameraStack != null ? new List<Camera>(data.cameraStack) : new List<Camera>();
        if (data.cameraStack != null)
            data.cameraStack.Clear();
        if (keepUI)
            foreach (Camera c in stack)
            {
                c.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Base;
                uiClears.Add(c.clearFlags);
                uiBackgrounds.Add(c.backgroundColor);
                c.clearFlags = CameraClearFlags.SolidColor;
                c.backgroundColor = Color.clear;
                c.targetTexture = uiRt;
            }
        warmUp = 3;
        // Never the debug frame counter (Settings > Show FPS): it showed the Editor's own slow
        // rate in the corner of a clip.
        foreach (ShowFPS counter in FindObjectsByType<ShowFPS>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            counter.SetVisible(false);

        Debug.Log("[SC] video " + dir + " " + width + "x" + height + " at " + (1f / Time.captureDeltaTime).ToString("0") + " fps");
        Frames = 0;
        recording = true;
#if UNITY_EDITOR
        SoundLog.Begin(dir);
#endif
    }

    private void LateUpdate()
    {
        if (!recording)
            return;
        // The Editor's version check thinks it is out of date, whenever its answer comes.
        GameObject update = GameObject.Find("UpdateDialog");
        if (update != null)
            update.SetActive(false);
        RenderTexture previous = cam.targetTexture;
        cam.targetTexture = rt;
        float fov = cam.fieldOfView;
        if (Zoom != 1f)
            cam.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) / Zoom) * Mathf.Rad2Deg;
        cam.Render();
        cam.fieldOfView = fov;
        if (keepUI)
            foreach (Camera c in stack)
                c.Render();
        cam.targetTexture = previous;
        if (warmUp > 0)
        {
            warmUp--; // the canvas lays itself out for the new size first
            return;
        }
        Graphics.Blit(rt, shown);
        RenderTexture.active = shown;
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        if (keepUI)
        {
            RenderTexture.active = uiRt;
            uiTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            overlay();
        }
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(dir, "f_" + Frames.ToString("00000") + ".png"), tex.EncodeToPNG());
#if UNITY_EDITOR
        SoundLog.Frame(); // what was playing at this frame...
#endif
        Frames++;
#if UNITY_EDITOR
        SoundLog.SetTime(Frames * Time.captureDeltaTime); // ...and what is played next belongs to the next
#endif
    }

    // The UI over the picture. The UI shader blends alpha the way it blends colour, so over a clear
    // texture one layer leaves colour c*a and alpha a*a: the picture shows through by 1 - sqrt.
    private void overlay()
    {
        Color32[] under = tex.GetPixels32(), over = uiTex.GetPixels32();
        for (int i = 0; i < under.Length; i++)
        {
            Color32 o = over[i];
            if (o.a == 0)
                continue;
            float keep = 1f - Mathf.Sqrt(o.a / 255f);
            Color32 u = under[i];
            under[i] = new Color32((byte)Mathf.Min(255f, o.r + u.r * keep), (byte)Mathf.Min(255f, o.g + u.g * keep),
                                   (byte)Mathf.Min(255f, o.b + u.b * keep), 255);
        }
        tex.SetPixels32(under);
    }

    public void End()
    {
        recording = false;
#if UNITY_EDITOR
        SoundLog.End();
#endif
        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        if (keepUI)
            for (int i = 0; i < stack.Count; i++)
            {
                stack[i].targetTexture = null;
                stack[i].clearFlags = uiClears[i];
                stack[i].backgroundColor = uiBackgrounds[i];
                stack[i].GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
            }
        if (data.cameraStack != null)
        {
            data.cameraStack.Clear();
            foreach (Camera c in stack) data.cameraStack.Add(c);
        }
        rt.Release();
        shown.Release();
        Destroy(tex);
        if (uiRt != null)
        {
            uiRt.Release();
            Destroy(uiTex);
        }
        Debug.Log("[SC] video done: " + Frames + " frames in " + dir);
    }
}
#endif
