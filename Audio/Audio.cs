using Il2CppEekCharacterEngine.Interaction;
using Il2CppHouseParty;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

namespace HPAudio
{
    public class Audio : MelonMod
    {
        private readonly List<AudioClip?> clips = new();
        private readonly Il2CppReferenceArray<GUILayoutOption> opt = new(System.Array.Empty<GUILayoutOption>());
        private readonly List<string> songs = new();
        private int _currentSong = 0;
        private int CurrentSong
        {
            get
            {
                return _currentSong;
            }
            set
            {
                if (value < songs.Count)
                {
                    _currentSong = value;
                    previousSongName = currentSongName;
                    currentSongName = Path.GetFileNameWithoutExtension(songs[value]);
                }
            }
        }
        private string currentSongName = "None", previousSongName = "None";
        private string folderPath = "";
        private AudioSource gameSource1 = default!, gameSource2 = default!, modSource1 = default!, modSource2 = default!;
        private bool gotSpeakers = false;
        private bool inGameMain = false;
        private MelonPreferences_Entry<bool> makeClipsOnStart = default!;
        private MelonPreferences_Category settings = default!;
        private bool ShowUI = false;
        private Speaker speaker1 = default!, speaker2 = default!;
        private Rect UIRect = new(10, Screen.height * 0.2f, Screen.width * 0.2f, Screen.height * 0.2f);
        public bool CurrentlyPlaying { get; private set; } = false;

        public void ManageSpeakerSettings()
        {
            modSource1.time = 0;
            modSource2.time = 0;
            modSource1.priority = 32;
            modSource2.priority = 32;
            modSource1.enabled = true;
            modSource2.enabled = true;
            modSource1.ignoreListenerPause = true;
            modSource2.ignoreListenerPause = true;
            modSource1.mute = false;
            modSource2.mute = false;
            modSource1.bypassEffects = gameSource1.bypassEffects;
            modSource2.bypassEffects = gameSource2.bypassEffects;
            modSource1.bypassListenerEffects = gameSource1.bypassListenerEffects;
            modSource2.bypassListenerEffects = gameSource2.bypassListenerEffects;
            modSource1.volume = gameSource1.volume;
            modSource2.volume = gameSource2.volume;
            modSource1.outputAudioMixerGroup = gameSource1.outputAudioMixerGroup;
            modSource2.outputAudioMixerGroup = gameSource2.outputAudioMixerGroup;
            modSource1.priority = gameSource1.priority;
            modSource2.priority = gameSource2.priority;
            modSource1.spatialBlend = gameSource1.spatialBlend;
            modSource2.spatialBlend = gameSource2.spatialBlend;
        }

        /// <summary>
        /// Loads and plays the next song from the list
        /// </summary>
        public void Next()
        {
            if (CurrentSong + 1 >= songs.Count) CurrentSong = 0;
            else CurrentSong++;
            if (CurrentlyPlaying) Play();
        }

        public override void OnInitializeMelon()
        {
            settings = MelonPreferences.CreateCategory("AudioMod");
            makeClipsOnStart = settings.CreateEntry("MakeAllClipsOnStart", false);

            folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Eek", "House Party", "Mods", "Songs");
            MelonLogger.Msg("[Audio] Place songs (as .mp3) here: " + folderPath);
            ReloadSongList();

            if (makeClipsOnStart.Value)
                MelonLogger.Msg("[Audio] The mod will create all necessary audio ressources on game start, can take some time");
            else
                MelonLogger.Msg("[Audio] Audio ressources are going to be created on the fly, so it will take some time until a song first loads");
        }

