using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Movers.EditorTools
{
    // Pins the editor and player to Direct3D11 on Windows.
    //
    // Why: Unity 6000.6.1 defaults to D3D12, and on a hybrid NVIDIA + Intel laptop the
    // editor crashes while creating the swap chain:
    //     D3D12SwapChain::CreateHWND -> GfxDeviceD3D12Base::CreateGfxWindow
    // The project ran fine on 6000.6.0 with DX11. Leaving the API on "auto" makes the
    // editor unopenable on this machine, so it is set explicitly rather than left to chance.
    public static class MoversGraphicsFixCLI
    {
        public static void ForceD3D11()
        {
            var target = BuildTarget.StandaloneWindows64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
            PlayerSettings.SetGraphicsAPIs(target, new[] { GraphicsDeviceType.Direct3D11 });

            var target32 = BuildTarget.StandaloneWindows;
            PlayerSettings.SetUseDefaultGraphicsAPIs(target32, false);
            PlayerSettings.SetGraphicsAPIs(target32, new[] { GraphicsDeviceType.Direct3D11 });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var apis = PlayerSettings.GetGraphicsAPIs(target);
            foreach (var a in apis) Debug.Log("[GraphicsFix] StandaloneWindows64 API: " + a);
            Debug.Log("[GraphicsFix] useDefault=" + PlayerSettings.GetUseDefaultGraphicsAPIs(target));
            Debug.Log("[GraphicsFix] done");
        }
    }
}
