using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Xaml.Behaviors.Core;
using Prism.Mvvm;

namespace DurationHelper
{
    public class DurationViewModel : BindableBase
    {
        private int _tempo = 100;
        private string _text;
        private ObservableCollection<WordViewModel> _words;
        private bool _isGenerateWordsCommandEnabled = true;
        private ActionCommand _generateWordsCommand;
        private ActionCommand _generateCommand;
        private bool _isGenerateCommandEnabled = true;

        private NoteViewModel Whole { get; } = new NoteViewModel("1/1", 4.0);
        private NoteViewModel Half { get; } = new NoteViewModel("1/2", 2.0);
        private NoteViewModel Quarter { get; } = new NoteViewModel("1/4", 1.0);
        private NoteViewModel Eighth { get; } = new NoteViewModel("1/8", 0.5);
        private NoteViewModel Sixteenth { get; } = new NoteViewModel("1/16", 0.25);
        private NoteViewModel ThirtySecond { get; } = new NoteViewModel("1/32", 0.125);

        public ObservableCollection<NoteViewModel> Notes { get; }

        public ObservableCollection<WordViewModel> Words
        {
            get => _words;
            set => SetProperty(ref _words, value);
        }

        public int Tempo
        {
            get => _tempo;
            set => SetProperty(ref _tempo, value);
        }

        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public DurationViewModel()
        {
            Notes = new ObservableCollection<NoteViewModel>(new List<NoteViewModel>()
            {
                Whole,
                Half,
                Quarter,
                Eighth,
                Sixteenth,
                ThirtySecond
            });

            GenerateWordsCommand = new ActionCommand(GenerateWordsAsync);
            GenerateCommand = new ActionCommand(GenerateAsync);
        }

        public bool IsGenerateWordsCommandEnabled
        {
            get => _isGenerateWordsCommandEnabled;
            set => SetProperty(ref _isGenerateWordsCommandEnabled, value);
        }

        public bool IsGenerateCommandEnabled
        {
            get => _isGenerateCommandEnabled;
            set => SetProperty(ref _isGenerateCommandEnabled, value);
        }

        public ActionCommand GenerateWordsCommand
        {
            get => _generateWordsCommand;
            set => SetProperty(ref _generateWordsCommand, value);
        }

        public ActionCommand GenerateCommand
        {
            get => _generateCommand;
            set => SetProperty(ref _generateCommand, value);
        }

        public async void GenerateWordsAsync()
        {
            try
            {
                IsGenerateWordsCommandEnabled = false;
                try
                {
                    if (string.IsNullOrWhiteSpace(Text))
                    {
                        MessageBox.Show("Sentence is required...");
                        return;
                    }
                    File.WriteAllText("sentences.txt", Text, Encoding.ASCII);
                    await Task.Run(() =>
                    {
                        ExecutePython("gen_data.py", $"--alpha 1.0 --amp 1.0  wavernn");
                    });


                    var chars = File.ReadAllLines("pred/data/chars.txt", Encoding.UTF8).Where(x => x.Length > 0).Select(x => x[0]).ToList();
                    var durs = File.ReadAllLines("pred/d/out.txt", Encoding.UTF8).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => decimal.Parse(x, System.Globalization.NumberStyles.Float)).ToList();
                    var pitches = File.ReadAllLines("pred/p/out.txt", Encoding.UTF8).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => decimal.Parse(x, System.Globalization.NumberStyles.Float)).ToList();

                    var words = GenerateWords(chars, pitches, durs);

                    Words = new ObservableCollection<WordViewModel>(words);

                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }
      
            }
            finally
            {
                IsGenerateWordsCommandEnabled = true;
            }
        }

        public async void GenerateAsync()
        {
            try
            {
                IsGenerateCommandEnabled = false;
                try
                {
                    HashSet<char> noChange = new HashSet<char>(".".ToCharArray());
                    decimal quarter = (112.50702m / (Tempo / 100.0m));
                    foreach (var word in Words)
                    {
                        var noChangeSum = word.Chars.Where(x => noChange.Contains(x.Char)).Sum(x => x.Duration);
                        var changeSum = word.Chars.Where(x => !noChange.Contains(x.Char)).Sum(x => x.Duration);

                        if (changeSum == 0)
                            continue;

                        decimal target = ((quarter * (decimal)word.Note.Value) - noChangeSum) / changeSum;

                        foreach (var c in word.Chars.Where(x => !noChange.Contains(x.Char)))
                        {
                            c.Duration = target * c.Duration;
                        }

                        var durVals = Words.SelectMany(x => x.Chars).Select(x => x.Duration.ToString(CultureInfo.InvariantCulture)).ToArray();

                        File.WriteAllLines("pred/d/in.txt", durVals, Encoding.ASCII);
                    }

                    await Task.Run(() =>
                    {
                        ExecutePython("gen_forward.py", $"--alpha 1.0 --amp 1.0  wavernn");
                    });


                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }

            }
            finally
            {
                IsGenerateCommandEnabled = true;
            }
        }

        public void ExecutePython(string script, string args)
        {
            RunCmd("python", $"{script} {args}");
        }

        private void RunCmd(string cmd, string args)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = cmd;
            start.Arguments = args;
            start.UseShellExecute = true;
            start.RedirectStandardOutput = false;
            using (Process process = Process.Start(start))
            {
                process.WaitForExit();
            }
        }

        private List<WordViewModel> GenerateWords(List<char> chars, List<decimal> pitches, List<decimal> durs)
        {
            var words = new List<WordViewModel>();
            WordViewModel buffer = null;

            for (int i = 0; i < chars.Count; i++)
            {
                var c = chars[i];
                var p = pitches[i];
                var d = durs[i];

                if (c == '.')
                {
                    if (buffer == null)
                    {
                        buffer = new WordViewModel();
                        buffer.Note = Quarter;
                    }
                    else
                    {
                        words.Add(buffer);
                        buffer = new WordViewModel();
                        buffer.Note = Quarter;
                    }

                    buffer.Chars.Add(new WordChar()
                    {
                        Char = c,
                        Duration = d,
                        Pitch = p
                    });

                    buffer.IsPunctuation = true;

                    words.Add(buffer);
                    buffer = null;
                }
                else
                {
                    if (buffer == null)
                    {
                        buffer = new WordViewModel();
                        buffer.Note = Quarter;
                    }

                    buffer.Chars.Add(new WordChar()
                    {
                        Char = c,
                        Duration = d,
                        Pitch = p,
                    });

                    if (c == ' ')
                    {
                        words.Add(buffer);
                        buffer = null;
                    }
                }
            }

            if (buffer != null)
            {
                words.Add(buffer);
            }

            return words;
        }
    }
}
