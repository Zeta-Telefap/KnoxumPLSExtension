using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// The mod's sounds: the game's sounds it borrows by name and the quiet one-shots it plays itself.
    /// </summary>
    public sealed partial class PropLifter
    {


        // --- sounds of a flying prop ---

        /// <summary>
        /// A sound by its SoundObject name. All SoundObjects are indexed on first use (and again after
        /// SoundRescanInterval seconds while a requested sound is missing), so a lookup is free afterwards.
        /// </summary>
        private static SoundObject GetSound(string name)
        {
            SoundObject sound;
            if (SoundsByName.TryGetValue(name, out sound) && sound != null)
            {
                return sound;
            }

            if (Time.unscaledTime < _nextSoundScanTime)
            {
                return null;
            }

            _nextSoundScanTime = Time.unscaledTime + SoundRescanInterval;
            ScanSounds();
            return SoundsByName.TryGetValue(name, out sound) && sound != null ? sound : null;
        }

        private static void ScanSounds()
        {
            try
            {
                SoundObject known;
                SoundObject[] sounds = Resources.FindObjectsOfTypeAll<SoundObject>();
                for (int i = 0; i < sounds.Length; i++)
                {
                    SoundObject candidate = sounds[i];
                    if (candidate != null && !string.IsNullOrEmpty(candidate.name)
                        && (!SoundsByName.TryGetValue(candidate.name, out known) || known == null))
                    {
                        SoundsByName[candidate.name] = candidate;
                    }
                }

                // 1st Prize plays its audBang when it slams into a wall.
                if (!SoundsByName.TryGetValue(BangSoundName, out known) || known == null)
                {
                    FirstPrize[] prizes = Resources.FindObjectsOfTypeAll<FirstPrize>();
                    for (int i = 0; i < prizes.Length; i++)
                    {
                        if (prizes[i] != null && prizes[i].audBang != null)
                        {
                            SoundsByName[BangSoundName] = prizes[i].audBang;
                            break;
                        }
                    }
                }

                // The Gum (the projectile of Beans) loops its audWhoosh while it flies.
                if (!SoundsByName.TryGetValue(WhooshSoundName, out known) || known == null)
                {
                    FieldInfo whooshField = typeof(Gum).GetField(
                        "audWhoosh", BindingFlags.Instance | BindingFlags.NonPublic);
                    Gum[] gums = whooshField != null ? Resources.FindObjectsOfTypeAll<Gum>() : new Gum[0];
                    for (int i = 0; i < gums.Length; i++)
                    {
                        SoundObject whoosh = gums[i] != null ? whooshField.GetValue(gums[i]) as SoundObject : null;
                        if (whoosh != null)
                        {
                            SoundsByName[WhooshSoundName] = whoosh;
                            break;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                LogSoundError("Indexing the sounds", exception);
            }

            ReportSound(BangSoundName);
            ReportSound(WhooshSoundName);
            ReportSound(FloorSoundName);
        }

        /// <summary>Logs, once per outcome, whether a sound the mod plays exists.</summary>
        private static void ReportSound(string name)
        {
            SoundObject sound;
            bool found = SoundsByName.TryGetValue(name, out sound) && sound != null;
            if (SoundsReported.Add(name + (found ? ":found" : ":missing")))
            {
                Debug.Log("[Lifter Frenzy] Sound '" + name + "': "
                    + (found ? "found (" + sound.name + ")." : "not found, so it stays silent."));
            }
        }

        /// <summary>
        /// A 3D sound source on its own object, built like AudioManager.VirtualAwake builds its AudioSourceManager
        /// (inactive while it is set up), so the game's volume settings and mixer groups apply.
        /// </summary>
        private static AudioSourceManager CreateSoundSource(string hostName, Vector3 position, out GameObject host)
        {
            host = new GameObject(hostName);
            try
            {
                host.SetActive(false);
                host.transform.position = position;
                AudioSourceManager manager = host.AddComponent<AudioSourceManager>();
                manager.Initialize(true, false, false);
                manager.spatialBlend = 1f;
                manager.rolloffMode = AudioRolloffMode.Linear;
                manager.minDistance = SoundMinDistance;
                manager.maxDistance = SoundMaxDistance;
                host.SetActive(true);
                return manager;
            }
            catch
            {
                Destroy(host);
                throw;
            }
        }

        /// <summary>A short sound at a position, fire and forget: its object removes itself.</summary>
        private static void PlaySoundAt(Vector3 position, string soundName, float volume)
        {
            try
            {
                SoundObject sound = GetSound(soundName);
                if (sound == null || sound.soundClip == null)
                {
                    return;
                }

                GameObject host;
                AudioSourceManager manager = CreateSoundSource("Lifter Frenzy sound", position, out host);
                manager.PlayOneShot(SoundType.Effect, sound.soundClip, sound.volumeMultiplier * volume);
                Destroy(host, sound.soundClip.length + 0.5f);
            }
            catch (Exception exception)
            {
                LogSoundError("Playing the sound '" + soundName + "'", exception);
            }
        }

        /// <summary>The looping whoosh of a thrown prop; StepFlight moves it with the prop, landing ends it.</summary>
        private static void StartFlightSound(CarryState state)
        {
            try
            {
                SoundObject whoosh = GetSound(WhooshSoundName);
                if (whoosh == null || whoosh.soundClip == null)
                {
                    return;
                }

                GameObject host;
                AudioSourceManager manager = CreateSoundSource(
                    "Lifter Frenzy flight sound", state.FlightPosition + state.FlightCenterOffset, out host);
                manager.SetClip(SoundType.Effect, whoosh.soundClip);
                manager.loop = true;
                manager.volume = whoosh.volumeMultiplier;
                manager.Play();
                // A flight ends by itself after ReleaseTimeout; this only guards against a missed stop.
                Destroy(host, ReleaseTimeout + 2f);
                state.FlightSoundObject = host;
            }
            catch (Exception exception)
            {
                LogSoundError("Starting the flight sound", exception);
            }
        }

        private static void StopFlightSound(CarryState state)
        {
            if (state == null)
            {
                return;
            }

            if (state.FlightSoundObject != null)
            {
                Destroy(state.FlightSoundObject);
            }

            state.FlightSoundObject = null;
        }

        private static void LogSoundError(string what, Exception exception)
        {
            if (Time.unscaledTime >= _nextSoundErrorLogTime)
            {
                _nextSoundErrorLogTime = Time.unscaledTime + 5f;
                Debug.LogError("[Lifter Frenzy] " + what + " failed: " + exception);
            }
        }
    }
}
