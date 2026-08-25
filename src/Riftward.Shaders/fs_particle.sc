$input v_uv, v_color
/*
 * Project Riftward - Partikel-Fragmentshader der Graybox-Benchmarkszene
 * (T-023): weiche runde Sprites mit deterministischer Alphakurve.
 */


void main()
{
	vec2 centered = v_uv * 2.0 - 1.0;
	float radiusSquared = dot(centered, centered);

	if (radiusSquared > 1.0)
	{
		discard;
	}

	float falloff = 1.0 - radiusSquared;
	gl_FragColor = vec4(v_color.rgb, v_color.a * falloff);
}
