/*
 * Project Riftward - gemeinsames Beleuchtungsmodell der Graybox-Benchmarkszene
 * (T-023). Bewusst einfach gehaltene Darstellung: eine Sonne ohne Schatten,
 * vier lokale Lichter mit aktivem Schattenpass je Licht. Kein Spielinhalt.
 *
 * Vertrag: ausschliesslich offline uebersetzte Shader; diese Datei wird vom
 * gepinnten shaderc inkludiert und nie zur Laufzeit geladen.
 */

#include "bgfx_shader.sh"

/* Sonne: xyz = normierte Richtung, w = Intensitaet. */
uniform vec4 u_sunDirection;
uniform vec4 u_sunColor;

/* Lokale Lichter: xyz = Position Weltkoordinaten, w = Reichweite. */
uniform vec4 u_lightPosRadius[4];

/* Farbe mal Intensitaet der lokalen Lichter. */
uniform vec4 u_lightColorInner[4];

/* Schattenkarten (RGBA32F, gespeicherte Distanz zur Lichtposition). */
SAMPLER2D(s_shadowMap0, 6);
SAMPLER2D(s_shadowMap1, 7);
SAMPLER2D(s_shadowMap2, 8);
SAMPLER2D(s_shadowMap3, 9);

/* x = Kantenlaenge der quadratischen Schattenkarten in Texeln. */
uniform vec4 u_shadowParams;

const float rkAmbient = 0.34;
const float rkShadowBiasMeters = 0.12;

float rift_fetchShadow(int lightIndex, vec3 worldPos, vec3 shadowCoord)
{
	vec2 uv = shadowCoord.xy * vec2(0.5, 0.5) + vec2(0.5, 0.5);

	if (uv.x < 0.001 || uv.x > 0.999 || uv.y < 0.001 || uv.y > 0.999 || shadowCoord.z <= 0.0)
	{
		return 1.0;
	}

	ivec2 texel = ivec2(uv * vec2(u_shadowParams.x, u_shadowParams.x));
	float storedDistance = 0.0;

	if (0 == lightIndex)
	{
		storedDistance = texelFetch(s_shadowMap0, texel, 0).r;
	}
	else if (1 == lightIndex)
	{
		storedDistance = texelFetch(s_shadowMap1, texel, 0).r;
	}
	else if (2 == lightIndex)
	{
		storedDistance = texelFetch(s_shadowMap2, texel, 0).r;
	}
	else
	{
		storedDistance = texelFetch(s_shadowMap3, texel, 0).r;
	}

	float fragmentDistance = length(worldPos - u_lightPosRadius[lightIndex].xyz);
	return storedDistance >= fragmentDistance - rkShadowBiasMeters ? 1.0 : 0.35;
}

vec3 rift_shade(vec3 albedo, vec3 worldPos, vec3 worldNormal, vec3 shadowCoord0To3[4])
{
	float sunTerm = max(dot(worldNormal, normalize(-u_sunDirection.xyz)), 0.0);
	vec3 result = albedo * (vec3(rkAmbient, rkAmbient, rkAmbient) + u_sunColor.rgb * sunTerm * u_sunDirection.w);

	for (int light = 0; light < 4; ++light)
	{
		vec3 delta = u_lightPosRadius[light].xyz - worldPos;
		float distance = length(delta);
		float radius = u_lightPosRadius[light].w;

		if (distance >= radius || radius <= 0.0)
		{
			continue;
		}

		float falloff = 1.0 - distance / radius;
		float attenuation = falloff * falloff;
		float lambert = max(dot(worldNormal, delta / max(distance, 0.0001)), 0.0);

		if (lambert <= 0.0)
		{
			continue;
		}

		float shadow = rift_fetchShadow(light, worldPos, shadowCoord0To3[light]);
		result += albedo * u_lightColorInner[light].rgb * attenuation * lambert * shadow;
	}

	return result;
}
