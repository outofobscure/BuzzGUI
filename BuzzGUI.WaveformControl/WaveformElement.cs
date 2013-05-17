﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using BuzzGUI.Interfaces;
using System.Globalization;
using System.ComponentModel;
using System.Linq;

namespace BuzzGUI.WaveformControl
{
	public class WaveformElement : FrameworkElement, IScrollInfo, INotifyPropertyChanged
    {
        #region fields
        MinMaxCache minMaxCache;
		int zoomLevel = 14;
		int resolution;
		SegmentVisual.Resources res;
        //int cursorSamplePos = 0;
        //double cursorOffset = 0;
        public DrawingVisual cursorDrawingVisual; //TODO refactor cursor into its own class
        DrawingVisual selectionDrawingVisual;
        DrawingVisual adjustmentTargetVisual;
        IDisposable mouseOverObserver;
        IDisposable observer;       
        #endregion

        #region events
        public delegate void ChangedEventHandler(object sender, EventArgs e);
        public event ChangedEventHandler SelectionChanged;        
        #endregion

        public WaveformCursor PlayCursor { get; private set; }
        public WaveformSelection Selection { get; private set; }

        public IWaveformBase Waveform
        {
            get { return (IWaveformBase)GetValue(WaveformProperty); }
            set { SetValue(WaveformProperty, value);}
        }

        public void OnSelectionChanged(object sender, EventArgs e)
        {
            UpdateCursorPosition();
            UpdateSelection();
            UpdateAdjustmentTargetVisual();
            SelectionChanged(this, null);
            OnPropertyChanged("SelectionString");
        }

