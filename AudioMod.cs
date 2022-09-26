using Il2CppSystem.Collections.Generic;
using EekCharacterEngine;
using EekCharacterEngine.Interaction;
using Il2CppSystem.Net;
using UnityEngine.InputSystem;
using MelonLoader;
using Object = UnityEngine.GameObject;
using NLayer;
using HouseParty;
using UnityEngine;
using Il2CppSystem.IO;
using UnityEngine.Networking;
using Il2CppSystem;

namespace HPAudio
{
    public class AudioMod : MelonMod
    {
        private bool inGameMain = false;
        private Speaker speaker1, speaker2;
        private AudioSource speaker1Source, speaker2Source;
        private readonly List<string> songs = new List<string>();
        private string folderPath;
        private int currentSong = 0;
        public bool CurrentlyPlaying = false;
        private bool gotSpeakers = false;
        private string currentSongName = "None";

        public override void OnApplicationStart()
        {
            folderPath = Directory.GetCurrentDirectory() + "\\HouseParty_Data\\Resources\\Songs";
            MelonLogger.Msg("Place songs (as .mp3) here: " + folderPath);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            inGameMain = sceneName == "GameMain";
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
        /// Loads the given song into the speakers clip
        /// </summary>
        /// <param name="path">path to the mp3 file</param>
        public void LoadSong(string path)
        {

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
            MelonLogger.Msg($"'{currentSongName}' can't be loaded!");
        }

        private MpegFile LoadSongFromFile()
        {
            //read in mp3 as samples with nlayer
            try
            {
                currentSongName = Path.GetFileNameWithoutExtension(songs[currentSong]);
                return new MpegFile(songs[currentSong]);
            }
            catch (System.Exception e)
            {
                LogException(e);
                return null;
            }
        }

        private void LoadSongIntoSpeakers(MpegFile songfile)
        {
            float[] samples = new float[songfile.Length / sizeof(float)];
            try
            {
                songfile.ReadSamples(samples, 0, samples.Length);
                AudioClip clip = AudioClip.Create(currentSongName, samples.Length / songfile.Channels, songfile.Channels, songfile.SampleRate, false);
                clip.SetData(samples, 1);
                speaker1.GBPLLLIBAIO.clip = clip;
                speaker2.GBPLLLIBAIO.clip = clip;
            }
            catch (System.Runtime.CompilerServices.RuntimeWrappedException e)
            {
                LogException(e.InnerException);
                return;
            }
        }

        public void ManageSpeakerSettings()
        {
            speaker1.GBPLLLIBAIO.enabled = true;
            speaker2.GBPLLLIBAIO.enabled = true;
            speaker1.GBPLLLIBAIO.ignoreListenerPause = true;
            speaker2.GBPLLLIBAIO.ignoreListenerPause = true;
            speaker1.GBPLLLIBAIO.mute = false;
            speaker2.GBPLLLIBAIO.mute = false;
            speaker1.GBPLLLIBAIO.Play();
            speaker2.GBPLLLIBAIO.Play();
        }

        public override void OnUpdate()
        {
            if (inGameMain && Keyboard.current[Key.D].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed)
            {
                GetSpeakers();

                speaker1.StopMusic();
                speaker2.StopMusic();

                if(songs.Count < 1) ReloadSongList();

                var songfile = LoadSongFromFile();

                Next();

                speaker1.GBPLLLIBAIO.Stop();
                speaker2.GBPLLLIBAIO.Stop();

                LoadSongIntoSpeakers(songfile);

                ManageSpeakerSettings();

                MelonLogger.Msg("Now playing: " + currentSongName);


                MelonLogger.Msg(speaker1.GBPLLLIBAIO.volume);
                MelonLogger.Msg(speaker1.GBPLLLIBAIO.pitch);
                MelonLogger.Msg(speaker1.GBPLLLIBAIO.clip.length);
                MelonLogger.Msg(speaker1.GBPLLLIBAIO.isPlaying);
                MelonLogger.Msg(speaker1.GBPLLLIBAIO.isActiveAndEnabled);
            }
        }
    }
}
