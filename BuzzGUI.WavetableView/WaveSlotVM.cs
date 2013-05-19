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
                        layers.Add(new WaveLayerVM(this, x));
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
                            //but the event is fired for every layer, so while the SelectedLayerIndex is lower than the one the user operate on, set selected layer to the first or null (otherwise it throws an exception)
                            if (Layers.Count > 0)
                            {
                                SelectedLayer = layers.FirstOrDefault();
                            }
                            else
                            {
                                SelectedLayer = null;
                            }
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

            SaveCommand =  new SimpleCommand
            {
                CanExecuteDelegate = x => wave != null,
                ExecuteDelegate = x => { if (wave != null) WaveCommandHelpers.SaveToFile(wave.Layers.LastOrDefault(), wave.Name); }
            };

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
                    var il = wave.Layers[0]; //TODO rethink this, can we copy the selected layer ?
                    il.SaveAsWAV(ms);

                    //to add multiple items to the clipboard you must use a dataobject!
                    IDataObject clips = new DataObject();
                    clips.SetData(DataFormats.WaveAudio, ms); //external copy TODO: 32bit float doesn't work, need to convert to 24bit int (or 32bit int?)
                    clips.SetData("BuzzWaveSlot", new WaveCommandHelpers.BuzzWaveSlot(wave.Index, WaveCommandHelpers.BackupLayersInSlot(wave.Layers))); //internal copy
                    Clipboard.SetDataObject(clips, true);
                }
            };

            PasteCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => (Clipboard.ContainsAudio() || Clipboard.ContainsData("BuzzWaveSlot")),
                ExecuteDelegate = x =>
                {
                    // if we have a WaveSlot in our clipboard
                    if (Clipboard.ContainsData("BuzzWaveSlot"))
                    {
                        WaveCommandHelpers.BuzzWaveSlot ws = Clipboard.GetData("BuzzWaveSlot") as WaveCommandHelpers.BuzzWaveSlot;
                        int sourceLayerIndex = WaveCommandHelpers.GetLayerIndex(wt.Waves[ws.SourceSlotIndex].SelectedLayer.Layer); //must save this
                        WaveCommandHelpers.ReplaceSlot(wt.Wavetable, ws.Layers, index);
                        Wavetable.SelectedItem = wt.Waves[index]; //need to set this again otherwise there's an exception when editing in the wave editor
                        SelectedLayer = wt.Waves[index].layers[sourceLayerIndex]; //switch to the same layer that was selected in the original
                    }
                    // if we have a TemporaryWave in our clipboard, replace the whole slot with this one wave
                    else if (Clipboard.ContainsData("BuzzTemporaryWave"))
                    {
                        List<TemporaryWave> Layers = new List<TemporaryWave>();
                        Layers.Add(Clipboard.GetData("BuzzTemporaryWave") as TemporaryWave);
                        WaveCommandHelpers.ReplaceSlot(wt.Wavetable, Layers, index);
                        Wavetable.SelectedItem = wt.Waves[index]; //need to set this again otherwise there's an exception when editing in the wave editor
                        SelectedLayer = wt.Waves[index].layers.FirstOrDefault(); //there's only one layer so switch to it
                    }
                    // if contains audio from windows clipboard
                    else if (Clipboard.ContainsAudio())
                    {
                        // get audio stream 
                        var ms = Clipboard.GetAudioStream();

                        var tw = new TemporaryWave(ms);

                        if (tw != null)
                        {
                            List<TemporaryWave> Layers = new List<TemporaryWave>();
                            Layers.Add(tw);
                            WaveCommandHelpers.ReplaceSlot(wt.Wavetable, Layers, index);
                            Wavetable.SelectedItem = wt.Waves[index]; //need to set this again otherwise there's an exception when editing in the wave editor
                            SelectedLayer = wt.Waves[index].layers.FirstOrDefault(); //there's only one layer so switch to it                            
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

