using EekCharacterEngine.Interaction;
using HouseParty;
using System.Collections.Generic;
using Il2CppSystem.IO;
using MelonLoader;
using NLayer;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;
using Il2CppSystem.Reflection;
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;
using System.Diagnostics;
using Il2CppSystem.Diagnostics.Tracing;
using Il2CppMono.Unity;
using Mono.Cecil.Cil;
using EekUI.Support;

namespace HPAudio
{
    public class AudioMod : MelonMod
    {
        private bool inGameMain = false;
        private bool inMainMenu = false;
        private Speaker speaker1, speaker2;
        private AudioSource gameSource1, gameSource2, modSource1, modSource2;
        private readonly List<string> songs = new List<string>();
        private readonly List<AudioClip> clips = new List<AudioClip>();
        private string folderPath = "";
        private int currentSong = 0;
        public bool CurrentlyPlaying = false;
        private bool gotSpeakers = false;
        private string currentSongName = "None";
        private Rect UIRect = new Rect(10, Screen.height * 0.6f, Screen.width * 0.1f, Screen.height * 0.2f);
        private bool ShowUI = false;
        private bool EekPerformedStoryUpdate = false;
        private PropertyInfo source = null;

        private MelonPreferences_Category settings;
        private MelonPreferences_Entry<bool> makeClipsOnStart;

        public void OnStoryUpdated(bool b1, bool b2)
        {
            PerformAfterStoryUpdate();
        }

        public void OnStoryFailed(Color color, string s)
        {
            PerformAfterStoryUpdate();
        }

        private void PerformAfterStoryUpdate()
        {
            EekPerformedStoryUpdate = true;
            if (makeClipsOnStart.Value && !inMainMenu)
            {
                MelonLogger.Msg("Game finished downloading and extracting Stories, loading songs now");

                MakeAllClips();
                MelonLogger.Msg("All current songs loaded");
            }
            else
            {
                MelonLogger.Msg("Game finished downloading and extracting Stories.");
            }
        }

        public override void OnApplicationStart()
        {
            settings = MelonPreferences.CreateCategory("AudioMod");
            makeClipsOnStart = settings.CreateEntry("MakeAllClipsOnStart", true);

            folderPath = Directory.GetCurrentDirectory() + "\\HouseParty_Data\\Resources\\Songs";
            MelonLogger.Msg("Place songs (as .mp3) here: " + folderPath);
            ReloadSongList();

            if (makeClipsOnStart.Value)
                MelonLogger.Msg("The mod will create all necessary audio ressources on game start (as soon as the game has updated its story), can take some time");
            else
                MelonLogger.Msg("Audio ressources are going to be created on the fly, so it will take some time until a song first loads");


            EekCharacterEngine.StoryManager.add_OnPerformedEekStoryUpdate(new System.Action<bool, bool>(OnStoryUpdated));
            EekCharacterEngine.StoryManager.add_OnFailedEekStoryUpdate(new System.Action<Color, string>(OnStoryFailed));
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            inGameMain = sceneName == "GameMain";
            inMainMenu = sceneName == "MainMenu";
        }

        private void MakeAllClips()
        {
            for (int i = 0; i < songs.Count; i++)
            {
                currentSong = i;
                clips.Add(MakeClip(i));
            }
            currentSong = 0;
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
                    clips.Add(null);
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
            source.SetValue(speaker1, gameSource1);
            source.SetValue(speaker2, gameSource2);
            speaker1.StartMusic();
            speaker2.StartMusic();
        }

