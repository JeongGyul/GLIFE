Shader "Custom/ShadowOnly"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "SHADOW"
            Blend Zero One // 아무것도 렌더링 안 함
        }
    }
    Fallback "Transparent/Cutout/Diffuse"
}
