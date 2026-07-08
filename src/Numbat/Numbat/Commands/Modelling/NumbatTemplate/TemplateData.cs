using System.Collections.Generic;

namespace Numbat.Commands.Modelling.NumbatTemplate
{
    internal static class TemplateData
    {
        internal sealed class LayerSpec
        {
            public LayerSpec(string name, int parentIndex, int red, int green, int blue)
            {
                Name = name;
                ParentIndex = parentIndex;
                Red = red;
                Green = green;
                Blue = blue;
            }

            public string Name { get; }
            public int ParentIndex { get; }
            public int Red { get; }
            public int Green { get; }
            public int Blue { get; }
        }

        internal static IEnumerable<LayerSpec> EnumerateLayers()
        {
            yield return new LayerSpec("#DEFAULT", -1, 255, 0, 255);
            yield return new LayerSpec("ANIMATION", -1, 39, 38, 193);
            yield return new LayerSpec("ANIM_FURNITURES", 1, 38, 104, 193);
            yield return new LayerSpec("ANIM_FUR_ALUMINIUM", 2, 46, 46, 111);
            yield return new LayerSpec("ANIM_FUR_COLORS", 2, 121, 121, 127);
            yield return new LayerSpec("ANIM_FUR_FABRIC", 2, 22, 21, 139);
            yield return new LayerSpec("ANIM_FUR_GALVANIZED", 2, 196, 196, 230);
            yield return new LayerSpec("ANIM_FUR_GLASS", 2, 139, 138, 246);
            yield return new LayerSpec("ANIM_FUR_H", 2, 255, 255, 255);
            yield return new LayerSpec("ANIM_FUR_L_USAGEAREA", 2, 116, 116, 184);
            yield return new LayerSpec("ANIM_FUR_METAL", 2, 67, 66, 228);
            yield return new LayerSpec("ANIM_FUR_SCREENS", 2, 188, 188, 208);
            yield return new LayerSpec("ANIM_FUR_WATER", 2, 94, 93, 255);
            yield return new LayerSpec("ANIM_FUR_WOOD", 2, 110, 109, 152);
            yield return new LayerSpec("ANIM_PEOPLE", 1, 79, 79, 203);
            yield return new LayerSpec("ANIM_PEOPLE_H", 14, 140, 139, 214);
            yield return new LayerSpec("ANIM_VEGETATION", 1, 51, 51, 115);
            yield return new LayerSpec("ANIM_VEG_H", 16, 184, 184, 224);
            yield return new LayerSpec("ANIM_VEG_ROOF", 16, 78, 78, 125);
            yield return new LayerSpec("ANIM_VEHICLES", 1, 19, 19, 46);
            yield return new LayerSpec("ANIM_VEHI_H", 19, 212, 212, 212);
            yield return new LayerSpec("BUILDING", -1, 34, 235, 214);
            yield return new LayerSpec("BLD_CEILINGS", 21, 151, 111, 203);
            yield return new LayerSpec("BLD_CEI_ACOUSTIC", 22, 46, 36, 102);
            yield return new LayerSpec("BLD_CEI_LIGHTS", 22, 196, 185, 230);
            yield return new LayerSpec("BLD_CEI_SUSPENDED", 22, 123, 89, 170);
            yield return new LayerSpec("BLD_CEI_TECHNICAL", 22, 95, 72, 145);
            yield return new LayerSpec("BLD_DOORS", 21, 0, 138, 255);
            yield return new LayerSpec("BLD_DOO_GLASS", 27, 204, 233, 232);
            yield return new LayerSpec("BLD_DOO_METAL", 27, 75, 118, 194);
            yield return new LayerSpec("BLD_DOO_OTHER", 27, 107, 146, 207);
            yield return new LayerSpec("BLD_DOO_WOOD", 27, 63, 103, 152);
            yield return new LayerSpec("BLD_ELEVATORS", 21, 134, 90, 86);
            yield return new LayerSpec("BLD_FACADE", 21, 31, 116, 134);
            yield return new LayerSpec("BLD_FAC_CLADDING", 33, 153, 100, 170);
            yield return new LayerSpec("BLD_FAC_CLAD_ALU", 34, 218, 116, 221);
            yield return new LayerSpec("BLD_FAC_CLAD_BRICKS", 34, 180, 60, 73);
            yield return new LayerSpec("BLD_FAC_CLAD_BRICKS_Red", 34, 182, 91, 91);
            yield return new LayerSpec("BLD_FAC_CLAD_CORRUGATED", 34, 0, 0, 255);
            yield return new LayerSpec("BLD_FAC_CLAD_GALVA", 34, 204, 206, 207);
            yield return new LayerSpec("BLD_FAC_CLAD_METAL_EXPANDED", 34, 181, 204, 224);
            yield return new LayerSpec("BLD_FAC_CLAD_WOOD", 34, 161, 123, 80);
            yield return new LayerSpec("BLD_FAC_CLAD_WOOD_black", 34, 139, 159, 158);
            yield return new LayerSpec("BLD_FAC_GLASS", 33, 154, 241, 255);
            yield return new LayerSpec("BLD_FAC_MULLIONS", 33, 56, 122, 132);
            yield return new LayerSpec("BLD_FAC_MUL_ALU", 44, 26, 48, 79);
            yield return new LayerSpec("BLD_FAC_MUL_WOOD", 44, 45, 94, 132);
            yield return new LayerSpec("BLD_FAC_STRUCTURE", 33, 23, 80, 118);
            yield return new LayerSpec("BLD_FAC_STR_METAL", 47, 48, 81, 120);
            yield return new LayerSpec("BLD_FAC_STR_WOOD", 47, 148, 117, 94);
            yield return new LayerSpec("BLD_FLOORS", 21, 196, 114, 106);
            yield return new LayerSpec("BLD_FLOORS_CLT", 50, 0, 0, 0);
            yield return new LayerSpec("BLD_FLOORS_CONCRETE", 50, 0, 0, 0);
            yield return new LayerSpec("BLD_FLOORS_TERRACE", 50, 0, 0, 0);
            yield return new LayerSpec("BLD_FLOORS_WOODFLOOR", 50, 0, 0, 0);
            yield return new LayerSpec("BLD_FUNDATIONS", 21, 128, 70, 157);
            yield return new LayerSpec("BLD_LIGHT", 21, 240, 224, 173);
            yield return new LayerSpec("BLD_RAILINGS", 21, 232, 169, 237);
            yield return new LayerSpec("BLD_RAI_HANDRAIL", 57, 127, 76, 109);
            yield return new LayerSpec("BLD_RAI_MESH", 57, 180, 106, 169);
            yield return new LayerSpec("BLD_ROOFS", 21, 159, 143, 45);
            yield return new LayerSpec("BLD_ROOF_EDGE", 60, 159, 143, 45);
            yield return new LayerSpec("BLD_ROOF_SEDUM", 60, 183, 231, 205);
            yield return new LayerSpec("BLD_SIGNAGE", 21, 203, 80, 80);
            yield return new LayerSpec("BLD_SIGNAGE_SUPPORTS", 63, 221, 221, 221);
            yield return new LayerSpec("BLD_SOLARPANNELS", 21, 0, 77, 255);
            yield return new LayerSpec("BLD_STAIRS", 21, 118, 49, 56);
            yield return new LayerSpec("BLD_STRUCTURE", 21, 146, 95, 60);
            yield return new LayerSpec("BLD_STR_BEAMS", 67, 182, 100, 68);
            yield return new LayerSpec("BLD_STR_COLUMNS", 67, 57, 108, 131);
            yield return new LayerSpec("BLD_WALLS", 21, 231, 170, 133);
            yield return new LayerSpec("BLD_WA_GLASS", 70, 137, 206, 224);
            yield return new LayerSpec("BLD_WA_PARTITION", 70, 226, 165, 86);
            yield return new LayerSpec("BLD_WA_STRUCTURAL", 70, 201, 123, 9);
            yield return new LayerSpec("CONTEXT", -1, 0, 238, 255);
            yield return new LayerSpec("CON_BUILDINGS", 74, 226, 226, 226);
            yield return new LayerSpec("CON_GRASS", 74, 61, 170, 108);
            yield return new LayerSpec("CON_GROUND", 74, 82, 134, 171);
            yield return new LayerSpec("CON_INFINITEPLANE", 74, 208, 251, 255);
            yield return new LayerSpec("CON_ROADS", 74, 89, 89, 146);
            yield return new LayerSpec("CON_TREES (blocks only)", 74, 33, 102, 83);
            yield return new LayerSpec("CON_VEHICULES (blocks only)", 74, 85, 156, 164);
            yield return new LayerSpec("CON_WATER", 74, 16, 36, 139);
            yield return new LayerSpec("DATA", -1, 255, 0, 0);
            yield return new LayerSpec("DATA_CONTOUR", 83, 0, 255, 143);
            yield return new LayerSpec("DATA_DIMENSIONS", 83, 44, 51, 182);
            yield return new LayerSpec("DATA_DRIVINGCIRCLES", 83, 211, 125, 244);
            yield return new LayerSpec("DATA_FORMAT", 83, 255, 0, 0);
            yield return new LayerSpec("DATA_GRID", 83, 190, 190, 190);
            yield return new LayerSpec("DATA_H_CUT", 83, 0, 0, 0);
            yield return new LayerSpec("DATA_H_GREY", 83, 190, 190, 190);
            yield return new LayerSpec("DATA_H_WHITE", 83, 255, 255, 255);
            yield return new LayerSpec("DATA_HELPLINES", 83, 233, 179, 159);
            yield return new LayerSpec("DATA_L_BACKGROUND", 83, 190, 190, 190);
            yield return new LayerSpec("DATA_L_DASHED_L", 83, 223, 120, 255);
            yield return new LayerSpec("DATA_L_DASHED_M", 83, 189, 105, 216);
            yield return new LayerSpec("DATA_L_DASHED_S", 83, 153, 82, 175);
            yield return new LayerSpec("DATA_L_DASHED_XS", 83, 112, 68, 125);
            yield return new LayerSpec("DATA_L_DASHED_XXS", 83, 71, 37, 81);
            yield return new LayerSpec("DATA_PLOTLINE", 83, 54, 105, 210);
            yield return new LayerSpec("DATA_TEXT AND SYMBOLS", 83, 157, 44, 12);
            yield return new LayerSpec("LANDSCAPE", -1, 0, 127, 0);
            yield return new LayerSpec("LND_GREENROOF", 101, 117, 214, 156);
            yield return new LayerSpec("LND_GROUND", 101, 0, 0, 0);
            yield return new LayerSpec("LND_GRD_DRIVINGGUIDES", 103, 191, 191, 255);
            yield return new LayerSpec("LND_GRD_GRASS", 103, 70, 146, 108);
            yield return new LayerSpec("LND_GRD_PAVEDGRASS", 103, 203, 228, 215);
            yield return new LayerSpec("LND_GRD_PAVEDSAND", 103, 249, 228, 208);
            yield return new LayerSpec("LND_GRD_RUBBER", 103, 244, 222, 208);
            yield return new LayerSpec("LND_PARKING", 101, 23, 94, 166);
            yield return new LayerSpec("LND_PARK_EDGE", 109, 212, 203, 203);
            yield return new LayerSpec("LND_PATH", 101, 70, 100, 91);
            yield return new LayerSpec("LND_PATH_PEBBLES", 111, 173, 172, 167);
            yield return new LayerSpec("LND_PATH_TAMPEDGRAVELS", 111, 235, 209, 163);
            yield return new LayerSpec("LND_PATH_WOODCHIP", 111, 100, 73, 53);
            yield return new LayerSpec("LND_TERRAIN", 101, 79, 92, 85);
            yield return new LayerSpec("LND_WATER", 101, 0, 0, 255);
        }
    }
}
