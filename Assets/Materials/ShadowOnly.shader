Shader "Custom/ShadowOnly"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "SHADOW"
            Blend Zero One // �ƹ��͵� ������ �� ��
        }
    }
    Fallback "Transparent/Cutout/Diffuse"
}
