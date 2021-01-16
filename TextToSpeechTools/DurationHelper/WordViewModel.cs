using System.Collections.Generic;
using System.Linq;
using Prism.Mvvm;

namespace DurationHelper
{
    public class WordViewModel : BindableBase
    {
        public List<WordChar> Chars = new List<WordChar>();
        private NoteViewModel _note;

        public string Word => new string(Chars.Select(x => x.Char).ToArray());

        public NoteViewModel Note
        {
            get => _note;
            set => SetProperty(ref _note, value);
        }

        public bool IsPunctuation { get; set; }

    }
}
