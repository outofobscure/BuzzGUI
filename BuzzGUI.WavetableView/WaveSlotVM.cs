﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.ComponentModel;
using System.Collections.ObjectModel;
using BuzzGUI.Interfaces;
using BuzzGUI.Common;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.FileBrowser;
using libsndfile;

namespace BuzzGUI.WavetableView
{
    public class WaveSlotVM : INotifyPropertyChanged
    {
        public WavetableVM Wavetable { get; private set; }
        int index;

        IWave wave;
        public IWave Wave
        {
            get { return wave; }
            set
            {
                if (wave != null)
                {
                    wave.PropertyChanged -= wave_PropertyChanged;
                }

                wave = value;
                layers = new List<WaveLayerVM>();

                if (wave != null)
                {
                    wave.PropertyChanged += wave_PropertyChanged;
                    foreach (var x in wave.Layers) 
                    {
                        layers.Add(new WaveLayerVM(x));
                    }

                    if (Wavetable.WaveformVm.SelectedLayerIndex != -1)
                    {
                        //user ran a destructive command, the slot will rebuild one layer at a time now.
                        if (layers.Count > Wavetable.WaveformVm.SelectedLayerIndex)
                        {                           
                            //we must set the selected layer again the user had selected
                            SelectedLayer = layers[Wavetable.WaveformVm.SelectedLayerIndex];
                        }
                        else
                        {
                            //the event is fired for every layer, so until the layer index is lower than the one the user operate on, set selected layer to null (otherwise it throws an exception)
                            SelectedLayer = null;
                        }
                    }
                    else
                    {
                        SelectedLayer = layers.LastOrDefault(); // we want to switch to the newest loaded layer immediately
                    }               
                }
                else
                {
                    SelectedLayer = null;
                }

                PropertyChanged.RaiseAll(this);
            }
        }

