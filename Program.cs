using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CoordinatesLab
{
    // 1. ПРОЕКТУВАННЯ ТА РЕАЛІЗАЦІЯ МОДЕЛЕЙ ДАНИХ

    public readonly struct CartesianPoint2D
    {
        public double X { get; }
        public double Y { get; }

        public CartesianPoint2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static CartesianPoint2D FromPolar(PolarPoint p)
        {
            return new CartesianPoint2D(p.Radius * Math.Cos(p.Angle), p.Radius * Math.Sin(p.Angle));
        }
    }

    public readonly struct PolarPoint
    {
        public double Radius { get; }
        public double Angle { get; } // в радіанах

        public PolarPoint(double radius, double angle)
        {
            Radius = radius;
            Angle = angle;
        }

        public static PolarPoint FromCartesian(CartesianPoint2D p)
        {
            // Використовуємо Math.Atan2 для правильного визначення чверті
            return new PolarPoint(Math.Sqrt(p.X * p.X + p.Y * p.Y), Math.Atan2(p.Y, p.X));
        }
    }

    public readonly struct CartesianPoint3D
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public CartesianPoint3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static CartesianPoint3D FromSpherical(SphericalPoint p)
        {
            double x = p.Radius * Math.Sin(p.PolarAngle) * Math.Cos(p.Azimuth);
            double y = p.Radius * Math.Sin(p.PolarAngle) * Math.Sin(p.Azimuth);
            double z = p.Radius * Math.Cos(p.PolarAngle);
            return new CartesianPoint3D(x, y, z);
        }
    }

    public readonly struct SphericalPoint
    {
        public double Radius { get; }
        public double Azimuth { get; }     // в радіанах (кут в площині XY)
        public double PolarAngle { get; }  // в радіанах (кут від осі Z)

        public SphericalPoint(double radius, double azimuth, double polarAngle)
        {
            Radius = radius;
            Azimuth = azimuth;
            PolarAngle = polarAngle;
        }

        public static SphericalPoint FromCartesian(CartesianPoint3D p)
        {
            double r = Math.Sqrt(p.X * p.X + p.Y * p.Y + p.Z * p.Z);
            double azimuth = Math.Atan2(p.Y, p.X);
            double polarAngle = (r != 0.0) ? Math.Acos(p.Z / r) : 0.0;
            return new SphericalPoint(r, azimuth, polarAngle);
        }
    }

    class Program
    {
        // 2. РЕАЛІЗАЦІЯ ОБЧИСЛЕННЯ ВІДСТАНЕЙ
  
        static double DistCartesian2D(CartesianPoint2D p1, CartesianPoint2D p2)
        {
            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
        }

        static double DistPolar(PolarPoint p1, PolarPoint p2)
        {
            double val = p1.Radius * p1.Radius + p2.Radius * p2.Radius -
                         2 * p1.Radius * p2.Radius * Math.Cos(p1.Angle - p2.Angle);
            return Math.Sqrt(Math.Abs(val)); // Abs страхує від похибок типу -1e-16
        }

        static double DistCartesian3D(CartesianPoint3D p1, CartesianPoint3D p2)
        {
            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2) + Math.Pow(p2.Z - p1.Z, 2));
        }

        static double DistSphericalChord(SphericalPoint p1, SphericalPoint p2)
        {
            double term = Math.Sin(p1.PolarAngle) * Math.Sin(p2.PolarAngle) * Math.Cos(p1.Azimuth - p2.Azimuth) +
                          Math.Cos(p1.PolarAngle) * Math.Cos(p2.PolarAngle);
            double val = p1.Radius * p1.Radius + p2.Radius * p2.Radius - 2 * p1.Radius * p2.Radius * term;
            return Math.Sqrt(Math.Abs(val));
        }

        static double DistSphericalArc(SphericalPoint p1, SphericalPoint p2)
        {
            double term = Math.Sin(p1.PolarAngle) * Math.Sin(p2.PolarAngle) * Math.Cos(p1.Azimuth - p2.Azimuth) +
                          Math.Cos(p1.PolarAngle) * Math.Cos(p2.PolarAngle);
            // Захист від виходу за межі [-1, 1] через похибки float
            term = Math.Clamp(term, -1.0, 1.0);
            return p1.Radius * Math.Acos(term);
        }

        // 3. ПЕРЕВІРКА ПЕРЕТВОРЕНЬ ТА БЕНЧМАРКІНГ

        static bool IsClose(double a, double b, double epsilon = 1e-9) => Math.Abs(a - b) < epsilon;

        static void TestConversions()
        {
            Console.WriteLine("--- Перевірка перетворень ---");
            // 2D
            var c2d = new CartesianPoint2D(3.0, 4.0);
            var pFromC = PolarPoint.FromCartesian(c2d);
            var cFromP = CartesianPoint2D.FromPolar(pFromC);
            Console.WriteLine($"2D Зворотне перетворення успішне: {IsClose(c2d.X, cFromP.X) && IsClose(c2d.Y, cFromP.Y)}");

            // 3D
            var c3d = new CartesianPoint3D(1.0, 2.0, 3.0);
            var sFromC = SphericalPoint.FromCartesian(c3d);
            var cFromS = CartesianPoint3D.FromSpherical(sFromC);
            Console.WriteLine($"3D Зворотне перетворення успішне: {IsClose(c3d.X, cFromS.X) && IsClose(c3d.Y, cFromS.Y) && IsClose(c3d.Z, cFromS.Z)}\n");
        }

        static double MeasureMedian<T>(Func<T, T, double> func, List<(T, T)> data, int runs = 5)
        {
            var times = new List<double>(runs);
            var sw = new Stopwatch();

            for (int i = 0; i < runs; i++)
            {
                sw.Restart();
                double sum = 0; // Щоб компілятор не зігнорував цикл
                foreach (var pair in data)
                {
                    sum += func(pair.Item1, pair.Item2);
                }
                sw.Stop();
                times.Add(sw.Elapsed.TotalSeconds);
            }

            times.Sort();
            return times[runs / 2];
        }

        static void Benchmark2D()
        {
            const int numPairs = 100000;
            Console.WriteLine($"--- Бенчмарк 2D ({numPairs} пар) ---");

            var rnd = new Random(42);
            var polarPairs = new List<(PolarPoint, PolarPoint)>(numPairs);
            var cartesianPairs = new List<(CartesianPoint2D, CartesianPoint2D)>(numPairs);

            for (int i = 0; i < numPairs; i++)
            {
                var p1 = new PolarPoint(rnd.NextDouble() * 100, (rnd.NextDouble() * 2 * Math.PI) - Math.PI);
                var p2 = new PolarPoint(rnd.NextDouble() * 100, (rnd.NextDouble() * 2 * Math.PI) - Math.PI);
                
                polarPairs.Add((p1, p2));
                cartesianPairs.Add((CartesianPoint2D.FromPolar(p1), CartesianPoint2D.FromPolar(p2)));
            }

            double tPolar = MeasureMedian(DistPolar, polarPairs);
            double tCartesian = MeasureMedian(DistCartesian2D, cartesianPairs);

            Console.WriteLine($"Полярна система: {tPolar:F6} сек (медіана)");
            Console.WriteLine($"Декартова система: {tCartesian:F6} сек (медіана)\n");
        }

        static void Benchmark3D()
        {
            const int numPairs = 100000;
            Console.WriteLine($"--- Бенчмарк 3D ({numPairs} пар) ---");

            var rnd = new Random(42);
            var sphericalPairs = new List<(SphericalPoint, SphericalPoint)>(numPairs);
            var cartesianPairs = new List<(CartesianPoint3D, CartesianPoint3D)>(numPairs);

            for (int i = 0; i < numPairs; i++)
            {
                double r = 10 + rnd.NextDouble() * 90; // однаковий радіус для дуги
                var p1 = new SphericalPoint(r, (rnd.NextDouble() * 2 * Math.PI) - Math.PI, rnd.NextDouble() * Math.PI);
                var p2 = new SphericalPoint(r, (rnd.NextDouble() * 2 * Math.PI) - Math.PI, rnd.NextDouble() * Math.PI);
                
                sphericalPairs.Add((p1, p2));
                cartesianPairs.Add((CartesianPoint3D.FromSpherical(p1), CartesianPoint3D.FromSpherical(p2)));
            }

            double tChord = MeasureMedian(DistSphericalChord, sphericalPairs);
            double tArc = MeasureMedian(DistSphericalArc, sphericalPairs);
            double tCartesian = MeasureMedian(DistCartesian3D, cartesianPairs);

            Console.WriteLine($"Сферична система (хорда): {tChord:F6} сек (медіана)");
            Console.WriteLine($"Сферична система (дуга): {tArc:F6} сек (медіана)");
            Console.WriteLine($"Декартова система: {tCartesian:F6} сек (медіана)");
        }

        static void Main(string[] args)
        {
            TestConversions();
            Benchmark2D();
            Benchmark3D();
        }
    }
}