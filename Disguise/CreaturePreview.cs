#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Disguise;

/// <summary>
/// The 3D preview of the Disguise window: one creature at a time, filmed by a camera of its
/// own into a RenderTexture that GuiPreview draws (a RawImage in the right half of the
/// window).
///
/// The stage the creature stands on is 20 km under the world, on a layer nothing of the game
/// draws on (the highest layer without a name, 31 down to 8, else one of the three Unity
/// keeps spare). Every camera and every directional light has that layer taken away when the
/// stage is built: the cameras would draw the creature into the world, and the lights would
/// light the preview with the moment's sun - a picture that turns black at night. What lights
/// it are the two lights here, one of them hung on the camera so the side you look at is
/// always lit. The masks go back in Forget, which the window closing calls.
///
/// The creature itself is GhostFactory.BuildInert, the inert copy SelfView uses: no ZDO, no
/// AI, no collider, no sound. Its skinned meshes are told to update while off screen (one
/// outside every camera stops being deformed, so the preview would freeze on a stale pose),
/// and its animator is evaluated once and switched off: the pose on the picture is the first
/// frame of its own default state. The camera turns around the creature rather than the
/// creature turning, so the framing measured once from the bounds stays true while it spins.
///
/// The stage is built on the first preview asked for and taken down with the window.
/// </summary>
internal static class CreaturePreview
{
    /// <summary>
    /// Size of the picture, in the window's own units - which are screen pixels (the canvas
    /// under the window is 1 unit = 1 pixel), so the texture is made at exactly this size and
    /// no scaling can blur it.
    /// </summary>
    internal const float PictureWidth = 380f;

    internal const float PictureHeight = 300f;

    /// <summary>Where the stage stands: 20 km under the world, out of every camera playing the game.</summary>
    private static readonly Vector3 StageOrigin = new Vector3(0f, -20000f, 0f);

    private static readonly Vector3 KeyAngles = new Vector3(28f, 22f, 0f);

    private static readonly Vector3 FillAngles = new Vector3(20f, -135f, 0f);

    private static readonly Color PreviewAmbient = new Color(0.42f, 0.42f, 0.47f);

    private const float KeyIntensity = 1.1f;

    private const float FillIntensity = 0.4f;

    private const float VerticalFov = 35f;

    /// <summary>How high above the creature the camera looks down, in degrees.</summary>
    private const float Tilt = 14f;

    private const float TurnDegreesPerSecond = 25f;

    /// <summary>How much bigger the framed sphere is than the creature, so nothing is ever clipped.</summary>
    private const float FrameMargin = 1.2f;

    /// <summary>Never frame closer than this, so a tiny creature (a Neck, a Skeleton) stays clear of the near plane.</summary>
    private const float MinRadius = 0.25f;

    /// <summary>Spares for s_layer: not searched yet, and nothing free found (a mod named them all).</summary>
    private const int NotSearched = -2;

    private const int NoFreeLayer = -1;

    /// <summary>The layers Unity keeps spare under 8: unnamed, and nothing in the game draws on them.</summary>
    private static readonly int[] SpareLayers = { 7, 6, 3 };

    private static int s_layer = NotSearched;

    private static bool s_layerWarned;

    private static GameObject? s_root;

    private static Camera? s_camera;

    private static RenderTexture? s_texture;

    private static GameObject? s_creature;

    /// <summary>Cameras and lights missing the stage's layer, with the mask each had, given back by Forget.</summary>
    private static readonly List<KeyValuePair<Camera, int>> s_hiddenCameras = new();

    private static readonly List<KeyValuePair<Light, int>> s_hiddenLights = new();

    private static Vector3 s_center;

    private static float s_radius;

    private static float s_distance;

    private static float s_yaw;

    /// <summary>
    /// True while the texture shows what stands on the stage. Set false by every change, set
    /// true after a film: an empty stage is then filmed once (to clear a picture that is no
    /// longer true) rather than every frame of a closed preview.
    /// </summary>
    private static bool s_pictureFresh = true;

