using Numbat.Commands.Analysis.NumbatAf;
using Numbat.Commands.Analysis.NumbatGfa;
using Rhino.PlugIns;

namespace Numbat
{
    ///<summary>
    /// <para>Every RhinoCommon .rhp assembly must have one and only one PlugIn-derived
    /// class. DO NOT create instances of this class yourself. It is the
    /// responsibility of Rhino to create an instance of this class.</para>
    /// <para>To complete plug-in information, please also see all PlugInDescription
    /// attributes in AssemblyInfo.cs (you might need to click "Project" ->
    /// "Show All Files" to see it in the "Solution Explorer" window).</para>
    ///</summary>
    public class NumbatPlugin : Rhino.PlugIns.PlugIn
    {
        public NumbatPlugin()
        {
            Instance = this;
        }

        ///<summary>Gets the only instance of the NumbatPlugin plug-in.</summary>
        public static NumbatPlugin Instance { get; private set; }

        /// <summary>Live GFA state for the active document, shared by the nbGFA window and commands.</summary>
        internal GfaModel GfaModel { get; private set; }

        /// <summary>Live Abstandsflächen state for the active document, shared by the nbAF window and commands.</summary>
        internal AfModel AfModel { get; private set; }

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            GfaModel = new GfaModel();
            GfaModel.Attach();
            AfModel = new AfModel();
            AfModel.Attach();
            return LoadReturnCode.Success;
        }

        protected override void OnShutdown()
        {
            GfaModel?.Detach();
            AfModel?.Detach();
            base.OnShutdown();
        }
    }
}