$input a_position
$output v_wpos
/*
 * Project Riftward - Schattenpass (statisch, Terrain) der
 * Graybox-Benchmarkszene (T-023). Speichert die Distanz zur Lichtposition.
 */

#include "bgfx_shader.sh"


void main()
{
	vec3 wpos = a_position.xyz;
	v_wpos = wpos;
	gl_Position = mul(u_viewProj, vec4(wpos, 1.0) );
}
