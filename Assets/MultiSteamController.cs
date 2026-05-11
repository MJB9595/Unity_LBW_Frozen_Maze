using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MultiSteamController : MonoBehaviour
{
    [Header("Particle Groups")]
    public List<ParticleSystem> leftSteams = new List<ParticleSystem>();
    public List<ParticleSystem> rightSteams = new List<ParticleSystem>();

    [Header("Audio")]
    public AudioClip steamSfx;
    private AudioSource audioSource;

    [Header("Timing")]
    public float activeTime = 2.0f;
    public float restTime = 2.0f;

    void Start()
    {
        foreach (var ps in leftSteams) { SetupParticle(ps); }
        foreach (var ps in rightSteams) { SetupParticle(ps); }

        StartCoroutine(SteamRoutine());
    }

    void SetupParticle(ParticleSystem ps)
    {
        if (ps == null) return;
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        ps.Stop();
    }

    IEnumerator SteamRoutine()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = steamSfx;
        audioSource.volume = 0.6f; // Reduced to 60%
        audioSource.playOnAwake = false;

        while (true)
        {
            if (steamSfx != null) audioSource.Play();
            foreach (var ps in leftSteams) { if (ps != null) ps.Play(); }
            yield return new WaitForSeconds(activeTime);
            foreach (var ps in leftSteams) { if (ps != null) ps.Stop(); }
            if (audioSource.isPlaying) audioSource.Stop();

            yield return new WaitForSeconds(restTime);

            if (steamSfx != null) audioSource.Play();
            foreach (var ps in rightSteams) { if (ps != null) ps.Play(); }
            yield return new WaitForSeconds(activeTime);
            foreach (var ps in rightSteams) { if (ps != null) ps.Stop(); }
            if (audioSource.isPlaying) audioSource.Stop();

            yield return new WaitForSeconds(restTime);
        }
    }
}