using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using BuzzGUI.Interfaces;
using BuzzGUI.FileBrowser;
using BuzzGUI.Common;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.WaveformControl;

namespace BuzzGUI.WavetableView
{
	public class WavetableVM : INotifyPropertyChanged
	{
		IWavetable wavetable;
        private  WaveformVM waveformVm;
		public IWavetable Wavetable
		{
            get { return wavetable; }
			set
            {
				if (wavetable != null)
				{
					wavetable.WaveChanged -= wavetable_WaveChanged;
					wavetable.Song.MachineAdded -= Song_MachineAdded;
					wavetable.Song.MachineRemoved -= Song_MachineRemoved;
				}

				wavetable = value;

				if (wavetable != null)
				{
					wavetable.WaveChanged += wavetable_WaveChanged;
					wavetable.Song.MachineAdded += Song_MachineAdded;
					wavetable.Song.MachineRemoved += Song_MachineRemoved;

					waves = new ObservableCollection<WaveSlotVM>();
					var w = wavetable.Waves;

					for (int i = 0; i < wavetable.Waves.Count; i++)
						waves.Add(new WaveSlotVM(this, i) { Wave = w[i] });

                    waveformVm.Wavetable = wavetable;
				}
           
				PropertyChanged.RaiseAll(this);
			}
		}

        private int selectedWaveIndex = 0;
        public int SelectedWaveIndex
        {
            get { return selectedWaveIndex; }
            set
            {
                selectedWaveIndex = value;
            }
        }
        private WaveSlotVM selectedItem;
        public WaveSlotVM SelectedItem
        {
            get { return selectedItem; }
            set
            {
                selectedItem = value;
                WaveformVm.SelectedWave = value.Wave;
                if (value.SelectedLayer != null)
                {
                    WaveformVm.Waveform = value.SelectedLayer.Layer;
                }
                else
                {
                    WaveformVm.Waveform = null;
                }
                PropertyChanged.RaiseAll(this);
                selectedItem.PropertyChanged -= selectedItem_PropertyChanged;
                selectedItem.PropertyChanged += selectedItem_PropertyChanged;
            }
        }

        void selectedItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("SelectedLayer"))
            {
                if (selectedItem.SelectedLayer != null)
                {
                    WaveformVm.Waveform = selectedItem.SelectedLayer.Layer;
                }
                else
                {
                    WaveformVm.Waveform = null;
                }
            }
        }

        private IWaveLayer SelectedWaveform()
        {
            if (waves[selectedWaveIndex].Wave == null) return null;
            return waves[selectedWaveIndex].Wave.Layers.FirstOrDefault();
        }

		public ICommand PlayFileCommand { get; private set; }
		public ICommand FileKeyDownCommand { get; private set; }

        public WaveformVM WaveformVm
        {
            get { return waveformVm;}
            private set 
            { 
                waveformVm = value;
                PropertyChanged.RaiseAll(this);
            }

        }

		public WavetableVM()
		{
			PlayFileCommand = new SimpleCommand
			{
				CanExecuteDelegate = x => true,
				ExecuteDelegate = x => { wavetable.PlayWave((x as FSItemVM).FullPath); }
			};

			FileKeyDownCommand = new SimpleCommand
			{
				CanExecuteDelegate = x => true,
				ExecuteDelegate = x => 
				{
					var p = x as Tuple<FSItemVM, KeyEventArgs>;
					if (p.Item2.Key == Key.Space || p.Item2.Key == Key.Right)
					{
						if (p.Item1.IsFile)
							wavetable.PlayWave(p.Item1.FullPath);
					}
					else if (p.Item2.Key == Key.Back)
					{
						if (p.Item1.IsFile)
							LoadWaves(SelectedWaveIndex, new FSItemVM[] { p.Item1 }, false);
					}
				}
			};

			wavePlayerMachines.Add(new MachineVM(null));
            WaveformVm = new WaveformVM();
            
            StickyFocus = true;

		}

		ObservableCollection<WaveSlotVM> waves;
		public ObservableCollection<WaveSlotVM> Waves
		{
			get
			{
				return waves;
			}
		}

		void wavetable_WaveChanged(int i)
		{
            waves[i].Wave = wavetable.Waves[i];

            if (i == selectedWaveIndex && waveformVm.Waveform == null)
            {
                waveformVm.Waveform = SelectedWaveform();
            }
		}

		public class MachineVM
		{
			public IMachine Machine { get; private set; }
			public override string ToString() { return Machine != null ? Machine.Name : "<select machine>"; }
			public MachineVM(IMachine m) { Machine = m;}
		}

		ObservableCollection<MachineVM> wavePlayerMachines = new ObservableCollection<MachineVM>();
		public ObservableCollection<MachineVM> WavePlayerMachines { get { return wavePlayerMachines; } }


		void Song_MachineAdded(IMachine m)
		{
			if ((m.DLL.Info.Flags & MachineInfoFlags.PLAYS_WAVES) != 0)
				wavePlayerMachines.Add(new MachineVM(m));
		}

		void Song_MachineRemoved(IMachine m)
		{
			var mi = wavePlayerMachines.FirstOrDefault(x => x.Machine == m);
			if (mi != null)	wavePlayerMachines.Remove(mi);
		}

		public void LoadWaves(int index, IEnumerable waves, bool add)
		{
			if (waves != null)
			{
				foreach (var item in waves)
				{
					if (item is FSItemVM)
					{
						var fsi = (FSItemVM)item;

						if (fsi.IsFile)
						{
							wavetable.LoadWaveEx(index, fsi.FullPath, System.IO.Path.GetFileNameWithoutExtension(fsi.Name), add);
							if (++index >= 200) break;
						}
					}
					else if (item is string)
					{
						string path = (string)item;
						wavetable.LoadWaveEx(index, path, System.IO.Path.GetFileNameWithoutExtension(path), add);
						if (++index >= 200) break;
					}
				}
                SelectedItem = Waves[SelectedWaveIndex];
			}
			else
			{
				wavetable.LoadWave(index, null, null, false);
			}

            if (!StickyFocus)
            {
                SelectedWaveIndex = FindNextAvailableIndex(SelectedWaveIndex, wavetable.Waves);
                OnPropertyChanged("SelectedWaveIndex");
            }
		}

        public bool StickyFocus { get; set; }

        private int FindNextAvailableIndex(int begin, ReadOnlyCollection<IWave> waves)
        {
            for (int i = begin; i < waves.Count; i++)
            {
                if (waves[i] == null) return i;
            }
            return 0;
        }

        protected void OnPropertyChanged(string field)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(field));
        }

		public IList<string> ExtensionFilter { get { return wavetable.GetSupportedFileTypeExtensions(); } } 

		public event PropertyChangedEventHandler PropertyChanged;

        
    }
}