        void wave_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Layers":
                    PropertyChanged.Raise(this, "Layers");
                    break;
            }
        }

        [Serializable]
        private class WaveClip
        {
            private int index = -1;
            public int Index
            {
                get
                {
                    return index;
                }
                set
                {
                    index = value;
                }
            }
            public WaveClip(int ini)
            {
                index = ini;
            }

        }

        public ICommand LoadCommand { get; private set; }
        public ICommand SaveCommand { get; private set; }
        public ICommand PlayCommand { get; private set; }
        public ICommand StopCommand { get; private set; }
        public ICommand ClearCommand { get; private set; }
        public ICommand CopyCommand { get; private set; }
        public ICommand PasteCommand { get; private set; }
        public ICommand DropCommand { get; private set; }

        public WaveSlotVM(WavetableVM wt, int indx)
        {
            Wavetable = wt;
            this.index = indx;
            SelectedWavePlayerMachine = Wavetable.WavePlayerMachines[0];

            LoadCommand = new SimpleCommand
            {
                ExecuteDelegate = wavelist =>
                {
                    wt.LoadWaves(index, (IEnumerable)wavelist, false);
                }
            };

            SaveCommand = new Commands.SaveFileCommand(this);

            PlayCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => wave != null,
                ExecuteDelegate = x => { if (wave != null) wave.Play(SelectedWavePlayerMachine.Machine); }
            };

            StopCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => wave != null,
                ExecuteDelegate = x => wave.Stop(SelectedWavePlayerMachine.Machine)
            };

            ClearCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => wave != null,
                ExecuteDelegate = x => {
                    Wavetable.WaveformVm.SelectedLayerIndex = -1;
                    wt.LoadWaves(index, null, false);
                }
            };

            CopyCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => wave != null,
                ExecuteDelegate = x =>
                {
                    // audio to the clipboard
                    var ms = new MemoryStream();
                    var il = wave.Layers[0];
                    il.SaveAsWAV(ms);
                    System.Windows.Clipboard.SetAudio(ms);

                    // 
                    Clipboard.SetData("BuzzWaveClip", new WaveClip(index));
                    //wt.WaveClipboard = wt.Waves[index].Wave;
                }
            };

            PasteCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => (Clipboard.ContainsAudio() || Clipboard.ContainsData("BuzzWaveClip")),
                ExecuteDelegate = x =>
                {
                    // if we have a waveslot in our clipboard
                    if (Clipboard.ContainsData("BuzzWaveClip"))
                    {
                        WaveClip wc = Clipboard.GetData("BuzzWaveClip") as WaveClip;
                        int fromIndex = wc.Index;
                        WaveCommandHelpers.CopyWaveSlotToWaveSlot(wt.Wavetable, fromIndex, index);
                        //wt.Waves[index].Wave = wt.WaveClipboard;
                    }
                    // if contains audio
                    else if (Clipboard.ContainsAudio())
                    {
                        // get audio stream 
                        var ms = Clipboard.GetAudioStream();
                        if (ms == null) return;
                        libsndfile.SF_INFO msInfo = new SF_INFO();

                        using (var s = new SoundFile(ms, libsndfile.FileMode.SFM_READ, msInfo))
                        {
                            if (s == null) return;

                            float[] hold = new float[s.FrameCount * s.ChannelCount];

                            var subformat = s.Format & Format.SF_FORMAT_SUBMASK;

                            WaveFormat wf;

                            if (subformat == Format.SF_FORMAT_FLOAT || subformat == Format.SF_FORMAT_DOUBLE) wf = WaveFormat.Float32;
                            else if (subformat == Format.SF_FORMAT_PCM_32) wf = WaveFormat.Int32;
                            else if (subformat == Format.SF_FORMAT_PCM_24) wf = WaveFormat.Int24;
                            else wf = WaveFormat.Int16;

                            s.ReadFloat(hold, s.FrameCount);

                            // write to wavetable
                            int rootnote = BuzzNote.FromMIDINote(Math.Max(0, s.Instrument.basenote - 12));
                            Wavetable.Wavetable.AllocateWave(index,
                                                "",
                                                "Copy",
                                                (int)(s.FrameCount * s.ChannelCount),
                                                wf,
                                                s.ChannelCount == 2,
                                                rootnote,
                                                true,
                                                false);

                            WaveLayerVM lastLayer = Wavetable.Waves[index].Layers.Last();

                            if (s.ChannelCount == 2)
                            {
                                lastLayer.Layer.SetDataAsFloat(hold, 0, 2, 0, 0, (int)s.FrameCount);
                                lastLayer.Layer.SetDataAsFloat(hold, 0, 2, 1, 0, (int)s.FrameCount);
                            }
                            else
                            {
                                lastLayer.Layer.SetDataAsFloat(hold, 0, 1, 0, 0, (int)s.FrameCount);
                            }
                            lastLayer.Layer.InvalidateData();
                        }
                    }
                }
            };

            DropCommand = new SimpleCommand
            {
                CanExecuteDelegate = x =>
                {
                    var p = x as Tuple<DragEventArgs, UIElement>;
                    var e = p.Item1;
                    if (e.Data.GetDataPresent(typeof(BuzzGUI.FileBrowser.FSItemVM)))
                    {
                        var fsi = e.Data.GetData(typeof(BuzzGUI.FileBrowser.FSItemVM)) as FSItemVM;
                        if (fsi == null || !fsi.IsFile) return false;
                        return true;
                    }
                    else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    {
                        var filenames = (string[])e.Data.GetData(DataFormats.FileDrop);
                        return filenames.Any(fn => WavetableExtensions.CanLoadFile(fn));
                    }

                    return false;
                },
                ExecuteDelegate = x =>
                {
                    Wavetable.WaveformVm.SelectedLayerIndex = -1;

                    var p = x as Tuple<DragEventArgs, UIElement>;
                    var param = DragTargetBehavior.GetParameter(p.Item2);
                    var e = p.Item1;
                    if (e.Data.GetDataPresent(typeof(BuzzGUI.FileBrowser.FSItemVM)))
                    {
                        var fsi = p.Item1.Data.GetData(typeof(BuzzGUI.FileBrowser.FSItemVM)) as FSItemVM;
                        wt.LoadWaves(index, new FSItemVM[] { fsi }, param != null && param == "Add");
                    }
                    else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    {
                        var filenames = (string[])e.Data.GetData(DataFormats.FileDrop);
                        wt.LoadWaves(index, filenames.Where(fn => WavetableExtensions.CanLoadFile(fn)), param != null && param == "Add");
                    }
                }
            };

        }

        public bool HasWave { get { return wave != null; } }

        public string NumberString
        {
            get
            {
                return string.Format("{0:X2}", index + 1);
            }
        }

        public string Name
        {
            get
            {
                return wave != null ? wave.Name : null;
            }
        }

        public float Volume
        {
            get { return wave != null ? wave.Volume : 0; }
            set { if (wave != null) wave.Volume = value; PropertyChanged.Raise(this, "Volume"); PropertyChanged.Raise(this, "VolumeText"); }
        }

        public double VolumedB
        {
            get { return Math.Max(-48, Decibel.FromAmplitude(Volume)); }
            set { Volume = (float)Decibel.ToAmplitude(value); PropertyChanged.Raise(this, "VolumedB"); }
        }

        public string VolumeText
        {
            get
            {
                if (Volume == 0)
                    return "-inf.dB";
                else
                    return string.Format("{0:F1}dB", VolumedB);
            }
        }

        public int LoopMode
        {
            get
            {
                if (wave == null) return 0;
                bool loop = (wave.Flags & WaveFlags.Loop) != 0;
                bool bidir = (wave.Flags & WaveFlags.BidirectionalLoop) != 0;
                if (bidir) return 2;
                else if (loop) return 1;
                else return 0;
            }
            set
            {
                if (wave == null) return;
                WaveFlags f = wave.Flags;
                f &= ~(WaveFlags.Loop | WaveFlags.BidirectionalLoop);
                if (value > 0) f |= WaveFlags.Loop;
                if (value == 2) f |= WaveFlags.BidirectionalLoop;
                wave.Flags = f;
                PropertyChanged.Raise(this, "LoopMode");
            }
        }

        List<WaveLayerVM> layers;
        public List<WaveLayerVM> Layers
        {
            get
            {
                return layers;
            }
        }

        WaveLayerVM selectedLayer;
        public WaveLayerVM SelectedLayer
        {
            get { return selectedLayer; }
            set
            {
                selectedLayer = value;
                PropertyChanged.Raise(this, "SelectedLayer");
            }
        }


        WavetableVM.MachineVM selectedWavePlayerMachine;
        public WavetableVM.MachineVM SelectedWavePlayerMachine
        {
            get { return selectedWavePlayerMachine; }
            set
            {
                selectedWavePlayerMachine = value;

                if (selectedWavePlayerMachine != null && selectedWavePlayerMachine.Machine != null)
                {
                    var names = selectedWavePlayerMachine.Machine.EnvelopeNames;
                    var l = new List<EnvelopeVM>();
                    for (int i = 0; i < names.Count; i++) l.Add(new EnvelopeVM(Wave.GetEnvelope(i, selectedWavePlayerMachine.Machine), names[i]));
                    envelopes = l.AsReadOnly();
                    if (Envelopes.Count > 0) SelectedEnvelope = envelopes[0];
                }
                else
                {
                    envelopes = null;
                }

                PropertyChanged.Raise(this, "SelectedWavePlayerMachine");
                PropertyChanged.Raise(this, "Envelopes");

            }
        }

        public class EnvelopeVM
        {
            public IEnvelope Envelope { get; private set; }
            public string Name { get; private set; }
            public override string ToString() { return Name; }
            public EnvelopeVM(IEnvelope e, string n) { Envelope = e; Name = n; }
        }

        ReadOnlyCollection<EnvelopeVM> envelopes;
        public ReadOnlyCollection<EnvelopeVM> Envelopes
        {
            get
            {
                return envelopes;
            }
        }

        EnvelopeVM selectedEnvelope;
        public EnvelopeVM SelectedEnvelope
        {
            get { return selectedEnvelope; }
            set
            {
                selectedEnvelope = value;
                PropertyChanged.Raise(this, "SelectedEnvelope");
            }
        }


        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
