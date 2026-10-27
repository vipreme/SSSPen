using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SSSPen;

public enum Tool { Pen, Line, Rect, Ellipse, Arrow, Eraser }

/// <summary>Создание и обновление фигур — общее для оверлея и редактора скриншота.</summary>
public static class ShapeFactory
{
    public static Shape Create(Tool tool, Point start, Brush brush, double thickness)
    {
        switch (tool)
        {
            case Tool.Line:
                return new Line
                {
                    X1 = start.X, Y1 = start.Y, X2 = start.X, Y2 = start.Y,
                    Stroke = brush, StrokeThickness = thickness,
                    StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
                };
            case Tool.Rect:
                return new Rectangle { Stroke = brush, StrokeThickness = thickness, StrokeLineJoin = PenLineJoin.Round };
            case Tool.Ellipse:
                return new Ellipse { Stroke = brush, StrokeThickness = thickness };
            case Tool.Arrow:
                // стрелка — один залитый многоугольник (без обводки), поэтому кончик остаётся острым
                return new Path { Fill = brush, StrokeThickness = thickness };
            default: // Pen — ломаная
                return new Polyline
                {
                    Stroke = brush, StrokeThickness = thickness,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
                };
        }
    }

    public static void Update(Shape shape, Tool tool, Point start, Point p, double thickness)
    {
        switch (tool)
        {
            case Tool.Pen:
                ((Polyline)shape).Points.Add(p);
                break;

            case Tool.Line:
            {
                var l = (Line)shape;
                l.X2 = p.X;
                l.Y2 = p.Y;
                break;
            }

            case Tool.Rect:
            case Tool.Ellipse:
                System.Windows.Controls.Canvas.SetLeft(shape, Math.Min(start.X, p.X));
                System.Windows.Controls.Canvas.SetTop(shape, Math.Min(start.Y, p.Y));
                shape.Width = Math.Abs(p.X - start.X);
                shape.Height = Math.Abs(p.Y - start.Y);
                break;

            case Tool.Arrow:
            {
                var path = (Path)shape;
                Vector d = p - start;
                double length = d.Length;

                if (length < 2)
                {
                    path.Data = new EllipseGeometry(start, thickness / 2, thickness / 2);
                    break;
                }

                d.Normalize();
                var n = new Vector(-d.Y, d.X);

                double half = thickness / 2;                                   // полуширина древка
                double head = Math.Min(10 + thickness * 2, length);            // длина наконечника (короткий)
                double hw = Math.Max(head * 0.22, half + 1.5 + thickness * 0.4); // полуширина наконечника (узкий -> острый)
                Point b = p - d * head;                                        // основание наконечника

                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(start + n * half, true, true);
                    ctx.LineTo(b + n * half, true, false);
                    ctx.LineTo(b + n * hw, true, false);
                    ctx.LineTo(p, true, false);                                // острый кончик
                    ctx.LineTo(b - n * hw, true, false);
                    ctx.LineTo(b - n * half, true, false);
                    ctx.LineTo(start - n * half, true, false);
                }
                g.Freeze();
                path.Data = g;
                break;
            }
        }
    }

    /// <summary>Попал ли курсор (с запасом) в линию/фигуру — для ластика.</summary>
    public static bool Hits(Shape shape, UIElement parent, Point p, double tolerance = 8)
    {
        Geometry geo = shape.RenderedGeometry;
        if (geo == null || geo.IsEmpty()) return false;

        Point local = parent.TranslatePoint(p, shape);
        var pen = new Pen(Brushes.Black, shape.StrokeThickness + tolerance * 2);
        if (geo.StrokeContains(pen, local)) return true;
        return shape.Fill != null && geo.FillContains(local);
    }
}
