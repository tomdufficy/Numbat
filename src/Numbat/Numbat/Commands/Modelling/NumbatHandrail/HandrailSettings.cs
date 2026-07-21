using Rhino;

namespace Numbat.Commands.Modelling.NumbatHandrail
{
    internal class HandrailSettings
    {
        private const string Prefix = "nbHandrail.";

        public double Height { get; set; }
        public int TopRailStyleIndex { get; set; }
        public double BoxRailDepth { get; set; }
        public double BoxRailHeight { get; set; }
        public double TopRailDiameter { get; set; }

        public int BottomRailModeIndex { get; set; }
        public double BottomRailHeight { get; set; }
        public bool SupportFeet { get; set; }

        public int BayLayoutIndex { get; set; }
        public double MaxBayLength { get; set; }

        public bool Tabs { get; set; }
        public double TabLength { get; set; }

        public int InfillStyleIndex { get; set; }
        public double InfillWidth { get; set; }
        public double InfillDepth { get; set; }
        public double MaxInfillSpacing { get; set; }

        public double ZigZagDiameter { get; set; }
        public double ZigZagBayLength { get; set; }

        public double PanelGap { get; set; }
        public double PanelFrameWidth { get; set; }
        public double PanelFrameDepth { get; set; }
        public double PanelSheetThickness { get; set; }
        public double PanelTopGap { get; set; }
        public double PanelBottomGap { get; set; }
        public int PanelFrameConstructionIndex { get; set; }

        public bool PreviewDims { get; set; }

        public double GroundZ { get; set; }

        public static HandrailSettings CreateDefaults()
        {
            return new HandrailSettings
            {
                Height = 1100.0,
                TopRailStyleIndex = 1,
                BoxRailDepth = 40.0,
                BoxRailHeight = 20.0,
                TopRailDiameter = 50.0,
                BottomRailModeIndex = 1,
                BottomRailHeight = 100.0,
                SupportFeet = false,
                BayLayoutIndex = 1,
                MaxBayLength = 1200.0,
                Tabs = false,
                TabLength = 75.0,
                InfillStyleIndex = 0,
                InfillWidth = 10.0,
                InfillDepth = 20.0,
                MaxInfillSpacing = 100.0,
                ZigZagDiameter = 10.0,
                ZigZagBayLength = 100.0,
                PanelGap = 50.0,
                PanelFrameWidth = 25.0,
                PanelFrameDepth = 25.0,
                PanelSheetThickness = 5.0,
                PanelTopGap = 50.0,
                PanelBottomGap = 100.0,
                PanelFrameConstructionIndex = 0,
                PreviewDims = true,
                GroundZ = 0.0
            };
        }