        public static readonly DependencyProperty WaveformProperty =
            DependencyProperty.Register("Waveform", 
            typeof(IWaveformBase), typeof(WaveformElement),
            new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnWaveformChanged)));

        static void OnWaveformChanged(DependencyObject controlInstance, DependencyPropertyChangedEventArgs args)
        {
            var x = (WaveformElement)controlInstance;
            var oldval = args.OldValue as IWaveformBase;
            var newval = args.NewValue as IWaveformBase;
            //TODO could do with a way of figuring out whether oldval and newval are the same wave
            x.HandleWaveformChanged(true);
        }

        private void HandleWaveformChanged(bool isSameWave)
        {
            zoomLevel = MaxZoomLevel;
            if (!isSameWave)
            {
                Selection.Reset(0);
                PlayCursor.OffsetSamples = 0;
                PlayCursor.Offset = 0;
            }
            WaveformChangeSelectionAndCursorExtracted();
            WaveformChanged();
        }

        private void WaveformChangeSelectionAndCursorExtracted()
        {
            OnPropertyChanged("WaveformSelectionTuple");
            OnPropertyChanged("Offset");
            OnPropertyChanged("CursorOffset");
        }

		public double SamplesPerPixel
		{
			get
			{
				return (double)resolution / sampleWidth;
			}
		}

		protected void WaveformChanged()
		{
			//System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
			//sw.Reset();
			//sw.Start();

			resolution = (int)Math.Round(Math.Pow(2, 0.5 * zoomLevel));

            if (Waveform != null)
            {
                if (zoomLevel == MaxZoomLevel)
                {
                    resolution = FitResolution;
                }

                minMaxCache = new MinMaxCache(Waveform, resolution, segmentWidth);
            }
            else
            {
                minMaxCache = null;
            }

            RecreateVisuals();

			//sw.Stop();
            //editor.cb.WriteDC(string.Format("RecreateVisuals {0}ms", sw.ElapsedMilliseconds));

            SetHorizontalOffset(PlayCursor.OffsetSamples / resolution * sampleWidth - ViewportWidth / 2);


            OnPropertyChanged("WaveFormatString");

        }

		int FitResolution
		{
			get
			{
				if (Waveform == null)
					return 1;

				return Math.Max(1, (int)Math.Ceiling(Waveform.SampleCount / Math.Floor(ViewportWidth / sampleWidth)));
			}
		}

		int MaxZoomLevel
		{
			get
			{
				int n = (int)(2 * Math.Log(FitResolution) / Math.Log(2));
				return n;
			}
		}

        public void AdjustZoom(bool zoomin)
		{
			int old = zoomLevel;

			if (zoomin)
			{
				zoomLevel = Math.Min(MaxZoomLevel, zoomLevel + 1);
			}
			else
			{
				zoomLevel = Math.Max(1, zoomLevel - 1);
			}

			if (zoomLevel != old)
			{
				WaveformChanged();
			}

		}

		void RecreateVisuals()
		{
            //BuzzGUI.Common.Global.Buzz.DCWriteLine("RecreateVisuals");

            res = new SegmentVisual.Resources(this, (int)this.ActualHeight);

            allocatedSegments.Clear();
			children.Clear();

			int w = segmentWidth * sampleWidth;
			int n = (int)(ViewportWidth / w) + 2;

            for (int i = 0; i < n; i++)
            {
                children.Add(new SegmentVisual());
            }

            //add the 0 line visual
            DrawingVisual midlineDrawingVisual = new DrawingVisual();
            using (var dc = midlineDrawingVisual.RenderOpen())
            {
                dc.DrawRectangle(FindResource("MidlineBrush") as Brush, new Pen(), new Rect(0, (this.ActualHeight / 2), this.ActualWidth, 1.0));
            }
            children.Add(midlineDrawingVisual);

            //add a selection
            selectionDrawingVisual = new DrawingVisual();
            using (var dc = selectionDrawingVisual.RenderOpen())
            {
                dc.DrawRectangle(FindResource("SelectionBrush") as Brush, null, new Rect(0, 0, 0, this.ActualHeight));
            }
            children.Add(selectionDrawingVisual);

            //add adjustment target visual
            adjustmentTargetVisual = new DrawingVisual();
            using (var dc = adjustmentTargetVisual.RenderOpen())
            {
                dc.DrawRectangle(FindResource("AdjustmentTargetBrush") as Brush, new Pen(), new Rect(0, 0, 1.0, this.ActualHeight));
            }
            children.Add(adjustmentTargetVisual);

            //add the cursor visual
            cursorDrawingVisual = new DrawingVisual();
            using (var dc = cursorDrawingVisual.RenderOpen())
            {
                dc.DrawRectangle(FindResource("CursorBrush") as Brush, new Pen(), new Rect(0, 0, 1.0, this.ActualHeight));
            }
            children.Add(cursorDrawingVisual);


            if (minMaxCache != null)
            {
                extent.Width = minMaxCache.Length * sampleWidth;
            }
            else
            {
                extent.Width = 0;
            }

            if (owner != null)
            {
                owner.InvalidateScrollInfo();
            }

            //editor.cb.WriteDC(string.Format("[Wavetable] Created {0} visuals", n));
		}

		List<SegmentVisual> allocatedSegments = new List<SegmentVisual>(20);

		SegmentVisual AllocateSegment(int index)
		{
			foreach (SegmentVisual sv in children)
			{
    			if (!sv.Allocated)
				{
					sv.SampleIndex = index;
					sv.Render(this, minMaxCache, index, sampleWidth, (int)this.ActualHeight, res);
					sv.Allocated = true;
                    //BuzzGUI.Common.Global.Buzz.DCWriteLine(string.Format("[Wavetable] Allocated segment at {0}", index));
					allocatedSegments.Add(sv);
					return sv; //allocate and return the first free segment ?
				}
			}

			return null;
		}

		void FreeSegment(SegmentVisual sv)
		{
			System.Diagnostics.Debug.Assert(sv.Allocated);
			allocatedSegments.Remove(sv);
			sv.Allocated = false;
            //BuzzGUI.Common.Global.Buzz.DCWriteLine(string.Format("[Wavetable] Freed segment at {0}", sv.SampleIndex));
		}

		bool SegmentVisible(int index)
		{
			int w = segmentWidth * sampleWidth;
			double x = Math.Floor(index * w - offset.X);
            return x > -w && x < ViewportWidth; // original
            //return x > -w && x < ViewportWidth+w; //exceptions
            //return x > 0 && x < ViewportWidth; // first segment not drawn
            //return x > -64 && x < ViewportWidth; //exceptions
        }
		IEnumerable<SegmentVisual> SegmentVisualChildren
		{
			get
			{
				foreach (DrawingVisual dv in children)
				{
					SegmentVisual sv = dv as SegmentVisual;
					if (sv == null) continue;
					yield return sv;
				}
			}			
		}

		SegmentVisual GetSegmentAt(int index)
		{
			return SegmentVisualChildren.FirstOrDefault(x => x.Allocated && x.SampleIndex == index);
		}

        protected void UpdateVisuals()
        {
            if (minMaxCache == null)
            {
                return;
            }

            if (!IsVisible)
            {
                return;
            }

            List<SegmentVisual> temp = new List<SegmentVisual>(allocatedSegments);
            foreach (SegmentVisual sv in temp)
            {
                if (!SegmentVisible(sv.SampleIndex))
                {
                    sv.Offset = new Vector(Math.Floor(sv.SampleIndex * sampleWidth * segmentWidth - offset.X), sv.Offset.Y); //TODO this fixes drawing artefacts, but why does it?
                    FreeSegment(sv);
                }
            }

            for (int i = 0; i < Math.Ceiling((double)minMaxCache.Length / segmentWidth); i++)
            {
                SegmentVisual sv = GetSegmentAt(i);

                if (SegmentVisible(i))
                {
                    //BuzzGUI.Common.Global.Buzz.DCWriteLine(string.Format("sv {0} is visible", i.ToString()));
                    if (sv == null)
                    {
                        //BuzzGUI.Common.Global.Buzz.DCWriteLine(string.Format("sv {0} is null", i.ToString()));
                        sv = AllocateSegment(i);
                    }

                    //sv.Offset = new Vector(Math.Floor(i * sampleWidth * segmentWidth - offset.X), sv.Offset.Y);
                    sv.Offset = new Vector(Math.Floor(i * sampleWidth * segmentWidth - offset.X), sv.Offset.Y);
                }

            }

            UpdateCursorPosition();
            UpdateSelection();
            UpdateAdjustmentTargetVisual();
        }

		VisualCollection children;

		bool canHScroll = false, canVScroll = false;
		ScrollViewer owner;
		Size extent = new Size(3000, 200);
		Size viewport = new Size(0, 0);
		Point offset = new Point(0, 0);
		int sampleWidth = 1; //todo make user adjustable for slower machines ?
		int segmentWidth = 32;

		public WaveformElement()
		{
			children = new VisualCollection(this);
            PlayCursor = new WaveformCursor(this);
            Selection = new WaveformSelection(this);

			this.IsVisibleChanged += (sender, e) =>
			{
                if (IsVisible)
                {
                    Selection.PropertyChanged += OnSelectionChanged;
                    this.SizeChanged += new SizeChangedEventHandler(WaveformControl_SizeChanged);                    
                    UpdateVisuals();
                }
                else
                {
                    Selection.PropertyChanged -= OnSelectionChanged;
                    this.SizeChanged -= WaveformControl_SizeChanged;
                }
			};

            this.MouseDown += (sender, e) => 
            { 
                HandleMouseDown(e); 
            };

            HandleSelection();
            HandleMouseOver();
		}

        #region mouse interaction
        
        private void HandleMouseOver()
        {
            var mouseMove = Observable.FromEvent<MouseEventArgs>(this, "MouseMove");
            var query = from m in mouseMove
                        select m;

            mouseOverObserver = query.Subscribe(x =>
            {
                var pos = x.EventArgs.GetPosition(this).X;
                if (Selection.IsNearStart(pos) || Selection.IsNearEnd(pos))
                    Cursor = Cursors.SizeWE;
                else Cursor = Cursors.Arrow;
            });
        }

        
        private void HandleSelection()
        {
            var mouseDown = Observable.FromEvent<MouseButtonEventArgs>(this, "MouseDown").Where(m => m.EventArgs.ChangedButton == MouseButton.Left);
            var mouseUp = Observable.FromEvent<MouseButtonEventArgs>(this, "MouseUp").Where(m => m.EventArgs.ChangedButton == MouseButton.Left);
            var mouseMove = Observable.FromEvent<MouseEventArgs>(this, "MouseMove");
            var mouseLeave = Observable.FromEvent<MouseEventArgs>(this, "MouseLeave");
            var query = from x in mouseDown
                        from p in mouseMove.TakeUntil(mouseUp.Merge(mouseLeave))//SelectMany
                        .Do(_ => { }, () =>
                        {
                            FinishSelection();                        
                        })
                        select new { pos = p.EventArgs.GetPosition(this).X };

            observer = query.Subscribe(a =>
            {
                int currentMouseSample = PositionToSample(a.pos); //the current sample the mouse is at

                if (Selection.AdjustmentTarget.Equals(AdjustmentTargetValue.Start))
                {
                    if (Selection.StartSample <= Selection.EndSample)
                    {
                        Selection.StartSample = currentMouseSample;
                    }
                    else
                    {
                        //selection start is bigger than end, need to set start to end and then continue adjusting only end
                        Selection.StartSample = Selection.EndSample;
                        Selection.AdjustmentTarget = AdjustmentTargetValue.End;
                    }               
                }
                else if (Selection.AdjustmentTarget.Equals(AdjustmentTargetValue.End))
                {
                    if (Selection.EndSample >= Selection.StartSample)
                    {
                        Selection.EndSample = currentMouseSample;
                    }
                    else
                    {
                        //selection end is smaller than start, need to set end to start then continue adjusting only start
                        Selection.EndSample = Selection.StartSample;
                        Selection.AdjustmentTarget = AdjustmentTargetValue.Start;
                    }
                }
                else
                {
                    if (Selection.OriginSample > currentMouseSample)
                    {
                        Selection.StartSample = currentMouseSample;
                        Selection.EndSample = Selection.OriginSample;
                        Selection.AdjustmentTarget = AdjustmentTargetValue.Start;
                    }
                    else if (Selection.OriginSample < currentMouseSample)
                    {
                        Selection.StartSample = Selection.OriginSample;
                        Selection.EndSample = currentMouseSample;
                        Selection.AdjustmentTarget = AdjustmentTargetValue.End;
                    }
                }

                if (Selection.StartSample == Selection.EndSample)
                {
                    Selection.AdjustmentTarget = AdjustmentTargetValue.None;
                }

                //TODO FOR LATER it breaks keyboard logic currently (nudging a selection already made)
                //cursorSamplePos = Selection.StartSample; //we always want the cursor at the start of the selection so we can play it immediately

                InvalidateScrollInfo();
                OnPropertyChanged("Offset");
                OnPropertyChanged("CursorOffset");
            });
        }

        private void FinishSelection()
        {

            Mouse.Capture(null);

            if (Selection.StartSample < Selection.OriginSample)
            {
                //user selected from right to left so we want arrow keys to nudge the start
                Selection.AdjustmentTarget = AdjustmentTargetValue.Start;
            }
            else if (Selection.EndSample > Selection.OriginSample)
            {
                //user selected from left to right so we want arrow keys to nudge the end
                Selection.AdjustmentTarget = AdjustmentTargetValue.End;
            }
            else
            {
                //there is no selection so we let the cursor key logic decide what to nudge
                Selection.AdjustmentTarget = AdjustmentTargetValue.None;
            }

            UpdateAdjustmentTargetVisual();
        }

        private void HandleMouseDown(MouseButtonEventArgs e)
        {
            var args = e as MouseButtonEventArgs;
            if (args.ChangedButton.Equals(MouseButton.Left) && (args.ClickCount == 1)) // single leftclick
            {
                var downPos = e.GetPosition(this).X;
                Selection.OriginSample = PositionToSample(downPos);

                if (Cursor.Equals(Cursors.SizeWE))
                {
                    if (Selection.IsNearStart(downPos))
                    {
                        Selection.AdjustmentTarget = AdjustmentTargetValue.Start;
                        Selection.OriginSample = Selection.EndSample;
                    }
                    else if (Selection.IsNearEnd(downPos))
                    {
                        Selection.AdjustmentTarget = AdjustmentTargetValue.End;
                        Selection.OriginSample = Selection.StartSample;
                    }
                    else
                    {
                        PlayCursor.OffsetSamples = PositionToSample(downPos);
                        //adjustmentTarget = AdjustmentTarget.None;
                    }
                }
                else
                {
                    //normal cursor movement
                    PlayCursor.OffsetSamples = PositionToSample(downPos);
                    Selection.Reset(PlayCursor.OffsetSamples);
                    UpdateCursorPosition();
                }
                Mouse.Capture(this);
                InvalidateScrollInfo();
                OnPropertyChanged("Offset");
                OnPropertyChanged("CursorOffset");

                UpdateAdjustmentTargetVisual();
            }
            else if (args.ChangedButton.Equals(MouseButton.Left) && (args.ClickCount == 2)) // double leftclick
            {
                // TODO: allow for different behaviour based on in select, out of select
 
                // select all
                Selection.StartSample = 0;
                Selection.EndSample = Waveform.SampleCount;
                UpdateSelection();
            }

        }

        #endregion

        private void InvalidateScrollInfo()
        {
            if (owner != null) owner.InvalidateScrollInfo();
        }

		void WaveformControl_SizeChanged(object sender, SizeChangedEventArgs e)
		{
            //editor.cb.WriteDC(string.Format("[Wavetable] SizeChanged {0}, {1}", e.NewSize.Width, e.NewSize.Height));
			WaveformChanged();
		}

        #region FrameworkElement members
        
        protected override int VisualChildrenCount { get { return children.Count; } }
		protected override Visual GetVisualChild(int index)
		{
			if (index < 0 || index >= children.Count)
				throw new ArgumentOutOfRangeException();

			return (Visual)children[index];
		}

        protected override Size MeasureOverride(Size constraint)
		{
            //BuzzGUI.Common.Global.Buzz.DCWriteLine(string.Format("[Wavetable] Measure {0}, {1}", constraint.Width, constraint.Height));

            if (constraint.Width == double.PositiveInfinity)
            {
                constraint.Width = ViewportWidth;
            }

            if (constraint.Height == double.PositiveInfinity)
            {
                constraint.Height = ViewportHeight;
            }

			if (constraint != viewport)
			{
				viewport = constraint;

                if (owner != null)
                {
                    owner.InvalidateScrollInfo();
                }
			}

			return constraint;
		}

		protected override Size ArrangeOverride(Size arrangeBounds)
		{
            //editor.cb.WriteDC(string.Format("[Wavetable] Arrange {0}, {1}", arrangeBounds.Width, arrangeBounds.Height));
			return arrangeBounds;
		}

		public ScrollViewer ScrollOwner
		{
			get { return owner; }
			set { owner = value; }
		}

		public bool CanHorizontallyScroll
		{
			get { return canHScroll; }
			set { canHScroll = value; }
		}

		public bool CanVerticallyScroll
		{
			get { return canVScroll; }
			set { canVScroll = value; }
		}

		public double ExtentWidth { get { return extent.Width; } }
		public double ExtentHeight { get { return extent.Height; } }
		public double ViewportWidth { get { return viewport.Width; } }
		public double ViewportHeight { get { return viewport.Height; } }
		public double HorizontalOffset { get { return offset.X; } }
		public double VerticalOffset { get { return offset.Y; } }

		public void LineUp()
		{
			if (Keyboard.Modifiers == ModifierKeys.Control)
			{
				zoomLevel = 1;
				WaveformChanged();
			}
			else
			{
				AdjustZoom(false);
			}
		}

		public void LineDown()
		{
			if (Keyboard.Modifiers == ModifierKeys.Control)
			{
				zoomLevel = MaxZoomLevel;
				WaveformChanged();
		    }
			else
			{
				AdjustZoom(true);
			}
		}

		public void LineLeft()
		{
			int speed = 1;
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                speed = 32; 
            }

			if (Keyboard.Modifiers == ModifierKeys.Shift)
			{
                if (Selection.AdjustmentTarget == AdjustmentTargetValue.None)
                {
                    //there is no selection and the user moved to the left so we want to adjust the start
                    Selection.AdjustmentTarget = AdjustmentTargetValue.Start;
                }

                if (Selection.AdjustmentTarget.Equals(AdjustmentTargetValue.Start))
                {
                    if (Selection.StartSample <= Selection.EndSample)
                    {
                        Selection.StartSample -= (int)Math.Ceiling(SamplesPerPixel * speed);
                    }
                    else
                    {
                        //selection start is bigger than end, need to set start to end and then continue adjusting only end
                        Selection.StartSample = Selection.EndSample;
                        Selection.AdjustmentTarget = AdjustmentTargetValue.End;
                    }
                }
                else if (Selection.AdjustmentTarget.Equals(AdjustmentTargetValue.End))
                {
                    if (Selection.EndSample >= Selection.StartSample)
                    {
                        Selection.EndSample -= (int)Math.Ceiling(SamplesPerPixel * speed);
                    }
                    else
                    {
                        //selection end is smaller than start, need to set end to start then continue adjusting only start
                        Selection.EndSample = Selection.StartSample;
                        Selection.AdjustmentTarget = AdjustmentTargetValue.Start;
                    }
                }
			}
			else
			{
				// normal cursor movement
                PlayCursor.OffsetSamples -= (int)Math.Ceiling(SamplesPerPixel * speed);

                if (Selection.IsActive() == false)
                {
                    //if the selection is 0 samples we want the selection to move with the cursor
                    Selection.StartSample = PlayCursor.OffsetSamples;
                    Selection.EndSample = PlayCursor.OffsetSamples;
                }
                
                UpdateVisuals();
			}

            if (Selection.IsActive() == false)
            {
                //there is no selection after we moved start / end or the cursor, so the adjustment target must be reset and the next iteration decides what to adjust
                Selection.AdjustmentTarget = AdjustmentTargetValue.None;
            }

            OnPropertyChanged("Offset");
            OnPropertyChanged("CursorOffset");
        }

		public void LineRight()
		{
			int speed = 1;
			if (Keyboard.Modifiers == ModifierKeys.Control) 
            {
                speed = 32;
            }

			if (Keyboard.Modifiers == ModifierKeys.Shift)
			{
                if (Selection.AdjustmentTarget == AdjustmentTargetValue.None)
                {
                    //there is no selection and the user moved to the right so we want to adjust the end
                    Selection.AdjustmentTarget = AdjustmentTargetValue.End;
                }

                if (Selection.AdjustmentTarget.Equals(AdjustmentTargetValue.Start))
                {
                    if (Selection.StartSample <= Selection.EndSample)
                    {
                        Selection.StartSample += (int)Math.Ceiling(SamplesPerPixel * speed);
                    }
                    else
                    {
                        //selection start is bigger than end, need to set start to end and then continue adjusting only end
                        Selection.StartSample = Selection.EndSample;
                        Selection.AdjustmentTarget = AdjustmentTargetValue.End;
                    }
                }
                else if (Selection.AdjustmentTarget.Equals(AdjustmentTargetValue.End))
                {
                    if (Selection.EndSample >= Selection.StartSample)
                    {
                        Selection.EndSample += (int)Math.Ceiling(SamplesPerPixel * speed);
                    }
                    else
                    {
                        //selection end is smaller than start, need to set end to start then continue adjusting only start
                        Selection.EndSample = Selection.StartSample;
                        Selection.AdjustmentTarget = AdjustmentTargetValue.Start;
                    }
                }
            }
			else
			{
                // normal cursor movement
                PlayCursor.OffsetSamples += (int)Math.Ceiling(SamplesPerPixel * speed);

                if (Selection.IsActive() == false)
                {
                    //if the selection is 0 samples we want the selection to move with the cursor
                    Selection.StartSample = PlayCursor.OffsetSamples;
                    Selection.EndSample = PlayCursor.OffsetSamples;
                }

                UpdateVisuals();
			}

            if (Selection.IsActive() == false)
            {
                //there is no selection after we moved start / end or the cursor, so the adjustment target must be reset and the next iteration decides what to adjust
                Selection.AdjustmentTarget = AdjustmentTargetValue.None;
            }

            OnPropertyChanged("Offset");
            OnPropertyChanged("CursorOffset");
        }

		public Rect MakeVisible(Visual visual, Rect rectangle)
		{
			return new Rect();
		}

		public void MouseWheelDown()
		{
		}

		public void MouseWheelLeft()
		{
		}

		public void MouseWheelRight()
		{
		}

		public void MouseWheelUp()
		{
		}

		public void PageUp()
		{
			SetHorizontalOffset(HorizontalOffset - 64);
		}

		public void PageDown()
		{
			SetHorizontalOffset(HorizontalOffset + 64);
		}

		public void PageLeft()
		{
			SetHorizontalOffset(HorizontalOffset - ViewportWidth);
		}

		public void PageRight()
		{
			SetHorizontalOffset(HorizontalOffset + ViewportWidth);
		}

		public void SetHorizontalOffset(double newoffset)
		{
			if (newoffset < 0 || viewport.Width >= extent.Width)
			{
				newoffset = 0;
			}
			else
			{
                if (newoffset + viewport.Width >= extent.Width)
                {
                    newoffset = extent.Width - viewport.Width;
                }
			}

			offset.X = newoffset;

            if (owner != null)
            {
                owner.InvalidateScrollInfo();
            }

			UpdateVisuals();
		}

		public void SetVerticalOffset(double newoffset)
		{
			if (newoffset < 0 || viewport.Height >= extent.Height)
			{
				newoffset = 0;
			}
			else
			{
                if (newoffset + viewport.Height >= extent.Height)
                {
                    newoffset = extent.Height - viewport.Height;
                }
			}

			offset.Y = newoffset;

            if (owner != null)
            {
                owner.InvalidateScrollInfo();
            }
		}
        #endregion

        #region INofityPropertyChanged
        
        public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged(string propertyName)
		{
			PropertyChangedEventHandler handler = this.PropertyChanged;
			if (handler != null)
			{
				var e = new PropertyChangedEventArgs(propertyName);
				handler(this, e);
			}
		}
        #endregion

        #region Cursor and Selection Related

        public Tuple<IWaveformBase, WaveformSelection> WaveformSelectionTuple
        {
            get { return Tuple.Create(Waveform, Selection); }
        }

        internal int PositionToSample(double pos)
        {
            if (pos < 0)
            {
                pos = 0;
            }
            return Math.Min(Waveform.SampleCount, (int)Math.Floor((pos + offset.X) * resolution / sampleWidth));
        }

        internal double SampleToPosition(double sample)
        {
            return Waveform == null ? 0 : (ExtentWidth * (sample / (Waveform.SampleCount))) - offset.X;
        }

        void UpdateCursorPosition()
        {
            //TODO REFACTOR INTO CURSOR CLASS
            PlayCursor.Offset = PlayCursor.OffsetSamples / resolution * sampleWidth;
            cursorDrawingVisual.Offset = new Vector(Math.Floor(PlayCursor.Offset - offset.X), 0.0);
        }

        void UpdateSelection()
        {
            selectionDrawingVisual.Offset = new Vector(Math.Floor(Selection.Start), 0.0);

            using (var dc = selectionDrawingVisual.RenderOpen())
            {
                dc.DrawRectangle(FindResource("SelectionBrush") as Brush, null, new Rect(0, 0, Selection.Width, this.ActualHeight));
            }
        }

        void UpdateAdjustmentTargetVisual()
        {
            switch (Selection.AdjustmentTarget)
            {
                case AdjustmentTargetValue.None:
                    //adjustmentTargetVisual.Offset = new Vector(Math.Floor(PlayCursor.Offset), 0.0);
                    //adjustmentTargetVisual.Offset = cursorDrawingVisual.Offset;
                    adjustmentTargetVisual.Offset = new Vector(Math.Floor(PlayCursor.Offset - offset.X), 0.0);
                    break;
                case AdjustmentTargetValue.Start:
                    adjustmentTargetVisual.Offset = new Vector(Math.Floor(Selection.Start), 0.0);
                    break;
                case AdjustmentTargetValue.End:
                    adjustmentTargetVisual.Offset = new Vector(Math.Floor(Selection.End), 0.0);
                    break;
                default:
                    break;
            }
        }

        public string OffsetString
        {
            get 
            {
                double frac = (PlayCursor.Offset / ExtentWidth);
                return string.Format("CURSOR: {0} (samples) | {0:X4} (hex%)", PlayCursor.OffsetSamples, (int)(frac * 0xFFFE)); 
            }
        }

        public string SelectionString
        {
            get
            {
                if (Selection != null)
                {
                    return string.Format("SELECTION: {0} to {1} | {2} (samples)", Selection.StartSample, Selection.EndSample, Selection.LengthInSamples);
                }
                else
                {
                    return "No Selection";
                }
            }
        }

        public string WaveFormatString
        {
            get
            {
                if (Waveform != null)
                {
                    return string.Format("{0} | {1}ch", Waveform.Format.ToString(), Waveform.ChannelCount.ToString());
                }
                else
                {
                    return "HELLO";
                }
            }
        }
        
        #endregion
        
    }
}
