using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Interfaces;

namespace BuzzGUI.Common
{
    public class TemporaryWave : IWaveformBase
    {
        WaveFormat format;
        int sampleCount;
        int rootNote;
        int sampleRate;
        int channelCount;
        int loopStart;
        int loopEnd;
        float[] left;
        float[] right;
        int index;

        public WaveFormat Format { get { return format; } }
        public int SampleCount { get { return sampleCount; } }
        public int RootNote { get { return rootNote; } set { throw new NotImplementedException(); } }
        public int SampleRate { get { return sampleRate; } set { throw new NotImplementedException(); } }
        public int ChannelCount { get { return channelCount; } }
        public int LoopStart { get { return loopStart; } set { throw new NotImplementedException(); } }
        public int LoopEnd { get { return loopEnd; } set { throw new NotImplementedException(); } }
        public float[] Left { get { return left; } set { throw new NotImplementedException(); } }
        public float[] Right { get { return right; } set { throw new NotImplementedException(); } }
        public int Index { get { return index; } } //note, only valid for TemporaryWaves constructed trough an IWaveformBase

        #pragma warning disable 67
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        public TemporaryWave(float[] DataLeft, WaveFormat iFormat, int iSampleRate, int iRootNote) //for mono
        {
            left = DataLeft;
            InitializeWave(1, iFormat, iSampleRate, iRootNote, DataLeft.Length, 0, DataLeft.Length);
        }

        public TemporaryWave(float[] DataLeft, float[] DataRight, WaveFormat iFormat, int iSampleRate, int iRootNote) //for stereo
        {
            left = DataLeft;
            right = DataRight;
            InitializeWave(2, iFormat, iSampleRate, iRootNote, DataLeft.Length, 0, DataLeft.Length);
        }

        public TemporaryWave(IWaveformBase layer) //for making a copy of an existing layer
        {
            if (layer.ChannelCount == 1)
            {
                left = new float[layer.SampleCount];
                layer.GetDataAsFloat(left, 0, 1, 0, 0, layer.SampleCount);
            }
            else if (layer.ChannelCount == 2)
            {
                left = new float[layer.SampleCount];
                right = new float[layer.SampleCount];
                layer.GetDataAsFloat(left, 0, 1, 0, 0, layer.SampleCount);
                layer.GetDataAsFloat(right, 0, 1, 1, 0, layer.SampleCount);
            }

            InitializeWave(layer.ChannelCount, layer.Format, layer.SampleRate, layer.RootNote, layer.SampleCount, loopStart, layer.LoopEnd);

            //we need to store the index to find out which one was selected when running a command that allocates again.
            index = WaveCommandHelpers.GetLayerIndex(layer);
        }

        private void InitializeWave(int iChannelCount, WaveFormat iFormat, int iSampleRate, int iRootNote, int iSampleCount, int iLoopStart, int iLoopEnd)
        {
            channelCount = iChannelCount;
            format = iFormat;
            sampleRate = iSampleRate;
            rootNote = iRootNote;
            sampleCount = iSampleCount;
            loopStart = iLoopStart;
            loopEnd = iLoopEnd;
        }

        public void GetDataAsFloat(float[] output, int outoffset, int outstride, int channel, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public void SetDataAsFloat(float[] input, int inoffset, int instride, int channel, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public void InvalidateData()
        {
            throw new NotImplementedException();
        }

    }
}
