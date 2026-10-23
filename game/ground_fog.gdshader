// The battlefield's ground, darkened where the local player's side can't see (fog of war). `fog` has FogRes texels
// per world unit across the map, white where seen; Main fills it every tick. Past the map's edge it's darker still.
//
// Painted per pixel (the user, 2026-10-20: "higher definition battlefields"): dry earth everywhere, with broad patches
// paler and darker; scrub grass where the mesh says it's grassy (COLOR.b, game/Terrain.cs Grassy) with blades in it;
// dark damp earth by the ponds (COLOR.g); bare banded rock on steep slopes and high ground; gravel and pebbles; and the
// hollows shaded (COLOR.r, Terrain.Hollow). Small bumps tilt the light. All from world-space noise, so it never tiles.
shader_type spatial;
render_mode specular_schlick_ggx;

uniform sampler2D fog : filter_linear, repeat_disable;
uniform vec2 half_size = vec2(60.0, 40.0);

varying vec3 world;
varying vec3 world_normal;

// A random 0..1 for a whole-number lattice point (integer maths: neighbouring cells agree exactly on shared corners).
float hash(vec2 p) {
	uvec2 q = uvec2(ivec2(floor(p + 0.5)));
	q = q * uvec2(1597334673u, 3812015801u);
	uint n = (q.x ^ q.y) * 1597334673u;
	n ^= n >> 16u;
	return float(n) * (1.0 / 4294967295.0);
}

// Noise from a seamless fractal noise texture made once at load (Main: FastNoiseLite, 512 px = 16 units of p), so a
// pixel reads it instead of hashing dozens of lattice points (the user, 2026-10-23: "why is the performance so bad?":
// the ground's per-pixel noise was most of a full-screen frame).
uniform sampler2D noise_tex : filter_linear_mipmap, repeat_enable;

float noise(vec2 p) {
	return texture(noise_tex, p * 0.0625 + vec2(0.37, 0.71)).r;
}

float fbm(vec2 p) {
	return 0.47 + (texture(noise_tex, p * 0.0625).r - 0.5) * 0.7;   // (the texture is stretched to 0..1: narrowed to the old spread)
}


// Cell edges (Voronoi: the gap between the nearest and second-nearest random point): 0 on an edge. Dried mud cracks.
float cell_edge(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	float d1 = 8.0, d2 = 8.0;
	for (int y = -1; y <= 1; y++)
		for (int x = -1; x <= 1; x++) {
			vec2 o = vec2(float(x), float(y));
			vec2 pt = o + vec2(hash(i + o), hash(i + o + vec2(37.0, 11.0)));
			float d = length(pt - f);
			if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) { d2 = d; }
		}
	return d2 - d1;
}
// The fine relief the light catches: grains, clods, cracks.
float bumps(vec2 p) {
	return noise(p * 2.2) * 0.5 + noise(p * 6.5) * 0.3 + noise(p * 17.0) * 0.2;
}

uniform sampler2D dirt_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D mud_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D gravel_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D scorched_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;

// A soil texture as a multiplier around 1 (its mean divided out, half its colour kept): `size` metres a tile, read
// twice at different turns and scales and blended by noise so the repeat doesn't show.
vec3 soil(sampler2D t, vec2 p, float size, vec3 mean, float contrast) {
	vec2 a = p / size;
	vec2 b = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8)) * p / (size * 1.37) + 0.31;
	vec3 c = mix(texture(t, a).rgb, texture(t, b).rgb, smoothstep(0.35, 0.65, noise(p * 0.08 + size)));
	vec3 r = c / mean;
	r = mix(vec3(dot(r, vec3(0.333))), r, 0.5);
	return mix(vec3(1.0), r, contrast);
}

