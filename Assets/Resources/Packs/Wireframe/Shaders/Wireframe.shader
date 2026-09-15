Shader "Wireframe/Double-Sided" 
{
	Properties 
	{
		_Color ("Line Color", Color) = (1,1,1,1)
		_MainTex ("Main Texture", 2D) = "white" {}
		_Thickness ("Thickness", Float) = 1
	}

	SubShader 
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent" }
		
		// First pass that renders the back faces of the model (cull front faces)
		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha 
			ZWrite Off
			Cull Front
			LOD 200
			
			CGPROGRAM
				#pragma target 5.0
				#include "UnityCG.cginc"
				#include "Wireframe Functions.cginc"
				#pragma vertex vert
				#pragma fragment frag
				#pragma geometry geom

				// Vertex Shader
				WireframeV2G vert(appdata_base v)
				{
					return WireframeVertex(v);
				}
				
				// Geometry Shader
				[maxvertexcount(3)]
				void geom(triangle WireframeV2G p[3], inout TriangleStream<WireframeG2F> triStream)
				{
					WireframeGeometry(p, triStream);
				}
				
				// Fragment Shader
				float4 frag(WireframeG2F input) : COLOR
				{	
					return WireframeFragment(input);
				}
			
			ENDCG
		}
		// Second pass to render the fronts of polygons.
		// Guarantees render order of back then front to avoid render artifacts
		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha 
			ZWrite Off
			Cull Back
			LOD 200
			
			CGPROGRAM
				#pragma target 5.0
				#include "UnityCG.cginc"
				#include "Wireframe Functions.cginc"
				#pragma vertex vert
				#pragma fragment frag
				#pragma geometry geom

				// Vertex Shader
				WireframeV2G vert(appdata_base v)
				{
					return WireframeVertex(v);
				}
				
				// Geometry Shader
				[maxvertexcount(3)]
				void geom(triangle WireframeV2G p[3], inout TriangleStream<WireframeG2F> triStream)
				{
					WireframeGeometry(p, triStream);
				}
				
				// Fragment Shader
				float4 frag(WireframeG2F input) : COLOR
				{	
					return WireframeFragment(input);
				}
			
			ENDCG
		}
	} 
}
