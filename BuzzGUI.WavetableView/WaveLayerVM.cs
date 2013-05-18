using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using BuzzGUI.Common;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Interfaces;

namespace BuzzGUI.WavetableView
{
	public class WaveLayerVM : INotifyPropertyChanged
	{
        WaveSlotVM WaveSlot;
		IWaveLayer layer;

		public IWaveLayer Layer { get { return layer; } }

        public ICommand LoadLayerCommand { get; private set; }
        public ICommand SaveLayerCommand { get; private set; }
        public ICommand PlayLayerCommand { get; private set; }
        public ICommand StopLayerCommand { get; private set; }
        public ICommand CopyLayerCommand { get; private set; }
        public ICommand PasteLayerCommand { get; private set; }
        public ICommand AddLayerCommand { get; private set; }
        public ICommand ClearLayerCommand { get; private set; }

        public WaveLayerVM(WaveSlotVM slot, IWaveLayer layer)
        {
            WaveSlot = slot;
            this.layer = layer;

            LoadLayerCommand = new SimpleCommand
            {
                ExecuteDelegate = wavelist =>
                {
                    BuzzGUI.Common.Global.Buzz.DCWriteLine("LoadLayerCommand PRESSED");
                }
            };

            //SaveLayerCommand = new Commands.SaveFileCommand(this); //TODO add layer param to savefilecommand
            SaveLayerCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => WaveSlot.Wave != null && layer != null,
                ExecuteDelegate = x =>
                {
                    WaveCommandHelpers.SaveToFile(layer);
                    BuzzGUI.Common.Global.Buzz.DCWriteLine("SaveLayerCommand PRESSED");
                }
            };

            PlayLayerCommand = new SimpleCommand
            {
                //CanExecuteDelegate = x => wave != null, //TODO
                ExecuteDelegate = x => 
                {
                    //if (wave != null) wave.Play(SelectedWavePlayerMachine.Machine); //TODO 
                    BuzzGUI.Common.Global.Buzz.DCWriteLine("PlayLayerCommand PRESSED");
                }
            };

            StopLayerCommand = new SimpleCommand
            {
                //CanExecuteDelegate = x => wave != null, //TODO
                ExecuteDelegate = x => 
                {
                    //wave.Stop(SelectedWavePlayerMachine.Machine); //TODO
                    BuzzGUI.Common.Global.Buzz.DCWriteLine("StopLayerCommand PRESSED");
                }
            };

            CopyLayerCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => WaveSlot.Wave != null && layer !=null,
                ExecuteDelegate = x =>
                {
                    // audio to the clipboard
                    var ms = new MemoryStream();
                    layer.SaveAsWAV(ms);
                    Clipboard.SetAudio(ms);

                    //to add multiple items to the clipboard you must use a dataobject!
                    IDataObject clips = new DataObject();
                    clips.SetData(DataFormats.WaveAudio, ms); //external copy TODO: 32bit float doesn't work, need to convert to 24bit int (or 32bit int?)
                    clips.SetData("BuzzWaveLayerClip", new WaveLayerClip(WaveSlot.Wave.Index, WaveCommandHelpers.GetLayerIndex(layer))); //internal copy
                    Clipboard.SetDataObject(clips, true);

                    BuzzGUI.Common.Global.Buzz.DCWriteLine("CopyLayerCommand PRESSED");               
                }
            };

            PasteLayerCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => (Clipboard.ContainsAudio() || Clipboard.ContainsData("BuzzWaveLayerClip")),
                ExecuteDelegate = x =>
                {
                    // if we have a wavelayer in our clipboard
                    if (Clipboard.ContainsData("BuzzWaveLayerClip"))
                    {
                        WaveLayerClip wc = Clipboard.GetData("BuzzWaveLayerClip") as WaveLayerClip;
                        int targetSlotIndex = WaveSlot.Wave.Index; //need to save this
                        int targetLayerIndex = WaveCommandHelpers.GetLayerIndex(layer); //need to save this
                        WaveCommandHelpers.CopyLayerToLayer(WaveSlot.Wavetable.Wavetable, wc.SourceSlotIndex, wc.SourceLayerIndex, targetSlotIndex, targetLayerIndex);

                        WaveSlot.Wavetable.SelectedItem = WaveSlot.Wavetable.Waves[targetSlotIndex]; //need to set this again otherwise there's an exception when editing in the wave editor
                        WaveSlot.SelectedLayer = WaveSlot.Wavetable.Waves[targetSlotIndex].Layers[targetLayerIndex]; //get by indices because WaveSlotVM might have changed
                        BuzzGUI.Common.Global.Buzz.DCWriteLine("PasteLayerCommand INTERNAL");
                    }
                    // if contains audio
                    else if (Clipboard.ContainsAudio())
                    {
                    }
                    BuzzGUI.Common.Global.Buzz.DCWriteLine("PasteLayerCommand PRESSED");
                }
            };

            AddLayerCommand = new SimpleCommand
            {
                //TODO CanExecuteDelegate
                ExecuteDelegate = x =>
                {
                    BuzzGUI.Common.Global.Buzz.DCWriteLine("AddLayerCommand PRESSED");
                }
            };

            ClearLayerCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => this.layer != null,
                ExecuteDelegate = x =>
                {
                    BuzzGUI.Common.Global.Buzz.DCWriteLine("ClearLayerCommand PRESSED");                  
                    BuzzGUI.Common.Global.Buzz.DCWriteLine("on layer: " + WaveCommandHelpers.GetLayerIndex(layer).ToString());

                    //remove the selected layer from the slot
                    WaveCommandHelpers.ClearLayer(WaveSlot.Wavetable.Wavetable, WaveSlot.Wave.Index, WaveCommandHelpers.GetLayerIndex(layer));
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


        [Serializable]
        private class WaveLayerClip
        {
            private int sourceSlotIndex = -1;
            private int sourceLayerIndex = -1;

            public int SourceSlotIndex
            {
                get
                {
                    return sourceSlotIndex;
                }
                private set
                {
                    sourceSlotIndex = value;
                }
            }

            public int SourceLayerIndex
            {
                get
                {
                    return sourceLayerIndex;
                }
                private set
                {
                    sourceLayerIndex = value;
                }
            }

            public WaveLayerClip(int iSourceSlotIndex, int iSourceLayerIndex)
            {
                sourceSlotIndex = iSourceSlotIndex;
                sourceLayerIndex = iSourceLayerIndex;
            }

        }
	}
}
