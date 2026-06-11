using System.Collections.Generic;
using UnityEngine;

public class LOTOAnimationController : MonoBehaviour
{
    [Header("Animators")]
    public Animator generatorAnimator;
    public Animator cableAnimator;
    public string generatorRootName = "Generator_Model";

    [Header("Animator Layers")]
    public int generatorLayer = 0;
    public int cableLayer = 0;

    [Header("Clip Names")]
    public string switchBoxOpenClipName = "SwitchBox_Door_Unlock_And_Open";
    public string powerHandleToggleClipName = "MainPower_Handle_Toggle";
    public string generatorShutdownClipName = "Generator_Shutdown";
    public string cableWiggleClipName = "Cable_Baked_Shutdown_Wiggle_BlendShapes";
    public string mainDoorOpenClipName = "Door_Open";

    private readonly Dictionary<string, ClipPose> clipPoses = new Dictionary<string, ClipPose>();
    private readonly List<ActiveClip> activeClips = new List<ActiveClip>();
    private readonly List<ClipPose> heldPoses = new List<ClipPose>();
    private readonly List<TransformPose> initialTransformPoses = new List<TransformPose>();
    private readonly List<BlendShapePose> initialBlendShapePoses = new List<BlendShapePose>();

    private GameObject animationRoot;
    private bool initialized;

    private void Awake()
    {
        Initialize();
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            Initialize();
        }

        if (!initialized)
        {
            return;
        }

        ApplyInitialPose();

        activeClips.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        for (int i = 0; i < activeClips.Count; i++)
        {
            ActiveClip activeClip = activeClips[i];
            activeClip.Elapsed += Time.deltaTime;

            float clipLength = activeClip.Pose.Clip.length;
            float sampleTime = Mathf.Min(activeClip.Elapsed, clipLength);
            if (activeClip.Reverse)
            {
                sampleTime = Mathf.Max(0f, clipLength - sampleTime);
            }

            activeClip.Pose.Clip.SampleAnimation(animationRoot, sampleTime);

            if (activeClip.Elapsed >= clipLength)
            {
                if (activeClip.HoldFinalPose && !heldPoses.Contains(activeClip.Pose))
                {
                    heldPoses.Add(activeClip.Pose);
                }

                activeClips.RemoveAt(i);
                i--;
            }
            else
            {
                activeClips[i] = activeClip;
            }
        }

