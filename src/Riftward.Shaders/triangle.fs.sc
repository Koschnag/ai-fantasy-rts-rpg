$input v_color0

/*
 * T-010 technisches Testdreieck (kein Shipping-Asset).
 * Offline mit bgfx-shaderc fuer OpenGL 3.3 Core kompiliert (-p 130).
 */

#include <bgfx_shader.sh>

void main()
{
	gl_FragColor = v_color0;
}
