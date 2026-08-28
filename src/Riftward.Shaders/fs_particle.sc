$input v_uv, v_color, v_shape
/*
 * Project Riftward - Partikel-Fragmentshader der Graybox-Benchmarkszene
 * (T-023/T-034): weiche Rundpartikel sowie drehbare, klar begrenzte
 * Glyphenquadrate. Letztere werden mit pi/4 als Diamanten dargestellt.
 */


void main()
{
	vec2 centered = v_uv * 2.0 - 1.0;
	float radiusSquared = dot(centered, centered);
	float squareDistance = max(abs(centered.x), abs(centered.y));
	float isGlyph = step(0.5, v_shape);
	float shapeDistance = mix(radiusSquared, squareDistance, isGlyph);

	if (shapeDistance > 1.0)
	{
		discard;
	}

	float roundFalloff = 1.0 - radiusSquared;
	float glyphFalloff = smoothstep(0.0, 0.08, 1.0 - squareDistance);
	float falloff = mix(roundFalloff, glyphFalloff, isGlyph);
	gl_FragColor = vec4(v_color.rgb, v_color.a * falloff);
}