        public static HandrailSettings Load(PersistentSettings storage)
        {
            var defaults = CreateDefaults();

            if (storage == null)
                return defaults;

            return new HandrailSettings
            {
                Height = storage.GetDouble(Key("Height"), defaults.Height),
                TopRailStyleIndex = storage.GetInteger(Key("TopRailStyleIndex"), defaults.TopRailStyleIndex),
                BoxRailDepth = storage.GetDouble(Key("BoxRailDepth"), defaults.BoxRailDepth),
                BoxRailHeight = storage.GetDouble(Key("BoxRailHeight"), defaults.BoxRailHeight),
                TopRailDiameter = storage.GetDouble(Key("TopRailDiameter"), defaults.TopRailDiameter),
                BottomRailModeIndex = storage.GetInteger(Key("BottomRailModeIndex"), defaults.BottomRailModeIndex),
                BottomRailHeight = storage.GetDouble(Key("BottomRailHeight"), defaults.BottomRailHeight),
                SupportFeet = storage.GetBool(Key("SupportFeet"), defaults.SupportFeet),
                BayLayoutIndex = storage.GetInteger(Key("BayLayoutIndex"), defaults.BayLayoutIndex),
                MaxBayLength = storage.GetDouble(Key("MaxBayLength"), defaults.MaxBayLength),
                Tabs = storage.GetBool(Key("Tabs"), defaults.Tabs),
                TabLength = storage.GetDouble(Key("TabLength"), defaults.TabLength),
                InfillStyleIndex = storage.GetInteger(Key("InfillStyleIndex"), defaults.InfillStyleIndex),
                InfillWidth = storage.GetDouble(Key("InfillWidth"), defaults.InfillWidth),
                InfillDepth = storage.GetDouble(Key("InfillDepth"), defaults.InfillDepth),
                MaxInfillSpacing = storage.GetDouble(Key("MaxInfillSpacing"), defaults.MaxInfillSpacing),
                ZigZagDiameter = storage.GetDouble(Key("ZigZagDiameter"), defaults.ZigZagDiameter),
                ZigZagBayLength = storage.GetDouble(Key("ZigZagBayLength"), defaults.ZigZagBayLength),
                PanelGap = storage.GetDouble(Key("PanelGap"), defaults.PanelGap),
                PanelFrameWidth = storage.GetDouble(Key("PanelFrameWidth"), defaults.PanelFrameWidth),
                PanelFrameDepth = storage.GetDouble(Key("PanelFrameDepth"), defaults.PanelFrameDepth),
                PanelSheetThickness = storage.GetDouble(Key("PanelSheetThickness"), defaults.PanelSheetThickness),
                PanelTopGap = storage.GetDouble(Key("PanelTopGap"), defaults.PanelTopGap),
                PanelBottomGap = storage.GetDouble(Key("PanelBottomGap"), defaults.PanelBottomGap),
                PanelFrameConstructionIndex = storage.GetInteger(Key("PanelFrameConstructionIndex"), defaults.PanelFrameConstructionIndex),
                PreviewDims = storage.GetBool(Key("PreviewDims"), defaults.PreviewDims),
                GroundZ = 0.0
            };
        }

        public void Save(PersistentSettings storage)
        {
            if (storage == null)
                return;

            storage.SetDouble(Key("Height"), Height);
            storage.SetInteger(Key("TopRailStyleIndex"), TopRailStyleIndex);
            storage.SetDouble(Key("BoxRailDepth"), BoxRailDepth);
            storage.SetDouble(Key("BoxRailHeight"), BoxRailHeight);
            storage.SetDouble(Key("TopRailDiameter"), TopRailDiameter);
            storage.SetInteger(Key("BottomRailModeIndex"), BottomRailModeIndex);
            storage.SetDouble(Key("BottomRailHeight"), BottomRailHeight);
            storage.SetBool(Key("SupportFeet"), SupportFeet);
            storage.SetInteger(Key("BayLayoutIndex"), BayLayoutIndex);
            storage.SetDouble(Key("MaxBayLength"), MaxBayLength);
            storage.SetBool(Key("Tabs"), Tabs);
            storage.SetDouble(Key("TabLength"), TabLength);
            storage.SetInteger(Key("InfillStyleIndex"), InfillStyleIndex);
            storage.SetDouble(Key("InfillWidth"), InfillWidth);
            storage.SetDouble(Key("InfillDepth"), InfillDepth);
            storage.SetDouble(Key("MaxInfillSpacing"), MaxInfillSpacing);
            storage.SetDouble(Key("ZigZagDiameter"), ZigZagDiameter);
            storage.SetDouble(Key("ZigZagBayLength"), ZigZagBayLength);
            storage.SetDouble(Key("PanelGap"), PanelGap);
            storage.SetDouble(Key("PanelFrameWidth"), PanelFrameWidth);
            storage.SetDouble(Key("PanelFrameDepth"), PanelFrameDepth);
            storage.SetDouble(Key("PanelSheetThickness"), PanelSheetThickness);
            storage.SetDouble(Key("PanelTopGap"), PanelTopGap);
            storage.SetDouble(Key("PanelBottomGap"), PanelBottomGap);
            storage.SetInteger(Key("PanelFrameConstructionIndex"), PanelFrameConstructionIndex);
            storage.SetBool(Key("PreviewDims"), PreviewDims);
        }

        private static string Key(string name)
        {
            return Prefix + name;
        }
    }
}
