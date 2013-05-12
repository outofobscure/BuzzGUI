using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BuzzGUI.Interfaces;

namespace BuzzGUI.WaveformControl
{
    public class MinMaxCache
    {
        public struct MinMax
        {
            public MinMax(short minv, short maxv)
            {
                MinValue = minv;
                MaxValue = maxv;
            }

			public MinMax(float minv, float maxv)
			{
				MinValue = (short)(minv * 32768.0f);
				MaxValue = (short)(maxv * 32768.0f);
			}

            public double MinNormalized { get { return MinValue / 32768.0; } }
            public double MaxNormalized { get { return MaxValue / 32768.0; } }

            public short MinValue;
            public short MaxValue;
        }

        MinMax[] values;
        BitArray cachedSegments;
        IWaveformBase wave;
        int resolution;
        int segmentSize;

        public MinMaxCache(IWaveformBase wave, int resolution, int segsize)
        {
            this.wave = wave;
            this.resolution = resolution;
            this.segmentSize = segsize;

            int size = 1 + (wave.SampleCount - 1) / resolution;
            values = new MinMax[size];

            cachedSegments = new BitArray((size + segmentSize - 1) / segmentSize);
        }

        MinMax GetMinMax(int first, int length)
        {
            length = Math.Min(length, wave.SampleCount - first);

            //short[] buf = wave.GetSamples(first, length);
			float[] buf = new float[length];
			wave.GetDataAsFloat(buf, 0, 1, 0, first, length);

			float minf = float.MaxValue;
			float maxf = -float.MaxValue;

            for (int i = 0; i < length; i++)
            {
                var x = buf[i];
                if (x < minf) minf = x;
				if (x > maxf) maxf = x;

            }

			return new MinMax(minf, maxf);
        }

        void CacheSegment(int index)
        {
            if (index >= cachedSegments.Length)
                return;

            if (cachedSegments.Get(index))
                return;

            int first = index * segmentSize;

            for (int i = first; i < Math.Min(values.Length, first + segmentSize); i++)
                values[i] = GetMinMax(i * resolution, resolution);

            cachedSegments.Set(index, true);
        }

        public MinMax[] GetRange(int first, int length)
        {
            MinMax[] r = new MinMax[length];
            
            if (first < values.Length)
                Array.Copy(values, first, r, 0, Math.Min(length, values.Length - first));

            return r;
        }

        public MinMax[] GetSegmentPlusOne(int index)
        {
            CacheSegment(index);
            CacheSegment(index + 1);

            int first = index * segmentSize;
            int length = Math.Min(segmentSize, values.Length - first);

            return GetRange(first, length + 1);
        }


        public int Length
        {
            get { return values.Length; }
        }
    }
}
