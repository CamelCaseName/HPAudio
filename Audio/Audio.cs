using Il2CppEekCharacterEngine;
using Il2CppEekCharacterEngine.Interaction;
using Il2CppEekCharacterEngine.Interface;
using Il2CppEekEvents.Values;
using Il2CppHouseParty;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
        private AudioSource spoofer = null!;
        private bool gotSpeakers = false;
        private bool inGameMain = false;
        private bool ShowUI = false;
        private bool shuffling = false;
        private float pausedTime = 0;
        private int AllMusicOff = 0;
        private int Music1Off = 0;
        private int Music2On = 0;
        private MelonPreferences_Category settings = default!;
        private MelonPreferences_Entry<bool> makeClipsOnStart = default!;
        private MelonPreferences_Entry<bool> autorestartPlaying = default!;
        private MelonPreferences_Entry<bool> reloadSongsOnRestart = default!;
        private Rect UIRect = new(10, Screen.height * 0.2f, Screen.width * 0.2f, Screen.height * 0.2f);
        private Speaker speaker1 = default!, speaker2 = default!;
        private string currentSongName = "None";
        private string folderPath = "";
        public bool CurrentlyPlaying = false;
        public bool paused = false;
        public bool stopped = false;
        private bool fadedIn = false;
        private int reloadCount = 0;

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
            autorestartPlaying = settings.CreateEntry("AutoRestartAfterGameLoad", true);
            reloadSongsOnRestart = settings.CreateEntry("ReloadSongsOnRestart", false);

            folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Eek", "House Party", "Mods", "Songs");
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
            MelonLogger.Msg("[Audio] Place songs (as .mp3) here: " + folderPath);

            if (makeClipsOnStart.Value)
                MelonLogger.Msg("[Audio] The mod will create all necessary audio ressources on game start, can take some time");
            else
                MelonLogger.Msg("[Audio] Audio ressources are going to be created on the fly, so it will take some time until a song first loads");
        }

        public override void OnGUI()
        {
            //todo redo ui at some point
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
                if (!paused && !stopped)
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
            //so far only teste for OS
            //todo test for other stories
            inGameMain = sceneName == "GameMain" && GameManager.GetActiveStoryName() == "Original Story";
            //MelonLogger.Msg("laoded " + sceneName);
            if (inGameMain)
            {
                ResetAudioMod();
                Initialize();
            }
            else
            {
                ShowUI = false;
            }
        }

        private void ResetAudioMod()
        {
            spoofer = null!;
            speaker1 = null!;
            speaker2 = null!;
            gotSpeakers = false;
            CurrentlyPlaying = false;
            fadedIn = false;
            ShowUI = false;
            shuffling = false;
            pausedTime = 0;
            AllMusicOff = 0;
            Music1Off = 0;
            Music2On = 0;
            paused = false;
            stopped = false;
        }

        public override void OnUpdate()
        {
            if (inGameMain)
            {
                if (autorestartPlaying.Value)
                {
                    if (!fadedIn && !ScreenFade.Singleton.IsFadeVisible)
                    {
                        ToggleUI();

                        if (ShowUI && !gotSpeakers)
                        {
                            Initialize();
                        }
                        fadedIn = true;
                    }
                }

                if (Keyboard.current[Key.A].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed)
                {
                    ToggleUI();

                    if (ShowUI && !gotSpeakers)
                    {
                        Initialize();
                    }
                }

                if (speaker1 is null || speaker2 is null) return;
                if (speaker1._audioSource is null || spoofer is null) return;

                if (CurrentlyPlaying)
                {
                    if (speaker1._audioSource.clip != spoofer.clip && speaker1._audioSource.clip.name.Contains("Track"))
                    {
                        if (spoofer.isPlaying)
                        {
                            speaker1._audioSource.clip = spoofer.clip;
                            speaker1._audioSource.time = spoofer.time;
                        }
                        else
                        {
                            Play();
                        }
                    }

                    //speaker 2 starts and stops itself and stops all
                    //the should play is a bit f'd cuz it also gets set when we change the clip
                    if (Music2On == 0 && ValueStore.GetPlayerValues().GetInt("Music2On") == 1)
                    {
                        //only join if we are playing our clip, if we are playing a game clip we restart
                        if (speaker1._audioSource.clip == clips[CurrentSong])
                        {
                            speaker2.StopMusic();
                            spoofer.Play();
                            spoofer.time = speaker1._audioSource.time;
                        }
                        else
                        {
                            Play();
                        }
                    }
                    else if (Music2On == 1 && ValueStore.GetPlayerValues().GetInt("Music2On") == 0)
                    {
                        spoofer.Stop();
                    }

                    //speaker1 starts all
                    if (Music1Off == 1 && ValueStore.GetPlayerValues().GetInt("Music1Off") == 0)
                    {
                        if (spoofer.isPlaying)
                        {
                            speaker2.StopMusic();
                            speaker1.StopMusic();
                            speaker1._audioSource.clip = clips[CurrentSong];
                            speaker1._audioSource.Play();
                            speaker1._audioSource.time = spoofer.time;
                        }
                        else
                        {
                            Play();
                        }
                    }
                    //speaker 1 stops all
                    else if (Music1Off == 0 && ValueStore.GetPlayerValues().GetInt("Music1Off") == 1)
                    {
                        Stop();
                    }

                    if (AllMusicOff == 0 && ValueStore.GetPlayerValues().GetInt("AllMusicOff") == 1)
                    {
                        Stop();
                    }

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

                    SyncSpoofer();

                    if (speaker2._audioSource.isPlaying && spoofer.isPlaying)
                    {
                        spoofer.Stop();
                        CurrentlyPlaying = false;
                        currentSongName = "None";
                    }
                }
                else
                {
                    if (speaker2._audioSource.isPlaying && spoofer.isPlaying)
                    {
                        spoofer.Stop();
                    }
                }

                Music2On = ValueStore.GetPlayerValues().GetInt("Music2On");
                Music1Off = ValueStore.GetPlayerValues().GetInt("Music1Off");
                AllMusicOff = ValueStore.GetPlayerValues().GetInt("AllMusicOff");
            }
        }

        /// <summary>
        /// Pauses song but keeps current time
        /// </summary>
        public void Pause()
        {
            if (gotSpeakers)
            {
                paused = true;
                MelonLogger.Msg($"Paused playback");
                pausedTime = spoofer.isPlaying ? spoofer.time : speaker1._audioSource.time;
                Stop();
            }
        }

        /// <summary>
        /// Starts/Resumes playback
        /// </summary>
        public void Play()
        {
            //MelonLogger.Msg("Trying to play " + CurrentSong + " - " + currentSongName);
            if (gotSpeakers)
            {
                if (clips.ContainsKey(CurrentSong) && clips[CurrentSong] != null)
                {
                    speaker1._audioSource.clip = clips[CurrentSong];
                    spoofer.clip = clips[CurrentSong];
                    CurrentlyPlaying = true;
                }
                else
                {
                    if (MakeClip(CurrentSong, out var audio))
                    {
                        clips[CurrentSong] = audio!;
                        //update name as it hasnt been done yet
                        currentSongName = clips[CurrentSong].name;

                        speaker1._audioSource.clip = clips[CurrentSong];
                        spoofer.clip = clips[CurrentSong];
                        CurrentlyPlaying = true;
                    }
                    else
                    {
                        CurrentlyPlaying = false;
                        ReturnToGameMusic();
                    }
                }

                //Play if everything went fine
                if (CurrentlyPlaying)
                {
                    speaker2.StopMusic();
                    speaker1._audioSource.Play();
                    if (ValueStore.GetPlayerValues().GetInt("Music2On") == 1)
                        spoofer.Play();

                    if (paused)
                    {
                        speaker1._audioSource.time = pausedTime;
                        if (ValueStore.GetPlayerValues().GetInt("Music2On") == 1)
                            spoofer.time = pausedTime;
                        MelonLogger.Msg("Resuming playback");
                    }
                    else
                    {
                        if (clips[CurrentSong] != null)
                            MelonLogger.Msg("Now playing: " + CurrentSong + " - " + clips[CurrentSong].name);
                    }
                    paused = false;
                    stopped = false;
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
                            clips[i] = audio!;
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
                stopped = true;
                spoofer.Stop();
                speaker1.PlayOriginalTrack();
                speaker1.StartMusic();
                speaker2.PlayOriginalTrack();
                speaker2.StartMusic();
                MelonLogger.Msg($"Playing game music");
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
                speaker1.StopMusic();
                speaker2.StopMusic();
                spoofer.Stop();
                stopped = true;
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

                //audiosource to spoof stuff
                spoofer = speaker2.gameObject.AddComponent<AudioSource>();
                SyncSpoofer();
                spoofer.outputAudioMixerGroup = speaker2._audioSource.outputAudioMixerGroup;
                spoofer.ignoreListenerPause = speaker2._audioSource.ignoreListenerPause;
                spoofer.ignoreListenerVolume = speaker2._audioSource.ignoreListenerVolume;
                spoofer.spatialBlend = speaker2._audioSource.spatialBlend;
                spoofer.minDistance = speaker2._audioSource.minDistance;
                spoofer.maxDistance = speaker2._audioSource.maxDistance;
                spoofer.bypassEffects = speaker2._audioSource.bypassEffects;
                spoofer.bypassListenerEffects = speaker2._audioSource.bypassListenerEffects;
                spoofer.bypassReverbZones = speaker2._audioSource.bypassReverbZones;
                spoofer.playOnAwake = false;
            }
            if (!gotSpeakers && speaker1 != null && speaker2 != null)
            {
                gotSpeakers = true;
                MelonLogger.Msg("Got game's speakers");
            }
        }

        private void SyncSpoofer()
        {
            spoofer.volume = speaker2._audioSource.volume;
            spoofer.priority = speaker2._audioSource.priority;
            spoofer.enabled = speaker2._audioSource.enabled;
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

                    if (reloadSongsOnRestart.Value || reloadCount == 0)
                    {
                        ReloadSongList();
                        reloadCount++;
                    }

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
