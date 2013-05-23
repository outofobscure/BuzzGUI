using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Common;

namespace BuzzGUI.WaveformControl.Commands
{
    public class SnapToZeroCrossingCommand : NoopCommand
	{
        public SnapToZeroCrossingCommand(WaveformVM waveformVm) : base(waveformVm) { }

		public override void Execute(object parameter)
		{
			if (UpdateFromParam(parameter))
			{
                int inStart, inEnd, dist, curStart, curEnd;
                Nullable<bool> isRising = null; // true for -0+, false for +0-
                Nullable<bool> startFirst = null; // true for start, false for end
                
                TemporaryWave wave = new TemporaryWave(WaveformVm.Waveform);

                inStart = curStart = Selection.StartSample;
                inEnd = curEnd = Selection.EndSample;
                dist = 0;

                // search outwards from current selection point for zero crossings
                while(((inStart - dist) > 0) && ((inEnd + dist) < Waveform.SampleCount))
                {
                    // check for first crossing, if one found, remember direction
                    if (startFirst == null)
                    {
                        // check start then end
                        if ((wave.Left[inStart - dist - 1] < 0) && (wave.Left[inStart - dist] > 0))
                        {
                            // start is rising
                            isRising = true;
                            startFirst = true;
                            curStart = inStart - dist;
                        }
                        else if ((wave.Left[inStart - dist - 1] > 0) && (wave.Left[inStart - dist] < 0))
                        {
                            // start is falling
                            isRising = false;
                            startFirst = true;
                            curStart = inStart - dist;
                        }
                        else if ((wave.Left[inEnd + dist] < 0) && (wave.Left[inEnd + dist + 1] > 0))
                        {
                            // end is rising
                            isRising = true;
                            startFirst = false;
                            curEnd = inEnd + dist;
                        }
                        else if ((wave.Left[inEnd + dist] > 0) && (wave.Left[inEnd + dist + 1] < 0))
                        {
                            // end is falling
                            isRising = false;
                            startFirst = false;
                            curEnd = inEnd + dist;
                        }
                    }
                    // if second of same direction is found, change selection and break
                    else
                    {
                        // look for second crossing
                        if (startFirst == true)
                        {
                            // check for zero crossings at end
                            if ((isRising == true) && (wave.Left[inEnd + dist] < 0) && (wave.Left[inEnd + dist + 1] > 0))
                            {
                                // end is rising
                                curEnd = inEnd + dist;
                                break;
                            }
                            else if ((isRising == false) && (wave.Left[inEnd + dist] > 0) && (wave.Left[inEnd + dist + 1] < 0))
                            {
                                // end is falling
                                curEnd = inEnd + dist;
                                break;
                            }
                        }
                        else
                        {
                            // check for zero crossings at start
                            if ((isRising == true) && (wave.Left[inStart - dist - 1] < 0) && (wave.Left[inStart - dist] > 0))
                            {
                                // start is rising
                                curStart = inStart - dist;
                                break;
                            }
                            else if ((isRising == false) && (wave.Left[inStart - dist - 1] > 0) && (wave.Left[inStart - dist] < 0))
                            {
                                // start is falling
                                curStart = inStart - dist;
                                break;
                            }
                        }
                    }

                    dist++;
                }

                // update selection
                Selection.StartSample = curStart;
                Selection.EndSample = curEnd;
			}
		}
    }
}
