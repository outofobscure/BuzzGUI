using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using BuzzGUI.Interfaces;
using BuzzGUI.Common;

namespace BuzzGUI.WaveformControl
{
	public partial class WaveformControl : UserControl
	{
        public ICommand ZoomInCommand { get; set; }
		public WaveformControl()
		{
			InitializeComponent();

			int mouseWheelAcc = 0;

			this.PreviewMouseWheel += (sender, e) =>
			{
                if (waveformElement.Waveform != null)
				{
					mouseWheelAcc += e.Delta;

					while (mouseWheelAcc > 120)
					{
						mouseWheelAcc -= 120;
						waveformElement.AdjustZoom(false);
					}

					while (mouseWheelAcc < 120)
					{
						mouseWheelAcc += 120;
						waveformElement.AdjustZoom(true);
					}

				}

			};
            DataContextChanged += new DependencyPropertyChangedEventHandler(UserControl1_DataContextChanged);

            NameScope.SetNameScope(contextMenu, NameScope.GetNameScope(this));

            ZoomInCommand = new SimpleCommand()
            {
                CanExecuteDelegate = (x) => true,
                ExecuteDelegate = (x) =>
                    {
                        waveformElement.AdjustZoom(true);
                    }
            };

            waveformElement.SelectionChanged += new WaveformElement.ChangedEventHandler((t, e) =>
            {
                //
            });

            waveformElement.PlayCursor.PropertyChanged += PlayCursor_PropertyChanged;

		}

        void PlayCursor_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Offset")
            {
                WaveformCursor Cursor = (WaveformCursor)sender;

                double ScreenOffset = (Cursor.Offset - waveformElement.getOffset().X) % waveformElement.ActualWidth;

                Canvas.SetLeft(TimelineCursor, Math.Floor(ScreenOffset));

                BuzzGUI.Common.Global.Buzz.DCWriteLine(ScreenOffset.ToString());
                //BuzzGUI.Common.Global.Buzz.DCWriteLine(waveformElement.ActualWidth.ToString());
            }
        }
        void UserControl1_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // You can also validate the data going into the DataContext using the event args
        }
	}
}