        private void GetSpeakers()
        {
            if (!gotSpeakers)
            {
                PropertyInfo itemList = null;
                foreach (var item in Il2CppType.Of<ItemManager>().GetProperties(BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.GetField | BindingFlags.Public))
                {
                    if (item.PropertyType == Il2CppType.Of<Il2CppSystem.Collections.Generic.List<InteractiveItem>>())
                    {
                        itemList = item;
#if DEBUG
                        MelonLogger.Msg("found " + item.ToString());
#endif
                        break;
                    }
                }
                if (itemList == null) return;

                foreach (var property in Il2CppType.Of<Speaker>().GetProperties(BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.GetField | BindingFlags.SetProperty | BindingFlags.SetField | BindingFlags.Public))
                {
                    if (property.PropertyType == Il2CppType.Of<AudioSource>())
                    {
                        source = property;
#if DEBUG
                        MelonLogger.Msg("found " + property.ToString());
#endif
                        break;
                    }
                }
                if (source == null) return;

                foreach (var item in itemList.GetValue(ItemManager.Singleton).Cast<Il2CppSystem.Collections.Generic.List<InteractiveItem>>())
                {
                    if (item.Name == "Speaker1")
                    {
                        speaker1 = item.gameObject.GetComponent<Speaker>();
                        gameSource1 = Object.Instantiate(source.GetValue(speaker1).Cast<AudioSource>());
                        modSource1 = source.GetValue(speaker1).Cast<AudioSource>();
                        source.SetValue(speaker1, modSource1);
                    }
                    else if (item.Name == "Speaker2")
                    {
                        speaker2 = item.gameObject.GetComponent<Speaker>();
                        gameSource2 = Object.Instantiate(source.GetValue(speaker2).Cast<AudioSource>());
                        modSource2 = source.GetValue(speaker2).Cast<AudioSource>();
                        source.SetValue(speaker2, modSource2);
                    }
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
                songfile.ReadSamples(samples, 0, samples.Length);

#if DEBUG
                MelonLogger.Msg($"{Path.GetFileNameWithoutExtension(songs[songIndex])} samples are {samples.Length} floats({songfile.Length} PCM bytes) long at {songfile.Channels} channels and {songfile.SampleRate}hz");
#endif
                AudioClip clip = AudioClip.Create(currentSongName, samples.Length, songfile.Channels, songfile.SampleRate, false);
#if DEBUG
                MelonLogger.Msg($"created audiofile without data is {clip.length} seconds long with {clip.samples} samples in {clip.channels} channels and {clip.frequency}hz");
                MelonLogger.Msg($"other values (isReadyToPlay : {clip.isReadyToPlay}) (preLoadAudioData : {clip.preloadAudioData}) (loadState: {clip.loadState}) (loadInBackground : {clip.loadInBackground}) (loadType : {clip.loadType})");
#endif
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
            if (currentSong < clips.Count)
            {
                currentSongName = Path.GetFileNameWithoutExtension(songs[currentSong]);
                if (clips[currentSong] != null)
                {
                    modSource1.clip = clips[currentSong];
                    modSource2.clip = clips[currentSong];
                }
                else
                {
#if DEBUG
                    MelonLogger.Msg($"{currentSongName} is null, loading again");
#endif
                    AudioClip clip;
                    clip = MakeClip(currentSong);

                    modSource1.clip = clip;
                    modSource2.clip = clip;
                }

                CurrentlyPlaying = true;
            }
            else
            {
                currentSong = 0;
            }
        }

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
            modSource1.bypassEffects = true;
            modSource2.bypassEffects = true;
            modSource1.bypassListenerEffects = true;
            modSource2.bypassListenerEffects = true;
            //modSource1.volume = volume;
            //modSource2.volume = volume;
        }

        private void Initialize()
        {
            //if the storys are downloaded
            if (EekPerformedStoryUpdate)
            {
                GetSpeakers();

                modSource1.Stop();
                modSource2.Stop();

                if (songs.Count < 1) ReloadSongList();

                modSource1.Stop();
                modSource2.Stop();

                ManageSpeakerSettings();

                LoadSongIntoSpeakers();
                if (!CurrentlyPlaying) return;

                modSource1.Play();
                modSource2.Play();

                Next();

                if (CurrentlyPlaying) MelonLogger.Msg("Now playing: " + currentSongName);
                MelonLogger.Msg($"vol: {modSource1.volume:0.00} | length {modSource1.clip.length / 60:0}m{modSource1.clip.length % 60:0.00}s");
            }
            else
            {
                MelonLogger.Msg("Please wait for the game to finish its story file download and extraction");
            }
        }

        private void ToggleUI()
        {
            ShowUI = !ShowUI;
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

            if (Keyboard.current[Key.D].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed)
            {
                var songfile = LoadSongFromFile(0);
                float[] samples = new float[songfile.Length / sizeof(float)];
                songfile.ReadSamples(samples, 0, samples.Length);

                MelonLogger.Msg($"{Path.GetFileNameWithoutExtension(songs[0])} samples are {samples.Length} floats({songfile.Length} PCM bytes) long at {songfile.Channels} channels and {songfile.SampleRate}hz");
                AudioClip clip = AudioClip.Create(currentSongName, samples.Length, songfile.Channels, songfile.SampleRate, false);

                MelonLogger.Msg($"created audiofile without data is {clip.length} seconds long with {clip.samples} samples in {clip.channels} channels and {clip.frequency}hz");
                MelonLogger.Msg($"other values (isReadyToPlay : {clip.isReadyToPlay}) (preLoadAudioData : {clip.preloadAudioData}) (loadState: {clip.loadState}) (loadInBackground : {clip.loadInBackground}) (loadType : {clip.loadType})");
                clip.SetData(samples, 0);
            }
        }

        public override void OnGUI()
        {
            if (ShowUI)
            {
                GUILayout.BeginArea(UIRect);
                GUILayout.EndArea();
            }
        }

    }
}
