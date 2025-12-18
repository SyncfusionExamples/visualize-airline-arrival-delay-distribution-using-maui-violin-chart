using Syncfusion.Maui.Charts;
using System;
using System.Collections.Generic;
using System.Text;

namespace ViolinChart
{ 
    public class BoxAndWhiskerSeriesViolin : BoxAndWhiskerSeries
    { 
        public float ViolinStrokeThickness { get; set; } = 2f;

        // Shape tuning.
        public float MaxHalfWidthFraction { get; set; } = 0.7f;
        public float PinchAmount { get; set; } = 0.6f;
        public float PinchSigma { get; set; } = 0.09f;

        // Vertical visuals.
        public float WhiskerThickness { get; set; } = 2.2f;
        public float IqrBarWidthFraction { get; set; } = 0.08f;
        public float IqrBarHeightFraction { get; set; } = 0.70f;
        public float IqrBarMinHeightPixels { get; set; } = 8f; 
        public float IqrFillOpacity { get; set; } = 0.35f;

        // Gaps.
        public float TipGapPixels { get; set; } = 7f;
        public float WhiskerShortenPixels { get; set; } = 6f;
        public float WhiskerLengthFraction { get; set; } = 0.55f;
        public float MinWhiskerPixels { get; set; } = 7f;

        protected override ChartSegment CreateSegment()
        {
            return new BoxAndWhiskerSegmentViolin(this);
        }
    }

    public class BoxAndWhiskerSegmentViolin : BoxAndWhiskerSegment
    {
        private readonly BoxAndWhiskerSeriesViolin _owner;

        // Base box (Q1..Q3) pixels computed by Syncfusion.
        private float _left, _right, _top, _bottom;
        private float _centerX, _halfWidth;

        // Derived Y positions (using only Q1..Q3, NOT extended tips).
        private float _yQ1, _yQ3, _yMedian;

        public BoxAndWhiskerSegmentViolin(BoxAndWhiskerSeriesViolin owner)
        {
            _owner = owner;
        }

        protected override void OnLayout()
        {
            base.OnLayout();

            _left = Left;
            _right = Right;
            _top = Top;
            _bottom = Bottom;

            if (float.IsNaN(_left) || float.IsNaN(_right) || float.IsNaN(_top) || float.IsNaN(_bottom))
                return;

            _centerX = (_left + _right) * 0.5f;
            _halfWidth = MathF.Max(1f, (_right - _left) * 0.5f);

            // Use only the Q1..Q3 range for the violin body.
            _yQ1 = _top;       
            _yQ3 = _bottom;    
            _yMedian = (_yQ1 + _yQ3) * 0.5f;
        }

        protected override void Draw(ICanvas canvas)
        {
            if (float.IsNaN(_left) || float.IsNaN(_right) || float.IsNaN(_top) || float.IsNaN(_bottom))
                return;

            DrawViolin(canvas);
            DrawVerticals(canvas);
        }