        foreach (ClipPose heldPose in heldPoses)
        {
            heldPose.ApplyFinalPose();
        }
    }

    public void PlaySwitchBoxOpen()
    {
        PlayClip(switchBoxOpenClipName, "switch box open", true);
    }

    public void PlaySwitchBoxClose()
    {
        PlayClip(switchBoxOpenClipName, "switch box close", false, true);
    }

    public void PlayPowerHandleToggle()
    {
        PlayClip(powerHandleToggleClipName, "power handle toggle", true);
    }

    public void PlayShutdownAndCableWiggle()
    {
        PlayClip(generatorShutdownClipName, "generator shutdown", true);
        PlayClip(cableWiggleClipName, "cable wiggle", false);
    }

    public void PlayMainDoorOpen()
    {
        PlayClip(mainDoorOpenClipName, "main door open", true);
    }

    public void ResetPoses()
    {
        heldPoses.Clear();
        activeClips.Clear();

        if (!initialized)
        {
            Initialize();
        }

        if (initialized)
        {
            ApplyInitialPose();
        }
    }

    public float GetClipLength(string clipName, float fallbackLength)
    {
        if (!initialized)
        {
            Initialize();
        }

        if (string.IsNullOrWhiteSpace(clipName))
        {
            return fallbackLength;
        }

        if (clipPoses.TryGetValue(clipName, out ClipPose clipPose) && clipPose.Clip != null)
        {
            return clipPose.Clip.length;
        }

        return fallbackLength;
    }

    public void ResolveAnimators()
    {
        if (string.IsNullOrWhiteSpace(generatorRootName))
        {
            generatorRootName = "Generator_Model";
        }

        if (generatorAnimator == null)
        {
            GameObject generatorRoot = GameObject.Find(generatorRootName);
            if (generatorRoot != null)
            {
                generatorAnimator = generatorRoot.GetComponentInChildren<Animator>();
            }
        }

        if (generatorAnimator == null)
        {
#if UNITY_2023_1_OR_NEWER
            generatorAnimator = FindFirstObjectByType<Animator>();
#else
            generatorAnimator = FindObjectOfType<Animator>();
#endif
        }

        if (cableAnimator == null)
        {
            cableAnimator = generatorAnimator;
        }
    }

    private void Initialize()
    {
        ResolveAnimators();
        if (generatorAnimator == null || generatorAnimator.runtimeAnimatorController == null)
        {
            return;
        }

        animationRoot = generatorAnimator.gameObject;
        CaptureInitialPose();
        BuildClipPoseCache();
        ApplyInitialPose();

        generatorAnimator.enabled = false;
        if (cableAnimator != null)
        {
            cableAnimator.enabled = false;
        }

        initialized = true;
    }

    private void PlayClip(string clipName, string purpose, bool holdFinalPose)
    {
        PlayClip(clipName, purpose, holdFinalPose, false);
    }

    private void PlayClip(string clipName, string purpose, bool holdFinalPose, bool reverse)
    {
        if (!initialized)
        {
            Initialize();
        }

        if (!initialized)
        {
            Debug.LogWarning($"LOTOAnimationController cannot play {purpose}: Animator is missing.");
            return;
        }

        if (string.IsNullOrWhiteSpace(clipName))
        {
            Debug.LogWarning($"LOTOAnimationController cannot play {purpose}: clip name is empty.");
            return;
        }

        if (!clipPoses.TryGetValue(clipName, out ClipPose clipPose) || clipPose.Clip == null)
        {
            Debug.LogWarning($"LOTOAnimationController cannot play {purpose}: clip '{clipName}' was not found.");
            return;
        }

        RemoveActiveClip(clipPose);
        if (reverse)
        {
            heldPoses.Remove(clipPose);
        }

        activeClips.Add(new ActiveClip
        {
            Pose = clipPose,
            HoldFinalPose = holdFinalPose,
            Priority = GetClipPriority(clipName),
            Elapsed = 0f,
            Reverse = reverse
        });
    }

    private void RemoveActiveClip(ClipPose clipPose)
    {
        for (int i = activeClips.Count - 1; i >= 0; i--)
        {
            if (activeClips[i].Pose == clipPose)
            {
                activeClips.RemoveAt(i);
            }
        }
    }

    private int GetClipPriority(string clipName)
    {
        if (clipName == generatorShutdownClipName || clipName == cableWiggleClipName)
        {
            return 0;
        }

        if (clipName == switchBoxOpenClipName)
        {
            return 10;
        }

        if (clipName == powerHandleToggleClipName)
        {
            return 20;
        }

        if (clipName == mainDoorOpenClipName)
        {
            return 30;
        }

        return 10;
    }

    private void CaptureInitialPose()
    {
        initialTransformPoses.Clear();
        initialBlendShapePoses.Clear();

        foreach (Transform targetTransform in animationRoot.GetComponentsInChildren<Transform>(true))
        {
            initialTransformPoses.Add(new TransformPose(targetTransform));
        }

        foreach (SkinnedMeshRenderer skinnedMeshRenderer in animationRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh sharedMesh = skinnedMeshRenderer.sharedMesh;
            if (sharedMesh == null)
            {
                continue;
            }

            for (int i = 0; i < sharedMesh.blendShapeCount; i++)
            {
                initialBlendShapePoses.Add(new BlendShapePose(skinnedMeshRenderer, i));
            }
        }
    }

    private void BuildClipPoseCache()
    {
        clipPoses.Clear();
        RuntimeAnimatorController controller = generatorAnimator.runtimeAnimatorController;
        if (controller == null)
        {
            return;
        }

        foreach (AnimationClip clip in controller.animationClips)
        {
            if (clip == null || clipPoses.ContainsKey(clip.name))
            {
                continue;
            }

            ApplyInitialPose();
            clip.SampleAnimation(animationRoot, clip.length);

            ClipPose pose = new ClipPose(clip);
            foreach (TransformPose initialPose in initialTransformPoses)
            {
                TransformPose finalPose = new TransformPose(initialPose.Target);
                if (finalPose.DiffersFrom(initialPose))
                {
                    pose.TransformPoses.Add(finalPose);
                }
            }

            foreach (BlendShapePose initialPose in initialBlendShapePoses)
            {
                BlendShapePose finalPose = new BlendShapePose(initialPose.Renderer, initialPose.Index);
                if (finalPose.DiffersFrom(initialPose))
                {
                    pose.BlendShapePoses.Add(finalPose);
                }
            }

            clipPoses.Add(clip.name, pose);
        }
    }

    private void ApplyInitialPose()
    {
        foreach (TransformPose pose in initialTransformPoses)
        {
            pose.Apply();
        }

        foreach (BlendShapePose pose in initialBlendShapePoses)
        {
            pose.Apply();
        }
    }

    private struct ActiveClip
    {
        public ClipPose Pose;
        public bool HoldFinalPose;
        public int Priority;
        public float Elapsed;
        public bool Reverse;
    }

    private sealed class ClipPose
    {
        public readonly AnimationClip Clip;
        public readonly List<TransformPose> TransformPoses = new List<TransformPose>();
        public readonly List<BlendShapePose> BlendShapePoses = new List<BlendShapePose>();

        public ClipPose(AnimationClip clip)
        {
            Clip = clip;
        }

        public void ApplyFinalPose()
        {
            foreach (TransformPose pose in TransformPoses)
            {
                pose.Apply();
            }

            foreach (BlendShapePose pose in BlendShapePoses)
            {
                pose.Apply();
            }
        }
    }

    private struct TransformPose
    {
        public readonly Transform Target;
        private readonly Vector3 localPosition;
        private readonly Quaternion localRotation;
        private readonly Vector3 localScale;

        public TransformPose(Transform target)
        {
            Target = target;
            localPosition = target.localPosition;
            localRotation = target.localRotation;
            localScale = target.localScale;
        }

        public bool DiffersFrom(TransformPose other)
        {
            return Vector3.Distance(localPosition, other.localPosition) > 0.0001f ||
                Quaternion.Angle(localRotation, other.localRotation) > 0.01f ||
                Vector3.Distance(localScale, other.localScale) > 0.0001f;
        }

        public void Apply()
        {
            if (Target == null)
            {
                return;
            }

            Target.localPosition = localPosition;
            Target.localRotation = localRotation;
            Target.localScale = localScale;
        }
    }

    private struct BlendShapePose
    {
        public readonly SkinnedMeshRenderer Renderer;
        public readonly int Index;
        private readonly float weight;

        public BlendShapePose(SkinnedMeshRenderer renderer, int index)
        {
            Renderer = renderer;
            Index = index;
            weight = renderer.GetBlendShapeWeight(index);
        }

        public bool DiffersFrom(BlendShapePose other)
        {
            return Mathf.Abs(weight - other.weight) > 0.001f;
        }

        public void Apply()
        {
            if (Renderer != null)
            {
                Renderer.SetBlendShapeWeight(Index, weight);
            }
        }
    }
}
