using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Prism.Mvvm;

namespace DurationHelper
{
    public class NoteViewModel : BindableBase
    {
        public double Value { get; }
        public string Name { get; }

        public NoteViewModel(string name, double value)
        {
            Value = value;
            Name = name;
        }

        public override string ToString()
        {
            return Name;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }
}
