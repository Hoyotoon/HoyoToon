using System;
using UnityEngine;

namespace HoyoToon.Runtime.Character.HSR
{
    internal static class HSRCharacterEditorHooks
    {
        internal static Func<ComputeShader> ResolveDefaultComputeShaderHandler;

        internal static ComputeShader ResolveDefaultComputeShader()
        {
            return ResolveDefaultComputeShaderHandler?.Invoke();
        }
    }
}
