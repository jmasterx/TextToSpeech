using Microsoft.VisualBasic.FileIO;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading;

using System.Windows.Forms;

namespace VoiceRecorder
{
    public partial class Form1 : Form
    {
        private string CurrentFile = "test.wav";
        private string Normalized = "Normalized";
        private string Stereo = "Stereo";
        private string Raw = "Raw";
        private string Trimmed = "Trimmed";
        public Form1()
        {
            InitializeComponent();
        }

        public WasapiCapture capture;
        public WaveFileWriter waveFile = null;

        void waveSource_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (waveFile != null)
            {
                waveFile.Write(e.Buffer, 0, e.BytesRecorded);
                waveFile.Flush();
            }
        }

        void waveSource_RecordingStopped(object sender, StoppedEventArgs e)
        {
            if (capture != null)
            {
                capture.Dispose();
                capture = null;
            }

            if (waveFile != null)
            {
                waveFile.Dispose();
                waveFile = null;
            }

            StereoToMono();
            Normalize();
            StripSilence();

            btnRecord.Enabled = true;
        }

        private void BtnRecord_Click(object sender, EventArgs e)
        {
            if (CurrentFile == null)
                return;

            File.Delete($"{Stereo}/{CurrentFile}");
            File.Delete($"{Raw}/{CurrentFile}");
            File.Delete($"{Trimmed}/{CurrentFile}");
            File.Delete($"{Normalized}/{CurrentFile}");

            capture = new WasapiCapture();
            btnRecord.Enabled = false;
            btnStop.Enabled = true;

            capture.DataAvailable += new EventHandler<WaveInEventArgs>(waveSource_DataAvailable);
            capture.RecordingStopped += new EventHandler<StoppedEventArgs>(waveSource_RecordingStopped);

            waveFile = new WaveFileWriter($"{Stereo}/{CurrentFile}", capture.WaveFormat);

            capture.StartRecording();
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            btnStop.Enabled = false;

            capture.StopRecording();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var sox = ConfigurationSettings.AppSettings.Get("Sox");

            if (!File.Exists(sox))
            {
                MessageBox.Show(
                    $"{sox} was not found and is required to trim silence. Get it from https://sourceforge.net/projects/sox/ and update VoiceRecorder.exe.config to point to its installation folder.");
            }
            btnStop.Enabled = false;
            Directory.CreateDirectory(Trimmed);
            Directory.CreateDirectory(Raw);
            Directory.CreateDirectory(Normalized);
            Directory.CreateDirectory(Stereo);

            listBox1.SelectedIndexChanged += (send, args) =>
            {
                CurrentFile = ((WorkItem)listBox1.SelectedItem).Filename;
                textBox1.Text = ((WorkItem)listBox1.SelectedItem).Text;
            };

            using (TextFieldParser parser = new TextFieldParser(@"Data\metadata.csv"))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters("|");
                parser.HasFieldsEnclosedInQuotes = true;
                while (!parser.EndOfData)
                {
                    //Processing row
                    string[] fields = parser.ReadFields();
                    var item = new WorkItem
                    {
                        Filename = fields[0] + ".wav",
                        Text = fields[1],
                    };

                    listBox1.Items.Add(item);

                    if(File.Exists($"{Stereo}/{item.Filename}"))
                    {
                        listBox1.SelectedItem = item;
                    }
                }
            }
        }

        public void StereoToMono()
        {
            var inPath = $"{Stereo}/{CurrentFile}";
            var outPath = $"{Raw}/{CurrentFile}";
            using (var inputReader = new AudioFileReader(inPath))
            {
                // convert our stereo ISampleProvider to mono
                var mono = new StereoToMonoSampleProvider(inputReader);
                mono.LeftVolume = 1.0f; // discard the left channel
                mono.RightVolume = 0.0f; // keep the right channel

                // ... OR ... could write the mono audio out to a WAV file
                WaveFileWriter.CreateWaveFile16(outPath, mono);
            }
        }

        private void Normalize()
        {
            var inPath = $"{Raw}/{CurrentFile}";
            var outPath = $"{Normalized}/{CurrentFile}";
            float max = 0;

            using (var reader = new AudioFileReader(inPath))
            {
                // find the max peak
                float[] buffer = new float[reader.WaveFormat.SampleRate];
                int read;
                do
                {
                    read = reader.Read(buffer, 0, buffer.Length);
                    for (int n = 0; n < read; n++)
                    {
                        var abs = Math.Abs(buffer[n]);
                        if (abs > max) max = abs;
                    }
                } while (read > 0);
                Console.WriteLine($"Max sample value: {max}");

                if (max == 0 || max > 1.0f)
                    throw new InvalidOperationException("File cannot be normalized");

                // rewind and amplify
                reader.Position = 0;
                reader.Volume = 1.0f / max;

                // write out to a new WAV file
                WaveFileWriter.CreateWaveFile16(outPath, reader);
            }
        }

        private void StripSilence()
        {
            var inPath = $"{Normalized}/{CurrentFile}";
            var outPath = $"{Trimmed}/{CurrentFile}";
            var startInfo = new ProcessStartInfo();
            startInfo.FileName = ConfigurationSettings.AppSettings.Get("Sox");
            startInfo.Arguments = ConfigurationSettings.AppSettings.Get("SoxArgs").Replace("inPath", inPath).Replace("outPath", outPath);
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = false;
            startInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
            using (Process soxProc = Process.Start(startInfo))
            {
                soxProc.WaitForExit();
            }
        }

        private void BtnPlay_Click(object sender, EventArgs e)
        {
            if(!File.Exists($"{Trimmed}/{CurrentFile}"))
            {
                MessageBox.Show("No recording");
                return;
            }

            btnPlay.Enabled = false;
            using (WaveStream mainOutputStream = new WaveFileReader($"{Trimmed}/{CurrentFile}"))
            {
                using (WaveChannel32 volumeStream = new WaveChannel32(mainOutputStream))
                {
                    volumeStream.PadWithZeroes = false;

                    using (WaveOutEvent player = new WaveOutEvent())
                    {
                        player.Init(volumeStream);

                        player.Play();
                        while(player.PlaybackState == PlaybackState.Playing)
                        {
                            Thread.Sleep(1);
                        }
                        player.Dispose();
                    }
                }
            }
            btnPlay.Enabled = true;
        }

        private void BtnNext_Click(object sender, EventArgs e)
        {
            listBox1.SelectedIndex++;
        }
    }
}