    /// <summary>The texture to show, null before the stage exists - read by GuiPreview every frame.</summary>
    internal static RenderTexture? Texture => s_texture;

    // ------------------------------------------------------------------ stage

    /// <summary>
    /// Puts <paramref name="creature"/> (a prefab name, "" for none) on the stage, or empties
    /// the stage when the name is empty or resolves to nothing. The previous creature is
    /// deactivated before the new one stands, so a film can never catch both.
    /// </summary>
    internal static void Show(string creature)
    {
        s_pictureFresh = false;
        GameObject? built = Build(creature);
        DropCreature();
        if (built == null)
        {
            return;
        }

        s_creature = built;
        built.SetActive(true);
        Settle(built);
        if (!Frame(built))
        {
            DropCreature();
        }
    }

    /// <summary>An inactive, stripped copy on the stage's layer, or null (nothing to show).</summary>
    private static GameObject? Build(string creature)
    {
        if (creature.Length == 0 || !Ensure())
        {
            return null;
        }

        ZNetScene? zns = ZNetScene.instance;
        GameObject? prefab = zns != null ? zns.GetPrefab(creature) : null;
        if (zns == null || prefab == null)
        {
            Disguise.LogDebug($"Preview: no prefab named \"{creature}\".");
            return null;
        }

        GameObject? clone = GhostFactory.BuildInert(zns.GetPrefabHash(prefab));
        if (clone == null)
        {
            return null;
        }

        clone.transform.SetParent(s_root!.transform, false);
        clone.transform.localPosition = Vector3.zero;
        clone.transform.localRotation = Quaternion.identity;
        SetLayer(clone.transform, s_layer);
        foreach (
            SkinnedMeshRenderer mesh in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true)
        )
        {
            if (mesh != null)
            {
                mesh.updateWhenOffscreen = true;
            }
        }
        return clone;
    }

    /// <summary>
    /// The creature held in the first frame of its own default state: an animator left running
    /// would play an idle loop nobody asked for, and moving it would be movement on a copy
    /// with no Character to own it. applyRootMotion and the events go off first, so that one
    /// evaluation can neither walk the copy nor send anything to the scripts already stripped.
    /// </summary>
    private static void Settle(GameObject clone)
    {
        foreach (Animator animator in clone.GetComponentsInChildren<Animator>(true))
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                continue;
            }
            animator.applyRootMotion = false;
            animator.fireEvents = false;
            if (animator.runtimeAnimatorController != null)
            {
                animator.Update(0f);
            }
            animator.enabled = false;
        }
    }

    /// <summary>
    /// Measures what stands on the stage and works out where the camera has to be to show it
    /// whole: the sphere of every visible mesh, framed by the field of view with a margin.
    /// Returns false when there is nothing to draw (a prefab of effects only, say).
    /// </summary>
    private static bool Frame(GameObject clone)
    {
        Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
        bool any = false;
        Bounds bounds = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            // Particles and trails are not the creature: framing on their bounds would shrink
            // it into a corner of the picture. The meshes are what has to fit.
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }
            if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer)
            {
                continue;
            }
            if (!any)
            {
                bounds = renderer.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        if (!any)
        {
            Disguise.LogWarning($"Preview: {clone.name} has no mesh to show.");
            return false;
        }

        Camera? camera = s_camera;
        if (camera == null)
        {
            return false;
        }

        s_center = bounds.center;
        s_radius = Mathf.Max(bounds.extents.magnitude, MinRadius);
        s_distance = s_radius / Mathf.Tan(0.5f * VerticalFov * Mathf.Deg2Rad) * FrameMargin;
        s_yaw = 0f;

        // A near plane a hundredth of the way off, and a far plane that stops not far behind
        // the creature: depth stays in a ratio Unity can draw without z-fighting.
        camera.nearClipPlane = Mathf.Max(0.1f, s_distance * 0.01f);
        camera.farClipPlane = s_distance + (s_radius * 3f) + 50f;
        return true;
    }

    private static void DropCreature()
    {
        GameObject? creature = s_creature;
        s_creature = null;
        if (creature == null)
        {
            return;
        }
        // Deactivated before it is destroyed: Destroy only takes it at the end of the frame,
        // and a film in between would show the old creature standing inside the new one.
        creature.SetActive(false);
        UnityEngine.Object.Destroy(creature);
    }

    private static void SetLayer(Transform transform, int layer)
    {
        transform.gameObject.layer = layer;
        for (int i = 0; i < transform.childCount; i++)
        {
            SetLayer(transform.GetChild(i), layer);
        }
    }

    // ------------------------------------------------------------------ building the stage

    /// <summary>Builds the stage once; false when this machine has no layer left to show it on.</summary>
    private static bool Ensure()
    {
        if (s_layer == NotSearched)
        {
            s_layer = FreeLayer();
        }
        if (s_layer < 0)
        {
            return false;
        }
        if (s_camera != null && s_texture != null)
        {
            return true;
        }

        // A stage a scene change took down without Forget noticing (the window was closed).
        Forget();

        s_root = new GameObject("Disguise preview stage");
        s_root.transform.position = StageOrigin;
        HideStage();

        GameObject cameraObject = new GameObject("Disguise preview camera");
        cameraObject.transform.SetParent(s_root.transform, false);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false; // filmed by hand, once per frame from DisguiseGui.Tick
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f); // the window shows through
        camera.cullingMask = 1 << s_layer;
        camera.fieldOfView = VerticalFov;
        camera.allowHDR = false;
        camera.allowMSAA = true;
        camera.useOcclusionCulling = false;
        s_camera = camera;

        RenderTexture texture = new RenderTexture(
            (int)PictureWidth,
            (int)PictureHeight,
            24,
            RenderTextureFormat.ARGB32
        );
        texture.name = "Disguise preview";
        texture.antiAliasing = 4;
        texture.Create();
        s_texture = texture;
        camera.targetTexture = texture;

        // Key light on the camera (the side you look at is always lit), fill on the stage
        // (a fixed direction, so the shading shifts as the camera goes round).
        AddLight(cameraObject.transform, KeyAngles, KeyIntensity);
        AddLight(s_root.transform, FillAngles, FillIntensity);
        return true;
    }

    private static void AddLight(Transform parent, Vector3 angles, float intensity)
    {
        GameObject lightObject = new GameObject("Disguise preview light");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localRotation = Quaternion.Euler(angles);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.shadows = LightShadows.None; // nothing stands on a surface here to cast on
        light.cullingMask = 1 << s_layer; // and it must not reach anything outside the stage
    }

    /// <summary>
    /// The highest layer no mod gave a name to (31 down to 8), else one of the three Unity
    /// keeps spare under 8, else -1: every layer taken, and there is no preview.
    /// </summary>
    private static int FreeLayer()
    {
        for (int layer = 31; layer >= 8; layer--)
        {
            if (string.IsNullOrEmpty(LayerMask.LayerToName(layer)))
            {
                return layer;
            }
        }
        for (int i = 0; i < SpareLayers.Length; i++)
        {
            int layer = SpareLayers[i];
            if (string.IsNullOrEmpty(LayerMask.LayerToName(layer)))
            {
                return layer;
            }
        }
        if (!s_layerWarned)
        {
            s_layerWarned = true;
            Disguise.LogWarning(
                "GUI: every layer is named (by a mod), so the 3D preview has nowhere to stand and stays off."
            );
        }
        return NoFreeLayer;
    }

    /// <summary>
    /// Takes the stage's layer off every camera and every directional light. They belong to
    /// the game: without this the creature would be drawn into the world (20 km down, but a
    /// camera pointed down and a long far plane could reach it) and lit by the sun of the
    /// moment. Nothing else is ever on that layer - it is one nothing named - so taking it
    /// away changes nothing for the game, and it is given back in Forget.
    ///
    /// Done once, when the stage is built: a light created later (a lightning flash, for one
    /// frame of a storm) is not caught, and would light the preview for that moment only.
    /// </summary>
    private static void HideStage()
    {
        int bit = 1 << s_layer;

        s_hiddenCameras.Clear();
        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || (camera.cullingMask & bit) == 0)
            {
                continue;
            }
            s_hiddenCameras.Add(new KeyValuePair<Camera, int>(camera, camera.cullingMask));
            camera.cullingMask &= ~bit;
        }

        s_hiddenLights.Clear();
        Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            // Only a directional light can cross 20 km; a torch or a bonfire has a range.
            if (
                light == null
                || light.type != LightType.Directional
                || (light.cullingMask & bit) == 0
            )
            {
                continue;
            }
            s_hiddenLights.Add(new KeyValuePair<Light, int>(light, light.cullingMask));
            light.cullingMask &= ~bit;
        }
    }

    private static void GiveLayerBack()
    {
        for (int i = 0; i < s_hiddenCameras.Count; i++)
        {
            KeyValuePair<Camera, int> pair = s_hiddenCameras[i];
            if (pair.Key != null)
            {
                pair.Key.cullingMask = pair.Value;
            }
        }
        s_hiddenCameras.Clear();

        for (int i = 0; i < s_hiddenLights.Count; i++)
        {
            KeyValuePair<Light, int> pair = s_hiddenLights[i];
            if (pair.Key != null)
            {
                pair.Key.cullingMask = pair.Value;
            }
        }
        s_hiddenLights.Clear();
    }

    // ------------------------------------------------------------------ per frame

    /// <summary>
    /// Films the stage: DisguiseGui.Tick, once per frame while the window is on screen. The
    /// camera turns around the creature (the turntable), then the film is taken with the
    /// world's fog and ambient held off it - both are global, so they are restored before
    /// Unity renders anything else, which it does only after every Update (this is one).
    /// </summary>
    internal static void Render()
    {
        Camera? camera = s_camera;
        GameObject? creature = s_creature;
        if (camera == null || (creature == null && s_pictureFresh))
        {
            return;
        }

        if (creature != null)
        {
            s_yaw += TurnDegreesPerSecond * Time.unscaledDeltaTime;
            float yaw = s_yaw * Mathf.Deg2Rad;
            float pitch = Tilt * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(
                Mathf.Sin(yaw) * Mathf.Cos(pitch),
                Mathf.Sin(pitch),
                Mathf.Cos(yaw) * Mathf.Cos(pitch)
            );
            camera.transform.position = s_center + (direction * s_distance);
            camera.transform.LookAt(s_center);
        }

        bool fog = RenderSettings.fog;
        Color ambient = RenderSettings.ambientLight;
        Color sky = RenderSettings.ambientSkyColor;
        Color equator = RenderSettings.ambientEquatorColor;
        Color ground = RenderSettings.ambientGroundColor;
        try
        {
            RenderSettings.fog = false;
            RenderSettings.ambientLight = PreviewAmbient;
            RenderSettings.ambientSkyColor = PreviewAmbient;
            RenderSettings.ambientEquatorColor = PreviewAmbient;
            RenderSettings.ambientGroundColor = PreviewAmbient * 0.7f;
            camera.Render();
        }
        finally
        {
            RenderSettings.fog = fog;
            RenderSettings.ambientLight = ambient;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
        }
        s_pictureFresh = true;
    }

    // ------------------------------------------------------------------ teardown

    /// <summary>
    /// Lets go of everything: the creature, the stage, the texture (which no scene change
    /// releases) and the layer the cameras and the lights are missing. Called with the window
    /// closing, and every frame it is closed - hence the way out when there is nothing left.
    /// </summary>
    internal static void Forget()
    {
        if (s_texture == null && s_root == null)
        {
            return;
        }

        DropCreature();
        GiveLayerBack();
        if (s_root != null)
        {
            // The camera and the two lights are children of it.
            UnityEngine.Object.Destroy(s_root);
        }
        s_root = null;
        s_camera = null;
        if (s_texture != null)
        {
            s_texture.Release();
            UnityEngine.Object.Destroy(s_texture);
        }
        s_texture = null;
        s_center = Vector3.zero;
        s_radius = 0f;
        s_distance = 0f;
        s_yaw = 0f;
        s_pictureFresh = true;
    }
}
