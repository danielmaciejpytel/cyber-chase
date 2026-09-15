#ifndef CYBERCHASE_WIREFRAME_FUNCTIONS_INCLUDED
#define CYBERCHASE_WIREFRAME_FUNCTIONS_INCLUDED

#include "UnityCG.cginc"

// DATA STRUCTURES //
// Vertex to Geometry
struct WireframeV2G
{
	float4	pos		: POSITION;		// vertex position
	float2  uv		: TEXCOORD0;	// vertex uv coordinate
};

// Geometry to fragment
struct WireframeG2F
{
	float4	pos		: POSITION;		// fragment position
	float2	uv		: TEXCOORD0;	// fragment uv coordinate
	float3  dist	: TEXCOORD1;	// distance to each edge of the triangle
};

// PARAMETERS //

//float4 _Texture_ST;			// For the Main Tex UV transform
float _Thickness = 1;		// Thickness of the wireframe line rendering
float4 _Color = {1,1,1,1};	// Color of the line
float4 _MainTex_ST;			// For the Main Tex UV transform
sampler2D _MainTex;			// Texture used for the line

// SHADER PROGRAMS //
// Vertex Shader
WireframeV2G WireframeVertex(appdata_base v)
{
	WireframeV2G output;
	output.pos =  UnityObjectToClipPos(v.vertex);
	output.uv = TRANSFORM_TEX (v.texcoord, _MainTex);//v.texcoord;

	return output;
}

// Geometry Shader
[maxvertexcount(3)]
void WireframeGeometry(triangle WireframeV2G p[3], inout TriangleStream<WireframeG2F> triStream)
{
	//points in screen space
	float2 p0 = _ScreenParams.xy * p[0].pos.xy / p[0].pos.w;
	float2 p1 = _ScreenParams.xy * p[1].pos.xy / p[1].pos.w;
	float2 p2 = _ScreenParams.xy * p[2].pos.xy / p[2].pos.w;
	
	//edge vectors
	float2 v0 = p2 - p1;
	float2 v1 = p2 - p0;
	float2 v2 = p1 - p0;

	//area of the triangle
 	float area = abs(v1.x*v2.y - v1.y * v2.x);

	//values based on distance to the edges
	float dist0 = area / length(v0);
	float dist1 = area / length(v1);
	float dist2 = area / length(v2);
	
	WireframeG2F pIn;
	
	//add the first point
	pIn.pos = p[0].pos;
	pIn.uv = p[0].uv;
	pIn.dist = float3(dist0,0,0);
	triStream.Append(pIn);

	//add the second point
	pIn.pos =  p[1].pos;
	pIn.uv = p[1].uv;
	pIn.dist = float3(0,dist1,0);
	triStream.Append(pIn);
	
	//add the third point
	pIn.pos = p[2].pos;
	pIn.uv = p[2].uv;
	pIn.dist = float3(0,0,dist2);
	triStream.Append(pIn);
}

// Fragment Shader
float4 WireframeFragment(WireframeG2F input) : COLOR
{			
	//find the smallest distance
	float val = min( input.dist.x, min( input.dist.y, input.dist.z));
	
	//calculate power to 2 to thin the line
	val = exp2( -1/_Thickness * val * val );
		
	//blend between the lines and the negative space to give illusion of anti aliasing
	float4 targetColor = _Color * tex2D( _MainTex, input.uv);
	float4 transCol = _Color * tex2D( _MainTex, input.uv);
	transCol.a = 0;
	return val * targetColor + ( 1 - val ) * transCol;
}


#endif
