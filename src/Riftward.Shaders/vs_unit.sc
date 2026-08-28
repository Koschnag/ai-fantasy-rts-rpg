$input a_position, a_normal, a_indices, a_weight, i_data0, i_data1, i_data2
$output v_wpos, v_normal, v_color, v_shadowCoord0, v_shadowCoord1, v_shadowCoord2, v_shadowCoord3
/*
 * Project Riftward - Einheiten-Vertexshader der Graybox-Benchmarkszene
 * (T-023). Repraesentativer Skinningpfad: 48 Bones je Einheit, Palette als
 * RGBA32F-Textur (3 Texel = Spalten 0..2 der Knochenmatrix), instanzierte
 * Welttransformation und deterministische Pose aus der Tickzeit.
 */

#include "bgfx_shader.sh"
#include "lighting.sh"


SAMPLER2D(s_bonePalette, 5);

uniform mat4 u_lightViewProj0;
uniform mat4 u_lightViewProj1;
uniform mat4 u_lightViewProj2;
uniform mat4 u_lightViewProj3;

mat4 rift_boneMatrix(int boneIndex, int row)
{
	ivec2 texel = ivec2(boneIndex * 3, row);
	vec4 column0 = texelFetch(s_bonePalette, ivec2(texel.x + 0, texel.y), 0);
	vec4 column1 = texelFetch(s_bonePalette, ivec2(texel.x + 1, texel.y), 0);
	vec4 column2 = texelFetch(s_bonePalette, ivec2(texel.x + 2, texel.y), 0);

	return mat4(
		vec4(column0.xyz, 0.0),
		vec4(column1.xyz, 0.0),
		vec4(column2.xyz, 0.0),
		vec4(column0.w, column1.w, column2.w, 1.0) );
}

void main()
{
	int row = int(i_data1.z + 0.5);

	mat4 skin =
		  a_weight.x * rift_boneMatrix(int(a_indices.x + 0.5), row)
		+ a_weight.y * rift_boneMatrix(int(a_indices.y + 0.5), row)
		+ a_weight.z * rift_boneMatrix(int(a_indices.z + 0.5), row)
		+ a_weight.w * rift_boneMatrix(int(a_indices.w + 0.5), row);

	vec3 localPos = mul(skin, vec4(a_position.xyz, 1.0) ).xyz;
	vec3 localNormal = normalize(mul(skin, vec4(a_normal.rgb * 2.0 - 1.0, 0.0) ).xyz);

	float yaw = i_data0.w;
	float cosYaw = cos(yaw);
	float sinYaw = sin(yaw);
	mat3 rotY = mat3(
		vec3(cosYaw, 0.0, -sinYaw),
		vec3(0.0, 1.0, 0.0),
		vec3(sinYaw, 0.0, cosYaw) );

	float scale = i_data1.y;
	vec3 wpos = i_data0.xyz + rotY * (localPos * scale);
	vec3 wnormal = rotY * localNormal;

	gl_Position = mul(u_viewProj, vec4(wpos, 1.0) );

	v_wpos = wpos;
	v_normal = wnormal;
	v_color = i_data2;

	vec4 clip0 = mul(u_lightViewProj0, vec4(wpos, 1.0) );
	vec4 clip1 = mul(u_lightViewProj1, vec4(wpos, 1.0) );
	vec4 clip2 = mul(u_lightViewProj2, vec4(wpos, 1.0) );
	vec4 clip3 = mul(u_lightViewProj3, vec4(wpos, 1.0) );

	v_shadowCoord0 = clip0.xyz / max(clip0.w, 0.0001);
	v_shadowCoord1 = clip1.xyz / max(clip1.w, 0.0001);
	v_shadowCoord2 = clip2.xyz / max(clip2.w, 0.0001);
	v_shadowCoord3 = clip3.xyz / max(clip3.w, 0.0001);
}
