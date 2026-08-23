$input a_position, a_color0
$output v_color0

/*
 * T-010 technisches Testdreieck (kein Shipping-Asset).
 * Offline mit bgfx-shaderc fuer OpenGL 3.3 Core kompiliert (-p 130).
 */

#include <bgfx_shader.sh>

void main()
{
	gl_Position = vec4(a_position, 1.0);
	v_color0 = a_color0;
}
