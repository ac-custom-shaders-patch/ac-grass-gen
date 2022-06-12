using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ac_grass_gen {
    static class MathUtils {
        [ThreadStatic]
        private static Random _random;

        public static Random RandomInstance => _random ?? (_random = new Random(Guid.NewGuid().GetHashCode()));

        public static double Abs(this double v) => v < 0d ? -v : v;
        public static double Random() => RandomInstance.NextDouble();
        public static double Lerp(this double t, double v0, double v1) => (1d - t) * v0 + t * v1;
        public static double Saturate(this double value) => value < 0d ? 0d : value > 1d ? 1d : value;
    }

    class FinOptions {
        public double FinCount1 = 15, FinCount2 = 15;
        public double X1 = 0.3, X2 = 0.7;
        public double Y1 = 0.96, Y2 = 0.99;
        public double Vx1 = -0.6, Vx2 = 0.6;
        public double Vy1 = -0.8, Vy2 = -1.8;
        public double Gravity1 = 1.0, Gravity2 = 2.2;
        public double Width1 = 0.03, Width2 = 0.05;
        public double CurveBack1 = 0.0, CurveBack2 = 1.7;
        public double Resolution1 = 512, Resolution2 = 512;
        public double Count1 = 16, Count2 = 16;
        public double SuperSampling = 2;
    }

    internal class Program {
        public class Fin {
            public double X, Y, Width, Life;
            public double Vx, Vy, Gravity, StartWidth, LifeTotal;
            public double CurveBack;
            public double Intensity, HeightAO;

            public Fin(double yOffset, FinOptions options) {
                X = MathUtils.Random().Lerp(options.X1, options.X2);
                Y = yOffset.Lerp(options.Y1, options.Y2);
                Vx = MathUtils.Random().Lerp(options.Vx1, options.Vx2) * (1 - (X - 0.5).Abs());
                Vy = MathUtils.Random().Lerp(options.Vy1, options.Vy2) * 1.2;
                Gravity = MathUtils.Random().Lerp(options.Gravity1, options.Gravity2);
                Width = MathUtils.Random().Lerp(options.Width1, options.Width2);
                CurveBack = MathUtils.Random() > 0.6 ?
                        (MathUtils.Random() > 0.5 ? 1 : -1) * Math.Pow(MathUtils.Random(), 2).Lerp(options.CurveBack1, options.CurveBack2) : 0;
                StartWidth = Width;
                if (MathUtils.Random() > 0.3 * yOffset) {
                    Life = MathUtils.Random().Lerp(0.8, 1.0);
                } else {
                    Life = MathUtils.Random().Lerp(0.2, 0.4) * 2;
                    Vy *= 0.8;
                }
                Life *= (Gravity / 2.2 + 1) / 2;
                LifeTotal = Life;
                Intensity = (yOffset + MathUtils.Random()) / 2.0;
                HeightAO = MathUtils.Random().Lerp(0.4, 0.8);
            }

            public void Move(double dt) {
                Life -= dt;
                X += Vx * dt;
                Y += Vy * dt;
                Vy += Gravity * (Life / LifeTotal) * dt;
                Vy += CurveBack * Math.Pow(1 - Life / LifeTotal, 2) * dt;
                Vy *= 0.99;
                Width = 0.005 + (StartWidth - 0.005) * Life / LifeTotal;
            }

            public void Step(ref Point a, ref Point b, int size) {
                a.X = (int)(size * (X - Width / 2.0));
                b.X = (int)(size * (X + Width / 2.0));
                a.Y = (int)(size * Y);
                b.Y = (int)(size * Y);
            }

            public void Run(Graphics g, int size) {
                var p = new Point[4];
                var rx = -1.0;
                var ry = 0.0;
                var lines = new Tuple<double, Pen>[10];
                for (var i = 0; i < lines.Length; ++i) {
                    lines[i] = Tuple.Create(MathUtils.Random(), new Pen(Color.FromArgb((int)(15 + 5 * MathUtils.Random()), 0, 0, 0)));
                    lines[i].Item2.Width = 4;
                }
                while (Life > 0) {
                    var x0 = X;
                    var y0 = Y;
                    var w0 = Width;
                    Move(0.01);
                    var x1 = X;
                    var y1 = Y;
                    var w1 = Width;

                    var dx = x1 - x0;
                    var dy = y1 - y0;
                    var dl = Math.Sqrt(dx * dx + dy * dy);
                    var nx = dx / dl;
                    var ny = dy / dl;
                    var px = ny;
                    var py = -nx;

                    p[0].X = (int)(size * (x0 + rx * w0 / 2.0));
                    p[0].Y = (int)(size * (y0 + ry * w0 / 2.0));
                    p[1].X = (int)(size * (x0 - rx * w0 / 2.0));
                    p[1].Y = (int)(size * (y0 - ry * w0 / 2.0));
                    p[3].X = (int)(size * (x1 + px * w1 / 2.0));
                    p[3].Y = (int)(size * (y1 + py * w1 / 2.0));
                    p[2].X = (int)(size * (x1 - px * w1 / 2.0));
                    p[2].Y = (int)(size * (y1 - py * w1 / 2.0));
                    rx = px;
                    ry = py;

                    var b =
                            ((Intensity * 2.0 - 1.0).Saturate() * 0.67 + (Intensity * 2.0).Saturate() * (1 - Y * HeightAO + dx.Abs() / dy.Abs()).Saturate())
                                    .Saturate();
                    var s = Math.Pow(1 - Life / LifeTotal, 2.1);
                    var c = (byte)(255 * (b * s.Lerp(0.65, 1)).Saturate().Lerp(0.67, 1.0));
                    var e = new SolidBrush(Color.FromArgb(0, c, 0));
                    g.FillPolygon(e, p);

                    var l = (int)(lines.Length * Math.Sqrt(Math.Min(Life / LifeTotal, 1)) * Math.Min((1 - Life / LifeTotal) * 3, 1));
                    for (var index = 0; index < l; index++) {
                        var t = lines[index];
                        g.DrawLine(t.Item2,
                                (int)t.Item1.Lerp(p[0].X, p[1].X),
                                (int)t.Item1.Lerp(p[0].Y, p[1].Y),
                                (int)t.Item1.Lerp(p[3].X, p[2].X),
                                (int)t.Item1.Lerp(p[3].Y, p[2].Y));
                    }
                }
            }
        }

        private static Image ResizeImage(Image imgToResize, int width, int height) {
            var b = new Bitmap(width, height);
            var g = Graphics.FromImage(b);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(imgToResize, 0, 0, width, height);
            g.Dispose();
            return b;
        }

        private static Image GenPiece(int size, FinOptions options) {
            var img = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var grp = Graphics.FromImage(img)) {
                for (int i = 0, t = (int)MathUtils.Random().Lerp(options.FinCount1, options.FinCount2); i < t; i++) {
                    new Fin((double)i / (t - 1), options).Run(grp, size);
                }
            }
            return img;
        }

        private static void GenPiece(int size, string dest, FinOptions options) {
            if (options.SuperSampling > 1) {
                ResizeImage(GenPiece((int)(size * options.SuperSampling), options), size, size).Save(dest);
            } else {
                GenPiece(size, options).Save(dest);
            }
        }

        private static Image GenMap(int size, int count, FinOptions options) {
            var b = new Bitmap(size * count, size, PixelFormat.Format32bppArgb);
            var g = Graphics.FromImage(b);
            g.Clear(Color.Black);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            for (var i = 0; i < count; i++) {
                if (options.SuperSampling > 1) {
                    g.DrawImage(ResizeImage(GenPiece((int)(size * options.SuperSampling), options), size, size), size * i, 0, size, size);
                } else {
                    g.DrawImage(GenPiece(size, options), size * i, 0, size, size);
                }
            }
            g.Dispose();

            var rect = new Rectangle(0, 0, size * count, size);
            var bmpData = b.LockBits(rect, ImageLockMode.ReadWrite, b.PixelFormat);
            var ptr = bmpData.Scan0;
            var bytes = bmpData.Stride * size;
            var rgbValues = new byte[bytes];
            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);
            for (var x = 0; x < size * count; x++) {
                for (var y = 0; y < size; y++) {
                    var position = (y * bmpData.Stride) + (x * Image.GetPixelFormatSize(bmpData.PixelFormat) / 8);
                    var alpha = rgbValues[position + 3] / 255.0;
                    rgbValues[position + 1] = (byte)alpha.Lerp(229, rgbValues[position + 1]);
                }
            }
            System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);
            b.UnlockBits(bmpData);
            return b;
        }

        private static void GenMap(int size, int count, string dest, FinOptions options) {
            GenMap(size, count, options).Save(dest);
        }

        private struct IntensityPoint {
            public double X;
            public double Y;
            public byte Intensity;
        }

        private static Image GenDrops(IEnumerable<Tuple<double, double>> drops) {
            var list = drops.ToList();
            var img = new Bitmap(2048, 2048, PixelFormat.Format32bppArgb);
            var padding = 4;
            var total = 0;
            var intensityPoints = new List<IntensityPoint>();

            foreach (var point in list) {
                // var dropSize = (int)MathUtils.Random().Lerp(20, 28);
                var dropSize = (int)Math.Pow(MathUtils.Random(), 1.8).Lerp(6, 20);
                var dropSizeHalf = dropSize / 2d;
                var dropSizeHalfSquared = dropSizeHalf * dropSizeHalf;

                var fromX = padding + (int)((2048 - padding * 2) * ((point.Item1 + (MathUtils.Random() - 0.5) * 0.03) * 0.5 + 0.5));
                var fromY = padding + (int)((2048 - padding * 2) * ((point.Item2 + (MathUtils.Random() - 0.5) * 0.03) * 0.5 + 0.5));
                var centerX = fromX + dropSizeHalf;
                var centerY = fromY + dropSizeHalf;
                var random = (byte)(255 * MathUtils.Random());
                intensityPoints.Add(new IntensityPoint { X = centerX, Y = centerY, Intensity = random });
                Console.WriteLine($"{Math.Round(1000d * total / list.Count) / 10d}% ({fromX}, {fromY})");
                for (var y = 0; y < dropSize; ++y) {
                    for (var x = 0; x < dropSize; ++x) {
                        var pointX = fromX + x;
                        var pointY = fromY + y;
                        var deltaX = pointX - centerX;
                        var deltaY = pointY - centerY;
                        var distance = deltaX * deltaX + deltaY * deltaY;
                        if (distance > dropSizeHalfSquared) continue;

                        var normalX = deltaX / dropSizeHalf;
                        var normalY = deltaY / dropSizeHalf;
                        img.SetPixel((pointX + 2048) % 2048, (pointY + 2048) % 2048,
                                Color.FromArgb(255, (byte)(normalX * 127 + 127), (byte)(normalY * 127 + 127), random));
                    }
                }
                ++total;
            }

            for (var y = 0; y < 2048; ++y) {
                for (var x = 0; x < 2048; ++x) {
                    var pixel = img.GetPixel(x, y);
                    if (pixel.A == 0 && pixel.B == 0) {
                        var minDistance = double.MaxValue;
                        var intensity = 0;
                        for (var i = intensityPoints.Count - 1; i >= 0; i--) {
                            var point = intensityPoints[i];
                            var distanceX = x - point.X;
                            var distanceY = y - point.Y;
                            var distance = Math.Sqrt(distanceX * distanceX + distanceY * distanceY);
                            if (distance < minDistance) {
                                minDistance = distance;
                                intensity = point.Intensity;
                            }
                        }

                        img.SetPixel(x, y, Color.FromArgb(0, 0, 0, intensity));
                    }
                }
            }
            Console.WriteLine($"Expansion is finished");

            return img;
        }

        public static void Main(string[] args) {
            if (File.Exists("list.txt")) {
                // const double threshold = 0.43;
                const double threshold = 0.6;
                GenDrops(File.ReadLines("list.txt").Select(x => x.Split(',')).Where(x => x.Length == 2)
                        .Select(x => Tuple.Create(FlexibleParser.TryParseDouble(x[0]) / threshold ?? 0d, FlexibleParser.TryParseDouble(x[1]) / threshold ?? 0d))
                        .Where(x => Math.Abs(x.Item1) < 1d && Math.Abs(x.Item2) < 1d)).Save("drops_rare.png");
                return;
            }

            var options = new FinOptions();
            foreach (var line in File.ReadAllLines("config.txt").Select(x => x.Split('=')).Where(x => x.Length == 2).Select(x => new {
                Key = x[0].Trim(),
                Value = x[1].Split(',').Select(FlexibleParser.TryParseDouble).Where(y => y.HasValue).Select(y => y.Value).ToList()
            })) {
                void Fill(ref double v1, ref double v2, List<double> values) {
                    if (values.Count == 0) return;
                    v1 = values[0];
                    v2 = values.Count == 2 ? values[1] : v1;
                }

                switch (line.Key) {
                    case "FinCount":
                        Fill(ref options.FinCount1, ref options.FinCount2, line.Value);
                        break;
                    case "X":
                        Fill(ref options.X1, ref options.X2, line.Value);
                        break;
                    case "Y":
                        Fill(ref options.Y1, ref options.Y2, line.Value);
                        break;
                    case "Vx":
                        Fill(ref options.Vx1, ref options.Vx2, line.Value);
                        break;
                    case "Vy":
                        Fill(ref options.Vy1, ref options.Vy2, line.Value);
                        break;
                    case "Gravity":
                        Fill(ref options.Gravity1, ref options.Gravity2, line.Value);
                        break;
                    case "Width":
                        Fill(ref options.Width1, ref options.Width2, line.Value);
                        break;
                    case "CurveBack":
                        Fill(ref options.CurveBack1, ref options.CurveBack2, line.Value);
                        break;
                    case "ResolutionSingle":
                        if (line.Value.Count > 0) options.Resolution1 = line.Value[0];
                        break;
                    case "ResolutionSet":
                        if (line.Value.Count > 0) options.Resolution2 = line.Value[0];
                        break;
                    case "CountSingle":
                        if (line.Value.Count > 0) options.Count1 = line.Value[0];
                        break;
                    case "CountSet":
                        if (line.Value.Count > 0) options.Count2 = line.Value[0];
                        break;
                    case "SuperSampling":
                        if (line.Value.Count > 0) options.SuperSampling = line.Value[0];
                        break;
                    default:
                        Console.Error.WriteLine($"Unknown key: {line.Key}");
                        break;
                }
            }

            var destination = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? "", "generated");
            Directory.CreateDirectory(destination);
            for (var i = 0; i < (int)options.Count1; i++) {
                GenPiece((int)options.Resolution1, Path.Combine(destination, $"{i}.png"), options);
            }
            if ((int)options.Count2 > 0) {
                GenMap((int)options.Resolution2, (int)options.Count2, Path.Combine(destination, "atlas.png"), options);
            }
        }
    }
}