        private void DrawViolin(ICanvas canvas)
        {
            // Violin body spans from Q1 to Q3 ONLY (original box height).
            float yTop = MathF.Min(_yQ1, _yQ3);
            float yBottom = MathF.Max(_yQ1, _yQ3);
            float span = MathF.Max(1f, yBottom - yTop);

            float maxHalf = _owner.MaxHalfWidthFraction * _halfWidth;

            // Shape parameters.
            float basePow = 1.15f;
            float a = Math.Clamp(_owner.PinchAmount, 0f, 0.9f);
            float s = Math.Clamp(_owner.PinchSigma, 0.06f, 0.20f);

            // Width profile - creates the violin shape.
            float WidthAt(float y)
            {
                float t = (y - yTop) / span;
                t = Math.Clamp(t, 0f, 1f);

                // Bell shape (pointed tips at 0 and 1).
                float baseShape = MathF.Sin(MathF.PI * t);
                baseShape = MathF.Pow(MathF.Max(0f, baseShape), basePow);

                // Waist pinch (concave sides).
                float g = MathF.Exp(-((t - 0.5f) * (t - 0.5f)) / (2f * s * s));
                float pinch = 1f - a * g;

                return MathF.Max(0f, maxHalf * baseShape * pinch);
            }

            // Generate outline points (top -> bottom).
            int samples = 64;
            float[] ys = Linspace(yTop, yBottom, samples);

            var right = new List<PointF>(samples);
            var left = new List<PointF>(samples);

            foreach (float y in ys)
            {
                float w = WidthAt(y);
                right.Add(new PointF(_centerX + w, y));
                left.Add(new PointF(_centerX - w, y));
            }

            if (right.Count < 2 || left.Count < 2)
                return;

            // Tip radii for round caps (distance from center to side at the ends).
            float rTop = MathF.Max(0f, right[0].X - _centerX);
            float rBottom = MathF.Max(0f, right[^1].X - _centerX);

            // Build path.
            var path = new PathF();

            // Start at top-right.
            path.MoveTo(right[0]);

            // Right side (top -> bottom) with smooth curve.
            for (int i = 0; i < right.Count - 1; i++)
            {
                PointF p0 = right[i];
                PointF p1 = right[i + 1];
                PointF c1 = new PointF(p0.X + (p1.X - p0.X) * 0.33f, p0.Y + (p1.Y - p0.Y) * 0.33f);
                PointF c2 = new PointF(p0.X + (p1.X - p0.X) * 0.66f, p0.Y + (p1.Y - p0.Y) * 0.66f);
                path.CurveTo(c1, c2, p1);
            }

            // Bottom semicircle cap: from right-bottom to left-bottom, bulging upward.
            if (rBottom > 0.5f)
            {
                AddBottomSemiCircleCap(path, _centerX, yBottom, rBottom);
            }
            else
            {
                // Fallback straight bridge if extremely thin.
                path.LineTo(left[^1]);
            }

            // Left side (bottom -> top) with smooth curve.
            for (int i = left.Count - 1; i > 0; i--)
            {
                PointF p0 = left[i];
                PointF p1 = left[i - 1];
                PointF c1 = new PointF(p0.X + (p1.X - p0.X) * 0.66f, p0.Y + (p1.Y - p0.Y) * 0.66f);
                PointF c2 = new PointF(p0.X + (p1.X - p0.X) * 0.33f, p0.Y + (p1.Y - p0.Y) * 0.33f);
                path.CurveTo(c1, c2, p1);
            }

            // Top semicircle cap: from left-top to right-top, bulging downward.
            if (rTop > 0.5f)
            {
                AddTopSemiCircleCap(path, _centerX, yTop, rTop);
            }
            else
            {
                // Fallback straight bridge if extremely thin.
                path.LineTo(right[0]);
            }

            // Draw: outline only (transparent interior).
            var primary = GetSeriesFillColor();
            float strokeSize = GetSeriesStrokeWidthOrFallback();

            canvas.SaveState();
            canvas.StrokeColor = primary;
            canvas.StrokeSize = strokeSize;
            canvas.DrawPath(path);
            canvas.RestoreState();
        }

        // Add a semicircle from left-top to right-top, bulging downward (within y >= yTop).
        private static void AddTopSemiCircleCap(PathF path, float cx, float yTop, float r)
        {
            const float k = 0.552284749831f; // cubic Bezier circle constant.

            PointF left = new PointF(cx - r, yTop);
            PointF right = new PointF(cx + r, yTop);
            PointF mid = new PointF(cx, yTop + r);

            // Assumes current path point is left.
            PointF c1 = new PointF(left.X, left.Y + k * r);
            PointF c2 = new PointF(mid.X - k * r, mid.Y);
            path.CurveTo(c1, c2, mid);

            PointF c3 = new PointF(mid.X + k * r, mid.Y);
            PointF c4 = new PointF(right.X, right.Y + k * r);
            path.CurveTo(c3, c4, right);
        }

