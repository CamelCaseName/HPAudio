using EekCharacterEngine.Interaction;
using HouseParty;
using System.Collections.Generic;
using Il2CppSystem.IO;
using MelonLoader;
using NLayer;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace HPAudio
{
    public class AudioMod : MelonMod
    {
        private bool inGameMain = false;
        private Speaker speaker1, speaker2;
        private AudioSource speaker1Source, speaker2Source;
        private readonly List<string> songs = new List<string>();
        private readonly List<AudioClip> clips = new List<AudioClip>();
        private string folderPath;
        private int currentSong = 0;
        public bool CurrentlyPlaying = false;
        private bool gotSpeakers = false;
        private string currentSongName = "None";
        private Rect UIRect = new Rect(10, Screen.height * 0.6f, Screen.width * 0.1f, Screen.height * 0.2f);
        private bool ShowUI = false;
        private bool EekPerformedStoryUpdate = false;

        private MelonPreferences_Category settings;
        private MelonPreferences_Entry<bool> makeClipsOnStart;
        //private Il2CppSystem.Action<Il2CppSystem.Object, Il2CppSystem.IntPtr> OnStoryUpdated;

        public void OnStoryUpdated(bool b1, bool b2)
        {
            EekPerformedStoryUpdate = true;
        }

        public override void OnApplicationStart()
        {
            settings = MelonPreferences.CreateCategory("AudioMod");
            makeClipsOnStart = settings.CreateEntry("MakeAllClipsOnStart", true);

            if (makeClipsOnStart.Value)
            {
                MelonLogger.Msg("The mod will create all necessary audio ressources on game start, can take some time");
                MakeAllClips();
                MelonLogger.Msg("All current songs loaded");
            }
            else
            {
                MelonLogger.Msg("Audio ressources are going to be created on the fly, so it will take some time until a song first loads");
            }
            folderPath = Directory.GetCurrentDirectory() + "\\HouseParty_Data\\Resources\\Songs";
            MelonLogger.Msg("Place songs (as .mp3) here: " + folderPath);
            ReloadSongList();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            inGameMain = sceneName == "GameMain";
            if (inGameMain)
            {
                EekCharacterEngine.StoryManager.add_OnPerformedEekStoryUpdate(new System.Action<bool, bool>(OnStoryUpdated));
            }
        }

        private void MakeAllClips()
        {
            for (int i = 0; i < songs.Count; i++)
            {
                clips.Add(MakeClip(i));
            }
        }

        /// <summary>
        /// Loads and plays the next song from the list
        /// </summary>
        public void Next()
        {
            if (++currentSong >= songs.Count) currentSong = 0;
        }

        /// <summary>
        /// Reloads the song index
        /// </summary>
        public void ReloadSongList()
        {
            songs.Clear();
            var _songs = Directory.GetFiles(folderPath);
            for (int i = 0; i < _songs.Count; i++)
            {
                if (Path.GetExtension(_songs[i]) == ".mp3")
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
                for (int i = 0; i < _songs.Count; i++)
                {
                    MelonLogger.Msg(" - " + _songs[i]);
                }
                return;
            }
        }

        /// <summary>
        /// Pauses song but keeps current time
        /// </summary>
        public void Pause()
        {
            CurrentlyPlaying = false;
        }

        /// <summary>
        /// Starts/Resumes playback
        /// </summary>
        public void Play()
        {
            CurrentlyPlaying = true;
        }

        /// <summary>
        /// Randomly selects the next song, then plays that.
        /// </summary>
        public void Shuffle()
        {

        }

        /// <summary>
        /// Skips to the previous track in the list
        /// </summary>
        public void Previous()
        {
            if (--currentSong < 0) currentSong = songs.Count - 1;
        }

        /// <summary>
        /// Stops playback and resets time to 0
        /// </summary>
        public void Stop()
        {
            CurrentlyPlaying = false;
        }

        /// <summary>
        /// Stops all this overriding and goes back to the game thing as before
        /// </summary>
        public void ReturnToGameMusic()
        {
            CurrentlyPlaying = false;
            speaker1.GBPLLLIBAIO = speaker1Source;
            speaker2.GBPLLLIBAIO = speaker2Source;
            speaker1.StartMusic();
            speaker2.StartMusic();
        }

        private void GetSpeakers()
        {
            if (!gotSpeakers)
                foreach (var item in ItemManager.Singleton.AKDPLOGJEIN)
                {
                    if (item.Name == "Speaker1")
                    {
                        speaker1 = item.gameObject.GetComponent<Speaker>();
                        speaker1Source = Object.Instantiate(speaker1.GBPLLLIBAIO);
                    }
                    else if (item.Name == "Speaker2")
                    {
                        speaker2 = item.gameObject.GetComponent<Speaker>();
                        speaker2Source = Object.Instantiate(speaker2.GBPLLLIBAIO);
                    }
                }
            if (!gotSpeakers && speaker1 != null && speaker2 != null) gotSpeakers = true;
        }

        private void LogException(System.Exception e)
        {
#if DEBUG
            MelonLogger.Msg(e.ToString());
#endif
            MelonLogger.Msg($"'{currentSongName}' can't be loaded! (You can retry, give it some time and it might just work again)");
            CurrentlyPlaying = false;
        }

        private MpegFile LoadSongFromFile(int index = -1)
        {
            if (index < 0) index = currentSong;
            if (index >= songs.Count || index < 0) index = 0;
            //read in mp3 as samples with nlayer
            try
            {
                currentSongName = Path.GetFileNameWithoutExtension(songs[currentSong]);
                return new MpegFile(songs[index]);
            }
            catch (System.Exception e)
            {
                LogException(e);
                return null;
            }
        }

        private AudioClip MakeClip(int songIndex)
        {
            var songfile = LoadSongFromFile(songIndex);
            float[] samples = new float[songfile.Length / sizeof(float)];
            try
            {
                songfile.StereoMode = StereoMode.DownmixToMono;
                songfile.ReadSamples(samples, 0, samples.Length);
                AudioClip clip = AudioClip.Create(currentSongName, samples.Length / songfile.Channels, songfile.Channels, songfile.SampleRate, false);
                clip.SetData(samples, 0);
                return clip;
            }
            catch (System.Exception e)
            {
                LogException(e);
                return new AudioClip();
            }
        }

        private void LoadSongIntoSpeakers()
        {
            AudioClip clip;
            if (clips[currentSong].length > 0) clip = clips[currentSong];
            else clip = MakeClip(currentSong);
            speaker1.GBPLLLIBAIO.clip = clip;
            speaker2.GBPLLLIBAIO.clip = clip;

            CurrentlyPlaying = true;
        }

        public void ManageSpeakerSettings()
        {
            speaker1.GBPLLLIBAIO.time = 0;
            speaker2.GBPLLLIBAIO.time = 0;
            speaker1.GBPLLLIBAIO.priority = 32;
            speaker2.GBPLLLIBAIO.priority = 32;
            speaker1.GBPLLLIBAIO.enabled = true;
            speaker2.GBPLLLIBAIO.enabled = true;
            speaker1.GBPLLLIBAIO.ignoreListenerPause = true;
            speaker2.GBPLLLIBAIO.ignoreListenerPause = true;
            speaker1.GBPLLLIBAIO.mute = false;
            speaker2.GBPLLLIBAIO.mute = false;
            speaker1.GBPLLLIBAIO.bypassEffects = true;
            speaker2.GBPLLLIBAIO.bypassEffects = true;
            speaker1.GBPLLLIBAIO.bypassListenerEffects = true;
            speaker2.GBPLLLIBAIO.bypassListenerEffects = true;
            //speaker1.GBPLLLIBAIO.volume = volume;
            //speaker2.GBPLLLIBAIO.volume = volume;
        }

        private void Initialize()
        {
            //if the storys are downloaded
            if (EekPerformedStoryUpdate)
            {
                GetSpeakers();

                speaker1.GBPLLLIBAIO.Stop();
                speaker2.GBPLLLIBAIO.Stop();

                if (songs.Count < 1) ReloadSongList();

                speaker1.GBPLLLIBAIO.Stop();
                speaker2.GBPLLLIBAIO.Stop();

                ManageSpeakerSettings();

                LoadSongIntoSpeakers();
                if (!CurrentlyPlaying) return;

                speaker1.GBPLLLIBAIO.Play();
                speaker2.GBPLLLIBAIO.Play();

                Next();

                if (CurrentlyPlaying) MelonLogger.Msg("Now playing: " + currentSongName);
                MelonLogger.Msg($"vol: {speaker1.GBPLLLIBAIO.volume:0.00} | length {speaker1.GBPLLLIBAIO.clip.length / 60:0}m{speaker1.GBPLLLIBAIO.clip.length % 60:0.00}s");
            }
            else
            {
                MelonLogger.Msg("Please wait for the game to finish its story file download and extraction");
            }
        }

        private void ToggleUI()
        {
            ShowUI = !ShowUI;
            if (ShowUI)
            {
                GUILayout.BeginArea(UIRect);
                GUILayout.EndArea();
            }
        }

        public override void OnUpdate()
        {
            if (inGameMain && Keyboard.current[Key.I].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed)
            {
                Initialize();
            }
            if (inGameMain && Keyboard.current[Key.A].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed)
            {
                ToggleUI();
            }
        }
    }
}
