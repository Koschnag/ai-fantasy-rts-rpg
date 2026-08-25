$input a_position, a_normal, a_color0
$output v_wpos, v_normal, v_color, v_shadowCoord0, v_shadowCoord1, v_shadowCoord2, v_shadowCoord3
/*
 * Project Riftward - Terrain-Vertexshader der Graybox-Benchmarkszene (T-023).
 * Rechnet die vier Licht-Schattenkoordinaten im Vertexshader, damit das
 * Fragmentprogramm unter der GL-3.3-Minimumgrenze fuer Fragmentuniforms bleibt.
 */

#include "bgfx_shader.sh"
#include "lighting.sh"


uniform mat4 u_lightViewProj0;
uniform mat4 u_lightViewProj1;
uniform mat4 u_lightViewProj2;
uniform mat4 u_lightViewProj3;

void main()
{
	vec3 wpos = a_position.xyz;
	gl_Position = mul(u_viewProj, vec4(wpos, 1.0) );

	vec4 wnormal = vec4(a_normal.rgb * 2.0 - 1.0, 0.0);

	v_wpos = wpos;
	v_normal = wnormal.rgb;
	v_color = a_color0;

	vec4 clip0 = mul(u_lightViewProj0, vec4(wpos, 1.0) );
	vec4 clip1 = mul(u_lightViewProj1, vec4(wpos, 1.0) );
	vec4 clip2 = mul(u_lightViewProj2, vec4(wpos, 1.0) );
	vec4 clip3 = mul(u_lightViewProj3, vec4(wpos, 1.0) );

	v_shadowCoord0 = clip0.xyz / max(clip0.w, 0.0001);
	v_shadowCoord1 = clip1.xyz / max(clip1.w, 0.0001);
	v_shadowCoord2 = clip2.xyz / max(clip2.w, 0.0001);
	v_shadowCoord3 = clip3.xyz / max(clip3.w, 0.0001);
}
