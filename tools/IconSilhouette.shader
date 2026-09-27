// Editor-only coverage pass. Native opaque Custom/Piece writes non-coverage
// values into alpha, so its color alpha must never become the PNG silhouette.
Shader "Hidden/RunicStorage/IconSilhouette" {
 SubShader {
  Tags { "RenderType"="Opaque" }
  Pass {
   Cull Off ZWrite On ZTest LEqual
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   float4 vert(float4 vertex:POSITION):SV_POSITION { return UnityObjectToClipPos(vertex); }
   fixed4 frag():SV_Target { return fixed4(1,1,1,1); }
   ENDCG
  }
 }
 FallBack Off
}
