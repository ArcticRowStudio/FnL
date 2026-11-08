using System.Collections;
using UnityEngine;

/// <summary>
/// Clamshell radome: each half of the dome shell swings down about the hinge at the
/// centre of the dome base and tucks below the rim, the way an observatory dome opens.
/// The shutters must pivot at that hinge (the *_Shell sprites do), and a SpriteMask above
/// the rim line hides whatever has swung below it.
/// The lamps on the seam clamps show the lock: red while sealed. Opening first runs them
/// green from the top down, holds a beat, and only then swings the shells; closing turns
/// them back to red once the shells have sealed.
/// </summary>
[ExecuteAlways]
public class RadomeShutterController : MonoBehaviour
{
    [Header("Shutter References")]
    [Tooltip("Left dome shell, pivoted at the hinge (centre of the dome base)")]
    [SerializeField] private Transform leftShutter;
    [Tooltip("Right dome shell, pivoted at the hinge (centre of the dome base)")]
    [SerializeField] private Transform rightShutter;

    [Header("Motion Settings")]
    [Tooltip("How far each shell swings down. 90 tucks it entirely below the rim")]
    [SerializeField] private float openAngle = 90f;
    [Tooltip("Travel time in seconds")]
    [SerializeField] private float transitionDuration = 3.0f;
    [SerializeField] private AnimationCurve motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Seam Lamps")]
    [Tooltip("Lamp discs on the seam clamps, children of the shells so they swing with them")]
    [SerializeField] private SpriteRenderer[] seamLamps;
    [Tooltip("Glow halos, same order as Seam Lamps")]
    [SerializeField] private SpriteRenderer[] seamGlows;
    [SerializeField] private Color lockedColor = new Color(0.86f, 0.16f, 0.12f, 1f);   // Red
    [SerializeField] private Color unlockedColor = new Color(0.25f, 1f, 0.45f, 1f);    // Green
    [SerializeField] private float lockedGlowAlpha = 0.25f;
    [SerializeField] private float unlockedGlowAlpha = 0.85f;
    [Tooltip("Delay between one row of lamps and the next as they change, top to bottom")]
    [SerializeField] private float lampStepDelay = 0.08f;
    [Tooltip("Pause with every lamp green before the shells start to move")]
    [SerializeField] private float unlockHold = 0.5f;

    [Header("State Control")]
    [Range(0f, 1f)]
    [SerializeField] private float openProgress = 0f; // 0 = fully closed, 1 = fully open
    [SerializeField] private bool isOpen = false;

    private Coroutine transitionCoroutine;

    public bool IsOpen => isOpen;
    public bool IsMoving => transitionCoroutine != null;
    /// <summary>True while the lamps are turning green, before the shells move.</summary>
    public bool IsUnlocking { get; private set; }
    public float TransitionDuration => transitionDuration;

    private void Awake()
    {
        // Play mode reuses this instance (domain/scene reload are off): drop a handle left
        // over from a session stopped mid-swing.
        transitionCoroutine = null;
        IsUnlocking = false;
    }

    private void OnValidate()
    {
        ApplyProgress(openProgress);
        SetLamps(openProgress > 0f);
    }

    public void ApplyProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);
        float curveValue = motionCurve != null ? motionCurve.Evaluate(progress) : progress;
        float angle = curveValue * openAngle;

        // The left shell swings counter-clockwise (top goes left), the right one clockwise.
        if (leftShutter != null)
        {
            leftShutter.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        if (rightShutter != null)
        {
            rightShutter.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }
    }

    [ContextMenu("Toggle Shutter")]
    public void ToggleShutter()
    {
        SetState(!isOpen);
    }

    [ContextMenu("Open Shutter")]
    public void OpenShutter()
    {
        SetState(true);
    }

    [ContextMenu("Close Shutter")]
    public void CloseShutter()
    {
        SetState(false);
    }

    public void SetState(bool open)
    {
        isOpen = open;
        if (Application.isPlaying)
        {
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }
            IsUnlocking = false;
            transitionCoroutine = StartCoroutine(AnimateShutter(open ? 1f : 0f));
        }
        else
        {
            openProgress = open ? 1f : 0f;
            ApplyProgress(openProgress);
            SetLamps(open);
        }
    }

    private IEnumerator AnimateShutter(float targetProgress)
    {
        bool opening = targetProgress > openProgress;
        if (opening && openProgress <= 0f)
        {
            // Sealed: release the clamps first, top to bottom.
            IsUnlocking = true;
            yield return ChaseLamps(true);
            yield return new WaitForSeconds(unlockHold);
            IsUnlocking = false;
        }

        float startProgress = openProgress;
        // A reversal part way through only travels the remaining distance, at the same speed.
        float duration = transitionDuration * Mathf.Abs(targetProgress - startProgress);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            openProgress = Mathf.Lerp(startProgress, targetProgress, elapsed / duration);
            ApplyProgress(openProgress);
            yield return null;
        }

        openProgress = targetProgress;
        ApplyProgress(openProgress);

        if (openProgress <= 0f)
        {
            // Sealed again: clamps lock, bottom to top.
            yield return ChaseLamps(false);
        }

        transitionCoroutine = null;
    }

    /// <summary>Changes the lamps one row at a time (pairs at the same height go together).</summary>
    private IEnumerator ChaseLamps(bool unlocked)
    {
        if (seamLamps == null) yield break;

        var rows = new System.Collections.Generic.SortedDictionary<int, System.Collections.Generic.List<int>>();
        for (int i = 0; i < seamLamps.Length; i++)
        {
            if (seamLamps[i] == null) continue;
            // Unlock runs top-down (highest first), lock runs bottom-up.
            int key = Mathf.RoundToInt(seamLamps[i].transform.localPosition.y * 10f) * (unlocked ? -1 : 1);
            if (!rows.TryGetValue(key, out var list)) rows[key] = list = new System.Collections.Generic.List<int>();
            list.Add(i);
        }

        foreach (var row in rows.Values)
        {
            foreach (int i in row) SetLamp(i, unlocked);
            yield return new WaitForSeconds(lampStepDelay);
        }
    }

    private void SetLamps(bool unlocked)
    {
        if (seamLamps == null) return;
        for (int i = 0; i < seamLamps.Length; i++) SetLamp(i, unlocked);
    }

    private void SetLamp(int i, bool unlocked)
    {
        Color c = unlocked ? unlockedColor : lockedColor;
        if (seamLamps[i] != null) seamLamps[i].color = c;
        if (seamGlows != null && i < seamGlows.Length && seamGlows[i] != null)
        {
            c.a = unlocked ? unlockedGlowAlpha : lockedGlowAlpha;
            seamGlows[i].color = c;
        }
    }
}
