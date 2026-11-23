using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatPV
{
    internal enum PVFitMode
    {
        Strict = 0,
        Loose = 1
    }

    internal class PVParameters
    {
        public double PanelWidth { get; set; } = 1130.0;
        public double PanelLength { get; set; } = 1760.0;
        public double PanelThickness { get; set; } = 40.0;
        public double FrameWidth { get; set; } = 20.0;
        public double FrameDepth { get; set; } = 40.0;
        public double StandHeight { get; set; } = 150.0;
        public double TiltAngleDegrees { get; set; } = 10.0;
        public double PanelGap { get; set; } = 20.0;
        public double RowGap { get; set; } = 600.0;
        public double ParapetMargin { get; set; } = 1000.0;
        public bool Rotate90 { get; set; }
        public PVFitMode FitMode { get; set; } = PVFitMode.Strict;

        public void Clamp()
        {
            PanelWidth = ClampMinimum(PanelWidth, 10.0);
            PanelLength = ClampMinimum(PanelLength, 10.0);
            PanelThickness = ClampMinimum(PanelThickness, 10.0);
            FrameWidth = ClampMinimum(FrameWidth, 10.0);
            FrameDepth = ClampMinimum(FrameDepth, 10.0);
            StandHeight = ClampMinimum(StandHeight, 10.0);
            PanelGap = ClampMinimum(PanelGap, 10.0);
            RowGap = ClampMinimum(RowGap, 10.0);
            ParapetMargin = ClampMinimum(ParapetMargin, 0.0);
        }

        private static double ClampMinimum(double value, double minimum)
        {
            return value < minimum ? minimum : value;
        }
    }
}
