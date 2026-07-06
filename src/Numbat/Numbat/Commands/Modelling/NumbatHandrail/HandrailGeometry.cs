using System.Collections.Generic;
using Rhino.Geometry;

namespace Numbat.Commands.Modelling.NumbatHandrail
{
    internal class HandrailGeometry
    {
        public List<Brep> TopRails { get; } = new List<Brep>();
        public List<Brep> BottomRails { get; } = new List<Brep>();
        public List<Brep> Infill { get; } = new List<Brep>();
        public List<Brep> EndPosts { get; } = new List<Brep>();
        public List<Brep> IntermediatePosts { get; } = new List<Brep>();
        public List<Brep> SupportFeet { get; } = new List<Brep>();
        public List<Brep> WallTabs { get; } = new List<Brep>();

        public List<Brep> AllBreps()
        {
            var all = new List<Brep>();

            all.AddRange(TopRails);
            all.AddRange(BottomRails);
            all.AddRange(Infill);
            all.AddRange(EndPosts);
            all.AddRange(IntermediatePosts);
            all.AddRange(SupportFeet);
            all.AddRange(WallTabs);

            return all;
        }
    }
}
