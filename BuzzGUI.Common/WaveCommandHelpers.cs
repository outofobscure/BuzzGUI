using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;

namespace BuzzGUI.Common
{
    public class WaveCommandHelpers
    {
        private WaveCommandHelpers(){}

        private static List<TemporaryWave> BackupLayersInSlot(IEnumerable<IWaveLayer> waves)
        {
            var newLayers = new List<TemporaryWave>();
            foreach (var layer in waves)
            {
                var l = new TemporaryWave(layer);
                newLayers.Add(l);
            }
            return newLayers;
        }

        private static void RestoreLayerFromBackup(IWavetable wavetable, IWave sourceSlot, TemporaryWave sourceLayer, IWave targetSlot, bool add, bool ToFloat, bool ToStereo)
        {
            //TODO REFACTOR ADD PARAM
            //can we set ADD to false if targetSlot.Index == 0 otherwise true ?

            WaveFormat wf = sourceLayer.Format;
            if (ToFloat == true)
            {
                //BuzzGUI.Common.Global.Buzz.DCWriteLine("MAKE OLD SLOT FLOAT");
                wf = WaveFormat.Float32;
            }

            int ChannelCount = sourceLayer.ChannelCount;
            if (ToStereo == true)
            {
                //BuzzGUI.Common.Global.Buzz.DCWriteLine("MAKE OLD SLOT STEREO");
                ChannelCount = 2; //target layer will have 2 channels and left will be copied to right automatically in CopyAudioData()
            }

            wavetable.AllocateWave(targetSlot.Index, sourceLayer.Path, sourceLayer.Name, sourceLayer.SampleCount, wf, ChannelCount == 2, sourceLayer.RootNote, add, false);
            var targetLayer = wavetable.Waves[targetSlot.Index].Layers.Last();
            //BuzzGUI.Common.Global.Buzz.DCWriteLine("sourceLayer Channels: " + sourceLayer.ChannelCount.ToString());
            //BuzzGUI.Common.Global.Buzz.DCWriteLine("targetLayer Channels: " + targetLayer.ChannelCount.ToString());
            //BuzzGUI.Common.Global.Buzz.DCWriteLine("sourceLayer SampleCount: " + sourceLayer.SampleCount.ToString());
            //BuzzGUI.Common.Global.Buzz.DCWriteLine("targetLayer SampleCount: " + targetLayer.SampleCount.ToString());
            //BuzzGUI.Common.Global.Buzz.DCWriteLine("sourceLayer Format: " + sourceLayer.Format.ToString());
            //BuzzGUI.Common.Global.Buzz.DCWriteLine("targetLayer Format: " + targetLayer.Format.ToString());

            CopyMetaData(sourceLayer, targetLayer);
            CopyAudioData(sourceLayer, targetLayer);
            targetLayer.InvalidateData(); //TODO when must this be called ? even if data didn't really change ?
        }

        private static void RestoreLayerFromBackup(IWavetable wavetable, IWave sourceSlot, TemporaryWave sourceLayer, bool add, bool ToFloat, bool ToStereo)
        {
            //convenience method to call if sourceSlot == targetSlot
            //TODO REFACTOR ADD PARAM
            //can we set ADD to false if targetSlot.Index == 0 otherwise true ?
            RestoreLayerFromBackup(wavetable, sourceSlot, sourceLayer, sourceSlot, add, ToFloat, ToStereo);
        }

        private static void RestoreLayerFromBackup(IWavetable wavetable, IWave sourceSlot, TemporaryWave sourceLayer, bool add)
        {
            //convenience method to call if sourceSlot == targetSlot and no conversions are needed
            //TODO REFACTOR ADD PARAM
            //can we set ADD to false if targetSlot.Index == 0 otherwise true ?
            RestoreLayerFromBackup(wavetable, sourceSlot, sourceLayer, sourceSlot, add, false, false);
        }

