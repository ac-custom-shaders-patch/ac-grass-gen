using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
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

    internal class Program {
        public class Fin {
            public double X, Y, Width, Life;
            public double Vx, Vy, Gravity, StartWidth, LifeTotal;
            public double CurveBack;
            public double Intensity, HeightAO;

            public Fin(double yOffset) {
                X = MathUtils.Random().Lerp(0.3, 0.7);
                Y = yOffset.Lerp(0.96, 0.99);
                Vx = MathUtils.Random().Lerp(-0.6, 0.6) * (1 - (X - 0.5).Abs());
                Vy = MathUtils.Random().Lerp(-0.8, -1.8) * 1.2;
                Gravity = MathUtils.Random().Lerp(1.0, 2.2);
                Width = MathUtils.Random().Lerp(0.03, 0.05);
                CurveBack = MathUtils.Random() > 0.6 ? (MathUtils.Random() > 0.5 ? 1 : -1) * Math.Pow(MathUtils.Random(), 2).Lerp(0.0, 1.7) : 0;
                StartWidth = Width;
                if (MathUtils.Random() > 0.3 * yOffset) {
                    Life = MathUtils.Random().Lerp(0.8, 1.0);
                } else {
                    Life = MathUtils.Random().Lerp(0.2, 0.4);
                    Vy *= 0.5;
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

                    var b = ((Intensity * 2.0 - 1.0).Saturate() * 0.67 + (Intensity * 2.0).Saturate() * (1 - Y * HeightAO + dx.Abs() / dy.Abs()).Saturate()).Saturate();
                    var s = Math.Pow(1 - Life / LifeTotal, 2.1);
                    var c = (byte)(255 * (b * s.Lerp(0.65, 1)).Saturate().Lerp(0.67, 1.0));
                    var e = new SolidBrush(Color.FromArgb(0, c, 0));
                    g.FillPolygon(e, p);
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

        private static Image GenPiece(int size) {
            var img = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var grp = Graphics.FromImage(img)) {
                for (int i = 0, t = 15; i < t; i++) {
                    new Fin((double)i / (t - 1)).Run(grp, size);
                }
            }
            return img;
        }

        private static void GenPiece(int size, string dest) {
            ResizeImage(GenPiece(size * 2), size, size).Save(dest);
        }

        private static Image GenMap(int size, int count) {
            var b = new Bitmap(size * count, size, PixelFormat.Format32bppArgb);
            var g = Graphics.FromImage(b);
            g.Clear(Color.Black);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            for (var i = 0; i < count; i++) {
                g.DrawImage(GenPiece(size * 2), size * i, 0, size, size);
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

        private static void GenMap(int size, int count, string dest) {
            GenMap(size, count).Save(dest);
        }

        public static void Main(string[] args) {
            var destination = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? "", "generated");
            Directory.CreateDirectory(destination);
            for (var i = 0; i < 16; i++) {
                GenPiece(512, Path.Combine(destination, $"{i}.png"));
            }
            GenMap(256, 16, Path.Combine(destination, "atlas.png"));
        }
    }
}