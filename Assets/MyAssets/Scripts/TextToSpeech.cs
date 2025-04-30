using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CognitiveServices.Speech;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace MyAssets.Scripts.Speech
{
    public class TextToSpeech : MonoBehaviour
    {
        public AudioSource audioSource;

        [SerializeField] private string subscriptionKey = "填写你的Azure创建语音服务的密钥";
        [SerializeField] private string region = "填写你的Azure创建语音服务的区域";
        private string soundSetting = "zh-CN-XiaoyiNeural";


        // The frequency is 44100
        private const int SampleRate = 24000;
        private object threadLocker = new object();
        private bool waitingForSpeak;
        private bool audioSourceNeedStop;
        private string message;


        // Voice Configuration
        private SpeechConfig speechConfig;

        // Speech Synthesizer
        private SpeechSynthesizer synthesizer;


        // Character facial animation
        public SkinnedMeshRenderer skinnedMeshRenderer;

        // 39 starts with the pronunciation of aiueo
        public int blendShapeIndex;

        // Mouth animation speed, 180f is more natural
        public float animationSpeed = 180f;

        // Animation range, 99 means 0 to 99
        public float animationRange = 99;

        // The duration of speech synthesis, which will be assigned after each synthesis
        private float audioDuration;

        // Whether the duration of speech synthesis has been set, because the synthesizer.SynthesisCompleted event will be executed later, so you need to wait until the event is completed before playing the audio
        bool isDurationSet = false;

        // Time offset, used to adjust the mouth animation playback, -0.8f is more natural
        public float timeOffset = -0.5f;

        // Set the drop-down UI to switch voice
        [SerializeField] private TMP_Dropdown ChangeLanguageDropdown;

        private void Start()
        {
            // Creating a Voice Configuration
            speechConfig = SpeechConfig.FromSubscription(subscriptionKey, region);
            // Set the speech synthesis output format
            speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw24Khz16BitMonoPcm);
            // Setting the sound
            speechConfig.SpeechSynthesisVoiceName = soundSetting;

            CreatSynthesizer();


            ChangeLanguageDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
        }


        // Creating a Speech Synthesizer
        public void CreatSynthesizer()
        {
            // Create a speech synthesizer.
            // Please make sure to dispose of the synthesizer after use!
            synthesizer = new SpeechSynthesizer(speechConfig, null);
            // Event triggered when speech synthesis is canceled
            synthesizer.SynthesisCanceled += (s, e) =>
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(e.Result);
                Debug.Log(
                    $"CANCELED:\nReason=[{cancellation.Reason}]\nErrorDetails=[{cancellation.ErrorDetails}]\nDid you update the subscription info?");
            };

            // Get the duration of speech synthesis after synthesis is completed
            synthesizer.SynthesisCompleted += (s, e) =>
            {
                var result = e.Result;
                var duration = result.AudioDuration;
                audioDuration = (float)duration.TotalSeconds;
                isDurationSet = true;
            };
        }


        /// <summary>
        /// Speech Synthesis
        /// </summary>
        /// <param name="input">Text to be converted into speech</param>
        public void AzureTextToSpeech(string input)
        {
            // If playing, stop playing
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            // Locking Threads
            lock (threadLocker)
            {
                waitingForSpeak = true;
            }

            string newMessage = null;
            var startTime = DateTime.Now;

            // Starts speech synthesis and returns when synthesis has started.
            using (var result = synthesizer.StartSpeakingTextAsync(input).Result)
            {
                // Native playback is not yet supported on Unity (currently only on Windows/Linux desktop).
                // Using Unity API to play audio is used here as a short term solution.
                // Native playback support will be added in a future release.

                var audioDataStream = AudioDataStream.FromResult(result);

                var isFirstAudioChunk = true;


                // Create an audio clip
                var audioClip = AudioClip.Create(
                    "Speech",
                    SampleRate * 600, // A maximum of 10 minutes of audio can be spoken
                    1,
                    SampleRate,
                    true,
                    (float[] audioChunk) =>
                    {
                        var chunkSize = audioChunk.Length;
                        var audioChunkBytes = new byte[chunkSize * 2];
                        var readBytes = audioDataStream.ReadData(audioChunkBytes);
                        if (isFirstAudioChunk && readBytes > 0)
                        {
                            var endTime = DateTime.Now;
                            var latency = endTime.Subtract(startTime).TotalMilliseconds;
                            newMessage = $"Speech synthesis succeeded!\nLatency: {latency} ms.";
                            isFirstAudioChunk = false;
                        }

                        for (int i = 0; i < chunkSize; ++i)
                        {
                            if (i < readBytes / 2)
                            {
                                audioChunk[i] = (short)(audioChunkBytes[i * 2 + 1] << 8 | audioChunkBytes[i * 2]) /
                                                32768.0F;
                            }
                            else
                            {
                                audioChunk[i] = 0.0f;
                            }
                        }

                        if (readBytes == 0)
                        {
                            Thread.Sleep(200); //Allow some time for the audioSource to finish playing.
                            audioSourceNeedStop = true;
                        }
                    });


                audioSource.clip = audioClip;

                // Since you need to get the duration of speech synthesis, you need to wait until the speech synthesis is finished to get the duration and then play the audio.
                StartCoroutine(WaitUntilTrue());
            }

            lock (threadLocker)
            {
                if (newMessage != null)
                {
                    message = newMessage;
                }

                waitingForSpeak = false;
            }
        }

        //Coroutine for playing mouth animation
        private IEnumerator AnimateValue(float duration)
        {
            float startTime = Time.time;
            float endTime = startTime + duration + timeOffset;

            while (Time.time < endTime)
            {
                float timeSinceStart = Time.time - startTime;
                float range = animationRange;
                float speed = animationSpeed;

                float value = Mathf.PingPong(timeSinceStart * speed, range) + 1f;
                skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, value);
                yield return null;
            }

            skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, 0f);
        }

        private IEnumerator WaitUntilTrue()
        {
            while (!isDurationSet)
            {
                yield return null;
            }

            audioSource.Play();
            StartCoroutine(AnimateValue(audioDuration));
            this.isDurationSet = false;
        }

        // Drop-down bar execution method
        public void OnDropdownValueChanged(int value)
        {
            if (ChangeLanguageDropdown.options[value].text == "中文CHN")
            {
                // To modify a synthesizer, you must first destroy the previous synthesizer and then recreate a new one.
                synthesizer = null;

                soundSetting = "zh-CN-XiaoyiNeural";
                speechConfig.SpeechSynthesisVoiceName = soundSetting;
                synthesizer = new SpeechSynthesizer(speechConfig, null);

                CreatSynthesizer();
            }

            if (ChangeLanguageDropdown.options[value].text == "日语JPN")
            {
                // To modify a synthesizer, you must first destroy the previous synthesizer and then recreate a new one.
                synthesizer = null;

                soundSetting = "ja-JP-MayuNeural";
                speechConfig.SpeechSynthesisVoiceName = soundSetting;
                synthesizer = new SpeechSynthesizer(speechConfig, null);

                CreatSynthesizer();
            }

            if (ChangeLanguageDropdown.options[value].text == "English")
            {
                // To modify a synthesizer, you must first destroy the previous synthesizer and then recreate a new one.
                synthesizer = null;

                soundSetting = "en-US-JaneNeural";
                speechConfig.SpeechSynthesisVoiceName = soundSetting;
                synthesizer = new SpeechSynthesizer(speechConfig, null);

                CreatSynthesizer();
            }
        }
    }
}