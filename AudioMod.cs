using EekCharacterEngine.Interaction;
using HouseParty;
using Il2CppSystem.IO;
using Il2CppSystem.Reflection;
using MelonLoader;
using System.Collections.Generic;
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace HPAudio
{
    public class AudioMod : MelonMod
    {
        private readonly List<AudioClip> clips = new List<AudioClip>();
        private readonly Il2CppReferenceArray<GUILayoutOption> opt = new Il2CppReferenceArray<GUILayoutOption>(new GUILayoutOption[] { });
        private readonly List<string> songs = new List<string>();
        private int currentSong = 0;
        private string currentSongName = "None", previousSongName = "None";
        private bool EekPerformedStoryUpdate = false;
        private string folderPath = "";
        private AudioSource gameSource1, gameSource2, modSource1, modSource2;
        private bool gotSpeakers = false;
        private bool inGameMain = false;
        private bool inMainMenu = false;
        private MelonPreferences_Entry<bool> makeClipsOnStart;
        private MelonPreferences_Category settings;
        private bool ShowUI = false;
        private PropertyInfo source = null;
        private Speaker speaker1, speaker2;
        private Rect UIRect = new Rect(10, Screen.height * 0.2f, Screen.width * 0.2f, Screen.height * 0.2f);
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
            modSource1.bypassEffects = true;
            modSource2.bypassEffects = true;
            modSource1.bypassListenerEffects = true;
            modSource2.bypassListenerEffects = true;
            //modSource1.volume = volume;
            //modSource2.volume = volume;
        }

        /// <summary>
        /// Loads and plays the next song from the list
        /// </summary>
        public void Next()
        {
            if (++currentSong >= songs.Count) currentSong = 0;

            previousSongName = currentSongName;
            currentSongName = Path.GetFileNameWithoutExtension(songs[currentSong]);

            if (CurrentlyPlaying) Play();
        }

        public override void OnInitializeMelon()
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
            inMainMenu = sceneName == "MainMenu";
            if (EekPerformedStoryUpdate && inGameMain)
                Initialize();
        }

        public override void OnUpdate()
        {
            if (inGameMain && Keyboard.current[Key.A].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed)
            {
                ToggleUI();
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
            else
            {
                GetSpeakers();
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
                    if (clips[currentSong] != null)
                    {
                        modSource1.clip = clips[currentSong];
                        modSource2.clip = clips[currentSong];
                    }
                    else
                    {
                        clips[currentSong] = MakeClip(currentSong);

                        modSource1.clip = clips[currentSong];
                        modSource2.clip = clips[currentSong];
                    }
                    CurrentlyPlaying = true;
                }
                //Play if everything went fine
                if (CurrentlyPlaying)
                {
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
            if (--currentSong < 0) currentSong = songs.Count - 1;

            previousSongName = currentSongName;
            currentSongName = Path.GetFileNameWithoutExtension(songs[currentSong]);

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
            for (int i = 0; i < _songs.Count; i++)
            {
                if (Path.GetExtension(_songs[i]) == ".mp3")
                {
                    songs.Add(_songs[i]);
                    if (!makeClipsOnStart.Value) clips.Add(MakeClip(i));
                    else clips.Add(null);
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
                source.SetValue(speaker1, gameSource1);
                source.SetValue(speaker2, gameSource2);
                speaker1.StartMusic();
                speaker2.StartMusic();
            }
            else
            {
                GetSpeakers();
            }
        }

        /// <summary>
        /// Randomly selects the next song, then plays that.
        /// </summary>
        public void Shuffle()
        {
            currentSong = Random.RandomRangeInt(0, clips.Count - 1);

            previousSongName = currentSongName;
            currentSongName = Path.GetFileNameWithoutExtension(songs[currentSong]);

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
            }
            else
            {
                GetSpeakers();
            }
        }

        internal void OnStoryFailed(Color color, string s)
        {
            PerformAfterStoryUpdate();
        }

        internal void OnStoryUpdated(bool b1, bool b2)
        {
            PerformAfterStoryUpdate();
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

        private void Initialize()
        {
            //if the storys are downloaded
            if (EekPerformedStoryUpdate && inGameMain)
            {
                GetSpeakers();

                if (gotSpeakers)
                {
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
                //MelonLogger.Msg($"vol: {modSource1.volume:0.00} | length {modSource1.clip.length / 60:0}m{modSource1.clip.length % 60:0.00}s");
            }
            else
            {
                MelonLogger.Msg("Please wait for the game to finish its story file download and extraction");
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
                clips[i] = MakeClip(i);
            }
        }

        private AudioClip MakeClip(int songIndex)
        {
            if (songIndex < 0) songIndex = 0;
            if (songIndex >= songs.Count || songIndex < 0) songIndex = 0;
            try
            {
                var uwr = UnityWebRequest.Get($"file://{songs[songIndex]}");
                uwr.SendWebRequest();

                while (!uwr.isDone) ;

                return WebRequestWWW.InternalCreateAudioClipUsingDH(uwr.downloadHandler, uwr.url, false, false, AudioType.UNKNOWN);
            }
            catch (System.Exception e)
            {
                LogException(e);
                return new AudioClip();
            }
        }

        private void PerformAfterStoryUpdate()
        {
            EekPerformedStoryUpdate = true;

            if (inGameMain)
            {
                Initialize();

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
        }

        private void ToggleUI()
        {
            ShowUI = !ShowUI;
        }
    }
}
