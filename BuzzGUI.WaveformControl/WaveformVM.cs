using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using BuzzGUI.Interfaces;
using BuzzGUI.WaveformControl.Commands;
using BuzzGUI.Common;

namespace BuzzGUI.WaveformControl
{
    public class WaveformVM : INotifyPropertyChanged
    {
        //Commands
        public SetLoopWaveEditCommand SetLoopCommand { get; set; }
        public DeleteEditCommand DeleteEditCommand { get; set; }
        public FadeEditCommand FadeInLinearCommand { get; set; }
        public FadeEditCommand FadeOutLinearCommand { get; set; }
        public ReverseEditCommand ReverseEditCommand { get; set; }
        public NormalizeEditCommand NormalizeEditCommand { get; set; }
        public MuteCommand MuteCommand { get; set; }
        public GainEditCommand GainEditCommand { get; set; }
        public PhaseInvertCommand PhaseInvertCommand { get; set; }
        public TrimEditCommand TrimEditCommand { get; set; }
        public SaveSelectionCommand SaveSelectionCommand { get; set; }
        public InsertSilenceCommand InsertSilenceCommand { get; set; }

        public SimpleCommand SelectionChangedCommand { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;


        public WaveformVM()
        {
            SetLoopCommand = new SetLoopWaveEditCommand(this);
            DeleteEditCommand = new DeleteEditCommand(this);
            TrimEditCommand = new TrimEditCommand(this);
            ReverseEditCommand = new ReverseEditCommand(this);
            NormalizeEditCommand = new NormalizeEditCommand(this);
            MuteCommand = new MuteCommand(this);
            GainEditCommand = new GainEditCommand(this);
            PhaseInvertCommand = new PhaseInvertCommand(this);
            FadeInLinearCommand = new FadeEditCommand(this, FadeEditCommand.FadeType.LinIn);
            FadeOutLinearCommand = new FadeEditCommand(this, FadeEditCommand.FadeType.LinOut);
            SaveSelectionCommand = new SaveSelectionCommand(this);
            InsertSilenceCommand = new InsertSilenceCommand(this);

            SelectionChangedCommand = new SimpleCommand()
            {
                CanExecuteDelegate = (x) => true,
                ExecuteDelegate = (x) =>
                {
                    var selection = x as WaveformSelection;
                    if (selection == null) return;

                    SetLoopCommand.UpdateCanExecute(selection.IsActive());
                    DeleteEditCommand.UpdateCanExecute(selection.IsActive());
                    TrimEditCommand.UpdateCanExecute(selection.IsActive());
                    FadeInLinearCommand.UpdateCanExecute(selection.IsActive());
                    FadeOutLinearCommand.UpdateCanExecute(selection.IsActive());
                    ReverseEditCommand.UpdateCanExecute(selection.IsActive());
                    NormalizeEditCommand.UpdateCanExecute(selection.IsActive());
                    MuteCommand.UpdateCanExecute(selection.IsActive());
                    GainEditCommand.UpdateCanExecute(selection.IsActive());
                    PhaseInvertCommand.UpdateCanExecute(selection.IsActive());
                    SaveSelectionCommand.UpdateCanExecute(selection.IsActive());
                    InsertSilenceCommand.UpdateCanExecute(true);
                    //SetLoopCommand.UpdateCanExecute(SelectionIsActive());
                }
            };

        }

        private void OnPropertyChanged(string field)
        {           
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(field));
        }

        IWaveLayer waveform;

        public IWave SelectedWave { get; set; }

        public IWaveLayer Waveform
        {
            get { return waveform; }
            set 
            { 
                waveform = value;
                OnPropertyChanged("Waveform");
            }
        }

        private IWavetable wavetable;
        public IWavetable Wavetable 
        { 
            get { return wavetable;}
            set
            {
                wavetable = value;
                DeleteEditCommand.Wavetable = wavetable;
                TrimEditCommand.Wavetable = wavetable;
                SaveSelectionCommand.Wavetable = wavetable;
                InsertSilenceCommand.Wavetable = wavetable;
            }
        }

        internal int SelectedSlotIndex { get; set; }
        public int SelectedLayerIndex { get; set; }
    }
}