        private static void CopyMetaData(IWaveformBase sourceLayer, IWaveformBase targetLayer)
        {
            targetLayer.SampleRate = sourceLayer.SampleRate;
            targetLayer.LoopStart = sourceLayer.LoopStart;
            targetLayer.LoopEnd = sourceLayer.LoopEnd;

            if (targetLayer.LoopStart < 0)
            {
                targetLayer.LoopStart = 0;
            }
            else if (targetLayer.LoopStart > targetLayer.SampleCount)
            {
                targetLayer.LoopStart = 0;                
            }

            if (targetLayer.LoopEnd < 0)
            {
                targetLayer.LoopEnd = targetLayer.SampleCount;
            }
            else if (targetLayer.LoopEnd > targetLayer.SampleCount)
            {
                targetLayer.LoopEnd = targetLayer.SampleCount;
            }
        }

        private static void CopyAudioDataMono(float[] left, IWaveformBase targetLayer, int StartSample, int EndSample)
        {
            var SampleCount = EndSample - StartSample;
            targetLayer.SetDataAsFloat(left, StartSample, 1, 0, 0, SampleCount);
        }
        private static void CopyAudioDataStereo(float[] left, float[] right, IWaveformBase targetLayer, int StartSample, int EndSample)
        {
            var SampleCount = EndSample - StartSample;
            targetLayer.SetDataAsFloat(left, StartSample, 1, 0, 0, SampleCount);
            targetLayer.SetDataAsFloat(right, StartSample, 1, 1, 0, SampleCount);
        }

        private static void CopyAudioData(TemporaryWave sourceLayer, IWaveformBase targetLayer)
        {
            if (sourceLayer.ChannelCount == 1 && targetLayer.ChannelCount == 1)
            {
                CopyAudioDataMono(sourceLayer.Left, targetLayer, 0, targetLayer.SampleCount);
            }
            else if (sourceLayer.ChannelCount == 2 && targetLayer.ChannelCount == 2)
            {
                CopyAudioDataStereo(sourceLayer.Left, sourceLayer.Right, targetLayer, 0, targetLayer.SampleCount);
            }
            else if (sourceLayer.ChannelCount == 1 && targetLayer.ChannelCount == 2) //convert mono to stereo
            {
                BuzzGUI.Common.Global.Buzz.DCWriteLine("CONVERT TO STEREO");                
                CopyAudioDataStereo(sourceLayer.Left, sourceLayer.Left, targetLayer, 0, targetLayer.SampleCount);
            }
        }

        private static void CopyAudioData(IWaveformBase sourceLayer, IWaveformBase targetLayer)
        {
            CopyAudioData(sourceLayer, targetLayer, 0, sourceLayer.SampleCount);
        }

        private static void CopyAudioData(IWaveformBase sourceLayer, IWaveformBase targetLayer, int StartSample, int EndSample)
        {
            if (sourceLayer.ChannelCount == 1)
            {
                float[] left = new float[sourceLayer.SampleCount];
                sourceLayer.GetDataAsFloat(left, 0, 1, 0, 0, sourceLayer.SampleCount); //TODO use tempwaveclass and left right ?
                CopyAudioDataMono(left, targetLayer, StartSample, EndSample);
            }
            else if (sourceLayer.ChannelCount == 2)
            {
                float[] left = new float[sourceLayer.SampleCount];
                float[] right = new float[sourceLayer.SampleCount];

                sourceLayer.GetDataAsFloat(left, 0, 1, 0, 0, sourceLayer.SampleCount); //TODO use tempwaveclass and left right ?
                sourceLayer.GetDataAsFloat(right, 0, 1, 1, 0, sourceLayer.SampleCount); //TODO use tempwaveclass and left right ?
                CopyAudioDataStereo(left, right, targetLayer, StartSample, EndSample);            
            }
        }

        public static int GetLayerIndex(IWaveformBase layer)
        {
            //TODO refactor this, should be possible without reflection ?
            var f = layer.GetType().GetField("layerIndex");
            if (f != null) return (int)f.GetValue(layer);
            return -1;
        }

        public static void ClearWaveSlot(IWavetable wavetable, int sourceSlotIndex)
        {
            //Deletes all layers in the slot
            wavetable.LoadWave(sourceSlotIndex, null, null, false);
        }

