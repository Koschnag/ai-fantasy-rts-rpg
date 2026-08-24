$input a_position, a_color0
$output v_color0

/*
 * T-020 BENCH-EMPTY technisches Testdreieck in Weltkoordinaten (kein
 * Shipping-Asset). Gegenueber dem T-010-Clip-Space-Dreieck wird hier die von
 * bgfx bereitgestellte View-/Projektionsmatrix angewandt, damit das feste
 * Kameraflugskript die Darstellung tatsaechlich beeinflusst.
 * Offline mit bgfx-shaderc fuer OpenGL 3.3 Core kompiliert (-p 130).
 */

#include <bgfx_shader.sh>

void main()
{
	gl_Position = mul(u_viewProj, vec4(a_position, 1.0));
	v_color0 = a_color0;
}
