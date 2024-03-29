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
        private readonly Dictionary<int, AudioClip> clips = new();
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
                _currentSong = value;
                if (clips.ContainsKey(value))
                {
                    currentSongName = clips[value].name;
                }
                else
                {
                    currentSongName = "None";
                }
            }
        }
        private string currentSongName = "None";
        private string folderPath = "";
        private bool gotSpeakers = false;
        private bool inGameMain = false;
        private MelonPreferences_Entry<bool> makeClipsOnStart = default!;
        private MelonPreferences_Category settings = default!;
        private bool ShowUI = false;
        private Speaker speaker1 = default!, speaker2 = default!;
        private Rect UIRect = new(10, Screen.height * 0.2f, Screen.width * 0.2f, Screen.height * 0.2f);
        public bool CurrentlyPlaying = false;
        private bool shuffling = false;

        /// <summary>
        /// Loads and plays the next song from the list
        /// </summary>
        public void Next()
        {
            shuffling = false;
            if (CurrentSong + 1 >= songs.Count) CurrentSong = 0;
            else CurrentSong++;
            Play();
        }

        public override void OnInitializeMelon()
        {
            settings = MelonPreferences.CreateCategory("AudioMod");
            makeClipsOnStart = settings.CreateEntry("MakeAllClipsOnStart", false);

            folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Eek", "House Party", "Mods", "Songs");
            MelonLogger.Msg("[Audio] Place songs (as .mp3) here: " + folderPath);

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
            if (inGameMain)
            {
                if (Keyboard.current[Key.A].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed)
                {
                    ToggleUI();

                    if (ShowUI && !gotSpeakers)
                    {
                        Initialize();
                    }
                }

                if (speaker1 is null || speaker2 is null) return;
                if (speaker1._audioSource is null || speaker2._audioSource is null) return;

                if (CurrentlyPlaying)
                    if ((speaker1._audioSource.clip.length - speaker1._audioSource.time) / speaker1._audioSource.pitch < 0.2f)
                    {
                        if (shuffling)
                        {
                            Shuffle();
                        }
                        else
                        {
                            Next();
                        }
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
                MelonLogger.Msg($"Paused playback");
                CurrentlyPlaying = false;
                speaker1._audioSource.Pause();
                speaker2._audioSource.Pause();
            }
        }

        /// <summary>
        /// Starts/Resumes playback
        /// </summary>
        public void Play()
        {
            MelonLogger.Msg("Trying to play " + CurrentSong + " - " + currentSongName);
            if (gotSpeakers)
            {
                if (clips.ContainsKey(CurrentSong))
                {
                    speaker1._audioSource.clip = clips[CurrentSong];
                    speaker2._audioSource.clip = clips[CurrentSong];
                    CurrentlyPlaying = true;
                }
                else
                {
                    if (MakeClip(CurrentSong, out var audio))
                    {
                        clips.Add(CurrentSong, audio!);
                        //update name as it hasnt been done yet
                        currentSongName = clips[CurrentSong].name;

                        speaker1._audioSource.clip = clips[CurrentSong];
                        speaker2._audioSource.clip = clips[CurrentSong];
                        CurrentlyPlaying = true;
                    }
                    else
                    {
                        CurrentlyPlaying = false;
                    }
                }
                //Play if everything went fine
                if (CurrentlyPlaying)
                {
                    Stop();
                    speaker1._audioSource.Play();
                    speaker2._audioSource.Play();

                    MelonLogger.Msg("Now playing: " + CurrentSong + " - " + clips[CurrentSong].name);
                }
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
            shuffling = false;
            if (CurrentSong - 1 < 0) CurrentSong = songs.Count - 1;
            else CurrentSong--;
            Play();
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
                if (makeClipsOnStart.Value)
                {
                    if (Path.GetExtension(_songs[i]) == ".mp3")
                    {
                        if (MakeClip(i, out var audio))
                        {
                            songs.Add(_songs[i]);
                            clips.Add(i, audio!);
                        }
                    }
                }
                else
                {
                    songs.Add(_songs[i]);
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
                speaker1.PlayOriginalTrack();
                speaker2.PlayOriginalTrack();
                speaker1.StartMusic();
                speaker2.StartMusic();
            }
        }

        /// <summary>
        /// Randomly selects the next song, then plays that.
        /// </summary>
        public void Shuffle()
        {
            CurrentSong = UnityEngine.Random.RandomRangeInt(0, songs.Count - 1);
            shuffling = true;
            MelonLogger.Msg($"Shuffle enabled");
            Play();
        }

        /// <summary>
        /// Stops playback and resets time to 0
        /// </summary>
        public void Stop()
        {
            if (gotSpeakers)
            {
                MelonLogger.Msg($"Stopped playback");
                CurrentlyPlaying = false;
                speaker1.StopMusic();
                speaker2.StopMusic();
                //speaker2.StopAllCoroutines();
                //speaker1.StopAllCoroutines();
                //MusicManager.Singleton.StopAllCoroutines();//singleton is null
            }
        }

        private void GetSpeakers()
        {
            if (!gotSpeakers)
            {
                if (ItemManager.GetItem("Speaker1") is null)
                    return;
                speaker1 = ItemManager.GetItem("Speaker1").gameObject.GetComponent<Speaker>();

                if (speaker1 is null)
                    return;

                if (ItemManager.GetItem("Speaker2") is null)
                    return;
                speaker2 = ItemManager.GetItem("Speaker2").gameObject.GetComponent<Speaker>();

                if (speaker2 is null)
                    return;

                speaker1._audioSource = speaker1._audioSource;
            }
            if (!gotSpeakers && speaker1 != null && speaker2 != null)
            {
                gotSpeakers = true;
                MelonLogger.Msg("Got game's speakers");
            }
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

                    if (clips.Count == 0) ReloadSongList();

                    Play();

                    MelonLogger.Msg("Audiomod initialized.");
                    if (!CurrentlyPlaying) return;
                }
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
                    clips[i] = audio!;
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
                if (audio is not null)
                {
                    audio.name = Path.GetFileNameWithoutExtension(songs[songIndex]);
                }
            }
            catch (Exception e)
            {
                LogException(e);
                audio = null;
            }
            return audio is not null;
        }

        private void ToggleUI()
        {
            ShowUI = !ShowUI;
        }
    }
}
