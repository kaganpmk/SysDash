using System.Windows;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace SysDash.Controls;

public class SparklineChart : FrameworkElement
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(IList<float>), typeof(SparklineChart),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineColorProperty =
        DependencyProperty.Register(nameof(LineColor), typeof(Color), typeof(SparklineChart),
            new FrameworkPropertyMetadata(Color.FromRgb(0x5C, 0x8A, 0x69), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillColorProperty =
        DependencyProperty.Register(nameof(FillColor), typeof(Color), typeof(SparklineChart),
            new FrameworkPropertyMetadata(Color.FromArgb(50, 0x2A, 0x3F, 0x30), FrameworkPropertyMetadataOptions.AffectsRender));

    public IList<float>? Data
    {
        get => (IList<float>?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public Color LineColor
    {
        get => (Color)GetValue(LineColorProperty);
        set => SetValue(LineColorProperty, value);
    }

    public Color FillColor
    {
        get => (Color)GetValue(FillColorProperty);
        set => SetValue(FillColorProperty, value);
    }

    protected override void OnRender(System.Windows.Media.DrawingContext dc)
    {
        base.OnRender(dc);

        var data = Data;
        if (data == null || data.Count < 2) return;

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0) return;

        var min = data.Min();
        var max = data.Max();
        var range = max - min;

        if (range < 1) range = 1;

        var points = new Point[data.Count];
        for (int i = 0; i < data.Count; i++)
        {
            var x = i * (width - 2) / (data.Count - 1) + 1;
            var y = height - 2 - ((data[i] - min) / range) * (height - 4);
            points[i] = new Point(x, y);
        }

        var linePen = new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(LineColor), 1.5);

        var geometry = new System.Windows.Media.StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], false, false);
            for (int i = 1; i < points.Length; i++)
                ctx.LineTo(points[i], true, false);
        }
        geometry.Freeze();

        dc.DrawGeometry(null, linePen, geometry);

        if (data.Count > 1)
        {
            var fillGeometry = new System.Windows.Media.StreamGeometry();
            using (var ctx = fillGeometry.Open())
            {
                ctx.BeginFigure(new Point(points[0].X, height - 2), true, false);
                ctx.LineTo(points[0], true, false);
                for (int i = 1; i < points.Length; i++)
                    ctx.LineTo(points[i], true, false);
                ctx.LineTo(new Point(points[^1].X, height - 2), true, false);
                ctx.LineTo(new Point(points[0].X, height - 2), true, false);
            }
            fillGeometry.Freeze();
            dc.DrawGeometry(new System.Windows.Media.SolidColorBrush(FillColor), null, fillGeometry);
        }
    }
}