        public static void CopyWaveSlotToWaveSlot(IWavetable wavetable, int sourceSlotIndex, int targetSlotIndex)
        {
            //TODO THE LAYER NAME!

            //Copy all layers into a new slot
            if (sourceSlotIndex != targetSlotIndex)
            {
                IWave sourceSlot = wavetable.Waves[sourceSlotIndex];

                bool add = false; //first layer allocates the whole slot
                foreach (IWaveLayer sourceLayer in sourceSlot.Layers)
                {
                    wavetable.AllocateWave(targetSlotIndex, sourceLayer.Path, sourceSlot.Name + "_copy", sourceLayer.SampleCount, sourceLayer.Format, sourceLayer.ChannelCount == 2, sourceLayer.RootNote, add, false);
                    IWave targetSlot = wavetable.Waves[targetSlotIndex]; //contains the slot we just allocated with AllocateWave
                    IWaveLayer targetLayer = targetSlot.Layers.Last(); //contains the layer we just allocated with AllocateWave

                    CopyMetaData(sourceLayer, targetLayer);
                    CopyAudioData(sourceLayer, targetLayer);
                    targetLayer.InvalidateData();

                    add = true; //all subsequent layers are added to this slot
                }
            }
        }

        public static void CopySelectionToNewWaveSlot(IWavetable wavetable, int sourceSlotIndex, int sourceLayerIndex, int targetSlotIndex, int targetLayerIndex, int StartSample, int EndSample, string name = "copy")
        {
            IWave sourceSlot = wavetable.Waves[sourceSlotIndex];
            IWaveLayer sourceLayer = sourceSlot.Layers[sourceLayerIndex];

            if (targetLayerIndex == 0)
            {
                wavetable.AllocateWave(targetSlotIndex, sourceLayer.Path, name, EndSample - StartSample, sourceLayer.Format, sourceLayer.ChannelCount == 2, sourceLayer.RootNote, false, false);
                IWave targetSlot = wavetable.Waves[targetSlotIndex]; //contains the slot we just allocated with AllocateWave           
                IWaveLayer targetLayer = targetSlot.Layers.Last(); //contains the layer we just allocated with AllocateWave

                CopyMetaData(sourceLayer, targetLayer);
                CopyAudioData(sourceLayer, targetLayer, StartSample, EndSample);
                targetLayer.InvalidateData();
            }
            else
            {
                //TODO Note: there is currently no way to copy to a specific layer in a slot
                //to do that we probably have to clear the whole slot and rebuild it (use backuplayers to do it?)
                //even if we do that, it will not be guaranteed that the targetLayerIndex will match
                //we could also just append here...
            }       
        }

        /*NOTE: FUNCTIONS BELOW HERE ARE DESTRUCTIVE AND NEED TO REBUILD THE WHOLE SLOT*/

        public static void ConvertSlot(IWavetable wavetable, int sourceSlotIndex, bool ToFloat, bool ToStereo)
        {
            IWave sourceSlot = wavetable.Waves[sourceSlotIndex];

            //we need to backup the whole slot with all layers contained
            List<TemporaryWave> backupLayers = BackupLayersInSlot(sourceSlot.Layers);

            bool add = false; //first layer allocates the whole slot
            foreach (TemporaryWave sourceLayer in backupLayers)
            {
                RestoreLayerFromBackup(wavetable, sourceSlot, sourceLayer, add, ToFloat, ToStereo);
                add = true;
            }
        }

        public static void ClearLayer(IWavetable wavetable, int sourceSlotIndex, int sourceLayerIndex)
        {
            //TODO its possible to end up with an unnamed slot

            IWave sourceSlot = wavetable.Waves[sourceSlotIndex];

            //we need to backup the whole slot with all layers contained
            List<TemporaryWave> backupLayers = BackupLayersInSlot(sourceSlot.Layers);

            //clear the whole slot, we're going to rebuild it without the layer that should get cleared
            WaveCommandHelpers.ClearWaveSlot(wavetable, sourceSlotIndex);

            bool add = false; //first layer allocates the whole slot
            foreach (TemporaryWave sourceLayer in backupLayers)
            {
                if (sourceLayer.Index == sourceLayerIndex) //only delete from the selected layer
                {
                    //do no restore the selected layer so it gets dropped
                }
                else
                {
                    RestoreLayerFromBackup(wavetable, sourceSlot, sourceLayer, add);
                }
                add = true;
            }
        }

