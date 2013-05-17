using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace BuzzGUI.WaveformControl
{
    public class WaveformCursor : INotifyPropertyChanged
    {
        WaveformElement element;
        double offset;
        int offsetSamples;

        public WaveformElement Element { get { return element; } }
        public double Offset { get { return offset; } set { offset = value; OnPropertyChanged("Offset"); } }
        public int OffsetSamples { get { return offsetSamples; } set { offsetSamples = Math.Min(Math.Max(0, value), element.Waveform.SampleCount);} }

        public WaveformCursor(WaveformElement element)
        {
            this.element = element;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }
    }
}

