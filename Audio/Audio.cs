using HPUI;
using Il2CppEekCharacterEngine;
using Il2CppEekCharacterEngine.Interaction;
using Il2CppEekCharacterEngine.Interface;
using Il2CppEekEvents.Values;
using Il2CppHouseParty;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Audio
{
    public class Audio : MelonMod
    {
        private readonly Dictionary<int, AudioClip> clips = new();
        private readonly List<string> songs = new();
        private int _currentSong = 0;
        private int CurrentSong
        {
            get => _currentSong;
            set
            {
                _currentSong = value;
                currentSongName = clips.ContainsKey(value) ? clips[value].name : "None";
            }
        }

        public float WindowWidth { get; private set; } = 0.22f;
        public float WindowHeight { get; private set; } = 0.09f;

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
        private GameObject? CanvasGO;
        private Canvas? canvas;
        private Text? text;
        private GameObject? shuffleButton;
        private static Color defaultColor = new(0.1f, 0.1f, 0.1f);
        private static Color setColor = new(0.10f, 0.25f, 0.15f);
        private ColorBlock buttonColor = new() { normalColor = defaultColor, highlightedColor = defaultColor * 1.2f, pressedColor = defaultColor * 0.8f, colorMultiplier = 1 };
        private ColorBlock setButtonColor = new() { normalColor = setColor, highlightedColor = setColor * 1.2f, pressedColor = setColor * 0.8f, colorMultiplier = 1 };

        static Audio()
        {
            SetOurResolveHandlerAtFront();
        }

        public Audio()
        {
        }

        private void BuildUI()
        {
            // Canvas
            CanvasGO = new()
            {
                name = "Audiomod UI"
            };
            canvas = CanvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = CanvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _ = CanvasGO.AddComponent<GraphicRaycaster>();

            _ = UIBuilder.CreatePanel("Audiomod UI Container", CanvasGO, new(WindowWidth, WindowHeight), new(0, Screen.height * 0.5f), out GameObject contentHolder);
            text = UIBuilder.CreateLabel(contentHolder, "Audiomod info text", "");
            text.fontSize = 14;

            UIBuilder.SetLayoutGroup<VerticalLayoutGroup>(contentHolder);

            var layout = UIBuilder.CreateUIObject("Control buttons", contentHolder);
            //set lower min height on layout group
            UIBuilder.SetLayoutElement(layout, minHeight: 5, flexibleHeight: 0);
            UIBuilder.SetLayoutGroup<HorizontalLayoutGroup>(layout).spacing = 2f;
            shuffleButton = UIBuilder.CreateButton(layout, "shuffle", "Shuffle", buttonColor);
            UIBuilder.SetLayoutElement(shuffleButton, minHeight: 8, minWidth: P(3), flexibleHeight: 0, flexibleWidth: 0);
            shuffleButton.GetComponent<Button>().colors = buttonColor;
            shuffleButton.GetComponent<Button>().onClick.AddListener(new Action(() => Shuffle()));
            var previousButton = UIBuilder.CreateButton(layout, "previous", "Previous", buttonColor);
            UIBuilder.SetLayoutElement(previousButton, minHeight: 8, minWidth: P(3), flexibleHeight: 0, flexibleWidth: 0);
            previousButton.GetComponent<Button>().colors = buttonColor;
            previousButton.GetComponent<Button>().onClick.AddListener(new Action(() => Previous()));
            var playButton = UIBuilder.CreateButton(layout, "play", "Play", buttonColor);
            UIBuilder.SetLayoutElement(playButton, minHeight: 8, minWidth: P(3), flexibleHeight: 0, flexibleWidth: 0);
            playButton.GetComponent<Button>().colors = buttonColor;
            playButton.GetComponent<Button>().onClick.AddListener(new Action(() => Play()));
            var pauseButton = UIBuilder.CreateButton(layout, "pause", "Pause", buttonColor);
            UIBuilder.SetLayoutElement(pauseButton, minHeight: 8, minWidth: P(3), flexibleHeight: 0, flexibleWidth: 0);
            pauseButton.GetComponent<Button>().colors = buttonColor;
            pauseButton.GetComponent<Button>().onClick.AddListener(new Action(() => Pause()));
            var nextButton = UIBuilder.CreateButton(layout, "next", "Next", buttonColor);
            UIBuilder.SetLayoutElement(nextButton, minHeight: 8, minWidth: P(3), flexibleHeight: 0, flexibleWidth: 0);
            nextButton.GetComponent<Button>().colors = buttonColor;
            nextButton.GetComponent<Button>().onClick.AddListener(new Action(() => Next()));
            var stopButton = UIBuilder.CreateButton(layout, "stop", "Stop", buttonColor);
            UIBuilder.SetLayoutElement(stopButton, minHeight: 8, minWidth: P(3), flexibleHeight: 0, flexibleWidth: 0);
            stopButton.GetComponent<Button>().colors = buttonColor;
            stopButton.GetComponent<Button>().onClick.AddListener(new Action(() => Stop()));
            var returnToGameButton = UIBuilder.CreateButton(contentHolder, "returnToGame", "Return to Game Songs", buttonColor);
            UIBuilder.SetLayoutElement(returnToGameButton, minHeight: 8, minWidth: P(15), flexibleHeight: 0, flexibleWidth: 0);
            returnToGameButton.GetComponent<Button>().colors = buttonColor;
            returnToGameButton.GetComponent<Button>().onClick.AddListener(new Action(() => ReturnToGameMusic()));

            MelonLogger.Msg("created UI");
            //MelonLogger.Msg(shuffleButton.GetComponent<Button>().colors.normalColor.ToString());
        }

        private static int P(int percentage)
        {
            Math.Clamp(percentage, 0, 100);
            var f = (percentage / 100);
            return Screen.width * f;
        }

        private static Assembly AssemblyResolveEventListener(object sender, ResolveEventArgs args)
        {
            if (args is null)
            {
                return null!;
            }
            string name = "Audio.Resources." + args.Name[..args.Name.IndexOf(',')] + ".dll";

            using Stream? str = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (str is not null)
            {
                var context = new AssemblyLoadContext(name, false);
                string path = Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly()?.Location!)!.Parent!.FullName, "UserLibs", args.Name[..args.Name.IndexOf(',')] + ".dll");
                FileStream fstr = new(path, FileMode.Create);
                str.CopyTo(fstr);
                fstr.Close();
                str.Position = 0;

                var asm = context.LoadFromStream(str);
                MelonLogger.Warning($"Loaded {asm.FullName} from our embedded resources, saving to userlibs for next time");

                return asm;
            }
            return null!;
        }

        private static void SetOurResolveHandlerAtFront()
        {
            //MelonLogger.Msg("setting our resolvehandler");
            BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            FieldInfo? field = null;

            Type domainType = typeof(AssemblyLoadContext);

            while (field is null)
            {
                if (domainType is not null)
                {
                    field = domainType.GetField("AssemblyResolve", flags);
                }
                else
                {
                    //MelonDebug.Error("domainType got set to null for the AssemblyResolve event was null");
                    return;
                }
                if (field is null)
                {
                    domainType = domainType.BaseType!;
                }
            }

            var resolveDelegate = (MulticastDelegate)field.GetValue(null)!;
            Delegate[] subscribers = resolveDelegate.GetInvocationList();

            Delegate currentDelegate = resolveDelegate;
            for (int i = 0; i < subscribers.Length; i++)
            {
                currentDelegate = System.Delegate.RemoveAll(currentDelegate, subscribers[i])!;
            }

            var newSubscriptions = new Delegate[subscribers.Length + 1];
            newSubscriptions[0] = (ResolveEventHandler)AssemblyResolveEventListener!;
            System.Array.Copy(subscribers, 0, newSubscriptions, 1, subscribers.Length);

            currentDelegate = Delegate.Combine(newSubscriptions)!;

            field.SetValue(null, currentDelegate);

            //MelonLogger.Msg("set our resolvehandler");
        }

        /// <summary>
        /// Loads and plays the next song from the list
        /// </summary>
        public void Next()
        {
            if (shuffling)
            {
                CurrentSong = UnityEngine.Random.RandomRangeInt(0, songs.Count - 1);
            }
            else
            {
                if (CurrentSong + 1 >= songs.Count)
                {
                    CurrentSong = 0;
                }
                else
                {
                    CurrentSong++;
                }
            }

            Play();
        }

        public override void OnInitializeMelon()
        {
            settings = MelonPreferences.CreateCategory("Audio");
            makeClipsOnStart = settings.CreateEntry("MakeAllClipsOnStart", false);
            autorestartPlaying = settings.CreateEntry("AutoRestartAfterGameLoad", true);
            reloadSongsOnRestart = settings.CreateEntry("ReloadSongsOnRestart", false);

            folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Eek", "House Party", "Mods", "Songs");
            if (!Directory.Exists(folderPath))
            {
                _ = Directory.CreateDirectory(folderPath);
            }

            MelonLogger.Msg("[Audio] Place songs (as .mp3) here: " + folderPath);

            if (makeClipsOnStart.Value)
            {
                MelonLogger.Msg("[Audio] The mod will create all necessary audio ressources on game start, can take some time");
            }
            else
            {
                MelonLogger.Msg("[Audio] Audio ressources are going to be created on the fly, so it will take some time until a song first loads");
            }
        }

        public override void OnGUI()
        {
            try
            {
                if (CanvasGO is null || text is null || canvas is null || shuffleButton is null)
                {
                    return;
                }
                if (!inGameMain)
                {
                    return;
                }

                CanvasGO.SetActive(ShowUI);
                if (!ShowUI)
                {
                    return;
                }

                canvas.scaleFactor = 1.0f;
                if (paused)
                {
                    text.text = "Paused: " + currentSongName;
                }
                else
                {
                    text.text = "Now playing: " + currentSongName;
                    if (shuffling)
                    {
                        shuffleButton.GetComponent<Button>().colors = setButtonColor;
                    }
                    else
                    {
                        shuffleButton.GetComponent<Button>().colors = buttonColor;
                    }
                }
            }
            catch (SEHException e)
            {
                MelonLogger.Error(e.Message);
                MelonLogger.Error(e.StackTrace);
                MelonLogger.Error(e.ErrorCode);
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            //so far only teste for OS
            inGameMain = sceneName == "GameMain" && GameManager.GetActiveStoryName() != string.Empty;
            //MelonLogger.Msg("laoded " + sceneName);
            if (inGameMain)
            {
                ResetAudio();
                Initialize();

                BuildUI();
            }
            else
            {
                ShowUI = false;
                CanvasGO = null!;
            }
        }

        private void ResetAudio()
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

                if (Keyboard.current[Key.Digit3].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed)
                {
                    ToggleUI();

                    if (ShowUI && !gotSpeakers)
                    {
                        Initialize();
                    }
                }

                if (speaker1 is null || speaker2 is null)
                {
                    return;
                }

                if (speaker1._audioSource is null || spoofer is null)
                {
                    return;
                }

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

                    if (GetRemainingSongTime() < 0.2f)
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

        private float GetRemainingSongTime()
        {
            return (speaker1._audioSource.clip.length - speaker1._audioSource.time) / speaker1._audioSource.pitch;
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
            if (!gotSpeakers)
            {
                GetSpeakers();
            }
            else
            {
                if (clips.ContainsKey(CurrentSong) && clips[CurrentSong] != null)
                {
                    speaker1._audioSource.clip = clips[CurrentSong];
                    spoofer.clip = clips[CurrentSong];
                    CurrentlyPlaying = true;
                }
                else
                {
                    if (MakeClip(CurrentSong, out AudioClip? audio))
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
                    {
                        spoofer.Play();
                    }

                    if (paused)
                    {
                        speaker1._audioSource.time = pausedTime;
                        if (ValueStore.GetPlayerValues().GetInt("Music2On") == 1)
                        {
                            spoofer.time = pausedTime;
                        }

                        MelonLogger.Msg("Resuming playback");
                    }
                    else
                    {
                        if (clips[CurrentSong] != null)
                        {
                            MelonLogger.Msg("Now playing: " + CurrentSong + " - " + clips[CurrentSong].name);
                        }
                    }
                    paused = false;
                    stopped = false;
                }
            }
        }

        /// <summary>
        /// Skips to the previous track in the list
        /// </summary>
        public void Previous()
        {
            if (shuffling)
            {
                CurrentSong = UnityEngine.Random.RandomRangeInt(0, songs.Count - 1);
            }
            else
            {
                if (CurrentSong - 1 < 0)
                {
                    CurrentSong = songs.Count - 1;
                }
                else
                {
                    CurrentSong--;
                }
            }
            Play();
        }

        /// <summary>
        /// Reloads the song songIndex
        /// </summary>
        public void ReloadSongList()
        {
            songs.Clear();
            clips.Clear();
            string[] _songs = Directory.GetFiles(folderPath);
            for (int i = 0; i < _songs.Length; i++)
            {
                if (makeClipsOnStart.Value)
                {
                    if (Path.GetExtension(_songs[i]) == ".mp3")
                    {
                        if (MakeClip(i, out AudioClip? audio))
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
                foreach (string songNames in songs)
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
            shuffling = !shuffling;
            if (shuffling)
            {
                CurrentSong = UnityEngine.Random.RandomRangeInt(0, songs.Count - 1);

                Play();
            }
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
                {
                    return;
                }

                speaker1 = ItemManager.GetItem("Speaker1").gameObject.GetComponent<Speaker>();

                if (speaker1 is null)
                {
                    return;
                }

                if (ItemManager.GetItem("Speaker2") is null)
                {
                    return;
                }

                speaker2 = ItemManager.GetItem("Speaker2").gameObject.GetComponent<Speaker>();

                if (speaker2 is null)
                {
                    return;
                }

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
                    if (!CurrentlyPlaying)
                    {
                        return;
                    }
                }
            }
        }

        private void LogException(Exception e)
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
                if (MakeClip(i, out AudioClip? audio))
                {
                    clips[i] = audio!;
                    MelonLogger.Msg($"{clips[i]?.name} {clips[i]?.length}");
                }
            }
        }

        private bool MakeClip(int songIndex, out AudioClip? audio)
        {
            if (songIndex < 0)
            {
                songIndex = 0;
            }

            if (songIndex >= songs.Count || songIndex < 0)
            {
                songIndex = 0;
            }

            try
            {
                UnityWebRequest uwr = UnityWebRequest.Get($"file://{songs[songIndex]}");
                _ = uwr.SendWebRequest();

                while (!uwr.isDone)
                {
                    ;
                }

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

        private void ToggleUI() => ShowUI = !ShowUI;
    }
}
