namespace Numbat.Commands.Modelling.NumbatHandrail
{
    internal class HandrailSettings
    {
        public double Height { get; set; }
        public int TopRailStyleIndex { get; set; }
        public double BoxRailDepth { get; set; }
        public double BoxRailHeight { get; set; }
        public double TopRailDiameter { get; set; }

        public int BottomRailModeIndex { get; set; }
        public double BottomRailHeight { get; set; }
        public bool SupportFeet { get; set; }

        public int PostPlacementIndex { get; set; }
        public bool IntermediatePosts { get; set; }
        public double IntermediatePostSpacing { get; set; }
        public double PostSpacing { get; set; }
        public int PostDistributionIndex { get; set; }

        public bool WallTabs { get; set; }
        public double TabLength { get; set; }

        public int InfillStyleIndex { get; set; }
        public double InfillWidth { get; set; }
        public double InfillDepth { get; set; }
        public double MaxInfillSpacing { get; set; }

        public double ZigZagDiameter { get; set; }
        public double ZigZagBayLength { get; set; }

        public double GroundZ { get; set; }
    }
}
