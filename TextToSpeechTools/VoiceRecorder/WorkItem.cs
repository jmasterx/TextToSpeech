using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VoiceRecorder
{
    public class WorkItem
    {
        public string Filename { get; set; }
        public string Text { get; set; }

        public override string ToString()
        {
            return Filename;
        }
    }
}
