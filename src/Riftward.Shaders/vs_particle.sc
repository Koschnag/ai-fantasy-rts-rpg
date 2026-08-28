$input a_position, a_texcoord0, i_data0, i_data1, i_data2
$output v_uv, v_color, v_shape
/*
 * Project Riftward - Partikel-Vertexshader der Graybox-Benchmarkszene
 * (T-023): Kamerabillboards aus einer statischen Quadgeometrie plus
 * instanzierten Partikeldaten (Position, Groesse, Drehung, Farbe).
 */

#include "bgfx_shader.sh"


/* Kamerabasis des aktuellen Views (aus der Viewmatrix abgeleitet, CPU-seitig). */
uniform vec4 u_camRight;
uniform vec4 u_camUp;

void main()
{
	float angle = i_data1.x;
	float cosA = cos(angle);
	float sinA = sin(angle);

	vec2 corner = a_position.xy;
	vec2 rotated = vec2(
		corner.x * cosA - corner.y * sinA,
		corner.x * sinA + corner.y * cosA );

	float size = i_data0.w;
	vec3 wpos = i_data0.xyz
		+ u_camRight.xyz * (rotated.x * size)
		+ u_camUp.xyz * (rotated.y * size);

	gl_Position = mul(u_viewProj, vec4(wpos, 1.0) );
	v_uv = a_texcoord0;
	v_color = i_data2;
	v_shape = i_data1.z;
}
