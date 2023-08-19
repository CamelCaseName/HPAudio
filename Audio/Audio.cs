using Il2CppEekCharacterEngine;
using Il2CppEekCharacterEngine.Interaction;
using Il2CppEekUI.Items;
using Il2CppHouseParty;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.IO;
using Il2CppSystem.Reflection;
using MelonLoader;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using Color = UnityEngine.Color;
using Object = UnityEngine.Object;

namespace HPAudio
{
    public class Audio : MelonMod
    {
        private readonly List<AudioClip?> clips = new();
        private readonly Il2CppReferenceArray<GUILayoutOption> opt = new(System.Array.Empty<GUILayoutOption>());
        private readonly List<string> songs = new();
        private int currentSong = 0;
        private string currentSongName = "None", previousSongName = "None";
        private string folderPath = "";
        private AudioSource gameSource1 = default!, gameSource2 = default!, modSource1 = default!, modSource2 = default!;
        private bool gotSpeakers = false;
        private bool inGameMain = false;
        private MelonPreferences_Entry<bool> makeClipsOnStart = default!;
        private MelonPreferences_Category settings = default!;
        private bool ShowUI = false;
        private MethodInfo? setSource = null;
        private MethodInfo? getSource = null;
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
                setSource!.Invoke(speaker1, new(new[] { gameSource1 }));
                setSource!.Invoke(speaker2, new(new[] { gameSource2 }));
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

        private void GetSpeakers()
        {
            if (!gotSpeakers)
            {
                MethodInfo itemList = default!;
                foreach (var item in Il2CppType.Of<ItemManager>().GetMethods(BindingFlags.Static | BindingFlags.Public))
                {
                    //MelonLogger.Msg($"{item.Name} {item.ReturnType.FullName}");
                    if (item.ReturnType == Il2CppType.Of<Il2CppSystem.Collections.Generic.List<InteractiveItem>>())
                    {
                        itemList = item;
#if DEBUG
                        MelonLogger.Msg("found " + item.ToString());
#endif
                        break;
                    }
                }
                if (itemList == null) MelonLogger.Error("Itemlist not found, can't get the speakers!");

                foreach (var property in Il2CppType.Of<Speaker>().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    //MelonLogger.Msg($"{property.Name} {property.GetParametersInternal()?[0].Name}");
                    if (property.GetParametersCount() == 1)
                        if (property.GetParametersInternal()[0].ParameterType == Il2CppType.Of<AudioSource>())
                        {
                            setSource = property;
#if DEBUG
                            MelonLogger.Msg("found " + property.ToString());
#endif
                            break;
                        }
                }
                if (setSource == null)
                {
                    MelonLogger.Error("method to set the speakers source not found, cant set our own source!");
                    return;
                }

                foreach (var property in Il2CppType.Of<Speaker>().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    MelonLogger.Msg($"{property.Name} {property.ReturnType.FullName}");
                    if (property.ReturnType == Il2CppType.Of<AudioSource>() && property.GetParametersCount() == 0)
                    {
                        getSource ??= property;
#if DEBUG
                        MelonLogger.Msg("found " + property.ToString());
#endif
                        //break;
                    }
                }
                if (getSource == null)
                {
                    MelonLogger.Error("method to get the speakers source not found, cant get the game's source!");
                    return;
                }

                if (itemList != null)
                    foreach (var item in itemList.Invoke(ItemManager.Singleton, null).Cast<Il2CppSystem.Collections.Generic.List<InteractiveItem>>())
                    {
                        if (item.Name == "Speaker1")
                        {
                            MelonLogger.Msg("got speaker one");
                            speaker1 = item.gameObject.GetComponent<Speaker>();
                            gameSource1 = Object.Instantiate(getSource.Invoke(speaker1, null).Cast<AudioSource>());
                            modSource1 = getSource.Invoke(speaker1, null).Cast<AudioSource>();
                            setSource.Invoke(speaker1, new Il2CppReferenceArray<Il2CppSystem.Object>(new Il2CppSystem.Object[] { modSource1 }));
                            MelonLogger.Msg("prepared speaker one's source");
                        }
                        else if (item.Name == "Speaker2")
                        {
                            MelonLogger.Msg("got speaker two");
                            speaker2 = item.gameObject.GetComponent<Speaker>();
                            gameSource2 = Object.Instantiate(getSource.Invoke(speaker2, null).Cast<AudioSource>());
                            modSource2 = getSource.Invoke(speaker2, null).Cast<AudioSource>();
                            setSource.Invoke(speaker2, new Il2CppReferenceArray<Il2CppSystem.Object>(new Il2CppSystem.Object[] { modSource2 }));
                            MelonLogger.Msg("prepared speaker two's source");
                        }
                    }
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
                clips[i] = MakeClip(i);
                MelonLogger.Msg($"{clips[i]?.name} {clips[i]?.length}");
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

        private void ToggleUI()
        {
            ShowUI = !ShowUI;
        }
    }
}
