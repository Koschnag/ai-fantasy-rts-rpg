$input a_position, a_indices, a_weight, i_data0, i_data1, i_data2
$output v_wpos
/*
 * Project Riftward - Schattenpass (instanziert, geskinnte Einheiten) der
 * Graybox-Benchmarkszene (T-023). Dieselbe 48-Bone-Palette wie der
 * Hauptdurchgang; die Schattengeometrie folgt der animierten Pose.
 */

#include "bgfx_shader.sh"


SAMPLER2D(s_bonePalette, 5);

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
		vec4(0.0, 0.0, 0.0, 1.0) );
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

	float yaw = i_data0.w;
	float cosYaw = cos(yaw);
	float sinYaw = sin(yaw);
	mat3 rotY = mat3(
		vec3(cosYaw, 0.0, -sinYaw),
		vec3(0.0, 1.0, 0.0),
		vec3(sinYaw, 0.0, cosYaw) );

	vec3 wpos = i_data0.xyz + rotY * (localPos * i_data1.y);
	v_wpos = wpos;

	gl_Position = mul(u_viewProj, vec4(wpos, 1.0) );
}
