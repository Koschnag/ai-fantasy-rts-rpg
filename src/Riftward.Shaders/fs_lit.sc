$input v_wpos, v_normal, v_color, v_shadowCoord0, v_shadowCoord1, v_shadowCoord2, v_shadowCoord3
/*
 * Project Riftward - gemeinsamer Beleuchtungs-Fragmentshader der
 * Graybox-Benchmarkszene (T-023) fuer Terrain und Einheiten.
 */

#include "lighting.sh"


void main()
{
	vec3 shadowCoords[4];
	shadowCoords[0] = v_shadowCoord0;
	shadowCoords[1] = v_shadowCoord1;
	shadowCoords[2] = v_shadowCoord2;
	shadowCoords[3] = v_shadowCoord3;

	vec3 color = rift_shade(v_color.rgb, v_wpos, normalize(v_normal), shadowCoords);
	gl_FragColor = vec4(color, v_color.a);
}