        public static void DeleteSelectionFromLayer(IWavetable wavetable, int sourceSlotIndex, int sourceLayerIndex, int StartSample, int EndSample)
        {
            IWave sourceSlot = wavetable.Waves[sourceSlotIndex];

            //we need to backup the whole slot with all layers contained so we can operate on the selected layer
            List<TemporaryWave> backupLayers = BackupLayersInSlot(sourceSlot.Layers);

            if (StartSample != EndSample)
            {
                bool add = false; //first layer allocates the whole slot
                foreach (TemporaryWave sourceLayer in backupLayers)
                {
                    if (sourceLayer.Index == sourceLayerIndex) //only delete from the selected layer
                    {
                        wavetable.AllocateWave(sourceSlotIndex, sourceLayer.Path, sourceLayer.Name, sourceLayer.Left.Length - (EndSample - StartSample), sourceLayer.Format, sourceLayer.ChannelCount == 2, sourceLayer.RootNote, add, false);
                        IWaveLayer targetLayer = wavetable.Waves[sourceSlotIndex].Layers.Last();

                        if (sourceLayer.ChannelCount == 1)
                        {
                            CopyMetaData(sourceLayer, targetLayer);
                            
                            //copy parts before and after selection to get rid of selected part
                            targetLayer.SetDataAsFloat(sourceLayer.Left, 0, 1, 0, 0, StartSample);
                            targetLayer.SetDataAsFloat(sourceLayer.Left, EndSample, 1, 0, StartSample, sourceLayer.Left.Length - EndSample);
                            targetLayer.InvalidateData();
                        }
                        else if (sourceLayer.ChannelCount == 2)
                        {
                            CopyMetaData(sourceLayer, targetLayer);

                            //copy parts before and after selection to get rid of selected part
                            targetLayer.SetDataAsFloat(sourceLayer.Left, 0, 1, 0, 0, StartSample);
                            targetLayer.SetDataAsFloat(sourceLayer.Left, EndSample, 1, 0, StartSample, sourceLayer.Left.Length - EndSample);
                            targetLayer.SetDataAsFloat(sourceLayer.Right, 0, 1, 1, 0, StartSample);
                            targetLayer.SetDataAsFloat(sourceLayer.Right, EndSample, 1, 1, StartSample, sourceLayer.Right.Length - EndSample);
                            targetLayer.InvalidateData();
                        }
                    }
                    else //if this is not the selected layer we still need to copy all the data (unaltered)
                    {
                        RestoreLayerFromBackup(wavetable, sourceSlot, sourceLayer, add);
                    }

                    add = true; //all subsequent layers are added to this slot
                }
            }
        }

        public static void TrimSelectionFromLayer(IWavetable wavetable, int sourceSlotIndex, int sourceLayerIndex, int StartSample, int EndSample)
        {
            IWave sourceSlot = wavetable.Waves[sourceSlotIndex];

            //we need to backup the whole slot with all layers contained so we can operate on the selected layer
            List<TemporaryWave> backupLayers = BackupLayersInSlot(sourceSlot.Layers);

            if (StartSample != EndSample)
            {
                bool add = false; //first layer allocates the whole slot
                foreach (TemporaryWave sourceLayer in backupLayers)
                {
                    if (sourceLayer.Index == sourceLayerIndex) //only trim the selected layer
                    {
                        wavetable.AllocateWave(sourceSlotIndex, sourceLayer.Path, sourceLayer.Name, EndSample - StartSample, sourceLayer.Format, sourceLayer.ChannelCount == 2, sourceLayer.RootNote, add, false);
                        var targetLayer = wavetable.Waves[sourceSlotIndex].Layers.Last();

                        if (sourceLayer.ChannelCount == 1)
                        {
                            CopyMetaData(sourceLayer, targetLayer);

                            //copy selection and get rid of the rest
                            targetLayer.SetDataAsFloat(sourceLayer.Left, StartSample, 1, 0, 0, EndSample - StartSample);
                            targetLayer.InvalidateData();
                        }
                        else if (sourceLayer.ChannelCount == 2)
                        {
                            CopyMetaData(sourceLayer, targetLayer);

                            //copy selection and get rid of the rest
                            targetLayer.SetDataAsFloat(sourceLayer.Left, StartSample, 1, 0, 0, EndSample - StartSample);
                            targetLayer.SetDataAsFloat(sourceLayer.Right, StartSample, 1, 1, 0, EndSample - StartSample);
                            targetLayer.InvalidateData();
                        }
                    }
                    else //if this is not the selected layer we still need to copy all the data (unaltered)
                    {
                        RestoreLayerFromBackup(wavetable, sourceSlot, sourceLayer, add);
                    }

                    add = true; //all subsequent layers are added to this slot
                }
            }
        }

