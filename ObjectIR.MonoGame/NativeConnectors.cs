using MonoGame;
using MonoGame.Framework;
using MonoGame.OpenGL;
using ObjectIR.Core;
using OCRuntime;
namespace ObjectIR.MonoGame
{
    public class NativeConnectors 
    {
        private IRRuntime IRRuntime;
        public NativeConnectors(IRRuntime runtime)
        {
            IRRuntime = runtime;
            Register(runtime);
        }
        public static void Register(IRRuntime runtime)
        {
            // Register MonoGame specific connectors here
        }
    }
}
