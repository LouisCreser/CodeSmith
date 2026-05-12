using UnityEngine;
using System;

public class TickDriver : MonoBehaviour
{
    [Tooltip("Base tick interval in seconds at 1x speed.")]
    public float baseTickSeconds = 1f;

    public bool IsPlaying { get; private set; }

    public event Action OnTick;

    bool tickInProgress;
    bool stepQueued;
    float cooldownRemaining;

    float currentPlaySpeedMultiplier = 1f;

    public bool IsStepQueued => stepQueued;

    void Update()
    {
        if (cooldownRemaining > 0f)
            cooldownRemaining -= Time.unscaledDeltaTime;

        if (tickInProgress)
            return;

        // A queued step has priority over autoplay
        if (stepQueued)
        {
            if (cooldownRemaining <= 0f)
            {
                stepQueued = false;
                RunTick(baseTickSeconds); // Step always runs at base speed
            }

            return;
        }

        if (!IsPlaying)
            return;

        if (cooldownRemaining <= 0f)
            RunTick(currentPlaySpeedMultiplier);
    }

    void RunTick(float speedMultiplier)
    {
        if (tickInProgress)
            return;

        tickInProgress = true;
        OnTick?.Invoke();
        tickInProgress = false;

        float clamped = Mathf.Max(0.2f, speedMultiplier);
        cooldownRemaining = baseTickSeconds / clamped;
    }

    public void Play(float speedMultiplier)
    {
        IsPlaying = true;
        currentPlaySpeedMultiplier = Mathf.Max(0.2f, speedMultiplier);
    }

    public void Pause()
    {
        IsPlaying = false;
    }

    public void QueueStep()
    {
        // A step always pauses autoplay
        IsPlaying = false;

        // Only allow one queued step at a time
        stepQueued = true;
    }

    public void CancelQueuedStep()
    {
        stepQueued = false;
    }

    public void ResetGate()
    {
        cooldownRemaining = 0f;
        tickInProgress = false;
        stepQueued = false;
    }
}