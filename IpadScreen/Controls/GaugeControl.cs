using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Pen = System.Windows.Media.Pen;
using Size = System.Windows.Size;

namespace IpadScreen.Controls;

public class GaugeControl : FrameworkElement
{
    public static readonly DependencyProperty PercentageProperty =
        DependencyProperty.Register(nameof(Percentage), typeof(double), typeof(GaugeControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GaugeColorProperty =
        DependencyProperty.Register(nameof(GaugeColor), typeof(Color), typeof(GaugeControl),
            new FrameworkPropertyMetadata(Color.FromRgb(0x5C, 0x8A, 0x69), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackColorProperty =
        DependencyProperty.Register(nameof(TrackColor), typeof(Color), typeof(GaugeControl),
            new FrameworkPropertyMetadata(Color.FromRgb(0x35, 0x39, 0x40), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(GaugeControl),
            new FrameworkPropertyMetadata(14.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(GaugeControl),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SubLabelProperty =
        DependencyProperty.Register(nameof(SubLabel), typeof(string), typeof(GaugeControl),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public double Percentage
    {
        get => (double)GetValue(PercentageProperty);
        set => SetValue(PercentageProperty, value);
    }

    public Color GaugeColor
    {
        get => (Color)GetValue(GaugeColorProperty);
        set => SetValue(GaugeColorProperty, value);
    }

    public Color TrackColor
    {
        get => (Color)GetValue(TrackColorProperty);
        set => SetValue(TrackColorProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string SubLabel
    {
        get => (string)GetValue(SubLabelProperty);
        set => SetValue(SubLabelProperty, value);
    }

    private readonly Typeface _typeface = new("Consolas");

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var w = ActualWidth;
        var h = ActualHeight;
        var size = Math.Min(w, h);
        if (size <= 0) return;

        var cx = w / 2;
        var cy = h / 2;
        var radius = (size - StrokeThickness) / 2 - 2;
        var startAngle = 135.0;
        var sweepAngle = 270.0;

        var trackPen = new Pen(new SolidColorBrush(TrackColor), StrokeThickness)
        { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var gaugePen = new Pen(new SolidColorBrush(GaugeColor), StrokeThickness)
        { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

        var trackArc = CreateArc(cx, cy, radius, startAngle, sweepAngle);
        var filledAngle = sweepAngle * Math.Clamp(Percentage / 100.0, 0, 1);
        var gaugeArc = CreateArc(cx, cy, radius, startAngle, filledAngle);

        if (trackArc != null) dc.DrawGeometry(null, trackPen, trackArc);
        if (gaugeArc != null) dc.DrawGeometry(null, gaugePen, gaugeArc);

        if (!string.IsNullOrEmpty(Label) || !string.IsNullOrEmpty(SubLabel))
        {
            var textColor = new SolidColorBrush(GaugeColor);
            textColor.Freeze();

            if (!string.IsNullOrEmpty(Label))
            {
                var ft = new FormattedText(Label, Culture, System.Windows.FlowDirection.LeftToRight, _typeface, 16, textColor, 96)
                { TextAlignment = TextAlignment.Center };
                dc.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height - 2));
            }

            if (!string.IsNullOrEmpty(SubLabel))
            {
                var ft = new FormattedText(SubLabel, Culture, System.Windows.FlowDirection.LeftToRight, _typeface, 11, textColor, 96)
                { TextAlignment = TextAlignment.Center };
                dc.DrawText(ft, new Point(cx - ft.Width / 2, cy + 2));
            }
        }
    }

    private static System.Globalization.CultureInfo Culture => System.Globalization.CultureInfo.CurrentCulture;

    private static StreamGeometry? CreateArc(double cx, double cy, double radius, double startAngle, double sweepAngle)
    {
        if (sweepAngle <= 0) return null;

        var isLarge = sweepAngle > 180.0;
        var startRad = startAngle * Math.PI / 180.0;
        var endRad = (startAngle + sweepAngle) * Math.PI / 180.0;

        var x1 = cx + radius * Math.Cos(startRad);
        var y1 = cy + radius * Math.Sin(startRad);
        var x2 = cx + radius * Math.Cos(endRad);
        var y2 = cy + radius * Math.Sin(endRad);

        var arcSize = new System.Windows.Size(radius, radius);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(x1, y1), false, false);
            ctx.ArcTo(new Point(x2, y2), arcSize, 0, isLarge, SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        return geometry;
    }
}
