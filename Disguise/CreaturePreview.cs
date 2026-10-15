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
/// creature turning, so the framing measured once from the bounds stays true while it spins,
/// and it never stands nearer than MinCameraDistance to it - the stage is 20 km from the
/// origin, where a float keeps two millimetres, and a camera a hand's width from a small
/// prefab went round on whole pixels of rounding: an Acorn trembled in the picture, a
/// creature metres farther off did not.
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
    internal const float PictureWidth = 420f;

    internal const float PictureHeight = 300f;

    /// <summary>Where the stage stands: 20 km under the world, out of every camera playing the game.</summary>
    private static readonly Vector3 s_stageOrigin = new Vector3(0f, -20000f, 0f);

    private static readonly Vector3 s_keyAngles = new Vector3(28f, 22f, 0f);

    private static readonly Vector3 s_fillAngles = new Vector3(20f, -135f, 0f);

    private static readonly Color s_previewAmbient = new Color(0.42f, 0.42f, 0.47f);

    private const float KeyIntensity = 1.1f;

    private const float FillIntensity = 0.4f;

    private const float VerticalFov = 35f;

    /// <summary>How high above the creature the camera looks down, in degrees.</summary>
    private const float Tilt = 14f;

    private const float TurnDegreesPerSecond = 25f;

    /// <summary>How much bigger the framed picture is than the creature, so nothing is ever clipped.</summary>
    private const float FrameMargin = 1.2f;

    /// <summary>
    /// How far a posed box may stick out of the box Unity culls by and still be believed, as
    /// a share of that box's own size on each axis: the two describe the same geometry, so
    /// the whisker of difference between them is rounding, and this much tolerance is what
    /// lets the fit take the posed box for the drawn one.
    /// </summary>
    private const float PoseSlack = 0.02f;

    /// <summary>
    /// The shortest distance the camera is ever framed at, and with it the most a prefab can
    /// be grown on the stage (MinCameraDistance / it): a floor under the FIT, so a prefab
    /// whose meshes measure nothing at all cannot ask for endless growth. It is only a
    /// guard - nothing real is this small - and every prefab above it fills the picture
    /// whatever its own size, which is what a floor on the RADIUS denied the smallest of
    /// them: the 0.25 m this replaced framed an Acorn at a hand's width, and the acorn
    /// filled a fifth of the picture.
    /// </summary>
    private const float MinFramedDistance = 0.05f;

    /// <summary>
    /// The camera never stands nearer to the creature than this, whatever the prefab. The
    /// stage is 20 km from the origin, where a float keeps about two millimetres: framed a
    /// hand's width away - which is where a small prefab without this rule is framed - the
    /// camera's own position, its line of sight and the mesh's way back from world to view
    /// space were all rounded to that grid, and the rounding differed with every step of the
    /// turntable, so small prefabs visibly trembled in the picture while creatures, metres
    /// farther off, did not. A prefab framed nearer than this is grown on the stage until it
    /// stands this far off - the picture does not change, a projection cannot tell a bigger
    /// object from a receded camera - and the rounding falls to a twentieth of a pixel. It
    /// bounds the growth too: nothing is ever grown more than 40 / MinFramedDistance.
    /// </summary>
    private const float MinCameraDistance = 40f;

    /// <summary>Spares for s_layer: not searched yet, and nothing free found (a mod named them all).</summary>
    private const int NotSearched = -2;

    private const int NoFreeLayer = -1;

    /// <summary>The layers Unity keeps spare under 8: unnamed, and nothing in the game draws on them.</summary>
    private static readonly int[] s_spareLayers = { 7, 6, 3 };

    private static int s_layer = NotSearched;

    private static bool s_layerWarned;

    private static GameObject? s_root;

    private static Camera? s_camera;

    private static RenderTexture? s_texture;

    private static GameObject? s_creature;

    /// <summary>Cameras and lights missing the stage's layer, with the mask each had, given back by Forget.</summary>
    private static readonly List<KeyValuePair<Camera, int>> s_hiddenCameras = new();

    private static readonly List<KeyValuePair<Light, int>> s_hiddenLights = new();

    /// <summary>
    /// The mesh the pose of a skinned renderer is baked into, and the vertices read back out
    /// of it: one for the whole window, reused, so walking the list of hundreds of prefabs
    /// never allocates.
    /// </summary>
    private static Mesh? s_bake;

    private static readonly List<Vector3> s_bakeVertices = new();

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

        // The player's own seed: the picture of YOUR costume is then the very pick that
        // costume wears everywhere else - your screen (SelfView) and the machines running no
        // DLL (CostumeGear) both roll with it. Any other creature in the list is a picture
        // too, rolled with the same seed: one picture, the same at every rebuild (no ZDO to
        // read - the menu, a probe - falls back to the prefab's own name inside GearSkin).
        GameObject? clone = GhostFactory.BuildInert(
            zns.GetPrefabHash(prefab),
            keepEquipment: true,
            gearSeed: GearSkin.Seed(GameAccess.GetLocalPlayerZDO())
        );
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
        // The picture shows the costume as the world draws it: dressed for the configured
        // level, its scale included - and measured by the frame that fits it to the picture,
        // so a big one still fits.
        LevelSkin.Apply(clone, prefab, DisguiseConfig.ConfigCreatureLevel.Value);
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
    /// whole: the box of every mesh Unity really draws (a mesh it measures and never draws
    /// has no say in the framing), framed along both axes by the field of view with a margin,
    /// grown first when it would stand nearer to the camera than MinCameraDistance.
    /// A skinned mesh is measured from its POSE rather than from the box it hands out - the
    /// saved one is the bind pose, and a ragdoll is posed nothing like it (DrawnBounds).
    /// Returns false when there is nothing to draw (a prefab of effects only, say).
    /// </summary>
    private static bool Frame(GameObject clone)
    {
        Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
        bool any = false;
        Bounds bounds = default;
        int measured = 0;
        int posed = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!DrawsMesh(renderer))
            {
                continue;
            }
            Bounds box = DrawnBounds(renderer, out bool fromPose);
            measured++;
            if (fromPose)
            {
                posed++;
            }
            if (!any)
            {
                bounds = box;
                any = true;
            }
            else
            {
                bounds.Encapsulate(box);
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

        float framed = FramedDistance(bounds);
        float scale = Mathf.Max(1f, MinCameraDistance / framed);
        if (scale > 1f)
        {
            clone.transform.localScale *= scale;
        }

        // The framing grows with the clone: every mesh moves scale times farther from the
        // root's own position, so the box does too, and the picture stays the one it was - a
        // projection cannot tell a bigger object from a receded camera. What the picture does
        // not keep is the rounding of a position 20 km from the origin, which is what made
        // small prefabs tremble (MinCameraDistance).
        Vector3 root = clone.transform.position;
        s_center = root + ((bounds.center - root) * scale);
        s_radius = Mathf.Max(bounds.extents.magnitude, MinFramedDistance) * scale;
        s_distance = framed * scale;
        s_yaw = 0f;

        // A near plane a hundredth of the way off, and a far plane that stops not far behind
        // the creature: depth stays in a ratio Unity can draw without z-fighting.
        camera.nearClipPlane = Mathf.Max(0.1f, s_distance * 0.01f);
        camera.farClipPlane = s_distance + (s_radius * 3f) + 50f;

        // One line when Logs is on, so a prefab that fills its picture badly can be read off
        // the log: what was measured, and where the camera was put for it. A long, low thing
        // framed by its own length is not a bug - 12 m of ship in a picture 9 m tall leaves
        // its hull a band - but a size twice the visible one is, and how many meshes were
        // measured from their pose says whether the saving boxes were believed.
        Disguise.LogDebug(
            $"Preview: {clone.name} measures {bounds.size.x:0.##} x {bounds.size.y:0.##} x {bounds.size.z:0.##} m"
                + $" ({posed} of {measured} meshes measured from their pose),"
                + $" framed at {framed:0.##} m, grown {scale:0.#}x."
        );
        return true;
    }

    /// <summary>
    /// Whether this renderer draws something the picture has to fit: a mesh Unity really
    /// draws - not a particle or a trail (those are effects the costume spawns, not the
    /// costume, and a puff of smoke would otherwise be the size of the creature), and not a
    /// mesh Unity still measures but never draws: one on a GameObject the prefab leaves
    /// inactive (a sail kept furled, a spare part, the second mesh of an item), one told to
    /// draw no colour at all, one kept for its shadow only. Those have bounds all the same,
    /// and one of them is what a chest and a ship were being framed on - the chest they
    /// really are then filling a fifth of the picture.
    ///
    /// The size on the status line promises this very rule (PrefabResolver.CreatureSize), so
    /// it lives where both of them can reach it rather than being written twice.
    /// </summary>
    internal static bool DrawsMesh(Renderer renderer)
    {
        return renderer != null
            && renderer.enabled
            && !renderer.forceRenderingOff
            && SwitchedOn(renderer.transform)
            && renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
            && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer);
    }

    /// <summary>
    /// Whether this GameObject and every one above it are switched on.
    ///
    /// It reads activeSelf rather than activeInHierarchy because the answer has to be the
    /// same for a prefab ASSET and for a copy standing on the stage, and an asset is not in a
    /// scene: activeInHierarchy says no for every object of one however the prefab ships it,
    /// so the size on the status line - measured on the asset - went blank for every prefab
    /// there was when the two were measured with that one call. Walking the parents reads the
    /// prefab's own idea of what is on, which is the one a copy is built with.
    /// </summary>
    internal static bool SwitchedOn(Transform transform)
    {
        for (Transform? node = transform; node != null; node = node.parent)
        {
            if (!node.gameObject.activeSelf)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// The box of what Unity is about to DRAW for one renderer - which for a skinned one is
    /// not the box it hands out, and that is a bug worth a method of its own.
    ///
    /// Renderer.bounds comes from the bounds saved with the prefab, and those are the bounds
    /// of the BIND pose: the arms-out stand the model was authored in. A ragdoll is posed
    /// nothing like it - Abomination_ragdoll lies folded, its skeleton spanning half of what
    /// the saved box says - so framing on that box put the camera twice too far back and the
    /// creature filled a corner of its own picture. The same saving box is why the living
    /// Abomination framed too large: 12 m saved, 7.5 m of skeleton.
    ///
    /// The posed vertices settle it, and this is the moment to read them: Settle has just
    /// left the clone in the first frame of its own default state and nothing moves it again
    /// - the strip takes Cloth and every script, the animator is off - so the box measured
    /// here is the box of every picture the turntable takes of it.
    /// </summary>
    internal static Bounds DrawnBounds(Renderer renderer, out bool fromPose)
    {
        fromPose = false;
        Bounds culling = renderer.bounds;
        if (renderer is SkinnedMeshRenderer skinned)
        {
            Bounds posed;
            if (TryPoseBounds(skinned, culling, out posed))
            {
                fromPose = true;
                return posed;
            }
        }
        return culling;
    }

    /// <summary>
    /// The box of one skinned renderer's posed vertices - false when there is nothing to bake,
    /// or when neither reading of the bake fits inside the box Unity culls by.
    ///
    /// A bake is the posed mesh in the renderer's own space, and Unity says no more than that
    /// about whether the transform's scale is already counted in those vertices. Both
    /// readings are therefore taken, and the culling box decides between them: it has to hold
    /// the drawn geometry whatever it is made of, so the wrong reading is either far too big
    /// to fit inside it - the scale counted twice - or, when the scale would shrink it, the
    /// smaller of the two that do fit: which is when the larger one is kept, and it is the
    /// right one. Neither fits (a culling box drawn tighter than the geometry it culls), and
    /// the saved bounds stand, as they did before any of this existed.
    /// </summary>
    private static bool TryPoseBounds(
        SkinnedMeshRenderer skinned,
        Bounds culling,
        out Bounds bounds
    )
    {
        bounds = default;
        if (skinned.sharedMesh == null || !Bake(skinned))
        {
            return false;
        }

        // The whole transform, for a bake without the scale in it; and the transform with the
        // scale taken off, for a bake with it.
        Transform body = skinned.transform;
        bool wholeFit = BoxOf(body.localToWorldMatrix, out Bounds whole) && Inside(whole, culling);
        bool turnedFit =
            BoxOf(Matrix4x4.TRS(body.position, body.rotation, Vector3.one), out Bounds turned)
            && Inside(turned, culling);
        if (wholeFit && turnedFit)
        {
            bounds = turned.size.sqrMagnitude > whole.size.sqrMagnitude ? turned : whole;
            return true;
        }
        if (wholeFit || turnedFit)
        {
            bounds = wholeFit ? whole : turned;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Writes the current pose of one skinned renderer into the shared mesh and reads its
    /// vertices back out; false when the renderer had none.
    /// </summary>
    private static bool Bake(SkinnedMeshRenderer skinned)
    {
        Mesh? bake = s_bake;
        if (bake == null)
        {
            bake = new Mesh
            {
                name = "Disguise preview pose",
                hideFlags = HideFlags.HideAndDontSave, // no scene change is going to release it
            };
            s_bake = bake;
        }
        bake.Clear();
        skinned.BakeMesh(bake, true);
        s_bakeVertices.Clear();
        bake.GetVertices(s_bakeVertices);
        return s_bakeVertices.Count > 0;
    }

    /// <summary>The box of the baked vertices pushed through a matrix; false when there are none.</summary>
    private static bool BoxOf(Matrix4x4 matrix, out Bounds bounds)
    {
        bounds = default;
        int count = s_bakeVertices.Count;
        for (int i = 0; i < count; i++)
        {
            Vector3 point = matrix.MultiplyPoint3x4(s_bakeVertices[i]);
            if (i == 0)
            {
                bounds = new Bounds(point, Vector3.zero);
            }
            else
            {
                bounds.Encapsulate(point);
            }
        }
        return count > 0;
    }

    /// <summary>Whether a box fits inside another, give or take PoseSlack of the outer one's size.</summary>
    private static bool Inside(Bounds inner, Bounds outer)
    {
        Vector3 slack = outer.size * PoseSlack;
        Vector3 below = inner.min - outer.min;
        Vector3 above = outer.max - inner.max;
        return below.x >= -slack.x
            && below.y >= -slack.y
            && below.z >= -slack.z
            && above.x >= -slack.x
            && above.y >= -slack.y
            && above.z >= -slack.z;
    }

    /// <summary>
    /// How far back the camera has to stand to fit what stands on the stage whole - the two
    /// axes fitted apart, and the worse of the two winning.
    ///
    /// A bounding SPHERE would fit the object to its longest corner to corner line and leave
    /// every other direction empty, which is why so much of the picture went unused: a ship
    /// filling it end to end showed a third of its height, a deer two thirds of its own.
    /// The camera goes round the object, so the widest it can ever meet is the plan diagonal
    /// (size.x, size.z - the widest a box ever presents while turning), against the horizontal
    /// field of view; and the tallest it ever meets is the height, which the turn never
    /// changes and only the tilt adds a share of the plan to. Both are therefore exact worst
    /// cases over the whole turntable, and the object fills its picture without ever being
    /// clipped by it.
    /// </summary>
    private static float FramedDistance(Bounds bounds)
    {
        float x = bounds.size.x;
        float y = bounds.size.y;
        float z = bounds.size.z;
        float plan = Mathf.Sqrt((x * x) + (z * z));
        float tilt = Tilt * Mathf.Deg2Rad;
        float height = (y * Mathf.Cos(tilt)) + (plan * Mathf.Sin(tilt));

        // The horizontal field of view is the vertical one widened by the shape of the
        // picture: 420 over 300, so a little wider than tall (a projection keeps angles).
        float halfTan = Mathf.Tan(0.5f * VerticalFov * Mathf.Deg2Rad);
        float horizontalTan = halfTan * (PictureWidth / PictureHeight);
        float vertical = (height * 0.5f) / halfTan;
        float horizontal = (plan * 0.5f) / horizontalTan;
        return Mathf.Max(Mathf.Max(vertical, horizontal) * FrameMargin, MinFramedDistance);
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

    /// <summary>
    /// Puts every node of the creature on the stage's layer, and reports whether anything
    /// had to be moved.
    ///
    /// SetLayer is where the copy is built - and the gear is not there yet: GearSkin writes
    /// the slots while the copy is STILL INACTIVE, and VisEquipment instantiates the armor
    /// and the weapon of each of them a moment later, when its own Start runs on the
    /// activated copy. Every one of those objects carries the layer of its own PREFAB (a
    /// layer belongs to a GameObject, it is not inherited from its parent), and the stage
    /// camera draws one single layer - which is why the picture showed the creature but
    /// never what it wears, whatever Show armor and Show weapons said. The walk runs once
    /// per film (Render), before the film is taken: one layer test per node, and only what
    /// a spawn just brought in is written. A caller that gets true back re-measures the
    /// creature, so what arrived late is framed into the picture too.
    /// </summary>
    private static bool StageLayer(Transform transform)
    {
        bool moved = false;
        if (transform.gameObject.layer != s_layer)
        {
            transform.gameObject.layer = s_layer;
            moved = true;
        }
        for (int i = 0; i < transform.childCount; i++)
        {
            moved |= StageLayer(transform.GetChild(i));
        }
        return moved;
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
        s_root.transform.position = s_stageOrigin;
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
        AddLight(cameraObject.transform, s_keyAngles, KeyIntensity);
        AddLight(s_root.transform, s_fillAngles, FillIntensity);
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
        for (int i = 0; i < s_spareLayers.Length; i++)
        {
            int layer = s_spareLayers[i];
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
            // The gear of the picture lands on the stage a moment AFTER the copy does
            // (VisEquipment.Start instantiates it from the slots GearSkin wrote while the
            // copy was inactive), and every new object carries the layer of its own PREFAB -
            // while the camera draws one single layer. Anything a spawn just brought in is
            // therefore put back on the stage before the film is taken, and the creature is
            // measured once more when something arrived: a spear held out can reach beyond
            // the box the picture was framed on. The walk finds its own silence within a
            // frame or two - nothing else ever adds a node to the copy (the strip took every
            // script) - and only a node off the stage is ever written.
            if (StageLayer(creature.transform))
            {
                _ = Frame(creature);
            }

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
            RenderSettings.ambientLight = s_previewAmbient;
            RenderSettings.ambientSkyColor = s_previewAmbient;
            RenderSettings.ambientEquatorColor = s_previewAmbient;
            RenderSettings.ambientGroundColor = s_previewAmbient * 0.7f;
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
        if (s_bake != null)
        {
            // A Mesh made at runtime is not released by a scene change, any more than the
            // texture above it - and the next window builds another.
            UnityEngine.Object.Destroy(s_bake);
            s_bake = null;
        }
        s_bakeVertices.Clear();
        s_center = Vector3.zero;
        s_radius = 0f;
        s_distance = 0f;
        s_yaw = 0f;
        s_pictureFresh = true;
    }
}