        // Add a semicircle from right-bottom to left-bottom, bulging upward (within y <= yBottom).
        private static void AddBottomSemiCircleCap(PathF path, float cx, float yBottom, float r)
        {
            const float k = 0.552284749831f;

            PointF right = new PointF(cx + r, yBottom);
            PointF left = new PointF(cx - r, yBottom);
            PointF mid = new PointF(cx, yBottom - r);

            // Assumes current path point is right.
            PointF c1 = new PointF(right.X, right.Y - k * r);
            PointF c2 = new PointF(mid.X + k * r, mid.Y);
            path.CurveTo(c1, c2, mid);

            PointF c3 = new PointF(mid.X - k * r, mid.Y);
            PointF c4 = new PointF(left.X, left.Y - k * r);
            path.CurveTo(c3, c4, left);
        }

        private void DrawVerticals(ICanvas canvas)
        {
            var primary = GetSeriesFillColor();
            float strokeSize = MathF.Max(GetSeriesStrokeWidthOrFallback(), _owner.WhiskerThickness);

            // Whisker bounds within Q1..Q3, reduced by tip gaps.
            float baseGap = MathF.Max(0f, _owner.TipGapPixels);
            float extra = MathF.Max(0f, _owner.WhiskerShortenPixels);
            float safeGap = baseGap + extra;

            float fullSpan = MathF.Max(0f, _yQ3 - _yQ1);
            float maxDrawable = MathF.Max(0f, fullSpan - 2f * safeGap);

            float frac = Math.Clamp(_owner.WhiskerLengthFraction, 0f, 1f);
            float whiskerLen = MathF.Max(_owner.MinWhiskerPixels, maxDrawable * frac);

            float start = _yQ1 + safeGap + (maxDrawable - whiskerLen) * 0.5f;
            float end = start + whiskerLen;

            start = MathF.Max(_yQ1 + 1f, MathF.Min(start, _yQ3 - 1f));
            end = MathF.Max(start + 1f, MathF.Min(end, _yQ3 - 1f));

            // Whisker (center vertical).
            canvas.SaveState();
            canvas.StrokeColor = primary;
            canvas.StrokeSize = strokeSize;
            canvas.DrawLine(_centerX, start, _centerX, end);
            canvas.RestoreState();

            // IQR box dimensions.
            float barHalf = MathF.Max(1.5f, _owner.IqrBarWidthFraction * _halfWidth);
            float trueIqrH = MathF.Abs(_yQ3 - _yQ1);
            float iqrFrac = Math.Clamp(_owner.IqrBarHeightFraction, 0f, 1f);
            float rectH = MathF.Max(_owner.IqrBarMinHeightPixels, trueIqrH * iqrFrac);

            float mid = (_yQ1 + _yQ3) * 0.5f;
            float rectY = mid - rectH * 0.5f;
            var rect = new RectF(_centerX - barHalf, rectY, barHalf * 2f, rectH);

            // IQR body: fill with decreased opacity of the series Fill color.
            float opacity = Math.Clamp(_owner.IqrFillOpacity, 0f, 1f);
            Color iqrFill = primary.WithAlpha(opacity);

            canvas.SaveState();
            canvas.FillColor = iqrFill;
            canvas.FillRectangle(rect);

            // IQR outline + median tick using stroke color.
            canvas.StrokeColor = primary;
            canvas.StrokeSize = strokeSize;
            canvas.DrawRectangle(rect);

            // Median tick (vertical, centered).
            float cap = MathF.Max(2f, barHalf * 0.4f);
            canvas.DrawLine(_centerX, _yMedian - cap, _centerX, _yMedian + cap);

            canvas.RestoreState();
        }

        private Color GetSeriesFillColor()
        {
            // Use the series Fill (SolidColorBrush) as the source color; fallback to black if not solid.
            if (Fill is SolidColorBrush solid)
                return solid.Color;
            return Colors.Black;
        }

        private float GetSeriesStrokeWidthOrFallback()
        {
            // Prefer the series StrokeWidth if set; otherwise fallback to ViolinStrokeThickness.
            return (float)(_owner.StrokeWidth > 0 ? _owner.StrokeWidth : _owner.ViolinStrokeThickness);
        }

        private static float[] Linspace(float start, float end, int count)
        {
            if (count <= 1) return new[] { start };
            float[] arr = new float[count];
            float step = (end - start) / (count - 1);
            for (int i = 0; i < count; i++)
                arr[i] = start + step * i;
            return arr;
        }
    }
}
