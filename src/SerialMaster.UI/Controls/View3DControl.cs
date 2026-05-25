using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace SerialMaster.UI.Controls;

public class View3DControl : UserControl
{
    private readonly PerspectiveCamera _camera;
    private readonly Model3DGroup _scene;
    private readonly Model3DGroup _staticGeometry;
    private readonly Model3DGroup _dynamicPoints;
    private readonly RotateTransform3D _rotation;
    private readonly AxisAngleRotation3D _rotX, _rotY;
    private Point _lastMouse;
    private int _pointCount;

    private static readonly Color AxisXColor = Color.FromRgb(0xF3, 0x8B, 0xA8);
    private static readonly Color AxisYColor = Color.FromRgb(0xA6, 0xE3, 0xA1);
    private static readonly Color AxisZColor = Color.FromRgb(0x89, 0xB4, 0xFA);

    public View3DControl()
    {
        Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E));

        _rotX = new AxisAngleRotation3D(new Vector3D(1, 0, 0), -30);
        _rotY = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 45);

        var tg = new Transform3DGroup();
        tg.Children.Add(new RotateTransform3D(_rotY));
        tg.Children.Add(new RotateTransform3D(_rotX));
        _rotation = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0));
        tg.Children.Add(_rotation);

        _camera = new PerspectiveCamera(
            new Point3D(6, 4, 8), new Vector3D(-6, -4, -8), new Vector3D(0, 1, 0), 50);

        _scene = new Model3DGroup();
        _staticGeometry = new Model3DGroup();
        _dynamicPoints = new Model3DGroup();
        _scene.Children.Add(new AmbientLight(Color.FromRgb(0x50, 0x50, 0x50)));
        _scene.Children.Add(new DirectionalLight(
            Color.FromRgb(0x90, 0x90, 0x90), new Vector3D(1, -1, -1)));
        _scene.Children.Add(new DirectionalLight(
            Color.FromRgb(0x40, 0x40, 0x40), new Vector3D(-1, 0, 1)));

        BuildStaticGeometry();

        _scene.Children.Add(_staticGeometry);
        _scene.Children.Add(_dynamicPoints);

        var model = new ModelVisual3D
        {
            Content = _scene,
            Transform = tg
        };

        var vp = new Viewport3D { Camera = _camera };
        vp.Children.Add(model);

        // Legend overlay — pass through mouse events
        var legend = new StackPanel
        {
            Margin = new Thickness(10),
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsHitTestVisible = false
        };
        legend.Children.Add(CreateLegendItem("X轴", AxisXColor));
        legend.Children.Add(CreateLegendItem("Y轴", AxisYColor));
        legend.Children.Add(CreateLegendItem("Z轴", AxisZColor));
        legend.Children.Add(new TextBlock
        {
            Text = "拖拽旋转 | 滚轮缩放",
            Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x70, 0x86)),
            FontSize = 10,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            Margin = new Thickness(0, 4, 0, 0)
        });

        var grid = new Grid { Background = Brushes.Transparent };
        grid.Children.Add(vp);
        grid.Children.Add(legend);
        Content = grid;

        grid.MouseMove += (_, e) =>
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(vp);
                double dx = pos.X - _lastMouse.X;
                double dy = pos.Y - _lastMouse.Y;
                _rotY.Angle = (_rotY.Angle + dx * 0.5) % 360;
                _rotX.Angle = (_rotX.Angle + dy * 0.5) % 360;
                _lastMouse = pos;
            }
        };
        grid.MouseDown += (_, e) =>
        {
            grid.Focus();
            _lastMouse = e.GetPosition(vp);
            grid.CaptureMouse();
        };
        grid.MouseUp += (_, _) => grid.ReleaseMouseCapture();
        grid.MouseWheel += (_, e) =>
        {
            var dir = _camera.Position - new Point3D(0, 0, 0);
            double dist = dir.Length;
            dir.Normalize();
            double newDist = Math.Clamp(dist - e.Delta * 0.01, 3, 25);
            _camera.Position = new Point3D(0, 0, 0) + dir * newDist;
            _camera.LookDirection = new Vector3D(-_camera.Position.X, -_camera.Position.Y, -_camera.Position.Z);
        };
    }

    private static UIElement CreateLegendItem(string label, Color color)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
        sp.Children.Add(new System.Windows.Shapes.Rectangle
        {
            Width = 12, Height = 12, Fill = new SolidColorBrush(color),
            RadiusX = 2, RadiusY = 2, Margin = new Thickness(0, 0, 4, 0)
        });
        sp.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4)),
            FontSize = 11,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            VerticalAlignment = VerticalAlignment.Center
        });
        return sp;
    }

    private void BuildStaticGeometry()
    {
        double range = 5;
        int gridLines = 10;

        for (int i = 0; i <= gridLines; i++)
        {
            double pos = -range + (2 * range * i / gridLines);
            byte alpha = i == gridLines / 2 ? (byte)0x40 : (byte)0x18;
            uint color = (uint)((alpha << 24) | 0xFFFFFF);
            AddLine3D(new Point3D(pos, 0, -range), new Point3D(pos, 0, range), color);
            AddLine3D(new Point3D(-range, 0, pos), new Point3D(range, 0, pos), color);
        }

        double tickSize = 0.2;
        int ticksPerAxis = 10;

        // X axis
        AddThickLine3D(new Point3D(-range, 0, 0), new Point3D(range, 0, 0), AxisXColor, 0.04);
        for (int i = 0; i <= ticksPerAxis; i++)
        {
            double pos = -range + (2 * range * i / ticksPerAxis);
            AddLine3D(new Point3D(pos, -tickSize, 0), new Point3D(pos, tickSize, 0), 0x80F38BA8);
        }
        AddArrowHead(new Point3D(range, 0, 0), new Vector3D(1, 0, 0), AxisXColor);

        // Y axis
        AddThickLine3D(new Point3D(0, -range, 0), new Point3D(0, range, 0), AxisYColor, 0.04);
        for (int i = 0; i <= ticksPerAxis; i++)
        {
            double pos = -range + (2 * range * i / ticksPerAxis);
            AddLine3D(new Point3D(-tickSize, pos, 0), new Point3D(tickSize, pos, 0), 0x80A6E3A1);
        }
        AddArrowHead(new Point3D(0, range, 0), new Vector3D(0, 1, 0), AxisYColor);

        // Z axis
        AddThickLine3D(new Point3D(0, 0, -range), new Point3D(0, 0, range), AxisZColor, 0.04);
        for (int i = 0; i <= ticksPerAxis; i++)
        {
            double pos = -range + (2 * range * i / ticksPerAxis);
            AddLine3D(new Point3D(-tickSize, 0, pos), new Point3D(tickSize, 0, pos), 0x8089B4FA);
        }
        AddArrowHead(new Point3D(0, 0, range), new Vector3D(0, 0, 1), AxisZColor);
    }

    private void AddArrowHead(Point3D tip, Vector3D dir, Color color)
    {
        dir.Normalize();
        double size = 0.3;
        Vector3D perp;
        if (Math.Abs(dir.X) < 0.9)
            perp = Vector3D.CrossProduct(dir, new Vector3D(1, 0, 0));
        else
            perp = Vector3D.CrossProduct(dir, new Vector3D(0, 1, 0));
        perp.Normalize();
        Vector3D perp2 = Vector3D.CrossProduct(dir, perp);
        perp2.Normalize();

        Point3D base1 = tip - dir * size + perp * (size * 0.4);
        Point3D base2 = tip - dir * size - perp * (size * 0.4);
        Point3D base3 = tip - dir * size + perp2 * (size * 0.4);
        Point3D base4 = tip - dir * size - perp2 * (size * 0.4);

        uint c = (uint)((0xFF << 24) | (color.R << 16) | (color.G << 8) | color.B);
        AddLine3D(tip, base1, c);
        AddLine3D(tip, base2, c);
        AddLine3D(tip, base3, c);
        AddLine3D(tip, base4, c);
    }

    private const int MaxPoints = 1500;  // tightened from 5000 — sphere mesh is heavy
    private const int TrimBatch = 500;

    public void AddPoint(double x, double y, double z, uint color = 0xFFA6E3A1)
    {
        byte r = (byte)(color >> 16), g = (byte)(color >> 8), b = (byte)color;
        var mesh = CreateSphere(new Point3D(x, y, z), 0.08,
            Color.FromRgb(r, g, b));
        _dynamicPoints.Children.Add(mesh);
        _pointCount++;

        if (_pointCount > MaxPoints)
        {
            for (int i = 0; i < TrimBatch && _dynamicPoints.Children.Count > 0; i++)
                _dynamicPoints.Children.RemoveAt(0);
            _pointCount -= TrimBatch;
        }
    }

    public void Clear()
    {
        _dynamicPoints.Children.Clear();
        _pointCount = 0;
    }

    private void AddLine3D(Point3D p1, Point3D p2, uint color)
    {
        byte r = (byte)(color >> 16), g = (byte)(color >> 8), b = (byte)color;
        byte a = (byte)(color >> 24);
        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection { p1, p2 },
            TriangleIndices = new Int32Collection { 0, 1, 0 }
        };
        _staticGeometry.Children.Add(new GeometryModel3D(mesh,
            new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(a, r, g, b)))));
    }

    private void AddThickLine3D(Point3D start, Point3D end, Color color, double thickness)
    {
        Vector3D dir = end - start;
        double length = dir.Length;
        dir.Normalize();

        Vector3D perp;
        if (Math.Abs(dir.X) < 0.9)
            perp = Vector3D.CrossProduct(dir, new Vector3D(1, 0, 0));
        else
            perp = Vector3D.CrossProduct(dir, new Vector3D(0, 1, 0));
        perp.Normalize();
        Vector3D perp2 = Vector3D.CrossProduct(dir, perp);
        perp2.Normalize();

        double r = thickness / 2;
        var p = new Point3DCollection();
        for (int i = 0; i < 4; i++)
        {
            double angle = Math.PI / 4 + i * Math.PI / 2;
            Vector3D offset = perp * (r * Math.Cos(angle)) + perp2 * (r * Math.Sin(angle));
            p.Add(start + offset);
            p.Add(end + offset);
        }

        var indices = new Int32Collection();
        for (int i = 0; i < 4; i++)
        {
            int i0 = i * 2, i1 = i * 2 + 1;
            int j0 = ((i + 1) % 4) * 2, j1 = ((i + 1) % 4) * 2 + 1;
            indices.Add(i0); indices.Add(i1); indices.Add(j0);
            indices.Add(i1); indices.Add(j1); indices.Add(j0);
            indices.Add(j1); indices.Add(i1); indices.Add(j0);
        }

        var mesh = new MeshGeometry3D { Positions = p, TriangleIndices = indices };
        _staticGeometry.Children.Add(new GeometryModel3D(mesh,
            new DiffuseMaterial(new SolidColorBrush(color))));
    }

    private static GeometryModel3D CreateSphere(Point3D center, double radius, Color color)
    {
        int stacks = 6, slices = 6;
        var mesh = new MeshGeometry3D();

        for (int i = 0; i <= stacks; i++)
        {
            double phi = Math.PI * i / stacks;
            for (int j = 0; j <= slices; j++)
            {
                double theta = 2 * Math.PI * j / slices;
                double x = center.X + radius * Math.Sin(phi) * Math.Cos(theta);
                double y = center.Y + radius * Math.Sin(phi) * Math.Sin(theta);
                double z = center.Z + radius * Math.Cos(phi);
                mesh.Positions.Add(new Point3D(x, y, z));
            }
        }

        for (int i = 0; i < stacks; i++)
        {
            for (int j = 0; j < slices; j++)
            {
                int a = i * (slices + 1) + j;
                int b = a + slices + 1;
                mesh.TriangleIndices.Add(a); mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(a + 1);
                mesh.TriangleIndices.Add(a + 1); mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(b + 1);
            }
        }

        return new GeometryModel3D(mesh, new DiffuseMaterial(new SolidColorBrush(color)));
    }
}