        public override void OnGUI()
        {
            if (ShowUI)
            {
                GUILayout.BeginArea(UIRect);
                GUILayout.BeginVertical(opt);
                GUILayout.Label(currentSongName, opt);
                GUILayout.BeginHorizontal(opt);
                if (GUILayout.Button("Prev", opt))
                    Previous();
                if (GUILayout.Button("Shuffle", opt))
                    Shuffle();
                if (CurrentlyPlaying)
                {
                    if (GUILayout.Button("Pause", opt))
                        Pause();
                }
                else
                {
                    if (GUILayout.Button("Play", opt))
                        Play();
                }
                if (GUILayout.Button("Next", opt))
                    Next();
                if (GUILayout.Button("Stop", opt))
                    Stop();
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Stop playback and resume eek songs", opt))
                    ReturnToGameMusic();
                GUILayout.EndVertical();
                GUILayout.EndArea();
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            inGameMain = sceneName == "GameMain";
            if (inGameMain)
                Initialize();
        }

        public override void OnUpdate()
        {
            if (inGameMain && Keyboard.current[Key.A].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed)
            {
                ToggleUI();

                if (ShowUI && !gotSpeakers)
                {
                    Initialize();
                }
            }
        }

        /// <summary>
        /// Pauses song but keeps current time
        /// </summary>
        public void Pause()
        {
            if (gotSpeakers)
            {
                CurrentlyPlaying = false;

                modSource1.Pause();
                modSource2.Pause();
            }
        }

        /// <summary>
        /// Starts/Resumes playback
        /// </summary>
        public void Play()
        {
            if (gotSpeakers)
            {
                //resuming playback
                if (currentSongName == previousSongName && !CurrentlyPlaying)
                {
                    CurrentlyPlaying = true;
                }
                //new playback from pause or switching during playback to a new song
                if (currentSongName != previousSongName)
                {
                    if (clips[CurrentSong] != null)
                    {
                        modSource1.clip = clips[CurrentSong];
                        modSource2.clip = clips[CurrentSong];
                        CurrentlyPlaying = true;
                    }
                    else
                    {
                        if (MakeClip(CurrentSong, out var audio))
                        {
                            clips[CurrentSong] = audio;

                            modSource1.clip = clips[CurrentSong];
                            modSource2.clip = clips[CurrentSong];
                            CurrentlyPlaying = true;
                        }
                        else
                        {
                            CurrentlyPlaying = false;
                        }
                    }
                }
                //Play if everything went fine
                if (CurrentlyPlaying)
                {
                    speaker1._audioSource = modSource1;
                    speaker2._audioSource = modSource2;
                    gameSource1.enabled = false;
                    gameSource2.enabled = false;
                    modSource1.enabled = true;
                    modSource2.enabled = true;
                    modSource1.Play();
                    modSource2.Play();
                }

                if (CurrentlyPlaying) MelonLogger.Msg("Now playing: " + currentSongName);
            }
            else
            {
                GetSpeakers();
            }
        }

        /// <summary>
        /// Skips to the previous track in the list
        /// </summary>
        public void Previous()
        {
            if (CurrentSong - 1 < 0) CurrentSong = songs.Count - 1;
            else CurrentSong--;
            if (CurrentlyPlaying) Play();
        }

        /// <summary>
        /// Reloads the song songIndex
        /// </summary>
        public void ReloadSongList()
        {
            songs.Clear();
            clips.Clear();
            var _songs = Directory.GetFiles(folderPath);
            for (int i = 0; i < _songs.Length; i++)
            {
                if (Path.GetExtension(_songs[i]) == ".mp3")
                {
                    songs.Add(_songs[i]);
                    if (!makeClipsOnStart.Value)
                    {
                        if (MakeClip(i, out var audio))
                            clips.Add(audio);
                    }
                }
            }

            MelonLogger.Msg("Valid songs found:");
            if (songs.Count > 0)
            {
                foreach (var songNames in songs)
                {
                    MelonLogger.Msg(" - " + Path.GetFileNameWithoutExtension(songNames));
                }
            }
            else
            {
                MelonLogger.Msg("No songs found, here are files we did find:");
                for (int i = 0; i < _songs.Length; i++)
                {
                    MelonLogger.Msg(" - " + _songs[i]);
                }
            }
        }

        /// <summary>
        /// Stops all this overriding and goes back to the game thing as before
        /// </summary>
        public void ReturnToGameMusic()
        {
            if (gotSpeakers)
            {
                CurrentlyPlaying = false;
                gameSource1.enabled = true;
                gameSource2.enabled = true;
                modSource1.enabled = false;
                modSource2.enabled = false;
                speaker1._audioSource = gameSource1;
                speaker2._audioSource = gameSource2;
                speaker1.StartMusic();
                speaker2.StartMusic();
            }
        }

        /// <summary>
        /// Randomly selects the next song, then plays that.
        /// </summary>
        public void Shuffle()
        {
            CurrentSong = UnityEngine.Random.RandomRangeInt(0, clips.Count - 1);
            if (CurrentlyPlaying) Play();
        }

        /// <summary>
        /// Stops playback and resets time to 0
        /// </summary>
        public void Stop()
        {
            if (gotSpeakers)
            {
                CurrentlyPlaying = false;

                modSource1.Stop();
                modSource2.Stop();
                modSource1.enabled = false;
                modSource2.enabled = false;
                gameSource1.enabled = false;
                gameSource2.enabled = false;
            }
        }

        private void GetSpeakers()
        {
            if (!gotSpeakers)
            {
                if (ItemManager.GetItem("Speaker1") is null)
                    return;
                speaker1 = ItemManager.GetItem("Speaker1").gameObject.GetComponent<Speaker>();

                if (speaker1 is null || speaker1._audioSource is null)
                    return;

                gameSource1 = speaker1._audioSource;
                modSource1 = speaker1.gameObject.AddComponent<AudioSource>();
                speaker1._audioSource = modSource1;
                MelonLogger.Msg("prepared first speaker");

                if (ItemManager.GetItem("Speaker2") is null)
                    return;
                speaker2 = ItemManager.GetItem("Speaker2").gameObject.GetComponent<Speaker>();
                if (speaker2 is null || speaker2._audioSource is null)
                    return;

                gameSource2 = speaker2._audioSource;
                modSource2 = speaker2.gameObject.AddComponent<AudioSource>();
                speaker2._audioSource = modSource2;
                MelonLogger.Msg("prepared second speaker");
            }
            if (!gotSpeakers && speaker1 != null && speaker2 != null) gotSpeakers = true;
        }

        private void Initialize()
        {
            //if the storys are downloaded
            if (inGameMain)
            {
                GetSpeakers();

                if (gotSpeakers)
                {
                    if (makeClipsOnStart.Value)
                    {
                        MelonLogger.Msg("opened UI for the first time, loading songs now");

                        MakeAllClips();
                        MelonLogger.Msg("All current songs loaded");
                    }
                    else
                    {
                        MelonLogger.Msg("Songs will be loaded when they are needed");
                    }

                    modSource1.Stop();
                    modSource2.Stop();

                    if (clips.Count == 0) ReloadSongList();

                    modSource1.Stop();
                    modSource2.Stop();

                    ManageSpeakerSettings();

                    Play();

                    MelonLogger.Msg("Audiomod initialized.");
                    if (!CurrentlyPlaying) return;
                }
                MelonLogger.Msg($"vol: {modSource1?.volume ?? 0:0.00} | length {modSource1?.clip?.length ?? 0 / 60:0}m{modSource1?.clip?.length ?? 0 % 60:0.00}s");
            }
        }

        private void LogException(System.Exception e)
        {
#if DEBUG
            MelonLogger.Msg(e.ToString());
#endif
            MelonLogger.Msg($"'{currentSongName}' can't be loaded! (You can retry, give it some time and it might just work again)");
            CurrentlyPlaying = false;
        }

        private void MakeAllClips()
        {
            for (int i = 0; i < clips.Count; i++)
            {
                if (MakeClip(i, out var audio))
                {
                    clips[i] = audio;
                    MelonLogger.Msg($"{clips[i]?.name} {clips[i]?.length}");
                }
            }
        }

        private bool MakeClip(int songIndex, out AudioClip? audio)
        {
            if (songIndex < 0) songIndex = 0;
            if (songIndex >= songs.Count || songIndex < 0) songIndex = 0;
            try
            {
                var uwr = UnityWebRequest.Get($"file://{songs[songIndex]}");
                uwr.SendWebRequest();

                while (!uwr.isDone) ;

                audio = WebRequestWWW.InternalCreateAudioClipUsingDH(uwr.downloadHandler, uwr.url, false, false, AudioType.UNKNOWN);
                if (audio is null)
                    return false;
                audio.name = Path.GetFileNameWithoutExtension(songs[songIndex]);
                return true;
            }
            catch (Exception e)
            {
                LogException(e);
                audio = null;
                return false;
            }
        }

        private void ToggleUI()
        {
            ShowUI = !ShowUI;
        }
    }
}