float lum(vec3 c) { return dot(c, vec3(0.3, 0.5, 0.2)); }

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec2 uv = (world.xz + half_size) / (half_size * 2.0);
	bool inside = uv.x >= 0.0 && uv.y >= 0.0 && uv.x <= 1.0 && uv.y <= 1.0;
	float seen = inside ? texture(fog, uv).r : 0.0;
	vec2 p = world.xz;
	float hollow = COLOR.r, wet = COLOR.g, grassy = COLOR.b;
	float slope = 1.0 - clamp(world_normal.y, 0.0, 1.0);   // 0 flat, more as it steepens

	// Dry earth, its tone drifting over tens of metres; the grain from the painted soil textures.
	vec3 earth_light = vec3(0.30, 0.25, 0.18), earth_dark = vec3(0.17, 0.14, 0.10);
	float broad = fbm(p * 0.035);
	vec3 col = mix(earth_dark, earth_light, smoothstep(0.3, 0.7, broad));
	col *= 0.88 + 0.24 * noise(p * 0.9);
	// Where each soil lies: cracked earth, gravel and scorch in patches over the dirt (not in the grass).
	float cracked = smoothstep(0.52, 0.68, fbm(p * 0.05 + 40.0)) * (1.0 - wet);
	float stony = smoothstep(0.58, 0.72, fbm(p * 0.07 + 90.0));
	float burnt = smoothstep(0.66, 0.78, fbm(p * 0.04 + 7.0));
	vec3 dirt = soil(dirt_tex, p, 1.5, vec3(0.178, 0.106, 0.062), 0.55);
	vec3 grain = dirt;
	grain = mix(grain, soil(mud_tex, p, 4.0, vec3(0.119, 0.065, 0.038), 0.5) * 0.9, cracked);
	vec3 stones = soil(gravel_tex, p, 1.6, vec3(0.148, 0.117, 0.097), 0.5);
	col = mix(col * grain, col * vec3(0.95, 0.97, 1.0) * stones, stony * 0.8);
	col = mix(col, vec3(0.075, 0.065, 0.055) * soil(scorched_tex, p, 5.0, vec3(0.066, 0.053, 0.043), 0.6), burnt * 0.85);

	// Scrub: olive grass in the grassy places, broken up at its edges, with streaks of blades.
	float grass_edge = smoothstep(0.15, 0.6, grassy + (fbm(p * 0.6) - 0.5) * 0.5);
	vec3 grass_a = vec3(0.10, 0.13, 0.05), grass_b = vec3(0.17, 0.18, 0.08);
	float blades = noise(vec2(p.x * 14.0, p.y * 3.5)) * 0.5 + noise(vec2(p.x * 4.0, p.y * 16.0)) * 0.5;
	vec3 grass = mix(grass_a, grass_b, blades) * (0.8 + 0.4 * noise(p * 1.7));
	col = mix(col, grass, grass_edge * (1.0 - slope * 2.0));

	// Damp earth by the ponds: darker, a little glossy.
	col = mix(col, col * 0.55 + vec3(0.0, 0.01, 0.01), wet * 0.85);

	// Rock on the steep slopes and the heights: grey, in bands that follow the height, cracked.
	float rocky = smoothstep(0.18, 0.4, slope + (noise(p * 0.5) - 0.5) * 0.15);   // (by steepness only: hilltops stay earth and grass)
	float strata = 0.5 + 0.5 * sin(world.y * 7.0 + noise(p * 0.8) * 4.0);
	vec3 rock = mix(vec3(0.24, 0.23, 0.21), vec3(0.38, 0.36, 0.33), strata) * (0.8 + 0.4 * noise(p * 3.0));
	float crack = smoothstep(0.04, 0.0, abs(noise(p * 1.4) - 0.5));
	rock *= 1.0 - crack * 0.5;
	col = mix(col, rock, clamp(rocky, 0.0, 1.0));

	// Hollows darker, ridges a touch lighter.
	col *= mix(1.15, 0.6, hollow);

	ALBEDO = col * mix(0.32, 1.0, seen) * (inside ? 1.0 : 0.65);
	ROUGHNESS = mix(0.95, 0.55, wet);
	SPECULAR = mix(0.25, 0.5, wet);

	// The light catches the small relief (world-space tilt, carried into view space: the mesh has no tangents).
	// Off the soil itself (lighter reads as higher): stones and clods stand up from the ground. The height's slope
	// comes from the screen-space derivatives (one height per pixel, not three), bent into the normal.
	float strength = 0.10 + rocky * 0.12 + stony * 0.1;
	float hgt = (bumps(p) * 0.3 + lum(texture(dirt_tex, p / 1.5).rgb) * 0.6) * strength;
	vec3 dpdx = dFdx(VERTEX), dpdy = dFdy(VERTEX);
	vec3 r1 = cross(dpdy, NORMAL), r2 = cross(NORMAL, dpdx);
	float det = dot(dpdx, r1);
	vec3 grad = sign(det) * (dFdx(hgt) * r1 + dFdy(hgt) * r2);
	NORMAL = normalize(abs(det) * NORMAL - grad);
}