        public static void AddSelectionToLayer(IWavetable wavetable, int sourceSlotIndex, int sourceLayerIndex, int SamplePosition, TemporaryWave inputLayer)
        {
            // get right destination layer
            IWave sourceSlot = wavetable.Waves[sourceSlotIndex];

            //we need to backup the whole slot with all layers contained so we can operate on the selected layer
            List<TemporaryWave> backupLayers = BackupLayersInSlot(sourceSlot.Layers);

            bool add = false; //first layer allocates the whole slot
            foreach (TemporaryWave sourceLayer in backupLayers)
            {
                if (sourceLayer.Index == sourceLayerIndex) //only add to the selected layer
                {
                    wavetable.AllocateWave(sourceSlotIndex, sourceLayer.Path, sourceLayer.Name, sourceLayer.Left.Length + inputLayer.SampleCount, sourceLayer.Format, sourceLayer.ChannelCount == 2, sourceLayer.RootNote, add, false);
                    var targetLayer = wavetable.Waves[sourceSlotIndex].Layers.Last();

                    // check input format matches destination format
                    if ((sourceLayer.Format != inputLayer.Format) || (sourceLayer.ChannelCount != inputLayer.ChannelCount))
                    {
                        // TODO: convert input format
                        //note that we can't do it here, we must check before we iterate all backup layers etc (so two iterations are needed: convert, then do the add selection)
                        //also note that ALL layers in a slot must have the same format so you need to use ConvertSlot()
                    } 
                    else if (sourceLayer.ChannelCount == 1)
                    {
                        CopyMetaData(sourceLayer, targetLayer);

                        // add 0-StartSample of old layer
                        targetLayer.SetDataAsFloat(sourceLayer.Left, 0, 1, 0, 0, SamplePosition);
                        // add input
                        targetLayer.SetDataAsFloat(inputLayer.Left, 0, 1, 0, SamplePosition, inputLayer.SampleCount);
                        // add StartSample - length of old layer
                        targetLayer.SetDataAsFloat(sourceLayer.Left, SamplePosition, 1, 0, SamplePosition + inputLayer.SampleCount, sourceLayer.SampleCount - SamplePosition);

                        targetLayer.InvalidateData();
                    }
                    else if (sourceLayer.ChannelCount == 2)
                    {
                        CopyMetaData(sourceLayer, targetLayer);

                        // add 0-StartSample of old layer
                        targetLayer.SetDataAsFloat(sourceLayer.Left, 0, 1, 0, 0, SamplePosition);
                        targetLayer.SetDataAsFloat(sourceLayer.Right, 0, 1, 1, 0, SamplePosition);
                        // add input
                        targetLayer.SetDataAsFloat(inputLayer.Left, 0, 1, 0, SamplePosition, inputLayer.SampleCount);
                        targetLayer.SetDataAsFloat(inputLayer.Right, 0, 1, 1, SamplePosition, inputLayer.SampleCount);
                        // add StartSample - length of old layer
                        targetLayer.SetDataAsFloat(sourceLayer.Left, SamplePosition, 1, 0, SamplePosition + inputLayer.SampleCount, sourceLayer.SampleCount - SamplePosition);
                        targetLayer.SetDataAsFloat(sourceLayer.Right, SamplePosition, 1, 1, SamplePosition + inputLayer.SampleCount, sourceLayer.SampleCount - SamplePosition);

                        targetLayer.InvalidateData();
                    }
                }
                else //if this is not the selected layer we still need to copy all the data (unaltered)
                {
                    RestoreLayerFromBackup(wavetable, sourceSlot, sourceLayer, add);
                }

                add = true; //all subsequent layers are added to this slot
            }            
        }

    }
}
