using System;
using System.Collections.Generic;
using System.Text;

namespace EastFive.Graphics
{
    public struct PigmentColor
    {
        private double r;

        private double g;

        private double b;

        public double Magnitude => Math.Sqrt(
            (this.R * this.R) +
            (this.Y * this.Y) +
            (this.B * this.B));

        public double White => Math.Min(r, Math.Min(g, b));

        public double R { get; private set; }
        public double Y { get; private set; }
        public double B { get; private set; }

        public double R_ { get; private set; }
        public double Y_ { get; private set; }
        public double B_ { get; private set; }

        public double RgbR
        {
            get => this.r;
            set
            {
                this.r = value;
                Update();
            }
        }

        public double RgbG
        {
            get => this.g;
            set
            {
                this.g = value;
                Update();
            }
        }

        public double RgbB
        {
            get => this.b;
            set
            {
                this.b = value;
                Update();
            }
        }

        public PigmentColor(double r, double g, double b)
        {
            this.r = r;
            this.g = g;
            this.b = b;

            this.R = 0;
            this.Y = 0;
            this.B = 0;
            this.R_ = 0;
            this.Y_ = 0;
            this.B_ = 0;

            Update();
        }

        private void Update()
        {
            // base is needed so it can be added and removed
            var baseWhite = White;

            // Compute the amount of contributed coloration
            var rColoration = this.r - baseWhite;
            var gColoration = this.g - baseWhite;
            var bColoration = this.b - baseWhite;

            // Yellow is the amount of red and green light that gets mixed
            var yStarting = Math.Min(rColoration, gColoration);

            (this.R_, this.Y_, this.B_) = ComputeDbs();
            (this.R, this.Y, this.B) = ComputeJosh();

            (double, double, double) ComputeDbs()
            {
                var rPigment = rColoration - yStarting;
                var gRemaining = gColoration - yStarting;

                // For some reason this helps the overall appearance
                if (bColoration > 0 && gRemaining > 0)
                {
                    bColoration /= 2.0;
                    gRemaining /= 2.0;
                }

                // Remaining green goes to yellow and blue
                var yPigment = yStarting + gRemaining;
                var bPigment = bColoration + gRemaining;

                return Normalize(rPigment, yPigment, bPigment);
            }


            // CONSIDER RUNNING COMPUTE JOSH W/O White removal

            (double, double, double) ComputeJosh()
            {
                var bSquared = bColoration * bColoration;
                var gSquared = gColoration * gColoration;
                if (rColoration < gColoration)
                {
                    var rPigment = 0;
                    var yPigment = rColoration;
                    var bPigment = Math.Sqrt(gSquared + bSquared);

                    return Normalize(rPigment, yPigment, bPigment);
                }
                {
                    var iRgb = (rColoration * rColoration) + gSquared + bSquared;
                    var rPigment = rColoration - gColoration;
                    var yPigment = gColoration;
                    var rg2 = 2 * rColoration * gColoration;
                    var bPigment = Math.Sqrt(bSquared - gSquared + rg2);

                    var iRyb = (rPigment * rPigment) + (yPigment * yPigment) + (bPigment * bPigment);

                    return Normalize(rPigment, yPigment, bPigment);
                }
            }

            (double, double, double) Normalize(double rPigment, double yPigment, double bPigment)
            {
                // Normalize to values.
                var maxRgb = Math.Max(rColoration, Math.Max(gColoration, bColoration));
                var maxRyb = Math.Max(rPigment, Math.Max(yPigment, bPigment));

                var normalizer = maxRyb <= 0 ?
                    0.0
                    :
                    (maxRgb / maxRyb);

                var rNorm = rPigment * normalizer;
                var yNorm = yPigment * normalizer;
                var bNorm = bPigment * normalizer;

                var red = baseWhite + rNorm;
                var yellow = baseWhite + yNorm;
                var blue = baseWhite + bNorm;
                return (red, yellow, blue);
            }
        }

        // A number between 0 and 6 (0-12 color wheel) representing the distance from perfect complement the colors are.
        public double ComplementaryComponent(PigmentColor color)
        {
            return 0;
        }
    }
}
