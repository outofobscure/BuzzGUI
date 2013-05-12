using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Windows.Input;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;

namespace BuzzGUI.WavetableView
{
	public class WaveLayerVM : INotifyPropertyChanged
	{
		IWaveLayer layer;

		public IWaveLayer Layer { get { return layer; } }

        public ICommand ClearLayerCommand { get; private set; }
        public WaveLayerVM(IWaveLayer layer)
        {
            this.layer = layer;
            ClearLayerCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => this.layer != null,
                ExecuteDelegate = x =>
                {
                    BuzzGUI.Common.Global.Buzz.DCWriteLine("CLEAR PRESSED");
                }
            };
        }

		public int SampleCount { get { return layer.SampleCount; } }
		public int SampleRate 
		{ 
			get { return layer.SampleRate; } 
			set 
			{
                if (value < 8000 || value > 768000)
					throw new ArgumentException("Invalid sample rate");

				layer.SampleRate = value;
				PropertyChanged.Raise(this, "SampleRate");
			} 
		}
		public int LoopStart 
		{ 
			get 
			{ 
				return layer.LoopStart; 
			} 
			set 
			{ 
				if (value < 0 || value >= LoopEnd || value >= SampleCount)
					throw new ArgumentException("Invalid loop start");

				layer.LoopStart = value;
				PropertyChanged.Raise(this, "LoopStart");
			} 
		}
		public int LoopEnd 
		{ 
			get 
			{ 
				return layer.LoopEnd; 
			} 
			set 
			{
				if (value < 0 || value <= LoopStart || value > SampleCount)
					throw new ArgumentException("Invalid loop end");

				layer.LoopEnd = value;
				PropertyChanged.Raise(this, "LoopEnd");
			}  
		}

        public string ToolTipString 
        {
            get
            {
                return string.Format("Samplerate: {0} Channels: {1} Format: {2}", layer.SampleRate, layer.ChannelCount, layer.Format);
            }
        }
		public static IEnumerable<string> NoteList { get { return BuzzNote.Names; } }
		public string RootNote { get { return BuzzNote.ToString(layer.RootNote); } set { layer.RootNote = BuzzNote.Parse(value); PropertyChanged.Raise(this, "RootNote"); } }

		public event PropertyChangedEventHandler PropertyChanged;
	}
}
