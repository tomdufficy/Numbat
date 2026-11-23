using Rhino;

namespace Numbat.Commands.Modelling.NumbatTextureShuffle
{
    /// <summary>
    /// The window's inputs. Lengths are stored in millimetres and converted to model units when used, so the same
    /// settings work in mm and metre models. Remembered between sessions in Numbat's plugin settings.
    /// </summary>
    internal sealed class TextureShuffleSettings
    {
        private const string Prefix = "textureShuffle.";

        /// <summary>Size of one texture repeat along the box's X, Y and Z, in mm.</summary>
        public double SizeX { get; set; } = 1000;
        public double SizeY { get; set; } = 1000;
        public double SizeZ { get; set; } = 1000;

        /// <summary>Largest random shift of the texture in each enabled direction, in mm.</summary>
        public double MaxOffset { get; set; } = 1000;

        public bool OffsetX { get; set; } = true;
        public bool OffsetY { get; set; } = true;
        public bool OffsetZ { get; set; } = true;

        /// <summary>True: box aligned to each object's largest flat face. False: aligned to the world axes.</summary>
        public bool AlignToObject { get; set; }

        /// <summary>True: one random shift and turn for all selected objects. False: each object gets its own.</summary>
        public bool SameForAll { get; set; }

        /// <summary>Millimetres to model units for this document.</summary>
        public static double MmToModel(RhinoDoc doc) => RhinoMath.UnitScale(UnitSystem.Millimeters, doc.ModelUnitSystem);

        public static TextureShuffleSettings Load()
        {
            var s = new TextureShuffleSettings();
            var p = NumbatPlugin.Instance.Settings;
            s.SizeX = p.GetDouble(Prefix + "sizeX", s.SizeX);
            s.SizeY = p.GetDouble(Prefix + "sizeY", s.SizeY);
            s.SizeZ = p.GetDouble(Prefix + "sizeZ", s.SizeZ);
            s.MaxOffset = p.GetDouble(Prefix + "maxOffset", s.MaxOffset);
            s.OffsetX = p.GetBool(Prefix + "offsetX", s.OffsetX);
            s.OffsetY = p.GetBool(Prefix + "offsetY", s.OffsetY);
            s.OffsetZ = p.GetBool(Prefix + "offsetZ", s.OffsetZ);
            s.AlignToObject = p.GetBool(Prefix + "alignToObject", s.AlignToObject);
            s.SameForAll = p.GetBool(Prefix + "sameForAll", s.SameForAll);
            return s;
        }

        public void Save()
        {
            var p = NumbatPlugin.Instance.Settings;
            p.SetDouble(Prefix + "sizeX", SizeX);
            p.SetDouble(Prefix + "sizeY", SizeY);
            p.SetDouble(Prefix + "sizeZ", SizeZ);
            p.SetDouble(Prefix + "maxOffset", MaxOffset);
            p.SetBool(Prefix + "offsetX", OffsetX);
            p.SetBool(Prefix + "offsetY", OffsetY);
            p.SetBool(Prefix + "offsetZ", OffsetZ);
            p.SetBool(Prefix + "alignToObject", AlignToObject);
            p.SetBool(Prefix + "sameForAll", SameForAll);
        }
    }
}
